using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the Moonlit, Characters and Flight panes and the config window. The other half of this partial class
/// holds the main window's strings; constant names here are prefixed so the two halves never collide.
/// </summary>
static partial class Strings
{
    // ---- Moonlit pane ----
    public static string MoonlitAllKinds => Loc.Get("MoonlitAllKinds");
    public static string MoonlitNoData => Loc.Get("MoonlitNoData");
    public static string MoonlitNothingMatches => Loc.Get("MoonlitNothingMatches");
    public static string MoonlitHideObtainedLabel => Loc.Get("MoonlitHideObtainedLabel");
    public static string MoonlitFilterHint => Loc.Get("MoonlitFilterHint");
    /// <summary>Subtitle under the Moonlit pane header; the tab keeps the brand word.</summary>
    public static string MoonlitSubtitle => Loc.Get("MoonlitSubtitle");
    public static string MoonlitOfflineHint => Loc.Get("MoonlitOfflineHint");
    public static string MoonlitAchievementsFromQuests => Loc.Get("MoonlitAchievementsFromQuests");
    public static string MoonlitColumnObtained => Loc.Get("MoonlitColumnObtained");
    public static string MoonlitColumnReward => Loc.Get("MoonlitColumnReward");
    public static string MoonlitColumnKind => Loc.Get("MoonlitColumnKind");
    public static string MoonlitColumnQuest => Loc.Get("MoonlitColumnQuest");
    public static string MoonlitColumnState => Loc.Get("MoonlitColumnState");
    public static string MoonlitColumnConfidence => Loc.Get("MoonlitColumnConfidence");
    public static string MoonlitObtainedYes => Loc.Get("MoonlitObtainedYes");
    public static string MoonlitObtainedNo => Loc.Get("MoonlitObtainedNo");
    public static string MoonlitObtainedUnknown => Loc.Get("MoonlitObtainedUnknown");
    public static string MoonlitShowInJournal => Loc.Get("MoonlitShowInJournal");
    public static string MoonlitMarkNotUnique => Loc.Get("MoonlitMarkNotUnique");
    public static string MoonlitRestoreOverride => Loc.Get("MoonlitRestoreOverride");
    public const string MoonlitVerdictPopup = "##moonlitVerdict";
    public static string MoonlitHiddenTooltip => Loc.Get("MoonlitHiddenTooltip");
    public static string MoonlitConfidenceStatic => Loc.Get("MoonlitConfidenceStatic");
    public static string MoonlitConfidenceCommunity => Loc.Get("MoonlitConfidenceCommunity");
    public static string MoonlitConfidenceCurated => Loc.Get("MoonlitConfidenceCurated");
    public static string MoonlitConfidenceUser => Loc.Get("MoonlitConfidenceUser");
    public static string MoonlitSourceUnknown => Loc.Get("MoonlitSourceUnknown");

    // Confidence badge tooltips: what the badge means, with the source under it.
    public static string MoonlitBadgeStatic => Loc.Get("MoonlitBadgeStatic");
    public static string MoonlitBadgeCommunity => Loc.Get("MoonlitBadgeCommunity");
    public static string MoonlitBadgeCurated => Loc.Get("MoonlitBadgeCurated");
    public static string MoonlitBadgeUser => Loc.Get("MoonlitBadgeUser");
    public static string MoonlitBadgeHidden => Loc.Get("MoonlitBadgeHidden");

    /// <summary>Hover text of the veiled stand-in drawn where a reward of a kind without sheet art would show its icon.</summary>
    public static string MoonlitNoIconTooltip => Loc.Get("MoonlitNoIconTooltip");

    /// <summary>{0} = the kind row's obtained/total.</summary>
    public static string MoonlitKindCountTooltipFormat => Loc.Get("MoonlitKindCountTooltipFormat");
    /// <summary>{0} = the quest's row id, for a quest the catalog does not have.</summary>
    public static string MoonlitQuestFormat => Loc.Get("MoonlitQuestFormat");

    // Rewards the FFXIV Online Store also sells (curated/online_store.json; entry OtherSources carries OnlineStore)
    public static string MoonlitStoreOnly => Loc.Get("MoonlitStoreOnly");
    public static string MoonlitStoreOnlyTooltip => Loc.Get("MoonlitStoreOnlyTooltip");
    /// <summary>The same as the second line of the reward tooltip, after "Store only": composed once, since the tooltip draws every hovered frame.</summary>
    public static string MoonlitStoreOnlyTooltipLine => Loc.Get("MoonlitStoreOnlyTooltipLine");

    // Rewards a duty also drops (curated/other_sources.json; entry OtherSources carries DungeonDrop, OtherSourceNotes the duties)
    public static string MoonlitAlsoDrops => Loc.Get("MoonlitAlsoDrops");
    /// <summary>{0} = the duties that also drop the reward.</summary>
    private static string AlsoDropsInFormat => Loc.Get("AlsoDropsInFormat");
    private static string AlsoDropsInADuty => Loc.Get("AlsoDropsInADuty");
    /// <summary>{0} = the "Also drops in …" line.</summary>
    private static string NotExclusiveFormat => Loc.Get("NotExclusiveFormat");

