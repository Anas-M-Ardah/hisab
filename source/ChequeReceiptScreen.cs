using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace Hisab;
public sealed partial class MainWindow
{
    sealed record ChequeDraft(ReceiptCheque Cheque){public string Number=>Cheque.Number;public string Bank=>Cheque.Bank;public string Amount=>Store.Money(Store.M(Cheque.Amount));public string Due=>Cheque.Due.ToString("yyyy-MM-dd");}
    void ChequeReceiptForm()
    {
        var (w,p,finish)=Dialog(T("قبض بشيكات متعددة","Receive multiple cheques"),980);
        var date=Date();var party=Choose(UiAccounts().Where(a=>a.Kind!="Unclassified"));var total=Input("",true);var note=Input();
        var header=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};foreach(var field in new[]{Field(T("تاريخ القبض","Receipt date"),date),Field(T("المقبوض من / الحساب","Received from / account"),party),Field(T("إجمالي مبلغ القبض JOD","Receipt total JOD"),total),Field(T("البيان","Description"),note)}){field.Margin=new(0,0,12,12);header.Children.Add(field);}p.Children.Add(header);
        var allocation=new ComboBox();void Allocations(){allocation.ItemsSource=new[]{new PositionChoice(null,T("دفعة على الحساب","Payment on account"))}.Concat(party.SelectedItem is Account a?A.Positions(DateTime.Today,a.Id,"SI").Where(i=>i.Outstanding>0).Select(i=>new PositionChoice(i.Invoice,i.Number+" · "+Store.Money(i.Outstanding))):Enumerable.Empty<PositionChoice>()).ToList();allocation.SelectedIndex=0;}party.SelectionChanged+=(s,e)=>Allocations();Allocations();p.Children.Add(Field(T("تخصيص لفاتورة (اختياري)","Allocate to invoice (optional)"),allocation));
        p.Children.Add(Text(T("أدخل كل شيك وتاريخ استحقاقه. يمكن تحصيل أو إرجاع كل شيك بشكل مستقل.","Add each cheque and its due date. Each cheque can clear or bounce independently."),18));
        var number=Input();var bank=Input();var amount=Input("",true);var due=new DatePicker{SelectedDate=DateTime.Today};
        var fields=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};foreach(var f in new[]{Field(T("رقم الشيك","Cheque number"),number),Field(T("البنك","Bank"),bank),Field(T("مبلغ الشيك JOD","Cheque amount JOD"),amount),Field(T("تاريخ الاستحقاق","Due date"),due)}){f.Margin=new(0,0,12,12);fields.Children.Add(f);}p.Children.Add(fields);
        var drafts=new ObservableCollection<ChequeDraft>();var grid=new DataGrid{ItemsSource=drafts,Height=200,FontSize=FontSize*.9};
        foreach(var c in new[]{("Number",T("الرقم","Number")),("Bank",T("البنك","Bank")),("Amount",T("المبلغ JOD","Amount JOD")),("Due",T("الاستحقاق","Due date"))})grid.Columns.Add(new DataGridTextColumn{Header=c.Item2,Binding=new Binding(c.Item1),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
        var summary=Text("",20,true);void Update()=>summary.Text=T("مجموع الشيكات: ","Cheque total: ")+Store.Money(Store.M(drafts.Sum(d=>d.Cheque.Amount)))+" JOD";
        p.Children.Add(Btn(T("+ إضافة الشيك","+ Add cheque"),()=>{decimal value=Store.Parse(amount.Text);if(string.IsNullOrWhiteSpace(number.Text)||string.IsNullOrWhiteSpace(bank.Text)||value<=0||Store.D(Store.M(value))!=value||GetDate(due).Date<GetDate(date).Date)throw new InvalidOperationException(T("أكمل بيانات الشيك وتحقق من المبلغ والتاريخ","Complete cheque details and check amount and date"));if(drafts.Any(d=>d.Number==number.Text.Trim()&&d.Bank==bank.Text.Trim()))throw new InvalidOperationException(T("هذا الشيك مضاف بالفعل","This cheque is already added"));drafts.Add(new(new(number.Text.Trim(),bank.Text.Trim(),value,GetDate(due))));number.Clear();amount.Clear();Update();}));
        p.Children.Add(grid);p.Children.Add(Btn(T("إزالة الشيك المحدد","Remove selected cheque"),()=>{if(grid.SelectedItem is ChequeDraft selected){drafts.Remove(selected);Update();}}));p.Children.Add(summary);Update();
        p.Children.Add(Btn(T("مراجعة وحفظ جميع الشيكات","Review & save all cheques"),()=>{
            if(!string.IsNullOrWhiteSpace(number.Text)||!string.IsNullOrWhiteSpace(amount.Text))throw new InvalidOperationException(T("أضف الشيك الجاري أولًا أو امسح رقمه ومبلغه","Add the current cheque first or clear its number and amount"));
            var account=GetAccount(party);decimal value=Store.Parse(total.Text);if(drafts.Count==0||drafts.Sum(d=>d.Cheque.Amount)!=value)throw new InvalidOperationException(T("مجموع الشيكات يجب أن يساوي مبلغ القبض","Cheque total must equal the receipt amount"));
            if(Confirm(account.Name+"\n"+summary.Text+"\n"+string.Join("\n",drafts.Select(d=>d.Number+" · "+d.Bank+" · "+d.Amount+" · "+d.Due)))){var ids=A.ReceiveCheques(GetDate(date),account.Id,value,note.Text,drafts.Select(d=>d.Cheque).ToList(),(allocation.SelectedItem as PositionChoice)?.Id);finish();Navigate("cheques");vm.Status=T("تم حفظ الشيكات: ","Cheques saved: ")+ids.Count;}
        },true));w.ShowDialog();
    }
}
