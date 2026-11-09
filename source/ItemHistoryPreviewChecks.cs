using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace Hisab;
public sealed partial class MainWindow
{
    public void RenderItemHistoryPreviews(string path)
    {
        Directory.CreateDirectory(path);previewDirectory=path;
        S.SaveItem(null,"HIST-DEMO","Business laptop",true,0);long item=S.Items().Single(i=>i.Code=="HIST-DEMO").Id;var customer=S.Accounts().First(a=>a.Kind=="Customer");var supplier=S.Accounts().First(a=>a.Kind=="Supplier");long location=S.SaveLocation(null,"Warehouse / المستودع");
        A.ConfigureInventoryItem(item,"HIST-DEMO",true,[]);A.Invoice(false,DateTime.Today,supplier.Id,null,0,0,false,"Devices received / استلام أجهزة",[new(item,"Business laptop",4,250,0,1,"DEMO-01,DEMO-02,DEMO-03,DEMO-04")]);A.TransferInventory(DateTime.Today,1,location,item,1,"DEMO-01","Moved to warehouse");A.Invoice(true,DateTime.Today,customer.Id,null,0,0,false,"Customer collection",[new(item,"Business laptop",1,350,0,1,"DEMO-02")]);long hold=A.CreateReservation(customer.Id,DateTime.Today.AddDays(5),[new(item,1,1,"DEMO-03")],"Awaiting collection");
        historyItem=item;historyBackRoute="inventory";
        foreach(string language in new[]{"ar","en"})foreach(int font in new[]{14,24}){
            S.Set("language",language);S.Set("font",font.ToString());vm.Route="item-history";BuildShell();UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);Capture(this,Path.Combine(path,language+"-item-history-"+font+".png"));
            var grid=Descendants<DataGrid>(workspaceViewport).First();if(grid.Items.Count!=5)throw new InvalidOperationException("Unexpected history rows");grid.SelectedIndex=0;PreviewDialog("history-details-"+font,()=>ItemHistoryDetails(S.ItemHistory(item).First()));
            var search=Descendants<TextBox>(body).First();search.Text="NO-SUCH-SERIAL";UpdateLayout();if(grid.Items.Count!=0)throw new InvalidOperationException("Empty history filter failed");Capture(this,Path.Combine(path,language+"-history-empty-"+font+".png"));search.Text="DEMO-01";if(grid.Items.Count!=3)throw new InvalidOperationException("Serial filter failed");
            if(S.Items().Single(i=>i.Id==item).Qty!=3000||S.ReservedQuantity(item,1)!=1000)throw new InvalidOperationException("Preview changed inventory");File.AppendAllText(Path.Combine(path,"ui-checks.txt"),"PASS "+language+"/"+font+" history, details, empty state and serial search"+Environment.NewLine);
        }
    }
}
