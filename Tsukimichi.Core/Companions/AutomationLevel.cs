namespace Tsukimichi.Core.Companions;

/// <summary>
/// The four automation levels of Settings › Automation › Automation buttons (1.18, A10), lowest first. Each level shows
/// the buttons of the levels below it and adds its own (<see cref="AutomationLevels.Adds"/>).
/// </summary>
public enum AutomationLevel
{
    /// <summary>Flag and Map only: Tsukimichi tracks and never presses anything for the player.</summary>
    TrackerOnly,

    /// <summary>Adds Teleport and the aethernet through Lifestream (and Gather, whose GatherBuddy command teleports).</summary>
    Travel,

    /// <summary>Adds Walk to giver and Go to giver through vnavmesh.</summary>
    TravelAndWalking,

    /// <summary>Adds the full hand-offs: Questionable, AutoDuty and Artisan play the quest, the duty or the craft.</summary>
    FullHandOffs,
}

/// <summary>
/// The buttons that hand the game to another plugin, each shown or hidden by the automation level and the per-button
/// fine-tuning (1.18, A10). A hidden button is not drawn anywhere (the detail pane's pills and round buttons, menus,
/// panels, the Todo overlay); a Stop for a run already under way always shows.
/// </summary>
[Flags]
public enum AutomationButtons
{
    None = 0,

    /// <summary>Teleport and the aethernet hop through Lifestream, wherever they appear.</summary>
    Teleport = 1,

    /// <summary>Gather with GatherBuddy (its <c>/gather</c> command teleports toward the node).</summary>
    Gather = 2,

    /// <summary>Walk to giver through vnavmesh.</summary>
    Walk = 4,

    /// <summary>Go to giver: teleport, aethernet and walk in one click.</summary>
    GoTo = 8,

    /// <summary>Start (this quest), Send to Questionable and Add to Questionable priority.</summary>
    Questionable = 16,

    /// <summary>Run with AutoDuty.</summary>
    AutoDuty = 32,

    /// <summary>Craft with Artisan.</summary>
    Artisan = 64,
}

/// <summary>
/// Which buttons each automation level shows, the level a set of buttons reads as (or Custom), and the one-time
/// migration (1.18, A10). The per-button toggles are the stored truth: picking a level sets them, and the level shown is
/// derived from them, so a player whose toggles match no level sees "Custom" and keeps exactly the buttons they had.
/// Pure, so the Settings page, every surface and the tests read the same answers.
/// </summary>
public static class AutomationLevels
{
    /// <summary>The level a fresh install starts at (coordinator's decision at approval).</summary>
    public const AutomationLevel NewUserDefault = AutomationLevel.Travel;

    /// <summary>Every button a level can show.</summary>
    public const AutomationButtons Every = AutomationButtons.Teleport | AutomationButtons.Gather | AutomationButtons.Walk | AutomationButtons.GoTo
        | AutomationButtons.Questionable | AutomationButtons.AutoDuty | AutomationButtons.Artisan;

    /// <summary>The levels, lowest first, as the Settings cards show them.</summary>
    public static IReadOnlyList<AutomationLevel> All { get; } =
        [AutomationLevel.TrackerOnly, AutomationLevel.Travel, AutomationLevel.TravelAndWalking, AutomationLevel.FullHandOffs];

    /// <summary>The buttons, in the order the fine-tuning list shows them.</summary>
    public static IReadOnlyList<AutomationButtons> Buttons { get; } =
    [
        AutomationButtons.Teleport, AutomationButtons.Gather, AutomationButtons.Walk, AutomationButtons.GoTo,
        AutomationButtons.Questionable, AutomationButtons.AutoDuty, AutomationButtons.Artisan,
    ];

    /// <summary>The buttons <paramref name="level"/> adds to the level below it.</summary>
    public static AutomationButtons Adds(AutomationLevel level) => level switch
    {
        AutomationLevel.Travel => AutomationButtons.Teleport | AutomationButtons.Gather,
        AutomationLevel.TravelAndWalking => AutomationButtons.Walk | AutomationButtons.GoTo,
        AutomationLevel.FullHandOffs => AutomationButtons.Questionable | AutomationButtons.AutoDuty | AutomationButtons.Artisan,
        _ => AutomationButtons.None,
    };

    /// <summary>Every button <paramref name="level"/> shows: its own and those of the levels below it.</summary>
    public static AutomationButtons ButtonsOf(AutomationLevel level)
    {
        var shown = AutomationButtons.None;
        foreach (var each in All)
        {
            if (each > level)
            {
                break;
            }

            shown |= Adds(each);
        }

        return shown;
    }

    /// <summary>The level that shows exactly <paramref name="shown"/>; null (Custom) when no level does.</summary>
    public static AutomationLevel? LevelOf(AutomationButtons shown)
    {
        shown &= Every;
        foreach (var level in All)
        {
            if (ButtonsOf(level) == shown)
            {
                return level;
            }
        }

        return null;
    }

    /// <summary>The level a button first appears at.</summary>
    public static AutomationLevel LevelOfButton(AutomationButtons button)
    {
        foreach (var level in All)
        {
            if ((Adds(level) & button) != 0)
            {
                return level;
            }
        }

        return AutomationLevel.TrackerOnly;
    }

    /// <summary>Whether <paramref name="shown"/> includes every button in <paramref name="button"/>.</summary>
    public static bool Shows(AutomationButtons shown, AutomationButtons button) => (shown & button) == button;

    /// <summary><paramref name="shown"/> with <paramref name="button"/> turned on or off (the fine-tuning toggles).</summary>
    public static AutomationButtons With(AutomationButtons shown, AutomationButtons button, bool on) =>
        (on ? shown | button : shown & ~button) & Every;

    /// <summary>
    /// The buttons to show after loading the configuration: the saved choice (bits this build does not know dropped);
    /// for a configuration saved before 1.18, the buttons that build showed, so nothing a player uses disappears
    /// (Teleport, Gather, Questionable, AutoDuty and Artisan always showed; Walk and Go to giver had their own toggles,
    /// <paramref name="legacyWalk"/> and <paramref name="legacyGoTo"/>); for a fresh install,
    /// <see cref="NewUserDefault"/>.
    /// </summary>
    /// <param name="saved">The saved buttons; null when the configuration predates the level or is new.</param>
    /// <param name="hadFile">Whether a configuration file existed before this load.</param>
    public static AutomationButtons Migrate(AutomationButtons? saved, bool hadFile, bool legacyWalk, bool legacyGoTo)
    {
        if (saved is { } known)
        {
            return known & Every;
        }

        if (!hadFile)
        {
            return ButtonsOf(NewUserDefault);
        }

        var shown = Every;
        shown = With(shown, AutomationButtons.Walk, legacyWalk);
        return With(shown, AutomationButtons.GoTo, legacyGoTo);
    }
}