    /// <summary>
    /// "Also drops in Snowcloak, …": the line the reward tooltip and the item hover hint show for a reward a duty also
    /// drops; <paramref name="where"/> empty (the data names no duty) reads "Also drops in a duty".
    /// </summary>
    public static string AlsoDropsLine(string? where) =>
        string.IsNullOrWhiteSpace(where) ? AlsoDropsInADuty : string.Format(System.Globalization.CultureInfo.CurrentCulture, AlsoDropsInFormat, where);

    /// <summary>The "Also drops" mark's tooltip: <see cref="AlsoDropsLine"/> plus "; not exclusive to the quest".</summary>
    public static string MoonlitAlsoDropsTooltip(string? where) => string.Format(System.Globalization.CultureInfo.CurrentCulture, NotExclusiveFormat, AlsoDropsLine(where));

    // One toggle hides both (the persisted setting keeps its 0.6.0 name, MoonlitHideStoreResells)
    public static string MoonlitHideStoreResellsLabel => Loc.Get("MoonlitHideStoreResellsLabel");
    public static string MoonlitHideStoreResellsTooltip => Loc.Get("MoonlitHideStoreResellsTooltip");

    // Confidence filter next to "Hide obtained"
    public static string MoonlitConfidenceFilterTooltip => Loc.Get("MoonlitConfidenceFilterTooltip");
    public static string MoonlitConfidenceAny => Loc.Get("MoonlitConfidenceAny");
    public static string MoonlitConfidenceStaticOnly => Loc.Get("MoonlitConfidenceStaticOnly");
    public static string MoonlitConfidenceCuratedOnly => Loc.Get("MoonlitConfidenceCuratedOnly");
    public static string MoonlitConfidenceYoursOnly => Loc.Get("MoonlitConfidenceYoursOnly");
    public static string MoonlitConfidenceUnknownObtained => Loc.Get("MoonlitConfidenceUnknownObtained");

    // ---- Flight pane ----
    public static string TabFlight => Loc.Get("TabFlight");
    public static string FlightNoData => Loc.Get("FlightNoData");
    public static string FlightNoZone => Loc.Get("FlightNoZone");
    public static string FlightOfflineHint => Loc.Get("FlightOfflineHint");
    public const string FlightCurrentZoneMarker = "● ";
    public static string FlightCurrentZoneTooltip => Loc.Get("FlightCurrentZoneTooltip");
    /// <summary>{0} = quest currents done, {1} = quest currents in the zone.</summary>
    public const string FlightZoneCountFormat = "{0}/{1}";
    /// <summary>{0} = expansion name; the entry that covers all of an expansion's field zones (A Realm Reborn).</summary>
    public static string FlightAllZonesFormat => Loc.Get("FlightAllZonesFormat");
    /// <summary>{0} = attuned, {1} = total currents, {2} = quest currents attuned, {3} = quest currents.</summary>
    public static string FlightZoneTooltipFormat => Loc.Get("FlightZoneTooltipFormat");
    /// <summary>{0} = total currents, {1} = quest currents whose quest is complete, {2} = quest currents.</summary>
    public static string FlightZoneTooltipUnknownFormat => Loc.Get("FlightZoneTooltipUnknownFormat");
    /// <summary>{0} = zone, {1} = attuned, {2} = total currents.</summary>
    public static string FlightHeaderFormat => Loc.Get("FlightHeaderFormat");
    /// <summary>{0} = zone, {1} = total currents.</summary>
    public static string FlightHeaderUnknownFormat => Loc.Get("FlightHeaderUnknownFormat");
    public static string FlightHeaderComplete => Loc.Get("FlightHeaderComplete");
    public static string FlightQuestCurrents => Loc.Get("FlightQuestCurrents");
    public static string FlightColumnAttuned => Loc.Get("FlightColumnAttuned");
    public static string FlightColumnQuest => Loc.Get("FlightColumnQuest");
    public static string FlightColumnState => Loc.Get("FlightColumnState");
    public static string FlightColumnStatus => Loc.Get("FlightColumnStatus");
    public const string FlightColumnActions = "##actions";
    public static string FlightAttunedYes => Loc.Get("FlightAttunedYes");
    public static string FlightAttunedNo => Loc.Get("FlightAttunedNo");
    public static string FlightAttunedUnknown => Loc.Get("FlightAttunedUnknown");
    public static string FlightQuestClickHint => Loc.Get("FlightQuestClickHint");
    /// <summary>{0} = the quest's row id, for a quest the catalog does not have.</summary>
    public static string FlightQuestFormat => Loc.Get("FlightQuestFormat");
    public static string FlightFlag => Loc.Get("FlightFlag");
    public static string FlightFlagTooltip => Loc.Get("FlightFlagTooltip");
    public static string FlightTeleport => Loc.Get("FlightTeleport");
    /// <summary>{0} = attuned field currents, {1} = field currents in the zone.</summary>
    public static string FlightFieldFormat => Loc.Get("FlightFieldFormat");
    /// <summary>{0} = field currents in the zone.</summary>
    public static string FlightFieldAllFormat => Loc.Get("FlightFieldAllFormat");
    /// <summary>{0} = field currents in the zone.</summary>
    public static string FlightFieldUnknownFormat => Loc.Get("FlightFieldUnknownFormat");
    public static string FlightFieldNone => Loc.Get("FlightFieldNone");
    public static string FlightFieldTooltip => Loc.Get("FlightFieldTooltip");

