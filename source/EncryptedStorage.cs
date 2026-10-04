using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Hisab;
// SQLite runs in memory. Only authenticated encrypted snapshots are written to disk.
public sealed class EncryptedStorage
{
    readonly byte[] key;
    readonly string path;
    static readonly byte[] LocalHeader=Encoding.ASCII.GetBytes("HISAB2-L");
    static readonly byte[] PortableHeader=Encoding.ASCII.GetBytes("HISAB2-P");
    public EncryptedStorage(string file)
    {
        path=file;string keyFile=file+".key";
        if(File.Exists(keyFile)) key=ProtectedData.Unprotect(File.ReadAllBytes(keyFile),Encoding.UTF8.GetBytes("Hisab encrypted database v2"),DataProtectionScope.CurrentUser);
        else {
            if(File.Exists(file))throw new InvalidOperationException("مفتاح البيانات مفقود. استعد نسخة احتياطية على جهاز جديد. / Data key missing; restore a portable backup.");
            key=RandomNumberGenerator.GetBytes(32);AtomicWrite(keyFile,ProtectedData.Protect(key,Encoding.UTF8.GetBytes("Hisab encrypted database v2"),DataProtectionScope.CurrentUser));
        }
    }
    public byte[]? Read()=>File.Exists(path)?Decrypt(File.ReadAllBytes(path),key,LocalHeader):null;
    public void Save(byte[] data)=>AtomicWrite(path,Encrypt(data,key,LocalHeader));
    public void LocalBackup(string target,byte[] data)=>AtomicWrite(target,Encrypt(data,key,LocalHeader));
    public byte[] ReadBackup(string source,string? password)
    {
        byte[] file=File.ReadAllBytes(source);
        if(file.AsSpan().StartsWith(LocalHeader))return Decrypt(file,key,LocalHeader);
        if(file.AsSpan().StartsWith(PortableHeader))return PortableDecrypt(file,password??throw new InvalidOperationException("كلمة مرور النسخة الاحتياطية مطلوبة / Backup password required"));
        if(file.AsSpan().StartsWith(Encoding.ASCII.GetBytes("SQLite format 3\0")))return file;
        throw new InvalidOperationException("صيغة النسخة غير صالحة / Invalid backup format");
    }
    public static void PortableBackup(string target,byte[] data,string password)
    {
        if(password.Length<10)throw new InvalidOperationException("كلمة مرور النسخة يجب أن تتكون من ١٠ أحرف على الأقل / Backup password must contain at least 10 characters");
        byte[] salt=RandomNumberGenerator.GetBytes(16);byte[] derived=Rfc2898DeriveBytes.Pbkdf2(password,salt,600000,HashAlgorithmName.SHA256,32);
        try{byte[] header=PortableHeader.Concat(salt).ToArray();AtomicWrite(target,Encrypt(data,derived,header));}finally{CryptographicOperations.ZeroMemory(derived);}
    }
    static byte[] PortableDecrypt(byte[] file,string password)
    {
        if(file.Length<52)throw new InvalidOperationException("نسخة تالفة / Damaged backup");
        byte[] header=file[..24];byte[] derived=Rfc2898DeriveBytes.Pbkdf2(password,file.AsSpan(8,16),600000,HashAlgorithmName.SHA256,32);
        try{return Decrypt(file,derived,header);}finally{CryptographicOperations.ZeroMemory(derived);}
    }
    static byte[] Encrypt(byte[] data,byte[] key,byte[] header)
    {
        byte[] nonce=RandomNumberGenerator.GetBytes(12),tag=new byte[16],cipher=new byte[data.Length];using var aes=new AesGcm(key,16);aes.Encrypt(nonce,data,cipher,tag,header);return header.Concat(nonce).Concat(tag).Concat(cipher).ToArray();
    }
    static byte[] Decrypt(byte[] file,byte[] key,byte[] header)
    {
        if(file.Length<header.Length+28||!file.AsSpan().StartsWith(header))throw new InvalidOperationException("نسخة تالفة / Damaged encrypted file");
        byte[] data=new byte[file.Length-header.Length-28];try{using var aes=new AesGcm(key,16);aes.Decrypt(file.AsSpan(header.Length,12),file.AsSpan(header.Length+28),file.AsSpan(header.Length+12,16),data,header);return data;}catch(CryptographicException){throw new InvalidOperationException("كلمة المرور غير صحيحة أو النسخة تالفة / Incorrect password or damaged encrypted file");}
    }
    static void AtomicWrite(string target,byte[] data)
    {
        string temporary=target+"."+Guid.NewGuid()+".tmp";
        try{using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(data);file.Flush(true);}for(int attempt=0;;attempt++){try{if(File.Exists(target))File.Replace(temporary,target,null);else File.Move(temporary,target);break;}catch(IOException)when(attempt<4){Thread.Sleep(100*(attempt+1));}catch(UnauthorizedAccessException)when(attempt<4){Thread.Sleep(100*(attempt+1));}}}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    [DllImport("e_sqlite3",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_serialize(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)]string schema,out long size,uint flags);
    [DllImport("e_sqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_deserialize(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)]string schema,IntPtr data,long size,long bufferSize,uint flags);
    [DllImport("e_sqlite3",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_malloc64(ulong size);
    [DllImport("e_sqlite3",CallingConvention=CallingConvention.Cdecl)]static extern void sqlite3_free(IntPtr pointer);
    public static byte[] Snapshot(SqliteConnection db)
    {
        var p=sqlite3_serialize(db.Handle!.DangerousGetHandle(),"main",out long size,0);if(p==IntPtr.Zero||size<=0||size>int.MaxValue)throw new InvalidOperationException("Database snapshot failed");
        try{byte[] data=new byte[(int)size];Marshal.Copy(p,data,0,data.Length);return data;}finally{sqlite3_free(p);}
    }
    public static void Load(SqliteConnection db,byte[] data)
    {
        IntPtr p=sqlite3_malloc64((ulong)data.Length);if(p==IntPtr.Zero)throw new OutOfMemoryException();Marshal.Copy(data,0,p,data.Length);
        int result=sqlite3_deserialize(db.Handle!.DangerousGetHandle(),"main",p,data.Length,data.Length,3);if(result!=0){sqlite3_free(p);throw new InvalidOperationException("Database snapshot load failed: "+result);}
    }
}
