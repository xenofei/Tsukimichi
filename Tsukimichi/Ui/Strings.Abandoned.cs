using Tsukimichi.Core.Query;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the abandoned-quest ledger (P10): the Characters dashboard section, the Abandoned filter and its
/// chip, the chat line and its setting. Constant names carry the <c>Abandoned</c> prefix so this part of the partial
/// class never collides with the others.
/// </summary>
static partial class Strings
{
    // ---- Characters dashboard ----
    /// <summary>{0} = number of abandoned quests.</summary>
    public static string AbandonedHeaderFormat => Loc.Get("AbandonedHeaderFormat");
    public static string AbandonedNone => Loc.Get("AbandonedNone");
    public static string AbandonedShowInJournal => Loc.Get("AbandonedShowInJournal");
    public static string AbandonedShowInJournalTooltip => Loc.Get("AbandonedShowInJournalTooltip");
    public static string AbandonedFlag => Loc.Get("AbandonedFlag");
    public static string AbandonedFlagTooltip => Loc.Get("AbandonedFlagTooltip");
    public static string AbandonedTeleport => Loc.Get("AbandonedTeleport");
    public static string AbandonedReveal => Loc.Get("AbandonedReveal");
    public static string AbandonedRevealTooltip => Loc.Get("AbandonedRevealTooltip");
    /// <summary>{0} = NPC name.</summary>
    public static string AbandonedGiverFormat => Loc.Get("AbandonedGiverFormat");
    /// <summary>{0} = date and time.</summary>
    public static string AbandonedAtFormat => Loc.Get("AbandonedAtFormat");

    // ---- Filter ----
    public static string AbandonedOnly => Loc.Get("AbandonedOnly");
    public static string AbandonedOnlyTooltip => Loc.Get("AbandonedOnlyTooltip");
    public const string AbandonedChip = FilterNames.Abandoned;

    // ---- Chat ----
    /// <summary>{0} = the quest link; " (step 3 of 5)" follows the line.</summary>
    public static string AbandonedChatFormat => Loc.Get("AbandonedChatFormat");

    /// <summary>{0} = "step 3 of 5".</summary>
    public const string AbandonedChatStepFormat = " ({0})";

    public static string AbandonedConfigNotice => Loc.Get("AbandonedConfigNotice");
    public static string AbandonedConfigNoticeHint => Loc.Get("AbandonedConfigNoticeHint");
}
