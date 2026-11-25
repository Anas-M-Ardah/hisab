using System.Data;
using System.IO;
using System.Text.Json;
namespace Hisab;
public static class LateBillingTests
{
    public static void Run(string directory,List<string> results)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,name);}
        var day=DateTime.Today.AddDays(-40);
        foreach(bool discount in new[]{false,true}){
            using var s=new Store(Path.Combine(directory,"late-billing-"+Guid.NewGuid()+".db"));var a=new AccountingService(s);
            var sup=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(sup.Id,sup.Code,sup.Name,"Supplier");var cus=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(cus.Id,cus.Code,cus.Name,"Customer");long item=s.Items()[0].Id;
            long po=a.CreatePurchaseOrder(sup.Id,1,day,[new(item,2,100)]);a.IssuePurchaseOrder(po);long ol=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",po));long gr=a.ReceivePurchaseOrder(po,day,[new(ol,2,"")]);long receipt=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",gr));
            long sale=a.Invoice(true,day.AddDays(1),cus.Id,null,0,0,false,"sold before close",[new(item,"item",1,150,0)]);a.CloseYear(day,day.AddDays(5));
            string Snapshot()=>JsonSerializer.Serialize(s.Table("SELECT e.*,d.date FROM entries e JOIN documents d ON d.id=e.doc WHERE d.date<=@p0 ORDER BY e.id",day.AddDays(5).ToString("yyyy-MM-dd")).Rows.Cast<DataRow>().Select(r=>r.ItemArray));string before=Snapshot();
            decimal price=discount?90:110,cap=discount?-10:10;long count=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));
            Reject(()=>a.BillReceipts(sup.Id,day.AddDays(10),[new(receipt,2,price)],0),"Closed price difference requires explicit allocation");
            Reject(()=>a.BillReceipts(sup.Id,day.AddDays(10),[new(receipt,2,price,-cap)],0),"Inventory share must have the price difference sign");
            Reject(()=>a.BillReceipts(sup.Id,day.AddDays(10),[new(receipt,2,price,cap*3)],0),"Inventory share cannot exceed total price difference");
            Reject(()=>a.BillReceipts(sup.Id,day.AddDays(10),[new(receipt,2,1,-198)],0),"Price reduction cannot make remaining stock value negative");
            Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==count,"Invalid late price allocation rolls back document and links");
            long bill=a.BillReceipts(sup.Id,day.AddDays(10),[new(receipt,2,price,cap)],0);
            Check(Snapshot()==before&&Convert.ToInt64(s.Scalar("SELECT cost FROM invoice_lines WHERE doc=@p0",sale))==100000,"Late price adjustment preserves closed entries and original sale cost");
            Check(s.Items().Single(i=>i.Id==item).Value==100000+Store.M(cap)&&s.Balance(s.AccountId("1301"))==100000+Store.M(cap),"Late price allocation reconciles current inventory and GL");
            Check(s.Balance(s.AccountId("5104"))==Store.M(cap)&&s.Balance(sup.Id)==-Store.M(price*2)&&s.Balance(s.AccountId("2195"))==0&&s.Balance(s.AccountId("2196"))==0,"Late variance posts current COGS, AP and clears GRNI without clearing-account residue");
            long later=a.Invoice(true,day.AddDays(11),cus.Id,null,0,0,false,"later sale",[new(item,"item",1,150,0)]);
            Check(Convert.ToInt64(s.Scalar("SELECT cost FROM invoice_lines WHERE doc=@p0",later))==100000+Store.M(cap)&&Snapshot()==before,"Later sale uses adjusted average cost while closed entries remain frozen");
            a.Reverse(bill,"correct invoice",day.AddDays(12));
            Check(Convert.ToInt64(s.Scalar("SELECT cost FROM invoice_lines WHERE doc=@p0",later))==100000&&s.Balance(sup.Id)==0&&a.UnbilledQuantity(receipt)==2000&&Snapshot()==before,"Reversing open late bill replays only open costing and restores unbilled receipt");
            Reject(()=>a.BillReceipts(sup.Id,day.AddDays(13),[new(receipt,2,price,cap)],0),"Depleted inventory cannot receive a capitalized price adjustment");
            a.BillReceipts(sup.Id,day.AddDays(13),[new(receipt,2,price,0)],0);
            Check(s.Items().Single(i=>i.Id==item).Value==0&&s.Balance(s.AccountId("5104"))==100000+Store.M((price-100)*2),"Fully consumed stock takes all late price variance to current COGS");
        }
        using(var s=new Store(Path.Combine(directory,"late-partial-"+Guid.NewGuid()+".db"))){
            var a=new AccountingService(s);var sup=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(sup.Id,sup.Code,sup.Name,"Supplier");var cus=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(cus.Id,cus.Code,cus.Name,"Customer");long item=s.Items()[0].Id;
            long po=a.CreatePurchaseOrder(sup.Id,1,day,[new(item,2,100)]);a.IssuePurchaseOrder(po);long ol=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",po));long gr=a.ReceivePurchaseOrder(po,day,[new(ol,2,"")]);long receipt=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",gr));a.CloseYear(day,day.AddDays(5));
            a.BillReceipts(sup.Id,day.AddDays(10),[new(receipt,1,110,10)],0);
            Check(a.UnbilledQuantity(receipt)==1000&&s.Balance(sup.Id)==-110000&&s.Balance(s.AccountId("2195"))==-100000&&s.Items().Single(i=>i.Id==item).Value==210000,"Partial late invoice adjusts only billed cost and leaves remaining GRNI");
            a.CloseYear(day.AddDays(6),day.AddDays(12));long period=Convert.ToInt64(s.Scalar("SELECT id FROM periods ORDER BY end DESC LIMIT 1"));
            string Snapshot()=>JsonSerializer.Serialize(s.Table("SELECT e.* FROM entries e JOIN documents d ON d.id=e.doc WHERE d.date<=@p0 ORDER BY e.id",day.AddDays(12).ToString("yyyy-MM-dd")).Rows.Cast<DataRow>().Select(r=>r.ItemArray));string before=Snapshot();
            long sale=a.Invoice(true,day.AddDays(13),cus.Id,null,0,0,false,"after adjustment closed",[new(item,"item",1,150,0)]);
            Check(Convert.ToInt64(s.Scalar("SELECT cost FROM invoice_lines WHERE doc=@p0",sale))==105000&&Snapshot()==before,"Closed variance bill seeds later average cost without rewriting its journal");
            a.RebuildInventory("repeat frozen replay");Check(Snapshot()==before&&s.Items().Single(i=>i.Id==item).Value==105000,"Repeated frozen replay preserves partial adjustment");
            a.ReopenYear(period,"review adjustment period");a.RebuildInventory("reopened adjustment replay");
            Check(s.Items().Single(i=>i.Id==item).Value==105000&&s.Balance(s.AccountId("1301"))==105000,"Reopened adjustment period replays consistently with inventory GL");
        }
    }
}
