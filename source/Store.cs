using System.Data;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Hisab;

public record Account(long Id, string Code, string Name, string Kind, bool System, long? ParentId=null)
{ public override string ToString() => $"{Name} · {Code}"; }
public record Item(long Id, string Code, string Name, bool Stock, long Price, long Qty, long Value)
{ public override string ToString() => $"{Name} · {Code}"; }
public record EntryLine(long Account, long Debit, long Credit);
public record InvoiceLine(long Item, string Description, decimal Qty, decimal Price, decimal Discount);
public record Totals(long Net, long Tax, long Total);

public sealed partial class Store : IDisposable
{
    public const int SchemaVersion = 2;
    public string Path { get; }
    public SqliteConnection Db { get; }
    public Store(string path,bool encrypted=false,bool memoryOnly=false)
    {
        Path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        if(encrypted)encryption=new EncryptedStorage(path);
        Db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=encrypted||memoryOnly?":memory:":path, Pooling=false }.ToString());
        Db.Open();
        var snapshot=encryption?.Read();if(snapshot!=null)EncryptedStorage.Load(Db,snapshot);
        string legacy=System.IO.Path.ChangeExtension(path,".db");bool imported=false;
        if(encrypted&&snapshot==null&&legacy!=path&&File.Exists(legacy)){using var old=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=legacy,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());old.Open();old.BackupDatabase(Db);imported=true;}
        Exec("PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000; PRAGMA synchronous=FULL;");
        var version = Convert.ToInt32(Scalar("PRAGMA user_version"));
        if (version > SchemaVersion) throw new InvalidOperationException("This database requires a newer version of Hisab.");
        Exec("""
        CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY,value TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS accounts(id INTEGER PRIMARY KEY,code TEXT UNIQUE NOT NULL,name TEXT NOT NULL,kind TEXT NOT NULL,system INTEGER NOT NULL DEFAULT 0);
        CREATE TABLE IF NOT EXISTS items(id INTEGER PRIMARY KEY,code TEXT UNIQUE NOT NULL,name TEXT NOT NULL,stock INTEGER NOT NULL DEFAULT 1,price INTEGER NOT NULL DEFAULT 0 CHECK(price>=0),qty INTEGER NOT NULL DEFAULT 0 CHECK(qty>=0),value INTEGER NOT NULL DEFAULT 0 CHECK(value>=0));
        CREATE TABLE IF NOT EXISTS documents(id INTEGER PRIMARY KEY,number TEXT UNIQUE NOT NULL,kind TEXT NOT NULL,date TEXT NOT NULL,party INTEGER REFERENCES accounts(id),cash INTEGER REFERENCES accounts(id),note TEXT NOT NULL,net INTEGER NOT NULL,tax INTEGER NOT NULL,total INTEGER NOT NULL,paid INTEGER NOT NULL DEFAULT 0,vat TEXT NOT NULL DEFAULT '0',inclusive INTEGER NOT NULL DEFAULT 0,company TEXT NOT NULL DEFAULT '',terms TEXT NOT NULL DEFAULT '',warranty TEXT NOT NULL DEFAULT '',shipping TEXT NOT NULL DEFAULT '',bank TEXT NOT NULL DEFAULT '',reversed INTEGER NOT NULL DEFAULT 0,original INTEGER REFERENCES documents(id),created TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
        CREATE TABLE IF NOT EXISTS entries(id INTEGER PRIMARY KEY,doc INTEGER NOT NULL REFERENCES documents(id),account INTEGER NOT NULL REFERENCES accounts(id),debit INTEGER NOT NULL CHECK(debit>=0),credit INTEGER NOT NULL CHECK(credit>=0),CHECK((debit>0 AND credit=0) OR (credit>0 AND debit=0)));
        CREATE TABLE IF NOT EXISTS invoice_lines(id INTEGER PRIMARY KEY,doc INTEGER NOT NULL REFERENCES documents(id),item INTEGER NOT NULL REFERENCES items(id),description TEXT NOT NULL,qty INTEGER NOT NULL CHECK(qty>0),price INTEGER NOT NULL CHECK(price>=0),discount INTEGER NOT NULL CHECK(discount>=0),net INTEGER NOT NULL,tax INTEGER NOT NULL,cost INTEGER NOT NULL DEFAULT 0);
        CREATE TABLE IF NOT EXISTS stock_moves(id INTEGER PRIMARY KEY,doc INTEGER NOT NULL REFERENCES documents(id),item INTEGER NOT NULL REFERENCES items(id),qty INTEGER NOT NULL,value INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY,action TEXT NOT NULL,detail TEXT NOT NULL,created TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
        CREATE INDEX IF NOT EXISTS ix_entries_account ON entries(account,doc);
        CREATE INDEX IF NOT EXISTS ix_documents_date ON documents(date);
        CREATE INDEX IF NOT EXISTS ix_moves_item ON stock_moves(item,doc);
        """);
        if(version==0)Exec("PRAGMA user_version=1");
        if (Convert.ToInt64(Scalar("SELECT count(*) FROM accounts")) == 0) Seed();
        Upgrade();ready=true;Persist();
        if(imported){encryption!.LocalBackup(legacy+".encrypted.hdb",EncryptedStorage.Snapshot(Db));File.Delete(legacy);}
        if(Encrypted){var backups=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!,"backups");if(Directory.Exists(backups))foreach(var file in Directory.EnumerateFiles(backups,"*.db")){byte[] bytes=File.ReadAllBytes(file);if(!bytes.AsSpan().StartsWith(System.Text.Encoding.ASCII.GetBytes("SQLite format 3\0")))continue;string secured=System.IO.Path.ChangeExtension(file,".hdb");encryption!.LocalBackup(secured,bytes);if(!encryption.ReadBackup(secured,null).AsSpan().SequenceEqual(bytes))throw new IOException("Backup encryption verification failed");File.Delete(file);}}
    }
    public SqliteCommand Command(string sql, params object?[] values)
    {
        var cmd = Db.CreateCommand(); cmd.CommandText=sql;
        for (int i=0;i<values.Length;i++) cmd.Parameters.AddWithValue("@p"+i,values[i] ?? DBNull.Value);
        return cmd;
    }
    public void Exec(string sql, params object?[] args) { byte[]? before=ready&&transactions==0&&Encrypted?EncryptedStorage.Snapshot(Db):null;using(var cmd=Command(sql,args))cmd.ExecuteNonQuery();if(ready&&transactions==0)try{Persist();}catch{if(before!=null)EncryptedStorage.Load(Db,before);throw;} }
    public object? Scalar(string sql, params object?[] args) { using var cmd=Command(sql,args); return cmd.ExecuteScalar(); }
    public DataTable Table(string sql, params object?[] args) { using var cmd=Command(sql,args); using var r=cmd.ExecuteReader(); var t=new DataTable();for(int i=0;i<r.FieldCount;i++)t.Columns.Add(r.GetName(i),typeof(object));while(r.Read()){var values=new object[r.FieldCount];r.GetValues(values);t.Rows.Add(values);}return t; }
    public string Setting(string key,string fallback="") => Scalar("SELECT value FROM settings WHERE key=@p0",key)?.ToString() ?? fallback;
    public void Set(string key,string value) {if(ready)Require(key is "language" or "font"?"preferences":"settings");Exec("INSERT INTO settings VALUES(@p0,@p1) ON CONFLICT(key) DO UPDATE SET value=excluded.value",key,value);}
    public static long M(decimal value) => checked((long)decimal.Round(value*1000,0,MidpointRounding.AwayFromZero));
    public static decimal D(long value) => value/1000m;
    public static string Money(long value) => D(value).ToString("N3",CultureInfo.InvariantCulture);
    public static decimal Parse(string text)
    {
        var cleaned=new string(text.Trim().Select(c => c>='٠'&&c<='٩'?(char)('0'+c-'٠'):c>='۰'&&c<='۹'?(char)('0'+c-'۰'):c=='٫'?'.':c=='٬'?',':c).ToArray());
        if (!decimal.TryParse(cleaned,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var n)) throw new InvalidOperationException("أدخل رقمًا صحيحًا، مثل 1250.500\nEnter a valid number, e.g. 1250.500");
        return n;
    }
    static string[] Resource(string name)
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Hisab."+name)!;
        return JsonSerializer.Deserialize<string[]>(stream)!;
    }
    void Seed()
    {
        using var tx=BeginTransaction();
        var core=new (string Code,string Name,string Kind)[] {
            ("1101","الصندوق","Cash"),("1102","البنك","Cash"),("1103","البنك توفير","Cash"),("1104","شيكات برسم التحصيل","Asset"),
            ("1301","المخزون","Asset"),("1401","ضريبة المشتريات","Asset"),("1501","الاثاث","Asset"),("2102","ضريبة المبيعات","Liability"),
            ("3101","جاري الشريك محمد","Equity"),("3102","جاري الشربك ايمن","Equity"),("3201","رصيد افتتاحي","Equity"),
            ("4101","المبيعات","Income"),("5101","مصاريف عامة","Expense"),("5102","مصاريف بيع","Expense"),("5104","تكلفة المبيعات","Expense") };
        foreach(var a in core) Exec("INSERT INTO accounts(code,name,kind,system) VALUES(@p0,@p1,@p2,1)",a.Code,a.Name,a.Kind);
        int n=1;
        foreach(var raw in Resource("accounts.json")) {
            var name=raw.Trim(); if(core.Any(a=>a.Name==name)) continue;
            Exec("INSERT INTO accounts(code,name,kind) VALUES(@p0,@p1,'Unclassified')",("A"+n++.ToString("D3")),name);
        }
        n=1; foreach(var name in Resource("items.json").Distinct())
            Exec("INSERT INTO items(code,name) VALUES(@p0,@p1)","ITM-"+n++.ToString("D3"),name.Trim());
        Set("language","ar"); Set("vat","16"); Set("company","نظام المحاسبة"); Set("terms",""); Set("warranty",""); Set("shipping",""); Set("bank",""); Set("font","20");
        tx.Commit();
    }
    public List<Account> Accounts() => Table("SELECT * FROM accounts ORDER BY code").Rows.Cast<DataRow>().Select(r=>new Account((long)r["id"],(string)r["code"],(string)r["name"],(string)r["kind"],(long)r["system"]==1,r["parent_id"]==DBNull.Value?null:(long)r["parent_id"])).ToList();
    public List<Item> Items() => Table("SELECT * FROM items ORDER BY code").Rows.Cast<DataRow>().Select(r=>new Item((long)r["id"],(string)r["code"],(string)r["name"],(long)r["stock"]==1,(long)r["price"],(long)r["qty"],(long)r["value"])).ToList();
    public long AccountId(string code)=>Convert.ToInt64(Scalar("SELECT id FROM accounts WHERE code=@p0",code) ?? throw new InvalidOperationException("Account missing"));
    public void SaveAccount(long? id,string code,string name,string kind,long? parentId=null)
    {
        Require("master");
        if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("اسم الحساب وكوده مطلوبان / Account name and code are required");
        using var tx=BeginTransaction();
        ValidateAccountParent(id,kind,parentId);
        if(id!=null) {
            var a=Accounts().Single(x=>x.Id==id);
            if(a.System && (a.Kind!=kind||a.Code!=code)) throw new InvalidOperationException("لا يمكن تغيير نوع أو كود الحساب الأساسي / Core account type and code cannot change");
            if(a.Kind!=kind && Convert.ToInt64(Scalar("SELECT count(*) FROM entries WHERE account=@p0",id))>0) throw new InvalidOperationException("لا يمكن تغيير نوع حساب مستخدم / Used account type cannot change");
            Exec("UPDATE accounts SET code=@p0,name=@p1,kind=@p2,parent_id=@p4 WHERE id=@p3",code.Trim(),name.Trim(),kind,id,parentId);
        } else Exec("INSERT INTO accounts(code,name,kind,parent_id) VALUES(@p0,@p1,@p2,@p3)",code.Trim(),name.Trim(),kind,parentId);
        Audit("account",name); tx.Commit();
    }
    public void SaveItem(long? id,string code,string name,bool stock,decimal price)
    {
        Require("master");
        if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(code)||price<0) throw new InvalidOperationException("أكمل الاسم والكود والسعر / Complete name, code and price");
        using var tx=BeginTransaction();
        if(id!=null) {
            var item=Items().Single(x=>x.Id==id);
            if(item.Stock!=stock && Convert.ToInt64(Scalar("SELECT count(*) FROM invoice_lines WHERE item=@p0",id))>0) throw new InvalidOperationException("لا يمكن تغيير نوع صنف مستخدم / Used item type cannot change");
            Exec("UPDATE items SET code=@p0,name=@p1,stock=@p2,price=@p3 WHERE id=@p4",code.Trim(),name.Trim(),stock?1:0,M(price),id);
        } else Exec("INSERT INTO items(code,name,stock,price) VALUES(@p0,@p1,@p2,@p3)",code.Trim(),name.Trim(),stock?1:0,M(price));
        Audit("item",name); tx.Commit();
    }
    public long Balance(long id)=>Convert.ToInt64(Scalar("SELECT coalesce(sum(debit-credit),0) FROM entries WHERE account=@p0",id));
    internal void Audit(string action,string detail)=>Exec("INSERT INTO audit(action,detail,username) VALUES(@p0,@p1,@p2)",action,detail,User.Username);
    public void Backup(string destination)
    {
        if(System.IO.Path.GetFullPath(destination).Equals(System.IO.Path.GetFullPath(Path),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("اختر ملفًا مختلفًا / Choose another file");
        if(encryption!=null)encryption.LocalBackup(destination,EncryptedStorage.Snapshot(Db));else{using var target=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=destination,Pooling=false}.ToString()); target.Open(); Db.BackupDatabase(target);}
        Audit("backup",System.IO.Path.GetFileName(destination));
    }
    public void Restore(string source)
    {
        Require("restore");if(encryption!=null){RestorePortable(source);return;}
        if(System.IO.Path.GetFullPath(source).Equals(System.IO.Path.GetFullPath(Path),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("اختر نسخة احتياطية مختلفة عن قاعدة البيانات الحالية / Choose a backup other than the current database");
        using var candidate=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString()); candidate.Open();
        using var check=candidate.CreateCommand(); check.CommandText="PRAGMA integrity_check";
        if(check.ExecuteScalar()?.ToString()!="ok") throw new InvalidOperationException("نسخة احتياطية تالفة / Backup integrity check failed");
        check.CommandText="PRAGMA user_version"; if(Convert.ToInt32(check.ExecuteScalar())!=SchemaVersion) throw new InvalidOperationException("نسخة غير متوافقة / Incompatible backup");
        foreach(var name in new[]{"settings","accounts","items","documents","entries","invoice_lines","stock_moves","audit"}) {check.CommandText=$"SELECT count(*) FROM {name}"; check.ExecuteScalar();}
        check.CommandText="PRAGMA foreign_key_check"; using(var r=check.ExecuteReader()) if(r.Read()) throw new InvalidOperationException("Backup has invalid references");
        check.CommandText="SELECT count(*) FROM (SELECT doc FROM entries GROUP BY doc HAVING sum(debit)!=sum(credit))"; if(Convert.ToInt64(check.ExecuteScalar())!=0) throw new InvalidOperationException("Backup has unbalanced journals");
        var safe=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!,"backups"); Directory.CreateDirectory(safe); Backup(System.IO.Path.Combine(safe,"before-restore-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".db"));
        candidate.BackupDatabase(Db); Upgrade(); Audit("restore",System.IO.Path.GetFileName(source));
    }
    public void Dispose(){Db.Dispose();}
}

