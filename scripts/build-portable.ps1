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
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Only these files are shipped; anything else in the dist folder stays local.
$packageFiles = @(
    'ArcGisMcpServer.exe', 'APBridgeAddIn.esriAddinX',
    'Install.cmd', 'Install.ps1', 'Uninstall.cmd', 'Uninstall.ps1', 'README-SETUP.md'
)

# Fails if the build machine's repository path is embedded in a shipped binary.
function Assert-NoLocalPath([byte[]]$Bytes, [string]$Label) {
    foreach ($text in [Text.Encoding]::UTF8.GetString($Bytes), [Text.Encoding]::Unicode.GetString($Bytes)) {
        if ($text.IndexOf($root, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "$Label contains the local path '$root'."
        }
    }
}

Write-Host "==> Publishing self-contained MCP server ($Runtime)..." -ForegroundColor Cyan
$server = Join-Path $root 'McpServer/ArcGisMcpServer/ArcGisMcpServer.csproj'
$stage  = Join-Path $env:TEMP ('arcgismcp_pub_' + [guid]::NewGuid().ToString('N'))
dotnet publish $server -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none -p:DebugSymbols=false -o $stage | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)." }
Copy-Item (Join-Path $stage 'ArcGisMcpServer.exe') $dist -Force
Remove-Item $stage -Recurse -Force

Write-Host "==> Building the ArcGIS Pro Add-In (Release, no debug symbols)..." -ForegroundColor Cyan
$addinDir = Join-Path $root 'AddIn/APBridgeAddIn'
# Start clean so no stale .pdb from an earlier build ends up in the package.
foreach ($d in 'bin/Release', 'obj/Release') {
    $p = Join-Path $addinDir $d
    if (Test-Path $p) { Remove-Item $p -Recurse -Force }
}
dotnet build (Join-Path $addinDir 'APBridgeAddIn.csproj') -c Release -p:DebugType=none -p:DebugSymbols=false | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet build of the Add-In failed (exit code $LASTEXITCODE)." }
$esri = Get-ChildItem (Join-Path $addinDir 'bin/Release') -Recurse -Filter 'APBridgeAddIn.esriAddinX' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $esri) { throw "APBridgeAddIn.esriAddinX not found after build." }
Copy-Item $esri.FullName $dist -Force

Write-Host "==> Checking binaries for local paths..." -ForegroundColor Cyan
Assert-NoLocalPath ([IO.File]::ReadAllBytes((Join-Path $dist 'ArcGisMcpServer.exe'))) 'ArcGisMcpServer.exe'
$pkg = [IO.Compression.ZipFile]::OpenRead((Join-Path $dist 'APBridgeAddIn.esriAddinX'))
try {
    foreach ($entry in $pkg.Entries) {
        if ($entry.FullName -like '*.pdb') { throw "Add-In package unexpectedly contains $($entry.FullName)." }
        $ms = New-Object IO.MemoryStream
        $s = $entry.Open(); $s.CopyTo($ms); $s.Dispose()
        Assert-NoLocalPath $ms.ToArray() "APBridgeAddIn.esriAddinX/$($entry.FullName)"
    }
} finally { $pkg.Dispose() }

Write-Host "==> Zipping package..." -ForegroundColor Cyan
$zip = Join-Path $root 'dist/ArcGisMcp-Portable.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
$paths = $packageFiles | ForEach-Object {
    $f = Join-Path $dist $_
    if (-not (Test-Path $f)) { throw "Missing package file: $f" }
    $f
}
Compress-Archive -Path $paths -DestinationPath $zip -CompressionLevel Optimal

Write-Host "Done." -ForegroundColor Green
Write-Host "  Package folder : $dist"
Write-Host "  Zip            : $zip"
Write-Host "These are gitignored build outputs - upload them as GitHub Release assets."
