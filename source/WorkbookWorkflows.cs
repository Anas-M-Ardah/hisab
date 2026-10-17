using System.Data;
using static Hisab.Store;
namespace Hisab;

public record VoucherAllocation(long Account,decimal Amount,string Description);
public record StockTransferLine(long Item,decimal Quantity,decimal Cost);
public record AccountSummary(long Account,string Name,long Debit,long Credit,long Balance,bool Direct=false);

public sealed partial class Store
{
    // Disjoint families: totals never count a parent and its children twice.
    public List<AccountSummary> HierarchyTrial(DateTime from,DateTime to,long? parent,bool arabic)
    {
        if(from.Date>to.Date)throw new InvalidOperationException("Invalid report period");
        var accounts=Accounts(arabic);var movements=Table("SELECT e.account,sum(CASE WHEN d.date>=@p0 THEN e.debit ELSE 0 END) debit,sum(CASE WHEN d.date>=@p0 THEN e.credit ELSE 0 END) credit,sum(e.debit-e.credit) balance FROM entries e JOIN documents d ON d.id=e.doc WHERE d.date<=@p1 GROUP BY e.account",from.ToString("yyyy-MM-dd"),to.ToString("yyyy-MM-dd")).Rows.Cast<DataRow>().ToDictionary(r=>(long)r["account"]);
        AccountSummary Sum(Account a,IEnumerable<long> family,bool direct=false){var rows=family.Where(movements.ContainsKey).Select(id=>movements[id]).ToList();return new(a.Id,a.Name,rows.Sum(r=>(long)r["debit"]),rows.Sum(r=>(long)r["credit"]),rows.Sum(r=>(long)r["balance"]),direct);}
        var result=accounts.Where(a=>a.ParentId==parent).Select(a=>Sum(a,AccountFamily(a.Id))).ToList();
        if(parent!=null){var a=accounts.Single(a=>a.Id==parent);var own=Sum(a,[a.Id],true);if(own.Debit!=0||own.Credit!=0||own.Balance!=0)result.Insert(0,own);}
        return result;
    }
}

public sealed partial class AccountingService
{
    public long SplitVoucher(bool receipt,DateTime date,long cash,string note,IReadOnlyList<VoucherAllocation> allocations)
    {
        if(allocations.Count==0||allocations.Any(l=>l.Amount<=0||D(M(l.Amount))!=l.Amount||string.IsNullOrWhiteSpace(l.Description)))throw new InvalidOperationException("أدخل الحساب والمبلغ والبيان لكل سطر / Enter an account, positive amount and description for each line");
        var accounts=Accounts().ToDictionary(a=>a.Id);
        if(!accounts.TryGetValue(cash,out var bank)||bank.Kind!="Cash"||allocations.Any(l=>l.Account==cash||!accounts.ContainsKey(l.Account)||l.Account==AccountId("1301")))throw new InvalidOperationException("تحقق من الحسابات؛ للمخزون استخدم تحويل الطلبية / Check accounts; use order transfer for stock");
        long total=allocations.Sum(l=>M(l.Amount));using var tx=store.BeginTransaction();
        long id=NewDoc(receipt?"RC":"PV",date,null,cash,note,new(total,0,total));
        var lines=allocations.Select(l=>new EntryLine(l.Account,receipt?0:M(l.Amount),receipt?M(l.Amount):0,l.Description.Trim())).ToList();
        lines.Add(new(cash,receipt?total:0,receipt?0:total,note));Journal(id,lines);tx.Commit();return id;
    }
    public long AmendSplitVoucher(long original,DateTime date,long cash,string note,IReadOnlyList<VoucherAllocation> lines,string reason)
    {
        using var tx=store.BeginTransaction();var source=Table("SELECT * FROM documents WHERE id=@p0",original).Rows[0];
        if(source["party"]!=DBNull.Value||source["kind"].ToString() is not("RC" or "PV"))throw new InvalidOperationException("Choose a split voucher");
        Reverse(original,reason,DateTime.Parse((string)source["date"]));long replacement=SplitVoucher(source["kind"].ToString()=="RC",date,cash,note,lines);
        Exec("UPDATE documents SET replaced_by=@p0 WHERE id=@p1",replacement,original);Exec("INSERT INTO revisions(doc,replacement,reason,snapshot,username) VALUES(@p0,@p1,@p2,@p3,@p4)",original,replacement,reason,System.Text.Json.JsonSerializer.Serialize(source.Table.Columns.Cast<DataColumn>().ToDictionary(c=>c.ColumnName,c=>source[c]==DBNull.Value?null:source[c])),store.User.Username);tx.Commit();return replacement;
    }
    public long TransferOrderToStock(DateTime date,long order,string note,IReadOnlyList<StockTransferLine> lines)
    {
        var account=Accounts().Single(a=>a.Id==order);
        if(account.Kind!="Asset"||account.ParentId==null||account.System||Accounts().Any(a=>a.ParentId==order)||string.IsNullOrWhiteSpace(note))throw new InvalidOperationException("اختر حساب طلبية فرعيًا من نوع أصول واكتب البيان / Choose a non-system leaf asset account for the order and enter a description");
        if(lines.Count==0||lines.Select(l=>l.Item).Distinct().Count()!=lines.Count||lines.Any(l=>l.Quantity<=0||l.Cost<=0||D(M(l.Quantity))!=l.Quantity||D(M(l.Cost))!=l.Cost||!Items().Any(i=>i.Id==l.Item&&i.Stock)))throw new InvalidOperationException("أدخل أصنافًا مخزنية مختلفة وكميات وتكاليف موجبة / Enter distinct stock items with positive quantities and total costs");
        long total=lines.Sum(l=>M(l.Cost));long available=Convert.ToInt64(Scalar("SELECT coalesce(sum(e.debit-e.credit),0) FROM entries e JOIN documents d ON d.id=e.doc WHERE e.account=@p0 AND d.date<=@p1",order,date.ToString("yyyy-MM-dd")));
        if(total>available||total>store.Balance(order))throw new InvalidOperationException("تكلفة التحويل تتجاوز رصيد الطلبية / Transfer cost exceeds the order balance");
        using var tx=store.BeginTransaction();long id=NewDoc("OT",date,order,null,note,new(total,0,total));
        foreach(var line in lines)Exec("INSERT INTO stock_moves(doc,item,qty,value) VALUES(@p0,@p1,@p2,@p3)",id,line.Item,M(line.Quantity),M(line.Cost));
        Journal(id,[new(AccountId("1301"),total,0,note),new(order,0,total,note)]);RebuildInventory("order transfer "+id);tx.Commit();return id;
    }
}
