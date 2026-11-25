using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Hisab;
public record ReportSnapshot(string Key,string Title,string Note,string Company,string Language,string Filters,string[] Headers,string[][] Rows)
{
    public static ReportSnapshot Capture(string key,string title,string note,string company,string language,string filters,DataTable data)=>new(key,title,note,company,language,filters,data.Columns.Cast<DataColumn>().Select(c=>c.ColumnName).ToArray(),data.Rows.Cast<DataRow>().Select(r=>r.ItemArray.Select(v=>v?.ToString()??"").ToArray()).ToArray());
    public DataTable ToTable(){var data=new DataTable();foreach(string h in Headers)data.Columns.Add(h);foreach(var row in Rows)data.Rows.Add(row.Cast<object>().ToArray());return data;}
}
public sealed partial class Store
{
    public long SaveReportSnapshot(ReportSnapshot snapshot)
    {
        Require("master");if(snapshot.Language is not("ar" or "en")||string.IsNullOrWhiteSpace(snapshot.Title)||snapshot.Headers.Length==0||snapshot.Headers.Distinct().Count()!=snapshot.Headers.Length||snapshot.Rows.Any(r=>r.Length!=snapshot.Headers.Length))throw new InvalidOperationException("Invalid report snapshot / نسخة تقرير غير صالحة");
        string payload=JsonSerializer.Serialize(snapshot),hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        using var tx=BeginTransaction();Exec("INSERT INTO report_snapshots(created,username,title,payload,hash) VALUES(@p0,@p1,@p2,@p3,@p4)",DateTime.UtcNow.ToString("O"),User.Username,snapshot.Title,payload,hash);long id=Convert.ToInt64(Scalar("SELECT last_insert_rowid()"));Audit("report-snapshot",id+" / "+hash);tx.Commit();return id;
    }
    public ReportSnapshot LoadReportSnapshot(long id)
    {
        var table=Table("SELECT payload,hash FROM report_snapshots WHERE id=@p0",id);if(table.Rows.Count==0)throw new InvalidOperationException("Unknown snapshot / نسخة غير معروفة");var row=table.Rows[0];string payload=(string)row["payload"];
        if(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))!=(string)row["hash"])throw new InvalidOperationException("Snapshot integrity check failed / فشل التحقق من سلامة نسخة التقرير");
        return JsonSerializer.Deserialize<ReportSnapshot>(payload)??throw new InvalidOperationException("Invalid snapshot / نسخة غير صالحة");
    }
    public DataTable ReportSnapshots()=>Table("SELECT id,created,username,title FROM report_snapshots ORDER BY id DESC");
}
public static class ReportCsv
{
    public static string SerializeSnapshot(ReportSnapshot snapshot)
    {
        var metadata=new DataTable();metadata.Columns.Add("Snapshot metadata");metadata.Columns.Add("Value");metadata.Rows.Add("Title",snapshot.Title);metadata.Rows.Add("Company",snapshot.Company);metadata.Rows.Add("Language",snapshot.Language);metadata.Rows.Add("Filters",snapshot.Filters);metadata.Rows.Add("Notes",snapshot.Note);return Serialize(metadata)+Environment.NewLine+Serialize(snapshot.ToTable());
    }
    public static string Serialize(DataTable table)
    {
        string Cell(string text){if(!decimal.TryParse(text,NumberStyles.Number,CultureInfo.InvariantCulture,out _)&&(text.TrimStart().StartsWith('=')||text.TrimStart().StartsWith('+')||text.TrimStart().StartsWith('-')||text.TrimStart().StartsWith('@')))text="'"+text;return "\""+text.Replace("\"","\"\"")+"\"";}
        var text=new StringBuilder();text.AppendLine(string.Join(",",table.Columns.Cast<DataColumn>().Select(c=>Cell(c.ColumnName))));foreach(DataRow row in table.Rows)text.AppendLine(string.Join(",",row.ItemArray.Select(v=>Cell(v?.ToString()??""))));return text.ToString();
    }
}
