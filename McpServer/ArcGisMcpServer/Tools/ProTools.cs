using ArcGisMcpServer.Ipc;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ArcGisMcpServer.Tools
{

    [McpServerToolType]
    public static class ProTools
    {
        private static BridgeClient? _client;
        public static void Configure(BridgeClient client) => _client = client;

        [McpServerTool, Description("Name of the active map in ArcGIS Pro")]
        public static async Task<string> GetActiveMapName()
        {
            var r = await _client!.OpAsync("pro.getActiveMapName");
            if (!r.Ok) throw new Exception(r.Error);
            return ((System.Text.Json.JsonElement)r.Data!).GetProperty("name").GetString()!;
        }

        [McpServerTool, Description("List of layers in the active map")]
        public static async Task<List<string>> ListLayers()
        {
            var r = await _client!.OpAsync("pro.listLayers");
            if (!r.Ok) throw new Exception(r.Error);
            return
            System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.Data!.ToString()!)!;
        }

        [McpServerTool, Description("Count features in a layer by name")]
        public static async Task<int> CountFeatures(string layer)
        {
            var r = await _client!.OpAsync("pro.countFeatures", new()
            {
                ["layer"] = layer
            });
            if (!r.Ok) throw new Exception(r.Error);
            return
            ((System.Text.Json.JsonElement)r.Data!).GetProperty("count").GetInt32();
        }

        [McpServerTool, Description("Zoom to a layer's extent by name")]
        public static async Task<bool> ZoomToLayer(string layer)
        {
            var r = await _client!.OpAsync("pro.zoomToLayer", new()
            {
                ["layer"] = layer
            });
            if (!r.Ok) throw new Exception(r.Error);
            return true;
        }

        [McpServerTool, Description("Select features in a layer using a SQL where clause; returns the number of selected features")]
        public static async Task<int> SelectByAttribute(string layer, string where)
        {
            var r = await _client!.OpAsync("pro.selectByAttribute", new()
            {
                ["layer"] = layer,
                ["where"] = where
            });
            if (!r.Ok) throw new Exception(r.Error);
            return ((System.Text.Json.JsonElement)r.Data!).GetProperty("count").GetInt32();
        }

        [McpServerTool, Description("Clear the current selection on a layer, or on the whole active map when no layer is given")]
        public static async Task<bool> ClearSelection(string? layer = null)
        {
            var args = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(layer)) args["layer"] = layer;
            var r = await _client!.OpAsync("pro.clearSelection", args);
            if (!r.Ok) throw new Exception(r.Error);
            return true;
        }

        [McpServerTool, Description("Get the number of currently selected features in a layer by name")]
        public static async Task<int> GetSelectionCount(string layer)
        {
            var r = await _client!.OpAsync("pro.getSelectionCount", new()
            {
                ["layer"] = layer
            });
            if (!r.Ok) throw new Exception(r.Error);
            return ((System.Text.Json.JsonElement)r.Data!).GetProperty("count").GetInt32();
        }

        [McpServerTool, Description("Get the current extent of the active map view (xmin, ymin, xmax, ymax and spatial reference)")]
        public static async Task<MapExtent> GetCurrentExtent()
        {
            var r = await _client!.OpAsync("pro.getCurrentExtent");
            if (!r.Ok) throw new Exception(r.Error);
            return System.Text.Json.JsonSerializer.Deserialize<MapExtent>(r.Data!.ToString()!)!;
        }

        [McpServerTool, Description("List the fields (name and type) of a feature layer by name")]
        public static async Task<List<FieldInfo>> ListFields(string layer)
        {
            var r = await _client!.OpAsync("pro.listFields", new()
            {
                ["layer"] = layer
            });
            if (!r.Ok) throw new Exception(r.Error);
            return System.Text.Json.JsonSerializer.Deserialize<List<FieldInfo>>(r.Data!.ToString()!)!;
        }

        [McpServerTool, Description("Select features in a layer by a SQL where clause and zoom the map to their extent; returns the number of features zoomed to")]
        public static async Task<int> ZoomToFeatures(string layer, string where)
        {
            var r = await _client!.OpAsync("pro.zoomToFeatures", new()
            {
                ["layer"] = layer,
                ["where"] = where
            });
            if (!r.Ok) throw new Exception(r.Error);
            return ((System.Text.Json.JsonElement)r.Data!).GetProperty("count").GetInt32();
        }

        [McpServerTool, Description("Zoom the map to the currently selected features of a layer, or to the whole map's selection when no layer is given")]
        public static async Task<bool> ZoomToSelection(string? layer = null)
        {
            var args = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(layer)) args["layer"] = layer;
            var r = await _client!.OpAsync("pro.zoomToSelection", args);
            if (!r.Ok) throw new Exception(r.Error);
            return true;
        }

        [McpServerTool, Description("Identify the current selection: for every selected feature, list each field and its value. Optionally restrict to one layer, and cap how many features per layer are returned (default 50).")]
        public static async Task<SelectionDetails> GetSelectionDetails(string? layer = null, int limit = 50)
        {
            var args = new Dictionary<string, string> { ["limit"] = limit.ToString() };
            if (!string.IsNullOrWhiteSpace(layer)) args["layer"] = layer;
            var r = await _client!.OpAsync("pro.getSelectionDetails", args);
            if (!r.Ok) throw new Exception(r.Error);
            return System.Text.Json.JsonSerializer.Deserialize<SelectionDetails>(r.Data!.ToString()!)!;
        }

        [McpServerTool, Description("Ping test to validate the MCP server (without depending on ArcGIS Pro)")]
        public static Task<string> Ping()
        {
            // Do not use the IPC bridge: this ping only verifies that the MCP server is running.
            return Task.FromResult($"pong {DateTimeOffset.UtcNow:O}");
        }

        [McpServerTool, Description("MCP echo test")]
        public static string Echo(string text)
        {
            return $"echo: {text}";
        }
    }

    public record MapExtent(
        [property: System.Text.Json.Serialization.JsonPropertyName("xmin")] double XMin,
        [property: System.Text.Json.Serialization.JsonPropertyName("ymin")] double YMin,
        [property: System.Text.Json.Serialization.JsonPropertyName("xmax")] double XMax,
        [property: System.Text.Json.Serialization.JsonPropertyName("ymax")] double YMax,
        [property: System.Text.Json.Serialization.JsonPropertyName("spatialReference")] string? SpatialReference
    );

    public record FieldInfo(
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("type")] string Type
    );

    public record SelectionDetails(
        [property: System.Text.Json.Serialization.JsonPropertyName("layers")] List<LayerSelection> Layers
    );

    public record LayerSelection(
        [property: System.Text.Json.Serialization.JsonPropertyName("layer")] string Layer,
        [property: System.Text.Json.Serialization.JsonPropertyName("count")] long Count,
        [property: System.Text.Json.Serialization.JsonPropertyName("returned")] int Returned,
        [property: System.Text.Json.Serialization.JsonPropertyName("features")] List<SelectionFeature> Features
    );

    public record SelectionFeature(
        [property: System.Text.Json.Serialization.JsonPropertyName("oid")] long Oid,
        [property: System.Text.Json.Serialization.JsonPropertyName("attributes")] Dictionary<string, System.Text.Json.JsonElement> Attributes
    );
}
