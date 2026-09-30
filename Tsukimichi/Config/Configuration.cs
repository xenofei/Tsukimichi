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

    /// <summary>
    /// Offer "Add to Questionable priority" in the detail pane's "…" menu when Questionable is loaded (V2-17). Off by
    /// default: the hand-off is opt-in, and it only ever calls Questionable's own gate.
    /// </summary>
    public bool QuestionableHandoff { get; set; }

    // ---- 0.5.0: todo overlay ----
    /// <summary>Show the small always-on todo overlay (pins, nearby feature quests, MSQ, job quests).</summary>
    public bool TodoOverlayEnabled { get; set; } = false;
    /// <summary>
    /// Locked = click-through (0.8.0): the overlay cannot be moved or resized and takes no mouse or keyboard input at
    /// all, so clicks go to the game behind it. Unlocked again from Settings (its own menu is unreachable while locked).
    /// </summary>
    public bool TodoOverlayLocked { get; set; } = false;

    /// <summary>
    /// 0.8.0 upgrade notice: set on the first load of a configuration an earlier release saved with the overlay Locked,
    /// when Locked still meant "cannot be moved" and the rows took clicks. While it is set and the overlay is locked,
    /// Settings › Todo overlay says the overlay is now click-through and how to unlock it; unlocking clears it.
    /// </summary>
    public bool TodoLockNoticeDue { get; set; }

    /// <summary>Whether the one chat line about <see cref="TodoLockNoticeDue"/> was printed (once, at a login).</summary>
    public bool TodoLockNoticePrinted { get; set; }
    /// <summary>Background opacity of the overlay, 0.6–1.0 (clamped when read).</summary>
    public float TodoOverlayOpacity { get; set; } = 0.85f;
    public bool TodoShowPins { get; set; } = true;
    public bool TodoShowNearbyFeature { get; set; } = true;
    public bool TodoShowMsq { get; set; } = true;
    public bool TodoShowJobQuests { get; set; } = true;

    // ---- 0.8.0: seasonal now (P11) ----
    /// <summary>The todo overlay's "Event quests running now" section. On by default.</summary>
    public bool TodoShowSeasonal { get; set; } = true;

    /// <summary>Print "Moonfire Faire is running: 2 quests ready (ends Aug 28)" once per login (see <c>Game.ChatNotifier</c>). On by default.</summary>
    public bool ChatNoticeSeasonal { get; set; } = true;

    // ---- 0.9.0: Clear my blues (P3) ----
    /// <summary>The todo overlay's "Clear my blues" section: the pinned expansion's Ready unlock quests. On by default; empty until an expansion is pinned.</summary>
    public bool TodoShowPlan { get; set; } = true;

    /// <summary>ExVersion row id of the expansion pinned from the My blues tab ("Pin to overlay"); -1 when none.</summary>
    public int TodoPlanExpansion { get; set; } = -1;

    // ---- 0.5.0: item hints ----
    /// <summary>Show a small hint near the cursor when hovering an item that is a quest-exclusive reward.</summary>
    public bool ItemHintsEnabled { get; set; } = true;
    /// <summary>Add a "Tsukimichi: quest reward" entry to item context menus.</summary>
    public bool ItemContextMenuEnabled { get; set; } = true;

    // ---- 0.6.2: NPC context menu (P2) ----
    /// <summary>Add a "Tsukimichi: quests here (N)" entry to the target bar's menu on a quest-giving NPC.</summary>
    public bool NpcContextMenuEnabled { get; set; } = true;

    // ---- 0.9.0: Duty Finder unlock hint (P13) ----
    /// <summary>Show the quest that unlocks a padlocked duty beside the Duty Finder when that duty is selected.</summary>
    public bool DutyFinderHintEnabled { get; set; } = true;

    // ---- 0.8.0: addon kill switch (T20) ----
    /// <summary>
    /// The game version (ffxivgame.ver text) on which the player let the game hooks (item tooltip panel, item and NPC
    /// menu entries, server info bar entry) run although it is newer than the one they were tested on
    /// (<c>Core.Runtime.HookGate</c>); empty when off, the default. It applies to that version only, so the next patch
    /// pauses the hooks again.
    /// </summary>
    public string EnableHooksOnUntestedVersion { get; set; } = string.Empty;

    /// <summary>
    /// Migration of a pre-release boolean form of the override (development builds of 0.8.0 wrote
    /// <c>EnableHooksOnUntestedVersions: true</c>). No release shipped it, so its value is read and dropped: the
    /// version-scoped <see cref="EnableHooksOnUntestedVersion"/> stays empty. Write-only, so it is never saved again.
    /// </summary>
    [Newtonsoft.Json.JsonProperty]
    [Obsolete("Replaced by EnableHooksOnUntestedVersion; read only to drop the old value.")]
    public bool EnableHooksOnUntestedVersions
    {
        set { }
    }

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
    /// How many times the player answered the first-run offer with "Later" (0.8.0): the offer comes back next session
    /// until this reaches <c>Ui.TutorialOverlay.LaterLimit</c>. "Don't offer again" sets <see cref="TutorialCompleted"/>
    /// instead; turning the offer back on in Settings resets both.
    /// </summary>
    public int TutorialLaterCount { get; set; }

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
    /// Moonlit toolbar "Hide rewards found elsewhere" (0.6.0's "Hide store re-sells"; the property keeps its name so saved
    /// settings survive): drop rewards the FFXIV Online Store also sells (entry OtherSources carries OnlineStore) or a
    /// duty also drops (DungeonDrop, from 0.7.0) from the rows and from the obtained/total counts. Off by default.
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

    /// <summary>
    /// Newtonsoft's ShouldSerialize convention: <see cref="ReduceMotion"/> is written only once the user chose it.
    /// A value that merely mirrors the OS stays out of the file, so a saved <c>ReduceMotion: true</c> without
    /// <see cref="ReduceMotionChosen"/> can only come from a build before 0.7.0 and is read as the user's choice.
    /// </summary>
    public bool ShouldSerializeReduceMotion() => ReduceMotionChosen;

    /// <summary>Quest table row height: Comfortable (32 px, default) or Dense (24 px). Only the table's rows change.</summary>
    public RowDensity Density { get; set; } = RowDensity.Comfortable;

    // ---- 0.8.0: chrome (T13) ----
    /// <summary>
    /// Draw the windows in the user's Dalamud colours instead of the Night palette: the same layout, with the surface
    /// and text roles mapped from the Dalamud style (<c>Ui.Theme.Refresh</c>). Gold, Eclipse and the moons keep their
    /// colours. Off by default.
    /// </summary>
    public bool FollowDalamudColours { get; set; }

    /// <summary>The todo overlay's Compact mode: moon and name only, one line per row, no hints. Off by default.</summary>
    public bool TodoOverlayCompact { get; set; }

    // ---- 0.8.0: keyboard (T17, accessibility A7) ----
    // The game sees every key the plugin reads (only a text field swallows them), so beyond Ctrl+F and Esc every
    // shortcut is opt-in: Ctrl+1..4 are hotbar 2 in the default keybinds and single letters are often bound.

    /// <summary>Ctrl+1..4 switch the main window's tabs while it has focus. Off by default.</summary>
    public bool ShortcutTabs { get; set; }

    /// <summary>F flags the selected quest's giver on the map while the main window has focus. Off by default.</summary>
    public bool ShortcutFlag { get; set; }

    /// <summary>Enter shows the selected quest in the Journal while the main window has focus. Off by default.</summary>
    public bool ShortcutReveal { get; set; }

    /// <summary>P pins or unpins the selected quest while the main window has focus. Off by default.</summary>
    public bool ShortcutPin { get; set; }

    // ---- 1.0.0: "Before you continue" payoff gates (P5) ----
    /// <summary>
    /// Show "Before you continue" notes (Settings › Spoilers): the lines under the MSQ line on the Characters
    /// dashboard and in the Tonight card. Off hides them and suppresses the chat notice too (nothing is marked as
    /// announced while off). On by default.
    /// </summary>
    public bool ShowPayoffGates { get; set; } = true;

    /// <summary>
    /// Print "Before you continue: Finish the Eden raid series first." once per gate per character, the first time the
    /// gate's milestone quest is Ready or in the journal (see <c>Game.ChatNotifier</c>). On by default.
    /// </summary>
    public bool ChatNoticePayoffGates { get; set; } = true;

    /// <summary>Gate ids already announced in chat, by character content id; a gate is announced once per character, ever. Dropped with the character.</summary>
    public Dictionary<ulong, HashSet<string>> PayoffGatesNoticedByCharacter { get; set; } = [];

    /// <summary>Gate ids whose "why? (spoiler)" the player opened, by character content id; closed by default. Dropped with the character.</summary>
    public Dictionary<ulong, HashSet<string>> PayoffWhyOpenByCharacter { get; set; } = [];

    // ---- 1.1: "Since you were away" (P7) ----
    /// <summary>
    /// Settings › Notices "Show 'Since you were away' after N days": the card opens at a login when every stored
    /// character's newest capture is at least this many days old (see <c>Core.Return.WelcomeBackTrigger</c>). 0 turns
    /// it off; clamped to 0–180 on load. Per-character "Don't show again" lives in the character's sidecar.
    /// </summary>
    public int WelcomeBackDays { get; set; } = Core.Return.WelcomeBackTrigger.DefaultDays;

    /// <summary>Drops a forgotten character's payoff gate state; true when there was any.</summary>
    public bool ForgetPayoffGates(ulong contentId) =>
        PayoffGatesNoticedByCharacter.Remove(contentId) | PayoffWhyOpenByCharacter.Remove(contentId);

    /// <summary>Drops every character's payoff gate state ("Delete all data"); true when there was any.</summary>
    public bool ClearPayoffGates()
    {
        var any = PayoffGatesNoticedByCharacter.Count > 0 || PayoffWhyOpenByCharacter.Count > 0;
        PayoffGatesNoticedByCharacter.Clear();
        PayoffWhyOpenByCharacter.Clear();
        return any;
    }

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
        config.EnableHooksOnUntestedVersion = config.EnableHooksOnUntestedVersion?.Trim() ?? string.Empty;
        config.SpoilerShieldByCharacter ??= [];
        config.PayoffGatesNoticedByCharacter ??= [];
        config.PayoffWhyOpenByCharacter ??= [];
        config.ExportFolder ??= string.Empty;
        config.WelcomeBackDays = Math.Clamp(config.WelcomeBackDays, 0, Core.Return.WelcomeBackTrigger.MaxDays);
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

        // Before 0.7.0 ReduceMotion defaulted to false and had no Chosen flag, so a saved true was the user's choice;
        // the OS default must not overwrite it. (A value that only mirrored the OS is never saved: see
        // ShouldSerializeReduceMotion.)
        if (config.ReduceMotion && !config.ReduceMotionChosen)
        {
            config.ReduceMotionChosen = true;
        }

        // Read once per load: while the user has not chosen, Reduce motion mirrors the OS animation setting.
        if (!config.ReduceMotionChosen && OsMotion.AnimationsOff() is { } animationsOff)
        {
            config.ReduceMotion = animationsOff;
        }

        // Locked meant "no move" before 0.8.0 and means click-through now: a player upgrading with it on is told once.
        if (hadFile && config.TodoOverlayLocked && !config.TodoLockNoticeDue && !config.TodoLockNoticePrinted && SavedBeforeClickThroughLock(config.LastSeenVersion))
        {
            config.TodoLockNoticeDue = true;
        }

        config.HasPriorConfig = hadFile;
        return config;
    }

    /// <summary>
    /// Whether a configuration last seen by <paramref name="lastSeenVersion"/> comes from before 0.8.0 (where Locked became
    /// click-through): an older version, or none at all (every release before 0.6.0). An unreadable version says no.
    /// </summary>
    internal static bool SavedBeforeClickThroughLock(string? lastSeenVersion)
    {
        if (string.IsNullOrWhiteSpace(lastSeenVersion))
        {
            return true;
        }

        return System.Version.TryParse(ChangelogSection.NormalizeVersion(lastSeenVersion), out var seen) && seen < ClickThroughLockVersion;
    }

    private static readonly System.Version ClickThroughLockVersion = new(0, 8, 0);

    public void Save(IDalamudPluginInterface pluginInterface)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        Version = CurrentVersion;
        pluginInterface.SavePluginConfig(this);
    }
}
