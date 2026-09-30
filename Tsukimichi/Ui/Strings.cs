using System.Collections.Generic;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Every literal the UI shows, in one place, so DRAFT-NEEDED F can swap them for resource lookups without touching
/// the panes. Voice per spec §1: calm, precise, short; labels are nouns; no exclamation marks.
/// Members ending in <c>Format</c> are composite format strings; the argument order is documented on each.
/// </summary>
public static partial class Strings
{
    // Window
    public const string MainWindowTitle = "Tsukimichi###TsukimichiMain";
    public const string LoadingCatalog = "Loading catalog";
    public const string CatalogUnavailable = "Catalog unavailable";
    public const string Retry = "Retry";
    public const string Retrying = "Retrying";

    // Toolbar
    public const string SearchHint = "Search quests, rewards or ids";
    public const string SearchTooltip = "Matches quest names, reward names and numeric ids. Ctrl+F jumps here";
    public const string ClearSearch = "Clear search";
    public const string Filters = "Filters";
    public const string FiltersTooltip = "Show or hide the filter panel beside the tree";
    public const string NoCharacter = "No character";
    public const string NoSnapshots = "No snapshots yet";
    public const string LiveMarker = "● ";
    public const string SyncLive = "Live";
    public const string SyncPollerPaused = "Game reads paused after an error; retrying";
    /// <summary>{0} = snapshot time.</summary>
    public const string SyncSnapshotFormat = "Snapshot from {0}";
    /// <summary>{0} = name, {1} = world, {2} = time.</summary>
    public const string StaleBannerFormat = "Snapshot: {0}@{1}, {2}";
    /// <summary>{0} = name, {1} = world, {2} = age.</summary>
    public const string CharacterEntryFormat = "{0}@{1} · {2}";
    /// <summary>{0} = name, {1} = world.</summary>
    public const string CharacterNameFormat = "{0}@{1}";
    public const string BrowseModeNotice = "No character snapshot: states, blockers and availability are not evaluated.";
    public const string HelpButtonTooltip = "Help";
    public const string TutorialButtonTooltip = "Tour: a guided walk through the window";
    public const string SettingsButtonTooltip = "Settings";
    public const string ActionUnavailable = "Not available yet";

    // Status bar: {0} = catalog count, {1} = rows shown, {2} = total in scope. The live/snapshot text follows its pip,
    // the MSQ pill and the version (right-aligned) are separate segments (T12).
    public const string StatusFormat = "{0:N0} quests · showing {1:N0} of {2:N0}";
    /// <summary>{0} = plugin version.</summary>
    public const string StatusVersionFormat = "v{0}";
    /// <summary>{0} = whole percent of every counted quest done.</summary>
    public const string StatusPercentFormat = "{0}%";
    public const string StatusLive = "live";
    /// <summary>{0} = time.</summary>
    public const string StatusSnapshotFormat = "snapshot {0}";
    public const string StatusNoSnapshot = "no snapshot";

    // Main scenario position (status bar and Characters dashboard)
    /// <summary>{0} = quest name.</summary>
    public const string StatusMsqFormat = "MSQ · {0} ›";
    public const string StatusMsqComplete = "MSQ · complete";
    /// <summary>{0} = the routes of a branched main scenario, "route A 3/9 · route B —" (<c>MsqText.Compact</c>).</summary>
    public const string StatusMsqRoutesFormat = "MSQ · {0}";
    /// <summary>{0} = expansion, {1} = the routes, "route A 3 of 9 · route B not started" (<c>MsqText.Spelled</c>).</summary>
    public const string CharactersMsqRoutesFormat = "MSQ: {0} · {1}";
    /// <summary>{0} = expansion, {1} = done, {2} = total.</summary>
    public const string MsqProgressFormat = "{0} · {1:N0} of {2:N0} main scenario quests done";
    /// <summary>{0} = done, {1} = total.</summary>
    public const string MsqCompleteFormat = "Main scenario complete · {0:N0} of {1:N0}";
    /// <summary>{0} = NPC, {1} = zone.</summary>
    public const string MsqGiverFormat = "{0}, {1}";
    public const string MsqClickHint = "Click to select";
    /// <summary>{0} = expansion, {1} = quest, {2} = giver ("NPC, zone").</summary>
    public const string CharactersMsqFormat = "MSQ: {0} · next: {1} ({2})";
    /// <summary>{0} = expansion, {1} = quest.</summary>
    public const string CharactersMsqNoGiverFormat = "MSQ: {0} · next: {1}";
    public const string CharactersMsqComplete = "MSQ: complete";

