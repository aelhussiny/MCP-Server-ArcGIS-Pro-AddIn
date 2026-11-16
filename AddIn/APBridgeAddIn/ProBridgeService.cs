using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace APBridgeAddIn
{
    internal class ProBridgeService : IDisposable
    {
        private readonly string _pipeName;
        private CancellationTokenSource _cts;
        private Task _serverLoop;
        public ProBridgeService(string pipeName) => _pipeName = pipeName;
        public void Start()
        {
            _cts = new CancellationTokenSource();
            _serverLoop = Task.Run(() => RunAsync(_cts.Token));
        }
        public void Dispose()
        {
            try { _cts?.Cancel(); _serverLoop?.Wait(2000); } catch { }
        }
        private async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                using var server = new NamedPipeServerStream(_pipeName,
                PipeDirection.InOut, 1, PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(ct);
                using var reader = new StreamReader(server, Encoding.UTF8,
                leaveOpen: true);
                using var writer = new StreamWriter(server, new
                UTF8Encoding(false), leaveOpen: true)
                { AutoFlush = true };
                // Handle requests on this connection
                while (server.IsConnected && !ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;
                    IpcRequest req;
                    try
                    {
                        req =
                    JsonSerializer.Deserialize<IpcRequest>(line);
                    }
                    catch (Exception ex)
                    {
                        await SendAsync(writer, new IpcResponse(false,
                        $"parse:{ex.Message}", null));
                        continue;
                    }
                    try
                    {
                        var resp = await HandleAsync(req, ct);
                        await SendAsync(writer, resp);
                    }
                    catch (Exception ex)
                    {
                        await SendAsync(writer, new IpcResponse(false,
                        ex.Message, null));
                    }
                }
            }
        }
        private static Task SendAsync(StreamWriter w, IpcResponse resp)
        => w.WriteLineAsync(JsonSerializer.Serialize(resp));
        private static async Task<IpcResponse> HandleAsync(IpcRequest req,
        CancellationToken ct)
        {
            switch (req.Op)
            {
                case "pro.getActiveMapName":
                    var name = MapView.Active?.Map?.Name ?? "<none>";
                    return new(true, null, new { name });
                case "pro.listLayers":
                    var layers = await QueuedTask.Run(() =>
                    MapView.Active?.Map?.Layers.Select(l => l.Name).ToList() ?? new
                    List<string>());
                    return new(true, null, layers);
                case "pro.countFeatures":
                    {
                        if (req.Args == null)
                            return new(false, "arg 'layer' required", null);


                        if (!req.Args.TryGetValue("layer", out string? layerName) ||
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
                case "pro.zoomToLayer":
                    {
                        if (req.Args == null ||
                            !req.Args.TryGetValue("layer", out string? layerName) ||
                            string.IsNullOrWhiteSpace(layerName))
                            return new(false, "arg 'layer' required", null);


                        await QueuedTask.Run(async () =>
                        {
                            var fl = MapView.Active?.Map?.Layers
                            .OfType<FeatureLayer>()
                            .FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
                            if (fl != null)
                                await MapView.Active!.ZoomToAsync(fl);
                        });


                        return new(true, null, new { done = true });
                    }
                case "pro.selectByAttribute":
                    {
                        if (req.Args == null ||
                        !req.Args.TryGetValue("layer", out string? layerName) ||
                        string.IsNullOrWhiteSpace(layerName) ||
                        !req.Args.TryGetValue("where", out string? where) ||
                        string.IsNullOrWhiteSpace(where))
                            return new(false, "args 'layer' & 'where' required", null);


                        long count = await QueuedTask.Run<long>(() =>
                        {
                            var fl = MapView.Active?.Map?.Layers
                            .OfType<FeatureLayer>()
                            .FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
                            if (fl == null) return 0L;
                            fl.Select(new ArcGIS.Core.Data.QueryFilter { WhereClause = where });
                            using var sel = fl.GetSelection();
                            return sel.GetCount();
                        });


                        return new(true, null, new { count });
                    }
                case "pro.clearSelection":
                    {
                        await QueuedTask.Run(() =>
                        {
                            var map = MapView.Active?.Map;
                            if (map == null) return;
                            if (req.Args != null &&
                                req.Args.TryGetValue("layer", out string? layerName) &&
                                !string.IsNullOrWhiteSpace(layerName))
                            {
                                var fl = map.Layers
                                .OfType<FeatureLayer>()
                                .FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
                                fl?.ClearSelection();
                            }
                            else
                            {
                                map.ClearSelection();
                            }
                        });

                        return new(true, null, new { done = true });
                    }
                case "pro.getSelectionCount":
                    {
                        if (req.Args == null ||
                            !req.Args.TryGetValue("layer", out string? layerName) ||
                            string.IsNullOrWhiteSpace(layerName))
                            return new(false, "arg 'layer' required", null);

                        long count = await QueuedTask.Run<long>(() =>
                        {
                            var fl = MapView.Active?.Map?.Layers
                            .OfType<FeatureLayer>()
                            .FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
                            if (fl == null) return 0L;
                            using var sel = fl.GetSelection();
                            return sel.GetCount();
                        });

                        return new(true, null, new { count });
                    }
                case "pro.getCurrentExtent":
                    {
                        var extent = await QueuedTask.Run(() =>
                        {
                            var env = MapView.Active?.Extent;
                            if (env == null) return null;
                            return (object)new
                            {
                                xmin = env.XMin,
                                ymin = env.YMin,
                                xmax = env.XMax,
                                ymax = env.YMax,
                                spatialReference = env.SpatialReference?.Name
                            };
                        });

                        if (extent == null)
                            return new(false, "no active map view", null);

                        return new(true, null, extent);
                    }
                case "pro.listFields":
                    {
                        if (req.Args == null ||
                            !req.Args.TryGetValue("layer", out string? layerName) ||
                            string.IsNullOrWhiteSpace(layerName))
                            return new(false, "arg 'layer' required", null);

                        var fields = await QueuedTask.Run(() =>
                        {
                            var fl = MapView.Active?.Map?.Layers
                            .OfType<FeatureLayer>()
                            .FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
                            if (fl == null) return null;
                            using var fc = fl.GetFeatureClass();
                            var def = fc.GetDefinition();
                            return def.GetFields()
                                .Select(f => new { name = f.Name, type = f.FieldType.ToString() })
                                .ToList();
                        });

                        if (fields == null)
                            return new(false, $"layer not found: {layerName}", null);

                        return new(true, null, fields);
                    }
                case "pro.zoomToFeatures":
                    {
                        if (req.Args == null ||
                            !req.Args.TryGetValue("layer", out string? layerName) ||
                            string.IsNullOrWhiteSpace(layerName) ||
                            !req.Args.TryGetValue("where", out string? where) ||
                            string.IsNullOrWhiteSpace(where))
                            return new(false, "args 'layer' & 'where' required", null);

                        long count = await QueuedTask.Run(async () =>
                        {
                            var fl = MapView.Active?.Map?.Layers
                            .OfType<FeatureLayer>()
                            .FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
                            if (fl == null) return -1L;
                            fl.Select(new ArcGIS.Core.Data.QueryFilter { WhereClause = where });
                            using var sel = fl.GetSelection();
                            var oids = sel.GetObjectIDs();
                            if (oids.Count > 0)
                                await MapView.Active!.ZoomToAsync(fl, oids, TimeSpan.FromSeconds(1.0), false);
                            return (long)oids.Count;
                        });

                        if (count < 0)
                            return new(false, $"layer not found: {layerName}", null);

                        return new(true, null, new { count });
                    }
                case "pro.zoomToSelection":
                    {
                        int result = await QueuedTask.Run(async () =>
                        {
                            var map = MapView.Active?.Map;
                            if (map == null) return -2; // no active map view

                            if (req.Args != null &&
                                req.Args.TryGetValue("layer", out string? layerName) &&
                                !string.IsNullOrWhiteSpace(layerName))
                            {
                                var fl = map.Layers
                                .OfType<FeatureLayer>()
                                .FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
                                if (fl == null) return -1; // layer not found
                                using var sel = fl.GetSelection();
                                var oids = sel.GetObjectIDs();
                                if (oids.Count == 0) return 0; // nothing selected
                                await MapView.Active!.ZoomToAsync(fl, oids, TimeSpan.FromSeconds(1.0), false);
                                return oids.Count;
                            }

                            await MapView.Active!.ZoomToSelectedAsync(TimeSpan.FromSeconds(1.0), false);
                            return 1;
                        });

                        if (result == -2)
                            return new(false, "no active map view", null);
                        if (result == -1)
                            return new(false, "layer not found", null);

                        return new(true, null, new { done = true });
                    }
                case "pro.getSelectionDetails":
                    {
                        int limit = 50;
                        if (req.Args != null &&
                            req.Args.TryGetValue("limit", out string? limStr) &&
                            int.TryParse(limStr, out int parsedLimit) && parsedLimit > 0)
                            limit = parsedLimit;

                        string? layerFilter = null;
                        if (req.Args != null &&
                            req.Args.TryGetValue("layer", out string? lf) &&
                            !string.IsNullOrWhiteSpace(lf))
                            layerFilter = lf;

                        var details = await QueuedTask.Run(() =>
                        {
                            var map = MapView.Active?.Map;
                            if (map == null) return null;

                            var layersOut = new List<object>();
                            var featureLayers = map.Layers.OfType<FeatureLayer>()
                                .Where(l => layerFilter == null ||
                                            l.Name.Equals(layerFilter, StringComparison.OrdinalIgnoreCase));

                            foreach (var fl in featureLayers)
                            {
                                using var sel = fl.GetSelection();
                                long count = sel.GetCount();
                                if (count == 0) continue;

                                var oids = sel.GetObjectIDs().Take(limit).ToList();
                                using var fc = fl.GetFeatureClass();
                                var def = fc.GetDefinition();
                                string oidField = def.GetObjectIDField();
                                var fields = def.GetFields()
                                    .Where(f => f.FieldType != ArcGIS.Core.Data.FieldType.Geometry &&
                                                f.FieldType != ArcGIS.Core.Data.FieldType.Blob &&
                                                f.FieldType != ArcGIS.Core.Data.FieldType.Raster)
                                    .ToList();

                                var featuresOut = new List<object>();
                                var qf = new ArcGIS.Core.Data.QueryFilter { ObjectIDs = oids };
                                using (var cursor = fc.Search(qf, false))
                                {
                                    while (cursor.MoveNext())
                                    {
                                        using var row = cursor.Current;
                                        var attrs = new Dictionary<string, object?>();
                                        foreach (var f in fields)
                                        {
                                            var val = row[f.Name];
                                            // Keep JSON-friendly types; stringify anything exotic.
                                            if (val is null || val is string || val is bool ||
                                                val is sbyte || val is byte || val is short || val is ushort ||
                                                val is int || val is uint || val is long || val is ulong ||
                                                val is float || val is double || val is decimal ||
                                                val is DateTime || val is Guid)
                                                attrs[f.Name] = val;
                                            else
                                                attrs[f.Name] = val.ToString();
                                        }
                                        long oid = Convert.ToInt64(row[oidField]);
                                        featuresOut.Add(new { oid, attributes = attrs });
                                    }
                                }

                                layersOut.Add(new
                                {
                                    layer = fl.Name,
                                    count,
                                    returned = featuresOut.Count,
                                    features = featuresOut
                                });
                            }

                            return (object)new { layers = layersOut };
                        });

                        if (details == null)
                            return new(false, "no active map view", null);

                        return new(true, null, details);
                    }
                default:
                    return new(false, $"op not found: {req.Op}", null);
            }
        }
    }
}
