using System.IO;
using System.Runtime.InteropServices;
namespace Hisab;

public static class DesktopIntegration
{
    public const string ShortcutName="Hisab.lnk";
    public static bool CreateShortcut(string executable,string? desktopDirectory=null)
    {
        executable=Path.GetFullPath(executable);
        if(!File.Exists(executable)||!Path.GetFileName(executable).Equals("Hisab.exe",StringComparison.OrdinalIgnoreCase))return false;
        string desktop=desktopDirectory??Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if(string.IsNullOrWhiteSpace(desktop))return false;
        desktop=Path.GetFullPath(desktop);
        Directory.CreateDirectory(desktop);
        string path=Path.Combine(desktop,ShortcutName);
        object? shell=null,shortcut=null;
        try
        {
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")??throw new InvalidOperationException("Windows shortcut service unavailable"));
            dynamic automation=shell!;shortcut=automation.CreateShortcut(path);dynamic link=shortcut;
            // Refresh a shortcut from an earlier portable version, but preserve unrelated user shortcuts.
            if(File.Exists(path)&&!Path.GetFileName((string)link.TargetPath).Equals("Hisab.exe",StringComparison.OrdinalIgnoreCase))return false;
            link.TargetPath=executable;link.WorkingDirectory=Path.GetDirectoryName(executable)!;
            link.IconLocation=executable+",0";link.Description="حساب — Hisab accounting";link.WindowStyle=1;link.Save();return true;
        }
        finally
        {
            if(shortcut!=null&&Marshal.IsComObject(shortcut))Marshal.FinalReleaseComObject(shortcut);
            if(shell!=null&&Marshal.IsComObject(shell))Marshal.FinalReleaseComObject(shell);
        }
    }
}
