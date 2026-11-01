using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Hisab;
public sealed partial class MainWindow
{
    public void RenderInventoryPreviews(string path)
    {
        Directory.CreateDirectory(path);previewDirectory=path;
        var checks=new List<string>();var supplier=S.Accounts().First(a=>a.Kind=="Supplier");long item=S.Items()[1].Id;
        var probe=new StackPanel();var single=LocationPicker(probe);if(probe.Children.Count!=0||PickLocation(single)!=1)throw new Exception("Single-location picker must be implicit");checks.Add("PASS single location requires no location field");
        long warehouse=S.SaveLocation(null,"مستودع / Warehouse");probe=new StackPanel();LocationPicker(probe);if(probe.Children.Count!=1)throw new Exception("Multiple locations need a selector");checks.Add("PASS multiple locations expose a selector");
        A.ConfigureInventoryItem(item,"TECH-001",true,Array.Empty<string>());long order=A.CreatePurchaseOrder(supplier.Id,warehouse,DateTime.Today.AddDays(4),new[]{new PurchaseOrderLine(item,5,100)});A.IssuePurchaseOrder(order);long ol=Convert.ToInt64(S.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",order));long receipt=A.ReceivePurchaseOrder(order,DateTime.Today,new[]{new ReceiptInput(ol,2,"DEVICE-1001,DEVICE-1002")});A.SetReorderRule(item,warehouse,3,8);long count=A.StartStockCount(warehouse,new[]{item});
        foreach(var language in new[]{"ar","en"})foreach(var font in new[]{14,24}){
            S.Set("language",language);S.Set("font",font.ToString());Width=font==24?1024:1360;Height=768;BuildShell();
            foreach(string route in new[]{"inventory","purchasing"}){Navigate(route);Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();if(!body.Children.OfType<DataGrid>().Any())throw new Exception("Inventory route did not show a data table");Capture(this,Path.Combine(path,$"{language}-{font}-{route}.png"));checks.Add($"PASS {language}/{font} {route} renders data");}
            PreviewDialog("inventory-tracking-"+font,()=>InventoryItemForm(item));PreviewDialog("inventory-count-"+font,()=>CountEditor(count));PreviewDialog("inventory-order-"+font,()=>PurchaseOrderDetails(order));PreviewDialog("inventory-billing-"+font,BillReceiptsForm);PreviewDialog("inventory-landed-"+font,LandedCostForm);PreviewDialog("inventory-reorder-"+font,ReorderForm);PreviewDialog("inventory-transfer-"+font,TransferInventoryForm);PreviewDialog("inventory-serials-"+font,()=>SerialHistoryForm(item));
        }
        A.CancelStockCount(count);PreviewDialog("inventory-receipt-print",()=>ShowDocument(receipt));File.WriteAllLines(Path.Combine(path,"inventory-ui-checks.txt"),checks);previewDirectory=null;
    }
}
