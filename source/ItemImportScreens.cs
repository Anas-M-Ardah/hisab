using System.Data;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace Hisab;
public sealed partial class MainWindow
{
    ItemImportPlan? itemImportPlan;
    string itemImportFile="";
    string ImportAction(string action)=>action switch {"New"=>T("جديد","New"),"Opening"=>T("رصيد افتتاحي","Opening stock"),"Skip"=>T("تخطي","Skip"),_=>T("خطأ","Error")};
    string ImportMessage(string message)=>string.Join("; ",System.Text.RegularExpressions.Regex.Split(message,@"; (?=[A-Za-z]+:)").Select(segment=>{var parts=segment.Split(" / ",2);if(parts.Length!=2)return segment;if(vm.Arabic)return parts[0];int colon=parts[0].IndexOf(':');return (colon>=0&&colon<20?parts[0][..(colon+1)]+" ":"")+parts[1];}));
    string ImportField(string field)=>field switch {"Code"=>T("كود الصنف *","Item code *"),"Name"=>T("اسم الصنف *","Item name *"),"Type"=>T("النوع (Stock / Service)","Type (Stock / Service)"),"Price"=>T("سعر البيع","Selling price"),"Barcode"=>T("الباركود","Barcode"),"OpeningQuantity"=>T("الكمية الافتتاحية","Opening quantity"),"UnitCost"=>T("تكلفة الوحدة الافتتاحية","Opening unit cost"),"Serialized"=>T("تتبع الأجهزة (Yes / No)","Serialized (Yes / No)"),_=>T("أرقام الأجهزة","Serial numbers")};
    void StartItemImport()
    {
        S.Require("master");var dialog=new OpenFileDialog{Filter="Excel workbook (*.xlsx)|*.xlsx",Title=T("اختر ملف الأصناف","Choose items workbook")};if(dialog.ShowDialog(this)!=true)return;
        var sheets=ExcelWorkbook.Read(dialog.FileName);ItemImportMappingForm(dialog.FileName,sheets);
    }
    void SaveItemImportTemplate()
    {
        var dialog=new SaveFileDialog{Filter="Excel workbook (*.xlsx)|*.xlsx",FileName="Hisab-items-template.xlsx"};if(dialog.ShowDialog(this)==true){ExcelWorkbook.WriteTemplate(dialog.FileName);vm.Status=T("تم حفظ القالب. أدخل البيانات من الصف الثاني.","Template saved. Enter data from row 2.");}
    }
    void ItemImportMappingForm(string file,List<ImportSheet> sheets)
    {
        var (w,p,finish)=Dialog(T("مطابقة أعمدة Excel","Match Excel columns"),1000,false);p.Children.Add(Text(Path.GetFileName(file),18,true));
        p.Children.Add(Text(T("كل الصفوف، بما فيها المخفية، تدخل المعاينة. احفظ الأكواد والباركود والأرقام التسلسلية كنص. الصق قيم المعادلات أولًا.","Preview includes hidden rows. Store codes, barcodes and serial numbers as text. Paste formula results as values first."),17));
        var sheet=Choose(sheets);sheet.SelectedIndex=0;p.Children.Add(Field(T("ورقة العمل","Worksheet"),sheet));var header=Input("1",true);p.Children.Add(Field(T("رقم صف العناوين (1–100)","Header row number (1–100)"),header));
        int matchedHeader=0;var mappings=new Dictionary<string,ComboBox>();var mappingPanel=new WrapPanel();
        int Header(){if(!int.TryParse(header.Text,out int n)||n<1||n>100)throw new InvalidOperationException(T("أدخل صف عناوين من 1 إلى 100","Enter a header row from 1 to 100"));return n;}
        string Normalize(string value)=>new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        void Match(){var selected=(ImportSheet)sheet.SelectedItem;int rowNumber=Header();matchedHeader=rowNumber;var labels=selected.Rows.FirstOrDefault(r=>r.Number==rowNumber);var columns=selected.Rows.SelectMany(r=>r.Cells.Keys).Distinct().Order().Select(c=>new Choice(c.ToString(),ExcelWorkbook.ColumnName(c)+" · "+(labels?.Cells.GetValueOrDefault(c)?.Text??""))).ToList();mappingPanel.Children.Clear();mappings.Clear();
            string[][] aliases={new[]{"code","itemcode","sku","كود","كودالصنف"},new[]{"name","itemname","اسم","اسمالصنف"},new[]{"type","النوع"},new[]{"price","sellingprice","سعرالبيع"},new[]{"barcode","الباركود"},new[]{"openingquantity","quantity","qty","الكميةالافتتاحية"},new[]{"unitcost","cost","تكلفةالوحدة"},new[]{"serialized","تتبعالأجهزة"},new[]{"serials","serialnumbers","أرقامالأجهزة"}};
            for(int i=0;i<ItemImportMapping.Fields.Length;i++){string field=ItemImportMapping.Fields[i];var box=new ComboBox{ItemsSource=new[]{new Choice("-1",T("غير مستخدم","Not used"))}.Concat(columns).ToList(),DisplayMemberPath="Label",SelectedValuePath="Key",SelectedIndex=0};var found=columns.FirstOrDefault(c=>aliases[i].Any(a=>Normalize(a)==Normalize(labels?.Cells.GetValueOrDefault(int.Parse(c.Key))?.Text??"")));if(found!=null)box.SelectedValue=found.Key;var control=Field(ImportField(field),box);control.Width=290;control.Margin=new Thickness(0,0,12,8);mappingPanel.Children.Add(control);mappings[field]=box;}
        }
        p.Children.Add(Btn(T("قراءة العناوين ومطابقة الأعمدة","Read headers & match columns"),Match));p.Children.Add(mappingPanel);sheet.SelectionChanged+=(_,_)=>Guard(Match);Match();
        var date=Date();p.Children.Add(Field(T("تاريخ الرصيد الافتتاحي","Opening stock date"),date));var location=LocationPicker(p);
        var existing=new CheckBox{Content=T("استخدام الأصناف الحالية لرصيد افتتاحي فقط (دون تعديل بياناتها)","Use existing items for opening stock only (preserve their details)"),IsChecked=false};p.Children.Add(existing);
        p.Children.Add(Text(T("افتراضيًا تُتخطى الأكواد الموجودة. المخزون الافتتاحي متاح فقط لصنف بلا حركات سابقة. عمود Type الفارغ يعني Stock، وPrice الفارغ يعني صفرًا.","Existing codes are skipped by default. Opening stock is allowed only for items without previous stock movements. Blank Type means Stock; blank Price means zero."),17));
        p.Children.Add(Btn(T("معاينة الاستيراد","Preview import"),()=>{if(Header()!=matchedHeader)throw new InvalidOperationException(T("أعد قراءة العناوين بعد تغيير صفها","Read headers again after changing the header row"));var rows=ItemImportMapping.Read((ImportSheet)sheet.SelectedItem,Header(),mappings.ToDictionary(x=>x.Key,x=>int.Parse(((Choice)x.Value.SelectedItem).Key)));var plan=A.PreviewItemImport(rows,GetDate(date),PickLocation(location),existing.IsChecked==true);itemImportFile=file;itemImportPlan=plan;finish();Navigate("excel-import");},true));w.ShowDialog();
    }
    void ItemImportPage()
    {
        Heading(T("معاينة استيراد Excel","Excel import preview"),Path.GetFileName(itemImportFile));
        if(itemImportPlan==null){body.Children.Add(Actions(Btn(T("اختيار ملف","Choose workbook"),StartItemImport,true),Btn(T("حفظ قالب فارغ","Save blank template"),SaveItemImportTemplate)));return;}
        var plan=itemImportPlan;var summary=Text(T($"جديد: {plan.Rows.Count(r=>r.Action=="New")} · افتتاحي: {plan.Rows.Count(r=>r.Action=="Opening")} · تخطي: {plan.Rows.Count(r=>r.Action=="Skip")} · أخطاء: {plan.Rows.Count(r=>r.Action=="Error")}",$"New: {plan.Rows.Count(r=>r.Action=="New")} · Opening: {plan.Rows.Count(r=>r.Action=="Opening")} · Skipped: {plan.Rows.Count(r=>r.Action=="Skip")} · Errors: {plan.Rows.Count(r=>r.Action=="Error")}"),18,true);body.Children.Add(summary);
        body.Children.Add(Text(T("قيمة المخزون الافتتاحي: ","Opening stock value: ")+plan.OpeningValue.ToString("N3",CultureInfo.InvariantCulture)+" JOD · "+plan.Date.ToString("yyyy-MM-dd")+" · "+S.Locations(false).Single(l=>l.Id==plan.Location).Name,17));
        var errorsOnly=new CheckBox{Content=T("الأخطاء فقط","Errors only"),IsChecked=false};body.Children.Add(errorsOnly);
        DataTable Rows(){var table=new DataTable();table.Columns.Add("id",typeof(long));foreach(var c in new[]{"row","code","name","qty","price","state","message"})table.Columns.Add(c,c=="row"?typeof(int):typeof(string));for(int i=0;i<plan.Rows.Count;i++){var r=plan.Rows[i];if(errorsOnly.IsChecked==true&&r.Action!="Error")continue;table.Rows.Add(i,r.Row,r.Code,r.Name,Store.D(Store.M(r.Quantity)),r.Price.ToString("0.###",CultureInfo.InvariantCulture),ImportAction(r.Action),ImportMessage(r.Message));}return table;}
        var grid=Grid(Rows(),("row",T("صف","Row"),.5),("code",T("الكود","Code"),1),("name",T("الصنف","Item"),2),("qty",T("افتتاحي","Opening qty"),.8),("state",T("الإجراء","Action"),1),("message",T("التحقق","Validation"),2.8));double[] widths={90,130,220,110,110,280};for(int c=0;c<widths.Length;c++)grid.Columns[c].MinWidth=widths[c];errorsOnly.Checked+=(_,_)=>grid.ItemsSource=Rows().DefaultView;errorsOnly.Unchecked+=(_,_)=>grid.ItemsSource=Rows().DefaultView;
        var details=Btn(T("تفاصيل الصف","Row details"),()=>ItemImportRowDetails(plan,checked((int)Selected(grid))));var import=Btn(T("استيراد الصفوف المعتمدة","Import reviewed rows"),()=>{if(!Confirm(T("سيتم حفظ الأصناف والأرصدة الافتتاحية المعروضة معًا. متابعة؟","Save the reviewed products and opening stock together?")))return;int count;IsEnabled=false;System.Windows.Input.Mouse.OverrideCursor=System.Windows.Input.Cursors.Wait;try{Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.Render);count=A.CommitItemImport(plan,itemImportFile);}finally{IsEnabled=true;System.Windows.Input.Mouse.OverrideCursor=null;}itemImportPlan=null;Navigate("items");vm.Status=T($"تم استيراد {count} صف بنجاح.",$"Imported {count} rows successfully.");},true);import.IsEnabled=plan.CanImport;
        body.Children.Add(Actions(Btn(T("رجوع للأصناف","Back to items"),()=>Navigate("items")),Btn(T("اختيار الملف مجددًا","Reload workbook"),StartItemImport),details,import));BindSelection(grid,details);body.Children.Add(grid);
        body.Children.Add(Text(T("لن يُحفظ شيء حتى تخلو المعاينة من الأخطاء وتؤكد الاستيراد. صحح الملف ثم أعد تحميله. النقر المزدوج يعرض التفاصيل.","Nothing is saved until the preview has no errors and you confirm import. Fix the workbook and reload it. Double-click a row for details."),16));grid.MouseDoubleClick+=(_,_)=>{if(grid.SelectedItem!=null)Guard(()=>ItemImportRowDetails(plan,checked((int)Selected(grid))));};
    }
    void ItemImportRowDetails(ItemImportPlan plan,int index)
    {
        var row=plan.Rows[index];var input=plan.Input[index];var (w,p,_)=Dialog(T("تفاصيل صف ","Row details ")+row.Row,850,false);p.Children.Add(Text(ImportAction(row.Action)+" · "+row.Code,22,true));p.Children.Add(Text(ImportMessage(row.Message),18));
        foreach(var pair in new[]{("Code",input.Code),("Name",input.Name),("Type",input.Type),("Price",input.Price),("Barcode",input.Barcode),("OpeningQuantity",input.Quantity),("UnitCost",input.Cost),("Serialized",input.Serialized),("Serials",input.Serials)})p.Children.Add(Text(ImportField(pair.Item1)+": "+(pair.Item2.Length==0?"—":pair.Item2),17));w.ShowDialog();
    }
}
