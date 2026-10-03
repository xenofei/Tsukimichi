using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>Strings for the filter drawer's 1.14.0 sheet (plan v7 UI-2): its sections, captions, summary lines and footer.</summary>
public static partial class Strings
{
    public static string FilterDrawerShow => Loc.Get("FilterDrawerShow");
    public static string FilterDrawerPerCategory => Loc.Get("FilterDrawerPerCategory");

    /// <summary>{0} = how many categories are overridden.</summary>
    public static string FilterDrawerPerCategoryChangedFormat => Loc.Get("FilterDrawerPerCategoryChangedFormat");

    public static string FilterDrawerPinnedFirstCaption => Loc.Get("FilterDrawerPinnedFirstCaption");
    public static string FilterDrawerStalledCaption => Loc.Get("FilterDrawerStalledCaption");
    public static string FilterDrawerStalledFewer => Loc.Get("FilterDrawerStalledFewer");
    public static string FilterDrawerStalledMore => Loc.Get("FilterDrawerStalledMore");

    /// <summary>{0} = quests the filters keep, {1} = quests in the selected tree node.</summary>
    public static string FilterDrawerShowingFormat => Loc.Get("FilterDrawerShowingFormat");

    /// <summary>{0} = how many filters are on.</summary>
    public static string FilterDrawerOnFormat => Loc.Get("FilterDrawerOnFormat");

    /// <summary>{0} = how many groups or kinds are set.</summary>
    public static string FilterDrawerSetFormat => Loc.Get("FilterDrawerSetFormat");

    /// <summary>{0} = how many states there are.</summary>
    public static string FilterDrawerAllStatesFormat => Loc.Get("FilterDrawerAllStatesFormat");

    /// <summary>{0} = states kept, {1} = how many states there are.</summary>
    public static string FilterDrawerSomeStatesFormat => Loc.Get("FilterDrawerSomeStatesFormat");

    public static string FilterDrawerAny => Loc.Get("FilterDrawerAny");

    /// <summary>{0} = the patch series ("7.5").</summary>
    public static string FilterDrawerAddedInFormat => Loc.Get("FilterDrawerAddedInFormat");

    /// <summary>{0} = lowest level, {1} = highest.</summary>
    public static string FilterDrawerLevelFormat => Loc.Get("FilterDrawerLevelFormat");

    public static string FilterDrawerLevelTo => Loc.Get("FilterDrawerLevelTo");
    public static string FilterDrawerNoneOn => Loc.Get("FilterDrawerNoneOn");
    public static string FilterDrawerLevel => Loc.Get("FilterDrawerLevel");
    public static string FilterDrawerJob => Loc.Get("FilterDrawerJob");
    public static string FilterDrawerRewards => Loc.Get("FilterDrawerRewards");
    public static string FilterDrawerMore => Loc.Get("FilterDrawerMore");

    /// <summary>{0} = how many reward kinds wait behind the link.</summary>
    public static string FilterDrawerMoreKindsFormat => Loc.Get("FilterDrawerMoreKindsFormat");

    public static string FilterDrawerFewerKinds => Loc.Get("FilterDrawerFewerKinds");
    public static string FilterDrawerSummaryTooltip => Loc.Get("FilterDrawerSummaryTooltip");
    public static string FilterDrawerResetNothing => Loc.Get("FilterDrawerResetNothing");
    public static string UndoToastFiltersReset => Loc.Get("UndoToastFiltersReset");
}
