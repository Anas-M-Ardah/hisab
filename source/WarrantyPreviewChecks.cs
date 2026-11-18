using System.IO;
using System.Data;
using System.Windows.Controls;
namespace Hisab;
public sealed partial class MainWindow
{
    public void RenderWarrantyPreviews(string path)
    {
        Directory.CreateDirectory(path);previewDirectory=path;var customer=S.Accounts().First(a=>a.Kind=="Customer");S.SaveItem(null,"WARRANTY-PREVIEW","Business laptop / حاسوب محمول",true,0);long item=S.Items().Single(i=>i.Code=="WARRANTY-PREVIEW").Id;A.ConfigureInventoryItem(item,"WARRANTY-PREVIEW",true,[]);A.OpeningStock(item,DateTime.Today,3,250,1,"LAPTOP-001,LAPTOP-002,LAPTOP-003");long sale=A.Invoice(true,DateTime.Today,customer.Id,null,0,16,false,"",[new(item,"Business laptop",1,350,0,1,"LAPTOP-001")]);long unit=Convert.ToInt64(S.Scalar("SELECT id FROM serial_units WHERE number='LAPTOP-001'"));long warranty=A.SaveWarranty(sale,unit,DateTime.Today.AddYears(1),"Manufacturing defects / عيوب التصنيع");long claim=A.ReceiveWarranty(warranty,DateTime.Today,"Will not power on / لا يعمل","No visible damage / بدون أضرار ظاهرة","Charger / الشاحن");
        foreach(string language in new[]{"ar","en"})foreach(int font in new[]{14,24}){
            S.Set("language",language);S.Set("font",font.ToString());vm.Route="warranties";BuildShell();UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);Capture(this,Path.Combine(path,language+"-warranties-"+font+".png"));
            PreviewDialog("warranty-device-picker-"+font,WarrantyDevicePicker);PreviewDialog("warranty-registration-"+font,()=>WarrantyRegistration(S.WarrantyDevices().Rows[0]));PreviewDialog("warranty-certificate-"+font,()=>PrintWarrantyCertificate(warranty));PreviewDialog("warranty-intake-"+font,()=>WarrantyIntake(warranty));PreviewDialog("warranty-details-"+font,()=>WarrantyDetails(claim));PreviewDialog("warranty-replacement-"+font,()=>WarrantyReplacementForm(claim));PreviewDialog("warranty-supplier-"+font,()=>WarrantyAction(claim,"Send"));
            var search=Descendants<TextBox>(body).First();search.Text="NO-SUCH-SERIAL";UpdateLayout();var grid=Descendants<DataGrid>(workspaceViewport).First();if(grid.Items.Count!=0)throw new InvalidOperationException("Warranty empty filter failed");Capture(this,Path.Combine(path,language+"-warranties-empty-"+font+".png"));search.Text="LAPTOP-001";if(grid.Items.Count!=1)throw new InvalidOperationException("Warranty serial search failed");RenderPrintPages(WarrantyReceipt(S.WarrantyClaims().Rows[0]),path,language+"-warranty-receipt-"+font);File.AppendAllText(Path.Combine(path,"ui-checks.txt"),"PASS "+language+"/"+font+" warranty list, forms, details, print and empty/search states"+Environment.NewLine);
        }
        if(S.LocationQuantity(item,1)!=2000)throw new InvalidOperationException("Warranty previews changed stock");
    }
}
