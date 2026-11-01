using System.Data;
using static Hisab.Store;
namespace Hisab;
public record CostAllocation(long ReceiptLine,decimal Amount);
public sealed partial class AccountingService
{
    public IReadOnlyList<CostAllocation> AllocateLandedCost(decimal amount,IReadOnlyList<long> receiptLines,bool byQuantity)
    {
        if(amount<=0||D(M(amount))!=amount||receiptLines.Count==0||receiptLines.Distinct().Count()!=receiptLines.Count)InventoryError("Choose receipts and a positive charge");
        var weights=receiptLines.Select(id=>Convert.ToInt64(Scalar("SELECT "+(byQuantity?"qty":"net")+" FROM invoice_lines WHERE id=@p0",id))).ToArray();decimal total=weights.Sum();if(total<=0)InventoryError("Allocation basis is zero; enter manual amounts");long left=M(amount);var result=new List<CostAllocation>();for(int i=0;i<weights.Length;i++){long value=i==weights.Length-1?left:Math.Min(left,M(amount*weights[i]/total));left-=value;result.Add(new(receiptLines[i],D(value)));}return result;
    }
    public long AddLandedCost(DateTime date,long supplier,long? cash,decimal amount,IReadOnlyList<CostAllocation> allocations,string reason,long? sourceDocument=null)
    {
        if(amount<=0||D(M(amount))!=amount||allocations.Count==0||allocations.Any(a=>a.Amount<0||D(M(a.Amount))!=a.Amount)||allocations.Sum(a=>a.Amount)!=amount||allocations.Select(a=>a.ReceiptLine).Distinct().Count()!=allocations.Count||string.IsNullOrWhiteSpace(reason))InventoryError("Allocations must exactly match a positive charge; enter a reason");
        if(!Accounts().Any(a=>a.Id==supplier&&a.Kind=="Supplier")||(cash!=null&&!Accounts().Any(a=>a.Id==cash&&a.Kind=="Cash")))InventoryError("Choose a supplier and valid payment account");
        using var tx=store.BeginTransaction();
        foreach(var a in allocations){var t=Table("SELECT d.date FROM invoice_lines l JOIN documents d ON d.id=l.doc WHERE l.id=@p0 AND d.kind IN ('GR','PI') AND d.reversed=0 AND NOT EXISTS(SELECT 1 FROM receipt_bills b WHERE b.line=l.id) AND EXISTS(SELECT 1 FROM items i WHERE i.id=l.item AND i.stock=1)",a.ReceiptLine);if(t.Rows.Count!=1)InventoryError("Choose active stock receipt lines");var received=DateTime.Parse((string)t.Rows[0][0]);EnsureOpen(received);if(date.Date<received)InventoryError("Charge cannot precede receipt");}
        long id=NewDoc("LC",date,supplier,cash,reason,new(M(amount),0,M(amount)));
        if(sourceDocument!=null){var src=Table("SELECT * FROM documents WHERE id=@p0 AND reversed=0 AND kind IN ('PV','JV')",sourceDocument);long debit=Convert.ToInt64(Scalar("SELECT coalesce(sum(debit-credit),0) FROM entries WHERE doc=@p0 AND account=@p1",sourceDocument,AccountId("2196")));if(src.Rows.Count!=1||debit!=M(amount)||Convert.ToInt64(Scalar("SELECT count(*) FROM landed_sources WHERE source=@p0",sourceDocument))>0)InventoryError("Source must be an unused acquisition-clearing payment/journal for the exact charge");Exec("INSERT INTO landed_sources(doc,source) VALUES(@p0,@p1)",id,sourceDocument);}
        else Journal(id,[new(AccountId("2196"),M(amount),0),new(cash??supplier,0,M(amount))]);
        foreach(var a in allocations)Exec("INSERT INTO landed_allocations(doc,receipt_line,amount) VALUES(@p0,@p1,@p2)",id,a.ReceiptLine,M(a.Amount));RebuildInventory("landed cost "+id);Audit("landed-cost",id+": "+reason);tx.Commit();return id;
    }
}