    // Tabs
    public const string TabJournal = "Journal";
    public const string TabMoonlit = "Moonlit";
    public const string TabCharacters = "Characters";
    public const string Placeholder = "Coming in the next merge";

    // Tree
    public const string AllQuests = "All quests";
    public const string FeatureUnlocks = "Unlock quests";
    public const string RemovedFromGame = "Removed from the game";

    // Journal filing provenance (detail pane, under the journal path)
    /// <summary>{0} = genre name, {1} = rule number, {2} = <see cref="FilingReason"/>.</summary>
    public const string FilingRuleFormat = "Filed under {0} (rule {1}: {2})";
    /// <summary>{0} = genre name.</summary>
    public const string FilingCuratedFormat = "Filed under {0} (curated override)";
    /// <summary>{0} = the patch the curated note names.</summary>
    public const string RemovedInPatchFormat = "Removed from the game in patch {0}";
    /// <summary>
    /// The sheet's own signal behind a rule-1 retirement, under a journal path that already reads "Removed from the
    /// game" (an unlisted row): {0} = <see cref="RetiredReason"/>.
    /// </summary>
    public const string RetiredRuleFormat = "Rule 1: {0}";
    /// <summary>The same signal for a retired row the journal still lists, whose path is its genre: {0} = <see cref="RetiredReason"/>.</summary>
    public const string RemovedByRuleFormat = "Removed from the game (rule 1: {0})";

    /// <summary>Which sheet signal retired a row under rule 1 (docs/data/unlisted-report.md section 4).</summary>
    public static string RetiredReason(bool placeholderIssuer, bool hiddenFlag) => (placeholderIssuer, hiddenFlag) switch
    {
        (true, true) => "placeholder issuer, hidden flag",
        (_, true) => "hidden flag",
        _ => "placeholder issuer",
    };

