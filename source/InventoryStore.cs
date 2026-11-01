using System.Data;
namespace Hisab;

public record StockLocation(long Id,string Name,bool Active) { public override string ToString()=>Name; }
public sealed partial class Store
{
    void UpgradeInventory()
    {
        void Column(string table,string name,string type){if(!Table("PRAGMA table_info("+table+")").Rows.Cast<DataRow>().Any(r=>r["name"].ToString()==name))Exec($"ALTER TABLE {table} ADD COLUMN {name} {type}");}
        Exec("CREATE TABLE IF NOT EXISTS locations(id INTEGER PRIMARY KEY,name TEXT NOT NULL UNIQUE COLLATE NOCASE,active INTEGER NOT NULL DEFAULT 1); INSERT OR IGNORE INTO locations(id,name) VALUES(1,'المخزون الرئيسي / Main stock');");
        Column("items","barcode","TEXT NOT NULL DEFAULT ''");Column("items","serialized","INTEGER NOT NULL DEFAULT 0");Column("items","serial_start","INTEGER NOT NULL DEFAULT 0");
        Column("invoice_lines","location","INTEGER NOT NULL DEFAULT 1 REFERENCES locations(id)");Column("stock_moves","location","INTEGER NOT NULL DEFAULT 1 REFERENCES locations(id)");
        Exec("""
        CREATE UNIQUE INDEX IF NOT EXISTS ix_item_barcode ON items(barcode) WHERE barcode<>'';
        CREATE TABLE IF NOT EXISTS serial_units(id INTEGER PRIMARY KEY,item INTEGER NOT NULL REFERENCES items(id),number TEXT NOT NULL,normalized TEXT NOT NULL,supplier_warranty TEXT,customer_warranty TEXT,UNIQUE(item,normalized));
        CREATE TABLE IF NOT EXISTS serial_events(id INTEGER PRIMARY KEY,unit INTEGER NOT NULL REFERENCES serial_units(id),doc INTEGER NOT NULL REFERENCES documents(id),line INTEGER REFERENCES invoice_lines(id),location INTEGER NOT NULL REFERENCES locations(id),direction INTEGER NOT NULL CHECK(direction IN (-1,1)));
        CREATE INDEX IF NOT EXISTS ix_serial_doc ON serial_events(doc);
        CREATE TABLE IF NOT EXISTS stock_counts(id INTEGER PRIMARY KEY,location INTEGER NOT NULL REFERENCES locations(id),date TEXT NOT NULL,start_doc INTEGER NOT NULL,state TEXT NOT NULL DEFAULT 'Open',posted_doc INTEGER REFERENCES documents(id),reason TEXT NOT NULL DEFAULT '');
        CREATE TABLE IF NOT EXISTS stock_count_lines(count_id INTEGER NOT NULL REFERENCES stock_counts(id),item INTEGER NOT NULL REFERENCES items(id),expected INTEGER NOT NULL,actual INTEGER,serials TEXT NOT NULL DEFAULT '',gain_cost INTEGER NOT NULL DEFAULT 0,PRIMARY KEY(count_id,item));
        CREATE TABLE IF NOT EXISTS inventory_adjustments(doc INTEGER NOT NULL REFERENCES documents(id),item INTEGER NOT NULL REFERENCES items(id),location INTEGER NOT NULL REFERENCES locations(id),qty INTEGER NOT NULL,gain_cost INTEGER NOT NULL,PRIMARY KEY(doc,item,location));
        CREATE TABLE IF NOT EXISTS purchase_orders(id INTEGER PRIMARY KEY,supplier INTEGER NOT NULL REFERENCES accounts(id),location INTEGER NOT NULL REFERENCES locations(id),expected TEXT NOT NULL,state TEXT NOT NULL DEFAULT 'Draft');
        CREATE TABLE IF NOT EXISTS purchase_order_lines(id INTEGER PRIMARY KEY,order_id INTEGER NOT NULL REFERENCES purchase_orders(id),item INTEGER NOT NULL REFERENCES items(id),qty INTEGER NOT NULL CHECK(qty>0),price INTEGER NOT NULL CHECK(price>=0));
        CREATE TABLE IF NOT EXISTS receipt_links(line INTEGER PRIMARY KEY REFERENCES invoice_lines(id),order_line INTEGER NOT NULL REFERENCES purchase_order_lines(id));
        CREATE TABLE IF NOT EXISTS receipt_bills(line INTEGER PRIMARY KEY REFERENCES invoice_lines(id),receipt_line INTEGER NOT NULL REFERENCES invoice_lines(id));
        CREATE TABLE IF NOT EXISTS landed_allocations(doc INTEGER NOT NULL REFERENCES documents(id),receipt_line INTEGER NOT NULL REFERENCES invoice_lines(id),amount INTEGER NOT NULL CHECK(amount>=0),PRIMARY KEY(doc,receipt_line));
        CREATE TABLE IF NOT EXISTS landed_sources(doc INTEGER PRIMARY KEY REFERENCES documents(id),source INTEGER UNIQUE REFERENCES documents(id));
        CREATE TABLE IF NOT EXISTS reorder_rules(item INTEGER NOT NULL REFERENCES items(id),location INTEGER NOT NULL REFERENCES locations(id),minimum INTEGER NOT NULL,target INTEGER NOT NULL,PRIMARY KEY(item,location));
        INSERT OR IGNORE INTO accounts(code,name,name_en,kind,system) VALUES('2195','بضاعة مستلمة غير مفوترة','Goods received not invoiced','Liability',1),('5195','فروقات الجرد','Inventory count differences','Expense',1),('2196','تسوية تكاليف الشراء','Acquisition cost clearing','Liability',1);
        PRAGMA user_version=4;
        """);
        Column("receipt_bills","base_value","INTEGER NOT NULL DEFAULT 0");
        Column("stock_count_lines","expected_serials","TEXT NOT NULL DEFAULT ''");
    }
    public List<StockLocation> Locations(bool activeOnly=true)=>Table("SELECT * FROM locations WHERE @p0=0 OR active=1 ORDER BY id",activeOnly?1:0).Rows.Cast<DataRow>().Select(r=>new StockLocation((long)r["id"],(string)r["name"],(long)r["active"]==1)).ToList();
    public long SaveLocation(long? id,string name,bool active=true)
    {
        Require("master");name=name.Trim();if(name.Length==0||Locations(false).Any(l=>l.Id!=id&&l.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("اسم موقع فريد مطلوب / Enter a unique location name");
        if(id==1&&!active)throw new InvalidOperationException("الموقع الرئيسي مطلوب / Main stock must remain active");
        if(id!=null&&!Locations(false).Any(l=>l.Id==id))throw new InvalidOperationException("Unknown location");
        if(!active&&(Items().Any(i=>LocationQuantity(i.Id,id!.Value)!=0)||Convert.ToInt64(Scalar("SELECT (SELECT count(*) FROM stock_counts WHERE location=@p0 AND state='Open')+(SELECT count(*) FROM purchase_orders WHERE location=@p0 AND state NOT IN ('Cancelled','Received'))",id))>0))throw new InvalidOperationException("سوّ المخزون والعمليات المفتوحة أولًا / Resolve stock and open operations first");
        using var tx=BeginTransaction();if(id==null){Exec("INSERT INTO locations(name,active) VALUES(@p0,@p1)",name,active?1:0);id=Convert.ToInt64(Scalar("SELECT last_insert_rowid()"));}else Exec("UPDATE locations SET name=@p0,active=@p1 WHERE id=@p2",name,active?1:0,id);Audit("location",id+": "+name);tx.Commit();return id.Value;
    }
    public long LocationQuantity(long item,long location)=>Convert.ToInt64(Scalar("SELECT coalesce(sum(m.qty),0) FROM stock_moves m JOIN documents d ON d.id=m.doc WHERE m.item=@p0 AND m.location=@p1 AND d.reversed=0 AND d.kind<>'RV'",item,location));
    public bool IsSerialized(long item)=>Convert.ToInt64(Scalar("SELECT serialized FROM items WHERE id=@p0",item))==1;
    public static string NormalizeStockCode(string code)=>code.Trim().ToUpperInvariant();
    public List<string> AvailableSerials(long item,long location)=>Table("SELECT u.number FROM serial_units u JOIN serial_events e ON e.unit=u.id JOIN documents d ON d.id=e.doc WHERE u.item=@p0 AND e.location=@p1 AND d.reversed=0 GROUP BY u.id HAVING sum(e.direction)=1 ORDER BY u.number",item,location).Rows.Cast<DataRow>().Select(r=>(string)r[0]).ToList();
    public DataTable SerialHistory(long item)=>Table("SELECT u.number,d.number document,d.date,d.kind,e.location,e.direction,d.reversed,u.supplier_warranty,u.customer_warranty FROM serial_units u JOIN serial_events e ON e.unit=u.id JOIN documents d ON d.id=e.doc WHERE u.item=@p0 ORDER BY d.date,d.id,e.id",item);
    public void SetSerialWarranty(long unit,DateTime? supplier,DateTime? customer){Require("master");using var tx=BeginTransaction();if(Convert.ToInt64(Scalar("SELECT count(*) FROM serial_units WHERE id=@p0",unit))!=1)throw new InvalidOperationException("Unknown serial");Exec("UPDATE serial_units SET supplier_warranty=@p0,customer_warranty=@p1 WHERE id=@p2",supplier?.ToString("yyyy-MM-dd"),customer?.ToString("yyyy-MM-dd"),unit);Audit("serial-warranty",unit.ToString());tx.Commit();}
}

