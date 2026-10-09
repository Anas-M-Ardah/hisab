using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Hisab;
public static class AppEntry
{
    [STAThread]
    public static int Main(string[] args)
    {
        SQLitePCL.Batteries_V2.Init();
        if(args.Contains("--diagnostics")){File.WriteAllText(args.Last(),Diagnostics.Report());return 0;}
        if(args.Contains("--self-test")) return SelfTest.Run(args.Last());
        var preview=args.Contains("--preview");
        string root=preview?System.IO.Path.Combine(args.Last(),"preview-data"):System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Hisab");
        Directory.CreateDirectory(root);
        using var single=new Mutex(true,preview?"HisabPreview":"HisabDesktop-"+Environment.UserName,out bool owns);
        if(!owns) { MessageBox.Show("البرنامج مفتوح بالفعل.\nHisab is already open.","Hisab"); return 0; }
        try {
            if(!preview&&Environment.ProcessPath is string executable){try{DesktopIntegration.CreateShortcut(executable);}catch(Exception ex){File.AppendAllText(System.IO.Path.Combine(root,"errors.log"),"Desktop shortcut: "+ex+Environment.NewLine);}}
            using var store=new Store(System.IO.Path.Combine(root,preview?"accounting.db":"accounting.hdb"),encrypted:!preview);
            if(preview&&args.Contains("--preview-demo"))PreviewData.Seed(store);
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            if(!preview&&!SessionGate.Enter(store))return 0;
            app.ShutdownMode=ShutdownMode.OnMainWindowClose;
            app.DispatcherUnhandledException+=(s,e)=>{ File.AppendAllText(System.IO.Path.Combine(root,"errors.log"),DateTime.Now+" "+e.Exception+Environment.NewLine); MessageBox.Show("تعذر إكمال العملية. تم تسجيل التفاصيل.\nThe operation could not finish. Details were logged.","Hisab"); e.Handled=true; };
            var window=new MainWindow(new MainViewModel(store));
            app.MainWindow=window;
            if(preview) window.Loaded+=(s,e)=>window.Dispatcher.BeginInvoke(()=>{ try {window.RenderPreviews(args.Last());} finally {window.Close();} },DispatcherPriority.ApplicationIdle);
            app.Run(window);
            return 0;
        } catch(Exception ex) { File.WriteAllText(System.IO.Path.Combine(root,"startup-error.txt"),ex.ToString()); MessageBox.Show("تعذر فتح البرنامج. راجع ملف startup-error.txt في مجلد البيانات.\nUnable to open Hisab. See startup-error.txt in the data folder.","Hisab"); return 1; }
    }
}
