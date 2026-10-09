using System.IO;
namespace Hisab;
public static class HierarchyMoveTests
{
    public static void Run(string directory,List<string> results)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action action,string name){try{action();}catch(InvalidOperationException){results.Add("PASS "+name);return;}throw new Exception("Did not reject: "+name);}
        using var s=new Store(Path.Combine(directory,"hierarchy-moves.db"));var accounting=new AccountingService(s);
        s.SaveAccount(null,"G1","Expenses","Expense");s.SaveAccount(null,"G2","Household","Expense");long first=s.AccountId("G1"),second=s.AccountId("G2");
        s.SaveAccount(null,"W1","Water","Expense",first);long water=s.AccountId("W1");s.SaveAccount(null,"W2","Meter","Expense",water);long meter=s.AccountId("W2");
        s.SaveTranslations("accounts",water,"المياه","Water bill","عنوان","Address");accounting.Voucher(false,DateTime.Today,water,s.AccountId("1101"),10,"Water");accounting.Voucher(false,DateTime.Today,meter,s.AccountId("1101"),5,"Meter");
        long entries=Convert.ToInt64(s.Scalar("SELECT count(*) FROM entries")),documents=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));
        Check(s.CanMoveAccount(water,second,out _),"Valid drag target is accepted");s.MoveAccount(water,second);
        Check(s.Accounts().Single(a=>a.Id==water).ParentId==second,"Move updates parent directly");
        Check(s.Accounts().Single(a=>a.Id==meter).ParentId==water,"Moving parent retains its nested children");
        Check(s.LocalName("accounts",water,true)=="المياه"&&s.LocalName("accounts",water,false)=="Water bill"&&s.Accounts().Single(a=>a.Id==water).Name=="Water","Move preserves original and translated names");
        Check(s.GroupBalance(first)==0&&s.GroupBalance(second)==15000&&s.Balance(water)==10000,"Moved subtree updates group totals without changing own balances");
        Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM entries"))==entries&&Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==documents,"Hierarchy move creates no accounting documents or postings");
        Reject(()=>s.MoveAccount(water,water),"Drag onto self rejected");
        Reject(()=>s.MoveAccount(second,meter),"Drag onto descendant rejected");
        Reject(()=>s.MoveAccount(water,s.AccountId("1101")),"Drag onto different account type rejected");
        s.MoveAccount(water,null);Check(s.Accounts().Single(a=>a.Id==water).ParentId==null&&s.AccountFamily(water).Contains(meter),"Move to top level retains subtree");
        s.MoveAccount(water,second);Check(!s.CanMoveAccount(water,second,out _)&&s.GroupBalance(second)==15000,"Undo restores prior parent and no-op targets are rejected");
        s.SaveUser("hierarchy-viewer","TestPassword123","Viewer");if(!s.Login("hierarchy-viewer","TestPassword123"))throw new Exception("Viewer login failed");Reject(()=>s.MoveAccount(water,first),"Viewer cannot move accounts");
    }
}
