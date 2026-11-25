using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;

namespace Hisab;
public sealed partial class MainWindow
{
    void Reports(string initial="trial")
    {
        body.Children.Clear(); Heading(T("التقارير","Reports"),T("اختر التقرير والفترة، ثم اعرضه أو اطبعه أو صدّره.","Choose a report and date range, then view, print or export it."));
        var type=new ComboBox{ItemsSource=new[]{new Choice("trial",T("ميزان المراجعة — كل الحسابات","Trial balance — all accounts")),new Choice("main-trial",T("ميزان الحسابات الرئيسية","Main-account trial balance")),new Choice("child-trial",T("ميزان الحسابات الفرعية","Child-account trial balance")),new Choice("ledger",T("كشف حساب / دفتر الأستاذ","Account statement / ledger")),new Choice("journal",T("دفتر اليومية","Daybook")),new Choice("stock",T("جرد المخزون الحالي","Current inventory")),new Choice("historical-stock",T("تقييم المخزون حتى تاريخ","Inventory valuation as of date")),new Choice("receipt-accruals",T("تسوية الاستلامات غير المفوترة","Unbilled receipt reconciliation")),new Choice("moves",T("حركة المخزون","Stock movements")),new Choice("profit",T("قائمة الدخل المقارنة","Comparative income statement")),new Choice("noncash",T("إفصاحات غير نقدية","Noncash disclosures")),new Choice("cash-flow-details",T("تفاصيل التدفقات النقدية","Cash-flow details")),new Choice("cash-flow",T("قائمة التدفقات النقدية","Cash-flow statement")),new Choice("balance-sheet",T("قائمة المركز المالي","Balance sheet")),new Choice("party-reconciliation",T("تسوية ذمم العملاء والموردين","Customer / supplier reconciliation")),new Choice("aging",T("أعمار الذمم في نهاية الفترة","Aging at period end")),new Choice("tax",T("ورقة ضريبة المبيعات الأردنية","Jordan sales-tax worksheet"))},DisplayMemberPath="Label",SelectedValuePath="Key",SelectedValue=initial};
        var from=Date();from.SelectedDate=new DateTime(DateTime.Today.Year,1,1);var to=Date();var account=Choose(UiAccounts());account.SelectedIndex=0;
        var filters=new WrapPanel();var reportField=Field(T("التقرير","Report"),type);reportField.Width=Math.Max(320,FontSize*16);reportField.Margin=new(0,0,16,12);filters.Children.Add(reportField);var dates=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};var f=Field(T("من تاريخ","From"),from);f.Margin=new(0,0,14,12);dates.Children.Add(f);dates.Children.Add(Field(T("إلى تاريخ","To"),to));dates.Width=Math.Max(400,FontSize*20);filters.Children.Add(dates);body.Children.Add(filters);var accountField=Field(T("الحساب","Account"),account);body.Children.Add(accountField);
        var children=new CheckBox{Content=T("تضمين الحسابات الفرعية في كشف الحساب","Include child accounts in statement"),IsChecked=true};body.Children.Add(children);
        void StatementOptions(){bool statement=type.SelectedValue?.ToString()=="ledger";children.Visibility=statement?Visibility.Visible:Visibility.Collapsed;accountField.Visibility=statement||type.SelectedValue?.ToString()=="child-trial"?Visibility.Visible:Visibility.Collapsed;}type.SelectionChanged+=(s,e)=>StatementOptions();StatementOptions();
        var classify=Btn("",()=>{if(type.SelectedValue?.ToString() is "cash-flow" or "cash-flow-details")CashFlowClassifications(GetDate(from),GetDate(to));else if(type.SelectedValue?.ToString()=="noncash")NoncashDisclosureForm();else ReportingClassifications();});void ClassificationLabel(){classify.Content=type.SelectedValue?.ToString() is "cash-flow" or "cash-flow-details"?T("تصنيف التدفقات النقدية","Cash-flow classifications"):type.SelectedValue?.ToString()=="noncash"?T("تسجيل إفصاح غير نقدي","Record noncash disclosure"):T("تصنيف القوائم المالية","Statement classifications");}type.SelectionChanged+=(s,e)=>ClassificationLabel();ClassificationLabel();var target=new StackPanel();DataTable? current=null;ReportSnapshot? displayedSnapshot=null;Func<ReportSnapshot>? captureDisplayed=null;string title="",reportNote="";
        void Run(){var a=GetDate(from);var b=GetDate(to);if(a>b)throw new InvalidOperationException(T("تاريخ البداية بعد النهاية","Start date is after end date"));string key=type.SelectedValue?.ToString()??"trial";title=(type.SelectedItem as Choice)!.Label+" · "+a.ToString("yyyy-MM-dd")+" → "+b.ToString("yyyy-MM-dd");if(key is "party-reconciliation" or "aging" or "balance-sheet" or "historical-stock" or "receipt-accruals")title=(type.SelectedItem as Choice)!.Label+" · "+T("حتى ","As of ")+b.ToString("yyyy-MM-dd");if(key=="stock")title=(type.SelectedItem as Choice)!.Label+" · "+DateTime.Today.ToString("yyyy-MM-dd");if(key is "ledger" or "child-trial")title+=" · "+GetAccount(account).Name+(children.IsChecked==true?T(" (مع الفروع)"," (including children)"):"");current=ReportData(key,a,b,key is "ledger" or "child-trial"?GetAccount(account).Id:null,children.IsChecked==true);target.Children.Clear();target.Children.Add(Text(title,22,true));reportNote=ReportNote(key);displayedSnapshot=ReportSnapshot.Capture(key,title,reportNote,S.Setting("company"),vm.Arabic?"ar":"en",JsonSerializer.Serialize(new{From=a,To=b,Account=key is "ledger" or "child-trial"?GetAccount(account).Id:(long?)null,IncludeChildren=children.IsChecked==true}),current);if(reportNote.Length>0)target.Children.Add(Text(reportNote,16));var cols=current.Columns.Cast<DataColumn>().Select(c=>(c.ColumnName,c.ColumnName,c.Ordinal==0?2.0:1.0)).ToArray();var reportGrid=Grid(current,cols);AlignFigures(reportGrid,current);var capturedBasis=displayedSnapshot!;captureDisplayed=()=>CaptureDisplayedReport(capturedBasis,reportGrid);reportGrid.ColumnHeaderHeight=double.NaN;if(key is "profit" or "balance-sheet" or "cash-flow" or "cash-flow-details" or "noncash" or "historical-stock" or "receipt-accruals"){reportGrid.RowHeight=double.NaN;foreach(var column in reportGrid.Columns.OfType<DataGridTextColumn>()){var style=new Style(typeof(TextBlock),column.ElementStyle);style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.None));style.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));column.ElementStyle=style;}}for(int i=0;i<reportGrid.Columns.Count;i++)reportGrid.Columns[i].MinWidth=i==0?220:130;reportGrid.MaxHeight=Math.Max(180,workspaceViewport.ActualHeight-(key=="child-trial"?580:420));target.Children.Add(reportGrid);
            if(key is "profit" or "balance-sheet")target.Children.Add(Btn(T("كشف الحساب المحدد","Selected account statement"),()=>{if(reportGrid.SelectedItem is not DataRowView row)return;int index=current.Rows.IndexOf(row.Row);var ids=(List<long?>)current.ExtendedProperties["statementAccounts"]!;if(index<0||ids[index] is not long id)throw new InvalidOperationException(T("اختر سطر حساب وليس إجماليًا","Select an account row, not a total"));account.SelectedItem=account.Items.Cast<Account>().Single(a=>a.Id==id);type.SelectedValue="ledger";children.IsChecked=false;Run();}));
            if(key is "main-trial" or "child-trial"){
                var summaries=S.HierarchyTrial(a,b,key=="child-trial"?GetAccount(account).Id:null,vm.Arabic);
                target.Children.Insert(1,Actions(Btn(T("عرض الفروع","View children"),()=>{if(reportGrid.SelectedItem is not DataRowView row)return;int index=current.Rows.IndexOf(row.Row);if(index>=summaries.Count)return;long id=summaries[index].Account;account.ItemsSource=UiAccounts();account.SelectedItem=account.Items.Cast<Account>().Single(x=>x.Id==id);type.SelectedValue="child-trial";Run();}),Btn(T("كشف الحساب المحدد","Selected account statement"),()=>{if(reportGrid.SelectedItem is not DataRowView row)return;int index=current.Rows.IndexOf(row.Row);if(index>=summaries.Count)return;long id=summaries[index].Account;account.ItemsSource=UiAccounts();account.SelectedItem=account.Items.Cast<Account>().Single(x=>x.Id==id);type.SelectedValue="ledger";children.IsChecked=!summaries[index].Direct;Run();})));
            }if(current.Rows.Count==0)target.Children.Add(Text(T("لا توجد حركات في هذه الفترة","No movements in this period"),18));}
        body.Children.Add(Actions(Btn(T("عرض التقرير","View report"),Run,true),classify,Btn(T("طباعة / PDF","Print / PDF"),()=>{if(current==null)Run();var snapshot=captureDisplayed!();Print(ReportDocument(snapshot.Title,snapshot.ToTable(),snapshot.Note,snapshot.Company,snapshot.Language));}),Btn(T("تصدير CSV","Export CSV"),()=>{if(current==null)Run();var snapshot=captureDisplayed!();Export(snapshot.ToTable(),snapshot);})));body.Children.Add(Actions(Btn(T("حفظ نسخة التقرير المعروض","Save displayed report snapshot"),()=>{if(displayedSnapshot==null)Run();long id=S.SaveReportSnapshot(captureDisplayed!());vm.Status=T("حُفظت نسخة التقرير رقم ","Saved report snapshot #")+id;}),Btn(T("النسخ المحفوظة","Saved snapshots"),SavedReportSnapshots)));body.Children.Add(target);Run();
    }
    DataTable ReportData(string key,DateTime from,DateTime to,long? account,bool includeChildren=true)
    {
        string start=from.ToString("yyyy-MM-dd"),end=to.ToString("yyyy-MM-dd");
        DataTable Result(params string[] columns){var t=new DataTable();foreach(var c in columns)t.Columns.Add(c);return t;}
        if(key is "main-trial" or "child-trial" or "trial") {
            var t=Result(T("الحساب","Account"),T("حركة مدينة JOD","Debit movements JOD"),T("حركة دائنة JOD","Credit movements JOD"),T("رصيد مدين JOD","Debit balance JOD"),T("رصيد دائن JOD","Credit balance JOD"));
            List<AccountSummary> rows;
            if(key!="trial")rows=S.HierarchyTrial(from,to,key=="child-trial"?account:null,vm.Arabic);
            else rows=S.Table("SELECT a.id,coalesce(sum(CASE WHEN d.date>=@p0 THEN e.debit ELSE 0 END),0) debit,coalesce(sum(CASE WHEN d.date>=@p0 THEN e.credit ELSE 0 END),0) credit,coalesce(sum(e.debit-e.credit),0) balance FROM accounts a LEFT JOIN entries e ON e.account=a.id AND e.doc IN (SELECT id FROM documents WHERE date<=@p1) LEFT JOIN documents d ON d.id=e.doc GROUP BY a.id ORDER BY a.code",start,end).Rows.Cast<DataRow>().Select(r=>new AccountSummary((long)r["id"],S.LocalName("accounts",(long)r["id"],vm.Arabic),(long)r["debit"],(long)r["credit"],(long)r["balance"])).ToList();
            foreach(var row in rows)t.Rows.Add(row.Name+(row.Direct?T(" (حركات مباشرة)"," (direct postings)"):""),Store.Money(row.Debit),Store.Money(row.Credit),Store.Money(Math.Max(0,row.Balance)),Store.Money(Math.Max(0,-row.Balance)));
            t.Rows.Add(T("الإجمالي — الأرصدة حتى نهاية الفترة","Total — balances through period end"),Store.Money(rows.Sum(r=>r.Debit)),Store.Money(rows.Sum(r=>r.Credit)),Store.Money(rows.Sum(r=>Math.Max(0,r.Balance))),Store.Money(rows.Sum(r=>Math.Max(0,-r.Balance))));return t;
        }
        if(key=="ledger") {
            string filter=includeChildren?"e.account IN (WITH RECURSIVE family(id) AS (SELECT @p0 UNION ALL SELECT a.id FROM accounts a JOIN family f ON a.parent_id=f.id) SELECT id FROM family)":"e.account=@p0";
            var t=Result(T("التاريخ / المستند","Date / document"),T("الحساب","Account"),T("البيان","Description"),T("مدين JOD","Debit JOD"),T("دائن JOD","Credit JOD"),T("الرصيد JOD","Balance JOD"),T("رصيد مدين JOD","Debit balance JOD"),T("رصيد دائن JOD","Credit balance JOD"));
            long balance=Convert.ToInt64(S.Scalar("SELECT coalesce(sum(e.debit-e.credit),0) FROM entries e JOIN documents d ON d.id=e.doc WHERE "+filter+" AND d.date<@p1",account,start));t.Rows.Add(T("رصيد أول الفترة","Opening balance"),"","","","",Store.Money(balance),Store.Money(Math.Max(0,balance)),Store.Money(Math.Max(0,-balance)));
            var rows=S.Table("SELECT d.date,d.number,CASE WHEN e.description='' THEN d.note ELSE e.description END note,e.account,e.debit,e.credit FROM entries e JOIN documents d ON d.id=e.doc WHERE "+filter+" AND d.date BETWEEN @p1 AND @p2 ORDER BY d.date,d.id,e.id",account,start,end);
            foreach(DataRow r in rows.Rows){balance+=(long)r["debit"]-(long)r["credit"];t.Rows.Add(r["date"]+" / "+r["number"],S.LocalName("accounts",(long)r["account"],vm.Arabic),r["note"],Store.Money((long)r["debit"]),Store.Money((long)r["credit"]),Store.Money(balance),Store.Money(Math.Max(0,balance)),Store.Money(Math.Max(0,-balance)));}return t;
        }
        if(key=="journal") {
            var t=Result(T("التاريخ / الرقم","Date / number"),T("الحساب","Account"),T("البيان","Description"),T("مدين JOD","Debit JOD"),T("دائن JOD","Credit JOD"));
            foreach(DataRow r in S.Table("SELECT d.date,d.number,a.name,CASE WHEN e.description='' THEN d.note ELSE e.description END note,e.debit,e.credit FROM entries e JOIN documents d ON d.id=e.doc JOIN accounts a ON a.id=e.account WHERE d.date BETWEEN @p0 AND @p1 ORDER BY d.date,d.id,e.id",start,end).Rows)t.Rows.Add(r["date"]+" / "+r["number"],r["name"],r["note"],Store.Money((long)r["debit"]),Store.Money((long)r["credit"]));return t;
        }
        if(key=="stock") {
            var t=Result(T("الصنف","Item"),T("الوارد منذ البداية","In since start"),T("الصادر منذ البداية","Out since start"),T("الكمية الحالية","Current quantity"),T("القيمة JOD","Value JOD"));var flows=S.StockFlows();long sum=0;foreach(var i in UiItems().Where(i=>i.Stock)){var flow=flows.GetValueOrDefault(i.Id);sum+=i.Value;t.Rows.Add(i.Name,Store.D(flow.In).ToString("0.###",CultureInfo.InvariantCulture),Store.D(flow.Out).ToString("0.###",CultureInfo.InvariantCulture),Store.D(i.Qty).ToString("0.###",CultureInfo.InvariantCulture),Store.Money(i.Value));}t.Rows.Add(T("الإجمالي الحالي (لا يتأثر بالفترة)","Current total (ignores date range)"),"","","",Store.Money(sum));return t;
        }
        if(key=="historical-stock"){
            var report=A.InventoryAsOf(to);var t=Result(T("الصنف","Item"),T("الموقع","Location"),T("الكمية حتى التاريخ","Quantity as of date"),T("القيمة JOD","Value JOD"));
            foreach(var row in report.Rows)t.Rows.Add(S.LocalName("items",row.Item,vm.Arabic),row.Location==0?T("قيمة دون كمية — للمراجعة","Value without quantity — review"):S.Scalar("SELECT name FROM locations WHERE id=@p0",row.Location),Store.D(row.Quantity).ToString("0.###",CultureInfo.InvariantCulture),Store.Money(row.Value));
            t.Rows.Add(T("إجمالي تقييم المخزون","Total stock valuation"),"","",Store.Money(report.StockValue));t.Rows.Add(T("رصيد المخزون في الأستاذ","Inventory GL balance"),"","",Store.Money(report.LedgerValue));t.Rows.Add(T("فرق التسوية — يجب أن يكون صفرًا","Reconciliation difference — must be zero"),"","",Store.Money(report.Difference));return t;
        }
        if(key=="receipt-accruals"){
            var report=A.ReceiptAccruals(to);var t=Result(T("الاستلام / التاريخ","Receipt / date"),T("المورد","Supplier"),T("الصنف","Item"),T("الكمية غير المفوترة","Unbilled quantity"),T("القيمة JOD","Value JOD"));
            foreach(var row in report.Rows)t.Rows.Add(row.Number+" · "+row.Date,S.LocalName("accounts",row.Supplier,vm.Arabic),S.LocalName("items",row.Item,vm.Arabic),Store.D(row.Quantity).ToString("0.###",CultureInfo.InvariantCulture),Store.Money(row.Value));
            t.Rows.Add(T("إجمالي الاستلامات غير المفوترة","Total unbilled receipts"),"","","",Store.Money(report.PendingValue));t.Rows.Add(T("رصيد الاستلامات في الأستاذ","GRNI ledger balance"),"","","",Store.Money(report.LedgerValue));t.Rows.Add(T("فرق التسوية — يجب أن يكون صفرًا","Reconciliation difference — must be zero"),"","","",Store.Money(report.Difference));return t;
        }
        if(key=="moves") {
            var t=Result(T("التاريخ / المستند","Date / document"),T("الصنف","Item"),T("الوارد","In"),T("الصادر","Out"),T("تغير القيمة JOD","Value change JOD"),T("الحالة","Status"));
            foreach(DataRow r in S.Table("SELECT d.date,d.number,i.name,m.qty,m.value,d.reversed FROM stock_moves m JOIN items i ON i.id=m.item JOIN documents d ON d.id=m.doc WHERE d.date BETWEEN @p0 AND @p1 ORDER BY d.date,d.id,m.id",start,end).Rows)t.Rows.Add(r["date"]+" / "+r["number"],r["name"],Store.D(Math.Max(0,(long)r["qty"])).ToString("0.###",CultureInfo.InvariantCulture),Store.D(Math.Max(0,-(long)r["qty"])).ToString("0.###",CultureInfo.InvariantCulture),Store.Money((long)r["value"]),(long)r["reversed"]==1?T("ملغى — قيد الإلغاء معروض منفصلًا","Cancelled — reversal listed separately"):T("محفوظ","Posted"));return t;
        }
        if(key=="noncash")return NoncashData(from,to);
        if(key=="cash-flow-details")return CashFlowDetails(from,to);
        if(key is "profit" or "balance-sheet" or "cash-flow")return FinancialStatementData(key,from,to);
        if(key=="party-reconciliation"){
            var t=Result(T("الحساب","Account"),T("النوع","Type"),T("رصيد الأستاذ JOD","Ledger balance JOD"),T("متبقي الفواتير JOD","Invoice outstanding JOD"),T("خارج أعمار الفواتير — للمراجعة JOD","Outside invoice aging — review JOD"));
            foreach(var p in A.PartyReconciliation(to))t.Rows.Add(S.LocalName("accounts",p.Account,vm.Arabic),p.Kind=="Customer"?T("عميل","Customer"):T("مورد","Supplier"),Store.Money(p.Ledger),Store.Money(p.InvoiceOutstanding),Store.Money(p.OutsideAging));
            return t;
        }
        if(key=="aging"){
            var t=Result(T("الفاتورة / الحساب","Invoice / account"),T("الاستحقاق","Due date"),T("غير مستحق JOD","Not due JOD"),"1–30 JOD","31–60 JOD","61–90 JOD",T("أكثر من 90 JOD","Over 90 JOD"),T("رصيد دائن JOD","Credit JOD"));
            foreach(var p in A.Positions(to)){int age=(to.Date-DateTime.Parse(p.Due)).Days;var amounts=new long[6];int bucket=p.Outstanding<0?5:age<=0?0:age<=30?1:age<=60?2:age<=90?3:4;amounts[bucket]=p.Outstanding;t.Rows.Add(new object[]{Kind(p.Kind)+" "+p.Number+" · "+p.Party,p.Due}.Concat(amounts.Select(v=>(object)Store.Money(v))).ToArray());}return t;
        }
        var tax=Result(T("التاريخ / المستند","Date / document"),T("نوع الحركة","Event type"),T("وعاء المبيعات JOD","Sales base JOD"),T("ضريبة المخرجات JOD","Output tax JOD"),T("وعاء المشتريات JOD","Purchase base JOD"),T("ضريبة المدخلات JOD","Input tax JOD"));long sales=0,output=0,purchases=0,input=0;
        foreach(DataRow r in A.TaxEvents(from,to).Rows){long n=(long)r["net"]*(long)r["sign"],v=(long)r["tax"]*(long)r["sign"];bool sale=r["kind"].ToString() is "SI" or "SR";if(sale){sales+=n;output+=v;}else{purchases+=n;input+=v;}tax.Rows.Add(r["date"]+" / "+r["number"],Kind((string)r["kind"]),sale?Store.Money(n):"",sale?Store.Money(v):"",sale?"":Store.Money(n),sale?"":Store.Money(v));}
        tax.Rows.Add(T("إجمالي ورقة العمل الأردنية","Jordan worksheet totals"),"",Store.Money(sales),Store.Money(output),Store.Money(purchases),Store.Money(input));tax.Rows.Add(T("صافي المخرجات ناقص المدخلات","Net output less input tax"),Store.Money(output-input),"","","","");return tax;
    }
    FlowDocument Page(string title,string company)
    {
        var doc=new FlowDocument{FontFamily=FontFamily,FontSize=13,FlowDirection=FlowDirection,PagePadding=new(42),ColumnWidth=double.PositiveInfinity,PageWidth=794,PageHeight=1123};
        doc.Blocks.Add(new Paragraph(new Run(company)){FontSize=24,FontWeight=FontWeights.Bold,Margin=new(0,0,0,16)});doc.Blocks.Add(new Paragraph(new Run(title)){FontSize=19,FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,16)});return doc;
    }
    void Paragraph(FlowDocument doc,string text,bool bold=false)=>doc.Blocks.Add(new Paragraph(new Run(text)){FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,Margin=new(0,0,0,10)});
    void PrintTable(FlowDocument doc,IEnumerable<string> headers,IEnumerable<IEnumerable<string>> rows)
    {
        var table=new System.Windows.Documents.Table{CellSpacing=0,Margin=new(0,8,0,16)};var h=headers.ToList();for(int i=0;i<h.Count;i++)table.Columns.Add(new TableColumn{Width=new GridLength(i==0?3:1,GridUnitType.Star)});
        var group=new TableRowGroup();table.RowGroups.Add(group);
        TableRow Row(IEnumerable<string> values,bool header){var row=new TableRow();foreach(var value in values){var cell=new TableCell(new Paragraph(new Run(value)){Margin=new(0)}){Padding=new(7),BorderBrush=Brushes.LightGray,BorderThickness=new(0.5),FontWeight=header?FontWeights.Bold:FontWeights.Normal};if(header)cell.Background=new SolidColorBrush(Color.FromRgb(232,240,237));row.Cells.Add(cell);}return row;}
        group.Rows.Add(Row(h,true));foreach(var row in rows)group.Rows.Add(Row(row,false));doc.Blocks.Add(table);
    }
    FlowDocument InvoiceReview(string title,string party,DateTime date,List<InvoiceLine> lines,Totals totals,decimal paid,decimal vat,bool inclusive,string note)
    {
        var doc=Page(T("مراجعة — ","Review — ")+title,S.Setting("company"));Paragraph(doc,T("التاريخ: ","Date: ")+date.ToString("yyyy-MM-dd"));Paragraph(doc,T("العميل / المورد: ","Customer / supplier: ")+party);PrintTable(doc,new[]{T("الوصف","Description"),T("الكمية","Qty"),T("السعر","Price"),T("الخصم","Discount")},lines.Select(l=>new[]{l.Description+(string.IsNullOrWhiteSpace(l.Serials)?"":"\n"+l.Serials),l.Qty.ToString(CultureInfo.InvariantCulture),l.Price.ToString("N3",CultureInfo.InvariantCulture),l.Discount.ToString("N3",CultureInfo.InvariantCulture)}));
        AddTotals(doc,totals,Store.M(paid),vat.ToString(CultureInfo.InvariantCulture),inclusive);Paragraph(doc,note);return doc;
    }
    void AddTotals(FlowDocument doc,Totals t,long paid,string vat,bool inclusive)
    {
        Paragraph(doc,T("طريقة السعر: ","Price mode: ")+(inclusive?T("شامل الضريبة","Tax inclusive"):T("قبل الضريبة","Before tax")));Paragraph(doc,T("الصافي: ","Net: ")+Store.Money(t.Net)+" JOD");Paragraph(doc,T("الضريبة ","Tax ")+vat+"%: "+Store.Money(t.Tax)+" JOD");Paragraph(doc,T("الإجمالي: ","Total: ")+Store.Money(t.Total)+" JOD",true);Paragraph(doc,T("المدفوع عند إصدار الفاتورة: ","Paid at issue: ")+Store.Money(paid)+" JOD");Paragraph(doc,T("الآجل عند الإصدار: ","Credit at issue: ")+Store.Money(t.Total-paid)+" JOD");
    }
    bool Review(FlowDocument doc)
    {
        bool accepted=false;var (w,p,finish)=Dialog(T("راجع الفاتورة قبل حفظها","Review invoice before saving"),1000,false);var view=new FlowDocumentScrollViewer{Document=doc,Height=480,IsToolBarVisible=false};p.Children.Add(view);p.Children.Add(Actions(Btn(T("تأكيد وحفظ","Confirm & save"),()=>{accepted=true;finish();},true),Btn(T("رجوع للتعديل","Back to edit"),finish)));w.ShowDialog();return accepted;
    }
    public void ShowDocument(long id)
    {
        var row=S.Table("SELECT d.*,coalesce(a.name,'') party_name FROM documents d LEFT JOIN accounts a ON a.id=d.party WHERE d.id=@p0",id).Rows[0];bool combined=row["receipt_group"]!=DBNull.Value;var title=(combined?T("سند قبض بشيكات","Cheque receipt"):Kind((string)row["kind"]))+" · "+(combined?S.Scalar("SELECT number FROM documents WHERE id=@p0",row["receipt_group"]):row["number"]);
        var branding=JsonSerializer.Deserialize<Dictionary<string,string>>((string)row["branding"])??new();
        string Brand(string key,string fallback="")=>branding.TryGetValue(key,out var value)&&!string.IsNullOrWhiteSpace(value)?value:fallback;
        var doc=Page(title,vm.Arabic?(string)row["company"]:Brand("company_en",(string)row["company"]));
        void Picture(string field,double width,double height){if(row[field] is byte[] bytes&&bytes.Length>0){using var stream=new MemoryStream(bytes);var bitmap=new System.Windows.Media.Imaging.BitmapImage();bitmap.BeginInit();bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();doc.Blocks.Add(new BlockUIContainer(new Image{Source=bitmap,MaxWidth=width,MaxHeight=height,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left}));}}
        Picture("logo",160,80);
        if(!combined&&(long)row["reversed"]==1)Paragraph(doc,T("ملغى — هذا المستند لا يمثل حركة سارية","CANCELLED — this document is no longer active"),true);
        Paragraph(doc,T("التاريخ: ","Date: ")+row["date"]);if(row["party_name"].ToString()!=""){Paragraph(doc,T("إلى: ","Bill to: ")+Brand(vm.Arabic?"party_name_ar":"party_name_en",Brand("party_name",row["party_name"].ToString()!)));var address=Brand(vm.Arabic?"party_address_ar":"party_address_en");if(address!="")Paragraph(doc,address);}if(!combined&&row["due_date"].ToString()!="")Paragraph(doc,T("تاريخ الاستحقاق: ","Due date: ")+row["due_date"]);
        if(row["kind"].ToString() is "OC" or "OS"){
            var opening=S.Table("SELECT o.*,d.number source_number FROM opening_balances o LEFT JOIN documents d ON d.id=o.source_doc WHERE o.doc=@p0",id).Rows[0];
            Paragraph(doc,T("مرجع الفاتورة الأصلية: ","Original invoice reference: ")+opening["reference"]);
            Paragraph(doc,T("تاريخ الفاتورة الأصلية: ","Original invoice date: ")+opening["original_date"]);
            Paragraph(doc,opening["source_doc"]==DBNull.Value?T("رصيد افتتاحي جديد مقابل حساب الرصيد الافتتاحي","New balance posted against opening equity"):T("تفاصيل فقط للقيد: ","Details only for journal: ")+opening["source_number"]);
            var position=A.Positions(DateTime.Today).FirstOrDefault(p=>p.Invoice==id);if(position!=null){Paragraph(doc,T("المدفوع حتى اليوم: ","Paid through today: ")+Store.Money(position.Paid)+" JOD");Paragraph(doc,T("المتبقي حتى اليوم: ","Outstanding through today: ")+Store.Money(position.Outstanding)+" JOD",true);}
        }
        var lines=S.Table("SELECT * FROM invoice_lines WHERE doc=@p0 ORDER BY id",id);
        if(lines.Rows.Count>0){PrintTable(doc,new[]{T("الوصف","Description"),T("الكمية","Qty"),T("السعر JOD","Price JOD"),T("الخصم JOD","Discount JOD"),T("الصافي JOD","Net JOD")},lines.Rows.Cast<DataRow>().Select(r=>new[]{string.IsNullOrWhiteSpace(r[vm.Arabic?"description_ar":"description_en"].ToString())?(string)r["description"]:r[vm.Arabic?"description_ar":"description_en"].ToString()!,Store.D((long)r["qty"]).ToString("0.###",CultureInfo.InvariantCulture),Store.Money((long)r["price"]),Store.Money((long)r["discount"]),Store.Money((long)r["net"])}));AddTotals(doc,new((long)row["net"],(long)row["tax"],(long)row["total"]),(long)row["paid"],(string)row["vat"],(long)row["inclusive"]==1);
            var position=A.Positions(DateTime.Today).FirstOrDefault(p=>p.Invoice==id);if(position!=null){Paragraph(doc,T("المدفوع حتى اليوم: ","Paid through today: ")+Store.Money(position.Paid)+" JOD");Paragraph(doc,T("المرتجعات حتى اليوم: ","Returns through today: ")+Store.Money(position.Returned)+" JOD");Paragraph(doc,T("المتبقي حتى اليوم: ","Outstanding through today: ")+Store.Money(position.Outstanding)+" JOD",true);}
            foreach(var f in new[]{("terms",T("شروط الدفع: ","Payment terms: ")),("warranty",T("الضمان: ","Warranty: ")),("shipping",T("الشحن والتركيب: ","Shipping / installation: ")),("bank",T("بيانات الدفع: ","Payment details: "))}){var text=vm.Arabic?row[f.Item1].ToString():Brand(f.Item1+"_en",row[f.Item1].ToString()!);if(text!="")Paragraph(doc,f.Item2+text);}
        }else if(!combined){Paragraph(doc,T("المبلغ: ","Amount: ")+Store.Money((long)row["total"])+" JOD",true);var entries=S.Table("SELECT a.name,coalesce(p.name,'') parent,e.description,e.debit,e.credit FROM entries e JOIN accounts a ON a.id=e.account LEFT JOIN accounts p ON p.id=a.parent_id WHERE doc=@p0",id);PrintTable(doc,new[]{T("الحساب","Account"),T("الحساب الرئيسي","Main account"),T("البيان","Description"),T("مدين JOD","Debit JOD"),T("دائن JOD","Credit JOD")},entries.Rows.Cast<DataRow>().Select(r=>new[]{(string)r["name"],(string)r["parent"],(string)r["description"],Store.Money((long)r["debit"]),Store.Money((long)r["credit"])}));}
        var priceAdjustments=S.Table("SELECT l.description,l.net-b.base_value difference,b.inventory_adjustment FROM receipt_bills b JOIN invoice_lines l ON l.id=b.line WHERE l.doc=@p0 AND b.current_variance=1",id);
        if(priceAdjustments.Rows.Count>0){Paragraph(doc,T("فرق سعر مثبت في الفترة الحالية؛ الاستلام المقفل لم يتغير.","Price difference posted in the current period; the closed receipt is unchanged."));PrintTable(doc,new[]{T("الصنف","Item"),T("فرق السعر JOD","Difference JOD"),T("حصة المخزون JOD","Inventory share JOD"),T("تكلفة المبيعات JOD","Cost of sales JOD")},priceAdjustments.Rows.Cast<DataRow>().Select(r=>new[]{r["description"].ToString()!,Store.Money((long)r["difference"]),Store.Money((long)r["inventory_adjustment"]),Store.Money((long)r["difference"]-(long)r["inventory_adjustment"])}));}
        var devices=S.Table("SELECT u.number,i.name,l.name location,e.direction FROM serial_events e JOIN serial_units u ON u.id=e.unit JOIN items i ON i.id=u.item JOIN locations l ON l.id=e.location WHERE e.doc=@p0 ORDER BY e.id",id);
        if(devices.Rows.Count>0)PrintTable(doc,new[]{T("الصنف","Item"),T("التسلسلي","Serial"),T("الموقع","Location"),T("الحركة","Movement")},devices.Rows.Cast<DataRow>().Select(r=>new[]{(string)r["name"],(string)r["number"],(string)r["location"],(long)r["direction"]>0?T("وارد","In"):T("صادر","Out")}));
        if(row["kind"].ToString() is "IA" or "IT" or "OB"){
            var stock=S.Table("SELECT i.name,l.name location,m.qty,m.value FROM stock_moves m JOIN items i ON i.id=m.item JOIN locations l ON l.id=m.location WHERE m.doc=@p0",id);
            PrintTable(doc,new[]{T("الصنف","Item"),T("الموقع","Location"),T("الكمية","Quantity"),T("القيمة JOD","Value JOD")},stock.Rows.Cast<DataRow>().Select(r=>new[]{(string)r["name"],(string)r["location"],Store.D((long)r["qty"]).ToString("0.###",CultureInfo.InvariantCulture),Store.Money((long)r["value"])}));
        }
        if(row["note"].ToString()!="")Paragraph(doc,T("البيان: ","Description: ")+row["note"]);Paragraph(doc,T("التوقيع: __________________","Signature: __________________"));
        Picture("seal",120,70);
        AddChequeReceipt(doc,id);
        if(row["kind"].ToString()=="OT"){var movements=S.Table("SELECT i.name,m.qty,m.value FROM stock_moves m JOIN items i ON i.id=m.item WHERE doc=@p0",id);PrintTable(doc,new[]{T("الصنف","Item"),T("الوارد","In"),T("التكلفة JOD","Cost JOD")},movements.Rows.Cast<DataRow>().Select(r=>new[]{(string)r["name"],Store.D((long)r["qty"]).ToString("0.###",CultureInfo.InvariantCulture),Store.Money((long)r["value"])}));}
        if(previewDirectory!=null)RenderPrintPages(doc,previewDirectory,(vm.Arabic?"ar":"en")+"-print");
        var (w,p,_)=Dialog(title,1000,false);var viewer=new FlowDocumentScrollViewer{Document=doc,Height=500,IsToolBarVisible=false};p.Children.Add(viewer);p.Children.Add(Actions(Btn(T("طباعة / حفظ PDF","Print / save PDF"),()=>Print(doc),true),Btn(T("إغلاق","Close"),()=>w.Close())));w.ShowDialog();
    }
    static void RenderPrintPages(FlowDocument doc,string directory,string name)
    {
        var paginator=((IDocumentPaginatorSource)doc).DocumentPaginator;paginator.PageSize=new Size(794,1123);paginator.ComputePageCount();
        for(int i=0;i<paginator.PageCount;i++){var page=paginator.GetPage(i);var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(794,1123,96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()){drawing.DrawRectangle(Brushes.White,null,new Rect(0,0,794,1123));drawing.DrawRectangle(new VisualBrush(page.Visual),null,new Rect(0,0,page.Size.Width,page.Size.Height));}bitmap.Render(visual);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var file=File.Create(System.IO.Path.Combine(directory,name+"-"+(i+1)+".png"));encoder.Save(file);}
    }
    string ReportNote(string key)=>key switch {
        "noncash"=>T("إفصاحات مسجلة يدويًا مرتبطة بالقيود؛ لا تؤكد اكتمال جميع العمليات غير النقدية. لا تضاف إلى التدفقات النقدية. العكس يظهر سالبًا بنفس تصنيف الأصل. راجع أي قيد متغير.","Explicit journal-linked disclosures; this register does not certify that every noncash transaction has been identified. Excluded from cash flows. Reversals appear negative under the original category. Review changed journals."),
        "cash-flow" or "cash-flow-details"=>T("الطريقة المباشرة من قيود الحسابات النقدية. المدفوعات سالبة. التحويلات الداخلية مستبعدة من الأنشطة. صنّف الحركات غير المصنفة قبل الاعتماد؛ التحويلات الجزئية يجب أن تتوازن في كل مستند. التصنيفات الحالية تطبق على المقارنة، والعكس يرث تصنيف الأصل ما لم يصنف منفصلاً.","Direct method from Cash-type ledger accounts. Payments are negative. Internal transfers are excluded from activities. Classify unknown movements before relying on this report; transfer splits must balance within each document. Current classifications apply to comparisons; reversals inherit original classifications unless separately classified."),
        "historical-stock"=>T("قيمة المواقع موزعة بمتوسط تكلفة الصنف على مستوى الشركة حتى التاريخ. عند صافي كمية صفر تُعرض كميات المواقع دون توزيع قيمة، وأي قيمة متبقية تظهر منفصلة للمراجعة. الفترات المفتوحة قد يعاد احتسابها؛ الفترات المقفلة تحتفظ بالقيم المسجلة.","Location values use the company-wide item average at the cutoff. At zero net quantity, location quantities carry no allocated value; any residual value is shown separately for review. Open periods can be recalculated; closed periods retain recorded values."),
        "receipt-accruals"=>T("القيمة المتبقية بتكلفة الاستلام الأصلية، دون ضريبة الفاتورة أو فروق أسعارها.","Remaining value uses original receipt basis, excluding invoice tax and invoice price differences."),
        "profit"=>T("المقارنة لنفس الفترة قبل سنة. القيود الختامية مستبعدة. راجع الحسابات غير المصنفة قبل اعتماد المجاميع.","Comparison is the same date range one year earlier. Closing transfers are excluded. Review unmapped accounts before relying on subtotals."),
        "balance-sheet"=>T("المقارنة لنفس التاريخ قبل سنة. الأرباح غير المقفلة منفصلة عن حقوق الملكية المرحلة. أرصدة العملاء الدائنة التزامات وأرصدة الموردين المدينة أصول.","Comparison is the same date one year earlier. Unclosed earnings are separate from posted equity. Customer credits are liabilities; supplier debits are assets."),
        "party-reconciliation"=>T("الرصيد الموجب: مستحق من العميل أو للمورد. الفرق يشمل الافتتاحيات والدفعات غير الموزعة وأي فرق يحتاج مراجعة؛ لا يُنسب إلى تاريخ استحقاق. راجع كشف الحساب للتفاصيل.","Positive balances are due from customers or to suppliers. The difference includes openings, unallocated payments and any discrepancy requiring review; it has no assumed due date. Use the account statement for details."),
        "aging"=>T("هذا التقرير للفواتير فقط. راجع تسوية ذمم العملاء والموردين للأرصدة الافتتاحية والدفعات غير الموزعة.","This report ages invoices only. Use Customer / supplier reconciliation for opening balances and unallocated payments."),
        _=>""
    };
    FlowDocument ReportDocument(string title,DataTable data,string note="",string? company=null,string? language=null)
    {
        var doc=Page(title,company??S.Setting("company"));if(language!=null)doc.FlowDirection=language=="ar"?FlowDirection.RightToLeft:FlowDirection.LeftToRight;if(note.Length>0)Paragraph(doc,note);PrintTable(doc,data.Columns.Cast<DataColumn>().Select(c=>c.ColumnName),data.Rows.Cast<DataRow>().Select(r=>r.ItemArray.Select(v=>v?.ToString()??"")));return doc;
    }
    void Print(FlowDocument doc)
    {
        var dialog=new PrintDialog();if(dialog.ShowDialog()!=true)return;doc.PageWidth=dialog.PrintableAreaWidth;doc.PageHeight=dialog.PrintableAreaHeight;dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator,"Hisab");
    }
    void Export(DataTable table,ReportSnapshot? snapshot=null)
    {
        var save=new SaveFileDialog{Filter="CSV (*.csv)|*.csv",FileName="Hisab-report-"+DateTime.Today.ToString("yyyyMMdd")+".csv"};if(save.ShowDialog(this)!=true)return;
        File.WriteAllText(save.FileName,snapshot==null?ReportCsv.Serialize(table):ReportCsv.SerializeSnapshot(snapshot),new UTF8Encoding(true));vm.Status=T("تم تصدير التقرير","Report exported");
    }
}