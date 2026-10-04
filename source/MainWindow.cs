using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Hisab;
public sealed partial class MainWindow : Window
{
    readonly MainViewModel vm;
    Store S=>vm.Store;
    AccountingService A=>vm.Accounting;
    string T(string ar,string en)=>vm.T(ar,en);
    readonly Brush ink=new SolidColorBrush(Color.FromRgb(28,49,65));
    readonly Brush muted=new SolidColorBrush(Color.FromRgb(83,103,117));
    readonly Brush accent=new SolidColorBrush(Color.FromRgb(14,104,99));
    StackPanel body=null!;
    DockPanel shell=null!;
    readonly Dictionary<string,Button> navigation=[];
    string? previewDirectory;
    string? previewCaptureName;
    int previewDialog;
    public MainWindow(MainViewModel model)
    {
        vm=model; DataContext=vm; Title="Hisab · حساب"; Width=Math.Min(1320,SystemParameters.WorkArea.Width-24); Height=Math.Min(880,SystemParameters.WorkArea.Height-24); MinWidth=640; MinHeight=480; WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=new SolidColorBrush(Color.FromRgb(243,246,245)); FontFamily=new FontFamily("Segoe UI"); Foreground=ink;
        Resources=UiTheme.Create(); ink=UiTheme.Brush(Resources,"Brush.Text"); muted=UiTheme.Brush(Resources,"Brush.Secondary"); accent=UiTheme.Brush(Resources,"Brush.Primary"); Background=UiTheme.Brush(Resources,"Brush.Window"); Foreground=ink;
        BuildShell();
        SizeChanged+=(s,e)=>ApplyResponsiveLayout();
        PreviewKeyDown+=(s,e)=>{if(e.Key==Key.F1){Help();e.Handled=true;} if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.N){InvoiceForm(true);e.Handled=true;} if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.B){Backup();e.Handled=true;}};
        Closing+=(s,e)=>{ try{ var dir=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(S.Path)!,"backups");Directory.CreateDirectory(dir);S.Backup(System.IO.Path.Combine(dir,"automatic-"+DateTime.Now.ToString("yyyyMMdd")+(S.Encrypted?".hdb":".db")));}catch(Exception ex){File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(S.Path)!,"errors.log"),ex+Environment.NewLine);} };
    }
    TextBlock Text(string text,double size=0,bool bold=false)=>new(){Text=text,FontSize=size==0?FontSize:size*FontSize/20,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,10)};
    Button Btn(string label,Action action,bool primary=false)
    {
        var b=new Button {Content=label,Command=new RelayCommand(()=>Guard(action)),FontSize=FontSize};
        System.Windows.Automation.AutomationProperties.SetName(b,label);
        if(primary){b.Background=accent;b.Foreground=UiTheme.Brush(Resources,"Brush.OnPrimary");b.BorderBrush=accent;} return b;
    }
    void Guard(Action action)
    {
        try {action();} catch(Exception ex) {
            File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(S.Path)!,"errors.log"),DateTime.Now+" "+ex+Environment.NewLine);
            var msg=ex is InvalidOperationException||ex is OverflowException?ex.Message:T("تعذر حفظ العملية. تحقق من عدم تكرار الكود ومن صحة البيانات.","Could not save. Check for duplicate codes and invalid values.");
            MessageBox.Show(this,msg,T("تحقق من البيانات","Check the information"),MessageBoxButton.OK,MessageBoxImage.Information);
        }
    }
    void BuildShell()=>BuildModernShell();
    public void Navigate(string route)
    {
        vm.Route=route; body.Children.Clear();
        foreach(var b in navigation){b.Value.Background=b.Key==route?accent:Brushes.Transparent;b.Value.Foreground=UiTheme.Brush(Resources,"Brush.NavText");}
        switch(route){case "home":Home();break;case "tools":ToolsPage();break;case "invoices":Documents();break;case "receipt":VoucherForm(true);break;case "payment":VoucherForm(false);break;case "journal":JournalForm();break;case "accounts":AccountsPage();break;case "items":ItemsPage();break;case "reports":Reports();break;case "cheques":ChequesPage();break;case "periods":PeriodsPage();break;case "users":UsersPage();break;case "backup":BackupsPage();break;case "settings":Settings();break;}
        if(route is "receipt" or "payment" or "journal") {vm.Route="home";Navigate("home");}
    }
    void Heading(string title,string subtitle){body.Children.Add(Text(title,32,true));var t=Text(subtitle,18);t.Foreground=muted;body.Children.Add(t);}
    Border Card(UIElement content)=>new(){Background=UiTheme.Brush(Resources,"Brush.Surface"),CornerRadius=new(16),BorderBrush=UiTheme.Brush(Resources,"Brush.Divider"),BorderThickness=new(1),Padding=new(22),Margin=new(0,6,0,16),Child=content};
    WrapPanel Actions(params Button[] buttons){var p=new WrapPanel{Margin=new(0,10,0,6)};foreach(var b in buttons)p.Children.Add(b);return p;}
    DataGrid Grid(DataTable table,params(string Field,string Label,double Width)[] columns)
    {
        var grid=new DataGrid{ItemsSource=table.DefaultView,MaxHeight=440,FontSize=18};
        foreach(var c in columns) {
            var style=new Style(typeof(TextBlock));style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));style.Setters.Add(new Setter(TextBlock.ToolTipProperty,new Binding("["+c.Field+"]")));
            grid.Columns.Add(new DataGridTextColumn{Header=c.Label,Binding=new Binding("["+c.Field+"]"),ElementStyle=style,Width=new DataGridLength(c.Width,DataGridLengthUnitType.Star)});
        }
        return grid;
    }
    void Home()=>ModernHome();
    string Kind(string kind)=>kind switch{"SI"=>T("فاتورة مبيعات","Sales invoice"),"PI"=>T("فاتورة مشتريات","Purchase invoice"),"RC"=>T("سند قبض","Receipt"),"PV"=>T("سند صرف","Payment"),"JV"=>T("سند قيد","Journal"),"OB"=>T("مخزون افتتاحي","Opening stock"),"RV"=>T("قيد إلغاء","Reversal"),"SR"=>T("مرتجع مبيعات","Sales return"),"PR"=>T("مرتجع مشتريات","Purchase return"),"CR"=>T("شيك وارد","Incoming cheque"),"CP"=>T("شيك صادر","Outgoing cheque"),"CC"=>T("تسوية شيك","Cheque clearing"),"CL"=>T("إقفال مالي","Year closing"),"VA"=>T("تعديل تكلفة","Cost adjustment"),_=>kind};
    string AccountKind(string kind)=>kind switch{"Customer"=>T("عميل","Customer"),"Supplier"=>T("مورد","Supplier"),"Cash"=>T("صندوق / بنك","Cash / bank"),"Asset"=>T("أصول","Asset"),"Liability"=>T("التزامات","Liability"),"Equity"=>T("حقوق ملكية","Equity"),"Income"=>T("إيرادات","Income"),"Expense"=>T("مصروفات","Expense"),_=>T("يحتاج تصنيف","Needs classification")};
    DataTable DocumentTable(string search,int limit=500)
    {
        var t=S.Table("SELECT d.id,d.number,d.kind,d.date,coalesce(a.name,'') party,d.total,d.reversed FROM documents d LEFT JOIN accounts a ON a.id=d.party WHERE d.number LIKE @p0 OR coalesce(a.name,'') LIKE @p0 OR d.note LIKE @p0 ORDER BY d.id DESC LIMIT @p1","%"+search+"%",limit);
        t.Columns.Add("amount");t.Columns.Add("type");t.Columns.Add("state");foreach(DataRow r in t.Rows){r["amount"]=Store.Money((long)r["total"]);r["type"]=Kind((string)r["kind"]);r["state"]=(long)r["reversed"]==1?T("ملغى","Cancelled"):T("محفوظ","Posted");}return t;
    }
    DataGrid DocumentGrid(DataTable t)=>Grid(t,("number",T("الرقم","Number"),1.2),("date",T("التاريخ","Date"),1.1),("type",T("النوع","Type"),1.5),("party",T("الحساب","Account"),2),("amount",T("الإجمالي JOD","Total JOD"),1.2),("state",T("الحالة","Status"),1));
    long Selected(DataGrid grid)=>grid.SelectedItem is DataRowView r?(long)r["id"]:throw new InvalidOperationException(T("اختر سطرًا أولًا","Select a row first"));
    void OpenSelected(DataGrid grid)=>ShowDocument(Selected(grid));
    void Documents()
    {
        Heading(T("الفواتير والمستندات","Invoices & documents"),T("ابحث برقم المستند أو اسم الحساب، ثم افتحه أو اطبعه.","Search by document number or account name, then open or print it."));
        body.Children.Add(Actions(Btn(T("+ فاتورة مبيعات","+ Sales invoice"),()=>InvoiceForm(true),true),Btn(T("+ فاتورة مشتريات","+ Purchase invoice"),()=>InvoiceForm(false))));
        var search=new TextBox();body.Children.Add(Field(T("بحث","Search"),search));var grid=DocumentGrid(DocumentTable(""));body.Children.Add(grid);search.TextChanged+=(s,e)=>grid.ItemsSource=DocumentTable(search.Text).DefaultView;
        grid.MouseDoubleClick+=(s,e)=>Guard(()=>OpenSelected(grid));
        body.Children.Add(Actions(Btn(T("تعديل المستند","Amend document"),()=>{long id=Selected(grid);var d=S.Table("SELECT kind FROM documents WHERE id=@p0",id).Rows[0];if(d["kind"].ToString() is "SI" or "PI")InvoiceForm(d["kind"].ToString()=="SI",id);else AmendOtherDocument(id);}),Btn(T("مرتجع","Return"),()=>ReturnForm(Selected(grid))),Btn(T("توزيع الدفعات","Payment allocations"),()=>Settlements(Selected(grid))),Btn(T("المرفقات","Attachments"),()=>Attachments(Selected(grid))),Btn(T("وصف باللغتين","Bilingual descriptions"),()=>DocumentTranslations(Selected(grid)))));
        body.Children.Add(Actions(Btn(T("فتح / طباعة","Open / print"),()=>OpenSelected(grid)),Btn(T("إلغاء المستند المحدد","Cancel selected document"),()=>{
            long id=Selected(grid); var (w,p,finish)=Dialog(T("إلغاء مستند","Cancel document"),620);p.Children.Add(Text(T("يبقى المستند في السجل، ويُحفظ قيد معاكس. لا يمكن حذف التاريخ المحاسبي.","The document stays in history and a reversing entry is posted."),18));var reason=new TextBox();p.Children.Add(Field(T("سبب الإلغاء","Cancellation reason"),reason));p.Children.Add(Btn(T("مراجعة الإلغاء","Review cancellation"),()=>{if(Confirm(T("هل تريد إلغاء هذا المستند؟","Cancel this document?")+"\n"+reason.Text)){long reversal=A.Reverse(id,reason.Text);finish();Navigate("invoices");ShowDocument(reversal);}},true));w.ShowDialog();
        })));
    }
    FrameworkElement Field(string name,FrameworkElement control)
    {
        var p=new StackPanel{Margin=new(0,0,0,16)};var label=Text(name,18,true);p.Children.Add(label);p.Children.Add(control);System.Windows.Automation.AutomationProperties.SetName(control,name);System.Windows.Automation.AutomationProperties.SetLabeledBy(control,label);return p;
    }
    TextBox Input(string value="",bool numeric=false)=>new(){Text=value,FlowDirection=numeric?FlowDirection.LeftToRight:FlowDirection,HorizontalContentAlignment=numeric?HorizontalAlignment.Left:HorizontalAlignment.Stretch};
    ComboBox Choose<TItem>(IEnumerable<TItem> values)=>new(){ItemsSource=values.ToList(),IsEditable=false,IsTextSearchEnabled=true};
    DatePicker Date()=>new(){SelectedDate=DateTime.Today,DisplayDateEnd=DateTime.Today,SelectedDateFormat=DatePickerFormat.Short};
    DateTime GetDate(DatePicker date)=>date.SelectedDate??throw new InvalidOperationException(T("اختر التاريخ","Choose a date"));
    Account GetAccount(ComboBox box)=>box.SelectedItem as Account??throw new InvalidOperationException(T("اختر الحساب","Choose an account"));
    Item GetItem(ComboBox box)=>box.SelectedItem as Item??throw new InvalidOperationException(T("اختر الصنف","Choose an item"));
    (Window Window,StackPanel Panel,Action Finish) Dialog(string title,double width=850,bool draft=true)
    {
        var w=new Window{Owner=this,Title=title,Width=width,Height=760,MaxHeight=SystemParameters.WorkArea.Height-35,MinHeight=400,WindowStartupLocation=WindowStartupLocation.CenterOwner,FlowDirection=FlowDirection,FontSize=FontSize,FontFamily=FontFamily,Foreground=ink,Background=Background,Resources=Resources};
        var p=new StackPanel{Margin=new(26)};p.Children.Add(Text(title,28,true));w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};bool done=false,dirty=false;
        p.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((s,e)=>dirty=true));p.AddHandler(SelectorSelectionChanged(),new SelectionChangedEventHandler((s,e)=>dirty=true));
        w.Closing+=(s,e)=>{if(draft&&dirty&&!done&&!Confirm(T("توجد بيانات لم تحفظ. هل تريد إغلاق الشاشة؟","You have unsaved information. Close this screen?")))e.Cancel=true;};
        w.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){w.Close();e.Handled=true;}};
        void Finish(){done=true;w.Close();}
        if(previewDirectory!=null)w.Loaded+=(s,e)=>w.Dispatcher.BeginInvoke(()=>{w.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);w.UpdateLayout();Capture(w,System.IO.Path.Combine(previewDirectory,(vm.Arabic?"ar":"en")+"-"+(previewCaptureName??("dialog-"+(++previewDialog)))+".png"));Finish();},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        return(w,p,Finish);
    }
    static RoutedEvent SelectorSelectionChanged()=>System.Windows.Controls.Primitives.Selector.SelectionChangedEvent;
    bool Confirm(string text)=>MessageBox.Show(this,text,T("مراجعة قبل الحفظ","Review before saving"),MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;
    void Saved(long id,Action finish){finish();vm.Status=T("تم الحفظ بنجاح. يمكنك فتح المستند وطباعته.","Saved successfully. Open the document to print it.");Navigate("home");ShowDocument(id);}
    public void VoucherForm(bool receipt)
    {
        var (w,p,finish)=Dialog(T(receipt?"سند قبض":"سند صرف",receipt?"Receive money":"Pay money"));
        p.Children.Add(Text(T("١. أدخل البيانات   ٢. راجع المبلغ   ٣. احفظ","1. Enter details   2. Review the amount   3. Save"),18));
        var date=Date();var party=Choose(UiAccounts().Where(a=>a.Kind!="Unclassified"));var cash=Choose(UiAccounts().Where(a=>a.Kind=="Cash"));cash.SelectedIndex=0;var amount=Input("",true);var note=Input();
        p.Children.Add(Field(T("التاريخ","Date"),date));p.Children.Add(Field(T(receipt?"المقبوض من / الحساب":"المصروف إلى / الحساب",receipt?"Received from / account":"Paid to / account"),party));p.Children.Add(Field(T(receipt?"أين استلمت المبلغ؟":"من أين دفعت المبلغ؟",receipt?"Where did you receive it?":"Where did you pay from?"),cash));p.Children.Add(Field(T("المبلغ بالدينار الأردني","Amount in Jordanian dinars"),amount));p.Children.Add(Field(T("البيان","Description"),note));
        var allocation=new ComboBox();void RefreshAllocations(){allocation.ItemsSource=new[]{new PositionChoice(null,T("دفعة على الحساب دون توزيع","Unallocated payment on account"))}.Concat(party.SelectedItem is Account a?A.Positions(DateTime.Today,a.Id,receipt?"SI":"PI").Where(i=>i.Outstanding>0).Select(i=>new PositionChoice(i.Invoice,i.Number+" · "+Store.Money(i.Outstanding)+" JOD")):Enumerable.Empty<PositionChoice>()).ToList();allocation.SelectedIndex=0;}party.SelectionChanged+=(s,e)=>RefreshAllocations();RefreshAllocations();p.Children.Add(Field(T("تخصيص لفاتورة (اختياري)","Allocate to invoice (optional)"),allocation));
        p.Children.Add(Btn(T("مراجعة وحفظ","Review & save"),()=>{var a=GetAccount(party);var c=GetAccount(cash);var value=Store.Parse(amount.Text);if(Confirm($"{w.Title}\n{a.Name}\n{Store.Money(Store.M(value))} JOD\n{c.Name}\n{GetDate(date):yyyy-MM-dd}"))Saved(SaveVoucherForm(receipt,GetDate(date),a.Id,c.Id,value,note.Text,(allocation.SelectedItem as PositionChoice)?.Id),finish);},true));w.ShowDialog();
    }
    public void InvoiceForm(bool sale,long? invoiceToEdit=null)
    {
        var (w,p,finish)=Dialog(invoiceToEdit==null?T(sale?"فاتورة مبيعات جديدة":"فاتورة مشتريات جديدة",sale?"New sales invoice":"New purchase invoice"):T("تعديل الفاتورة","Amend invoice")+" · "+S.Scalar("SELECT number FROM documents WHERE id=@p0",invoiceToEdit),1080);
        var date=Date();var party=Choose(UiAccounts().Where(a=>a.Kind==(sale?"Customer":"Supplier")));var cash=Choose(UiAccounts().Where(a=>a.Kind=="Cash"));cash.SelectedIndex=0;
        var headerRow=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};headerRow.Children.Add(Field(T("التاريخ","Date"),date));var partyField=Field(T(sale?"العميل":"المورد",sale?"Customer":"Supplier"),party);partyField.Margin=new(12,0,0,16);headerRow.Children.Add(partyField);p.Children.Add(headerRow);
        if(party.Items.Count==0)p.Children.Add(Text(T("صنّف حسابًا كعميل أو مورد من دليل الحسابات أولًا.","First classify an account as a customer or supplier in Accounts."),18));
        var lines=new ObservableCollection<InvoiceLine>();var list=new DataGrid{ItemsSource=lines,Height=190,MinHeight=150,FontSize=17};
        foreach(var col in new[]{("Description",T("الوصف","Description"),4.0),("Qty",T("الكمية","Qty"),1.0),("Price",T("السعر","Price"),1.1),("Discount",T("الخصم","Discount"),1.0)})list.Columns.Add(new DataGridTextColumn{Header=col.Item2,Binding=new Binding(col.Item1),Width=new DataGridLength(col.Item3,DataGridLengthUnitType.Star)});
        var item=Choose(UiItems());var qty=Input("1",true);var price=Input("0",true);var discount=Input("0",true);var description=Input();description.AcceptsReturn=true;description.TextWrapping=TextWrapping.Wrap;description.MinHeight=70;
        item.SelectionChanged+=(s,e)=>{if(item.SelectedItem is Item selected){price.Text=Store.D(selected.Price).ToString(CultureInfo.InvariantCulture);description.Text=selected.Name;}};
        var add=new StackPanel();add.Children.Add(Text(T("أضف الأصناف","Add items"),22,true));add.Children.Add(Field(T("الصنف","Item"),item));add.Children.Add(Field(T("الوصف على الفاتورة","Description printed on invoice"),description));
        var row=new System.Windows.Controls.Primitives.UniformGrid{Columns=3};foreach(var pair in new[]{(T("الكمية","Quantity"),qty),(T("سعر الوحدة JOD","Unit price JOD"),price),(T("خصم السطر JOD","Line discount JOD"),discount)}){var field=Field(pair.Item1,pair.Item2);field.Margin=new(0,0,12,10);row.Children.Add(field);}add.Children.Add(row);
        var vat=Input(S.Setting("vat","16"),true);var inclusive=new CheckBox{Content=T("الأسعار تشمل الضريبة","Prices include tax"),Margin=new(0,4,0,16)};var total=Text("",24,true);
        void Refresh(){var t=AccountingService.Calculate(lines,Store.Parse(vat.Text),inclusive.IsChecked==true);total.Text=T($"الصافي {Store.Money(t.Net)}  +  الضريبة {Store.Money(t.Tax)}  =  الإجمالي {Store.Money(t.Total)} JOD",$"Net {Store.Money(t.Net)}  +  Tax {Store.Money(t.Tax)}  =  Total {Store.Money(t.Total)} JOD");}
        Border? editorCard=null;InvoiceLine? editing=null;
        add.Children.Add(Btn(T("اعتماد السطر","Apply line"),()=>{var i=GetItem(item);var l=new InvoiceLine(i.Id,description.Text,Store.Parse(qty.Text),Store.Parse(price.Text),Store.Parse(discount.Text));AccountingService.Calculate([l],Store.Parse(vat.Text),inclusive.IsChecked==true);if(editing!=null&&lines.Contains(editing))lines[lines.IndexOf(editing)]=l;else lines.Add(l);editing=null;Refresh();editorCard!.Visibility=Visibility.Collapsed;},true));
        editorCard=Card(add);editorCard.Visibility=Visibility.Collapsed;p.Children.Add(Btn(T("+ إضافة صنف","+ Add item"),()=>{editing=null;qty.Text="1";discount.Text="0";editorCard.Visibility=Visibility.Visible;},true));p.Children.Add(editorCard);p.Children.Add(list);
        p.Children.Add(Actions(Btn(T("تعديل السطر المحدد","Edit selected line"),()=>{if(list.SelectedItem is not InvoiceLine l)throw new InvalidOperationException(T("اختر سطرًا أولًا","Select a line first"));editing=l;item.SelectedItem=item.Items.Cast<Item>().Single(i=>i.Id==l.Item);description.Text=l.Description;qty.Text=l.Qty.ToString(CultureInfo.InvariantCulture);price.Text=l.Price.ToString(CultureInfo.InvariantCulture);discount.Text=l.Discount.ToString(CultureInfo.InvariantCulture);editorCard.Visibility=Visibility.Visible;}),Btn(T("إزالة السطر المحدد","Remove selected line"),()=>{if(list.SelectedItem is InvoiceLine l){lines.Remove(l);Refresh();}})));
        vat.TextChanged+=(s,e)=>{try{Refresh();}catch{total.Text=T("تحقق من نسبة الضريبة","Check the tax rate");}};inclusive.Checked+=(s,e)=>Guard(Refresh);inclusive.Unchecked+=(s,e)=>Guard(Refresh);
        p.Children.Add(Field(T("نسبة الضريبة %","Tax rate %"),vat));p.Children.Add(inclusive);p.Children.Add(Btn(T("حساب الإجمالي","Calculate total"),Refresh));
        var paid=Input("0",true);var note=Input();var amendmentReason=Input();var due=new DatePicker{SelectedDate=DateTime.Today.AddDays(int.Parse(S.Setting("due_days","30")))};p.Children.Add(Field(T("المدفوع الآن JOD (٠ للآجل)","Paid now JOD (0 for credit)"),paid));p.Children.Add(Field(T("الصندوق / البنك للمدفوع الآن","Cash / bank for amount paid now"),cash));p.Children.Add(Field(T("ملاحظات الفاتورة","Invoice notes"),note));
        p.Children.Add(Field(T("تاريخ الاستحقاق","Due date"),due));if(invoiceToEdit!=null)p.Children.Add(Field(T("سبب التعديل","Amendment reason"),amendmentReason));
        p.Children.Add(Text(T("شروط الدفع والضمان وبيانات الشركة تؤخذ من الإعدادات وتُحفظ مع الفاتورة.","Payment terms, warranty and company details come from Settings and are saved with this invoice."),17));
        var footer=new StackPanel{Margin=new(26,12,26,12),Background=Background};footer.Children.Add(total);footer.Children.Add(Btn(T("مراجعة الفاتورة وحفظها","Review & save invoice"),()=>{Refresh();var a=GetAccount(party);decimal paidAmount=Store.Parse(paid.Text);var tt=AccountingService.Calculate(lines,Store.Parse(vat.Text),inclusive.IsChecked==true);var review=InvoiceReview(w.Title,a.Name,GetDate(date),lines.ToList(),tt,paidAmount,Store.Parse(vat.Text),inclusive.IsChecked==true,note.Text);if(Review(review))Saved(SaveInvoiceForm(invoiceToEdit,sale,GetDate(date),a.Id,paidAmount>0?GetAccount(cash).Id:null,paidAmount,Store.Parse(vat.Text),inclusive.IsChecked==true,note.Text,lines.ToList(),GetDate(due),amendmentReason.Text),finish);},true));
        var invoiceScroll=(ScrollViewer)w.Content;w.Content=null;var layout=new DockPanel();DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);layout.Children.Add(invoiceScroll);w.Content=layout;
        if(invoiceToEdit!=null){var existing=S.Table("SELECT * FROM documents WHERE id=@p0",invoiceToEdit).Rows[0];date.SelectedDate=DateTime.Parse((string)existing["date"]);party.SelectedItem=party.Items.Cast<Account>().Single(a=>a.Id==(long)existing["party"]);if(existing["cash"]!=DBNull.Value)cash.SelectedItem=cash.Items.Cast<Account>().Single(a=>a.Id==(long)existing["cash"]);paid.Text=Store.D((long)existing["paid"]).ToString(CultureInfo.InvariantCulture);vat.Text=(string)existing["vat"];inclusive.IsChecked=(long)existing["inclusive"]==1;note.Text=(string)existing["note"];due.SelectedDate=DateTime.Parse((string)existing["due_date"]);foreach(DataRow line in S.Table("SELECT * FROM invoice_lines WHERE doc=@p0 ORDER BY id",invoiceToEdit).Rows)lines.Add(new((long)line["item"],(string)line["description"],Store.D((long)line["qty"]),Store.D((long)line["price"]),Store.D((long)line["discount"])));}
        Refresh();w.ShowDialog();
    }
    public void JournalForm(long? journalToEdit=null)
    {
        var (w,p,finish)=Dialog(T("سند قيد جديد","New journal entry"),1000);var date=Date();var note=Input();p.Children.Add(Field(T("التاريخ","Date"),date));p.Children.Add(Field(T("البيان / سبب القيد","Description / reason"),note));
        var amendmentReason=Input();if(journalToEdit!=null)p.Children.Add(Field(T("سبب التعديل","Amendment reason"),amendmentReason));var lines=new ObservableCollection<JournalRow>();var account=Choose(UiAccounts().Where(a=>a.Kind!="Unclassified"));var debit=Input("0",true);var credit=Input("0",true);p.Children.Add(Field(T("الحساب","Account"),account));p.Children.Add(Field(T("مدين JOD","Debit JOD"),debit));p.Children.Add(Field(T("دائن JOD","Credit JOD"),credit));
        var grid=new DataGrid{ItemsSource=lines,Height=220};foreach(var c in new[]{("Name",T("الحساب","Account"),3.0),("DebitText",T("مدين","Debit"),1.0),("CreditText",T("دائن","Credit"),1.0)})grid.Columns.Add(new DataGridTextColumn{Header=c.Item2,Binding=new Binding(c.Item1),Width=new DataGridLength(c.Item3,DataGridLengthUnitType.Star)});
        var summary=Text("",22,true);void Refresh(){summary.Text=T("مجموع المدين: ","Total debit: ")+Store.Money(lines.Sum(l=>l.Debit))+T("  |  مجموع الدائن: ","  |  Total credit: ")+Store.Money(lines.Sum(l=>l.Credit));}
        p.Children.Add(Btn(T("إضافة حساب للقيد","Add account to journal"),()=>{var a=GetAccount(account);long d=Store.M(Store.Parse(debit.Text)),c=Store.M(Store.Parse(credit.Text));if(d<0||c<0||(d>0)==(c>0))throw new InvalidOperationException(T("أدخل مبلغًا موجبًا في المدين أو الدائن فقط","Enter a positive amount in debit or credit only"));lines.Add(new(a.Id,a.Name,d,c));Refresh();}));p.Children.Add(grid);p.Children.Add(Btn(T("إزالة السطر المحدد","Remove selected line"),()=>{if(grid.SelectedItem is JournalRow l)lines.Remove(l);Refresh();}));p.Children.Add(summary);
        p.Children.Add(Btn(T("مراجعة وحفظ القيد","Review & save journal"),()=>{if(lines.Count<2||lines.Sum(l=>l.Debit)!=lines.Sum(l=>l.Credit))throw new InvalidOperationException(T("مجموع المدين يجب أن يساوي مجموع الدائن","Total debit must equal total credit"));if(Confirm(summary.Text+"\n"+string.Join("\n",lines.Select(l=>$"{l.Name}: {l.DebitText} / {l.CreditText}"))))Saved(journalToEdit==null?A.Manual(GetDate(date),note.Text,lines.Select(l=>new EntryLine(l.Account,l.Debit,l.Credit)).ToList()):A.AmendSimple(journalToEdit.Value,GetDate(date),null,null,0,note.Text,lines.Select(l=>new EntryLine(l.Account,l.Debit,l.Credit)).ToList(),amendmentReason.Text),finish);},true));if(journalToEdit!=null){var original=S.Table("SELECT * FROM documents WHERE id=@p0",journalToEdit).Rows[0];date.SelectedDate=DateTime.Parse((string)original["date"]);note.Text=(string)original["note"];foreach(DataRow line in S.Table("SELECT * FROM entries WHERE doc=@p0",journalToEdit).Rows)lines.Add(new((long)line["account"],S.LocalName("accounts",(long)line["account"],vm.Arabic),(long)line["debit"],(long)line["credit"]));Refresh();}w.ShowDialog();
    }
    public record JournalRow(long Account,string Name,long Debit,long Credit){public string DebitText=>Store.Money(Debit);public string CreditText=>Store.Money(Credit);}
    void AccountsPage()
    {
        Heading(T("دليل الحسابات","Accounts"),T("صنّف الأسماء المستوردة قبل استخدامها. الأرصدة تأتي من القيود والسندات.","Classify imported names before use. Balances come from posted journals and vouchers."));
        var search=Input();body.Children.Add(Field(T("بحث بالاسم أو الكود","Search name or code"),search));
        DataTable Rows(){var t=new DataTable();foreach(var col in new[]{"id","code","name","type","balance"})t.Columns.Add(col,col=="id"?typeof(long):typeof(string));foreach(var a in UiAccounts().Where(a=>(a.Name+a.Code).Contains(search.Text,StringComparison.OrdinalIgnoreCase)))t.Rows.Add(a.Id,a.Code,a.Name,AccountKind(a.Kind),Store.Money(S.Balance(a.Id)));return t;}
        var grid=Grid(Rows(),("code",T("الكود","Code"),1),("name",T("الاسم","Name"),3),("type",T("النوع","Type"),1.7),("balance",T("الرصيد مدين − دائن","Balance debit − credit"),1.8));search.TextChanged+=(s,e)=>grid.ItemsSource=Rows().DefaultView;
        body.Children.Add(Actions(Btn(T("+ حساب جديد","+ New account"),()=>AccountForm(null),true),Btn(T("تعديل الحساب المحدد","Edit selected account"),()=>AccountForm(UiAccounts().Single(a=>a.Id==Selected(grid))))));body.Children.Add(Btn(T("الأسماء والعناوين باللغتين","Bilingual names & addresses"),()=>BilingualForm("accounts",Selected(grid))));body.Children.Add(grid);grid.MouseDoubleClick+=(s,e)=>Guard(()=>AccountForm(UiAccounts().Single(a=>a.Id==Selected(grid))));
        body.Children.Add(Text(T("للأرصدة الافتتاحية: استخدم سند قيد مقابل حساب «رصيد افتتاحي». أدخل المخزون من شاشة الأصناف.","For opening account balances, use a journal against Opening balance. Enter opening stock in Items & stock."),18));
    }
    void AccountForm(Account? a)
    {
        var (w,p,finish)=Dialog(T(a==null?"حساب جديد":"تعديل الحساب",a==null?"New account":"Edit account"),700);var code=Input(a?.Code??"");var name=Input(a?.Name??"");var kinds=new[]{"Unclassified","Customer","Supplier","Cash","Asset","Liability","Equity","Income","Expense"};var type=new ComboBox{ItemsSource=kinds.Select(k=>new Choice(k,AccountKind(k))).ToList(),DisplayMemberPath="Label",SelectedValuePath="Key",SelectedValue=a?.Kind??"Unclassified"};
        p.Children.Add(Field(T("كود الحساب","Account code"),code));p.Children.Add(Field(T("اسم الحساب","Account name"),name));p.Children.Add(Field(T("نوع الحساب","Account type"),type));p.Children.Add(Btn(T("حفظ الحساب","Save account"),()=>{S.SaveAccount(a?.Id,code.Text,name.Text,type.SelectedValue?.ToString()??"Unclassified");finish();Navigate("accounts");},true));w.ShowDialog();
    }
    public record Choice(string Key,string Label);
    void ItemsPage()
    {
        Heading(T("الأصناف والمخزون","Items & stock"),T("أسعار البيع والكميات تبدأ بصفر. اختر مخزنيًا أو خدمة لكل صنف.","Selling prices and quantities start at zero. Choose stock item or service for each item."));
        var search=Input();body.Children.Add(Field(T("بحث بالاسم أو الكود","Search name or code"),search));
        DataTable Rows(){var t=new DataTable();foreach(var c in new[]{"id","code","name","type","price","qty","avg"})t.Columns.Add(c,c=="id"?typeof(long):typeof(string));foreach(var i in UiItems().Where(i=>(i.Name+i.Code).Contains(search.Text,StringComparison.OrdinalIgnoreCase)))t.Rows.Add(i.Id,i.Code,i.Name,i.Stock?T("مخزني","Stock"):T("خدمة","Service"),Store.Money(i.Price),Store.D(i.Qty).ToString("0.###"),i.Qty==0?"0.000":Store.Money(Store.M(Store.D(i.Value)*1000/i.Qty)));return t;}
        var grid=Grid(Rows(),("code",T("الكود","Code"),1),("name",T("الصنف","Item"),3),("type",T("النوع","Type"),1),("price",T("سعر البيع","Sale price"),1.2),("qty",T("المتاح","Available"),1),("avg",T("متوسط التكلفة","Avg cost"),1.3));search.TextChanged+=(s,e)=>grid.ItemsSource=Rows().DefaultView;
        body.Children.Add(Actions(Btn(T("+ صنف جديد","+ New item"),()=>ItemForm(null),true),Btn(T("تعديل المحدد","Edit selected"),()=>ItemForm(UiItems().Single(i=>i.Id==Selected(grid)))),Btn(T("رصيد مخزون افتتاحي","Opening stock"),()=>OpeningForm(UiItems().Single(i=>i.Id==Selected(grid)))),Btn(T("الأسماء باللغتين","Bilingual names"),()=>BilingualForm("items",Selected(grid))),Btn(T("تعديل تكلفة المخزون","Adjust stock cost"),()=>StockRevaluationForm(Selected(grid)))));body.Children.Add(grid);
        body.Children.Add(Text(T("الكميات تتحدث من الفواتير. يعاد احتساب التكلفة عند التعديل أو التأريخ السابق. يمكن تفعيل المخزون السالب من الإعدادات.","Invoices update stock. Costs recalculate for amendments and backdated documents. Negative stock can be enabled in Settings."),18));
    }
    void ItemForm(Item? i)
    {
        var (w,p,finish)=Dialog(T(i==null?"صنف جديد":"تعديل الصنف",i==null?"New item":"Edit item"),750);var code=Input(i?.Code??"");var name=Input(i?.Name??"");var price=Input(Store.D(i?.Price??0).ToString(CultureInfo.InvariantCulture),true);var stock=new CheckBox{Content=T("صنف مخزني (له كمية وتكلفة)","Stock item (tracks quantity and cost)"),IsChecked=i?.Stock??true,Margin=new(0,12,0,22)};
        p.Children.Add(Field(T("الكود","Code"),code));p.Children.Add(Field(T("الاسم والوصف","Name and description"),name));p.Children.Add(Field(T("سعر البيع الافتراضي JOD","Default selling price JOD"),price));p.Children.Add(stock);p.Children.Add(Text(T("ألغِ الخيار للخدمات. الأصناف المستوردة تحتاج مراجعة النوع، خصوصًا التركيب والتدريس.","Uncheck for services. Review imported item types, especially installation and teaching."),18));p.Children.Add(Btn(T("حفظ الصنف","Save item"),()=>{S.SaveItem(i?.Id,code.Text,name.Text,stock.IsChecked==true,Store.Parse(price.Text));finish();Navigate("items");},true));w.ShowDialog();
    }
    void OpeningForm(Item i)
    {
        var (w,p,finish)=Dialog(T("مخزون افتتاحي","Opening stock"),700);p.Children.Add(Text(i.Name,22,true));var date=Date();var qty=Input("",true);var cost=Input("",true);p.Children.Add(Field(T("التاريخ","Date"),date));p.Children.Add(Field(T("الكمية","Quantity"),qty));p.Children.Add(Field(T("تكلفة الوحدة JOD","Unit cost JOD"),cost));p.Children.Add(Btn(T("مراجعة وحفظ","Review & save"),()=>{var q=Store.Parse(qty.Text);var c=Store.Parse(cost.Text);if(Confirm(i.Name+"\n"+q+" × "+c+" = "+Store.Money(Store.M(q*c))+" JOD"))Saved(A.OpeningStock(i.Id,GetDate(date),q,c),finish);},true));w.ShowDialog();
    }
    void Settings()
    {
        Heading(T("الإعدادات","Settings"),T("اختر اللغة وحجم الخط، وأكمل بيانات الشركة للطباعة.","Choose language and text size, and complete company details for printing."));
        PersonalProtectionSettings(); body.Children.Add(Btn(T("فحص الجهاز والطباعة","Machine & printer check"),MachineCheck)); var lang=new ComboBox{ItemsSource=new[]{new Choice("ar","العربية"),new Choice("en","English")},DisplayMemberPath="Label",SelectedValuePath="Key",SelectedValue=S.Setting("language","ar")};
        var font=new ComboBox{ItemsSource=new[]{new Choice("18",T("كبير — 18","Large — 18")),new Choice("20",T("أكبر — 20","Larger — 20")),new Choice("24",T("كبير جدًا — 24","Extra large — 24"))},DisplayMemberPath="Label",SelectedValuePath="Key",SelectedValue=S.Setting("font","20")};body.Children.Add(Field(T("لغة الواجهة","Interface language"),lang));body.Children.Add(Field(T("حجم الخط","Text size"),font));
        if(S.User.Role!="Admin"){body.Children.Add(Btn(T("حفظ التفضيلات","Save preferences"),()=>{using var tx=S.BeginTransaction();S.Set("language",lang.SelectedValue?.ToString()??"ar");S.Set("font",font.SelectedValue?.ToString()??"20");tx.Commit();BuildShell();},true));return;}var fields=new Dictionary<string,TextBox>();foreach(var entry in new[]{("company",T("اسم الشركة","Company name")),("vat",T("نسبة الضريبة الافتراضية %","Default tax rate %")),("terms",T("شروط الدفع","Payment terms")),("warranty",T("الضمان","Warranty")),("shipping",T("الشحن والتركيب","Shipping and installation")),("bank",T("بيانات التحويل البنكي للطباعة","Bank payment details for printing"))}){var box=Input(S.Setting(entry.Item1),entry.Item1=="vat");if(entry.Item1!="vat"){box.AcceptsReturn=true;box.TextWrapping=TextWrapping.Wrap;box.MinHeight=65;}fields[entry.Item1]=box;body.Children.Add(Field(entry.Item2,box));}
        body.Children.Add(Btn(T("حفظ الإعدادات","Save settings"),()=>{decimal vat=Store.Parse(fields["vat"].Text);if(vat<0||vat>100)throw new InvalidOperationException(T("الضريبة من 0 إلى 100","Tax must be 0–100"));using var tx=S.BeginTransaction();foreach(var f in fields)S.Set(f.Key,f.Value.Text);S.Set("language",lang.SelectedValue?.ToString()??"ar");S.Set("font",font.SelectedValue?.ToString()??"20");tx.Commit();vm.Status=T("تم حفظ الإعدادات","Settings saved");BuildShell();},true));
        if(S.User.Role=="Admin")BrandingSettings(body);
    }
    void BackupsPage()
    {
        Heading(T("النسخ الاحتياطي","Backups"),T("احتفظ بنسخة على USB أو قرص آخر لنقل بياناتك أو استعادتها.","Keep a copy on a USB drive or another disk to transfer or restore your data."));
        var p=new StackPanel();p.Children.Add(Text(T("نسخة من جميع الحسابات والمستندات","A copy of all accounts and documents"),23,true));p.Children.Add(Text(T("تُنشأ أيضًا نسخة يومية عند إغلاق البرنامج. النسخة على نفس الجهاز لا تحمي من تلف القرص.","A daily backup is also created when the app closes. A copy on the same PC cannot protect against disk failure."),18));p.Children.Add(Actions(Btn(T("حفظ نسخة احتياطية…","Save backup…"),Backup,true),Btn(T("استعادة نسخة…","Restore backup…"),()=>{PortableRestore();})));body.Children.Add(Card(p));
        body.Children.Add(Text(T("مكان حفظ البيانات","Data location"),22,true));var path=new TextBox{Text=S.Path,IsReadOnly=true,FlowDirection=FlowDirection.LeftToRight,TextWrapping=TextWrapping.Wrap};body.Children.Add(path);
    }
    void Backup()
    {
        S.Require("backup");var dialog=new SaveFileDialog{Filter="Encrypted Hisab backup (*.hisab)|*.hisab",FileName="Hisab-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".hisab"};if(dialog.ShowDialog(this)==true){var password=PasswordPrompt(T("حماية النسخة الاحتياطية بكلمة مرور","Protect backup with password"),true);if(password==null)return;S.PortableBackup(dialog.FileName,password);vm.Status=T("تم حفظ النسخة المشفرة","Encrypted backup saved");}
    }
    void Help()
    {
        var (w,p,_)=Dialog(T("مساعدة سريعة","Quick help"),850,false);
        foreach(var pair in new[]{(T("١. جهّز الحسابات والأصناف","1. Prepare accounts and items"),T("صنّف الحسابات المستوردة وأدخل الأرصدة الافتتاحية. حدّد الأصناف المخزنية والخدمات.","Classify imported accounts and enter opening balances. Mark stock items and services.")),(T("٢. سجّل عملية واحدة","2. Record one transaction"),T("استخدم فاتورة للبيع أو الشراء، وسند قبض للتحصيل وسند صرف للدفع. لا تعِد تسجيل قيد العملية يدويًا.","Use an invoice for a sale or purchase, a receipt for money received, and a payment for money paid. Do not manually post the same transaction again.")),(T("٣. راجع قبل الحفظ","3. Review before saving"),T("تحقق من الاسم والتاريخ والمبلغ. يمكن فتح المستند المحفوظ من شاشة الفواتير والمستندات.","Check name, date and amount. Open saved documents from Invoices & documents.")),(T("٤. صحّح واحتفظ بنسخة","4. Correct and back up"),T("للتصحيح اختر تعديل المستند واكتب السبب. احتفظ بنسخة احتياطية على قرص خارجي.","To correct, select Amend document and enter the reason. Keep a backup on an external drive."))}){p.Children.Add(Text(pair.Item1,22,true));p.Children.Add(Text(pair.Item2,19));}
        p.Children.Add(Text(T("اختصارات: F1 مساعدة · Ctrl+N فاتورة مبيعات · Ctrl+B نسخة احتياطية · Esc إغلاق الشاشة","Shortcuts: F1 help · Ctrl+N sales invoice · Ctrl+B backup · Esc close dialog"),18));p.Children.Add(Btn(T("إغلاق","Close"),()=>w.Close()));w.ShowDialog();
    }
    public void RenderPreviews(string path)
    {
        Directory.CreateDirectory(path);previewDirectory=path;
        WelcomeWindow.Show(S,this,System.IO.Path.Combine(path,"welcome.png"));LoginWindow.Show(S,this,System.IO.Path.Combine(path,"sign-in.png"),false);
        foreach(var lang in new[]{"ar","en"}) {
            S.Set("language",lang); BuildShell();previewDialog=0;
            foreach(var route in new[]{"home","accounts","items","reports","backup","settings","invoices","cheques","periods","users"}) {
                Navigate(route);Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();Capture(this,System.IO.Path.Combine(path,lang+"-"+route+".png"));
            }
            VoucherForm(true);VoucherForm(false);InvoiceForm(true);JournalForm();AccountForm(null);ItemForm(null);ChequeForm(true);UserForm(null);Navigate("reports");Reports("aging");Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();Capture(this,System.IO.Path.Combine(path,lang+"-aging.png"));Reports("tax");Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();Capture(this,System.IO.Path.Combine(path,lang+"-tax.png"));ShowDocumentIfAvailable();if(Convert.ToInt64(S.Scalar("SELECT count(*) FROM documents WHERE kind=\u0027SI\u0027"))>0){long invoice=Convert.ToInt64(S.Scalar("SELECT max(id) FROM documents WHERE kind=\u0027SI\u0027"));Settlements(invoice);ReturnForm(invoice);Attachments(invoice);DocumentTranslations(invoice);InvoiceForm(true,invoice);}
            ExtraPreviews(path,lang);
        }
        previewDirectory=null;
    }
    void ShowDocumentIfAvailable(){if(Convert.ToInt64(S.Scalar("SELECT count(*) FROM documents"))>0)ShowDocument(Convert.ToInt64(S.Scalar("SELECT max(id) FROM documents WHERE kind='SI'")));}
    static void Capture(Window window,string path){var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var f=File.Create(path);encoder.Save(f);}
}




