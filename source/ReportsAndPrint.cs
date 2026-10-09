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
        var type=new ComboBox{ItemsSource=new[]{new Choice("trial",T("ميزان المراجعة","Trial balance")),new Choice("ledger",T("كشف حساب / دفتر الأستاذ","Account statement / ledger")),new Choice("journal",T("دفتر اليومية","Daybook")),new Choice("stock",T("جرد المخزون الحالي","Current inventory")),new Choice("moves",T("حركة المخزون","Stock movements")),new Choice("profit",T("الأرباح والخسائر","Profit & loss")),new Choice("aging",T("أعمار الذمم في نهاية الفترة","Aging at period end")),new Choice("tax",T("ورقة ضريبة المبيعات الأردنية","Jordan sales-tax worksheet"))},DisplayMemberPath="Label",SelectedValuePath="Key",SelectedValue=initial};
        var from=Date();from.SelectedDate=new DateTime(DateTime.Today.Year,1,1);var to=Date();var account=Choose(UiAccounts());account.SelectedIndex=0;
        var filters=new WrapPanel();var reportField=Field(T("التقرير","Report"),type);reportField.Width=320;reportField.Margin=new(0,0,16,12);filters.Children.Add(reportField);var dates=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};var f=Field(T("من تاريخ","From"),from);f.Margin=new(0,0,14,12);dates.Children.Add(f);dates.Children.Add(Field(T("إلى تاريخ","To"),to));dates.Width=400;filters.Children.Add(dates);body.Children.Add(filters);var accountField=Field(T("الحساب (لكشف الحساب)","Account (for statement)"),account);body.Children.Add(accountField);
        var children=new CheckBox{Content=T("تضمين الحسابات الفرعية في كشف الحساب","Include child accounts in statement"),IsChecked=true};body.Children.Add(children);
        void StatementOptions(){bool statement=type.SelectedValue?.ToString()=="ledger";children.Visibility=statement?Visibility.Visible:Visibility.Collapsed;accountField.Visibility=children.Visibility;}type.SelectionChanged+=(s,e)=>StatementOptions();StatementOptions();
        var target=new StackPanel();DataTable? current=null;string title="";
        void Run(){var a=GetDate(from);var b=GetDate(to);if(a>b)throw new InvalidOperationException(T("تاريخ البداية بعد النهاية","Start date is after end date"));string key=type.SelectedValue?.ToString()??"trial";title=(type.SelectedItem as Choice)!.Label+" · "+a.ToString("yyyy-MM-dd")+" → "+b.ToString("yyyy-MM-dd");if(key=="ledger")title+=" · "+GetAccount(account).Name+(children.IsChecked==true?T(" (مع الفروع)"," (including children)"):"");current=ReportData(key,a,b,key=="ledger"?GetAccount(account).Id:null,children.IsChecked==true);target.Children.Clear();target.Children.Add(Text(title,21,true));var cols=current.Columns.Cast<DataColumn>().Select(c=>(c.ColumnName,c.ColumnName,c.Ordinal==0?2.0:1.0)).ToArray();var reportGrid=Grid(current,cols);reportGrid.ColumnHeaderHeight=double.NaN;for(int i=0;i<reportGrid.Columns.Count;i++)reportGrid.Columns[i].MinWidth=i==0?220:130;target.Children.Add(reportGrid);if(current.Rows.Count==0)target.Children.Add(Text(T("لا توجد حركات في هذه الفترة","No movements in this period"),18));}
        body.Children.Add(Actions(Btn(T("عرض التقرير","View report"),Run,true),Btn(T("طباعة / PDF","Print / PDF"),()=>{if(current==null)Run();Print(ReportDocument(title,current!));}),Btn(T("تصدير CSV","Export CSV"),()=>{if(current==null)Run();Export(current!);})));body.Children.Add(target);Run();
    }
    DataTable ReportData(string key,DateTime from,DateTime to,long? account,bool includeChildren=true)
    {
        string start=from.ToString("yyyy-MM-dd"),end=to.ToString("yyyy-MM-dd");
        DataTable Result(params string[] columns){var t=new DataTable();foreach(var c in columns)t.Columns.Add(c);return t;}
        if(key=="trial") {
            var t=Result(T("الحساب","Account"),T("مدين JOD","Debit JOD"),T("دائن JOD","Credit JOD"));long td=0,tc=0;
            var rows=S.Table("SELECT a.name,coalesce(sum(e.debit-e.credit),0) balance FROM accounts a LEFT JOIN entries e ON e.account=a.id AND e.doc IN (SELECT id FROM documents WHERE date<=@p0) GROUP BY a.id ORDER BY a.code",end);
            foreach(DataRow r in rows.Rows){long bal=(long)r["balance"];long d=Math.Max(0,bal),c=Math.Max(0,-bal);td+=d;tc+=c;t.Rows.Add(r["name"],Store.Money(d),Store.Money(c));}t.Rows.Add(T("المجموع حتى نهاية الفترة","Total through period end"),Store.Money(td),Store.Money(tc));return t;
        }
        if(key=="ledger") {
            string filter=includeChildren?"e.account IN (WITH RECURSIVE family(id) AS (SELECT @p0 UNION ALL SELECT a.id FROM accounts a JOIN family f ON a.parent_id=f.id) SELECT id FROM family)":"e.account=@p0";
            var t=Result(T("التاريخ / المستند","Date / document"),T("الحساب","Account"),T("البيان","Description"),T("مدين JOD","Debit JOD"),T("دائن JOD","Credit JOD"),T("الرصيد JOD","Balance JOD"));
            long balance=Convert.ToInt64(S.Scalar("SELECT coalesce(sum(e.debit-e.credit),0) FROM entries e JOIN documents d ON d.id=e.doc WHERE "+filter+" AND d.date<@p1",account,start));t.Rows.Add(T("رصيد أول الفترة","Opening balance"),"","","","",Store.Money(balance));
            var rows=S.Table("SELECT d.date,d.number,d.note,e.account,e.debit,e.credit FROM entries e JOIN documents d ON d.id=e.doc WHERE "+filter+" AND d.date BETWEEN @p1 AND @p2 ORDER BY d.date,d.id,e.id",account,start,end);
            foreach(DataRow r in rows.Rows){balance+=(long)r["debit"]-(long)r["credit"];t.Rows.Add(r["date"]+" / "+r["number"],S.LocalName("accounts",(long)r["account"],vm.Arabic),r["note"],Store.Money((long)r["debit"]),Store.Money((long)r["credit"]),Store.Money(balance));}return t;
        }
        if(key=="journal") {
            var t=Result(T("التاريخ / الرقم","Date / number"),T("الحساب","Account"),T("البيان","Description"),T("مدين JOD","Debit JOD"),T("دائن JOD","Credit JOD"));
            foreach(DataRow r in S.Table("SELECT d.date,d.number,a.name,d.note,e.debit,e.credit FROM entries e JOIN documents d ON d.id=e.doc JOIN accounts a ON a.id=e.account WHERE d.date BETWEEN @p0 AND @p1 ORDER BY d.date,d.id,e.id",start,end).Rows)t.Rows.Add(r["date"]+" / "+r["number"],r["name"],r["note"],Store.Money((long)r["debit"]),Store.Money((long)r["credit"]));return t;
        }
        if(key=="stock") {
            var t=Result(T("الصنف","Item"),T("الوارد منذ البداية","In since start"),T("الصادر منذ البداية","Out since start"),T("الكمية الحالية","Current quantity"),T("القيمة JOD","Value JOD"));var flows=S.StockFlows();long sum=0;foreach(var i in UiItems().Where(i=>i.Stock)){var flow=flows.GetValueOrDefault(i.Id);sum+=i.Value;t.Rows.Add(i.Name,Store.D(flow.In).ToString("0.###",CultureInfo.InvariantCulture),Store.D(flow.Out).ToString("0.###",CultureInfo.InvariantCulture),Store.D(i.Qty).ToString("0.###",CultureInfo.InvariantCulture),Store.Money(i.Value));}t.Rows.Add(T("الإجمالي الحالي (لا يتأثر بالفترة)","Current total (ignores date range)"),"","","",Store.Money(sum));return t;
        }
        if(key=="moves") {
            var t=Result(T("التاريخ / المستند","Date / document"),T("الصنف","Item"),T("الوارد","In"),T("الصادر","Out"),T("تغير القيمة JOD","Value change JOD"),T("الحالة","Status"));
            foreach(DataRow r in S.Table("SELECT d.date,d.number,i.name,m.qty,m.value,d.reversed FROM stock_moves m JOIN items i ON i.id=m.item JOIN documents d ON d.id=m.doc WHERE d.date BETWEEN @p0 AND @p1 ORDER BY d.date,d.id,m.id",start,end).Rows)t.Rows.Add(r["date"]+" / "+r["number"],r["name"],Store.D(Math.Max(0,(long)r["qty"])).ToString("0.###",CultureInfo.InvariantCulture),Store.D(Math.Max(0,-(long)r["qty"])).ToString("0.###",CultureInfo.InvariantCulture),Store.Money((long)r["value"]),(long)r["reversed"]==1?T("ملغى — قيد الإلغاء معروض منفصلًا","Cancelled — reversal listed separately"):T("محفوظ","Posted"));return t;
        }
        if(key=="profit") {
            var t=Result(T("الحساب","Account"),T("الإيراد / المصروف JOD","Income / expense JOD"));long profit=0;foreach(DataRow r in S.Table("SELECT a.name,a.kind,sum(e.credit-e.debit) amount FROM entries e JOIN accounts a ON a.id=e.account JOIN documents d ON d.id=e.doc WHERE a.kind IN ('Income','Expense') AND d.kind!='CL' AND NOT(d.kind='RV' AND d.original IN (SELECT id FROM documents WHERE kind='CL')) AND d.date BETWEEN @p0 AND @p1 GROUP BY a.id ORDER BY a.code",start,end).Rows){long value=(long)r["amount"];profit+=value;t.Rows.Add(r["name"],Store.Money(r["kind"].ToString()=="Expense"?-value:value));}t.Rows.Add(T("صافي الربح / الخسارة","Net profit / loss"),Store.Money(profit));return t;
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
        var doc=Page(T("مراجعة — ","Review — ")+title,S.Setting("company"));Paragraph(doc,T("التاريخ: ","Date: ")+date.ToString("yyyy-MM-dd"));Paragraph(doc,T("العميل / المورد: ","Customer / supplier: ")+party);PrintTable(doc,new[]{T("الوصف","Description"),T("الكمية","Qty"),T("السعر","Price"),T("الخصم","Discount")},lines.Select(l=>new[]{l.Description,l.Qty.ToString(CultureInfo.InvariantCulture),l.Price.ToString("N3",CultureInfo.InvariantCulture),l.Discount.ToString("N3",CultureInfo.InvariantCulture)}));
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
        var row=S.Table("SELECT d.*,coalesce(a.name,'') party_name FROM documents d LEFT JOIN accounts a ON a.id=d.party WHERE d.id=@p0",id).Rows[0];var title=Kind((string)row["kind"])+" · "+row["number"];
        var branding=JsonSerializer.Deserialize<Dictionary<string,string>>((string)row["branding"])??new();
        string Brand(string key,string fallback="")=>branding.TryGetValue(key,out var value)&&!string.IsNullOrWhiteSpace(value)?value:fallback;
        var doc=Page(title,vm.Arabic?(string)row["company"]:Brand("company_en",(string)row["company"]));
        void Picture(string field,double width,double height){if(row[field] is byte[] bytes&&bytes.Length>0){using var stream=new MemoryStream(bytes);var bitmap=new System.Windows.Media.Imaging.BitmapImage();bitmap.BeginInit();bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();doc.Blocks.Add(new BlockUIContainer(new Image{Source=bitmap,MaxWidth=width,MaxHeight=height,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left}));}}
        Picture("logo",160,80);
        if((long)row["reversed"]==1)Paragraph(doc,T("ملغى — هذا المستند لا يمثل حركة سارية","CANCELLED — this document is no longer active"),true);
        Paragraph(doc,T("التاريخ: ","Date: ")+row["date"]);if(row["party_name"].ToString()!=""){Paragraph(doc,T("إلى: ","Bill to: ")+Brand(vm.Arabic?"party_name_ar":"party_name_en",Brand("party_name",row["party_name"].ToString()!)));var address=Brand(vm.Arabic?"party_address_ar":"party_address_en");if(address!="")Paragraph(doc,address);}if(row["due_date"].ToString()!="")Paragraph(doc,T("تاريخ الاستحقاق: ","Due date: ")+row["due_date"]);
        var lines=S.Table("SELECT * FROM invoice_lines WHERE doc=@p0 ORDER BY id",id);
        if(lines.Rows.Count>0){PrintTable(doc,new[]{T("الوصف","Description"),T("الكمية","Qty"),T("السعر JOD","Price JOD"),T("الخصم JOD","Discount JOD"),T("الصافي JOD","Net JOD")},lines.Rows.Cast<DataRow>().Select(r=>new[]{string.IsNullOrWhiteSpace(r[vm.Arabic?"description_ar":"description_en"].ToString())?(string)r["description"]:r[vm.Arabic?"description_ar":"description_en"].ToString()!,Store.D((long)r["qty"]).ToString("0.###",CultureInfo.InvariantCulture),Store.Money((long)r["price"]),Store.Money((long)r["discount"]),Store.Money((long)r["net"])}));AddTotals(doc,new((long)row["net"],(long)row["tax"],(long)row["total"]),(long)row["paid"],(string)row["vat"],(long)row["inclusive"]==1);
            var position=A.Positions(DateTime.Today).FirstOrDefault(p=>p.Invoice==id);if(position!=null){Paragraph(doc,T("المدفوع حتى اليوم: ","Paid through today: ")+Store.Money(position.Paid)+" JOD");Paragraph(doc,T("المرتجعات حتى اليوم: ","Returns through today: ")+Store.Money(position.Returned)+" JOD");Paragraph(doc,T("المتبقي حتى اليوم: ","Outstanding through today: ")+Store.Money(position.Outstanding)+" JOD",true);}
            foreach(var f in new[]{("terms",T("شروط الدفع: ","Payment terms: ")),("warranty",T("الضمان: ","Warranty: ")),("shipping",T("الشحن والتركيب: ","Shipping / installation: ")),("bank",T("بيانات الدفع: ","Payment details: "))}){var text=vm.Arabic?row[f.Item1].ToString():Brand(f.Item1+"_en",row[f.Item1].ToString()!);if(text!="")Paragraph(doc,f.Item2+text);}
        }else{Paragraph(doc,T("المبلغ: ","Amount: ")+Store.Money((long)row["total"])+" JOD",true);var entries=S.Table("SELECT a.name,e.debit,e.credit FROM entries e JOIN accounts a ON a.id=e.account WHERE doc=@p0",id);PrintTable(doc,new[]{T("الحساب","Account"),T("مدين JOD","Debit JOD"),T("دائن JOD","Credit JOD")},entries.Rows.Cast<DataRow>().Select(r=>new[]{(string)r["name"],Store.Money((long)r["debit"]),Store.Money((long)r["credit"])}));}
        if(row["note"].ToString()!="")Paragraph(doc,T("البيان: ","Description: ")+row["note"]);Paragraph(doc,T("التوقيع: __________________","Signature: __________________"));
        Picture("seal",120,70);
        if(previewDirectory!=null)RenderPrintPages(doc,previewDirectory,(vm.Arabic?"ar":"en")+"-print");
        var (w,p,_)=Dialog(title,1000,false);var viewer=new FlowDocumentScrollViewer{Document=doc,Height=500,IsToolBarVisible=false};p.Children.Add(viewer);p.Children.Add(Actions(Btn(T("طباعة / حفظ PDF","Print / save PDF"),()=>Print(doc),true),Btn(T("إغلاق","Close"),()=>w.Close())));w.ShowDialog();
    }
    static void RenderPrintPages(FlowDocument doc,string directory,string name)
    {
        var paginator=((IDocumentPaginatorSource)doc).DocumentPaginator;paginator.PageSize=new Size(794,1123);paginator.ComputePageCount();
        for(int i=0;i<paginator.PageCount;i++){var page=paginator.GetPage(i);var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(794,1123,96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()){drawing.DrawRectangle(Brushes.White,null,new Rect(0,0,794,1123));drawing.DrawRectangle(new VisualBrush(page.Visual),null,new Rect(0,0,page.Size.Width,page.Size.Height));}bitmap.Render(visual);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var file=File.Create(System.IO.Path.Combine(directory,name+"-"+(i+1)+".png"));encoder.Save(file);}
    }
    FlowDocument ReportDocument(string title,DataTable data)
    {
        var doc=Page(title,S.Setting("company"));PrintTable(doc,data.Columns.Cast<DataColumn>().Select(c=>c.ColumnName),data.Rows.Cast<DataRow>().Select(r=>r.ItemArray.Select(v=>v?.ToString()??"")));return doc;
    }
    void Print(FlowDocument doc)
    {
        var dialog=new PrintDialog();if(dialog.ShowDialog()!=true)return;doc.PageWidth=dialog.PrintableAreaWidth;doc.PageHeight=dialog.PrintableAreaHeight;dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator,"Hisab");
    }
    void Export(DataTable table)
    {
        var save=new SaveFileDialog{Filter="CSV (*.csv)|*.csv",FileName="Hisab-report-"+DateTime.Today.ToString("yyyyMMdd")+".csv"};if(save.ShowDialog(this)!=true)return;
        string Cell(string text){if(!decimal.TryParse(text,NumberStyles.Number,CultureInfo.InvariantCulture,out _)&&(text.TrimStart().StartsWith('=')||text.TrimStart().StartsWith('+')||text.TrimStart().StartsWith('-')||text.TrimStart().StartsWith('@')))text="'"+text;return "\""+text.Replace("\"","\"\"")+"\"";}
        using var writer=new StreamWriter(save.FileName,false,new UTF8Encoding(true));writer.WriteLine(string.Join(",",table.Columns.Cast<DataColumn>().Select(c=>Cell(c.ColumnName))));foreach(DataRow r in table.Rows)writer.WriteLine(string.Join(",",r.ItemArray.Select(v=>Cell(v?.ToString()??""))));vm.Status=T("تم تصدير التقرير","Report exported");
    }
}
