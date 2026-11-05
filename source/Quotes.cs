using System.Data;
using System.Globalization;
using System.Text.Json;
using static Hisab.Store;
namespace Hisab;

public record Quote(long Id,string Number,long Customer,string CustomerName,string CustomerNameEn,DateTime Date,DateTime Expires,string State,int Revision,string Note,string Company,string CompanyEn,string Terms,string TermsEn,decimal Vat,bool Inclusive,List<InvoiceLine> Lines,long? Invoice)
{
    public string EffectiveState=>Invoice!=null?"Converted":State=="Declined"?State:Expires.Date<DateTime.Today?"Expired":State;
}
public sealed partial class Store
{
    void UpgradeQuotes()
    {
        using var tx=BeginTransaction();
        Exec("""
        CREATE TABLE IF NOT EXISTS quotes(id INTEGER PRIMARY KEY,number TEXT NOT NULL UNIQUE,customer INTEGER NOT NULL REFERENCES accounts(id),date TEXT NOT NULL,expires TEXT NOT NULL,state TEXT NOT NULL,revision INTEGER NOT NULL DEFAULT 1,payload TEXT NOT NULL,invoice INTEGER UNIQUE REFERENCES documents(id));
        CREATE TABLE IF NOT EXISTS quote_versions(quote INTEGER NOT NULL REFERENCES quotes(id),revision INTEGER NOT NULL,payload TEXT NOT NULL,PRIMARY KEY(quote,revision));
        """);tx.Commit();
    }
    public Quote GetQuote(long id)
    {
        var rows=Table("SELECT * FROM quotes WHERE id=@p0",id);if(rows.Rows.Count!=1)throw new InvalidOperationException("عرض السعر غير موجود / Quote not found");
        var r=rows.Rows[0];var q=JsonSerializer.Deserialize<Quote>((string)r["payload"])!;
        return q with {Id=(long)r["id"],Number=(string)r["number"],State=(string)r["state"],Revision=Convert.ToInt32(r["revision"]),Invoice=r["invoice"]==DBNull.Value?null:(long)r["invoice"]};
    }
    public List<Quote> Quotes()=>Table("SELECT id FROM quotes ORDER BY id DESC").Rows.Cast<DataRow>().Select(r=>GetQuote((long)r[0])).ToList();
}
public sealed partial class AccountingService
{
    public long SaveQuote(long? id,int revision,long customer,DateTime date,DateTime expires,decimal vat,bool inclusive,string note,List<InvoiceLine> lines)
    {
        store.Require("post");
        if(!Accounts().Any(a=>a.Id==customer&&a.Kind=="Customer"))InventoryError("Choose a customer / اختر عميلًا");
        if(date.Date>DateTime.Today||expires.Date<date.Date)InventoryError("Check quotation and expiry dates / تحقق من تاريخ العرض والصلاحية");
        if(lines.Count==0||Calculate(lines,vat,inclusive).Total<=0)InventoryError("Add priced quotation lines / أضف أصناف العرض وأسعارها");
        foreach(var l in lines)if(!Items().Any(i=>i.Id==l.Item)||string.IsNullOrWhiteSpace(l.Description)||(store.IsSerialized(l.Item)&&l.Qty!=decimal.Truncate(l.Qty)))InventoryError("Check item, description and whole device quantities / تحقق من الصنف والوصف والكمية");
        using var tx=store.BeginTransaction();Quote? old=id==null?null:store.GetQuote(id.Value);
        if(id!=null&&store.ActiveQuoteReservation(id.Value)!=null)InventoryError("حرر حجز العرض قبل تعديله / Release the quote reservation before editing");
        if(old!=null&&(old.Invoice!=null||old.Revision!=revision))InventoryError("Quote was converted or changed; reopen it / تم تحويل العرض أو تعديله؛ أعد فتحه");
        string number=old?.Number??"QT-"+(Convert.ToInt64(Scalar("SELECT coalesce(max(id),0)+1 FROM quotes"))).ToString("D6");
        var q=new Quote(id??0,number,customer,store.LocalName("accounts",customer,true),store.LocalName("accounts",customer,false),date.Date,expires.Date,"Draft",(old?.Revision??0)+1,note,Setting("company"),Setting("company_en"),Setting("terms"),Setting("terms_en"),vat,inclusive,lines.Select(l=>l with {Serials="",Location=1}).ToList(),null);
        if(id==null){Exec("INSERT INTO quotes(number,customer,date,expires,state,payload) VALUES(@p0,@p1,@p2,@p3,'Draft',@p4)",number,customer,date.ToString("yyyy-MM-dd"),expires.ToString("yyyy-MM-dd"),JsonSerializer.Serialize(q));id=Convert.ToInt64(Scalar("SELECT last_insert_rowid()"));}
        else Exec("UPDATE quotes SET customer=@p0,date=@p1,expires=@p2,state='Draft',revision=revision+1,payload=@p3 WHERE id=@p4",customer,date.ToString("yyyy-MM-dd"),expires.ToString("yyyy-MM-dd"),JsonSerializer.Serialize(q),id);
        SnapshotQuote(id.Value);Audit("quote-save",number);tx.Commit();return id.Value;
    }
    void SnapshotQuote(long id){var q=store.GetQuote(id);Exec("INSERT INTO quote_versions(quote,revision,payload) VALUES(@p0,@p1,@p2)",id,q.Revision,JsonSerializer.Serialize(q));}
    public void SetQuoteState(long id,int revision,string state)
    {
        store.Require("post");using var tx=store.BeginTransaction();var q=store.GetQuote(id);
        if(store.ActiveQuoteReservation(id)!=null)InventoryError("حرر حجز العرض قبل تغيير حالته / Release the quote reservation before changing status");
        if(q.Invoice!=null||q.Revision!=revision||state is not ("Sent" or "Accepted" or "Declined")||q.EffectiveState=="Expired")InventoryError("Reopen or revise this quote before changing its status / أعد فتح العرض أو تعديله");
        Exec("UPDATE quotes SET state=@p0,revision=revision+1 WHERE id=@p1",state,id);SnapshotQuote(id);Audit("quote-status",q.Number+": "+state);tx.Commit();
    }
    public long ConvertQuote(long id,int revision,DateTime date,long customer,long? cash,decimal paid,decimal vat,bool inclusive,string note,List<InvoiceLine> lines,DateTime due)
    {
        store.Require("post");using var tx=store.BeginTransaction();var q=store.GetQuote(id);
        if(q.Invoice!=null||q.Revision!=revision||q.EffectiveState is "Expired" or "Declined"||date.Date<q.Date||date.Date>q.Expires)InventoryError("Quote is expired, declined, changed or already converted; revise it first / راجع حالة العرض وصلاحيته");
        if(customer!=q.Customer||vat!=q.Vat||inclusive!=q.Inclusive||!lines.Select(l=>l with {Location=1,Serials=""}).SequenceEqual(q.Lines))InventoryError("Revise the quote before changing its customer, prices or lines / عدّل العرض أولًا لتغيير العميل أو الأسعار أو الأصناف");
        long? reservation=store.ActiveQuoteReservation(id);if(reservation!=null)PrepareReservation(reservation.Value,date,customer,lines);
        long invoice=Invoice(true,date,customer,cash,paid,vat,inclusive,note,lines);SetDueDate(invoice,due);if(reservation!=null)CompleteReservation(reservation.Value,invoice);
        Exec("UPDATE quotes SET state='Converted',invoice=@p0,revision=revision+1 WHERE id=@p1",invoice,id);SnapshotQuote(id);Audit("quote-convert",q.Number+" -> "+invoice);tx.Commit();return invoice;
    }
}