    // ---- Discovery commands ----
    public static string ZoneNoCharacter => Loc.Get("ZoneNoCharacter");
    public static string ZoneNoQuests => Loc.Get("ZoneNoQuests");
    public static string WhichNoTarget => Loc.Get("WhichNoTarget");
    /// <summary>{0} = NPC name.</summary>
    public static string WhichNoQuestsFormat => Loc.Get("WhichNoQuestsFormat");
    public const string ChatSuffixSeparator = "  · ";

    // ---- Wotsit entries ----
    /// <summary>{0} = quest name.</summary>
    public static string WotsitQuestFormat => Loc.Get("WotsitQuestFormat");
    /// <summary>{0} = reward name, {1} = reward kind.</summary>
    public static string WotsitRewardFormat => Loc.Get("WotsitRewardFormat");

    // ---- Lifestream teleport (table and detail pane) ----
    public static string TeleportToGiver => Loc.Get("TeleportToGiver");
    public static string TeleportBusy => Loc.Get("TeleportBusy");
    public static string TeleportNoAetheryte => Loc.Get("TeleportNoAetheryte");
    /// <summary>{0} = aetheryte place name.</summary>
    public static string TeleportTooltipFormat => Loc.Get("TeleportTooltipFormat");

    /// <summary>Display name of a reward kind, plural, as the Moonlit left column lists them.</summary>
    public static string MoonlitKindName(RewardKind kind) => kind switch
    {
        RewardKind.Item => Loc.Get("MoonlitKindName.Item"),
        RewardKind.OptionalItem => Loc.Get("MoonlitKindName.OptionalItem"),
        RewardKind.Emote => Loc.Get("MoonlitKindName.Emote"),
        RewardKind.Action => Loc.Get("MoonlitKindName.Action"),
        RewardKind.GeneralAction => Loc.Get("MoonlitKindName.GeneralAction"),
        RewardKind.Instance => Loc.Get("MoonlitKindName.Instance"),
        RewardKind.ClassJob => Loc.Get("MoonlitKindName.ClassJob"),
        RewardKind.Other => Loc.Get("MoonlitKindName.Other"),
        RewardKind.ArtifactGear => Loc.Get("MoonlitKindName.ArtifactGear"),
        RewardKind.Mount => Loc.Get("MoonlitKindName.Mount"),
        RewardKind.Minion => Loc.Get("MoonlitKindName.Minion"),
        RewardKind.Orchestrion => Loc.Get("MoonlitKindName.Orchestrion"),
        RewardKind.TripleTriadCard => Loc.Get("MoonlitKindName.TripleTriadCard"),
        RewardKind.Ornament => Loc.Get("MoonlitKindName.Ornament"),
        RewardKind.Barding => Loc.Get("MoonlitKindName.Barding"),
        RewardKind.Hairstyle => Loc.Get("MoonlitKindName.Hairstyle"),
        RewardKind.AetherCurrent => Loc.Get("MoonlitKindName.AetherCurrent"),
        RewardKind.BlueMageSpell => Loc.Get("MoonlitKindName.BlueMageSpell"),
        RewardKind.Trait => Loc.Get("MoonlitKindName.Trait"),
        RewardKind.Achievement => Loc.Get("MoonlitKindName.Achievement"),
        RewardKind.Title => Loc.Get("MoonlitKindName.Title"),
        RewardKind.DutyUnlock => Loc.Get("MoonlitKindName.DutyUnlock"),
        RewardKind.SystemUnlock => Loc.Get("MoonlitKindName.SystemUnlock"),
        _ => kind.ToString(),
    };

    // ---- Characters pane ----
    public static string CharactersNoneStored => Loc.Get("CharactersNoneStored");
    public static string CharactersNoneViewed => Loc.Get("CharactersNoneViewed");
    public static string CharactersLive => Loc.Get("CharactersLive");
    /// <summary>{0} = date and time, {1} = how long ago.</summary>
    public static string CharactersSnapshotFormat => Loc.Get("CharactersSnapshotFormat");
    /// <summary>{0} = quests completed (plural form; the one form is CharactersCompletedOneFormat).</summary>
    public static string CharactersCompletedFormat => Loc.Get("CharactersCompletedFormat");

    /// <summary>{0} = quests completed, one form (<see cref="Loc.Plural"/>).</summary>
    public static string CharactersCompletedOneFormat => Loc.Get("CharactersCompletedOneFormat");

