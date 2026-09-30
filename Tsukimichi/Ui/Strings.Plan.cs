using Tsukimichi.Core.Plan;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for "Clear my blues" (P3): the Plan tab, its todo overlay block and its setting. Every constant carries
/// the <c>Plan</c> prefix so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    // ---- Rail ----
    public const string PlanTab = "My blues";
    public const string PlanTabTooltip = "Clear my blues: every unlock quest you have left, by expansion and zone in story order";

    // ---- Left column ----
    public const string PlanTitle = "Clear my blues";

    /// <summary>{0} = quests left, {1} = ready now.</summary>
    public const string PlanSummaryFormat = "{0:N0} unlock quests left · {1:N0} ready";
    public const string PlanSummaryBrowse = "Every unlock quest in the game; pick a character to see what is left.";
    public const string PlanKinds = "What it unlocks";
    public const string PlanShow = "Show";
    public const string PlanAllKinds = "All kinds";
    public const string PlanAllKindsTooltip = "Show every kind of unlock again";

    /// <summary>{0} = kind name, {1} = quests of that kind shown with the other filters.</summary>
    public const string PlanKindChipFormat = "{0} {1:N0}";
    public const string PlanKindChipTooltip = "Click to show or hide this kind";
    public const string PlanReadyOnly = "Ready only";
    public const string PlanReadyOnlyTooltip = "Only the quests you can pick up now, on this job or another";
    public const string PlanSprout = "Sprout mode";
    public const string PlanSproutTooltip = "Only the expansions your main scenario has reached. Turns on by itself when the Journal's Sprout mode quick view is on.";

    /// <summary>{0} = number of later expansions hidden.</summary>
    public const string PlanSproutHiddenFormat = "{0} later expansions hidden by Sprout mode";
    public const string PlanExpansions = "Expansions";

    /// <summary>{0} = left, {1} = ready.</summary>
    public const string PlanExpansionCountFormat = "{0:N0} · {1:N0} ready";
    public const string PlanExpansionClickHint = "Click to open this expansion's block";

    // ---- Main column ----
    public const string PlanCopy = "Copy as checklist";
    public const string PlanCopyTooltip = "Copies the plan as shown (filters applied) as a Markdown checklist, ready to paste into Discord or a document. Quest names follow the spoiler shield; nothing names your character.";

    /// <summary>{0} = number of quests copied.</summary>
    public const string PlanCopiedFormat = "Copied {0:N0} quests";

    /// <summary>{0} = shown, {1} = left in total.</summary>
    public const string PlanShowingFormat = "Showing {0:N0} of {1:N0}";
    public const string PlanEmptyAllDone = "Every unlock quest is done. Nothing blue left.";
    public const string PlanEmptyFiltered = "No unlock quest matches these filters.";
    public const string PlanLoading = "The catalog is loading…";

    /// <summary>{0} = expansion, {1} = left.</summary>
    public const string PlanCardFormat = "{0}  ·  {1:N0} left";

    /// <summary>{0} = ready count.</summary>
    public const string PlanCardReadyFormat = "{0:N0} ready";
    public const string PlanPin = "Pin to overlay";
    public const string PlanUnpin = "Unpin from overlay";
    public const string PlanPinTooltip = "Show this expansion's Ready unlock quests in the Todo overlay (and turn the overlay on)";
    public const string PlanUnpinTooltip = "Stop showing this expansion's block in the Todo overlay";
    public const string PlanCardToggleTooltip = "Click to fold or unfold this expansion";
    public const string PlanUnknownZone = "Giver not placed";
    public const string PlanFlag = "Flag";
    public const string PlanFlagTooltip = "Flag the quest giver on the map";
    public const string PlanReveal = "Reveal";
    public const string PlanRevealTooltip = "Show the quest in the Journal";
    public const string PlanRowClickHint = "Click to show the quest in the detail pane";

    // ---- Help › Flight and nearby ----
    public const string PlanHelpTitle = "My blues: clear your unlock quests";
    public const string PlanHelpBody = "The My blues tab is every blue unlock quest you have left, by expansion and then zone, zones in the order the story reaches them and quests by level. Each row shows its moon, what it unlocks (Dungeon, Trial, Raid, Alliance raid, Field operation, Job, Allied society, Flying, System, Other; \"leads to\" when the quest is a step towards it), its status and what blocks it, with Flag and Reveal. Filter by kind, Ready only, or Sprout mode to hide expansions your story has not reached. Copy as checklist puts the list as shown on the clipboard as Markdown, ready for Discord (\"- [ ] Hallo Halatali (Lv 20, Western Thanalan) — unlocks: Dungeon: Halatali\"), with nothing that names you. Pin to overlay on an expansion shows its Ready unlock quests in the Todo overlay; Settings › Todo overlay turns that section off. Seasonal event quests are not in the plan.";

    // ---- Todo overlay ----
    public const string PlanTodoSection = "Clear my blues";

    /// <summary>{0} = expansion, {1} = quests left there.</summary>
    public const string PlanTodoNoteFormat = "{0} · {1:N0} left";
    public const string PlanTodoConfig = "Pinned expansion from Clear my blues";
    public const string PlanTodoConfigHint = "The Ready unlock quests of the expansion pinned from the My blues tab (\"Pin to overlay\" on an expansion).";

    /// <summary>{0} = expansion name.</summary>
    public const string PlanTodoConfigPinnedFormat = "Pinned: {0}";
    public const string PlanTodoConfigNone = "Nothing pinned yet";

    /// <summary>The pill text of a kind in the Plan tab (short enough for a row).</summary>
    public static string PlanKindName(UnlockKind kind) => UnlockKinds.Name(kind);
}
