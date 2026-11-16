<#
    ArcGIS Pro MCP Bridge - uninstaller
    Reverses what Install.ps1 did.
#>

$ErrorActionPreference = 'SilentlyContinue'
Write-Host "ArcGIS Pro MCP Bridge - Uninstaller" -ForegroundColor Green
Write-Host "-----------------------------------"

# 1. Remove the server folder
$installDir = 'C:\ArcGisMcp'
if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
    Write-Host "Removed $installDir"
}

# 2. Remove the Add-In (both a loose copy and the GUID folder the Esri installer creates)
$docs = [Environment]::GetFolderPath('MyDocuments')
$addinDir = Join-Path $docs 'ArcGIS\AddIns\ArcGISPro'
$addinId = '{c56ccfd4-f12a-4916-84c2-64248b3d746c}'
$removed = $false
$guidFolder = Join-Path $addinDir $addinId
if (Test-Path $guidFolder) { Remove-Item $guidFolder -Recurse -Force; $removed = $true }
$loose = Join-Path $addinDir 'APBridgeAddIn.esriAddinX'
if (Test-Path $loose) { Remove-Item $loose -Force; $removed = $true }
if ($removed) { Write-Host "Removed the ArcGIS Pro Add-In (restart ArcGIS Pro to finish)." }

# 3. Remove the arcgis entry from the Copilot app config
$cfgPath = Join-Path (Join-Path $env:USERPROFILE '.copilot') 'mcp-config.json'
if (Test-Path $cfgPath) {
    try {
        $root = Get-Content $cfgPath -Raw | ConvertFrom-Json
        if ($root.mcpServers -and ($root.mcpServers.PSObject.Properties.Name -contains 'arcgis')) {
            $root.mcpServers.PSObject.Properties.Remove('arcgis')
            ($root | ConvertTo-Json -Depth 20) | Set-Content -Path $cfgPath -Encoding UTF8
            Write-Host "Removed the 'arcgis' server from the Copilot app config."
        }
    } catch { }
}

Write-Host "`nDone. Restart the GitHub Copilot app and ArcGIS Pro." -ForegroundColor Green
