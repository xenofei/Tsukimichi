using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Wrapped text that breaks between words, never inside one (feature plan v4 L2, UI audit §4): ImGui's own
/// <c>TextWrapped</c> breaks a long word, or a Japanese line, wherever the width runs out, which in a narrow pane left
/// one or two letters per line. The break points come from <see cref="WordWrap"/> (Core, tested); a single word wider
/// than the pane ends in an ellipsis with the whole text as the tooltip. The lines are cached per text, width (in steps
/// of <see cref="WidthStep"/> pixels), font and font size, so a text drawn every frame is measured once, and nothing is
/// allocated on the frames in between.
/// </summary>
public static class TextFlow
{
    /// <summary>More cached texts than this and the cache starts over (a pane shows far fewer at once).</summary>
    private const int MaxEntries = 512;

    /// <summary>
    /// Widths are cached in steps of this many pixels (rounded down, so a line never runs past the room it is given):
    /// dragging a pane edge then breaks a text again every few pixels rather than on every frame of the drag.
    /// </summary>
    private const int WidthStep = 4;

    private static readonly Dictionary<(string Text, int Width, int Size, ImFontPtr Font), WrapLine[]> Cache = [];
    private static readonly List<WrapLine> Scratch = [];
    private static readonly MeasureText Measure = static text => ImGui.CalcTextSize(text).X;

    /// <summary>
    /// <paramref name="text"/> wrapped between words in <paramref name="width"/> pixels (the content region's width when
    /// 0 or less), in the current text colour, as one item. Returns whether a word was too long for a line and was
    /// ellipsised; the whole text is then the item's tooltip.
    /// </summary>
    public static bool Wrapped(string text, float width = 0f)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Wrapped(text, width, ImGui.GetColorU32(ImGuiCol.Text));
    }

    /// <inheritdoc cref="Wrapped(string, float)"/>
    /// <param name="text">The text.</param>
    /// <param name="width">The wrap width in pixels.</param>
    /// <param name="color">The text colour.</param>
    public static bool Wrapped(string text, float width, uint color)
    {
        ArgumentNullException.ThrowIfNull(text);
        // A string that is or holds a spoiler placeholder is Secondary as a whole (spec-1.20 N6).
        color = ShieldText.Ink(text, color);
        var room = width > 0f ? width : ImGui.GetContentRegionAvail().X;
        var lines = Lines(text, room);
        var lineHeight = ImGui.GetTextLineHeight();
        var origin = ImGui.GetCursorScreenPos();
        var widest = 0f;
        foreach (var line in lines)
        {
            widest = MathF.Max(widest, MathF.Min(line.Width, room));
        }

        ImGui.Dummy(new Vector2(widest, MathF.Max(1, lines.Length) * lineHeight));
        lastMin = ImGui.GetItemRectMin();
        lastMultiLine = lines.Length > 1;
        if (!ImGui.IsItemVisible())
        {
            return false;
        }

        var dl = ImGui.GetWindowDrawList();
        var cut = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var pos = new Vector2(origin.X, origin.Y + (i * lineHeight));
            var span = text.AsSpan(line.Start, line.Length);
            if (line.Cut)
            {
                cut |= Chrome.EllipsisTextAt(dl, pos, room, span, color, line.Width);
            }
            else if (line.Length > 0)
            {
                dl.AddText(pos, color, span);
            }
        }

        if (cut && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(text);
        }

        return cut;
    }

    // The last wrapped text's item: where it starts and whether it took more than one line.
    private static Vector2 lastMin = new(float.NaN);
    private static bool lastMultiLine;

    /// <summary>
    /// The item just drawn is a <see cref="Wrapped(string, float, uint)"/> text that took more than one line: an item
    /// put on its line would sit beside its first line, over the lines under it (<see cref="Chrome.SameLineOrWrap(float)"/>).
    /// </summary>
    public static bool LastItemWrapped() => lastMultiLine && ImGui.GetItemRectMin() == lastMin;

    /// <summary>
    /// <paramref name="text"/> wrapped between words in <paramref name="width"/> pixels and drawn at
    /// <paramref name="pos"/> on <paramref name="dl"/> (no item), at most <paramref name="maxLines"/> lines, each centred
    /// when <paramref name="center"/> is set (an ellipsised line too); what does not fit ends the last line in an
    /// ellipsis (a gallery tile's name). With <paramref name="strike"/> each drawn line is struck through across its own
    /// width (a row hidden by the user's verdict). Returns whether anything was cut, so the caller can show the whole
    /// text on hover.
    /// </summary>
    public static bool DrawClamped(ImDrawListPtr dl, Vector2 pos, string text, float width, int maxLines, uint color, bool center = false, bool strike = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0 || maxLines <= 0 || !(width > 0f))
        {
            return false;
        }

        var lines = Lines(text, width);
        var lineHeight = ImGui.GetTextLineHeight();
        var shown = Math.Min(lines.Length, maxLines);
        var cut = false;
        for (var i = 0; i < shown; i++)
        {
            var line = lines[i];
            var at = new Vector2(pos.X, pos.Y + (i * lineHeight));

            // The rest of the text on the last line when more follows (ellipsised), else the line itself.
            var last = i == shown - 1 && lines.Length > shown;
            var span = last ? text.AsSpan(line.Start).TrimEnd() : text.AsSpan(line.Start, line.Length);
            if (span.IsEmpty)
            {
                continue;
            }

            var full = last ? ImGui.CalcTextSize(span).X : line.Width;
            var drawn = MathF.Min(full, width);
            if (center)
            {
                at.X += MathF.Floor(MathF.Max(0f, width - drawn) * 0.5f);
            }

            if (last || line.Cut)
            {
                cut |= Chrome.EllipsisTextAt(dl, at, width, span, color, full);
            }
            else
            {
                dl.AddText(at, color, span);
            }

            if (strike)
            {
                var y = MathF.Round(at.Y + (lineHeight * 0.5f));
                dl.AddLine(new Vector2(at.X, y), new Vector2(at.X + drawn, y), color, UiMetrics.Hairline);
            }
        }

        return cut || lines.Length > shown;
    }

    /// <summary>The height <paramref name="text"/> takes wrapped in <paramref name="width"/> pixels, in the current font.</summary>
    public static float Height(string text, float width)
    {
        ArgumentNullException.ThrowIfNull(text);
        return MathF.Max(1, Lines(text, width).Length) * ImGui.GetTextLineHeight();
    }

    /// <summary>The cached lines of <paramref name="text"/> in <paramref name="width"/> pixels, broken on the first call.</summary>
    private static WrapLine[] Lines(string text, float width)
    {
        // The font itself is part of the key, not only its size: two fonts of one size (body and heading) measure apart.
        var step = (int)MathF.Floor(MathF.Max(0f, width) / WidthStep) * WidthStep;
        var key = (text, step, (int)MathF.Round(ImGui.GetFontSize() * 100f), ImGui.GetFont());
        if (Cache.TryGetValue(key, out var lines))
        {
            return lines;
        }

        if (Cache.Count >= MaxEntries)
        {
            Cache.Clear();
        }

        WordWrap.Break(text, key.Item2, Measure, Scratch);
        lines = Scratch.ToArray();
        Cache[key] = lines;
        return lines;
    }
}
