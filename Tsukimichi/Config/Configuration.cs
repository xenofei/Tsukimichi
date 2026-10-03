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
public sealed partial class Configuration : IPluginConfiguration
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

    /// <summary>
    /// Copy for Discord (1.8.0) makes each quest name a masked link to its page (Lodestone, else Garland Tools); a name
    /// the spoiler shield masks never gets one. Off by default; the Copy for Discord buttons' right-click menu flips it.
    /// </summary>
    public bool DiscordCopyLinks { get; set; }

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

    // ---- 1.6.0: travel ----
    /// <summary>
    /// Show Walk to giver (vnavmesh) beside Teleport. On by default; without vnavmesh the button stays, greyed, and
    /// names it. The character only moves on a click.
    /// </summary>
    public bool ShowWalkToGiver { get; set; } = true;

    /// <summary>Show Go to giver (teleport, aethernet and walk in one click, with Stop). On by default.</summary>
    public bool ShowGoToGiver { get; set; } = true;

    // ---- 1.10: travel, getting there faster ----
    /// <summary>Walk and Go to giver mount before a walk longer than this many yalms (0 never mounts).</summary>
    public int TravelMountDistance { get; set; } = (int)Core.Travel.TravelOptions.DefaultMountDistance;

    /// <summary>The mount Walk and Go to giver summon (a Mount sheet row the character owns); 0 for Mount Roulette.</summary>
    public uint TravelMountId { get; set; }

    /// <summary>On a mount, fly to the giver where the zone's flying is unlocked, then land.</summary>
    public bool TravelFly { get; set; } = true;

    /// <summary>Sprint at the start of a walk where mounts are not allowed (towns).</summary>
    public bool TravelSprintInTowns { get; set; } = true;

    // The last TravelOptions() handed out; a private field, so the configuration file never holds it.
    private Core.Travel.TravelOptions? travelOptions;

    /// <summary>
    /// The travel settings as <see cref="Core.Travel.TravelOptions"/> for a new plan. Asked every frame while travel
    /// buttons show, so it hands out one instance, made again only when a travel setting changed.
    /// </summary>
    public Core.Travel.TravelOptions TravelOptions()
    {
        var mount = Math.Max(0, TravelMountDistance);
        if (travelOptions is not { } options || options.MountDistance != mount || options.Fly != TravelFly || options.SprintInTowns != TravelSprintInTowns)
        {
            options = travelOptions = new(mount, TravelFly, TravelSprintInTowns);
        }

        return options;
    }

    /// <summary>
    /// 1.6.0 (decision 1): "Run with AutoDuty" may queue a duty that has neither Duty Support nor Trust in the regular
    /// Duty Finder, with other players. Off by default: such a duty's button stays disabled and says why.
    /// </summary>
    public bool AutoDutyAllowDutyFinder { get; set; }

    // ---- 1.6.0: Questionable hand-offs (feature plan v5, decision 1) ----
    /// <summary>
    /// Offer "Add and start Questionable" beside Send to Questionable, and Stop while it runs. On by default (the owner
    /// allows full automation through other plugins); nothing starts unless the player presses the button, and
    /// <see cref="QuestionableConfirmStart"/> asks first.
    /// </summary>
    public bool QuestionableAllowStart { get; set; } = true;

    /// <summary>Ask before starting Questionable; cleared when the player ticks "Don't ask again" in the confirmation.</summary>
    public bool QuestionableConfirmStart { get; set; } = true;

    /// <summary>
    /// Ask before Stop while Questionable's "Run command after stop" is on (it then runs that command, /li auto by
    /// default, on any stop another plugin asks for); cleared when the player ticks "Don't ask again".
    /// </summary>
    public bool QuestionableConfirmStopCommand { get; set; } = true;

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

    // ---- 1.6.0: routes and Next stops (R6, C3) ----
    /// <summary>
    /// The route being followed ("Follow this route" in the route window): its target and the character it is for,
    /// never its steps, which are worked out from that character's states each time. Null when none is followed.
    /// </summary>
    public Core.Route.SavedRoute? ActiveRoute { get; set; }

    /// <summary>The todo overlay's route section (the followed route's next steps). On by default; empty until a route is followed.</summary>
    public bool TodoShowRoute { get; set; } = true;

    /// <summary>
    /// While a route is followed, move the map flag to the next stop's giver each time a step is turned in (the flag
    /// only, the map does not open). On by default.
    /// </summary>
    public bool RouteFlagAdvance { get; set; } = true;

    /// <summary>The todo overlay's "Next stops" section: Ready quests batched by aetheryte. Off by default.</summary>
    public bool TodoShowNextStops { get; set; }

    // ---- 0.5.0: item hints ----
    /// <summary>Show a small hint near the cursor when hovering an item that is a quest-exclusive reward.</summary>
    public bool ItemHintsEnabled { get; set; } = true;
    /// <summary>Add a "Tsukimichi: quest reward" entry to item context menus.</summary>
    public bool ItemContextMenuEnabled { get; set; } = true;

    // ---- 1.6.0: hand-in items ----
    /// <summary>
    /// With Allagan Tools loaded, count what the retainers hold in the detail pane's Hand in section, and read relic and
    /// special weapons as owned when it counts them anywhere (Moonlit). On by default; read-only either way.
    /// </summary>
    public bool HandInAllaganTools { get; set; } = true;

    /// <summary>The item hint and the item menu also name the open quests (in the journal or ready) that ask for the item. On by default.</summary>
    public bool ItemNeededForEnabled { get; set; } = true;

    // ---- 0.6.2: NPC context menu (P2) ----
    /// <summary>Add a "Tsukimichi: quests here (N)" entry to the target bar's menu on a quest-giving NPC.</summary>
    public bool NpcContextMenuEnabled { get; set; } = true;

    // ---- 0.9.0: Duty Finder unlock hint (P13) ----
    /// <summary>Show the quest that unlocks a padlocked duty beside the Duty Finder when that duty is selected.</summary>
    public bool DutyFinderHintEnabled { get; set; } = true;

    // ---- 1.7.0: panels beside game windows (R8 A, B, C) ----
    /// <summary>Show the "Worth it?" panel beside the game's quest-offer window.</summary>
    public bool QuestOfferPanelEnabled { get; set; } = true;

    /// <summary>Show the "What this opened" panel beside the game's quest-complete window.</summary>
    public bool QuestResultPanelEnabled { get; set; } = true;

    /// <summary>Show the companion panel beside the game's Journal.</summary>
    public bool JournalCompanionEnabled { get; set; } = true;

    // ---- 0.8.0: addon kill switch (T20) ----
    /// <summary>
    /// The game version (ffxivgame.ver text) on which the player let the game hooks (item tooltip panel, item and NPC
    /// menu entries, server info bar entry) run although its patch date is newer than the one they were tested on
    /// (<c>Core.Runtime.HookGate</c>); empty when off, the default. It applies to that patch date only (its hotfixes
    /// included), so the next patch pauses the hooks again.
    /// </summary>
    public string EnableHooksOnUntestedVersion { get; set; } = string.Empty;

    // ---- 1.5.0: data freshness strip ----
    /// <summary>
    /// The client game version (ffxivgame.ver text) on which the player dismissed the main window's "Game updated"
    /// strip (<c>Core.Diagnostics.DataFreshness</c>); empty when never dismissed. One dismissal covers that client
    /// version only: the next game update shows the strip again.
    /// </summary>
    public string DataFreshnessDismissedFor { get; set; } = string.Empty;

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
    /// Legacy (1.7 and earlier): the per-character override of the shield, by content id. Since 1.8.0 it lives in
    /// <c>user/characters.json</c> (<see cref="Core.Storage.CharacterSettingsBook"/>), shared by every game client; this
    /// is read once at load, moved there, and emptied (<see cref="TakeLegacyCharacterSettings"/>).
    /// </summary>
    public Dictionary<ulong, bool> SpoilerShieldByCharacter { get; set; } = [];

    /// <summary>
    /// The shield for a character (null in browse mode): the settings above, with the character's own override applied
    /// (<paramref name="shield"/>: true always shields, false shows everything, null follows the settings).
    /// </summary>
    public SpoilerOptions SpoilerOptionsFor(ulong? contentId, bool? shield)
    {
        var ahead = Math.Clamp(SpoilerRevealAhead, 0, SpoilerOptions.MaxAhead);
        if (contentId is not null && shield is { } shielded)
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

    /// <summary>
    /// Whether the "Set up your road" card (1.7.0, decision 7) has been shown; it shows by itself once, after the tour
    /// offer, and Help › Quick start opens it again. Null until a load decides it: a configuration that existed before
    /// the card (an update) reads as shown, so only a fresh install gets it unasked.
    /// </summary>
    public bool? SetupCardSeen { get; set; }

    /// <summary>Whether the first pin while the Todo overlay was off already offered to turn the overlay on (1.7.0); it asks once.</summary>
    public bool PinOverlayPromptShown { get; set; }

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

    /// <summary>
    /// Size of Tsukimichi's text on its own, on top of <see cref="UiScale"/> (1.13.0, feature plan v6 U7): 0.8–1.5 in
    /// steps of 0.1, default 1. The fonts are built at that size, so text stays sharp. Clamped and stepped by
    /// <see cref="ScaleMetrics.ClampTextScale"/> when read.
    /// </summary>
    public float TextScale { get; set; } = ScaleMetrics.DefaultTextScale;

    /// <summary>
    /// The Settings page open last, by name (<see cref="SettingsSections.Name"/>), so Settings opens where it was left
    /// across sessions. Names from before 1.13 open the page their settings moved to (<see cref="SettingsSections.Parse"/>).
    /// </summary>
    public string SettingsPage { get; set; } = string.Empty;

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

    // ---- 1.5.0: Moonlit totals (feature plan v5, decision 4) ----
    /// <summary>
    /// Moonlit toolbar "Count rewards that are gone for good": keep rewards whose event is over (or whose quest the game
    /// removed) and that the character does not have in the obtained/total counts. Off by default: they leave the totals.
    /// </summary>
    public bool MoonlitCountGone { get; set; }

    /// <summary>Moonlit toolbar "Group by expansion": the rows sorted by their quest's expansion under one heading each. Off by default.</summary>
    public bool MoonlitGroupByExpansion { get; set; }

    // ---- 0.5.1: motion ----
    /// <summary>
    /// Replace the hold-to-confirm arc with a text countdown (and, later, other animation with a cut). Until the user
    /// sets it in Settings (<see cref="ReduceMotionChosen"/>), every load follows Windows' "Show animations" setting
    /// (<see cref="OsMotion"/>), so it is on by default for players who turned animations off system-wide.
    /// </summary>
    public bool ReduceMotion { get; set; }

    // ---- 1.11.0: the safety table (feature plan v6 S2) ----
    /// <summary>
    /// How long a press-and-hold button (Delete all, Forget, Replace Questionable's list, Apply) must be held; 0.3–2.0 s,
    /// default 0.6 s. Clamped by <see cref="SafetyHoldSecondsClamped"/> when read.
    /// </summary>
    public float SafetyHoldSeconds { get; set; } = SafetyRules.DefaultHoldSeconds;

    /// <summary>
    /// For hand strain: two clicks confirm in place of a held Ctrl or Shift key and in place of a held button.
    /// </summary>
    public bool SafetyTwoClick { get; set; }

    /// <summary><see cref="SafetyHoldSeconds"/> within the range Settings offers.</summary>
    [Newtonsoft.Json.JsonIgnore]
    public float SafetyHoldSecondsClamped => SafetyRules.ClampHoldSeconds(SafetyHoldSeconds);

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

    // ---- 1.4: pane widths (feature plan v4 L1) ----
    /// <summary>
    /// The left pane's width (the Journal tree, or another tab's list) in logical pixels, so it follows the UI scale:
    /// 300 by default, never under 180 (<see cref="PaneLayout"/>). Set by dragging its handle; a double-click resets
    /// it. Before 1.4 the width lived in imgui.ini with the old body table and is not carried over.
    /// </summary>
    public float TreePaneWidth { get; set; } = PaneLayout.TreeDefaultLogical;

    /// <summary>The detail pane's width in logical pixels: 360 by default, never under 260 (<see cref="PaneLayout"/>).</summary>
    public float DetailPaneWidth { get; set; } = PaneLayout.DetailDefaultLogical;

    /// <summary>The Journal tree was dragged shut to its strip of icons; a drag outwards or a double-click opens it.</summary>
    public bool TreePaneStrip { get; set; }

    /// <summary>
    /// The filter drawer's pin (feature plan v6 U2): while on, a click elsewhere in the window leaves the drawer open; it
    /// still closes on Esc, its × and the Filters button. Off by default.
    /// </summary>
    public bool FilterDrawerPinned { get; set; }

    /// <summary>
    /// Settings › Display › Compact rail (feature plan v4 L7): the 44 px rail of icons, their labels in tooltips, at every
    /// window width. Off by default, when the rail is compact only on windows under about 1,040 px.
    /// </summary>
    public bool CompactRail { get; set; }

    // ---- 1.4: the Moon Road look (feature plan v4 V1, V3) ----
    /// <summary>
    /// Settings › Display › Look › Flair: how much of the Moon Road ornament shows. Full (the default) draws all of it,
    /// Quiet keeps the section rules and dividers only, Plain is the look before 1.4. The high-contrast glyph palette
    /// draws at most Quiet (<see cref="Core.Ui.FlairRules.Effective"/>).
    /// </summary>
    public Flair Flair { get; set; } = Flair.Full;

    /// <summary>
    /// Settings › Display › Look › Moon style (feature plan v6 G3): Medallion (the default) draws the 1.12 medals and
    /// gauges, Classic the 1.11 moons everywhere they draw, so the two can be compared in game.
    /// </summary>
    public MoonStyle MoonStyle { get; set; } = MoonStyle.Medallion;

    /// <summary>
    /// Settings › Display › Look › Game fonts for headings: section headings, titles and counts in the game's own
    /// display fonts (TrumpGothic, Jupiter, MiedingerMid). On by default; off, or with Flair set to Plain, headings use
    /// the Caption and Display roles as before.
    /// </summary>
    public bool GameHeadingFonts { get; set; } = true;

    // ---- 1.14.0: the Full sky (feature plan v7 UI-6) ----
    /// <summary>
    /// Settings › General › Look › Moving night sky (docs/design/v7/ui/spec.md Revision 3): at Full, the stars drift as
    /// one sky, 6 px a minute, while the main window is focused, with a rare faint shooting star. On by default; Reduce
    /// motion stills it.
    /// </summary>
    public bool MovingNightSky { get; set; } = true;

    /// <summary>
    /// Settings › General › Look › Shooting star on completion (spec §3.6): at Full, one meteor crosses the largest empty
    /// sky when a quest is completed, at most once in 30 seconds. On by default; Reduce motion stops it.
    /// </summary>
    public bool CompletionMeteor { get; set; } = true;

    /// <summary>
    /// Settings › General › Look › Milky Way in the sky (spec §3.4): at Full, a faint band across one large patch of empty
    /// sky. Off by default (the owner's call).
    /// </summary>
    public bool MilkyWay { get; set; }

    /// <summary>
    /// Moonlit's view (feature plan v4 V5, proposal §7.5): a gallery of reward icons instead of the table. Off (the
    /// table) by default; remembered once switched.
    /// </summary>
    public bool MoonlitGallery { get; set; }

    // ---- 0.8.0: chrome (T13) ----
    /// <summary>
    /// Draw the windows in the user's Dalamud colours instead of the Night palette: the same layout, with the surface
    /// and text roles mapped from the Dalamud style (<c>Ui.Theme.Refresh</c>). Gold, Eclipse and the moons keep their
    /// colours. Off by default.
    /// </summary>
    public bool FollowDalamudColours { get; set; }

    // ---- 1.1: high-contrast glyph palette (accessibility panel §2.2) ----
    /// <summary>
    /// Settings › Display › Glyph palette: Standard (the moons as designed) or High contrast (flat colours on a
    /// luminance ladder, thicker rims and one in-disc mark per state; the table stripes, halos and marks follow it).
    /// Standard by default.
    /// </summary>
    public GlyphPaletteKind GlyphPalette { get; set; } = GlyphPaletteKind.Standard;

    // ---- 1.1: localization (V2-19) ----
    /// <summary>
    /// Settings › Display › Plugin language: follow Dalamud's UI language (the default; English where Tsukimichi has
    /// no translation) or always English. <see cref="Localization.PluginLanguage.Pseudo"/> is the layout check.
    /// </summary>
    public Localization.PluginLanguage PluginLanguage { get; set; } = Localization.PluginLanguage.FollowDalamud;

    /// <summary>
    /// Set once 1.13.0 has put <see cref="PluginLanguage"/> back on following Dalamud: the picker is hidden while
    /// localization is frozen (feature plan v6 decision 7), so a choice made before could no longer be undone. Null on a
    /// configuration from before.
    /// </summary>
    public bool? LanguageFreezeApplied { get; set; }

    /// <summary>The todo overlay's Compact mode: moon and name only, one line per row, no hints. Off by default.</summary>
    public bool TodoOverlayCompact { get; set; }

    // ---- 0.8.0: keyboard (T17, accessibility A7) ----
    // The game sees every key the plugin reads (only a text field swallows them), so beyond Ctrl+F and Esc every
    // shortcut is opt-in: Ctrl+1..5 are hotbar 2 in the default keybinds and single letters are often bound.

    /// <summary>Ctrl+1..5 switch the main window's tabs (Journal, Moonlit, Characters, Flight, My blues) while it has focus. Off by default.</summary>
    public bool ShortcutTabs { get; set; }

    /// <summary>F flags the selected quest's giver on the map while the main window has focus. Off by default.</summary>
    public bool ShortcutFlag { get; set; }

    /// <summary>Enter shows the selected quest in the Journal while the main window has focus. Off by default.</summary>
    public bool ShortcutReveal { get; set; }

    /// <summary>P pins or unpins the selected quest while the main window has focus. Off by default.</summary>
    public bool ShortcutPin { get; set; }

    // ---- 1.11.0: command aliases (A12) ----
    /// <summary>
    /// Extra names for <c>/tsuki</c>, separated by spaces (Settings › Keyboard › Chat commands); <c>/ts</c> and
    /// <c>/moon</c> work without them. Read by <see cref="Core.Text.CommandAliases.Parse"/>. Empty by default.
    /// </summary>
    public string CommandAliases { get; set; } = string.Empty;

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

    /// <summary>Legacy (1.7 and earlier): gate ids already announced in chat, by content id. Moved to <c>user/characters.json</c> at load (1.8.0).</summary>
    public Dictionary<ulong, HashSet<string>> PayoffGatesNoticedByCharacter { get; set; } = [];

    /// <summary>Legacy (1.7 and earlier): gate ids whose "why? (spoiler)" the player opened, by content id. Moved to <c>user/characters.json</c> at load (1.8.0).</summary>
    public Dictionary<ulong, HashSet<string>> PayoffWhyOpenByCharacter { get; set; } = [];

    // ---- 1.1: "Since you were away" (P7) ----
    /// <summary>
    /// Settings › Notices "Show 'Since you were away' after N days": the card opens at a login when every stored
    /// character's newest capture is at least this many days old (see <c>Core.Return.WelcomeBackTrigger</c>). 0 turns
    /// it off; clamped to 0–180 on load. Per-character "Don't show again" lives in the character's sidecar.
    /// </summary>
    public int WelcomeBackDays { get; set; } = Core.Return.WelcomeBackTrigger.DefaultDays;

    /// <summary>
    /// The per-character settings 1.7 kept here, as copies for the one-time move to <c>user/characters.json</c>; empty
    /// once moved. <see cref="ClearLegacyCharacterSettings"/> empties them after the move landed.
    /// </summary>
    public Core.Storage.LegacyCharacterSettings TakeLegacyCharacterSettings() => new(
        new Dictionary<ulong, bool>(SpoilerShieldByCharacter),
        new Dictionary<ulong, HashSet<string>>(PayoffGatesNoticedByCharacter),
        new Dictionary<ulong, HashSet<string>>(PayoffWhyOpenByCharacter));

    /// <summary>Empties the legacy per-character settings once they are in <c>user/characters.json</c>; true when there was any.</summary>
    public bool ClearLegacyCharacterSettings()
    {
        var any = SpoilerShieldByCharacter.Count > 0 || PayoffGatesNoticedByCharacter.Count > 0 || PayoffWhyOpenByCharacter.Count > 0;
        SpoilerShieldByCharacter.Clear();
        PayoffGatesNoticedByCharacter.Clear();
        PayoffWhyOpenByCharacter.Clear();
        return any;
    }

    // ---- 1.8.0: alts (R7 B, E, H) ----
    /// <summary>The Characters list groups the characters not logged in by data center when they span more than one. On by default.</summary>
    public bool CharacterListByDataCenter { get; set; } = true;

    /// <summary>Hidden characters show (dimmed) in the Characters list and the character switcher, so they can be shown again. Off by default.</summary>
    public bool ShowHiddenCharacters { get; set; }

    public const int MinForgetDays = 7;
    public const int MaxForgetDays = 3650;
    public const int DefaultForgetDays = 180;

    /// <summary>Settings › Data "Forget characters not seen in N days": N, clamped to 7–3650 on load. 180 by default.</summary>
    public int ForgetNotSeenDays { get; set; } = DefaultForgetDays;

    // ---- 1.1: journal text (P9) ----
    /// <summary>
    /// Settings › Journal text › "Search journal text of completed quests": the search box also matches the words of
    /// the journal entries of quests the viewed character completed, through a word index built once per game version
    /// (<c>Game.QuestTextService</c>). Off by default.
    /// </summary>
    public bool JournalTextSearch { get; set; }

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
        config.DataFreshnessDismissedFor = config.DataFreshnessDismissedFor?.Trim() ?? string.Empty;
        config.SpoilerShieldByCharacter ??= [];
        config.PayoffGatesNoticedByCharacter ??= [];
        config.PayoffWhyOpenByCharacter ??= [];
        config.ExportFolder ??= string.Empty;
        config.CommandAliases ??= string.Empty;
        config.SettingsPage ??= string.Empty;
        config.TextScale = ScaleMetrics.ClampTextScale(config.TextScale);
        config.WelcomeBackDays = Math.Clamp(config.WelcomeBackDays, 0, Core.Return.WelcomeBackTrigger.MaxDays);
        config.ForgetNotSeenDays = Math.Clamp(config.ForgetNotSeenDays, MinForgetDays, MaxForgetDays);
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

        config.JournalColumnWidths = SanitizeColumnWidths(config.JournalColumnWidths);
        config.TreePaneWidth = PaneLayout.SanitizeTree(config.TreePaneWidth);
        config.DetailPaneWidth = PaneLayout.SanitizeDetail(config.DetailPaneWidth);

        if (!Enum.IsDefined(config.GlyphPalette))
        {
            config.GlyphPalette = GlyphPaletteKind.Standard;
        }

        if (!Enum.IsDefined(config.Flair))
        {
            config.Flair = Flair.Full;
        }

        config.MoonStyle = MoonStyleRules.Effective(config.MoonStyle);

        // 1.13.0: the language picker is hidden while localization is frozen, so a language chosen before (English, or
        // the pseudo layout check) would stick with no way back; it follows Dalamud again, once. An unknown value does too.
        if (config.LanguageFreezeApplied != true || !Enum.IsDefined(config.PluginLanguage))
        {
            config.PluginLanguage = Localization.PluginLanguage.FollowDalamud;
            config.LanguageFreezeApplied = true;
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

        // 1.12.1: the Unlocks column is on by default. A configuration saved by 1.12.0 (where it was off by default) gets it
        // turned on once; from then on the player's own choice stands.
        if (config.UnlocksColumnDefaultApplied != true)
        {
            config.JournalShowOpensColumn = true;
            config.UnlocksColumnDefaultApplied = true;
        }

        // The setup card (1.7.0) is for a fresh install; a configuration saved before the card existed has set up already.
        config.SetupCardSeen ??= hadFile;

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
