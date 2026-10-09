using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace Hisab;
public sealed partial class MainWindow
{
    void HierarchyPreviewChecks(string path,string language)
    {
        if(Convert.ToInt64(S.Scalar("SELECT count(*) FROM accounts WHERE code='EXP'"))==0)return;
        long root=S.AccountId("EXP"),water=S.AccountId("EXP-W");Navigate("accounts");var grid=body.Children.OfType<DataGrid>().Single();
        var rootRow=((DataView)grid.ItemsSource).Cast<DataRowView>().Single(r=>(long)r["id"]==root);grid.ScrollIntoView(rootRow);grid.UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        int expanded=((DataView)grid.ItemsSource).Count;
        var collapse=Descendants<Button>(grid).First(b=>b.DataContext is DataRowView r&&(long)r["id"]==root&&b.Visibility==Visibility.Visible);collapse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));grid.UpdateLayout();
        bool collapsed=((DataView)grid.ItemsSource).Count==expanded-2;
        var expand=Descendants<Button>(grid).First(b=>b.DataContext is DataRowView r&&(long)r["id"]==root&&b.Visibility==Visibility.Visible);expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));grid.UpdateLayout();bool expandedAgain=((DataView)grid.ItemsSource).Count==expanded;
        grid.SelectedItem=((DataView)grid.ItemsSource).Cast<DataRowView>().Single(r=>(long)r["id"]==water);
        var move=Descendants<Button>(body).Single(b=>b.Content?.ToString()==T("إلى المستوى الرئيسي","Move to top level"));move.Command.Execute(null);bool moved=S.Accounts().Single(a=>a.Id==water).ParentId==null;
        var undo=Descendants<Button>(body).Single(b=>b.Content?.ToString()==T("تراجع عن النقل","Undo move"));undo.Command.Execute(null);bool undone=S.Accounts().Single(a=>a.Id==water).ParentId==root;
        File.AppendAllText(Path.Combine(path,"ui-checks.txt"),language+": hierarchy collapse="+collapsed+", expand="+expandedAgain+", move="+moved+", undo="+undone+Environment.NewLine);
        if(!collapsed||!expandedAgain||!moved||!undone)throw new InvalidOperationException("Hierarchy UI interactions failed");
    }
}
