param([string]$Version='0.1.0')
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid version'}
$projectRoot=Split-Path $PSScriptRoot -Parent
$build=Join-Path $projectRoot 'build'
if(!(Test-Path -LiteralPath (Join-Path $build 'AgentBloom.exe'))){throw 'Run build.ps1 first'}
$dist=Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$stage=Join-Path $dist ('package-'+[Guid]::NewGuid().ToString('N'))
$app=Join-Path $stage 'app'
New-Item -ItemType Directory -Path $app -Force | Out-Null
# Explicit runtime allow-list: never package data, QA, logs or development executables.
foreach($file in @('AgentBloom.exe','AgentBloom.exe.config','AgentBloom.ico','AgentBloom.png','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll','WebView2Loader.dll','update.ps1')){
 Copy-Item -LiteralPath (Join-Path $build $file) -Destination $app
}
foreach($dir in @('ui','editor','licenses','third-party-notices')){
 Copy-Item -LiteralPath (Join-Path $build $dir) -Destination $app -Recurse
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-AgentBloom.vbs') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'),(Join-Path $projectRoot 'LICENSE'),(Join-Path $projectRoot 'NOTICE.md') -Destination $stage
New-Item -ItemType Directory -Path (Join-Path $stage 'docs') | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\icon.png') -Destination (Join-Path $stage 'docs')
$zip=Join-Path $dist ('AgentBloom-'+$Version+'-windows-x64.zip')
if(Test-Path -LiteralPath $zip){throw 'Archive already exists; choose an unused output before packaging'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
($hash+'  '+[IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII
Write-Output ('Release archive: '+$zip)
Write-Output ('SHA-256: '+$hash)
