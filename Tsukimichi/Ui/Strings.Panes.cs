using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the Moonlit pane, the Characters pane and the config window. The other half of this partial class
/// holds the main window's strings; constant names here are prefixed so the two halves never collide.
/// </summary>
static partial class Strings
{
    // ---- Moonlit pane ----
    public const string MoonlitAllKinds = "All";
    public const string MoonlitNoData = "Reward data is not shipped in this build.";
    public const string MoonlitNothingMatches = "No rewards match.";
    public const string MoonlitHideObtainedLabel = "Hide obtained";
    public const string MoonlitFilterHint = "Filter rewards";
    public const string MoonlitOfflineHint = "Obtained states need the live character.";
    public const string MoonlitColumnObtained = "Have";
    public const string MoonlitColumnReward = "Reward";
    public const string MoonlitColumnKind = "Kind";
    public const string MoonlitColumnQuest = "Quest";
    public const string MoonlitColumnState = "State";
    public const string MoonlitColumnConfidence = "Confidence";
    public const string MoonlitObtainedYes = "Obtained";
    public const string MoonlitObtainedNo = "Not obtained";
    public const string MoonlitObtainedUnknown = "Unknown: not readable for this reward kind or character";
    public const string MoonlitShowInJournal = "Show in Journal";
    public const string MoonlitMarkNotUnique = "Not unique (hide)";
    public const string MoonlitRestoreOverride = "Restore shipped verdict";
    public const string MoonlitConfidenceStatic = "static";
    public const string MoonlitConfidenceCommunity = "community";
    public const string MoonlitConfidenceCurated = "curated";
    public const string MoonlitConfidenceUser = "yours";
    public const string MoonlitSourceUnknown = "Source not recorded";
    public const string MoonlitQuestPrefix = "Quest ";

    /// <summary>Display name of a reward kind, plural, as the Moonlit left column lists them.</summary>
    public static string MoonlitKindName(RewardKind kind) => kind switch
    {
        RewardKind.Item => "Items",
        RewardKind.OptionalItem => "Optional items",
        RewardKind.Emote => "Emotes",
        RewardKind.Action => "Actions",
        RewardKind.GeneralAction => "General actions",
        RewardKind.Instance => "Instances",
        RewardKind.ClassJob => "Jobs",
        RewardKind.Other => "Other",
        RewardKind.ArtifactGear => "Artifact gear",
        RewardKind.Mount => "Mounts",
        RewardKind.Minion => "Minions",
        RewardKind.Orchestrion => "Orchestrion rolls",
        RewardKind.TripleTriadCard => "Triple Triad cards",
        RewardKind.Ornament => "Fashion accessories",
        RewardKind.Barding => "Bardings",
        RewardKind.Hairstyle => "Hairstyles",
        RewardKind.AetherCurrent => "Aether currents",
        RewardKind.BlueMageSpell => "Blue mage spells",
        RewardKind.Trait => "Traits",
        RewardKind.Achievement => "Achievements",
        RewardKind.Title => "Titles",
        RewardKind.DutyUnlock => "Duty unlocks",
        RewardKind.SystemUnlock => "System unlocks",
        _ => kind.ToString(),
    };

    /// <summary>State name for glyph tooltips.</summary>
    public static string MoonlitStateName(QuestState state) => state switch
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

    // ---- Characters pane ----
    public const string CharactersNoneStored = "No characters stored yet. Log in once to capture one.";
    public const string CharactersNoneViewed = "No character. Log in, or pick a stored character on the left.";
    public const string CharactersLive = "Live";
    public const string CharactersSnapshotPrefix = "Snapshot taken ";
    public const string CharactersCompletedSuffix = " quests completed";
    public const string CharactersAcceptedSuffix = " in journal";
    public const string CharactersExport = "Export JSON…";
    public const string CharactersExportedPrefix = "Exported to ";
    public const string CharactersExportFailedPrefix = "Export failed: ";
    public const string CharactersForget = "Forget this character";
    public const string CharactersForgetLiveHint = "The logged-in character is captured again on its next change; log out first.";
    public const string CharactersForgetPopup = "Forget character###TsukimichiForgetCharacter";
    public const string CharactersForgetQuestionPrefix = "Forget ";
    public const string CharactersForgetQuestionSuffix = "? The stored snapshot is deleted. Nothing in the game is changed.";
    public const string CharactersForgetConfirm = "Forget";
    public const string CharactersCancel = "Cancel";
    public const string CharactersJobs = "Job levels";
    public const string CharactersNoJobs = "No job levels recorded.";
    public const string CharactersColumnJob = "Job";
    public const string CharactersColumnLevel = "Level";
    public const string CharactersGrandCompany = "Grand Company";
    public const string CharactersNoGrandCompany = "No Grand Company";
    public const string CharactersRankPrefix = "rank ";
    public const string CharactersTribes = "Allied societies";
    public const string CharactersNoTribes = "No allied society standing recorded.";
    public const string CharactersColumnTribe = "Society";
    public const string CharactersColumnRank = "Rank";
    public const string CharactersColumnReputation = "Reputation";
    public const string CharactersAllowancesPrefix = "Allowances: ";
    public const string CharactersTribeAllowanceSuffix = " society, ";
    public const string CharactersLeveAllowanceSuffix = " leve";
    public const string CharactersAccountView = "Account view";
    public const string CharactersAccountNoQuest = "Select a quest in the Journal to see every character's state for it.";
    public const string CharactersAccountUnknownQuest = "The selected quest is not in the catalog.";
    public const string CharactersColumnCharacter = "Character";
    public const string CharactersColumnState = "State";
    public const string CharactersColumnNextStep = "Next step";
    public const string CharactersSnapshotUnreadable = "snapshot unreadable";
    public const string CharactersLiveMarker = "● ";
    public const string CharactersWorldPrefix = "World ";
    public const string CharactersJobPrefix = "Job ";
    public const string CharactersTribePrefix = "Society ";
    public const string CharactersAgeJustNow = "just now";
    public const string CharactersAgeMinutesSuffix = " min ago";
    public const string CharactersAgeHoursSuffix = " h ago";
    public const string CharactersAgeDaysSuffix = " d ago";

