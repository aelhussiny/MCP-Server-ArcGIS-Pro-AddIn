# MCP Server with ArcGIS Pro Add-In (C# .NET)

> **This is an enhanced fork** of [nicogis/MCP-Server-ArcGIS-Pro-AddIn](https://github.com/nicogis/MCP-Server-ArcGIS-Pro-AddIn).
> It adds seven new MCP tools, ArcGIS Pro 3.7 / .NET 10 support, a stdio-logging fix, auto-start,
> and a dependency-free portable installer. See [CHANGELOG.md](CHANGELOG.md) and [NOTICE](NOTICE).

This repository demonstrates how to integrate a **Model Context Protocol (MCP) server** with an **ArcGIS Pro Add-In**. The goal is to expose ArcGIS Pro functionality as MCP tools so that GitHub Copilot (in Agent mode) or any MCP client can interact with your GIS environment.

---

## Overview
- **ArcGIS Pro Add-In** (C# with ArcGIS Pro SDK): runs *in-process* with ArcGIS Pro and exposes GIS operations through a local IPC channel (Named Pipes). Targets `net10.0-windows` for ArcGIS Pro 3.7+.
- **MCP Server** (.NET 8 console app): defines MCP tools, communicates with the Add-In via Named Pipes, and is configured as an MCP server through `.mcp.json`.

This can allow Copilot (Agent Mode) to query maps, list layers, count features, zoom to layers, and more — directly in ArcGIS Pro.

---

## Prerequisites
- Visual Studio 2022 **17.14 or later** (for MCP Agent Mode support)
- ArcGIS Pro SDK for .NET
- ArcGIS Pro installed (same machine)
- A .NET SDK whose version **matches the installed ArcGIS Pro runtime**:
  - ArcGIS Pro 3.2–3.6 → **.NET 8 SDK** (Add-In targets `net8.0-windows`)
  - ArcGIS Pro 3.7+ → **.NET 10 SDK** (Add-In targets `net10.0-windows`)

> **Why this matters:** the Add-In references the `ArcGIS.Desktop.*` assemblies from your local Pro install. Those assemblies are compiled against a specific .NET version, so the Add-In's `TargetFramework` must match it or the build fails with `CS1705` (`System.Runtime` version mismatch). This repo currently targets `net10.0-windows` for **ArcGIS Pro 3.7**. If you run an older Pro, change `AddIn/APBridgeAddIn/APBridgeAddIn.csproj` back to `net8.0-windows`. The MCP server itself targets `net8.0` and is independent of the Pro version.

---

## Solution Structure
```
ArcGisProMcpSample/
+- ArcGisProBridgeAddIn/           # ArcGIS Pro Add-In project (in-process)
¦  +- Config.daml
¦  +- Module.cs
¦  +- ProBridgeService.cs          # Named Pipe server + command handler
¦  +- IpcModels.cs                 # IPC request/response DTOs
+- ArcGisMcpServer/                # MCP server project (.NET 8)
¦  +- Program.cs
¦  +- Tools/ProTools.cs            # MCP tool definitions (bridge client)
¦  +- Ipc/BridgeClient.cs          # Named Pipe client
¦  +- Ipc/IpcModels.cs             # Shared IPC DTOs
+- .mcp.json                       # MCP server manifest for VS Copilot
```

---

## ArcGIS Pro Add-In
The Add-In starts a **Named Pipe server** on ArcGIS Pro launch. It handles operations like:
- `pro.getActiveMapName`
- `pro.listLayers`
- `pro.countFeatures`
- `pro.zoomToLayer`

### Example: `Module.cs` (in sample is in a button)
```csharp
protected override bool Initialize()
{
    _service = new ProBridgeService("ArcGisProBridgePipe");
    _service.Start();
    return true; // initialization successful
}

protected override bool CanUnload()
{
    _service?.Dispose();
    return true;
}
```

### Example: `ProBridgeService` handler
```csharp
case "pro.countFeatures":
{
    if (req.Args == null ||
        !req.Args.TryGetValue("layer", out string? layerName) ||
        string.IsNullOrWhiteSpace(layerName))
        return new(false, "arg 'layer' required", null);

    int count = await QueuedTask.Run(() =>
    {
        var fl = MapView.Active?.Map?.Layers
            .OfType<FeatureLayer>()
            .FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
        if (fl == null) return 0;
        using var fc = fl.GetFeatureClass();
        return (int)fc.GetCount();
    });

    return new(true, null, new { count });
}
```

---

## MCP Server (.NET 8)
The MCP server uses the official `ModelContextProtocol` NuGet package.

### `Program.cs`
```csharp
await Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(new BridgeClient("ArcGisProBridgePipe"));
        services.AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(ProTools).Assembly);
    })
    .RunConsoleAsync();
```

### Example tool
```csharp
[McpServerToolType]
public static class ProTools
{
    private static BridgeClient _client;
    public static void Configure(BridgeClient client) => _client = client;

    [McpServerTool(Title = "Count features in a layer", Name = "pro.countFeatures")]
    public static async Task<object> CountFeatures(string layer)
    {
        var r = await _client.OpAsync("pro.countFeatures", new() { ["layer"] = layer });
        if (!r.Ok) throw new Exception(r.Error);
        var count = ((System.Text.Json.JsonElement)r.Data).GetProperty("count").GetInt32();
        return new { layer, count };
    }
}
```
---

---

## Available MCP Tools
Tool names exposed to the MCP client are the **snake_case** form of the C# method names in `Tools/ProTools.cs` (the `ModelContextProtocol` SDK converts them automatically). The `pro.*` strings elsewhere in this repo are the **internal IPC op codes** sent to the Add-In over the named pipe — they are not the MCP tool names.

| MCP tool | Parameters | IPC op | Description |
|----------|------------|--------|-------------|
| `get_active_map_name` | – | `pro.getActiveMapName` | Name of the active map |
| `list_layers` | – | `pro.listLayers` | Layer names in the active map |
| `count_features` | `layer` | `pro.countFeatures` | Feature count of a layer |
| `zoom_to_layer` | `layer` | `pro.zoomToLayer` | Zoom the active view to a layer |
| `zoom_to_features` | `layer`, `where` | `pro.zoomToFeatures` | Select features with a SQL `where` clause and zoom to their combined extent; returns the count |
| `zoom_to_selection` | `layer` *(optional)* | `pro.zoomToSelection` | Zoom to the current selection of a layer, or the whole map's selection when omitted |
| `select_by_attribute` | `layer`, `where` | `pro.selectByAttribute` | Select features with a SQL `where` clause; returns the selected count |
| `clear_selection` | `layer` *(optional)* | `pro.clearSelection` | Clear selection on a layer, or the whole map when omitted |
| `get_selection_count` | `layer` | `pro.getSelectionCount` | Number of currently selected features in a layer |
| `get_current_extent` | – | `pro.getCurrentExtent` | Active map view extent (`xmin/ymin/xmax/ymax` + spatial reference) |
| `list_fields` | `layer` | `pro.listFields` | Field names and types of a feature layer |
| `ping` | – | *(none)* | Liveness check; does **not** require ArcGIS Pro |
| `echo` | `text` | *(none)* | Echoes input back; useful for client debugging |

> **Adding a tool** is a two-step change: (1) add a `case` in `AddIn/APBridgeAddIn/ProBridgeService.cs` `HandleAsync(...)` that performs the GIS work (wrap Pro API calls in `QueuedTask.Run`), and (2) add a matching `[McpServerTool]` method in `McpServer/ArcGisMcpServer/Tools/ProTools.cs` that calls `_client.OpAsync("pro.yourOp", args)`. Keep the IPC op code identical on both sides.

> **Logging note:** an stdio MCP server must keep `stdout` reserved for JSON-RPC. `Program.cs` routes all console logs to `stderr` (`LogToStandardErrorThreshold = Trace`) so the protocol stream stays clean.

---


## `.mcp.json` Manifest
Place in solution root (`.mcp.json`):
```json
{
  "servers": {
    "arcgis": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "McpServer/ArcGisMcpServer/ArcGisMcpServer.csproj"
      ]
    }
  }
}
```
---

## Running in Visual Studio
1. Open the solution in **Visual Studio 2022 (≥17.14)**.
2. Start ArcGIS Pro with the Add-In loaded. The bridge **starts automatically** when Pro loads (the module uses `autoLoad="true"` and starts the pipe in `Module1.Initialize()`), opening the `ArcGisProBridgePipe` named pipe. *(The **"Start MCP Server"** button on the Add-In tab, group *MCP Bridge*, is now optional — use it only to restart the bridge or confirm it is running.)*
3. In VS, open **Copilot Chat Agent Mode**.
4. Copilot reads `.mcp.json` and starts the MCP server.
5. Ask Copilot (Agent Mode) to use the tools, e.g.:
   - "list the layers in the active map" → `list_layers`
   - "count features in the Buildings layer" → `count_features` with `layer=Buildings`
   - "select buildings where HEIGHT > 50" → `select_by_attribute` with `layer=Buildings`, `where=HEIGHT > 50`
   - "what fields does the Roads layer have?" → `list_fields`
   - "what's the current map extent?" → `get_current_extent`

---

## Portable distribution (for colleagues with ArcGIS Pro but no .NET)

A ready-to-share package lives in [`dist/ArcGisMcp-Portable/`](dist/ArcGisMcp-Portable). It targets machines that have **ArcGIS Pro 3.7+** but **no .NET SDK/runtime**:

| File | Purpose |
|------|---------|
| `ArcGisMcpServer.exe` | The MCP server published **self-contained, single-file** (bundles the .NET runtime — nothing to install). |
| `APBridgeAddIn.esriAddinX` | The prebuilt ArcGIS Pro Add-In (Pro supplies its own runtime). |
| `Install.cmd` / `Install.ps1` | One-click installer: copies the server to `C:\ArcGisMcp`, drops the Add-In into the ArcGIS Pro well-known folder (`Documents\ArcGIS\AddIns\ArcGISPro`, OneDrive-aware), and registers the server in `~/.copilot/mcp-config.json` (merging, not clobbering, existing servers). |
| `Uninstall.cmd` / `Uninstall.ps1` | Reverses the install. |
| `README-SETUP.md` | Non-developer setup guide. |

Colleagues just unzip the folder and double-click **`Install.cmd`**, then restart the GitHub Copilot app. Because the bridge auto-starts with ArcGIS Pro, there is no button to click.

### Rebuilding the portable package
Run the maintainer script (requires the .NET SDK and ArcGIS Pro 3.7+ installed):
```powershell
pwsh ./scripts/build-portable.ps1          # win-x64 (default)
# pwsh ./scripts/build-portable.ps1 -Runtime win-arm64
```
It publishes the self-contained server exe, builds the Add-In `.esriAddinX`, and zips them into
`dist/ArcGisMcp-Portable/` (+ `dist/ArcGisMcp-Portable.zip`). Those build outputs are gitignored —
upload them as GitHub Release assets.

> The server is published **untrimmed** on purpose: MCP tool discovery uses reflection (`WithToolsFromAssembly`), which trimming can break.

---



## Next Steps
- Already implemented in this repo: `select_by_attribute`, `clear_selection`, `get_selection_count`, `get_current_extent`, `list_fields`, `zoom_to_features`, `zoom_to_selection`.
- Add more operations, e.g. `pro.exportLayer`, `pro.getFeatureAttributes`, `pro.runGeoprocessing`.
- Add retry/timeout logic for IPC communication (the client currently uses a fixed 2s connect timeout).
- Containerize the MCP server for deployment.
---

## Credits & License

- **Original project:** [nicogis/MCP-Server-ArcGIS-Pro-AddIn](https://github.com/nicogis/MCP-Server-ArcGIS-Pro-AddIn) by **Studio A&T s.r.l.** — thank you for the foundation this builds on.
- **Fork enhancements:** Ahmed ElHussiny (see [CHANGELOG.md](CHANGELOG.md)).
- **License:** MIT (see [LICENSE](LICENSE) and [NOTICE](NOTICE)). The original copyright is retained; fork modifications are released under the same MIT License.

![MCP Server with ArcGIS Pro Add-In](MCPServer_ArcGISAddIn.gif)