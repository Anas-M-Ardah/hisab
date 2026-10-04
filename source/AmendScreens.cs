using System.Data;
using System.Globalization;
using System.Windows.Controls;

namespace Hisab;
public sealed partial class MainWindow
{
    void AmendOtherDocument(long id)
    {
        var source=S.Table("SELECT * FROM documents WHERE id=@p0",id).Rows[0];string kind=(string)source["kind"];if((long)source["reversed"]==1)throw new InvalidOperationException(T("المستند ملغى","Document is cancelled"));
        if(kind=="JV"){JournalForm(id);return;}
        if(kind is not("RC" or "PV"))throw new InvalidOperationException(T("للشيكات استخدم سجل الشيكات. لتكلفة المخزون استخدم إعادة التقييم.","Use the cheque register for cheque corrections, or stock revaluation for inventory costs."));
        var (w,p,finish)=Dialog(T("تعديل السند","Amend voucher"),850);var date=Date();date.SelectedDate=DateTime.Parse((string)source["date"]);var party=Choose(UiAccounts().Where(a=>a.Kind!="Unclassified"));party.SelectedItem=party.Items.Cast<Account>().Single(a=>a.Id==(long)source["party"]);var cash=Choose(UiAccounts().Where(a=>a.Kind=="Cash"));cash.SelectedItem=cash.Items.Cast<Account>().Single(a=>a.Id==(long)source["cash"]);var amount=Input(Store.D((long)source["total"]).ToString(CultureInfo.InvariantCulture),true);var note=Input((string)source["note"]);var reason=Input();
        foreach(var f in new[]{Field(T("التاريخ","Date"),date),Field(T("الحساب","Account"),party),Field(T("الصندوق / البنك","Cash / bank"),cash),Field(T("المبلغ","Amount"),amount),Field(T("البيان","Description"),note),Field(T("سبب التعديل","Amendment reason"),reason)})p.Children.Add(f);
        p.Children.Add(Btn(T("مراجعة وحفظ التعديل","Review & save amendment"),()=>{if(Confirm(reason.Text+"\n"+amount.Text+" JOD"))Saved(A.AmendSimple(id,GetDate(date),GetAccount(party).Id,GetAccount(cash).Id,Store.Parse(amount.Text),note.Text,null,reason.Text),finish);},true));w.ShowDialog();
    }
    void StockRevaluationForm(long item)
    {
        var (w,p,finish)=Dialog(T("تعديل تكلفة المخزون / إعادة تقييم","Stock cost adjustment / revaluation"),800);var current=S.Items().Single(i=>i.Id==item);p.Children.Add(Text(current.Name,23,true));var cost=Input(current.Qty==0?"0":(Store.D(current.Value)*1000/current.Qty).ToString("0.000",CultureInfo.InvariantCulture),true);var reason=Input();p.Children.Add(Field(T("متوسط تكلفة الوحدة الجديد JOD","New average unit cost JOD"),cost));p.Children.Add(Field(T("سبب إعادة التقييم","Revaluation reason"),reason));p.Children.Add(Text(T("يُحفظ التعديل بتاريخ اليوم بقيد موثق. لا تُمحى التكلفة التاريخية.","The adjustment posts today as an audited entry. Historical cost is retained."),18));p.Children.Add(Btn(T("مراجعة وحفظ","Review & save"),()=>{if(Confirm(reason.Text+"\n"+cost.Text+" JOD"))Saved(A.RevalueStock(item,Store.Parse(cost.Text),reason.Text),finish);},true));w.ShowDialog();
    }
}
