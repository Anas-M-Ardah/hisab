using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Hisab;

// Search displays a separate result list; the bound collection and selected value are never filtered or replaced.
public static class DropdownSearch
{
    public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(DropdownSearch),new PropertyMetadata(false,Changed));
    public static void SetEnabled(DependencyObject target,bool value)=>target.SetValue(EnabledProperty,value);
    public static bool GetEnabled(DependencyObject target)=>(bool)target.GetValue(EnabledProperty);
    static void Changed(DependencyObject target,DependencyPropertyChangedEventArgs e)
    {
        if(target is not ComboBox combo||!(bool)e.NewValue)return;
        combo.Loaded+=(_,_)=>Initialize(combo);
    }
    static void Initialize(ComboBox combo)
    {
        combo.ApplyTemplate();if(combo.Template.FindName("PART_Search",combo) is not TextBox search||search.Tag!=null)return;search.Tag=true;
        var searchPanel=(StackPanel)combo.Template.FindName("PART_SearchPanel",combo);
        var caption=(TextBlock)combo.Template.FindName("PART_SearchLabel",combo);
        var results=(ListBox)combo.Template.FindName("PART_SearchResults",combo);
        var empty=(TextBlock)combo.Template.FindName("PART_NoMatches",combo);
        var original=(ScrollViewer)combo.Template.FindName("menuItems",combo);
        string Label(object item)=>string.IsNullOrEmpty(combo.DisplayMemberPath)?item.ToString()??"":TypeDescriptor.GetProperties(item)[combo.DisplayMemberPath]?.GetValue(item)?.ToString()??item.ToString()??"";
        void Update(){bool searching=!string.IsNullOrWhiteSpace(search.Text);results.ItemsSource=combo.Items.Cast<object>().Where(i=>Label(i).Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase)).ToList();results.DisplayMemberPath=combo.DisplayMemberPath;results.ItemTemplate=combo.ItemTemplate;results.Visibility=searching?Visibility.Visible:Visibility.Collapsed;original.SetCurrentValue(UIElement.VisibilityProperty,searching||!combo.HasItems?Visibility.Collapsed:Visibility.Visible);empty.Visibility=searching&&combo.HasItems&&results.Items.Count==0?Visibility.Visible:Visibility.Collapsed;}
        void Choose(){if(results.SelectedItem==null)return;combo.SelectedItem=results.SelectedItem;combo.IsDropDownOpen=false;combo.Focus();}
        search.TextChanged+=(_,_)=>Update();
        ((System.Collections.Specialized.INotifyCollectionChanged)combo.Items).CollectionChanged+=(_,_)=>{if(combo.IsDropDownOpen){searchPanel.Visibility=combo.HasItems?Visibility.Visible:Visibility.Collapsed;Update();}};
        combo.DropDownOpened+=(_,_)=>{search.Text="";searchPanel.Visibility=combo.HasItems?Visibility.Visible:Visibility.Collapsed;bool ar=combo.FlowDirection==FlowDirection.RightToLeft;search.ToolTip=ar?"ابحث في الخيارات":"Search options";caption.Text=search.ToolTip.ToString();System.Windows.Automation.AutomationProperties.SetName(search,search.ToolTip.ToString());empty.Text=ar?"لا توجد نتائج مطابقة":"No matching options";Update();};
        combo.DropDownClosed+=(_,_)=>{search.Text="";};
        search.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Down&&results.Items.Count>0&&!string.IsNullOrWhiteSpace(search.Text)){results.SelectedIndex=0;results.Focus();e.Handled=true;}else if(e.Key==Key.Escape){combo.IsDropDownOpen=false;combo.Focus();e.Handled=true;}else if(e.Key==Key.Enter&&!string.IsNullOrWhiteSpace(search.Text)){if(results.SelectedItem==null&&results.Items.Count>0)results.SelectedIndex=0;Choose();e.Handled=true;}};
        results.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter){Choose();e.Handled=true;}else if(e.Key==Key.Escape){combo.IsDropDownOpen=false;combo.Focus();e.Handled=true;}};
        results.PreviewMouseLeftButtonUp+=(_,_)=>Choose();
    }
}
