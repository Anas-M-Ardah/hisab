using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Hisab;
public static class LoginWindow
{
    public static bool Show(Store store,Window? owner=null,string? previewFile=null,bool? setupOverride=null)
    {
        if(previewFile==null&&store.TryPersonalSession())return true;
        bool locked=store.Setting("auth_mode","")=="lock",accepted=false,arabic=store.Setting("language","ar")=="ar";string T(string ar,string en)=>arabic?ar:en;var resources=UiTheme.Create();
        var window=new Window{Title=T("افتح دفترك","Open your ledger"),Width=640,Height=locked?530:660,MaxHeight=SystemParameters.WorkArea.Height-30,Owner=owner,WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner,FontFamily=new FontFamily("Segoe UI"),FontSize=21,FlowDirection=arabic?FlowDirection.RightToLeft:FlowDirection.LeftToRight,Resources=resources,Background=UiTheme.Brush(resources,"Brush.Window"),Foreground=UiTheme.Brush(resources,"Brush.Text")};
        var panel=new StackPanel{Margin=new(38)};window.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(new TextBlock{Text=T("أهلًا بعودتك","Welcome back"),FontSize=32,FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,14)});panel.Children.Add(new TextBlock{Text=locked?T("أدخل الرمز الذي اخترته لدفترك.","Enter the code you chose for your ledger."):T("سجّل الدخول إلى الدفتر المشترك.","Sign in to your shared ledger."),TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,28)});
        var name=new TextBox{Margin=new(0,8,0,20)};if(!locked){panel.Children.Add(new TextBlock{Text=T("اسم المستخدم","Username")});panel.Children.Add(name);}panel.Children.Add(new TextBlock{Text=locked?T("رمز الدخول","Unlock code"):T("كلمة المرور","Password")});var password=new PasswordBox{Margin=new(0,8,0,18)};panel.Children.Add(password);var error=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,14),FontSize=18};panel.Children.Add(error);
        var button=new Button{Content=T("افتح الدفتر","Open ledger"),Style=(Style)resources["PrimaryButton"],IsDefault=true,Margin=new(0)};panel.Children.Add(button);button.Click+=(s,e)=>{try{if(!(locked?store.UnlockPersonal(password.Password):store.Login(name.Text,password.Password)))throw new InvalidOperationException(T("تحقق من الرمز أو كلمة المرور. بعد خمس محاولات انتظر خمس دقائق.","Check your credentials. After five failed attempts, wait five minutes."));accepted=true;window.Close();}catch(Exception ex){error.Text=ex is InvalidOperationException?ex.Message:T("تعذر فتح الدفتر. حاول مرة أخرى.","Could not open the ledger. Try again.");}};
        if(previewFile!=null)window.Loaded+=(s,e)=>window.Dispatcher.BeginInvoke(()=>{WelcomeWindow.Capture(window,previewFile);window.Close();},System.Windows.Threading.DispatcherPriority.ApplicationIdle);window.Loaded+=(s,e)=>password.Focus();window.ShowDialog();return accepted;
    }
}
