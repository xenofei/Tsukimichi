namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the keyboard (T17): Settings › Keyboard and the row "…" button. Voice per
/// spec §1: calm, precise, short; no exclamation marks.
/// </summary>
static partial class Strings
{
    // Row "…" button (accessibility A6)
    public const string RowMenuButtonTooltip = "More actions. Right-click the row, or press the Menu key or Shift+F10 on it, for the same menu";

    // Settings › Keyboard
    public const string ConfigSectionKeyboard = "Keyboard";
    public const string ConfigKeyboardAlwaysOn = "Always on while this window has focus: Ctrl+F puts the caret in the search; Esc closes the open menu or the filter panel, then the window; the Menu key or Shift+F10 opens the focused row's menu.";
    public const string ConfigKeyboardGameSeesKeys = "The game sees these keys too: Ctrl+1 to Ctrl+4 are hotbar 2 in the default keybinds, and many players bind single letters. Turn one on only if the game does nothing with it.";
    public const string ConfigShortcutTabs = "Ctrl+1 to Ctrl+4 switch tabs";
    public const string ConfigShortcutTabsHint = "Journal, Moonlit, Characters, Flight, while this window has focus";
    public const string ConfigShortcutFlag = "F flags the selected quest's giver on the map";
    public const string ConfigShortcutFlagHint = "The same as the detail pane's Flag on map";
    public const string ConfigShortcutReveal = "Enter shows the selected quest in the Journal";
    public const string ConfigShortcutRevealHint = "On the Moonlit, Characters and Flight tabs: switches to the Journal with the quest's category open in the tree and its row selected";
    public const string ConfigShortcutPin = "P pins or unpins the selected quest";
    public const string ConfigShortcutPinHint = "The same as the detail pane's Pin";
}
