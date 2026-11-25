using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;

namespace Hisab;
public sealed partial class MainWindow
{
    void ConfigureOpeningGrid(DataGrid grid)
    {
        grid.RowHeight=double.NaN;grid.MinRowHeight=(double)Resources["Row.Height"];
        foreach(var column in grid.Columns.OfType<DataGridTextColumn>()){
            string field=(column.Binding as Binding)?.Path.Path??"";
            var style=new Style(typeof(TextBlock),column.ElementStyle);
            style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.None));
            style.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));
            if(field is "[due]" or "[due_date]" or "[original]" or "[number]"){
                column.MinWidth=grid.FontSize*7+36;
                style.Setters.Add(new Setter(TextBlock.FlowDirectionProperty,FlowDirection.LeftToRight));
                style.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.NoWrap));
            }else column.MinWidth=field=="[amount]"?grid.FontSize*6+36:130;
            if(field=="[reference]")style.Setters.Add(new Setter(TextBlock.FlowDirectionProperty,FlowDirection.LeftToRight));
            column.ElementStyle=style;
        }
    }
    void OpeningBalancesPage()
    {
        Heading(T("الذمم الافتتاحية","Opening customer / supplier balances"),T("سجل المبالغ المتبقية من الفواتير القديمة مع استحقاقها، أو اربط تفاصيلها بقيد افتتاحي قائم دون تكرار الرصيد.","Record outstanding historical invoices with due dates, or attach details to an existing opening journal without duplicating its balance."));
        body.Children.Add(Actions(Btn(T("+ رصيد افتتاحي","+ Opening balance"),()=>OpeningBalanceForm(),true),Btn(T("قالب Excel","Excel template"),()=>{var save=new SaveFileDialog{Filter="Excel (*.xlsx)|*.xlsx",FileName="Hisab-opening-balances.xlsx"};if(save.ShowDialog(this)==true)OpeningBalanceWorkbook.WriteTemplate(save.FileName);}),Btn(T("استيراد Excel مع معاينة","Import Excel with preview"),StartOpeningImport),Btn(T("تسوية الذمم","Reconcile balances"),()=>Reports("party-reconciliation"))));
        var data=S.Table("SELECT d.id,d.number,d.date,o.reference,o.original_date,d.due_date,d.total,d.reversed,a.id party,o.source_doc FROM opening_balances o JOIN documents d ON d.id=o.doc JOIN accounts a ON a.id=d.party ORDER BY d.date DESC,d.id DESC");
        data.Columns.Add("party_name");data.Columns.Add("amount");data.Columns.Add("mode");data.Columns.Add("status");foreach(DataRow r in data.Rows){r["party_name"]=S.LocalName("accounts",(long)r["party"],vm.Arabic);r["amount"]=Store.Money((long)r["total"]);r["mode"]=r["source_doc"]==DBNull.Value?T("رصيد جديد","New posting"):T("تفاصيل لقيد قائم","Existing journal details");r["status"]=(long)r["reversed"]==0?T("ساري","Active"):T("ملغى","Reversed");}
        var grid=Grid(data,("number",T("المستند","Document"),1),("party_name",T("الحساب","Account"),2),("reference",T("مرجع الفاتورة","Invoice reference"),1),("due_date",T("الاستحقاق","Due"),1),("amount",T("المبلغ JOD","Amount JOD"),1),("mode",T("طريقة الإدخال","Entry mode"),1.5),("status",T("الحالة","Status"),1));ConfigureOpeningGrid(grid);body.Children.Add(grid);
        body.Children.Add(Actions(Btn(T("فتح / طباعة","Open / print"),()=>ShowDocument(Selected(grid))),Btn(T("توزيع الدفعات","Allocate payments"),()=>Settlements(Selected(grid))),Btn(T("إلغاء المحدد","Reverse selected"),()=>{long id=Selected(grid);var(w,p,finish)=Dialog(T("إلغاء الرصيد الافتتاحي","Reverse opening balance"));var reason=Input();p.Children.Add(Field(T("السبب","Reason"),reason));p.Children.Add(Text(T("إذا كان الرصيد تفاصيل لقيد قائم، يلغى التفصيل فقط ويظل القيد الأصلي.","For existing-journal details, only the detail is reversed; the original journal remains."),16));p.Children.Add(Btn(T("تأكيد الإلغاء","Confirm reversal"),()=>{A.Reverse(id,reason.Text);finish();Navigate("opening-balances");},true));w.ShowDialog();})));
    }
    void OpeningBalanceForm()
    {
        var(w,p,finish)=Dialog(T("رصيد افتتاحي مفصل","Detailed opening balance"),850);
        var party=Choose(UiAccounts().Where(a=>a.Kind is "Customer" or "Supplier"));var cutoff=Date();var original=Date();var due=Date();due.DisplayDateEnd=null;
        var reference=Input();var amount=Input("",true);var mode=Choose(new[]{new Choice("post",T("إثبات رصيد جديد","Post a new opening balance")),new Choice("detail",T("تفاصيل لقيد افتتاحي قائم — دون ترحيل جديد","Attach to existing opening journal — no new posting"))});mode.DisplayMemberPath="Label";mode.SelectedIndex=0;var journal=Input();
        p.Children.Add(Field(T("العميل / المورد","Customer / supplier"),party));p.Children.Add(Field(T("تاريخ إدخال الرصيد في النظام","System opening date"),cutoff));p.Children.Add(Field(T("مرجع الفاتورة الأصلية","Original invoice reference"),reference));p.Children.Add(Field(T("تاريخ الفاتورة الأصلية","Original invoice date"),original));p.Children.Add(Field(T("تاريخ الاستحقاق الأصلي","Original due date"),due));p.Children.Add(Field(T("المبلغ المتبقي JOD","Outstanding amount JOD"),amount));p.Children.Add(Field(T("طريقة الإدخال","Entry mode"),mode));var sourceField=Field(T("رقم القيد الافتتاحي القائم (JV-…)","Existing opening journal number (JV-…)"),journal);p.Children.Add(sourceField);
        void Mode()=>sourceField.Visibility=(mode.SelectedItem as Choice)?.Key=="detail"?Visibility.Visible:Visibility.Collapsed;mode.SelectionChanged+=(s,e)=>Mode();Mode();
        p.Children.Add(Text(T("لا تعِد إدخال رصيد مسجل سابقًا. الرصيد الجديد يقابل حساب الرصيد الافتتاحي ولا يسجل مبيعات أو ضريبة أو مخزونًا.","Do not post a balance already recorded. New balances offset the opening balance account; they do not create sales, tax or stock."),16));
        p.Children.Add(Btn(T("معاينة قبل الحفظ","Preview before saving"),()=>{var row=new OpeningBalanceInput(GetAccount(party).Code,reference.Text,GetDate(original),GetDate(due),Store.Parse(amount.Text),(mode.SelectedItem as Choice)?.Key=="detail"?journal.Text:"",(mode.SelectedItem as Choice)?.Key=="detail");OpeningBalancePreviewForm([row],GetDate(cutoff),finish);},true));w.ShowDialog();
    }
    void OpeningBalancePreviewForm(IReadOnlyList<OpeningBalanceInput> input,DateTime cutoff,Action? closeEditor=null)
    {
        var preview=A.PreviewOpeningBalances(input,cutoff);var(w,p,finish)=Dialog(T("مراجعة الذمم الافتتاحية","Review opening balances"),1100,false);
        var data=new DataTable();foreach(string c in new[]{"account","reference","original","due","amount","mode","error"})data.Columns.Add(c);
        foreach(var row in preview)data.Rows.Add(row.Input.AccountCode+" · "+(S.Accounts().FirstOrDefault(a=>a.Code==row.Input.AccountCode.Trim()) is Account account?S.LocalName("accounts",account.Id,vm.Arabic):row.Party),row.Input.Reference,row.Input.OriginalDate.ToString("yyyy-MM-dd"),row.Input.DueDate.ToString("yyyy-MM-dd"),Store.Money(row.Amount),string.IsNullOrWhiteSpace(row.Input.ExistingJournal)?T("ترحيل جديد","New posting"):row.Input.ExistingJournal,ImportMessage(row.Error));
        var grid=Grid(data,("account",T("الحساب","Account"),2),("reference",T("المرجع","Reference"),1),("original",T("الأصلي","Original"),1),("due",T("الاستحقاق","Due"),1),("amount",T("المبلغ","Amount"),1),("mode",T("الطريقة","Mode"),1),("error",T("التحقق","Validation"),2));ConfigureOpeningGrid(grid);grid.MaxHeight=300;p.Children.Add(grid);
        foreach(string kind in new[]{"Customer","Supplier"})foreach(bool posted in new[]{true,false}){
            long total=preview.Where(r=>r.Error.Length==0&&r.Kind==kind&&string.IsNullOrWhiteSpace(r.Input.ExistingJournal)==posted).Sum(r=>r.Amount);
            p.Children.Add(Text((kind=="Customer"?T("عملاء","Customers"):T("موردون","Suppliers"))+" · "+(posted?T("ترحيل جديد","New posting"):T("تفاصيل فقط","Details only"))+": "+Store.Money(total)+" JOD",16));
        }
        var save=Btn(T("حفظ جميع الصفوف","Save all rows"),()=>{A.ImportOpeningBalances(input,cutoff);finish();closeEditor?.Invoke();Navigate("opening-balances");},true);save.IsEnabled=preview.All(r=>r.Error.Length==0);p.Children.Add(save);w.ShowDialog();
    }
    void StartOpeningImport()
    {
        var file=new OpenFileDialog{Filter="Excel (*.xlsx)|*.xlsx"};if(file.ShowDialog(this)!=true)return;var sheets=ExcelWorkbook.Read(file.FileName);
        var(w,p,finish)=Dialog(T("استيراد الذمم الافتتاحية","Import opening balances"),1000,false);var sheet=Choose(sheets);sheet.SelectedIndex=0;var cutoff=Date();
        p.Children.Add(Text(T("الصف الأول يحتوي الأعمدة التالية بالترتيب. احفظ التواريخ كنص yyyy-MM-dd والمعرّفات كنص. ExistingJournal اختياري؛ تركه فارغًا يرحل رصيدًا جديدًا.","First row must contain these columns in order. Use text dates yyyy-MM-dd and text identifiers. ExistingJournal is optional; leaving it blank posts a new balance."),16));p.Children.Add(Text("AccountCode | Reference | OriginalDate | DueDate | Amount | ExistingJournal",16));p.Children.Add(Field(T("الورقة","Worksheet"),sheet));p.Children.Add(Field(T("تاريخ إدخال الرصيد","Opening date"),cutoff));p.Children.Add(Btn(T("معاينة","Preview"),()=>{var selected=sheet.SelectedItem as ImportSheet??throw new InvalidOperationException("Choose worksheet");OpeningBalancePreviewForm(OpeningBalanceWorkbook.Read(selected),GetDate(cutoff),finish);},true));w.ShowDialog();
    }
}
