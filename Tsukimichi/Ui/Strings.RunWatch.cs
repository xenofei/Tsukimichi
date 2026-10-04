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

    /// <summary>{0} = the duty with other players; {1} = the quest.</summary>
    public static string DutyGuardStopMaybeFormat => Loc.Get("DutyGuardStopMaybeFormat");

    /// <summary>{0} = the duty.</summary>
    public static string DutyGuardWarnFormat => Loc.Get("DutyGuardWarnFormat");

    /// <summary>{0} = the duty with other players; {1} = the quest.</summary>
    public static string DutyGuardWarnMaybeFormat => Loc.Get("DutyGuardWarnMaybeFormat");

    /// <summary>{0} = the quest.</summary>
    public static string DutyGuardUnsureFormat => Loc.Get("DutyGuardUnsureFormat");

    /// <summary>{0} = the duty.</summary>
    public static string DutyGuardNoStopFormat => Loc.Get("DutyGuardNoStopFormat");

    public static string DutyGuardStillQueued => Loc.Get("DutyGuardStillQueued");

    public static string DutyGuardHiddenDuty => Loc.Get("DutyGuardHiddenDuty");

    public static string DutyGuardToastStop => Loc.Get("DutyGuardToastStop");

    public static string DutyGuardToastWarn => Loc.Get("DutyGuardToastWarn");

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

    public static string ConfigNeedsYouSound => Loc.Get("ConfigNeedsYouSound");

    public static string ConfigNeedsYouSoundHint => Loc.Get("ConfigNeedsYouSoundHint");

    public static string ConfigNeedsYouToast => Loc.Get("ConfigNeedsYouToast");

    public static string ConfigNeedsYouToastHint => Loc.Get("ConfigNeedsYouToastHint");

    public static string NeedsYouDeathLine => Loc.Get("NeedsYouDeathLine");

    /// <summary>{0} = seconds.</summary>
    public static string NeedsYouStuckFormat => Loc.Get("NeedsYouStuckFormat");

    /// <summary>{0} = the duty.</summary>
    public static string NeedsYouDutyPopFormat => Loc.Get("NeedsYouDutyPopFormat");

    public static string NeedsYouDutyPopUnnamed => Loc.Get("NeedsYouDutyPopUnnamed");

    /// <summary>{0} = the sender.</summary>
    public static string NeedsYouTellFormat => Loc.Get("NeedsYouTellFormat");

    public static string NeedsYouTellUnnamed => Loc.Get("NeedsYouTellUnnamed");

    public static string NeedsYouToastDeath => Loc.Get("NeedsYouToastDeath");

    public static string NeedsYouToastStuck => Loc.Get("NeedsYouToastStuck");

    public static string NeedsYouToastDutyPop => Loc.Get("NeedsYouToastDutyPop");

    public static string NeedsYouToastTell => Loc.Get("NeedsYouToastTell");
}