    /// <summary>"1 quest completed" or "N quests completed" by the language's plural rule.</summary>
    public static string CharactersCompleted(int count) => Loc.Plural(count, CharactersCompletedOneFormat, CharactersCompletedFormat);
    /// <summary>{0} = quests in the journal.</summary>
    public static string CharactersAcceptedFormat => Loc.Get("CharactersAcceptedFormat");
    public static string CharactersExport => Loc.Get("CharactersExport");
    /// <summary>{0} = file path.</summary>
    public static string CharactersExportedFormat => Loc.Get("CharactersExportedFormat");
    /// <summary>{0} = the error.</summary>
    public static string CharactersExportFailedFormat => Loc.Get("CharactersExportFailedFormat");
    public static string CharactersForget => Loc.Get("CharactersForget");
    public static string CharactersForgetLiveHint => Loc.Get("CharactersForgetLiveHint");
    public static string CharactersForgetPopup => Loc.Get("CharactersForgetPopup");
    /// <summary>{0} = character name.</summary>
    public static string CharactersForgetQuestionFormat => Loc.Get("CharactersForgetQuestionFormat");
    public static string CharactersForgetConfirm => Loc.Get("CharactersForgetConfirm");
    public static string CharactersCancel => Loc.Get("CharactersCancel");
    public static string CharactersJobs => Loc.Get("CharactersJobs");
    public static string CharactersNoJobs => Loc.Get("CharactersNoJobs");
    public static string CharactersColumnJob => Loc.Get("CharactersColumnJob");
    public static string CharactersColumnLevel => Loc.Get("CharactersColumnLevel");
    public static string CharactersGrandCompany => Loc.Get("CharactersGrandCompany");
    public static string CharactersNoGrandCompany => Loc.Get("CharactersNoGrandCompany");
    /// <summary>{0} = Grand Company, {1} = rank number.</summary>
    public static string CharactersGcRankFormat => Loc.Get("CharactersGcRankFormat");
    public static string CharactersTribes => Loc.Get("CharactersTribes");
    public static string CharactersNoTribes => Loc.Get("CharactersNoTribes");
    public static string CharactersColumnTribe => Loc.Get("CharactersColumnTribe");
    public static string CharactersColumnRank => Loc.Get("CharactersColumnRank");
    public static string CharactersColumnReputation => Loc.Get("CharactersColumnReputation");
    /// <summary>{0} = allied society allowances left today, {1} = levequest allowances left.</summary>
    public static string CharactersAllowancesFormat => Loc.Get("CharactersAllowancesFormat");
    public static string CharactersAccountView => Loc.Get("CharactersAccountView");
    public static string CharactersAccountNoQuest => Loc.Get("CharactersAccountNoQuest");
    public static string CharactersAccountUnknownQuest => Loc.Get("CharactersAccountUnknownQuest");
    public static string CharactersColumnCharacter => Loc.Get("CharactersColumnCharacter");
    public static string CharactersColumnState => Loc.Get("CharactersColumnState");
    public static string CharactersColumnStatus => Loc.Get("CharactersColumnStatus");
    public static string CharactersSnapshotUnreadable => Loc.Get("CharactersSnapshotUnreadable");
    public const string CharactersLiveMarker = "● ";
    /// <summary>{0} = world id, for a world with no name.</summary>
    public static string CharactersWorldFormat => Loc.Get("CharactersWorldFormat");
    /// <summary>{0} = job id, for a job with no name.</summary>
    public static string CharactersJobFormat => Loc.Get("CharactersJobFormat");
    /// <summary>{0} = allied society id, for a society with no name.</summary>
    public static string CharactersTribeFormat => Loc.Get("CharactersTribeFormat");

    // Characters dashboard
    public static string CharactersSectionCompletion => Loc.Get("CharactersSectionCompletion");
    public static string CharactersNoSections => Loc.Get("CharactersNoSections");
    public static string CharactersAllQuests => Loc.Get("CharactersAllQuests");
    /// <summary>{0} = journal section id, for a section with no name.</summary>
    public static string CharactersSectionFormat => Loc.Get("CharactersSectionFormat");
    public static string CharactersColumnSection => Loc.Get("CharactersColumnSection");
    public static string CharactersColumnDone => Loc.Get("CharactersColumnDone");
    public const string CharactersColumnPercent = "%";
    public static string CharactersMoonlitSummary => Loc.Get("CharactersMoonlitSummary");
    public static string CharactersMoonlitUnavailable => Loc.Get("CharactersMoonlitUnavailable");
    public static string CharactersColumnKind => Loc.Get("CharactersColumnKind");
    public static string CharactersColumnObtained => Loc.Get("CharactersColumnObtained");
    public static string CharactersPinned => Loc.Get("CharactersPinned");
    public static string CharactersNoPins => Loc.Get("CharactersNoPins");
    public static string CharactersColumnQuest => Loc.Get("CharactersColumnQuest");
    public static string CharactersRecent => Loc.Get("CharactersRecent");
    public static string CharactersRecentNeedsLive => Loc.Get("CharactersRecentNeedsLive");
    public static string CharactersNoRecent => Loc.Get("CharactersNoRecent");
    public static string CharactersColumnTime => Loc.Get("CharactersColumnTime");
    public static string CharactersColumnEvent => Loc.Get("CharactersColumnEvent");

    /// <summary>Label for a recent-activity row.</summary>
    public static string CharactersEventName(QuestEventKind kind) => kind switch
    {
        QuestEventKind.Completed => Loc.Get("CharactersEventName.Completed"),
        QuestEventKind.Accepted => Loc.Get("CharactersEventName.Accepted"),
        QuestEventKind.Abandoned => Loc.Get("CharactersEventName.Abandoned"),
        QuestEventKind.NewlyAvailable => Loc.Get("CharactersEventName.NewlyAvailable"),
        _ => kind.ToString(),
    };

