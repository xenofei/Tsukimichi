using Tsukimichi.Core.Query;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the spoiler shield (T19): Settings › Spoilers, the detail pane's reveal button, the artwork placeholder
/// and the Sprout mode quick view. Every constant is prefixed <c>Spoiler</c>, <c>Artwork</c> or <c>Sprout</c> so the
/// partial halves never collide.
/// </summary>
static partial class Strings
{
    // ---- Artwork ----
    public static string ArtworkHidden => Loc.Get("ArtworkHidden");

    // ---- Detail pane ----
    public static string SpoilerRevealName => Loc.Get("SpoilerRevealName");
    public static string SpoilerRevealNameTooltip => Loc.Get("SpoilerRevealNameTooltip");
    public static string SpoilerMaskedNote => Loc.Get("SpoilerMaskedNote");

    // ---- Settings › Spoilers ----
    public static string SettingsSpoilers => Loc.Get("SettingsSpoilers");
    public static string SpoilerHideNames => Loc.Get("SpoilerHideNames");
    public static string SpoilerHideNamesHelp => Loc.Get("SpoilerHideNamesHelp");
    public static string SpoilerAhead => Loc.Get("SpoilerAhead");
    public static string SpoilerAheadHelp => Loc.Get("SpoilerAheadHelp");
    public static string SpoilerHideArtwork => Loc.Get("SpoilerHideArtwork");
    public static string SpoilerHideArtworkHelp => Loc.Get("SpoilerHideArtworkHelp");
    public static string SpoilerCharacterLabel => Loc.Get("SpoilerCharacterLabel");
    public static string SpoilerCharacterFormat => Loc.Get("SpoilerCharacterFormat");
    public static string SpoilerCharacterNone => Loc.Get("SpoilerCharacterNone");
    public static string SpoilerCharacterDefault => Loc.Get("SpoilerCharacterDefault");
    public static string SpoilerCharacterOn => Loc.Get("SpoilerCharacterOn");
    public static string SpoilerCharacterOff => Loc.Get("SpoilerCharacterOff");
    public static string SpoilerCharacterHelp => Loc.Get("SpoilerCharacterHelp");
    public static string SpoilerMaskedCountFormat => Loc.Get("SpoilerMaskedCountFormat");

    // ---- Sprout mode quick view ----
    public const string PresetSprout = FilterNames.Sprout;
    public static string PresetSproutTooltip => Loc.Get("PresetSproutTooltip");
    public static string SproutReachFormat => Loc.Get("SproutReachFormat");
    public static string SproutFoldedTooltip => Loc.Get("SproutFoldedTooltip");
}
