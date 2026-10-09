using System.IO;
using System.Windows.Controls;
namespace Hisab;
public sealed partial class MainWindow
{
    void RedesignPreviewChecks(string path,string language)
    {
        Navigate("invoices");UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        var table=PageTable();var more=Descendants<Button>(body).Single(b=>b.Content?.ToString()==T("المزيد…","More…"));bool disabled=!more.IsEnabled;
        bool menu=true;if(table.Items.Count>0){table.SelectedIndex=0;more.Command.Execute(null);Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);menu=more.IsEnabled&&more.ContextMenu!.IsOpen&&more.ContextMenu.Items.OfType<MenuItem>().Count()==4&&more.ContextMenu.Items.OfType<MenuItem>().All(item=>item.Command!=null);more.ContextMenu.IsOpen=false;}
        var search=Descendants<TextBox>(body).First();search.Text="__hisab_no_match__";UpdateLayout();bool empty=table.Items.Count==0&&!more.IsEnabled&&Descendants<TextBlock>(dataWorkspace!).Any(text=>text.Text==T("لا توجد سجلات لعرضها. غيّر البحث أو أضف سجلًا.","No records to show. Change the search or add a record.")&&text.Visibility==System.Windows.Visibility.Visible);
        Navigate("settings");UpdateLayout();bool save=pageActionFooter!=null&&pageActionFooter.ActualHeight>0;Navigate("items");UpdateLayout();bool cleared=pageActionFooter==null;
        File.AppendAllText(Path.Combine(path,"ui-checks.txt"),language+": document actions gated="+disabled+", more menu="+menu+", empty state="+empty+", settings save visible="+save+", footer clears="+cleared+Environment.NewLine);
        if(!disabled||!menu||!empty||!save||!cleared)throw new InvalidOperationException("Redesign UI checks failed");
    }
}
