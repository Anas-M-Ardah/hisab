using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace Hisab;
public sealed partial class MainWindow
{
    void TablePreviewChecks(string path,string language)
    {
        Navigate("items");UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        var table=PageTable();var column=table.Columns.Single(c=>c.SortMemberPath=="incoming_numeric");var header=Descendants<DataGridColumnHeader>(table).Single(h=>h.Column==column);
        void ClickHeader()=>typeof(DataGridColumnHeader).GetMethod("OnClick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(header,null);
        ClickHeader();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        bool ascending=column.SortDirection==System.ComponentModel.ListSortDirection.Ascending&&((DataRowView)table.Items[0])["incoming"].ToString()=="0";
        ClickHeader();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        bool descending=column.SortDirection==System.ComponentModel.ListSortDirection.Descending&&((DataRowView)table.Items[0])["incoming"].ToString()=="10";
        double before=column.ActualWidth;var grip=(Thumb)header.Template.FindName("PART_RightHeaderGripper",header);grip.RaiseEvent(new DragStartedEventArgs(0,0){RoutedEvent=Thumb.DragStartedEvent});grip.RaiseEvent(new DragDeltaEventArgs(35,0){RoutedEvent=Thumb.DragDeltaEvent});grip.RaiseEvent(new DragCompletedEventArgs(35,0,false){RoutedEvent=Thumb.DragCompletedEvent});UpdateLayout();bool resize=Math.Abs(column.ActualWidth-before)>10;
        table.ScrollIntoView(table.Items[table.Items.Count-1]);UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);var scroll=Descendants<ScrollViewer>(table).First();bool scrolling=scroll.VerticalOffset>0;var bar=Descendants<ScrollBar>(table).First(b=>b.Orientation==Orientation.Vertical);bool thumb=bar.Template.FindName("PART_Track",bar) is Track track&&track.Thumb.ActualHeight>0;
        table.SelectedIndex=0;bool count=Descendants<TextBlock>(dataWorkspace!).Any(t=>t.Text==T("17 سجل · سجل محدد","17 records · 1 selected"));
        File.AppendAllText(Path.Combine(path,"ui-checks.txt"),language+": table numeric sort="+(ascending&&descending)+", resize="+resize+", scrolling="+scrolling+", scrollbar thumb="+thumb+", selection count="+count+Environment.NewLine);
        if(!ascending||!descending||!resize||!scrolling||!thumb||!count)throw new InvalidOperationException("Table UI checks failed");
        Navigate("items");
    }
}
