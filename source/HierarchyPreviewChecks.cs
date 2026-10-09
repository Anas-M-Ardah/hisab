using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace Hisab;
public sealed partial class MainWindow
{
    static DragEventArgs MakeDragDropArgs(IDataObject data,DependencyObject target) {
        var args=(DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{data,DragDropKeyStates.None,DragDropEffects.Move,target,new Point()},null)!;args.RoutedEvent=DragDrop.DropEvent;return args;
    }
    void HierarchyPreviewChecks(string path,string language)
    {
        if(Convert.ToInt64(S.Scalar("SELECT count(*) FROM accounts WHERE code='EXP'"))==0)return;
        long root=S.AccountId("EXP"),water=S.AccountId("EXP-W");Navigate("accounts");var table=PageTable();
        void Select(long id){table.SelectedItem=((DataView)table.ItemsSource).Cast<DataRowView>().Single(r=>(long)r["id"]==id);table.ScrollIntoView(table.SelectedItem);UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);}
        Button Disclosure()=>Descendants<Button>(table).Single(b=>b.Name=="AccountDisclosure"&&b.DataContext is DataRowView r&&(long)r["id"]==root);
        Select(root);int expanded=table.Items.Count;long commands=S.CommandCount;Disclosure().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));UpdateLayout();bool collapsed=table.Items.Count==expanded-2&&S.CommandCount==commands;
        Navigate("items");Navigate("accounts");table=PageTable();Select(root);bool retained=table.Items.Count==expanded-2;Disclosure().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));UpdateLayout();bool expandedAgain=table.Items.Count==expanded;
        var stopwatch=System.Diagnostics.Stopwatch.StartNew();Navigate("organize-accounts");UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);stopwatch.Stop();table=PageTable();Select(water);
        commands=S.CommandCount;Select(root);Select(water);bool cached=S.CommandCount==commands;
        bool handle=Descendants<Button>(table).Any(b=>b.Name=="AccountDragHandle"&&b.DataContext is DataRowView row&&(long)row["id"]==water);
        var target=Descendants<Border>(dataWorkspace!).Single(b=>b.Name=="AccountTopLevelDrop");var scope=(Guid)table.Tag;var invalidData=new DataObject(AccountDragFormat,Guid.NewGuid().ToString("N")+"|"+water);target.RaiseEvent(MakeDragDropArgs(invalidData,target));bool scoped=S.Accounts().Single(a=>a.Id==water).ParentId==root;
        var data=new DataObject(AccountDragFormat,scope.ToString("N")+"|"+water);target.RaiseEvent(MakeDragDropArgs(data,target));bool moved=S.Accounts().Single(a=>a.Id==water).ParentId==null;
        var undo=Descendants<Button>(dataWorkspace!).Single(b=>b.Content?.ToString()==T("تراجع","Undo"));undo.Command.Execute(null);bool undone=S.Accounts().Single(a=>a.Id==water).ParentId==root;
        long transport=S.AccountId("EXP-T");Select(transport);var parentRow=Descendants<DataGridRow>(table).Single(r=>r.Item is DataRowView item&&(long)item["id"]==transport);parentRow.RaiseEvent(MakeDragDropArgs(data,parentRow));bool parentDrop=S.Accounts().Single(a=>a.Id==water).ParentId==transport;undo.Command.Execute(null);parentDrop=parentDrop&&S.Accounts().Single(a=>a.Id==water).ParentId==root;Select(water);
        var before=S.AccountEdit(water);var name=Descendants<TextBox>(dataWorkspace!).Single(t=>t.Text==S.LocalName("accounts",water,vm.Arabic));name.Text+=" UI";Descendants<Button>(dataWorkspace!).Single(b=>b.Content?.ToString()==T("حفظ التعديل","Save changes")).Command.Execute(null);bool saved=S.LocalName("accounts",water,vm.Arabic).EndsWith(" UI");undo.Command.Execute(null);bool exactUndo=S.AccountEdit(water)==before;
        var viewer=Descendants<ScrollViewer>(table).First();bool finite=table.ActualHeight<=workspaceViewport.ActualHeight&&viewer.ScrollableHeight>0&&Descendants<DataGridRow>(table).Count()<table.Items.Count;
        var search=Descendants<TextBox>(dataWorkspace!).Single(t=>t.Name=="AccountSearch");search.Text=S.LocalName("accounts",root,vm.Arabic);Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);System.Threading.Thread.Sleep(200);Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.Background);UpdateLayout();Capture(this,Path.Combine(path,language+"-organizer-focused.png"));
        AccountWorkspaceLayoutChecks(path,language,"standard");
        var saveButton=Descendants<Button>(dataWorkspace!).Single(b=>b.Content?.ToString()==T("حفظ التعديل","Save changes"));bool fieldHeight=Math.Abs(name.ActualHeight-saveButton.ActualHeight)<1.5;
        bool consistent=fieldHeight&&Descendants<Button>(dataWorkspace!).Where(b=>b.Content is string text&&text is not "⠿").All(b=>b.MinHeight>=(double)Resources["Control.Height"]);
        File.AppendAllText(Path.Combine(path,"ui-checks.txt"),language+": hierarchy collapse="+collapsed+", retained="+retained+", expand="+expandedAgain+", cached selection="+cached+", drag handle="+handle+", scoped drop="+scoped+", parent drop="+parentDrop+", drop/undo="+(moved&&undone)+", edit/undo="+(saved&&exactUndo)+", finite virtualized table="+finite+", controls consistent="+consistent+", organizer render ms="+stopwatch.ElapsedMilliseconds+Environment.NewLine);
        if(!collapsed||!retained||!expandedAgain||!cached||!handle||!scoped||!parentDrop||!moved||!undone||!saved||!exactUndo||!finite||!consistent)throw new InvalidOperationException("Organizer UI interactions failed");
        Descendants<Button>(body).Single(b=>b.Content?.ToString()==T("تم","Done")).Command.Execute(null);if(vm.Route!="accounts")throw new InvalidOperationException("Organizer Done navigation failed");
    }
}
