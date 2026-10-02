using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the Nearby quests window and the server info bar entry. Constant names carry the <c>Discovery</c>
/// prefix so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    // ---- Nearby quests window ----
    public static string DiscoveryWindowTitle => Loc.Get("DiscoveryWindowTitle");

    /// <summary>{0} zone name, {1} count (two or more).</summary>
    public static string DiscoveryHeaderFormat => Loc.Get("DiscoveryHeaderFormat");

    /// <summary>{0} zone name.</summary>
    public static string DiscoveryHeaderOneFormat => Loc.Get("DiscoveryHeaderOneFormat");

    /// <summary>{0} zone name.</summary>
    public static string DiscoveryEmptyFormat => Loc.Get("DiscoveryEmptyFormat");

    /// <summary>Stands in for the zone name when the territory has no place name.</summary>
    public static string DiscoveryUnknownZone => Loc.Get("DiscoveryUnknownZone");

    /// <summary>{0} count of quests in the journal whose giver stands in the zone.</summary>
    public static string DiscoveryAcceptedHeaderFormat => Loc.Get("DiscoveryAcceptedHeaderFormat");

    public static string DiscoveryColumnState => Loc.Get("DiscoveryColumnState");
    public static string DiscoveryColumnQuest => Loc.Get("DiscoveryColumnQuest");
    public static string DiscoveryColumnLevel => Loc.Get("DiscoveryColumnLevel");
    public static string DiscoveryColumnJob => Loc.Get("DiscoveryColumnJob");
    public const string DiscoveryColumnActions = "##actions";

    /// <summary>{0} quest level.</summary>
    public static string DiscoveryLevelFormat => Loc.Get("DiscoveryLevelFormat");

    public static string DiscoveryRevealInJournal => Loc.Get("DiscoveryRevealInJournal");
    public static string DiscoveryRowTooltip => Loc.Get("DiscoveryRowTooltip");
    public static string DiscoveryRowMoreTooltip => Loc.Get("DiscoveryRowMoreTooltip");

    /// <summary>Tooltip on the "Also in your journal here" caption.</summary>
    public static string DiscoveryAcceptedToggleTooltip => Loc.Get("DiscoveryAcceptedToggleTooltip");

    // Settings popup (cog at the top right).
    public static string DiscoverySettingsTooltip => Loc.Get("DiscoverySettingsTooltip");
    public static string DiscoveryShowDtrLabel => Loc.Get("DiscoveryShowDtrLabel");
    public static string DiscoveryDtrShowWhenEmptyLabel => Loc.Get("DiscoveryDtrShowWhenEmptyLabel");
    public static string DiscoveryIncludeOtherJobLabel => Loc.Get("DiscoveryIncludeOtherJobLabel");

    // ---- Server info bar entry ----
    public const string DiscoveryDtrTitle = "Tsukimichi";

    /// <summary>{0} count of startable quests in the current zone.</summary>
    public const string DiscoveryDtrTextFormat = "☾ {0}";

    // The server info bar tooltip names the state the count means (Ready, docs/glossary.md), not just "can start".

    /// <summary>{0} count, {1} zone name.</summary>
    public static string DiscoveryDtrTooltipHeaderFormat => Loc.Get("DiscoveryDtrTooltipHeaderFormat");

    /// <summary>{0} zone name.</summary>
    public static string DiscoveryDtrTooltipHeaderOneFormat => Loc.Get("DiscoveryDtrTooltipHeaderOneFormat");

    /// <summary>{0} zone name.</summary>
    public static string DiscoveryDtrTooltipEmptyFormat => Loc.Get("DiscoveryDtrTooltipEmptyFormat");

    public static string DiscoveryDtrTooltipClick => Loc.Get("DiscoveryDtrTooltipClick");
}
