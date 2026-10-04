using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for runs you can trust (plan v7, 1.18.0): the Questionable duty guard (A3) and the "Needs you" alerts
/// while a hand-off runs (A5), their chat lines, toasts and settings.
/// </summary>
public static partial class Strings
{
    public static string ConfigQuestionableDutyGuard => Loc.Get("ConfigQuestionableDutyGuard");

    public static string ConfigQuestionableDutyGuardHint => Loc.Get("ConfigQuestionableDutyGuardHint");

    public static string DutyGuardOptionStop => Loc.Get("DutyGuardOptionStop");

    public static string DutyGuardOptionWarn => Loc.Get("DutyGuardOptionWarn");

    public static string DutyGuardOptionNothing => Loc.Get("DutyGuardOptionNothing");

    /// <summary>{0} = the duty.</summary>
    public static string DutyGuardStopFormat => Loc.Get("DutyGuardStopFormat");

    /// <summary>{0} = the duty.</summary>
    public static string DutyGuardWarnFormat => Loc.Get("DutyGuardWarnFormat");

    /// <summary>A step that may be a duty with or without Duty Support or Trust; names no duty. {0} = the quest.</summary>
    public static string DutyGuardWarnNextFormat => Loc.Get("DutyGuardWarnNextFormat");

    /// <summary>The guard cannot read Questionable's steps: one chat line per session.</summary>
    public static string DutyGuardBlindChat => Loc.Get("DutyGuardBlindChat");

    /// <summary>Settings' duty guard status label.</summary>
    public static string SettingsDutyGuardStatus => Loc.Get("SettingsDutyGuardStatus");

    /// <summary>Settings' duty guard status while it cannot read Questionable's steps.</summary>
    public static string DutyGuardBlindStatus => Loc.Get("DutyGuardBlindStatus");

    /// <summary>The <c>/tsuki stop</c> part for a cancelled automatic restart.</summary>
    public static string StopPendingCancelled => Loc.Get("StopPendingCancelled");

    /// <summary>{0} = the quest.</summary>
    public static string DutyGuardUnsureFormat => Loc.Get("DutyGuardUnsureFormat");

    /// <summary>{0} = the duty.</summary>
    public static string DutyGuardNoStopFormat => Loc.Get("DutyGuardNoStopFormat");

    public static string DutyGuardStillQueued => Loc.Get("DutyGuardStillQueued");

    public static string DutyGuardHiddenDuty => Loc.Get("DutyGuardHiddenDuty");

    /// <summary>A run receipt's reason (A4) when the duty guard stopped the run.</summary>
    public static string QuestionableRunWhyDutyGuard => Loc.Get("QuestionableRunWhyDutyGuard");

    public static string ConfigSectionNeedsYou => Loc.Get("ConfigSectionNeedsYou");

    public static string ConfigNeedsYouScope => Loc.Get("ConfigNeedsYouScope");

    public static string ConfigNeedsYouScopeHint => Loc.Get("ConfigNeedsYouScopeHint");

    public static string ConfigNeedsYouDeath => Loc.Get("ConfigNeedsYouDeath");

    public static string ConfigNeedsYouDeathHint => Loc.Get("ConfigNeedsYouDeathHint");

    public static string ConfigNeedsYouStuck => Loc.Get("ConfigNeedsYouStuck");

    public static string ConfigNeedsYouStuckHint => Loc.Get("ConfigNeedsYouStuckHint");

    public static string ConfigNeedsYouDutyPop => Loc.Get("ConfigNeedsYouDutyPop");

    public static string ConfigNeedsYouDutyPopHint => Loc.Get("ConfigNeedsYouDutyPopHint");

    public static string ConfigNeedsYouTell => Loc.Get("ConfigNeedsYouTell");

    public static string ConfigNeedsYouTellHint => Loc.Get("ConfigNeedsYouTellHint");
}
