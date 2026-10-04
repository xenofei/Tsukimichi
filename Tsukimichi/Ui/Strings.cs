using System.Collections.Generic;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Every string the UI shows, in one place (V2-19): each member reads its key from the resource files through
/// <see cref="Loc"/> (<c>Tsukimichi/Localization/Strings.resx</c> in English, the translations beside it), so the panes
/// ask for <c>Strings.TabJournal</c> and get the current language. Only separators, markers and ImGui ids stay
/// constants. Voice per spec §1: calm, precise, short; labels are nouns; no exclamation marks. Members ending in
/// <c>Format</c> are composite format strings with positional placeholders, so a language can reorder them; the
/// argument order is documented on each (and in the resx comment translators read). docs/localization.md.
/// </summary>
public static partial class Strings
{
    // Window
    public static string MainWindowTitle => Loc.Get("MainWindowTitle");
    public static string LoadingCatalog => Loc.Get("LoadingCatalog");
    public static string CatalogUnavailable => Loc.Get("CatalogUnavailable");
    /// <summary>{0} = the error; a rebuild failed while the previous catalog stays in use.</summary>
    public static string CatalogRebuildFailedFormat => Loc.Get("CatalogRebuildFailedFormat");
    public static string Retry => Loc.Get("Retry");
    public static string Retrying => Loc.Get("Retrying");

    // Toolbar
    public static string SearchHint => Loc.Get("SearchHint");
    public static string SearchTooltip => Loc.Get("SearchTooltip");
    public static string ClearSearch => Loc.Get("ClearSearch");
    public static string Filters => Loc.Get("Filters");
    public static string FiltersTooltip => Loc.Get("FiltersTooltip");
    public static string NoCharacter => Loc.Get("NoCharacter");
    public static string NoSnapshots => Loc.Get("NoSnapshots");
    public const string LiveMarker = "● ";
    public static string SyncLive => Loc.Get("SyncLive");
    public static string SyncPollerPaused => Loc.Get("SyncPollerPaused");
    /// <summary>{0} = snapshot time.</summary>
    public static string SyncSnapshotFormat => Loc.Get("SyncSnapshotFormat");
    /// <summary>{0} = name, {1} = world, {2} = time.</summary>
    public static string StaleBannerFormat => Loc.Get("StaleBannerFormat");
    /// <summary>{0} = name, {1} = world, {2} = age.</summary>
    public const string CharacterEntryFormat = "{0}@{1} · {2}";
    /// <summary>{0} = name, {1} = world.</summary>
    public const string CharacterNameFormat = "{0}@{1}";
    public static string BrowseModeNotice => Loc.Get("BrowseModeNotice");
    public static string HelpButtonTooltip => Loc.Get("HelpButtonTooltip");
    public static string SettingsButtonTooltip => Loc.Get("SettingsButtonTooltip");
    public static string ActionUnavailable => Loc.Get("ActionUnavailable");

    // Status bar: {0} = catalog count, {1} = rows shown, {2} = total in scope. The live/snapshot text follows its pip,
    // the MSQ pill and the version (right-aligned) are separate segments (T12).
    public static string StatusFormat => Loc.Get("StatusFormat");
    /// <summary>{0} = plugin version.</summary>
    public static string StatusVersionFormat => Loc.Get("StatusVersionFormat");
    /// <summary>{0} = whole percent of every counted quest done.</summary>
    public const string StatusPercentFormat = "{0}%";
    public static string StatusLive => Loc.Get("StatusLive");
    /// <summary>{0} = time.</summary>
    public static string StatusSnapshotFormat => Loc.Get("StatusSnapshotFormat");
    public static string StatusNoSnapshot => Loc.Get("StatusNoSnapshot");

