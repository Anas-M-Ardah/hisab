using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Hisab;
public sealed partial class MainWindow
{
    ComboBox LocationPicker(Panel panel,string? label=null)
    {
        var box=Choose(S.Locations());box.SelectedIndex=0;
        if(S.Locations().Count>1)panel.Children.Add(Field(label??T("الموقع","Location"),box));return box;
    }
    long PickLocation(ComboBox box)=>(box.SelectedItem as StockLocation)?.Id??throw new InvalidOperationException(T("اختر الموقع","Choose a location"));
    TextBox SerialInput(Panel panel,string? label=null)
    {
        var box=Input();box.AcceptsReturn=true;box.MinHeight=90;box.MaxHeight=180;box.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;box.FlowDirection=FlowDirection.LeftToRight;
        panel.Children.Add(Field(label??T("الأرقام التسلسلية — رقم في كل سطر","Serial numbers — one per line"),box));return box;
    }
    void BarcodeInput(Panel panel,ComboBox items)
    {
        var scan=Input();scan.FlowDirection=FlowDirection.LeftToRight;panel.Children.Add(Field(T("مسح باركود الصنف ثم Enter","Scan item barcode, then Enter"),scan));
        scan.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){e.Handled=true;Guard(()=>{long id=Convert.ToInt64(S.Scalar("SELECT id FROM items WHERE barcode=@p0",Store.NormalizeStockCode(scan.Text))??0);items.SelectedItem=items.Items.Cast<Item>().FirstOrDefault(i=>i.Id==id)??throw new InvalidOperationException(T("الباركود غير معروف","Unknown barcode"));scan.Clear();});}};
    }
    void InventoryPage()
    {
        Heading(T("إدارة المخزون","Inventory management"),T("المواقع والأجهزة والجرد وإعادة الطلب.","Locations, devices, stock counts and replenishment."));
        body.Children.Add(Actions(Btn(T("جرد جديد","New stock count"),StartCountForm,true),Btn(T("تحويل مخزون","Transfer stock"),TransferInventoryForm),Btn(T("المواقع","Locations"),LocationsForm),Btn(T("إعادة الطلب","Replenishment"),ReorderForm),Btn(T("المشتريات","Purchasing"),()=>Navigate("purchasing"))));
        var search=Input();body.Children.Add(Field(T("بحث بالصنف أو الكود أو الباركود","Search item, code or barcode"),search));
        var location=Choose(new[]{new StockLocation(0,T("كل المواقع","All locations"),true)}.Concat(S.Locations()));location.SelectedIndex=0;if(S.Locations().Count>1)body.Children.Add(Field(T("الموقع","Location"),location));
        DataTable Rows(){var t=new DataTable();foreach(string col in new[]{"id","code","name","location","qty","barcode","tracking"})t.Columns.Add(col,col=="id"?typeof(long):typeof(string));foreach(var i in UiItems().Where(i=>i.Stock)){var barcode=S.Scalar("SELECT barcode FROM items WHERE id=@p0",i.Id)?.ToString()??"";if(!(i.Name+" "+i.Code+" "+barcode).Contains(search.Text,StringComparison.OrdinalIgnoreCase))continue;foreach(var l in S.Locations(false).Where(l=>PickLocation(location)==0||l.Id==PickLocation(location)))t.Rows.Add(i.Id,i.Code,i.Name,l.Name,Store.D(S.LocationQuantity(i.Id,l.Id)).ToString("0.###"),barcode,S.IsSerialized(i.Id)?T("تسلسلي","Serialized"):T("كمية","Quantity"));}return t;}
        var grid=Grid(Rows(),("code",T("الكود","Code"),1),("name",T("الصنف","Item"),2),("location",T("الموقع","Location"),1.5),("qty",T("المتوفر","On hand"),1),("barcode",T("الباركود","Barcode"),1.2),("tracking",T("التتبع","Tracking"),1));body.Children.Add(grid);
        search.TextChanged+=(s,e)=>grid.ItemsSource=Rows().DefaultView;location.SelectionChanged+=(s,e)=>grid.ItemsSource=Rows().DefaultView;
        body.Children.Add(Actions(Btn(T("إعداد التتبع","Configure tracking"),()=>InventoryItemForm(Selected(grid))),Btn(T("سجل الأجهزة والضمان","Devices & warranties"),()=>SerialHistoryForm(Selected(grid)))));
        body.Children.Add(Text(T("جلسات الجرد","Stock counts"),22,true));var counts=Grid(S.Table("SELECT c.id,c.date,l.name location,c.state,c.reason FROM stock_counts c JOIN locations l ON l.id=c.location ORDER BY c.id DESC"),("id",T("الرقم","ID"),.6),("date",T("التاريخ","Date"),1),("location",T("الموقع","Location"),1.5),("state",T("الحالة","State"),1),("reason",T("السبب","Reason"),2));body.Children.Add(counts);body.Children.Add(Btn(T("فتح الجرد المحدد","Open selected count"),()=>CountEditor(Selected(counts))));
    }
    void LocationsForm()
    {
        var (w,p,finish)=Dialog(T("المواقع","Locations"));var locations=Choose(S.Locations(false));p.Children.Add(Field(T("تعديل موقع موجود (اختياري)","Existing location to edit (optional)"),locations));var name=Input();p.Children.Add(Field(T("اسم الموقع","Location name"),name));var active=new CheckBox{Content=T("فعال","Active"),IsChecked=true};p.Children.Add(active);locations.SelectionChanged+=(s,e)=>{if(locations.SelectedItem is StockLocation l){name.Text=l.Name;active.IsChecked=l.Active;}};
        p.Children.Add(Actions(Btn(T("موقع جديد","New location"),()=>{locations.SelectedIndex=-1;name.Clear();active.IsChecked=true;}),Btn(T("حفظ","Save"),()=>{S.SaveLocation((locations.SelectedItem as StockLocation)?.Id,name.Text,active.IsChecked==true);finish();Navigate("inventory");},true)));w.ShowDialog();
    }
    void InventoryItemForm(long id)
    {
        var (w,p,finish)=Dialog(T("تتبع الصنف","Item tracking"));p.Children.Add(Text(S.LocalName("items",id,vm.Arabic),22,true));var barcode=Input(S.Scalar("SELECT barcode FROM items WHERE id=@p0",id)?.ToString()??"");p.Children.Add(Field(T("باركود الصنف","Product barcode"),barcode));bool enabled=S.IsSerialized(id);var serial=new CheckBox{Content=T("تتبع كل وحدة برقم تسلسلي","Track each unit by serial number"),IsChecked=enabled,IsEnabled=!enabled};p.Children.Add(serial);p.Children.Add(Text(T("عند التفعيل، أدخل أرقام الوحدات الموجودة في المخزون الرئيسي. لا يغير التسجيل الكمية أو القيمة.","When enabling, register current units in Main stock. Registration does not change quantity or value."),17));var initial=SerialInput(p);initial.IsEnabled=!enabled;
        p.Children.Add(Btn(T("مراجعة وحفظ","Review & save"),()=>{if(!Confirm(T("حفظ إعدادات التتبع؟ لا يمكن تعطيل التتبع التسلسلي لاحقًا.","Save tracking settings? Serial tracking cannot subsequently be disabled.")))return;A.ConfigureInventoryItem(id,barcode.Text,serial.IsChecked==true,AccountingService.SerialList(initial.Text));finish();Navigate("inventory");},true));w.ShowDialog();
    }
    void SerialHistoryForm(long item)
    {
        var (w,p,_)=Dialog(T("الأجهزة والضمان","Devices & warranties"),1000,false);var units=S.Table("SELECT id,number,supplier_warranty,customer_warranty FROM serial_units WHERE item=@p0 ORDER BY number",item);var grid=Grid(units,("number",T("التسلسلي","Serial"),2),("supplier_warranty",T("نهاية ضمان المورد","Supplier warranty end"),2),("customer_warranty",T("نهاية ضمان العميل","Customer warranty end"),2));p.Children.Add(grid);
        var sw=new DatePicker();var cw=new DatePicker();p.Children.Add(Field(T("نهاية ضمان المورد","Supplier warranty end"),sw));p.Children.Add(Field(T("نهاية ضمان العميل","Customer warranty end"),cw));grid.SelectionChanged+=(s,e)=>{if(grid.SelectedItem is DataRowView r){sw.SelectedDate=DateTime.TryParse(r["supplier_warranty"].ToString(),out var sd)?sd:null;cw.SelectedDate=DateTime.TryParse(r["customer_warranty"].ToString(),out var cd)?cd:null;}};
        p.Children.Add(Btn(T("حفظ الضمان","Save warranty"),()=>{S.SetSerialWarranty(Selected(grid),sw.SelectedDate,cw.SelectedDate);w.Close();SerialHistoryForm(item);},true));p.Children.Add(Text(T("سجل الحركات — تشمل المستندات الملغاة","Movement history — includes cancelled documents"),20,true));p.Children.Add(Grid(S.SerialHistory(item),("number",T("التسلسلي","Serial"),1.4),("document",T("المستند","Document"),1.4),("date",T("التاريخ","Date"),1),("location",T("الموقع","Location"),.7),("direction",T("وارد 1 / صادر -1","In 1 / out -1"),1),("reversed",T("ملغى 1","Cancelled 1"),.8)));w.ShowDialog();
    }
    void TransferInventoryForm()
    {
        var (w,p,finish)=Dialog(T("تحويل مخزون","Transfer stock"));if(S.Locations().Count<2){p.Children.Add(Text(T("أضف موقعًا آخر أولًا من إدارة المواقع.","Add another location first using Locations."),20));w.ShowDialog();return;}var from=LocationPicker(p,T("من الموقع","From location"));var to=LocationPicker(p,T("إلى الموقع","To location"));to.SelectedIndex=1;var item=Choose(UiItems().Where(i=>i.Stock));BarcodeInput(p,item);p.Children.Add(Field(T("الصنف","Item"),item));var qty=Input("1",true);p.Children.Add(Field(T("الكمية","Quantity"),qty));var serials=SerialInput(p);var date=Date();p.Children.Add(Field(T("التاريخ","Date"),date));var reason=Input();p.Children.Add(Field(T("السبب","Reason"),reason));p.Children.Add(Btn(T("مراجعة التحويل","Review transfer"),()=>{if(Confirm(T("تحويل المخزون بين الموقعين؟","Transfer stock between these locations?"))){A.TransferInventory(GetDate(date),PickLocation(from),PickLocation(to),GetItem(item).Id,Store.Parse(qty.Text),serials.Text,reason.Text);finish();Navigate("inventory");}},true));w.ShowDialog();
    }
    void StartCountForm()
    {
        var (w,p,finish)=Dialog(T("بدء جرد","Start stock count"));var location=LocationPicker(p);var items=new ListBox{ItemsSource=UiItems().Where(i=>i.Stock).ToList(),SelectionMode=SelectionMode.Multiple,MaxHeight=260};p.Children.Add(Field(T("حدد الأصناف — يمكن اختيار عدة أصناف","Select items — multiple selection allowed"),items));p.Children.Add(Btn(T("تحديد الكل","Select all"),()=>items.SelectAll()));p.Children.Add(Text(T("تتوقف حركات الأصناف المحددة في هذا الموقع حتى إكمال الجرد أو إلغائه.","Movements for selected items at this location are blocked until the count is completed or cancelled."),18));p.Children.Add(Btn(T("بدء","Start"),()=>{long id=A.StartStockCount(PickLocation(location),items.SelectedItems.Cast<Item>().Select(i=>i.Id).ToArray());finish();CountEditor(id);},true));w.ShowDialog();
    }
    void CountEditor(long id)
    {
        var c=S.Table("SELECT * FROM stock_counts WHERE id=@p0",id).Rows[0];var (w,p,finish)=Dialog(T("جلسة الجرد ","Stock count ")+id,950,false);bool open=c["state"].ToString()=="Open";var rows=S.Table("SELECT l.*,i.name FROM stock_count_lines l JOIN items i ON i.id=l.item WHERE count_id=@p0",id);var edits=new List<(long Item,TextBox Qty,TextBox Serials,TextBox Cost)>();
        foreach(DataRow r in rows.Rows){long item=(long)r["item"];p.Children.Add(Text(S.LocalName("items",item,vm.Arabic)+T(" — المتوقع: "," — expected: ")+Store.D((long)r["expected"]),20,true));var qty=Input(r["actual"]==DBNull.Value?"":Store.D((long)r["actual"]).ToString(CultureInfo.InvariantCulture),true);p.Children.Add(Field(T("الكمية الفعلية","Actual quantity"),qty));var serials=S.IsSerialized(item)?SerialInput(p):Input();serials.Text=(string)r["serials"];var cost=Input(Store.D((long)r["gain_cost"]).ToString(CultureInfo.InvariantCulture),true);p.Children.Add(Field(T("تكلفة وحدة الزيادة JOD","Gain unit cost JOD"),cost));qty.IsEnabled=serials.IsEnabled=cost.IsEnabled=open;edits.Add((item,qty,serials,cost));}
        void Save(){using var tx=S.BeginTransaction();foreach(var e in edits)if(!string.IsNullOrWhiteSpace(e.Qty.Text))A.RecordStockCount(id,e.Item,Store.Parse(e.Qty.Text),e.Serials.Text,Store.Parse(e.Cost.Text));tx.Commit();}
        if(open){var reason=Input();p.Children.Add(Field(T("سبب الفروقات","Discrepancy reason"),reason));p.Children.Add(Actions(Btn(T("حفظ التقدم","Save progress"),()=>{Save();finish();Navigate("inventory");}),Btn(T("مراجعة وترحيل الفروقات","Review & post differences"),()=>{Save();var summary=string.Join("\n",edits.Select(e=>S.LocalName("items",e.Item,vm.Arabic)+": "+e.Qty.Text));if(Confirm(summary+"\n"+reason.Text)){A.CompleteStockCount(id,reason.Text);finish();Navigate("inventory");}},true),Btn(T("إلغاء الجرد","Cancel count"),()=>{if(Confirm(T("إلغاء الجرد وفك القفل؟","Cancel this count and release its locks?"))){A.CancelStockCount(id);finish();Navigate("inventory");}})));}else if(c["posted_doc"]!=DBNull.Value)p.Children.Add(Btn(T("فتح مستند التسوية","Open adjustment document"),()=>ShowDocument((long)c["posted_doc"])));w.ShowDialog();
    }
    void ReorderForm()
    {
        var (w,p,finish)=Dialog(T("إعادة الطلب","Replenishment"),1000);var item=Choose(UiItems().Where(i=>i.Stock));p.Children.Add(Field(T("الصنف","Item"),item));var location=LocationPicker(p);var minimum=Input("0",true);var target=Input("0",true);p.Children.Add(Field(T("الحد الأدنى","Minimum quantity"),minimum));p.Children.Add(Field(T("الكمية المستهدفة","Target quantity"),target));p.Children.Add(Btn(T("حفظ القاعدة","Save rule"),()=>{A.SetReorderRule(GetItem(item).Id,PickLocation(location),Store.Parse(minimum.Text),Store.Parse(target.Text));finish();ReorderForm();},true));
        var t=new DataTable();foreach(string col in new[]{"id","location","name","qty","onorder","suggested","expected"})t.Columns.Add(col,col is "id" or "location"?typeof(long):typeof(string));foreach(var r in A.ReorderSuggestions())t.Rows.Add(r.Item,r.Location,S.LocalName("items",r.Item,vm.Arabic),Store.D(r.OnHand),Store.D(r.OnOrder),Store.D(r.Suggested),r.Expected+(r.Overdue?T(" — متأخر"," — overdue"):""));var grid=Grid(t,("name",T("الصنف","Item"),2),("location",T("الموقع","Location"),.7),("qty",T("المتوفر","On hand"),1),("onorder",T("قيد الطلب","On order"),1),("suggested",T("المقترح","Suggested"),1),("expected",T("الوصول المتوقع","Expected arrival"),1.5));p.Children.Add(grid);p.Children.Add(Text(T("المقترح يأخذ الطلبات المفتوحة في الحسبان؛ المتأخر ليس متوفرًا اليوم.","Suggestions include open orders; overdue incoming stock is not available today."),17));p.Children.Add(Btn(T("إنشاء مسودة طلب للمحدد","Create draft order for selected item"),()=>{if(grid.SelectedItem is not DataRowView r)throw new InvalidOperationException(T("اختر سطرًا","Select a row"));long selected=(long)r["id"],loc=(long)r["location"];var suggestion=A.ReorderSuggestions().Single(x=>x.Item==selected&&x.Location==loc);finish();PurchaseOrderForm(selected,loc,Store.D(suggestion.Suggested));}));w.ShowDialog();
    }
}
