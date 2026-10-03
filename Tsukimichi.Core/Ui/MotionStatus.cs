using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Ui;

/// <summary>The one-click fix the Motion line offers.</summary>
public enum MotionFix : byte
{
    /// <summary>Nothing to offer.</summary>
    None,

    /// <summary>Windows' "Show animations" is off and Tsukimichi follows it: animate Tsukimichi alone anyway.</summary>
    AnimateAnyway,

    /// <summary>The player chose for Tsukimichi, and Windows now says otherwise: follow Windows again.</summary>
    FollowWindows,
}

/// <summary>
/// Settings' Motion line (feature plan v6 decision 3): whether Tsukimichi animates, and why. Until the player sets
/// Reduce motion, Tsukimichi follows Windows' "Show animations" switch, which can turn every animation off without the
/// player knowing; the line says so and offers <see cref="Fix"/>, an override for Tsukimichi alone. Pure; phrases
/// through <see cref="CoreText"/> (<c>Core.Motion.*</c>).
/// </summary>
/// <param name="On">Whether motion plays (Reduce motion off).</param>
/// <param name="Text">The status line.</param>
/// <param name="Fix">The one-click fix to offer beside it.</param>
public readonly record struct MotionStatus(bool On, string Text, MotionFix Fix)
{
    /// <summary>The fix button's label; empty for <see cref="MotionFix.None"/>.</summary>
    public string FixLabel => Fix switch
    {
        MotionFix.AnimateAnyway => CoreText.T("Core.Motion.AnimateAnyway", "Animate Tsukimichi anyway"),
        MotionFix.FollowWindows => CoreText.T("Core.Motion.FollowWindows", "Follow Windows again"),
        _ => string.Empty,
    };

    /// <summary>
    /// The line for the saved <paramref name="reduceMotion"/>, whether the player <paramref name="chosen"/> it, and
    /// Windows' switch (<paramref name="windowsAnimationsOff"/>: true when off, null when it cannot be read).
    /// </summary>
    public static MotionStatus Of(bool reduceMotion, bool chosen, bool? windowsAnimationsOff)
    {
        if (!chosen)
        {
            return (reduceMotion, windowsAnimationsOff) switch
            {
                (true, true) => new(false, CoreText.T("Core.Motion.OffWindows", "Motion is off, because Windows' \"Show animations\" is off."), MotionFix.AnimateAnyway),
                (false, false) => new(true, CoreText.T("Core.Motion.OnWindows", "Motion is on, following Windows' \"Show animations\"."), MotionFix.None),
                (true, _) => new(false, CoreText.T("Core.Motion.Off", "Motion is off."), MotionFix.None),
                _ => new(true, CoreText.T("Core.Motion.On", "Motion is on."), MotionFix.None),
            };
        }

        // The player's own choice: offer Windows back only when following it would change something.
        var fix = windowsAnimationsOff is { } off && off != reduceMotion ? MotionFix.FollowWindows : MotionFix.None;
        return reduceMotion
            ? new(false, CoreText.T("Core.Motion.OffChosen", "Motion is off: Reduce motion is on."), fix)
            : windowsAnimationsOff == true
                ? new(true, CoreText.T("Core.Motion.OnOverride", "Motion is on for Tsukimichi, though Windows' \"Show animations\" is off."), fix)
                : new(true, CoreText.T("Core.Motion.On", "Motion is on."), fix);
    }
}
