using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the 1.14.0 rail (plan v7 UI-4, spec Revision 3): the Journal badge's newly-ready count, its tooltip, the
/// Newly ready list it opens, and its setting. Constant names carry the <c>Rail</c> or <c>ConfigJournalBadge</c> prefix
/// so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    /// <summary>The scope chip and table title of the list the badge opens, and the setting's default choice.</summary>
    public static string RailNewlyReady => Loc.Get("RailNewlyReady");

    /// <summary>{0} = quests newly ready since the player last looked.</summary>
    public static string RailNewlyReadyFormat => Loc.Get("RailNewlyReadyFormat");

    /// <summary>{0} = Ready main scenario and unlock quests.</summary>
    public static string RailStoryReadyFormat => Loc.Get("RailStoryReadyFormat");

    /// <summary>Joins the badge tooltip's counts.</summary>
    public static string RailCountSeparator => Loc.Get("RailCountSeparator");

    public static string RailNewlyReadyClick => Loc.Get("RailNewlyReadyClick");

    public static string ConfigJournalBadge => Loc.Get("ConfigJournalBadge");
    public static string ConfigJournalBadgeHint => Loc.Get("ConfigJournalBadgeHint");
    public static string ConfigJournalBadgeStory => Loc.Get("ConfigJournalBadgeStory");
    public static string ConfigJournalBadgeEvery => Loc.Get("ConfigJournalBadgeEvery");
    public static string ConfigJournalBadgeNothing => Loc.Get("ConfigJournalBadgeNothing");
}
