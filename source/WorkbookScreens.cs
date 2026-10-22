using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
namespace Hisab;

public sealed partial class MainWindow
{
    FrameworkElement AccountPicker(ComboBox account)
    {
        var all=((IEnumerable<Account>)account.ItemsSource).ToList();var parents=UiAccounts().Where(a=>all.Any(c=>c.ParentId==a.Id)).ToList();
        if(parents.Count==0)return account;
        var panel=new StackPanel();var group=new ComboBox{ItemsSource=new[]{new PositionChoice(null,T("كل الحسابات الرئيسية","All main accounts"))}.Concat(parents.Select(a=>new PositionChoice(a.Id,a.ToString()))).ToList(),SelectedIndex=0,Margin=new(0,0,0,8)};
        System.Windows.Automation.AutomationProperties.SetName(group,T("تصفية حسب الحساب الرئيسي","Filter by main account"));
        group.SelectionChanged+=(s,e)=>{long? id=(group.SelectedItem as PositionChoice)?.Id;var selected=account.SelectedItem as Account;var family=id==null?null:S.AccountFamily(id.Value).ToHashSet();var filtered=all.Where(a=>family==null||family.Contains(a.Id)&&a.Id!=id).ToList();account.ItemsSource=filtered;account.SelectedItem=filtered.FirstOrDefault(a=>a.Id==selected?.Id);};
        account.SelectionChanged+=(s,e)=>{if(account.SelectedItem is Account a&&(group.SelectedItem as PositionChoice)?.Id is long id&&!S.AccountFamily(id).Contains(a.Id))group.SelectedIndex=0;};
        panel.Children.Add(group);panel.Children.Add(account);return panel;
    }
    sealed record AllocationDraft(long Account,string Name,decimal Amount,string Description)
    {public string AmountText=>Store.Money(Store.M(Amount));}
    void SplitVoucherForm(bool receipt,long? editing=null)
    {
        var (w,p,finish)=Dialog(T(receipt?"سند قبض موزع":"سند صرف موزع",receipt?"Split receipt":"Split payment"),980);
        var date=Date();var cash=Choose(UiAccounts().Where(a=>a.Kind=="Cash"));cash.SelectedIndex=0;var note=Input();var reason=Input();
        var top=FormColumns();top.Children.Add(Field(T("التاريخ","Date"),date));top.Children.Add(Field(T("الصندوق / البنك","Cash / bank"),cash));p.Children.Add(top);p.Children.Add(Field(T("البيان العام","Voucher description"),note));
        
        var lines=new ObservableCollection<AllocationDraft>();var grid=new DataGrid{ItemsSource=lines,Height=190};
        foreach(var c in new[]{("Name",T("الحساب","Account"),2.0),("Description",T("البيان","Description"),3.0),("AmountText",T("المبلغ JOD","Amount JOD"),1.0)})grid.Columns.Add(new DataGridTextColumn{Header=c.Item2,Binding=new Binding(c.Item1),Width=new DataGridLength(c.Item3,DataGridLengthUnitType.Star)});
        var account=Choose(UiAccounts().Where(a=>a.Kind!="Unclassified"&&a.Code!="1301"));var amount=Input("",true);var description=Input();AllocationDraft? current=null;
        var editor=new StackPanel();var expand=new Border{Child=editor,Visibility=Visibility.Collapsed,Margin=new(0,8,0,12)};editor.Children.Add(Field(T("الحساب","Account"),account));var fields=FormColumns();fields.Children.Add(Field(T("المبلغ JOD","Amount JOD"),amount));fields.Children.Add(Field(T("بيان السطر","Line description"),description));editor.Children.Add(fields);
        var total=Text("",20,true);void Refresh()=>total.Text=T("الإجمالي: ","Total: ")+Store.Money(Store.M(lines.Sum(l=>l.Amount)))+" JOD";
        editor.Children.Add(Btn(T("اعتماد السطر","Apply line"),()=>{var selected=GetAccount(account);decimal value=Store.Parse(amount.Text);if(value<=0||Store.D(Store.M(value))!=value||string.IsNullOrWhiteSpace(description.Text))throw new InvalidOperationException(T("أدخل مبلغًا موجبًا وبيانًا","Enter a positive amount and description"));var line=new AllocationDraft(selected.Id,selected.Name,value,description.Text.Trim());if(current!=null&&lines.Contains(current))lines[lines.IndexOf(current)]=line;else lines.Add(line);current=null;amount.Clear();description.Clear();expand.Visibility=Visibility.Collapsed;Refresh();}));
        p.Children.Add(Btn(T("+ إضافة / تعديل سطر","+ Add / edit line"),()=>expand.Visibility=expand.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible));StyleEditorGrid(grid);p.Children.Add(expand);p.Children.Add(grid);p.Children.Add(Actions(Btn(T("تعديل المحدد","Edit selected"),()=>{if(grid.SelectedItem is AllocationDraft l){current=l;expand.Visibility=Visibility.Visible;account.ItemsSource=UiAccounts().Where(a=>a.Kind!="Unclassified"&&a.Code!="1301").ToList();account.SelectedItem=account.Items.Cast<Account>().Single(a=>a.Id==l.Account);amount.Text=l.Amount.ToString(CultureInfo.InvariantCulture);description.Text=l.Description;}}),Btn(T("حذف المحدد","Remove selected"),()=>{if(grid.SelectedItem is AllocationDraft l){lines.Remove(l);if(current==l){current=null;amount.Clear();description.Clear();}Refresh();}})));
        
        if(editing!=null){var row=S.Table("SELECT * FROM documents WHERE id=@p0",editing).Rows[0];date.SelectedDate=DateTime.Parse((string)row["date"]);cash.SelectedItem=cash.Items.Cast<Account>().Single(a=>a.Id==(long)row["cash"]);note.Text=(string)row["note"];foreach(DataRow line in S.Table("SELECT * FROM entries WHERE doc=@p0 AND account!=@p1 ORDER BY id",editing,row["cash"]).Rows)lines.Add(new((long)line["account"],S.LocalName("accounts",(long)line["account"],vm.Arabic),Store.D((long)line[receipt?"credit":"debit"]),(string)line["description"]));p.Children.Add(Field(T("سبب التعديل","Amendment reason"),reason));}
        p.Children.Add(total);Refresh();p.Children.Add(Btn(T("مراجعة وحفظ","Review & save"),()=>{if(amount.Text!=""||description.Text!="")throw new InvalidOperationException(T("اعتمد السطر الجاري أولًا","Apply the current line first"));if(lines.Count==0)throw new InvalidOperationException(T("أضف سطرًا أولًا","Add a line first"));if(Confirm(total.Text+"\n"+string.Join("\n",lines.Select(l=>l.Name+" · "+l.AmountText+" · "+l.Description)))){var allocations=lines.Select(l=>new VoucherAllocation(l.Account,l.Amount,l.Description)).ToList();Saved(editing==null?A.SplitVoucher(receipt,GetDate(date),GetAccount(cash).Id,note.Text,allocations):A.AmendSplitVoucher(editing.Value,GetDate(date),GetAccount(cash).Id,note.Text,allocations,reason.Text),finish);}},true));PinWorkflowTotal(w,p,total);w.ShowDialog();
    }
    sealed record TransferDraft(StockTransferLine Line,string Name)
    {public string Quantity=>Line.Quantity.ToString("0.###",CultureInfo.InvariantCulture);public string Cost=>Store.Money(Store.M(Line.Cost));}
    void OrderTransferForm()
    {
        var (w,p,finish)=Dialog(T("تحويل طلبية إلى المخزون","Transfer order to stock"),980);var date=Date();var order=Choose(UiAccounts().Where(a=>a.Kind=="Asset"&&a.ParentId!=null&&!a.System&&!S.Accounts().Any(c=>c.ParentId==a.Id)));var note=Input();
        p.Children.Add(Text(T("سجّل تكاليف الطلبية في حساب فرعي من نوع أصول، ثم حوّل تكلفة الأصناف المستلمة إلى المخزون.","Accumulate order costs in a child asset account, then transfer the received items and their costs into stock."),16));
        var fields=FormColumns();fields.Children.Add(Field(T("التاريخ","Date"),date));fields.Children.Add(Field(T("حساب الطلبية","Order account"),order));p.Children.Add(fields);p.Children.Add(Field(T("البيان","Description"),note));
        var balance=Text("",18,true);order.SelectionChanged+=(s,e)=>balance.Text=order.SelectedItem is Account a?T("الرصيد المتاح اليوم: ","Available balance today: ")+Store.Money(S.Balance(a.Id))+" JOD":"";p.Children.Add(balance);
        var item=Choose(UiItems().Where(i=>i.Stock));var qty=Input("",true);var cost=Input("",true);var editor=new StackPanel();var expand=new Border{Child=editor,Visibility=Visibility.Collapsed,Margin=new(0,8,0,12)};editor.Children.Add(Field(T("الصنف","Item"),item));var amounts=FormColumns();amounts.Children.Add(Field(T("الكمية الواردة","Incoming quantity"),qty));amounts.Children.Add(Field(T("التكلفة الإجمالية للصنف JOD","Total item cost JOD"),cost));editor.Children.Add(amounts);
        var lines=new ObservableCollection<TransferDraft>();var grid=new DataGrid{ItemsSource=lines,Height=180};foreach(var c in new[]{("Name",T("الصنف","Item")),("Quantity",T("الوارد","In")),("Cost",T("التكلفة JOD","Cost JOD"))})grid.Columns.Add(new DataGridTextColumn{Header=c.Item2,Binding=new Binding(c.Item1),Width=new DataGridLength(c.Item1=="Name"?3:1,DataGridLengthUnitType.Star)});
        var total=Text("",20,true);void Refresh()=>total.Text=T("إجمالي التحويل: ","Transfer total: ")+Store.Money(Store.M(lines.Sum(l=>l.Line.Cost)))+" JOD";
        editor.Children.Add(Btn(T("إضافة الصنف","Add item"),()=>{var selected=GetItem(item);decimal q=Store.Parse(qty.Text),v=Store.Parse(cost.Text);if(q<=0||v<=0||Store.D(Store.M(q))!=q||Store.D(Store.M(v))!=v||lines.Any(l=>l.Line.Item==selected.Id))throw new InvalidOperationException(T("تحقق من الكمية والتكلفة وتكرار الصنف","Check quantity, cost and duplicate item"));lines.Add(new(new(selected.Id,q,v),selected.Name));qty.Clear();cost.Clear();expand.Visibility=Visibility.Collapsed;Refresh();}));p.Children.Add(Btn(T("+ إضافة صنف","+ Add item"),()=>expand.Visibility=expand.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible));StyleEditorGrid(grid);p.Children.Add(expand);p.Children.Add(grid);p.Children.Add(Btn(T("إزالة المحدد","Remove selected"),()=>{if(grid.SelectedItem is TransferDraft l)lines.Remove(l);Refresh();}));p.Children.Add(total);Refresh();
        p.Children.Add(Btn(T("مراجعة وتحويل","Review & transfer"),()=>{if(qty.Text!=""||cost.Text!="")throw new InvalidOperationException(T("أضف الصنف الجاري أولًا","Add the current item first"));if(Confirm(GetAccount(order).Name+"\n"+total.Text))Saved(A.TransferOrderToStock(GetDate(date),GetAccount(order).Id,note.Text,lines.Select(l=>l.Line).ToList()),finish);},true));PinWorkflowTotal(w,p,total);w.ShowDialog();
    }
    void PinWorkflowTotal(Window window,StackPanel panel,TextBlock total)
    {
        var save=panel.Children.OfType<Button>().Last();panel.Children.Remove(save);panel.Children.Remove(total);
        var footer=new StackPanel{Margin=new(24,8,24,12)};footer.Children.Add(total);footer.Children.Add(Actions(save));
        var scroll=(ScrollViewer)window.Content;window.Content=null;var layout=new DockPanel();DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);layout.Children.Add(scroll);window.Content=layout;
        foreach(var fields in Descendants<System.Windows.Controls.Primitives.UniformGrid>(panel))foreach(FrameworkElement field in fields.Children)field.Margin=new(6,0,6,12);
        if(previewDirectory!=null)window.Loaded+=(s,e)=>window.Dispatcher.BeginInvoke(()=>{
            window.UpdateLayout();var bounds=save.TransformToAncestor(window).TransformBounds(new Rect(0,0,save.ActualWidth,save.ActualHeight));
            bool visible=bounds.Bottom<=window.ActualHeight&&bounds.Top>=0&&save.ActualWidth>0;
            bool headers=Descendants<System.Windows.Controls.Primitives.DataGridColumnHeader>(panel).Where(h=>h.Content!=null).All(h=>h.ActualHeight>=h.DesiredSize.Height);
            System.IO.File.AppendAllText(System.IO.Path.Combine(previewDirectory!,"ui-checks.txt"),(vm.Arabic?"ar":"en")+": workflow save visible="+visible+", table headers fit="+headers+", font="+FontSize+Environment.NewLine);
            if(!visible||!headers)throw new InvalidOperationException("Workflow layout check failed");
        },System.Windows.Threading.DispatcherPriority.Loaded);
    }
    void AddChequeReceipt(FlowDocument doc,long id)
    {
        var group=S.Scalar("SELECT receipt_group FROM documents WHERE id=@p0",id);if(group is null or DBNull)return;
        var cheques=S.Table("SELECT c.number,c.bank,c.due_date,c.amount,c.state FROM cheques c JOIN documents d ON d.id=c.issue_doc WHERE d.receipt_group=@p0 ORDER BY c.id",group);
        Paragraph(doc,T("سند قبض مجمع / مرجع: ","Combined receipt / reference: ")+S.Scalar("SELECT number FROM documents WHERE id=@p0",group),true);
        PrintTable(doc,new[]{T("رقم الشيك","Cheque number"),T("البنك","Bank"),T("الاستحقاق","Due date"),T("المبلغ JOD","Amount JOD"),T("الحالة","Status")},cheques.Rows.Cast<DataRow>().Select(r=>new[]{(string)r["number"],(string)r["bank"],(string)r["due_date"],Store.Money((long)r["amount"]),r["state"].ToString() switch{"Pending"=>T("قيد الانتظار","Pending"),"Cleared"=>T("محصل","Cleared"),"Bounced"=>T("مرتجع","Bounced"),"Cancelled"=>T("ملغى","Cancelled"),_=>r["state"].ToString()!}}));
        Paragraph(doc,T("إجمالي الشيكات عند القبض: ","Total cheques at receipt: ")+Store.Money(cheques.Rows.Cast<DataRow>().Sum(r=>(long)r["amount"]))+" JOD",true);
    }
}
