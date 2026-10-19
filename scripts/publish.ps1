# Membuat paket rilis Celah: satu Celah.exe mandiri (tidak perlu memasang .NET di PC tujuan),
# lalu dibungkus zip di folder publish\.
#
#   powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
param(
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\Celah.App\Celah.App.csproj'
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
$outDir = Join-Path $root "publish\Celah-$version-$Runtime"
$zip = Join-Path $root "publish\Celah-$version-$Runtime.zip"

Write-Host "Celah $version ($Runtime) -> $outDir"
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

dotnet test (Join-Path $root 'tests\Celah.Core.Tests') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Test gagal; rilis dibatalkan.' }

# Tanpa EnableCompressionInSingleFile: exe lebih besar (±180 MB, zip tetap ±70 MB), tetapi memori
# turun dari ±130 MB ke ±55 MB karena library tidak perlu didekompresi ke RAM saat mulai.
dotnet publish $project -c Release -r $Runtime --nologo -o $outDir `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw 'Publish gagal.' }

Copy-Item (Join-Path $root 'README.md') $outDir
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zip

$size = (Get-Item $zip).Length / 1MB
Write-Host ("Selesai: {0} ({1:N1} MB)" -f $zip, $size)
