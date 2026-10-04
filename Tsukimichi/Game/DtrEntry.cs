using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Discovery;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Ui;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// The one server info bar entry (plan v8 M1; spec-1.22 M1, decisions 13, 14 and 20): "◐ 12 Ready", the quests Ready on
/// the current job, or with "The entry counts" set to Quests in this zone "◐ 3 here", 1.x's Nearby count. A click opens
/// Tsukimichi (Quests in this zone: Nearby, as before); a right-click opens Tonight. Its tooltip is text lines, because
/// Dalamud gives an entry a text tooltip (<c>IDtrBarEntry.Tooltip</c>) and Umbra draws that in its own style: the title in
/// the game's gold, Up next and its step, both counts, the journal, the events ending soon, and what a click does.
/// <para>
/// <b>The moon.</b> "◐" in the UIColor row nearest the theme's moon (<see cref="Theme.Accent"/>) when the game's UI font
/// has it; the game's font has no "◐" (<see cref="ServerInfoBar"/>), so the game's own icon
/// (<see cref="ServerInfoBar.FallbackIcon"/>, owner decision 8) stands in. The font is checked once, from the game's own
/// <c>common/font/AXIS_12.fdt</c>.
/// </para>
/// <para>
/// <b>When it shows.</b> As the player set it; until they do, on while Umbra is installed and Tsukimichi for Umbra is not
/// (<see cref="ServerInfoBar.Shown"/>). Hidden at zero unless Show at zero, and while the shared <see cref="HookGate"/>
/// pauses game hooks. The entry is acquired lazily on first show and removed on dispose. Its words are rebuilt only when
/// what they say changed (<see cref="Tick"/> compares a key each frame), so nothing is built per frame.
/// </para>
/// </summary>
public sealed class DtrEntry : IDisposable
{
    public const string Title = Strings.DiscoveryDtrTitle;

    private const string FontFile = "common/font/AXIS_12.fdt";

    private readonly IDtrBar bar;
    private readonly IDataManager data;
    private readonly DiscoveryWindow window;
    private readonly DiscoverySettings settings;
    private readonly HookGate gate;
    private readonly IPluginLog log;
    private readonly SummarySource summary;
    private readonly UmbraProbe umbra;
    private readonly Action openMain;
    private readonly Action openTonight;
    private IDtrBarEntry? entry;
    private bool disposed;
    private bool warned;
    private bool? fontHasMoon;
    private ushort? moonColor;
    private uint moonColorFor = uint.MaxValue;
    private ushort? goldColor;
    private bool colorsRead;
    private (bool Hooks, bool Shown, DtrCounts Counts, bool AtZero, int Summary, int Here, uint Accent, int Language, bool Ready)? key;

