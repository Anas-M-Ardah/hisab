using System.Data;
using static Hisab.Store;
namespace Hisab;

public sealed partial class AccountingService
{
    static void InventoryError(string text)=>throw new InvalidOperationException("تحقق من المخزون / "+text);
    internal static string[] SerialList(string text)=>text.Split(new[]{'\r','\n',',',';'},StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
    void RequireLocation(long location){if(!store.Locations().Any(l=>l.Id==location))InventoryError("Choose an active location");}
    public void ConfigureInventoryItem(long item,string barcode,bool serialized,IReadOnlyList<string> existingSerials)
    {
        store.Require("master");var it=Items().Single(i=>i.Id==item);barcode=NormalizeStockCode(barcode);
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM items WHERE barcode=@p0 AND id<>@p1 AND barcode<>''",barcode,item))>0)InventoryError("Barcode already belongs to another item");
        using var tx=store.BeginTransaction();
        if(store.IsSerialized(item)&&!serialized)InventoryError("Serial tracking cannot be disabled after registration");
        if(serialized&&!store.IsSerialized(item)){
            if(!it.Stock||it.Qty<0||it.Qty%1000!=0||existingSerials.Count!=it.Qty/1000)InventoryError("Register one serial for every existing whole unit");
            if(store.Locations(false).Any(l=>l.Id!=1&&store.LocationQuantity(item,l.Id)!=0))InventoryError("Move existing stock to Main stock before initial serial registration");
            long doc=NewDoc("SN",DateTime.Today,null,null,"Serial registration",new(0,0,0));
            Exec("UPDATE items SET serialized=1,serial_start=@p0 WHERE id=@p1",doc,item);
            RecordSerials(doc,null,item,1,it.Qty,string.Join("\n",existingSerials),1,true);
        }
        Exec("UPDATE items SET barcode=@p0 WHERE id=@p1",barcode,item);Audit("inventory-item",item.ToString());ValidateInventory();tx.Commit();
    }
    void RecordSerials(long doc,long? line,long item,long location,long quantity,string serials,int direction,bool newUnits=false,long? sourceLine=null)
    {
        RequireLocation(location);if(!store.IsSerialized(item)){if(SerialList(serials).Length>0)InventoryError("Enable serial tracking for this item first");return;}
        var numbers=SerialList(serials);if(quantity%1000!=0||numbers.Length!=quantity/1000||numbers.Select(NormalizeStockCode).Distinct().Count()!=numbers.Length)InventoryError("Enter one distinct serial per whole unit");
        bool legacyIncoming=direction==1&&sourceLine!=null&&Convert.ToInt64(Scalar("SELECT doc FROM invoice_lines WHERE id=@p0",sourceLine))<Convert.ToInt64(Scalar("SELECT serial_start FROM items WHERE id=@p0",item));
        foreach(string number in numbers){string key=NormalizeStockCode(number);object? found=Scalar("SELECT id FROM serial_units WHERE item=@p0 AND normalized=@p1",item,key);long unit;
            if(found==null){if(!newUnits&&sourceLine!=null&&!legacyIncoming)InventoryError("Returned serial was not on the original document");if(direction<0)InventoryError("Unknown serial: "+number);Exec("INSERT INTO serial_units(item,number,normalized) VALUES(@p0,@p1,@p2)",item,number,key);unit=Convert.ToInt64(Scalar("SELECT last_insert_rowid()"));}
            else unit=Convert.ToInt64(found);
            if(sourceLine!=null&&Convert.ToInt64(Scalar("SELECT count(*) FROM serial_events WHERE line=@p0 AND unit=@p1",sourceLine,unit))==0){long sourceDoc=Convert.ToInt64(Scalar("SELECT doc FROM invoice_lines WHERE id=@p0",sourceLine));long start=Convert.ToInt64(Scalar("SELECT serial_start FROM items WHERE id=@p0",item));if(sourceDoc>=start)InventoryError("Serial does not belong to the original line");Audit("legacy-return-serial",doc+": "+number);}
            Exec("INSERT INTO serial_events(unit,doc,line,location,direction) VALUES(@p0,@p1,@p2,@p3,@p4)",unit,doc,line,location,direction);
        }
    }
    public long TransferInventory(DateTime date,long from,long to,long item,decimal quantity,string serials,string reason)
    {
        RequireLocation(from);RequireLocation(to);long q=M(quantity);if(from==to||q<=0||D(q)!=quantity||string.IsNullOrWhiteSpace(reason)||!Items().Any(i=>i.Id==item&&i.Stock))InventoryError("Choose different locations, a stock item, positive quantity and reason");
        using var tx=store.BeginTransaction();long id=NewDoc("IT",date,null,null,reason,new(0,0,0));
        Exec("INSERT INTO stock_moves(doc,item,qty,value,location) VALUES(@p0,@p1,@p2,0,@p3),(@p0,@p1,@p4,0,@p5)",id,item,-q,from,q,to);
        RecordSerials(id,null,item,from,q,serials,-1);RecordSerials(id,null,item,to,q,serials,1);RebuildInventory("transfer "+id);tx.Commit();return id;
    }
    void ValidateInventory()
    {
        var quantities=new Dictionary<(long,long),long>();
        foreach(DataRow m in Table("SELECT m.*,d.date,d.kind FROM stock_moves m JOIN documents d ON d.id=m.doc WHERE d.reversed=0 AND d.kind<>'RV' ORDER BY d.date,d.id,m.id").Rows){
            long item=(long)m["item"],location=(long)m["location"],doc=(long)m["doc"];var key=(item,location);quantities[key]=quantities.GetValueOrDefault(key)+(long)m["qty"];
            if(quantities[key]<0&&(store.IsSerialized(item)||location!=1||store.Setting("allow_negative","0")!="1"))InventoryError("Insufficient stock at location in date order");
            if((long)m["qty"]!=0&&Convert.ToInt64(Scalar("SELECT count(*) FROM stock_counts c JOIN stock_count_lines l ON l.count_id=c.id WHERE l.item=@p0 AND c.location=@p1 AND ((c.state='Open' AND @p2>c.start_doc) OR (c.state='Posted' AND @p2>c.posted_doc AND @p3<c.date))",item,location,doc,m["date"]))>0)InventoryError("Stock count locks this item/location; complete, cancel or reverse the count first");
        }
        var positions=new Dictionary<long,long>();
        foreach(DataRow e in Table("SELECT e.* FROM serial_events e JOIN documents d ON d.id=e.doc WHERE d.reversed=0 ORDER BY d.date,d.id,e.id").Rows){long unit=(long)e["unit"],location=(long)e["location"];if((long)e["direction"]==1){if(positions.ContainsKey(unit))InventoryError("Serial is already in stock");positions[unit]=location;}else{if(positions.GetValueOrDefault(unit)!=location)InventoryError("Serial is not available at the selected location");positions.Remove(unit);}}
        foreach(var item in Items().Where(i=>store.IsSerialized(i.Id))){
            foreach(var loc in store.Locations(false)){long count=positions.Count(p=>p.Value==loc.Id&&Convert.ToInt64(Scalar("SELECT item FROM serial_units WHERE id=@p0",p.Key))==item.Id);if(count*1000!=quantities.GetValueOrDefault((item.Id,loc.Id)))InventoryError("Serials and stock quantity differ; register/select all units");}
        }
    }
    void CheckInventoryReversal(long id)
    {
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM stock_counts WHERE state='Open'"))>0&&Convert.ToInt64(Scalar("SELECT count(*) FROM stock_moves m JOIN stock_counts c ON c.location=m.location JOIN stock_count_lines l ON l.count_id=c.id AND l.item=m.item WHERE m.doc=@p0 AND c.state='Open'",id))>0)InventoryError("Cancel the active stock count before reversing stock movements");
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM stock_moves m JOIN stock_counts c ON c.location=m.location JOIN stock_count_lines l ON l.count_id=c.id AND l.item=m.item JOIN documents d ON d.id=m.doc WHERE m.doc=@p0 AND c.state='Posted' AND c.posted_doc<>@p0 AND (d.date<c.date OR (d.date=c.date AND d.id<c.posted_doc))",id))>0)InventoryError("Reverse the later stock count first");
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM documents WHERE id=@p0 AND kind='SN'",id))>0)InventoryError("Serial registration is permanent");
    }
}

