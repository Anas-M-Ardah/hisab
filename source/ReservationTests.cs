using System.IO;
namespace Hisab;
public static class ReservationTests
{
    public static void Run(string directory,List<string> results)
    {
        using var s=new Store(Path.Combine(directory,"reservation-"+Guid.NewGuid()+".db"));var a=new AccountingService(s);var today=DateTime.Today;
        void Check(bool valid,string name){if(!valid)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,name);}
        var customer=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(customer.Id,customer.Code,customer.Name,"Customer");var other=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(other.Id,other.Code,other.Name,"Customer");long item=s.Items()[0].Id;
        long opening=a.OpeningStock(item,today,10,50);long docs=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));
        List<InvoiceLine> Sale(decimal q)=>[new(item,"Hardware",q,100,0)];
        long Hold(decimal q)=>a.CreateReservation(customer.Id,today.AddDays(7),[new ReservationInput(item,1,q)],"Customer order");
        long hold=Hold(4);Check(s.ReservedQuantity(item,1)==4000&&s.AvailableQuantity(item,1)==6000&&s.LocationQuantity(item,1)==10000&&Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==docs,"Reservation reduces availability without posting stock or accounting");
        Reject(()=>Hold(7),"Reservation cannot exceed unreserved stock");
        Reject(()=>a.Invoice(true,today,other.Id,null,0,0,false,"",Sale(7)),"Other sales cannot consume reserved quantities");
        long remote=s.SaveLocation(null,"Reservation warehouse");Reject(()=>a.TransferInventory(today,1,remote,item,7,"","Transfer"),"Transfers protect reserved quantities at their location");
        Reject(()=>a.Reverse(opening,"Undo stock"),"Receipt reversal cannot remove stock backing reservations");
        long count=a.StartStockCount(1,[item]);a.RecordStockCount(count,item,3,"",50);Reject(()=>a.CompleteStockCount(count,"Physical shortage"),"Stock count shortage requires resolving conflicting reservations");a.CancelStockCount(count);
        Reject(()=>a.FulfillReservation(hold,today,other.Id,null,0,0,false,"",Sale(4),today),"Reservation fulfillment requires its customer");
        Reject(()=>a.FulfillReservation(hold,today,customer.Id,null,0,0,false,"",Sale(3),today),"Full fulfillment requires every reserved quantity");
        a.Invoice(true,today,other.Id,null,0,0,false,"Available sale",Sale(6));Check(s.AvailableQuantity(item,1)==0&&s.ReservedQuantity(item,1)==4000,"Unreserved quantities remain sellable");
        Reject(()=>a.FulfillReservation(hold,today,customer.Id,null,0,0,false,"",Sale(4),today.AddDays(-1)),"Invalid due date rolls back reservation fulfillment");Check(s.GetReservation(hold).EffectiveState=="Active"&&s.LocationQuantity(item,1)==4000,"Failed fulfillment preserves reservation and physical stock");
        long invoice=a.FulfillReservation(hold,today,customer.Id,null,0,0,false,"",Sale(4),today);Check(s.GetReservation(hold).Invoice==invoice&&s.ReservedQuantity(item,1)==0&&s.LocationQuantity(item,1)==0,"Fulfillment links one invoice and deducts stock exactly once");
        Reject(()=>a.FulfillReservation(hold,today,customer.Id,null,0,0,false,"",Sale(4),today),"Reservation cannot be fulfilled twice");a.Reverse(invoice,"Customer cancelled");Check(s.AvailableQuantity(item,1)==4000&&s.GetReservation(hold).EffectiveState=="Fulfilled","Invoice reversal restores stock without reactivating a fulfilled hold");
        long release=Hold(2);Reject(()=>a.ReleaseReservation(release,""),"Reservation release requires an audit reason");a.ReleaseReservation(release,"Customer cancelled order");Check(s.ReservedQuantity(item,1)==0&&s.GetReservation(release).State=="Released","Released reservation restores availability and preserves history");
        Reject(()=>a.CreateReservation(customer.Id,today.AddDays(-1),[new ReservationInput(item,1,1)],""),"New reservations cannot already be expired");long expired=Hold(1);s.Exec("UPDATE reservations SET expires=@p0 WHERE id=@p1",today.AddDays(-1).ToString("yyyy-MM-dd"),expired);Check(s.GetReservation(expired).EffectiveState=="Expired"&&s.ReservedQuantity(item,1)==0,"Expiry automatically releases availability without a scheduled job");Reject(()=>a.FulfillReservation(expired,today,customer.Id,null,0,0,false,"",Sale(1),today),"Expired reservations cannot fulfill");
        long device=s.Items()[1].Id;a.ConfigureInventoryItem(device,"RES-SERIAL",true,[]);a.OpeningStock(device,today,3,50,1,"RS1,RS2,RS3");
        long serialHold=a.CreateReservation(customer.Id,today.AddDays(3),[new ReservationInput(device,1,2,"RS1")],"Device order");
        Reject(()=>a.Invoice(true,today,other.Id,null,0,0,false,"",[new(device,"Device",1,100,0,1,"RS1")]),"Specific reserved serial cannot be sold to another customer");
        Reject(()=>a.TransferInventory(today,1,remote,device,1,"RS1","Transfer"),"Specific reserved serial cannot be moved away");
        Reject(()=>a.CreateReservation(other.Id,today,[new ReservationInput(device,1,1,"RS1")],""),"Specific serial cannot belong to two active reservations");
        a.Invoice(true,today,other.Id,null,0,0,false,"",[new(device,"Device",1,100,0,1,"RS2")]);
        Reject(()=>a.FulfillReservation(serialHold,today,customer.Id,null,0,0,false,"",[new(device,"Device",2,100,0,1,"RS2,RS3")],today),"Fulfillment must include the specifically reserved device");
        a.FulfillReservation(serialHold,today,customer.Id,null,0,0,false,"",[new(device,"Device",2,100,0,1,"RS1,RS3")],today);Check(s.AvailableSerials(device,1).Count==0,"Quantity reservation with optional fixed serials fulfills remaining serials at sale");
        long quote=a.SaveQuote(null,0,customer.Id,today,today.AddDays(7),0,false,"",Sale(2));Reject(()=>a.CreateReservation(customer.Id,today,[new ReservationInput(item,1,2)],"",quote),"Quote reservations require customer acceptance");a.SetQuoteState(quote,1,"Accepted");long quoteHold=a.CreateReservation(customer.Id,today.AddDays(2),[new ReservationInput(item,1,2)],"",quote);
        Reject(()=>a.SaveQuote(quote,2,customer.Id,today,today.AddDays(7),0,false,"",Sale(3)),"An actively reserved quote cannot be changed before release");Reject(()=>a.SetQuoteState(quote,2,"Declined"),"An actively reserved quote cannot be declined before release");
        long quoteInvoice=a.ConvertQuote(quote,2,today,customer.Id,null,0,0,false,"",Sale(2),today);Check(s.GetReservation(quoteHold).Invoice==quoteInvoice&&s.GetQuote(quote).Invoice==quoteInvoice&&s.LocationQuantity(item,1)==2000,"Accepted quote conversion atomically fulfills its linked reservation");
        long remaining=Hold(1);a.SetReorderRule(item,1,2,5);Check(a.ReorderSuggestions().Single(r=>r.Item==item).Suggested==4000,"Replenishment accounts for reserved demand");
        string backup=Path.Combine(directory,"reservations.hisab");s.PortableBackup(backup,"reservation-backup-password");using var restored=new Store(Path.Combine(directory,"reservations-restored.db"));restored.RestorePortable(backup,"reservation-backup-password");Check(restored.ReservedQuantity(item,1)==1000&&restored.GetReservation(quoteHold).Invoice==quoteInvoice&&restored.Table("PRAGMA foreign_key_check").Rows.Count==0,"Reservations and fulfillment links survive portable restore");
        s.Set("allow_negative","1");Reject(()=>a.Invoice(true,today,other.Id,null,0,0,false,"",Sale(5)),"Negative-stock preference cannot bypass active reservations");s.Set("allow_negative","0");
        Reject(()=>a.CreateReservation(customer.Id,today,[new ReservationInput(item,1,0)],""),"Zero reservation quantity is rejected");
        Reject(()=>a.CreateReservation(customer.Id,today,[new ReservationInput(item,1,1),new ReservationInput(item,1,1)],""),"Duplicate reservation item/location lines are rejected");
        long sameDay=a.CreateReservation(other.Id,today,[new ReservationInput(item,1,1)],"Same-day collection");Check(s.GetReservation(sameDay).EffectiveState=="Active"&&s.AvailableQuantity(item,1)==0,"Reservation stays active through its expiry day");a.ReleaseReservation(sameDay,"Not collecting");
        long lockedCount=a.StartStockCount(1,[item]);Reject(()=>Hold(1),"A new reservation cannot use stock currently being counted");a.CancelStockCount(lockedCount);
        long purchaseItem=s.Items()[2].Id;var supplier=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(supplier.Id,supplier.Code,supplier.Name,"Supplier");long purchase=a.Invoice(false,today,supplier.Id,null,0,0,false,"",[new(purchaseItem,"Supplier device",3,50,0)]);long purchaseLine=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",purchase));a.CreateReservation(customer.Id,today,[new ReservationInput(purchaseItem,1,2)],"");Reject(()=>a.ReturnInvoice(purchase,today,[new(purchaseLine,2)],"Supplier return"),"Supplier returns cannot consume held stock");
        long secondQuote=a.SaveQuote(null,0,customer.Id,today,today.AddDays(2),0,false,"",[new(purchaseItem,"Supplier device",1,100,0)]);a.SetQuoteState(secondQuote,1,"Accepted");long secondHold=a.CreateReservation(customer.Id,today,[new ReservationInput(purchaseItem,1,1)],"",secondQuote);
        Reject(()=>a.ConvertQuote(secondQuote,2,today,customer.Id,null,0,0,false,"",[new(purchaseItem,"Supplier device",1,100,0)],today.AddDays(-1)),"Failed quote fulfillment rolls back due-date validation");Check(s.GetQuote(secondQuote).Invoice==null&&s.GetReservation(secondHold).EffectiveState=="Active"&&s.ReservedQuantity(purchaseItem,1)==3000,"Failed quote fulfillment preserves both hold and quotation");
        s.SaveUser("reservation-viewer","reservation-viewer-password","Viewer");s.Login("reservation-viewer","reservation-viewer-password");Reject(()=>Hold(1),"Viewer cannot reserve stock");Reject(()=>a.ReleaseReservation(remaining,"Release"),"Viewer cannot release reservations");
    }
}
