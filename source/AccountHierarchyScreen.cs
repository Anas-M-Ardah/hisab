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
    sealed record AccountMove(long Id,long? Before,long? After);
    AccountMove? lastAccountMove;
    sealed class AccountIndent(bool arabic):IValueConverter
    {
        public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture){int depth=System.Convert.ToInt32(value);return new Thickness(arabic?10:10+depth*20,6,arabic?10+depth*20:10,6);}
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
    void HierarchyAccountsPage()
    {
        Heading(T("دليل الحسابات","Accounts"),T("اسحب حسابًا إلى حساب رئيسي لترتيبه، أو استخدم زر «نقل الحساب».","Drag an account onto its parent, or use Move account."));
        bool editable=S.User.Role is "Admin" or "Accountant";var collapsed=new HashSet<long>();var scope=Guid.NewGuid();
        var search=Input();body.Children.Add(Field(T("بحث بالاسم أو الكود","Search name or code"),search));
        var hint=Text(T("نقل الحساب ينقل جميع فروعه معه. يمكنك التراجع عن آخر نقل.","Moving an account moves its children too. You can undo the last move."),18);
        body.Children.Add(hint);
        DataTable Rows()
        {
            var table=new DataTable();foreach(var col in new[]{"id","code","name","type","own","balance","toggle"})table.Columns.Add(col,col=="id"?typeof(long):typeof(string));table.Columns.Add("expand",typeof(bool));table.Columns.Add("depth",typeof(int));
            var all=UiAccounts();var matching=new HashSet<long>();
            if(search.Text.Length>0)foreach(var account in all.Where(a=>(a.Name+a.Code).Contains(search.Text,StringComparison.OrdinalIgnoreCase)))
            {matching.UnionWith(S.AccountFamily(account.Id));var parent=account.ParentId;while(parent!=null){matching.Add(parent.Value);parent=all.Single(a=>a.Id==parent).ParentId;}}
            void Add(Account account,int depth)
            {
                if(search.Text.Length>0&&!matching.Contains(account.Id))return;
                bool hasChildren=all.Any(a=>a.ParentId==account.Id);table.Rows.Add(account.Id,account.Code,account.Name,AccountKind(account.Kind),Store.Money(S.Balance(account.Id)),Store.Money(S.GroupBalance(account.Id)),collapsed.Contains(account.Id)?"+":"−",hasChildren,depth);
                if(!collapsed.Contains(account.Id))foreach(var child in all.Where(a=>a.ParentId==account.Id))Add(child,depth+1);
            }
            foreach(var root in all.Where(a=>a.ParentId==null))Add(root,0);return table;
        }
        var grid=Grid(Rows(),("code",T("الكود","Code"),1),("name",T("الحساب / الفروع","Account / children"),3),("type",T("النوع","Type"),1.4),("own",T("رصيد الحساب","Own balance"),1.5),("balance",T("مع الفروع","Including children"),1.5));
        // Sorting flat rows would separate children from their parents.
        grid.CanUserSortColumns=false;grid.AllowDrop=editable;
        var nameColumn=(DataGridTextColumn)grid.Columns[1];var nameStyle=new Style(typeof(TextBlock),nameColumn.ElementStyle);nameStyle.Setters.Add(new Setter(TextBlock.MarginProperty,new Binding("[depth]"){Converter=new AccountIndent(vm.Arabic)}));nameColumn.ElementStyle=nameStyle;
        void Refresh(long? selected=null){grid.ItemsSource=Rows().DefaultView;if(selected!=null){foreach(DataRowView row in (DataView)grid.ItemsSource)if((long)row["id"]==selected){grid.SelectedItem=row;grid.ScrollIntoView(row);break;}}}
        var toggle=new FrameworkElementFactory(typeof(Button));toggle.SetBinding(Button.ContentProperty,new Binding("[toggle]"));toggle.SetBinding(Button.VisibilityProperty,new Binding("[expand]"){Converter=new BooleanToVisibilityConverter()});toggle.SetValue(Button.MinWidthProperty,30.0);toggle.SetValue(Button.MinHeightProperty,30.0);toggle.SetValue(Button.PaddingProperty,new Thickness(4));toggle.SetValue(Button.MarginProperty,new Thickness(4));toggle.SetValue(Button.ToolTipProperty,T("طي / فتح الفروع","Collapse / expand children"));
        toggle.AddHandler(Button.ClickEvent,new RoutedEventHandler((s,e)=>{if(((Button)s).DataContext is DataRowView row){long id=(long)row["id"];if(!collapsed.Add(id))collapsed.Remove(id);Refresh(id);e.Handled=true;}}));
        grid.Columns.Insert(0,new DataGridTemplateColumn{Header="",Width=44,MinWidth=44,MaxWidth=44,CellTemplate=new DataTemplate{VisualTree=toggle}});
        var undo=Btn(T("تراجع عن النقل","Undo move"),()=>{});undo.IsEnabled=editable&&lastAccountMove!=null;
        void Move(long id,long? parent)
        {
            if(!S.CanMoveAccount(id,parent,out var reason)){hint.Text=reason;return;}
            var before=S.Accounts().Single(a=>a.Id==id).ParentId;S.MoveAccount(id,parent);lastAccountMove=new(id,before,parent);undo.IsEnabled=true;
            if(parent!=null)collapsed.Remove(parent.Value);search.Clear();Refresh(id);vm.Status=T("تم نقل الحساب. يمكنك التراجع.","Account moved. Undo is available.");hint.Text=vm.Status;
        }
        undo.Command=new RelayCommand(()=>Guard(()=>{if(lastAccountMove is not AccountMove move)return;var current=S.Accounts().Single(a=>a.Id==move.Id);if(current.ParentId!=move.After)throw new InvalidOperationException(T("تغير مكان الحساب بعد النقل؛ استخدم نقل الحساب من جديد.","Account changed since the move; use Move account again."));S.MoveAccount(move.Id,move.Before);lastAccountMove=null;undo.IsEnabled=false;collapsed.Clear();search.Clear();Refresh(move.Id);hint.Text=vm.Status=T("تم التراجع عن نقل الحساب","Account move undone");}));
        void MoveDialog()
        {
            long id=Selected(grid);var account=S.Accounts().Single(a=>a.Id==id);var family=S.AccountFamily(id);
            var (w,p,finish)=Dialog(T("نقل الحساب","Move account"),740);p.Children.Add(Text(S.LocalName("accounts",id,vm.Arabic),22,true));
            var parents=new ComboBox{DisplayMemberPath="Label",SelectedValuePath="Id",ItemsSource=new[]{new PositionChoice(null,T("المستوى الرئيسي — بدون أب","Top level — no parent"))}.Concat(UiAccounts().Where(a=>a.Kind==account.Kind&&!family.Contains(a.Id)).Select(a=>new PositionChoice(a.Id,a.ToString()))).ToList()};parents.SelectedValue=account.ParentId;if(parents.SelectedIndex<0)parents.SelectedIndex=0;
            p.Children.Add(Field(T("الحساب الرئيسي الجديد","New parent account"),parents));p.Children.Add(Text(T("تنتقل جميع الفروع مع الحساب. لا تتغير قيوده أو أرصدته الأصلية.","All children move with the account. Its posted entries and own balance stay the same."),18));
            p.Children.Add(Btn(T("نقل الحساب","Move account"),()=>{Move(id,(parents.SelectedItem as PositionChoice)?.Id);finish();},true));w.ShowDialog();
        }
        var moveButton=Btn(T("نقل الحساب…","Move account…"),MoveDialog);var rootButton=Btn(T("إلى المستوى الرئيسي","Move to top level"),()=>Move(Selected(grid),null));var childButton=Btn(T("+ حساب فرعي","+ Child account"),()=>AccountForm(null,Selected(grid)));
        foreach(var button in new[]{moveButton,rootButton,childButton})button.IsEnabled=editable;
        body.Children.Add(Actions(Btn(T("+ حساب جديد","+ New account"),()=>AccountForm(null),true),childButton,moveButton,rootButton,undo));
        var topLevel=Card(Text(T("⬆ اسحب هنا لجعل الحساب رئيسيًا","⬆ Drop here to move an account to the top level"),18,true));topLevel.Padding=new Thickness(14,8,14,0);topLevel.AllowDrop=editable;body.Children.Add(topLevel);
        body.Children.Add(grid);body.Children.Add(Actions(Btn(T("تعديل المحدد","Edit selected"),()=>AccountForm(UiAccounts().Single(a=>a.Id==Selected(grid)))),Btn(T("الأسماء والعناوين باللغتين","Bilingual names & addresses"),()=>BilingualForm("accounts",Selected(grid)))));
        body.Children.Add(Text(T("الرصيد مع الفروع يشمل جميع الحسابات الفرعية. لا تجمع هذا العمود لتجنب التكرار.","Including children totals all descendants. Do not sum this column, as totals overlap."),18));
        search.TextChanged+=(s,e)=>{collapsed.Clear();Refresh();};grid.MouseDoubleClick+=(s,e)=>{if(Ancestor<Button>(e.OriginalSource as DependencyObject)==null)Guard(()=>AccountForm(UiAccounts().Single(a=>a.Id==Selected(grid))));};
        Point start=default;long? dragging=null;DataGridRow? highlighted=null;DateTime lastScroll=DateTime.MinValue;
        void ClearHighlight(){highlighted?.ClearValue(Control.BackgroundProperty);highlighted=null;topLevel.ClearValue(Border.BorderBrushProperty);topLevel.BorderBrush=UiTheme.Brush(Resources,"Brush.Divider");}
        AccountDrag? Drag(IDataObject data){if(!data.GetDataPresent(AccountDragFormat)||data.GetData(AccountDragFormat) is not string payload)return null;var parts=payload.Split('|');return parts.Length==2&&Guid.TryParse(parts[0],out var origin)&&origin==scope&&long.TryParse(parts[1],out var id)?new(origin,id):null;}
        grid.PreviewMouseLeftButtonDown+=(s,e)=>{dragging=null;if(!editable||Ancestor<ButtonBase>(e.OriginalSource as DependencyObject)!=null||Ancestor<ScrollBar>(e.OriginalSource as DependencyObject)!=null)return;if(Ancestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is DataRowView row){start=e.GetPosition(grid);dragging=(long)row["id"];}};
        grid.PreviewMouseMove+=(s,e)=>{if(e.LeftButton!=MouseButtonState.Pressed){dragging=null;return;}if(dragging==null)return;Point point=e.GetPosition(grid);if(Math.Abs(point.X-start.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(point.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return;long id=dragging.Value;dragging=null;try{DragDrop.DoDragDrop(grid,new DataObject(AccountDragFormat,scope.ToString("N")+"|"+id),DragDropEffects.Move);}finally{ClearHighlight();}};
        void Over(DragEventArgs e,bool root)
        {
            ClearHighlight();e.Effects=DragDropEffects.None;e.Handled=true;var drag=Drag(e.Data);var row=root?null:Ancestor<DataGridRow>(e.OriginalSource as DependencyObject);long? parent=row?.Item is DataRowView item?(long)item["id"]:null;
            if(!root&&drag!=null&&(DateTime.UtcNow-lastScroll).TotalMilliseconds>120){var point=e.GetPosition(grid);var viewer=Descendants<ScrollViewer>(grid).FirstOrDefault();if(point.Y<90)viewer?.LineUp();else if(point.Y>grid.ActualHeight-35)viewer?.LineDown();lastScroll=DateTime.UtcNow;}
            if(drag==null||(!root&&row==null))return;
            if(S.CanMoveAccount(drag.Id,parent,out var reason)){e.Effects=DragDropEffects.Move;hint.Text=root?T("اتركه هنا ليصبح حسابًا رئيسيًا","Drop to make this a top-level account"):T("اتركه هنا ليصبح حسابًا فرعيًا","Drop to make this a child account");if(row!=null){highlighted=row;row.Background=UiTheme.Brush(Resources,"Brush.Sunken");}else topLevel.BorderBrush=accent;}
            else hint.Text=reason;
        }
        void Drop(DragEventArgs e,bool root){ClearHighlight();e.Handled=true;var drag=Drag(e.Data);var row=root?null:Ancestor<DataGridRow>(e.OriginalSource as DependencyObject);if(drag==null||(!root&&row==null))return;long? parent=row?.Item is DataRowView item?(long)item["id"]:null;Guard(()=>Move(drag.Id,parent));}
        grid.DragOver+=(s,e)=>Over(e,false);grid.Drop+=(s,e)=>Drop(e,false);grid.DragLeave+=(s,e)=>ClearHighlight();topLevel.DragOver+=(s,e)=>Over(e,true);topLevel.Drop+=(s,e)=>Drop(e,true);topLevel.DragLeave+=(s,e)=>ClearHighlight();
    }
}
