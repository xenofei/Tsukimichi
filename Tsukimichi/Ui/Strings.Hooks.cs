using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the addon kill switch (T20): the notice shown in chat and in Settings › Integrations while the game
/// hooks are paused on an untested game version, and the "enable anyway" setting. Constant names carry the
/// <c>Hooks</c> prefix so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    /// <summary>The one chat line (once per load) and the Settings notice while the hooks are paused.</summary>
    public static string HooksPausedNotice => Loc.Get("HooksPausedNotice");

    public static string HooksEnableUntested => Loc.Get("HooksEnableUntested");

    public static string HooksEnableUntestedHint => Loc.Get("HooksEnableUntestedHint");

    /// <summary>Shown under the setting while it keeps the hooks running on an untested version.</summary>
    public static string HooksRunningUntested => Loc.Get("HooksRunningUntested");
}
