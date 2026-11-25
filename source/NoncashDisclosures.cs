using System.Data;
namespace Hisab;
public record NoncashDisclosure(long Document,string Number,string Date,long DebitAccount,long CreditAccount,string Category,long Amount,string Note,bool Stale);
public sealed partial class AccountingService
{
    public static readonly string[] NoncashCategories=["AssetAcquisition","DebtToEquity","OtherNoncash"];
    public DataTable NoncashDocuments()=>Table("SELECT d.id,d.number,d.date FROM documents d WHERE d.kind!='RV' AND EXISTS(SELECT 1 FROM entries e JOIN accounts a ON a.id=e.account WHERE e.doc=d.id AND a.kind IN ('Asset','Liability','Equity','Supplier') AND e.debit>0) AND EXISTS(SELECT 1 FROM entries e JOIN accounts a ON a.id=e.account WHERE e.doc=d.id AND a.kind IN ('Asset','Liability','Equity','Supplier') AND e.credit>0) ORDER BY d.date DESC,d.id DESC");
    public DataTable NoncashAccounts(long document,bool debit)=>Table("SELECT a.id,a.code,a.name,a.kind,sum(e."+(debit?"debit":"credit")+") amount FROM entries e JOIN accounts a ON a.id=e.account WHERE e.doc=@p0 AND a.kind IN ('Asset','Liability','Equity','Supplier') GROUP BY a.id HAVING amount>0",document);
    public void SaveNoncashDisclosure(long document,long debitAccount,long creditAccount,string category,decimal amount,string note)
    {
        store.Require("master");if(!NoncashCategories.Contains(category)||amount<0||Store.D(Store.M(amount))!=amount||string.IsNullOrWhiteSpace(note))throw new InvalidOperationException("Choose category, valid amount and explanation / اختر التصنيف والمبلغ والتفسير");
        var doc=Table("SELECT kind FROM documents WHERE id=@p0",document);if(doc.Rows.Count==0||doc.Rows[0]["kind"].ToString()=="RV")throw new InvalidOperationException("Classify the original document / صنف المستند الأصلي");
        using var tx=store.BeginTransaction();
        if(amount==0){Exec("DELETE FROM noncash_disclosures WHERE doc=@p0 AND debit_account=@p1 AND credit_account=@p2 AND category=@p3",document,debitAccount,creditAccount,category);Audit("noncash-disclosure-removed",document+" / "+note);tx.Commit();return;}
        var debit=NoncashAccounts(document,true).Rows.Cast<DataRow>().SingleOrDefault(r=>(long)r["id"]==debitAccount);var credit=NoncashAccounts(document,false).Rows.Cast<DataRow>().SingleOrDefault(r=>(long)r["id"]==creditAccount);
        if(debit==null||credit==null||debitAccount==creditAccount)throw new InvalidOperationException("Choose noncash journal accounts / اختر حسابات غير نقدية من القيد");
        if(category=="AssetAcquisition"&&(debit["kind"].ToString()!="Asset"||credit["kind"].ToString() is not("Liability" or "Equity" or "Supplier"))||category=="DebtToEquity"&&(debit["kind"].ToString() is not("Liability" or "Supplier")||credit["kind"].ToString()!="Equity"))throw new InvalidOperationException("Category does not match journal accounts / التصنيف لا يناسب حسابات القيد");
        long value=Store.M(amount);var existing=Table("SELECT * FROM noncash_disclosures WHERE doc=@p0 AND NOT(debit_account=@p1 AND credit_account=@p2 AND category=@p3)",document,debitAccount,creditAccount,category).Rows.Cast<DataRow>();
        if(existing.Where(r=>(long)r["debit_account"]==debitAccount).Sum(r=>(long)r["amount"])+value>(long)debit["amount"]||existing.Where(r=>(long)r["credit_account"]==creditAccount).Sum(r=>(long)r["amount"])+value>(long)credit["amount"])throw new InvalidOperationException("Disclosure exceeds available journal amount / الإفصاح يتجاوز مبلغ القيد المتاح");
        Exec("INSERT INTO noncash_disclosures(doc,debit_account,credit_account,category,amount,note,debit_basis,credit_basis) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7) ON CONFLICT(doc,debit_account,credit_account,category) DO UPDATE SET amount=excluded.amount,note=excluded.note,debit_basis=excluded.debit_basis,credit_basis=excluded.credit_basis",document,debitAccount,creditAccount,category,value,note.Trim(),debit["amount"],credit["amount"]);Audit("noncash-disclosure",document+" / "+category+" / "+value+" / "+note);tx.Commit();
    }
    public List<NoncashDisclosure> NoncashDisclosures(DateTime from,DateTime to)
    {
        if(from.Date>to.Date)throw new InvalidOperationException("Invalid reporting range / نطاق التقرير غير صالح");var result=new List<NoncashDisclosure>();
        foreach(DataRow r in Table("SELECT n.*,d.id displayed_doc,d.number,d.date,1 sign FROM noncash_disclosures n JOIN documents d ON d.id=n.doc WHERE d.date BETWEEN @p0 AND @p1 UNION ALL SELECT n.*,d.id displayed_doc,d.number,d.date,-1 sign FROM noncash_disclosures n JOIN documents d ON d.original=n.doc AND d.kind='RV' WHERE d.date BETWEEN @p0 AND @p1 ORDER BY date,displayed_doc",from.ToString("yyyy-MM-dd"),to.ToString("yyyy-MM-dd")).Rows){
            long doc=(long)r["doc"],debit=(long)r["debit_account"],credit=(long)r["credit_account"];
            bool stale=Convert.ToInt64(Scalar("SELECT coalesce(sum(debit),0) FROM entries WHERE doc=@p0 AND account=@p1",doc,debit))!=(long)r["debit_basis"]||Convert.ToInt64(Scalar("SELECT coalesce(sum(credit),0) FROM entries WHERE doc=@p0 AND account=@p1",doc,credit))!=(long)r["credit_basis"];
            result.Add(new((long)r["displayed_doc"],(string)r["number"],(string)r["date"],debit,credit,(string)r["category"],(long)r["amount"]*(long)r["sign"],(string)r["note"],stale));
        }return result;
    }
}
