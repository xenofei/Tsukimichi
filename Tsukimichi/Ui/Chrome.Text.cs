using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The text helpers of the responsive system (feature plan v4 L2, UI audit §4): text that ends in an ellipsis instead
/// of running under its neighbour, a status line whose state word is never cut, a label beside or above its value, and
/// a row of items that wraps whole. Words wrap through <see cref="TextFlow"/>. None of them allocates per frame: the
/// texts are the caller's and ImGui draws the ellipsis itself.
/// </summary>
public static partial class Chrome
{
    /// <summary>
    /// <paramref name="text"/> in <paramref name="color"/> within <paramref name="width"/>, ending in an ellipsis when
    /// it is longer, as one item (hover and click tests work on it). Returns whether the text was cut, so the caller
    /// can put the whole text in a tooltip.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="width">The room, in pixels.</param>
    /// <param name="color">The text colour.</param>
    /// <param name="textWidth">The text's width when the caller already measured it; negative to measure here.</param>
    public static bool EllipsisText(ReadOnlySpan<char> text, float width, uint color, float textWidth = -1f)
    {
        var min = ImGui.GetCursorScreenPos();
        var room = MathF.Max(0f, width);
        ImGui.Dummy(new Vector2(room, ImGui.GetTextLineHeight()));
        return EllipsisTextAt(ImGui.GetWindowDrawList(), min, room, text, color, textWidth);
    }

    /// <inheritdoc cref="EllipsisText(ReadOnlySpan{char}, float, uint, float)"/>
    public static bool EllipsisText(string text, float width, uint color, float textWidth = -1f) =>
        EllipsisText(text.AsSpan(), width, color, textWidth);

    /// <summary>
    /// <paramref name="text"/> drawn on <paramref name="dl"/> at <paramref name="pos"/> within <paramref name="width"/>,
    /// ending in an ellipsis when it is longer; no item. Returns whether the text was cut.
    /// </summary>
    public static bool EllipsisTextAt(ImDrawListPtr dl, Vector2 pos, float width, ReadOnlySpan<char> text, uint color, float textWidth = -1f)
    {
        if (text.IsEmpty || !(width > 0f))
        {
            return !text.IsEmpty;
        }

        var full = textWidth >= 0f ? textWidth : ImGui.CalcTextSize(text).X;
        var max = new Vector2(pos.X + width, pos.Y + ImGui.GetTextLineHeight());
        if (full <= width + 0.5f)
        {
            dl.AddText(pos, color, text);
            return false;
        }

        using var ink = ImRaii.PushColor(ImGuiCol.Text, color);
        Vector2? size = new Vector2(full, max.Y - pos.Y);
        ImGuiP.RenderTextEllipsis(dl, in pos, in max, max.X, max.X, text, in size);
        return true;
    }

    /// <inheritdoc cref="EllipsisTextAt(ImDrawListPtr, Vector2, float, ReadOnlySpan{char}, uint, float)"/>
    public static bool EllipsisTextAt(ImDrawListPtr dl, Vector2 pos, float width, string text, uint color, float textWidth = -1f) =>
        EllipsisTextAt(dl, pos, width, text.AsSpan(), color, textWidth);

    /// <summary>
    /// A status line (P1, game UX panel finding 3): the state word (<see cref="TableGeometry.StateWordLength"/>) in
    /// <paramref name="stateInk"/>, never cut, then the reason after the separator in <paramref name="reasonInk"/>,
    /// ellipsised in the room the state word leaves (<see cref="TableGeometry.ReasonWidth"/>). With
    /// <paramref name="tooltip"/> the whole line is the tooltip of a cut line; a caller whose row owns the hover (the
    /// quest table) passes false and shows it itself. Returns whether the reason was cut.
    /// </summary>
    public static bool StatusText(string text, float width, Vector4 stateInk, Vector4 reasonInk, bool tooltip = true)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = ImGui.GetCursorScreenPos();
        var split = TableGeometry.StateWordLength(text);
        var state = text.AsSpan(0, split);
        using (ImRaii.PushColor(ImGuiCol.Text, stateInk))
        {
            ImGui.TextUnformatted(state);
        }

        if (split >= text.Length)
        {
            return false;
        }

        var reason = text.AsSpan(split);
        var room = TableGeometry.ReasonWidth(width, ImGui.CalcTextSize(state).X);
        var reasonWidth = ImGui.CalcTextSize(reason).X;
        ImGui.SameLine(0f, 0f);
        if (!TableGeometry.ReasonNeedsEllipsis(reasonWidth, room))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, reasonInk))
            {
                ImGui.TextUnformatted(reason);
            }

            return false;
        }

        EllipsisText(reason, room, ImGui.GetColorU32(reasonInk), reasonWidth);
        if (tooltip && ImGui.IsMouseHoveringRect(start, new Vector2(start.X + width, ImGui.GetItemRectMax().Y)))
        {
            UiMetrics.Tooltip(text);
        }

        return true;
    }

    /// <summary>
    /// A label and its value (UI audit §4 "stack label over value"): side by side while the value keeps at least
    /// <see cref="LayoutBudgets.LabelValueMinEm"/> ems of room beside a label column <paramref name="labelMin"/> wide
    /// (wider when the label is), else the label on its own line with the value under it. The label is in the
    /// secondary tone; the value wraps between words (<see cref="TextFlow.Wrapped"/>).
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="value">The value.</param>
    /// <param name="labelMin">The label column's least width, in pixels, so the values of several rows line up.</param>
    public static void LabelValue(string label, string value, float labelMin)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(value);
        var available = ImGui.GetContentRegionAvail().X;
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var labelWidth = MathF.Max(MathF.Max(0f, labelMin), ImGui.CalcTextSize(label).X);
        var stacked = LayoutBudgets.StackLabelValue(available, labelWidth, gap, ImGui.GetFontSize());
        var startX = ImGui.GetCursorPosX();
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(label);
        }

        if (stacked)
        {
            TextFlow.Wrapped(value, available);
            return;
        }

        ImGui.SameLine(startX + labelWidth + gap);
        TextFlow.Wrapped(value, MathF.Max(0f, available - labelWidth - gap));
    }

    /// <summary>
    /// Continues the line when an item <paramref name="width"/> wide still fits before the content edge, else starts
    /// a new one: a row of buttons or radio buttons that wraps whole instead of running off the edge.
    /// </summary>
    public static void SameLineOrWrap(float width)
    {
        ImGui.SameLine();
        if (ImGui.GetCursorScreenPos().X + width > ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X)
        {
            ImGui.NewLine();
        }
    }
}
