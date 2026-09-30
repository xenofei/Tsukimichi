using Tsukimichi.Core.Evaluation;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// One column of a table planned by <see cref="TableGeometry.PlanColumns"/>.
/// </summary>
/// <param name="Priority">0 never hides; among the others the highest number hides first (ties: the later column first).</param>
/// <param name="Min">The least width the column needs to show.</param>
/// <param name="Ideal">The width a fixed column grows to when there is room (never under <paramref name="Min"/>).</param>
/// <param name="Weight">A stretch column's share of the room left over; 0 for a fixed column.</param>
public readonly record struct ColumnSpec(int Priority, float Min, float Ideal, float Weight = 0f)
{
    /// <summary>Whether the column takes a share of the room the fixed columns leave.</summary>
    public bool Stretch => Weight > 0f;
}

/// <summary>
/// Pure layout rules of the quest table's rows (feature plan v3 T15): the state moon sized from the row height, and
/// the Status cell's fit, where the state word is never cut (P1, game UX panel finding 3) and only the reason after
/// it gives way to an ellipsis.
/// </summary>
public static class TableGeometry
{
    /// <summary>Moon radius per pixel of row content: r 9 in a Comfortable row (28 px of content), r 6.4 in a Dense one (20 px), ui-revamp §2.4.</summary>
    public const float GlyphRadiusPerContent = 0.32f;

    /// <summary>Side of the square a row moon is centred in, per unit of radius (room for the Ready glow).</summary>
    public const float GlyphBoxPerRadius = 2.4f;

    /// <summary>
    /// The row moon's radius for a row with <paramref name="rowContent"/> pixels of content: a share of the row, never
    /// under <paramref name="minRadius"/> (the icon-scaled radius). The box it needs (radius × <see cref="GlyphBoxPerRadius"/>)
    /// always fits a row whose content already holds the minimum moon.
    /// </summary>
    public static float GlyphRadius(float rowContent, float minRadius)
    {
        var floor = float.IsFinite(minRadius) ? MathF.Max(0f, minRadius) : 0f;
        var content = float.IsFinite(rowContent) ? MathF.Max(0f, rowContent) : 0f;
        return MathF.Max(floor, content * GlyphRadiusPerContent);
    }

    /// <summary>
    /// Length of the state word at the start of a status line (<see cref="BlockerText.StatusText"/>): everything before
    /// the first <see cref="BlockerText.Separator"/>, or the whole line when it has no reason.
    /// </summary>
    public static int StateWordLength(string? status)
    {
        if (string.IsNullOrEmpty(status))
        {
            return 0;
        }

        var split = status.IndexOf(BlockerText.Separator, StringComparison.Ordinal);
        return split <= 0 ? status.Length : split;
    }

    /// <summary>
    /// Width the reason may take after the state word in a Status cell <paramref name="cellWidth"/> wide: whatever the
    /// state word leaves, never negative. The state word itself always keeps <paramref name="stateWidth"/>; when the
    /// reason is wider than this it is ellipsised, the state word never (P1).
    /// </summary>
    public static float ReasonWidth(float cellWidth, float stateWidth)
    {
        if (!float.IsFinite(cellWidth) || !float.IsFinite(stateWidth))
        {
            return 0f;
        }

        return MathF.Max(0f, cellWidth - MathF.Max(0f, stateWidth));
    }

    /// <summary>Whether a reason <paramref name="reasonWidth"/> wide must be ellipsised to fit <paramref name="room"/>.</summary>
    public static bool ReasonNeedsEllipsis(float reasonWidth, float room) => reasonWidth > room + 0.5f;

