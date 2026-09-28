using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

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
    public const string SearchTooltip = "Matches quest names, reward names and numeric ids; applied 150 ms after you stop typing";
    public const string CharacterComboTooltip = "Which character's progress is shown: ● is the logged-in character, the others are stored snapshots";
    public const string ClearSearch = "Clear search";
    public const string Filters = "Filters";
    public const string FiltersTooltip = "Show or hide the filter panel";
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
    public const string BrowseModeNotice = "No character snapshot: states, next steps and availability are not evaluated.";

    // Status bar: {0} = catalog count, {1} = rows shown, {2} = total in scope, {3} = live/snapshot text, {4} = version.
    public const string StatusFormat = "{0:N0} quests · showing {1:N0} of {2:N0} · {3} · v{4}";
    public const string StatusLive = "live";
    /// <summary>{0} = time.</summary>
    public const string StatusSnapshotFormat = "snapshot {0}";
    public const string StatusNoSnapshot = "no snapshot";

    // Tabs
    public const string TabJournal = "Journal";
    public const string TabMoonlit = "Moonlit";
    public const string TabCharacters = "Characters";
    public const string Placeholder = "Coming in the next merge";

    // Tree
    public const string AllQuests = "All quests";
    public const string FeatureUnlocks = "Feature Unlocks";
    public const string Unlisted = "Unlisted";
    /// <summary>{0} = done, {1} = total.</summary>
    public const string CountFormat = "{0}/{1}";
    /// <summary>Hover text of a folded tree node: {0} = section, {1} = category, {2} = genre.</summary>
    public const string FoldedPathFormat = "{0} › {1} › {2}";

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
    public const string RewardOptions = "Hidden\0Show\0Only\0";
    public const string RepeatableOnly = "Repeatable only";
    public const string SeasonalActiveOnly = "Seasonal active only";
    public const string IncludeUnlisted = FilterNames.IncludeUnlisted;
    public const string PinnedOnly = "Pinned only";
    public const string PinnedFirst = "Pinned first";
    public const string Reset = "Reset";

    // Filter panel tooltips
    public const string HideCompletedTooltip = "Remove Completed and Foreclosed quests from the table";
    public const string AvailableOnlyTooltip = "Keep only quests you can pick up now: Ready, Ready on another job and Accepted";
    public const string OverridesTooltip = "Turn this filter on or off for single categories";
    public const string PinnedFirstTooltip = "Keep pinned quests at the top of the table whatever the sort";
    public const string StatesTooltip = "Untick a state to hide quests in it";
    public const string ExpansionsTooltip = "Tick expansions to keep only their quests; none ticked keeps all";
    public const string LevelRangeTooltip = "Keep quests whose level is inside the range; drag the top to 100 for no upper bound";
    public const string JobCategoryTooltip = "Keep quests restricted to a discipline, or to exactly the current job";
    public const string RewardKindsTooltip = "Per reward kind: Hidden removes quests giving it, Only keeps just those";
    public const string RepeatableOnlyTooltip = "Keep only repeatable quests such as dailies and weeklies";
    public const string SeasonalActiveOnlyTooltip = "Keep only seasonal-event quests whose event is running right now";
    public const string IncludeUnlistedTooltip = "Also show quests with no journal genre under All quests and Feature Unlocks";
    public const string PinnedOnlyTooltip = "Keep only quests you pinned";
    public const string ResetTooltip = "Clear every filter and the search";
    public const string ResetFilters = "Reset filters";
    public const string NothingMatches = "Nothing matches";
    public const string NothingMatchesHint = "Remove one of these filters:";
    public const string NothingMatchesCombination = "No single filter is to blame; loosen several or reset.";
    public const string ScopeEmpty = "This node has no quests.";
    public const string ChipSearch = FilterNames.Search;
    public const string ChipState = FilterNames.State;
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

    // Table
    public const string ColumnGlyph = "State";
    public const string ColumnName = "Name";
    public const string ColumnLevel = "Lv";
    public const string ColumnJob = "Job";
    public const string ColumnNextStep = "Next step";
    public const string ColumnExpansion = "Exp";
    public const string ColumnRewards = "Rewards";
    public const string ColumnGlyphTooltip = "Quest state as a moon phase; click to sort by state";
    public const string ColumnNameTooltip = "Quest name; click to sort, right-click a header to hide columns";
    public const string ColumnLevelTooltip = "Quest level; click to sort";
    public const string ColumnJobTooltip = "Who can take it: Any, one job, or a discipline";
    public const string ColumnNextStepTooltip = "The first unmet requirement, what to do next";
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
    /// <summary>{0} = reward name, {1} = count.</summary>
    public const string RewardCountFormat = "{0} ×{1}";
    /// <summary>Reward tooltip item line: {0} = item level, {1} = ItemUICategory name.</summary>
    public const string ItemSummaryFormat = "iLv {0} · {1}";
    /// <summary>{0} = item level.</summary>
    public const string ItemLevelFormat = "iLv {0}";

    // Detail pane
    public const string SelectQuest = "Select a quest";
    public const string QuestNotInCatalog = "Quest not in catalog";
    public const string Requirements = "Requirements";
    public const string NoRequirements = "Nothing gates this quest.";
    public const string RequirementsNeedSnapshot = "Requirements are evaluated once a character snapshot exists.";
    public const string Rewards = "Rewards";
    public const string NoRewards = "No listed rewards.";
    public const string Path = "Path";
    public const string PathSingle = "This quest starts its own path.";
    public const string Giver = "Giver";
    public const string NoGiver = "No issuer recorded.";
    public const string Met = "✓";
    public const string Unmet = "✗";
    public const string NextStepMarker = "▶";
    /// <summary>{0} = x, {1} = y.</summary>
    public const string CoordinatesFormat = "({0:0.0}, {1:0.0})";
    /// <summary>{0} = genre, {1} = category.</summary>
    public const string JournalPathFormat = "{0} › {1}";
    /// <summary>{0} = expansion, {1} = level, {2} = job category.</summary>
    public const string HeaderLineFormat = "{0} · Lv {1} · {2}";
    /// <summary>{0} = time.</summary>
    public const string ProvenanceCompletedFormat = "Completed per client flags at {0}";
    /// <summary>{0} = time.</summary>
    public const string ProvenanceEvaluatedFormat = "Evaluated from snapshot taken {0}";
    public const string ProvenanceNoSnapshot = "No snapshot; state unknown";
    /// <summary>{0} = job abbreviation.</summary>
    public const string ReadyOnJobFormat = "Ready on {0}";
    /// <summary>{0} = sequence.</summary>
    public const string AcceptedSequenceFormat = "Accepted, step {0}";
    public const string Pinned = "Pinned";

    // Chat
    /// <summary>{0} = remaining count.</summary>
    public const string AndMoreFormat = "and {0} more";
    public const string NoMatches = "No quests match.";
    public const string CatalogNotReady = "The catalog is still loading.";
    public const string ChatTag = "Tsukimichi";

    // Command help
    public const string CommandHelp = "Open Tsukimichi (also /tsuki). /tsukimichi <text> prints matching quest links; config, help and glyphs open those windows.";
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

    public static string StateName(QuestState state) => state switch
    {
        QuestState.Ready => "Ready",
        QuestState.ReadyOnOtherJob => "Ready on another job",
        QuestState.Accepted => "Accepted",
        QuestState.Blocked => "Blocked",
        QuestState.DoneThisCycle => "Done this cycle",
        QuestState.Completed => "Completed",
        QuestState.Foreclosed => "Foreclosed",
        QuestState.Unknown => "Unknown",
        _ => state.ToString(),
    };

    /// <summary>Short state name for chips.</summary>
    public static string StateShortName(QuestState state) => state switch
    {
        QuestState.Ready => "Ready",
        QuestState.ReadyOnOtherJob => "Other job",
        QuestState.Accepted => "Accepted",
        QuestState.Blocked => "Blocked",
        QuestState.DoneThisCycle => "Done cycle",
        QuestState.Completed => "Completed",
        QuestState.Foreclosed => "Foreclosed",
        QuestState.Unknown => "Unknown",
        _ => state.ToString(),
    };

    public static string RequirementName(RequirementKind kind) => kind switch
    {
        RequirementKind.Foreclosure => "Foreclosure",
        RequirementKind.ExpansionCap => "Expansion",
        RequirementKind.LevelCap => "Level cap",
        RequirementKind.ClassJob => "Class or job",
        RequirementKind.Level => "Level",
        RequirementKind.PreviousQuests => "Previous quests",
        RequirementKind.GrandCompany => "Grand Company",
        RequirementKind.GrandCompanyRank => "Grand Company rank",
        RequirementKind.TribeRank => "Allied society rank",
        RequirementKind.TribeReputation => "Allied society reputation",
        RequirementKind.TribeAllowance => "Allowance",
        RequirementKind.TribeDailyOffer => "Daily offer",
        RequirementKind.DutyCompletion => "Duty",
        RequirementKind.Seasonal => "Seasonal",
        RequirementKind.AcceptCondition => "Condition",
        RequirementKind.Mount => "Mount",
        RequirementKind.House => "House",
        RequirementKind.Achievement => "Achievement",
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
