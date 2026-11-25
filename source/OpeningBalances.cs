using System.Data;
using System.Globalization;
using static Hisab.Store;

namespace Hisab;
public record OpeningBalanceInput(string AccountCode,string Reference,DateTime OriginalDate,DateTime DueDate,decimal Amount,string ExistingJournal="",bool AttachToExisting=false);
public record OpeningBalancePreview(OpeningBalanceInput Input,string Party,string Kind,long Amount,string Error);
public sealed partial class AccountingService
{
    (Account Party,long Amount,long? Source) ValidateOpeningBalance(OpeningBalanceInput input,DateTime cutoff)
    {
        store.Require("post");EnsureOpen(cutoff);
        if(input.AttachToExisting&&string.IsNullOrWhiteSpace(input.ExistingJournal))throw new InvalidOperationException("رقم القيد مطلوب عند ربط التفاصيل / Journal number is required when attaching details");
        var party=Accounts().SingleOrDefault(a=>a.Code==input.AccountCode.Trim());
        if(party==null||party.Kind is not("Customer" or "Supplier"))throw new InvalidOperationException("اختر كود عميل أو مورد / Choose a customer or supplier account code");
        long amount=M(input.Amount);
        if(amount<=0||D(amount)!=input.Amount||cutoff.Date>DateTime.Today||input.OriginalDate.Date>cutoff.Date||input.DueDate.Date<input.OriginalDate.Date||string.IsNullOrWhiteSpace(input.Reference)||input.Reference.Trim().Length>150)
            throw new InvalidOperationException("أدخل مرجعًا ومبلغًا موجبًا بثلاث منازل وتواريخ صالحة / Enter a reference, positive three-decimal amount and valid dates");
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM opening_balances o JOIN documents d ON d.id=o.doc WHERE d.party=@p0 AND o.reference=@p1 COLLATE NOCASE AND (d.reversal_date IS NULL OR d.reversal_date>@p2)",party.Id,input.Reference.Trim(),cutoff.ToString("yyyy-MM-dd")))>0)
            throw new InvalidOperationException("مرجع افتتاحي مكرر لهذا الحساب / Duplicate opening reference for this account");
        long? source=null;
        if(!string.IsNullOrWhiteSpace(input.ExistingJournal)){
            var sources=Table("SELECT * FROM documents WHERE number=@p0 AND kind='JV' AND reversed=0 AND date<=@p1",input.ExistingJournal.Trim(),cutoff.ToString("yyyy-MM-dd"));
            if(sources.Rows.Count!=1)throw new InvalidOperationException("اختر قيدًا افتتاحيًا ساريًا لا يتجاوز تاريخ الإدخال / Choose an active opening journal on or before cutoff");
            source=(long)sources.Rows[0]["id"];
            long control=AccountId("3201");
            if(Convert.ToInt64(Scalar("SELECT count(*) FROM entries WHERE doc=@p0 AND account NOT IN (@p1,@p2)",source,party.Id,control))!=0)
                throw new InvalidOperationException("يجب أن يكون القيد بين الحساب المختار وحساب الرصيد الافتتاحي فقط / Journal must contain only this party and the opening balance account");
            long available=Convert.ToInt64(Scalar("SELECT coalesce(sum(debit-credit),0) FROM entries WHERE doc=@p0 AND account=@p1",source,party.Id))*(party.Kind=="Customer"?1:-1);
            string start=cutoff.ToString("yyyy-MM-dd");
            var details=Table("SELECT d.date,d.reversal_date,d.total FROM opening_balances o JOIN documents d ON d.id=o.doc WHERE o.source_doc=@p0",source).Rows.Cast<DataRow>().ToList();
            var dates=details.Select(r=>(string)r["date"]).Where(d=>string.CompareOrdinal(d,start)>=0).Append(start).Distinct();
            long used=dates.Max(date=>details.Where(r=>string.CompareOrdinal((string)r["date"],date)<=0&&(r["reversal_date"] is not string last||string.CompareOrdinal(last,date)>0)).Sum(r=>(long)r["total"]));
            if(amount>available-used)throw new InvalidOperationException("التفاصيل تتجاوز الرصيد المتاح في القيد الافتتاحي / Details exceed the available opening journal balance");
        }
        return(party,amount,source);
    }
    public List<OpeningBalancePreview> PreviewOpeningBalances(IReadOnlyList<OpeningBalanceInput> input,DateTime cutoff)
    {
        if(input.Count==0||input.Count>ExcelWorkbook.MaxRows)throw new InvalidOperationException("أدخل من 1 إلى 5000 صف / Enter 1 to 5000 rows");
        // Use a rolled-back transaction to validate duplicate references and shared
        // source capacity across the entire batch without saving any preview data.
        using var tx=store.BeginTransaction();var result=new List<OpeningBalancePreview>();
        foreach(var row in input){try{var valid=ValidateOpeningBalance(row,cutoff);CreateOpeningBalance(row,cutoff);result.Add(new(row,valid.Party.Name,valid.Party.Kind,valid.Amount,""));}catch(InvalidOperationException ex){result.Add(new(row,"","",0,ex.Message));}}
        return result;
    }
    public List<long> ImportOpeningBalances(IReadOnlyList<OpeningBalanceInput> input,DateTime cutoff)
    {
        if(input.Count==0||input.Count>ExcelWorkbook.MaxRows)throw new InvalidOperationException("أدخل من 1 إلى 5000 صف / Enter 1 to 5000 rows");
        using var tx=store.BeginTransaction();var ids=new List<long>();foreach(var row in input)ids.Add(CreateOpeningBalance(row,cutoff));tx.Commit();return ids;
    }
    public long CreateOpeningBalance(OpeningBalanceInput input,DateTime cutoff)
    {
        using var tx=store.BeginTransaction();var valid=ValidateOpeningBalance(input,cutoff);bool receivable=valid.Party.Kind=="Customer";
        long id=NewDoc(receivable?"OC":"OS",cutoff,valid.Party.Id,null,input.Reference.Trim(),new(valid.Amount,0,valid.Amount));
        Exec("INSERT INTO opening_balances(doc,reference,original_date,source_doc) VALUES(@p0,@p1,@p2,@p3)",id,input.Reference.Trim(),input.OriginalDate.ToString("yyyy-MM-dd"),valid.Source);
        if(valid.Source==null)Journal(id,receivable?[new(valid.Party.Id,valid.Amount,0),new(AccountId("3201"),0,valid.Amount)]:[new(AccountId("3201"),valid.Amount,0),new(valid.Party.Id,0,valid.Amount)]);
        SnapshotBranding(id);Exec("UPDATE documents SET due_date=@p0 WHERE id=@p1",input.DueDate.ToString("yyyy-MM-dd"),id);
        Audit("opening-balance",id+" "+(valid.Source==null?"posted":"details for journal "+valid.Source));tx.Commit();return id;
    }
    void CheckOpeningReversal(long doc,DateTime date)
    {
        if(Convert.ToInt64(Scalar("SELECT count(*) FROM opening_balances o JOIN documents d ON d.id=o.doc WHERE o.source_doc=@p0 AND (d.reversal_date IS NULL OR d.reversal_date>@p1)",doc,date.ToString("yyyy-MM-dd")))>0)
            throw new InvalidOperationException("ألغِ تفاصيل الأرصدة الافتتاحية المرتبطة أولًا / Reverse linked opening details before changing the source journal");
    }
}
public static class OpeningBalanceWorkbook
{
    public static void WriteTemplate(string path)
    {
        ExcelWorkbook.WriteTemplate(path);
        using var book=DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(path,true);
        var wb=book.WorkbookPart??throw new InvalidOperationException("Missing workbook");var workbook=wb.Workbook??throw new InvalidOperationException("Missing workbook contents");var sheet=wb.WorksheetParts.Single().Worksheet??throw new InvalidOperationException("Missing worksheet");
        var data=sheet.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.SheetData>()!;data.RemoveAllChildren();
        var header=new DocumentFormat.OpenXml.Spreadsheet.Row{RowIndex=1};
        string[] headers=["AccountCode","Reference","OriginalDate","DueDate","Amount","ExistingJournal"];
        for(int i=0;i<headers.Length;i++)header.Append(new DocumentFormat.OpenXml.Spreadsheet.Cell{CellReference=ExcelWorkbook.ColumnName(i)+"1",DataType=DocumentFormat.OpenXml.Spreadsheet.CellValues.InlineString,InlineString=new(new DocumentFormat.OpenXml.Spreadsheet.Text(headers[i]))});
        data.Append(header);
        var columns=sheet.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.Columns>()!;columns.RemoveAllChildren();
        for(uint i=1;i<=6;i++)columns.Append(new DocumentFormat.OpenXml.Spreadsheet.Column{Min=i,Max=i,Width=24,CustomWidth=true,Style=i==5?0U:1U});
        sheet.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.AutoFilter>()!.Reference="A1:F1";
        workbook.Sheets!.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().Single().Name="Opening balances";
        sheet.Save();workbook.Save();
    }
    public static List<OpeningBalanceInput> Read(ImportSheet sheet)
    {
        if(sheet.Error.Length>0)throw new InvalidOperationException(sheet.Error);
        string[] headers=["AccountCode","Reference","OriginalDate","DueDate","Amount","ExistingJournal"];
        var first=sheet.Rows.FirstOrDefault()??throw new InvalidOperationException("Empty worksheet");
        for(int i=0;i<headers.Length;i++)if(sheet.Cell(first,i).Text!=headers[i])throw new InvalidOperationException("Columns: "+string.Join(", ",headers));
        var result=new List<OpeningBalanceInput>();
        foreach(var row in sheet.Rows.Skip(1)){
            string Get(int column){var cell=sheet.Cell(row,column);if(cell.Error.Length>0)throw new InvalidOperationException($"Row {row.Number}: {cell.Error}");if(column is 0 or 1 or 5 && cell.Text.Length>0&&!cell.IsText)throw new InvalidOperationException($"Row {row.Number}: identifiers must be text / المعرّفات نصية");return cell.Text;}
            DateTime Date(int column)=>DateTime.TryParseExact(Get(column),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var value)?value:throw new InvalidOperationException($"Row {row.Number}: use date text yyyy-MM-dd / التاريخ نص بصيغة yyyy-MM-dd");
            result.Add(new(Get(0),Get(1),Date(2),Date(3),Store.Parse(Get(4)),Get(5)));
        }
        return result;
    }
}
