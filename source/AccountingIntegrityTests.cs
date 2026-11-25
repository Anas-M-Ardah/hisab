using System.Data;
using System.IO;
using System.Text.Json;

namespace Hisab;
public static class AccountingIntegrityTests
{
    public static void Run(string directory, List<string> results)
    {
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); results.Add("PASS " + name); }
        void Reject(Action action, string name) { bool rejected=false; try { action(); } catch (InvalidOperationException) { rejected=true; } Check(rejected,name); }
        void Case(string name, Action<Store, AccountingService> test)
        {
            try { using var s = new Store(Path.Combine(directory,"integrity-"+Guid.NewGuid()+".db")); test(s,new(s)); }
            catch(Exception ex) { results.Add("FAIL " + name + ": " + ex); }
        }
        long Party(Store s,bool customer) { var p=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(p.Id,p.Code,p.Name,customer?"Customer":"Supplier");return p.Id; }
        long Service(Store s) { var i=s.Items()[0];s.SaveItem(i.Id,i.Code,i.Name,false,0);return i.Id; }
        long Balance(Store s,long account,DateTime date)=>Convert.ToInt64(s.Scalar("SELECT coalesce(sum(e.debit-e.credit),0) FROM entries e JOIN documents d ON d.id=e.doc WHERE e.account=@p0 AND d.date<=@p1",account,date.ToString("yyyy-MM-dd")));
        string Entries(Store s,DateTime end)=>JsonSerializer.Serialize(s.Table("SELECT e.*,d.date FROM entries e JOIN documents d ON d.id=e.doc WHERE d.date<=@p0 ORDER BY e.id",end.ToString("yyyy-MM-dd")).Rows.Cast<DataRow>().Select(r=>r.ItemArray));
        var day=DateTime.Today.AddDays(-90);
        foreach(bool customer in new[]{true,false})
        {
            string label=customer?"AR":"AP";
            Case(label+" historical allocation",(s,a)=>{
                long p=Party(s,customer),item=Service(s),cash=s.AccountId("1101");
                long Invoice()=>a.Invoice(customer,day,p,null,0,0,false,"fixture",[new(item,"service",1,100,0)]);
                long first=Invoice(),second=Invoice();long voucher=a.Voucher(customer,day,p,cash,100,"payment");
                a.Allocate(voucher,first,100,day);
                a.RemoveAllocation(Convert.ToInt64(s.Scalar("SELECT id FROM allocations")),day.AddDays(30));
                Reject(()=>a.Allocate(voucher,second,100,day.AddDays(1)),label+" rejects reuse before revocation");
                Check(a.Positions(day.AddDays(15),p).Sum(x=>x.Outstanding)==100000 && Balance(s,p,day.AddDays(15))==(customer?100000:-100000),label+" historical positions reconcile");
                a.Allocate(voucher,second,100,day.AddDays(30));
                Check(a.Positions(day.AddDays(30),p).Sum(x=>x.Paid)==100000,label+" allows reuse on revocation day");
                Reject(()=>a.Allocate(voucher,first,1,day.AddDays(40)),label+" rejects current over-allocation");
            });
            Case(label+" invoice capacity",(s,a)=>{
                long p=Party(s,customer),item=Service(s),cash=s.AccountId("1101");
                long inv=a.Invoice(customer,day,p,null,0,0,false,"invoice",[new(item,"service",1,100,0)]);
                long v1=a.Voucher(customer,day,p,cash,100,"first"),v2=a.Voucher(customer,day,p,cash,100,"second");
                a.Allocate(v1,inv,100,day.AddDays(10));a.RemoveAllocation(Convert.ToInt64(s.Scalar("SELECT id FROM allocations")),day.AddDays(30));
                Reject(()=>a.Allocate(v2,inv,1,day),label+" checks future interval peak, not just start/current balance");
                a.Allocate(v2,inv,60,day.AddDays(30));a.Allocate(v2,inv,40,day.AddDays(31));
                Check(a.Positions(day.AddDays(30),p).Single().Outstanding==40000 && a.Positions(day.AddDays(31),p).Single().Outstanding==0,label+" partial allocations preserve cutoff");
                a.Reverse(v2,"undo payment",day.AddDays(40));
                Check(a.Positions(day.AddDays(39),p).Single().Paid==100000 && a.Positions(day.AddDays(40),p).Single().Paid==0,label+" reversal preserves historical settlement");
            });
        }
        foreach(bool incoming in new[]{true,false}) foreach(bool cleared in new[]{false,true})
        {
            string label=(incoming?"incoming":"outgoing")+(cleared?" cleared":" pending");
            Case(label+" closed cheque",(s,a)=>{
                long p=Party(s,incoming),item=Service(s),cash=s.AccountId("1101");
                long inv=a.Invoice(incoming,day,p,null,0,0,false,"invoice",[new(item,"service",1,100,0)]);
                long issue=a.RegisterCheque(incoming,"fixture","bank",p,100,day.AddDays(1),day.AddDays(5),inv);
                long ch=Convert.ToInt64(s.Scalar("SELECT id FROM cheques WHERE issue_doc=@p0",issue));
                if(cleared) a.SettleCheque(ch,"Cleared",cash,day.AddDays(5),"clear");
                a.CloseYear(day,day.AddDays(10));string before=Entries(s,day.AddDays(10));
                Reject(()=>a.Reverse(issue,"bypass",day.AddDays(20)),label+" generic reversal stays protected");
                Reject(()=>a.Reverse(issue,"managed bypass",day.AddDays(20),true),label+" generic managed reversal still checks original closed period");
                a.SettleCheque(ch,"Bounced",cash,day.AddDays(20),"bank returned");
                Check(Entries(s,day.AddDays(10))==before,label+" closed entries untouched");
                Check(Balance(s,p,day.AddDays(19))==0 && Balance(s,p,day.AddDays(20))==(incoming?100000:-100000),label+" debt restored only on bounce date");
                Check(a.Positions(day.AddDays(19),p).Single().Outstanding==0 && a.Positions(day.AddDays(20),p).Single().Outstanding==100000,label+" allocation released on bounce date");
                Check(Balance(s,s.AccountId(incoming?"1104":"2190"),day.AddDays(20))==0 && Balance(s,cash,day.AddDays(20))==0,label+" cheque and bank balances reconciled");
                Check(s.Scalar("SELECT state FROM cheques WHERE id=@p0",ch)?.ToString()=="Bounced",label+" terminal state recorded");
                long count=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));
                Reject(()=>a.SettleCheque(ch,"Bounced",cash,day.AddDays(21),"again"),label+" duplicate bounce rejected");
                Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==count,label+" duplicate creates no documents");
            });
        }
        Case("return intervals",(s,a)=>{
            long p=Party(s,true),item=Service(s),cash=s.AccountId("1101");
            long inv=a.Invoice(true,day,p,null,0,0,false,"invoice",[new(item,"service",1,100,0)]);
            long line=Convert.ToInt64(s.Scalar("SELECT id FROM invoice_lines WHERE doc=@p0",inv));
            long ret=a.ReturnInvoice(inv,day.AddDays(10),[new(line,0.5m)],"half returned");a.Reverse(ret,"undo return",day.AddDays(20));
            long v=a.Voucher(true,day,p,cash,100,"payment");
            Reject(()=>a.Allocate(v,inv,60,day),"Allocation respects a historical return even when cancelled later");
            a.Allocate(v,inv,50,day);a.Allocate(v,inv,50,day.AddDays(20));
            Check(a.Positions(day.AddDays(15),p).Single().Outstanding==0 && a.Positions(day.AddDays(20),p).Single().Outstanding==0,"Return cancellation releases capacity only on its event date");
        });
        Case("cheque rejection atomicity",(s,a)=>{
            long p=Party(s,true),cash=s.AccountId("1101");
            long issue=a.RegisterCheque(true,"atomic","bank",p,100,day,day.AddDays(5));
            long ch=Convert.ToInt64(s.Scalar("SELECT id FROM cheques WHERE issue_doc=@p0",issue));
            a.SettleCheque(ch,"Cleared",cash,day.AddDays(10),"clear");
            long count=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));
            Reject(()=>a.SettleCheque(ch,"Bounced",cash,day.AddDays(5),"too early"),"Bounce cannot precede clearance");
            Reject(()=>a.SettleCheque(ch,"Bounced",cash,day.AddDays(11),""),"Bounce requires a reason");
            Reject(()=>a.SettleCheque(ch,"Bounced",cash,DateTime.Today.AddDays(1),"future"),"Future bounce rejected");
            a.CloseYear(day,day.AddDays(15));count=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));
            Reject(()=>a.SettleCheque(ch,"Bounced",cash,day.AddDays(12),"closed"),"Bounce event must be in an open period");
            s.SaveUser("reader","ReaderPassword123","Viewer");s.Login("reader","ReaderPassword123");
            Reject(()=>a.SettleCheque(ch,"Bounced",cash,day.AddDays(20),"forbidden"),"Viewer cannot bounce a cheque");
            Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==count && s.Scalar("SELECT state FROM cheques WHERE id=@p0",ch)?.ToString()=="Cleared","Rejected bounce leaves documents and state intact");
        });
        Case("cheque transaction rollback",(s,a)=>{
            long p=Party(s,true),cash=s.AccountId("1101");
            long issue=a.RegisterCheque(true,"rollback","bank",p,100,day,day.AddDays(5));
            long ch=Convert.ToInt64(s.Scalar("SELECT id FROM cheques WHERE issue_doc=@p0",issue));
            a.SettleCheque(ch,"Cleared",cash,day.AddDays(5),"clear");
            string before=Entries(s,DateTime.Today);long count=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));
            s.Exec("CREATE TRIGGER fail_cheque_state BEFORE UPDATE OF state ON cheques BEGIN SELECT RAISE(ABORT,'simulated persistence failure'); END");
            bool failed=false;try { a.SettleCheque(ch,"Bounced",cash,day.AddDays(20),"failure"); } catch(Microsoft.Data.Sqlite.SqliteException) { failed=true; }
            Check(failed && Entries(s,DateTime.Today)==before && Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==count,"Cheque failure rolls back both reversing entries");
            Check(Convert.ToInt64(s.Scalar("SELECT reversed FROM documents WHERE id=@p0",issue))==0 && s.Scalar("SELECT state FROM cheques WHERE id=@p0",ch)?.ToString()=="Cleared","Cheque failure rolls back reversal metadata and state");
        });
        Case("pending cancellation",(s,a)=>{
            long p=Party(s,true),cash=s.AccountId("1101");
            a.RegisterCheque(true,"cancel","bank",p,100,day,day.AddDays(5));
            long ch=Convert.ToInt64(s.Scalar("SELECT id FROM cheques"));
            a.SettleCheque(ch,"Cancelled",cash,day.AddDays(1),"cancelled");
            Check(Balance(s,p,day.AddDays(1))==0 && s.Scalar("SELECT state FROM cheques")?.ToString()=="Cancelled","Open-period cancellation remains supported");
        });
    }
}
