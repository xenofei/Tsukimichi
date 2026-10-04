using System;
using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// 1.22.0 moon icon wiring (feature plan v8 H1, H2): the icon over the game, drawn from <c>UiBuilder.Draw</c>, its quick
/// card's sources (Up next through the guidance command, the ending-soon events, Needs you), <c>/tsuki icon</c> and
/// Settings › In game › Moon icon's Reset position. Umbra's toolbar (<see cref="Core.Ui.IUmbraLayout"/>) and the update
/// state (<see cref="Core.Ui.IUpdateState"/>) are set on <see cref="moonIcon"/> once their services exist; until then
/// the icon keeps no clearance and shows no update dot. Called once from the constructor after the Todo overlay and the
/// events (<see cref="InitializeMoonIcon"/>), unwound by <see cref="DisposeMoonIcon"/>.
/// </summary>
public sealed partial class Plugin
{
    private MoonIconWindow? moonIcon;
    private MoonIconCard? moonIconCard;
    private bool moonIconDrawSubscribed;
    private bool moonIconFailed;

    /// <summary>The ending-soon warnings (Plugin.ReplayAndEvents.cs), for the moon icon's quick card.</summary>
    private EventWarningSource? eventWarnings;

    private void InitializeMoonIcon()
    {
        var card = new MoonIconCard(Session, gameLinks)
        {
            Guidance = guidanceCommand,
            Events = eventWarnings,
            Stops = runStops,
        };

        void OpenTonight()
        {
            // The quick card describes the logged-in character: Tonight opens on it.
            ViewLiveCharacter();
            ui.Tab = NavTab.Journal;
            ui.SelectedRowId = null;
            mainWindow.IsOpen = true;
            mainWindow.BringToFront();
        }

        void OpenSettings() => configWindow?.OpenAt(Core.Ui.SettingsSection.InGame, Core.Ui.SettingsAnchor.MoonIcon);

        moonIconCard = card;
        moonIcon = new MoonIconWindow(Settings, () => Settings.Save(PluginInterface), ClientState, Condition, mainWindow.Toggle, OpenTonight, OpenSettings, card)
        {
            Stops = runStops,
            Log = Log,
        };

        MoonIconWindow icon = moonIcon;
        command.ToggleMoonIcon = icon.ToggleEnabled;
        if (configWindow is { } settings)
        {
            settings.ResetMoonIconPosition = icon.ResetPosition;
        }

        PluginInterface.UiBuilder.Draw += DrawMoonIcon;
        moonIconDrawSubscribed = true;
    }

    /// <summary>A failed frame rests the icon, then it tries again (<see cref="Core.Ui.DrawRetry"/>): one bad frame never hides it for the session.</summary>
    private readonly Core.Ui.DrawRetry moonIconRetry = new();

    /// <summary>
    /// The icon, its card and its Hide toast, in the body font. A failure hides it for a back-off (1 s, doubling to 30 s)
    /// and is logged at most once a minute; a frame that draws resets the back-off.
    /// </summary>
    private void DrawMoonIcon()
    {
        var now = Dalamud.Bindings.ImGui.ImGui.GetTime();
        moonIconFailed = !moonIconRetry.Ready(now);
        if (moonIcon is not { } icon || moonIconFailed)
        {
            return;
        }

        try
        {
            icon.Draw();
            moonIconRetry.Succeeded();
        }
        catch (Exception ex)
        {
            moonIconFailed = true;
            if (moonIconRetry.Failed(now))
            {
                Log.Error(ex, "The moon icon could not be drawn; it tries again in {Seconds} s", moonIconRetry.Backoff);
            }
        }
    }

    private void DisposeMoonIcon()
    {
        if (moonIconDrawSubscribed)
        {
            moonIconDrawSubscribed = false;
            PluginInterface.UiBuilder.Draw -= DrawMoonIcon;
        }

        if (command is not null)
        {
            command.ToggleMoonIcon = null;
        }
    }
}
