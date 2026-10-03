using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>Strings for Back and forward through the quests looked at (feature plan v7 N1): the toolbar buttons and Settings › Keyboard.</summary>
static partial class Strings
{
    // The toolbar's Back and Forward buttons
    public static string HistoryBackFormat => Loc.Get("HistoryBackFormat");
    public static string HistoryForwardFormat => Loc.Get("HistoryForwardFormat");
    public static string HistoryBackNone => Loc.Get("HistoryBackNone");
    public static string HistoryForwardNone => Loc.Get("HistoryForwardNone");
    public static string HistoryBackKeys => Loc.Get("HistoryBackKeys");
    public static string HistoryForwardKeys => Loc.Get("HistoryForwardKeys");

    // Settings › Advanced › Keyboard
    public static string ConfigShortcutHistory => Loc.Get("ConfigShortcutHistory");
    public static string ConfigShortcutHistoryHint => Loc.Get("ConfigShortcutHistoryHint");
}
