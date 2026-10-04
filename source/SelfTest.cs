using System.IO;
using System.Text.Json;

namespace Hisab;
public static class SelfTest
{
    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var results=new List<string>();
        void Check(bool condition,string name){if(!condition)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action action,string name){try{action();}catch(InvalidOperationException){results.Add("PASS "+name);return;}throw new Exception("Did not reject: "+name);}
        try {
            string path=Path.Combine(directory,"test-"+Guid.NewGuid()+".db");using var s=new Store(path);var accounting=new AccountingService(s);var today=DateTime.Today;
            Check(s.Items().Count==17,"17 source item names imported");Check(s.Accounts().All(a=>!a.Name.Contains("ChatGPT")),"Source chat text not imported");Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==0,"Sample invoice not posted");
            var customer=s.Accounts().First(a=>a.Kind=="Unclassified");s.SaveAccount(customer.Id,customer.Code,customer.Name,"Customer");var supplier=s.Accounts().First(a=>a.Kind=="Unclassified");s.SaveAccount(supplier.Id,supplier.Code,supplier.Name,"Supplier");var item=s.Items()[0];var cash=s.AccountId("1101");
            Check(Store.Parse("١٢٥٠٫٥٠٠")==1250.5m,"Arabic numeral parsing");
            var purchase=accounting.Invoice(false,today,supplier.Id,null,0,16,false,"Purchase",[new(item.Id,"Goods",10,100,0)]);
            Check(s.Items()[0].Qty==10000&&s.Items()[0].Value==1000000,"Purchase increases stock and value");
            var purchase2=accounting.Invoice(false,today,supplier.Id,cash,232,16,false,"Purchase",[new(item.Id,"Goods",2,100,0)]);
            Check(s.Balance(supplier.Id)==-1160000,"Cash purchase does not create supplier liability");
            var sale=accounting.Invoice(true,today,customer.Id,cash,80,16,false,"Sale",[new(item.Id,"Goods",3,150,0)]);
            Check(s.Items()[0].Qty==9000&&s.Items()[0].Value==900000,"Sale deducts stock at weighted average");Check(s.Balance(customer.Id)==442000,"Part-paid invoice posts remaining customer balance");
            var receipt=accounting.Voucher(true,today,customer.Id,cash,442,"Collection");Check(s.Balance(customer.Id)==0,"Receipt clears customer balance");
            var payment=accounting.Voucher(false,today,supplier.Id,cash,1160,"Supplier payment");Check(s.Balance(supplier.Id)==0,"Payment clears supplier balance");
            var sum=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));var qty=s.Items()[0].Qty;var val=s.Items()[0].Value;
            Reject(()=>accounting.Invoice(true,today,customer.Id,null,0,16,false,"Oversell",[new(item.Id,"Goods",20,100,0)]),"Overselling rejected");Check(sum==Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))&&s.Items()[0].Qty==qty,"Failed invoice rolls back number, document and stock");
            Reject(()=>accounting.Invoice(true,today,customer.Id,null,0,16,false,"Repeated",[new(item.Id,"Goods",5,100,0),new(item.Id,"Goods",5,100,0)]),"Repeated-item oversell rejected");Check(s.Items()[0].Qty==qty&&s.Items()[0].Value==val,"Multi-line invoice failure fully rolls back");
            Reject(()=>accounting.Manual(today,"Unbalanced",[new(cash,10000,0),new(customer.Id,0,9000)]),"Unbalanced journal rejected");Check(sum==Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents")),"Unbalanced journal rolls back document");
            Reject(()=>accounting.Invoice(true,today.AddDays(-1),customer.Id,null,0,16,false,"Backdated",[new(item.Id,"Goods",1,100,0)]),"Backdated stock transaction rejected");
            Reject(()=>accounting.Voucher(true,today,cash,cash,10,"Self transfer"),"Same cash and counterpart rejected");
            Reject(()=>accounting.Voucher(true,today,customer.Id,cash,10.0001m,"Precision"),"Money beyond 3 decimals rejected");
            Reject(()=>s.SaveAccount(customer.Id,customer.Code,customer.Name,"Supplier"),"Used account cannot be reclassified");
            var inclusive=AccountingService.Calculate([new(item.Id,"Tax inclusive",1,1300,0)],16,true);Check(inclusive.Total==1300000&&inclusive.Net==1120690&&inclusive.Tax==179310,"Inclusive tax uses 3-decimal JOD rounding");
            var discount=AccountingService.Calculate([new(item.Id,"Discount",2,100,10)],16,false);Check(discount.Net==190000&&discount.Tax==30400,"Discount applied before tax");
            var rv=accounting.Reverse(sale,"Correction");Check(s.Items()[0].Qty==12000&&s.Items()[0].Value==1200000,"Invoice reversal restores stock and cost");Check(s.Balance(customer.Id)==-442000,"Reversal preserves later receipts as customer credit");Reject(()=>accounting.Reverse(sale,"Again"),"Duplicate reversal rejected");
            var item2=s.Items()[1];accounting.OpeningStock(item2.Id,today,10,200);Check(s.Items()[1].Value==2000000,"Opening stock posts value");Reject(()=>accounting.OpeningStock(item2.Id,today,1,200),"Duplicate opening stock rejected");
            var weighted=accounting.Invoice(false,today,supplier.Id,null,0,0,false,"Weighted",[new(item2.Id,"Goods",10,300,0)]);accounting.Invoice(true,today,customer.Id,null,0,0,false,"Weighted sale",[new(item2.Id,"Goods",5,400,0)]);Check(s.Items()[1].Qty==15000&&s.Items()[1].Value==3750000,"Weighted average across different purchase costs");accounting.Reverse(weighted,"Correct older purchase");Check(s.Items()[1].Qty==5000&&s.Items()[1].Value==1000000,"Cancelling older purchase recalculates later sale costs");
            var service=s.Items()[2];s.SaveItem(service.Id,service.Code,service.Name,false,0);accounting.Invoice(true,today,customer.Id,null,0,0,false,"Service",[new(service.Id,"Service",1,100,0)]);Check(s.Items()[2].Qty==0,"Service sale does not move inventory");
            var item4=s.Items()[3];var p4=accounting.Invoice(false,today,supplier.Id,null,0,0,false,"Purchase to cancel",[new(item4.Id,"Goods",2,100,0)]);var si4=accounting.Invoice(true,today,customer.Id,null,0,0,false,"Sale to cancel",[new(item4.Id,"Goods",1,150,0)]);accounting.Reverse(si4,"Cancel latest");accounting.Reverse(p4,"Cancel prior");Check(s.Items()[3].Qty==0&&s.Items()[3].Value==0,"Older inventory document can reverse after newer documents are cancelled");
            Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM (SELECT doc FROM entries GROUP BY doc HAVING sum(debit)!=sum(credit))"))==0,"All posted journals balance");
            Check(s.Balance(s.AccountId("1301"))==s.Items().Sum(i=>i.Value),"Inventory ledger equals stock valuation");
            var backup=Path.Combine(directory,"backup.db");s.Backup(backup);var before=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));accounting.Voucher(true,today,customer.Id,cash,5,"After backup");s.Restore(backup);Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==before,"Restore recovers database snapshot");Check(Directory.GetFiles(Path.Combine(directory,"backups"),"before-restore-*.db").Length>0,"Restore first creates safety backup");
            var bad=Path.Combine(directory,"unrelated.db");using(var unrelated=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+bad)){unrelated.Open();}Reject(()=>s.Restore(bad),"Unrelated SQLite database rejected");
            Check(s.Scalar("PRAGMA integrity_check")?.ToString()=="ok","Database integrity check");Check(s.Table("PRAGMA foreign_key_check").Rows.Count==0,"Foreign key check");
            V2Tests.Run(directory,results);PersonalModeTests.Run(directory,results);File.WriteAllLines(Path.Combine(directory,"test-results.txt"),results);return 0;
        }catch(Exception ex){results.Add("FAIL "+ex);File.WriteAllLines(Path.Combine(directory,"test-results.txt"),results);return 1;}
    }
}

