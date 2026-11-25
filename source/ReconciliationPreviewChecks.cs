using System.Data;
using System.IO;

namespace Hisab;
public sealed partial class MainWindow
{
    public void RenderReconciliationPreviews(string path)
    {
        Directory.CreateDirectory(path);
        var customer=S.Accounts().First(a=>a.Kind=="Unclassified");S.SaveAccount(customer.Id,customer.Code,customer.Name,"Customer");
        S.SaveTranslations("accounts",customer.Id,"عميل الاختبار","Test customer","","");
        var supplier=S.Accounts().First(a=>a.Kind=="Unclassified");S.SaveAccount(supplier.Id,supplier.Code,supplier.Name,"Supplier");
        S.SaveTranslations("accounts",supplier.Id,"مورد الاختبار","Test supplier","","");
        var day=DateTime.Today.AddDays(-2);long opening=S.AccountId("3201");
        A.Manual(day,"Opening customer",[new(customer.Id,100000,0),new(opening,0,100000)]);
        A.Manual(day,"Opening supplier",[new(opening,60000,0),new(supplier.Id,0,60000)]);
        foreach(string language in new[]{"ar","en"})foreach(int font in new[]{14,24}){
            S.Set("language",language);S.Set("font",font.ToString());vm.Route="reports";BuildShell();Reports("party-reconciliation");
            UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();
            var data=ReportData("party-reconciliation",day,DateTime.Today,null);
            if(data.Rows.Count!=2||!data.Rows.Cast<DataRow>().Any(r=>r[2].ToString()=="100.000"&&r[3].ToString()=="0.000"&&r[4].ToString()=="100.000"))throw new InvalidOperationException("Reconciliation report totals differ from fixture");
            Capture(this,Path.Combine(path,language+"-reconciliation-"+font+".png"));
            RenderPrintPages(ReportDocument(T("تسوية الذمم","Party reconciliation"),data,ReportNote("party-reconciliation")),path,language+"-reconciliation-print-"+font);
            File.AppendAllText(Path.Combine(path,"ui-checks.txt"),"PASS "+language+"/"+font+" reconciliation labels, totals and print rendering"+Environment.NewLine);
        }
        string source=S.Scalar("SELECT number FROM documents WHERE note='Opening customer'")!.ToString()!;
        long openingDoc=A.CreateOpeningBalance(new(customer.Code,"OLD-INV-001",day.AddDays(-90),day.AddDays(-30),100,source,true),day);
        previewDirectory=path;
        try{
            foreach(string language in new[]{"ar","en"})foreach(int font in new[]{14,24}){
                S.Set("language",language);S.Set("font",font.ToString());vm.Route="opening-balances";BuildShell();UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);Capture(this,Path.Combine(path,language+"-openings-"+font+".png"));
                PreviewDialog("opening-form-"+font,OpeningBalanceForm);
                PreviewDialog("opening-preview-"+font,()=>OpeningBalancePreviewForm([new(customer.Code,"OLD-INV-002",day.AddDays(-90),day.AddDays(-30),50)],day));
                PreviewDialog("opening-allocation-"+font,()=>Settlements(openingDoc));
                PreviewDialog("opening-document-"+font,()=>ShowDocument(openingDoc));
                PreviewDialog("late-bill-form-"+font,BillReceiptsForm);
                if(S.Balance(customer.Id)!=100000||A.Positions(day,customer.Id).Single().Outstanding!=100000)throw new InvalidOperationException("Opening previews modified fixture balances");
                File.AppendAllText(Path.Combine(path,"ui-checks.txt"),"PASS "+language+"/"+font+" opening forms, preview, allocation, print and late-billing form"+Environment.NewLine);
            }
        }finally{previewDirectory=null;}
        long cash=S.AccountId("1101");long capital=A.Manual(day,"Report preview capital",[new(cash,1000000,0),new(opening,0,1000000)]);
        A.ClassifyCashMovement(capital,cash,1,0,0,1000,0);long receipt=A.Voucher(true,day,customer.Id,cash,25,"Cash report receipt");A.ClassifyCashMovement(receipt,cash,1,25,0,0,0);
        long item=S.Items()[0].Id;long order=A.CreatePurchaseOrder(supplier.Id,1,day,[new(item,10,20)]);A.IssuePurchaseOrder(order);long orderLine=Convert.ToInt64(S.Scalar("SELECT id FROM purchase_order_lines WHERE order_id=@p0",order));A.ReceivePurchaseOrder(order,day,[new(orderLine,10,"")]);
        A.Invoice(true,day,customer.Id,null,0,0,false,"Preview sale",[new(item,"Device",2,30,0)]);
        long noncash=A.Manual(day,"Noncash equipment contribution",[new(S.AccountId("1501"),100000,0),new(opening,0,100000)]);A.SaveNoncashDisclosure(noncash,S.AccountId("1501"),opening,"AssetAcquisition",100,"Equipment contributed by owner");
        var numericBaselines=new Dictionary<string,string[]>();
        var sortData=new DataTable();sortData.Columns.Add("Name");sortData.Columns.Add("JOD");sortData.Rows.Add("B","2.000");sortData.Rows.Add("A","1.000");
        var sortGrid=Grid(sortData,("Name","Name",1.0),("JOD","JOD",1.0));sortData.DefaultView.Sort="Name ASC";sortGrid.Columns[1].DisplayIndex=0;
        var sorted=CaptureDisplayedReport(ReportSnapshot.Capture("test","Sort test","","Test","en","",sortData),sortGrid);
        if(sorted.Headers[0]!="JOD"||sorted.Rows[0][0]!="1.000"||sorted.Rows[0][1]!="A")throw new InvalidOperationException("Snapshot does not preserve displayed row and column order");
        if(S.LoadReportSnapshot(S.SaveReportSnapshot(sorted)).Rows[0][0]!="1.000"||!ReportCsv.Serialize(sorted.ToTable()).Contains("\"1.000\",\"A\""))throw new InvalidOperationException("Sorted snapshot/export lost displayed order");
        File.AppendAllText(Path.Combine(path,"ui-checks.txt"),"PASS sorted rows and reordered columns preserved in saved snapshot and export"+Environment.NewLine);
        foreach(string language in new[]{"ar","en"})foreach(int font in new[]{14,24})foreach(string key in new[]{"profit","balance-sheet","historical-stock","receipt-accruals","cash-flow","cash-flow-details","noncash"}){
            S.Set("language",language);S.Set("font",font.ToString());vm.Route="reports";BuildShell();Reports(key);UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();
            var data=ReportData(key,day,DateTime.Today,null);
            if(key=="cash-flow"&&(!A.CashFlowStatement(day,DateTime.Today).Complete||A.CashFlowStatement(day,DateTime.Today).Difference!=0))throw new InvalidOperationException("Cash preview does not reconcile");
            if(data.Rows.Count==0)throw new InvalidOperationException("Financial preview has no data");
            if(key=="historical-stock"&&(A.InventoryAsOf(DateTime.Today).StockValue!=160000||A.InventoryAsOf(DateTime.Today).Difference!=0))throw new InvalidOperationException("Stock preview does not reconcile");
            if(key=="receipt-accruals"&&(A.ReceiptAccruals(DateTime.Today).PendingValue!=200000||A.ReceiptAccruals(DateTime.Today).Difference!=0))throw new InvalidOperationException("Receipt preview does not reconcile");
            bool Numeric(string value)=>decimal.TryParse(value,System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out _);
            var numbers=data.Rows.Cast<DataRow>().SelectMany(r=>r.ItemArray.Select(v=>v?.ToString()??"")).Where(Numeric).ToArray();
            if(numericBaselines.TryGetValue(key,out var baseline)&&!baseline.SequenceEqual(numbers))throw new InvalidOperationException("Arabic/English report values differ: "+key);numericBaselines[key]=numbers;
            var snapshot=ReportSnapshot.Capture(key,key,ReportNote(key),S.Setting("company"),language,"Preview cutoff "+day.ToString("yyyy-MM-dd"),data);
            if(font==14){long snapshotId=S.SaveReportSnapshot(snapshot);snapshot=S.LoadReportSnapshot(snapshotId);}
            if(ReportCsv.Serialize(snapshot.ToTable())!=ReportCsv.Serialize(data))throw new InvalidOperationException("Snapshot/export values differ");
            var print=ReportDocument(snapshot.Title,snapshot.ToTable(),snapshot.Note,snapshot.Company,snapshot.Language);
            var printed=print.Blocks.OfType<System.Windows.Documents.Table>().SelectMany(t=>t.RowGroups).SelectMany(g=>g.Rows).SelectMany(r=>r.Cells).Select(c=>string.Concat(c.Blocks.OfType<System.Windows.Documents.Paragraph>().SelectMany(b=>b.Inlines.OfType<System.Windows.Documents.Run>()).Select(r=>r.Text))).Where(Numeric).ToArray();
            if(!printed.SequenceEqual(numbers))throw new InvalidOperationException("Printed report values differ: "+key);
            Capture(this,Path.Combine(path,language+"-"+key+"-"+font+".png"));RenderPrintPages(ReportDocument(key,data,ReportNote(key)),path,language+"-"+key+"-print-"+font);
            File.AppendAllText(Path.Combine(path,"ui-checks.txt"),"PASS "+language+"/"+font+" "+key+" bilingual values, snapshot, CSV and print parity"+Environment.NewLine);
        }
        previewDirectory=path;
        try{foreach(string language in new[]{"ar","en"}){S.Set("language",language);S.Set("font","24");BuildShell();PreviewDialog("cash-flow-classifications",()=>CashFlowClassifications(day,DateTime.Today));PreviewDialog("noncash-disclosure",NoncashDisclosureForm);PreviewDialog("saved-report-snapshots",SavedReportSnapshots);}}
        finally{previewDirectory=null;}
    }
}
