using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// 1.23.0 (feature plan v9, "Your answers": "a normal button on the main display"): the Moonfall button, a small pill at
/// the right end of the status bar, always on show, that opens or closes the game. <c>/tsuki moonfall</c> does the same.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>Opens or closes Moonfall; null (before the game is wired) draws no button.</summary>
    public Action? OpenMoonfall { get; set; }

    /// <summary>
    /// Draws the Moonfall pill ending at <paramref name="right"/> on the status bar's line and returns where the bar's
    /// other segments must end (the pill's left edge less <paramref name="gap"/>); <paramref name="right"/> unchanged
    /// while the game is not wired.
    /// </summary>
    private float DrawMoonfallButton(ImDrawListPtr dl, float right, float textY, float line, float gap)
    {
        if (OpenMoonfall is not { } open)
        {
            return right;
        }

        var label = Strings.MoonfallButton;
        var pad = UiMetrics.Px(7f);
        var dot = MathF.Max(2f, MathF.Round(line * 0.2f));
        var between = UiMetrics.Px(5f);
        var width = pad + (2f * dot) + between + ImGui.CalcTextSize(label).X + pad;
        var min = new Vector2(MathF.Round(right - width), textY - UiMetrics.Px(1f));
        var max = new Vector2(MathF.Round(right), textY + line + UiMetrics.Px(1f));
        ImGui.SetCursorScreenPos(min);
        if (ImGui.InvisibleButton("##moonfallButton", max - min))
        {
            open();
        }

        var hovered = ImGui.IsItemHovered();
        var rounding = (max.Y - min.Y) * 0.5f;
        var s = Theme.Surface;
        dl.AddRectFilled(min, max, hovered ? Theme.U32(s.Hover) : Theme.WithAlpha(s.Text, 0.05f), rounding);
        dl.AddRect(min, max, Theme.WithAlpha(Theme.Gold, hovered ? 0.55f : 0.28f), rounding, ImDrawFlags.None, UiMetrics.Hairline);

        // A peg as the pill's mark: the game's orange.
        var mark = new Vector2(min.X + pad + dot, textY + (line * 0.5f));
        dl.AddCircleFilled(mark, dot, Theme.U32(Theme.Palette.Pegs.Orange), 12);
        dl.AddText(new Vector2(mark.X + dot + between, textY), Theme.U32(hovered ? s.Text : s.TextSecondary), label);
        Chrome.FocusRing(rounding);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            UiMetrics.Tooltip(Strings.MoonfallButtonTooltip);
        }

        return min.X - gap;
    }
}
