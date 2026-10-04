using System.IO;
using System.Printing;
using System.Runtime.InteropServices;
using System.Windows.Controls;
namespace Hisab;
public static class Diagnostics
{
    public static string Report()
    {
        var lines=new List<string>{"Hisab 0.2 deployment and printer check",DateTime.Now.ToString("O"),"OS: "+RuntimeInformation.OSDescription,"OS architecture: "+RuntimeInformation.OSArchitecture,"Process architecture: "+RuntimeInformation.ProcessArchitecture,"Runtime: "+RuntimeInformation.FrameworkDescription,"Executable: "+Environment.ProcessPath};
        try{using var server=new LocalPrintServer();using var queues=server.GetPrintQueues();foreach(var printer in queues)using(printer){lines.Add("Printer: "+printer.FullName+" | Driver: "+printer.QueueDriver.Name+" | Status: "+printer.QueueStatus);}}catch(Exception ex){lines.Add("Printer discovery unavailable: "+ex.Message);}
        lines.Add("Physical output requires a test page and visual inspection. This check does not send a print job.");return string.Join(Environment.NewLine,lines);
    }
}
public sealed partial class MainWindow
{
    void MachineCheck()
    {
        var (w,p,_)=Dialog(T("فحص الجهاز والطباعة","Machine & printer check"),950,false);var report=Diagnostics.Report();p.Children.Add(new TextBox{Text=report,IsReadOnly=true,TextWrapping=System.Windows.TextWrapping.Wrap,Height=350,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FlowDirection=System.Windows.FlowDirection.LeftToRight,FontSize=16});
        p.Children.Add(Actions(Btn(T("طباعة صفحة اختبار…","Print test page…"),()=>{var doc=Page("Hisab · حساب",S.Setting("company"));Paragraph(doc,"اختبار الطباعة العربية: فاتورة رقم ١٢٣ — ١٬٢٥٠٫٥٠٠ دينار.");Paragraph(doc,"English print test: Invoice 123 — 1,250.500 JOD.");PrintTable(doc,new[]{"الوصف / Description","Qty","JOD"},new[]{new[]{"صنف تجريبي / Test item","2","125.000"}});Paragraph(doc,"تحقق من وضوح الخط، الهوامش، اتجاه العربية وعدم اقتطاع الجدول. / Check text, margins, Arabic direction and table clipping.");Print(doc);}),Btn(T("حفظ تقرير الفحص…","Save check report…"),()=>{var save=new Microsoft.Win32.SaveFileDialog{Filter="Text (*.txt)|*.txt",FileName="Hisab-machine-check.txt"};if(save.ShowDialog(this)==true)File.WriteAllText(save.FileName,report);}),Btn(T("إغلاق","Close"),()=>w.Close())));w.ShowDialog();
    }
}
