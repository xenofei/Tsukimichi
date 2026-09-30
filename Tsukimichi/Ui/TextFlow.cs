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
/// than the pane ends in an ellipsis with the whole text as the tooltip. The lines are cached per text, width and font
/// size, so a text drawn every frame is measured once, and nothing is allocated on the frames in between.
/// </summary>
public static class TextFlow
{
    /// <summary>More cached texts than this and the cache starts over (a pane shows far fewer at once).</summary>
    private const int MaxEntries = 512;

    private static readonly Dictionary<(string Text, int Width, int Font), WrapLine[]> Cache = [];
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

    /// <summary>The height <paramref name="text"/> takes wrapped in <paramref name="width"/> pixels, in the current font.</summary>
    public static float Height(string text, float width)
    {
        ArgumentNullException.ThrowIfNull(text);
        return MathF.Max(1, Lines(text, width).Length) * ImGui.GetTextLineHeight();
    }

    /// <summary>The cached lines of <paramref name="text"/> in <paramref name="width"/> pixels, broken on the first call.</summary>
    private static WrapLine[] Lines(string text, float width)
    {
        var key = (text, (int)MathF.Floor(MathF.Max(0f, width)), (int)MathF.Round(ImGui.GetFontSize() * 100f));
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