    /// <summary>The short reason behind a refiling rule (docs/data/unlisted-report.md section 4).</summary>
    public static string FilingReason(byte rule) => rule switch
    {
        2 => "class or job intro",
        3 => "Grand Company",
        4 => "nearest listed prerequisite",
        5 => "nearest listed successor",
        6 => "issuer's zone",
        7 => "no signal",
        _ => "rule " + rule.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
    /// <summary>{0} = done, {1} = total.</summary>
    public const string CountFormat = "{0}/{1}";
    /// <summary>A filling moon's tooltip line: {0} = done/total, {1} = percent.</summary>
    public const string ProgressFormat = "{0} · {1}%";
    /// <summary>Hover text of a folded tree node: {0} = section, {1} = category, {2} = genre.</summary>
    public const string FoldedPathFormat = "{0} › {1} › {2}";

    // Quick views (one-click presets at the top of the filter panel)
    public const string Presets = "Quick views";
    public const string PresetFeatureQuests = FilterNames.FeatureQuests;
    public const string PresetLevelBand = FilterNames.LevelBand;
    public const string PresetStalled = FilterNames.Stalled;
    public const string PresetFeatureQuestsTooltip = "Unlock quests: duties, jobs, actions, aether currents, systems. Those from the newest patch series come first, then quests you can act on now";
    public const string PresetLevelBandTooltip = "Quests within five levels of your current job's level";
    public const string PresetStalledTooltip = "Quests that have sat in your journal for the number of days below";
    public const string PresetStorySidequests = FilterNames.StorySidequests;
    public const string PresetStorySidequestsTooltip = "Sidequests with journal artwork, the ones that tell a small story, zone by zone; each side story in the order you play it";

    // The book badge on a story sidequest's table row. {0} = the side story (its first quest's name), {1} = place, {2} = length.
    public const string StoryBadgeFormat = "Part of a side story: {0} ({1} of {2})";
    public const string StoryBadgeLone = "A side story in one quest: it carries journal artwork";
    public const string StalledDaysFormat = "%d days";
    public const string StalledDaysLabel = "Stalled after";
    public const string StalledDaysTooltip = "How long an accepted quest sits untouched before Stalled lists it";

    // Filter panel
    public const string HideCompleted = FilterNames.HideCompleted;
    public const string AvailableOnly = FilterNames.AvailableOnly;
    public const string Overrides = "Overrides";
    public const string OverrideInherit = "Inherit";
    public const string OverrideOn = "On";
    public const string OverrideOff = "Off";
    public const string OverrideOptions = "Inherit\0On\0Off\0";
    public const string NeedsSnapshot = "Needs a character snapshot";
    public const string Advanced = "Advanced";
    public const string States = "States";
    public const string Expansions = "Expansions";
    public const string LevelRange = FilterNames.LevelRange;
    public const string LevelFormat = "Lv %d";
    public const string LevelMaxFormat = "to %d";

    /// <summary>The level range filter's chip: {0} = lowest level, {1} = highest.</summary>
    public const string LevelRangeChipFormat = "Lv {0}–{1}";
    public const string JobCategory = FilterNames.JobCategory;
    public const string JobAll = "All";
    public const string JobDowDom = "DoW/DoM";
    public const string JobDoh = "DoH";
    public const string JobDol = "DoL";
    public const string JobCurrentOnly = "Current job only";
    public const string JobCurrentOnlyTooltip = "Quests restricted to the current job";
    public const string RewardKinds = FilterNames.RewardKinds;
    public const string RewardHidden = "Hidden";
    public const string RewardShow = "Show";
    public const string RewardOnly = "Only";

    public static string RewardOptionName(TriState option) => option switch
    {
        TriState.Hidden => RewardHidden,
        TriState.Only => RewardOnly,
        _ => RewardShow,
    };
    public const string RepeatableOnly = "Repeatable only";
    public const string SeasonalActiveOnly = "Seasonal active only";
    public const string IncludeUnlisted = FilterNames.IncludeUnlisted;
    public const string PinnedOnly = "Pinned only";
    public const string PinnedFirst = "Pinned first";
    public const string Reset = "Reset";

    // Display (size sliders at the bottom of the filter panel)
    public const string Display = "Display";
    public const string UiScale = "UI scale";
    public const string UiScaleTooltip = "Text and layout size of this window on top of Dalamud's global scale";
    public const string IconScale = "Icon scale";
    public const string IconScaleTooltip = "Size of moons, reward icons and banners relative to the text";
    public const string ScaleFormat = "%.2f×";
    public const string ResetDisplay = "Default sizes";
    public const string ResetDisplayTooltip = "Back to the default UI and icon scale";

    // Filter panel tooltips
    public const string HideCompletedTooltip = "Remove Completed and Locked out quests from the table";
    public const string AvailableOnlyTooltip = "Keep only quests you can act on now: Ready, Ready on another job and In journal";
    public const string OverridesTooltip = "Turn this filter on or off for single categories";
    public const string PinnedFirstTooltip = "Keep pinned quests at the top of the table whatever the sort";
    public const string StatesTooltip = "Untick a state to hide quests in it";
    public const string ExpansionsTooltip = "Tick expansions to keep only their quests; none ticked keeps all";
    public const string LevelRangeTooltip = "Keep quests whose level is inside the range; drag the top to 100 for no upper bound";
    public const string JobCategoryTooltip = "Keep quests restricted to a discipline, or to exactly the current job";
    public const string RewardKindsTooltip = "Per reward kind: Hidden removes quests giving it, Only keeps just those";
    public const string RepeatableOnlyTooltip = "Keep only repeatable quests such as dailies and weeklies";
    public const string SeasonalActiveOnlyTooltip = "Keep only seasonal-event quests whose event is running right now";
    public const string IncludeUnlistedTooltip = "Also show quests removed from the game under All quests and Unlock quests";
    public const string PinnedOnlyTooltip = "Keep only quests you pinned";
    public const string ResetTooltip = "Clear every filter and the search";
    public const string ResetFilters = "Reset filters";
    public const string NothingMatchesCombination = "No single filter is to blame; loosen several or reset.";
    public const string ScopeEmpty = "This node has no quests.";
    public const string ChipStatePrefix = "States: ";
    public const string ChipStateExcludedMarker = "−";
    public const string ChipStateSeparator = ", ";
    /// <summary>{0} = number of excluded states beyond the named ones.</summary>
    public const string ChipStateMoreFormat = " +{0}";
    public const string ChipExpansion = FilterNames.Expansion;
    public const string ChipRepeatable = FilterNames.Repeatable;
    public const string ChipSeasonal = FilterNames.SeasonalActive;
    public const string ChipPinned = FilterNames.Pinned;
    public const string ChipTooltip = "Click to clear";
    /// <summary>First line of the state chip's tooltip; the excluded states follow, all of them, however many the chip names.</summary>
    public const string ChipStateTooltipPrefix = "Hiding: ";

    /// <summary>First line of a filling moon's tooltip; the done/total (and percent where shown) follows.</summary>
    public const string FillingMoonTooltip = "Done of total";

    // Table
    public const string ColumnGlyph = "State";
    public const string ColumnName = "Name";
    public const string ColumnLevel = "Lv";
    public const string ColumnJob = "Job";
    public const string ColumnStatus = "Status";
    public const string ColumnExpansion = "Exp";
    public const string ColumnRewards = "Rewards";
    public const string ColumnGlyphTooltip = "Quest state as a moon phase, and as the pattern of the stripe at the row's edge (hover it for its name); click to sort by state";
    public const string ColumnNameTooltip = "Quest name; click to sort, right-click a header to hide columns";
    public const string ColumnLevelTooltip = "Quest level; click to sort";
    public const string ColumnJobTooltip = "Who can take it: Any, one job (with its icon), or a discipline; hover for the name";
    public const string ColumnStatusTooltip = "Why the quest is not ready yet: the first unmet requirement, or what to do next";
    public const string ColumnExpansionTooltip = "Expansion the quest belongs to; click to sort";
    public const string ColumnRewardsTooltip = "Up to four reward icons; hover one for details";
    public const string JobAny = "Any";
    public const string JobMulti = "Multi";
    public const string JobDohDol = "DoH/DoL";
    public const string Pin = "Pin";
    public const string Unpin = "Unpin";
    public const string FlagOnMap = "Flag on map";
    public const string OpenJournal = "Open journal";
    public const string OpenJournalUnavailable = "Only quests in your journal (accepted or completed) can be opened in the game journal.";
    public const string CopyName = "Copy name";
    public const string CopyCoordinates = "Copy coordinates";
    public const string CopyCoordinatesTooltip = "Copy \"Place (x.x, y.y)\" to the clipboard, ready to paste into chat";
    /// <summary>{0} = place name, {1} = x, {2} = y.</summary>
    public const string CoordinateClipboardFormat = "{0} ({1:0.0}, {2:0.0})";
    public const string ShowPath = "Show path";
    public const string QuestMapGraph = "Quest Map graph";
    public const string LinkInChat = "Link in chat";
    public const string Report = "Report";
    public const string ReportTooltip = "Copy a diagnostic block for this quest to the clipboard, ready to paste into a GitHub issue: versions, the quest, its state, every requirement's verdict and the inputs it was judged from. No character identifiers.";
    public const string ReportCopied = "Copied · paste it into a GitHub issue";
    public const string ReportClipboardFailed = "The clipboard refused the text; try again.";
    /// <summary>{0} = quest name.</summary>
    public const string ReportCopiedChatFormat = "Copied the diagnostic block for {0} to the clipboard; paste it into a GitHub issue.";
    public const string ReportNoSelection = "Select a quest first, or name one: /tsuki report <quest name>";
    /// <summary>{0} = the text given.</summary>
    public const string ReportNoMatchFormat = "No quest matches \"{0}\".";
    /// <summary>{0} = reward name, {1} = count.</summary>
    public const string RewardCountFormat = "{0} ×{1}";
    /// <summary>Reward tooltip item line: {0} = item level, {1} = ItemUICategory name.</summary>
    public const string ItemSummaryFormat = "iLv {0} · {1}";
    /// <summary>{0} = item level.</summary>
    public const string ItemLevelFormat = "iLv {0}";

    // Detail pane
    public const string SelectQuest = "Select a quest in the table to see its requirements, rewards and path.";
    public const string QuestNotInCatalog = "Quest not in catalog";
    public const string Requirements = "Requirements";
    public const string NoRequirements = "Nothing gates this quest.";
    public const string RequirementsNeedSnapshot = "Requirements are evaluated once a character snapshot exists.";
    public const string Rewards = "Rewards";
    public const string NoRewards = "No listed rewards.";
    public const string Path = "Path";
    public const string FoldedRunExpandTooltip = "Show the completed steps";
    public const string FoldedRunCollapseTooltip = "Fold the completed steps away";
    public const string UniqueSection = "Moonlit";
    public const string MarkedUniqueByYou = "Marked unique by you";
    public const string MarkedNotUniqueByYou = "Marked not unique by you";
    public const string RestoreOverride = "Restore";
    public const string RestoreOverrideTooltip = "Forget your verdict; the shipped reward data applies again";
    public const string ListedInMoonlit = "Listed in Moonlit treasures.";
    public const string NotListedInMoonlit = "Not listed in Moonlit treasures.";
    public const string MarkUnique = "Mark as unique…";
    public const string MarkUniqueTooltip = "Add this quest to Moonlit treasures as a unique reward you vouch for";
    public const string MarkUniquePopup = "##markUnique";
    public const string MarkUniqueNoteHint = "Note: which reward is unique";
    public const string MarkUniqueConfirm = "Mark as unique";
    public const string MarkNotUniqueNoteHint = "Note: why it is not unique (optional)";
    public const string MarkNotUniqueConfirm = "Hide as not unique";
    public const string VerdictQuestionUniquePrefix = "Add ";
    public const string VerdictQuestionUniqueSuffix = " to Moonlit treasures?";
    public const string VerdictQuestionHidePrefix = "Hide ";
    public const string VerdictQuestionHideSuffix = " from Moonlit treasures?";
    public const string VerdictConfirmTooltip = "Hold Shift and click, or press and hold";
    /// <summary>{0} = seconds left, shown instead of the hold arc under Reduce motion.</summary>
    public const string VerdictHoldCountdownFormat = "Hold… ({0:0.0} s)";
    public const string VerdictUndoMarkedUnique = "Marked unique";
    public const string VerdictUndoMarkedNotUnique = "Hidden as not unique";
    public const string VerdictUndoSeparator = " · ";
    public const string VerdictUndo = "Undo";
    public const string VerdictUndoTooltip = "Forget that verdict again";
    public const string Cancel = "Cancel";
    public const string Giver = "Giver";
    public const string NoGiver = "No issuer recorded.";
    public const string Met = "✓";
    public const string Unmet = "✗";
    public const string MetTooltip = "Met";
    public const string UnmetTooltip = "Not met";
    /// <summary>{0} = x, {1} = y.</summary>
    public const string CoordinatesFormat = "({0:0.0}, {1:0.0})";
    /// <summary>{0} = genre, {1} = category.</summary>
    public const string JournalPathFormat = "{0} › {1}";
    /// <summary>{0} = expansion, {1} = level, {2} = job category.</summary>
    public const string HeaderLineFormat = "{0} · Lv {1} · {2}";
    /// <summary>{0} = job abbreviation.</summary>
    public const string ReadyOnJobFormat = "Ready on {0}";
    /// <summary>{0} = sequence.</summary>
    public const string Pinned = "Pinned";

    // Chat
    /// <summary>{0} = remaining count.</summary>
    public const string AndMoreFormat = "and {0} more";
    public const string NoMatches = "No quests match.";
    public const string CatalogNotReady = "The catalog is still loading.";
    public const string ChatTag = "Tsukimichi";
    public const string ChatNewlyAvailablePrefix = "Now available: ";

    // Settings › Item hints (the hooks themselves are wired by the item-hint feature)
    public const string ConfigSectionItemHints = "Item hints";
    public const string ConfigItemHints = "Show a hint when hovering an item that is a quest reward";
    public const string ConfigItemHintsHint = "A small line near the cursor naming the quest and whether it is done";
    public const string ConfigItemContextMenu = "Add a Tsukimichi entry to item context menus";
    public const string ConfigItemContextMenuHint = "Right-click an item to reveal the quest that rewards it";

    // Command help
    public const string CommandHelp = "Open Tsukimichi (also /tsuki). search <text> (or just <text>) prints matching quest links; zone lists quests you can start here; which lists the targeted NPC's quests; why [quest name] says what blocks the selected or named quest; nearby and todo toggle the Nearby quests window and the Todo overlay; report [quest name] copies a diagnostic block for the selected or named quest; export [quests|moonlit] [json|csv] writes your completed quests or Moonlit collection to a file; settings (or config), help and glyphs open those windows.";
    public const string CommandAliasHelp = "Short form of /tsukimichi.";

    // Time
    public const string JustNow = "just now";
    /// <summary>{0} = minutes.</summary>
    public const string MinutesAgoFormat = "{0} min ago";
    /// <summary>{0} = hours.</summary>
    public const string HoursAgoFormat = "{0} h ago";
    /// <summary>{0} = days.</summary>
    public const string DaysAgoFormat = "{0} d ago";
    public const string TimeFormat = "HH:mm";
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>The display name of a state (docs/glossary.md); <see cref="StateNames"/> in Core owns the table.</summary>
    public static string StateName(QuestState state) => StateNames.Name(state);

    /// <summary>The display name of a state for a quest: a done repeatable says "Done today" or "Done this week" by its reset.</summary>
    public static string StateName(QuestState state, QuestRecord? quest) => StateNames.Name(state, quest);

    /// <summary>The moon-phase name of a state's glyph, shown under the display name in the Help legend and the glyph window only.</summary>
    public static string StateGlyphSubtitle(QuestState state) => StateNames.GlyphSubtitle(state);

    /// <summary>
    /// What a state moon says on hover: the display name and the glyph's shape hint ("Blocked · new moon, silver
    /// ring"); <see cref="StateNames"/> composes the eight strings once, so hovering allocates nothing. With the
    /// high-contrast glyph palette the shape hint names that palette's silhouette and mark ("Blocked · empty disc,
    /// thick rim").
    /// </summary>
    public static string StateTooltip(QuestState state) => StateNames.Tooltip(state, 0, Theme.Glyphs.HighContrast);

    /// <summary>The state moon tooltip for a quest: a done repeatable says "Done today" or "Done this week" by its reset.</summary>
    public static string StateTooltip(QuestState state, QuestRecord? quest) => StateNames.Tooltip(state, quest, Theme.Glyphs.HighContrast);

    /// <summary>
    /// The reason clause on its own, for the second line of a moon's tooltip: the decisive blocker from
    /// <see cref="BlockerText.For"/> for Blocked, Locked out and Not checked, so the tooltip matches the Status
    /// column; null for every other state, without a quest, or when the evaluation names nothing. Allocates: callers
    /// that hover every frame cache it (<see cref="UiMetrics.StateTooltip"/>).
    /// </summary>
    public static string? StateReason(QuestState state, QuestEvaluation? evaluation, QuestRecord? quest, BlockerNames names, IReadOnlyDictionary<uint, QuestEvaluation>? states)
    {
        if (evaluation is null || quest is null || state is not (QuestState.Blocked or QuestState.Foreclosed or QuestState.Unknown))
        {
            return null;
        }

        var reason = BlockerText.For(evaluation, quest, names, states);
        return reason.Length > 0 ? reason : null;
    }

    /// <summary>
    /// Between a state name and its reason; the same separator <see cref="BlockerText.StatusText"/> uses, so a surface
    /// that composes the two itself reads like the Status column.
    /// </summary>
    public const string StateReasonSeparator = BlockerText.Separator;

    public static string RequirementName(RequirementKind kind) => kind switch
    {
        RequirementKind.Retired => "Removed",
        RequirementKind.Foreclosure => "Foreclosure",
        RequirementKind.ExpansionCap => "Expansion",
        RequirementKind.LevelCap => "Level cap",
        RequirementKind.ClassJob => "Class or job",
        RequirementKind.Level => "Level",
        RequirementKind.PreviousQuests => "Previous quests",
        RequirementKind.GrandCompany => "Grand Company",
        RequirementKind.GrandCompanyRank => "Grand Company rank",
        RequirementKind.TribeRank => "Allied Society rank",
        RequirementKind.TribeReputation => "Allied Society reputation",
        RequirementKind.TribeAllowance => "Allowance",
        RequirementKind.TribeDailyOffer => "Daily offer",
        RequirementKind.DutyCompletion => "Duty",
        RequirementKind.Seasonal => "Seasonal",
        RequirementKind.AcceptCondition => "Condition",
        RequirementKind.Mount => "Mount",
        RequirementKind.House => "House",
        RequirementKind.Achievement => "Achievement",
        RequirementKind.CustomDeliveryRank => "Custom delivery",
        RequirementKind.CarrierLevel => "Carrier level",
        _ => kind.ToString(),
    };

    public static string RewardKindName(RewardKind kind) => kind switch
    {
        RewardKind.Item => "Item",
        RewardKind.OptionalItem => "Optional item",
        RewardKind.Emote => "Emote",
        RewardKind.Action => "Action",
        RewardKind.GeneralAction => "General action",
        RewardKind.Instance => "Instance",
        RewardKind.ClassJob => "Class or job",
        RewardKind.Other => "Other",
        RewardKind.ArtifactGear => "Artifact gear",
        RewardKind.Mount => "Mount",
        RewardKind.Minion => "Minion",
        RewardKind.Orchestrion => "Orchestrion roll",
        RewardKind.TripleTriadCard => "Triple Triad card",
        RewardKind.Ornament => "Fashion accessory",
        RewardKind.Barding => "Barding",
        RewardKind.Hairstyle => "Hairstyle",
        RewardKind.AetherCurrent => "Aether current",
        RewardKind.BlueMageSpell => "Blue magic spell",
        RewardKind.Trait => "Trait",
        RewardKind.Achievement => "Achievement",
        RewardKind.Title => "Title",
        RewardKind.DutyUnlock => "Duty unlock",
        RewardKind.SystemUnlock => "System unlock",
        _ => kind.ToString(),
    };

    /// <summary>Short expansion label for the table column.</summary>
    public static string ExpansionShort(byte expansion) => expansion switch
    {
        0 => "ARR",
        1 => "HW",
        2 => "StB",
        3 => "ShB",
        4 => "EW",
        5 => "DT",
        _ => expansion.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
