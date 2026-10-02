using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the alts work of 1.8.0 (R7 B, C, D, E, G, H): the Characters list's search, badges and menu, hide
/// and don't track, the honest "not updating" status, the collection grid and Settings › Data › Characters. Constant
/// names carry the <c>Alts</c> prefix so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    public static string AltsSearchHint => Loc.Get("Alts.SearchHint");

    public static string AltsNoMatch => Loc.Get("Alts.NoMatch");

    public static string AltsHiddenBadge => Loc.Get("Alts.HiddenBadge");

    public static string AltsUntrackedBadge => Loc.Get("Alts.UntrackedBadge");

    public static string AltsNotUpdatingBadge => Loc.Get("Alts.NotUpdatingBadge");

    public static string AltsNotUpdatingNewerTooltip => Loc.Get("Alts.NotUpdatingNewerTooltip");

    public static string AltsNotUpdatingInvalidTooltip => Loc.Get("Alts.NotUpdatingInvalidTooltip");

    /// <summary>{0} = how many characters are hidden. Checkbox under the Characters list.</summary>
    public static string AltsShowHiddenFormat => Loc.Get("Alts.ShowHiddenFormat");

    public static string AltsMenuView => Loc.Get("Alts.MenuView");

    public static string AltsMenuHide => Loc.Get("Alts.MenuHide");

    public static string AltsMenuShow => Loc.Get("Alts.MenuShow");

    public static string AltsMenuDontTrack => Loc.Get("Alts.MenuDontTrack");

    public static string AltsMenuTrack => Loc.Get("Alts.MenuTrack");

    public static string AltsHideTooltip => Loc.Get("Alts.HideTooltip");

    public static string AltsDontTrackTooltip => Loc.Get("Alts.DontTrackTooltip");

    public static string AltsUntrackedNotice => Loc.Get("Alts.UntrackedNotice");

    /// <summary>{0} = character name.</summary>
    public static string AltsForgetRefusedFormat => Loc.Get("Alts.ForgetRefusedFormat");

    public static string AltsViewDashboard => Loc.Get("Alts.ViewDashboard");

    public static string AltsViewCollection => Loc.Get("Alts.ViewCollection");

    public static string AltsGridModeRewards => Loc.Get("Alts.GridModeRewards");

    public static string AltsGridModeQuests => Loc.Get("Alts.GridModeQuests");

    public static string AltsGridKindAll => Loc.Get("Alts.GridKindAll");

    public static string AltsGridMissingOnAny => Loc.Get("Alts.GridMissingOnAny");

    public static string AltsGridMissingOnAnyTooltip => Loc.Get("Alts.GridMissingOnAnyTooltip");

    public static string AltsGridNotDoneOnAny => Loc.Get("Alts.GridNotDoneOnAny");

    public static string AltsGridNotDoneOnAnyTooltip => Loc.Get("Alts.GridNotDoneOnAnyTooltip");

    public static string AltsGridSearchHint => Loc.Get("Alts.GridSearchHint");

    public static string AltsGridColumnReward => Loc.Get("Alts.GridColumnReward");

    public static string AltsGridColumnQuest => Loc.Get("Alts.GridColumnQuest");

    public static string AltsGridColumnCount => Loc.Get("Alts.GridColumnCount");

    /// <summary>{0} = character name.</summary>
    public static string AltsGridCellOwnedFormat => Loc.Get("Alts.GridCellOwnedFormat");

    /// <summary>{0} = character name.</summary>
    public static string AltsGridCellMissingFormat => Loc.Get("Alts.GridCellMissingFormat");

    /// <summary>{0} = character name.</summary>
    public static string AltsGridCellUnknownFormat => Loc.Get("Alts.GridCellUnknownFormat");

    /// <summary>{0} = character name, {1} = the quest's state for that character.</summary>
    public static string AltsGridCellStateFormat => Loc.Get("Alts.GridCellStateFormat");

    /// <summary>{0} = character name.</summary>
    public static string AltsGridCellPendingFormat => Loc.Get("Alts.GridCellPendingFormat");

    /// <summary>{0} = character name, {1} = world, {2} = how long ago it was saved.</summary>
    public static string AltsGridColumnTooltipFormat => Loc.Get("Alts.GridColumnTooltipFormat");

    /// <summary>{0} = rows shown, {1} = character columns.</summary>
    public static string AltsGridSummaryFormat => Loc.Get("Alts.GridSummaryFormat");

    public static string AltsGridEmpty => Loc.Get("Alts.GridEmpty");

    public static string AltsGridNeedsCatalog => Loc.Get("Alts.GridNeedsCatalog");

    public static string AltsGridNoCharacters => Loc.Get("Alts.GridNoCharacters");

    public static string AltsGridRewardsHint => Loc.Get("Alts.GridRewardsHint");

    public static string AltsSettingsHeading => Loc.Get("Alts.SettingsHeading");

    public static string AltsSettingsByDataCenter => Loc.Get("Alts.SettingsByDataCenter");

    public static string AltsSettingsByDataCenterHint => Loc.Get("Alts.SettingsByDataCenterHint");

    public static string AltsSettingsShowHidden => Loc.Get("Alts.SettingsShowHidden");

    public static string AltsSettingsShowHiddenHint => Loc.Get("Alts.SettingsShowHiddenHint");

    /// <summary>{0} = how many characters are hidden or not tracked.</summary>
    public static string AltsSettingsHiddenFormat => Loc.Get("Alts.SettingsHiddenFormat");

    public static string AltsSettingsHiddenHint => Loc.Get("Alts.SettingsHiddenHint");

    public static string AltsSettingsHiddenNone => Loc.Get("Alts.SettingsHiddenNone");

    public static string AltsSettingsShowButton => Loc.Get("Alts.SettingsShowButton");

    public static string AltsSettingsTrackButton => Loc.Get("Alts.SettingsTrackButton");

    public static string AltsSettingsForget => Loc.Get("Alts.SettingsForget");

    public static string AltsSettingsForgetDays => Loc.Get("Alts.SettingsForgetDays");

    public static string AltsSettingsForgetHint => Loc.Get("Alts.SettingsForgetHint");

    /// <summary>{0} = how many characters would be forgotten.</summary>
    public static string AltsSettingsForgetButtonFormat => Loc.Get("Alts.SettingsForgetButtonFormat");

    public static string AltsSettingsForgetNone => Loc.Get("Alts.SettingsForgetNone");

    public static string AltsForgetBulkPopup => Loc.Get("Alts.ForgetBulkPopup");

    /// <summary>{0} = how many characters.</summary>
    public static string AltsForgetBulkQuestionFormat => Loc.Get("Alts.ForgetBulkQuestionFormat");

    public static string AltsForgetBulkConfirm => Loc.Get("Alts.ForgetBulkConfirm");

    /// <summary>{0} = how many.</summary>
    public static string AltsForgetBulkDoneFormat => Loc.Get("Alts.ForgetBulkDoneFormat");

    /// <summary>{0} = forgotten, {1} = kept.</summary>
    public static string AltsForgetBulkSkippedFormat => Loc.Get("Alts.ForgetBulkSkippedFormat");
}
