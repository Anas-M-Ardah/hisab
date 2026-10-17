using System.IO;
using System.Windows.Controls;
namespace Hisab;
public sealed partial class MainWindow
{
    public void RenderWorkbookPreviews(string path)
    {
        Directory.CreateDirectory(path);previewDirectory=path;
        foreach(var language in new[]{"ar","en"}){S.Set("language",language);BuildShell();WorkbookPreviewChecks(path,language);}
        S.Set("font","24");Width=1024;Height=768;BuildShell();PreviewDialog("large-text-split",()=>SplitVoucherForm(false));PreviewDialog("large-text-transfer",OrderTransferForm);
        previewDirectory=null;
    }
    void WorkbookPreviewChecks(string path,string language)
    {
        if(S.Scalar("SELECT id FROM accounts WHERE code='WB-ROOT'")==null){
            S.SaveAccount(null,"WB-ROOT","مصاريف عامة","Expense");S.SaveAccount(null,"WB-RENT","إيجار المكتب","Expense",S.AccountId("WB-ROOT"));S.SaveAccount(null,"WB-ORDERS","الطلبيات","Asset");S.SaveAccount(null,"WB-ORDER1","طلبية 1","Asset",S.AccountId("WB-ORDERS"));
            foreach(var pair in new[]{("WB-ROOT","General expenses"),("WB-RENT","Office rent"),("WB-ORDERS","Purchase orders"),("WB-ORDER1","Order 1")})S.SaveTranslations("accounts",S.AccountId(pair.Item1),S.Accounts().Single(a=>a.Code==pair.Item1).Name,pair.Item2);
            A.SplitVoucher(false,DateTime.Today,S.AccountId("1101"),"Workbook example",[new(S.AccountId("WB-RENT"),2500,"إيجار المكتب / Office rent"),new(S.AccountId("WB-ORDER1"),55.6m,"تخليص / Customs clearance")]);
            var customer=S.Accounts().First(a=>a.Kind=="Customer");A.ReceiveCheques(DateTime.Today,customer.Id,3294.41m,"Workbook receipt",new[]{500m,500m,1294.41m,500m,250m,250m}.Select((v,i)=>new ReceiptCheque("WB-PREVIEW-"+i,"البنك الإسلامي",v,DateTime.Today.AddMonths(i))).ToList());
        }
        var combo=Choose(UiAccounts());var picker=AccountPicker(combo);var filter=((StackPanel)picker).Children.OfType<ComboBox>().First();filter.SelectedItem=filter.Items.Cast<PositionChoice>().Single(x=>x.Id==S.AccountId("WB-ROOT"));bool filtered=combo.Items.Cast<Account>().Select(a=>a.Id).SequenceEqual(new[]{S.AccountId("WB-RENT")});filter.SelectedIndex=0;bool restored=combo.Items.Count==UiAccounts().Count;
        if(!filtered||!restored)throw new InvalidOperationException("Main-account selection filter failed");
        var report=ReportData("main-trial",DateTime.Today,DateTime.Today,null);var children=ReportData("child-trial",DateTime.Today,DateTime.Today,S.AccountId("WB-ROOT"));bool childTotal=children.Rows[0][1].ToString()=="2,500.000"||children.Rows[0][1].ToString()=="2500.000";
        if(!childTotal)throw new InvalidOperationException("Child trial report failed");
        File.AppendAllText(Path.Combine(path,"ui-checks.txt"),language+": main-account filter="+filtered+", reset="+restored+", child report="+childTotal+Environment.NewLine);
        PreviewDialog("split-payment",()=>SplitVoucherForm(false,Convert.ToInt64(S.Scalar("SELECT id FROM documents WHERE note='Workbook example'"))));
        PreviewDialog("order-transfer",OrderTransferForm);
        Reports("main-trial");UpdateLayout();Capture(this,Path.Combine(path,language+"-main-trial.png"));
        Reports("child-trial");var account=Descendants<ComboBox>(body).First(c=>c.ItemsSource is IEnumerable<Account>);account.SelectedItem=account.Items.Cast<Account>().Single(a=>a.Code=="WB-ROOT");Descendants<Button>(body).Single(b=>b.Content?.ToString()==T("عرض التقرير","View report")).Command.Execute(null);UpdateLayout();Capture(this,Path.Combine(path,language+"-child-trial.png"));
        PreviewDialog("combined-cheque-receipt",()=>ShowDocument(Convert.ToInt64(S.Scalar("SELECT min(id) FROM documents WHERE receipt_group IS NOT NULL"))));
    }
}
