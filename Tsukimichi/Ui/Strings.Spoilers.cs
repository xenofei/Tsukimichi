using Tsukimichi.Core.Query;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the spoiler shield (T19): Settings › Spoilers, the detail pane's reveal button, the artwork placeholder
/// and the Sprout mode quick view. Every constant is prefixed <c>Spoiler</c>, <c>Artwork</c> or <c>Sprout</c> so the
/// partial halves never collide.
/// </summary>
static partial class Strings
{
    // ---- Artwork ----
    public const string ArtworkHidden = "Artwork appears once the quest is in your journal";

    // ---- Detail pane ----
    public const string SpoilerRevealName = "Reveal this name";
    public const string SpoilerRevealNameTooltip = "Show this main scenario quest's real name everywhere until the plugin reloads";
    public const string SpoilerMaskedNote = "Name hidden: this main scenario quest is further ahead than you are";

    // ---- Settings › Spoilers ----
    public const string SettingsSpoilers = "Spoilers";
    public const string SpoilerHideNames = "Hide main scenario names ahead of me";
    public const string SpoilerHideNamesHelp = "Main scenario quests further along the story than you read \"Main scenario quest (Lv 83)\" everywhere: the tree, the table, the detail pane, the status bar, the Todo overlay, chat, Wotsit and search.";
    public const string SpoilerAhead = "Quests ahead to reveal";
    public const string SpoilerAheadHelp = "How many main scenario quests past your current one keep their names (0 to 10).";
    public const string SpoilerHideArtwork = "Hide journal artwork until a quest is in my journal";
    public const string SpoilerHideArtworkHelp = "The banner art sums up a quest; it shows once you accept or complete the quest.";
    public const string SpoilerCharacterLabel = "For this character";
    public const string SpoilerCharacterFormat = "For {0}";
    public const string SpoilerCharacterNone = "No character is shown; the settings above apply.";
    public const string SpoilerCharacterDefault = "Use the settings above";
    public const string SpoilerCharacterOn = "Always shield";
    public const string SpoilerCharacterOff = "Show everything";
    public const string SpoilerCharacterHelp = "A character who finished the story can show everything while an alt stays shielded.";
    public const string SpoilerMaskedCountFormat = "{0:N0} main scenario names hidden for the character shown.";

    // ---- Sprout mode quick view ----
    public const string PresetSprout = FilterNames.Sprout;
    public const string PresetSproutTooltip = "Only the quests of the expansions your main scenario has reached; later sections of the tree fold to their counts";
    public const string SproutReachFormat = "{0:N0} quests in your reach";
    public const string SproutFoldedTooltip = "Beyond your main scenario: folded in Sprout mode";
}
