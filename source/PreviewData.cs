using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Hisab;
public static class PreviewData
{
    public static void Seed(Store s)
    {
        if(Convert.ToInt64(s.Scalar("SELECT count(*) FROM documents"))>0)return;
        s.SaveAccount(null,"EXP","المصروفات","Expense");long expenses=s.AccountId("EXP");s.SaveTranslations("accounts",expenses,"المصروفات","Expenses");
        s.SaveAccount(null,"EXP-W","المياه","Expense",expenses);s.SaveTranslations("accounts",s.AccountId("EXP-W"),"المياه","Water");s.SaveAccount(null,"EXP-T","المواصلات","Expense",expenses);s.SaveTranslations("accounts",s.AccountId("EXP-T"),"المواصلات","Transportation");
        new AccountingService(s).Voucher(false,DateTime.Today,s.AccountId("EXP-W"),s.AccountId("1101"),10,"Water");new AccountingService(s).Voucher(false,DateTime.Today,s.AccountId("EXP-T"),s.AccountId("1101"),5,"Transportation");
        var a=new AccountingService(s);var c=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(c.Id,c.Code,c.Name,"Customer");s.SaveTranslations("accounts",c.Id,"عميل توضيحي","Example customer","عمّان، الأردن — شارع الجامعة","University Street, Amman, Jordan");var p=s.Accounts().First(x=>x.Kind=="Unclassified");s.SaveAccount(p.Id,p.Code,p.Name,"Supplier");s.Set("company","شركة توضيحية — بيانات للمعاينة فقط");s.Set("company_en","Example Company — PREVIEW DATA");s.Set("terms","الدفع خلال ٣٠ يومًا");s.Set("terms_en","Payment within 30 days");
        byte[] Mark(string label){var visual=new DrawingVisual();using(var d=visual.RenderOpen()){d.DrawRoundedRectangle(Brushes.Teal,null,new Rect(0,0,160,54),8,8);d.DrawText(new FormattedText(label,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),22,Brushes.White,1),new Point(14,12));}var image=new RenderTargetBitmap(160,54,96,96,PixelFormats.Pbgra32);image.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=new MemoryStream();encoder.Save(stream);return stream.ToArray();}
        s.Set("logo",Convert.ToBase64String(Mark("DEMO LOGO")));s.Set("seal",Convert.ToBase64String(Mark("DEMO SEAL")));var item=s.Items()[0];var date=DateTime.Today.AddDays(-40);a.Invoice(false,date,p.Id,null,0,16,false,"Preview purchase",[new(item.Id,"صنف توضيحي",10,100,0)]);long invoice=a.Invoice(true,date.AddDays(1),c.Id,s.AccountId("1101"),80,16,false,"",[new(item.Id,"صنف توضيحي",3,150,0)]);s.Exec("UPDATE invoice_lines SET description_ar='صنف توضيحي',description_en='Example item' WHERE doc=@p0",invoice);a.SetDueDate(invoice,date.AddDays(31));long receipt=a.Voucher(true,date.AddDays(5),c.Id,s.AccountId("1101"),100,"Preview collection");a.Allocate(receipt,invoice,100,date.AddDays(5));a.RegisterCheque(true,"DEMO-001","Example bank",c.Id,50,date.AddDays(6),DateTime.Today.AddDays(5));s.SaveUser("preview-admin","PreviewPassword123","Admin");
    }
}
