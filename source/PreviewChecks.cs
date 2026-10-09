using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Hisab;
public sealed partial class MainWindow
{
    void PreviewDialog(string name,Action action){previewCaptureName=name;try{action();}finally{previewCaptureName=null;}}
    void ExtraPreviews(string path,string language)
    {
        HierarchyPreviewChecks(path,language);
        var customer=UiAccounts().FirstOrDefault(a=>a.Kind=="Customer");var item=UiItems().FirstOrDefault();
        if(Convert.ToInt64(S.Scalar("SELECT count(*) FROM accounts WHERE code='EXP'"))>0){long root=S.AccountId("EXP");var grouped=ReportData("ledger",DateTime.Today,DateTime.Today,root,true);var own=ReportData("ledger",DateTime.Today,DateTime.Today,root,false);bool valid=grouped.Rows.Count==3&&own.Rows.Count==1&&grouped.Rows[2][5].ToString()=="15.000";File.AppendAllText(Path.Combine(path,"ui-checks.txt"),language+": grouped statement="+valid+Environment.NewLine);if(!valid)throw new InvalidOperationException("Grouped statement failed");}
        string originalFont=S.Setting("font","20");S.Set("font","14");vm.Route="accounts";BuildShell();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();Capture(this,Path.Combine(path,language+"-small-text-accounts.png"));Navigate("items");Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();Capture(this,Path.Combine(path,language+"-small-text-items.png"));PreviewDialog("small-text-cheques",ChequeReceiptForm);S.Set("font",originalFont);BuildShell();
        if(Convert.ToInt64(S.Scalar("SELECT count(*) FROM accounts WHERE code='EXP'"))>0){Navigate("accounts");var search=Descendants<TextBox>(body).First();search.Text=S.LocalName("accounts",S.AccountId("EXP"),vm.Arabic);Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();Capture(this,Path.Combine(path,language+"-account-hierarchy.png"));}
        if(customer!=null)PreviewDialog("customer-edit",()=>AccountForm(customer));
        if(item!=null){PreviewDialog("item-edit",()=>ItemForm(item));PreviewDialog("opening-stock",()=>OpeningForm(item));}
        Navigate("tools");Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();Capture(this,Path.Combine(path,language+"-tools.png"));
        var w=new Window{Owner=this,Width=750,Height=440,Resources=Resources,FontSize=FontSize,FlowDirection=FlowDirection};var p=new StackPanel{Margin=new(24)};w.Content=p;Action finish=w.Close;var combo=new ComboBox{ItemsSource=new[]{new Choice("a",T("اختيار أول","First choice")),new Choice("b",T("اختيار ثانٍ","Second choice"))},DisplayMemberPath="Label",SelectedValuePath="Key",SelectedIndex=0};var date=Date();p.Children.Add(combo);p.Children.Add(date);w.Loaded+=(s,e)=>w.Dispatcher.BeginInvoke(()=>{
            combo.ApplyTemplate();combo.IsDropDownOpen=true;w.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);var popup=(Popup)combo.Template.FindName("PART_Popup",combo);bool comboOk=popup.IsOpen&&popup.Child is FrameworkElement element&&element.ActualHeight>0;
            combo.SelectedIndex=1;combo.IsDropDownOpen=false;date.ApplyTemplate();date.IsDropDownOpen=true;w.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);var calendar=(Popup)date.Template.FindName("PART_Popup",date);bool calendarOk=calendar.IsOpen&&calendar.Child is FrameworkElement c&&c.ActualHeight>0;date.IsDropDownOpen=false;
            File.AppendAllText(Path.Combine(path,"ui-checks.txt"),language+": combo popup="+comboOk+", selected value="+combo.SelectedValue+", calendar popup="+calendarOk+Environment.NewLine);if(!comboOk||!calendarOk)throw new InvalidOperationException("Control interaction preview failed");finish();
        },System.Windows.Threading.DispatcherPriority.Loaded);w.ShowDialog();
        double oldWidth=Width,oldHeight=Height;string oldFont=S.Setting("font","20");Width=1024;Height=768;S.Set("font","24");vm.Route="home";BuildShell();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();Capture(this,Path.Combine(path,language+"-large-text-small-window.png"));Width=oldWidth;Height=oldHeight;S.Set("font",oldFont);BuildShell();
    }
}
