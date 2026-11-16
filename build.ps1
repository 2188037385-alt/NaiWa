# 编译奶蛙：只用 Windows 自带的 .NET Framework 4 编译器（csc.exe），不需要安装任何 SDK
#   .\build.ps1               → dist\NaiWa.exe
#   .\build.ps1 -Screenshots  → 编译后离屏渲染界面截图到 docs\images（更新 README 配图用）
param([switch]$Screenshots)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root 'src'
$obj  = Join-Path $root 'obj'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $obj, $dist | Out-Null

$fw  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$wpf = "$fw\WPF"
if (-not (Test-Path "$fw\csc.exe")) { throw "找不到 $fw\csc.exe（需要 Windows 自带的 .NET Framework 4.x）" }
Add-Type -AssemblyName System.Drawing

# 1. 由像素图生成程序图标（PNG 内嵌 ICO）
$frames = Get-Content "$src\frames.txt" -Encoding UTF8
$rows = @(); $on = $false
foreach ($l in $frames) { if ($l -like '== *') { if ($rows.Count) { break }; $on = $true; continue }; if ($on) { $rows += $l } }
$pal = @{ 'K'='#343C34'; 'Y'='#F7EEC6'; 'L'='#FFFDF5'; 'G'='#D8E6C5'; 'B'='#1C221C'; 'H'='#8E9471' }
$s = 8; $bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
for ($y = 0; $y -lt $rows.Count; $y++) { for ($x = 0; $x -lt 32; $x++) { $ch = [string]$rows[$y][$x]; if ($pal.ContainsKey($ch)) { $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($pal[$ch]))), $x * $s, ($y + 1) * $s, $s, $s) } } }
$ms = New-Object IO.MemoryStream; $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $png = $ms.ToArray()
$ico = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter $ico
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]1)
$w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$png.Length); $w.Write([uint32]22); $w.Write($png); $w.Flush()
[IO.File]::WriteAllBytes("$obj\naiwa.ico", $ico.ToArray())

# 2. 编译
$out = Join-Path $dist 'NaiWa.exe'
$refs = @("$wpf\PresentationFramework.dll", "$wpf\PresentationCore.dll", "$wpf\WindowsBase.dll", "$fw\System.Xaml.dll", "$fw\System.Windows.Forms.dll", "$fw\System.Drawing.dll", "$fw\System.Web.Extensions.dll", "$fw\System.Security.dll", "$fw\Microsoft.CSharp.dll", "$fw\System.Core.dll", "$fw\System.dll") | ForEach-Object { "/r:$_" }
& "$fw\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 "/win32icon:$obj\naiwa.ico" "/out:$out" $refs "$src\NaiWa.cs"
if ($LASTEXITCODE -ne 0) { throw "编译失败" }
"OK -> $out"

# 3. 可选：离屏渲染 README 配图（不会弹出窗口、不抢焦点）
if ($Screenshots) {
    $img = Join-Path $root 'docs\images'
    New-Item -ItemType Directory -Force $img | Out-Null
    $p = Start-Process $out -ArgumentList '--snapshot', "`"$img`"" -PassThru
    if (-not $p.WaitForExit(30000)) { $p.Kill(); throw "截图渲染超时" }
    Get-ChildItem $img -Filter *.png | ForEach-Object { "  $($_.Name)" }
}
