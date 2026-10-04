using System.Security.Cryptography;
namespace Hisab;
public sealed partial class Store
{
    public bool PersonalMode=>Setting("auth_mode","") is "personal" or "lock";
    public bool TryPersonalSession()
    {
        if(Setting("auth_mode","")!="personal")return false;
        var owner=Table("SELECT id,username,role FROM users WHERE personal_owner=1 AND active=1 AND role='Admin'");if(owner.Rows.Count!=1)return false;
        User=new((long)owner.Rows[0]["id"],"صاحب الدفتر",(string)owner.Rows[0]["role"]);return true;
    }
    long EnsureOwner()
    {
        Require("users");var owners=Table("SELECT id FROM users WHERE personal_owner=1");if(owners.Rows.Count>0)return (long)owners.Rows[0]["id"];
        string username="__personal_owner__";if(Convert.ToInt64(Scalar("SELECT count(*) FROM users WHERE username=@p0",username))>0)username+="_"+Guid.NewGuid().ToString("N")[..6];
        SaveUser(username,Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),"Admin");long id=Convert.ToInt64(Scalar("SELECT id FROM users WHERE username=@p0",username));Exec("UPDATE users SET personal_owner=1 WHERE id=@p0",id);return id;
    }
    public void EnablePersonalMode()
    {
        Require("users");using var tx=BeginTransaction();long id=EnsureOwner();Exec("UPDATE users SET active=1,role='Admin',failed=0,locked_until=NULL WHERE id=@p0",id);Set("auth_mode","personal");Set("setup_done","1");Audit("personal-mode","Direct opening on this Windows profile");tx.Commit();TryPersonalSession();
    }
    public void SetPersonalLock(string code)
    {
        Require("users");if(code.Length<4||code.Length>128)throw new InvalidOperationException("اختر رمزًا أو كلمة مرور من ٤ أحرف أو أرقام على الأقل / Use at least 4 characters or digits");using var tx=BeginTransaction();long id=EnsureOwner();byte[] salt=RandomNumberGenerator.GetBytes(16),hash=Rfc2898DeriveBytes.Pbkdf2(code,salt,600000,HashAlgorithmName.SHA256,32);Exec("UPDATE users SET salt=@p0,hash=@p1,active=1,role='Admin',failed=0,locked_until=NULL WHERE id=@p2",salt,hash,id);Set("auth_mode","lock");Set("setup_done","1");Audit("personal-lock","Optional lock enabled");tx.Commit();
    }
    public bool UnlockPersonal(string code)
    {
        string? username=Scalar("SELECT username FROM users WHERE personal_owner=1 AND active=1")?.ToString();return username!=null&&Login(username,code);
    }
    public void EnableSharedSignIn()
    {
        Require("users");if(Convert.ToInt64(Scalar("SELECT count(*) FROM users WHERE personal_owner=0 AND role='Admin' AND active=1"))==0)throw new InvalidOperationException("أضف حساب مدير أولًا قبل تفعيل الحسابات المشتركة / Add an administrator account first");Set("auth_mode","login");Audit("shared-sign-in","Enabled");
    }
}
