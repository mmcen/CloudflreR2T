$p = Start-Process -FilePath 'F:\1_GitHun\workspace\R2Explorer\bin\Release\net8.0-windows\R2Explorer.exe' -PassThru
Start-Sleep -Seconds 6
$p.Refresh()
"Main PID: $($p.Id) HasExited: $($p.HasExited)"

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
public static class W32F {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    public static List<string> Dump(uint pid) {
        var list = new List<string>();
        EnumWindows((h, l) => {
            uint wpid; GetWindowThreadProcessId(h, out wpid);
            if (wpid == pid) {
                var t = new StringBuilder(2048); GetWindowText(h, t, 2048);
                var c = new StringBuilder(256); GetClassName(h, c, 256);
                list.Add("CLASS=" + c.ToString() + " VISIBLE=" + IsWindowVisible(h) + " TEXT=" + t.ToString());
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
"@
$wins = [W32F]::Dump([uint32]$p.Id)
$wins | ForEach-Object { $_ }
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }