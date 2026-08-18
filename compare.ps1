param()

Add-Type -AssemblyName System.Drawing

function Render-Ascii($path, $x0, $y0, $w, $h, $label) {
    $src = [System.Drawing.Bitmap]::FromFile($path)
    Write-Output "=== $label : ($x0,$y0) ${w}x${h} ==="
    $yy = 0
    while ($yy -lt $h) {
        $line = ''
        $xx = 0
        while ($xx -lt $w) {
            $c = $src.GetPixel($x0 + $xx, $y0 + $yy)
            $dr = [math]::Abs($c.R - 21); $dg = [math]::Abs($c.G - 24); $db = [math]::Abs($c.B - 29)
            $diff = $dr + $dg + $db
            if ($diff -gt 120) { $line += '#' }
            elseif ($diff -gt 60) { $line += '+' }
            elseif ($diff -gt 30) { $line += '.' }
            else { $line += ' ' }
            $xx++
        }
        Write-Output $line
        $yy += 1
    }
    $src.Dispose()
}

# 顶部工具栏按钮区：下载按钮（x=470..520, y=88..112）——OCR 显示 [470,97] "下" 是文字，图标在其左侧 x=449-459
# 左侧栏三个按钮 y=69
Render-Ascii 'F:\1_GitHun\workspace\R2Explorer_XVCbSJgtxG.png' 440 88 60 28 'USER screenshot: toolbar icon area'
Render-Ascii 'F:\1_GitHun\workspace\window.png'               440 88 60 28 'MY screenshot:   toolbar icon area'