using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
namespace Hisab;

public sealed partial class MainWindow
{
    sealed record AccountDrag(Guid Scope,long Id);
    const string AccountDragFormat="Hisab.AccountHierarchy";
    sealed record AccountChange(AccountEditState Before,AccountEditState After);
    AccountChange? lastAccountChange;
    long? selectedAccountId;
    Func<bool>? leaveAccountEditor;
    readonly HashSet<long> collapsedAccountGroups=[];
    sealed class AccountIndent:IValueConverter
    {
        public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture){int depth=System.Convert.ToInt32(value);return new Thickness(10+depth*20,6,10,6);}
        public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
    }
    static IEnumerable<TElement> Descendants<TElement>(DependencyObject parent) where TElement:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is TElement found)yield return found;foreach(var item in Descendants<TElement>(child))yield return item;}
    }
    static TElement? Ancestor<TElement>(DependencyObject? source) where TElement:DependencyObject
    {
        while(source!=null){if(source is TElement found)return found;source=source is Visual?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);}return null;
    }
}
