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
    public const string ConfigSectionHelp = "Help";
    public const string ConfigShowHelp = "Show help";
    public const string ConfigShowHelpOnFirstRun = "Show help on first run";
    public const string ConfigShowHelpOnFirstRunHint = "Opens the help window the next time the main window opens, then turns itself off.";
    public const string ConfigPollTimingNone = "No polls yet";
    /// <summary>{0} = last ms, {1} = average ms, {2} = poll count.</summary>
    public const string ConfigPollTimingFormat = "Last poll: {0:0.00} ms · average {1:0.00} ms · {2:N0} polls";
    /// <summary>{0} = average ms.</summary>
    public const string ConfigPollCostFormat = "Each poll costs about {0:0.0} ms; 1 s is the default and is safe.";
    public const string ConfigPollCostUnknown = "Each poll costs a few milliseconds; 1 s is the default and is safe.";

    /// <summary>Help window text, one array per topic; each element is a paragraph.</summary>
    public static class Help
    {
        public const string WindowTitle = "Tsukimichi Help###TsukimichiHelp";
        public const string ShowAgain = "Show on first run";
        public const string ShowAgainHint = "Open this window by itself the next time the main window opens.";

        public static string TopicName(HelpTopic topic) => topic switch
        {
            HelpTopic.GettingStarted => "Getting started",
            HelpTopic.MoonPhases => "The moon phases",
            HelpTopic.Filters => "Filters and chips",
            HelpTopic.TableAndDetail => "Quest table and detail pane",
            HelpTopic.Moonlit => "Moonlit and confidence",
            HelpTopic.Characters => "Characters and snapshots",
            HelpTopic.Commands => "Commands",
            HelpTopic.Tips => "Tips",
            _ => topic.ToString(),
        };

        public static readonly string[] GettingStarted =
        [
            "Tsukimichi is a quest catalog for every journal type. Open it with /tsukimichi, /tsuki, or from the Dalamud plugin list.",
            "The window has three regions: the navigation on the left with the Journal, Moonlit and Characters tabs, the quest table in the middle and the detail pane on the right.",
            "Journal: pick a section, category or genre in the tree to scope the table. Each node shows done/total and a small moon that fills as you complete it.",
            "Search matches quest names, reward names and numeric ids. Filters opens the filter panel; the chips under the search box show what is active.",
            "Select a quest to see its requirements, rewards, path and giver in the detail pane. Right-click a row for pin, map flag, journal and more.",
            "Log in and the plugin reads the character once a second; states update on their own. A snapshot is kept per character, so you can browse others while logged out.",
            "Reopen this help any time from Settings or with /tsukimichi help.",
        ];

        public static readonly string[] MoonPhasesIntro =
        [
            "A quest's state is a moon phase. The eight glyphs carry the meaning without text; hover any glyph in the window for its name.",
        ];

        public const string PhaseCompletedName = "full moon";
        public const string PhaseCompletedMeaning = "Turned in on this character.";
        public const string PhaseCompletedFilters = "Hidden by Hide completed. State filter: Completed.";
        public const string PhaseAcceptedName = "waxing gibbous, gold ring";
        public const string PhaseAcceptedMeaning = "In the journal now; the detail pane shows the step.";
        public const string PhaseAcceptedFilters = "Included by Available now. State filter: Accepted.";
        public const string PhaseReadyName = "first quarter, glow";
        public const string PhaseReadyMeaning = "Every requirement is met on the current job; go get it.";
        public const string PhaseReadyFilters = "Included by Available now. State filter: Ready.";
        public const string PhaseReadyOtherJobName = "first quarter, silver, gold ring";
        public const string PhaseReadyOtherJobMeaning = "Met on another job; the detail pane names it.";
        public const string PhaseReadyOtherJobFilters = "Included by Available now. State filter: Ready on another job.";
        public const string PhaseDoneThisCycleName = "waning gibbous, silver";
        public const string PhaseDoneThisCycleMeaning = "A repeatable quest already done this daily or weekly cycle.";
        public const string PhaseDoneThisCycleFilters = "Excluded by Available now. State filter: Done this cycle.";
        public const string PhaseBlockedName = "new moon, silver ring";
        public const string PhaseBlockedMeaning = "A requirement is unmet; the next step names it in one clause.";
        public const string PhaseBlockedFilters = "Excluded by Available now. State filter: Blocked.";
        public const string PhaseForeclosedName = "eclipsed";
        public const string PhaseForeclosedMeaning = "Locked out for good, usually by a choice made in another quest.";
        public const string PhaseForeclosedFilters = "Hidden by Hide completed; left out of every total. State filter: Foreclosed.";
        public const string PhaseUnknownName = "veiled";
        public const string PhaseUnknownMeaning = "Not evaluated: no snapshot, or data the plugin cannot read for this character.";
        public const string PhaseUnknownFilters = "Only the State filter hides it.";

        public static readonly string[] Filters =
        [
            "Filters opens a panel in the navigation column. Every active filter shows as a chip under the search box; a chip's × clears it and Reset clears them all.",
            "Hide completed drops Completed and Foreclosed quests. Available now keeps only Ready, Ready on another job and Accepted. Both take per-category overrides, so you can hide completed everywhere except the main scenario.",
            "State lists all eight phases for fine control. Expansion, Level range, Job category, Reward kind (three-state per kind), Repeatable and Seasonal active narrow the table further.",
            "Include Unlisted adds quests with no journal genre: removed, hidden and legacy entries. Settings has a switch for the Unlisted tree node.",
            "Pinned only shows your pins. When nothing matches, the panel names the filters responsible and offers Reset.",
            "Filters, sort and the viewed character are remembered between sessions.",
        ];

        public static readonly string[] TableAndDetail =
        [
            "Columns: state moon, name, level, job, next step, expansion and up to four reward icons. Click a header to sort by name, level, state or expansion; click again to reverse, a third time to return to journal order. Drag headers to reorder, right-click them to hide.",
            "Next step is the first unmet requirement in one clause, such as needs Sworn, you are Trusted.",
            "Right-click a row: pin, flag the giver on the map, open the in-game journal, copy the name, show the path, or link the quest in chat.",
            "The detail pane lists every requirement with ✓ or ✗ and a ▶ on the one that blocks you. Hover a requirement for detail.",
            "Path is the prerequisite chain as a trail of moons, lit where done; click any step to inspect it. Unlocks next lists what this quest opens.",
            "The giver line has map and journal buttons. The provenance line says where the state came from and when: client flags for the live character, the snapshot time otherwise.",
        ];

        public static readonly string[] Moonlit =
        [
            "Moonlit treasures are quests whose reward exists nowhere else: an emote, mount, minion, orchestrion roll, Triple Triad card, duty or feature you can only find on this road.",
            "Unique means the reward has no other source in the game data or the curated lists. A reward that trades, shops or achievements also hand out is not unique.",
            "Confidence badges: static comes from the game data alone; curated was checked by hand and shipped with the plugin; yours is your own override. Hover a badge for its source.",
            "Have shows whether the logged-in character owns the reward. Emotes, mounts, minions, rolls, cards and duties are read from the live client, so other characters show a veiled moon for them.",
            "Not unique (hide) in a row's context menu removes a quest from Moonlit; Mark quest unique in the detail pane adds one, with a note naming the reward.",
            "Restore shipped verdict, in the row's context menu or the detail pane, undoes either. Overrides are stored in user/overrides.json and survive updates.",
        ];

        public static readonly string[] Characters =
        [
            "A snapshot is everything the plugin read from a character: completed quests, journal, job levels, Grand Company, allied societies and unlocked duties. One is kept per character and refreshed while you play, and again on logout.",
            "Pick a stored character in the list to browse the whole catalog as that character, evaluated from its snapshot. The toolbar's sync moon goes veiled and a banner names the snapshot.",
            "The dashboard shows completion per journal section, Moonlit progress, the character's pins, recent activity, job levels grouped by role, and Grand Company and society standings.",
            "Rewards the plugin can only read from the live client show a veiled moon for other characters: a snapshot has no record of them.",
            "Account view at the bottom shows every character's state for the quest selected in the Journal, without switching characters.",
            "Export JSON writes the snapshot to the exports folder in the config directory. Forget deletes a stored character; Settings can delete everything.",
        ];

        public static readonly string[] Commands =
        [
            "/tsukimichi — open or close the window.",
            "/tsuki — the same, shorter.",
            "/tsukimichi search <text> — search and print matching quests to chat as links; /tsukimichi <text> does the same.",
            "/tsukimichi config — open Settings.",
            "/tsukimichi help — open this window.",
            "/tsukimichi glyphs — the glyph sheet: every moon at every size.",
        ];

        public static readonly string[] Tips =
        [
            "• Hide completed plus Available now is the fastest view of what to do next.",
            "• Pin a quest and it stays one click away in the Characters dashboard, with its next step.",
            "• The search box takes a quest id; paste one from a wiki.",
            "• Hover a requirement's ✗ for the exact gap, such as the rank or level you still need.",
            "• The path is clickable: select any earlier step to see its own requirements.",
            "• Scope the tree to a genre and sort by level to run a zone's side quests in order.",
            "• Reward kind filters are three-state: require, exclude or ignore each kind.",
            "• The toolbar's sync moon is full when live and veiled on a snapshot; hover it for the time.",
            "• Chat notices for newly available quests are off by default; turn them on in Settings, main scenario excluded.",
            "• Foreclosed quests are left out of totals, so a category can reach 100% without them.",
            "• Settings shows how long each poll takes; 1 s is the default and is safe.",
            "• Delete all data in Settings removes snapshots, pins and overrides but keeps your settings.",
        ];
    }
}
