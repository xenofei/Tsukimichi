using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// Keys of the region rectangles the panes record in <see cref="UiState.Rects"/> every frame for the interactive
/// tutorial, plus the recording helpers. Keys are constants so recording allocates nothing.
/// </summary>
public static class UiRects
{
    public const string Toolbar = "toolbar";
    public const string Search = "search";
    public const string FiltersButton = "filtersButton";
    public const string Chips = "chips";
    public const string Character = "character";
    public const string Sync = "sync";
    public const string HelpButton = "helpButton";
    public const string TutorialButton = "tutorialButton";
    public const string SettingsButton = "settingsButton";
    public const string Tabs = "tabs";
    public const string Tree = "tree";
    public const string Table = "table";
    public const string Detail = "detail";
    public const string DetailRequirements = "detail.requirements";
    public const string DetailPath = "detail.path";
    public const string DetailGiver = "detail.giver";
    public const string StatusBar = "statusBar";
    public const string FilterPanel = "filterPanel";
    public const string MoonlitKinds = "moonlit.kinds";
    public const string MoonlitTable = "moonlit.table";
    public const string CharactersList = "characters.list";
    public const string CharactersDashboard = "characters.dashboard";

    /// <summary>Records the rectangle of the item drawn last.</summary>
    public static void RecordItem(this UiState ui, string key) =>
        ui.RecordRect(key, ImGui.GetItemRectMin(), ImGui.GetItemRectMax());

    /// <summary>Records the current window's rectangle; call right after a child window or scrolling table began.</summary>
    public static void RecordWindow(this UiState ui, string key)
    {
        var pos = ImGui.GetWindowPos();
        ui.RecordRect(key, pos, pos + ImGui.GetWindowSize());
    }

    /// <summary>
    /// Records the vertical span from <paramref name="start"/> (a cursor position taken before drawing) down to the
    /// current cursor, <paramref name="width"/> wide, clipped to the current window so a scrolled-away region never
    /// records a rectangle outside its pane (it degenerates to an empty one instead).
    /// </summary>
    public static void RecordSpan(this UiState ui, string key, Vector2 start, float width)
    {
        var windowMin = ImGui.GetWindowPos();
        var windowMax = windowMin + ImGui.GetWindowSize();
        var min = Vector2.Max(start, windowMin);
        var max = Vector2.Min(new Vector2(start.X + width, ImGui.GetCursorScreenPos().Y), windowMax);
        ui.RecordRect(key, min, Vector2.Max(min, max));
    }
}
