namespace Tsukimichi.Core.Ui;

/// <summary>What <see cref="LineFit.Fit"/> decided for one line.</summary>
/// <param name="NameRoom">The width the name may take; a longer name ends in an ellipsis in it.</param>
/// <param name="NameDrawn">The width the name is drawn at: its own, or <paramref name="NameRoom"/> when it is cut.</param>
/// <param name="NameCut">Whether the name ends in an ellipsis (its whole text then belongs in the tooltip).</param>
/// <param name="Visible">How many of the parts show (always the first ones).</param>
/// <param name="TailRoom">The width left for the tail after the name and the parts; 0 when the tail is left out.</param>
public readonly record struct LineFitResult(float NameRoom, float NameDrawn, bool NameCut, int Visible, float TailRoom);

/// <summary>
/// A line read left to right (R3 #5, "ellipses instead of names cut mid-letter"): a name, then short parts that follow
/// it (a route step's target mark, Questionable's mark), then a tail that takes whatever is left (a step's detail, an
/// overlay row's hint). The name keeps at least its minimum before the least important part is dropped
/// (<see cref="RowFit"/>); a longer name ends in an ellipsis rather than being cut mid-letter by a clip rect. The tail
/// shows only with at least its own minimum of room, ending in an ellipsis when it is longer. Span based, so a line
/// fitted every frame allocates nothing.
/// </summary>
public static class LineFit
{
    /// <summary>
    /// Fits a line <paramref name="available"/> pixels wide.
    /// </summary>
    /// <param name="available">The line's width for the name, the parts and the tail.</param>
    /// <param name="nameWidth">The name's full width.</param>
    /// <param name="nameMin">The least room the name keeps before a part is dropped.</param>
    /// <param name="parts">Each part's width including the gap before it, most important first.</param>
    /// <param name="visible">Filled with whether each part shows; at least as long as <paramref name="parts"/>.</param>
    /// <param name="tailGap">The gap before the tail.</param>
    /// <param name="tailMin">The least room the tail needs to show at all; 0 shows any tail that has room.</param>
    public static LineFitResult Fit(float available, float nameWidth, float nameMin, ReadOnlySpan<float> parts, Span<bool> visible, float tailGap = 0f, float tailMin = 0f)
    {
        var name = float.IsFinite(nameWidth) ? MathF.Max(0f, nameWidth) : 0f;
        var row = RowFit.Fit(available, name, nameMin, parts, visible);
        var cut = NeedsEllipsis(name, row.NameRoom);
        var drawn = cut ? row.NameRoom : name;
        var room = float.IsFinite(available) ? MathF.Max(0f, available) : 0f;
        var gap = float.IsFinite(tailGap) ? MathF.Max(0f, tailGap) : 0f;
        var min = float.IsFinite(tailMin) ? MathF.Max(0f, tailMin) : 0f;

        // A cut name leaves nothing for the tail: the name comes first.
        var tail = cut ? 0f : room - drawn - row.PartsWidth - gap;
        if (!(tail > 0f) || tail < min)
        {
            tail = 0f;
        }

        return new LineFitResult(row.NameRoom, drawn, cut, row.Visible, tail);
    }

    /// <summary>Whether a text <paramref name="width"/> wide needs an ellipsis in <paramref name="room"/> (half a pixel of grace for rounding).</summary>
    public static bool NeedsEllipsis(float width, float room) => width > MathF.Max(0f, room) + 0.5f;
}
