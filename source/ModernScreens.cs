using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Path=System.Windows.Shapes.Path;
namespace Hisab;
public sealed partial class MainWindow
{
    Border? modernRail;
    System.Windows.Controls.Primitives.UniformGrid? homeTasks,homeOverview;
    void ApplyResponsiveLayout(){double width=ActualWidth>0?ActualWidth:Width;if(modernRail!=null)modernRail.Width=width<1000?210:(FontSize>24?280:240);if(homeTasks!=null)homeTasks.Columns=width<1000?1:2;if(homeOverview!=null)homeOverview.Columns=width<850?1:3;}
    Path UiIcon(string key,double size=24)
    {
        string data=key switch{
            "home"=>"M 2 11 L 12 3 L 22 11 M 5 9 L 5 22 L 10 22 L 10 15 L 15 15 L 15 22 L 20 22 L 20 9",
            "invoices"=>"M 5 2 L 15 2 L 21 8 L 21 22 L 5 22 Z M 15 2 L 15 8 L 21 8 M 9 12 L 17 12 M 9 17 L 17 17",
            "accounts"=>"M 15 7 A 4 4 0 1 1 7 7 A 4 4 0 1 1 15 7 M 3 22 L 3 19 Q 3 13 11 13 Q 19 13 19 19 L 19 22",
            "items"=>"M 2 7 L 12 2 L 22 7 L 12 12 Z M 2 7 L 2 19 L 12 24 L 22 19 L 22 7 M 12 12 L 12 24",
            "reports"=>"M 3 2 L 3 22 L 23 22 M 8 18 L 8 12 M 14 18 L 14 8 M 20 18 L 20 3",
            "backup"=>"M 3 9 A 9 9 0 1 1 4 19 M 3 3 L 3 9 L 9 9 M 12 6 L 12 13 L 17 16",
            "settings" or "tools"=>"M 3 6 L 21 6 M 3 12 L 21 12 M 3 18 L 21 18 M 8 3 L 8 9 M 17 9 L 17 15 M 11 15 L 11 21",
            "receive"=>"M 3 8 L 3 21 L 21 21 L 21 8 M 12 2 L 12 15 M 7 10 L 12 15 L 17 10",
            "pay"=>"M 3 8 L 3 21 L 21 21 L 21 8 M 12 15 L 12 2 M 7 7 L 12 2 L 17 7",
            _=>"M 12 2 A 10 10 0 1 1 12 22 A 10 10 0 1 1 12 2 M 12 16 L 12 19 M 8 8 Q 8 4 12 4 Q 17 4 17 9 Q 17 12 12 13 L 12 15"};
        var path=new Path{Data=Geometry.Parse(data),StrokeThickness=1.8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=size,Height=size,Stretch=Stretch.Uniform,IsHitTestVisible=false};path.SetBinding(Shape.StrokeProperty,new Binding("Foreground"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(Button),1)});return path;
    }
    Button NavButton(string route,string label)
    {
        var button=Btn(label,()=>Navigate(route));button.FontSize=Math.Max(14,FontSize*.85);button.Padding=new(12,9,12,9);button.Margin=new(0,0,0,5);button.BorderThickness=new(0);button.Background=Brushes.Transparent;button.Foreground=UiTheme.Brush(Resources,"Brush.NavText");button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        var content=new DockPanel();var icon=UiIcon(route,20);icon.Margin=new(0,0,12,0);DockPanel.SetDock(icon,Dock.Left);content.Children.Add(icon);content.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap});button.Content=content;navigation[route]=button;return button;
    }
    void BuildModernShell()
    {
        FontSize=double.TryParse(S.Setting("font","20"),NumberStyles.Number,CultureInfo.InvariantCulture,out double size)&&double.IsFinite(size)?Math.Clamp(size,14,28):20;FlowDirection=vm.Arabic?FlowDirection.RightToLeft:FlowDirection.LeftToRight; Language=System.Windows.Markup.XmlLanguage.GetLanguage(vm.Arabic?"ar-JO":"en-GB");
        Resources["Control.Height"]=Math.Max(36,FontSize*2.2);shell=new DockPanel();Content=shell;navigation.Clear();var navRoot=new DockPanel{Margin=new(0)};var rail=new Border{Width=240,BorderBrush=UiTheme.Brush(Resources,"Brush.Divider"),BorderThickness=new Thickness(0,0,1,0),Background=UiTheme.Brush(Resources,"Brush.Nav"),Child=navRoot};modernRail=rail;DockPanel.SetDock(rail,Dock.Left);shell.Children.Add(rail);
        var brand=new StackPanel{Margin=new(24,24,24,28)};brand.Children.Add(new TextBlock{Text=T("حساب","Hisab"),FontSize=28*FontSize/20,FontWeight=FontWeights.SemiBold,Foreground=UiTheme.Brush(Resources,"Brush.NavText")});brand.Children.Add(new TextBlock{Text=T("دفتر الحسابات","Your everyday ledger"),FontSize=Math.Max(14,17*FontSize/20),Foreground=UiTheme.Brush(Resources,"Brush.NavMuted"),Margin=new(0,4,0,0)});DockPanel.SetDock(brand,Dock.Top);navRoot.Children.Add(brand);
        var bottom=new StackPanel{Margin=new(16,12,16,18)};bottom.Children.Add(NavButton("settings",T("الإعدادات","Settings")));var help=Btn(T("كيف أستخدم الدفتر؟","How do I use this?"),Help);help.Background=Brushes.Transparent;help.BorderBrush=UiTheme.Brush(Resources,"Brush.NavMuted");help.Foreground=UiTheme.Brush(Resources,"Brush.NavText");help.FontSize=18*FontSize/20;help.Margin=new(0);bottom.Children.Add(help);if(S.Setting("auth_mode","") is "lock" or "login"){var signout=Btn(T(S.PersonalMode?"قفل الدفتر":"تبديل المستخدم",S.PersonalMode?"Lock ledger":"Switch user"),()=>{if(LoginWindow.Show(S,this)){vm.Route="home";BuildShell();}else Close();});signout.Background=Brushes.Transparent;signout.BorderThickness=new(0);signout.Foreground=UiTheme.Brush(Resources,"Brush.NavMuted");signout.Margin=new(0,8,0,0);bottom.Children.Add(signout);}DockPanel.SetDock(bottom,Dock.Bottom);navRoot.Children.Add(bottom);
        var nav=new StackPanel{Margin=new(16,0,16,0)};foreach(var r in new[]{("home","الرئيسية","Home"),("invoices","الفواتير والمستندات","Documents"),("accounts","دليل الحسابات","Accounts"),("items","الأصناف والمخزون","Items & stock"),("reports","التقارير","Reports"),("backup","النسخ الاحتياطي","Backups"),("tools","أدوات إضافية","More tools")})nav.Children.Add(NavButton(r.Item1,T(r.Item2,r.Item3)));navRoot.Children.Add(new ScrollViewer{Content=nav,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        var workspace=new DockPanel();shell.Children.Add(workspace);var bar=new DockPanel{Margin=new(32,18,32,14)};var language=Btn(vm.Arabic?"English":"العربية",()=>{S.Set("language",vm.Arabic?"en":"ar");BuildShell();});language.Margin=new(0);language.FontSize=Math.Max(14,17*FontSize/20);language.MinHeight=42;language.Padding=new(14,8,14,8);DockPanel.SetDock(language,Dock.Right);bar.Children.Add(language);var title=new StackPanel();title.Children.Add(new TextBlock{Text=S.Setting("company",T("دفتر الحسابات","My ledger")),FontWeight=FontWeights.SemiBold,FontSize=20*FontSize/20,TextWrapping=TextWrapping.Wrap});title.Children.Add(new TextBlock{Text=DateTime.Today.ToString("dddd، d MMMM yyyy",CultureInfo.GetCultureInfo(vm.Arabic?"ar-JO":"en-GB")),FontSize=16*FontSize/20,Foreground=muted,Margin=new(0,4,0,0)});bar.Children.Add(title);DockPanel.SetDock(bar,Dock.Top);workspace.Children.Add(bar);
        var foot=new TextBlock{Foreground=accent,FontSize=Math.Max(14,17*FontSize/20),Margin=new(32,8,32,16),TextWrapping=TextWrapping.Wrap};foot.SetBinding(TextBlock.TextProperty,new Binding(nameof(vm.Status)));DockPanel.SetDock(foot,Dock.Bottom);workspace.Children.Add(foot);
        body=new StackPanel{Margin=new(32,12,32,24)};workspace.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});Navigate(vm.Route);ApplyResponsiveLayout();
    }
    Button TaskButton(string title,string hint,string icon,Action action,bool primary=false)
    {
        var button=Btn(title,action,primary);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new(20,16,20,16);button.Margin=new(0,0,14,14);button.MinHeight=Math.Max(88,FontSize*4.4);
        var panel=new DockPanel();var mark=UiIcon(icon,24);mark.Margin=new(0,0,20,0);DockPanel.SetDock(mark,Dock.Left);panel.Children.Add(mark);var words=new StackPanel();words.Children.Add(new TextBlock{Text=title,FontSize=22*FontSize/20,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});words.Children.Add(new TextBlock{Text=hint,FontSize=Math.Max(14,17*FontSize/20),TextWrapping=TextWrapping.Wrap,Margin=new(0,6,0,0),Opacity=primary?0.95:0.9});panel.Children.Add(words);button.Content=panel;return button;
    }
    void ModernHome()
    {
        Heading(T("ماذا تريد أن تسجّل اليوم؟","What would you like to record?"),T("اختر العملية، ثم راجعها قبل الحفظ.","Choose a task, then review it before saving."));
        var tasks=new System.Windows.Controls.Primitives.UniformGrid{Columns=2,Margin=new(0,12,0,14)};homeTasks=tasks;tasks.Children.Add(TaskButton(T("فاتورة بيع","Sell something"),T("سجّل ما بعته للعميل","Create a sales invoice"),"invoices",()=>InvoiceForm(true),true));tasks.Children.Add(TaskButton(T("استلام مبلغ","Receive money"),T("دفعة وصلتك من عميل","Record a collection"),"receive",()=>VoucherForm(true)));tasks.Children.Add(TaskButton(T("دفع مبلغ","Pay money"),T("مبلغ دفعته لمورد أو مصروف","Record a payment"),"pay",()=>VoucherForm(false)));tasks.Children.Add(TaskButton(T("فاتورة شراء","Buy something"),T("سجّل ما اشتريته من المورد","Create a purchase invoice"),"items",()=>InvoiceForm(false)));body.Children.Add(tasks);
        var overview=new System.Windows.Controls.Primitives.UniformGrid{Columns=3,Margin=new(0,0,0,16)};homeOverview=overview;foreach(var entry in new[]{("1101",T("الصندوق","Cash")),("1102",T("البنك","Bank")),("4101",T("رصيد حساب المبيعات","Sales account balance"))}){long balance=S.Balance(S.AccountId(entry.Item1));if(entry.Item1=="4101")balance=-balance;var stack=new StackPanel();var label=Text(entry.Item2,17);label.Foreground=muted;stack.Children.Add(label);stack.Children.Add(Text(Store.Money(balance),28,true));stack.Children.Add(Text(T("دينار أردني","JOD"),16));var card=Card(stack);card.Margin=new(0,0,14,0);card.Padding=new(22,18,22,10);overview.Children.Add(card);}body.Children.Add(overview);ApplyResponsiveLayout();
        if(Convert.ToInt64(S.Scalar("SELECT count(*) FROM accounts WHERE kind='Customer'"))==0){var setup=new StackPanel();setup.Children.Add(Text(T("قبل أول فاتورة: جهّز عميلًا وصنفًا","Before your first invoice: prepare a customer and an item"),22,true));setup.Children.Add(Text(T("الأسماء موجودة بالفعل. ابدأ بعميل واحد، وراجع سعر الصنف وكمّيته.","Your names are already here. Start with one customer, then check the item's price and quantity."),18));setup.Children.Add(Actions(Btn(T("١. اختر العميل","1. Choose customer"),()=>Navigate("accounts")),Btn(T("٢. راجع الصنف","2. Review item"),()=>Navigate("items"))));body.Children.Add(Card(setup));}
        body.Children.Add(Text(T("آخر ما سجّلته","Recent activity"),24,true));var rows=DocumentTable("",8);if(rows.Rows.Count==0){var empty=new StackPanel();empty.Children.Add(Text(T("لا توجد عمليات حتى الآن","No transactions yet"),21,true));empty.Children.Add(Text(T("ستظهر فواتيرك ودفعاتك هنا بعد حفظها.","Your invoices and payments will appear here after saving."),18));body.Children.Add(Card(empty));}else{var grid=DocumentGrid(rows);grid.MaxHeight=300;body.Children.Add(grid);grid.MouseDoubleClick+=(s,e)=>Guard(()=>OpenSelected(grid));body.Children.Add(Btn(T("فتح المستند المحدد","Open selected document"),()=>OpenSelected(grid)));}
    }
    void ToolsPage()
    {
        Heading(T("أدوات إضافية","More tools"),T("كل ما تحتاجه للتفاصيل المحاسبية، في مكان واحد.","Accounting tools in one place."));var tools=new WrapPanel();foreach(var tool in new[]{("cheques","الشيكات","Cheques","مواعيد الشيكات والتحصيل والارتجاع","Due dates, clearing and bouncing"),("journal","سند قيد","Journal entry","الأرصدة الافتتاحية والقيود المحاسبية","Opening balances and journal entries"),("periods","إقفال الفترة","Period closing","إقفال السنة وإعادة فتحها","Close and reopen a financial period"),("users","حسابات مشتركة","Shared accounts","اختياري عند استخدام أكثر من شخص","Optional, for more than one person")}){if(S.User.Role!="Admin"&&tool.Item1 is "periods" or "users")continue;var b=TaskButton(T(tool.Item2,tool.Item3),T(tool.Item4,tool.Item5),"tools",()=>Navigate(tool.Item1));b.Width=420;tools.Children.Add(b);}body.Children.Add(tools);
    }
    void PersonalProtectionSettings()
    {
        if(S.User.Role!="Admin")return;var panel=new StackPanel();panel.Children.Add(Text(T("طريقة فتح الدفتر","Opening your ledger"),24,true));panel.Children.Add(Text(T("الفتح المباشر مناسب لجهازك الشخصي. اختر رمزًا فقط إذا أردت قفل الدفتر.","Direct opening suits your personal PC. Add a code only if you want to lock the ledger."),18));panel.Children.Add(Actions(Btn(T("فتح مباشر دون رمز","Open without a code"),()=>{S.EnablePersonalMode();BuildShell();vm.Status=T("سيفتح الدفتر مباشرة في المرة القادمة.","The ledger will open directly next time.");}),Btn(T("إضافة أو تغيير رمز الدخول","Add or change unlock code"),()=>{var (w,p,finish)=Dialog(T("رمز دخول اختياري","Optional unlock code"),700);var code=new PasswordBox();var again=new PasswordBox();p.Children.Add(Field(T("رمز من ٤ أحرف أو أرقام على الأقل","Code, at least 4 characters or digits"),code));p.Children.Add(Field(T("أعد كتابة الرمز","Repeat the code"),again));p.Children.Add(Btn(T("احفظ رمز الدخول","Save unlock code"),()=>{if(code.Password!=again.Password)throw new InvalidOperationException(T("الرمزان غير متطابقين.","The codes do not match."));S.SetPersonalLock(code.Password);finish();BuildShell();},true));w.ShowDialog();})));body.Children.Add(Card(panel));
    }
}
