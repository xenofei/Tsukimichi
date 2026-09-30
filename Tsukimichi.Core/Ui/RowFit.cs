namespace Tsukimichi.Core.Ui;

/// <summary>What <see cref="RowFit.Fit"/> decided for one row.</summary>
/// <param name="NameRoom">The width the name may take; a longer name is ellipsised in it.</param>
/// <param name="PartsWidth">The width of the parts that show, gaps included.</param>
/// <param name="Visible">How many parts show.</param>
public readonly record struct RowFitResult(float NameRoom, float PartsWidth, int Visible);

/// <summary>
/// A row that drops parts rather than let them collide (feature plan v4 L2, UI audit §4, design v4 §8.1): a name
/// followed by optional parts (a pill, a badge, a bar, a count) laid out from the row's right edge. The parts come in
/// priority order, the first the most important; while the name would have less than its minimum the least important
/// part still showing goes, so what remains is always a prefix of the priority list. A name shorter than its minimum
/// only needs its own width. Span based, so a row fitted every frame allocates nothing. The tree and the list rows
/// adopt it in later tasks (L3, L6).
/// </summary>
public static class RowFit
{
    /// <summary>
    /// Fits a row: which parts show and the room left for the name.
    /// </summary>
    /// <param name="available">The row's width for the name and the parts.</param>
    /// <param name="nameWidth">The name's full width (<see cref="float.PositiveInfinity"/> when not measured).</param>
    /// <param name="nameMin">The least room the name keeps before a part is dropped (<see cref="LayoutBudgets.RowNameMinLogical"/> in pixels).</param>
    /// <param name="parts">Each part's width including its gap, most important first.</param>
    /// <param name="visible">Filled with whether each part shows; at least as long as <paramref name="parts"/>.</param>
    public static RowFitResult Fit(float available, float nameWidth, float nameMin, ReadOnlySpan<float> parts, Span<bool> visible)
    {
        if (visible.Length < parts.Length)
        {
            throw new ArgumentException("The visible span is shorter than the parts.", nameof(visible));
        }

        var room = float.IsFinite(available) ? MathF.Max(0f, available) : 0f;
        var name = float.IsNaN(nameWidth) ? float.PositiveInfinity : MathF.Max(0f, nameWidth);
        var min = float.IsFinite(nameMin) ? MathF.Max(0f, nameMin) : 0f;
        var need = MathF.Min(name, min);

        var used = 0f;
        for (var i = 0; i < parts.Length; i++)
        {
            used += Width(parts[i]);
        }

        // Drop from the least important end until the name has what it needs.
        var count = parts.Length;
        while (count > 0 && room - used < need)
        {
            count--;
            used -= Width(parts[count]);
        }

        for (var i = 0; i < parts.Length; i++)
        {
            visible[i] = i < count;
        }

        used = MathF.Max(0f, used);
        return new RowFitResult(MathF.Max(0f, room - used), used, count);
    }

    private static float Width(float part) => float.IsFinite(part) ? MathF.Max(0f, part) : 0f;
}
