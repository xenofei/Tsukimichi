using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Tsukimichi.Ui;

public static partial class Chrome
{
    private static readonly string FiltersPillIcon = FontAwesomeIcon.SlidersH.ToIconString();

    /// <summary>
    /// The width of a <see cref="FiltersPill"/>: icon, "Filters" and room for its badge, so nothing beside it moves as
    /// the badge comes and goes.
    /// </summary>
    public static float FiltersPillWidth()
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var icon = ImGui.CalcTextSize(FiltersPillIcon).X;
        ImGui.PopFont();
        return UiMetrics.Px(10f) + icon + UiMetrics.Px(6f) + ImGui.CalcTextSize(Strings.Filters).X + UiMetrics.Px(18f);
    }

    /// <summary>
    /// A pane's Filters button (feature plan v6, U4): a pill with the sliders icon and "Filters" at
    /// <paramref name="min"/>, a neutral wash while its popover is <paramref name="open"/>, and a badge at its top-right
    /// counting the engaged narrowing filters. The filters themselves live in the popover, so setting one never adds a
    /// row to the pane. True on a click.
    /// </summary>
    public static bool FiltersPill(string id, Vector2 min, float width, float height, int count, bool open)
    {
        var size = new Vector2(width, height);
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var max = min + size;
        var rounding = height * 0.5f;
        if (open)
        {
            dl.AddRectFilled(min, max, Theme.WithAlpha(s.Text, 0.12f), rounding);
        }
        else if (hovered)
        {
            dl.AddRectFilled(min, max, Theme.U32(s.Hover), rounding);
        }

        var ink = Theme.U32(open || hovered ? s.Text : s.TextSecondary);
        var x = min.X + UiMetrics.Px(10f);
        ImGui.PushFont(UiBuilder.IconFont);
        var iconSize = ImGui.CalcTextSize(FiltersPillIcon);
        dl.AddText(new Vector2(x, min.Y + (height - iconSize.Y) * 0.5f), ink, FiltersPillIcon);
        ImGui.PopFont();
        x += iconSize.X + UiMetrics.Px(6f);
        var labelSize = ImGui.CalcTextSize(Strings.Filters);
        dl.AddText(new Vector2(x, min.Y + (height - labelSize.Y) * 0.5f), ink, Strings.Filters);
        if (count > 0)
        {
            var badge = UiMetrics.Px(14f);
            Badge(dl, new Vector2(max.X - badge * 0.6f, min.Y + badge * 0.35f), count, actionable: false);
        }

        FocusRing(rounding);
        return clicked;
    }
}
