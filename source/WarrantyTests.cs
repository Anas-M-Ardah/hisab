using System.IO;
using System.Data;
namespace Hisab;
public static class WarrantyTests
{
    public static void Run(string directory,List<string> results)
    {
        using var s=new Store(Path.Combine(directory,"warranty-"+Guid.NewGuid()+".db"));var a=new AccountingService(s);var today=DateTime.Today;
        void Check(bool valid,string name){if(!valid)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,name);}
        long Count(string table)=>Convert.ToInt64(s.Scalar("SELECT count(*) FROM "+table));
        var customer=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(customer.Id,customer.Code,customer.Name,"Customer");var supplier=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(supplier.Id,supplier.Code,supplier.Name,"Supplier");long item=s.Items()[0].Id;
        a.ConfigureInventoryItem(item,"WARRANTY",true,[]);a.OpeningStock(item,today,6,50,1,"W1,W2,W3,W4,W5,W6");
        long sale=a.Invoice(true,today,customer.Id,null,0,16,false,"Warranty sale",[new(item,"Device",1,100,0,1,"W1")]);long line=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",sale));
        long Unit(string serial)=>Convert.ToInt64(s.Scalar("SELECT id FROM serial_units WHERE number=@p0",serial));
        DataRow Claim(long id)=>s.WarrantyClaims().Rows.Cast<DataRow>().Single(r=>(long)r["id"]==id);
        Check(s.WarrantyDevices().Rows.Count==1,"Warranty lookup includes serialized posted sales");
        Reject(()=>a.SaveWarranty(sale,Unit("W2"),today.AddYears(1),""),"Cannot register an unsold device against a sale");
        Reject(()=>a.SaveWarranty(sale,Unit("W1"),today.AddDays(-1),""),"Warranty expiry cannot precede sale");
        long warranty=a.SaveWarranty(sale,Unit("W1"),today.AddYears(1),"Hardware defects only");long docs=Count("documents");long customerBalance=s.Balance(customer.Id),tax=s.Balance(s.AccountId("2102")),revenue=s.Balance(s.AccountId("4101"));
        Reject(()=>a.ReceiveWarranty(warranty,today.AddDays(1),"Fault","Clean",""),"Future warranty intake is rejected");
        Reject(()=>a.ReceiveWarranty(warranty,today,"","Clean",""),"Warranty intake requires a reported fault");
        long claim=a.ReceiveWarranty(warranty,today,"No power","No visible damage","Charger");
        Check(Count("documents")==docs&&s.LocationQuantity(item,1)==5000,"Warranty intake changes neither stock nor accounting");
        Reject(()=>a.ReceiveWarranty(warranty,today,"Fault","Clean",""),"Duplicate open warranty claim is rejected");
        Reject(()=>a.SaveWarranty(sale,Unit("W1"),today.AddYears(2),"Changed"),"Coverage is locked after intake");
        Reject(()=>a.Reverse(sale,"Cancel sale"),"Original invoice cannot be reversed while warranty is open");
        Reject(()=>a.ReturnInvoice(sale,today,[new(line,1,"W1")],"Return"),"Customer return cannot duplicate a device in warranty custody");
        Reject(()=>a.UpdateWarrantyCustody(claim,"Send","Checking",null,"RMA-1"),"Supplier custody requires a valid supplier");
        a.UpdateWarrantyCustody(claim,"Send","Supplier inspection",supplier.Id,"RMA-1");
        Reject(()=>a.ReplaceWarranty(claim,today,item,1,"W2"),"Replacement requires faulty device in store");
        a.UpdateWarrantyCustody(claim,"Receive","Returned from inspection");Check((string)Claim(claim)["custody"]=="InStore","Supplier round trip retains off-stock custody");
        long hold=a.CreateReservation(customer.Id,today,[new(item,1,1,"W2")],"Reserved device");
        Reject(()=>a.ReplaceWarranty(claim,today,item,1,"W2"),"Warranty replacement protects reserved serials");
        Check((string)Claim(claim)["state"]=="Open"&&Count("warranty_replacements")==0&&s.LocationQuantity(item,1)==5000,"Failed replacement rolls back claim, document and inventory");a.ReleaseReservation(hold,"Use replacement");
        Reject(()=>a.ReplaceWarranty(claim,today,item,1,"W1"),"Faulty original cannot replace itself");
        long exchange=a.ReplaceWarranty(claim,today,item,1,"W2");
        Check(s.LocationQuantity(item,1)==4000&&s.Items().Single(i=>i.Id==item).Value==200000,"Free replacement consumes exactly one device at cost");
        Check(s.Balance(customer.Id)==customerBalance&&s.Balance(s.AccountId("2102"))==tax&&s.Balance(s.AccountId("4101"))==revenue,"Warranty replacement leaves receivables, sales and tax unchanged");
        Check(s.Balance(s.AccountId("5196"))==50000&&s.Balance(s.AccountId("1301"))==s.Items().Sum(i=>i.Value),"Warranty cost posts balanced expense against inventory");
        Check(!s.AvailableSerials(item,1).Contains("W1")&&!s.AvailableSerials(item,1).Contains("W2"),"Neither faulty nor delivered replacement is sellable stock");
        Reject(()=>a.ReplaceWarranty(claim,today,item,1,"W3"),"Claim cannot be replaced twice");
        Reject(()=>a.Reverse(exchange,"Generic reversal"),"Replacement cancellation must use claim workflow");
        Reject(()=>a.ReturnInvoice(sale,today,[new(line,1,"W1")],"Old device"),"Already replaced original cannot be returned as customer stock");
        Reject(()=>a.ReceiveWarranty(warranty,today,"Again","Clean",""),"Already replaced original cannot create another claim");
        long replacementWarranty=Convert.ToInt64(s.Scalar("SELECT id FROM warranties WHERE source_doc=@p0",exchange));
        Check((string)s.Scalar("SELECT expires FROM warranties WHERE id=@p0",replacementWarranty)! ==today.AddYears(1).ToString("yyyy-MM-dd"),"Replacement inherits original expiry");
        a.UndoWarrantyReplacement(claim,"Physical exchange undone");Check(s.LocationQuantity(item,1)==5000&&s.Balance(s.AccountId("5196"))==0&&(string)Claim(claim)["state"]=="Open","Undo replacement restores stock and expense atomically");
        exchange=a.ReplaceWarranty(claim,today,item,1,"W2");replacementWarranty=Convert.ToInt64(s.Scalar("SELECT id FROM warranties WHERE source_doc=@p0",exchange));long second=a.ReceiveWarranty(replacementWarranty,today,"Replacement failure","Clean","Charger");
        Reject(()=>a.UndoWarrantyReplacement(claim,"Too late"),"Replacement with subsequent claim history cannot be undone");
        a.ReplaceWarranty(second,today,item,1,"W3");Check(s.LocationQuantity(item,1)==3000&&s.Balance(s.AccountId("5196"))==100000,"Replacement device can receive a linked subsequent free replacement");
        a.UpdateWarrantyCustody(claim,"Send","Supplier RMA",supplier.Id,"RMA-2");a.UpdateWarrantyCustody(claim,"Receive","Beyond repair");a.UpdateWarrantyCustody(claim,"Dispose","Authorized disposal");Check(s.LocationQuantity(item,1)==3000&&(string)Claim(claim)["custody"]=="Disposed","Faulty-device disposal never changes sellable stock");
        long sale2=a.Invoice(true,today,customer.Id,null,0,0,false,"Unknown warranty",[new(item,"Device",1,100,0,1,"W4")]);long unknown=a.SaveWarranty(sale2,Unit("W4"),null,"");long unrecorded=a.ReceiveWarranty(unknown,today,"Fault","Clean","");
        Reject(()=>a.ReplaceWarranty(unrecorded,today,item,1,"W5"),"Unrecorded coverage requires explicit administrator reason");
        a.ReplaceWarranty(unrecorded,today,item,1,"W5","Goodwill approved");Check((string)s.Scalar("SELECT override_reason FROM warranty_replacements WHERE claim=@p0",unrecorded)! =="Goodwill approved","Goodwill exception retains administrator reason");
        long sale3=a.Invoice(true,today,customer.Id,null,0,0,false,"Return test",[new(item,"Device",1,100,0,1,"W6")]);long warranty3=a.SaveWarranty(sale3,Unit("W6"),today,"");long close=a.ReceiveWarranty(warranty3,today,"No fault","Clean","");a.UpdateWarrantyCustody(close,"Close","Tested and returned to customer");
        long line3=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",sale3));a.ReturnInvoice(sale3,today,[new(line3,1,"W6")],"Customer return");Reject(()=>a.ReceiveWarranty(warranty3,today,"Again","Clean",""),"Returned device cannot open a warranty claim");
        Check(s.Table("PRAGMA foreign_key_check").Rows.Count==0&&Convert.ToInt64(s.Scalar("SELECT count(*) FROM (SELECT doc FROM entries GROUP BY doc HAVING sum(debit)!=sum(credit))"))==0,"Warranty lifecycle keeps foreign keys and journals valid");
        Reject(()=>a.Invoice(false,today,supplier.Id,null,0,0,false,"Duplicate faulty stock",[new(item,"Faulty device",1,1,0,1,"W1")]),"Purchase cannot reintroduce a replaced device as available stock");
        long count=a.StartStockCount(1,[item]);a.RecordStockCount(count,item,2,"W6,W1",50);Reject(()=>a.CompleteStockCount(count,"Faulty device counted"),"Stock count cannot reintroduce warranty custody devices");a.CancelStockCount(count);
        s.SaveUser("warranty-admin","warranty-admin-password","Admin");s.SaveUser("warranty-accountant","warranty-accountant-password","Accountant");s.SaveUser("warranty-viewer","warranty-viewer-password","Viewer");
        long sale4=a.Invoice(true,today,customer.Id,null,0,0,false,"Permission fixture",[new(item,"Device",1,100,0,1,"W6")]);long w4=a.SaveWarranty(sale4,Unit("W6"),null,"");long c4=a.ReceiveWarranty(w4,today,"Fault","Clean","");
        s.Login("warranty-accountant","warranty-accountant-password");Reject(()=>a.ReplaceWarranty(c4,today,item,1,"W1","Override"),"Accountant cannot override unrecorded coverage");
        s.Login("warranty-viewer","warranty-viewer-password");Reject(()=>a.UpdateWarrantyCustody(c4,"Close","Viewer attempt"),"Viewer cannot change warranty custody");Reject(()=>a.ReplaceWarranty(c4,today,item,1,"W1","Override"),"Viewer cannot post a warranty replacement");Reject(()=>a.SaveWarranty(sale4,Unit("W6"),today,""),"Viewer cannot change warranty registration");
        s.Login("warranty-admin","warranty-admin-password");a.UpdateWarrantyCustody(c4,"Close","Returned without replacement");
        string backup=Path.Combine(directory,"warranty-backup.hisab");s.PortableBackup(backup,"warranty-backup-password");using var restored=new Store(Path.Combine(directory,"warranty-restored.db"));restored.RestorePortable(backup,"warranty-backup-password");Check(restored.WarrantyClaims().Rows.Count==5&&restored.Table("SELECT * FROM warranty_events").Rows.Count==Count("warranty_events"),"Warranty claims, replacement chain and custody history survive restore");
        string legacyPath=Path.Combine(directory,"warranty-legacy6.db");long oldDocument;
        using(var legacy=new Store(legacyPath)){var oldService=new AccountingService(legacy);oldDocument=oldService.OpeningStock(legacy.Items()[0].Id,today,2,25);legacy.Exec("DROP TABLE warranty_replacements; DROP TABLE warranty_events; DROP TABLE warranty_claims; DROP TABLE warranties; DELETE FROM accounts WHERE code='5196'; PRAGMA user_version=6;");}
        using(var upgraded=new Store(legacyPath))Check(upgraded.WarrantyClaims().Rows.Count==0&&upgraded.Items()[0].Qty==2000&&Convert.ToInt64(upgraded.Scalar("SELECT count(*) FROM documents WHERE id=@p0",oldDocument))==1&&upgraded.Accounts().Any(a=>a.Code=="5196"&&a.Kind=="Expense"),"Real schema 6 upgrade creates warranty tables while preserving inventory and documents");
        ReplayAndExpiry(directory,results);
    }
    static void ReplayAndExpiry(string directory,List<string> results)
    {
        using var s=new Store(Path.Combine(directory,"warranty-replay-"+Guid.NewGuid()+".db"));var a=new AccountingService(s);var today=DateTime.Today;
        void Check(bool b,string name){if(!b)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,name);}
        var customer=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(customer.Id,customer.Code,customer.Name,"Customer");var supplier=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(supplier.Id,supplier.Code,supplier.Name,"Supplier");long item=s.Items()[0].Id;
        a.ConfigureInventoryItem(item,"REPLAY-WARRANTY",true,[]);a.OpeningStock(item,today.AddDays(-5),3,50,1,"E1,E2,E3");
        long sale=a.Invoice(true,today.AddDays(-3),customer.Id,null,0,0,false,"Older sale",[new(item,"Device",1,100,0,1,"E1")]);long unit=Convert.ToInt64(s.Scalar("SELECT id FROM serial_units WHERE number='E1'"));long warranty=a.SaveWarranty(sale,unit,today.AddDays(-1),"Short warranty");long claim=a.ReceiveWarranty(warranty,today,"Fault","Clean","");
        Reject(()=>a.ReplaceWarranty(claim,today,item,1,"E2"),"Expired coverage needs an explicit administrator exception");
        long exchange=a.ReplaceWarranty(claim,today,item,1,"E2","Goodwill replacement");
        a.Invoice(false,today.AddDays(-2),supplier.Id,null,0,0,false,"Backdated cost",[new(item,"Device",1,200,0,1,"E4")]);
        Check(s.Balance(s.AccountId("5196"))==100000&&s.Items().Single(i=>i.Id==item).Value==200000,"Historical purchase replays warranty replacement weighted-average cost");
        Check(s.ItemHistory(item).Any(h=>h.Id==exchange),"Warranty replacement appears in item history");
        a.UndoWarrantyReplacement(claim,"Devices physically returned");Check(s.Balance(s.AccountId("5196"))==0&&s.Items().Single(i=>i.Id==item).Value==300000,"Undo after cost replay reverses the recalculated warranty expense");
        long location=s.SaveLocation(null,"Other warehouse");Reject(()=>a.ReplaceWarranty(claim,today,item,location,"E2","Goodwill"),"Replacement cannot consume a serial from another location");
        Reject(()=>a.ReplaceWarranty(claim,today.AddDays(-1),item,1,"E2","Goodwill"),"Replacement cannot predate warranty intake");
        a.UpdateWarrantyCustody(claim,"Close","Customer collected original");s.Exec("PRAGMA user_version=6");
        string migration=Path.Combine(directory,"warranty-schema6.db");s.Backup(migration);using var reopened=new Store(migration);Check(Convert.ToInt64(reopened.Scalar("PRAGMA user_version"))==Store.SchemaVersion&&reopened.WarrantyClaims().Rows.Count==1,"Schema upgrade is repeatable and preserves existing warranty records");
    }
}