    // Main scenario position (status bar and Characters dashboard)
    /// <summary>{0} = quest name.</summary>
    public static string StatusMsqFormat => Loc.Get("StatusMsqFormat");
    public static string StatusMsqComplete => Loc.Get("StatusMsqComplete");
    /// <summary>{0} = the routes of a branched main scenario, "route A 3/9 · route B —" (<c>MsqText.Compact</c>).</summary>
    public static string StatusMsqRoutesFormat => Loc.Get("StatusMsqRoutesFormat");
    /// <summary>{0} = expansion, {1} = the routes, "route A 3 of 9 · route B not started" (<c>MsqText.Spelled</c>).</summary>
    public static string CharactersMsqRoutesFormat => Loc.Get("CharactersMsqRoutesFormat");
    /// <summary>{0} = expansion, {1} = done, {2} = total.</summary>
    public static string MsqProgressFormat => Loc.Get("MsqProgressFormat");
    /// <summary>{0} = done, {1} = total.</summary>
    public static string MsqCompleteFormat => Loc.Get("MsqCompleteFormat");
    /// <summary>{0} = NPC, {1} = zone.</summary>
    public static string MsqGiverFormat => Loc.Get("MsqGiverFormat");
    public static string MsqClickHint => Loc.Get("MsqClickHint");
    /// <summary>{0} = expansion, {1} = quest, {2} = giver ("NPC, zone").</summary>
    public static string CharactersMsqFormat => Loc.Get("CharactersMsqFormat");
    /// <summary>{0} = expansion, {1} = quest.</summary>
    public static string CharactersMsqNoGiverFormat => Loc.Get("CharactersMsqNoGiverFormat");
    public static string CharactersMsqComplete => Loc.Get("CharactersMsqComplete");

    // Tabs
    public static string TabJournal => Loc.Get("TabJournal");
    public static string TabMoonlit => Loc.Get("TabMoonlit");
    public static string TabCharacters => Loc.Get("TabCharacters");
    public static string Placeholder => Loc.Get("Placeholder");

    // Tree
    public static string AllQuests => Loc.Get("AllQuests");

    /// <summary>The main window's pane dividers (feature plan v4 L1).</summary>
    public static string PaneDividerTooltip => Loc.Get("PaneDividerTooltip");
    public static string FeatureUnlocks => Loc.Get("FeatureUnlocks");
    public static string RemovedFromGame => Loc.Get("RemovedFromGame");

    // Journal filing provenance (detail pane, under the journal path)
    /// <summary>{0} = genre name, {1} = rule number, {2} = <see cref="FilingReason"/>.</summary>
    public static string FilingRuleFormat => Loc.Get("FilingRuleFormat");
    /// <summary>{0} = genre name.</summary>
    public static string FilingCuratedFormat => Loc.Get("FilingCuratedFormat");
    /// <summary>{0} = the patch the curated note names.</summary>
    public static string RemovedInPatchFormat => Loc.Get("RemovedInPatchFormat");
    /// <summary>
    /// The sheet's own signal behind a rule-1 retirement, under a journal path that already reads "Removed from the
    /// game" (an unlisted row): {0} = <see cref="RetiredReason"/>.
    /// </summary>
    public static string RetiredRuleFormat => Loc.Get("RetiredRuleFormat");
    /// <summary>The same signal for a retired row the journal still lists, whose path is its genre: {0} = <see cref="RetiredReason"/>.</summary>
    public static string RemovedByRuleFormat => Loc.Get("RemovedByRuleFormat");

    /// <summary>Which sheet signal retired a row under rule 1 (docs/data/unlisted-report.md section 4).</summary>
    public static string RetiredReason(bool placeholderIssuer, bool hiddenFlag) => (placeholderIssuer, hiddenFlag) switch
    {
        (true, true) => Loc.Get("RetiredReason.Both"),
        (_, true) => Loc.Get("RetiredReason.HiddenFlag"),
        _ => Loc.Get("RetiredReason.PlaceholderIssuer"),
    };

    /// <summary>The short reason behind a refiling rule (docs/data/unlisted-report.md section 4).</summary>
    public static string FilingReason(byte rule) => rule switch
    {
        2 => Loc.Get("FilingReason.2"),
        3 => Loc.Get("FilingReason.3"),
        4 => Loc.Get("FilingReason.4"),
        5 => Loc.Get("FilingReason.5"),
        6 => Loc.Get("FilingReason.6"),
        7 => Loc.Get("FilingReason.7"),
        9 => Loc.Get("FilingReason.9"),
        _ => string.Format(System.Globalization.CultureInfo.CurrentCulture, Loc.Get("FilingReason.Other"), rule),
    };
    /// <summary>{0} = done, {1} = total.</summary>
    public const string CountFormat = "{0}/{1}";
    /// <summary>A filling moon's tooltip line: {0} = done/total, {1} = percent.</summary>
    public const string ProgressFormat = "{0} · {1}%";
    /// <summary>Hover text of a folded tree node: {0} = section, {1} = category, {2} = genre.</summary>
    public const string FoldedPathFormat = "{0} › {1} › {2}";

