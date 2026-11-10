using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace Hisab;
public sealed partial class MainWindow
{
    public void RenderItemImportPreviews(string path)
    {
        Directory.CreateDirectory(path);previewDirectory=path;int itemCount=S.Items().Count;long documents=Convert.ToInt64(S.Scalar("SELECT count(*) FROM documents"));
        var good=ItemImportTests.Input("00042") with {Serialized="Yes",Serials="DEV-001,DEV-002"};var skip=ItemImportTests.Input(S.Items().First().Code,"0","0") with {Row=3,Barcode=""};var bad=ItemImportTests.Input("BAD-ROW","-1") with {Row=4,Barcode=""};var service=ItemImportTests.Input("INSTALL","0","0") with {Row=5,Type="Service",Barcode="",Name="Installation / تركيب"};
        itemImportFile=ItemImportTests.Fixture(path,"import-preview",good,skip,bad,service);var sheets=ExcelWorkbook.Read(itemImportFile);var rows=ItemImportMapping.Read(sheets.Single(),1,ItemImportTests.Mapping());
        foreach(string language in new[]{"ar","en"})foreach(int font in new[]{14,24}){
            S.Set("language",language);S.Set("font",font.ToString());itemImportPlan=A.PreviewItemImport(rows,DateTime.Today,1);vm.Route="excel-import";BuildShell();UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
            var import=Descendants<Button>(body).Single(b=>b.Content?.ToString()==T("استيراد الصفوف المعتمدة","Import reviewed rows"));if(import.IsEnabled)throw new InvalidOperationException("Invalid batch can be imported");Capture(this,Path.Combine(path,language+"-import-errors-"+font+".png"));
            var errors=Descendants<CheckBox>(body).Single();errors.IsChecked=true;var grid=Descendants<DataGrid>(workspaceViewport).First();if(grid.Items.Count!=1)throw new InvalidOperationException("Error filter failed");PreviewDialog("import-row-"+font,()=>ItemImportRowDetails(itemImportPlan,2));
            itemImportPlan=A.PreviewItemImport(rows.Where(r=>r.Code!="BAD-ROW").ToList(),DateTime.Today,1);Navigate("excel-import");UpdateLayout();import=Descendants<Button>(body).Single(b=>b.Content?.ToString()==T("استيراد الصفوف المعتمدة","Import reviewed rows"));if(!import.IsEnabled)throw new InvalidOperationException("Valid batch cannot be imported");Capture(this,Path.Combine(path,language+"-import-ready-"+font+".png"));
            PreviewDialog("import-mapping-"+font,()=>ItemImportMappingForm(itemImportFile,sheets));
            if(S.Items().Count!=itemCount||Convert.ToInt64(S.Scalar("SELECT count(*) FROM documents"))!=documents)throw new InvalidOperationException("Preview wrote data");File.AppendAllText(Path.Combine(path,"ui-checks.txt"),"PASS "+language+"/"+font+" mapping, errors, row details and guarded import preview"+Environment.NewLine);
        }
    }
}
