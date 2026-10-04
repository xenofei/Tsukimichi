using System;
using Tsukimichi.Core.Companions;

namespace Tsukimichi.Ui;

/// <summary>
/// The automation level's one question for every surface (1.18, A10): is this hand-off button shown? A button above the
/// chosen level, or turned off in its fine-tuning, is hidden, not greyed: the detail pane's pills, round buttons and "…"
/// menu, the Hand in rows, the Duties section, the Send to Questionable buttons and menus, travel menus and rows, the
/// Todo overlay and the settings that only serve that button. A Stop for a run already under way always shows. The
/// plugin attaches the configuration at load; until then (and in tools) every button shows.
/// </summary>
public static class AutomationGate
{
    /// <summary>The buttons whose runs a "Needs you" alert or the duty guard can watch: anything that moves or plays for the player.</summary>
    public const AutomationButtons RunningHandOffs = AutomationButtons.Walk | AutomationButtons.GoTo
        | AutomationButtons.Questionable | AutomationButtons.AutoDuty | AutomationButtons.Artisan;

    private static Func<AutomationButtons>? source;

    /// <summary>Reads the shown buttons from <paramref name="shown"/> (the configuration) from now on.</summary>
    public static void Attach(Func<AutomationButtons> shown) => source = shown ?? throw new ArgumentNullException(nameof(shown));

    /// <summary>Forgets the configuration (the plugin unloads): every button shows again.</summary>
    public static void Detach() => source = null;

    /// <summary>The buttons shown now.</summary>
    public static AutomationButtons Shown => source?.Invoke() ?? AutomationLevels.Every;

    /// <summary>Whether every button in <paramref name="button"/> is shown.</summary>
    public static bool Shows(AutomationButtons button) => AutomationLevels.Shows(Shown, button);

    /// <summary>
    /// <paramref name="actions"/> while the level shows Questionable, else null: the Send to Questionable buttons and
    /// menus of the panes and the Todo overlay draw from it, so they go with the level (a run's Stop stays on the pill).
    /// </summary>
    public static QuestionableActions? Questionable(QuestionableActions? actions) =>
        Shows(AutomationButtons.Questionable) ? actions : null;

    /// <summary>Whether any button in <paramref name="buttons"/> is shown.</summary>
    public static bool ShowsAny(AutomationButtons buttons) => (Shown & buttons) != 0;
}
