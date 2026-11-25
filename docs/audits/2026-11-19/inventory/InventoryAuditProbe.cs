using System.IO;
using static Hisab.Store;
namespace Hisab;
public static class InventoryAuditProbe {
 public static void Main(){
 var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../probes"));Directory.CreateDirectory(root);
 using var log=new StreamWriter(Path.Combine(root,"results.txt"),false){AutoFlush=true};
 void Say(string t){Console.WriteLine(t);log.WriteLine(t);}
 void Run(string name,Action<Store,AccountingService,long,long,long> f){try{using var s=new Store(Path.Combine(root,name+"-"+Guid.NewGuid()+".db"));var a=new AccountingService(s);var sup=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(sup.Id,sup.Code,sup.Name,"Supplier");var cus=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(cus.Id,cus.Code,cus.Name,"Customer");Say("CASE "+name);f(s,a,sup.Id,cus.Id,s.Items()[0].Id);Say("SCHEMA "+s.Scalar("PRAGMA user_version"));}catch(Exception ex){Say("ERROR "+name+" "+ex);}}
 var t=DateTime.Today;
 Run("warranty-reservation-location",(s,a,sup,cus,it)=>{
 a.ConfigureInventoryItem(it,"AUDIT",true,Array.Empty<string>());
 a.Invoice(false,t.AddDays(-5),sup,null,0,0,false,"receipt",[new(it,"test",3,100,0,1,"A,B,C")]);
 var sale=a.Invoice(true,t.AddDays(-4),cus,null,0,0,false,"sale A",[new(it,"test",1,150,0,1,"A")]);var unit=Convert.ToInt64(s.Scalar("SELECT id FROM serial_units WHERE number='A'"));var w=a.SaveWarranty(sale,unit,t.AddDays(90),"audit");var claim=a.ReceiveWarranty(w,t,"fault","good","");
 var hold=a.CreateReservation(cus,t.AddDays(1),[new(it,1,1,"B")],"hold B");long before=s.Items()[0].Value;
 try{a.ReplaceWarranty(claim,t,it,1,"B");Say("FAIL reserved replacement accepted");}catch(InvalidOperationException){Say("PASS reserved replacement rejected");}
 var wx=a.ReplaceWarranty(claim,t,it,1,"C");Say("Replacement stock expected=100000 actual="+s.Items()[0].Value+" GL="+s.Balance(s.AccountId("1301"))+" expense="+s.Balance(s.AccountId("5196"))+" customer="+s.Balance(cus)+" available="+s.AvailableQuantity(it,1));
 a.UndoWarrantyReplacement(claim,"undo");Say("Undo expected stock=200000 actual="+s.Items()[0].Value+" GL="+s.Balance(s.AccountId("1301"))+" expense="+s.Balance(s.AccountId("5196")));
 var loc=s.SaveLocation(null,"Other");try{a.TransferInventory(t,1,loc,it,1,"B","transfer held");Say("FAIL reserved transfer accepted");}catch(InvalidOperationException){Say("PASS reserved transfer rejected");}
 a.TransferInventory(t,1,loc,it,1,"C","transfer free");Say("Transfer main="+s.LocationQuantity(it,1)+" other="+s.LocationQuantity(it,loc)+" totalValue="+s.Items()[0].Value+" GL="+s.Balance(s.AccountId("1301")));
 });
 Run("return-rounding",(s,a,sup,cus,it)=>{
 a.Invoice(false,t.AddDays(-5),sup,null,0,0,false,"3 units total 100",[new(it,"test",3,33.334m,.002m)]);
 var sale=a.Invoice(true,t.AddDays(-4),cus,null,0,0,false,"sell all",[new(it,"test",3,50,0)]);var line=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",sale));
 for(int n=1;n<=3;n++)a.ReturnInvoice(sale,t.AddDays(-3+n),[new(line,1)],"partial return "+n);
 Say("Expected qty=3000 value=100000 COGS=0; actual qty="+s.Items()[0].Qty+" value="+s.Items()[0].Value+" GL1301="+s.Balance(s.AccountId("1301"))+" COGS="+s.Balance(s.AccountId("5104")));
 });
 Run("closed-receipt-bill",(s,a,sup,cus,it)=>{
 var po=a.CreatePurchaseOrder(sup,1,t.AddDays(-5),[new(it,2,100)]);a.IssuePurchaseOrder(po);var ol=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",po));var gr=a.ReceivePurchaseOrder(po,t.AddDays(-5),[new(ol,2,"")]);var rl=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",gr));a.CloseYear(t.AddDays(-10),t.AddDays(-2));
 try{a.BillReceipts(sup,t,[new(rl,2,100)],0);Say("BILL ACCEPTED");}catch(InvalidOperationException e){Say("BILL REJECTED "+e.Message);}Say("Expected unbilled=0 GRNI=0 AP=-200000; actual unbilled="+a.UnbilledQuantity(rl)+" GRNI="+s.Balance(s.AccountId("2195"))+" AP="+s.Balance(sup));
 });
 Run("late-cost-cutoff",(s,a,sup,cus,it)=>{
 var pi=a.Invoice(false,t.AddDays(-5),sup,null,0,0,false,"receipt",[new(it,"test",2,100,0)]);var line=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",pi));var sale=a.Invoice(true,t.AddDays(-4),cus,null,0,0,false,"sale",[new(it,"test",1,150,0)]);
 Say("BEFORE cutoff stock="+s.Scalar("SELECT sum(e.debit-e.credit) FROM entries e JOIN documents d ON d.id=e.doc WHERE e.account=@p0 AND d.date<=@p1",s.AccountId("1301"),t.AddDays(-1).ToString("yyyy-MM-dd"))+" sale-cost="+s.Scalar("SELECT cost FROM invoice_lines WHERE doc=@p0",sale));
 a.AddLandedCost(t,sup,null,20,[new(line,20)],"freight today");
 Say("AFTER cutoff stock="+s.Scalar("SELECT sum(e.debit-e.credit) FROM entries e JOIN documents d ON d.id=e.doc WHERE e.account=@p0 AND d.date<=@p1",s.AccountId("1301"),t.AddDays(-1).ToString("yyyy-MM-dd"))+" sale-cost="+s.Scalar("SELECT cost FROM invoice_lines WHERE doc=@p0",sale)+" clearing-at-cutoff="+s.Scalar("SELECT sum(e.debit-e.credit) FROM entries e JOIN documents d ON d.id=e.doc WHERE e.account=@p0 AND d.date<=@p1",s.AccountId("2196"),t.AddDays(-1).ToString("yyyy-MM-dd")));Say("Current stock="+s.Items()[0].Value+" GL="+s.Balance(s.AccountId("1301")));
 });
 }
}
