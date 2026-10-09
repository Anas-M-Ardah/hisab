using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Hisab;
public static class WelcomeWindow
{
    public static bool Show(Store store,Window? owner=null,string? previewFile=null)
    {
        bool accepted=false;bool arabic=store.Setting("language","ar")=="ar";string T(string ar,string en)=>arabic?ar:en;
        var resources=UiTheme.Create();var window=new Window{Title=T("أهلًا بك في حساب","Welcome to Hisab"),Width=760,Height=780,MaxHeight=SystemParameters.WorkArea.Height-30,MaxWidth=SystemParameters.WorkArea.Width-30,MinHeight=480,Owner=owner,WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner,FontFamily=new FontFamily("Segoe UI"),FontSize=20,FlowDirection=arabic?FlowDirection.RightToLeft:FlowDirection.LeftToRight,Resources=resources,Background=UiTheme.Brush(resources,"Brush.Window"),Foreground=UiTheme.Brush(resources,"Brush.Text")};
        var root=new DockPanel();window.Content=root;var content=new StackPanel{Margin=new(38,28,38,24)};var footer=new StackPanel{Margin=new(38,14,38,24)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);root.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        TextBlock Text(string text,double size,bool bold=false)=>new(){Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,14)};
        content.Children.Add(Text(T("حساب","Hisab"),32,true));content.Children.Add(Text(T("دفترك جاهز. لنبدأ ببساطة.","Your ledger is ready. Let's begin."),34,true));var intro=Text(T("لا تحتاج إلى حساب أو كلمة مرور. سيُفتح البرنامج مباشرة على جهازك.","No account or password needed. The app opens directly on your PC."),20);intro.Foreground=UiTheme.Brush(resources,"Brush.Secondary");content.Children.Add(intro);
        var form=new StackPanel{Margin=new(24)};content.Children.Add(new Border{Background=UiTheme.Brush(resources,"Brush.Surface"),CornerRadius=new(16),BorderBrush=UiTheme.Brush(resources,"Brush.Divider"),BorderThickness=new(1),Child=form,Margin=new(0,14,0,8)});
        form.Children.Add(Text(T("اسم المحل على الفواتير","Name on your invoices"),21,true));var company=new TextBox{Text=store.Setting("company"),Margin=new(0,0,0,12)};form.Children.Add(company);System.Windows.Automation.AutomationProperties.SetName(company,T("اسم المحل، اختياري","Business name, optional"));form.Children.Add(Text(T("اختياري. يمكنك تركه فارغًا وتغييره لاحقًا.","Optional. Leave blank and change it later."),17));
        form.Children.Add(Text(T("حجم الكتابة","Text size"),21,true));var sizes=new ComboBox{ItemsSource=new[]{new MainWindow.Choice("14",T("صغير — 14","Small — 14")),new MainWindow.Choice("16",T("متوسط — 16","Medium — 16")),new MainWindow.Choice("18",T("مريح — 18","Comfortable — 18")),new MainWindow.Choice("20",T("كبير وواضح","Large and clear")),new MainWindow.Choice("24",T("أكبر لراحة العين","Extra large"))},DisplayMemberPath="Label",SelectedValuePath="Key",SelectedValue=store.Setting("font","20"),Margin=new(0,0,0,18)};form.Children.Add(sizes);
        var protect=new CheckBox{Content=T("أريد رمز دخول عند فتح البرنامج (اختياري)","Ask for an unlock code when opening (optional)")};form.Children.Add(protect);var lockFields=new StackPanel{Visibility=Visibility.Collapsed,Margin=new(0,12,0,0)};form.Children.Add(lockFields);lockFields.Children.Add(Text(T("رمز أو كلمة مرور من ٤ أحرف أو أرقام على الأقل","Code or password, at least 4 characters"),18));var code=new PasswordBox();var again=new PasswordBox{Margin=new(0,10,0,0)};lockFields.Children.Add(code);lockFields.Children.Add(Text(T("أعد كتابة الرمز","Repeat the code"),18));lockFields.Children.Add(again);protect.Checked+=(s,e)=>lockFields.Visibility=Visibility.Visible;protect.Unchecked+=(s,e)=>lockFields.Visibility=Visibility.Collapsed;
        var error=Text("",18);footer.Children.Add(error);var start=new Button{Content=T("ابدأ استخدام البرنامج","Start using Hisab"),Style=(Style)resources["PrimaryButton"],Margin=new(0),FontSize=22};footer.Children.Add(start);footer.Children.Add(Text(T("بيانات محفوظة على جهازك · يمكنك التعديل لاحقًا","Local data · Settings can be changed later"),16));
        start.Click+=(s,e)=>{try{if(protect.IsChecked==true&&(code.Password!=again.Password||code.Password.Length<4))throw new InvalidOperationException(T("اكتب رمزًا من ٤ أحرف أو أرقام على الأقل، ثم أعده نفسه.","Enter 4+ characters and matching confirmation."));using var tx=store.BeginTransaction();store.Set("company",string.IsNullOrWhiteSpace(company.Text)?T("دفتر الحسابات","My ledger"):company.Text.Trim());store.Set("font",sizes.SelectedValue?.ToString()??"20");if(protect.IsChecked==true){store.SetPersonalLock(code.Password);if(!store.UnlockPersonal(code.Password))throw new InvalidOperationException("Unlock failed");}else store.EnablePersonalMode();tx.Commit();accepted=true;window.Close();}catch(Exception ex){error.Text=ex is InvalidOperationException?ex.Message:T("تعذر الحفظ. حاول مرة أخرى.","Could not save. Try again.");}};
        if(previewFile!=null)window.Loaded+=(s,e)=>window.Dispatcher.BeginInvoke(()=>{Capture(window,previewFile);window.Close();},System.Windows.Threading.DispatcherPriority.ApplicationIdle);window.ShowDialog();return accepted;
    }
    internal static void Capture(Window window,string file)
    {
        window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(file);encoder.Save(stream);
    }
}
public static class SessionGate
{
    public static bool Enter(Store store,Window? owner=null)
    {
        if(store.TryPersonalSession())return true;
        long users=Convert.ToInt64(store.Scalar("SELECT count(*) FROM users"));bool oldSingleOwner=users==1&&Convert.ToInt64(store.Scalar("SELECT count(*) FROM users WHERE role='Admin' AND active=1"))==1;
        if(users==0||(store.Setting("setup_done","")==""&&oldSingleOwner))return WelcomeWindow.Show(store,owner);
        return LoginWindow.Show(store,owner);
    }
}
