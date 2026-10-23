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
        public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture){int depth=System.Convert.ToInt32(value);return new Thickness(10+depth*28,0,10,0);}
        public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
    }
    sealed class GuideMargin:IValueConverter
    {
        public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture){int depth=System.Convert.ToInt32(value);return new Thickness(10+Math.Max(0,depth-1)*28+22,-9,0,-9);}
        public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
    }
    sealed class GuideVisibility:IValueConverter
    {
        public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>System.Convert.ToInt32(value)>0?Visibility.Visible:Visibility.Collapsed;
        public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
    }
    sealed class CollapsedWhenTrue:IValueConverter
    {
        public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>(bool)value?Visibility.Collapsed:Visibility.Visible;
        public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
    }
    sealed class HiddenWhenTrue:IValueConverter
    {
        public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>(bool)value?Visibility.Hidden:Visibility.Visible;
        public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
    }
    // The account list is grouped by type; a section header row carries a negative id so it can share the expand/collapse state of ordinary parents.
    static readonly string[] AccountSectionKinds=["Cash","Customer","Supplier","Asset","Liability","Equity","Income","Expense","Unclassified"];
    static string SectionKind(string kind)=>Array.IndexOf(AccountSectionKinds,kind)>=0?kind:"Unclassified";
    static long SectionId(string kind)=>-(Array.IndexOf(AccountSectionKinds,SectionKind(kind))+1);
    static string? SectionKindOf(long id)=>id<0&&-id<=AccountSectionKinds.Length?AccountSectionKinds[(int)(-id-1)]:null;
    string SubAccountCount(int n)=>vm.Arabic?(n==1?"حساب فرعي واحد":n==2?"حسابان فرعيان":n<=10?$"{n} حسابات فرعية":$"{n} حسابًا فرعيًا"):(n==1?"1 sub-account":$"{n} sub-accounts");
    // "Own + sub-accounts = total", so a parent's number can be traced to its parts.
    FrameworkElement BalanceBlock(AccountOutlineEntry entry,List<AccountOutlineEntry> kids)
    {
        var panel=new StackPanel{Margin=new(0,8,0,0)};var heading=Text(T("الرصيد","Balance"),16,true);heading.Foreground=muted;heading.Margin=new(0,0,0,6);panel.Children.Add(heading);
        void Line(string label,long amount,bool total=false,double indent=0)
        {
            var row=new DockPanel{Margin=new(indent,0,0,6)};var value=new TextBlock{Text=Store.Money(amount)+" JOD",FlowDirection=FlowDirection.LeftToRight,FontWeight=total?FontWeights.SemiBold:FontWeights.Normal,Margin=new(16,0,0,0)};DockPanel.SetDock(value,Dock.Right);row.Children.Add(value);
            row.Children.Add(new TextBlock{Text=label,Foreground=total?ink:muted,FontWeight=total?FontWeights.SemiBold:FontWeights.Normal,TextTrimming=TextTrimming.CharacterEllipsis});panel.Children.Add(row);
        }
        if(kids.Count==0){Line(T("رصيد الحساب","Account balance"),entry.Total,true);return panel;}
        Line(T("حركات الحساب نفسه","Own postings"),entry.Own);
        foreach(var kid in kids.Take(8))Line(kid.DisplayName,kid.Total,false,16);
        if(kids.Count>8){var more=Text(T($"و{kids.Count-8} حسابات فرعية أخرى",$"and {kids.Count-8} more sub-accounts"),14);more.Foreground=muted;more.Margin=new(16,0,0,6);panel.Children.Add(more);}
        panel.Children.Add(new Border{Height=1,Background=UiTheme.Brush(Resources,"Brush.Divider"),Margin=new(0,2,0,8)});Line(T("الإجمالي مع الفروع","Total with sub-accounts"),entry.Total,true);return panel;
    }
    string AccountCount(int n)=>vm.Arabic?(n==1?"حساب واحد":n==2?"حسابان":n<=10?$"{n} حسابات":$"{n} حسابًا"):(n==1?"1 account":$"{n} accounts");
    static IEnumerable<TElement> Descendants<TElement>(DependencyObject parent) where TElement:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is TElement found)yield return found;foreach(var item in Descendants<TElement>(child))yield return item;}
    }
    static TElement? Ancestor<TElement>(DependencyObject? source) where TElement:DependencyObject
    {
        while(source!=null){if(source is TElement found)return found;source=source is Visual?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);}return null;
    }
}
