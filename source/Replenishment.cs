using System.Data;
using static Hisab.Store;
namespace Hisab;
public record ReorderSuggestion(long Item,long Location,long OnHand,long OnOrder,long Minimum,long Target,long Suggested,string Expected,bool Overdue);
public sealed partial class AccountingService
{
    public void SetReorderRule(long item,long location,decimal minimum,decimal target)
    {
        store.Require("master");RequireLocation(location);if(!Items().Any(i=>i.Id==item&&i.Stock)||minimum<0||target<minimum||D(M(minimum))!=minimum||D(M(target))!=target||(store.IsSerialized(item)&&(minimum!=decimal.Truncate(minimum)||target!=decimal.Truncate(target))))InventoryError("Target must be at least minimum, with valid stock quantities");using var tx=store.BeginTransaction();Exec("INSERT INTO reorder_rules(item,location,minimum,target) VALUES(@p0,@p1,@p2,@p3) ON CONFLICT(item,location) DO UPDATE SET minimum=excluded.minimum,target=excluded.target",item,location,M(minimum),M(target));Audit("reorder-rule",item+":"+location);tx.Commit();
    }
    public List<ReorderSuggestion> ReorderSuggestions()
    {
        var result=new List<ReorderSuggestion>();foreach(DataRow r in Table("SELECT r.* FROM reorder_rules r JOIN locations l ON l.id=r.location WHERE l.active=1").Rows){long item=(long)r["item"],location=(long)r["location"],onHand=store.LocationQuantity(item,location),onOrder=0;var dates=new List<string>();foreach(DataRow l in Table("SELECT l.id,l.qty,o.expected FROM purchase_order_lines l JOIN purchase_orders o ON o.id=l.order_id WHERE l.item=@p0 AND o.location=@p1 AND o.state IN ('Issued','Partial','Received')",item,location).Rows){long remaining=Math.Max(0,(long)l["qty"]-ReceivedQuantity((long)l["id"]));if(remaining>0){onOrder+=remaining;dates.Add((string)l["expected"]);}}long projected=onHand-store.ReservedQuantity(item,location)+onOrder;result.Add(new(item,location,onHand,onOrder,(long)r["minimum"],(long)r["target"],projected<(long)r["minimum"]?Math.Max(0,(long)r["target"]-projected):0,dates.Count>0?dates.Min()!:"",dates.Any(d=>string.CompareOrdinal(d,DateTime.Today.ToString("yyyy-MM-dd"))<0)));}return result;
    }
}
