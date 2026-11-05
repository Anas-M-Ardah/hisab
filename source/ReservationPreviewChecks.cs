using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace Hisab;
public sealed partial class MainWindow
{
    public void RenderReservationPreviews(string path)
    {
        Directory.CreateDirectory(path);previewDirectory=path;var customer=S.Accounts().First(a=>a.Kind=="Customer");
        S.SaveItem(null,"RES-PREVIEW","Hardware for reservations",true,0);long item=S.Items().Single(i=>i.Code=="RES-PREVIEW").Id;A.OpeningStock(item,DateTime.Today,10,50);long hold=A.CreateReservation(customer.Id,DateTime.Today.AddDays(7),[new(item,1,3)],"Awaiting collection / بانتظار الاستلام");long quote=A.SaveQuote(null,0,customer.Id,DateTime.Today,DateTime.Today.AddDays(14),0,false,"",[new(item,"Hardware for reservations",2,100,0)]);A.SetQuoteState(quote,1,"Accepted");long quoteHold=A.CreateReservation(customer.Id,DateTime.Today.AddDays(5),[new(item,1,2)],"",quote);
        foreach(string language in new[]{"ar","en"})foreach(int font in new[]{14,24}){
            S.Set("language",language);S.Set("font",font.ToString());vm.Route="reservations";BuildShell();UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);Capture(this,Path.Combine(path,language+"-reservations-"+font+".png"));
            PreviewDialog("reserve-new-"+font,()=>ReservationForm());PreviewDialog("reservation-details-"+font,()=>ReservationDetails(hold));PreviewDialog("reservation-sale-"+font,()=>OpenReservedSale(hold));PreviewDialog("quote-reservation-sale-"+font,()=>OpenReservedSale(quoteHold));PreviewDialog("reservation-release-"+font,()=>ReleaseReservationForm(hold));
            Navigate("inventory");var search=Descendants<TextBox>(body).First();search.Text="RES-PREVIEW";UpdateLayout();Capture(this,Path.Combine(path,language+"-availability-"+font+".png"));
            if(S.LocationQuantity(item,1)!=10000||S.ReservedQuantity(item,1)!=5000||S.AvailableQuantity(item,1)!=5000)throw new InvalidOperationException("Reservation previews changed stock");
            File.AppendAllText(Path.Combine(path,"ui-checks.txt"),"PASS "+language+"/"+font+" reservation list, form, details, sales, release and availability"+Environment.NewLine);
        }
    }
}
