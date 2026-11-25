using System.IO;
using System.Data;

namespace Hisab;
public static class OpeningBalanceTests
{
    public static void Run(string directory,List<string> results)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action act,string name){bool rejected=false;try{act();}catch(InvalidOperationException){rejected=true;}Check(rejected,name);}
        string path=Path.Combine(directory,"openings-"+Guid.NewGuid()+".db");var day=DateTime.Today.AddDays(-30);
        using(var s=new Store(path)){
            var a=new AccountingService(s);var c=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(c.Id,c.Code,c.Name,"Customer");var p=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(p.Id,p.Code,p.Name,"Supplier");long opening=s.AccountId("3201"),cash=s.AccountId("1101");
            var ar=new OpeningBalanceInput(c.Code,"OLD-AR-001",day.AddDays(-90),day.AddDays(-30),100);
            var ap=new OpeningBalanceInput(p.Code,"OLD-AP-001",day.AddDays(-90),day.AddDays(10),60);
            Reject(()=>a.CreateOpeningBalance(ar with{AttachToExisting=true},day),"Attach mode cannot silently post when journal number is blank");
            long before=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents")),audits=Convert.ToInt64(s.Scalar("SELECT count(*) FROM audit"));
            var preview=a.PreviewOpeningBalances([ar,ap],day);
            Check(preview.All(x=>x.Error=="")&&preview.Sum(x=>x.Amount)==160000,"Opening preview validates customer/supplier control totals");
            Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==before&&Convert.ToInt64(s.Scalar("SELECT count(*) FROM audit"))==audits&&s.Balance(c.Id)==0,"Opening preview persists no documents, journal or audit rows");
            var ids=a.ImportOpeningBalances([ar,ap],day);
            Check(s.Balance(c.Id)==100000&&s.Balance(p.Id)==-60000&&s.Balance(opening)==-40000,"Opening postings use AR/AP and opening equity only");
            Check(a.Positions(day,c.Id,"SI").Single().Outstanding==100000&&a.Positions(day,p.Id,"PI").Single().Outstanding==60000,"Opening documents participate in receivable/payable positions");
            Check(a.Positions(day,c.Id).Single().Due==ar.DueDate.ToString("yyyy-MM-dd")&&a.Positions(day.AddDays(-1)).Count==0,"Opening aging retains original due date and system cutoff");
            Check(a.TaxEvents(day.AddDays(-100),DateTime.Today).Rows.Count==0&&s.Balance(s.AccountId("4101"))==0&&s.Balance(s.AccountId("1301"))==0,"Opening debt creates no sales, tax or inventory");
            long rc=a.Voucher(true,day.AddDays(1),c.Id,cash,40,"receipt");a.Allocate(rc,ids[0],40,day.AddDays(1));
            long pv=a.Voucher(false,day.AddDays(1),p.Id,cash,20,"payment");a.Allocate(pv,ids[1],20,day.AddDays(1));
            Check(a.Positions(day.AddDays(1),c.Id).Single().Outstanding==60000&&a.Positions(day.AddDays(1),p.Id).Single().Outstanding==40000,"Normal receipts and payments allocate to opening debt");
            Reject(()=>a.CreateOpeningBalance(ar with{Reference="old-ar-001"},day),"Opening duplicate references reject case-insensitively");
            Reject(()=>a.ReturnInvoice(ids[0],day.AddDays(1),[new(1,1)],"not stock"),"Opening debt cannot enter stock returns");
            a.Reverse(ids[0],"wrong opening",day.AddDays(2));
            Check(a.Positions(day.AddDays(1),c.Id).Single().Outstanding==60000&&a.Positions(day.AddDays(2),c.Id).Count==0&&s.Balance(c.Id)==-40000,"Opening reversal preserves earlier aging and later payment credit");
            long j=a.Manual(day,"existing opening",[new(c.Id,90000,0),new(opening,0,90000)]);string number=s.Scalar("SELECT number FROM documents WHERE id=@p0",j)!.ToString()!;
            var detail=ar with{Reference="EXISTING-1",Amount=50,ExistingJournal=number};long ledger=s.Balance(c.Id);
            var excess=a.PreviewOpeningBalances([detail,detail with{Reference="EXISTING-2"}],day);
            Check(excess[0].Error==""&&excess[1].Error.Length>0&&s.Balance(c.Id)==ledger,"Preview detects combined source capacity without duplicate posting");
            before=Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"));Reject(()=>a.ImportOpeningBalances([detail,detail with{Reference="EXISTING-2"}],day),"Opening import rejects shared-source overrun");
            Check(Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))==before&&s.Balance(c.Id)==ledger,"Failed opening batch rolls back its earlier valid rows");
            var details=a.ImportOpeningBalances([detail,detail with{Reference="EXISTING-2",Amount=40}],day);
            Check(s.Balance(c.Id)==ledger&&Convert.ToInt64(s.Scalar("SELECT count(*) FROM entries WHERE doc IN (@p0,@p1)",details[0],details[1]))==0,"Existing journal detail mode adds no ledger entries");
            Reject(()=>a.Reverse(j,"remove source",day.AddDays(3)),"Opening source journal cannot reverse while details are active");
            Reject(()=>a.AmendSimple(j,day,null,null,0,"replace",[new(c.Id,80000,0),new(opening,0,80000)],"edit"),"Opening source journal cannot be amended behind active details");
            a.Reverse(details[0],"remove detail",day.AddDays(3));Check(s.Balance(c.Id)==ledger,"Reversing existing-journal detail leaves source balance intact");
            a.Reverse(details[1],"remove detail",day.AddDays(3));
            Reject(()=>a.CreateOpeningBalance(detail with{Reference="BACKDATED",Amount=90},day.AddDays(1)),"Reversed opening details still consume historical source capacity");
            Reject(()=>a.CreateOpeningBalance(detail,day.AddDays(1)),"Opening reference cannot be reused during its historical interval");
            Reject(()=>a.Reverse(j,"backdated source",day.AddDays(1)),"Source reversal cannot precede linked detail reversal dates");
            Reject(()=>a.AmendSimple(j,day,null,null,0,"replace",[new(c.Id,80000,0),new(opening,0,80000)],"backdate"),"Source amendment cannot erase historical detail backing");
            a.Reverse(j,"remove source",day.AddDays(3));
            Check(s.Balance(c.Id)==ledger-90000,"Source journal reverses after linked details are reversed");
            var duplicate=a.PreviewOpeningBalances([ar with{Reference="BATCH"},ar with{Reference="batch"}],day);
            Check(duplicate[1].Error.Length>0,"Duplicate references inside one preview are detected");
            Reject(()=>a.CreateOpeningBalance(ar with{Reference="BAD",Amount=-10},day),"Opening outstanding amount must be positive");
            Reject(()=>a.CreateOpeningBalance(ar with{Reference="BAD",DueDate=ar.OriginalDate.AddDays(-1)},day),"Opening due date cannot precede original invoice date");
            s.SaveUser("reader","ReaderPassword123","Viewer");s.Login("reader","ReaderPassword123");Reject(()=>a.CreateOpeningBalance(ar with{Reference="DENIED"},day),"Viewer cannot post opening balances");
            string backup=Path.Combine(directory,"opening-restore.db");s.Backup(backup);
        }
        using(var reopened=new Store(path))Check(Convert.ToInt64(reopened.Scalar("PRAGMA user_version"))==Store.SchemaVersion&&Convert.ToInt64(reopened.Scalar("SELECT count(*) FROM opening_balances"))==4,"Opening details survive database reopening");
        string migration=Path.Combine(directory,"opening-schema7.db");using(var s=new Store(migration)){s.Exec("DROP TABLE opening_balances; ALTER TABLE receipt_bills DROP COLUMN current_variance; ALTER TABLE receipt_bills DROP COLUMN inventory_adjustment; PRAGMA user_version=7");}
        using(var upgraded=new Store(migration)){Check(Convert.ToInt64(upgraded.Scalar("PRAGMA user_version"))==8&&Convert.ToInt64(upgraded.Scalar("SELECT count(*) FROM opening_balances"))==0,"Schema 7 upgrades safely to opening-balance schema 8");Check(upgraded.Table("PRAGMA table_info(receipt_bills)").Rows.Cast<DataRow>().Count(r=>r["name"].ToString() is "current_variance" or "inventory_adjustment")==2,"Schema 7 receipt bills gain both adjustment fields");}
        using(var restore=new Store(Path.Combine(directory,"opening-restored.db"))){restore.Restore(Path.Combine(directory,"opening-restore.db"));Check(Convert.ToInt64(restore.Scalar("SELECT count(*) FROM opening_balances"))==4&&restore.Table("PRAGMA foreign_key_check").Rows.Count==0,"Opening links survive backup and restore with valid references");}
        var headers=new[]{"AccountCode","Reference","OriginalDate","DueDate","Amount","ExistingJournal"};
        var sheet=new ImportSheet("Openings",[new(1,headers.Select((h,i)=>(h,i)).ToDictionary(x=>x.i,x=>new ImportCell(x.h))),new(2,new(){[0]=new("001"),[1]=new("0002"),[2]=new("2026-01-01"),[3]=new("2026-02-01"),[4]=new("1,250.500")})]);
        var rows=OpeningBalanceWorkbook.Read(sheet);Check(rows.Single().AccountCode=="001"&&rows.Single().Reference=="0002"&&rows.Single().Amount==1250.5m,"Opening workbook preserves identifiers and parses grouped amount");
        sheet.Rows[1].Cells[0]=new("1",false);Reject(()=>OpeningBalanceWorkbook.Read(sheet),"Opening workbook rejects numeric identifiers that may lose zeros");
        string template=Path.Combine(directory,"opening-template.xlsx");OpeningBalanceWorkbook.WriteTemplate(template);
        using(var book=DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(template,false))Check(!new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(book).Any(),"Opening Excel template has valid workbook structure");
        Check(ExcelWorkbook.Read(template).Single().Rows[0].Cells[0].Text=="AccountCode"&&OpeningBalanceWorkbook.Read(ExcelWorkbook.Read(template).Single()).Count==0,"Opening template reads with expected headers and no sample balances");
    }
}