    // Quick views (one-click presets at the top of the filter panel)
    public static string Presets => Loc.Get("Presets");
    public static string PresetFeatureQuests => FilterNames.Display(FilterNames.FeatureQuests);
    public static string PresetLevelBand => FilterNames.Display(FilterNames.LevelBand);
    public static string PresetStalled => FilterNames.Display(FilterNames.Stalled);
    public static string PresetFeatureQuestsTooltip => Loc.Get("PresetFeatureQuestsTooltip");
    public static string PresetLevelBandTooltip => Loc.Get("PresetLevelBandTooltip");
    public static string PresetStalledTooltip => Loc.Get("PresetStalledTooltip");
    public static string PresetStorySidequests => FilterNames.Display(FilterNames.StorySidequests);
    public static string PresetStorySidequestsTooltip => Loc.Get("PresetStorySidequestsTooltip");

    // The book badge on a story sidequest's table row. {0} = the side story (its first quest's name), {1} = place, {2} = length.
    /// <summary>{0} = the side story's title, {1} = quests after this one.</summary>
    public static string StoryBadgeFormat => Loc.Get("StoryBadgeFormat");

    /// <summary>{0} = the side story's title; the quest is its last.</summary>
    public static string StoryBadgeLastFormat => Loc.Get("StoryBadgeLastFormat");
    public static string StoryBadgeLone => Loc.Get("StoryBadgeLone");
    public static string StalledDaysFormat => Loc.Get("StalledDaysFormat");
    public static string StalledDaysLabel => Loc.Get("StalledDaysLabel");
    public static string StalledDaysTooltip => Loc.Get("StalledDaysTooltip");

    // Filter panel
    public static string HideCompleted => FilterNames.Display(FilterNames.HideCompleted);
    public static string AvailableOnly => FilterNames.Display(FilterNames.AvailableOnly);
    public static string Overrides => Loc.Get("Overrides");
    public static string OverrideInherit => Loc.Get("OverrideInherit");
    public static string OverrideOn => Loc.Get("OverrideOn");
    public static string OverrideOff => Loc.Get("OverrideOff");
    /// <summary>The override combo's items, NUL-separated as ImGui.Combo takes them; composed once per language.</summary>
    public static string OverrideOptions => overrideOptions.Value;

    private static readonly LocText overrideOptions = new(static () => OverrideInherit + "\0" + OverrideOn + "\0" + OverrideOff + "\0");
    public static string NeedsSnapshot => Loc.Get("NeedsSnapshot");
    public static string Advanced => Loc.Get("Advanced");
    public static string States => Loc.Get("States");
    public static string Expansions => Loc.Get("Expansions");
    public static string LevelRange => FilterNames.Display(FilterNames.LevelRange);
    public static string LevelFormat => Loc.Get("LevelFormat");
    public static string LevelMaxFormat => Loc.Get("LevelMaxFormat");

    /// <summary>The level range filter's chip: {0} = lowest level, {1} = highest.</summary>
    public static string LevelRangeChipFormat => Loc.Get("LevelRangeChipFormat");
    public static string JobCategory => FilterNames.Display(FilterNames.JobCategory);
    public static string JobAll => Loc.Get("JobAll");
    public static string JobDowDom => Loc.Get("JobDowDom");
    public static string JobDoh => Loc.Get("JobDoh");
    public static string JobDol => Loc.Get("JobDol");
    public static string JobCurrentOnly => Loc.Get("JobCurrentOnly");
    public static string JobCurrentOnlyTooltip => Loc.Get("JobCurrentOnlyTooltip");
    public static string RewardKinds => FilterNames.Display(FilterNames.RewardKinds);
    public static string RewardHidden => Loc.Get("RewardHidden");
    public static string RewardShow => Loc.Get("RewardShow");
    public static string RewardOnly => Loc.Get("RewardOnly");

    public static string RewardOptionName(TriState option) => option switch
    {
        TriState.Hidden => RewardHidden,
        TriState.Only => RewardOnly,
        _ => RewardShow,
    };
    public static string RepeatableOnly => Loc.Get("RepeatableOnly");
    public static string SeasonalActiveOnly => Loc.Get("SeasonalActiveOnly");
    public static string IncludeUnlisted => FilterNames.Display(FilterNames.IncludeUnlisted);
    public static string IncludeOtherPaths => FilterNames.Display(FilterNames.IncludeOtherPaths);

