using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>How a row's trailing signal is drawn (spec-1.19).</summary>
public enum RowChipLook : byte
{
    /// <summary>The 1.14 chip: Secondary text in a <c>--line</c> outline, no fill ("Replaying", "Ends in 2 days").</summary>
    Chip,

    /// <summary>A muted word, no chip: Tertiary at caption size ("seen in game", C1).</summary>
    Word,

    /// <summary>A word that asks a look: Secondary at caption size over a 1 px dotted underline in <c>--vline</c> ("game disagrees", C1).</summary>
    Dotted,
}

/// <summary>A row's trailing chip (1.19.0): its label, the hover's explanation (both cached by their provider) and its look.</summary>
public readonly record struct RowChip(string Text, string Tooltip, RowChipLook Look = RowChipLook.Chip);

/// <summary>
/// Draws a <see cref="RowChip"/> at the trailing end of a row's status (spec-1.19: the 1.14 chip, Secondary text in a
/// <c>--line</c> outline, no fill; or the C1 words, "seen in game" in Tertiary and "game disagrees" in Secondary with a
/// dotted underline, both 11 px): painted on the draw list without an item, so the row's selectable keeps every click
/// and the row never grows. The hover names what it means. A 1.5 px outline under high contrast.
/// </summary>
internal static class RowChipView
{
    private const float PadXLogical = 7f;

    /// <summary>The chip's width with its text at the current font (the caption face for the C1 words).</summary>
    public static float Width(RowChip chip)
    {
        if (chip.Look == RowChipLook.Chip)
        {
            return MathF.Ceiling(ImGui.CalcTextSize(chip.Text).X + (2f * UiMetrics.Px(PadXLogical)));
        }

        using (Typography.Caption())
        {
            return MathF.Ceiling(ImGui.CalcTextSize(chip.Text).X);
        }
    }

    /// <summary>Draws the chip with its top-left at <paramref name="min"/> on the text line; its tooltip while the pointer is on it.</summary>
    public static void Draw(RowChip chip, Vector2 min, bool rowHovered)
    {
        if (chip.Look != RowChipLook.Chip)
        {
            DrawWord(chip, min, rowHovered);
            return;
        }

        var s = Theme.Surface;
        var line = ImGui.GetTextLineHeight();
        var size = new Vector2(Width(chip), line + UiMetrics.Px(2f));
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

    /// <summary>The C1 words: caption size, centred on the row's text line; the dotted one underlined in dots of 1 px.</summary>
    private static void DrawWord(RowChip chip, Vector2 min, bool rowHovered)
    {
        var s = Theme.Surface;
        var line = ImGui.GetTextLineHeight();
        var dl = ImGui.GetWindowDrawList();
        Vector2 size;
        Vector2 top;
        using (Typography.Caption())
        {
            size = ImGui.CalcTextSize(chip.Text);
            top = new Vector2(MathF.Round(min.X), MathF.Round(min.Y + ((line - size.Y) * 0.5f)));
            dl.AddText(top, Theme.U32(chip.Look == RowChipLook.Dotted ? s.TextSecondary : s.TextTertiary), chip.Text);
        }

        if (chip.Look == RowChipLook.Dotted)
        {
            var y = MathF.Round(top.Y + size.Y + UiMetrics.Px(1f)) + 0.5f;
            var dot = MathF.Max(1f, UiMetrics.Hairline);
            var ink = Theme.U32(s.StrongLine);
            for (var x = top.X; x < top.X + size.X; x += 2f * dot)
            {
                dl.AddRectFilled(new Vector2(x, y - (dot * 0.5f)), new Vector2(MathF.Min(x + dot, top.X + size.X), y + (dot * 0.5f)), ink);
            }
        }

        if (rowHovered && chip.Tooltip.Length > 0 && ImGui.IsMouseHoveringRect(top, top + size))
        {
            UiMetrics.Tooltip(chip.Tooltip);
        }
    }
}
