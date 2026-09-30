using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
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

        // Raw push and pop, not ImRaii: this runs in every cell of the long tables, and ImRaii's scopes are allocated.
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        try
        {
            Vector2? size = new Vector2(full, max.Y - pos.Y);
            ImGuiP.RenderTextEllipsis(dl, in pos, in max, max.X, max.X, text, in size);
        }
        finally
        {
            ImGui.PopStyleColor();
        }

        return true;
    }

    /// <summary><paramref name="text"/> in <paramref name="color"/> as one item, without allocating a colour scope.</summary>
    private static void ColoredText(ReadOnlySpan<char> text, uint color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        try
        {
            ImGui.TextUnformatted(text);
        }
        finally
        {
            ImGui.PopStyleColor();
        }
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
        ColoredText(state, ImGui.GetColorU32(stateInk));
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
            ColoredText(reason, ImGui.GetColorU32(reasonInk));
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
        var labelText = ImGui.CalcTextSize(label).X;
        var labelWidth = MathF.Max(MathF.Max(0f, labelMin), labelText);
        var stacked = LayoutBudgets.StackLabelValue(available, labelWidth, gap, ImGui.GetFontSize());
        ColoredText(label, ImGui.GetColorU32(Theme.Surface.TextSecondary));

        if (stacked)
        {
            TextFlow.Wrapped(value, available);
            return;
        }

        // Spacing after the label rather than an offset from the line start: SameLine(offset) adds the group and column
        // offsets that GetCursorPosX already holds, so inside a group or a column the value started too far right.
        ImGui.SameLine(0f, labelWidth - labelText + gap);
        TextFlow.Wrapped(value, MathF.Max(0f, available - labelWidth - gap));
    }

    /// <summary>
    /// Continues the line when an item <paramref name="width"/> wide still fits before the content edge, else starts
    /// a new one: a row of buttons or radio buttons that wraps whole instead of running off the edge. After a text
    /// that wrapped onto several lines (<see cref="TextFlow.LastItemWrapped"/>) the item always goes under it, never
    /// beside its first line.
    /// </summary>
    public static void SameLineOrWrap(float width) => SameLineOrWrap(width, float.NaN);

    /// <summary>
    /// <see cref="SameLineOrWrap(float)"/> measured against <paramref name="right"/> (a screen x, such as a card's
    /// inner edge the pane keeps itself) instead of the content edge.
    /// </summary>
    public static void SameLineOrWrap(float width, float right)
    {
        if (TextFlow.LastItemWrapped())
        {
            return;
        }

        ImGui.SameLine();
        if (ImGui.GetCursorScreenPos().X + width > (float.IsNaN(right) ? ContentRight() : right))
        {
            ImGui.NewLine();
        }
    }

    /// <summary>
    /// Puts the next items, <paramref name="width"/> wide together, at the right end of the line (a card's title
    /// buttons): beside what the line already holds when they fit, never over it, else right-aligned on a line of
    /// their own (feature plan v4 L6).
    /// </summary>
    public static void SameLineRightOrWrap(float width)
    {
        SameLineOrWrap(width);
        var right = ContentRight() - ImGui.GetWindowPos().X + ImGui.GetScrollX();
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), right - width));
    }

    /// <summary>
    /// The room right of the cursor, in pixels: to the open card's inner edge inside a card (<see cref="BeginCard(string, string?, string?, CardKind, Vector4?)"/>),
    /// else to the content region's edge.
    /// </summary>
    public static float RoomX() => MathF.Max(0f, ContentRight() - ImGui.GetCursorScreenPos().X);

    /// <summary>
    /// The screen x the current line may run to: the open card's inner edge, else the content region's (a table
    /// cell's own edge inside a table, which <c>GetWindowContentRegionMax</c> would not know).
    /// </summary>
    private static float ContentRight()
    {
        var region = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        return cardOpen ? MathF.Min(region, cardStart.X + cardWidth - UiMetrics.Px(CardPadX)) : region;
    }

    /// <summary>
    /// The width for a control that would like <paramref name="ideal"/> pixels (a slider, a combo): that, or the room
    /// left on the line when it is less (<see cref="PaneFit.ControlWidth"/>). Pass it to <c>SetNextItemWidth</c>.
    /// </summary>
    public static float FitWidth(float ideal) => PaneFit.ControlWidth(ideal, RoomX());

    /// <summary>
    /// The label of the item just drawn (a slider's, a combo's): beside it when it fits, else on the next line,
    /// wrapped between words (<see cref="TextFlow.Wrapped(string, float)"/>), in the current text colour.
    /// </summary>
    public static void TrailingLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
        if (ImGui.CalcTextSize(label).X > RoomX())
        {
            ImGui.NewLine();
        }

        TextFlow.Wrapped(label, RoomX());
    }

    /// <summary>
    /// A hint line under a setting: <paramref name="text"/> in the disabled tone, wrapped between words in the room
    /// left (<see cref="TextFlow.Wrapped(string, float)"/>) instead of running past the edge.
    /// </summary>
    public static void Hint(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        TextFlow.Wrapped(text, RoomX(), ImGui.GetColorU32(ImGuiCol.TextDisabled));
    }

    /// <summary>
    /// <paramref name="text"/> filling the room left on the line (a table cell, a card row), ending in an ellipsis
    /// when it is longer, with the whole text as the tooltip then. Returns whether it was cut.
    /// </summary>
    public static bool FitText(string text, uint color)
    {
        ArgumentNullException.ThrowIfNull(text);
        var room = RoomX();
        var width = ImGui.CalcTextSize(text).X;
        if (width <= room + 0.5f)
        {
            ColoredText(text, color);
            return false;
        }

        EllipsisText(text, room, color, width);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(text);
        }

        return true;
    }

    /// <inheritdoc cref="FitText(string, uint)"/>
    public static bool FitText(string text, Vector4 color) => FitText(text, ImGui.GetColorU32(color));

    /// <summary>
    /// A selectable <paramref name="width"/> wide whose <paramref name="text"/> ends in an ellipsis when it is longer
    /// (a plain selectable cuts it hard at the edge), in the current text colour. The id is the text's, as a plain
    /// selectable's would be. <paramref name="cut"/> says whether the text was cut, so the caller's tooltip can carry
    /// the whole name. Returns whether it was clicked.
    /// </summary>
    /// <param name="width">The selectable's width; 0 or less fills the room left (<see cref="RoomX"/>).</param>
    /// <param name="height">The selectable's height when taller than a line (a row as tall as its icon), the text centred on it; 0 or less for one line.</param>
    public static bool EllipsisSelectable(string text, bool selected, float width, out bool cut, ImGuiSelectableFlags flags = ImGuiSelectableFlags.None, float height = 0f)
    {
        ArgumentNullException.ThrowIfNull(text);
        var room = width > 0f ? width : RoomX();
        var pos = ImGui.GetCursorScreenPos();
        var line = ImGui.GetTextLineHeight();
        if (height > line)
        {
            pos.Y += MathF.Floor((height - line) * 0.5f);
        }

        bool clicked;
        ImGui.PushID(text);
        try
        {
            clicked = ImGui.Selectable("##fit", selected, flags, new Vector2(MathF.Max(1f, room), height > line ? height : 0f));
        }
        finally
        {
            ImGui.PopID();
        }

        cut = ImGui.IsItemVisible()
            ? EllipsisTextAt(ImGui.GetWindowDrawList(), pos, room, text, ImGui.GetColorU32(ImGuiCol.Text))
            : ImGui.CalcTextSize(text).X > room + 0.5f;
        return clicked;
    }
}
