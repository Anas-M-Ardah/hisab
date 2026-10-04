using System.IO;
namespace Hisab;
public static class PersonalModeTests
{
    public static void Run(string directory,List<string> results)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
        string path=Path.Combine(directory,"personal.hdb");long owner;
        using(var s=new Store(path,true)){
            Check(!s.TryPersonalSession(),"Fresh database does not silently select an account before setup");s.EnablePersonalMode();owner=s.User.Id;Check(owner>0&&s.TryPersonalSession(),"Personal setup requires no username or password");Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM users WHERE personal_owner=1"))==1,"Personal profile creates exactly one internal owner");s.EnablePersonalMode();Check(s.User.Id==owner,"Repeated personal setup reuses the owner");s.SetPersonalLock("1234");Check(!s.TryPersonalSession(),"Optional lock prevents direct opening");Check(!s.UnlockPersonal("0000")&&s.UnlockPersonal("1234"),"Four-character optional lock authenticates without a username");for(int i=0;i<5;i++)s.UnlockPersonal("wrong");Check(!s.UnlockPersonal("1234"),"Optional lock enforces failed-attempt lockout");s.Exec("UPDATE users SET failed=0,locked_until=NULL WHERE id=@p0",owner);s.EnablePersonalMode();Check(s.TryPersonalSession(),"Removing optional lock restores direct opening");
            bool blocked=false;try{s.EnableSharedSignIn();}catch(InvalidOperationException){blocked=true;}Check(blocked,"Shared sign-in cannot lock out the only personal owner");s.SaveUser("accountant","12345678","Accountant");s.SaveUser("manager","12345678","Admin");s.EnableSharedSignIn();Check(!s.TryPersonalSession()&&s.Login("accountant","12345678"),"Explicit shared mode requires named user sign-in");blocked=false;try{s.EnablePersonalMode();}catch(InvalidOperationException){blocked=true;}Check(blocked,"Accountant cannot bypass shared sign-in");Check(s.Login("manager","12345678"),"Administrator can enter shared mode");s.EnablePersonalMode();
        }
        using(var s=new Store(path,true)){Check(s.TryPersonalSession()&&s.User.Id==owner,"Personal access survives encrypted reopen");}
    }
}
