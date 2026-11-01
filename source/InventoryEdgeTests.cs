using System.IO;
namespace Hisab;
public static class InventoryEdgeTests
{
    public static void Run(string directory,List<string> results)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action action,string name){try{action();}catch(InvalidOperationException){results.Add("PASS "+name);return;}throw new Exception("Did not reject: "+name);}
        using var s=new Store(Path.Combine(directory,"inventory-edge-"+Guid.NewGuid()+".db"));var a=new AccountingService(s);var today=DateTime.Today;
        var supplier=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(supplier.Id,supplier.Code,supplier.Name,"Supplier");
        var customer=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(customer.Id,customer.Code,customer.Name,"Customer");long item=s.Items()[0].Id;
        a.Invoice(false,today.AddDays(-2),supplier.Id,null,0,0,false,"Before count",[new(item,"Device",4,100,0)]);
        long count=a.StartStockCount(1,new[]{item});a.RecordStockCount(count,item,4,"",100);a.CompleteStockCount(count,"No discrepancy");
        long sale=a.Invoice(true,today,customer.Id,null,0,0,false,"After count",[new(item,"Device",1,150,0)]);
        Check(s.LocationQuantity(item,1)==3000,"Completed count permits later same-day sales");
        a.Reverse(sale,"Correct later sale");Check(s.LocationQuantity(item,1)==4000,"Completed count permits reversal of later same-day stock");
        Reject(()=>a.Invoice(false,today.AddDays(-1),supplier.Id,null,0,0,false,"Backdate across count",[new(item,"Device",1,100,0)]),"Completed count rejects earlier-dated inventory changes");
        count=a.StartStockCount(1,new[]{item});
        using(var reopened=new Store(s.Path))Reject(()=>new AccountingService(reopened).Invoice(true,today,customer.Id,null,0,0,false,"Locked after restart",[new(item,"Device",1,150,0)]),"Count locks survive reopening");
        a.CancelStockCount(count);a.Invoice(true,today,customer.Id,null,0,0,false,"Unlocked",[new(item,"Device",1,150,0)]);
        Check(s.LocationQuantity(item,1)==3000,"Cancelled count releases movement locks");
        long serialized=s.Items()[1].Id;a.ConfigureInventoryItem(serialized," edge ",true,Array.Empty<string>());
        Reject(()=>s.SaveItem(serialized,s.Items()[1].Code,"Device",false,0),"Serialized item cannot be changed into a service");
        Reject(()=>a.ConfigureInventoryItem(item,"EDGE",false,Array.Empty<string>()),"Barcode matching ignores case and surrounding whitespace");
        s.Set("allow_negative","1");Reject(()=>a.Invoice(true,today,customer.Id,null,0,0,false,"Negative serial",[new(serialized,"Device",1,150,0,1,"ABSENT")]),"Negative-stock setting cannot bypass serial availability");s.Set("allow_negative","0");
        long order=a.CreatePurchaseOrder(supplier.Id,1,today.AddDays(-1),new[]{new PurchaseOrderLine(serialized,3,100)});long ol=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",order));
        Reject(()=>a.ReceivePurchaseOrder(order,today,new[]{new ReceiptInput(ol,1,"Q1")}),"Draft order cannot receive stock");a.IssuePurchaseOrder(order);
        Reject(()=>a.ReceivePurchaseOrder(order,today,new[]{new ReceiptInput(ol,4,"Q1,Q2,Q3,Q4")}),"Overreceiving requires an order amendment");
        long gr=a.ReceivePurchaseOrder(order,today,new[]{new ReceiptInput(ol,2,"Q1,Q2")});long rl=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",gr));
        long gu=a.ReturnInvoice(gr,today,[new(rl,1,"Q2")],"Unbilled faulty unit");Check(s.LocationQuantity(serialized,1)==1000&&a.ReceivedQuantity(ol)==1000,"Unbilled goods return reduces stock and outstanding receipt count");
        a.Reverse(gu,"Reinstate receipt");long bill=a.BillReceipts(supplier.Id,today,new[]{new BillInput(rl,2,90)},16);
        Reject(()=>a.BillReceipts(supplier.Id,today,new[]{new BillInput(rl,1,90)},16),"Duplicate billing cannot exceed received quantity");
        Check(s.Items().Single(i=>i.Id==serialized).Value==180000,"Lower supplier price reduces receipt valuation");
        Reject(()=>a.Reverse(gr,"Still billed"),"Billed receipt reversal explains its dependency");
        long bl=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",bill));a.ReturnInvoice(bill,today,[new(bl,1,"Q1")],"Supplier credit return");
        Check(s.AvailableSerials(serialized,1).Single()=="Q2","Billed purchase return checks receipt serials and removes the selected unit");
        a.SetReorderRule(serialized,1,3,6);var suggestion=a.ReorderSuggestions().Single();Check(suggestion.OnOrder==1000&&suggestion.Overdue&&suggestion.Suggested==4000,"Overdue incoming stock is visible separately from on-hand stock");
        long plain=s.Items()[2].Id;long pi=a.Invoice(false,today.AddDays(-5),supplier.Id,null,0,0,false,"Old receipt",[new(plain,"Device",2,100,0)]);long pl=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",pi));
        var shares=a.AllocateLandedCost(.001m,new[]{rl,pl},false);Check(shares.Sum(x=>x.Amount)==.001m&&shares.All(x=>x.Amount>=0),"Landed-cost rounding allocates every thousandth exactly");
        long source=a.Voucher(false,today,s.AccountId("2196"),s.AccountId("1101"),20,"Freight paid once");long cashBefore=s.Balance(s.AccountId("1101"));a.AddLandedCost(today,supplier.Id,null,20,new[]{new CostAllocation(pl,20)},"Reuse freight",source);
        Check(s.Balance(s.AccountId("1101"))==cashBefore&&s.Items().Single(i=>i.Id==plain).Value==220000,"Existing freight payment is capitalized without a second payable or cash payment");
        Reject(()=>a.AddLandedCost(today,supplier.Id,null,20,new[]{new CostAllocation(pl,20)},"Duplicate freight",source),"Existing charge cannot be capitalized twice");
        a.CloseYear(today.AddDays(-10),today.AddDays(-3));Reject(()=>a.AddLandedCost(today,supplier.Id,null,10,new[]{new CostAllocation(pl,10)},"Closed receipt"),"Late freight cannot rewrite a closed period");
        Check(s.Balance(s.AccountId("1301"))==s.Items().Sum(i=>i.Value),"Complex purchase and return workflow reconciles stock valuation");
        Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM (SELECT doc FROM entries GROUP BY doc HAVING sum(debit)<>sum(credit))"))==0,"All inventory edge-case journals balance");
        s.PortableBackup(Path.Combine(directory,"inventory-portable.hisab"),"Inventory-test-password");
        using var restored=new Store(Path.Combine(directory,"restored-inventory-"+Guid.NewGuid()+".hdb"),true);restored.RestorePortable(Path.Combine(directory,"inventory-portable.hisab"),"Inventory-test-password");
        Check(restored.LocationQuantity(serialized,1)==1000&&restored.AvailableSerials(serialized,1).Single()=="Q2"&&restored.Table("PRAGMA foreign_key_check").Rows.Count==0,"Portable restore preserves devices, locations and purchasing relations");
        long openingItem=s.Items()[3].Id;long remote=s.SaveLocation(null,"Opening warehouse");a.ConfigureInventoryItem(openingItem,"OPEN",true,Array.Empty<string>());a.OpeningStock(openingItem,today,2,50,remote,"O1,O2");Check(s.LocationQuantity(openingItem,remote)==2000&&s.AvailableSerials(openingItem,remote).Count==2,"Serialized opening stock respects the chosen location");
        s.SaveUser("inventory-viewer","a-strong-test-password","Viewer");s.Login("inventory-viewer","a-strong-test-password");Reject(()=>a.CreatePurchaseOrder(supplier.Id,1,today,new[]{new PurchaseOrderLine(item,1,1)}),"Viewer cannot create purchase orders");Reject(()=>a.StartStockCount(1,new[]{item}),"Viewer cannot lock stock using a count");
    }
}

