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
    public const string DutyHintCaption = "Locked duty";

    /// <summary>Opens the quest block; the quest name follows on the same line.</summary>
    public const string DutyHintUnlockedBy = "Unlocked by:";

    public const string DutyHintReveal = "Reveal in Tsukimichi";

    public const string DutyHintRevealHint = "Open Tsukimichi's Journal on this quest: its requirements, the path to it and where to pick it up.";

    public const string DutyHintFlagGiver = "Flag giver";

    public const string DutyHintFlagGiverHint = "Open the map with a flag on the NPC who gives this quest.";

    public const string DutyHintNoGiver = "The quest giver has no map position to flag.";

    /// <summary>Folded quest count: {0} is how many more quests also unlock the duty.</summary>
    public const string DutyHintMoreFormat = "and {0} more";

    public const string DutyHintSetting = "Duty Finder unlock hint";

    public const string DutyHintSettingHint = "When you select a padlocked duty in the Duty Finder or Raid Finder, a small panel beside the window names the quest that unlocks it, its state and what it is waiting for, with buttons to reveal it in Tsukimichi or flag its giver. Nothing shows for a duty you have unlocked, or one no known quest unlocks. It reads the selected duty only; it never queues or opens one.";
}
