using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings (spec §7; rebuilt in 1.13.0, feature plan v6 U7): nine pages with an index and a search box
/// (<c>ConfigWindow.Layout.cs</c>, which also holds the one row design and says how to add a block). General (size,
/// text size, look, motion, the main window, the tour), Journal, Overlay &amp; routes, Alerts, Spoilers, In game,
/// Automation, Characters &amp; data and Advanced, each page in its own partial file. Every change applies on the
/// frame it is made, the window scale and the text size included; a toggle or a choice is saved at once, a slider once
/// it has been still a moment (<see cref="SaveDebounce"/>), and anything waiting is saved when the window closes.
/// </summary>
public sealed partial class ConfigWindow : Window, IDisposable
{
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(8);

    private readonly Configuration settings;
    private readonly SessionState session;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Action<bool> onShowUnlistedChanged;

    private readonly LocText pluginVersionLine;
    private readonly string dataStampLine;
    private readonly DiagnosticBuilder diagnostics;
    private readonly LocText curatedLine;

    /// <param name="diagnostics">Owns the data stamp and the game-version warning Advanced › Diagnostics shows.</param>
    public ConfigWindow(Configuration settings, SessionState session, IDalamudPluginInterface pluginInterface, DiagnosticBuilder diagnostics, Action<bool> onShowUnlistedChanged)
        : base(Strings.ConfigWindowTitle)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        ArgumentNullException.ThrowIfNull(diagnostics);
        this.onShowUnlistedChanged = onShowUnlistedChanged ?? throw new ArgumentNullException(nameof(onShowUnlistedChanged));

        // Room at first use for the index beside a page whose rows keep their control column and three Decoration
        // previews side by side.
        Size = DefaultSizeLogical;
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(MinWidthLogical, MinHeightLogical) };
        blocks = BuildBlocks();

        // The page open last, by name; the names of the sections before 1.13 open the page their settings moved to.
        // None yet means Settings was last laid out before the rebuild: it takes the new size once.
        section = SettingsSections.Parse(settings.SettingsPage);
        widenOnce = string.IsNullOrEmpty(settings.SettingsPage);

        var pluginVersion = diagnostics.PluginVersion.Length > 0 ? diagnostics.PluginVersion : "unknown";
        pluginVersionLine = new LocText(() => string.Format(CultureInfo.CurrentCulture, Strings.ConfigPluginVersionFormat, pluginVersion));
        dataStampLine = diagnostics.DataStampLine;
        this.diagnostics = diagnostics;

        var curated = session.Curated;
        curatedLine = new LocText(() => string.Format(
            CultureInfo.CurrentCulture,
            Strings.ConfigCuratedFormat,
            curated.SystemUnlocks.Count,
            curated.DutyUnlocks.Count,
            curated.FeatureQuests.Count,
            curated.Festivals.Count));
    }

    /// <summary>
    /// The UI language service (V2-19). The language picker is hidden while localization is frozen (feature plan v6
    /// decision 7): Tsukimichi follows the saved choice, Dalamud's language by default. Null in no build.
    /// </summary>
    public LocService? Language { get; set; }

    /// <summary>Runs an action on the next framework tick, outside the ImGui frame; set by the plugin. Null runs it at once.</summary>
    public Action<Action>? RunNextTick { get; set; }

    /// <summary>Opens the help window; set by the plugin once the help window exists. Null hides the button.</summary>
    public Action? ShowHelp { get; set; }

    /// <summary>Starts the interactive tutorial; set by the plugin once the overlay exists. Null hides the button.</summary>
    public Action? StartTutorial { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.WotsitIntegration"/> is toggled and saved; the plugin points it at the Wotsit IPC.</summary>
    public Action<bool>? WotsitToggled { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.JournalFiling"/> changes and is saved; the plugin rebuilds the catalog.</summary>
    public Action<JournalFiling>? JournalFilingChanged { get; set; }

    /// <summary>Rebuilds the catalog after a failed build (Diagnostics' Retry); the plugin wires it. Null hides the button.</summary>
    public Action? RetryCatalog { get; set; }

    /// <summary>Moves the todo overlay back to its default place; set by the plugin once the overlay exists. Null hides the button.</summary>
    public Action? ResetTodoPosition { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.ItemHintsEnabled"/> is toggled and saved; the item-hint feature wires it.</summary>
    public Action<bool>? ItemHintsToggled { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.ItemContextMenuEnabled"/> is toggled and saved; the item-hint feature wires it.</summary>
    public Action<bool>? ItemContextMenuToggled { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.NpcContextMenuEnabled"/> is toggled and saved; the NPC menu hook wires it.</summary>
    public Action<bool>? NpcContextMenuToggled { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.DutyFinderHintEnabled"/> is toggled and saved; the Duty Finder hint wires it.</summary>
    public Action<bool>? DutyFinderHintToggled { get; set; }

    /// <summary>
    /// The shared addon kill switch (T20) behind In game's paused notice and Advanced's "Run game panels on an untested
    /// patch"; set by the plugin. Null hides both.
    /// </summary>
    public HookGate? HookGate { get; set; }

    /// <summary>Settings › Characters &amp; data › Export (P12); set by the plugin once the export service exists. Null hides it.</summary>
    public ExportSection? Export { get; set; }

    /// <summary>The user's Moonlit verdicts for Characters &amp; data; set by the plugin once the Moonlit pane exists. Null shows a placeholder.</summary>
    public IUniqueOverrides? Overrides { get; set; }

    public override void OnOpen()
    {
        ReadSettings();
        ClearSearch();
    }

    public override void OnClose()
    {
        ApplyAliasDraft();
        if (pendingSave.Flush())
        {
            Save();
        }
    }

    /// <summary>Resets the per-open drafts (on construction and each time the window opens).</summary>
    private void ReadSettings()
    {
        aliasDraft = null;
        uiScaleHeld = false;
    }

    /// <summary>The last item's hint as a Night tooltip while it is hovered.</summary>
    private static void HintOnHover(string hint)
    {
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(hint);
        }
    }

    private void Save() => settings.Save(pluginInterface);
}