    // Characters dashboard
    public const string CharactersSectionCompletion = "Completion by journal section";
    public const string CharactersNoSections = "Section counts appear once the catalog is built and a character is evaluated.";
    public const string CharactersAllQuests = "All quests";
    public const string CharactersSectionPrefix = "Section ";
    public const string CharactersColumnSection = "Section";
    public const string CharactersColumnDone = "Done";
    public const string CharactersColumnPercent = "%";
    public const string CharactersMoonlitSummary = "Moonlit treasures";
    public const string CharactersMoonlitUnavailable = "Reward data is not available.";
    public const string CharactersColumnKind = "Kind";
    public const string CharactersColumnObtained = "Obtained";
    public const string CharactersPinned = "Pinned quests";
    public const string CharactersNoPins = "No pinned quests. Pin one from the quest table's context menu.";
    public const string CharactersColumnQuest = "Quest";
    public const string CharactersRecent = "Recent activity";
    public const string CharactersRecentNeedsLive = "Activity is recorded for the logged-in character only.";
    public const string CharactersNoRecent = "Nothing yet this session. Turn in or accept a quest and it appears here.";
    public const string CharactersColumnTime = "Time";
    public const string CharactersColumnEvent = "Event";

    /// <summary>Label for a recent-activity row.</summary>
    public static string CharactersEventName(QuestEventKind kind) => kind switch
    {
        QuestEventKind.Completed => "Completed",
        QuestEventKind.Accepted => "Accepted",
        QuestEventKind.Abandoned => "Abandoned",
        QuestEventKind.NewlyAvailable => "Newly available",
        _ => kind.ToString(),
    };

    /// <summary>Group header in the job table.</summary>
    public static string CharactersJobGroupName(CharactersPane.JobGroup group) => group switch
    {
        CharactersPane.JobGroup.Tank => "Tanks",
        CharactersPane.JobGroup.Healer => "Healers",
        CharactersPane.JobGroup.Melee => "Melee DPS",
        CharactersPane.JobGroup.Ranged => "Physical ranged DPS",
        CharactersPane.JobGroup.Caster => "Magical ranged DPS",
        CharactersPane.JobGroup.Crafter => "Disciples of the Hand",
        CharactersPane.JobGroup.Gatherer => "Disciples of the Land",
        _ => "Other",
    };

    // ---- Config window ----
    public const string ConfigSectionPolling = "Polling";
    public const string ConfigPollInterval = "Poll interval";
    public const string ConfigPollIntervalHint = "How often the live character is re-read. Lower is more responsive; 1 s is plenty.";
    public const string ConfigSectionNotices = "Notices";
    public const string ConfigChatNotice = "Chat notice for newly available quests";
    public const string ConfigIncludeMsq = "Include main scenario";
    public const string ConfigSectionJournal = "Journal";
    public const string ConfigShowUnlisted = "Show Unlisted bucket";
    public const string ConfigShowUnlistedHint = "Quests with no journal genre: hidden, removed or legacy entries.";
    public const string ConfigSectionData = "Data";
    public const string ConfigDataRetention = "Tsukimichi keeps one snapshot per character, your pins and your unique-reward overrides in its config directory. Forget a single character from the Characters tab.";
    public const string ConfigDeleteAll = "Delete all Tsukimichi data";
    public const string ConfigDeleteStep1Popup = "Delete all data###TsukimichiDeleteAll1";
    public const string ConfigDeleteStep1Text = "Every stored character snapshot, all pins and all overrides will be removed. Settings stay.";
    public const string ConfigDeleteContinue = "Continue";
    public const string ConfigDeleteStep2Popup = "Confirm deletion###TsukimichiDeleteAll2";
    public const string ConfigDeleteStep2Text = "This cannot be undone. Delete everything?";
    public const string ConfigDeleteConfirm = "Delete everything";
    public const string ConfigDeleteDone = "All Tsukimichi data deleted.";
    public const string ConfigCancel = "Cancel";
    public const string ConfigSectionAbout = "About";
    public const string ConfigPluginVersionPrefix = "Tsukimichi ";
    public const string ConfigGameDataPrefix = "Reward data from game ";
    public const string ConfigGameDataMissing = "Reward data not shipped in this build";
    public const string ConfigGeneratedPrefix = ", generated ";
    public const string ConfigUniqueEntriesSuffix = " unique reward entries";
    public const string ConfigCuratedPrefix = "Curated: ";
    public const string ConfigCuratedSystemSuffix = " system unlocks, ";
    public const string ConfigCuratedDutySuffix = " duty unlocks, ";
    public const string ConfigCuratedFeatureSuffix = " feature quests, ";
    public const string ConfigCuratedFestivalSuffix = " festivals";
    public const string ConfigCatalogPrefix = "Catalog: ";
    public const string ConfigCatalogLoading = "Catalog: loading";
    public const string ConfigCatalogUnavailable = "Catalog unavailable: ";
    public const string ConfigCatalogQuestsSuffix = " quests, ";
}
