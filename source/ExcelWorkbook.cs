using System.IO;
using System.IO.Compression;
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
namespace Hisab;
public record ImportCell(string Text,bool IsText=true,string Error="");
public record ImportSheetRow(int Number,Dictionary<int,ImportCell> Cells);
public record ImportSheet(string Name,List<ImportSheetRow> Rows,string Error="")
{
    public override string ToString()=>Name;
    public ImportCell Cell(ImportSheetRow row,int column)=>row.Cells.GetValueOrDefault(column,new ImportCell(""));
}
public static class ExcelWorkbook
{
    public const int MaxRows=5000;
    public static string ColumnName(int column){string text="";for(int n=column+1;n>0;n=(n-1)/26)text=(char)('A'+(n-1)%26)+text;return text;}
    static int ColumnIndex(string reference){int n=0;foreach(char c in reference.TakeWhile(char.IsLetter)){if(c<'A'||c>'Z')throw new InvalidOperationException("Invalid Excel cell address");n=checked(n*26+c-'A'+1);}return n-1;}
    public static List<ImportSheet> Read(string path)
    {
        if(!Path.GetExtension(path).Equals(".xlsx",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("احفظ الملف بصيغة xlsx / Save the workbook as .xlsx first");
        if(new FileInfo(path).Length>10*1024*1024)throw new InvalidOperationException("الملف كبير؛ قسّمه / Workbook exceeds 10 MB; split it into smaller files");
        try{
            using var stream=File.OpenRead(path);using(var zip=new ZipArchive(stream,ZipArchiveMode.Read,true)){if(zip.Entries.Count>2000||zip.Entries.Sum(e=>e.Length)>30*1024*1024)throw new InvalidOperationException("حجم المصنف بعد الفتح كبير / Expanded workbook exceeds 30 MB");}
            stream.Position=0;using var book=SpreadsheetDocument.Open(stream,false,new OpenSettings{MaxCharactersInPart=30*1024*1024});var part=book.WorkbookPart??throw new InvalidOperationException("Missing workbook");
            if(book.DocumentType!=SpreadsheetDocumentType.Workbook)throw new InvalidOperationException("استخدم مصنف xlsx دون وحدات ماكرو / Use a standard .xlsx workbook");
            var strings=part.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(s=>string.Concat(s.Descendants<Text>().Select(t=>t.Text))).ToArray()??[];
            var styles=part.WorkbookStylesPart?.Stylesheet;var formats=styles?.CellFormats?.Elements<CellFormat>().ToArray()??[];
            bool DateStyle(Cell cell){if(cell.StyleIndex==null||cell.StyleIndex.Value>=formats.Length)return false;uint id=formats[cell.StyleIndex.Value].NumberFormatId?.Value??0;if(id is >=14 and <=22 or >=45 and <=47)return true;string code=styles?.NumberingFormats?.Elements<NumberingFormat>().FirstOrDefault(f=>f.NumberFormatId?.Value==id)?.FormatCode?.Value??"";code=System.Text.RegularExpressions.Regex.Replace(code,@"""[^""]*""|\[[^\]]*\]|\\.","");return System.Text.RegularExpressions.Regex.IsMatch(code,"[ymdhs]",System.Text.RegularExpressions.RegexOptions.IgnoreCase);}
            var result=new List<ImportSheet>();int totalRows=0;
            foreach(var sheet in (part.Workbook??throw new InvalidOperationException("Missing workbook contents")).Sheets?.Elements<Sheet>()??[]){if(result.Count>=20)throw new InvalidOperationException("الحد 20 ورقة / Maximum 20 worksheets");if(sheet.Id==null||part.GetPartById(sheet.Id!) is not WorksheetPart ws)continue;
                var worksheet=ws.Worksheet??throw new InvalidOperationException("Missing worksheet");if(worksheet.Descendants<MergeCell>().Any()){result.Add(new(sheet.Name?.Value??"Sheet",[],"ألغ دمج الخلايا في الورقة المختارة / Unmerge cells in the selected worksheet"));continue;}
                var rows=new List<ImportSheetRow>();var seenRows=new HashSet<int>();int nextRow=1;
                foreach(var row in worksheet.GetFirstChild<SheetData>()?.Elements<Row>()??[]){int number=checked((int)(row.RowIndex?.Value??(uint)nextRow));nextRow=number+1;if(number<1||!seenRows.Add(number))throw new InvalidOperationException("Invalid or repeated Excel row");var cells=new Dictionary<int,ImportCell>();int nextColumn=0;
                    foreach(var cell in row.Elements<Cell>()){int column=cell.CellReference==null?nextColumn:ColumnIndex(cell.CellReference!);nextColumn=column+1;string value=cell.CellValue?.Text??"",error="";var type=cell.DataType?.Value;bool isText=type==CellValues.SharedString||type==CellValues.InlineString||type==CellValues.String;
                        if(type==CellValues.SharedString){if(!int.TryParse(value,out int index)||index<0||index>=strings.Length)throw new InvalidOperationException("Invalid shared string reference");value=strings[index];}else if(type==CellValues.InlineString)value=string.Concat(cell.InlineString?.Descendants<Text>().Select(t=>t.Text)??[]);
                        else if(type==CellValues.Boolean)value=value=="1"?"true":"false";
                        if(cell.CellFormula!=null)error="استبدل المعادلة بقيمتها / Replace formulas with pasted values";else if(type==CellValues.Error)error="خلية Excel بها خطأ / Excel error cell";else if(type==CellValues.Date||!isText&&DateStyle(cell))error="استخدم رقمًا أو نصًا، وليس تاريخًا / Use a number or text, not an Excel date";
                        if(!isText&&(type==null||type==CellValues.Number)&&decimal.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out decimal numberValue))value=numberValue.ToString("0.############################",CultureInfo.InvariantCulture);if(value.Length==0&&error.Length==0)continue;if(column<0||column>=64)throw new InvalidOperationException("الحد 64 عمودًا / Maximum 64 populated columns");if(value.Length>32767||!cells.TryAdd(column,new(value.Trim(),isText,error)))throw new InvalidOperationException("Invalid Excel cell");
                    }
                    if(cells.Count>0){rows.Add(new(number,cells));if(++totalRows>MaxRows+20)throw new InvalidOperationException("الحد 5000 صف بيانات للمصنف / Maximum 5000 data rows per workbook");}
                }
                result.Add(new(sheet.Name?.Value??"Sheet",rows.OrderBy(r=>r.Number).ToList()));
            }
            if(result.Count==0)throw new InvalidOperationException("لا توجد أوراق بيانات / No worksheets found");return result;
        }catch(Exception ex)when(ex is InvalidDataException or OpenXmlPackageException or System.Xml.XmlException or ArgumentException or FormatException or OverflowException){throw new InvalidOperationException("تعذر قراءة المصنف؛ احفظ نسخة xlsx جديدة / Cannot read workbook; save a fresh .xlsx copy",ex);}
    }
    // The app can generate its own import template without requiring Excel.
    public static void WriteTemplate(string path)
    {
        using var doc=SpreadsheetDocument.Create(path,SpreadsheetDocumentType.Workbook);var wb=doc.AddWorkbookPart();wb.Workbook=new Workbook();var ws=wb.AddNewPart<WorksheetPart>();var data=new SheetData();
        string[] headers={"Code","Name","Type","Price","Barcode","OpeningQuantity","UnitCost","Serialized","Serials"};var header=new Row{RowIndex=1};for(int i=0;i<headers.Length;i++)header.Append(new Cell{CellReference=ColumnName(i)+"1",DataType=CellValues.InlineString,InlineString=new InlineString(new Text(headers[i]))});data.Append(header);
        var stylePart=wb.AddNewPart<WorkbookStylesPart>();stylePart.Stylesheet=new Stylesheet(new Fonts(new Font()),new Fills(new Fill(new PatternFill{PatternType=PatternValues.None}),new Fill(new PatternFill{PatternType=PatternValues.Gray125})),new Borders(new Border()),new CellStyleFormats(new CellFormat()),new CellFormats(new CellFormat(),new CellFormat{NumberFormatId=49,ApplyNumberFormat=true}));
        var columns=new Columns();for(uint i=1;i<=9;i++)columns.Append(new Column{Min=i,Max=i,Width=i==2||i==9?32:22,CustomWidth=true,Style=i is 1 or 5 or 9?1U:0U});
        ws.Worksheet=new Worksheet(new SheetViews(new SheetView(new Pane{VerticalSplit=1,TopLeftCell="A2",ActivePane=PaneValues.BottomLeft,State=PaneStateValues.Frozen}){WorkbookViewId=0}),columns,data,new AutoFilter{Reference="A1:I1"});wb.Workbook.Append(new Sheets(new Sheet{Name="Items",SheetId=1,Id=wb.GetIdOfPart(ws)}));wb.Workbook.Save();
    }
}
