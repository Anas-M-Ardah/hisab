using System.Data;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Hisab;
public sealed partial class MainWindow
{
    List<Account> UiAccounts()=>S.Accounts().Select(a=>a with{Name=S.LocalName("accounts",a.Id,vm.Arabic)}).ToList();
    List<Item> UiItems()=>S.Items().Select(i=>i with{Name=S.LocalName("items",i.Id,vm.Arabic)}).ToList();
    record PositionChoice(long? Id,string Label){public override string ToString()=>Label;}
    void BilingualForm(string table,long id)
    {
        var row=S.Table($"SELECT * FROM {table} WHERE id=@p0",id).Rows[0];var (w,p,finish)=Dialog(T("الاسم والعنوان باللغتين","Bilingual name & address"),800);
        var ar=Input((string)row["name_ar"]);var en=Input((string)row["name_en"]);p.Children.Add(Field("الاسم العربي / Arabic name",ar));p.Children.Add(Field("English name / الاسم الإنجليزي",en));var addressAr=Input(table=="accounts"?(string)row["address_ar"]:"");var addressEn=Input(table=="accounts"?(string)row["address_en"]:"");
        if(table=="accounts"){p.Children.Add(Field("العنوان العربي / Arabic address",addressAr));p.Children.Add(Field("English address / العنوان الإنجليزي",addressEn));}
        p.Children.Add(Text(T("تبقى الأسماء الأصلية احتياطًا عندما تكون الترجمة فارغة.","Original names remain the fallback when a translation is empty."),18));p.Children.Add(Btn(T("حفظ","Save"),()=>{S.SaveTranslations(table,id,ar.Text,en.Text,addressAr.Text,addressEn.Text);finish();Navigate(table=="accounts"?"accounts":"items");},true));w.ShowDialog();
    }
    void Settlements(long id)
    {
        var doc=S.Table("SELECT * FROM documents WHERE id=@p0",id).Rows[0];string kind=(string)doc["kind"];bool invoice=kind is "SI" or "PI";
        if(!invoice&&kind is not("RC" or "PV" or "CR" or "CP"))throw new InvalidOperationException(T("اختر فاتورة أو سند قبض/صرف أو شيكًا","Choose an invoice, voucher or cheque document"));
        var (w,p,_)=Dialog(T("توزيع الدفعات على الفواتير","Invoice payment allocation"),950,false);p.Children.Add(Text(doc["number"].ToString()!,24,true));var summary=Text("",21,true);p.Children.Add(summary);
        var grid=new DataGrid{Height=210};var selector=new ComboBox();var amount=Input("0",true);var date=Date();
        void Refresh(){var rows=S.Table("SELECT x.id,v.number voucher,i.number invoice,x.amount,x.date FROM allocations x JOIN documents v ON v.id=x.voucher JOIN documents i ON i.id=x.invoice WHERE "+(invoice?"x.invoice":"x.voucher")+"=@p0 AND x.revoked IS NULL",id);rows.Columns.Add("amount_text");foreach(DataRow r in rows.Rows)r["amount_text"]=Store.Money((long)r["amount"]);grid.ItemsSource=rows.DefaultView;
            if(invoice){var pos=A.Positions(DateTime.Today).FirstOrDefault(i=>i.Invoice==id);summary.Text=pos==null?T("المستند ملغى","Document cancelled"):T("المتبقي: ","Outstanding: ")+Store.Money(pos.Outstanding)+" JOD";selector.ItemsSource=S.Table("SELECT d.id,d.number,d.total-coalesce((SELECT sum(amount) FROM allocations WHERE voucher=d.id AND revoked IS NULL),0) available FROM documents d WHERE d.party=@p0 AND d.kind IN ('RC','PV','CR','CP') AND d.reversed=0 AND d.kind IN "+(kind=="SI"?"('RC','CR')":"('PV','CP')"),doc["party"]).Rows.Cast<DataRow>().Where(r=>(long)r["available"]>0).Select(r=>new PositionChoice((long)r["id"],r["number"]+" · "+Store.Money((long)r["available"])+" JOD")).ToList();}
            else{long available=(long)doc["total"]-Convert.ToInt64(S.Scalar("SELECT coalesce(sum(amount),0) FROM allocations WHERE voucher=@p0 AND revoked IS NULL",id));summary.Text=T("غير موزع: ","Unallocated: ")+Store.Money(available)+" JOD";selector.ItemsSource=A.Positions(DateTime.Today,(long)doc["party"],kind is "RC" or "CR"?"SI":"PI").Where(i=>i.Outstanding>0).Select(i=>new PositionChoice(i.Invoice,i.Number+" · "+Store.Money(i.Outstanding)+" JOD")).ToList();}}
        foreach(var c in new[]{("voucher",T("السند","Voucher")),("invoice",T("الفاتورة","Invoice")),("amount_text",T("المبلغ","Amount")),("date",T("التاريخ","Date"))})grid.Columns.Add(new DataGridTextColumn{Header=c.Item2,Binding=new System.Windows.Data.Binding("["+c.Item1+"]"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
        p.Children.Add(grid);p.Children.Add(Field(T(invoice?"السند":"الفاتورة",invoice?"Voucher":"Invoice"),selector));p.Children.Add(Field(T("المبلغ JOD","Amount JOD"),amount));p.Children.Add(Field(T("تاريخ التوزيع","Allocation date"),date));
        p.Children.Add(Actions(Btn(T("توزيع الدفعة","Allocate payment"),()=>{long selected=(selector.SelectedItem as PositionChoice)?.Id??throw new InvalidOperationException(T("اختر المستند","Choose document"));A.Allocate(invoice?selected:id,invoice?id:selected,Store.Parse(amount.Text),GetDate(date));Refresh();},true),Btn(T("إلغاء التوزيع المحدد","Remove selected allocation"),()=>{A.RemoveAllocation(Selected(grid),GetDate(date));Refresh();}),Btn(T("إغلاق","Close"),()=>w.Close())));Refresh();w.ShowDialog();
    }
    long SaveInvoiceForm(long? editing,bool sale,DateTime date,long party,long? cash,decimal paid,decimal vat,bool inclusive,string note,List<InvoiceLine> lines,DateTime due,string reason)
    {
        using var tx=S.BeginTransaction();long id=editing==null?A.Invoice(sale,date,party,cash,paid,vat,inclusive,note,lines):A.AmendInvoice(editing.Value,sale,date,party,cash,paid,vat,inclusive,note,lines,reason);A.SetDueDate(id,due);tx.Commit();return id;
    }
    long SaveVoucherForm(bool receipt,DateTime date,long party,long cash,decimal amount,string note,long? invoice)
    {
        using var tx=S.BeginTransaction();long id=A.Voucher(receipt,date,party,cash,amount,note);if(invoice!=null){long outstanding=A.Positions(DateTime.Today).Single(p=>p.Invoice==invoice).Outstanding;long allocated=Math.Min(Store.M(amount),outstanding);if(allocated>0)A.Allocate(id,invoice.Value,Store.D(allocated),date);}tx.Commit();return id;
    }
    void ReturnForm(long original)
    {
        var source=S.Table("SELECT * FROM documents WHERE id=@p0",original).Rows[0];if(source["kind"].ToString() is not("SI" or "PI"))throw new InvalidOperationException(T("اختر فاتورة مبيعات أو مشتريات","Choose a sales or purchase invoice"));
        var (w,p,finish)=Dialog(T("مرتجع فاتورة","Invoice return"),1000);var date=Date();var reason=Input();p.Children.Add(Text(source["number"].ToString()!,23,true));p.Children.Add(Field(T("تاريخ المرتجع","Return date"),date));p.Children.Add(Field(T("سبب المرتجع","Return reason"),reason));var quantities=new Dictionary<long,TextBox>();
        foreach(DataRow line in S.Table("SELECT * FROM invoice_lines WHERE doc=@p0 ORDER BY id",original).Rows){long returned=Convert.ToInt64(S.Scalar("SELECT coalesce(sum(l.qty),0) FROM invoice_lines l JOIN documents d ON d.id=l.doc WHERE l.source_line=@p0 AND d.reversed=0",line["id"]));var quantity=Input("0",true);quantities[(long)line["id"]]=quantity;p.Children.Add(Field(line["description"]+T(" — المتبقي للإرجاع: "," — returnable: ")+Store.D((long)line["qty"]-returned),quantity));}
        p.Children.Add(Text(T("المرتجع يخفض الفاتورة ويحدّث المخزون والضريبة. إذا سبق تحصيلها، يظهر رصيد للعميل أو المورد؛ استخدم سندًا لرد المبلغ.","The return reduces the invoice and updates stock and tax. Previously paid amounts become account credits; use a voucher for the refund."),18));p.Children.Add(Btn(T("مراجعة وحفظ المرتجع","Review & save return"),()=>{var rows=quantities.Select(q=>new ReturnLine(q.Key,Store.Parse(q.Value.Text))).Where(q=>q.Quantity!=0).ToList();if(Confirm(reason.Text+"\n"+string.Join("\n",rows.Select(r=>r.SourceLine+": "+r.Quantity))))Saved(A.ReturnInvoice(original,GetDate(date),rows,reason.Text),finish);},true));w.ShowDialog();
    }
    void Attachments(long doc)
    {
        var (w,p,_)=Dialog(T("مرفقات المستند","Document attachments"),900,false);var grid=Grid(S.Table("SELECT id,name,created,length(data) size FROM attachments WHERE doc=@p0",doc),("name",T("الملف","File"),3),("created",T("التاريخ","Date"),2),("size",T("بايت","Bytes"),1));p.Children.Add(grid);
        p.Children.Add(Actions(Btn(T("إضافة مرفق…","Add attachment…"),()=>{var open=new OpenFileDialog();if(open.ShowDialog(w)==true){S.AddAttachment(doc,open.FileName);grid.ItemsSource=S.Table("SELECT id,name,created,length(data) size FROM attachments WHERE doc=@p0",doc).DefaultView;}},true),Btn(T("حفظ الملف المحدد…","Save selected file…"),()=>{var r=S.Table("SELECT * FROM attachments WHERE id=@p0",Selected(grid)).Rows[0];var save=new SaveFileDialog{FileName=System.IO.Path.GetFileName((string)r["name"])};if(save.ShowDialog(w)==true)File.WriteAllBytes(save.FileName,(byte[])r["data"]);}),Btn(T("إغلاق","Close"),()=>w.Close())));w.ShowDialog();
    }
    void ChequesPage()
    {
        Heading(T("سجل الشيكات","Cheque register"),T("سجّل الشيك ثم حدّد التحصيل أو الصرف أو الارتجاع.","Record a cheque, then track clearing, payment or bouncing."));
        var rows=S.Table("SELECT c.*,a.name party_name FROM cheques c JOIN accounts a ON a.id=c.party ORDER BY c.due_date,c.id");rows.Columns.Add("amount_text");rows.Columns.Add("state_text");rows.Columns.Add("direction_text");foreach(DataRow r in rows.Rows){r["amount_text"]=Store.Money((long)r["amount"]);r["state_text"]=r["state"].ToString() switch{"Pending"=>T("قيد الانتظار","Pending"),"Cleared"=>T("محصل / مصروف","Cleared"),"Bounced"=>T("مرتجع","Bounced"),_=>T("ملغى","Cancelled")};r["direction_text"]=r["direction"].ToString()=="Incoming"?T("وارد","Incoming"):T("صادر","Outgoing");r["party_name"]=S.LocalName("accounts",(long)r["party"],vm.Arabic);}
        var grid=Grid(rows,("number",T("الرقم","Number"),1),("bank",T("البنك","Bank"),1.5),("direction_text",T("الاتجاه","Direction"),1),("party_name",T("الحساب","Account"),2),("due_date",T("تاريخ الاستحقاق","Due date"),1.2),("amount_text",T("المبلغ","Amount"),1.2),("state_text",T("الحالة","Status"),1.5));
        body.Children.Add(Btn(T("+ قبض بشيكات متعددة","+ Receive multiple cheques"),ChequeReceiptForm,true));
        body.Children.Add(Actions(Btn(T("+ شيك وارد","+ Incoming cheque"),()=>ChequeForm(true),true),Btn(T("+ شيك صادر","+ Outgoing cheque"),()=>ChequeForm(false)),Btn(T("تحديث حالة المحدد","Update selected status"),()=>ChequeStateForm(Selected(grid))),Btn(T("فتح مستند الشيك","Open cheque document"),()=>{var r=S.Table("SELECT issue_doc FROM cheques WHERE id=@p0",Selected(grid)).Rows[0];ShowDocument((long)r["issue_doc"]);})));body.Children.Add(grid);
    }
    void ChequeForm(bool incoming)
    {
        var (w,p,finish)=Dialog(T(incoming?"شيك وارد":"شيك صادر",incoming?"Incoming cheque":"Outgoing cheque"),850);var number=Input();var bank=Input();var account=Choose(UiAccounts().Where(a=>a.Kind!="Unclassified"));var amount=Input("",true);var issue=Date();var due=new DatePicker{SelectedDate=DateTime.Today};
        foreach(var field in new[]{Field(T("رقم الشيك","Cheque number"),number),Field(T("البنك","Bank"),bank),Field(T("الحساب","Account"),account),Field(T("المبلغ JOD","Amount JOD"),amount),Field(T("تاريخ الاستلام / الإصدار","Issue date"),issue),Field(T("تاريخ الاستحقاق","Due date"),due)})p.Children.Add(field);
        p.Children.Add(Btn(T("مراجعة وحفظ","Review & save"),()=>{if(Confirm(number.Text+" · "+bank.Text+"\n"+amount.Text+" JOD"))Saved(A.RegisterCheque(incoming,number.Text,bank.Text,GetAccount(account).Id,Store.Parse(amount.Text),GetDate(issue),GetDate(due)),finish);},true));w.ShowDialog();
    }
    void ChequeStateForm(long id)
    {
        var (w,p,finish)=Dialog(T("تحديث حالة الشيك","Update cheque status"),800);var state=new ComboBox{ItemsSource=new[]{new Choice("Cleared",T("تحصيل / صرف","Clear / pay")),new Choice("Bounced",T("مرتجع","Bounced")),new Choice("Cancelled",T("إلغاء","Cancel"))},DisplayMemberPath="Label",SelectedValuePath="Key",SelectedIndex=0};var bank=Choose(UiAccounts().Where(a=>a.Kind=="Cash"));bank.SelectedIndex=0;var date=Date();var reason=Input();p.Children.Add(Field(T("الحالة الجديدة","New status"),state));p.Children.Add(Field(T("حساب البنك","Bank account"),bank));p.Children.Add(Field(T("تاريخ العملية","Operation date"),date));p.Children.Add(Field(T("البيان / السبب","Description / reason"),reason));p.Children.Add(Btn(T("مراجعة وحفظ","Review & save"),()=>{if(Confirm(state.Text+"\n"+reason.Text)){long document=A.SettleCheque(id,state.SelectedValue.ToString()!,GetAccount(bank).Id,GetDate(date),reason.Text);finish();Navigate("cheques");ShowDocument(document);}},true));w.ShowDialog();
    }
    void UsersPage()
    {
        S.Require("users");Heading(T("حسابات مشتركة — اختيارية","Shared accounts — optional"),T("لا تحتاجها لاستخدامك الشخصي. أضف مديرًا ومستخدمين فقط إذا كان الدفتر مشتركًا.","You do not need these for personal use. Add accounts only when the ledger is shared."));var grid=Grid(S.Table("SELECT id,username,role,active FROM users WHERE personal_owner=0 ORDER BY username"),("username",T("المستخدم","Username"),2),("role",T("الدور","Role"),2),("active",T("فعال","Active"),1));body.Children.Add(Actions(Btn(T("+ مستخدم","+ User"),()=>UserForm(null),true),Btn(T("تعديل / تغيير كلمة المرور","Edit / change password"),()=>UserForm(Selected(grid))),Btn(T("تفعيل تسجيل الدخول المشترك","Enable shared sign-in"),()=>{S.EnableSharedSignIn();BuildShell();vm.Status=T("تم تفعيل الدخول بالحسابات المشتركة.","Shared sign-in enabled.");})));body.Children.Add(grid);
    }
    void UserForm(long? id)
    {
        var (w,p,finish)=Dialog(T("مستخدم وصلاحيات","User & permissions"),750);var row=id==null?null:S.Table("SELECT * FROM users WHERE id=@p0",id).Rows[0];var name=Input(row?["username"].ToString()??"");var password=new PasswordBox{MinHeight=48};var role=new ComboBox{ItemsSource=new[]{"Admin","Accountant","Viewer"},SelectedItem=row?["role"].ToString()??"Accountant"};var active=new CheckBox{Content=T("مستخدم فعال","Active user"),IsChecked=row==null||(long)row["active"]==1};p.Children.Add(Field(T("اسم المستخدم","Username"),name));p.Children.Add(Field(T("كلمة مرور جديدة (٨ أحرف على الأقل)","New password (at least 8 characters)"),password));p.Children.Add(Field(T("الدور","Role"),role));p.Children.Add(active);p.Children.Add(Btn(T("حفظ","Save"),()=>{S.SaveUser(name.Text,password.Password,role.SelectedItem.ToString()!,id,active.IsChecked==true);finish();Navigate("users");},true));w.ShowDialog();
    }
    void PeriodsPage()
    {
        S.Require("period");Heading(T("إقفال السنة والفترات","Year & period closing"),T("يُنقل صافي الإيرادات والمصروفات إلى الأرباح المحتجزة، ويُمنع الترحيل قبل نهاية الفترة المقفلة.","Income and expenses close to retained earnings. Posting on or before the closed period end is locked."));var grid=Grid(S.Table("SELECT * FROM periods ORDER BY end DESC"),("start",T("البداية","Start"),2),("end",T("النهاية","End"),2));body.Children.Add(grid);var start=Date();start.SelectedDate=new DateTime(DateTime.Today.Year-1,1,1);var end=Date();end.SelectedDate=new DateTime(DateTime.Today.Year-1,12,31);var reason=Input();body.Children.Add(Field(T("من تاريخ","From"),start));body.Children.Add(Field(T("إلى تاريخ","Through"),end));body.Children.Add(Field(T("سبب إعادة الفتح","Reopening reason"),reason));body.Children.Add(Actions(Btn(T("إقفال الفترة","Close period"),()=>{if(Confirm(T("سيُقفل الترحيل لهذه الفترة. متابعة؟","Posting will be locked for this period. Continue?"))){A.CloseYear(GetDate(start),GetDate(end));Navigate("periods");}},true),Btn(T("إعادة فتح المحددة","Reopen selected"),()=>{if(Confirm(reason.Text)){A.ReopenYear(Selected(grid),reason.Text);Navigate("periods");}})));
    }
    void BrandingSettings(StackPanel panel)
    {
        var fields=new Dictionary<string,TextBox>();foreach(var key in new[]{"company_en","terms_en","warranty_en","shipping_en","bank_en"}){var input=Input(S.Setting(key));fields[key]=input;panel.Children.Add(Field(key switch{"company_en"=>"English company name","terms_en"=>"English payment terms","warranty_en"=>"English warranty","shipping_en"=>"English shipping / installation",_=>"English bank payment details"},input));}
        var due=Input(S.Setting("due_days","30"),true);var negative=new CheckBox{Content=T("السماح بالمخزون السالب مع تكلفة مؤقتة وإعادة احتساب عند التوريد","Allow negative stock with provisional costing and receipt-time adjustment"),IsChecked=S.Setting("allow_negative","0")=="1",Margin=new(0,8,0,18)};panel.Children.Add(Field(T("أيام الاستحقاق الافتراضية","Default invoice due days"),due));panel.Children.Add(negative);
        void Image(string key){var open=new OpenFileDialog{Filter="Images (*.png;*.jpg)|*.png;*.jpg"};if(open.ShowDialog(this)==true){var data=File.ReadAllBytes(open.FileName);if(data.Length>2*1024*1024)throw new InvalidOperationException("Image limit: 2 MB");S.Set(key,Convert.ToBase64String(data));vm.Status=T("تم حفظ الصورة","Image saved");}}
        panel.Children.Add(Actions(Btn(T("اختيار الشعار…","Choose logo…"),()=>Image("logo")),Btn(T("اختيار الختم…","Choose seal…"),()=>Image("seal")),Btn(T("حذف الشعار والختم","Remove logo & seal"),()=>{S.Set("logo","");S.Set("seal","");})));
        panel.Children.Add(Btn(T("حفظ إعدادات الفواتير المتقدمة","Save advanced invoice settings"),()=>{if(!int.TryParse(due.Text,out int days)||days<0||days>3650)throw new InvalidOperationException("Due days must be 0–3650");using var tx=S.BeginTransaction();foreach(var entry in fields)S.Set(entry.Key,entry.Value.Text);S.Set("due_days",days.ToString());S.Set("allow_negative",negative.IsChecked==true?"1":"0");A.RebuildInventory("inventory policy change");tx.Commit();vm.Status=T("تم الحفظ","Saved");},true));
    }
    string? PasswordPrompt(string title,bool confirm)
    {
        string? result=null;var (w,p,finish)=Dialog(title,700,false);var password=new PasswordBox{MinHeight=48};var again=new PasswordBox{MinHeight=48};p.Children.Add(Field(T("كلمة المرور","Password"),password));if(confirm)p.Children.Add(Field(T("تأكيد كلمة المرور","Confirm password"),again));p.Children.Add(Btn(T("متابعة","Continue"),()=>{if(confirm&&(password.Password.Length<10||password.Password!=again.Password))throw new InvalidOperationException(T("كلمة المرور من ٨ أحرف على الأقل والتأكيد مطابق","Use 10+ characters and matching confirmation"));result=password.Password;finish();},true));w.ShowDialog();return result;
    }
    void PortableRestore()
    {
        S.Require("restore");var open=new OpenFileDialog{Filter="Hisab backups (*.hisab;*.hdb;*.db)|*.hisab;*.hdb;*.db"};if(open.ShowDialog(this)!=true)return;string? password=null;if(System.IO.Path.GetExtension(open.FileName).Equals(".hisab",StringComparison.OrdinalIgnoreCase)){password=PasswordPrompt(T("كلمة مرور النسخة الاحتياطية","Backup password"),false);if(password==null)return;}
        if(!Confirm(T("ستُستبدل البيانات الحالية بعد حفظ نسخة أمان. متابعة؟","Current data will be replaced after saving a safety backup. Continue?")))return;S.RestorePortable(open.FileName,password);if(!SessionGate.Enter(S,this)){Close();return;}BuildShell();vm.Status=T("تمت الاستعادة","Restored");
    }
}
