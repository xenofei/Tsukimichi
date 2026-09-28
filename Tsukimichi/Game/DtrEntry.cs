using System;
using System.Globalization;
using System.Text;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Discovery;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// Server info bar entry (F-76): "☾ N" where N is the number of quests the viewed character can start in the current
/// zone, with the first few names in the tooltip; a click toggles the Nearby quests window. Follows
/// <see cref="DiscoveryWindow.Changed"/>, which fires on the session and territory triggers, so nothing runs per
/// frame. Hidden at zero unless <see cref="DiscoverySettings.DtrShowWhenEmpty"/>, and entirely while
/// <see cref="DiscoverySettings.ShowDtrEntry"/> is off. The entry is acquired lazily on first show and removed on
/// dispose. Dalamud 15: <c>IDtrBar.Get(string, SeString)</c>, <c>IDtrBarEntry.Text</c>/<c>Tooltip</c> are
/// <see cref="SeString"/>, <c>OnClick</c> is <c>Action&lt;DtrInteractionEvent&gt;</c>.
/// </summary>
public sealed class DtrEntry : IDisposable
{
    public const string Title = Strings.DiscoveryDtrTitle;

    /// <summary>Quest names listed in the tooltip before "and N more".</summary>
    public const int MaxTooltipQuests = 5;

    private readonly IDtrBar bar;
    private readonly DiscoveryWindow window;
    private readonly DiscoverySettings settings;
    private readonly IPluginLog log;
    private readonly StringBuilder tooltip = new();
    private IDtrBarEntry? entry;
    private bool disposed;
    private bool warned;

    public DtrEntry(IDtrBar bar, DiscoveryWindow window, DiscoverySettings settings, IPluginLog log)
    {
        this.bar = bar ?? throw new ArgumentNullException(nameof(bar));
        this.window = window ?? throw new ArgumentNullException(nameof(window));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        window.Changed += Refresh;
        Refresh();
    }

    /// <summary>Recomputes text, tooltip and visibility from the window's current lists and the settings.</summary>
    public void Refresh()
    {
        if (disposed)
        {
            return;
        }

        var count = window.StartableCount;
        if (!settings.ShowDtrEntry || (count == 0 && !settings.DtrShowWhenEmpty))
        {
            if (entry is not null)
            {
                entry.Shown = false;
            }

            return;
        }

        try
        {
            entry ??= Acquire();
            entry.Text = string.Format(CultureInfo.CurrentCulture, Strings.DiscoveryDtrTextFormat, count);
            entry.Tooltip = BuildTooltip(count);
            entry.Shown = true;
        }
        catch (Exception ex)
        {
            // Warn once when the bar keeps refusing the entry; later failures go to the debug log.
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "Server info bar entry could not be updated; further failures are logged at debug level");
            }
            else
            {
                log.Debug(ex, "Server info bar entry could not be updated");
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        window.Changed -= Refresh;
        if (entry is null)
        {
            return;
        }

        try
        {
            entry.OnClick = null;
            entry.Remove();
            bar.Remove(Title);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Server info bar entry could not be removed");
        }

        entry = null;
    }

    private IDtrBarEntry Acquire()
    {
        var acquired = bar.Get(Title, SeString.Empty);
        acquired.OnClick = OnClick;
        return acquired;
    }

    private void OnClick(DtrInteractionEvent interaction) => window.Toggle();

    private SeString BuildTooltip(int count)
    {
        tooltip.Clear();
        var zone = window.ZoneLabel;
        if (count == 0)
        {
            tooltip.AppendFormat(CultureInfo.CurrentCulture, Strings.DiscoveryDtrTooltipEmptyFormat, zone);
        }
        else if (count == 1)
        {
            tooltip.AppendFormat(CultureInfo.CurrentCulture, Strings.DiscoveryDtrTooltipHeaderOneFormat, zone);
        }
        else
        {
            tooltip.AppendFormat(CultureInfo.CurrentCulture, Strings.DiscoveryDtrTooltipHeaderFormat, count, zone);
        }

        var names = window.StartableNames;
        for (var i = 0; i < names.Count && i < MaxTooltipQuests; i++)
        {
            tooltip.Append('\n').Append(names[i]);
        }

        if (names.Count > MaxTooltipQuests)
        {
            tooltip.Append('\n').AppendFormat(CultureInfo.CurrentCulture, Strings.AndMoreFormat, names.Count - MaxTooltipQuests);
        }

        tooltip.Append('\n').Append(Strings.DiscoveryDtrTooltipClick);
        return tooltip.ToString();
    }
}