    /// <summary>The "Other paths" virtual tree node (feature plan v4 D1).</summary>
    public static string OtherPaths => Core.Evaluation.PathText.NodeName;
    public static string PinnedOnly => Loc.Get("PinnedOnly");
    public static string PinnedFirst => Loc.Get("PinnedFirst");
    public static string Reset => Loc.Get("Reset");

    // Display (size sliders at the bottom of the filter panel)
    public static string Display => Loc.Get("Display");
    public static string UiScale => Loc.Get("UiScale");
    public static string UiScaleTooltip => Loc.Get("UiScaleTooltip");
    public static string IconScale => Loc.Get("IconScale");
    public static string IconScaleTooltip => Loc.Get("IconScaleTooltip");
    public const string ScaleFormat = "%.2f×";
    public static string ResetDisplay => Loc.Get("ResetDisplay");
    public static string ResetDisplayTooltip => Loc.Get("ResetDisplayTooltip");

    // Filter panel tooltips
    public static string HideCompletedTooltip => Loc.Get("HideCompletedTooltip");
    public static string AvailableOnlyTooltip => Loc.Get("AvailableOnlyTooltip");
    public static string OverridesTooltip => Loc.Get("OverridesTooltip");
    public static string PinnedFirstTooltip => Loc.Get("PinnedFirstTooltip");
    public static string StatesTooltip => Loc.Get("StatesTooltip");
    public static string ExpansionsTooltip => Loc.Get("ExpansionsTooltip");
    public static string LevelRangeTooltip => Loc.Get("LevelRangeTooltip");
    public static string JobCategoryTooltip => Loc.Get("JobCategoryTooltip");
    public static string RewardKindsTooltip => Loc.Get("RewardKindsTooltip");
    public static string RepeatableOnlyTooltip => Loc.Get("RepeatableOnlyTooltip");
    public static string SeasonalActiveOnlyTooltip => Loc.Get("SeasonalActiveOnlyTooltip");
    public static string IncludeUnlistedTooltip => Loc.Get("IncludeUnlistedTooltip");
    public static string IncludeOtherPathsTooltip => Loc.Get("IncludeOtherPathsTooltip");
    public static string PinnedOnlyTooltip => Loc.Get("PinnedOnlyTooltip");
    public static string ResetTooltip => Loc.Get("ResetTooltip");
    public static string ResetFilters => Loc.Get("ResetFilters");
    public static string NothingMatchesCombination => Loc.Get("NothingMatchesCombination");
    public static string ScopeEmpty => Loc.Get("ScopeEmpty");
    /// <summary>{0} = the hidden states, each with a "−" before it, joined by ChipStateSeparator.</summary>
    public static string ChipStateFormat => Loc.Get("ChipStateFormat");
    public const string ChipStateExcludedMarker = "−";
    public static string ChipStateSeparator => Loc.Get("ChipStateSeparator");
    /// <summary>{0} = number of excluded states beyond the named ones.</summary>
    public const string ChipStateMoreFormat = " +{0}";
    public static string ChipExpansion => FilterNames.Display(FilterNames.Expansion);
    public static string ChipRepeatable => FilterNames.Display(FilterNames.Repeatable);
    public static string ChipSeasonal => FilterNames.Display(FilterNames.SeasonalActive);
    public static string ChipPinned => FilterNames.Display(FilterNames.Pinned);
    public static string ChipTooltip => Loc.Get("ChipTooltip");
    /// <summary>First line of the state chip's tooltip; the excluded states follow, all of them, however many the chip names.</summary>
    /// <summary>{0} = every hidden state, joined by ChipStateSeparator.</summary>
    public static string ChipStateTooltipFormat => Loc.Get("ChipStateTooltipFormat");

    /// <summary>First line of a filling moon's tooltip; the done/total (and percent where shown) follows.</summary>
    public static string FillingMoonTooltip => Loc.Get("FillingMoonTooltip");

