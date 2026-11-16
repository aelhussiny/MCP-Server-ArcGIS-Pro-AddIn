# Changelog

All notable changes in this fork are documented here. This project is a fork of
[nicogis/MCP-Server-ArcGIS-Pro-AddIn](https://github.com/nicogis/MCP-Server-ArcGIS-Pro-AddIn);
the entries below describe enhancements made on top of that original work.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.2.0]

### Added
- **`get_selection_details`** — an "identify" tool. For the current selection it returns, per layer,
  every selected feature's object ID and all of its field names and values (skipping geometry/blob/raster
  fields). Optional `layer` filter and a `limit` (default 50) to cap features per layer. Ask things like
  *"tell me the details of the selection."*

## [1.1.0] — fork enhancements

### Added
- **Seven new MCP tools** exposed on both the server and the Add-In:
  - `select_by_attribute` — select features by a SQL `where` clause (returns the count).
  - `clear_selection` — clear a layer's selection, or the whole map's.
  - `get_selection_count` — number of selected features in a layer.
  - `get_current_extent` — active map view extent (xmin/ymin/xmax/ymax + spatial reference).
  - `list_fields` — field names and types of a feature layer.
  - `zoom_to_features` — select features by a `where` clause **and** zoom to their combined extent.
  - `zoom_to_selection` — zoom to the current selection of a layer, or the whole map's.
- **Auto-start of the bridge**: the Add-In module uses `autoLoad="true"` and starts the
  named pipe in `Module1.Initialize()`, so no button click is required each session. The
  ribbon button now just restarts/confirms the bridge.
- **Portable, dependency-free distribution** (`dist/ArcGisMcp-Portable/`): a self-contained,
  single-file server executable plus one-click `Install`/`Uninstall` scripts and a
  non-developer setup guide, so colleagues who have only ArcGIS Pro (no .NET) can install it.

### Changed
- **Targets ArcGIS Pro 3.7 / .NET 10**: the Add-In now targets `net10.0-windows` to match the
  assemblies shipped with ArcGIS Pro 3.7 (fixes the `CS1705` `System.Runtime` version mismatch).
- **Clean, professional UI text**: Add-In name, description, module/group captions, button
  caption, tooltip, and the confirmation dialog were rewritten in clear English.
- Translated leftover non-English code comments to English.

### Fixed
- **MCP stdio protocol pollution**: host logging was writing to `stdout`, which corrupts the
  JSON-RPC stream expected by MCP clients. Logging is now routed to `stderr`, keeping `stdout`
  reserved for protocol messages.

### Docs
- Rewrote `README.md` with a tool reference table, the .NET/ArcGIS-Pro version alignment,
  a "how to add a tool" guide, and a "Portable distribution" section.
- Added this `CHANGELOG.md` and a `NOTICE` file crediting the original author.
