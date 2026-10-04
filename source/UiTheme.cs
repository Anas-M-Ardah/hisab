using System.Windows;
using System.Windows.Media;
namespace Hisab;
public static class UiTheme
{
    public static ResourceDictionary Create()
    {
        var resources=new ResourceDictionary{Source=new Uri("/Hisab;component/Theme.xaml",UriKind.Relative)};
        if(SystemParameters.HighContrast){foreach(var key in new[]{"Brush.Window","Brush.Surface","Brush.Sunken","Brush.Nav"})resources[key]=SystemColors.WindowBrush;foreach(var key in new[]{"Brush.Text","Brush.Secondary","Brush.NavText","Brush.NavMuted"})resources[key]=SystemColors.WindowTextBrush;foreach(var key in new[]{"Brush.Border","Brush.Divider","Brush.Focus"})resources[key]=SystemColors.WindowTextBrush;resources["Brush.Primary"]=SystemColors.HighlightBrush;resources["Brush.OnPrimary"]=SystemColors.HighlightTextBrush;resources["Brush.Success"]=SystemColors.WindowTextBrush;}
        return resources;
    }
    public static Brush Brush(ResourceDictionary resources,string key)=>(Brush)resources[key];
}
