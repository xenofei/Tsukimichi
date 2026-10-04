using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The 1.14 filter chips as a fixed row (spec-1.21 P6 and P7): on, a raised fill and a hairline; off, dashed, the label
/// in Tertiary (the one place 1.21 uses Tertiary). Each chip is sized to its label and keeps its size on or off, so
/// toggling one never moves the row.
/// </summary>
internal static class BoardChips
{
    /// <summary>The width a chip takes for <paramref name="label"/>, in the caption role.</summary>
    public static float Width(string label)
    {
        using (Typography.Caption())
        {
            return ImGui.CalcTextSize(label).X + (2f * UiMetrics.Px(10f));
        }
    }

    /// <summary>The chips' height, in the caption role.</summary>
    public static float Height
    {
        get
        {
            using (Typography.Caption())
            {
                return MathF.Round(ImGui.GetTextLineHeight() + UiMetrics.Px(8f));
            }
        }
    }

    /// <summary>One chip at the cursor; returns true when clicked (the caller flips <paramref name="on"/>).</summary>
    public static bool Chip(string id, string label, bool on, string tooltip)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var width = Width(label);
        var height = Height;
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);
        var clicked = ImGui.InvisibleButton(id, max - min);
        var hovered = ImGui.IsItemHovered();
        if (hovered && tooltip.Length > 0)
        {
            UiMetrics.Tooltip(tooltip);
        }

        var rounding = Theme.Flair == Flair.Plain ? UiMetrics.Px(3f) : height * 0.5f;
        if (on)
        {
            dl.AddRectFilled(min, max, Theme.U32(Vector4.Lerp(s.Raised, s.Text, hovered ? 0.09f : 0.05f)), rounding);
            dl.AddRect(min, max, Theme.U32(Theme.Glyphs.HighContrast ? s.StrongLine : s.TextTertiary with { W = s.TextTertiary.W * 0.55f }), rounding, ImDrawFlags.None, Theme.Glyphs.HighContrast ? MathF.Max(1.5f, UiMetrics.Hairline) : UiMetrics.Hairline);
        }
        else
        {
            dl.AddRectFilled(min, max, Theme.U32(hovered ? s.Hover : s.Sunken), rounding);
            FilterPanel.DashedOutline(dl, min, max, rounding, Theme.U32(s.StrongLine with { W = 1f }));
        }

        Chrome.FocusRing(rounding);
        using (Typography.Caption())
        {
            var text = ImGui.CalcTextSize(label);
            dl.AddText(new Vector2(min.X + ((width - text.X) * 0.5f), MathF.Round(min.Y + ((height - text.Y) * 0.5f))), Theme.U32(on ? s.Text : s.TextTertiary), label);
        }

        return clicked;
    }
}
