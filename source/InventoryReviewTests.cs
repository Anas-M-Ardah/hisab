using System.IO;
using Microsoft.Data.Sqlite;
namespace Hisab;
public static class InventoryReviewTests
{
    public static void Run(string directory,List<string> results)
    {
        bool failed=false;
        void Case(string name,Action test){try{test();results.Add("PASS "+name);}catch(Exception ex){failed=true;results.Add("FAIL "+name+": "+ex.Message);}}
        void Fixture(Action<Store,AccountingService,long,long,long> test){using var s=new Store(Path.Combine(directory,"review-"+Guid.NewGuid()+".db"));var a=new AccountingService(s);var sp=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(sp.Id,sp.Code,sp.Name,"Supplier");var cu=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(cu.Id,cu.Code,cu.Name,"Customer");test(s,a,sp.Id,cu.Id,s.Items()[0].Id);}
        Case("Populated legacy location migration preserves stock and journals",()=>{
            string path=Path.Combine(directory,"legacy-populated-"+Guid.NewGuid()+".db");long item;
            using(var s=new Store(path)){var a=new AccountingService(s);var sp=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(sp.Id,sp.Code,sp.Name,"Supplier");item=s.Items()[0].Id;a.Invoice(false,DateTime.Today,sp.Id,null,0,0,false,"Legacy stock",[new(item,"Legacy",3,100,0)]);}
            using(var raw=new SqliteConnection("Data Source="+path)){raw.Open();using var cmd=raw.CreateCommand();cmd.CommandText="""
            PRAGMA foreign_keys=OFF;
            DROP TABLE serial_events; DROP TABLE serial_units; DROP TABLE stock_count_lines; DROP TABLE stock_counts; DROP TABLE inventory_adjustments;
            DROP TABLE landed_sources; DROP TABLE landed_allocations; DROP TABLE receipt_bills; DROP TABLE receipt_links; DROP TABLE purchase_order_lines; DROP TABLE purchase_orders; DROP TABLE reorder_rules;
            ALTER TABLE invoice_lines DROP COLUMN location; ALTER TABLE stock_moves DROP COLUMN location; DROP TABLE locations;
            DROP INDEX ix_item_barcode; ALTER TABLE items DROP COLUMN barcode; ALTER TABLE items DROP COLUMN serialized; ALTER TABLE items DROP COLUMN serial_start;
            PRAGMA user_version=3;
            """;cmd.ExecuteNonQuery();}
            using var migrated=new Store(path);if(migrated.LocationQuantity(item,1)!=3000||migrated.Balance(migrated.AccountId("1301"))!=300000||Convert.ToInt64(migrated.Scalar("SELECT count(*) FROM documents"))!=1||Convert.ToInt64(migrated.Scalar("PRAGMA foreign_keys"))!=1||migrated.Table("PRAGMA foreign_key_check").Rows.Count!=0)throw new Exception("Migration changed original state or failed to restore foreign keys");
            new AccountingService(migrated).RebuildInventory("migration test");if(migrated.Items().Single(i=>i.Id==item).Value!=300000)throw new Exception("Migrated replay changed value");
        });
        Case("Legacy sale returns accept an audited newly identified serial",()=>Fixture((s,a,sp,cu,item)=>{
            a.Invoice(false,DateTime.Today,sp,null,0,0,false,"Legacy purchase",[new(item,"Device",2,100,0)]);long sale=a.Invoice(true,DateTime.Today,cu,null,0,0,false,"Legacy sale",[new(item,"Device",1,150,0)]);long sl=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",sale));a.ConfigureInventoryItem(item,"",true,new[]{"REMAINING"});a.ReturnInvoice(sale,DateTime.Today,[new(sl,1,"RETURNED-LEGACY")],"Identified customer return");if(s.AvailableSerials(item,1).Count!=2||Convert.ToInt64(s.Scalar("SELECT count(*) FROM audit WHERE action='legacy-return-serial'"))!=1)throw new Exception("Legacy return identity or audit missing");
        }));
        Case("Partially billed receipts permit returning the unbilled remainder",()=>Fixture((s,a,sp,cu,item)=>{
            long po=a.CreatePurchaseOrder(sp,1,DateTime.Today,new[]{new PurchaseOrderLine(item,10,100)});a.IssuePurchaseOrder(po);long pol=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",po));long gr=a.ReceivePurchaseOrder(po,DateTime.Today,new[]{new ReceiptInput(pol,10,"")});long rl=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",gr));a.BillReceipts(sp,DateTime.Today,new[]{new BillInput(rl,2,100)},0);a.ReturnInvoice(gr,DateTime.Today,[new(rl,1)],"Unbilled return");if(a.UnbilledQuantity(rl)!=7000||s.LocationQuantity(item,1)!=9000||s.Balance(s.AccountId("2195"))!=-700000)throw new Exception("Unbilled return changed billed quantity or accrual incorrectly");
        }));
        Case("Final partial bill clears every thousandth of receipt accrual",()=>Fixture((s,a,sp,cu,item)=>{
            long po=a.CreatePurchaseOrder(sp,1,DateTime.Today,new[]{new PurchaseOrderLine(item,.003m,.5m)});a.IssuePurchaseOrder(po);long pol=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",po));long gr=a.ReceivePurchaseOrder(po,DateTime.Today,new[]{new ReceiptInput(pol,.003m,"")});long rl=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",gr));for(int i=0;i<3;i++)a.BillReceipts(sp,DateTime.Today,new[]{new BillInput(rl,.001m,.5m)},0);if(s.Balance(s.AccountId("2195"))!=0||s.Items().Single(i=>i.Id==item).Value!=3||s.Balance(sp)!=-3)throw new Exception("Partial billing left a rounding balance or lost inventory cost");
        }));
        Case("Reversed landed allocation releases its source charge for correction",()=>Fixture((s,a,sp,cu,item)=>{
            long pi=a.Invoice(false,DateTime.Today,sp,null,0,0,false,"Receipt",[new(item,"Device",2,100,0)]);long line=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",pi));long source=a.Voucher(false,DateTime.Today,s.AccountId("2196"),s.AccountId("1101"),20,"Freight");long lc=a.AddLandedCost(DateTime.Today,sp,null,20,new[]{new CostAllocation(line,20)},"Allocate freight",source);a.Reverse(lc,"Correct allocation");a.AddLandedCost(DateTime.Today,sp,null,20,new[]{new CostAllocation(line,20)},"Corrected allocation",source);if(s.Items().Single(i=>i.Id==item).Value!=220000||s.Balance(s.AccountId("1101"))!=-20000||Convert.ToInt64(s.Scalar("SELECT count(*) FROM landed_sources WHERE source=@p0",source))!=2)throw new Exception("Correction duplicated payment or lost allocation history");
        }));

        Case("Reversing replaced goods return cannot overreceive the order",()=>Fixture((s,a,sp,cu,item)=>{
            long po=a.CreatePurchaseOrder(sp,1,DateTime.Today,new[]{new PurchaseOrderLine(item,10,100)});a.IssuePurchaseOrder(po);long pol=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",po));long gr=a.ReceivePurchaseOrder(po,DateTime.Today,new[]{new ReceiptInput(pol,10,"")});long rl=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",gr));long ret=a.ReturnInvoice(gr,DateTime.Today,[new(rl,5)],"Return five");a.ReceivePurchaseOrder(po,DateTime.Today,new[]{new ReceiptInput(pol,5,"")});bool blocked=false;try{a.Reverse(ret,"Undo old return");}catch(InvalidOperationException){blocked=true;}if(!blocked||a.ReceivedQuantity(pol)!=10000||s.LocationQuantity(item,1)!=10000)throw new Exception("Return reversal bypassed order quantity or failed atomic rollback");
        }));
        if(failed)throw new Exception("Inventory review regression failures; see preceding results");
    }
}
