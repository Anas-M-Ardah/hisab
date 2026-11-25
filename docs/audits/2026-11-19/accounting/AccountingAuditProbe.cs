using System;
using System.IO;
using System.Linq;
using System.Data;
namespace Hisab;
public static class AccountingAuditProbe {
static DateTime D(string d)=>DateTime.Parse(d);
static Store New(string name) { var s=new Store(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",name+"-"+Guid.NewGuid()+".db"));return s; }
static long Party(Store s,string kind){var p=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(p.Id,p.Code,p.Name,kind);return p.Id;}
static long Service(Store s){var i=s.Items()[0];s.SaveItem(i.Id,i.Code,i.Name,false,0);return i.Id;}
static long Balance(Store s,long p,string date)=>Convert.ToInt64(s.Scalar("SELECT coalesce(sum(e.debit-e.credit),0) FROM entries e JOIN documents d ON d.id=e.doc WHERE e.account=@p0 AND d.date<=@p1",p,date));
public static void Main(){
 Console.WriteLine("Baseline a2899dd; probe "+DateTime.Now.ToString("o"));
 foreach(bool sale in new[]{true,false}) { using var s=New(sale?"ar-allocation":"ap-allocation");var a=new AccountingService(s);long p=Party(s,sale?"Customer":"Supplier"),i=Service(s);
 long x=a.Invoice(sale,D("2026-01-01"),p,null,0,0,false,"A",[new(i,"service",1,100,0)]),y=a.Invoice(sale,D("2026-01-01"),p,null,0,0,false,"B",[new(i,"service",1,100,0)]);
 long v=a.Voucher(sale,D("2026-01-01"),p,s.AccountId("1101"),100,"payment");a.Allocate(v,x,100,D("2026-01-01"));long al=Convert.ToInt64(s.Scalar("SELECT id FROM allocations"));a.RemoveAllocation(al,D("2026-03-01"));a.Allocate(v,y,100,D("2026-01-02"));
 Console.WriteLine($"A1 {(sale?"AR":"AP")} asof2026-02-01 ExpectedOutstanding=100000 ActualOutstanding={a.Positions(D("2026-02-01"),p).Sum(z=>z.Outstanding)} Ledger={Balance(s,p,"2026-02-01")} Allocated={a.Positions(D("2026-02-01"),p).Sum(z=>z.Paid)}"); }
 using(var s=New("cheque-close")){var a=new AccountingService(s);long p=Party(s,"Customer"),i=Service(s);long inv=a.Invoice(true,D("2026-01-01"),p,null,0,0,false,"sale",[new(i,"service",1,100,0)]);a.RegisterCheque(true,"CHK-1","bank",p,100,D("2026-01-02"),D("2026-02-02"),inv);long ch=Convert.ToInt64(s.Scalar("SELECT id FROM cheques"));a.CloseYear(D("2026-01-01"),D("2026-01-31"));try{a.SettleCheque(ch,"Bounced",s.AccountId("1102"),D("2026-02-02"),"bank return");Console.WriteLine("A2 Bounce accepted");}catch(Exception e){Console.WriteLine("A2 Expected=February reversal allowed; Actual="+e.Message);}Console.WriteLine($"A2 ExpectedAR=100000 ActualAR={Balance(s,p,"2026-02-02")} ChequeState={s.Scalar("SELECT state FROM cheques")}");}
 using(var s=New("opening-aging")){var a=new AccountingService(s);long p=Party(s,"Customer");a.Manual(D("2026-01-01"),"opening receivable",[new(p,100000,0),new(s.AccountId("3201"),0,100000)]);Console.WriteLine($"G1 Opening ledger={Balance(s,p,"2026-02-01")} Positions={a.Positions(D("2026-02-01"),p).Sum(x=>x.Outstanding)}");}
 using(var s=New("close-gap")){var a=new AccountingService(s);a.Manual(D("2026-01-01"),"January income",[new(s.AccountId("1101"),100000,0),new(s.AccountId("4101"),0,100000)]);a.CloseYear(D("2026-02-01"),D("2026-02-28"));Console.WriteLine($"A3 Close gap accepted; IncomeResidual={Balance(s,s.AccountId("4101"),"2026-02-28")} Retained={Balance(s,s.AccountId("3301"),"2026-02-28")}");try{a.Manual(D("2026-01-31"),"adjustment",[new(s.AccountId("1101"),1000,0),new(s.AccountId("4101"),0,1000)]);}catch(Exception e){Console.WriteLine("A3 Earlier adjustment blocked="+e.Message);}}
 using(var s=New("clean-tax-close")){var a=new AccountingService(s);long p=Party(s,"Customer"),i=Service(s);long inv=a.Invoice(true,D("2026-01-01"),p,null,0,16,false,"tax sale",[new(i,"service",1,100,0)]);a.Reverse(inv,"cancel",D("2026-02-01"));long Tax(string x,string y)=>a.TaxEvents(D(x),D(y)).Rows.Cast<DataRow>().Sum(r=>(long)r["tax"]*(long)r["sign"]);Console.WriteLine($"C1 Tax expectedJan=16000 actualJan={Tax("2026-01-01","2026-01-31")} expectedFeb=-16000 actualFeb={Tax("2026-02-01","2026-02-28")}; JanAR={Balance(s,p,"2026-01-31")} FebAR={Balance(s,p,"2026-02-28")}");a.CloseYear(D("2026-01-01"),D("2026-01-31"));long period=Convert.ToInt64(s.Scalar("SELECT id FROM periods"));a.ReopenYear(period,"review");Console.WriteLine($"C2 Reopen expectedJanIncome=-100000 actual={Balance(s,s.AccountId("4101"),"2026-01-31")} expectedRetained=0 actual={Balance(s,s.AccountId("3301"),"2026-01-31")}");}
}
}
