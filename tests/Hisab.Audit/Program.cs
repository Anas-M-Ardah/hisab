using System.IO;

namespace Hisab.Audit;
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 1 || (args.Length == 1 && args[0] is not("--all" or "--reports")))
            throw new ArgumentException("Only --all or --reports is accepted; database paths cannot be supplied.");
        string directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "HisabAudit", Guid.NewGuid().ToString("N")));
        string live = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hisab"));
        if (directory.Equals(live, StringComparison.OrdinalIgnoreCase) || directory.StartsWith(live + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Audit output must not use the live Hisab directory.");
        Directory.CreateDirectory(directory);
        Console.WriteLine("ENTRYPOINT Hisab.Audit.Program; synthetic fixtures: " + directory);
        SQLitePCL.Batteries_V2.Init();
        if(args.Contains("--reports"))
        {
            var app=new System.Windows.Application();
            using var store=new Store(Path.Combine(directory,"report-fixture.db"));
            var window=new MainWindow(new MainViewModel(store)){ShowActivated=false};
            try{window.Show();window.RenderReconciliationPreviews(directory);Console.WriteLine(File.ReadAllText(Path.Combine(directory,"ui-checks.txt")));}
            finally{window.Close();app.Shutdown();}
            return 0;
        }
        if (args.Contains("--all"))
        {
            int code = SelfTest.Run(directory);
            Console.WriteLine(File.ReadAllText(Path.Combine(directory, "test-results.txt")));
            return code;
        }
        var results = new List<string>();
        AccountingIntegrityTests.Run(directory, results);
        ClosingStockTests.Run(directory, results);
        try{OpeningBalanceTests.Run(directory,results);}catch(Exception ex){results.Add("FAIL "+ex);}
        try{LateBillingTests.Run(directory,results);}catch(Exception ex){results.Add("FAIL "+ex);}
        try{ReportingCompletenessTests.Run(directory,results);}catch(Exception ex){results.Add("FAIL "+ex);}
        try{CashFlowTests.Run(directory,results);}catch(Exception ex){results.Add("FAIL "+ex);}
        try{FinancialStatementTests.Run(directory,results);}catch(Exception ex){results.Add("FAIL "+ex);}
        try{InventoryReconciliationTests.Run(directory,results);}catch(Exception ex){results.Add("FAIL "+ex);}
        File.WriteAllLines(Path.Combine(directory, "test-results.txt"), results);
        foreach (string result in results) Console.WriteLine(result);
        return results.Any(x => x.StartsWith("FAIL")) ? 1 : 0;
    }
}
