namespace Tsukimichi.Core.Ui;

/// <summary>
/// The Journal's one-line chip lane (feature plan v6 U2): the engaged filters as chips on a single line of fixed
/// height that never wraps. When they do not all fit, as many as fit stay in order and the rest go behind a "+N" chip
/// whose popover lists them, each still clearable. Pure arithmetic over measured widths, so it is tested.
/// </summary>
public static class ChipLane
{
    /// <summary>
    /// How many leading chips show in <paramref name="room"/>: all of them when they fit with
    /// <paramref name="gap"/> between them; otherwise the most that leave room for the "+N" chip
    /// (<paramref name="moreWidth"/> wide, after one more gap). Never negative; 0 means only "+N" shows.
    /// </summary>
    public static int Fit(ReadOnlySpan<float> widths, float room, float gap, float moreWidth)
    {
        if (widths.Length == 0)
        {
            return 0;
        }

        if (Width(widths, widths.Length, gap) <= room)
        {
            return widths.Length;
        }

        var shown = 0;
        var used = 0f;
        for (var i = 0; i < widths.Length; i++)
        {
            var next = used + (i > 0 ? gap : 0f) + widths[i];
            if (next + gap + moreWidth > room)
            {
                break;
            }

            used = next;
            shown++;
        }

        return shown;
    }

    /// <summary>The width the first <paramref name="count"/> chips take with <paramref name="gap"/> between them.</summary>
    public static float Width(ReadOnlySpan<float> widths, int count, float gap)
    {
        count = Math.Clamp(count, 0, widths.Length);
        var width = 0f;
        for (var i = 0; i < count; i++)
        {
            width += widths[i] + (i > 0 ? gap : 0f);
        }

        return width;
    }
}
