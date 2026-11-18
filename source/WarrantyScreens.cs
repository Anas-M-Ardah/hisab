using System.Data;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Documents;
namespace Hisab;
public sealed partial class MainWindow
{
    string WarrantyState(string s)=>s switch{"Open"=>T("مفتوح","Open"),"Closed"=>T("مغلق","Closed"),"Replaced"=>T("تم الاستبدال","Replaced"),"InStore"=>T("في المتجر","In store"),"AtSupplier"=>T("لدى المورد","At supplier"),"WithCustomer"=>T("أعيد للعميل","Returned to customer"),"Disposed"=>T("تم الإتلاف","Disposed"),_=>s};
    string WarrantyCoverage(DataRow c)=>c["expires"]==DBNull.Value?T("غير مسجل","Not recorded"):DateTime.Parse((string)c["expires"])<DateTime.Parse((string)c["received"])?T("منتهٍ عند الاستلام","Expired at intake"):T("ساري عند الاستلام","Covered at intake");
    void WarrantyColumn(DataGrid grid,int index,double width)
    {
        var column=(DataGridTextColumn)grid.Columns[index];column.MinWidth=Math.Max(column.MinWidth,FontSize*width);var style=new Style(typeof(TextBlock),column.ElementStyle);style.Setters.Add(new Setter(TextBlock.FlowDirectionProperty,FlowDirection.LeftToRight));style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty,vm.Arabic?TextAlignment.Right:TextAlignment.Left));column.ElementStyle=style;
    }
    void WarrantiesPage()
    {
        Heading(T("الضمان والاستبدال","Warranties & exchanges"),T("أجهزة بتسلسل · استبدال مجاني · الأجهزة التالفة منفصلة عن المخزون المتاح","Serialized devices · Free replacements · Faulty devices kept outside available stock"));
        var search=Input();var active=new CheckBox{Content=T("المطالبات المفتوحة والأجهزة بعهدتنا فقط","Open claims and devices in our custody only"),IsChecked=true};
        body.Children.Add(SearchActions(T("بحث بالتسلسلي أو العميل أو الفاتورة","Search serial, customer or invoice"),search,Btn(T("+ تسجيل / استلام ضمان","+ Register / receive warranty"),WarrantyDevicePicker,true)));body.Children.Add(active);
        DataTable Rows(){var t=S.WarrantyClaims();t.Columns.Add("status");t.Columns.Add("holding");foreach(var r in t.Rows.Cast<DataRow>().ToList()){if((active.IsChecked==true&&(string)r["state"]=="Closed"||active.IsChecked==true&&(string)r["custody"]=="Disposed")||!string.Join(" ",new[]{r["id"],r["serial"],r["replacement"],r["customer"],r["invoice"],r["reference"]}).Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase)){t.Rows.Remove(r);continue;}r["status"]=WarrantyState((string)r["state"]);r["holding"]=WarrantyState((string)r["custody"]);}return t;}
        var grid=Grid(Rows(),("id",T("المطالبة","Claim"),.7),("received",T("الاستلام","Received"),1.1),("serial",T("التسلسلي","Serial"),1.5),("customer",T("العميل","Customer"),2),("status",T("الحالة","Status"),1),("holding",T("العهدة","Custody"),1.3));WarrantyColumn(grid,1,6.8);WarrantyColumn(grid,2,7.5);body.Children.Add(grid);
        void Refresh()=>grid.ItemsSource=Rows().DefaultView;search.TextChanged+=(_,_)=>Refresh();active.Checked+=(_,_)=>Refresh();active.Unchecked+=(_,_)=>Refresh();var details=Btn(T("فتح المطالبة","Open claim"),()=>WarrantyDetails(Selected(grid)),true);body.Children.Add(Actions(details));BindSelection(grid,details);grid.MouseDoubleClick+=(_,_)=>Guard(()=>WarrantyDetails(Selected(grid)));
    }
    void WarrantyDevicePicker()
    {
        var(w,p,finish)=Dialog(T("اختيار جهاز مباع","Choose a sold device"),1100);p.Children.Add(Text(T("اختر التسلسلي والفاتورة الصحيحة. الأجهزة المرتجعة أو المستبدلة لا تقبل مطالبة جديدة.","Choose the correct serial and invoice. Returned or replaced devices cannot start another claim."),17));var search=Input();p.Children.Add(Field(T("بحث بالتسلسلي أو العميل أو الفاتورة","Search serial, customer or invoice"),search));
        var all=S.WarrantyDevices();DataTable Rows(){var t=all.Clone();foreach(DataRow r in all.Rows)if((r["serial"]+" "+r["customer"]+" "+r["invoice"]).Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase))t.ImportRow(r);return t;}
        var grid=Grid(Rows(),("serial",T("التسلسلي","Serial"),1.5),("item",T("الصنف","Item"),2),("customer",T("العميل","Customer"),2),("invoice",T("الفاتورة","Invoice"),1),("expires",T("نهاية الضمان","Warranty expiry"),1.2));WarrantyColumn(grid,0,7.5);WarrantyColumn(grid,3,7);WarrantyColumn(grid,4,6.8);grid.Height=300;p.Children.Add(grid);search.TextChanged+=(_,_)=>grid.ItemsSource=Rows().DefaultView;
        var open=Btn(T("متابعة","Continue"),()=>{var r=((DataRowView)grid.SelectedItem).Row;finish();WarrantyRegistration(r);},true);p.Children.Add(open);BindSelection(grid,open);w.ShowDialog();
    }
    void WarrantyRegistration(DataRow r)
    {
        var(w,p,finish)=Dialog(T("ضمان الجهاز","Device warranty"),850);p.Children.Add(Text(r["serial"]+" · "+r["item"],22,true));p.Children.Add(Text(r["customer"]+" · "+r["invoice"]+" · "+r["sold"],17));
        var expiry=Date();expiry.SelectedDate=r["expires"]==DBNull.Value?null:DateTime.Parse((string)r["expires"]);p.Children.Add(Field(T("آخر يوم للضمان (فارغ = غير مسجل)","Last covered day (blank = not recorded)"),expiry));var terms=Input((string)r["terms"]);p.Children.Add(Field(T("شروط الضمان","Warranty terms"),terms));long? id=r["warranty"]==DBNull.Value?null:(long)r["warranty"];
        bool locked=id!=null&&Convert.ToInt64(S.Scalar("SELECT count(*) FROM warranty_claims WHERE warranty=@p0",id))>0||Convert.ToInt64(S.Scalar("SELECT count(*) FROM warranty_replacements WHERE doc=@p0",r["source_doc"]))>0;expiry.IsEnabled=terms.IsEnabled=!locked;
        long Save()=>locked?id!.Value:A.SaveWarranty((long)r["source_doc"],(long)r["id"],expiry.SelectedDate,terms.Text);
        p.Children.Add(Text(T("الاستبدال يرث نهاية الضمان الأصلية. تثبت الشروط عند أول مطالبة.","Replacements inherit the original expiry. Terms are locked at the first claim."),17));
        p.Children.Add(Actions(Btn(T("حفظ الضمان","Save warranty"),()=>{Save();finish();Navigate("warranties");}),Btn(T("طباعة شهادة الضمان","Print warranty certificate"),()=>PrintWarrantyCertificate(Save())),Btn(T("استلام مطالبة","Receive claim"),()=>{long warranty=Save();finish();WarrantyIntake(warranty);},true)));w.ShowDialog();
    }
    void WarrantyIntake(long warranty)
    {
        var(w,p,finish)=Dialog(T("استلام جهاز ضمان","Warranty intake"),850);var date=Date();p.Children.Add(Field(T("تاريخ الاستلام","Received date"),date));var issue=Input();p.Children.Add(Field(T("العطل المبلغ عنه","Reported fault"),issue));var condition=Input();p.Children.Add(Field(T("حالة الجهاز / الأضرار الظاهرة","Physical condition / visible damage"),condition));var accessories=Input();p.Children.Add(Field(T("الملحقات المستلمة","Accessories received"),accessories));p.Children.Add(Text(T("الاستلام لا يضيف مخزونًا صالحًا للبيع ولا يؤكد الموافقة على الضمان.","Intake does not add sellable stock or confirm warranty approval."),17));p.Children.Add(Btn(T("حفظ الاستلام","Save intake"),()=>{long id=A.ReceiveWarranty(warranty,GetDate(date),issue.Text,condition.Text,accessories.Text);finish();Navigate("warranties");WarrantyDetails(id);},true));w.ShowDialog();
    }
    void WarrantyDetails(long id)
    {
        var c=S.WarrantyClaims().Rows.Cast<DataRow>().Single(r=>(long)r["id"]==id);var(w,p,finish)=Dialog(T("مطالبة ضمان #","Warranty claim #")+id,950);p.Children.Add(Text(c["serial"]+" · "+c["item"],22,true));p.Children.Add(Text(c["customer"]+" · "+c["invoice"],18));p.Children.Add(Text(WarrantyState((string)c["state"])+" · "+WarrantyState((string)c["custody"])+" · "+WarrantyCoverage(c),18,true));p.Children.Add(Text(T("نهاية الضمان: ","Expiry: ")+c["expires"]+"\n"+c["terms"],17));p.Children.Add(Text(T("العطل: ","Fault: ")+c["issue"]+"\n"+T("الحالة: ","Condition: ")+c["condition"]+"\n"+T("الملحقات: ","Accessories: ")+c["accessories"],17));
        if(c["supplier"]!=DBNull.Value)p.Children.Add(Text(T("المورد: ","Supplier: ")+S.LocalName("accounts",(long)c["supplier"],vm.Arabic)+" · "+c["reference"],17));if((string)c["exchange"]!="")p.Children.Add(Text(T("البديل: ","Replacement: ")+c["replacement"]+" · "+c["exchange"],18,true));
        var events=S.Table("SELECT created,username,note FROM warranty_events WHERE claim=@p0 ORDER BY id",id);var history=Grid(events,("created",T("الوقت","Time"),1.3),("username",T("المستخدم","User"),1),("note",T("السجل","History"),3));history.Height=180;p.Children.Add(history);
        var actions=Actions(Btn(T("الفاتورة الأصلية","Original invoice"),()=>ShowDocument((long)c["sale_doc"])),Btn(T("طباعة إيصال المطالبة","Print claim receipt"),()=>PrintWarranty(c)));p.Children.Add(actions);
        void Action(string key,string ar,string en)=>actions.Children.Add(Btn(T(ar,en),()=>{finish();WarrantyAction(id,key);}));
        if((string)c["custody"]=="InStore"){
            if((string)c["state"]=="Open"){actions.Children.Add(Btn(T("استبدال مجاني","Free replacement"),()=>{finish();WarrantyReplacementForm(id);},true));Action("Close","إعادة للعميل وإغلاق","Return to customer & close");}
            if((string)c["state"] is "Open" or "Replaced")Action("Send","إرسال للمورد","Send to supplier");
            if((string)c["state"]=="Replaced"){Action("Dispose","إتلاف الجهاز التالف","Dispose faulty device");Action("Undo","إلغاء الاستبدال","Undo replacement");}
        }else if((string)c["custody"]=="AtSupplier")Action("Receive","استلام من المورد","Receive from supplier");w.ShowDialog();
    }
    void WarrantyAction(long id,string action)
    {
        var(w,p,finish)=Dialog(T("تحديث المطالبة #","Update claim #")+id,800);ComboBox? supplier=null;TextBox? reference=null;
        if(action=="Send"){supplier=Choose(UiAccounts().Where(a=>a.Kind=="Supplier"));p.Children.Add(Field(T("المورد","Supplier"),supplier));reference=Input();p.Children.Add(Field(T("رقم متابعة المورد","Supplier reference"),reference));}
        p.Children.Add(Text(action=="Undo"?T("أكد استرجاع الجهاز البديل فعليًا وبقاء الجهاز الأصلي في المتجر. سيعود البديل للمخزون وتفتح المطالبة.","Confirm the replacement is physically back and the original is in store. This restores replacement stock and reopens the claim."):T("تحديث عهدة الجهاز فقط؛ لا ينشئ قيدًا ماليًا أو مخزونًا قابلًا للبيع.","This updates device custody only; it creates no financial entry or sellable stock."),17));var note=Input();p.Children.Add(Field(T("السبب / نتيجة الفحص","Reason / inspection outcome"),note));p.Children.Add(Btn(T("تأكيد وحفظ","Confirm & save"),()=>{if(action=="Undo")A.UndoWarrantyReplacement(id,note.Text);else A.UpdateWarrantyCustody(id,action,note.Text,supplier==null?null:GetAccount(supplier).Id,reference?.Text??"");finish();Navigate("warranties");WarrantyDetails(id);},true));w.ShowDialog();
    }
    void WarrantyReplacementForm(long id)
    {
        var(w,p,finish)=Dialog(T("استبدال مجاني","Free warranty replacement"),850);var date=Date();p.Children.Add(Field(T("تاريخ التسليم","Handover date"),date));var item=Choose(UiItems().Where(i=>S.IsSerialized(i.Id)));p.Children.Add(Field(T("الصنف البديل","Replacement item"),item));var location=LocationPicker(p);var serial=Choose(Array.Empty<string>());p.Children.Add(Field(T("التسلسلي البديل","Replacement serial"),serial));void Refresh(){serial.ItemsSource=item.SelectedItem is Item i?S.AvailableSerials(i.Id,PickLocation(location)).ToList():new List<string>();serial.SelectedIndex=-1;}item.SelectionChanged+=(_,_)=>Refresh();location.SelectionChanged+=(_,_)=>Refresh();Refresh();
        var reason=Input();p.Children.Add(Field(T("سبب استثناء الضمان (للمدير عند انتهاء / غياب الضمان)","Coverage exception reason (administrator, expired / unrecorded warranty)"),reason));p.Children.Add(Text(T("لا رسوم على العميل. تخصم وحدة من المخزون وتسجل تكلفتها كمصروف ضمان. يبقى الجهاز التالف في عهدة المتجر خارج المخزون، ويرث البديل نهاية الضمان الأصلية.","No customer charge. One unit leaves stock at cost as a warranty expense. The faulty device stays in store custody outside stock, and the replacement inherits the original expiry."),17));p.Children.Add(Btn(T("تأكيد الاستبدال المجاني","Confirm free replacement"),()=>{if(serial.SelectedItem is not string number)throw new InvalidOperationException(T("اختر جهازًا متاحًا","Select an available device"));A.ReplaceWarranty(id,GetDate(date),GetItem(item).Id,PickLocation(location),number,reason.Text);finish();Navigate("warranties");WarrantyDetails(id);},true));w.ShowDialog();
    }
    void PrintWarrantyCertificate(long id)
    {
        var r=S.WarrantyDevices().Rows.Cast<DataRow>().Single(r=>r["warranty"]!=DBNull.Value&&(long)r["warranty"]==id);var doc=Page(T("شهادة ضمان","Warranty certificate"),S.Setting("company"));foreach(var f in new[]{("customer",T("العميل","Customer")),("invoice",T("الفاتورة الأصلية","Original invoice")),("sold",T("تاريخ البيع","Sale date")),("item",T("الجهاز","Device")),("serial",T("التسلسلي","Serial")),("expires",T("آخر يوم للضمان","Last covered day")),("terms",T("الشروط","Terms"))})Paragraph(doc,f.Item2+": "+r[f.Item1]);if(r["expires"]==DBNull.Value)Paragraph(doc,T("مدة الضمان غير مسجلة","Warranty duration is not recorded"),true);Paragraph(doc,T("الاستبدال المجاني لا يمدد نهاية الضمان الأصلية.","A free replacement does not extend the original warranty expiry."));Paragraph(doc,T("التوقيع: __________________","Signature: __________________"));var(w,p,finish)=Dialog(T("شهادة ضمان","Warranty certificate"),1000);p.Children.Add(new FlowDocumentScrollViewer{Document=doc,Height=480,IsToolBarVisible=false});p.Children.Add(Actions(Btn(T("طباعة / PDF","Print / PDF"),()=>Print(doc),true),Btn(T("إغلاق","Close"),finish)));w.ShowDialog();
    }
    FlowDocument WarrantyReceipt(DataRow c)
    {
        var doc=Page(T("إيصال مطالبة ضمان #","Warranty claim receipt #")+c["id"],S.Setting("company"));foreach(var pair in new[]{("customer",T("العميل","Customer")),("invoice",T("الفاتورة الأصلية","Original invoice")),("serial",T("تسلسلي الجهاز","Device serial")),("item",T("الصنف","Item")),("received",T("الاستلام","Received")),("expires",T("نهاية الضمان","Warranty expiry")),("terms",T("شروط الضمان","Warranty terms")),("issue",T("العطل","Fault")),("condition",T("الحالة الظاهرة","Physical condition")),("accessories",T("الملحقات","Accessories")),("replacement",T("التسلسلي البديل","Replacement serial")),("exchange",T("مستند الاستبدال","Replacement document")),("resolution",T("النتيجة","Resolution")),("reference",T("مرجع المورد","Supplier reference"))})Paragraph(doc,pair.Item2+": "+c[pair.Item1]);Paragraph(doc,WarrantyState((string)c["state"])+" · "+WarrantyState((string)c["custody"])+" · "+WarrantyCoverage(c),true);Paragraph(doc,T("الاستبدال مجاني ولا يمدد مدة الضمان. الاستلام وحده لا يعني الموافقة على المطالبة.","Replacement is free and does not extend warranty duration. Intake alone does not mean the claim is approved."));Paragraph(doc,T("توقيع العميل: __________________  الموظف: __________________","Customer signature: __________________  Staff: __________________"));return doc;
    }
    void PrintWarranty(DataRow c){var(w,p,finish)=Dialog(T("إيصال ضمان","Warranty receipt"),1000);var doc=WarrantyReceipt(c);p.Children.Add(new FlowDocumentScrollViewer{Document=doc,Height=480,IsToolBarVisible=false});p.Children.Add(Actions(Btn(T("طباعة / PDF","Print / PDF"),()=>Print(doc),true),Btn(T("إغلاق","Close"),finish)));w.ShowDialog();}
}