    public DtrEntry(IDtrBar bar, IDataManager data, DiscoveryWindow window, DiscoverySettings settings, HookGate gate, SummarySource summary, UmbraProbe umbra, Action openMain, Action openTonight, IPluginLog log)
    {
        this.bar = bar ?? throw new ArgumentNullException(nameof(bar));
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.window = window ?? throw new ArgumentNullException(nameof(window));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.summary = summary ?? throw new ArgumentNullException(nameof(summary));
        this.umbra = umbra ?? throw new ArgumentNullException(nameof(umbra));
        this.openMain = openMain ?? throw new ArgumentNullException(nameof(openMain));
        this.openTonight = openTonight ?? throw new ArgumentNullException(nameof(openTonight));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Whether the entry shows now (Settings › In game describes it from this).</summary>
    public bool Shown => ServerInfoBar.Shown(settings.DtrEntryChosen, settings.ShowDtrEntry, umbra.Loaded, umbra.AddonPresent);

    /// <summary>The moon's form in use (Settings shows which); null before the font was checked.</summary>
    public DtrMoonKind? MoonKind => fontHasMoon is { } has ? ServerInfoBar.MoonKind(has) : null;

    /// <summary>Framework thread, once per frame: rebuilds the entry only when what it says changed.</summary>
    public void Tick()
    {
        if (disposed)
        {
            return;
        }

        var current = summary.Current;
        var next = (gate.HooksAllowed, Shown, settings.DtrCounts, settings.DtrShowWhenEmpty, summary.Revision, window.StartableCount, ColorMath.ToHex(Theme.Accent), Localization.Loc.Version, current.Ready);
        if (key == next)
        {
            return;
        }

        key = next;
        Refresh();
    }

    /// <summary>Recomputes text, tooltip and visibility now.</summary>
    public void Refresh()
    {
        if (disposed)
        {
            return;
        }

        var current = summary.Current;
        var here = window.StartableCount;
        var words = new DtrWords(Strings.ServerBarReadyFormat, Strings.ServerBarHereFormat, Strings.ServerBarNothingReady, Strings.ServerBarNothingHere);
        var text = gate.HooksAllowed && Shown ? ServerInfoBar.Text(settings.DtrCounts, current.ReadyCount, here, settings.DtrShowWhenEmpty, words) : null;
        if (text is null)
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
            entry.Text = BuildText(text);
            entry.Tooltip = BuildTooltip(current, here);
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

    /// <summary>Left: Tsukimichi (Quests in this zone: Nearby, as 1.x). Right: Tonight.</summary>
    private void OnClick(DtrInteractionEvent interaction)
    {
        if (interaction.ClickType == MouseClickType.Right)
        {
            openTonight();
        }
        else if (settings.DtrCounts == DtrCounts.Zone)
        {
            window.Toggle();
        }
        else
        {
            openMain();
        }
    }

    /// <summary>The moon (the coloured glyph, or the game's icon where the font lacks it), a space, then the words.</summary>
    private SeString BuildText(string words)
    {
        ReadGameData();
        var builder = new SeStringBuilder();
        if (fontHasMoon == true)
        {
            var accent = ColorMath.ToHex(Theme.Accent);
            if (accent != moonColorFor)
            {
                moonColorFor = accent;
                moonColor = NearestColor(accent);
            }

            if (moonColor is { } row)
            {
                builder.AddUiForeground(ServerInfoBar.Moon, row);
            }
            else
            {
                builder.AddText(ServerInfoBar.Moon);
            }
        }
        else
        {
            builder.AddIcon((BitmapFontIcon)ServerInfoBar.FallbackIcon);
        }

        builder.AddText(" " + words);
        return builder.Build();
    }

    /// <summary>The quick card's content as lines (spec-1.22 M1 "The hover is text lines").</summary>
    private SeString BuildTooltip(TsukimichiSummary current, int here)
    {
        var culture = CultureInfo.CurrentCulture;
        string? upNext = null;
        string? step = null;
        if (current.UpNextName.Length > 0)
        {
            upNext = string.Format(culture, Strings.ServerBarUpNextFormat, current.UpNextName);
            step = current.UpNextStep;
        }

        var ready = current.ReadyCount switch
        {
            0 => string.Format(culture, Strings.ServerBarTipNothingReadyFormat, current.Job),
            1 => string.Format(culture, Strings.ServerBarTipReadyOneFormat, current.Job),
            _ => string.Format(culture, Strings.ServerBarTipReadyFormat, current.ReadyCount, current.Job),
        };
        var hereWords = here switch
        {
            0 => Strings.ServerBarTipHereNone,
            _ => string.Format(culture, Strings.ServerBarTipHereFormat, here),
        };
        var counts = current.Ready ? ready + Strings.UpNextSeparator + hereWords : hereWords;

        string? journal = null;
        if (current.JournalUsed >= 0 && current.JournalCap > 0)
        {
            var slots = new JournalSlots(current.JournalUsed, current.JournalCap);
            journal = slots.Room == JournalRoom.Full
                ? string.Format(culture, Strings.ServerBarJournalFullFormat, slots.Used, slots.Cap)
                : slots.Left == 1
                    ? string.Format(culture, Strings.ServerBarJournalOneFormat, slots.Used, slots.Cap)
                    : string.Format(culture, Strings.ServerBarJournalFormat, slots.Used, slots.Cap, slots.Left);
        }

        var ending = new List<string>(current.EndingSoon.Count);
        foreach (var (name, days) in current.EndingSoon)
        {
            ending.Add(days switch
            {
                <= 0 => string.Format(culture, Strings.ServerBarEndsTodayFormat, name),
                1 => string.Format(culture, Strings.ServerBarEndsTomorrowFormat, name),
                _ => string.Format(culture, Strings.ServerBarEndsInFormat, name, days),
            });
        }

        var clicks = settings.DtrCounts == DtrCounts.Zone ? Strings.ServerBarClicksZone : Strings.ServerBarClicksReady;
        var lines = ServerInfoBar.TooltipLines(Strings.DiscoveryDtrTitle, upNext, step, counts, journal, ending, clicks);

        var builder = new SeStringBuilder();
        if (goldColor is { } gold)
        {
            builder.AddUiForeground(lines[0], gold);
        }
        else
        {
            builder.AddText(lines[0]);
        }

        for (var i = 1; i < lines.Count; i++)
        {
            builder.AddText("\n" + lines[i]);
        }

        return builder.Build();
    }

    /// <summary>Once: whether the game's UI font has "◐", and the UIColor row nearest the game gold for the title.</summary>
    private void ReadGameData()
    {
        if (fontHasMoon is null)
        {
            try
            {
                fontHasMoon = data.GetFile(FontFile)?.Data is { } fdt && ServerInfoBar.FontHas(fdt, ServerInfoBar.MoonCodePoint);
            }
            catch (Exception ex)
            {
                log.Debug(ex, "The game's UI font could not be read; the server info bar uses the game's icon");
                fontHasMoon = false;
            }
        }

        if (!colorsRead)
        {
            colorsRead = true;
            goldColor = NearestColor(GoldHex);
        }
    }

    /// <summary>The title's gold: the game's own quest-gold, as its tooltips draw a heading.</summary>
    private const uint GoldHex = 0xEEC86E;

    private List<(ushort Row, uint Rgba)>? uiColors;

    private ushort? NearestColor(uint rgb)
    {
        try
        {
            if (uiColors is null)
            {
                uiColors = [];
                foreach (var row in data.GetExcelSheet<Lumina.Excel.Sheets.UIColor>())
                {
                    if (row.RowId <= ushort.MaxValue)
                    {
                        uiColors.Add(((ushort)row.RowId, row.Dark));
                    }
                }
            }

            return ServerInfoBar.NearestUiColor(rgb, uiColors);
        }
        catch (Exception ex)
        {
            log.Debug(ex, "The UIColor sheet could not be read; the server info bar's colours stay the game's default");
            return null;
        }
    }
}
