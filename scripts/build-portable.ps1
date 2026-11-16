<#
    build-portable.ps1 - regenerates the portable distribution in dist/ArcGisMcp-Portable:
      * ArcGisMcpServer.exe  - self-contained, single-file MCP server (bundles the .NET runtime)
      * APBridgeAddIn.esriAddinX - the ArcGIS Pro Add-In package
      * ArcGisMcp-Portable.zip - the full one-click package

    Requirements on the build machine:
      * .NET SDK (8 or newer)
      * ArcGIS Pro 3.7+ installed (needed to compile the Add-In against its assemblies)

    Usage:
      pwsh ./scripts/build-portable.ps1            # win-x64 (default)
      pwsh ./scripts/build-portable.ps1 -Runtime win-arm64
#>
param(
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot          # repository root
$dist = Join-Path $root 'dist/ArcGisMcp-Portable'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host "==> Publishing self-contained MCP server ($Runtime)..." -ForegroundColor Cyan
$server = Join-Path $root 'McpServer/ArcGisMcpServer/ArcGisMcpServer.csproj'
$stage  = Join-Path $env:TEMP ('arcgismcp_pub_' + [guid]::NewGuid().ToString('N'))
dotnet publish $server -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none -p:DebugSymbols=false -o $stage | Out-Null
Copy-Item (Join-Path $stage 'ArcGisMcpServer.exe') $dist -Force
Remove-Item $stage -Recurse -Force

Write-Host "==> Building the ArcGIS Pro Add-In..." -ForegroundColor Cyan
$addin = Join-Path $root 'AddIn/APBridgeAddIn/APBridgeAddIn.csproj'
dotnet build $addin -c Debug | Out-Null
$esri = Get-ChildItem (Join-Path $root 'AddIn/APBridgeAddIn/bin') -Recurse -Filter 'APBridgeAddIn.esriAddinX' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $esri) { throw "APBridgeAddIn.esriAddinX not found after build." }
Copy-Item $esri.FullName $dist -Force

Write-Host "==> Zipping package..." -ForegroundColor Cyan
$zip = Join-Path $root 'dist/ArcGisMcp-Portable.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $zip -CompressionLevel Optimal

Write-Host "Done." -ForegroundColor Green
Write-Host "  Package folder : $dist"
Write-Host "  Zip            : $zip"
Write-Host "These are gitignored build outputs - upload them as GitHub Release assets."
