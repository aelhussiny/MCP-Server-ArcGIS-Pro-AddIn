<#
    ArcGIS Pro MCP Bridge - portable installer
    ------------------------------------------
    Installs on a machine that has ArcGIS Pro 3.7+ but may NOT have .NET installed.
    It performs three steps:
      1. Copies the self-contained MCP server (ArcGisMcpServer.exe) to C:\ArcGisMcp
      2. Installs the ArcGIS Pro Add-In (APBridgeAddIn.esriAddinX)
      3. Registers the MCP server with the GitHub Copilot app

    No .NET SDK or runtime is required: the server .exe is self-contained, and the
    Add-In runs inside ArcGIS Pro, which provides its own .NET runtime.
#>

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }

Write-Host "ArcGIS Pro MCP Bridge - Installer" -ForegroundColor Green
Write-Host "---------------------------------"

# --- 1. Copy the self-contained server to a fixed, space-free folder -----------
Write-Step "Installing the MCP server to C:\ArcGisMcp"
$installDir = 'C:\ArcGisMcp'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
$exeSource = Join-Path $here 'ArcGisMcpServer.exe'
if (-not (Test-Path $exeSource)) { throw "ArcGisMcpServer.exe not found next to this script." }
Copy-Item $exeSource $installDir -Force
$exePath = Join-Path $installDir 'ArcGisMcpServer.exe'
Write-Host "    Installed: $exePath"

# --- 2. Install the ArcGIS Pro Add-In -----------------------------------------
Write-Step "Installing the ArcGIS Pro Add-In"
$docs = [Environment]::GetFolderPath('MyDocuments')
$addinDir = Join-Path $docs 'ArcGIS\AddIns\ArcGISPro'
New-Item -ItemType Directory -Force -Path $addinDir | Out-Null
$addinSource = Join-Path $here 'APBridgeAddIn.esriAddinX'
if (-not (Test-Path $addinSource)) { throw "APBridgeAddIn.esriAddinX not found next to this script." }
# Remove any previous copy of this Add-In so updates apply cleanly
# (the Esri double-click installer extracts into a folder named after the Add-In ID).
$addinId = '{c56ccfd4-f12a-4916-84c2-64248b3d746c}'
$oldGuidFolder = Join-Path $addinDir $addinId
if (Test-Path $oldGuidFolder) { Remove-Item $oldGuidFolder -Recurse -Force }
Get-ChildItem $addinDir -Filter 'APBridgeAddIn.esriAddinX' -File -ErrorAction SilentlyContinue | Remove-Item -Force
Copy-Item $addinSource $addinDir -Force
Write-Host "    Installed into: $addinDir"
Write-Host "    (ArcGIS Pro loads it automatically the next time it starts.)"

# --- 3. Register the MCP server with the GitHub Copilot app -------------------
Write-Step "Registering the MCP server with the GitHub Copilot app"
$cfgDir = Join-Path $env:USERPROFILE '.copilot'
New-Item -ItemType Directory -Force -Path $cfgDir | Out-Null
$cfgPath = Join-Path $cfgDir 'mcp-config.json'

$root = $null
if (Test-Path $cfgPath) {
    try { $root = Get-Content $cfgPath -Raw | ConvertFrom-Json } catch { $root = $null }
}
if ($null -eq $root) { $root = [pscustomobject]@{} }
if (-not ($root.PSObject.Properties.Name -contains 'mcpServers')) {
    $root | Add-Member -NotePropertyName mcpServers -NotePropertyValue ([pscustomobject]@{}) -Force
}

$server = [pscustomobject]@{
    type    = 'local'
    command = $exePath
    tools   = @('*')
}
$root.mcpServers | Add-Member -NotePropertyName 'arcgis' -NotePropertyValue $server -Force

# Write JSON. Windows PowerShell 5.1 unwraps single-element arrays, so re-arrayify "tools".
$json = $root | ConvertTo-Json -Depth 20
$json = [System.Text.RegularExpressions.Regex]::Replace($json, '("tools"\s*:\s*)"\*"', '$1["*"]')
Set-Content -Path $cfgPath -Value $json -Encoding UTF8
Write-Host "    Updated: $cfgPath"

Write-Host "`nDone!" -ForegroundColor Green
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Restart the GitHub Copilot app (so it reads the new server)."
Write-Host "  2. Start ArcGIS Pro and open a project with a map."
Write-Host "  3. In Copilot chat (agent mode), try: 'Use the arcgis ping tool.'"
Write-Host "     Then: 'List the layers in the active map.'"
