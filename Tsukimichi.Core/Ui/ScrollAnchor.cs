using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The row the quest table keeps in place (feature plan v6 U3): the selected row while it is on screen, else the first
/// row on screen, with how far it sat from the top of the rows' view.
/// </summary>
/// <param name="RowId">The anchored quest's row id.</param>
/// <param name="Index">Its index in the rows it was taken from.</param>
/// <param name="Offset">Its top's distance from the top of the rows' view (negative when partly scrolled past).</param>
/// <param name="Selected">Whether it is the selected row.</param>
public readonly record struct RowAnchor(uint RowId, int Index, float Offset, bool Selected);

/// <summary>
/// Keeps the quest table's rows still when they change (feature plan v6 U3): after a filter, a search, a re-sort or a
/// live quest update the selected row stays where it was on screen, rather than the list holding its pixel offset and
/// the row sliding away. Rows are <c>rowHeight</c> tall and row i starts at i × rowHeight in the scroll's coordinates.
/// Pure, so the arithmetic is tested.
/// </summary>
public static class ScrollAnchor
{
    /// <summary>Where a jump to a row puts it: 40 % down the view, as the table's reveal always has.</summary>
    public const float RevealFraction = 0.4f;

    /// <summary>
    /// The anchor for this frame: the selected row when it is on screen (<paramref name="selectedIndex"/>, -1 when it is
    /// not listed), else the first row on screen. False with no rows.
    /// </summary>
    public static bool TryCapture(ReadOnlySpan<QuestRow> rows, int selectedIndex, float scrollY, float rowHeight, float viewHeight, out RowAnchor anchor)
    {
        anchor = default;
        if (rows.Length == 0 || !(rowHeight > 0f))
        {
            return false;
        }

        if (selectedIndex >= 0 && selectedIndex < rows.Length)
        {
            var top = (selectedIndex * rowHeight) - scrollY;
            if (top + rowHeight > 0f && top < viewHeight)
            {
                anchor = new RowAnchor(rows[selectedIndex].Quest.RowId, selectedIndex, top, Selected: true);
                return true;
            }
        }

        var first = Math.Clamp((int)MathF.Floor(MathF.Max(0f, scrollY) / rowHeight), 0, rows.Length - 1);
        anchor = new RowAnchor(rows[first].Quest.RowId, first, (first * rowHeight) - scrollY, Selected: false);
        return true;
    }

    /// <summary>
    /// The scroll that keeps <paramref name="anchor"/> (taken from <paramref name="oldRows"/>) at its place on screen in
    /// <paramref name="newRows"/>, whose row ids <paramref name="newIndex"/> maps to their index. When the anchored row
    /// is gone, its nearest neighbour in the old rows that is still listed takes its place. Clamped to the new list;
    /// <paramref name="landedIndex"/> is where the kept row is now (-1 when none survived, and the scroll is 0).
    /// </summary>
    public static float Restore(in RowAnchor anchor, ReadOnlySpan<QuestRow> oldRows, IReadOnlyDictionary<uint, int> newIndex, int newCount, float rowHeight, float viewHeight, out int landedIndex)
    {
        ArgumentNullException.ThrowIfNull(newIndex);
        landedIndex = -1;
        if (newCount == 0 || !(rowHeight > 0f))
        {
            return 0f;
        }

        if (newIndex.TryGetValue(anchor.RowId, out var index))
        {
            landedIndex = index;
            return Clamp((index * rowHeight) - anchor.Offset, newCount, rowHeight, viewHeight);
        }

        // Filtered out: walk outward from where it was to the nearest row that is still listed, below first.
        var from = Math.Clamp(anchor.Index, 0, Math.Max(0, oldRows.Length - 1));
        for (var step = 1; step <= oldRows.Length; step++)
        {
            for (var side = 0; side < 2; side++)
            {
                var i = side == 0 ? from + step : from - step;
                if (i < 0 || i >= oldRows.Length || !newIndex.TryGetValue(oldRows[i].Quest.RowId, out index))
                {
                    continue;
                }

                // The neighbour keeps its own place on screen, so the rows around the gap do not move.
                landedIndex = index;
                var offset = anchor.Offset + ((i - anchor.Index) * rowHeight);
                return Clamp((index * rowHeight) - offset, newCount, rowHeight, viewHeight);
            }
        }

        return 0f;
    }

    /// <summary>The scroll that puts row <paramref name="index"/> <see cref="RevealFraction"/> down the view.</summary>
    public static float Reveal(int index, int count, float rowHeight, float viewHeight) =>
        Clamp((index * rowHeight) - (viewHeight * RevealFraction), count, rowHeight, viewHeight);

    /// <summary>
    /// The rows' view height: the table's clip rectangle (<paramref name="clipTop"/> to <paramref name="clipBottom"/>)
    /// less the frozen header above <paramref name="rowsTop"/>, at least one row. The header scrolls with nothing, so a
    /// view that counted it would clamp a list scrolled to its end one header short, and the rows would jump.
    /// </summary>
    public static float RowsView(float clipTop, float clipBottom, float rowsTop, float rowHeight) =>
        MathF.Max(rowHeight, clipBottom - MathF.Max(clipTop, rowsTop));

    /// <summary><paramref name="scrollY"/> kept between the top and the last row's bottom at the view's bottom.</summary>
    public static float Clamp(float scrollY, int count, float rowHeight, float viewHeight)
    {
        var max = MathF.Max(0f, (count * rowHeight) - MathF.Max(0f, viewHeight));
        return float.IsFinite(scrollY) ? Math.Clamp(scrollY, 0f, max) : 0f;
    }
}