    /// <summary>Group header in the job table.</summary>
    public static string CharactersJobGroupName(CharactersPane.JobGroup group) => group switch
    {
        CharactersPane.JobGroup.Tank => Loc.Get("CharactersJobGroupName.Tank"),
        CharactersPane.JobGroup.Healer => Loc.Get("CharactersJobGroupName.Healer"),
        CharactersPane.JobGroup.Melee => Loc.Get("CharactersJobGroupName.Melee"),
        CharactersPane.JobGroup.Ranged => Loc.Get("CharactersJobGroupName.Ranged"),
        CharactersPane.JobGroup.Caster => Loc.Get("CharactersJobGroupName.Caster"),
        CharactersPane.JobGroup.Crafter => Loc.Get("CharactersJobGroupName.Crafter"),
        CharactersPane.JobGroup.Gatherer => Loc.Get("CharactersJobGroupName.Gatherer"),
        _ => Loc.Get("CharactersJobGroupName.Default"),
    };

    // ---- Config window ----
    public static string ConfigWindowTitle => Loc.Get("ConfigWindowTitle");
    public static string UnknownError => Loc.Get("UnknownError");

    // Settings › Display › Plugin language (V2-19)
    public static string ConfigLanguage => Loc.Get("ConfigLanguage");
    /// <summary>{0} = the language Dalamud is set to, in its own name.</summary>
    public static string ConfigLanguageFollowFormat => Loc.Get("ConfigLanguageFollowFormat");
    public static string ConfigLanguageEnglish => Loc.Get("ConfigLanguageEnglish");
    public static string ConfigLanguagePseudo => Loc.Get("ConfigLanguagePseudo");
    public static string ConfigLanguageHint => Loc.Get("ConfigLanguageHint");
    /// <summary>{0} = the language in its own name, {1} = whole percent translated.</summary>
    public static string ConfigLanguageDraftFormat => Loc.Get("ConfigLanguageDraftFormat");
    public static string ConfigLanguageNotLoaded => Loc.Get("ConfigLanguageNotLoaded");
    public static string ConfigSectionPolling => Loc.Get("ConfigSectionPolling");
    public static string ConfigPollInterval => Loc.Get("ConfigPollInterval");
    public static string ConfigPollIntervalHint => Loc.Get("ConfigPollIntervalHint");
    public static string ConfigSectionNotices => Loc.Get("ConfigSectionNotices");
    public static string ConfigChatNotice => Loc.Get("ConfigChatNotice");
    public static string ConfigIncludeMsq => Loc.Get("ConfigIncludeMsq");
    public static string ConfigSectionJournal => Loc.Get("ConfigSectionJournal");
    public static string ConfigShowUnlisted => Loc.Get("ConfigShowUnlisted");
    public static string ConfigShowUnlistedHint => Loc.Get("ConfigShowUnlistedHint");
    public static string ConfigJournalFiling => Loc.Get("ConfigJournalFiling");
    public static string ConfigJournalFilingRefiled => Loc.Get("ConfigJournalFilingRefiled");
    public static string ConfigJournalFilingLegacy => Loc.Get("ConfigJournalFilingLegacy");
    public static string ConfigJournalFilingHint => Loc.Get("ConfigJournalFilingHint");
    public static string ConfigSectionIntegrations => Loc.Get("ConfigSectionIntegrations");
    public static string ConfigWotsitIntegration => Loc.Get("ConfigWotsitIntegration");
    public static string ConfigWotsitIntegrationHint => Loc.Get("ConfigWotsitIntegrationHint");
    public static string ConfigNpcContextMenu => Loc.Get("ConfigNpcContextMenu");
    public static string ConfigNpcContextMenuHint => Loc.Get("ConfigNpcContextMenuHint");

    // ---- /tsuki why ----
    public static string WhyNoSelection => Loc.Get("WhyNoSelection");

    /// <summary>Between "talk to &lt;giver&gt;" and the giver's map link on a Ready line.</summary>
    public static string WhyGiverIn => Loc.Get("WhyGiverIn");

