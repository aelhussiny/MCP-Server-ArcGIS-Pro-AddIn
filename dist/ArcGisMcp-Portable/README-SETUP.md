# ArcGIS Pro MCP Bridge — Setup (for ArcGIS Pro users)

This lets the **GitHub Copilot app** talk to **ArcGIS Pro** — you can ask it in plain English to list layers, count/select features, zoom to features, and more.

You do **not** need to be a developer, and you do **not** need to install .NET or any other tools. Everything needed is in this folder.

---

## What you need first
- **ArcGIS Pro 3.7 or newer** installed.
- The **GitHub Copilot** app (from the Microsoft Store), signed in.
- Windows 64-bit.

---

## Install (the easy way)
1. Keep all the files in this folder together (don't separate them).
2. **Double-click `Install.cmd`.**
   - If Windows shows a blue "Windows protected your PC" box, click **More info → Run anyway** (this is normal for a program that isn't from the Store).
3. When it says **Done!**, close the window.

That's it. The installer:
- copies the server to `C:\ArcGisMcp`,
- installs the ArcGIS Pro Add-In,
- registers the server with your GitHub Copilot app.

### After installing
1. **Restart the GitHub Copilot app** (fully close and reopen it).
2. **Start ArcGIS Pro** and open a project that has a map with layers.
   - The bridge now starts **automatically** — you do not need to click any button.
3. In a Copilot chat (make sure it's in **agent** mode), test it:
   - Type: **“Use the arcgis ping tool.”** → you should get `pong …`
   - Type: **“List the layers in the active map.”** → you should see your layers.

---

## What you can ask it to do
Once ArcGIS Pro is open with a map:

| Ask something like… | What happens |
|---|---|
| “What’s the active map name?” | Returns the current map name |
| “List the layers in the active map.” | Lists all layers |
| “What fields does the *Roads* layer have?” | Lists field names and types |
| “How many features are in *Buildings*?” | Returns the count |
| “What’s the current map extent?” | Returns the view extent |
| “In *Buildings*, select features where HEIGHT > 50.” | Selects matching features |
| “How many features are selected in *Buildings*?” | Returns the selection count |
| “In *Buildings*, select features where HEIGHT > 50 and zoom to them.” | Selects **and zooms** to them |
| “Zoom to the selected features.” | Zooms to the current selection |
| “Zoom to the *Buildings* layer.” | Zooms to the whole layer |
| “Clear the selection.” | Clears the selection |

Replace *Buildings* / *Roads* / *HEIGHT* with layer and field names that exist in your project (ask it to “list the layers” first).

---

## If something doesn't work
| Problem | Fix |
|---|---|
| Copilot says it has no `ping`/`arcgis` tool | Make sure you **restarted the Copilot app** after installing, and that the chat is in **agent mode**. Check **Customize → MCP → Installed** for an `arcgis` entry. |
| `ping` works but “list layers” fails | **ArcGIS Pro must be open with a map.** Open your project and try again. |
| “layer not found” | Use the exact layer name — ask “list the layers” first and copy the name. |
| Windows blocked the installer | Right-click `Install.cmd` → **Properties** → check **Unblock** → OK, then run it again. |

---

## To remove it
Double-click **`Uninstall.cmd`**, then restart the Copilot app and ArcGIS Pro.

---

## What's in this folder
| File | Purpose |
|---|---|
| `ArcGisMcpServer.exe` | The MCP server (self-contained — includes its own .NET runtime) |
| `APBridgeAddIn.esriAddinX` | The ArcGIS Pro Add-In |
| `Install.cmd` / `Install.ps1` | One-click installer |
| `Uninstall.cmd` / `Uninstall.ps1` | One-click uninstaller |
| `README-SETUP.md` | This file |