    // Table
    public static string ColumnGlyph => Loc.Get("ColumnGlyph");
    public static string ColumnName => Loc.Get("ColumnName");
    public static string ColumnLevel => Loc.Get("ColumnLevel");
    public static string ColumnJob => Loc.Get("ColumnJob");
    public static string ColumnStatus => Loc.Get("ColumnStatus");
    public static string ColumnExpansion => Loc.Get("ColumnExpansion");
    public static string ColumnRewards => Loc.Get("ColumnRewards");
    public static string ColumnGlyphTooltip => Loc.Get("ColumnGlyphTooltip");
    public static string ColumnNameTooltip => Loc.Get("ColumnNameTooltip");
    public static string ColumnLevelTooltip => Loc.Get("ColumnLevelTooltip");
    public static string ColumnJobTooltip => Loc.Get("ColumnJobTooltip");
    public static string ColumnStatusTooltip => Loc.Get("ColumnStatusTooltip");
    public static string ColumnExpansionTooltip => Loc.Get("ColumnExpansionTooltip");
    public static string ColumnRewardsTooltip => Loc.Get("ColumnRewardsTooltip");
    /// <summary>{0} = the sorted column's header, hidden by the table's width.</summary>
    public static string TableSortHiddenFormat => Loc.Get("TableSortHiddenFormat");
    public static string TableResetColumnWidths => Loc.Get("TableResetColumnWidths");
    public static string JobAny => Loc.Get("JobAny");
    public static string JobMulti => Loc.Get("JobMulti");
    public static string JobDohDol => Loc.Get("JobDohDol");
    public static string Pin => Loc.Get("Pin");
    public static string Unpin => Loc.Get("Unpin");
    public static string FlagOnMap => Loc.Get("FlagOnMap");
    public static string OpenJournal => Loc.Get("OpenJournal");
    public static string OpenJournalUnavailable => Loc.Get("OpenJournalUnavailable");
    public static string CopyName => Loc.Get("CopyName");
    public static string CopyCoordinates => Loc.Get("CopyCoordinates");
    public static string CopyCoordinatesTooltip => Loc.Get("CopyCoordinatesTooltip");
    /// <summary>{0} = place name, {1} = x, {2} = y.</summary>
    public const string CoordinateClipboardFormat = "{0} ({1:0.0}, {2:0.0})";
    public static string ShowPath => Loc.Get("ShowPath");
    public static string QuestMapGraph => Loc.Get("QuestMapGraph");
    public static string LinkInChat => Loc.Get("LinkInChat");
    public static string Report => Loc.Get("Report");
    public static string ReportTooltip => Loc.Get("ReportTooltip");
    public static string ReportCopied => Loc.Get("ReportCopied");
    public static string ReportClipboardFailed => Loc.Get("ReportClipboardFailed");
    /// <summary>{0} = quest name.</summary>
    public static string ReportCopiedChatFormat => Loc.Get("ReportCopiedChatFormat");
    public static string ReportNoSelection => Loc.Get("ReportNoSelection");
    /// <summary>{0} = the text given.</summary>
    public static string ReportNoMatchFormat => Loc.Get("ReportNoMatchFormat");
    /// <summary>{0} = reward name, {1} = count.</summary>
    public const string RewardCountFormat = "{0} ×{1}";
    /// <summary>Reward tooltip item line: {0} = item level, {1} = ItemUICategory name.</summary>
    public static string ItemSummaryFormat => Loc.Get("ItemSummaryFormat");
    /// <summary>{0} = item level.</summary>
    public static string ItemLevelFormat => Loc.Get("ItemLevelFormat");

