using System.Data;
using static Hisab.Store;
namespace Hisab;

public sealed partial class Store
{
    void ValidateAccountParent(long? id,string kind,long? parentId)
    {
        var accounts=Accounts().ToDictionary(a=>a.Id);
        if(id!=null&&accounts.Values.Any(a=>a.ParentId==id&&a.Kind!=kind))
            throw new InvalidOperationException("نوع الحساب يجب أن يطابق حساباته الفرعية / Account type must match its children");
        var seen=new HashSet<long>();
        while(parentId!=null)
        {
            if(parentId==id||!seen.Add(parentId.Value))throw new InvalidOperationException("لا يمكن إنشاء حلقة في الحسابات / Account hierarchy cannot contain a cycle");
            if(!accounts.TryGetValue(parentId.Value,out var parent)||parent.Kind!=kind)
                throw new InvalidOperationException("اختر حسابًا رئيسيًا من نفس النوع / Choose a parent of the same account type");
            parentId=parent.ParentId;
        }
    }
    public List<long> AccountFamily(long id)
    {
        var accounts=Accounts();var family=new HashSet<long>{id};var pending=new Queue<long>();pending.Enqueue(id);
        while(pending.Count>0){long parent=pending.Dequeue();foreach(var child in accounts.Where(a=>a.ParentId==parent))if(family.Add(child.Id))pending.Enqueue(child.Id);}
        return family.ToList();
    }
    public long GroupBalance(long id)=>AccountFamily(id).Sum(Balance);
    // Gross movement totals include reversal movements, so In minus Out equals current stock.
    public Dictionary<long,(long In,long Out)> StockFlows()=>Table("SELECT item,sum(CASE WHEN qty>0 THEN qty ELSE 0 END) incoming,sum(CASE WHEN qty<0 THEN -qty ELSE 0 END) outgoing FROM stock_moves GROUP BY item").Rows.Cast<DataRow>().ToDictionary(r=>(long)r["item"],r=>((long)r["incoming"],(long)r["outgoing"]));
}

public record ReceiptCheque(string Number,string Bank,decimal Amount,DateTime Due);
public sealed partial class AccountingService
{
    public List<long> ReceiveCheques(DateTime date,long party,decimal total,string note,IReadOnlyList<ReceiptCheque> cheques,long? invoice=null)
    {
        store.Require("post");
        if(cheques.Count==0||total<=0||D(M(total))!=total||cheques.Sum(c=>c.Amount)!=total)
            throw new InvalidOperationException("مجموع الشيكات يجب أن يساوي مبلغ القبض / Cheque total must equal the receipt amount");
        var keys=new HashSet<(string,string)>();
        foreach(var c in cheques)
        {
            if(c.Amount<=0||D(M(c.Amount))!=c.Amount||string.IsNullOrWhiteSpace(c.Number)||string.IsNullOrWhiteSpace(c.Bank)||c.Due.Date<date.Date)
                throw new InvalidOperationException("أكمل رقم الشيك والبنك والمبلغ وتاريخ الاستحقاق / Complete cheque number, bank, amount and due date");
            if(!keys.Add((c.Number.Trim(),c.Bank.Trim()))||Convert.ToInt64(Scalar("SELECT count(*) FROM cheques WHERE number=@p0 AND bank=@p1 AND direction='Incoming'",c.Number.Trim(),c.Bank.Trim()))>0)
                throw new InvalidOperationException("رقم الشيك مكرر لنفس البنك / Duplicate cheque number for this bank");
        }
        using var tx=store.BeginTransaction();var documents=new List<long>();
        foreach(var c in cheques)
        {
            // Each cheque keeps its own journal so clearing or bouncing it never affects its siblings.
            long id=RegisterCheque(true,c.Number,c.Bank,party,c.Amount,date,c.Due);
            Exec("UPDATE documents SET note=@p0,due_date=@p1 WHERE id=@p2",c.Number.Trim()+" · "+c.Bank.Trim()+"\n"+note,c.Due.ToString("yyyy-MM-dd"),id);
            if(invoice!=null){long outstanding=Positions(DateTime.Today).Single(p=>p.Invoice==invoice).Outstanding;if(outstanding>0)AllocateInternal(id,invoice.Value,Math.Min(M(c.Amount),outstanding),date);}
            documents.Add(id);
        }
        Audit("cheque-receipt",string.Join(",",documents));tx.Commit();return documents;
    }
}
