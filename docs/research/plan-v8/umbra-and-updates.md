# Research brief: Umbra integration, Dalamud updates, custom cursors (2026-10-04)

## ANSWER
1. Umbra has no Dalamud IPC/CallGate widget API that was found. Its extension model is "custom plugins": a separate .NET DLL (not a Dalamud plugin) that Umbra loads into its own AssemblyLoadContext. The DLL references Umbra.dll, Umbra.Common.dll and optionally Umbra.Game.dll and Una.Drawing.dll. Widgets use `[ToolbarWidget]` on a `StandardToolbarWidget` subclass; services use `[Service]`. Minimum Umbra is 2.1.0 (SamplePlugin README).
2. Umbra shows Dalamud DTR entries in its DtrBar/DtrSingle widgets: text (SeString), tooltip, left/right click with modifiers, hide and sort. Umbra's source shows no separate icon property, so any icon must be in the DTR text. The cheapest "Umbra support" is a good DTR entry (text, tooltip, click). No Umbra plugin-list widget was found.
3. A plugin can check its own update: `IDalamudPluginInterface.CheckForUpdateAsync()` returns `PluginUpdate?` (Version, IsTesting, Changelog). `OpenPluginInstallerTo(PluginInstallerOpenKind.UpdateablePlugins, searchText)` opens the "Can be updated" page. No API triggers a self-update. Auto-update settings are Dalamud's; plugins can read `IsAutoUpdateComplete`.
4. Just-updated detection: nothing built in was found; store the last-seen version in your config.
5. Custom mouse cursors: no Dalamud API found; see section 3 below (low confidence).

## 1. Umbra
- Licence: AGPL-3.0 (LICENSE; repo header). Linking against Umbra.dll or copying its code would likely make a derivative AGPL. This is a legal reading, not legal advice. Safe options: reference only through DTR, Dalamud public APIs, or your own separate Umbra add-on repo.
- Plugin loading: `Umbra/src/Plugins/PluginManager.cs`, `Plugin.cs` and `Repository/PluginFetcher.cs`. The user must enable "CustomPluginsEnabled" (an EULA gate). Plugins come from a local DLL or from GitHub releases (`api.github.com/repos/{owner}/{repo}/releases/latest`, .dll or .zip assets). Loaded by `PluginLoadContext`, with hot reload for local files. There is no version-compat check in `PluginEntry`.
- Sample: https://github.com/una-xiv/Umbra.SamplePlugin (Widgets/SampleWidget.cs, SampleWidgetWithMenu.cs, Markers/SampleMarkerFactory.cs). The menu sample uses `MenuPopup`, `StandardWidgetFeatures` (Text, Icon, CustomizableIcon), and `Framework.Service` injection (for example `IToastGui`).
- Build refs: https://github.com/una-xiv/umbra-dist (build-workflow assets only).
- Third-party example: https://github.com/duaL-Astriel/Umbra.WindowManager. README says no manifest or entry class is needed, only `[ToolbarWidget]` and `[Service]`. It auto-discovers other plugins' windows via reflection and ImGui context monitoring, so it needs no cooperation from the target plugin.
- DTR: https://github.com/una-xiv/umbra/blob/main/Umbra.Game/src/Dtr/DtrBarEntry.cs (Name, Text, TooltipText, IsInteractive, SortIndex, IsVisible, InvokeClickAction) and `Umbra/src/Toolbar/Widgets/Library/DtrBar/DtrBarWidget.cs` (tooltip, left/right click, modifier keys, CSS classes "dtr-bar-entry" and "decorated").
- "Official support" or "optimized for Umbra": not found. No badge, page or convention was found, and the web search found nothing. The only practical meanings found are (a) a DTR entry that renders well, or (b) a separate Umbra custom-plugin widget.
- Theme and colours: no IPC or config API for reading Umbra's theme was found. Styling is Una.Drawing stylesheets inside Umbra (for example `DurabilityWidget.Stylesheet.cs`), so only an in-process Umbra custom plugin could read them. Umbra popups are Una.Drawing node trees (`MenuPopup`). Source: https://github.com/una-xiv/umbra
- Docs site https://una-xiv.github.io/umbra-docs is JS-rendered and returned no content. The docs repo is https://github.com/una-xiv/umbra-docs (Stencil). Developer docs there: not verified.
- IPC: no Umbra IPC provider found in the file tree (the fetched tree listing was possibly truncated). Not found, not proven absent.

## 2. Dalamud updates (API 15, Dalamud 15.0.3.6, local XML doc `C:\Users\devon\AppData\Roaming\XIVLauncher\addon\Hooks\15.0.3.6\Dalamud.xml`)
- `IDalamudPluginInterface.CheckForUpdateAsync()` -> `Task<PluginUpdate?>`. Docs: "Does not actually re-request data from the remote repository ... Reloads happen at periodic intervals (10 minutes)". It uses Dalamud's own repo data, so no HTTP is needed for custom repos. https://raw.githubusercontent.com/goatcorp/Dalamud/master/Dalamud/Plugin/IDalamudPluginInterface.cs ; `PluginUpdate` record: https://raw.githubusercontent.com/goatcorp/Dalamud/master/Dalamud/Plugin/PluginUpdate.cs
- `bool OpenPluginInstallerTo(PluginInstallerOpenKind openTo = AllPlugins, string? searchText = null)`. Enum values (Dalamud.xml): AllPlugins, InstalledPlugins, UpdateablePlugins ("Can be updated"), Changelogs, DalamudChangelogs. Use `UpdateablePlugins` with the plugin name as search text.
- `InstalledPlugins` returns `IExposedPlugin` (Name, InternalName, Version, IsLoaded, IsTesting, IsThirdParty, IsDev, HasMainUi, HasConfigUi, `OpenMainUi()`, `OpenConfigUi()`). This is how a launcher widget can open other plugins' UIs (Dalamud.xml).
- Auto-update: `AutoUpdateBehavior` (None, OnlyNotify, UpdateMainRepo, and more not captured); per-plugin `AutoUpdatePreference` (NeverUpdate or AlwaysUpdate); `SettingsOpenKind.AutoUpdates`; `IsAutoUpdateComplete` ("auto-updates have already completed this session"). All in Dalamud.xml. Whether third-party repo plugins get notifications by default: not verified.
- No self-update trigger was found in the public plugin interface (`UpdateSinglePluginAsync` is on internal PluginManager only).
- Previous-version tracking: not built in; keep `LastSeenVersion` in config and compare with `Assembly` version on load. Plugin changelogs show in the installer's Changelogs page (manifest Changelog field).
- Examples of plugins doing their own HTTP checks: not found in this research.

## 3. Custom cursors
- Not found: no Dalamud docs, XML entries or plugin examples. Dalamud.xml has only `AddonCursorType` (the game's native addon cursor enum, for addon events), not a custom-image API.
- Standard ImGui technique (unverified in Dalamud here): `ImGui.SetMouseCursor(ImGuiMouseCursor.None)` while over your window, then draw a texture at `ImGui.GetMousePos()` on `ImGui.GetForegroundDrawList()`. It only works while Dalamud's ImGui is receiving input, and the game cursor may still show over non-ImGui areas. Treat as a prototype; test in game.

## CONFIDENCE
High for the Dalamud API facts (read from the installed 15.0.3.6 XML and upstream source). Medium for Umbra (some details came from summarised fetches of source files). Low for IPC and theme absence, and for cursors.

## CAVEATS
Source fetches were summarised by a small model, so exact names should be re-checked at https://github.com/una-xiv/umbra before coding. Umbra has no documented stable plugin API contract; custom plugins may break across Umbra releases.
