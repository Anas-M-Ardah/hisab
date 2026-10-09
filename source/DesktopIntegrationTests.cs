using System.IO;
using System.Runtime.InteropServices;
namespace Hisab;
public static class DesktopIntegrationTests
{
    public static void Run(string directory,List<string> results)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
        string root=Path.GetFullPath(Path.Combine(directory,"shortcut-test")),desktop=Path.Combine(root,"desktop"),old=Path.Combine(root,"old"),updated=Path.Combine(root,"new");Directory.CreateDirectory(old);Directory.CreateDirectory(updated);
        string first=Path.Combine(old,"Hisab.exe"),second=Path.Combine(updated,"Hisab.exe");File.WriteAllText(first,"");File.WriteAllText(second,"");
        string linkPath=Path.Combine(desktop,DesktopIntegration.ShortcutName);
        Check(DesktopIntegration.CreateShortcut(first,desktop)&&File.Exists(linkPath),"Desktop shortcut created in isolated test directory");
        object shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        try
        {
            (string Target,string Directory,string Icon) Read(){dynamic automation=shell;object obj=automation.CreateShortcut(linkPath);try{dynamic link=obj;return((string)link.TargetPath,(string)link.WorkingDirectory,(string)link.IconLocation);}finally{Marshal.FinalReleaseComObject(obj);}}
            var saved=Read();Check(saved.Target==first&&saved.Directory==old&&saved.Icon==first+",0","Shortcut targets app and embedded icon with correct working directory");
            Check(DesktopIntegration.CreateShortcut(second,desktop)&&Read().Target==second,"Desktop shortcut refreshes to new portable version");
            string unrelated=Path.Combine(root,"Other.exe");File.WriteAllText(unrelated,"");dynamic automation=shell;object obj=automation.CreateShortcut(linkPath);try{dynamic link=obj;link.TargetPath=unrelated;link.Save();}finally{Marshal.FinalReleaseComObject(obj);}
            Check(!DesktopIntegration.CreateShortcut(second,desktop)&&Read().Target==unrelated,"Unrelated existing desktop shortcut preserved");
            Check(!DesktopIntegration.CreateShortcut(unrelated,desktop),"Unsupported process cannot create Hisab shortcut");
        }
        finally{Marshal.FinalReleaseComObject(shell);}
    }
}
