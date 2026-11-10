using System.Globalization;
using System.Data;
using static Hisab.Store;
namespace Hisab;
public record ItemImportInput(int Row,string Code,string Name,string Type,string Price,string Barcode,string Quantity,string Cost,string Serialized,string Serials,string Error="");
public record ItemImportRow(int Row,string Code,string Name,string Action,string Message,long? Existing,bool Stock,decimal Price,string Barcode,decimal Quantity,decimal Cost,bool Serialized,string Serials);
public record ItemImportPlan(IReadOnlyList<ItemImportInput> Input,DateTime Date,long Location,bool UseExisting,IReadOnlyList<ItemImportRow> Rows)
{
    public bool CanImport=>Rows.All(r=>r.Action!="Error")&&Rows.Any(r=>r.Action is "New" or "Opening");
    public decimal OpeningValue=>Rows.Where(r=>r.Action is "New" or "Opening").Sum(r=>D(M(r.Quantity*r.Cost)));
}
public static class ItemImportMapping
{
    public static readonly string[] Fields={"Code","Name","Type","Price","Barcode","OpeningQuantity","UnitCost","Serialized","Serials"};
    public static List<ItemImportInput> Read(ImportSheet sheet,int headerRow,IReadOnlyDictionary<string,int> map)
    {
        if(sheet.Error.Length>0)throw new InvalidOperationException(sheet.Error);
        if(headerRow<1||headerRow>100||!map.TryGetValue("Code",out int code)||code<0||!map.TryGetValue("Name",out int name)||name<0)throw new InvalidOperationException("اربط عمودي الكود والاسم / Map Code and Name columns");
        if(map.Values.Where(v=>v>=0).Distinct().Count()!=map.Values.Count(v=>v>=0)||map.Any(p=>!Fields.Contains(p.Key)||p.Value< -1||p.Value>=64))throw new InvalidOperationException("لا تستخدم العمود لأكثر من حقل / Map each column only once");
        var result=new List<ItemImportInput>();foreach(var row in sheet.Rows.Where(r=>r.Number>headerRow)){
            ImportCell Cell(string field)=>sheet.Cell(row,map.GetValueOrDefault(field,-1));string Get(string field)=>Cell(field).Text;
            if(Fields.All(f=>Get(f).Length==0&&Cell(f).Error.Length==0))continue;
            var errors=Fields.Where(f=>Cell(f).Error.Length>0).Select(f=>f+": "+Cell(f).Error).ToList();foreach(string field in new[]{"Code","Barcode","Serials"})if(Get(field).Length>0&&!Cell(field).IsText)errors.Add(field+": احفظ المعرّف كنص للحفاظ على الأصفار / Store identifiers as text to preserve leading zeros");
            result.Add(new(row.Number,Get("Code"),Get("Name"),Get("Type"),Get("Price"),Get("Barcode"),Get("OpeningQuantity"),Get("UnitCost"),Get("Serialized"),Get("Serials"),string.Join("; ",errors)));
        }
        if(result.Count==0||result.Count>ExcelWorkbook.MaxRows)throw new InvalidOperationException("اختر من 1 إلى 5000 صف بيانات / Select 1 to 5000 data rows");return result;
    }
}
public sealed partial class AccountingService
{
    public ItemImportPlan PreviewItemImport(IReadOnlyList<ItemImportInput> input,DateTime date,long location,bool useExisting=false)
    {
        store.Require("master");if(input.Count==0||input.Count>ExcelWorkbook.MaxRows)throw new InvalidOperationException("Import requires 1 to 5000 rows");
        var existing=Items().GroupBy(i=>i.Code.Trim(),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.OrdinalIgnoreCase);
        var codes=input.GroupBy(r=>r.Code.Trim(),StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1).Select(g=>g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var used=Table("SELECT DISTINCT item FROM stock_moves").Rows.Cast<DataRow>().Select(r=>(long)r[0]).ToHashSet();
        var stockData=Table("SELECT id,barcode,serialized FROM items").Rows.Cast<DataRow>().ToDictionary(r=>(long)r["id"]);
        var fileBarcodes=input.Where(r=>r.Barcode.Trim().Length>0).GroupBy(r=>NormalizeStockCode(r.Barcode)).Where(g=>g.Count()>1).Select(g=>g.Key).ToHashSet();
        var result=new List<ItemImportRow>();
        foreach(var row in input){string code=row.Code.Trim(),name=row.Name.Trim(),barcode=NormalizeStockCode(row.Barcode);long? id=null;bool stock=true,serialized=false;decimal price=0,quantity=0,cost=0;string serials="",action="Error",message="";
            try{
                if(row.Error.Length>0)throw new InvalidOperationException(row.Error);
                if(code.Length==0||code.Length>100||codes.Contains(code))throw new InvalidOperationException("كود مطلوب وفريد في الملف (100 حرف كحد أقصى) / Code must be present and unique within the file (max 100 characters)");
                var matches=existing.GetValueOrDefault(code);if(matches?.Count>1)throw new InvalidOperationException("كود ملتبس في الأصناف الحالية / Existing item code is ambiguous");var current=matches?.Single();id=current?.Id;
                if(current!=null&&!useExisting){name=current.Name;action="Skip";message="الصنف موجود؛ لا تغيير / Existing item; unchanged";}
                else{
                    stock=row.Type.Trim().ToLowerInvariant() switch {"" or "stock" or "مخزني"=>true,"service" or "خدمة"=>false,_=>throw new InvalidOperationException("النوع Stock أو Service / Type must be Stock or Service")};
                    bool Flag(string value)=>value.Trim().ToLowerInvariant() switch {"" or "0" or "false" or "no" or "لا"=>false,"1" or "true" or "yes" or "نعم"=>true,_=>throw new InvalidOperationException("التتبع Yes أو No / Serialized must be Yes or No")};
                    decimal Number(string value){decimal n=value.Length==0?0:Parse(value);if(n<0||n>1000000000m||D(M(n))!=n)throw new InvalidOperationException("رقم غير سالب بثلاث منازل عشرية، بحد أقصى مليار / Use a nonnegative number with at most 3 decimals, maximum one billion");return n;}
                    price=Number(row.Price);quantity=Number(row.Quantity);cost=Number(row.Cost);serialized=Flag(row.Serialized);serials=string.Join("\n",SerialList(row.Serials));
                    if(current!=null){if(row.Type.Length==0)stock=current.Stock;if(row.Serialized.Length==0)serialized=store.IsSerialized(current.Id);if(stock!=current.Stock||serialized!=store.IsSerialized(current.Id))throw new InvalidOperationException("نوع الصنف أو التتبع لا يطابق الحالي / Item type or serial tracking differs from existing item");name=current.Name;price=D(current.Price);barcode=(string)stockData[current.Id]["barcode"];if(quantity<=0)throw new InvalidOperationException("أدخل رصيدًا افتتاحيًا للصنف الحالي أو اختر التخطي / Supply opening stock for the existing item or select Skip");}
                    if(name.Length==0||name.Length>500)throw new InvalidOperationException("اسم مطلوب حتى 500 حرف / Name is required (max 500 characters)");
                    if(!stock&&(quantity!=0||cost!=0||serialized||serials.Length>0))throw new InvalidOperationException("الخدمات لا تحمل مخزونًا أو أجهزة / Services cannot have stock or serials");
                    if(quantity==0&&(cost!=0||serials.Length>0))throw new InvalidOperationException("التكلفة والأجهزة تتطلب كمية افتتاحية / Cost and serials require an opening quantity");
                    var numbers=SerialList(serials);if(serialized&&(quantity!=decimal.Truncate(quantity)||numbers.Length!=quantity)||!serialized&&numbers.Length>0||numbers.Select(NormalizeStockCode).Distinct().Count()!=numbers.Length)throw new InvalidOperationException("رقم جهاز فريد لكل وحدة؛ كمية الأجهزة عدد صحيح / Provide one unique serial per whole serialized unit");
                    if(current==null&&serialized){store.Require("post");EnsureOpen(DateTime.Today);}
                    if(current==null&&barcode.Length>0&&(barcode.Length>100||fileBarcodes.Contains(barcode)||stockData.Values.Any(r=>((string)r["barcode"]).Equals(barcode,StringComparison.OrdinalIgnoreCase))))throw new InvalidOperationException("باركود مكرر أو طويل / Barcode is duplicated or too long");
                    if(quantity>0){store.Require("post");RequireLocation(location);EnsureOpen(date);if(date.Date>DateTime.Today)throw new InvalidOperationException("التاريخ مستقبلي / Opening date cannot be in the future");if(cost<=0||M(quantity*cost)<=0)throw new InvalidOperationException("تكلفة افتتاحية موجبة مطلوبة / Opening stock requires positive unit cost and value");if(id!=null&&used.Contains(id.Value))throw new InvalidOperationException("الصنف له حركات؛ لا يمكن استيراد رصيد افتتاحي آخر / Item has stock movements; another opening balance is not allowed");if(id!=null&&Convert.ToInt64(Scalar("SELECT count(*) FROM stock_counts c JOIN stock_count_lines l ON c.id=l.count_id WHERE l.item=@p0 AND c.state='Open'",id))>0)throw new InvalidOperationException("أكمل الجرد المفتوح أولًا / Complete the open stock count first");if(id!=null&&numbers.Any(n=>Scalar("SELECT id FROM serial_units WHERE item=@p0 AND normalized=@p1",id,NormalizeStockCode(n))!=null))throw new InvalidOperationException("رقم الجهاز موجود / Serial already exists");}
                    action=current==null?"New":"Opening";message=current==null?"صنف جديد / New item":"رصيد افتتاحي فقط؛ بيانات الصنف محفوظة / Opening only; item details unchanged";
                }
            }catch(Exception ex)when(ex is InvalidOperationException or OverflowException){action="Error";message=ex is OverflowException?"القيمة كبيرة جدًا / Value is too large":ex.Message;}
            result.Add(new(row.Row,code,name,action,message,id,stock,price,barcode,quantity,cost,serialized,serials));
        }
        // Bound the batch total before the UI or accounting can overflow.
        if(result.Where(r=>r.Action is "New" or "Opening").Sum(r=>r.Quantity*r.Cost)>1000000000000m)throw new InvalidOperationException("إجمالي الاستيراد كبير جدًا / Import value exceeds the supported limit");
        return new(input.ToArray(),date.Date,location,useExisting,result);
    }
    public int CommitItemImport(ItemImportPlan reviewed,string filename)
    {
        store.Require("master");using var tx=store.BeginTransaction();var fresh=PreviewItemImport(reviewed.Input,reviewed.Date,reviewed.Location,reviewed.UseExisting);
        if(!fresh.CanImport||!fresh.Rows.SequenceEqual(reviewed.Rows))throw new InvalidOperationException("تغيرت البيانات أو توجد أخطاء؛ أعد المعاينة / Data changed or contains errors; preview again");
        int count=0;deferredRebuild++;try{foreach(var row in fresh.Rows.Where(r=>r.Action is "New" or "Opening")){long id;if(row.Existing is long found)id=found;else{store.SaveItem(null,row.Code,row.Name,row.Stock,row.Price);id=Convert.ToInt64(Scalar("SELECT id FROM items WHERE code=@p0",row.Code));if(row.Serialized||row.Barcode.Length>0)ConfigureInventoryItem(id,row.Barcode,row.Serialized,[]);}if(row.Quantity>0)OpeningStock(id,fresh.Date,row.Quantity,row.Cost,fresh.Location,row.Serials);count++;}}finally{deferredRebuild--;}
        RebuildInventory("Excel import");Audit("excel-import",System.IO.Path.GetFileName(filename)+": "+count+" rows; "+fresh.Rows.Count(r=>r.Action=="Skip")+" skipped");tx.Commit();return count;
    }
}
