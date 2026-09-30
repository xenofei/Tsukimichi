using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the keyboard (T17): Settings › Keyboard and the row "…" button. Voice per
/// spec §1: calm, precise, short; no exclamation marks.
/// </summary>
static partial class Strings
{
    // Row "…" button (accessibility A6)
    public static string RowMenuButtonTooltip => Loc.Get("RowMenuButtonTooltip");

    // Settings › Keyboard
    public static string ConfigSectionKeyboard => Loc.Get("ConfigSectionKeyboard");
    public static string ConfigKeyboardAlwaysOn => Loc.Get("ConfigKeyboardAlwaysOn");
    public static string ConfigKeyboardGameSeesKeys => Loc.Get("ConfigKeyboardGameSeesKeys");
    public static string ConfigShortcutTabs => Loc.Get("ConfigShortcutTabs");
    public static string ConfigShortcutTabsHint => Loc.Get("ConfigShortcutTabsHint");
    public static string ConfigShortcutFlag => Loc.Get("ConfigShortcutFlag");
    public static string ConfigShortcutFlagHint => Loc.Get("ConfigShortcutFlagHint");
    public static string ConfigShortcutReveal => Loc.Get("ConfigShortcutReveal");
    public static string ConfigShortcutRevealHint => Loc.Get("ConfigShortcutRevealHint");
    public static string ConfigShortcutPin => Loc.Get("ConfigShortcutPin");
    public static string ConfigShortcutPinHint => Loc.Get("ConfigShortcutPinHint");
}
