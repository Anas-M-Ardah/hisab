using System.IO;
namespace Hisab;
public static class InventoryReconciliationTests
{
    public static void Run(string directory,List<string> results)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
        using var s=new Store(Path.Combine(directory,"historical-stock-"+Guid.NewGuid()+".db"));var a=new AccountingService(s);var day=DateTime.Today.AddDays(-30);
        var supplier=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(supplier.Id,supplier.Code,supplier.Name,"Supplier");var customer=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(customer.Id,customer.Code,customer.Name,"Customer");long item=s.Items()[0].Id,remote=s.SaveLocation(null,"Remote");
        long po=a.CreatePurchaseOrder(supplier.Id,remote,day,[new(item,10,20)]);a.IssuePurchaseOrder(po);long orderLine=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",po));long gr=a.ReceivePurchaseOrder(po,day,[new(orderLine,10,"")]);long line=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",gr));
        Check(a.InventoryAsOf(day.AddDays(-1)).StockValue==0&&a.ReceiptAccruals(day.AddDays(-1)).PendingValue==0,"Historical reports exclude receipts after cutoff");
        var stock=a.InventoryAsOf(day);Check(stock.StockValue==200000&&stock.Difference==0&&stock.Rows.Single().Location==remote&&stock.Rows.Single().Quantity==10000,"Historical receipt stock reconciles quantity, location and GL");
        var accrual=a.ReceiptAccruals(day);Check(accrual.PendingValue==200000&&accrual.Difference==0&&accrual.Rows.Single().Quantity==10000,"Unbilled receipt basis reconciles to GRNI");
        a.TransferInventory(day.AddDays(1),remote,1,item,4,"","move");stock=a.InventoryAsOf(day.AddDays(1));Check(stock.Rows.Single(r=>r.Location==remote).Value==120000&&stock.Rows.Single(r=>r.Location==1).Value==80000&&stock.Difference==0,"Historical transfers preserve company value and allocate location value");
        long sale=a.Invoice(true,day.AddDays(2),customer.Id,null,0,0,false,"sale",[new(item,"device",2,30,0,remote)]);stock=a.InventoryAsOf(day.AddDays(2));Check(stock.StockValue==160000&&stock.Rows.Single(r=>r.Location==remote).Quantity==4000&&stock.Difference==0,"Historical sale cutoff reflects cost and original location");
        a.Reverse(sale,"cancel sale",day.AddDays(3));stock=a.InventoryAsOf(day.AddDays(3));Check(stock.StockValue==200000&&stock.Rows.Single(r=>r.Location==remote).Quantity==6000&&stock.Difference==0,"Historical reversal restores original remote location despite legacy reversal moves");
        Check(a.InventoryAsOf(day.AddDays(2)).StockValue==160000,"Later reversal leaves earlier stock cutoff intact");
        long bill=a.BillReceipts(supplier.Id,day.AddDays(4),[new(line,3,20)],16);accrual=a.ReceiptAccruals(day.AddDays(4));Check(accrual.PendingValue==140000&&accrual.Rows.Single().Quantity==7000&&accrual.Difference==0,"Partial billing clears receipt basis without including invoice tax");
        long returned=a.ReturnInvoice(gr,day.AddDays(5),[new(line,2)],"return unbilled goods");accrual=a.ReceiptAccruals(day.AddDays(5));Check(accrual.PendingValue==100000&&accrual.Rows.Single().Quantity==5000&&accrual.Difference==0,"Unbilled goods returns reduce quantity and GRNI basis");
        a.Reverse(returned,"cancel return",day.AddDays(6));Check(a.ReceiptAccruals(day.AddDays(6)).PendingValue==140000&&a.ReceiptAccruals(day.AddDays(5)).PendingValue==100000,"Return reversal affects accrual only from reversal date");
        a.Reverse(bill,"cancel bill",day.AddDays(7));Check(a.ReceiptAccruals(day.AddDays(7)).PendingValue==200000&&a.ReceiptAccruals(day.AddDays(4)).PendingValue==140000&&a.ReceiptAccruals(day.AddDays(7)).Difference==0,"Bill reversal restores unbilled basis at its effective date");
        a.CloseYear(day,day.AddDays(8));a.BillReceipts(supplier.Id,day.AddDays(9),[new(line,10,22,20)],16);
        Check(a.ReceiptAccruals(day.AddDays(9)).PendingValue==0&&a.ReceiptAccruals(day.AddDays(9)).Difference==0&&a.ReceiptAccruals(day.AddDays(8)).PendingValue==200000,"Late price variance clears original GRNI without rewriting closed accrual report");
        Check(a.InventoryAsOf(day.AddDays(8)).StockValue==200000&&a.InventoryAsOf(day.AddDays(9)).StockValue==220000&&a.InventoryAsOf(day.AddDays(9)).Difference==0,"Current price allocation changes stock only after closed cutoff");
        long damaged=a.Manual(day.AddDays(10),"synthetic imported discrepancy",[new(s.AccountId("1101"),1000,0),new(s.AccountId("3201"),0,1000)]);s.Exec("UPDATE entries SET account=@p0 WHERE doc=@p1 AND debit>0",s.AccountId("1301"),damaged);Check(a.InventoryAsOf(day.AddDays(10)).Difference==-1000,"Direct inventory ledger posting is exposed as reconciliation difference");
        using var negative=new Store(Path.Combine(directory,"offset-stock-"+Guid.NewGuid()+".db"));var na=new AccountingService(negative);negative.Set("allow_negative","1");
        var ns=negative.Accounts().First(x=>x.Kind=="Unclassified");negative.SaveAccount(ns.Id,ns.Code,ns.Name,"Supplier");var nc=negative.Accounts().First(x=>x.Kind=="Unclassified");negative.SaveAccount(nc.Id,nc.Code,nc.Name,"Customer");long ni=negative.Items()[0].Id,nl=negative.SaveLocation(null,"Secondary");
        na.Invoice(false,day,ns.Id,null,0,0,false,"buy",[new(ni,"device",1,20,0)]);na.TransferInventory(day,1,nl,ni,1,"","move");na.Invoice(true,day,nc.Id,null,0,0,false,"negative sale",[new(ni,"device",1,30,0)]);
        var offset=na.InventoryAsOf(day);Check(offset.Rows.Any(r=>r.Location==1&&r.Quantity==-1000)&&offset.Rows.Any(r=>r.Location==nl&&r.Quantity==1000)&&offset.Rows.Sum(r=>r.Value)==offset.StockValue&&offset.Difference==0,"Zero company quantity retains opposing warehouse balances");
    }
}
