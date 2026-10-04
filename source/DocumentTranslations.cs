using System.Data;
using System.Windows.Controls;
namespace Hisab;
public sealed partial class MainWindow
{
    void DocumentTranslations(long id)
    {
        var (w,p,finish)=Dialog(T("وصف الفاتورة باللغتين","Bilingual invoice descriptions"),900);
        var fields=new Dictionary<long,(TextBox Ar,TextBox En)>();
        foreach(DataRow r in S.Table("SELECT * FROM invoice_lines WHERE doc=@p0",id).Rows){p.Children.Add(Text((string)r["description"],21,true));var ar=Input((string)r["description_ar"]);var en=Input((string)r["description_en"]);p.Children.Add(Field("الوصف العربي / Arabic description",ar));p.Children.Add(Field("English description / الوصف الإنجليزي",en));fields[(long)r["id"]]=(ar,en);}
        p.Children.Add(Btn(T("حفظ الأوصاف","Save descriptions"),()=>{S.Require("master");using var tx=S.BeginTransaction();foreach(var f in fields)S.Exec("UPDATE invoice_lines SET description_ar=@p0,description_en=@p1 WHERE id=@p2",f.Value.Ar.Text,f.Value.En.Text,f.Key);S.Audit("invoice-translations",id.ToString());tx.Commit();finish();},true));w.ShowDialog();
    }
}
