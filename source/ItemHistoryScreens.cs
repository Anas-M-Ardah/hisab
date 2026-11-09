using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
namespace Hisab;
public sealed partial class MainWindow
{
    long historyItem;
    string historyBackRoute="items";
    void OpenItemHistory(long item){historyItem=item;historyBackRoute=vm.Route;Navigate("item-history");}
    string HistoryKind(string kind)=>kind=="Reservation"?T("حجز مخزون","Reservation"):Kind(kind);
    string HistoryState(string state)=>state switch {"Posted"=>T("محفوظ","Posted"),"Cancelled"=>T("ملغى","Cancelled"),"Reversal"=>T("قيد إلغاء","Reversal"),_=>ReservationState(state)};
    void ItemHistoryPage()
    {
        var item=S.Items().Single(i=>i.Id==historyItem);var records=S.ItemHistory(item.Id);var locations=S.Locations(false).ToDictionary(l=>l.Id,l=>l.Name);
        Heading(T("سجل الصنف","Item history"),item.Code+" · "+S.LocalName("items",item.Id,vm.Arabic));
        var search=Input();body.Children.Add(SearchActions(T("بحث بالمستند أو الرقم التسلسلي أو الملاحظات","Search reference, serial number or notes"),search,Btn(T("رجوع","Back"),()=>Navigate(historyBackRoute)),Btn(T("تحديث","Refresh"),()=>Navigate("item-history"))));
        var from=new DatePicker();var to=new DatePicker();var location=Choose(new[]{new StockLocation(0,T("كل المواقع","All locations"),true)}.Concat(S.Locations(false)));location.SelectedIndex=0;
        var kind=new ComboBox{ItemsSource=new[]{new Choice("",T("كل الأنشطة","All activities"))}.Concat(records.Select(r=>r.Kind).Distinct().Select(k=>new Choice(k,HistoryKind(k)))).ToList(),DisplayMemberPath="Label",SelectedValuePath="Key",SelectedIndex=0};
        var filters=new WrapPanel();foreach(var pair in new (string,FrameworkElement)[]{(T("من تاريخ (اختياري)","From (optional)"),from),(T("إلى تاريخ (اختياري)","To (optional)"),to),(T("الموقع","Location"),location),(T("النشاط","Activity"),kind)}){var field=Field(pair.Item1,pair.Item2);field.Width=230;field.Margin=new Thickness(0,0,12,8);filters.Children.Add(field);}body.Children.Add(filters);
        var summary=Text("",17);body.Children.Add(summary);var error=Text("",17);error.Visibility=Visibility.Collapsed;body.Children.Add(error);
        var table=new DataTable();table.Columns.Add("id",typeof(long));foreach(var c in new[]{"date","number","kind","location","incoming","outgoing","state"})table.Columns.Add(c);
        var grid=Grid(table,("date",T("التاريخ","Date"),1),("number",T("المرجع","Reference"),1.3),("kind",T("النشاط","Activity"),1.6),("location",T("الموقع","Location"),1.4),("incoming",T("الوارد","In"),.7),("outgoing",T("الصادر","Out"),.7),("state",T("الحالة","Status"),1));
        var visible=new List<ItemHistoryEntry>();
        void Apply(){try{visible=Store.FilterItemHistory(records,from.SelectedDate,to.SelectedDate,PickLocation(location),(kind.SelectedItem as Choice)?.Key??"",search.Text);table.Rows.Clear();for(int index=0;index<visible.Count;index++){var r=visible[index];var row=table.Rows.Add(index,r.Date.ToString("yyyy-MM-dd"),r.Number,HistoryKind(r.Kind),locations.GetValueOrDefault(r.Location,""),r.Incoming==0?"—":Store.D(r.Incoming).ToString("0.###",CultureInfo.InvariantCulture),r.Outgoing==0?"—":Store.D(r.Outgoing).ToString("0.###",CultureInfo.InvariantCulture),HistoryState(r.State));row["incoming_numeric"]=Store.D(r.Incoming);row["outgoing_numeric"]=Store.D(r.Outgoing);}long loc=PickLocation(location);var locs=S.Locations(false).Where(l=>loc==0||l.Id==loc).ToList();long onhand=locs.Sum(l=>S.LocationQuantity(item.Id,l.Id)),reserved=locs.Sum(l=>S.ReservedQuantity(item.Id,l.Id));summary.Text=item.Stock?T("المخزون الحالي: ","Current stock: ")+Store.D(onhand)+T(" · المحجوز: "," · Reserved: ")+Store.D(reserved)+T(" · المتاح: "," · Available: ")+Store.D(onhand-reserved):T("صنف خدمي — لا توجد كمية مخزون","Service item — no physical stock");error.Visibility=Visibility.Collapsed;}catch(InvalidOperationException ex){table.Rows.Clear();visible.Clear();error.Text=ex.Message;error.Visibility=Visibility.Visible;}}
        ItemHistoryEntry SelectedEntry()=>visible[checked((int)Selected(grid))];
        var details=Btn(T("تفاصيل الحركة","Activity details"),()=>ItemHistoryDetails(SelectedEntry()));var source=Btn(T("فتح المصدر","Open source"),()=>OpenHistorySource(SelectedEntry()),true);
        body.Children.Add(Actions(Btn(T("تطبيق الفلاتر","Apply filters"),Apply),Btn(T("مسح الفلاتر","Clear filters"),()=>{from.SelectedDate=null;to.SelectedDate=null;location.SelectedIndex=0;kind.SelectedIndex=0;search.Clear();Apply();}),details,source));BindSelection(grid,details,source);body.Children.Add(grid);
        body.Children.Add(Text(T("الوارد والصادر حركات فعلية. الإلغاءات معروضة للتتبع؛ الملغى وقيد إلغائه لا يدخلان في الرصيد الحالي. الحجوزات بتاريخ إنشائها مع حالتها الحالية.","In/Out show physical movements. Cancelled records and their reversals are retained for reference and excluded from current stock. Reservations show creation date and current status."),16));
        search.TextChanged+=(_,_)=>Apply();grid.MouseDoubleClick+=(_,_)=>{if(grid.SelectedItem!=null)Guard(()=>ItemHistoryDetails(SelectedEntry()));};Apply();
    }
    void OpenHistorySource(ItemHistoryEntry r){if(r.Source=="reservation")ReservationDetails(r.Id);else ShowDocument(r.Id);}
    void ItemHistoryDetails(ItemHistoryEntry r)
    {
        var (w,p,finish)=Dialog(T("تفاصيل الحركة","Activity details")+" · "+r.Number,850,false);
        p.Children.Add(Text(HistoryKind(r.Kind)+" · "+HistoryState(r.State)+" · "+r.Date.ToString("yyyy-MM-dd"),20,true));
        p.Children.Add(Text(S.Locations(false).Single(l=>l.Id==r.Location).Name,18));if(r.Party is long party)p.Children.Add(Text(S.LocalName("accounts",party,vm.Arabic),18));
        p.Children.Add(Text(T("كمية المستند: ","Document quantity: ")+Store.D(r.DocumentQuantity)+T(" · وارد / صادر: "," · In / out: ")+Store.D(r.Incoming)+" / "+Store.D(r.Outgoing),18));
        p.Children.Add(Text(r.Note,17));p.Children.Add(Text(T("الأرقام التسلسلية","Serial numbers"),18,true));var serials=Input(r.Serials.Length==0?T("لا توجد أرقام تسلسلية لهذه الحركة.","No serial numbers for this activity."):r.Serials);serials.IsReadOnly=true;serials.AcceptsReturn=true;serials.TextWrapping=TextWrapping.Wrap;serials.MaxHeight=220;serials.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;p.Children.Add(serials);
        p.Children.Add(Btn(T("فتح المصدر","Open source"),()=>{finish();OpenHistorySource(r);},true));w.ShowDialog();
    }
}
