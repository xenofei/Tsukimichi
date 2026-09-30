using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Export;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Config;

/// <summary>
/// Plugin settings persisted by Dalamud (Newtonsoft under the hood). Window size and position are left to ImGui.
/// Load with <see cref="Load"/>, persist with <see cref="Save"/>.
/// </summary>
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public const int CurrentVersion = 1;
    public const double MinPollIntervalSeconds = 0.5;
    public const double MaxPollIntervalSeconds = 5.0;
    public const int DefaultStalledDays = QueryContext.DefaultStalledDays;
    public const int MinStalledDays = 1;
    public const int MaxStalledDays = 90;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>How often the poller reads game state, clamped to 0.5–5 s when read.</summary>
    public double PollIntervalSeconds { get; set; } = 1.0;

    /// <summary>Print a chat line when a pinned or feature quest becomes available (see <c>Game.ChatNotifier</c>). Off by default.</summary>
    public bool ChatNoticeNewlyAvailable { get; set; }

    /// <summary>Whether main scenario quests are included in those notices.</summary>
    public bool IncludeMsqInNotices { get; set; }

    /// <summary>Print a chat line when a level-up opens the next job or role quest (see <c>Game.ChatNotifier</c>). On by default.</summary>
    public bool JobQuestNudge { get; set; } = true;

    // ---- 0.7.0: abandoned ledger (P10) ----
    /// <summary>Print "Abandoned: [quest] (step 3 of 5)" when a quest leaves the journal uncompleted (see <c>Game.ChatNotifier</c>). On by default.</summary>
    public bool ChatNoticeAbandoned { get; set; } = true;

    // ---- 0.7.0: export (P12) ----
    /// <summary>Format of Settings › Data › Export and of <c>/tsuki export</c> without a format word. JSON by default.</summary>
    public ExportFormat ExportFormat { get; set; } = ExportFormat.Json;

    /// <summary>Put the character's name in the export's header and file name. Off by default: an export carries no identifier.</summary>
    public bool ExportIncludeCharacterName { get; set; }

    /// <summary>The quest export lists every quest with its completed flag instead of the completed ones only. Off by default.</summary>
    public bool ExportIncludeIncomplete { get; set; }

    /// <summary>Folder exports are written to; empty uses <c>exports</c> in the plugin's config directory.</summary>
    public string ExportFolder { get; set; } = string.Empty;

    /// <summary>Show the "Removed from the game" tree node (retired quests and those with no journal genre). Off by default.</summary>
    public bool ShowUnlisted { get; set; }

    // ---- 0.6.1: journal refiling ----
    /// <summary>
    /// Whether the catalog files the sheet's genre-less quests by the refiling rules (<see cref="JournalFiling.Refiled"/>,
    /// the default) or leaves them in the removed bucket as releases before 0.6.1 did (<see cref="JournalFiling.Legacy"/>,
    /// the in-field rollback). A change rebuilds the catalog.
    /// </summary>
    public JournalFiling JournalFiling { get; set; } = JournalFiling.Refiled;

    /// <summary>Register every quest and Moonlit reward with Wotsit when it is loaded (see <c>Game.WotsitIpc</c>). On by default.</summary>
    public bool WotsitIntegration { get; set; } = true;

    // ---- 0.5.0: todo overlay ----
    /// <summary>Show the small always-on todo overlay (pins, nearby feature quests, MSQ, job quests).</summary>
    public bool TodoOverlayEnabled { get; set; } = false;
    /// <summary>When true the overlay cannot be moved or resized and ignores clicks on its frame.</summary>
    public bool TodoOverlayLocked { get; set; } = false;
    /// <summary>Background opacity of the overlay, 0.2–1.0.</summary>
    public float TodoOverlayOpacity { get; set; } = 0.85f;
    public bool TodoShowPins { get; set; } = true;
    public bool TodoShowNearbyFeature { get; set; } = true;
    public bool TodoShowMsq { get; set; } = true;
    public bool TodoShowJobQuests { get; set; } = true;

    // ---- 0.5.0: item hints ----
    /// <summary>Show a small hint near the cursor when hovering an item that is a quest-exclusive reward.</summary>
    public bool ItemHintsEnabled { get; set; } = true;
    /// <summary>Add a "Tsukimichi: quest reward" entry to item context menus.</summary>
    public bool ItemContextMenuEnabled { get; set; } = true;

    // ---- 0.6.2: NPC context menu (P2) ----
    /// <summary>Add a "Tsukimichi: quests here (N)" entry to the target bar's menu on a quest-giving NPC.</summary>
    public bool NpcContextMenuEnabled { get; set; } = true;

    /// <summary>Days an accepted quest sits untouched before the Stalled preset lists it; 1–90, default 7. Clamped by <see cref="StalledDaysClamped"/> when read.</summary>
    public int StalledDays { get; set; } = DefaultStalledDays;

    /// <summary><see cref="StalledDays"/> within the allowed bounds.</summary>
    public int StalledDaysClamped => Math.Clamp(StalledDays, MinStalledDays, MaxStalledDays);

    // ---- 0.7.0: spoiler shield (T19) ----
    /// <summary>Print main scenario quests further ahead than <see cref="SpoilerRevealAhead"/> as "Main scenario quest (Lv 83)". On by default.</summary>
    public bool SpoilerHideMsqNames { get; set; } = true;

    /// <summary>Main scenario quests past the character's position whose names stay visible; 0–10, default 3.</summary>
    public int SpoilerRevealAhead { get; set; } = SpoilerOptions.DefaultAhead;

    /// <summary>Show journal artwork only for quests in the journal or completed. On by default.</summary>
    public bool SpoilerHideArtwork { get; set; } = true;

    /// <summary>
    /// Per-character override of the shield, by content id: true shields that character whatever the settings above
    /// say, false shows it everything; a character without an entry follows the settings. Dropped with the character.
    /// </summary>
    public Dictionary<ulong, bool> SpoilerShieldByCharacter { get; set; } = [];

    /// <summary>The shield for a character (null in browse mode): the settings above, with its override applied.</summary>
    public SpoilerOptions SpoilerOptionsFor(ulong? contentId)
    {
        var ahead = Math.Clamp(SpoilerRevealAhead, 0, SpoilerOptions.MaxAhead);
        if (contentId is { } id && SpoilerShieldByCharacter.TryGetValue(id, out var shielded))
        {
            return shielded ? new SpoilerOptions(true, ahead, true) : SpoilerOptions.Off with { Ahead = ahead };
        }

        return new SpoilerOptions(SpoilerHideMsqNames, ahead, SpoilerHideArtwork);
    }

    /// <summary>Last table filters, restored on load.</summary>
    public FilterSet Filters { get; set; } = new();

    /// <summary>Last table sort column, restored on load; <see cref="SortColumn.Journal"/> is journal order.</summary>
    public SortColumn SortColumn { get; set; } = SortColumn.Journal;

    public bool SortDescending { get; set; }

    /// <summary>Pinned quests sort to the top of the table whatever the sort column.</summary>
    public bool PinnedFirst { get; set; } = true;

    /// <summary>Character the user chose to view explicitly; null follows the live character.</summary>
    public ulong? ViewedContentId { get; set; }

    /// <summary>Open the help window by itself the first time the main window opens; cleared once that happened.</summary>
    public bool ShowHelpOnFirstRun { get; set; } = true;

    /// <summary>Set when the interactive tutorial was finished or declined; while false the welcome card offers the tour the first time the main window opens.</summary>
    public bool TutorialCompleted { get; set; }

    /// <summary>
    /// Text and layout scale of the main window on top of Dalamud's global scale; 0.9–1.6, default 1.15. Clamped by
    /// <see cref="ScaleMetrics.ClampUiScale"/> when read.
    /// </summary>
    public float UiScale { get; set; } = ScaleMetrics.DefaultUiScale;

    /// <summary>
    /// Size of moons, reward icons, banners and toolbar buttons relative to the scaled text; 0.8–2.0, default 1.25.
    /// Clamped by <see cref="ScaleMetrics.ClampIconScale"/> when read.
    /// </summary>
    public float IconScale { get; set; } = ScaleMetrics.DefaultIconScale;

    // ---- 0.6.0: what's new ----
    /// <summary>
    /// The plugin version whose "What's new" card was seen (or recorded silently on a fresh install); empty until the
    /// main window first opens. When it differs from the running version the card shows once (see <c>Ui.WhatsNewCard</c>).
    /// </summary>
    public string LastSeenVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether a configuration file existed before this load; set by <see cref="Load(IDalamudPluginInterface, IPluginLog?)"/>,
    /// never persisted. With <see cref="LastSeenVersion"/> empty it tells an update from a build that predates the
    /// card (every release before 0.6.0) apart from a fresh install (<c>Core.Ui.WhatsNew.Decide</c>).
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool HasPriorConfig { get; private set; }

    // ---- 0.6.0: Moonlit store re-sells ----
    /// <summary>
    /// Moonlit toolbar "Hide store re-sells": drop rewards the FFXIV Online Store also sells (entry OtherSources carries
    /// OnlineStore) from the rows and from the obtained/total counts. Off by default.
    /// </summary>
    public bool MoonlitHideStoreResells { get; set; }

    // ---- 0.5.1: motion ----
    /// <summary>
    /// Replace the hold-to-confirm arc with a text countdown (and, later, other animation with a cut). Until the user
    /// sets it in Settings (<see cref="ReduceMotionChosen"/>), every load follows Windows' "Show animations" setting
    /// (<see cref="OsMotion"/>), so it is on by default for players who turned animations off system-wide.
    /// </summary>
    public bool ReduceMotion { get; set; }

    // ---- 0.7.0: motion default and density ----
    /// <summary>
    /// Whether the user set <see cref="ReduceMotion"/> in Settings. Every config saved since 0.5.1 carries
    /// <c>ReduceMotion: false</c> whether chosen or not, so the choice is recorded separately; false means "follow the OS".
    /// </summary>
    public bool ReduceMotionChosen { get; set; }

    /// <summary>Quest table row height: Comfortable (32 px, default) or Dense (24 px). Only the table's rows change.</summary>
    public RowDensity Density { get; set; } = RowDensity.Comfortable;

    /// <summary>Poll interval as a <see cref="TimeSpan"/> within the allowed bounds.</summary>
    public TimeSpan PollInterval
    {
        get
        {
            var seconds = double.IsFinite(PollIntervalSeconds)
                ? Math.Clamp(PollIntervalSeconds, MinPollIntervalSeconds, MaxPollIntervalSeconds)
                : 1.0;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    /// <summary>
    /// Reads the saved configuration or returns defaults when there is none, it is of another type, or reading it
    /// throws (a corrupt file must not stop the plugin from loading). Before defaults replace an unreadable file it
    /// is copied aside (see <see cref="ConfigRecovery"/>) and the failure is logged once when a log is given.
    /// </summary>
    public static Configuration Load(IDalamudPluginInterface pluginInterface) => Load(pluginInterface, null);

    /// <inheritdoc cref="Load(IDalamudPluginInterface)"/>
    public static Configuration Load(IDalamudPluginInterface pluginInterface, IPluginLog? log)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        // Read before the load: a file that exists but cannot be read still counts as a prior configuration.
        var hadFile = pluginInterface.ConfigFile.Exists;
        Configuration config;
        try
        {
            config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        }
        catch (Exception ex)
        {
            // The next Save overwrites the file, so the unreadable one is kept beside it first.
            var path = pluginInterface.ConfigFile.FullName;
            if (ConfigRecovery.TryCopyAside(path, out var copiedTo, out var copyError) && copiedTo is not null)
            {
                log?.Warning(ex, "Could not read the saved configuration; a copy was kept at {Path} and defaults are in use", copiedTo);
            }
            else if (copyError is not null)
            {
                log?.Warning(ex, "Could not read the saved configuration and could not copy it aside ({Error}); using defaults", copyError);
            }
            else
            {
                log?.Warning(ex, "Could not read the saved configuration; using defaults");
            }

            config = new Configuration();
        }

        config.Filters ??= new FilterSet();
        config.LastSeenVersion ??= string.Empty;
        config.SpoilerShieldByCharacter ??= [];
        config.ExportFolder ??= string.Empty;
        if (!Enum.IsDefined(config.ExportFormat))
        {
            config.ExportFormat = ExportFormat.Json;
        }

        if (!Enum.IsDefined(config.JournalFiling))
        {
            // A hand-edited integer, or a value a newer build wrote before a downgrade: the mapper would read it as
            // Legacy while neither radio button showed selected. The default filing stands.
            log?.Warning("Saved JournalFiling {Value} is not a known mode; using {Default}", (int)config.JournalFiling, JournalFiling.Refiled);
            config.JournalFiling = JournalFiling.Refiled;
        }

        if (!Enum.IsDefined(config.Density))
        {
            config.Density = RowDensity.Comfortable;
        }

        // Read once per load: while the user has not chosen, Reduce motion mirrors the OS animation setting.
        if (!config.ReduceMotionChosen && OsMotion.AnimationsOff() is { } animationsOff)
        {
            config.ReduceMotion = animationsOff;
        }

        config.HasPriorConfig = hadFile;
        return config;
    }

    public void Save(IDalamudPluginInterface pluginInterface)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        Version = CurrentVersion;
        pluginInterface.SavePluginConfig(this);
    }
}
