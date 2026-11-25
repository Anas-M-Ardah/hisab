using System.IO;
namespace Hisab;
public static class FinancialStatementTests
{
    public static void Run(string directory,List<string> results)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,name);}
        using var s=new Store(Path.Combine(directory,"statements-"+Guid.NewGuid()+".db"));var a=new AccountingService(s);var day=DateTime.Today.AddDays(-20);
        long Value(FinancialStatement statement,string key)=>statement.Rows.Single(r=>r.Key==key&&r.Total).Current;
        var c=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(c.Id,c.Code,c.Name,"Customer");var p=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(p.Id,p.Code,p.Name,"Supplier");long cash=s.AccountId("1101"),equity=s.AccountId("3201"),expense=s.AccountId("5101"),item=s.Items()[0].Id;
        a.Manual(day,"capital",[new(cash,1000000,0),new(equity,0,1000000)]);
        a.Invoice(false,day,p.Id,null,0,0,false,"purchase",[new(item,"item",10,20,0)]);
        long sale=a.Invoice(true,day.AddDays(1),c.Id,null,0,0,false,"sale",[new(item,"item",4,50,0)]);
        a.Voucher(false,day.AddDays(1),expense,cash,10,"expense");long receipt=a.Voucher(true,day.AddDays(2),c.Id,cash,50,"receipt");a.Allocate(receipt,sale,50,day.AddDays(2));
        var profit=a.IncomeStatement(day,day.AddDays(3));var balance=a.BalanceSheet(day.AddDays(3));
        Check(Value(profit,"Revenue")==200000&&Value(profit,"COGS")==80000&&Value(profit,"GrossProfit")==120000&&Value(profit,"NetProfit")==110000,"Classified P&L separates revenue, COGS, gross and net profit");
        Check(balance.Complete&&Value(balance,"TotalAssets")==1310000&&Value(balance,"TotalLiabilities")==200000&&Value(balance,"TotalEquity")==1110000&&balance.Difference==0,"Balance sheet reconciles stock, cash, receivables, payables and earnings");
        long advanceCustomer=s.Accounts().First(x=>x.Kind=="Unclassified").Id;var ac=s.Accounts().Single(x=>x.Id==advanceCustomer);s.SaveAccount(ac.Id,ac.Code,ac.Name,"Customer");a.Voucher(true,day.AddDays(3),ac.Id,cash,20,"advance");
        balance=a.BalanceSheet(day.AddDays(3));Check(Value(balance,"TotalAssets")==1330000&&Value(balance,"CurrentLiabilities")==220000,"Customer credit balances present as liabilities instead of netting receivables");
        a.Voucher(false,day.AddDays(3),p.Id,cash,230,"supplier overpayment");balance=a.BalanceSheet(day.AddDays(3));
        Check(Value(balance,"CurrentLiabilities")==20000&&Value(balance,"TotalAssets")==1130000&&balance.Difference==0,"Supplier debit balances present as assets instead of netting liabilities");
        a.Manual(day.AddYears(-1),"prior income",[new(cash,500000,0),new(s.AccountId("4101"),0,500000)]);
        profit=a.IncomeStatement(day,day.AddDays(3));Check(profit.Rows.Single(r=>r.Key=="NetProfit"&&r.Total).Prior==500000&&Value(profit,"NetProfit")==110000,"P&L compares the same date range one year earlier");
        long equityBefore=Value(a.BalanceSheet(day.AddDays(3)),"TotalEquity");a.CloseYear(day.AddYears(-1),day.AddDays(3));
        Check(Value(a.BalanceSheet(day.AddDays(3)),"UnclosedEarnings")==0&&Value(a.BalanceSheet(day.AddDays(3)),"TotalEquity")==equityBefore,"Closing transfers profit without double-counting balance-sheet equity");
        Check(Value(a.IncomeStatement(day,day.AddDays(3)),"NetProfit")==110000,"P&L excludes profit-transfer closing journals");
        a.ReopenYear(Convert.ToInt64(s.Scalar("SELECT id FROM periods")),"review");Check(Value(a.IncomeStatement(day,day.AddDays(3)),"NetProfit")==110000&&Value(a.BalanceSheet(day.AddDays(3)),"TotalEquity")==equityBefore,"Reopening does not distort P&L or balance-sheet totals");
        s.SaveAccount(null,"CUSTOM-EXP","Custom direct cost","Expense");long custom=s.AccountId("CUSTOM-EXP");a.Voucher(false,day.AddDays(3),custom,cash,5,"custom cost");profit=a.IncomeStatement(day,day.AddDays(3));
        Check(!profit.Complete&&Value(profit,"UnmappedExpense")==5000&&Value(profit,"NetProfit")==105000,"Unmapped custom expense remains visible and included in net profit");
        long before=s.Balance(custom);s.SetReportingSection(custom,"COGS");profit=a.IncomeStatement(day,day.AddDays(3));Check(profit.Complete&&Value(profit,"GrossProfit")==115000&&Value(profit,"NetProfit")==105000&&s.Balance(custom)==before,"Classifying a custom cost changes presentation without changing ledger");
        s.SaveAccount(null,"CUSTOM-ASSET","Custom asset","Asset");long asset=s.AccountId("CUSTOM-ASSET");a.Manual(day.AddDays(3),"asset",[new(asset,100000,0),new(cash,0,100000)]);balance=a.BalanceSheet(day.AddDays(3));
        Check(!balance.Complete&&Value(balance,"UnmappedAssets")==100000&&balance.Difference==0,"Unmapped asset is disclosed without disappearing from balance sheet");
        s.SetReportingSection(asset,"NoncurrentAssets");Check(a.BalanceSheet(day.AddDays(3)).Complete,"Mapped asset clears classification warning");
        Reject(()=>s.SetReportingSection(asset,"Revenue"),"Reporting mapping cannot change an asset into income");
        Check(a.BalanceSheet(day.AddYears(-2)).Rows.Where(r=>r.Total).All(r=>r.Current==0),"Balance sheet cutoff excludes future postings");
        foreach(var (code,kind,section,amount) in new[]{("OTHER-IN","Income","OtherIncome",20000L),("OTHER-OUT","Expense","OtherExpense",7000L),("TAX-EXP","Expense","IncomeTax",3000L)}){
            s.SaveAccount(null,code,code,kind);long id=s.AccountId(code);s.SetReportingSection(id,section);a.Manual(day.AddDays(4),code,kind=="Income"?[new(cash,amount,0),new(id,0,amount)]:[new(id,amount,0),new(cash,0,amount)]);
        }
        profit=a.IncomeStatement(day,day.AddDays(4));Check(Value(profit,"OtherIncome")==20000&&Value(profit,"OtherExpense")==7000&&Value(profit,"IncomeTax")==3000&&Value(profit,"NetProfit")==115000,"Other income, expense and income tax affect net profit with correct signs");
        a.Manual(day.AddDays(5),"expense credit",[new(cash,200000,0),new(expense,0,200000)]);Check(Value(a.IncomeStatement(day.AddDays(5),day.AddDays(5)),"OperatingExpense")==-200000,"Expense credits remain negative expenses rather than hidden income");
        a.Manual(day.AddDays(6),"loss",[new(expense,50000,0),new(cash,0,50000)]);Check(Value(a.IncomeStatement(day.AddDays(6),day.AddDays(6)),"NetProfit")==-50000,"Loss periods show negative net profit");
        s.SaveAccount(null,"OLD-UNMAPPED","Prior custom income","Income");a.Manual(day.AddYears(-1),"prior unmapped",[new(cash,1000,0),new(s.AccountId("OLD-UNMAPPED"),0,1000)]);
        Check(!a.IncomeStatement(day,day.AddDays(3)).Complete,"Prior-only unmapped income keeps comparison warning visible");
        var flip=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(flip.Id,flip.Code,flip.Name,"Customer");
        a.Voucher(true,day.AddYears(-1),flip.Id,cash,20,"prior credit");a.Manual(day,"current receivable",[new(flip.Id,50000,0),new(equity,0,50000)]);
        balance=a.BalanceSheet(day.AddDays(6));
        Check(balance.Rows.Any(r=>r.Account==flip.Id&&r.Key=="CurrentLiabilities"&&r.Prior==20000&&r.Current==0)&&balance.Rows.Any(r=>r.Account==flip.Id&&r.Key=="CurrentAssets"&&r.Current==30000&&r.Prior==0),"Comparative customer sign change is shown in the proper section for each date");
    }
}
