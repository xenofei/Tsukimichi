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

    // Confidence filter next to "Hide obtained"
    public const string MoonlitConfidenceFilterTooltip = "Show only rows with this confidence, or only rows whose obtained state the plugin cannot read";
    public const string MoonlitConfidenceAny = "Any confidence";
    public const string MoonlitConfidenceStaticOnly = "Static only";
    public const string MoonlitConfidenceCuratedOnly = "Curated only";
    public const string MoonlitConfidenceYoursOnly = "Yours only";
    public const string MoonlitConfidenceUnknownObtained = "Unknown obtained";

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
    public const string FlightColumnNextStep = "Next step";
    public const string FlightColumnActions = "##actions";
    public const string FlightAttunedYes = "Attuned";
    public const string FlightAttunedNo = "Not attuned: completing the quest attunes it";
    public const string FlightAttunedUnknown = "Unknown: attunement is read for the logged-in character only";
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
    public const string ConfigSectionIntegrations = "Integrations";
    public const string ConfigWotsitIntegration = "Register quests and rewards with Wotsit";
    public const string ConfigWotsitIntegrationHint = "Every quest and Moonlit reward becomes a Wotsit search entry that reveals it in the Journal. Needs Wotsit installed.";
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
    public const string ConfigSectionDisplay = "Display";
    public const string ConfigUiScale = "Window scale";
    public const string ConfigUiScaleHint = "Text and spacing in Tsukimichi's windows, on top of Dalamud's global scale.";
    public const string ConfigIconScale = "Icon scale";
    public const string ConfigIconScaleHint = "Moon glyphs, reward icons and toolbar buttons.";
    public const string ConfigSectionHelp = "Help";
    public const string ConfigShowHelp = "Show help";
    public const string ConfigStartTutorial = "Start tutorial";
    public const string ConfigOfferTutorial = "Offer the tutorial on first run";
    public const string ConfigOfferTutorialHint = "Shows the tour's welcome card the next time the main window opens.";
    public const string ConfigPollTimingNone = "No polls yet";
    /// <summary>{0} = last ms, {1} = average ms, {2} = poll count.</summary>
    public const string ConfigPollTimingFormat = "Last poll: {0:0.00} ms · average {1:0.00} ms · {2:N0} polls";
    /// <summary>{0} = average ms.</summary>
    public const string ConfigPollCostFormat = "Each poll costs about {0:0.0} ms; 1 s is the default and is safe.";
    public const string ConfigPollCostUnknown = "Each poll costs a few milliseconds; 1 s is the default and is safe.";

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
            HelpTopic.Tips => "Tips",
            _ => topic.ToString(),
        };

        /// <summary>One sentence under the topic title.</summary>
        public static string TopicLede(HelpTopic topic) => topic switch
        {
            HelpTopic.QuickStart => "Six steps from an empty window to a plan for the evening.",
            HelpTopic.MoonPhases => "A quest's state is a moon phase; eight glyphs carry the meaning without text.",
            HelpTopic.Filters => "Narrow the table, see what is narrowing it, and clear it with one click.",
            HelpTopic.ReadingAQuest => "The detail pane answers what blocks a quest, what leads to it and what it opens.",
            HelpTopic.Moonlit => "Rewards that exist nowhere else, with how sure the plugin is about each.",
            HelpTopic.Characters => "One snapshot per character keeps the whole account in view, even logged out.",
            HelpTopic.Flight => "Which quests stand between you and flying, and what you can start where you are.",
            HelpTopic.Commands => "Everything the chat command can do.",
            HelpTopic.Tips => "Small habits that make the catalog faster.",
            _ => string.Empty,
        };

        // ---- Quick start: the "Try it" action of each step is attached by the window ----
        public const string StepOpenTitle = "Open the window";
        public const string StepOpenBody = "Type /tsukimichi or /tsuki, or pick Tsukimichi in the plugin list. The navigation sits on the left, the quest table in the middle and the detail pane on the right.";
        public const string StepFiltersTitle = "Narrow with filters";
        public const string StepFiltersBody = "Filters opens a panel beside the tree. Hide completed and Available now are the two you will use most; every active filter shows as a chip under the search box.";
        public const string StepMoonlitTitle = "Find Moonlit treasures";
        public const string StepMoonlitBody = "The Moonlit tab lists quests whose reward exists nowhere else, grouped by kind, with whether you already have each one.";
        public const string StepCharactersTitle = "Browse other characters";
        public const string StepCharactersBody = "The Characters tab keeps a snapshot per character. Pick one to browse the whole catalog as that character, with a dashboard of its progress.";
        public const string StepFlightTitle = "Unlock flying";
        public const string StepFlightBody = "The Flight tab lists every zone you can fly in. Pick one to see its quest currents, the quest that blocks each and where to fly next.";
        public const string StepTourTitle = "Take the tour";
        public const string StepTourBody = "The interactive tour points at each part of the window in turn and explains it in a sentence or two.";
        public const string QuickStartTip = "Select any row to read its requirements, path and giver in the detail pane. Right-click a row for pin, map flag and journal.";
        public const string QuickStartSettingsTip = "Text too small? Settings has a Display section with a window scale and an icon scale.";

        // ---- Moon phases ----
        public const string PhaseCompletedName = "full moon";
        public const string PhaseCompletedMeaning = "Turned in on this character.";
        public const string PhaseAcceptedName = "waxing gibbous, gold ring";
        public const string PhaseAcceptedMeaning = "In the journal now; the detail pane shows the step.";
        public const string PhaseReadyName = "first quarter, glow";
        public const string PhaseReadyMeaning = "Every requirement is met on the current job; go get it.";
        public const string PhaseReadyOtherJobName = "first quarter, silver, gold ring";
        public const string PhaseReadyOtherJobMeaning = "Met on another job; the detail pane names it.";
        public const string PhaseDoneThisCycleName = "waning gibbous, silver";
        public const string PhaseDoneThisCycleMeaning = "A repeatable quest already done this daily or weekly cycle.";
        public const string PhaseBlockedName = "new moon, silver ring";
        public const string PhaseBlockedMeaning = "A requirement is unmet; the next step names it in one clause.";
        public const string PhaseForeclosedName = "eclipsed";
        public const string PhaseForeclosedMeaning = "Locked out for good, usually by a choice made in another quest.";
        public const string PhaseUnknownName = "veiled";
        public const string PhaseUnknownMeaning = "Not evaluated: no snapshot, or data the plugin cannot read for this character.";

        // "Shown by" chips: what must be set for the phase to appear in the table.
        public const string ChipHideCompletedOff = "Hide completed off";
        public const string ChipAvailableNow = "Available now";
        public const string ChipAvailableNowOff = "Available now off";
        public const string ChipNotInTotals = "Left out of totals";
        public const string ChipStatePrefix = "State: ";

        public const string FillingTitle = "The filling moon";
        public const string FillingBody = "Tree nodes, Moonlit kinds and the Characters dashboard show a moon whose lit fraction is the completion ratio: new at none, half at half, full only when everything is done. Foreclosed quests are left out of the total.";

        // ---- Filters and chips ----
        public static readonly string[] FilterCardTitles =
        [
            "Hide completed",
            "Available now",
            "State",
            "More filters",
            "Chips",
            "Nothing matches",
        ];

        public static readonly string[] FilterCardBodies =
        [
            "Drops Completed and Foreclosed quests. Per-category overrides let you hide completed everywhere except, say, the main scenario.",
            "Keeps only Ready, Ready on another job and Accepted: the quests you can act on now. Takes the same per-category overrides.",
            "All eight phases as checkboxes for fine control. Untick a phase to hide its quests.",
            "Expansion, level range, job category, reward kind (three-state per kind), Repeatable, Seasonal active and Include Unlisted narrow the table further.",
            "Every active filter shows as a chip under the search box. Click a chip to clear it; Reset clears them all and the search.",
            "When the table empties, the panel names the filters responsible and offers Reset.",
        ];

        public const string FiltersTip = "Filters, sort and the viewed character are remembered between sessions.";

        // ---- Reading a quest ----
        public static readonly string[] QuestCardTitles =
        [
            "Requirements",
            "Path",
            "Unlocks next",
            "Giver actions",
            "Provenance",
        ];

        public static readonly string[] QuestCardBodies =
        [
            "Every requirement with ✓ or ✗ and a ▶ on the one that blocks you. Hover a ✗ for the exact gap, such as the rank or level you still need.",
            "The prerequisite chain as a trail of moons, lit where done. Long stretches of finished steps fold into one line; open it to see them. Click any step to inspect it.",
            "What this quest opens once turned in. Click an entry to jump to it.",
            "The giver line names the NPC and zone, with buttons to flag the giver on the map and open the in-game journal.",
            "The last line says where the state came from and when: client flags for the live character, the snapshot time for a stored one.",
        ];

        public const string QuestTip = "Right-click a table row for pin, map flag, journal, copy name, show path and chat link.";

        // ---- Moonlit treasures ----
        public const string UniqueTitle = "What unique means";
        public const string UniqueBody = "A Moonlit treasure is a reward that exists nowhere else: an emote, mount, minion, roll, card, duty or feature you can only find on this road. Anything trades, shops or achievements also hand out is not unique.";
        public const string ConfidenceTitle = "Confidence badges";
        public const string ConfidenceBody = "Each row carries a badge saying how the verdict was reached. Hover a badge in the table for its source.";
        public const string ConfidenceStaticMeaning = "from the game data alone";
        public const string ConfidenceCommunityMeaning = "reported by players, not yet checked";
        public const string ConfidenceCuratedMeaning = "checked by hand and shipped with the plugin";
        public const string ConfidenceUserMeaning = "your own override";
        public const string HaveTitle = "Have";
        public const string HaveBody = "Whether the viewed character owns the reward. Emotes, mounts, minions, rolls, cards and duties are read from the live client, so a stored snapshot shows a veiled moon for them.";
        public const string OverridesTitle = "Overrides";
        public const string OverridesBody = "Not unique (hide) in a row's context menu removes a quest from Moonlit. Mark quest unique in the detail pane adds one, with a note naming the reward.";
        public const string RestoreTitle = "Restore";
        public const string RestoreBody = "Restore shipped verdict, in the row's context menu or the detail pane, undoes either. Overrides live in user/overrides.json and survive updates.";

        // ---- Characters and snapshots ----
        public static readonly string[] CharacterCardTitles =
        [
            "Snapshot",
            "View another character",
            "Dashboard",
            "Account view",
            "Export and forget",
        ];

        public static readonly string[] CharacterCardBodies =
        [
            "Everything read from a character: completed quests, journal, job levels, Grand Company, allied societies and unlocked duties. One per character, refreshed while you play and again on logout.",
            "Pick a stored character to browse the catalog as that character. The toolbar's sync moon goes veiled and a banner names the snapshot.",
            "Completion per journal section, Moonlit progress, pins, recent activity, job levels by role, Grand Company and society standings.",
            "At the bottom: every character's state for the quest selected in the Journal, without switching characters.",
            "Export JSON writes the snapshot to the exports folder in the config directory. Forget deletes a stored character. Settings can delete everything.",
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
            "Every zone you can fly in, under its expansion, with a moon that fills as you attune its currents; the zone you stand in is marked ● and selected first. Pick a zone and the table lists its quest currents: attuned or not, the quest's state, its next step, and Flag or Teleport to the giver.",
            "Quest currents come from quests, five per zone in most expansions, and completing the quest attunes them. Field currents are touched in the world; the tab counts them but never locates them. Use the Aether Compass, a General Action under Actions & Traits, to point at the nearest one.",
            "/tsuki nearby opens a small window with the quests you can start in the current zone: state moon, level, job, Flag and Teleport, and a click on the name shows it in the Journal. Also accepted here folds out the accepted quests whose giver stands in the zone. The cog holds its settings.",
            "☾ N in the server info bar is the count of quests you can start here. Hover it for the first five names; click it to open Nearby quests. It hides at zero unless Keep the entry visible is on, and the cog in Nearby quests can turn it off entirely.",
            "/tsuki zone prints chat links for the quests you can start in the current zone, by level, up to ten. /tsuki which prints every quest the targeted NPC hands out with its state. Both need an evaluated character; the Nearby quests window keeps the same list on screen.",
        ];

        // ---- Commands ----
        public static readonly string[] CommandKeys =
        [
            "/tsukimichi",
            "/tsuki",
            "/tsukimichi search <text>",
            "/tsukimichi config",
            "/tsukimichi help",
            "/tsukimichi glyphs",
            "/tsuki zone",
            "/tsuki which",
            "/tsuki nearby",
        ];

        public static readonly string[] CommandMeanings =
        [
            "open or close the window",
            "the same, shorter",
            "search and print matching quests to chat as links; /tsukimichi <text> does the same",
            "open Settings",
            "open this window",
            "the glyph sheet: every moon at every size",
            "quests you can start in the current zone, as chat links by level",
            "every quest the targeted NPC hands out, with its state",
            "open or close the Nearby quests window: what you can start in the current zone",
        ];

        // ---- Tips ----
        public static readonly string[] Tips =
        [
            "Hide completed plus Available now is the fastest view of what to do next.",
            "Pin a quest and it stays one click away in the Characters dashboard, with its next step.",
            "The search box takes a quest id; paste one from a wiki.",
            "Hover a requirement's ✗ for the exact gap, such as the rank or level you still need.",
            "The path is clickable: select any earlier step to see its own requirements.",
            "Scope the tree to a genre and sort by level to run a zone's side quests in order.",
            "Reward kind filters are three-state: require, exclude or ignore each kind.",
            "The toolbar's sync moon is full when live and veiled on a snapshot; hover it for the time.",
            "Chat notices for newly available quests are off by default; turn them on in Settings, main scenario excluded.",
            "Foreclosed quests are left out of totals, so a category can reach 100% without them.",
            "Settings shows how long each poll takes; 1 s is the default and is safe.",
            "Delete all data in Settings removes snapshots, pins and overrides but keeps your settings.",
        ];
    }

    /// <summary>Interactive tutorial text: one title and one body (under fifty words) per step, plus the card buttons.</summary>
    public static class Tutorial
    {
        public const string CardId = "##TsukimichiTutorialCard";
        public const string TakeTour = "Take the tour";
        public const string NotNow = "Not now";
        public const string Back = "Back";
        public const string Next = "Next";
        public const string Skip = "Skip";
        public const string Done = "Done";
        public const string OpenHelp = "Open help";
        /// <summary>{0} = step number, {1} = step count.</summary>
        public const string ProgressFormat = "{0} of {1}";

        public const string WelcomeTitle = "Welcome to Tsukimichi";
        public const string WelcomeBody = "Tsukimichi is the road you walk by moonlight: every quest is a step, and the moon fills as you complete it. This tour points at each part of the window. Nothing in it changes your game.";
        public const string SearchTitle = "Search";
        public const string SearchBody = "Type a quest name, a reward name or a numeric id. The table narrows 150 ms after you stop typing; the × clears it.";
        public const string FiltersTitle = "Filters";
        public const string FiltersBody = "Filters opens this panel. Hide completed and Available now are the two you will use most; both take per-category overrides. States, expansions, level, job and reward kind sit below.";
        public const string ChipsTitle = "Chips";
        public const string ChipsBody = "Every active filter shows here as a chip. Click a chip to clear that filter; Reset clears them all together with the search.";
        public const string TabsTitle = "Four tabs";
        public const string TabsBody = "Journal is the catalog. Moonlit collects quests with unique rewards. Characters holds every snapshot on the account. Flight shows the aether current quests of each flying zone.";
        public const string TreeTitle = "Journal tree";
        public const string TreeBody = "Section, category and genre scope the table. Each node shows done/total and a moon that fills with completion; Feature Unlocks and Unlisted are virtual nodes.";
        public const string TableTitle = "Quest table";
        public const string TableBody = "One row per quest. The glyph is its moon phase: full is completed, first quarter is ready, new is blocked. Click a header to sort; right-click a row for pin, map flag and journal.";
        public const string RequirementsTitle = "Requirements";
        public const string RequirementsBody = "Select a row and the detail pane lists every requirement with ✓ or ✗. The ▶ marks the one blocking you; hover it for the exact gap.";
        public const string PathTitle = "Path and unlocks next";
        public const string PathBody = "Path is the prerequisite chain as moons, lit where done; finished stretches fold into one line. Unlocks next lists what this quest opens.";
        public const string GiverTitle = "Giver actions";
        public const string GiverBody = "The giver line names the NPC and zone. Flag it on the map or open the in-game journal from here; the last line says where the state came from and when.";
        public const string MoonlitTitle = "Moonlit treasures";
        public const string MoonlitBody = "Reward kinds on the left with obtained/total; the table lists each treasure, its quest, whether you have it and a confidence badge. Hover a badge for its source.";
        public const string CharactersTitle = "Characters";
        public const string CharactersBody = "Every stored snapshot on the left. The dashboard shows completion by section, Moonlit progress, pins, recent activity, job levels and standings for the viewed character.";
        public const string FlightTitle = "Flight";
        public const string FlightBody = "Every flying zone, under its expansion, with a moon of attuned currents. Pick one: the table lists its five quest currents, whether each is attuned, the quest that blocks it and its next step, plus Flag and Teleport for where to fly next.";
        public const string HelpTitle = "Help, tour and settings";
        public const string HelpBody = "The book reopens the guide, the graduation cap replays this tour, and the cog opens Settings: poll interval, display scale and data controls.";
        public const string FinishTitle = "That is the road";
        public const string FinishBody = "Reopen this tour any time from the toolbar or Settings. The help window has more on every topic, with buttons that take you straight to each part.";
    }
}
