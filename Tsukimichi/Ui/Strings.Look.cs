using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for 1.8.0 "Moon Road part 2" (feature plan v5, R3): the quest table's title line. English only until
/// localization reopens.
/// </summary>
static partial class Strings
{
    /// <summary>{0} = rows shown, {1} = quests in the node.</summary>
    public static string TableTitleCountFormat => Loc.Get("TableTitleCountFormat");

    public static string TableTitleCountTooltip => Loc.Get("TableTitleCountTooltip");

    /// <summary>After the parent node in the title's breadcrumb ("Main Scenario ›"), as in <see cref="FoldedScopeFormat"/>.</summary>
    public const string TitleCrumbSuffix = " ›";
}
