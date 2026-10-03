namespace Tsukimichi.Core.Ui;

/// <summary>How a guarded action is confirmed (feature plan v6 S2, the one safety table).</summary>
public enum SafetyTier
{
    /// <summary>One plain click: the change is small and the Undo toast follows.</summary>
    None,

    /// <summary>
    /// A single change that can be undone: it acts only while Ctrl or Shift is held (<see cref="ClickGuard"/>), at
    /// once, and the Undo toast follows. With <see cref="SafetySettings.TwoClick"/> two clicks do instead.
    /// </summary>
    Armed,

    /// <summary>
    /// A bulk or irreversible change, behind a confirm whose button must be pressed and held (<see cref="ConfirmGate"/>),
    /// or clicked with Ctrl or Shift held. With <see cref="SafetySettings.TwoClick"/> two clicks replace the hold.
    /// </summary>
    Hold,
}

/// <summary>Every action the safety table covers. Stop buttons are never guarded: stopping is always safe.</summary>
public enum GuardedAction
{
    /// <summary>Mark as unique (detail pane): the quest joins Moonlit treasures.</summary>
    MarkUnique,

    /// <summary>Not unique (hide) (a Moonlit row's menu): the reward leaves Moonlit treasures.</summary>
    MarkNotUnique,

    /// <summary>Restore shipped verdict (detail pane, Moonlit menu, Settings › Your Moonlit verdicts).</summary>
    RestoreVerdict,

    /// <summary>Saving the note of a verdict ("Add note" on the Undo toast).</summary>
    EditVerdictNote,

    /// <summary>Don't track this character: its progress stops being saved.</summary>
    DontTrackCharacter,

    /// <summary>Hide or show a character in the list.</summary>
    HideCharacter,

    /// <summary>Unpin a quest.</summary>
    Unpin,

    /// <summary>Restore all verdicts (Settings › Your Moonlit verdicts).</summary>
    RestoreAllVerdicts,

    /// <summary>Pin all of a route's steps.</summary>
    PinAll,

    /// <summary>Replace Questionable's priority list with Tsukimichi's.</summary>
    QuestionableReplace,

    /// <summary>Apply the recommended settings to a companion plugin.</summary>
    CompanionApply,

    /// <summary>Forget one character's saved data.</summary>
    ForgetCharacter,

    /// <summary>Forget the characters not seen for a while.</summary>
    ForgetCharacters,

    /// <summary>The last step of Delete all data.</summary>
    DeleteAllData,

    /// <summary>Reset in the filter drawer (plan v7 UI-2): clears every filter and the search.</summary>
    ResetFilters,
}

/// <summary>
/// The one safety table (feature plan v6 S1/S2, owner point 9): which gate each action that changes data goes through,
/// whether an Undo follows, and the timing every gate shares. The UI draws from this table only, so a new guarded
/// button picks its tier here rather than deciding for itself. Kept free of ImGui so the rules are tested.
/// </summary>
public static class SafetyRules
{
    /// <summary>The press-and-hold length out of the box.</summary>
    public const float DefaultHoldSeconds = ConfirmGate.DefaultHoldSeconds;

    /// <summary>The shortest hold Settings offers.</summary>
    public const float MinHoldSeconds = 0.3f;

    /// <summary>The longest hold Settings offers.</summary>
    public const float MaxHoldSeconds = 2.0f;

    /// <summary>The longest hold in tenths of a second: how many countdown labels the hold button needs.</summary>
    public const int MaxHoldTenths = 20;

    /// <summary>How long the Undo toast stays (hover pauses it).</summary>
    public const double UndoSeconds = 8.0;

    /// <summary>Two-click mode: how long the first click waits for the second.</summary>
    public const double SecondClickWindowSeconds = 4.0;

    /// <summary>Two-click mode: a second click sooner than this is a double-click and does not confirm.</summary>
    public const double SecondClickMinGapSeconds = 0.25;

    /// <summary>How long the "hold Shift or Ctrl" hint stays after a click that was not armed.</summary>
    public const double RefusedHintSeconds = 1.6;

    /// <summary>The gate an action goes through.</summary>
    public static SafetyTier TierOf(GuardedAction action) => action switch
    {
        GuardedAction.MarkUnique => SafetyTier.Armed,
        GuardedAction.MarkNotUnique => SafetyTier.Armed,
        GuardedAction.RestoreVerdict => SafetyTier.Armed,
        GuardedAction.DontTrackCharacter => SafetyTier.Armed,
        GuardedAction.EditVerdictNote => SafetyTier.None,
        GuardedAction.HideCharacter => SafetyTier.None,
        GuardedAction.Unpin => SafetyTier.None,
        GuardedAction.ResetFilters => SafetyTier.None,
        GuardedAction.RestoreAllVerdicts => SafetyTier.Hold,
        GuardedAction.PinAll => SafetyTier.Hold,
        GuardedAction.QuestionableReplace => SafetyTier.Hold,
        GuardedAction.CompanionApply => SafetyTier.Hold,
        GuardedAction.ForgetCharacter => SafetyTier.Hold,
        GuardedAction.ForgetCharacters => SafetyTier.Hold,
        GuardedAction.DeleteAllData => SafetyTier.Hold,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Every guarded action needs a row in the safety table."),
    };

    /// <summary>Whether the floating Undo toast follows the action. The irreversible ones have nothing to undo.</summary>
    public static bool OffersUndo(GuardedAction action) => action switch
    {
        GuardedAction.QuestionableReplace => false,
        GuardedAction.CompanionApply => false,
        GuardedAction.ForgetCharacter => false,
        GuardedAction.ForgetCharacters => false,
        GuardedAction.DeleteAllData => false,
        _ => true,
    };

    /// <summary>
    /// The saved hold length made safe: within <see cref="MinHoldSeconds"/>..<see cref="MaxHoldSeconds"/>, in whole
    /// tenths, and the default for a value that is not a number.
    /// </summary>
    public static float ClampHoldSeconds(float seconds)
    {
        if (!float.IsFinite(seconds))
        {
            return DefaultHoldSeconds;
        }

        var tenths = MathF.Round(Math.Clamp(seconds, MinHoldSeconds, MaxHoldSeconds) * 10f);
        return tenths / 10f;
    }
}

/// <summary>The user's safety settings as every gate reads them each frame.</summary>
/// <param name="HoldSeconds">The press-and-hold length (clamped by <see cref="SafetyRules.ClampHoldSeconds"/>).</param>
/// <param name="TwoClick">Two clicks confirm instead of a held key or a held button, for hand strain.</param>
public readonly record struct SafetySettings(float HoldSeconds, bool TwoClick)
{
    /// <summary>The settings out of the box.</summary>
    public static SafetySettings Default => new(SafetyRules.DefaultHoldSeconds, false);
}
