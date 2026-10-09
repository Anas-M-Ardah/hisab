using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Hisab;

public sealed partial class MainWindow
{
    ScrollViewer contentViewport=null!;
    System.Windows.Controls.Grid workspaceViewport=null!;
    System.Windows.Controls.Grid? dataWorkspace;
    StackPanel? workspaceFooter;
    DataGrid? workspaceTable;
    DockPanel workspaceHost=null!;
    Border? pageActionFooter;

    void ArrangeSettingsFooter()
    {
        var save=body.Children.OfType<Button>().LastOrDefault();if(save==null)return;body.Children.Remove(save);
        var actions=Actions(save);actions.HorizontalAlignment=HorizontalAlignment.Right;
        pageActionFooter=new Border{BorderBrush=UiTheme.Brush(Resources,"Brush.Divider"),BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(24,4,24,4),Background=UiTheme.Brush(Resources,"Brush.Surface"),Child=actions};
        DockPanel.SetDock(pageActionFooter,Dock.Bottom);workspaceHost.Children.Insert(workspaceHost.Children.IndexOf(workspaceViewport),pageActionFooter);
    }

    void ArrangeDataWorkspace(string route)
    {
        if(route is not ("accounts" or "items" or "invoices" or "cheques" or "users" or "periods"))return;
        var table=body.Children.OfType<DataGrid>().FirstOrDefault();if(table==null)return;
        if(route=="invoices")
        {
            var groups=body.Children.OfType<WrapPanel>().ToList();var commands=groups.SelectMany(group=>group.Children.OfType<Button>()).ToList();
            foreach(var group in groups){foreach(var command in group.Children.OfType<Button>().ToList())group.Children.Remove(command);body.Children.Remove(group);}
            var open=commands.Single(command=>command.Content?.ToString()==T("فتح / طباعة","Open / print"));commands.Remove(open);
            var frequent=commands.Take(2).ToList();commands.RemoveRange(0,frequent.Count);var more=MoreActions(commands);
            body.Children.Add(Actions(new[]{open}.Concat(frequent).Append(more).ToArray()));BindSelection(table,new[]{open}.Concat(frequent).Append(more).ToArray());
        }
        if(route=="items")foreach(var group in body.Children.OfType<WrapPanel>())BindSelection(table,group.Children.OfType<Button>().ToArray());
        int index=body.Children.IndexOf(table);var trailing=body.Children.Cast<UIElement>().Skip(index+1).ToList();
        body.Children.Remove(table);var footer=new StackPanel();
        foreach(var item in trailing){body.Children.Remove(item);if(item is TextBlock text){text.FontSize=Math.Max(14,FontSize*.75);text.Foreground=muted;footer.Children.Add(text);}else body.Children.Add(item);}
        contentViewport.Content=null;body.Margin=new Thickness(0);footer.Margin=new Thickness(0,10,0,0);
        var layout=new System.Windows.Controls.Grid{Margin=new Thickness(24,12,24,20)};
        layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        layout.Children.Add(body);table.MaxHeight=double.PositiveInfinity;table.MinHeight=0;
        var content=new System.Windows.Controls.Grid();content.Children.Add(table);
        var empty=new TextBlock{Text=T("لا توجد سجلات لعرضها. غيّر البحث أو أضف سجلًا.","No records to show. Change the search or add a record."),Foreground=muted,FontSize=Math.Max(14,FontSize*.9),TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(24),IsHitTestVisible=false};
        var emptyStyle=new Style(typeof(TextBlock));emptyStyle.Setters.Add(new Setter(VisibilityProperty,Visibility.Collapsed));var noRows=new DataTrigger{Binding=new System.Windows.Data.Binding(nameof(ItemsControl.HasItems)){Source=table},Value=false};noRows.Setters.Add(new Setter(VisibilityProperty,Visibility.Visible));emptyStyle.Triggers.Add(noRows);empty.Style=emptyStyle;content.Children.Add(empty);
        var surface=new Border{Background=UiTheme.Brush(Resources,"Brush.Surface"),BorderBrush=UiTheme.Brush(Resources,"Brush.Divider"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),ClipToBounds=true,Child=TableSurface(table,content)};
        surface.SizeChanged+=(s,e)=>surface.Clip=new RectangleGeometry(new Rect(0,0,surface.ActualWidth,surface.ActualHeight),10,10);
        System.Windows.Controls.Grid.SetRow(surface,1);layout.Children.Add(surface);System.Windows.Controls.Grid.SetRow(footer,2);layout.Children.Add(footer);
        dataWorkspace=layout;workspaceFooter=footer;workspaceTable=table;workspaceViewport.Children.Clear();workspaceViewport.Children.Add(layout);
    }
    DataGridColumn TableBadgeColumn(string field,string label,double width,Style header)
    {
        var badge=new FrameworkElementFactory(typeof(Border));badge.SetValue(Border.CornerRadiusProperty,new CornerRadius(5));badge.SetValue(Border.PaddingProperty,new Thickness(9,4,9,4));badge.SetValue(Border.MarginProperty,new Thickness(16,0,16,0));badge.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Left);badge.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
        var chrome=new Style(typeof(Border));chrome.Setters.Add(new Setter(Border.BackgroundProperty,UiTheme.Brush(Resources,"Brush.Window")));var selected=new DataTrigger{Binding=new System.Windows.Data.Binding(nameof(DataGridRow.IsSelected)){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(DataGridRow),1)},Value=true};selected.Setters.Add(new Setter(Border.BackgroundProperty,Brushes.Transparent));chrome.Triggers.Add(selected);badge.SetValue(FrameworkElement.StyleProperty,chrome);
        var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("["+field+"]"));text.SetBinding(TextBlock.ToolTipProperty,new System.Windows.Data.Binding("["+field+"]"));text.SetValue(TextBlock.FontSizeProperty,Math.Max(13,FontSize*.75));text.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);badge.AppendChild(text);
        return new DataGridTemplateColumn{Header=label,HeaderStyle=header,SortMemberPath=field,Width=new DataGridLength(width,DataGridLengthUnitType.Star),CellTemplate=new DataTemplate{VisualTree=badge}};
    }
    FrameworkElement TableSurface(DataGrid table,FrameworkElement content)
    {
        var layout=new DockPanel();var count=new TextBlock{Foreground=muted,FontSize=Math.Max(13,FontSize*.7),Margin=new Thickness(16,10,16,10)};
        void Refresh(){count.Text=T($"{table.Items.Count} سجل",$"{table.Items.Count} records")+(table.SelectedItem==null?"":T(" · سجل محدد"," · 1 selected"));}
        ((System.Collections.Specialized.INotifyCollectionChanged)table.Items).CollectionChanged+=(s,e)=>Refresh();table.SelectionChanged+=(s,e)=>Refresh();Refresh();
        var footer=new Border{Background=UiTheme.Brush(Resources,"Brush.Surface"),BorderBrush=UiTheme.Brush(Resources,"Brush.Divider"),BorderThickness=new Thickness(0,1,0,0),Child=count};DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);layout.Children.Add(content);return layout;
    }

    DataGrid PageTable()=>workspaceTable??body.Children.OfType<DataGrid>().Single();
    FrameworkElement SearchActions(string label,TextBox search,params Button[] actions)
    {
        var bar=new DockPanel();var commands=Actions(actions);commands.VerticalAlignment=VerticalAlignment.Bottom;commands.Margin=new Thickness(20,0,0,8);DockPanel.SetDock(commands,Dock.Right);bar.Children.Add(commands);bar.Children.Add(Field(label,search));
        bar.SizeChanged+=(s,e)=>{bool narrow=bar.ActualWidth<760;DockPanel.SetDock(commands,narrow?Dock.Bottom:Dock.Right);commands.Margin=narrow?new Thickness(0,0,0,8):new Thickness(20,0,0,8);};return bar;
    }
    void BindSelection(DataGrid table,params Button[] actions)
    {
        void Refresh(){foreach(var action in actions)action.IsEnabled=table.SelectedItem!=null;}
        table.SelectionChanged+=(s,e)=>Refresh();Refresh();
    }
    StackPanel Section(string title)=>new(){Children={Text(title,21,true)},Margin=new Thickness(0,0,0,8)};
    Button MoreActions(IEnumerable<Button> commands)
    {
        var menu=new ContextMenu{Resources=Resources,FontSize=FontSize,FlowDirection=FlowDirection,Background=UiTheme.Brush(Resources,"Brush.Surface"),Foreground=ink};
        foreach(var command in commands)menu.Items.Add(new MenuItem{Header=command.Content,Command=command.Command,Padding=new Thickness(14,8,14,8)});
        var more=Btn(T("المزيد…","More…"),()=>menu.IsOpen=true);menu.PlacementTarget=more;menu.Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom;more.ContextMenu=menu;return more;
    }
    System.Windows.Controls.Primitives.UniformGrid FormColumns()
    {
        var fields=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};fields.SizeChanged+=(s,e)=>fields.Columns=fields.ActualWidth<600?1:2;return fields;
    }
    void FoldSection(string title,Action build)
    {
        int before=body.Children.Count;build();var content=new StackPanel();foreach(var child in body.Children.Cast<UIElement>().Skip(before).ToList()){body.Children.Remove(child);content.Children.Add(child);}
        body.Children.Add(new Expander{Header=title,Content=content,Margin=new Thickness(0,12,0,12),Foreground=ink,IsExpanded=false});
    }

    void ArrangeDialog(Window window,StackPanel fields,string title)
    {
        // Forms retain their existing commands and draft guard while chrome stays visible.
        if(fields.Children.Count>0&&fields.Children[0] is TextBlock heading&&heading.Text==title)fields.Children.RemoveAt(0);
        DockPanel layout;
        if(window.Content is DockPanel existing)layout=existing;
        else if(window.Content is ScrollViewer scroll){window.Content=null;layout=new DockPanel();layout.Children.Add(scroll);window.Content=layout;
            var submit=fields.Children.OfType<Button>().LastOrDefault();if(submit!=null){fields.Children.Remove(submit);var actions=Actions(submit);actions.HorizontalAlignment=HorizontalAlignment.Right;var footer=new Border{Background=UiTheme.Brush(Resources,"Brush.Surface"),BorderBrush=UiTheme.Brush(Resources,"Brush.Divider"),BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(24,8,24,8),Child=actions};DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Insert(0,footer);}}
        else return;
        var header=new DockPanel{Margin=new Thickness(24,18,24,10)};var cancel=Btn(T("إلغاء","Cancel"),window.Close);cancel.Margin=new Thickness(16,0,0,0);DockPanel.SetDock(cancel,Dock.Right);header.Children.Add(cancel);var label=Text(title,24,true);label.VerticalAlignment=VerticalAlignment.Center;label.Margin=new Thickness(0);header.Children.Add(label);DockPanel.SetDock(header,Dock.Top);layout.Children.Insert(0,header);
        window.MaxWidth=Math.Max(500,SystemParameters.WorkArea.Width-32);window.UseLayoutRounding=true;
    }
}
