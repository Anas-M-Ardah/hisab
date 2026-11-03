using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace Hisab;

public sealed partial class MainWindow
{
    public void RenderAccountPreviews(string path)
    {
        Directory.CreateDirectory(path);previewDirectory=path;
        double originalWidth=Width,originalHeight=Height;
        foreach(string language in new[]{"ar","en"})
        {
            S.Set("language",language);S.Set("font","20");Width=originalWidth;Height=originalHeight;BuildShell();
            EmptyDropdownPreviewChecks(path,language);
            HierarchyPreviewChecks(path,language);
            foreach(var size in new[]{(Name:"small-text",Font:14,Width:originalWidth,Height:originalHeight),(Name:"standard",Font:20,Width:originalWidth,Height:originalHeight),(Name:"large-text",Font:24,Width:1024.0,Height:768.0)})
            {
                S.Set("font",size.Font.ToString());Width=size.Width;Height=size.Height;vm.Route="organize-accounts";BuildShell();UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
                AccountWorkspaceLayoutChecks(path,language,size.Name);Capture(this,Path.Combine(path,language+"-"+size.Name+"-organizer.png"));
            }
        }
    }
    void EmptyDropdownPreviewChecks(string path,string language)
    {
        var items=new System.Collections.ObjectModel.ObservableCollection<string>();
        var combo=new ComboBox{ItemsSource=items,Margin=new Thickness(24)};
        var window=new Window{Owner=this,Width=440,Height=180,Resources=Resources,FontSize=FontSize,FlowDirection=FlowDirection,Content=combo,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        window.Show();
        try {
            window.UpdateLayout();combo.ApplyTemplate();
            var placeholder=(TextBlock)combo.Template.FindName("emptySelection",combo);
            string expected=language=="ar"?"لا توجد بيانات":"No data available";
            if(placeholder.Visibility!=Visibility.Visible||placeholder.Text!=expected||combo.SelectedIndex!=-1)throw new InvalidOperationException("Empty dropdown placeholder failed");
            combo.IsDropDownOpen=true;window.UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
            var message=(TextBlock)combo.Template.FindName("emptyMenu",combo);
            if(!message.IsVisible||message.Text!=expected||combo.Items.Count!=0)throw new InvalidOperationException("Empty dropdown menu failed");
            combo.IsDropDownOpen=false;
            Capture(window,Path.Combine(path,language+"-empty-dropdown.png"));
            items.Add("Available item");window.UpdateLayout();
            if(placeholder.Visibility!=Visibility.Collapsed)throw new InvalidOperationException("Empty message stayed visible after data arrived");
            items.Add("Other choice");combo.SelectedIndex=0;combo.IsDropDownOpen=true;window.UpdateLayout();
            var search=(TextBox)combo.Template.FindName("PART_Search",combo);var matches=(ListBox)combo.Template.FindName("PART_SearchResults",combo);var noMatches=(TextBlock)combo.Template.FindName("PART_NoMatches",combo);
            search.Text="Other";window.UpdateLayout();if(matches.Items.Count!=1||combo.Items.Count!=2||combo.SelectedIndex!=0)throw new InvalidOperationException("Dropdown search changed source or selection");
            search.Text="missing";window.UpdateLayout();if(noMatches.Visibility!=Visibility.Visible)throw new InvalidOperationException("No-match feedback missing");
            search.Text="Other";window.UpdateLayout();matches.SelectedIndex=0;
            matches.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),0,System.Windows.Input.Key.Enter){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent});
            if(combo.SelectedItem?.ToString()!="Other choice"||combo.IsDropDownOpen)throw new InvalidOperationException("Keyboard search selection failed");
            combo.IsDropDownOpen=false;items.Clear();window.UpdateLayout();
            if(placeholder.Visibility!=Visibility.Visible||combo.SelectedIndex!=-1)throw new InvalidOperationException("Empty message failed after data cleared");
            File.AppendAllText(Path.Combine(path,"ui-checks.txt"),language+": empty dropdown placeholder/menu localized, nonselectable and reactive=True"+Environment.NewLine);
        } finally {window.Close();}
    }
    static void AccountEditorLayoutChecks(DependencyObject root)
    {
        foreach(string name in new[]{"AccountNameEditor","AccountCodeEditor","AccountParentEditor","AccountSaveButton"})
        {
            var control=Descendants<FrameworkElement>(root).Single(e=>e.Name==name);
            var viewport=Ancestor<ScrollViewer>(control) as FrameworkElement??Ancestor<Border>(control)!;
            Rect bounds=control.TransformToAncestor(viewport).TransformBounds(new Rect(control.RenderSize));
            if(control.ActualWidth<280||control.ActualHeight<40||!new Rect(-1,-1,viewport.ActualWidth+2,viewport.ActualHeight+2).Contains(bounds))
                throw new InvalidOperationException("Account editor control is clipped: "+name+" "+bounds+" / "+viewport.RenderSize);
        }
    }
    void AccountWorkspaceLayoutChecks(string path,string language,string size)
    {
        var table=PageTable();var scroll=Descendants<ScrollViewer>(table).First();
        bool treeRoom=scroll.ViewportHeight>=3&&table.ActualHeight<=workspaceViewport.ActualHeight;
        var inline=Descendants<Border>(dataWorkspace!).Single(e=>e.Name=="AccountInlineEditor");
        bool compact=!inline.IsVisible;var details=Descendants<Button>(dataWorkspace!).Single(e=>e.Name=="AccountDetailsButton");
        if(compact){if(!details.IsVisible||!details.IsEnabled)throw new InvalidOperationException("Compact editor action missing");PreviewDialog(size+"-account-editor",()=>details.Command.Execute(null));}
        else AccountEditorLayoutChecks(dataWorkspace!);
        File.AppendAllText(Path.Combine(path,"ui-checks.txt"),language+": "+size+" layout tree has 3+ rows="+treeRoom+", editor="+(compact?"dialog":"inline")+", name/code/parent/save fully visible=True"+Environment.NewLine);
        if(!treeRoom)throw new InvalidOperationException("Account tree has insufficient room: "+scroll.ViewportHeight);
    }
}