    /// <summary>The map link's text on a Ready line: {0} place name, {1} x, {2} y.</summary>
    public const string WhyGiverPlaceFormat = "{0} ({1:0.0}, {2:0.0})";
    public static string ConfigSectionData => Loc.Get("ConfigSectionData");
    public static string ConfigDataRetention => Loc.Get("ConfigDataRetention");
    public static string ConfigDeleteAll => Loc.Get("ConfigDeleteAll");
    public static string ConfigDeleteStep1Popup => Loc.Get("ConfigDeleteStep1Popup");
    public static string ConfigDeleteStep1Text => Loc.Get("ConfigDeleteStep1Text");
    public static string ConfigDeleteContinue => Loc.Get("ConfigDeleteContinue");
    public static string ConfigDeleteStep2Popup => Loc.Get("ConfigDeleteStep2Popup");
    public static string ConfigDeleteStep2Text => Loc.Get("ConfigDeleteStep2Text");
    public static string ConfigDeleteConfirm => Loc.Get("ConfigDeleteConfirm");
    public static string ConfigDeleteDone => Loc.Get("ConfigDeleteDone");
    public static string ConfigCancel => Loc.Get("ConfigCancel");
    /// <summary>{0} = number of stored verdicts.</summary>
    public static string ConfigVerdictsHeaderFormat => Loc.Get("ConfigVerdictsHeaderFormat");
    public static string ConfigVerdictsNone => Loc.Get("ConfigVerdictsNone");
    public static string ConfigVerdictsUnavailable => Loc.Get("ConfigVerdictsUnavailable");
    public static string ConfigVerdictColumnQuest => Loc.Get("ConfigVerdictColumnQuest");
    public static string ConfigVerdictColumnVerdict => Loc.Get("ConfigVerdictColumnVerdict");
    public static string ConfigVerdictColumnNote => Loc.Get("ConfigVerdictColumnNote");
    public static string ConfigVerdictColumnDate => Loc.Get("ConfigVerdictColumnDate");
    public const string ConfigVerdictColumnRestore = "##restore";
    public static string ConfigVerdictUnique => Loc.Get("ConfigVerdictUnique");
    public static string ConfigVerdictNotUnique => Loc.Get("ConfigVerdictNotUnique");
    public static string ConfigVerdictRestore => Loc.Get("ConfigVerdictRestore");
    public static string ConfigVerdictRestoreTooltip => Loc.Get("ConfigVerdictRestoreTooltip");
    public static string ConfigVerdictRestoreAll => Loc.Get("ConfigVerdictRestoreAll");
    public static string ConfigVerdictRestoreAllTooltip => Loc.Get("ConfigVerdictRestoreAllTooltip");
    public static string ConfigVerdictsRestored => Loc.Get("ConfigVerdictsRestored");
    public static string ConfigSectionAbout => Loc.Get("ConfigSectionAbout");
    /// <summary>{0} = plugin version.</summary>
    public static string ConfigPluginVersionFormat => Loc.Get("ConfigPluginVersionFormat");
    /// <summary>{0} = system unlocks, {1} = duty unlocks, {2} = unlock quests, {3} = festivals in the curated data.</summary>
    public static string ConfigCuratedFormat => Loc.Get("ConfigCuratedFormat");
    public static string ConfigDataStampTooltip => Loc.Get("ConfigDataStampTooltip");
    /// <summary>{0} = quests in the catalog, {1} = the game language its names are read in.</summary>
    public static string ConfigCatalogFormat => Loc.Get("ConfigCatalogFormat");
    public static string ConfigCatalogLoading => Loc.Get("ConfigCatalogLoading");
    /// <summary>{0} = the error.</summary>
    public static string ConfigCatalogUnavailableFormat => Loc.Get("ConfigCatalogUnavailableFormat");
    public static string ConfigSectionDisplay => Loc.Get("ConfigSectionDisplay");
    public static string ConfigUiScale => Loc.Get("ConfigUiScale");
    public static string ConfigUiScaleHint => Loc.Get("ConfigUiScaleHint");
    public static string ConfigIconScale => Loc.Get("ConfigIconScale");
    public static string ConfigIconScaleHint => Loc.Get("ConfigIconScaleHint");
    public static string ConfigReduceMotion => Loc.Get("ConfigReduceMotion");
    public static string ConfigReduceMotionHint => Loc.Get("ConfigReduceMotionHint");
    public static string ConfigCompactRail => Loc.Get("ConfigCompactRail");
    public static string ConfigCompactRailHint => Loc.Get("ConfigCompactRailHint");
    public static string ConfigFollowDalamudColours => Loc.Get("ConfigFollowDalamudColours");
    public static string ConfigFollowDalamudColoursHint => Loc.Get("ConfigFollowDalamudColoursHint");
    public static string ConfigGlyphPalette => Loc.Get("ConfigGlyphPalette");
    public static string ConfigGlyphPaletteStandard => Loc.Get("ConfigGlyphPaletteStandard");
    public static string ConfigGlyphPaletteHighContrast => Loc.Get("ConfigGlyphPaletteHighContrast");
    public static string ConfigGlyphPaletteHint => Loc.Get("ConfigGlyphPaletteHint");
    public static string ConfigDensity => Loc.Get("ConfigDensity");
    public static string ConfigDensityComfortable => Loc.Get("ConfigDensityComfortable");
    public static string ConfigDensityDense => Loc.Get("ConfigDensityDense");
    public static string ConfigDensityHint => Loc.Get("ConfigDensityHint");
    public static string ConfigSectionHelp => Loc.Get("ConfigSectionHelp");
    public static string ConfigShowHelp => Loc.Get("ConfigShowHelp");
    public static string ConfigStartTutorial => Loc.Get("ConfigStartTutorial");
    public static string ConfigOfferTutorial => Loc.Get("ConfigOfferTutorial");
    public static string ConfigOfferTutorialHint => Loc.Get("ConfigOfferTutorialHint");
    public static string ConfigPollTimingNone => Loc.Get("ConfigPollTimingNone");
    /// <summary>{0} = last ms, {1} = average ms, {2} = poll count.</summary>
    public static string ConfigPollTimingFormat => Loc.Get("ConfigPollTimingFormat");
    /// <summary>{0} = average ms.</summary>
    public static string ConfigPollCostFormat => Loc.Get("ConfigPollCostFormat");
    public static string ConfigPollCostUnknown => Loc.Get("ConfigPollCostUnknown");

    /// <summary>"What's new" card at the top of the detail column after an update.</summary>
    public static class WhatsNew
    {
        /// <summary>{0} = plugin version.</summary>
        public static string TitleFormat => Loc.Get("WhatsNew.TitleFormat");
        public static string Close => Loc.Get("WhatsNew.Close");
        public static string Help => Loc.Get("WhatsNew.Help");
        public const string Bullet = "• ";
    }

