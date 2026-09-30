using Tsukimichi.Core.Plan;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for "Clear my blues" (P3): the Plan tab, its todo overlay block and its setting. Every constant carries
/// the <c>Plan</c> prefix so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    // ---- Rail ----
    public static string PlanTab => Loc.Get("PlanTab");
    public static string PlanTabTooltip => Loc.Get("PlanTabTooltip");

    // ---- Left column ----
    public static string PlanTitle => Loc.Get("PlanTitle");

    /// <summary>{0} = quests left, {1} = ready now.</summary>
    public static string PlanSummaryFormat => Loc.Get("PlanSummaryFormat");
    public static string PlanSummaryBrowse => Loc.Get("PlanSummaryBrowse");
    public static string PlanKinds => Loc.Get("PlanKinds");
    public static string PlanShow => Loc.Get("PlanShow");
    public static string PlanAllKinds => Loc.Get("PlanAllKinds");
    public static string PlanAllKindsTooltip => Loc.Get("PlanAllKindsTooltip");

    /// <summary>{0} = kind name, {1} = quests of that kind shown with the other filters.</summary>
    public const string PlanKindChipFormat = "{0} {1:N0}";
    public static string PlanKindChipTooltip => Loc.Get("PlanKindChipTooltip");
    public static string PlanReadyOnly => Loc.Get("PlanReadyOnly");
    public static string PlanReadyOnlyTooltip => Loc.Get("PlanReadyOnlyTooltip");
    public static string PlanSprout => Loc.Get("PlanSprout");
    public static string PlanSproutTooltip => Loc.Get("PlanSproutTooltip");

    /// <summary>{0} = number of later expansions hidden.</summary>
    public static string PlanSproutHiddenFormat => Loc.Get("PlanSproutHiddenFormat");
    public static string PlanExpansions => Loc.Get("PlanExpansions");

    /// <summary>{0} = left, {1} = ready.</summary>
    public static string PlanExpansionCountFormat => Loc.Get("PlanExpansionCountFormat");
    public static string PlanExpansionClickHint => Loc.Get("PlanExpansionClickHint");

    // ---- Main column ----
    public static string PlanCopy => Loc.Get("PlanCopy");
    public static string PlanCopyTooltip => Loc.Get("PlanCopyTooltip");

    /// <summary>{0} = number of quests copied.</summary>
    public static string PlanCopiedFormat => Loc.Get("PlanCopiedFormat");

    /// <summary>{0} = shown, {1} = left in total.</summary>
    public static string PlanShowingFormat => Loc.Get("PlanShowingFormat");
    public static string PlanEmptyAllDone => Loc.Get("PlanEmptyAllDone");
    public static string PlanEmptyFiltered => Loc.Get("PlanEmptyFiltered");
    public static string PlanLoading => Loc.Get("PlanLoading");

    /// <summary>{0} = expansion, {1} = left.</summary>
    public static string PlanCardFormat => Loc.Get("PlanCardFormat");

    /// <summary>{0} = ready count.</summary>
    public static string PlanCardReadyFormat => Loc.Get("PlanCardReadyFormat");
    public static string PlanPin => Loc.Get("PlanPin");
    public static string PlanUnpin => Loc.Get("PlanUnpin");
    public static string PlanPinTooltip => Loc.Get("PlanPinTooltip");
    public static string PlanUnpinTooltip => Loc.Get("PlanUnpinTooltip");
    public static string PlanCardToggleTooltip => Loc.Get("PlanCardToggleTooltip");
    public static string PlanUnknownZone => Loc.Get("PlanUnknownZone");
    public static string PlanFlag => Loc.Get("PlanFlag");
    public static string PlanFlagTooltip => Loc.Get("PlanFlagTooltip");
    public static string PlanReveal => Loc.Get("PlanReveal");
    public static string PlanRevealTooltip => Loc.Get("PlanRevealTooltip");
    public static string PlanRowClickHint => Loc.Get("PlanRowClickHint");

    // ---- Help › Flight and nearby ----
    public static string PlanHelpTitle => Loc.Get("PlanHelpTitle");
    public static string PlanHelpBody => Loc.Get("PlanHelpBody");

    // ---- Todo overlay ----
    public static string PlanTodoSection => Loc.Get("PlanTodoSection");

    /// <summary>{0} = expansion, {1} = quests left there.</summary>
    public static string PlanTodoNoteFormat => Loc.Get("PlanTodoNoteFormat");
    public static string PlanTodoConfig => Loc.Get("PlanTodoConfig");
    public static string PlanTodoConfigHint => Loc.Get("PlanTodoConfigHint");

    /// <summary>{0} = expansion name.</summary>
    public static string PlanTodoConfigPinnedFormat => Loc.Get("PlanTodoConfigPinnedFormat");
    public static string PlanTodoConfigNone => Loc.Get("PlanTodoConfigNone");

    /// <summary>The pill text of a kind in the Plan tab (short enough for a row).</summary>
    public static string PlanKindName(UnlockKind kind) => UnlockKinds.Name(kind);
}
