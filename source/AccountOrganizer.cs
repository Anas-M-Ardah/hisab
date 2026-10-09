using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
namespace Hisab;

public sealed partial class MainWindow
{
    sealed class DisclosureVisibility:IValueConverter { public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>(bool)value?Visibility.Visible:Visibility.Hidden;public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException(); }
    sealed class DisclosureGeometry(bool rtl):IValueConverter
    {
        public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>Geometry.Parse((bool)value?(rtl?"M 12 2 L 5 9 L 12 16":"M 5 2 L 12 9 L 5 16"):"M 2 5 L 9 12 L 16 5");
        public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
    }
    void HierarchyAccountsPage(bool organize=false)
    {
        bool editable=S.User.Role is "Admin" or "Accountant";if(organize&&!editable){Navigate("accounts");return;}
        var snapshot=S.AccountOutline(vm.Arabic);var entries=snapshot.ToDictionary(x=>x.Account.Id);
        var children=snapshot.Where(x=>x.Account.ParentId!=null).ToLookup(x=>x.Account.ParentId!.Value);var scope=Guid.NewGuid();
        IEnumerable<long> Family(long id){yield return id;foreach(var child in children[id])foreach(var descendant in Family(child.Account.Id))yield return descendant;}
        string Breadcrumb(long id){var names=new List<string>();for(long? next=id;next!=null;next=entries[next.Value].Account.ParentId)names.Add(entries[next.Value].DisplayName);names.Reverse();return string.Join(" / ",names);}
        bool ValidMove(long id,long? parent,out string reason)
        {
            reason="";if(!entries.ContainsKey(id)||parent!=null&&!entries.ContainsKey(parent.Value))reason=T("الحساب غير موجود","Account no longer exists");
            else if(entries[id].Account.ParentId==parent)reason=T("الحساب موجود هنا بالفعل","Account is already here");
            else if(parent!=null&&Family(id).Contains(parent.Value))reason=T("لا يمكن نقل الحساب إلى نفسه أو أحد فروعه","Cannot move an account into itself or its children");
            else if(parent!=null&&entries[parent.Value].Account.Kind!=entries[id].Account.Kind)reason=T("اختر حسابًا من نفس النوع","Choose an account of the same type");return reason.Length==0;
        }
        Heading(T(organize?"ترتيب الحسابات":"دليل الحسابات",organize?"Organize accounts":"Accounts"),T(organize?"اسحب المقبض بجانب الحساب إلى حسابه الرئيسي، أو عدّل الحساب الرئيسي في المحرر.":"اختر حسابًا لعرض رصيده وتفاصيله.",organize?"Drag an account's handle onto its parent, or choose its parent in the editor.":"Select an account to view its balance and details."));
        var search=Input();var organizeButton=Btn(T(organize?"تم · العودة للحسابات":"ترتيب الحسابات",organize?"Done · back to accounts":"Organize accounts"),()=>Navigate(organize?"accounts":"organize-accounts"),!organize);organizeButton.IsEnabled=editable||organize;
        var newAccount=Btn(T("+ حساب رئيسي","+ Main account"),()=>AccountForm(null,null,organize?"organize-accounts":"accounts"),organize);newAccount.IsEnabled=editable;
        body.Children.Add(SearchActions(T("بحث بالاسم أو الكود","Search name or code"),search,newAccount,organizeButton));
        DataTable Rows()
        {
            var table=new DataTable();foreach(var column in new[]{"id","name","balance"})table.Columns.Add(column,column=="id"?typeof(long):typeof(string));table.Columns.Add("expand",typeof(bool));table.Columns.Add("toggle",typeof(bool));table.Columns.Add("depth",typeof(int));
            var matching=new HashSet<long>();string query=search.Text.Trim();if(query.Length>0)foreach(var entry in snapshot.Where(x=>(x.DisplayName+" "+x.Account.Code).Contains(query,StringComparison.OrdinalIgnoreCase))){matching.UnionWith(Family(entry.Account.Id));for(long? id=entry.Account.ParentId;id!=null;id=entries[id.Value].Account.ParentId)matching.Add(id.Value);}
            void Add(AccountOutlineEntry entry,int depth){long id=entry.Account.Id;if(query.Length>0&&!matching.Contains(id))return;table.Rows.Add(id,entry.DisplayName,Store.Money(entry.Total),children[id].Any(),query.Length==0&&collapsedAccountGroups.Contains(id),depth);if(query.Length>0||!collapsedAccountGroups.Contains(id))foreach(var child in children[id])Add(child,depth+1);}
            foreach(var entry in snapshot.Where(x=>x.Account.ParentId==null))Add(entry,0);return table;
        }
        var table=Grid(Rows(),("name",T("الحسابات","Accounts"),3), ("balance",T("الرصيد","Balance"),1.4));table.MaxHeight=double.PositiveInfinity;table.MinHeight=0;table.CanUserSortColumns=false;table.AllowDrop=organize;table.Tag=scope;
        var rowStyle=new Style(typeof(DataGridRow),(Style)FindResource(typeof(DataGridRow)));var selection=new Trigger{Property=DataGridRow.IsSelectedProperty,Value=true};selection.Setters.Add(new Setter(Control.BackgroundProperty,UiTheme.Brush(Resources,"Brush.Sunken")));selection.Setters.Add(new Setter(Control.ForegroundProperty,ink));rowStyle.Triggers.Add(selection);table.RowStyle=rowStyle;
        var cellStyle=new Style(typeof(DataGridCell),(Style)FindResource(typeof(DataGridCell)));var selectedCell=new Trigger{Property=DataGridCell.IsSelectedProperty,Value=true};selectedCell.Setters.Add(new Setter(Control.ForegroundProperty,ink));cellStyle.Triggers.Add(selectedCell);table.CellStyle=cellStyle;
        var detail=new StackPanel();var editorActions=new StackPanel();var hint=Text("",16);hint.Foreground=muted;hint.Margin=new(0);var undo=Btn(T("تراجع","Undo"),()=>{});undo.IsEnabled=editable&&lastAccountChange!=null;undo.Margin=new(12,0,0,0);
        TextBox? editName=null,editCode=null;ComboBox? editParent=null;long? editing=null;bool selecting=false;
        bool Dirty()=>organize&&editing is long id&&entries.ContainsKey(id)&&(editName!.Text!=entries[id].DisplayName||editCode!.Text!=entries[id].Account.Code||(editParent!.SelectedItem as PositionChoice)?.Id!=entries[id].Account.ParentId);
        bool Discard()=>!Dirty()||MessageBox.Show(this,T("تجاهل التعديل غير المحفوظ؟","Discard the unsaved account edit?"),T("تعديل الحساب","Account edit"),MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
        leaveAccountEditor=Discard;
        void Refresh(long? selected,bool reload=false)
        {
            selecting=true;if(reload){snapshot=S.AccountOutline(vm.Arabic);entries=snapshot.ToDictionary(x=>x.Account.Id);children=snapshot.Where(x=>x.Account.ParentId!=null).ToLookup(x=>x.Account.ParentId!.Value);}
            table.ItemsSource=Rows().DefaultView;table.SelectedItem=((DataView)table.ItemsSource).Cast<DataRowView>().FirstOrDefault(r=>(long)r["id"]==selected);selectedAccountId=table.SelectedItem is DataRowView row?(long)row["id"]:null;selecting=false;Details();
            if(table.SelectedItem!=null)table.ScrollIntoView(table.SelectedItem);hint.Text=T($"{snapshot.Count} حساب · {table.Items.Count} معروض",$"{snapshot.Count} accounts · {table.Items.Count} shown");
        }
        void SaveEdit()
        {
            if(editing is not long id)return;var before=S.AccountEdit(id);S.EditAccount(id,editCode!.Text,editName!.Text,vm.Arabic,(editParent!.SelectedItem as PositionChoice)?.Id);lastAccountChange=new(before,S.AccountEdit(id));undo.IsEnabled=true;editing=null;Refresh(id,true);hint.Text=vm.Status=T("تم حفظ التعديل. يمكنك التراجع.","Account saved. Undo is available.");
        }
        void Details()
        {
            detail.Children.Clear();editorActions.Children.Clear();editName=null;editCode=null;editParent=null;editing=selectedAccountId;
            if(selectedAccountId is not long id){detail.Children.Add(Text(T("اختر حسابًا لعرض تفاصيله","Select an account to view its details"),18));return;}
            var entry=entries[id];var account=entry.Account;var breadcrumb=Text(Breadcrumb(id),16);breadcrumb.Foreground=muted;detail.Children.Add(breadcrumb);detail.Children.Add(Text(T(organize?"تعديل الحساب":entry.DisplayName,organize?"Edit account":entry.DisplayName),22,true));
            if(organize)
            {
                editName=Input(entry.DisplayName);editCode=Input(account.Code,true);editCode.IsEnabled=!account.System;
                editParent=new ComboBox{DisplayMemberPath="Label",SelectedValuePath="Id",ItemsSource=new[]{new PositionChoice(null,T("المستوى الرئيسي","Top level"))}.Concat(snapshot.Where(x=>x.Account.Kind==account.Kind&&!Family(id).Contains(x.Account.Id)).Select(x=>new PositionChoice(x.Account.Id,Breadcrumb(x.Account.Id)))).ToList()};editParent.SelectedValue=account.ParentId;if(editParent.SelectedIndex<0)editParent.SelectedIndex=0;
                detail.Children.Add(Field(T("اسم الحساب","Account name"),editName));detail.Children.Add(Field(T("كود الحساب","Account code"),editCode));detail.Children.Add(Field(T("الحساب الرئيسي","Parent account"),editParent));detail.Children.Add(Text(AccountKind(account.Kind),16));
                editorActions.Children.Add(Btn(T("حفظ التعديل","Save changes"),SaveEdit,true));detail.Children.Add(Btn(T("+ حساب فرعي","+ Child account"),()=>{if(Discard()){editing=null;AccountForm(null,id,"organize-accounts");}}));
            }
            else{detail.Children.Add(Text(account.Code+" · "+AccountKind(account.Kind),16));var edit=Btn(T("تعديل الحساب","Edit account"),()=>AccountForm(account));edit.IsEnabled=editable;detail.Children.Add(Actions(edit,Btn(T("الأسماء والعناوين","Names & addresses"),()=>BilingualForm("accounts",id))));}
            detail.Children.Add(Text(T("رصيد الحساب فقط","Own balance"),16));detail.Children.Add(Text(Store.Money(entry.Own)+" JOD",22,true));detail.Children.Add(Text(T("مع جميع الفروع","Including all children"),16));detail.Children.Add(Text(Store.Money(entry.Total)+" JOD",22,true));
            if(organize)detail.Children.Add(Text(T("تُحفظ التغييرات عند الإفلات أو الضغط على حفظ. التراجع متاح لآخر نقل أو تعديل.","Drops and saved edits take effect immediately. Undo restores the last move or edit."),16));
        }
        void Move(long id,long? parent)
        {
            if(!ValidMove(id,parent,out var reason)){hint.Text=reason;return;}if(!Discard())return;var before=S.AccountEdit(id);S.MoveAccount(id,parent);lastAccountChange=new(before,S.AccountEdit(id));undo.IsEnabled=true;if(parent!=null)collapsedAccountGroups.Remove(parent.Value);editing=null;search.Text="";Refresh(id,true);hint.Text=vm.Status=T("تم نقل الحساب. يمكنك التراجع.","Account moved. Undo is available.");
        }
        undo.Command=new RelayCommand(()=>Guard(()=>{if(lastAccountChange is not AccountChange change||!Discard())return;S.UndoAccountEdit(change.Before,change.After);lastAccountChange=null;undo.IsEnabled=false;editing=null;for(long? id=change.Before.Parent;id!=null;id=entries[id.Value].Account.ParentId)collapsedAccountGroups.Remove(id.Value);search.Text="";Refresh(change.Before.Id,true);hint.Text=vm.Status=T("تم التراجع عن آخر تغيير","Last change undone");}));
        var outline=new FrameworkElementFactory(typeof(DockPanel));outline.SetBinding(FrameworkElement.MarginProperty,new Binding("[depth]"){Converter=new AccountIndent()});
        if(organize){var handle=new FrameworkElementFactory(typeof(Button));handle.SetValue(FrameworkElement.NameProperty,"AccountDragHandle");handle.SetValue(Button.ContentProperty,"⠿");handle.SetValue(Control.MinWidthProperty,24.0);handle.SetValue(FrameworkElement.WidthProperty,24.0);handle.SetValue(Control.PaddingProperty,new Thickness(0));handle.SetValue(FrameworkElement.MarginProperty,new Thickness(0));handle.SetValue(Control.BorderThicknessProperty,new Thickness(0));handle.SetValue(Control.BackgroundProperty,Brushes.Transparent);handle.SetValue(FrameworkElement.CursorProperty,Cursors.SizeAll);handle.SetValue(DockPanel.DockProperty,Dock.Left);handle.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,T("اسحب الحساب","Drag account"));outline.AppendChild(handle);}
        var toggle=new FrameworkElementFactory(typeof(Button));toggle.SetValue(FrameworkElement.NameProperty,"AccountDisclosure");toggle.SetValue(FrameworkElement.WidthProperty,44.0);toggle.SetValue(Control.PaddingProperty,new Thickness(0));toggle.SetValue(FrameworkElement.MarginProperty,new Thickness(0));toggle.SetValue(Control.BorderThicknessProperty,new Thickness(0));toggle.SetValue(Control.BackgroundProperty,Brushes.Transparent);toggle.SetValue(DockPanel.DockProperty,Dock.Left);toggle.SetBinding(UIElement.VisibilityProperty,new Binding("[expand]"){Converter=new DisclosureVisibility()});toggle.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,T("طي أو فتح الفروع","Collapse or expand children"));
        var chevron=new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));chevron.SetBinding(System.Windows.Shapes.Path.DataProperty,new Binding("[toggle]"){Converter=new DisclosureGeometry(vm.Arabic)});chevron.SetValue(Shape.StrokeProperty,muted);chevron.SetValue(Shape.StrokeThicknessProperty,2.0);chevron.SetValue(Shape.StrokeStartLineCapProperty,PenLineCap.Round);chevron.SetValue(Shape.StrokeEndLineCapProperty,PenLineCap.Round);chevron.SetValue(Shape.StrokeLineJoinProperty,PenLineJoin.Round);chevron.SetValue(FrameworkElement.WidthProperty,18.0);chevron.SetValue(FrameworkElement.HeightProperty,18.0);chevron.SetValue(Shape.StretchProperty,Stretch.Uniform);chevron.SetValue(UIElement.IsHitTestVisibleProperty,false);toggle.AppendChild(chevron);
        toggle.AddHandler(Button.ClickEvent,new RoutedEventHandler((s,e)=>{if(((Button)s).DataContext is DataRowView row&&Discard()){long id=(long)row["id"];if(!collapsedAccountGroups.Add(id))collapsedAccountGroups.Remove(id);editing=null;Refresh(id);}e.Handled=true;}));outline.AppendChild(toggle);
        var name=new FrameworkElementFactory(typeof(TextBlock));name.SetBinding(TextBlock.TextProperty,new Binding("[name]"));name.SetBinding(FrameworkElement.ToolTipProperty,new Binding("[name]"));name.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);name.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);var labelStyle=new Style(typeof(TextBlock));var parent=new DataTrigger{Binding=new Binding("[expand]"),Value=true};parent.Setters.Add(new Setter(TextBlock.FontWeightProperty,FontWeights.SemiBold));labelStyle.Triggers.Add(parent);name.SetValue(FrameworkElement.StyleProperty,labelStyle);outline.AppendChild(name);
        table.Columns[0]=new DataGridTemplateColumn{Header=T("الحسابات","Accounts"),HeaderStyle=table.Columns[0].HeaderStyle,Width=new DataGridLength(3,DataGridLengthUnitType.Star),CellTemplate=new DataTemplate{VisualTree=outline}};
        table.SelectionChanged+=(s,e)=>{if(selecting)return;var next=table.SelectedItem is DataRowView row?(long?)row["id"]:null;if(!Discard()){selecting=true;table.SelectedItem=((DataView)table.ItemsSource).Cast<DataRowView>().FirstOrDefault(r=>(long)r["id"]==selectedAccountId);selecting=false;return;}selectedAccountId=next;Details();};
        var debounce=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};search.TextChanged+=(s,e)=>{debounce.Stop();debounce.Start();};debounce.Tick+=(s,e)=>{debounce.Stop();if(vm.Route!=(organize?"organize-accounts":"accounts"))return;if(Discard()){editing=null;Refresh(selectedAccountId);}};search.Unloaded+=(s,e)=>debounce.Stop();
        var tree=new DockPanel();var topLevel=new Border{Name="AccountTopLevelDrop",Child=Text(T("المستوى الرئيسي — اسحب هنا لإخراج الحساب من مجموعته","Top level — drop here to ungroup an account"),16),Background=UiTheme.Brush(Resources,"Brush.Window"),BorderBrush=UiTheme.Brush(Resources,"Brush.Divider"),BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(16,12,16,4),AllowDrop=organize};if(organize){DockPanel.SetDock(topLevel,Dock.Top);tree.Children.Add(topLevel);}tree.Children.Add(TableSurface(table,table));
        var treeSurface=new Border{Child=tree,Background=UiTheme.Brush(Resources,"Brush.Surface"),BorderBrush=UiTheme.Brush(Resources,"Brush.Divider"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10)};
        var editorScroll=new ScrollViewer{Content=detail,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};var editorLayout=new DockPanel();DockPanel.SetDock(editorActions,Dock.Bottom);editorLayout.Children.Add(editorActions);editorLayout.Children.Add(editorScroll);var editorSurface=Card(editorLayout);editorSurface.Margin=new Thickness(16,0,0,0);
        var content=new System.Windows.Controls.Grid();content.ColumnDefinitions.Add(new ColumnDefinition());content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(330)});content.RowDefinitions.Add(new RowDefinition());content.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});content.Children.Add(treeSurface);System.Windows.Controls.Grid.SetColumn(editorSurface,1);content.Children.Add(editorSurface);
        content.SizeChanged+=(s,e)=>{bool narrow=content.ActualWidth<620;content.ColumnDefinitions[1].Width=narrow?new GridLength(0):new GridLength(Math.Max(280,FontSize*13.5));System.Windows.Controls.Grid.SetColumn(editorSurface,narrow?0:1);System.Windows.Controls.Grid.SetRow(editorSurface,narrow?1:0);editorSurface.MaxHeight=narrow?Math.Max(140,content.ActualHeight*.45):double.PositiveInfinity;editorSurface.Margin=narrow?new Thickness(0,12,0,0):new Thickness(16,0,0,0);};
        var layout=new System.Windows.Controls.Grid{Margin=new Thickness(24,12,24,16)};layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition());layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.Margin=new Thickness(0);contentViewport.Content=null;var description=(TextBlock)body.Children[1];layout.SizeChanged+=(s,e)=>description.Visibility=layout.ActualWidth<900&&FontSize>20?Visibility.Collapsed:Visibility.Visible;layout.Children.Add(body);System.Windows.Controls.Grid.SetRow(content,1);layout.Children.Add(content);var footer=new DockPanel{Margin=new Thickness(0,10,0,0)};if(organize){DockPanel.SetDock(undo,Dock.Right);footer.Children.Add(undo);}footer.Children.Add(hint);System.Windows.Controls.Grid.SetRow(footer,2);layout.Children.Add(footer);workspaceViewport.Children.Clear();workspaceViewport.Children.Add(layout);workspaceTable=table;dataWorkspace=layout;
        if(organize)InstallAccountDragging(table,topLevel,scope,ValidMove,Move,()=>Refresh(selectedAccountId),hint);
        selectedAccountId??=snapshot.FirstOrDefault(x=>x.Account.Kind=="Expense"&&x.Account.ParentId==null)?.Account.Id??snapshot.FirstOrDefault()?.Account.Id;Refresh(selectedAccountId);table.SetValue(System.Windows.Automation.AutomationProperties.HelpTextProperty,T("شجرة الحسابات","Account tree"));table.ToolTip=null;
    }
    delegate bool AccountMoveValidator(long id,long? parent,out string reason);
    void InstallAccountDragging(DataGrid table,Border topLevel,Guid scope,AccountMoveValidator valid,Action<long,long?> move,Action refresh,TextBlock hint)
    {
        Point start=default;long? dragging=null;DataGridRow? highlighted=null;DateTime lastScroll=DateTime.MinValue,hoverSince=DateTime.MinValue;long? hover=null;
        void Clear(){highlighted?.ClearValue(Control.BackgroundProperty);highlighted?.ClearValue(Control.BorderBrushProperty);highlighted?.ClearValue(Control.BorderThicknessProperty);highlighted=null;topLevel.Background=UiTheme.Brush(Resources,"Brush.Window");}
        AccountDrag? Read(IDataObject data){if(!data.GetDataPresent(AccountDragFormat)||data.GetData(AccountDragFormat) is not string payload)return null;var parts=payload.Split('|');return parts.Length==2&&Guid.TryParse(parts[0],out var origin)&&origin==scope&&long.TryParse(parts[1],out long id)?new(origin,id):null;}
        table.PreviewMouseLeftButtonDown+=(s,e)=>{dragging=null;var handle=Ancestor<Button>(e.OriginalSource as DependencyObject);if(handle?.Name!="AccountDragHandle"||handle.DataContext is not DataRowView row)return;start=e.GetPosition(table);dragging=(long)row["id"];};
        table.PreviewMouseMove+=(s,e)=>{if(e.LeftButton!=MouseButtonState.Pressed){dragging=null;return;}if(dragging is not long id)return;var point=e.GetPosition(table);if(Math.Abs(point.X-start.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(point.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return;dragging=null;try{DragDrop.DoDragDrop(table,new DataObject(AccountDragFormat,scope.ToString("N")+"|"+id),DragDropEffects.Move);}finally{Clear();hover=null;}};
        void Over(DragEventArgs e,bool root)
        {
            Clear();e.Effects=DragDropEffects.None;e.Handled=true;var drag=Read(e.Data);var row=root?null:Ancestor<DataGridRow>(e.OriginalSource as DependencyObject);long? parent=row?.Item is DataRowView value?(long)value["id"]:null;if(drag==null)return;
            if(!root&&(DateTime.UtcNow-lastScroll).TotalMilliseconds>100){var point=e.GetPosition(table);var scroll=Descendants<ScrollViewer>(table).FirstOrDefault();if(point.Y<70)scroll?.LineUp();else if(point.Y>table.ActualHeight-40)scroll?.LineDown();lastScroll=DateTime.UtcNow;}
            if(!root&&row==null)return;if(!valid(drag.Id,parent,out string reason)){hint.Text=reason;return;}e.Effects=DragDropEffects.Move;hint.Text=root?T("اتركه هنا ليصبح حسابًا رئيسيًا","Drop here to make a main account"):T("اتركه هنا ليصبح فرعًا للحساب","Drop here to make a child account");
            if(row!=null){highlighted=row;row.Background=UiTheme.Brush(Resources,"Brush.Sunken");row.BorderBrush=accent;row.BorderThickness=new Thickness(0,1,0,1);if(hover!=parent){hover=parent;hoverSince=DateTime.UtcNow;}else if(parent!=null&&collapsedAccountGroups.Contains(parent.Value)&&(DateTime.UtcNow-hoverSince).TotalMilliseconds>650){collapsedAccountGroups.Remove(parent.Value);refresh();hoverSince=DateTime.UtcNow;}}
            else{hover=null;topLevel.Background=UiTheme.Brush(Resources,"Brush.Sunken");}
        }
        void Drop(DragEventArgs e,bool root){Clear();e.Handled=true;var drag=Read(e.Data);var row=root?null:Ancestor<DataGridRow>(e.OriginalSource as DependencyObject);if(drag==null||!root&&row==null)return;long? parent=row?.Item is DataRowView value?(long)value["id"]:null;Guard(()=>move(drag.Id,parent));}
        table.DragOver+=(s,e)=>Over(e,false);table.Drop+=(s,e)=>Drop(e,false);table.DragLeave+=(s,e)=>{Clear();hover=null;};topLevel.DragOver+=(s,e)=>Over(e,true);topLevel.Drop+=(s,e)=>Drop(e,true);topLevel.DragLeave+=(s,e)=>Clear();
    }
}