    // Detail pane
    public static string SelectQuest => Loc.Get("SelectQuest");
    public static string QuestNotInCatalog => Loc.Get("QuestNotInCatalog");
    public static string Requirements => Loc.Get("Requirements");
    public static string NoRequirements => Loc.Get("NoRequirements");
    public static string RequirementsNeedSnapshot => Loc.Get("RequirementsNeedSnapshot");
    public static string Rewards => Loc.Get("Rewards");
    public static string NoRewards => Loc.Get("NoRewards");
    public static string Path => Loc.Get("Path");
    public static string FoldedRunExpandTooltip => Loc.Get("FoldedRunExpandTooltip");
    public static string FoldedRunCollapseTooltip => Loc.Get("FoldedRunCollapseTooltip");
    public static string UniqueSection => Loc.Get("UniqueSection");
    public static string MarkedUniqueByYou => Loc.Get("MarkedUniqueByYou");
    public static string MarkedNotUniqueByYou => Loc.Get("MarkedNotUniqueByYou");
    public static string RestoreOverride => Loc.Get("RestoreOverride");
    public static string RestoreOverrideTooltip => Loc.Get("RestoreOverrideTooltip");
    public static string ListedInMoonlit => Loc.Get("ListedInMoonlit");
    public static string NotListedInMoonlit => Loc.Get("NotListedInMoonlit");
    public static string MarkUnique => Loc.Get("MarkUnique");
    public static string MarkUniqueTooltip => Loc.Get("MarkUniqueTooltip");
    public const string MarkUniquePopup = "##markUnique";
    public static string MarkUniqueNoteHint => Loc.Get("MarkUniqueNoteHint");
    public static string MarkNotUniqueNoteHint => Loc.Get("MarkNotUniqueNoteHint");
    /// <summary>{0} = seconds left, shown instead of the hold arc under Reduce motion.</summary>
    public static string VerdictHoldCountdownFormat => Loc.Get("VerdictHoldCountdownFormat");
    public static string VerdictUndoMarkedUnique => Loc.Get("VerdictUndoMarkedUnique");
    public static string VerdictUndoMarkedNotUnique => Loc.Get("VerdictUndoMarkedNotUnique");
    public static string Cancel => Loc.Get("Cancel");
    public static string Giver => Loc.Get("Giver");
    public static string NoGiver => Loc.Get("NoGiver");
    public const string Met = "✓";
    public const string Unmet = "✗";
    public static string MetTooltip => Loc.Get("MetTooltip");
    public static string UnmetTooltip => Loc.Get("UnmetTooltip");
    /// <summary>{0} = x, {1} = y.</summary>
    public const string CoordinatesFormat = "({0:0.0}, {1:0.0})";
    /// <summary>{0} = genre, {1} = category.</summary>
    public const string JournalPathFormat = "{0} › {1}";
    /// <summary>{0} = expansion, {1} = level, {2} = job category.</summary>
    public static string HeaderLineFormat => Loc.Get("HeaderLineFormat");
    /// <summary>{0} = job abbreviation.</summary>
    public static string ReadyOnJobFormat => Loc.Get("ReadyOnJobFormat");
    /// <summary>{0} = sequence.</summary>
    public static string Pinned => Loc.Get("Pinned");

    // Chat
    /// <summary>{0} = remaining count.</summary>
    public static string AndMoreFormat => Loc.Get("AndMoreFormat");
    public static string NoMatches => Loc.Get("NoMatches");
    public static string CatalogNotReady => Loc.Get("CatalogNotReady");
    public const string ChatTag = "Tsukimichi";
    /// <summary>{0} = the quest link.</summary>
    public static string ChatNewlyAvailableFormat => Loc.Get("ChatNewlyAvailableFormat");

    // Settings › Item hints (the hooks themselves are wired by the item-hint feature)
    public static string ConfigSectionItemHints => Loc.Get("ConfigSectionItemHints");
    public static string ConfigItemHints => Loc.Get("ConfigItemHints");
    public static string ConfigItemHintsHint => Loc.Get("ConfigItemHintsHint");
    public static string ConfigItemContextMenu => Loc.Get("ConfigItemContextMenu");
    public static string ConfigItemContextMenuHint => Loc.Get("ConfigItemContextMenuHint");

    // Command help
    public static string CommandHelp => Loc.Get("CommandHelp");
    public static string CommandAliasHelp => Loc.Get("CommandAliasHelp");
    /// <summary>{0} = /tsuki and every alias, joined by <see cref="CommandListSeparator"/>.</summary>
    public static string CommandAlsoFormat => Loc.Get("CommandAlsoFormat");
    public const string CommandListSeparator = ", ";

    // /tsuki stop (1.11.0, A1): one chat line
    public static string StopNothing => Loc.Get("StopNothing");
    /// <summary>{0} = what stopped, joined by <see cref="CommandListSeparator"/>.</summary>
    public static string StopDoneFormat => Loc.Get("StopDoneFormat");
    /// <summary>{0} = what could not be asked to stop, joined by <see cref="CommandListSeparator"/>.</summary>
    public static string StopFailedFormat => Loc.Get("StopFailedFormat");

    // /tsuki look <code> (1.17, spec-1.17 §C2): an unreadable code says so in chat and opens nothing
    public static string CommandLookUnreadable => Loc.Get("CommandLookUnreadable");
    /// <summary>{0} = the command Questionable runs after a stop, {1} = seconds to confirm.</summary>
    public static string StopAskQuestionableFormat => Loc.Get("StopAskQuestionableFormat");
    /// <summary>{0} = seconds to confirm.</summary>
    public static string StopAskAutoDutyFormat => Loc.Get("StopAskAutoDutyFormat");
    public static string StopFailedAll => Loc.Get("StopFailedAll");