    /// <summary>
    /// The column plan (feature plan v4 L2/L4, UI audit §4): which columns of a table <paramref name="available"/>
    /// pixels wide show and how wide each is. Columns hide by priority, the highest number first, while the visible
    /// columns' minimums do not fit; priority 0 never hides. A column hidden last frame comes back only when it fits
    /// with <paramref name="hysteresis"/> to spare, so a width resting on a threshold does not make it flicker. The
    /// visible columns always form a prefix of the priority order, so the plan is the same for the same width and
    /// history. Widths: every visible column gets its minimum, then fixed columns grow toward their ideal in priority
    /// order, then stretch columns share what is left by weight. Nothing allocates.
    /// </summary>
    /// <param name="available">The table's width.</param>
    /// <param name="columns">The columns, in display order.</param>
    /// <param name="wasVisible">Last frame's visibility, in display order; empty on the first frame (everything counts as shown).</param>
    /// <param name="visible">Filled with this frame's visibility.</param>
    /// <param name="widths">Filled with each column's width (0 when hidden).</param>
    /// <param name="hysteresis">The margin a hidden column needs to come back (<see cref="LayoutBudgets.HysteresisLogical"/> in pixels).</param>
    /// <returns>How many columns show.</returns>
    public static int PlanColumns(float available, ReadOnlySpan<ColumnSpec> columns, ReadOnlySpan<bool> wasVisible, Span<bool> visible, Span<float> widths, float hysteresis)
    {
        var n = columns.Length;
        if (visible.Length < n || widths.Length < n)
        {
            throw new ArgumentException("The output spans are shorter than the columns.");
        }

        var room = float.IsFinite(available) ? MathF.Max(0f, available) : 0f;
        var margin = float.IsFinite(hysteresis) ? MathF.Max(0f, hysteresis) : 0f;
        var required = 0f;
        for (var i = 0; i < n; i++)
        {
            visible[i] = true;
            required += MinOf(columns[i]);
        }

        // Candidates from the first to hide: highest priority number, and among equals the later column. The first
        // column that stays ends the hiding, so everything more important stays too.
        var shown = n;
        while (true)
        {
            var candidate = -1;
            for (var i = 0; i < n; i++)
            {
                if (!visible[i] || columns[i].Priority <= 0)
                {
                    continue;
                }

                if (candidate < 0 || columns[i].Priority > columns[candidate].Priority || (columns[i].Priority == columns[candidate].Priority && i > candidate))
                {
                    candidate = i;
                }
            }

            if (candidate < 0)
            {
                break;
            }

            var returning = wasVisible.Length > candidate && !wasVisible[candidate];
            if (room >= required + (returning ? margin : 0f))
            {
                break;
            }

            visible[candidate] = false;
            required -= MinOf(columns[candidate]);
            shown--;
        }

        // Widths: minimums, then fixed columns toward their ideal by priority, then the stretch columns by weight.
        var left = room;
        var weights = 0f;
        for (var i = 0; i < n; i++)
        {
            widths[i] = visible[i] ? MinOf(columns[i]) : 0f;
            left -= widths[i];
            if (visible[i] && columns[i].Stretch)
            {
                weights += columns[i].Weight;
            }
        }

        // The distinct priorities of the visible fixed columns, lowest first (never every integer between them: a
        // priority of int.MaxValue is one step, not two billion).
        var done = false;
        var last = 0;
        while (left > 0f)
        {
            var found = false;
            var next = 0;
            for (var i = 0; i < n; i++)
            {
                var p = columns[i].Priority;
                if (visible[i] && !columns[i].Stretch && (!done || p > last) && (!found || p < next))
                {
                    next = p;
                    found = true;
                }
            }

            if (!found)
            {
                break;
            }

            for (var i = 0; i < n && left > 0f; i++)
            {
                if (visible[i] && !columns[i].Stretch && columns[i].Priority == next)
                {
                    var grow = MathF.Min(left, MathF.Max(0f, IdealOf(columns[i]) - widths[i]));
                    widths[i] += grow;
                    left -= grow;
                }
            }

            last = next;
            done = true;
        }

        if (left > 0f && weights > 0f)
        {
            for (var i = 0; i < n; i++)
            {
                if (visible[i] && columns[i].Stretch)
                {
                    widths[i] += left * (columns[i].Weight / weights);
                }
            }
        }

        return shown;
    }

    private static float MinOf(ColumnSpec column) => float.IsFinite(column.Min) ? MathF.Max(0f, column.Min) : 0f;

    private static float IdealOf(ColumnSpec column) => float.IsFinite(column.Ideal) ? MathF.Max(MinOf(column), column.Ideal) : MinOf(column);
}
