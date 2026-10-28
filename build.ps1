param([string]$OutputDirectory='build')
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$upstream=Join-Path $root 'vendor'
$build=[IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
New-Item -ItemType Directory -Force -Path $build | Out-Null
# Original vector-like flower mark, independent of the upstream product icon.
Add-Type -AssemblyName System.Drawing
$bitmap=New-Object Drawing.Bitmap(256,256)
$graphics=[Drawing.Graphics]::FromImage($bitmap);$graphics.SmoothingMode='AntiAlias';$graphics.Clear([Drawing.Color]::Transparent)
$graphics.ScaleTransform(2,2)
$cropPen=New-Object Drawing.Pen([Drawing.ColorTranslator]::FromHtml('#B4A0E4'),6)
$cropPen.StartCap=$cropPen.EndCap='Round'
$graphics.DrawLine($cropPen,16,38,16,16);$graphics.DrawLine($cropPen,16,16,38,16)
$graphics.DrawLine($cropPen,90,16,112,16);$graphics.DrawLine($cropPen,112,16,112,38)
$graphics.DrawLine($cropPen,112,90,112,112);$graphics.DrawLine($cropPen,112,112,90,112)
$graphics.DrawLine($cropPen,38,112,16,112);$graphics.DrawLine($cropPen,16,112,16,90)
$brush=New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml('#8B6AD8'))
for($i=0;$i -lt 7;$i++){$angle=$i*2*[math]::PI/7-[math]::PI/2;$graphics.FillEllipse($brush,[single](50+22*[math]::Cos($angle)),[single](50+22*[math]::Sin($angle)),[single]28,[single]28)}
$center=New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml('#FBF8FF'));$graphics.FillEllipse($center,53,53,22,22)
$bitmap.Save((Join-Path $build 'AgentBloom.png'),[Drawing.Imaging.ImageFormat]::Png)
# Write PNG frames directly: Icon.FromHandle(...).Save() loses this mark's color.
$iconSizes=@(16,20,24,32,48,64,128,256)
$iconFrames=New-Object 'Collections.Generic.List[byte[]]'
foreach($iconSize in $iconSizes){
 $frame=New-Object Drawing.Bitmap($iconSize,$iconSize);$frameGraphics=[Drawing.Graphics]::FromImage($frame);$frameGraphics.InterpolationMode='HighQualityBicubic';$frameGraphics.PixelOffsetMode='HighQuality';$frameGraphics.DrawImage($bitmap,0,0,$iconSize,$iconSize)
 $frameStream=New-Object IO.MemoryStream;try{$frame.Save($frameStream,[Drawing.Imaging.ImageFormat]::Png);$iconFrames.Add($frameStream.ToArray())}finally{$frameStream.Dispose();$frameGraphics.Dispose();$frame.Dispose()}
}
$stream=[IO.File]::Create((Join-Path $build 'AgentBloom.ico'));$writer=New-Object IO.BinaryWriter($stream)
try{
 $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$iconSizes.Count);$iconOffset=6+16*$iconSizes.Count
 for($iconIndex=0;$iconIndex -lt $iconSizes.Count;$iconIndex++){$dimension=[byte]$(if($iconSizes[$iconIndex] -eq 256){0}else{$iconSizes[$iconIndex]});$writer.Write($dimension);$writer.Write($dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$iconFrames[$iconIndex].Length);$writer.Write([uint32]$iconOffset);$iconOffset+=$iconFrames[$iconIndex].Length}
 foreach($iconFrame in $iconFrames){$writer.Write($iconFrame)}
}finally{$writer.Dispose();$stream.Dispose();$graphics.Dispose();$bitmap.Dispose();$brush.Dispose();$center.Dispose();$cropPen.Dispose()}
$refs=@("/r:$framework\WPF\PresentationCore.dll","/r:$framework\WPF\PresentationFramework.dll","/r:$framework\WPF\WindowsBase.dll","/r:$framework\System.Xaml.dll","/r:$framework\System.Web.Extensions.dll","/r:$framework\Microsoft.VisualBasic.dll","/r:$upstream\WebView2\Microsoft.Web.WebView2.Core.dll","/r:$upstream\WebView2\Microsoft.Web.WebView2.Wpf.dll",'/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Net.Http.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
$resources=@('/resource:'+(Join-Path $build 'AgentBloom.ico')+',PicRope.Icon')
$resources+=@(Get-ChildItem "$upstream\assets\basket-*.png" | ForEach-Object {'/resource:'+$_.FullName+',PicRope.Basket.'+$_.BaseName.Substring(7)})
$resources+=@(Get-ChildItem "$upstream\assets\envelope-*.png" | ForEach-Object {'/resource:'+$_.FullName+',PicRope.Envelope.'+$_.BaseName.Substring(9)})
$sources=@(Get-ChildItem "$root\src\Native\*.cs" | ForEach-Object {$_.FullName})
& "$framework\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/out:$build\AgentBloom.exe" "/win32manifest:$root\src\Native\app.manifest" "/win32icon:$build\AgentBloom.ico" $resources $refs $sources
if($LASTEXITCODE -ne 0){throw 'AgentBloom 构建失败'}
Copy-Item "$root\src\Native\App.config" "$build\AgentBloom.exe.config"
foreach($dir in @('ui','licenses')){Copy-Item -LiteralPath (Join-Path $root $dir) -Destination $build -Recurse -Force}
Copy-Item "$upstream\WebView2\*.dll" $build
New-Item -ItemType Directory -Force -Path "$build\editor","$build\third-party-notices\Filerobot","$build\third-party-notices\WebView2" | Out-Null
Copy-Item "$root\src\editor\*" "$build\editor"
Copy-Item "$upstream\Filerobot\filerobot-image-editor.min.js" "$build\editor"
# Guard the shared options toolbar against a selection whose annotation was undone.
# Keep the upstream download unchanged; fail loudly if a different vendor version arrives.
$vendorPath=Join-Path $build 'editor\filerobot-image-editor.min.js'
$vendorText=[IO.File]::ReadAllText($vendorPath)
$vendorSelection='return e=i[s[0]],null==(e=lR[e.name])'
if($vendorText.IndexOf($vendorSelection) -lt 0 -or $vendorText.IndexOf($vendorSelection) -ne $vendorText.LastIndexOf($vendorSelection)){throw 'Filerobot 版本与已验证的选择器补丁不匹配'}
[IO.File]::WriteAllText($vendorPath,$vendorText.Replace($vendorSelection,'return e=i[s[0]],null==(e=e&&lR[e.name])'),(New-Object Text.UTF8Encoding($false)))
[IO.File]::WriteAllText((Join-Path $build 'third-party-notices\Filerobot\AGENTBLOOM-CHANGES.txt'),'AgentBloom modifies the distributed Filerobot bundle with a null guard in its shared annotation options toolbar. This prevents stale selection IDs after undo from crashing the editor. The upstream reference and original license/notices are unchanged.',(New-Object Text.UTF8Encoding($false)))
Copy-Item "$upstream\Filerobot\LICENSE","$upstream\Filerobot\THIRD-PARTY-NOTICES.txt" "$build\third-party-notices\Filerobot"
Copy-Item "$upstream\WebView2\LICENSE.txt","$upstream\WebView2\NOTICE.txt" "$build\third-party-notices\WebView2"
Write-Output "BUILD OK: $build\AgentBloom.exe"
Copy-Item "$root\packaging\update.ps1" "$build\update.ps1"
Copy-Item "$root\LICENSE","$root\NOTICE.md" "$build"
