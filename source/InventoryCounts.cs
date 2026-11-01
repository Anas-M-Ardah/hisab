using System.Data;
using static Hisab.Store;
namespace Hisab;
public sealed partial class AccountingService
{
    public long StartStockCount(long location,IReadOnlyList<long> items)
    {
        store.Require("post");RequireLocation(location);EnsureOpen(DateTime.Today);
        if(items.Count==0||items.Distinct().Count()!=items.Count||items.Any(id=>!Items().Any(i=>i.Id==id&&i.Stock)))InventoryError("Choose distinct stock items to count");
        using var tx=store.BeginTransaction();
        foreach(long item in items)if(Convert.ToInt64(Scalar("SELECT count(*) FROM stock_counts c JOIN stock_count_lines l ON l.count_id=c.id WHERE c.location=@p0 AND l.item=@p1 AND c.state='Open'",location,item))>0)InventoryError("An overlapping count is already open");
        Exec("INSERT INTO stock_counts(location,date,start_doc) VALUES(@p0,@p1,(SELECT coalesce(max(id),0) FROM documents))",location,DateTime.Today.ToString("yyyy-MM-dd"));long id=Convert.ToInt64(Scalar("SELECT last_insert_rowid()"));
        foreach(long item in items){var it=Items().Single(i=>i.Id==item);Exec("INSERT INTO stock_count_lines(count_id,item,expected,gain_cost,expected_serials) VALUES(@p0,@p1,@p2,@p3,@p4)",id,item,store.LocationQuantity(item,location),it.Qty>0?M(D(it.Value)/D(it.Qty)):0,string.Join("\n",store.AvailableSerials(item,location)));}
        Audit("count-start",id.ToString());tx.Commit();return id;
    }
    DataRow OpenCount(long id){var t=Table("SELECT * FROM stock_counts WHERE id=@p0 AND state='Open'",id);if(t.Rows.Count!=1)InventoryError("Choose an open stock count");return t.Rows[0];}
    public void RecordStockCount(long count,long item,decimal quantity,string serials,decimal gainCost)
    {
        store.Require("post");OpenCount(count);if(quantity<0||D(M(quantity))!=quantity||gainCost<0||D(M(gainCost))!=gainCost)InventoryError("Use nonnegative quantities and costs with at most 3 decimals");
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM stock_count_lines WHERE count_id=@p0 AND item=@p1",count,item))!=1)InventoryError("Item is not in this count");
        var numbers=SerialList(serials);if(store.IsSerialized(item)&&(quantity!=numbers.Length||numbers.Select(NormalizeStockCode).Distinct().Count()!=numbers.Length))InventoryError("Count one distinct serial per unit");
        using var tx=store.BeginTransaction();Exec("UPDATE stock_count_lines SET actual=@p0,serials=@p1,gain_cost=@p2 WHERE count_id=@p3 AND item=@p4",M(quantity),serials,M(gainCost),count,item);Audit("count-entry",count+":"+item);tx.Commit();
    }
    public void CancelStockCount(long id){store.Require("post");OpenCount(id);using var tx=store.BeginTransaction();Exec("UPDATE stock_counts SET state='Cancelled' WHERE id=@p0",id);Audit("count-cancel",id.ToString());tx.Commit();}
    public long CompleteStockCount(long count,string reason)
    {
        store.Require("post");var c=OpenCount(count);if(string.IsNullOrWhiteSpace(reason))InventoryError("Enter the count adjustment reason");
        var lines=Table("SELECT * FROM stock_count_lines WHERE count_id=@p0",count);if(lines.Rows.Cast<DataRow>().Any(r=>r["actual"]==DBNull.Value))InventoryError("Enter a count for every item, including zero");
        using var tx=store.BeginTransaction();long id=NewDoc("IA",DateTime.Today,null,null,reason,new(0,0,0));long location=(long)c["location"];
        Exec("UPDATE stock_counts SET state='Posted',date=@p0,posted_doc=@p1,reason=@p2 WHERE id=@p3",DateTime.Today.ToString("yyyy-MM-dd"),id,reason,count);
        foreach(DataRow l in lines.Rows){long item=(long)l["item"],delta=(long)l["actual"]-(long)l["expected"];
            if(delta>0&&(long)l["gain_cost"]<=0)InventoryError("Review a positive cost for stock gains");
            Exec("INSERT INTO inventory_adjustments(doc,item,location,qty,gain_cost) VALUES(@p0,@p1,@p2,@p3,@p4)",id,item,location,delta,l["gain_cost"]);
            if(store.IsSerialized(item)){var expected=store.AvailableSerials(item,location);var actual=SerialList((string)l["serials"]);var lost=expected.Where(x=>!actual.Any(y=>NormalizeStockCode(x)==NormalizeStockCode(y))).ToArray();var gained=actual.Where(x=>!expected.Any(y=>NormalizeStockCode(x)==NormalizeStockCode(y))).ToArray();RecordSerials(id,null,item,location,lost.Length*1000,string.Join("\n",lost),-1);RecordSerials(id,null,item,location,gained.Length*1000,string.Join("\n",gained),1,true);}
        }
        RebuildInventory("stock count "+count);Audit("count-post",count.ToString());tx.Commit();return id;
    }
    void ReplayCount(DataRow doc,Dictionary<long,StockState> states)
    {
        long id=(long)doc["id"],total=0;Exec("DELETE FROM stock_moves WHERE doc=@p0",id);
        foreach(DataRow l in Table("SELECT * FROM inventory_adjustments WHERE doc=@p0",id).Rows){long item=(long)l["item"],q=(long)l["qty"];var st=states[item];long value=q>=0?M(D(q)*D((long)l["gain_cost"])):(-q==st.Qty?-st.Value:-M(D(st.Value)*(-q)/st.Qty));st.Qty+=q;st.Value+=value;if(st.Qty<0)InventoryError("Count adjustment would make historical stock negative");if(st.Qty>0)st.LastCost=D(st.Value)/D(st.Qty);total+=value;Exec("INSERT INTO stock_moves(doc,item,qty,value,location) VALUES(@p0,@p1,@p2,@p3,@p4)",id,item,q,value,l["location"]);}
        Exec("DELETE FROM entries WHERE doc=@p0",id);if(total>0)Journal(id,[new(AccountId("1301"),total,0),new(AccountId("5195"),0,total)]);if(total<0)Journal(id,[new(AccountId("5195"),-total,0),new(AccountId("1301"),0,-total)]);Exec("UPDATE documents SET net=@p0,total=@p0 WHERE id=@p1",Math.Abs(total),id);
    }
}
