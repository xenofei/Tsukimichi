using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

public static partial class Chrome
{
    private const string StripSeparator = "·";

    /// <summary>The least room a strip segment needs to be drawn at all, in logical pixels.</summary>
    private const float StripSegmentMinLogical = 32f;

    /// <summary>
    /// One segment of a reserved one-line status strip (feature plan v6, U4): <paramref name="text"/> at
    /// <paramref name="x"/>, after a Veil "·" unless it opens the strip at <paramref name="left"/>, cut to
    /// <paramref name="right"/> with an ellipsis, the whole text and <paramref name="detail"/> on hover. Returns where
    /// the next segment starts; nothing is drawn once the room left is too small to read. The caller reserves the
    /// strip's line, so whatever the segments say, nothing under it moves.
    /// </summary>
    public static float StripSegment(float x, float left, float right, float y, string text, uint color, string? detail = null)
    {
        if (text.Length == 0)
        {
            return x;
        }

        var gap = ImGui.GetStyle().ItemSpacing.X;
        var start = x > left ? x + gap + ImGui.CalcTextSize(StripSeparator).X + gap : x;
        var room = right - start;
        if (room < UiMetrics.Px(StripSegmentMinLogical))
        {
            return x;
        }

        if (x > left)
        {
            ImGui.GetWindowDrawList().AddText(new Vector2(x + gap, y), Theme.U32(Theme.Surface.TextDisabled), StripSeparator);
        }

        var textWidth = ImGui.CalcTextSize(text).X;
        var shown = MathF.Min(textWidth, room);
        ImGui.SetCursorScreenPos(new Vector2(start, y));
        var cut = EllipsisText(text, shown, color, textWidth);
        if ((cut || detail is not null) && ImGui.IsItemHovered())
        {
            if (cut)
            {
                UiMetrics.Tooltip(text, detail);
            }
            else
            {
                UiMetrics.Tooltip(detail!);
            }
        }

        return start + shown;
    }
}
