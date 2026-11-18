using System.Data;
using static Hisab.Store;
namespace Hisab;
public sealed partial class Store
{
    void UpgradeWarranties()
    {
        using var migration=BeginTransaction();
        Exec("""
        CREATE TABLE IF NOT EXISTS warranties(id INTEGER PRIMARY KEY,sale_line INTEGER NOT NULL REFERENCES invoice_lines(id),source_doc INTEGER NOT NULL REFERENCES documents(id),unit INTEGER NOT NULL REFERENCES serial_units(id),expires TEXT,terms TEXT NOT NULL DEFAULT '',UNIQUE(source_doc,unit));
        CREATE TABLE IF NOT EXISTS warranty_claims(id INTEGER PRIMARY KEY,warranty INTEGER NOT NULL REFERENCES warranties(id),received TEXT NOT NULL,state TEXT NOT NULL DEFAULT 'Open',custody TEXT NOT NULL DEFAULT 'InStore',issue TEXT NOT NULL,condition TEXT NOT NULL,accessories TEXT NOT NULL,expires TEXT,terms TEXT NOT NULL,supplier INTEGER REFERENCES accounts(id),reference TEXT NOT NULL DEFAULT '',resolution TEXT NOT NULL DEFAULT '');
        CREATE UNIQUE INDEX IF NOT EXISTS ix_warranty_open ON warranty_claims(warranty) WHERE state='Open';
        CREATE TABLE IF NOT EXISTS warranty_events(id INTEGER PRIMARY KEY,claim INTEGER NOT NULL REFERENCES warranty_claims(id),created TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,username TEXT NOT NULL,state TEXT NOT NULL,custody TEXT NOT NULL,note TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS warranty_replacements(doc INTEGER PRIMARY KEY REFERENCES documents(id),claim INTEGER NOT NULL REFERENCES warranty_claims(id),item INTEGER NOT NULL REFERENCES items(id),location INTEGER NOT NULL REFERENCES locations(id),unit INTEGER NOT NULL REFERENCES serial_units(id),override_reason TEXT NOT NULL DEFAULT '');
        CREATE INDEX IF NOT EXISTS ix_warranty_unit ON warranties(unit);
        CREATE INDEX IF NOT EXISTS ix_warranty_claim ON warranty_claims(warranty);
        CREATE INDEX IF NOT EXISTS ix_warranty_replacement_claim ON warranty_replacements(claim);
        CREATE INDEX IF NOT EXISTS ix_warranty_event_claim ON warranty_events(claim);
        INSERT OR IGNORE INTO accounts(code,name,name_ar,name_en,kind,system) VALUES('5196','مصروف ضمان','مصروف ضمان','Warranty expense','Expense',1);
        """);migration.Commit();
    }
    public DataTable WarrantyDevices()=>Table("""
        SELECT e.unit id,e.doc source_doc,e.line sale_line,u.number serial,i.name item,a.name customer,d.number invoice,d.date sold,w.id warranty,CASE WHEN w.id IS NULL THEN u.customer_warranty ELSE w.expires END expires,coalesce(w.terms,'') terms
        FROM serial_events e JOIN documents d ON d.id=e.doc JOIN serial_units u ON u.id=e.unit JOIN items i ON i.id=u.item JOIN accounts a ON a.id=d.party LEFT JOIN warranties w ON w.source_doc=e.doc AND w.unit=e.unit
        WHERE d.kind='SI' AND d.reversed=0 AND e.direction=-1
        UNION ALL
        SELECT r.unit,r.doc,w.sale_line,u.number,i.name,a.name,d.number,d.date,nw.id,nw.expires,coalesce(nw.terms,'') FROM warranty_replacements r JOIN documents x ON x.id=r.doc JOIN warranty_claims c ON c.id=r.claim JOIN warranties w ON w.id=c.warranty JOIN invoice_lines l ON l.id=w.sale_line JOIN documents d ON d.id=l.doc JOIN serial_units u ON u.id=r.unit JOIN items i ON i.id=u.item JOIN accounts a ON a.id=d.party LEFT JOIN warranties nw ON nw.source_doc=r.doc AND nw.unit=r.unit WHERE x.reversed=0 AND d.reversed=0
        ORDER BY sold DESC,source_doc DESC
        """);
    public DataTable WarrantyClaims()=>Table("""
        SELECT c.*,w.unit,w.sale_line,w.source_doc,u.number serial,i.name item,a.name customer,d.number invoice,d.id sale_doc,
        coalesce((SELECT x.number FROM warranty_replacements r JOIN documents x ON x.id=r.doc WHERE r.claim=c.id AND x.reversed=0),'') exchange,
        coalesce((SELECT u2.number FROM warranty_replacements r JOIN documents x ON x.id=r.doc JOIN serial_units u2 ON u2.id=r.unit WHERE r.claim=c.id AND x.reversed=0),'') replacement
        FROM warranty_claims c JOIN warranties w ON w.id=c.warranty JOIN serial_units u ON u.id=w.unit JOIN items i ON i.id=u.item JOIN invoice_lines l ON l.id=w.sale_line JOIN documents d ON d.id=l.doc JOIN accounts a ON a.id=d.party ORDER BY c.id DESC
        """);
}
public sealed partial class AccountingService
{
    bool warrantyReversal;
    static void WarrantyError(string message)=>throw new InvalidOperationException("الضمان / "+message);
    DataRow Warranty(long id){var t=Table("SELECT * FROM warranties WHERE id=@p0",id);if(t.Rows.Count!=1)WarrantyError("Warranty not found");return t.Rows[0];}
    DataRow Claim(long id){var t=store.WarrantyClaims();var r=t.Rows.Cast<DataRow>().SingleOrDefault(r=>(long)r["id"]==id);if(r==null)WarrantyError("Claim not found");return r!;}
    void CheckWarrantyOwner(DataRow w)
    {
        long unit=(long)w["unit"],source=(long)w["source_doc"];
        long latest=Convert.ToInt64(Scalar("SELECT e.doc FROM serial_events e JOIN documents d ON d.id=e.doc WHERE e.unit=@p0 AND d.reversed=0 ORDER BY d.date DESC,d.id DESC,e.id DESC LIMIT 1",unit));
        if(latest!=source||Convert.ToInt64(Scalar("SELECT count(*) FROM invoice_lines l JOIN documents d ON d.id=l.doc WHERE l.id=@p0 AND d.kind='SI' AND d.reversed=0",w["sale_line"]))!=1||Convert.ToInt64(Scalar("SELECT count(*) FROM warranty_claims c JOIN warranties w ON w.id=c.warranty WHERE w.unit=@p0 AND c.state='Replaced'",unit))>0)WarrantyError("Device has been returned, replaced, resold or its sale was cancelled");
    }
    public long SaveWarranty(long source,long unit,DateTime? expires,string terms)
    {
        store.Require("master");using var tx=store.BeginTransaction();var candidate=store.WarrantyDevices().Rows.Cast<DataRow>().SingleOrDefault(r=>(long)r["source_doc"]==source&&(long)r["id"]==unit);if(candidate==null)WarrantyError("Select a device from a posted serialized sale");
        if(expires!=null&&expires.Value.Date<DateTime.Parse((string)candidate!["sold"]))WarrantyError("Expiry cannot precede sale");
        if(candidate!["warranty"]!=DBNull.Value&&Convert.ToInt64(Scalar("SELECT count(*) FROM warranty_claims WHERE warranty=@p0",candidate["warranty"]))>0)WarrantyError("Warranty terms are locked after the first claim");
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM warranty_replacements WHERE doc=@p0",source))>0)WarrantyError("Replacement inherits the original warranty");
        Exec("INSERT INTO warranties(sale_line,source_doc,unit,expires,terms) VALUES(@p0,@p1,@p2,@p3,@p4) ON CONFLICT(source_doc,unit) DO UPDATE SET expires=excluded.expires,terms=excluded.terms",candidate["sale_line"],source,unit,expires?.ToString("yyyy-MM-dd"),terms.Trim());long id=Convert.ToInt64(Scalar("SELECT id FROM warranties WHERE source_doc=@p0 AND unit=@p1",source,unit));CheckWarrantyOwner(Warranty(id));Audit("warranty",id.ToString());tx.Commit();return id;
    }
    void WarrantyEvent(long id,string note){Exec("INSERT INTO warranty_events(claim,username,state,custody,note) SELECT id,@p0,state,custody,@p1 FROM warranty_claims WHERE id=@p2",store.User.Username,note,id);Audit("warranty-claim",id+": "+note);}
    public long ReceiveWarranty(long warranty,DateTime received,string issue,string condition,string accessories)
    {
        store.Require("post");using var tx=store.BeginTransaction();var w=Warranty(warranty);CheckWarrantyOwner(w);
        var saleDate=DateTime.Parse((string)Scalar("SELECT date FROM documents WHERE id=@p0",w["source_doc"])!);
        if(received.Date<saleDate||received.Date>DateTime.Today||string.IsNullOrWhiteSpace(issue)||string.IsNullOrWhiteSpace(condition))WarrantyError("Enter the fault, physical condition and a valid intake date");
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM warranty_claims WHERE warranty=@p0 AND state='Open'",warranty))>0)WarrantyError("This device already has an open claim");
        Exec("INSERT INTO warranty_claims(warranty,received,issue,condition,accessories,expires,terms) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6)",warranty,received.ToString("yyyy-MM-dd"),issue.Trim(),condition.Trim(),accessories.Trim(),w["expires"],w["terms"]);long id=Convert.ToInt64(Scalar("SELECT last_insert_rowid()"));WarrantyEvent(id,"Intake / استلام");tx.Commit();return id;
    }
    public void UpdateWarrantyCustody(long id,string action,string note,long? supplier=null,string reference="")
    {
        store.Require("post");using var tx=store.BeginTransaction();var c=Claim(id);if(string.IsNullOrWhiteSpace(note))WarrantyError("A reason / resolution is required");string state=(string)c["state"],custody=(string)c["custody"];
        if(action=="Send"&&custody=="InStore"&&state is "Open" or "Replaced"){
            if(supplier==null||!store.Accounts().Any(a=>a.Id==supplier&&a.Kind=="Supplier")||string.IsNullOrWhiteSpace(reference))WarrantyError("Select a supplier and tracking reference");custody="AtSupplier";
            Exec("UPDATE warranty_claims SET supplier=@p0,reference=@p1 WHERE id=@p2",supplier,reference.Trim(),id);
        }else if(action=="Receive"&&custody=="AtSupplier"&&state is "Open" or "Replaced")custody="InStore";
        else if(action=="Close"&&state=="Open"&&custody=="InStore"){state="Closed";custody="WithCustomer";}
        else if(action=="Dispose"&&state=="Replaced"&&custody=="InStore")custody="Disposed";
        else WarrantyError("Action is not available in the current claim state");
        Exec("UPDATE warranty_claims SET state=@p0,custody=@p1,resolution=@p2 WHERE id=@p3",state,custody,note.Trim(),id);WarrantyEvent(id,action+": "+note+(action=="Send"?" · "+store.LocalName("accounts",supplier!.Value,false)+" · "+reference:""));tx.Commit();
    }
    public long ReplaceWarranty(long claim,DateTime date,long item,long location,string serial,string overrideReason="")
    {
        store.Require("post");using var tx=store.BeginTransaction();var c=Claim(claim);var w=Warranty((long)c["warranty"]);CheckWarrantyOwner(w);
        if((string)c["state"]!="Open"||(string)c["custody"]!="InStore"||date.Date<DateTime.Parse((string)c["received"]))WarrantyError("Receive the faulty device in store before replacing it");
        bool covered=c["expires"]!=DBNull.Value&&DateTime.Parse((string)c["expires"])>=DateTime.Parse((string)c["received"]);
        if(!covered){store.Require("admin");if(string.IsNullOrWhiteSpace(overrideReason))WarrantyError("Expired / unrecorded coverage needs an administrator reason");}
        if(!store.IsSerialized(item)||SerialList(serial).Length!=1)WarrantyError("Choose one serialized replacement device");
        long unit=Convert.ToInt64(Scalar("SELECT id FROM serial_units WHERE item=@p0 AND normalized=@p1",item,NormalizeStockCode(serial)));
        if(unit==0||unit==(long)w["unit"])WarrantyError("Choose a different available device");
        long doc=NewDoc("WX",date,null,null,"Warranty claim #"+claim,new(0,0,0));
        Exec("INSERT INTO warranty_replacements(doc,claim,item,location,unit,override_reason) VALUES(@p0,@p1,@p2,@p3,@p4,@p5)",doc,claim,item,location,unit,overrideReason.Trim());RecordSerials(doc,null,item,location,1000,serial,-1);
        Exec("INSERT INTO warranties(sale_line,source_doc,unit,expires,terms) VALUES(@p0,@p1,@p2,@p3,@p4)",w["sale_line"],doc,unit,c["expires"],c["terms"]);
        Exec("UPDATE warranty_claims SET state='Replaced',resolution='Free replacement' WHERE id=@p0",claim);RebuildInventory("warranty "+claim);WarrantyEvent(claim,"Free replacement / استبدال مجاني: "+serial+" · "+overrideReason);tx.Commit();return doc;
    }
    void ReplayWarranty(DataRow doc,Dictionary<long,StockState> states)
    {
        long id=(long)doc["id"];var r=Table("SELECT * FROM warranty_replacements WHERE doc=@p0",id).Rows[0];long item=(long)r["item"];var st=states[item];if(st.Qty<1000)WarrantyError("Insufficient replacement stock in date order");long cost=st.Qty==1000?st.Value:M(D(st.Value)*1000/st.Qty);st.Qty-=1000;st.Value-=cost;
        Exec("DELETE FROM stock_moves WHERE doc=@p0",id);Exec("INSERT INTO stock_moves(doc,item,qty,value,location) VALUES(@p0,@p1,-1000,@p2,@p3)",id,item,-cost,r["location"]);Exec("DELETE FROM entries WHERE doc=@p0",id);if(cost>0)Journal(id,[new(AccountId("5196"),cost,0),new(AccountId("1301"),0,cost)]);
    }
    void CheckWarrantyReversal(long id)
    {
        if(!warrantyReversal&&Convert.ToInt64(Scalar("SELECT count(*) FROM warranty_replacements WHERE doc=@p0",id))>0)WarrantyError("Undo replacements from the warranty claim");
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM warranty_claims c JOIN warranties w ON w.id=c.warranty JOIN invoice_lines l ON l.id=w.sale_line WHERE (w.source_doc=@p0 OR l.doc=@p0) AND c.state IN ('Open','Replaced')",id))>0)WarrantyError("Resolve linked warranty claims before cancelling this document");
    }
    void CheckWarrantyReturn(long unit)
    {
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM warranty_claims c JOIN warranties w ON w.id=c.warranty WHERE w.unit=@p0 AND c.state IN ('Open','Replaced')",unit))>0)WarrantyError("Device is in a warranty claim or already replaced; resolve the claim first");
    }
    public void UndoWarrantyReplacement(long claim,string reason)
    {
        store.Require("post");using var tx=store.BeginTransaction();var c=Claim(claim);if((string)c["state"]!="Replaced"||(string)c["custody"]!="InStore"||string.IsNullOrWhiteSpace(reason))WarrantyError("Both devices must be back in store; enter the correction reason");
        long doc=Convert.ToInt64(Scalar("SELECT r.doc FROM warranty_replacements r JOIN documents d ON d.id=r.doc WHERE r.claim=@p0 AND d.reversed=0",claim));
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM warranties w JOIN warranty_claims c ON c.warranty=w.id WHERE w.source_doc=@p0",doc))>0)WarrantyError("Replacement device has subsequent claim history");
        warrantyReversal=true;try{Reverse(doc,reason);}finally{warrantyReversal=false;}
        Exec("UPDATE warranty_claims SET state='Open',resolution=@p0 WHERE id=@p1",reason.Trim(),claim);WarrantyEvent(claim,"Undo replacement / إلغاء الاستبدال: "+reason);tx.Commit();
    }
}
