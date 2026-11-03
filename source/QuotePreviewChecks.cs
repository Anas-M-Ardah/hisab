using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace Hisab;
public sealed partial class MainWindow
{
    public void RenderQuotePreviews(string path)
    {
        Directory.CreateDirectory(path);previewDirectory=path;
        var customer=S.Accounts().First(x=>x.Kind=="Customer");var item=S.Items().First();
        long id=A.SaveQuote(null,0,customer.Id,DateTime.Today,DateTime.Today.AddDays(14),16,false,"Delivery within 7 days / التسليم خلال ٧ أيام",[new(item.Id,item.Name,2,100,10)]);
        foreach(var language in new[]{"ar","en"})foreach(int font in new[]{14,24})
        {
            S.Set("language",language);S.Set("font",font.ToString());vm.Route="quotes";BuildShell();UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
            Capture(this,Path.Combine(path,language+"-quotes-"+font+".png"));
            PreviewDialog("quote-edit-"+font,()=>InvoiceForm(true,quotation:true,quoteId:id));
            PreviewDialog("quote-convert-"+font,()=>InvoiceForm(true,quoteId:id));
            PreviewDialog("quote-print-"+font,()=>ShowQuote(id));
            EmptyDropdownPreviewChecks(path,language);
            PreviewDialog("inline-error-"+font,()=>{
                var (w,p,finish)=Dialog(T("التحقق من البيانات","Form validation"),700);p.Children.Add(Field(T("الكمية","Quantity"),Input("0",true)));p.Children.Add(Btn(T("حفظ","Save"),()=>{}));
                w.Loaded+=(_,_)=>w.Dispatcher.BeginInvoke(()=>{Guard(()=>throw new InvalidOperationException(T("أدخل كمية أكبر من صفر.","Enter a quantity greater than zero.")));if(!formErrors.TryGetValue(w,out var message)||message.Visibility!=Visibility.Visible)throw new InvalidOperationException("Inline form feedback missing");},System.Windows.Threading.DispatcherPriority.ContextIdle);
                w.ShowDialog();
            });
            File.AppendAllText(Path.Combine(path,"ui-checks.txt"),"PASS "+language+"/"+font+" quotations, editing, conversion, printing and dropdown search"+Environment.NewLine);
        }
    }
}
