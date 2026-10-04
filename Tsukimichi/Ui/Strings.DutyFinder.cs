using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the Duty Finder unlock hint (P13): the panel beside the game's Duty Finder and its setting in
/// Settings › Integrations. Constant names carry the <c>DutyHint</c> prefix so this part of the partial class never
/// collides with the others.
/// </summary>
static partial class Strings
{
    /// <summary>ImGui id of the borderless panel window (never shown).</summary>
    public const string DutyHintWindowId = "##tsukimichi-duty-finder-hint";

    /// <summary>Caption above the duty name.</summary>
    public static string DutyHintCaption => Loc.Get("DutyHintCaption");

    /// <summary>Opens the quest block; the quest name follows on the same line.</summary>
    public static string DutyHintUnlockedBy => Loc.Get("DutyHintUnlockedBy");

    public static string DutyHintReveal => Loc.Get("DutyHintReveal");

    public static string DutyHintRevealHint => Loc.Get("DutyHintRevealHint");

    public static string DutyHintFlagGiver => Loc.Get("DutyHintFlagGiver");

    public static string DutyHintFlagGiverHint => Loc.Get("DutyHintFlagGiverHint");

    public static string DutyHintNoGiver => Loc.Get("DutyHintNoGiver");

    /// <summary>Folded quest count: {0} is how many more quests also unlock the duty.</summary>
    public static string DutyHintMoreFormat => Loc.Get("DutyHintMoreFormat");

    /// <summary>The caption for a selected roulette (1.19.0, N4).</summary>
    public static string DutyHintRouletteCaption => Loc.Get("DutyHintRouletteCaption");

    public static string DutyHintRouletteRouteTooltip => Loc.Get("DutyHintRouletteRouteTooltip");

    public static string DutyHintSetting => Loc.Get("DutyHintSetting");

    public static string DutyHintSettingHint => Loc.Get("DutyHintSettingHint");
}