    // Settings › Keyboard › Chat commands (1.11.0, A12)
    public static string ConfigCommandAliasesHeading => Loc.Get("ConfigCommandAliasesHeading");
    public static string ConfigCommandAliases => Loc.Get("ConfigCommandAliases");
    public static string ConfigCommandAliasesHint => Loc.Get("ConfigCommandAliasesHint");
    /// <summary>{0} = every alias in use, joined by <see cref="CommandListSeparator"/>.</summary>
    public static string ConfigCommandAliasesActiveFormat => Loc.Get("ConfigCommandAliasesActiveFormat");
    /// <summary>{0} = the aliases skipped, joined by <see cref="CommandListSeparator"/>.</summary>
    public static string ConfigCommandAliasesSkippedFormat => Loc.Get("ConfigCommandAliasesSkippedFormat");
    /// <summary>{0} = the words that are not an alias, joined by <see cref="CommandListSeparator"/>.</summary>
    public static string ConfigCommandAliasesInvalidFormat => Loc.Get("ConfigCommandAliasesInvalidFormat");

    // Time
    public static string JustNow => Loc.Get("JustNow");
    /// <summary>{0} = minutes.</summary>
    public static string MinutesAgoFormat => Loc.Get("MinutesAgoFormat");
    /// <summary>{0} = hours.</summary>
    public static string HoursAgoFormat => Loc.Get("HoursAgoFormat");
    /// <summary>{0} = days.</summary>
    public static string DaysAgoFormat => Loc.Get("DaysAgoFormat");
    public const string TimeFormat = "HH:mm";
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>A day, month and year ("12 Sep 2026"), in the current culture's month names.</summary>
    public const string DateFormat = "d MMM yyyy";

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

    /// <summary>
    /// The state moon tooltip for a quest: a done repeatable says "Done today" or "Done this week" by its reset, and
    /// on a second line when that reset comes ("resets in 3 h"; the daily at 15:00 UTC, the weekly on Tuesday at
    /// 08:00 UTC). Only a done repeatable's tooltip is composed per call; every other one is precomposed.
    /// </summary>
    public static string StateTooltip(QuestState state, QuestRecord? quest)
    {
        var tooltip = StateNames.Tooltip(state, quest, Theme.Glyphs.HighContrast);
        return state == QuestState.DoneThisCycle && quest is not null
            && Core.Runtime.GameResets.ResetsIn(quest.RepeatInterval, System.DateTime.UtcNow) is { } resets
            ? tooltip + "\n" + resets
            : tooltip;
    }

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
    /// Stands in for a chat link while a line's format is filled (<see cref="SplitAtLink"/>): a private-use character
    /// no translation contains.
    /// </summary>
    public const string LinkSlot = "\uE000";

    /// <summary>
    /// Fills <paramref name="format"/> with <paramref name="args"/>, one of which is <see cref="LinkSlot"/>, and returns
    /// the text before and after the slot, so a chat line puts its quest link wherever the language puts it ("Now
    /// available: [quest]", "[quest] が受注可能になりました"). A format that lost its placeholder yields the whole text
    /// before the link.
    /// </summary>
    public static (string Before, string After) SplitAtLink(string format, params object?[] args)
    {
        string text;
        try
        {
            text = string.Format(System.Globalization.CultureInfo.CurrentCulture, format, args);
        }
        catch (System.FormatException)
        {
            text = format;
        }

        var at = text.IndexOf(LinkSlot, System.StringComparison.Ordinal);
        return at < 0 ? (text + " ", string.Empty) : (text[..at], text[(at + LinkSlot.Length)..]);
    }

    /// <summary>
    /// Between a state name and its reason; the same separator <see cref="BlockerText.StatusText"/> uses, so a surface
    /// that composes the two itself reads like the Status column.
    /// </summary>
    public const string StateReasonSeparator = BlockerText.Separator;

