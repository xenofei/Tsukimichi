namespace Tsukimichi.Core.Ui;

/// <summary>
/// The width of <paramref name="text"/> at a font size of one (in ems), so it scales to any size by multiplication: the
/// UI measures at its own size and divides, the tests read a font table.
/// </summary>
public delegate float EmMeasure(ReadOnlySpan<char> text);

/// <summary>
/// How a rail label is drawn (<see cref="RailLabel.Fit"/>): its size, the tracking between its glyphs, and whether it
/// wraps or gives way to the icon alone. Sizes and widths are in the caller's units (logical px).
/// </summary>
/// <param name="Size">The font size to draw at.</param>
/// <param name="Tracking">Added to every glyph's advance but the last (negative: tighter).</param>
/// <param name="Lines">1, or 2 when the label wraps at <paramref name="Break"/>; 0 when <paramref name="IconOnly"/>.</param>
/// <param name="Break">The index of the space the label wraps at (the second line starts after it); -1 on one line.</param>
/// <param name="Width">The widest line's width as drawn.</param>
/// <param name="IconOnly">Nothing fits: the station shows its icon alone, and the tooltip names it.</param>
public readonly record struct RailLabelFit(float Size, float Tracking, int Lines, int Break, float Width, bool IconOnly)
{
    /// <summary>The label drawn whole, at its size, on one line.</summary>
    public bool AsIs => !IconOnly && Lines == 1 && Tracking == 0f;
}

/// <summary>
/// The rail labels' fit (plan v7 UI-4, spec Revision 3 R3.1): a station's label never leaves its plate and never ends
/// in an ellipsis. Each label tries, in order:
/// <list type="number">
/// <item>its own size, if it fits the room;</item>
/// <item>tracked at <see cref="TrackingEm"/> (−0.02 em);</item>
/// <item>tracked and shrunk to fit, never under the least size;</item>
/// <item>wrapped at a space onto two lines (where the station has the height), each line through the same three steps;</item>
/// <item>the icon alone, its label the first line of the tooltip.</item>
/// </list>
/// Pure, so it is tested without ImGui; the rail caches the result per language, Text size and UI scale.
/// </summary>
public static class RailLabel
{
    /// <summary>The tracking of a label that does not fit at its size, in ems.</summary>
    public const float TrackingEm = -0.02f;

    /// <summary>The line height of a wrapped label, as a factor of its size.</summary>
    public const float LineHeight = 1.05f;

    /// <summary>
    /// The fit of <paramref name="label"/> in <paramref name="room"/> at <paramref name="size"/>, never under
    /// <paramref name="minSize"/>, on at most <paramref name="maxLines"/> lines (1 or 2).
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="size">Its size (the rail label size, <see cref="LayoutBudgets.RailLabelLogical"/>).</param>
    /// <param name="minSize">The least it may shrink to (<see cref="LayoutBudgets.RailLabelMinLogical"/>).</param>
    /// <param name="room">The width it has (<see cref="LayoutBudgets.RailLabelRoom"/>).</param>
    /// <param name="maxLines">How many lines the station's height takes: 1, or 2 where it is tall enough.</param>
    /// <param name="measure">The text's width at a size of one.</param>
    public static RailLabelFit Fit(string label, float size, float minSize, float room, int maxLines, EmMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(measure);
        var text = label.AsSpan().Trim();
        if (text.IsEmpty || !(size > 0f) || !(room > 0f))
        {
            return IconOnlyFit(size);
        }

        var least = MathF.Min(size, MathF.Max(0f, minSize));
        if (Line(text, size, least, room, measure) is { } one)
        {
            return new RailLabelFit(one.Size, one.Tracking, 1, -1, one.Width, false);
        }

        if (maxLines >= 2 && Wrap(text, size, least, room, measure) is { } two)
        {
            var offset = label.AsSpan().Length - label.AsSpan().TrimStart().Length;
            return two with { Break = two.Break + offset };
        }

        return IconOnlyFit(size);
    }

    /// <summary>The width of <paramref name="text"/> at <paramref name="size"/> with <paramref name="trackingEm"/> between its glyphs.</summary>
    public static float Width(ReadOnlySpan<char> text, float size, float trackingEm, EmMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(measure);
        if (text.IsEmpty)
        {
            return 0f;
        }

        return (measure(text) * size) + (trackingEm * size * (text.Length - 1));
    }

    /// <summary>One line through the first three steps: as is, tracked, then tracked and shrunk to the least size. Null when none fits.</summary>
    private static (float Size, float Tracking, float Width)? Line(ReadOnlySpan<char> text, float size, float least, float room, EmMeasure measure)
    {
        var plain = Width(text, size, 0f, measure);
        if (plain <= room)
        {
            return (size, 0f, plain);
        }

        var tracked = Width(text, size, TrackingEm, measure);
        if (tracked <= room)
        {
            return (size, TrackingEm * size, tracked);
        }

        // Tracked width is linear in the size: the size that fills the room exactly, a hair under so rounding never spills.
        var perSize = Width(text, 1f, TrackingEm, measure);
        if (!(perSize > 0f))
        {
            return null;
        }

        var fitted = MathF.Floor(room / perSize * 100f) / 100f;
        if (fitted < least)
        {
            return null;
        }

        return (fitted, TrackingEm * fitted, Width(text, fitted, TrackingEm, measure));
    }

    /// <summary>
    /// Two lines split at the space that leaves the narrower widest line, at one size and tracking for both (the larger
    /// that fits). Null when the label has no space or no split fits.
    /// </summary>
    private static RailLabelFit? Wrap(ReadOnlySpan<char> text, float size, float least, float room, EmMeasure measure)
    {
        var best = -1;
        var bestWidth = float.MaxValue;
        for (var i = 1; i < text.Length - 1; i++)
        {
            if (text[i] != ' ')
            {
                continue;
            }

            var first = text[..i].TrimEnd();
            var second = text[(i + 1)..].TrimStart();
            if (first.IsEmpty || second.IsEmpty)
            {
                continue;
            }

            var widest = MathF.Max(Width(first, 1f, 0f, measure), Width(second, 1f, 0f, measure));
            if (widest < bestWidth)
            {
                bestWidth = widest;
                best = i;
            }
        }

        if (best < 0)
        {
            return null;
        }

        var a = text[..best].TrimEnd();
        var b = text[(best + 1)..].TrimStart();
        if (Line(a, size, least, room, measure) is not { } lineA || Line(b, size, least, room, measure) is not { } lineB)
        {
            return null;
        }

        // Both lines at the smaller size and the tighter tracking either needed, so they read as one label.
        var shared = MathF.Min(lineA.Size, lineB.Size);
        var trackingEm = lineA.Tracking != 0f || lineB.Tracking != 0f ? TrackingEm : 0f;
        var width = MathF.Max(Width(a, shared, trackingEm, measure), Width(b, shared, trackingEm, measure));
        return new RailLabelFit(shared, trackingEm * shared, 2, best, width, false);
    }

    private static RailLabelFit IconOnlyFit(float size) => new(size, 0f, 0, -1, 0f, true);
}
