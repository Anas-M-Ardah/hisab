using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
namespace Hisab;
public sealed partial class MainWindow
{
    string QuoteState(string state)=>state switch {"Draft"=>T("مسودة","Draft"),"Sent"=>T("مرسل","Sent"),"Accepted"=>T("مقبول","Accepted"),"Declined"=>T("مرفوض","Declined"),"Expired"=>T("منتهي الصلاحية","Expired"),"Converted"=>T("محول لفاتورة","Converted"),_=>state};
    void QuotesPage()
    {
        Heading(T("عروض الأسعار","Quotations"),T("جهّز العرض، راقب صلاحيته، ثم حوّله إلى فاتورة عند البيع.","Prepare a quote, track its validity and convert it when the customer buys."));
        var search=Input();var status=new ComboBox{ItemsSource=new[]{new Choice("All",T("كل الحالات","All statuses"))}.Concat(new[]{"Draft","Sent","Accepted","Declined","Expired","Converted"}.Select(k=>new Choice(k,QuoteState(k)))).ToList(),DisplayMemberPath="Label",SelectedValuePath="Key",SelectedIndex=0,MaxWidth=380,HorizontalAlignment=HorizontalAlignment.Left};
        body.Children.Add(SearchActions(T("بحث بالرقم أو العميل","Search number or customer"),search,Btn(T("+ عرض سعر","+ Quotation"),()=>InvoiceForm(true,quotation:true),true)));body.Children.Add(Field(T("الحالة","Status"),status));
        DataTable Rows(){var t=new DataTable();t.Columns.Add("id",typeof(long));foreach(var c in new[]{"number","customer","date","expires","state","total"})t.Columns.Add(c);foreach(var q in S.Quotes().Where(q=>(q.Number+" "+q.CustomerName+" "+q.CustomerNameEn).Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase)&&(status.SelectedValue?.ToString()=="All"||status.SelectedValue?.ToString()==q.EffectiveState)))t.Rows.Add(q.Id,q.Number,vm.Arabic?q.CustomerName:q.CustomerNameEn,q.Date.ToString("yyyy-MM-dd"),q.Expires.ToString("yyyy-MM-dd"),QuoteState(q.EffectiveState),Store.Money(AccountingService.Calculate(q.Lines,q.Vat,q.Inclusive).Total));return t;}
        var grid=Grid(Rows(),("number",T("الرقم","Number"),1),("customer",T("العميل","Customer"),2),("date",T("التاريخ","Date"),1),("expires",T("صالح حتى","Valid until"),1),("state",T("الحالة","Status"),1),("total",T("الإجمالي JOD","Total JOD"),1));foreach(int column in new[]{2,3})grid.Columns[column].MinWidth=Math.Max(135,FontSize*6.2);grid.Columns[0].MinWidth=Math.Max(130,FontSize*6);body.Children.Add(grid);
        search.TextChanged+=(s,e)=>grid.ItemsSource=Rows().DefaultView;status.SelectionChanged+=(s,e)=>grid.ItemsSource=Rows().DefaultView;
        var open=Btn(T("فتح / طباعة","Open / print"),()=>ShowQuote(Selected(grid)));var edit=Btn(T("تعديل","Edit"),()=>{var q=S.GetQuote(Selected(grid));if(q.Invoice!=null)throw new InvalidOperationException(T("تم تحويل العرض. افتح الفاتورة المرتبطة.","This quote is converted. Open its linked invoice."));InvoiceForm(true,quotation:true,quoteId:q.Id);});
        var convert=Btn(T("تحويل لفاتورة","Convert to invoice"),()=>{var q=S.GetQuote(Selected(grid));if(q.Invoice is long invoice){ShowDocument(invoice);return;}if(q.EffectiveState is "Expired" or "Declined")throw new InvalidOperationException(T("عدّل صلاحية العرض أو حالته أولًا.","Revise the quote's validity or status first."));InvoiceForm(true,quoteId:q.Id);},true);
        
        var states=new[]{("Sent","تحديد كمرسل","Mark sent"),("Accepted","تحديد كمقبول","Mark accepted"),("Declined","تحديد كمرفوض","Mark declined")}.Select(x=>Btn(T(x.Item2,x.Item3),()=>{var q=S.GetQuote(Selected(grid));A.SetQuoteState(q.Id,q.Revision,x.Item1);Navigate("quotes");})).ToArray();var more=MoreActions(states.ToList());body.Children.Add(Actions(open,edit,convert,more));BindSelection(grid,open,edit,convert,more);
        grid.MouseDoubleClick+=(s,e)=>Guard(()=>ShowQuote(Selected(grid)));
    }
    FlowDocument QuoteDocument(Quote q)
    {
        var doc=Page(T("عرض سعر — ","Quotation — ")+q.Number,vm.Arabic?q.Company:string.IsNullOrWhiteSpace(q.CompanyEn)?q.Company:q.CompanyEn);
        Paragraph(doc,T("العميل: ","Customer: ")+(vm.Arabic?q.CustomerName:q.CustomerNameEn));Paragraph(doc,T("التاريخ: ","Date: ")+q.Date.ToString("yyyy-MM-dd")+T("    صالح حتى: ","    Valid until: ")+q.Expires.ToString("yyyy-MM-dd"));Paragraph(doc,T("الحالة: ","Status: ")+QuoteState(q.EffectiveState));
        PrintTable(doc,new[]{T("الوصف","Description"),T("الكمية","Quantity"),T("سعر الوحدة","Unit price"),T("الخصم","Discount"),T("الإجمالي","Total")},q.Lines.Select(l=>new[]{l.Description,l.Qty.ToString("0.###"),Store.Money(Store.M(l.Price)),Store.Money(Store.M(l.Discount)),Store.Money(AccountingService.Calculate([l],q.Vat,q.Inclusive).Total)}));
        var total=AccountingService.Calculate(q.Lines,q.Vat,q.Inclusive);Paragraph(doc,T("الصافي: ","Net: ")+Store.Money(total.Net)+T("    الضريبة: ","    Tax: ")+Store.Money(total.Tax)+" JOD");Paragraph(doc,T("الإجمالي: ","Total: ")+Store.Money(total.Total)+" JOD",true);Paragraph(doc,T("نسبة الضريبة: ","Tax rate: ")+q.Vat+"% · "+(q.Inclusive?T("الأسعار تشمل الضريبة","Prices include tax"):T("الأسعار لا تشمل الضريبة","Prices exclude tax")));Paragraph(doc,q.Note);Paragraph(doc,vm.Arabic?q.Terms:string.IsNullOrWhiteSpace(q.TermsEn)?q.Terms:q.TermsEn);Paragraph(doc,T("هذا عرض سعر وليس فاتورة. المخزون غير محجوز.","This is a quotation, not an invoice. Stock is not reserved."));return doc;
    }
    void ShowQuote(long id)
    {
        var q=S.GetQuote(id);var doc=QuoteDocument(q);if(previewDirectory!=null)RenderPrintPages(doc,previewDirectory,(vm.Arabic?"ar":"en")+"-quote-print");
        var (w,p,_)=Dialog(T("عرض سعر","Quotation")+" · "+q.Number,1000,false);p.Children.Add(new FlowDocumentScrollViewer{Document=doc,Height=480,IsToolBarVisible=false});p.Children.Add(Actions(Btn(T("طباعة / حفظ PDF","Print / save PDF"),()=>Print(doc),true),Btn(T("إغلاق","Close"),w.Close)));w.ShowDialog();
    }
}
