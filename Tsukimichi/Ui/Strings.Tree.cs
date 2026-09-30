using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the Journal tree rows and the Journal tab (T11). Every constant is prefixed <c>Tree</c> so the partial
/// halves never collide.
/// </summary>
static partial class Strings
{
    /// <summary>A tree node's count: "done / total" with thousands separators.</summary>
    public const string TreeCountFormat = "{0:N0} / {1:N0}";

    /// <summary>The folded-path and halo tooltip header: the exact completion to two decimals.</summary>
    public static string TreeFractionFormat => Loc.Get("TreeFractionFormat");

    /// <summary>Hover text of a node's Ready badge.</summary>
    public static string TreeReadyBadgeFormat => Loc.Get("TreeReadyBadgeFormat");
}
