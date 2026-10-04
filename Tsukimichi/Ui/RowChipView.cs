using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>A row's trailing chip (1.19.0): its label and the hover's explanation, both cached by their provider.</summary>
public readonly record struct RowChip(string Text, string Tooltip);

/// <summary>
/// Draws a <see cref="RowChip"/> at the trailing end of a row's status (spec-1.19: the 1.14 chip, Secondary text in a
/// <c>--line</c> outline, no fill): painted on the draw list without an item, so the row's selectable keeps every
/// click and the row never grows. The hover names what it means. A 1.5 px outline under high contrast.
/// </summary>
internal static class RowChipView
{
    private const float PadXLogical = 7f;

    /// <summary>The chip's width with <paramref name="text"/> at the current font.</summary>
    public static float Width(string text) => MathF.Ceiling(ImGui.CalcTextSize(text).X + (2f * UiMetrics.Px(PadXLogical)));

    /// <summary>Draws the chip with its top-left at <paramref name="min"/> on the text line; its tooltip while the pointer is on it.</summary>
    public static void Draw(RowChip chip, Vector2 min, bool rowHovered)
    {
        var s = Theme.Surface;
        var line = ImGui.GetTextLineHeight();
        var size = new Vector2(Width(chip.Text), line + UiMetrics.Px(2f));
        var top = new Vector2(MathF.Round(min.X), MathF.Round(min.Y - UiMetrics.Px(1f)));
        var dl = ImGui.GetWindowDrawList();
        var rounding = size.Y * 0.5f;
        var high = Theme.Glyphs.HighContrast;
        dl.AddRect(top, top + size, Theme.U32(high ? s.StrongLine : s.Line), rounding, ImDrawFlags.None, high ? MathF.Max(1.5f, UiMetrics.Hairline) : UiMetrics.Hairline);
        var text = ImGui.CalcTextSize(chip.Text);
        dl.AddText(new Vector2(top.X + ((size.X - text.X) * 0.5f), top.Y + ((size.Y - text.Y) * 0.5f)), Theme.U32(s.TextSecondary), chip.Text);
        if (rowHovered && chip.Tooltip.Length > 0 && ImGui.IsMouseHoveringRect(top, top + size))
        {
            UiMetrics.Tooltip(chip.Text, chip.Tooltip);
        }
    }
}
