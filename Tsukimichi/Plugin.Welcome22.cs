using System;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// 1.22.0 "Welcome home" wiring for updates and Umbra (plan v8 U1, M1, M2, M3): the update watcher (the status-bar note,
/// Settings › About, and <see cref="Updates"/> for the moon icon's dot), Umbra read-only (<see cref="Game.UmbraProbe"/>,
/// which keeps <see cref="Game.UmbraLayout"/> current for the moon icon, the Todo overlay and Needs you, and gives the
/// Follow Umbra palette), the summary of tonight (<see cref="SummarySource"/>) that the server info bar entry and the
/// summary IPC gates read, and the entry itself. Called once from the constructor after the IPC gates exist; everything
/// is unwound in <see cref="TearDownWelcomeHome"/>.
/// </summary>
public sealed partial class Plugin
{
    private Game.UpdateWatcher? updateWatcher;
    private Game.UmbraProbe? umbraProbe;
    private SummarySource? summarySource;
    private EventWarningSource? eventWarningSource;

    /// <summary>What is known about an update (U1): the moon icon shows its dot while <c>Current.ShowsNote</c>. Null before load.</summary>
    internal Game.UpdateWatcher? Updates => updateWatcher;

    /// <summary>Umbra, read-only (M3): its toolbar's clearance is <see cref="Game.UmbraLayout.Clearance"/>. Null before load.</summary>
    internal Game.UmbraProbe? Umbra => umbraProbe;

    /// <summary>Tonight in a few lines for the logged-in character (M1, M2; the moon icon's quick card can read it). Null before load.</summary>
    internal SummarySource? Summary => summarySource;

    private void InitializeWelcomeHome(Core.Runtime.HookGate gate, string pluginVersion)
    {
        // U1: ask Dalamud at login and every 3 hours; no request of Tsukimichi's own.
        var updates = new Game.UpdateWatcher(PluginInterface, Framework, ClientState, ChatGui, Settings, pluginVersion, Log);
        updateWatcher = updates;
        mainWindow.Updates = updates;

        // M3: Umbra's toolbar and colour profile, read-only from its saved settings.
        var umbra = new Game.UmbraProbe(PluginInterface, Framework, Settings, () => Session.LiveContentId, static () => UiMetrics.UiScale, Log);
        umbraProbe = umbra;
        if (configWindow is { } settingsWindow)
        {
            settingsWindow.Updates = updates;
            settingsWindow.Umbra = umbra;
        }

        if (guidanceCommand is not { } guidance || discoveryWindow is not { } nearby)
        {
            return;
        }

        // M1 and M2: tonight in a few lines, the server info bar entry and the summary gates.
        var summary = new SummarySource(Session, guidance, () => nearby.StartableCount, eventWarningSource);
        summarySource = summary;
        dtrEntry = new Game.DtrEntry(DtrBar, DataManager, nearby, nearby.Settings, gate, summary, umbra, () => OpenWelcomePlace(IpcPlaces.Main), () => OpenWelcomePlace(IpcPlaces.Tonight), Log);
        if (ipcProvider is { } ipc)
        {
            ipc.Summary = () => summary.Current;
            ipc.ThemeKey = static () => Ui.Themes.GlyphSeam.Appearance.Theme.Key;
            ipc.OpenPlace = OpenWelcomePlace;
            ipc.Hello = (addon, version) =>
            {
                if (string.Equals(addon, Core.Umbra.UmbraSettings.AddonName, StringComparison.OrdinalIgnoreCase))
                {
                    umbra.AddonHello(version);
                }
            };
            summary.Changed += ipc.AnnounceSummary;
        }

        Framework.Update += WelcomeHomeTick;
    }

    /// <summary>Once per frame: the summary when its inputs moved, the entry when its words did, the palette Umbra gives.</summary>
    private void WelcomeHomeTick(Dalamud.Plugin.Services.IFramework framework)
    {
        try
        {
            summarySource?.Refresh(framework.LastUpdateUTC.Subtract(DateTime.UnixEpoch).TotalSeconds);
            dtrEntry?.Tick();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "The summary or the server info bar entry could not be refreshed");
        }
    }

    /// <summary>
    /// Opens Tsukimichi at a place (IPC <c>OpenAt</c>, the server info bar's clicks): only Tsukimichi's own windows, never
    /// travel, a route or a run. Up next is Tonight's first block, so it opens Tonight.
    /// </summary>
    private void OpenWelcomePlace(string place)
    {
        switch (place)
        {
            case IpcPlaces.Settings:
                if (configWindow is { } settingsWindow)
                {
                    settingsWindow.IsOpen = true;
                    settingsWindow.BringToFront();
                }

                return;
            case IpcPlaces.Route:
                routeWindow?.ShowFollowed();
                return;
            case IpcPlaces.Tonight or IpcPlaces.UpNext:
                ui.SelectedRowId = null;
                break;
        }

        mainWindow.IsOpen = true;
        mainWindow.BringToFront();
    }

    /// <summary>Unwinds the 1.22.0 pieces (from <see cref="TearDown"/>).</summary>
    private void TearDownWelcomeHome()
    {
        Framework.Update -= WelcomeHomeTick;
        if (summarySource is { } summary && ipcProvider is { } ipc)
        {
            summary.Changed -= ipc.AnnounceSummary;
        }

        updateWatcher?.Dispose();
        umbraProbe?.Dispose();
    }

    /// <summary>
    /// Once per frame, before the palette is chosen: the Follow Umbra palette while the player chose it (Night until Umbra's
    /// colours are read), otherwise none.
    /// </summary>
    private void RefreshFollowUmbra() =>
        Theme.FollowUmbra = Settings.FollowUmbraPalette ? umbraProbe?.Palette ?? Core.Ui.Themes.UiPalettes.Night : null;
}
