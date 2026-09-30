namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the addon kill switch (T20): the notice shown in chat and in Settings › Integrations while the game
/// hooks are paused on an untested game version, and the "enable anyway" setting. Constant names carry the
/// <c>Hooks</c> prefix so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    /// <summary>The one chat line (once per load) and the Settings notice while the hooks are paused.</summary>
    public const string HooksPausedNotice = "Tsukimichi's game hooks (item tooltip, item and NPC menus, server info bar, Duty Finder hint) are paused on this game version until an update is tested. The quest journal works as usual.";

    public const string HooksEnableUntested = "Enable game hooks on this untested version";

    public const string HooksEnableUntestedHint = "After a game patch the item tooltip panel, the item and NPC menu entries, the server info bar entry and the Duty Finder unlock hint pause until a Tsukimichi update has been tested on the new version. Tick this to run them anyway on the game version you are on now; the next patch pauses them again. If one draws in the wrong place or misbehaves, untick it. The quest journal is not affected either way.";

    /// <summary>Shown under the setting while it keeps the hooks running on an untested version.</summary>
    public const string HooksRunningUntested = "Game hooks are running on an untested game version.";
}
