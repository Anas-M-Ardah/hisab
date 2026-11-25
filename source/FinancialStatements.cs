using System.Data;

namespace Hisab;
public record StatementRow(string Key,long? Account,long Current,long Prior,bool Total=false);
public record FinancialStatement(List<StatementRow> Rows,bool Complete,long Difference=0);
public sealed partial class Store
{
    public static string DefaultReportingSection(Account account)=>account.Kind switch{
        "Cash" or "Customer"=>"CurrentAssets","Supplier"=>"CurrentLiabilities","Equity"=>"Equity",
        "Asset" when account.Code is "1104" or "1301" or "1401"=>"CurrentAssets",
        "Asset" when account.Code=="1501"=>"NoncurrentAssets",
        "Liability" when account.Code is "2102" or "2190" or "2195" or "2196"=>"CurrentLiabilities",
        "Income" when account.Code=="4101"=>"Revenue",
        "Expense" when account.Code=="5104"=>"COGS",
        "Expense" when account.Code is "5101" or "5102" or "5195" or "5196"=>"OperatingExpense",
        _=>"Unmapped"
    };
    public static string[] ReportingSections(string kind)=>kind switch{
        "Cash"=>["CurrentAssets"],"Customer" or "Asset"=>["CurrentAssets","NoncurrentAssets"],
        "Supplier" or "Liability"=>["CurrentLiabilities","NoncurrentLiabilities"],"Equity"=>["Equity"],
        "Income"=>["Revenue","OtherIncome"],"Expense"=>["COGS","OperatingExpense","OtherExpense","IncomeTax"],_=>[]
    };
    public string ReportingSection(Account account)
    {
        string? saved=Scalar("SELECT section FROM account_reporting WHERE account=@p0",account.Id) as string;
        return saved!=null&&ReportingSections(account.Kind).Contains(saved)?saved:DefaultReportingSection(account);
    }
    public void SetReportingSection(long account,string section)
    {
        Require("master");var current=Accounts().Single(a=>a.Id==account);
        if(!ReportingSections(current.Kind).Contains(section))throw new InvalidOperationException("تصنيف التقرير لا يناسب نوع الحساب / Reporting section is incompatible with account type");
        using var tx=BeginTransaction();Exec("INSERT INTO account_reporting(account,section) VALUES(@p0,@p1) ON CONFLICT(account) DO UPDATE SET section=excluded.section",account,section);Audit("reporting-section",current.Code+" → "+section);tx.Commit();
    }
}
public sealed partial class AccountingService
{
    Dictionary<long,long> LedgerBalances(DateTime end,DateTime? start=null,bool excludeClosing=false)
    {
        return Table("SELECT e.account,sum(e.debit-e.credit) balance FROM entries e JOIN documents d ON d.id=e.doc WHERE d.date<=@p0 AND (@p1 IS NULL OR d.date>=@p1) AND (@p2=0 OR (d.kind!='CL' AND NOT(d.kind='RV' AND d.original IN (SELECT id FROM documents WHERE kind='CL')))) GROUP BY e.account",end.ToString("yyyy-MM-dd"),start?.ToString("yyyy-MM-dd"),excludeClosing?1:0).Rows.Cast<DataRow>().ToDictionary(r=>(long)r["account"],r=>(long)r["balance"]);
    }
    public FinancialStatement IncomeStatement(DateTime from,DateTime to)
    {
        if(from.Date>to.Date)throw new InvalidOperationException("Invalid reporting range / نطاق التقرير غير صالح");
        var current=LedgerBalances(to,from,true);var prior=LedgerBalances(to.AddYears(-1),from.AddYears(-1),true);
        var rows=new List<StatementRow>();bool complete=true;
        foreach(var a in Accounts().Where(a=>a.Kind is "Income" or "Expense")){
            long c=current.GetValueOrDefault(a.Id)*(a.Kind=="Income"?-1:1),p=prior.GetValueOrDefault(a.Id)*(a.Kind=="Income"?-1:1);if(c==0&&p==0)continue;
            string section=store.ReportingSection(a);if(section=="Unmapped"){complete=false;section=a.Kind=="Income"?"UnmappedIncome":"UnmappedExpense";}rows.Add(new(section,a.Id,c,p));
        }
        (long C,long P) Sum(string key)=>(rows.Where(r=>r.Key==key&&!r.Total).Sum(r=>r.Current),rows.Where(r=>r.Key==key&&!r.Total).Sum(r=>r.Prior));
        var ordered=new List<StatementRow>();
        void Section(string key){ordered.AddRange(rows.Where(r=>r.Key==key));var sum=Sum(key);ordered.Add(new(key,null,sum.C,sum.P,true));}
        Section("Revenue");Section("COGS");var rev=Sum("Revenue");var cost=Sum("COGS");long gross=rev.C-cost.C,oldGross=rev.P-cost.P;ordered.Add(new("GrossProfit",null,gross,oldGross,true));
        Section("OperatingExpense");var op=Sum("OperatingExpense");ordered.Add(new("OperatingProfit",null,gross-op.C,oldGross-op.P,true));
        Section("OtherIncome");Section("OtherExpense");Section("IncomeTax");
        if(rows.Any(r=>r.Key=="UnmappedIncome"))Section("UnmappedIncome");if(rows.Any(r=>r.Key=="UnmappedExpense"))Section("UnmappedExpense");
        long Net(Dictionary<long,long> balances)=>Accounts().Where(a=>a.Kind is "Income" or "Expense").Sum(a=>-balances.GetValueOrDefault(a.Id));
        ordered.Add(new("NetProfit",null,Net(current),Net(prior),true));return new(ordered,complete);
    }
    public FinancialStatement BalanceSheet(DateTime asOf)
    {
        var current=LedgerBalances(asOf);var prior=LedgerBalances(asOf.AddYears(-1));var accounts=Accounts();var rows=new List<StatementRow>();bool complete=true;
        Dictionary<string,long> Build(Dictionary<long,long> balances,bool old)
        {
            var totals=new Dictionary<string,long>();
            foreach(var a in accounts.Where(a=>a.Kind is not("Income" or "Expense"))){long balance=balances.GetValueOrDefault(a.Id);if(balance==0)continue;
                string section=store.ReportingSection(a);long amount=a.Kind is "Liability" or "Supplier" or "Equity"?-balance:balance;
                if(a.Kind=="Customer"&&balance<0){section="CurrentLiabilities";amount=-balance;}if(a.Kind=="Supplier"&&balance>0){section="CurrentAssets";amount=balance;}
                if(section=="Unmapped"){complete=false;section=a.Kind switch{"Asset"=>"UnmappedAssets","Liability"=>"UnmappedLiabilities",_=>"UnmappedBalances"};}
                int index=rows.FindIndex(r=>r.Account==a.Id&&r.Key==section);
                if(index<0)rows.Add(new(section,a.Id,old?0:amount,old?amount:0));else rows[index]=old?rows[index] with{Prior=amount}:rows[index] with{Current=amount};
                totals[section]=totals.GetValueOrDefault(section)+amount;
            }
            long earnings=accounts.Where(a=>a.Kind is "Income" or "Expense").Sum(a=>-balances.GetValueOrDefault(a.Id));totals["UnclosedEarnings"]=earnings;return totals;
        }
        var c=Build(current,false);var p=Build(prior,true);var ordered=new List<StatementRow>();
        void Section(string key){ordered.AddRange(rows.Where(r=>r.Key==key));ordered.Add(new(key,null,c.GetValueOrDefault(key),p.GetValueOrDefault(key),true));}
        long Sum(Dictionary<string,long> source,params string[] keys)=>keys.Sum(k=>source.GetValueOrDefault(k));
        foreach(string key in new[]{"CurrentAssets","NoncurrentAssets","UnmappedAssets"})Section(key);
        long assets=Sum(c,"CurrentAssets","NoncurrentAssets","UnmappedAssets"),oldAssets=Sum(p,"CurrentAssets","NoncurrentAssets","UnmappedAssets");ordered.Add(new("TotalAssets",null,assets,oldAssets,true));
        foreach(string key in new[]{"CurrentLiabilities","NoncurrentLiabilities","UnmappedLiabilities"})Section(key);
        long liabilities=Sum(c,"CurrentLiabilities","NoncurrentLiabilities","UnmappedLiabilities"),oldLiabilities=Sum(p,"CurrentLiabilities","NoncurrentLiabilities","UnmappedLiabilities");ordered.Add(new("TotalLiabilities",null,liabilities,oldLiabilities,true));
        Section("Equity");ordered.Add(new("UnclosedEarnings",null,c.GetValueOrDefault("UnclosedEarnings"),p.GetValueOrDefault("UnclosedEarnings"),true));
        long equity=Sum(c,"Equity","UnclosedEarnings"),oldEquity=Sum(p,"Equity","UnclosedEarnings");ordered.Add(new("TotalEquity",null,equity,oldEquity,true));ordered.Add(new("LiabilitiesEquity",null,liabilities+equity,oldLiabilities+oldEquity,true));
        if(rows.Any(r=>r.Key=="UnmappedBalances"))Section("UnmappedBalances");
        long difference=assets-liabilities-equity,priorDifference=oldAssets-oldLiabilities-oldEquity;ordered.Add(new("BalanceDifference",null,difference,priorDifference,true));return new(ordered,complete&&difference==0&&priorDifference==0,difference);
    }
}
