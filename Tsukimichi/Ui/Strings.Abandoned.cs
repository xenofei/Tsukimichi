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
    public static string AbandonedGiverPrefix => Loc.Get("AbandonedGiverPrefix");
    public static string AbandonedAtPrefix => Loc.Get("AbandonedAtPrefix");

    // ---- Filter ----
    public static string AbandonedOnly => Loc.Get("AbandonedOnly");
    public static string AbandonedOnlyTooltip => Loc.Get("AbandonedOnlyTooltip");
    public const string AbandonedChip = FilterNames.Abandoned;

    // ---- Chat ----
    public static string AbandonedChatPrefix => Loc.Get("AbandonedChatPrefix");

    /// <summary>{0} = "step 3 of 5".</summary>
    public const string AbandonedChatStepFormat = " ({0})";

    public static string AbandonedConfigNotice => Loc.Get("AbandonedConfigNotice");
    public static string AbandonedConfigNoticeHint => Loc.Get("AbandonedConfigNoticeHint");
}