    /// <summary>
    /// Help window text. Topics are built from small blocks (cards, steps, tips, key caps), so each block's text is
    /// its own constant or array element; every paragraph stays under sixty words.
    /// </summary>
    public static class Help
    {
        public static string WindowTitle => Loc.Get("Help.WindowTitle");
        public static string SearchHint => Loc.Get("Help.SearchHint");
        public static string NoTopicMatches => Loc.Get("Help.NoTopicMatches");
        public static string TryIt => Loc.Get("Help.TryIt");
        public static string OpenSettings => Loc.Get("Help.OpenSettings");
        public static string ShownBy => Loc.Get("Help.ShownBy");

        public static string TopicName(HelpTopic topic) => topic switch
        {
            HelpTopic.QuickStart => Loc.Get("Help.TopicName.QuickStart"),
            HelpTopic.MoonPhases => Loc.Get("Help.TopicName.MoonPhases"),
            HelpTopic.Filters => Loc.Get("Help.TopicName.Filters"),
            HelpTopic.ReadingAQuest => Loc.Get("Help.TopicName.ReadingAQuest"),
            HelpTopic.Moonlit => Loc.Get("Help.TopicName.Moonlit"),
            HelpTopic.Characters => Loc.Get("Help.TopicName.Characters"),
            HelpTopic.Flight => Loc.Get("Help.TopicName.Flight"),
            HelpTopic.Plan => Loc.Get("Help.TopicName.Plan"),
            HelpTopic.Commands => Loc.Get("Help.TopicName.Commands"),
            HelpTopic.CountsDiffer => Loc.Get("Help.TopicName.CountsDiffer"),
            HelpTopic.KnownQuirks => Loc.Get("Help.TopicName.KnownQuirks"),
            HelpTopic.Spoilers => Loc.Get("Help.TopicName.Spoilers"),
            HelpTopic.Tips => Loc.Get("Help.TopicName.Tips"),
            _ => topic.ToString(),
        };

        /// <summary>One sentence under the topic title.</summary>
        public static string TopicLede(HelpTopic topic) => topic switch
        {
            HelpTopic.QuickStart => Loc.Get("Help.TopicLede.QuickStart"),
            HelpTopic.MoonPhases => Loc.Get("Help.TopicLede.MoonPhases"),
            HelpTopic.Filters => Loc.Get("Help.TopicLede.Filters"),
            HelpTopic.ReadingAQuest => Loc.Get("Help.TopicLede.ReadingAQuest"),
            HelpTopic.Moonlit => Loc.Get("Help.TopicLede.Moonlit"),
            HelpTopic.Characters => Loc.Get("Help.TopicLede.Characters"),
            HelpTopic.Flight => Loc.Get("Help.TopicLede.Flight"),
            HelpTopic.Plan => Loc.Get("Help.TopicLede.Plan"),
            HelpTopic.Commands => Loc.Get("Help.TopicLede.Commands"),
            HelpTopic.CountsDiffer => Loc.Get("Help.TopicLede.CountsDiffer"),
            HelpTopic.KnownQuirks => Loc.Get("Help.TopicLede.KnownQuirks"),
            HelpTopic.Spoilers => Loc.Get("Help.TopicLede.Spoilers"),
            HelpTopic.Tips => Loc.Get("Help.TopicLede.Tips"),
            _ => string.Empty,
        };

        // ---- Quick start: the "Try it" action of each step is attached by the window ----
        public static string StepOpenTitle => Loc.Get("Help.StepOpenTitle");
        public static string StepOpenBody => Loc.Get("Help.StepOpenBody");
        public static string StepFindTitle => Loc.Get("Help.StepFindTitle");
        public static string StepFindBody => Loc.Get("Help.StepFindBody");
        public static string StepFiltersTitle => Loc.Get("Help.StepFiltersTitle");
        public static string StepFiltersBody => Loc.Get("Help.StepFiltersBody");
        public static string StepReadTitle => Loc.Get("Help.StepReadTitle");
        public static string StepReadBody => Loc.Get("Help.StepReadBody");
        public static string StepMoonlitTitle => Loc.Get("Help.StepMoonlitTitle");
        public static string StepMoonlitBody => Loc.Get("Help.StepMoonlitBody");
        public static string StepCharactersTitle => Loc.Get("Help.StepCharactersTitle");
        public static string StepCharactersBody => Loc.Get("Help.StepCharactersBody");
        public static string StepFlightTitle => Loc.Get("Help.StepFlightTitle");
        public static string StepFlightBody => Loc.Get("Help.StepFlightBody");
        public static string StepPlanTitle => Loc.Get("Help.StepPlanTitle");
        public static string StepPlanBody => Loc.Get("Help.StepPlanBody");
        public static string StepTourTitle => Loc.Get("Help.StepTourTitle");
        public static string StepTourBody => Loc.Get("Help.StepTourBody");
        public static string QuickStartTip => Loc.Get("Help.QuickStartTip");
        public static string QuickStartSettingsTip => Loc.Get("Help.QuickStartSettingsTip");

