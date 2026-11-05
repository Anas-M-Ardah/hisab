using System.Data;
using static Hisab.Store;
namespace Hisab;
public record ReservationInput(long Item,long Location,decimal Quantity,string Serials="");
public record Reservation(long Id,long Customer,DateTime Created,DateTime Expires,string State,string Note,long? Quote,long? Invoice,List<ReservationInput> Lines)
{
    public string Number=>"RS-"+Id.ToString("D6");
    public string EffectiveState=>State=="Active"&&Expires.Date<DateTime.Today?"Expired":State;
}
public sealed partial class Store
{
    void UpgradeReservations()
    {
        using var tx=BeginTransaction();Exec("""
        CREATE TABLE IF NOT EXISTS reservations(id INTEGER PRIMARY KEY,customer INTEGER NOT NULL REFERENCES accounts(id),created TEXT NOT NULL,expires TEXT NOT NULL,state TEXT NOT NULL CHECK(state IN ('Active','Released','Fulfilled')),note TEXT NOT NULL,quote INTEGER REFERENCES quotes(id),invoice INTEGER UNIQUE REFERENCES documents(id));
        CREATE TABLE IF NOT EXISTS reservation_lines(reservation INTEGER NOT NULL REFERENCES reservations(id),item INTEGER NOT NULL REFERENCES items(id),location INTEGER NOT NULL REFERENCES locations(id),qty INTEGER NOT NULL CHECK(qty>0),serials TEXT NOT NULL DEFAULT '',PRIMARY KEY(reservation,item,location));
        CREATE INDEX IF NOT EXISTS ix_reservations_active ON reservations(state,expires);
        CREATE INDEX IF NOT EXISTS ix_reservation_stock ON reservation_lines(item,location);
        """);tx.Commit();
    }
    public Reservation GetReservation(long id)
    {
        var rows=Table("SELECT * FROM reservations WHERE id=@p0",id);if(rows.Rows.Count!=1)throw new InvalidOperationException("الحجز غير موجود / Reservation not found");var r=rows.Rows[0];
        return new(id,(long)r["customer"],DateTime.Parse((string)r["created"]),DateTime.Parse((string)r["expires"]),(string)r["state"],(string)r["note"],r["quote"]==DBNull.Value?null:(long)r["quote"],r["invoice"]==DBNull.Value?null:(long)r["invoice"],Table("SELECT * FROM reservation_lines WHERE reservation=@p0 ORDER BY item,location",id).Rows.Cast<DataRow>().Select(l=>new ReservationInput((long)l["item"],(long)l["location"],D((long)l["qty"]),(string)l["serials"])).ToList());
    }
    public List<Reservation> Reservations(bool activeOnly=false)=>Table("SELECT id FROM reservations WHERE @p0=0 OR (state='Active' AND expires>=@p1) ORDER BY id DESC",activeOnly?1:0,DateTime.Today.ToString("yyyy-MM-dd")).Rows.Cast<DataRow>().Select(r=>GetReservation((long)r[0])).ToList();
    public long ReservedQuantity(long item,long location)=>Convert.ToInt64(Scalar("SELECT coalesce(sum(l.qty),0) FROM reservation_lines l JOIN reservations r ON r.id=l.reservation WHERE l.item=@p0 AND l.location=@p1 AND r.state='Active' AND r.expires>=@p2",item,location,DateTime.Today.ToString("yyyy-MM-dd")));
    public long AvailableQuantity(long item,long location)=>LocationQuantity(item,location)-ReservedQuantity(item,location);
    public long? ActiveQuoteReservation(long quote){var found=Scalar("SELECT id FROM reservations WHERE quote=@p0 AND state='Active' AND expires>=@p1",quote,DateTime.Today.ToString("yyyy-MM-dd"));return found==null?null:Convert.ToInt64(found);}
}
public sealed partial class AccountingService
{
    public long CreateReservation(long customer,DateTime expires,IReadOnlyList<ReservationInput> lines,string note,long? quote=null)
    {
        store.Require("post");using var tx=store.BeginTransaction();
        if(!Accounts().Any(a=>a.Id==customer&&a.Kind=="Customer")||expires.Date<DateTime.Today||lines.Count==0||lines.Select(l=>(l.Item,l.Location)).Distinct().Count()!=lines.Count)InventoryError("اختر العميل والصلاحية وأصنافًا مميزة / Choose a customer, valid expiry and distinct item/location lines");
        if(quote!=null){var q=store.GetQuote(quote.Value);if(q.Customer!=customer||q.EffectiveState!="Accepted"||expires.Date>q.Expires.Date||store.ActiveQuoteReservation(q.Id)!=null)InventoryError("الحجز لعرض مقبول وساري وللعميل نفسه / Reserve a valid accepted quote for the same customer, within its validity");var expected=q.Lines.Where(l=>Items().Single(i=>i.Id==l.Item).Stock).GroupBy(l=>l.Item).ToDictionary(g=>g.Key,g=>g.Sum(l=>M(l.Qty)));if(lines.Select(l=>l.Item).Distinct().Count()!=lines.Count)InventoryError("اختر موقعًا واحدًا لكل صنف في العرض / Choose one location per quoted item");var actual=lines.GroupBy(l=>l.Item).ToDictionary(g=>g.Key,g=>g.Sum(l=>M(l.Quantity)));if(expected.Count!=actual.Count||expected.Any(p=>actual.GetValueOrDefault(p.Key)!=p.Value))InventoryError("احجز كل الكميات المخزنية في العرض / Reserve exactly the quote's stock quantities");}
        foreach(var l in lines){RequireLocation(l.Location);if(!Items().Any(i=>i.Id==l.Item&&i.Stock)||l.Quantity<=0||D(M(l.Quantity))!=l.Quantity||M(l.Quantity)>store.AvailableQuantity(l.Item,l.Location)||(store.IsSerialized(l.Item)&&l.Quantity!=decimal.Truncate(l.Quantity)))InventoryError("كمية الحجز تتجاوز المتاح أو غير صالحة / Reservation quantity exceeds available stock or is invalid");var serials=SerialList(l.Serials);if(serials.Length>0&&(!store.IsSerialized(l.Item)||serials.Length>l.Quantity||serials.Select(NormalizeStockCode).Distinct().Count()!=serials.Length))InventoryError("راجع أرقام الأجهزة المحجوزة / Check optional reserved serial numbers");if(Convert.ToInt64(Scalar("SELECT count(*) FROM stock_counts c JOIN stock_count_lines l ON l.count_id=c.id WHERE c.state='Open' AND c.location=@p0 AND l.item=@p1",l.Location,l.Item))>0)InventoryError("أكمل الجرد قبل الحجز / Complete the stock count before reserving");}
        Exec("INSERT INTO reservations(customer,created,expires,state,note,quote) VALUES(@p0,@p1,@p2,'Active',@p3,@p4)",customer,DateTime.Today.ToString("yyyy-MM-dd"),expires.Date.ToString("yyyy-MM-dd"),note,quote);long id=Convert.ToInt64(Scalar("SELECT last_insert_rowid()"));
        foreach(var l in lines)Exec("INSERT INTO reservation_lines(reservation,item,location,qty,serials) VALUES(@p0,@p1,@p2,@p3,@p4)",id,l.Item,l.Location,M(l.Quantity),string.Join("\n",SerialList(l.Serials)));
        ValidateReservations();Audit("reservation-create","RS-"+id.ToString("D6"));tx.Commit();return id;
    }
    public void ReleaseReservation(long id,string reason)
    {
        store.Require("post");using var tx=store.BeginTransaction();var r=store.GetReservation(id);if(r.State!="Active"||string.IsNullOrWhiteSpace(reason))InventoryError("اختر حجزًا نشطًا واكتب سبب التحرير / Choose an active reservation and enter a release reason");Exec("UPDATE reservations SET state='Released' WHERE id=@p0",id);Audit("reservation-release",r.Number+": "+reason.Trim());tx.Commit();
    }
    void ValidateReservations()
    {
        var active=store.Reservations(true);var lines=active.SelectMany(r=>r.Lines).ToList();
        foreach(var group in lines.GroupBy(l=>(l.Item,l.Location)))if(store.LocationQuantity(group.Key.Item,group.Key.Location)<group.Sum(l=>M(l.Quantity)))InventoryError("العملية تستهلك مخزونًا محجوزًا؛ حرر الحجز أو نفذ البيع منه / This operation consumes reserved stock; release the hold or fulfill its sale");
        var claimed=new HashSet<(long,string)>();foreach(var l in lines){var available=store.AvailableSerials(l.Item,l.Location).Select(NormalizeStockCode).ToHashSet();foreach(var serial in SerialList(l.Serials)){string key=NormalizeStockCode(serial);if(!available.Contains(key)||!claimed.Add((l.Item,key)))InventoryError("الجهاز محجوز أو غير متاح في موقعه / Reserved device is unavailable at its location or already held");}}
    }
    void PrepareReservation(long id,DateTime date,long customer,List<InvoiceLine> lines)
    {
        var r=store.GetReservation(id);if(r.EffectiveState!="Active"||r.Customer!=customer||date.Date<r.Created||date.Date>r.Expires)InventoryError("راجع العميل وتاريخ وصلاحية الحجز / Check reservation customer, date and validity");
        foreach(var held in r.Lines){var supplied=lines.Where(l=>l.Item==held.Item&&l.Location==held.Location).ToList();if(supplied.Sum(l=>M(l.Qty))<M(held.Quantity))InventoryError("يجب بيع كامل الكمية المحجوزة من موقعها / Sell all reserved quantities from their locations");var serials=supplied.SelectMany(l=>SerialList(l.Serials)).Select(NormalizeStockCode).ToHashSet();if(SerialList(held.Serials).Any(s=>!serials.Contains(NormalizeStockCode(s))))InventoryError("أضف الأجهزة المحددة في الحجز / Include the specifically reserved devices");}
        Exec("UPDATE reservations SET state='Fulfilled' WHERE id=@p0",id);
    }
    void CompleteReservation(long id,long invoice){Exec("UPDATE reservations SET invoice=@p0 WHERE id=@p1",invoice,id);Audit("reservation-fulfill","RS-"+id.ToString("D6")+" -> "+invoice);}
    public long FulfillReservation(long id,DateTime date,long customer,long? cash,decimal paid,decimal vat,bool inclusive,string note,List<InvoiceLine> lines,DateTime due)
    {
        store.Require("post");using var tx=store.BeginTransaction();var r=store.GetReservation(id);if(r.EffectiveState!="Active")InventoryError("الحجز لم يعد نشطًا / Reservation is no longer active");
        if(r.Quote is long quote){var q=store.GetQuote(quote);long result=ConvertQuote(quote,q.Revision,date,customer,cash,paid,vat,inclusive,note,lines,due);tx.Commit();return result;}
        PrepareReservation(id,date,customer,lines);long invoice=Invoice(true,date,customer,cash,paid,vat,inclusive,note,lines);SetDueDate(invoice,due);CompleteReservation(id,invoice);tx.Commit();return invoice;
    }
}
