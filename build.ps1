# Build NaiWa with the C# compiler that ships with Windows (.NET Framework 4, csc.exe). No SDK or Visual Studio needed.
#   .\build.ps1               -> dist\NaiWa.exe
#   .\build.ps1 -Screenshots  -> also re-render the README images into docs\images (offscreen, no windows appear)
param([switch]$Screenshots)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root 'src'
$obj  = Join-Path $root 'obj'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $obj, $dist | Out-Null

$fw  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$wpf = "$fw\WPF"
if (-not (Test-Path "$fw\csc.exe")) { throw "Can't find $fw\csc.exe (needs the .NET Framework 4.x that ships with Windows)" }
Add-Type -AssemblyName System.Drawing

# 1. App icon from the pixel art (PNG inside an ICO)
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

# 2. Compile
$out = Join-Path $dist 'NaiWa.exe'
$refs = @("$wpf\PresentationFramework.dll", "$wpf\PresentationCore.dll", "$wpf\WindowsBase.dll", "$fw\System.Xaml.dll", "$fw\System.Windows.Forms.dll", "$fw\System.Drawing.dll", "$fw\System.Web.Extensions.dll", "$fw\System.Security.dll", "$fw\Microsoft.CSharp.dll", "$fw\System.Core.dll", "$fw\System.dll") | ForEach-Object { "/r:$_" }
& "$fw\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 "/win32icon:$obj\naiwa.ico" "/out:$out" $refs "$src\NaiWa.cs"
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
"OK -> $out"

# 3. Optional: render the README images offscreen
if ($Screenshots) {
    $img = Join-Path $root 'docs\images'
    New-Item -ItemType Directory -Force $img | Out-Null
    $p = Start-Process $out -ArgumentList '--snapshot', "`"$img`"" -PassThru
    if (-not $p.WaitForExit(30000)) { $p.Kill(); throw "Screenshot rendering timed out" }
    Get-ChildItem $img -Filter *.png | ForEach-Object { "  $($_.Name)" }
}
