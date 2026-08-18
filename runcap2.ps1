param([string]$Exe)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$p = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Seconds 5
"PID: $($p.Id) HasExited: $($p.HasExited)"

# -- Capture full screen
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
$g.Dispose()
$bmp.Save('F:\1_GitHun\workspace\screen.png', [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"Screen saved"

# -- Find window & crop
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W32 {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@
$h = [W32]::FindWindow($null, 'R2 Explorer')
if ($h -eq [IntPtr]::Zero) {
    'WINDOW NOT FOUND'
} else {
    $r = New-Object W32+RECT
    [W32]::GetWindowRect($h, [ref]$r) | Out-Null
    "Window rect: $($r.L),$($r.T) - $($r.R),$($r.B)  size=$($r.R-$r.L)x$($r.B-$r.T)"
    $src = [System.Drawing.Bitmap]::FromFile('F:\1_GitHun\workspace\screen.png')
    $w = $r.R - $r.L; $hh = $r.B - $r.T
    $crop = New-Object System.Drawing.Bitmap($w, $hh)
    $gg = [System.Drawing.Graphics]::FromImage($crop)
    $gg.DrawImage($src, (New-Object System.Drawing.Rectangle(0,0,$w,$hh)), (New-Object System.Drawing.Rectangle($r.L,$r.T,$w,$hh)), [System.Drawing.GraphicsUnit]::Pixel)
    $gg.Dispose()
    $crop.Save('F:\1_GitHun\workspace\window.png', [System.Drawing.Imaging.ImageFormat]::Png)
    $src.Dispose(); $crop.Dispose()
    "Cropped window.png"
}

if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
"Done"