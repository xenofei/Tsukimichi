namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the Nearby quests window and the server info bar entry. Constant names carry the <c>Discovery</c>
/// prefix so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    // ---- Nearby quests window ----
    public const string DiscoveryWindowTitle = "Nearby quests###TsukimichiNearby";

    /// <summary>{0} zone name, {1} count (two or more).</summary>
    public const string DiscoveryHeaderFormat = "{0} · {1} quests you can start";

    /// <summary>{0} zone name.</summary>
    public const string DiscoveryHeaderOneFormat = "{0} · 1 quest you can start";

    /// <summary>{0} zone name.</summary>
    public const string DiscoveryEmptyFormat = "Nothing to start in {0}";

    /// <summary>Stands in for the zone name when the territory has no place name.</summary>
    public const string DiscoveryUnknownZone = "this zone";

    /// <summary>{0} count of quests in the journal whose giver stands in the zone.</summary>
    public const string DiscoveryAcceptedHeaderFormat = "Also in your journal here ({0})";

    public const string DiscoveryColumnState = "State";
    public const string DiscoveryColumnQuest = "Quest";
    public const string DiscoveryColumnLevel = "Lv";
    public const string DiscoveryColumnJob = "Job";
    public const string DiscoveryColumnActions = "##actions";

    /// <summary>{0} quest level.</summary>
    public const string DiscoveryLevelFormat = "Lv {0}";

    public const string DiscoveryRevealInJournal = "Show in the Journal";
    public const string DiscoveryRowTooltip = "Click: show in the Journal · double-click: flag the giver on the map · right-click or …: more";
    public const string DiscoveryRowMoreTooltip = "More: show in the Journal, flag, teleport, link in chat";

    /// <summary>Tooltip on the "Also in your journal here" caption.</summary>
    public const string DiscoveryAcceptedToggleTooltip = "Click to fold or unfold the quests already in your journal";

    // Settings popup (cog at the top right).
    public const string DiscoverySettingsPopup = "##nearbySettings";
    public const string DiscoverySettingsTooltip = "Nearby quests settings";
    public const string DiscoveryShowDtrLabel = "Show a count in the server info bar";
    public const string DiscoveryDtrShowWhenEmptyLabel = "Keep the entry visible when there is nothing to start";
    public const string DiscoveryIncludeOtherJobLabel = "Include quests ready on another job";

    // ---- Server info bar entry ----
    public const string DiscoveryDtrTitle = "Tsukimichi";

    /// <summary>{0} count of startable quests in the current zone.</summary>
    public const string DiscoveryDtrTextFormat = "☾ {0}";

    // The server info bar tooltip names the state the count means (Ready, docs/glossary.md), not just "can start".

    /// <summary>{0} count, {1} zone name.</summary>
    public const string DiscoveryDtrTooltipHeaderFormat = "{0} quests Ready to start in {1}";

    /// <summary>{0} zone name.</summary>
    public const string DiscoveryDtrTooltipHeaderOneFormat = "1 quest Ready to start in {0}";

    /// <summary>{0} zone name.</summary>
    public const string DiscoveryDtrTooltipEmptyFormat = "Nothing Ready to start in {0}";

    public const string DiscoveryDtrTooltipClick = "Click to open Nearby quests";
}
