using System.Data;
using System.IO;
using System.Text.Json;

namespace Hisab;
public static class ClosingStockTests
{
    public static void Run(string directory,List<string> results)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action act,string name){bool rejected=false;try{act();}catch(InvalidOperationException){rejected=true;}Check(rejected,name);}
        void Case(string name,Action<Store,AccountingService,long,long,long> test){try{using var s=new Store(Path.Combine(directory,"closing-stock-"+Guid.NewGuid()+".db"));var a=new AccountingService(s);var sup=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(sup.Id,sup.Code,sup.Name,"Supplier");var cus=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(cus.Id,cus.Code,cus.Name,"Customer");test(s,a,sup.Id,cus.Id,s.Items()[0].Id);}catch(Exception ex){results.Add("FAIL "+name+": "+ex);}}
        var day=DateTime.Today.AddDays(-60);
        Case("return remainder",(s,a,sup,cus,item)=>{
            a.Invoice(false,day,sup,null,0,0,false,"100 total",[new(item,"stock",3,33.334m,.002m)]);
            long sale=a.Invoice(true,day.AddDays(1),cus,null,0,0,false,"sale",[new(item,"stock",3,50,0)]);
            long line=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",sale));
            a.ReturnInvoice(sale,day.AddDays(2),[new(line,1)],"first");a.ReturnInvoice(sale,day.AddDays(3),[new(line,1)],"second");
            long last=a.ReturnInvoice(sale,day.AddDays(4),[new(line,1)],"last");
            Check(s.Items().Single(i=>i.Id==item).Value==100000&&s.Balance(s.AccountId("1301"))==100000&&s.Balance(s.AccountId("5104"))==0,"Three partial returns restore exactly 100.000 and zero COGS");
            a.RebuildInventory("repeat");Check(s.Items().Single(i=>i.Id==item).Value==100000,"Return remainder replay is deterministic");
            a.Reverse(last,"undo last",day.AddDays(5));a.ReturnInvoice(sale,day.AddDays(6),[new(line,1)],"return again");
            Check(s.Items().Single(i=>i.Id==item).Value==100000&&s.Balance(s.AccountId("5104"))==0,"Cancel and re-enter final return retains exact cost");
        });
        Case("closing gap",(s,a,sup,cus,item)=>{
            a.Manual(day,"income",[new(s.AccountId("1101"),100000,0),new(s.AccountId("4101"),0,100000)]);
            Reject(()=>a.CloseYear(day.AddDays(10),day.AddDays(20)),"First close cannot silently exclude earlier activity");
            Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM periods"))==0,"Rejected close leaves periods unchanged");
            a.CloseYear(day,day.AddDays(20));
            Reject(()=>a.CloseYear(day.AddDays(22),day.AddDays(30)),"Subsequent close must start the day after the prior close");
            a.CloseYear(day.AddDays(21),day.AddDays(30));
            Check(s.Balance(s.AccountId("4101"))==0&&s.Balance(s.AccountId("3301"))==-100000,"Contiguous closing transfers income exactly once");
            a.ReopenYear(Convert.ToInt64(s.Scalar("SELECT id FROM periods ORDER BY end DESC LIMIT 1")),"review");
            a.ReopenYear(Convert.ToInt64(s.Scalar("SELECT id FROM periods")),"review");
            Check(s.Balance(s.AccountId("4101"))==-100000&&s.Balance(s.AccountId("3301"))==0,"Reopening restores original period income");
            a.CloseYear(day,day.AddDays(30));Check(s.Balance(s.AccountId("3301"))==-100000,"Reclosing reopened periods transfers profit without duplication");
        });
        Case("closed receipt equal-cost billing",(s,a,sup,cus,item)=>{
            long po=a.CreatePurchaseOrder(sup,1,day,[new(item,2,100)]);a.IssuePurchaseOrder(po);
            long ol=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",po));
            long gr=a.ReceivePurchaseOrder(po,day,[new(ol,2,"")]);long line=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",gr));
            a.Invoice(true,day.AddDays(1),cus,null,0,0,false,"one sold",[new(item,"stock",1,150,0)]);
            a.CloseYear(day,day.AddDays(10));
            string Snapshot()=>JsonSerializer.Serialize(s.Table("SELECT e.* FROM entries e JOIN documents d ON d.id=e.doc WHERE d.date<=@p0 ORDER BY e.id",day.AddDays(10).ToString("yyyy-MM-dd")).Rows.Cast<DataRow>().Select(r=>r.ItemArray));
            string before=Snapshot();long bill=a.BillReceipts(sup,day.AddDays(11),[new(line,1,100)],16);
            Check(a.UnbilledQuantity(line)==1000&&s.Balance(s.AccountId("2195"))==-100000&&s.Balance(sup)==-116000,"Partial late bill clears GRNI and posts current tax/AP");
            Check(Snapshot()==before&&s.Items().Single(i=>i.Id==item).Value==100000,"Late bill leaves closed entries and sold-stock costing untouched");
            a.Reverse(bill,"cancel bill",day.AddDays(12));
            Check(Snapshot()==before&&a.UnbilledQuantity(line)==2000&&s.Balance(sup)==0,"Late bill reversal restores unbilled receipt without rewriting closed entries");
            a.BillReceipts(sup,day.AddDays(13),[new(line,2,100)],0);
            Check(a.UnbilledQuantity(line)==0&&s.Balance(s.AccountId("2195"))==0&&s.Balance(sup)==-200000,"Complete late billing settles GRNI exactly");
            Reject(()=>a.BillReceipts(sup,day.AddDays(14),[new(line,1,100)],0),"Already billed quantity cannot be billed twice");
        });
        Case("closed receipt variance guard",(s,a,sup,cus,item)=>{
            long po=a.CreatePurchaseOrder(sup,1,day,[new(item,2,100)]);a.IssuePurchaseOrder(po);
            long ol=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",po));
            long gr=a.ReceivePurchaseOrder(po,day,[new(ol,2,"")]);long line=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",gr));a.CloseYear(day,day.AddDays(10));
            long count=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));
            Reject(()=>a.BillReceipts(sup,day.AddDays(11),[new(line,2,110)],0),"Late bill cannot rewrite a closed receipt through a price increase");
            Reject(()=>a.BillReceipts(sup,day.AddDays(11),[new(line,2,90)],0),"Late bill cannot rewrite a closed receipt through a price decrease");
            Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==count&&a.UnbilledQuantity(line)==2000,"Rejected price variance rolls back bill and receipt links");
            long po2=a.CreatePurchaseOrder(sup,1,day.AddDays(11),[new(item,1,100)]);a.IssuePurchaseOrder(po2);
            long ol2=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",po2));
            long gr2=a.ReceivePurchaseOrder(po2,day.AddDays(11),[new(ol2,1,"")]);long line2=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",gr2));
            count=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));
            Reject(()=>a.BillReceipts(sup,day.AddDays(12),[new(line,1,100),new(line2,1,110)],0),"Mixed closed receipt and open cost adjustment must be billed separately");
            Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==count&&a.UnbilledQuantity(line)==2000&&a.UnbilledQuantity(line2)==1000,"Mixed bill rejection rolls back every receipt link");
        });
        Case("fractional receipt remainder",(s,a,sup,cus,item)=>{
            long po=a.CreatePurchaseOrder(sup,1,day,[new(item,1.5m,33.333m)]);a.IssuePurchaseOrder(po);
            long ol=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",po));
            long gr=a.ReceivePurchaseOrder(po,day,[new(ol,1.5m,"")]);long line=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",gr));a.CloseYear(day,day.AddDays(5));
            a.BillReceipts(sup,day.AddDays(6),[new(line,.5m,33.333m)],0);a.BillReceipts(sup,day.AddDays(7),[new(line,.5m,33.333m)],0);long last=a.BillReceipts(sup,day.AddDays(8),[new(line,.5m,33.333m)],0);
            Check(Convert.ToInt64(s.Scalar("SELECT net FROM documents WHERE id=@p0",last))==16666&&s.Balance(sup)==-50000&&s.Balance(s.AccountId("2195"))==0,"Final same-price fractional bill takes receipt remainder exactly");
        });
        Case("closed return and earlier cancellation",(s,a,sup,cus,item)=>{
            a.Invoice(false,day,sup,null,0,0,false,"100 total",[new(item,"stock",3,33.334m,.002m)]);
            long sale=a.Invoice(true,day.AddDays(1),cus,null,0,0,false,"sale",[new(item,"stock",3,50,0)]);long line=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",sale));
            long first=a.ReturnInvoice(sale,day.AddDays(2),[new(line,1)],"first");a.CloseYear(day,day.AddDays(2));
            long second=a.ReturnInvoice(sale,day.AddDays(3),[new(line,1)],"second");a.ReturnInvoice(sale,day.AddDays(4),[new(line,1)],"third");
            Check(Convert.ToInt64(s.Scalar("SELECT cost FROM invoice_lines WHERE doc=@p0",first))==33333&&s.Items().Single(i=>i.Id==item).Value==100000,"Closed return cost retained while later return receives remainder");
            a.Reverse(second,"cancel earlier open return",day.AddDays(5));a.ReturnInvoice(sale,day.AddDays(6),[new(line,1)],"replacement return");
            Check(s.Items().Single(i=>i.Id==item).Value==100000&&s.Balance(s.AccountId("1301"))==100000,"Earlier return cancellation and re-return preserve total stock cost");
        });
        Case("closed receipt zero remainder",(s,a,sup,cus,item)=>{
            long po=a.CreatePurchaseOrder(sup,1,day,[new(item,.003m,.5m)]);a.IssuePurchaseOrder(po);
            long ol=Convert.ToInt64(s.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",po));
            long gr=a.ReceivePurchaseOrder(po,day,[new(ol,.003m,"")]);long line=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",gr));a.CloseYear(day,day.AddDays(5));
            for(int i=0;i<3;i++)a.BillReceipts(sup,day.AddDays(6+i),[new(line,.001m,.5m)],0);
            Check(a.UnbilledQuantity(line)==0&&s.Balance(sup)==-2&&s.Balance(s.AccountId("2195"))==0&&s.Items().Single(i=>i.Id==item).Value==2,"Closed receipt zero-value final portion clears quantity without inventing cost");
        });
        Case("grouped numeric input",(s,a,sup,cus,item)=>{
            foreach(string input in new[]{"1,250.500","١٬٢٥٠٫٥٠٠","۱٬۲۵۰٫۵۰۰","1250.500"})Check(Store.Parse(input)==1250.500m,"Valid grouped/Arabic input: "+input);
            foreach(string input in new[]{"12,50.500","1,,250","1,250,50","1.250,500","1,250.5,0",",125","1,","1 250"})Reject(()=>Store.Parse(input),"Malformed grouping rejected: "+input);
            Check(Store.Parse(Store.Money(-1250500))==-1250.500m&&Store.Parse("+1,250.500")==1250.500m,"Formatted signed amounts round-trip");
        });
        Case("opening reconciliation",(s,a,sup,cus,item)=>{
            long cash=s.AccountId("1101"),opening=s.AccountId("3201");
            a.Manual(day,"opening AR",[new(cus,100000,0),new(opening,0,100000)]);
            a.Manual(day,"opening AP",[new(opening,60000,0),new(sup,0,60000)]);
            var ar=a.PartyReconciliation(day).Single(p=>p.Account==cus);var ap=a.PartyReconciliation(day).Single(p=>p.Account==sup);
            Check(ar.Ledger==100000&&ar.InvoiceOutstanding==0&&ar.OutsideAging==100000,"Manual customer opening appears outside invoice aging");
            Check(ap.Ledger==60000&&ap.InvoiceOutstanding==0&&ap.OutsideAging==60000,"Manual supplier opening appears outside invoice aging with payable sign");
            Check(a.PartyReconciliation(day.AddDays(-1)).Count==0,"Party reconciliation respects historical cutoff");
            a.Voucher(true,day.AddDays(1),cus,cash,20,"unallocated receipt");
            ar=a.PartyReconciliation(day.AddDays(1)).Single(p=>p.Account==cus);
            Check(ar.Ledger==80000&&ar.OutsideAging==80000,"Unallocated payment is included without duplicate opening posting");
            var i=s.Items().Single(x=>x.Id==item);s.SaveItem(item,i.Code,i.Name,false,0);
            long inv=a.Invoice(true,day.AddDays(2),cus,null,0,0,false,"invoice",[new(item,"service",1,50,0)]);
            long voucher=a.Voucher(true,day.AddDays(3),cus,cash,50,"invoice receipt");a.Allocate(voucher,inv,50,day.AddDays(3));
            ar=a.PartyReconciliation(day.AddDays(2)).Single(p=>p.Account==cus);
            Check(ar.Ledger==130000&&ar.InvoiceOutstanding==50000&&ar.OutsideAging==80000,"Invoice amount and non-invoice opening remain separate");
            ar=a.PartyReconciliation(day.AddDays(3)).Single(p=>p.Account==cus);
            Check(ar.Ledger==80000&&ar.InvoiceOutstanding==0&&ar.OutsideAging==80000,"Allocation reconciles without hiding outstanding opening balance");
        });
    }
}
