using System.Data;
using System.Windows.Controls;
namespace Hisab;
public sealed partial class MainWindow
{
    DataTable CashFlowDetails(DateTime from,DateTime to)
    {
        var table=new DataTable();foreach(string header in new[]{T("التاريخ / المستند","Date / document"),T("الحساب النقدي","Cash account"),T("الحركة JOD","Movement JOD"),T("تشغيلية JOD","Operating JOD"),T("استثمارية JOD","Investing JOD"),T("تمويلية JOD","Financing JOD"),T("تحويل داخلي JOD","Internal transfer JOD"),T("غير مصنف JOD","Unclassified JOD"),T("الحالة","Status")})table.Columns.Add(header);
        var movements=A.CashMovements(from,to);var transferErrors=movements.GroupBy(m=>m.Document).Where(g=>g.Sum(m=>m.Direction*m.Transfer)!=0).Select(g=>g.Key).ToHashSet();
        foreach(var m in movements)table.Rows.Add(m.Date+" · "+m.Number,S.LocalName("accounts",m.Account,vm.Arabic),Store.Money(m.Direction*m.Amount),Store.Money(m.Direction*m.Operating),Store.Money(m.Direction*m.Investing),Store.Money(m.Direction*m.Financing),Store.Money(m.Direction*m.Transfer),Store.Money(m.Direction*m.Unclassified),transferErrors.Contains(m.Document)?T("التحويل لا يتوازن في المستند","Transfer does not balance within document"):m.Stale?T("تغير المبلغ — أعد التصنيف","Amount changed — reclassify"):m.Unclassified!=0?T("يحتاج تصنيفًا","Needs classification"):T("مصنف","Classified"));
        return table;
    }
    string CashFlowLabel(string key)=>key switch{
        "OpeningCash"=>T("النقدية أول الفترة","Opening cash"),"ClosingCash"=>T("النقدية آخر الفترة","Closing cash"),"NetCashChange"=>T("صافي التغير في النقدية","Net change in cash"),"CashDifference"=>T("فرق التسوية — يجب أن يكون صفرًا","Reconciliation difference — must be zero"),
        "OperatingCashIn"=>T("مقبوضات تشغيلية","Operating receipts"),"OperatingCashOut"=>T("مدفوعات تشغيلية","Operating payments"),"OperatingCash"=>T("صافي التدفقات التشغيلية","Net operating cash flows"),
        "InvestingCashIn"=>T("مقبوضات استثمارية","Investing receipts"),"InvestingCashOut"=>T("مدفوعات استثمارية","Investing payments"),"InvestingCash"=>T("صافي التدفقات الاستثمارية","Net investing cash flows"),
        "FinancingCashIn"=>T("مقبوضات تمويلية","Financing receipts"),"FinancingCashOut"=>T("مدفوعات تمويلية","Financing payments"),"FinancingCash"=>T("صافي التدفقات التمويلية","Net financing cash flows"),
        "TransferCashIn"=>T("تحويلات داخلية واردة — مستبعدة","Internal transfers in — excluded"),"TransferCashOut"=>T("تحويلات داخلية صادرة — مستبعدة","Internal transfers out — excluded"),"TransferCash"=>T("صافي التحويلات — يجب أن يكون صفرًا","Net transfers — must be zero"),
        "UnclassifiedCashIn"=>T("مقبوضات تحتاج تصنيفًا","Unclassified receipts"),"UnclassifiedCashOut"=>T("مدفوعات تحتاج تصنيفًا","Unclassified payments"),"UnclassifiedCash"=>T("صافي غير المصنف — للمراجعة","Net unclassified — review"),_=>key
    };
    void CashFlowClassifications(DateTime from,DateTime to)
    {
        S.Require("master");var(w,p,finish)=Dialog(T("تصنيف التدفقات النقدية","Cash-flow classifications"),1050,false);
        p.Children.Add(Text(T("المبالغ موجبة بثلاث منازل عشرية؛ الباقي غير مصنف. التصنيف للعرض فقط. التحويل الداخلي يجب أن يتساوى وارده وصادره في نفس المستند. الحسابات من نوع نقدية فقط مشمولة؛ الشيكات المعلقة غير مشمولة.","Enter positive amounts with three decimals; the remainder stays unclassified. Classification changes presentation only. Internal transfer amounts must balance within the same document. Only Cash-type accounts are included; pending cheques are excluded."),16));
        var picker=new ComboBox{DisplayMemberPath="Label",SelectedValuePath="Key"};List<CashMovement> movements=[];
        TextBox operating=new(),investing=new(),financing=new(),transfer=new();var info=Text("",16);
        void Reload(){movements=A.CashMovements(from,to);picker.ItemsSource=movements.Select((m,i)=>new Choice(i.ToString(),m.Date+" · "+m.Number+" · "+S.LocalName("accounts",m.Account,vm.Arabic)+" · "+(m.Direction==1?T("وارد ","In "):T("صادر ","Out "))+Store.Money(m.Amount))).ToList();}
        picker.SelectionChanged+=(s,e)=>{if(!int.TryParse(picker.SelectedValue?.ToString(),out int index))return;var m=movements[index];operating.Text=Store.Money(m.Operating);investing.Text=Store.Money(m.Investing);financing.Text=Store.Money(m.Financing);transfer.Text=Store.Money(m.Transfer);info.Text=T("غير مصنف: ","Unclassified: ")+Store.Money(m.Unclassified)+(movements.Where(x=>x.Document==m.Document).Sum(x=>x.Direction*x.Transfer)!=0?T(" — التحويل غير متوازن في المستند"," — transfer does not balance within document"):"")+(m.Stale?T(" — تغير مبلغ الحركة؛ أعد التصنيف"," — amount changed; classify again"):"");};
        p.Children.Add(Field(T("الحركة النقدية","Cash movement"),picker));p.Children.Add(info);p.Children.Add(Field(T("تشغيلية JOD","Operating JOD"),operating));p.Children.Add(Field(T("استثمارية JOD","Investing JOD"),investing));p.Children.Add(Field(T("تمويلية JOD","Financing JOD"),financing));p.Children.Add(Field(T("تحويل داخلي JOD","Internal transfer JOD"),transfer));
        p.Children.Add(Actions(Btn(T("عرض المستند","View document"),()=>{if(int.TryParse(picker.SelectedValue?.ToString(),out int index))ShowDocument(movements[index].Document);}),Btn(T("حفظ التصنيف","Save classification"),()=>{if(!int.TryParse(picker.SelectedValue?.ToString(),out int index))throw new InvalidOperationException(T("اختر حركة","Choose a movement"));var m=movements[index];A.ClassifyCashMovement(m.Document,m.Account,m.Direction,Store.Parse(operating.Text),Store.Parse(investing.Text),Store.Parse(financing.Text),Store.Parse(transfer.Text));Reload();picker.SelectedValue=index.ToString();},true),Btn(T("إغلاق","Close"),finish)));
        Reload();if(picker.Items.Count>0)picker.SelectedIndex=0;w.ShowDialog();
    }
}
