using System.Data;
using System.Windows.Controls;

namespace Hisab;
public sealed partial class MainWindow
{
    string StatementLabel(string key)=>key switch{
        "Revenue"=>T("إجمالي الإيرادات","Total revenue"),"COGS"=>T("إجمالي تكلفة المبيعات","Total cost of sales"),"GrossProfit"=>T("مجمل الربح / الخسارة","Gross profit / loss"),
        "OperatingExpense"=>T("إجمالي المصاريف التشغيلية","Total operating expenses"),"OperatingProfit"=>T("الربح التشغيلي","Operating profit"),"OtherIncome"=>T("إيرادات أخرى","Other income"),"OtherExpense"=>T("مصروفات أخرى","Other expenses"),"IncomeTax"=>T("مصروف ضريبة الدخل","Income tax expense"),"NetProfit"=>T("صافي الربح / الخسارة","Net profit / loss"),
        "CurrentAssets"=>T("الأصول المتداولة","Current assets"),"NoncurrentAssets"=>T("الأصول غير المتداولة","Noncurrent assets"),"TotalAssets"=>T("إجمالي الأصول","Total assets"),"CurrentLiabilities"=>T("الالتزامات المتداولة","Current liabilities"),"NoncurrentLiabilities"=>T("الالتزامات غير المتداولة","Noncurrent liabilities"),"TotalLiabilities"=>T("إجمالي الالتزامات","Total liabilities"),"Equity"=>T("حقوق الملكية المرحلة","Posted equity"),"UnclosedEarnings"=>T("الأرباح / الخسائر غير المقفلة","Unclosed earnings / losses"),"TotalEquity"=>T("إجمالي حقوق الملكية","Total equity"),"LiabilitiesEquity"=>T("إجمالي الالتزامات وحقوق الملكية","Total liabilities and equity"),"BalanceDifference"=>T("فرق المعادلة — يجب أن يكون صفرًا","Balance difference — must be zero"),
        "UnmappedIncome"=>T("إيرادات تحتاج تصنيفًا","Income requiring classification"),"UnmappedExpense"=>T("مصروفات تحتاج تصنيفًا","Expenses requiring classification"),"UnmappedAssets"=>T("أصول تحتاج تصنيفًا","Assets requiring classification"),"UnmappedLiabilities"=>T("التزامات تحتاج تصنيفًا","Liabilities requiring classification"),"UnmappedBalances"=>T("أرصدة غير مصنفة — للمراجعة","Unclassified balances — review"),_=>CashFlowLabel(key)
    };
    DataTable FinancialStatementData(string key,DateTime from,DateTime to)
    {
        var statement=key=="cash-flow"?A.CashFlowStatement(from,to):key=="balance-sheet"?A.BalanceSheet(to):A.IncomeStatement(from,to);var table=new DataTable();
        string dates=key=="balance-sheet"?to.ToString("yyyy-MM-dd"):from.ToString("yyyy-MM-dd")+" → "+to.ToString("yyyy-MM-dd");
        string previousDates=key=="balance-sheet"?to.AddYears(-1).ToString("yyyy-MM-dd"):from.AddYears(-1).ToString("yyyy-MM-dd")+" → "+to.AddYears(-1).ToString("yyyy-MM-dd");
        foreach(string column in new[]{T("الحساب / البند","Account / line"),T("التصنيف","Section"),dates+" JOD",previousDates+" JOD",T("الفرق JOD","Change JOD")})table.Columns.Add(column);
        foreach(var row in statement.Rows){string label=row.Account is long account?S.LocalName("accounts",account,vm.Arabic):StatementLabel(row.Key);table.Rows.Add(label,row.Account!=null?StatementLabel(row.Key):T("إجمالي / نتيجة","Total / result"),Store.Money(row.Current),Store.Money(row.Prior),Store.Money(row.Current-row.Prior));}
        if(!statement.Complete)table.Rows.InsertAt(table.NewRow(),0);
        if(!statement.Complete){table.Rows[0][0]=T("تنبيه: التصنيف غير مكتمل أو يوجد فرق؛ راجع الحسابات قبل الاعتماد.","Warning: classification incomplete or balances differ; review before relying on this statement.");}
        table.ExtendedProperties["statementAccounts"]=(statement.Complete?statement.Rows.Select(r=>r.Account):new long?[]{null}.Concat(statement.Rows.Select(r=>r.Account))).ToList();
        return table;
    }
    void ReportingClassifications()
    {
        S.Require("master");var(w,p,finish)=Dialog(T("تصنيف الحسابات للقوائم المالية","Financial statement classifications"),900,false);
        p.Children.Add(Text(T("تؤثر التصنيفات في العرض فقط، ولا تغيّر القيود. تستخدم المقارنات التصنيفات الحالية. عيّن فئة واضحة للحسابات المخصصة.","Classifications change presentation only, not entries. Comparisons use current classifications. Assign custom accounts explicitly."),16));
        var account=Choose(UiAccounts().Where(a=>a.Kind!="Unclassified"));var section=new ComboBox{DisplayMemberPath="Label",SelectedValuePath="Key"};
        void Refresh(){if(account.SelectedItem is not Account a)return;section.ItemsSource=Store.ReportingSections(a.Kind).Select(key=>new Choice(key,StatementLabel(key))).ToList();section.SelectedValue=S.ReportingSection(a);}
        account.SelectionChanged+=(s,e)=>Refresh();p.Children.Add(Field(T("الحساب","Account"),account));p.Children.Add(Field(T("التصنيف","Classification"),section));
        p.Children.Add(Btn(T("حفظ التصنيف","Save classification"),()=>{var a=GetAccount(account);string value=(section.SelectedItem as Choice)?.Key??throw new InvalidOperationException(T("اختر التصنيف","Choose classification"));S.SetReportingSection(a.Id,value);Refresh();vm.Status=T("حُفظ التصنيف؛ أعد عرض التقرير.","Classification saved; refresh the report.");},true));
        p.Children.Add(Btn(T("إغلاق","Close"),finish));w.ShowDialog();
    }
}