        // ---- Moon phases ----
        // The moon-phase name under each state comes from Strings.StateGlyphSubtitle; only the meanings live here.
        public static string PhaseCompletedMeaning => Loc.Get("Help.PhaseCompletedMeaning");
        public static string PhaseAcceptedMeaning => Loc.Get("Help.PhaseAcceptedMeaning");
        public static string PhaseReadyMeaning => Loc.Get("Help.PhaseReadyMeaning");
        public static string PhaseReadyOtherJobMeaning => Loc.Get("Help.PhaseReadyOtherJobMeaning");
        public static string PhaseDoneThisCycleMeaning => Loc.Get("Help.PhaseDoneThisCycleMeaning");
        public static string PhaseBlockedMeaning => Loc.Get("Help.PhaseBlockedMeaning");
        public static string PhaseForeclosedMeaning => Loc.Get("Help.PhaseForeclosedMeaning");
        public static string PhaseUnknownMeaning => Loc.Get("Help.PhaseUnknownMeaning");

        // "Shown by" chips: what must be set for the phase to appear in the table.
        public static string ChipHideCompletedOff => Loc.Get("Help.ChipHideCompletedOff");
        public static string ChipAvailableNow => Loc.Get("Help.ChipAvailableNow");
        public static string ChipAvailableNowOff => Loc.Get("Help.ChipAvailableNowOff");
        public static string ChipNotInTotals => Loc.Get("Help.ChipNotInTotals");
        /// <summary>{0} = state name.</summary>
        public static string ChipStateFormat => Loc.Get("Help.ChipStateFormat");

        public static string HighContrastLegendNote => Loc.Get("Help.HighContrastLegendNote");

        public static string StripeTitle => Loc.Get("Help.StripeTitle");
        public static string StripeBody => Loc.Get("Help.StripeBody");

        public static string FillingTitle => Loc.Get("Help.FillingTitle");
        public static string FillingBody => Loc.Get("Help.FillingBody");

        // ---- Filters and chips ----
        public static string[] FilterCardTitles => Loc.Array("Help.FilterCardTitles");

        public static string[] FilterCardBodies => Loc.Array("Help.FilterCardBodies");

        public static string FiltersTip => Loc.Get("Help.FiltersTip");

        // ---- Reading a quest ----
        public static string[] QuestCardTitles => Loc.Array("Help.QuestCardTitles");

        public static string[] QuestCardBodies => Loc.Array("Help.QuestCardBodies");

        public static string QuestTip => Loc.Get("Help.QuestTip");

        // ---- Moonlit treasures ----
        public static string UniqueTitle => Loc.Get("Help.UniqueTitle");
        public static string UniqueBody => Loc.Get("Help.UniqueBody");
        public static string ConfidenceTitle => Loc.Get("Help.ConfidenceTitle");
        public static string ConfidenceBody => Loc.Get("Help.ConfidenceBody");
        public static string ConfidenceStaticMeaning => Loc.Get("Help.ConfidenceStaticMeaning");
        public static string ConfidenceCommunityMeaning => Loc.Get("Help.ConfidenceCommunityMeaning");
        public static string ConfidenceCuratedMeaning => Loc.Get("Help.ConfidenceCuratedMeaning");
        public static string ConfidenceUserMeaning => Loc.Get("Help.ConfidenceUserMeaning");
        public static string HaveTitle => Loc.Get("Help.HaveTitle");
        public static string HaveBody => Loc.Get("Help.HaveBody");
        public static string OverridesTitle => Loc.Get("Help.OverridesTitle");
        public static string OverridesBody => Loc.Get("Help.OverridesBody");
        public static string RestoreTitle => Loc.Get("Help.RestoreTitle");
        public static string RestoreBody => Loc.Get("Help.RestoreBody");

        // ---- Characters and snapshots ----
        public static string[] CharacterCardTitles => Loc.Array("Help.CharacterCardTitles");

        public static string[] CharacterCardBodies => Loc.Array("Help.CharacterCardBodies");

        // ---- Flight and nearby ----
        public static string[] FlightCardTitles => Loc.Array("Help.FlightCardTitles");

        public static string[] FlightCardBodies => Loc.Array("Help.FlightCardBodies");

        // ---- Commands ----
        public static string[] CommandKeys => Loc.Array("Help.CommandKeys");

        public static string[] CommandMeanings => Loc.Array("Help.CommandMeanings");

        // ---- Why my counts differ from the journal ----
        public static string[] CountsCardTitles => Loc.Array("Help.CountsCardTitles");

        public static string[] CountsCardBodies => Loc.Array("Help.CountsCardBodies");

        public static string CountsTip => Loc.Get("Help.CountsTip");

        // ---- Known quirks ----
        public static string[] QuirkCardTitles => Loc.Array("Help.QuirkCardTitles");

        public static string[] QuirkCardBodies => Loc.Array("Help.QuirkCardBodies");

        public static string QuirksTip => Loc.Get("Help.QuirksTip");

        // ---- Spoilers ----
        public static string[] SpoilerCardTitles => Loc.Array("Help.SpoilerCardTitles");

        public static string[] SpoilerCardBodies => Loc.Array("Help.SpoilerCardBodies");

        public static string SpoilersTip => Loc.Get("Help.SpoilersTip");

        // ---- Tips ----
        public static string[] Tips => Loc.Array("Help.Tips");
    }
}
