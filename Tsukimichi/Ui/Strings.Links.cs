using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the 1.8.0 sharing pieces: "Open on…" (Lodestone, Garland Tools, Console Games Wiki, Teamcraft, FFXIV
/// Collect), the spoiler question before a link opens, Copy for Discord and Copy table as TSV. Constant names carry the
/// <c>Links</c> prefix so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    // ---- Open on… ----
    public static string LinksOpenOn => Loc.Get("LinksOpenOn");
    public static string LinksOpenOnTooltip => Loc.Get("LinksOpenOnTooltip");
    public static string LinksLodestone => Loc.Get("LinksLodestone");
    public static string LinksGarland => Loc.Get("LinksGarland");
    public static string LinksWiki => Loc.Get("LinksWiki");
    public static string LinksTeamcraft => Loc.Get("LinksTeamcraft");

    /// <summary>Hover line of a site the link table has no page for: the site's search opens instead.</summary>
    public static string LinksSearchTooltip => Loc.Get("LinksSearchTooltip");

    public static string LinksOpenOnCollect => Loc.Get("LinksOpenOnCollect");
    public static string LinksOpenItemOnGarland => Loc.Get("LinksOpenItemOnGarland");
    public static string LinksCollectSearchTooltip => Loc.Get("LinksCollectSearchTooltip");

    // ---- The spoiler question ----
    public static string LinksConfirmTitle => Loc.Get("LinksConfirmTitle");
    public static string LinksConfirmBody => Loc.Get("LinksConfirmBody");
    public static string LinksConfirmOpen => Loc.Get("LinksConfirmOpen");
    public static string LinksConfirmCancel => Loc.Get("LinksConfirmCancel");

    // ---- Copy for Discord ----
    public static string LinksCopyDiscord => Loc.Get("LinksCopyDiscord");
    public static string LinksCopyDiscordTooltip => Loc.Get("LinksCopyDiscordTooltip");

    /// <summary>{0} = the part copied next, {1} = how many parts.</summary>
    public static string LinksCopyPartFormat => Loc.Get("LinksCopyPartFormat");

    public static string LinksDiscordAddLinks => Loc.Get("LinksDiscordAddLinks");
    public static string LinksDiscordCopied => Loc.Get("LinksDiscordCopied");

    // ---- Copy table as TSV ----
    public static string LinksCopyTableTsv => Loc.Get("LinksCopyTableTsv");
    public static string LinksCopyTableTsvTooltip => Loc.Get("LinksCopyTableTsvTooltip");
    public static string LinksCopyViewTsv => Loc.Get("LinksCopyViewTsv");
    public static string LinksCopyViewTsvTooltip => Loc.Get("LinksCopyViewTsvTooltip");
    public static string LinksCopyLink => Loc.Get("LinksCopyLink");
}
