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
            HierarchyPreviewChecks(path,language);
            foreach(var size in new[]{(Name:"small-text",Font:14,Width:originalWidth,Height:originalHeight),(Name:"standard",Font:20,Width:originalWidth,Height:originalHeight),(Name:"large-text",Font:24,Width:1024.0,Height:768.0)})
            {
                S.Set("font",size.Font.ToString());Width=size.Width;Height=size.Height;vm.Route="organize-accounts";BuildShell();UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
                AccountWorkspaceLayoutChecks(path,language,size.Name);Capture(this,Path.Combine(path,language+"-"+size.Name+"-organizer.png"));
            }
        }
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
