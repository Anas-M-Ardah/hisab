using System.Data;
namespace Hisab;
public record CashMovement(long Document,long Account,int Direction,string Date,string Number,long Amount,long Operating,long Investing,long Financing,long Transfer,long Unclassified,bool Stale);
public sealed partial class AccountingService
{
    // Classification keys survive inventory replay, which replaces entry IDs.
    public List<CashMovement> CashMovements(DateTime from,DateTime to)
    {
        if(from.Date>to.Date)throw new InvalidOperationException("Invalid reporting range / نطاق التقرير غير صالح");
        var result=new List<CashMovement>();
        var movements=Table("SELECT d.id,d.date,d.number,d.kind,d.original,e.account,CASE WHEN e.debit>0 THEN 1 ELSE -1 END direction,sum(e.debit+e.credit) amount FROM entries e JOIN documents d ON d.id=e.doc JOIN accounts a ON a.id=e.account WHERE a.kind='Cash' AND d.date BETWEEN @p0 AND @p1 AND e.debit+e.credit>0 GROUP BY d.id,e.account,CASE WHEN e.debit>0 THEN 1 ELSE -1 END ORDER BY d.date,d.id,e.account,direction",from.ToString("yyyy-MM-dd"),to.ToString("yyyy-MM-dd"));
        foreach(DataRow r in movements.Rows){
            long doc=(long)r["id"],account=(long)r["account"],amount=(long)r["amount"];int direction=Convert.ToInt32(r["direction"]);
            bool pureTransfer=Convert.ToInt64(Scalar("SELECT count(*) FROM entries e JOIN accounts a ON a.id=e.account WHERE e.doc=@p0 AND a.kind!='Cash' AND e.debit+e.credit>0",doc))==0&&Convert.ToInt64(Scalar("SELECT coalesce(sum(debit-credit),0) FROM entries WHERE doc=@p0",doc))==0;
            long operating=0,investing=0,financing=0,transfer=pureTransfer?amount:0;bool stale=false;
            if(!pureTransfer){
                var saved=Table("SELECT * FROM cash_flow_classifications WHERE doc=@p0 AND account=@p1 AND direction=@p2",doc,account,direction);
                if(saved.Rows.Count==0&&r["kind"].ToString()=="RV"&&r["original"]!=DBNull.Value)saved=Table("SELECT * FROM cash_flow_classifications WHERE doc=@p0 AND account=@p1 AND direction=@p2",r["original"],account,-direction);
                if(saved.Rows.Count>0){var c=saved.Rows[0];stale=(long)c["basis"]!=amount;if(!stale){operating=(long)c["operating"];investing=(long)c["investing"];financing=(long)c["financing"];transfer=(long)c["transfer"];}}
            }
            result.Add(new(doc,account,direction,(string)r["date"],(string)r["number"],amount,operating,investing,financing,transfer,amount-operating-investing-financing-transfer,stale));
        }
        return result;
    }
    public void ClassifyCashMovement(long document,long account,int direction,decimal operating,decimal investing,decimal financing,decimal transfer)
    {
        store.Require("master");string? date=Scalar("SELECT date FROM documents WHERE id=@p0",document) as string;if(date==null)throw new InvalidOperationException("Unknown document / مستند غير معروف");
        using var tx=store.BeginTransaction();var movement=CashMovements(DateTime.Parse(date),DateTime.Parse(date)).SingleOrDefault(m=>m.Document==document&&m.Account==account&&m.Direction==direction)??throw new InvalidOperationException("Unknown cash movement / حركة نقدية غير معروفة");
        var amounts=new[]{operating,investing,financing,transfer};if(amounts.Any(v=>v<0||Store.D(Store.M(v))!=v)||amounts.Sum()>Store.D(movement.Amount))throw new InvalidOperationException("Use positive amounts up to the cash movement, with three decimals / أدخل مبالغ موجبة حتى قيمة الحركة بثلاث منازل عشرية");
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM entries e JOIN accounts a ON a.id=e.account WHERE e.doc=@p0 AND a.kind!='Cash' AND e.debit+e.credit>0",document))==0)throw new InvalidOperationException("Cash-only transfers are automatic / التحويلات بين الحسابات النقدية تصنف تلقائياً");
        Exec("INSERT INTO cash_flow_classifications(doc,account,direction,basis,operating,investing,financing,transfer) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7) ON CONFLICT(doc,account,direction) DO UPDATE SET basis=excluded.basis,operating=excluded.operating,investing=excluded.investing,financing=excluded.financing,transfer=excluded.transfer",document,account,direction,movement.Amount,Store.M(operating),Store.M(investing),Store.M(financing),Store.M(transfer));
        Audit("cash-flow-classification",document+" / "+account+" / "+direction+": "+string.Join(",",amounts));tx.Commit();
    }
    public FinancialStatement CashFlowStatement(DateTime from,DateTime to)
    {
        if(from.Date>to.Date)throw new InvalidOperationException("Invalid reporting range / نطاق التقرير غير صالح");
        bool complete=true;
        Dictionary<string,long> Period(DateTime start,DateTime end){
            var moves=CashMovements(start,end);var values=new Dictionary<string,long>();
            var cash=Accounts().Where(a=>a.Kind=="Cash").Select(a=>a.Id).ToHashSet();
            var opening=LedgerBalances(start.AddDays(-1));var closing=LedgerBalances(end);
            values["OpeningCash"]=opening.Where(p=>cash.Contains(p.Key)).Sum(p=>p.Value);values["ClosingCash"]=closing.Where(p=>cash.Contains(p.Key)).Sum(p=>p.Value);
            foreach(var (key,select) in new (string,Func<CashMovement,long>)[]{("OperatingCash",m=>m.Operating),("InvestingCash",m=>m.Investing),("FinancingCash",m=>m.Financing),("TransferCash",m=>m.Transfer),("UnclassifiedCash",m=>m.Unclassified)}){
                values[key+"In"]=moves.Where(m=>m.Direction==1).Sum(select);values[key+"Out"]=-moves.Where(m=>m.Direction==-1).Sum(select);values[key]=values[key+"In"]+values[key+"Out"];
            }
            // Transfers must cancel within each document, not merely across the period.
            if(moves.Any(m=>m.Unclassified!=0||m.Stale)||moves.GroupBy(m=>m.Document).Any(g=>g.Sum(m=>m.Direction*m.Transfer)!=0))complete=false;
            values["NetCashChange"]=moves.Sum(m=>m.Direction*m.Amount);values["CashDifference"]=values["ClosingCash"]-values["OpeningCash"]-values["NetCashChange"];
            if(values["CashDifference"]!=0)complete=false;return values;
        }
        var current=Period(from,to);var prior=Period(from.AddYears(-1),to.AddYears(-1));
        string[] keys=["OpeningCash","OperatingCashIn","OperatingCashOut","OperatingCash","InvestingCashIn","InvestingCashOut","InvestingCash","FinancingCashIn","FinancingCashOut","FinancingCash","TransferCashIn","TransferCashOut","TransferCash","UnclassifiedCashIn","UnclassifiedCashOut","UnclassifiedCash","NetCashChange","ClosingCash","CashDifference"];
        return new(keys.Select(k=>new StatementRow(k,null,current[k],prior[k],true)).ToList(),complete,current["CashDifference"]);
    }
}
