using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Hisab;
public record AppUser(long Id,string Username,string Role);
public sealed partial class Store
{
    public AppUser User {get;private set;}=new(0,"System","Admin");
    int transactions;
    bool ready;
    readonly EncryptedStorage? encryption;
    public bool Encrypted=>encryption!=null;
    public void Require(string permission)
    {
        if(User.Id>0){var current=Table("SELECT role,active FROM users WHERE id=@p0",User.Id);if(current.Rows.Count==0||(long)current.Rows[0]["active"]!=1)throw new InvalidOperationException("الحساب غير فعال؛ سجّل الدخول من جديد / Account inactive; sign in again");User=User with{Role=(string)current.Rows[0]["role"]};}
        if(permission=="preferences")return;
        if(User.Role=="Admin")return;
        if(User.Role=="Accountant"&&permission is "post" or "master" or "attachment" or "company")return;
        throw new InvalidOperationException("لا تملك صلاحية هذه العملية / You do not have permission for this operation");
    }
    void Upgrade()
    {
        void Column(string table,string field,string definition){if(!Table("PRAGMA table_info("+table+")").Rows.Cast<DataRow>().Any(r=>r["name"].ToString()==field))Exec($"ALTER TABLE {table} ADD COLUMN {field} {definition}");}
        Column("accounts","name_ar","TEXT NOT NULL DEFAULT ''");Column("accounts","name_en","TEXT NOT NULL DEFAULT ''");Column("accounts","address_ar","TEXT NOT NULL DEFAULT ''");Column("accounts","address_en","TEXT NOT NULL DEFAULT ''");
        Column("accounts","parent_id","INTEGER REFERENCES accounts(id)");
        Exec("CREATE INDEX IF NOT EXISTS ix_accounts_parent ON accounts(parent_id)");
        Column("items","name_ar","TEXT NOT NULL DEFAULT ''");Column("items","name_en","TEXT NOT NULL DEFAULT ''");Column("items","last_cost","INTEGER NOT NULL DEFAULT 0");
        Column("documents","due_date","TEXT NOT NULL DEFAULT ''");Column("documents","reversal_date","TEXT");Column("documents","branding","TEXT NOT NULL DEFAULT '{}'");Column("documents","logo","BLOB");Column("documents","seal","BLOB");Column("documents","replaced_by","INTEGER REFERENCES documents(id)");
        Column("invoice_lines","source_line","INTEGER REFERENCES invoice_lines(id)");Column("audit","username","TEXT NOT NULL DEFAULT 'System'");
        Column("invoice_lines","description_ar","TEXT NOT NULL DEFAULT ''");Column("invoice_lines","description_en","TEXT NOT NULL DEFAULT ''");
        Exec("""
        CREATE TABLE IF NOT EXISTS allocations(id INTEGER PRIMARY KEY,voucher INTEGER NOT NULL REFERENCES documents(id),invoice INTEGER NOT NULL REFERENCES documents(id),amount INTEGER NOT NULL CHECK(amount>0),date TEXT NOT NULL,revoked TEXT);
        CREATE TABLE IF NOT EXISTS attachments(id INTEGER PRIMARY KEY,doc INTEGER NOT NULL REFERENCES documents(id),name TEXT NOT NULL,data BLOB NOT NULL,created TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
        CREATE TABLE IF NOT EXISTS users(id INTEGER PRIMARY KEY,username TEXT UNIQUE COLLATE NOCASE NOT NULL,role TEXT NOT NULL CHECK(role IN ('Admin','Accountant','Viewer')),salt BLOB NOT NULL,hash BLOB NOT NULL,active INTEGER NOT NULL DEFAULT 1,failed INTEGER NOT NULL DEFAULT 0,locked_until TEXT);
        CREATE TABLE IF NOT EXISTS periods(id INTEGER PRIMARY KEY,start TEXT NOT NULL,end TEXT NOT NULL,closing_doc INTEGER NOT NULL REFERENCES documents(id),created TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
        CREATE TABLE IF NOT EXISTS cheques(id INTEGER PRIMARY KEY,number TEXT NOT NULL,bank TEXT NOT NULL,direction TEXT NOT NULL,party INTEGER NOT NULL REFERENCES accounts(id),amount INTEGER NOT NULL CHECK(amount>0),issue_date TEXT NOT NULL,due_date TEXT NOT NULL,state TEXT NOT NULL DEFAULT 'Pending',issue_doc INTEGER NOT NULL REFERENCES documents(id),settlement_doc INTEGER REFERENCES documents(id),UNIQUE(number,bank,direction));
        CREATE TABLE IF NOT EXISTS revisions(id INTEGER PRIMARY KEY,doc INTEGER NOT NULL REFERENCES documents(id),replacement INTEGER REFERENCES documents(id),reason TEXT NOT NULL,snapshot TEXT NOT NULL,username TEXT NOT NULL,created TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
        CREATE TABLE IF NOT EXISTS cost_history(id INTEGER PRIMARY KEY,doc INTEGER NOT NULL REFERENCES documents(id),old_cost INTEGER NOT NULL,new_cost INTEGER NOT NULL,reason TEXT NOT NULL,created TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
        CREATE INDEX IF NOT EXISTS ix_allocations_invoice ON allocations(invoice,date);
        """);
        Column("users","personal_owner","INTEGER NOT NULL DEFAULT 0");
        if(Convert.ToInt32(Scalar("PRAGMA user_version"))<2){
            Exec("PRAGMA foreign_keys=OFF");
            Exec("""
            CREATE TABLE items_v2(id INTEGER PRIMARY KEY,code TEXT UNIQUE NOT NULL,name TEXT NOT NULL,stock INTEGER NOT NULL DEFAULT 1,price INTEGER NOT NULL DEFAULT 0 CHECK(price>=0),qty INTEGER NOT NULL DEFAULT 0,value INTEGER NOT NULL DEFAULT 0,name_ar TEXT NOT NULL DEFAULT '',name_en TEXT NOT NULL DEFAULT '',last_cost INTEGER NOT NULL DEFAULT 0);
            INSERT INTO items_v2 SELECT id,code,name,stock,price,qty,value,name_ar,name_en,last_cost FROM items;
            DROP TABLE items; ALTER TABLE items_v2 RENAME TO items;
            PRAGMA user_version=2;
            """);Exec("PRAGMA foreign_keys=ON");
        }
        Exec("UPDATE documents SET due_date=date WHERE due_date=''");
        Exec("UPDATE documents SET reversal_date=(SELECT date FROM documents rv WHERE rv.original=documents.id AND rv.kind='RV' LIMIT 1) WHERE reversed=1 AND reversal_date IS NULL");
        Exec("INSERT OR IGNORE INTO accounts(code,name,kind,system) VALUES('2190','شيكات صادرة','Liability',1),('3301','أرباح محتجزة','Equity',1)");
        foreach(var pair in new[]{("allow_negative","0"),("due_days","30"),("company_en",""),("terms_en",""),("warranty_en",""),("shipping_en",""),("bank_en",""),("logo",""),("seal","")})Exec("INSERT OR IGNORE INTO settings VALUES(@p0,@p1)",pair.Item1,pair.Item2);
    }
    public IDisposableTransaction BeginTransaction()=>new(this);
    public sealed class IDisposableTransaction : IDisposable
    {
        readonly Store store;readonly Microsoft.Data.Sqlite.SqliteTransaction? tx;readonly string? savepoint;readonly byte[]? before;readonly AppUser beforeUser;bool committed;
        public IDisposableTransaction(Store s){store=s;beforeUser=s.User;if(s.transactions++==0){before=s.Encrypted?EncryptedStorage.Snapshot(s.Db):null;tx=s.Db.BeginTransaction();}else{savepoint="nested_"+s.transactions;s.Exec("SAVEPOINT "+savepoint);}}
        public void Commit(){if(savepoint!=null){store.Exec("RELEASE "+savepoint);committed=true;return;}tx!.Commit();tx.Dispose();try{store.Persist();committed=true;}catch{if(before!=null)EncryptedStorage.Load(store.Db,before);throw;}}
        public void Dispose(){try{if(!committed){store.User=beforeUser;if(savepoint!=null){store.Exec("ROLLBACK TO "+savepoint);store.Exec("RELEASE "+savepoint);}else tx?.Dispose();}}finally{store.transactions--;}}
    }
    public void Persist(){if(ready&&encryption!=null)encryption.Save(EncryptedStorage.Snapshot(Db));}
    public void PortableBackup(string destination,string password){Require("backup");EncryptedStorage.PortableBackup(destination,EncryptedStorage.Snapshot(Db),password);Audit("portable-backup",System.IO.Path.GetFileName(destination));}
    public void RestorePortable(string source,string? password=null)
    {
        Require("restore");byte[] data=encryption!=null?encryption.ReadBackup(source,password):ReadPortableOrPlain(source,password);
        using var candidate=new Store(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!,"validation-"+Guid.NewGuid()+".db"),memoryOnly:true);EncryptedStorage.Load(candidate.Db,data);
        if(candidate.Scalar("PRAGMA integrity_check")?.ToString()!="ok"||Convert.ToInt32(candidate.Scalar("PRAGMA user_version"))>SchemaVersion)throw new InvalidOperationException("نسخة غير صالحة / Invalid backup");
        foreach(var table in new[]{"accounts","documents","entries","items","settings"})candidate.Scalar("SELECT count(*) FROM "+table);
        if(candidate.Table("PRAGMA foreign_key_check").Rows.Count!=0||Convert.ToInt64(candidate.Scalar("SELECT count(*) FROM (SELECT doc FROM entries GROUP BY doc HAVING sum(debit)!=sum(credit))"))!=0)throw new InvalidOperationException("نسخة غير متوازنة / Invalid backup journals");
        candidate.Upgrade();var dir=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!,"backups");Directory.CreateDirectory(dir);Backup(System.IO.Path.Combine(dir,"before-restore-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+(Encrypted?".hdb":".db")));
        byte[] before=EncryptedStorage.Snapshot(Db);var previousUser=User;try{if(Encrypted)EncryptedStorage.Load(Db,EncryptedStorage.Snapshot(candidate.Db));else candidate.Db.BackupDatabase(Db);Persist();User=new(0,"Restore","Admin");Audit("restore",System.IO.Path.GetFileName(source));}catch{User=previousUser;if(Encrypted){EncryptedStorage.Load(Db,before);Persist();}throw;}
    }
    static byte[] ReadPortableOrPlain(string source,string? password){if(File.ReadAllBytes(source).AsSpan().StartsWith(Encoding.ASCII.GetBytes("HISAB2-P"))){var temp=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(source)!,"key-test-"+Guid.NewGuid());try{return new EncryptedStorage(temp).ReadBackup(source,password);}finally{if(File.Exists(temp+".key"))File.Delete(temp+".key");}}return File.ReadAllBytes(source);}
    public void SaveTranslations(string table,long id,string arabic,string english,string addressArabic="",string addressEnglish="")
    {
        Require("master");if(table is not ("accounts" or "items"))throw new ArgumentException();using var tx=BeginTransaction();Exec($"UPDATE {table} SET name_ar=@p0,name_en=@p1 WHERE id=@p2",arabic,english,id);if(table=="accounts")Exec("UPDATE accounts SET address_ar=@p0,address_en=@p1 WHERE id=@p2",addressArabic,addressEnglish,id);Audit("translation",table+" "+id);tx.Commit();
    }
    public string LocalName(string table,long id,bool arabic){var r=Table($"SELECT name,name_ar,name_en FROM {table} WHERE id=@p0",id).Rows[0];return string.IsNullOrWhiteSpace(r[arabic?"name_ar":"name_en"].ToString())?(string)r["name"]:(string)r[arabic?"name_ar":"name_en"];}
    public void AddAttachment(long doc,string filename)
    {
        Require("attachment");var file=new FileInfo(filename);if(file.Length>20*1024*1024)throw new InvalidOperationException("حد المرفق ٢٠ ميغابايت / Attachment limit is 20 MB");using var tx=BeginTransaction();Exec("INSERT INTO attachments(doc,name,data) VALUES(@p0,@p1,@p2)",doc,file.Name,File.ReadAllBytes(filename));Audit("attachment",doc+": "+file.Name);tx.Commit();
    }
    public void SaveUser(string username,string password,string role,long? id=null,bool active=true)
    {
        Require("users");if(string.IsNullOrWhiteSpace(username)||role is not("Admin" or "Accountant" or "Viewer")||password.Length<8)throw new InvalidOperationException("اسم المستخدم وكلمة مرور من ٨ أحرف على الأقل والدور مطلوبة / Name, 8-character password and valid role required");
        if(id!=null&&Convert.ToInt64(Scalar("SELECT count(*) FROM users WHERE role='Admin' AND active=1 AND id<>@p0",id))==0&&(role!="Admin"||!active))throw new InvalidOperationException("يجب إبقاء مدير فعال / Keep at least one active administrator");
        byte[] salt=RandomNumberGenerator.GetBytes(16),hash=Rfc2898DeriveBytes.Pbkdf2(password,salt,600000,HashAlgorithmName.SHA256,32);using var tx=BeginTransaction();
        if(id==null)Exec("INSERT INTO users(username,role,salt,hash,active) VALUES(@p0,@p1,@p2,@p3,@p4)",username.Trim(),role,salt,hash,active?1:0);else Exec("UPDATE users SET username=@p0,role=@p1,salt=@p2,hash=@p3,active=@p4,failed=0,locked_until=NULL WHERE id=@p5",username.Trim(),role,salt,hash,active?1:0,id);Audit("user",username+" "+role);tx.Commit();
    }
    public bool Login(string username,string password)
    {
        var users=Table("SELECT * FROM users WHERE username=@p0 AND active=1",username);if(users.Rows.Count==0){Rfc2898DeriveBytes.Pbkdf2(password,new byte[16],600000,HashAlgorithmName.SHA256,32);return false;}
        var r=users.Rows[0];if(r["locked_until"]!=DBNull.Value&&DateTime.Parse((string)r["locked_until"]).ToUniversalTime()>DateTime.UtcNow)return false;
        bool ok=CryptographicOperations.FixedTimeEquals((byte[])r["hash"],Rfc2898DeriveBytes.Pbkdf2(password,(byte[])r["salt"],600000,HashAlgorithmName.SHA256,32));
        if(!ok){int failed=Convert.ToInt32(r["failed"])+1;Exec("UPDATE users SET failed=@p0,locked_until=@p1 WHERE id=@p2",failed,failed>=5?DateTime.UtcNow.AddMinutes(5).ToString("O"):null,r["id"]);return false;}
        Exec("UPDATE users SET failed=0,locked_until=NULL WHERE id=@p0",r["id"]);User=new((long)r["id"],(string)r["username"],(string)r["role"]);Audit("login",User.Username);return true;
    }
}
