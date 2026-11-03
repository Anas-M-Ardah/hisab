using System.IO;
namespace Hisab;
public static class QuoteTests
{
    public static void Run(string directory,List<string> results)
    {
        using var s=new Store(Path.Combine(directory,"quotes-"+Guid.NewGuid()+".db"));var a=new AccountingService(s);var today=DateTime.Today;
        void Check(bool valid,string name){if(!valid)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,name);}
        var customer=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(customer.Id,customer.Code,customer.Name,"Customer");long item=s.Items()[0].Id;
        List<InvoiceLine> Lines(decimal qty=1)=>[new(item,"Quoted device",qty,100,5)];
        long Save(DateTime? expires=null,decimal qty=1)=>a.SaveQuote(null,0,customer.Id,today.AddDays(-2),expires??today.AddDays(14),16,false,"Delivery subject to availability",Lines(qty));
        s.Set("company","Original company");long id=Save();var q=s.GetQuote(id);
        Check(q.Number=="QT-000001"&&q.EffectiveState=="Draft"&&Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==0&&s.Items().Single(i=>i.Id==item).Qty==0,"Quotation does not post accounting or reserve stock");
        s.Set("company","New company");Check(s.GetQuote(id).Company=="Original company","Quotation preserves customer-facing company snapshot");
        Reject(()=>a.SaveQuote(null,0,customer.Id,today,today.AddDays(-1),0,false,"",Lines()),"Quotation rejects expiry before issue date");
        Reject(()=>a.SaveQuote(null,0,customer.Id,today,today,0,false,"",[]),"Quotation requires at least one priced line");
        a.SetQuoteState(id,q.Revision,"Sent");Check(s.GetQuote(id).State=="Sent","Quotation tracks sent status");
        Reject(()=>a.SaveQuote(id,q.Revision,customer.Id,today,today.AddDays(5),16,false,"",Lines()),"Stale quote edits cannot overwrite a newer revision");
        q=s.GetQuote(id);a.SaveQuote(id,q.Revision,customer.Id,today,today.AddDays(5),16,false,"Revised",Lines());q=s.GetQuote(id);Check(q.State=="Draft"&&q.Revision==3,"Editing a quotation resets status to draft with new revision");
        long expired=Save(today.AddDays(-1));var expiredQuote=s.GetQuote(expired);Reject(()=>a.ConvertQuote(expired,expiredQuote.Revision,today,customer.Id,null,0,16,false,"",Lines(),today),"Expired quotation cannot convert to an invoice");
        long declined=Save();a.SetQuoteState(declined,1,"Declined");Reject(()=>a.ConvertQuote(declined,2,today,customer.Id,null,0,16,false,"",Lines(),today),"Declined quotation cannot convert to an invoice");
        long count=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));Reject(()=>a.ConvertQuote(id,q.Revision,today,customer.Id,null,0,16,false,"",Lines(),today),"Conversion validates actual stock availability");Check(s.GetQuote(id).Invoice==null&&Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==count,"Failed quote conversion rolls back invoice and quote state");
        a.OpeningStock(item,today,3,50);
        Reject(()=>a.ConvertQuote(id,q.Revision,today,customer.Id,null,0,16,false,"",Lines(2),today),"Conversion cannot silently change quoted quantities or prices");
        Reject(()=>a.ConvertQuote(id,q.Revision,today,customer.Id,null,0,16,false,"",Lines(),today.AddDays(-1)),"Invalid invoice due date rolls back quote conversion");
        a.SetQuoteState(id,q.Revision,"Accepted");q=s.GetQuote(id);long invoice=a.ConvertQuote(id,q.Revision,today,customer.Id,null,0,16,false,q.Note,q.Lines,today.AddDays(30));
        Check(s.GetQuote(id).Invoice==invoice&&s.GetQuote(id).EffectiveState=="Converted"&&s.Items().Single(i=>i.Id==item).Qty==2000&&Convert.ToInt64(s.Scalar("SELECT total FROM documents WHERE id=@p0",invoice))==110200,"Conversion creates exactly one linked invoice with quoted tax and discount");
        Reject(()=>a.ConvertQuote(id,q.Revision,today,customer.Id,null,0,16,false,q.Note,q.Lines,today),"A quotation cannot be converted twice");
        a.Reverse(invoice,"Customer cancelled");Check(s.GetQuote(id).Invoice==invoice&&s.GetQuote(id).State=="Converted","Invoice reversal retains quotation link and prevents duplicate conversion");
        string backup=Path.Combine(directory,"quotes-backup.hisab");s.PortableBackup(backup,"quote-backup-password");using var restored=new Store(Path.Combine(directory,"quotes-restored.db"));restored.RestorePortable(backup,"quote-backup-password");Check(restored.GetQuote(id).Invoice==invoice&&restored.GetQuote(id).Lines.Count==1&&restored.Table("PRAGMA foreign_key_check").Rows.Count==0,"Quotation and invoice link survive portable backup and restore");
        Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM quote_versions WHERE quote=@p0",id))==5,"Quotation retains snapshots of saved revisions and status changes");
        long device=s.Items()[1].Id;a.ConfigureInventoryItem(device,"QUOTE-SERIAL",true,Array.Empty<string>());a.OpeningStock(device,today,1,50,1,"SERIAL-Q");
        var serialLines=new List<InvoiceLine>{new(device,"Serialized hardware",1,100,0)};long serialQuote=a.SaveQuote(null,0,customer.Id,today,today.AddDays(1),0,false,"",serialLines);
        Reject(()=>a.ConvertQuote(serialQuote,1,today,customer.Id,null,0,0,false,"",serialLines,today),"Serialized quote conversion requires the actual device identity");
        serialLines[0]=serialLines[0] with {Serials="SERIAL-Q"};long serialInvoice=a.ConvertQuote(serialQuote,1,today,customer.Id,null,0,0,false,"",serialLines,today);
        Check(s.GetQuote(serialQuote).Invoice==serialInvoice&&s.AvailableSerials(device,1).Count==0,"Serialized quote converts after supplying the device serial");
        string reopenPath=Path.Combine(directory,"quote-reopen.db");s.Backup(reopenPath);using(var reopened=new Store(reopenPath))Check(reopened.GetQuote(serialQuote).Invoice==serialInvoice&&Convert.ToInt64(reopened.Scalar("PRAGMA user_version"))==Store.SchemaVersion,"Quotation data survives reopening with schema protection");
        s.SaveUser("quote-viewer","quote-viewer-password","Viewer");s.Login("quote-viewer","quote-viewer-password");Reject(()=>Save(),"Viewer cannot create quotations");
    }
}
