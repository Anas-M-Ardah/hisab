using System.Windows;
using System.Windows.Media;
namespace Hisab;
public static class UiTheme
{
    public static ResourceDictionary Create()
    {
        var resources=new ResourceDictionary{Source=new Uri("/Hisab;component/Theme.xaml",UriKind.Relative)};
        if(SystemParameters.HighContrast){foreach(var key in new[]{"Brush.Window","Brush.Surface","Brush.Sunken","Brush.Nav"})resources[key]=SystemColors.WindowBrush;foreach(var key in new[]{"Brush.Text","Brush.Secondary","Brush.NavText","Brush.NavMuted"})resources[key]=SystemColors.WindowTextBrush;foreach(var key in new[]{"Brush.Border","Brush.Divider","Brush.Focus"})resources[key]=SystemColors.WindowTextBrush;resources["Brush.SelectionText"]=SystemColors.HighlightTextBrush;resources["Brush.NavSelected"]=SystemColors.HighlightBrush;resources["Brush.Primary"]=SystemColors.HighlightBrush;resources["Brush.OnPrimary"]=SystemColors.HighlightTextBrush;resources["Brush.Success"]=SystemColors.WindowTextBrush;}
        resources["Control.Height"]=44.0;resources["Row.Height"]=52.0;
        return resources;
    }
    public static Brush Brush(ResourceDictionary resources,string key)=>(Brush)resources[key];
    // A quiet primary wash that reads as "selected" and stays distinct from the grey hover colour; high contrast keeps system colours.
    public static Brush SelectionTint(ResourceDictionary resources)
    {
        if(SystemParameters.HighContrast||Brush(resources,"Brush.Primary") is not SolidColorBrush primary||Brush(resources,"Brush.Surface") is not SolidColorBrush surface)return Brush(resources,"Brush.Sunken");
        byte Mix(byte tint,byte basis)=>(byte)Math.Round(basis+(tint-basis)*.2);
        return new SolidColorBrush(Color.FromRgb(Mix(primary.Color.R,surface.Color.R),Mix(primary.Color.G,surface.Color.G),Mix(primary.Color.B,surface.Color.B)));
    }
}