    public static string RequirementName(RequirementKind kind) => kind switch
    {
        RequirementKind.Retired => Loc.Get("RequirementName.Retired"),
        RequirementKind.Foreclosure => Loc.Get("RequirementName.Foreclosure"),
        RequirementKind.ExpansionCap => Loc.Get("RequirementName.ExpansionCap"),
        RequirementKind.LevelCap => Loc.Get("RequirementName.LevelCap"),
        RequirementKind.ClassJob => Loc.Get("RequirementName.ClassJob"),
        RequirementKind.Level => Loc.Get("RequirementName.Level"),
        RequirementKind.PreviousQuests => Loc.Get("RequirementName.PreviousQuests"),
        RequirementKind.GrandCompany => Loc.Get("RequirementName.GrandCompany"),
        RequirementKind.GrandCompanyRank => Loc.Get("RequirementName.GrandCompanyRank"),
        RequirementKind.TribeRank => Loc.Get("RequirementName.TribeRank"),
        RequirementKind.TribeReputation => Loc.Get("RequirementName.TribeReputation"),
        RequirementKind.TribeAllowance => Loc.Get("RequirementName.TribeAllowance"),
        RequirementKind.TribeDailyOffer => Loc.Get("RequirementName.TribeDailyOffer"),
        RequirementKind.DutyCompletion => Loc.Get("RequirementName.DutyCompletion"),
        RequirementKind.Seasonal => Loc.Get("RequirementName.Seasonal"),
        RequirementKind.AcceptCondition => Loc.Get("RequirementName.AcceptCondition"),
        RequirementKind.Mount => Loc.Get("RequirementName.Mount"),
        RequirementKind.House => Loc.Get("RequirementName.House"),
        RequirementKind.Achievement => Loc.Get("RequirementName.Achievement"),
        RequirementKind.CustomDeliveryRank => Loc.Get("RequirementName.CustomDeliveryRank"),
        RequirementKind.CarrierLevel => Loc.Get("RequirementName.CarrierLevel"),
        RequirementKind.OtherPath => Loc.Get("RequirementName.OtherPath"),
        RequirementKind.GameGate => Loc.Get("RequirementName.GameGate"),
        _ => kind.ToString(),
    };

    public static string RewardKindName(RewardKind kind) => kind switch
    {
        RewardKind.Item => Loc.Get("RewardKindName.Item"),
        RewardKind.OptionalItem => Loc.Get("RewardKindName.OptionalItem"),
        RewardKind.Emote => Loc.Get("RewardKindName.Emote"),
        RewardKind.Action => Loc.Get("RewardKindName.Action"),
        RewardKind.GeneralAction => Loc.Get("RewardKindName.GeneralAction"),
        RewardKind.Instance => Loc.Get("RewardKindName.Instance"),
        RewardKind.ClassJob => Loc.Get("RewardKindName.ClassJob"),
        RewardKind.Other => Loc.Get("RewardKindName.Other"),
        RewardKind.ArtifactGear => Loc.Get("RewardKindName.ArtifactGear"),
        RewardKind.Mount => Loc.Get("RewardKindName.Mount"),
        RewardKind.Minion => Loc.Get("RewardKindName.Minion"),
        RewardKind.Orchestrion => Loc.Get("RewardKindName.Orchestrion"),
        RewardKind.TripleTriadCard => Loc.Get("RewardKindName.TripleTriadCard"),
        RewardKind.Ornament => Loc.Get("RewardKindName.Ornament"),
        RewardKind.Barding => Loc.Get("RewardKindName.Barding"),
        RewardKind.Hairstyle => Loc.Get("RewardKindName.Hairstyle"),
        RewardKind.AetherCurrent => Loc.Get("RewardKindName.AetherCurrent"),
        RewardKind.BlueMageSpell => Loc.Get("RewardKindName.BlueMageSpell"),
        RewardKind.Trait => Loc.Get("RewardKindName.Trait"),
        RewardKind.Achievement => Loc.Get("RewardKindName.Achievement"),
        RewardKind.Title => Loc.Get("RewardKindName.Title"),
        RewardKind.DutyUnlock => Loc.Get("RewardKindName.DutyUnlock"),
        RewardKind.SystemUnlock => Loc.Get("RewardKindName.SystemUnlock"),
        _ => kind.ToString(),
    };

    /// <summary>Short expansion label for the table column.</summary>
    public static string ExpansionShort(byte expansion) => expansion switch
    {
        0 => Loc.Get("ExpansionShort.0"),
        1 => Loc.Get("ExpansionShort.1"),
        2 => Loc.Get("ExpansionShort.2"),
        3 => Loc.Get("ExpansionShort.3"),
        4 => Loc.Get("ExpansionShort.4"),
        5 => Loc.Get("ExpansionShort.5"),
        _ => expansion.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
