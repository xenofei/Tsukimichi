using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the Moonlit, Characters and Flight panes and the config window. The other half of this partial class
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
    /// <summary>Subtitle under the Moonlit pane header; the tab keeps the brand word.</summary>
    public const string MoonlitSubtitle = "rewards only a quest gives";
    public const string MoonlitOfflineHint = "Obtained states need the live character.";
    public const string MoonlitColumnObtained = "Have";
    public const string MoonlitColumnReward = "Reward";
    public const string MoonlitColumnKind = "Kind";
    public const string MoonlitColumnQuest = "Quest";
    public const string MoonlitColumnState = "State";
    public const string MoonlitColumnConfidence = "Confidence";
    public const string MoonlitObtainedYes = "Obtained";
    public const string MoonlitObtainedNo = "Not obtained";
    public const string MoonlitObtainedUnknown = "Not checked: not readable for this reward kind or character";
    public const string MoonlitShowInJournal = "Show in Journal";
    public const string MoonlitMarkNotUnique = "Not unique (hide)…";
    public const string MoonlitRestoreOverride = "Restore shipped verdict";
    public const string MoonlitVerdictPopup = "##moonlitVerdict";
    public const string MoonlitHiddenTooltip = "Hidden by your verdict; right-click for Restore shipped verdict";
    public const string MoonlitConfidenceStatic = "static";
    public const string MoonlitConfidenceCommunity = "community";
    public const string MoonlitConfidenceCurated = "curated";
    public const string MoonlitConfidenceUser = "yours";
    public const string MoonlitSourceUnknown = "Source not recorded";

    // Confidence badge tooltips: what the badge means, with the source under it.
    public const string MoonlitBadgeStatic = "Static: the game data alone marks this reward as found nowhere else";
    public const string MoonlitBadgeCommunity = "Community: reported by players, not yet checked";
    public const string MoonlitBadgeCurated = "Curated: checked by hand and shipped with the plugin";
    public const string MoonlitBadgeUser = "Yours: you marked this quest's reward as unique";
    public const string MoonlitBadgeHidden = "Yours: you hid this reward as not unique";

    /// <summary>Hover text of the veiled stand-in drawn where a reward of a kind without sheet art would show its icon.</summary>
    public const string MoonlitNoIconTooltip = "No icon for this reward kind";

    /// <summary>{0} = the kind row's obtained/total.</summary>
    public const string MoonlitKindCountTooltipFormat = "{0} obtained";
    public const string MoonlitQuestPrefix = "Quest ";

    // Rewards the FFXIV Online Store also sells (curated/online_store.json; entry OtherSources carries OnlineStore)
    public const string MoonlitStoreOnly = "Store only";
    public const string MoonlitStoreOnlyTooltip = "Also sold on the FFXIV Online Store; not exclusive to the quest";
    /// <summary>The same as the second line of the reward tooltip, after "Store only": composed once, since the tooltip draws every hovered frame.</summary>
    public const string MoonlitStoreOnlyTooltipLine = "· " + MoonlitStoreOnlyTooltip;

    // Rewards a duty also drops (curated/other_sources.json; entry OtherSources carries DungeonDrop, OtherSourceNotes the duties)
    public const string MoonlitAlsoDrops = "Also drops";
    private const string AlsoDropsInPrefix = "Also drops in ";
    private const string AlsoDropsInADuty = "Also drops in a duty";
    private const string NotExclusiveSuffix = "; not exclusive to the quest";

    /// <summary>
    /// "Also drops in Snowcloak, …": the line the reward tooltip and the item hover hint show for a reward a duty also
    /// drops; <paramref name="where"/> empty (the data names no duty) reads "Also drops in a duty".
    /// </summary>
    public static string AlsoDropsLine(string? where) =>
        string.IsNullOrWhiteSpace(where) ? AlsoDropsInADuty : AlsoDropsInPrefix + where;

    /// <summary>The "Also drops" mark's tooltip: <see cref="AlsoDropsLine"/> plus "; not exclusive to the quest".</summary>
    public static string MoonlitAlsoDropsTooltip(string? where) => AlsoDropsLine(where) + NotExclusiveSuffix;

    // One toggle hides both (the persisted setting keeps its 0.6.0 name, MoonlitHideStoreResells)
    public const string MoonlitHideStoreResellsLabel = "Hide rewards found elsewhere";
    public const string MoonlitHideStoreResellsTooltip = "Leave out rewards the FFXIV Online Store also sells or a duty also drops; the obtained/total counts leave them out too while this is on";

    // Confidence filter next to "Hide obtained"
    public const string MoonlitConfidenceFilterTooltip = "Show only rows with this confidence, or only rows whose obtained state the plugin cannot read";
    public const string MoonlitConfidenceAny = "Any confidence";
    public const string MoonlitConfidenceStaticOnly = "Static only";
    public const string MoonlitConfidenceCuratedOnly = "Curated only";
    public const string MoonlitConfidenceYoursOnly = "Yours only";
    public const string MoonlitConfidenceUnknownObtained = "Obtained not checked";

    // ---- Flight pane ----
    public const string TabFlight = "Flight";
    public const string FlightNoData = "Aether current data could not be read from the game; the Flight view is empty.";
    public const string FlightNoZone = "Pick a zone on the left.";
    public const string FlightOfflineHint = "Attunement is read for the logged-in character only; quest states come from the viewed character.";
    public const string FlightCurrentZoneMarker = "● ";
    public const string FlightCurrentZoneTooltip = "You are here";
    /// <summary>{0} = quest currents done, {1} = quest currents in the zone.</summary>
    public const string FlightZoneCountFormat = "{0}/{1}";
    /// <summary>{0} = attuned, {1} = total currents, {2} = quest currents done, {3} = quest currents.</summary>
    public const string FlightZoneTooltipFormat = "{0} of {1} currents attuned · {2} of {3} quest currents done";
    /// <summary>{0} = total currents, {1} = quest currents done, {2} = quest currents.</summary>
    public const string FlightZoneTooltipUnknownFormat = "{0} currents · {1} of {2} quest currents done · attunement needs the logged-in character";
    /// <summary>{0} = zone, {1} = attuned, {2} = total currents.</summary>
    public const string FlightHeaderFormat = "Flight in {0}: {1} of {2} currents attuned";
    /// <summary>{0} = zone, {1} = total currents.</summary>
    public const string FlightHeaderUnknownFormat = "Flight in {0}: {1} currents";
    public const string FlightHeaderComplete = "You can fly here.";
    public const string FlightQuestCurrents = "Quest currents";
    public const string FlightColumnAttuned = "Attuned";
    public const string FlightColumnQuest = "Quest";
    public const string FlightColumnState = "State";
    public const string FlightColumnStatus = "Status";
    public const string FlightColumnActions = "##actions";
    public const string FlightAttunedYes = "Attuned";
    public const string FlightAttunedNo = "Not attuned: completing the quest attunes it";
    public const string FlightAttunedUnknown = "Not checked: attunement is read for the logged-in character only";
    public const string FlightQuestClickHint = "Show the quest's requirements and path in the detail pane";
    public const string FlightQuestPrefix = "Quest ";
    public const string FlightFlag = "Flag";
    public const string FlightFlagTooltip = "Flag the quest giver on the map";
    public const string FlightTeleport = "Teleport";
    /// <summary>{0} = attuned field currents, {1} = field currents in the zone.</summary>
    public const string FlightFieldFormat = "Field currents: {0} of {1} attuned · use the Aether Compass (a General Action) to find the rest";
    /// <summary>{0} = field currents in the zone.</summary>
    public const string FlightFieldAllFormat = "Field currents: all {0} attuned";
    /// <summary>{0} = field currents in the zone.</summary>
    public const string FlightFieldUnknownFormat = "Field currents: {0} in this zone · use the Aether Compass (a General Action) to find them";
    public const string FlightFieldNone = "This zone has no field currents; the quests above are all it takes.";
    public const string FlightFieldTooltip = "Field currents are attuned by touching them in the world. The Aether Compass (Actions & Traits › General) points at the nearest one; Tsukimichi does not track their positions.";

    // ---- Discovery commands ----
    public const string ZoneNoCharacter = "No character evaluated yet; log in first.";
    public const string ZoneNoQuests = "No quests to start in this zone.";
    public const string WhichNoTarget = "Target an NPC first.";
    /// <summary>{0} = NPC name.</summary>
    public const string WhichNoQuestsFormat = "{0} starts no quests in the catalog.";
    public const string ChatSuffixSeparator = "  · ";

    // ---- Wotsit entries ----
    public const string WotsitQuestPrefix = "Quest: ";
    public const string WotsitRewardPrefix = "Reward: ";

    // ---- Lifestream teleport (table and detail pane) ----
    public const string TeleportToGiver = "Teleport to giver (Lifestream)";
    public const string TeleportBusy = "Lifestream is busy; wait for it to finish.";
    public const string TeleportNoAetheryte = "No aetheryte is known for the giver's zone.";
    /// <summary>{0} = aetheryte place name.</summary>
    public const string TeleportTooltipFormat = "Teleport to {0}, the aetheryte nearest the giver";

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
    public const string CharactersColumnStatus = "Status";
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
        QuestEventKind.Accepted => "Picked up",
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
    public const string ConfigShowUnlisted = "Show removed quests";
    public const string ConfigShowUnlistedHint = "Adds the Removed from the game node to the tree: quests the game deleted in later patches. They never count toward a total; a completed one still shows Completed.";
    public const string ConfigJournalFiling = "Journal filing";
    public const string ConfigJournalFilingRefiled = "Refiled";
    public const string ConfigJournalFilingLegacy = "Legacy";
    public const string ConfigJournalFilingHint = "Refiled puts the hidden quests the game never lists (class intros, Leves of…, Sights of…, Eureka entry, hidden chain steps) under the genre their links point at, and keeps removed quests out of every total. Legacy shows them as releases before 0.6.1 did, all in one bucket. Changing this rebuilds the catalog.";
    public const string ConfigSectionIntegrations = "Integrations";
    public const string ConfigWotsitIntegration = "Register quests and rewards with Wotsit";
    public const string ConfigWotsitIntegrationHint = "Every quest and Moonlit reward becomes a Wotsit search entry that reveals it in the Journal. Needs Wotsit installed.";
    public const string ConfigNpcContextMenu = "NPC context menu";
    public const string ConfigNpcContextMenuHint = "Target a quest-giving NPC and open the target bar's menu: \"Tsukimichi: quests here (N)\" opens the Journal on that NPC's quests, each with what blocks it. Nothing about the NPC is stored.";

    // ---- /tsuki why ----
    public const string WhyNoSelection = "Select a quest first, or name one: /tsuki why <quest name>";

    /// <summary>Between "talk to &lt;giver&gt;" and the giver's map link on a Ready line.</summary>
    public const string WhyGiverIn = " in ";

    /// <summary>The map link's text on a Ready line: {0} place name, {1} x, {2} y.</summary>
    public const string WhyGiverPlaceFormat = "{0} ({1:0.0}, {2:0.0})";
    public const string ConfigSectionData = "Data";
    public const string ConfigDataRetention = "Tsukimichi keeps one snapshot per character (with when each quest entered the journal and the quests you abandoned), your pins and your unique-reward overrides in its config directory. Forget a single character from the Characters tab.";
    public const string ConfigDeleteAll = "Delete all Tsukimichi data";
    public const string ConfigDeleteStep1Popup = "Delete all data###TsukimichiDeleteAll1";
    public const string ConfigDeleteStep1Text = "Every stored character snapshot, all pins and all overrides will be removed. Settings stay.";
    public const string ConfigDeleteContinue = "Continue";
    public const string ConfigDeleteStep2Popup = "Confirm deletion###TsukimichiDeleteAll2";
    public const string ConfigDeleteStep2Text = "This cannot be undone. Delete everything?";
    public const string ConfigDeleteConfirm = "Delete everything";
    public const string ConfigDeleteDone = "All Tsukimichi data deleted.";
    public const string ConfigCancel = "Cancel";
    /// <summary>{0} = number of stored verdicts.</summary>
    public const string ConfigVerdictsHeaderFormat = "Your Moonlit verdicts ({0})";
    public const string ConfigVerdictsNone = "No verdicts yet. Mark a quest from its detail pane, or hide one from a Moonlit row.";
    public const string ConfigVerdictsUnavailable = "Verdicts are listed once the Moonlit pane has loaded.";
    public const string ConfigVerdictColumnQuest = "Quest";
    public const string ConfigVerdictColumnVerdict = "Verdict";
    public const string ConfigVerdictColumnNote = "Note";
    public const string ConfigVerdictColumnDate = "Date";
    public const string ConfigVerdictColumnRestore = "##restore";
    public const string ConfigVerdictUnique = "unique";
    public const string ConfigVerdictNotUnique = "not unique";
    public const string ConfigVerdictRestore = "Restore";
    public const string ConfigVerdictRestoreTooltip = "Forget this verdict; the shipped reward data applies again";
    public const string ConfigVerdictRestoreAll = "Restore all";
    public const string ConfigVerdictRestoreAllTooltip = "Forget every verdict. Hold Shift and click, or press and hold.";
    public const string ConfigVerdictsRestored = "All verdicts restored; the shipped reward data applies again.";
    public const string ConfigSectionAbout = "About";
    public const string ConfigPluginVersionPrefix = "Tsukimichi ";
    public const string ConfigCuratedPrefix = "Curated: ";
    public const string ConfigCuratedSystemSuffix = " system unlocks, ";
    public const string ConfigCuratedDutySuffix = " duty unlocks, ";
    public const string ConfigCuratedFeatureSuffix = " unlock quests, ";
    public const string ConfigCuratedFestivalSuffix = " festivals";
    public const string ConfigDataStampTooltip = "The game version the reward data was generated from, how many entries and when, and the curated overlay's revision. The same stamp is in every diagnostic block.";
    public const string ConfigCatalogPrefix = "Catalog: ";
    public const string ConfigCatalogLoading = "Catalog: loading";
    public const string ConfigCatalogUnavailable = "Catalog unavailable: ";
    public const string ConfigCatalogQuestsSuffix = " quests, ";
    public const string ConfigSectionDisplay = "Display";
    public const string ConfigUiScale = "Window scale";
    public const string ConfigUiScaleHint = "Text and spacing in Tsukimichi's windows, on top of Dalamud's global scale.";
    public const string ConfigIconScale = "Icon scale";
    public const string ConfigIconScaleHint = "Moon glyphs, reward icons and toolbar buttons.";
    public const string ConfigReduceMotion = "Reduce motion";
    public const string ConfigReduceMotionHint = "No hover fades or pulses anywhere, and hold-to-confirm buttons count down in text instead of filling an arc. Until you change it here, it follows Windows' \"Show animations\" setting.";
    public const string ConfigFollowDalamudColours = "Follow Dalamud colours";
    public const string ConfigFollowDalamudColoursHint = "Draw Tsukimichi's windows in your Dalamud theme's colours instead of the Night palette. The layout, the moons and the gold stay the same.";
    public const string ConfigDensity = "Table rows";
    public const string ConfigDensityComfortable = "Comfortable";
    public const string ConfigDensityDense = "Dense";
    public const string ConfigDensityHint = "Height of the quest table's rows; the Journal tree keeps its size.";
    public const string ConfigSectionHelp = "Help";
    public const string ConfigShowHelp = "Show help";
    public const string ConfigStartTutorial = "Start the tour";
    public const string ConfigOfferTutorial = "Offer the tour on first run";
    public const string ConfigOfferTutorialHint = "Shows the tour's welcome card the next time the main window opens.";
    public const string ConfigPollTimingNone = "No polls yet";
    /// <summary>{0} = last ms, {1} = average ms, {2} = poll count.</summary>
    public const string ConfigPollTimingFormat = "Last poll: {0:0.00} ms · average {1:0.00} ms · {2:N0} polls";
    /// <summary>{0} = average ms.</summary>
    public const string ConfigPollCostFormat = "Each poll costs about {0:0.0} ms; 1 s is the default and is safe.";
    public const string ConfigPollCostUnknown = "Each poll costs a few milliseconds; 1 s is the default and is safe.";

    /// <summary>"What's new" card at the top of the detail column after an update.</summary>
    public static class WhatsNew
    {
        /// <summary>{0} = plugin version.</summary>
        public const string TitleFormat = "What's new in {0}";
        public const string Close = "Close";
        public const string Help = "Help";
        public const string Bullet = "• ";
    }

    /// <summary>
    /// Help window text. Topics are built from small blocks (cards, steps, tips, key caps), so each block's text is
    /// its own constant or array element; every paragraph stays under sixty words.
    /// </summary>
    public static class Help
    {
        public const string WindowTitle = "Tsukimichi Help###TsukimichiHelp";
        public const string SearchHint = "Search help";
        public const string NoTopicMatches = "No topic matches.";
        public const string TryIt = "Try it";
        public const string OpenSettings = "Open settings";
        public const string ShownBy = "Shown by";

        public static string TopicName(HelpTopic topic) => topic switch
        {
            HelpTopic.QuickStart => "Quick start",
            HelpTopic.MoonPhases => "The moon phases",
            HelpTopic.Filters => "Filters and chips",
            HelpTopic.ReadingAQuest => "Reading a quest",
            HelpTopic.Moonlit => "Moonlit treasures",
            HelpTopic.Characters => "Characters and snapshots",
            HelpTopic.Flight => "Flight and nearby",
            HelpTopic.Commands => "Commands",
            HelpTopic.CountsDiffer => "Why my counts differ from the journal",
            HelpTopic.KnownQuirks => "Known quirks",
            HelpTopic.Spoilers => "Spoilers",
            HelpTopic.Tips => "Tips",
            _ => topic.ToString(),
        };

        /// <summary>One sentence under the topic title.</summary>
        public static string TopicLede(HelpTopic topic) => topic switch
        {
            HelpTopic.QuickStart => "From an empty window to a plan for the evening.",
            HelpTopic.MoonPhases => "A quest's state is a moon phase; eight glyphs carry the meaning without text.",
            HelpTopic.Filters => "Narrow the table, see what is narrowing it, and clear it with one click.",
            HelpTopic.ReadingAQuest => "The detail pane answers what blocks a quest, what leads to it and what it opens.",
            HelpTopic.Moonlit => "Rewards that exist nowhere else, with how sure the plugin is about each.",
            HelpTopic.Characters => "One snapshot per character keeps the whole account in view, even logged out.",
            HelpTopic.Flight => "Which quests stand between you and flying, and what you can start where you are.",
            HelpTopic.Commands => "Everything the chat command can do.",
            HelpTopic.CountsDiffer => "Four reasons a done/total here is not the number in the game's journal or on a wiki.",
            HelpTopic.KnownQuirks => "Things the plugin gets wrong on purpose or cannot know yet, so you need not report them.",
            HelpTopic.Spoilers => "The spoiler shield keeps the story ahead of you out of sight, on by default.",
            HelpTopic.Tips => "Small habits that make the catalog faster.",
            _ => string.Empty,
        };

        // ---- Quick start: the "Try it" action of each step is attached by the window ----
        public const string StepOpenTitle = "Open the window";
        public const string StepOpenBody = "Type /tsukimichi or /tsuki, or pick Tsukimichi in the plugin list. The tabs run down the left edge beside the journal tree, the quest table sits in the middle and the detail pane on the right.";
        public const string StepFindTitle = "Find what you can do now";
        public const string StepFindBody = "Search by quest, reward or id (Ctrl+F), or pick a quick view on the toolbar: Unlocks, My level, Stalled, Story sidequests or Sprout mode. All turns the view off.";
        public const string StepFiltersTitle = "Narrow with filters";
        public const string StepFiltersBody = "Filters opens a panel beside the tree; the number on the button counts the filters narrowing the table. Each one shows as a chip under the toolbar, the tree scope first; click a chip to clear it.";
        public const string StepReadTitle = "Read why a quest is locked";
        public const string StepReadBody = "Select a row: the detail pane lists every requirement and marks the one that blocks you, and the Status column says it in one clause. The moon's shape is its state.";
        public const string StepMoonlitTitle = "Find Moonlit treasures";
        public const string StepMoonlitBody = "The Moonlit tab lists quests whose reward exists nowhere else, grouped by kind, with whether you already have each one.";
        public const string StepCharactersTitle = "Browse other characters";
        public const string StepCharactersBody = "The Characters tab keeps a snapshot per character. Pick one to browse the whole catalog as that character, with a dashboard of its progress.";
        public const string StepFlightTitle = "Unlock flying";
        public const string StepFlightBody = "The Flight tab lists every zone you can fly in. Pick one to see its quest currents, the quest that blocks each and where to fly next.";
        public const string StepTourTitle = "Take the tour";
        public const string StepTourBody = "The tour has three short chapters, Find, Read and Beyond, each pointing at the real window. Jump between chapters on the card; Enter moves on and Esc closes.";
        public const string QuickStartTip = "Select any row to read its requirements, path and giver in the detail pane. Right-click a row for pin, map flag and journal.";
        public const string QuickStartSettingsTip = "Text too small? Settings has a Display section with a window scale and an icon scale.";

        // ---- Moon phases ----
        // The moon-phase name under each state comes from Strings.StateGlyphSubtitle; only the meanings live here.
        public const string PhaseCompletedMeaning = "Turned in on this character.";
        public const string PhaseAcceptedMeaning = "In the journal now; the detail pane shows the step.";
        public const string PhaseReadyMeaning = "Every requirement is met on the current job; go get it.";
        public const string PhaseReadyOtherJobMeaning = "Met on another job; the detail pane names it.";
        public const string PhaseDoneThisCycleMeaning = "A repeatable quest already turned in since its last reset: Done today for a daily, Done this week for a weekly.";
        public const string PhaseBlockedMeaning = "A requirement is unmet; Status names it in one clause.";
        public const string PhaseForeclosedMeaning = "Locked out for good, usually by a choice made in another quest; Status names the cause.";
        public const string PhaseUnknownMeaning = "Not evaluated: no snapshot, or data the plugin cannot read for this character.";

        // "Shown by" chips: what must be set for the phase to appear in the table.
        public const string ChipHideCompletedOff = "Hide completed off";
        public const string ChipAvailableNow = "Available now";
        public const string ChipAvailableNowOff = "Available now off";
        public const string ChipNotInTotals = "Left out of totals";
        public const string ChipStatePrefix = "State: ";

        public const string StripeTitle = "The table's state stripe";
        public const string StripeBody = "The thin bar on the left edge of each quest table row repeats the row's state as a pattern, so it reads without colour too: in a greyscale stream, through a colour filter or with colour blindness. Hover the stripe for the state and the pattern's name.";

        public const string FillingTitle = "The halo";
        public const string FillingBody = "Tree nodes, Moonlit kinds, flying zones and the Characters dashboard show a ring that fills clockwise from the top with the completion ratio, around a small moon that fills with it. Even one quest shows a gold pip, and the ring closes only when everything is done. Where the ring is small the number sits beside it. Locked out quests are left out of the total.";

        // ---- Filters and chips ----
        public static readonly string[] FilterCardTitles =
        [
            "Hide completed",
            "Available now",
            "State",
            "More filters",
            "Quick views",
            "Chips",
            "Nothing matches",
        ];

        public static readonly string[] FilterCardBodies =
        [
            "Drops Completed and Locked out quests. Per-category overrides let you hide completed everywhere except, say, the main scenario.",
            "Keeps only Ready, Ready on another job and In journal: the quests you can act on now. Takes the same per-category overrides.",
            "All eight phases as checkboxes for fine control. Untick a phase to hide its quests.",
            "Expansion, Added in (the patch series a quest came with: 7.5x is 7.5, 7.51, 7.55 and the rest, newest first), level range, job category, reward kind (three-state per kind), Repeatable, Seasonal active, Pinned only and Abandoned only narrow the table further; Include removed widens it to quests the game deleted.",
            "One-click views on the toolbar, with All first to turn them off: Unlocks, My level, Stalled, Story sidequests and Sprout mode; the filter panel keeps the number of days Stalled waits. Unlocks opens with the unlock quests of the newest patch series (\"New in 7.5x\": 7.5, 7.51, 7.55 and the rest), above a gold line; the detail pane says which patch any quest came with (\"Added in 7.5\"). Story sidequests lists the sidequests with journal artwork, the ones that tell a small story, zone by zone, with each side story in the order you play it; a book after the name marks them in any view, and its tooltip says which story and how far in (\"3 of 9\"). The detail pane shows the story's progress as \"Story: <first quest> · 3 of 9 done · next: …\". The blue aether current quests that open a zone's story lines are part of it; other blue unlock quests (dungeons, systems, jobs) are left out.",
            "While something narrows the table, a row of chips sits under the toolbar: the tree scope first, then one chip per filter, which the Filters button's number counts. Click a chip to clear it; Reset clears them all and the search.",
            "When the table empties, it names the filters responsible as chips: click one to clear only that filter, or Reset filters to clear them all.",
        ];

        public const string FiltersTip = "Filters, sort and the viewed character are remembered between sessions.";

        // ---- Reading a quest ----
        public static readonly string[] QuestCardTitles =
        [
            "Requirements",
            "Path",
            "Unlocks next",
            "Action bar",
            "Provenance",
        ];

        public static readonly string[] QuestCardBodies =
        [
            "The first card under the quest's picture: every requirement with ✓ or ✗. The one that blocks you has a gold bar and a gold label; hover a ✗ for the exact gap, such as the rank or level you still need. A \"Note:\" under the list explains the few quests the game offers anyway.",
            "The prerequisite chain as a star chart: a moon per quest on a thread that is gold where you have walked and dashed where you have not, one band per expansion. Finished stretches fold into a bead (\"12 moons walked\"); click it to open them. Where a quest accepts either of two previous quests, the other way in is a hollow moon beside the thread; click it to see its own path. This quest is the large moon with the halo; when it is scrolled out of view, the \"target\" pill brings it back. Click any step to inspect it.",
            "Under this quest the thread branches into what it opens once turned in. Click an entry to jump to it.",
            "Along the bottom of the pane: Flag on map (Teleport instead when Lifestream is installed), then Pin, Show path, Link in chat, Copy coordinates, Open journal and Report. Every button works from the keyboard; a greyed one says why on hover. The Giver card above names the NPC, the zone and the coordinates.",
            "The last line says where the state came from: \"Checked just now · live\" for the character you are logged in as, \"From Michiru's snapshot, 2 d ago\" for a stored one, or \"Log in to check this quest\" before the first login.",
        ];

        public const string QuestTip = "With no quest selected, the pane shows Tonight: how many quests you can pick up now (Show them filters the table to them), the next main scenario quest and what blocks it, the events running now and your pinned quests that are ready. A table row's right-click menu has pin, map flag, journal, copy name, show path and chat link too.";

        // ---- Moonlit treasures ----
        public const string UniqueTitle = "What unique means";
        public const string UniqueBody = "A Moonlit treasure is a reward that exists nowhere else: an emote, mount, minion, roll, card, duty or feature you can only find on this road. Anything trades, shops or achievements also hand out is not unique. A few rewards the Online Store also sells or a duty also drops stay listed with a \"Store only\" or \"Also drops\" mark (hover it for where); tick \"Hide rewards found elsewhere\" to leave them out of the list and the counts.";
        public const string ConfidenceTitle = "Confidence badges";
        public const string ConfidenceBody = "Each row carries a badge saying how the verdict was reached. Hover a badge in the table for its source.";
        public const string ConfidenceStaticMeaning = "from the game data alone";
        public const string ConfidenceCommunityMeaning = "reported by players, not yet checked";
        public const string ConfidenceCuratedMeaning = "checked by hand and shipped with the plugin";
        public const string ConfidenceUserMeaning = "your own override";
        public const string HaveTitle = "Have";
        public const string HaveBody = "Whether the viewed character owns the reward. Emotes, mounts, minions, rolls, cards and duties are read from the live client, so a stored snapshot shows a dash for them.";
        public const string OverridesTitle = "Overrides";
        public const string OverridesBody = "Not unique (hide) in a row's context menu removes a quest from Moonlit; Mark as unique in the detail pane adds one, with a note naming the reward. Both ask first: press and hold the confirm button until its arc closes, or hold Shift and click. Enter in the note field confirms too. An Undo line stays for eight seconds.";
        public const string RestoreTitle = "Restore";
        public const string RestoreBody = "Restore shipped verdict, in the row's context menu or the detail pane, undoes either. Hidden quests stay listed under the Yours confidence filter, struck through, so they can be restored; Settings › Data lists every verdict with Restore and Restore all. Overrides live in user/overrides.json and survive updates.";

        // ---- Characters and snapshots ----
        public static readonly string[] CharacterCardTitles =
        [
            "Snapshot",
            "View another character",
            "Dashboard",
            "Account view",
            "Export and forget",
            "Abandoned quests",
            "Seasonal events",
        ];

        public static readonly string[] CharacterCardBodies =
        [
            "Everything read from a character: completed quests, journal, job levels, Grand Company, allied societies and unlocked duties. One per character, refreshed while you play and again on logout.",
            "Pick a stored character to browse the catalog as that character. The toolbar's live pip turns hollow and a banner names the snapshot.",
            "Completion per journal section, Moonlit progress, pins, recent activity, job levels by role, Grand Company and society standings.",
            "At the bottom: every character's state for the quest selected in the Journal, without switching characters.",
            "Export JSON writes the snapshot to the exports folder in the config directory. For a spreadsheet or a collection tracker, Settings › Data › Export (or /tsuki export) writes the completed quests or the Moonlit collection as JSON or CSV, without your name unless you tick Include character name; nothing is ever uploaded. Forget deletes a stored character. Settings can delete everything.",
            "A quest that leaves your journal without being completed is listed under Abandoned (N) with the step it had reached and when (\"step 3 of 5 · 2 days ago\"), with Flag, Teleport and Reveal to go and take it up again; Show in Journal turns on the Abandoned filter. Chat says \"Abandoned: [quest] (step 3 of 5)\" the moment it happens (Settings › Notices). Taking the quest up again or completing it clears the row.",
            "While a seasonal event runs, Seasonal events lists its quests with their moon, state and giver, and the Todo overlay shows the ones you can start or have in your journal under Event quests running now. At login, chat says \"Moonfire Faire is running: 2 quests ready\" once per event (Settings › Notices). An end date appears (\"ends Aug 28 (Lodestone)\") only when the Lodestone announced it; otherwise it says running now, never a guess. Completed seasonal quests by year keeps your history; the year is the one the Lodestone gave the event, or counted from the nearest announced one.",
        ];

        // ---- Flight and nearby ----
        public static readonly string[] FlightCardTitles =
        [
            "Flight tab",
            "Quest and field currents",
            "Nearby quests window",
            "Server bar entry",
            "/tsuki zone and /tsuki which",
        ];

        public static readonly string[] FlightCardBodies =
        [
            "Every zone you can fly in, under its expansion, with a halo that fills as you attune its currents; the zone you stand in is marked ● and selected first. Pick a zone and the table lists its quest currents: attuned or not, the quest's state and status, and Flag or Teleport to the giver.",
            "Quest currents come from quests, five per zone in most expansions, and completing the quest attunes them. Field currents are touched in the world; the tab counts them but never locates them. Use the Aether Compass, a General Action under Actions & Traits, to point at the nearest one.",
            "/tsuki nearby opens a small window with the quests you can start in the current zone: state moon, level and job. A click on the name shows it in the Journal, a double-click flags the giver on the map, and right-click or … opens Flag, Teleport and Link in chat. Also in your journal here folds out the quests in your journal whose giver stands in the zone. The cog holds its settings.",
            "☾ N in the server info bar is the count of quests you can start here. Hover it for the first five names; click it to open Nearby quests. It hides at zero unless Keep the entry visible is on, and the cog in Nearby quests can turn it off entirely.",
            "/tsuki zone prints chat links for the quests you can start in the current zone, by level, up to ten. /tsuki which prints every quest the targeted NPC hands out with its state. Both need an evaluated character; the Nearby quests window keeps the same list on screen.",
        ];

        // ---- Commands ----
        public static readonly string[] CommandKeys =
        [
            "/tsukimichi",
            "/tsuki",
            "/tsukimichi search <text>",
            "/tsukimichi settings",
            "/tsukimichi help",
            "/tsukimichi glyphs",
            "/tsuki zone",
            "/tsuki which",
            "/tsuki why [quest name]",
            "/tsuki nearby",
            "/tsuki todo",
            "/tsuki report [quest name]",
            "/tsuki export [quests|moonlit] [json|csv]",
            "Ctrl+F",
            "Esc",
            "Menu key, Shift+F10, …",
            "Ctrl+1 to 4, F, Enter, P",
        ];

        public static readonly string[] CommandMeanings =
        [
            "open or close the window",
            "the same, shorter; every subcommand works with either",
            "search and print matching quests to chat as links; /tsukimichi <text> does the same",
            "open Settings (/tsukimichi config does the same)",
            "open this window",
            "the glyph sheet: every moon at every size",
            "quests you can start in the current zone, as chat links by level",
            "every quest the targeted NPC hands out, with its state",
            "why the selected quest (or the named one) is not offered: its state and blocker, then one line per requirement with met or unmet and the values compared, then the curated note where the game is known to behave differently; a Ready quest says whom to talk to, with a map link. The same list opens in the Journal from an NPC's target-bar menu (\"Tsukimichi: quests here\")",
            "open or close the Nearby quests window: what you can start in the current zone",
            "show or hide the todo overlay: pins, Event quests running now, Unlocks you can start here, the next main scenario quest and job quests",
            "copy a diagnostic block for the selected quest (or the named one) to the clipboard, ready to paste into a GitHub issue; the Report button in the detail pane does the same",
            "write the viewed character's completed quests, its Moonlit collection, or both to the exports folder as JSON or CSV (the Settings format when none is named) and print where; see docs/export-format.md. No content id, account or world, and the name only when Settings › Data › Export › Include character name is ticked",
            "put the caret in the search, while the window has focus",
            "close the open menu or the filter panel; with neither open, close the window",
            "open the focused row's menu in the quest table and the Moonlit list, the same menu a right-click opens; the … button at a row's right end (shown on hover or focus) opens it with a left click",
            "off unless turned on in Settings › Keyboard: switch tabs, flag the selected quest's giver, show it in the Journal, pin it. The game sees these keys too: Ctrl+1 to 4 are hotbar 2 by default",
        ];

        // ---- Why my counts differ from the journal ----
        public static readonly string[] CountsCardTitles =
        [
            "Removed and hidden quests",
            "Seasonal quests out of season",
            "Locked-out choices",
            "Unlock quests is derived",
            "Repeatables count once",
        ];

        public static readonly string[] CountsCardBodies =
        [
            "Quests the game removed in later patches (the A Realm Reborn trim in 5.3, the Summoner rework, the Crystal Tower rewrite, a few deleted sidequests) sit under the Removed from the game node, off by default, and never count toward any total; a completed one still shows Completed when the node or Include removed reveals it, and its detail pane says which patch removed it where that is known. Quests the game's journal hides but still hands out (So You Want to Be a Gladiator, Leves of Kugane, Sights of the North, the Eureka entry quests, hidden steps of YoRHa and the Resistance Weapons) are filed under the genre their links point at, so a class's quests start with its intro and a zone's sidequests include its leve unlock; the detail pane says which rule filed them. The class intros (So You Want to Be a Gladiator and the other A Realm Reborn classes) are listed but never counted: the class you started as hands you \"Way of the …\" directly and never offers its intro, so counting it would keep that class one short for good; the job intros (A Dark Spectacle, So You Want to Be a Machinist, What's Your Sign) count as usual. Settings › Display › Journal filing › Legacy puts every one of them back in the single bucket releases before 0.6.1 showed.",
            "A seasonal quest of an event that is not running shows Blocked with \"seasonal event not active\", and is left out of its genre's total the way a locked-out quest is, so the halo can close without it; it counts again while the event runs. An event whose end date is known and past locks its quests out for good.",
            "A quest locked out by a choice, such as the other two Grand Companies' quests once you have joined one, is Locked out and left out of the total. A category can reach 100% with them undone, while a wiki's count per genre includes them.",
            "Unlock quests is not a journal category. It gathers every quest the game draws with the blue + icon, the curated duty and system unlocks, and quests that reward a duty, job, action, trait, aether current or blue magic spell; main scenario and repeatable quests are left out. Its total matches no page of the journal and moves when the curated lists do.",
            "A daily or weekly quest is one row and one count however many times you have turned it in. Done today or Done this week marks the ones already handed in; they are still counted as completed.",
        ];

        public const string CountsTip = "The State filter shows locked-out and seasonal quests again; the Removed from the game node and Include removed reveal the deleted ones. Both change the counts while they are on.";

        // ---- Known quirks ----
        public static readonly string[] QuirkCardTitles =
        [
            "Steps the game skips",
            "\"Bloodsworn\" reads \"Allied\"",
            "Prerequisites that moved in 7.5",
            "Delivery ranks and event chapters",
            "Store re-sells in Moonlit",
            "Game hooks paused after a patch",
        ];

        public static readonly string[] QuirkCardBodies =
        [
            "The plugin lists every prerequisite the game's data records. For a few quests the game waives one: Up in Arms is optional once the Zenith is in hand, so what follows it is offered while Tsukimichi still shows Up in Arms undone. The known cases carry a curated note: the detail pane shows it under the requirements as \"Note: …\", /tsuki why prints it and Report this quest includes it. When an NPC offers a quest shown Blocked here and there is no note, use Report this quest so the exception can be added.",
            "Patch 7.0 renamed beast tribes to allied societies and the top rank from Bloodsworn to Allied. Tsukimichi uses the current names, so a requirement reads \"Allied\" where an older guide, or quest text written before 7.0, says \"Bloodsworn\". They are the same rank; the quest that awards it carries a note saying so.",
            "Patch 7.5 changed the prerequisite of eleven crafter and gatherer sidequests (the Splendorous tools, Cosmic Exploration and the Kugane, Crystarium, Old Sharlayan and Tuliyollal scrip exchanges among them) from Go West, Craftsman to Inscrutable Tastes, a level 50 quest from Morgayne in Foundation. Tsukimichi follows the current game data, so it lists Inscrutable Tastes where an older guide names Go West, Craftsman; each affected quest carries a note with the patch notes as evidence.",
            "A custom delivery client's satisfaction rank, the Delivery Moogle's carrier level and, for a phased seasonal event, the running chapter are judged since 0.6.2: a quest that needs rank 4 with M'naago, carrier level 7 or a chapter that has not opened yet shows Blocked with that reason. Only the seven events whose quests open on different phases (Hatching-tide 2014 among them) are gated by chapter; every other seasonal quest is Ready whenever its event runs, whatever phase the game reports. A character file written by an older version carries none of these values, so its quests read as before until the next capture. The game keeps the event chapter in three places and it is not yet known which one the quest givers follow; the plugin reads the first (GameMain) and writes all three to the Dalamud log, once per character and again whenever an event starts, ends or changes phase, under \"[festival probe]\". If a chapter gate looks wrong during an event, send that log line with the report.",
            "Some past seasonal rewards (minions, emotes, mounts, bardings, orchestrion rolls, ornaments) are sold again on the Online Store, which the game files cannot know. Moonlit says so on the rows the curated list covers; if you find one it does not, open a data correction issue with the store page as evidence.",
            "The item tooltip panel, the item and NPC menu entries and the server info bar entry sit beside the game's own interface, which a patch can move. Each Tsukimichi release records the game version they were tested on; on a newer game they pause until an update is tested there, and a chat line at login and a notice in Settings › Integrations say so. The Journal, Moonlit, Characters, Nearby, the Todo overlay and every /tsuki command work as usual. To use the hooks anyway, tick Settings › Integrations › \"Enable game hooks on this untested version\"; it covers the game version you are on now only, so the next patch pauses them again. If one then draws in the wrong place, untick it and it stops at once.",
        ];

        public const string QuirksTip = "A quest in the wrong state that is not one of these? Report this quest in the detail pane copies a diagnostic block with the quest id, the state and every requirement's verdict, and no character identifiers; paste it into a GitHub issue.";

        // ---- Spoilers ----
        public static readonly string[] SpoilerCardTitles =
        [
            "Main scenario names ahead of you",
            "Journal artwork",
            "Sprout mode",
            "Your settings, per character",
        ];

        public static readonly string[] SpoilerCardBodies =
        [
            "A main scenario quest more than a set number of quests past your current one (three by default) reads \"Main scenario quest (Lv 83)\" everywhere a name prints: the table, the detail pane, the status bar, the Todo overlay, Characters, chat links, Wotsit and the diagnostic block. Search finds it only by that placeholder. Quests you have accepted or completed always show their names. Reveal this name in the detail pane shows one quest's name until the plugin reloads.",
            "A quest's banner art sums up the quest, so it shows only once the quest is in your journal or done; until then the detail pane and the name tooltip show a card saying so.",
            "The Sprout mode quick view in the filter panel keeps the table to the expansions your main scenario has reached, with a count of the quests in your reach, and folds the tree's later sections to their counts.",
            "Settings › Spoilers turns the names or the artwork off, sets how many quests ahead keep their names (0 to 10), and holds an override for the character shown: a character who finished the story can show everything while an alt stays shielded.",
        ];

        public const string SpoilersTip = "Without a character (or a stored one with no data yet) there is no position to measure from, so every main scenario name after the first quest stays hidden until the first capture.";

        // ---- Tips ----
        public static readonly string[] Tips =
        [
            "Hide completed plus Available now is the fastest view of what to do next.",
            "Pin a quest and it stays one click away in the Characters dashboard, with its status.",
            "The search box takes a quest id; paste one from a wiki.",
            "Hover a requirement's ✗ for the exact gap, such as the rank or level you still need.",
            "The path is clickable: select any earlier step to see its own requirements.",
            "Scope the tree to a genre and sort by level to run a zone's side quests in order.",
            "Reward kind filters are three-state: require, exclude or ignore each kind.",
            "The toolbar's pip is filled when live and hollow on a snapshot; hover it for the time.",
            "Chat notices for newly available quests are off by default; turn them on in Settings, main scenario excluded.",
            "Locked out quests are left out of totals, so a category can reach 100% without them.",
            "Settings shows how long each poll takes; 1 s is the default and is safe.",
            "Delete all data in Settings removes snapshots, pins and overrides but keeps your settings.",
        ];
    }
}
