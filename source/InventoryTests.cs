using System.IO;
namespace Hisab;

public static class InventoryTests
{
    public static void Run(string directory,List<string> results)
    {
        using var s=new Store(Path.Combine(directory,"inventory-"+Guid.NewGuid()+".db"));
        if(Convert.ToInt64(s.Scalar("SELECT count(*) FROM sqlite_master WHERE name='locations'"))!=1)
            throw new Exception("Inventory migration must create a default stock location");
        results.Add("PASS inventory migration creates locations");
        void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action action,string name){try{action();}catch(InvalidOperationException){results.Add("PASS "+name);return;}throw new Exception("Did not reject: "+name);}
        var a=new AccountingService(s);var today=DateTime.Today;
        var supplier=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(supplier.Id,supplier.Code,supplier.Name,"Supplier");
        var customer=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(customer.Id,customer.Code,customer.Name,"Customer");
        long item=s.Items()[0].Id;long other=s.SaveLocation(null,"Warehouse");
        a.ConfigureInventoryItem(item,"ABC",true,Array.Empty<string>());
        long purchase=a.Invoice(false,today,supplier.Id,null,0,0,false,"Serialized purchase",[new(item,"Device",2,100,0,1,"S1,S2")]);
        Check(s.LocationQuantity(item,1)==2000&&s.AvailableSerials(item,1).Count==2,"Serial receipt reconciles units and quantities");
        Reject(()=>a.Invoice(false,today,supplier.Id,null,0,0,false,"Duplicate",[new(item,"Device",1,100,0,1,"S1")]),"Duplicate serial rolls back receipt");
        a.TransferInventory(today,1,other,item,1,"S1","Move");
        Check(s.LocationQuantity(item,other)==1000&&s.Items().First(x=>x.Id==item).Value==200000,"Transfer preserves company valuation");
        Reject(()=>a.Invoice(true,today,customer.Id,null,0,0,false,"Wrong location",[new(item,"Device",1,150,0,1,"S1")]),"Cannot sell serial from another location");
        long sale=a.Invoice(true,today,customer.Id,null,0,0,false,"Sale",[new(item,"Device",1,150,0,other,"S1")]);
        long line=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",sale));
        a.ReturnInvoice(sale,today,[new(line,1,"S1")],"Returned");
        Check(s.AvailableSerials(item,other).Single()=="S1","Customer return restores original serial at location");
        Reject(()=>a.Reverse(purchase,"Dependent stock"),"Receipt reversal cannot orphan serial history");
        Check(s.Balance(s.AccountId("1301"))==200000,"Inventory accounting matches transferred returned units");
        s.Backup(Path.Combine(directory,"inventory-backup.db"));
        Check(s.Table("PRAGMA foreign_key_check").Rows.Count==0,"Inventory foreign keys hold");
        CountChecks(s,a,item,other,results);
        PurchasingChecks(s,a,supplier.Id,customer.Id,results);InventoryEdgeTests.Run(directory,results);

    }
    static void CountChecks(Store s,AccountingService a,long item,long location,List<string> results)
    {
        // Dynamic boundary permits a meaningful failing test before the service exists.
        dynamic service=a;
        long count=service.StartStockCount(location,new long[]{item});
        try{a.TransferInventory(DateTime.Today,location,1,item,1,"S1","Blocked count");throw new Exception("Active count did not block stock movement");}catch(InvalidOperationException){results.Add("PASS active count blocks stock movements");}
        service.RecordStockCount(count,item,0m,"",100m);
        long adjustment=service.CompleteStockCount(count,"Missing device");
        if(s.LocationQuantity(item,location)!=0||s.Balance(s.AccountId("1301"))!=100000)throw new Exception("Stock count loss must reduce inventory and value");
        results.Add("PASS count loss reconciles serials and valuation");
        a.Reverse(adjustment,"Recount");
        if(s.LocationQuantity(item,location)!=1000)throw new Exception("Count reversal must restore missing stock");
        results.Add("PASS count reversal restores serial stock");
    }
    static void PurchasingChecks(Store s,AccountingService a,long supplier,long customer,List<string> results)
    {
        long item=s.Items()[1].Id;var date=DateTime.Today;
        long order=a.CreatePurchaseOrder(supplier,1,date.AddDays(2),new PurchaseOrderLine[]{new(item,10,100)});
        if(s.LocationQuantity(item,1)!=0)throw new Exception("Draft order must not receive goods");
        a.IssuePurchaseOrder(order);
        long orderLine=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",order));
        long receipt=a.ReceivePurchaseOrder(order,date,new ReceiptInput[]{new(orderLine,4,"")});
        if(s.LocationQuantity(item,1)!=4000||s.Balance(s.AccountId("2195"))!=-400000)throw new Exception("Receipt must accrue unbilled goods");
        results.Add("PASS partial goods receipt accrues liability without supplier invoice");
        long receiptLine=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",receipt));
        long bill=a.BillReceipts(supplier,date,new BillInput[]{new(receiptLine,2,120)},0);
        if(s.LocationQuantity(item,1)!=4000||s.Items().Single(i=>i.Id==item).Value!=440000||s.Balance(s.AccountId("2195"))!=-200000)throw new Exception("Partial bill must clear accrual and revalue once");
        results.Add("PASS partial billing changes cost without receiving stock twice");
        a.Invoice(true,date,customer,null,0,0,false,"Sale",[new(item,"Device",2,200,0)]);
        a.AddLandedCost(date,supplier,null,40,new CostAllocation[]{new(receiptLine,40)},"Freight");
        if(s.Items().Single(i=>i.Id==item).Value!=240000)throw new Exception("Freight must allocate to sold and unsold units");
        if(s.Balance(s.AccountId("1301"))!=s.Items().Sum(i=>i.Value))throw new Exception("Landed costs must reconcile inventory ledger");
        results.Add("PASS late freight replays sold and unsold inventory costs");
        a.SetReorderRule(item,1,3,12);
        var suggestion=a.ReorderSuggestions().Single(x=>x.Item==item);
        if(suggestion.OnHand!=2000||suggestion.OnOrder!=6000||suggestion.Suggested!=0)throw new Exception("On-order stock must suppress unnecessary reorder");
        a.CancelPurchaseOrder(order);
        suggestion=a.ReorderSuggestions().Single(x=>x.Item==item);
        if(suggestion.Suggested!=10000)throw new Exception("Cancelled outstanding order must release replenishment demand");
        results.Add("PASS replenishment accounts for open and cancelled orders");
    }

}

