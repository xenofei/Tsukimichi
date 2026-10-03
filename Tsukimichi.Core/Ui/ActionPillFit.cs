namespace Tsukimichi.Core.Ui;

/// <summary>How one action pill is drawn on a row that <see cref="ActionPillFit.Fit"/> laid out.</summary>
public enum PillForm : byte
{
    /// <summary>Icon and the full label ("Go to giver").</summary>
    Full,

    /// <summary>Icon and the short label ("Go to").</summary>
    Short,

    /// <summary>The icon alone, still pill-shaped; the label moves into the tooltip.</summary>
    Icon,

    /// <summary>Not on the row: the pill is an item of the "…" menu instead.</summary>
    Overflow,
}

/// <summary>An action pill's width in each of its forms, in pixels.</summary>
/// <param name="Full">Icon and the full label.</param>
/// <param name="Short">Icon and the short label (the full width when the pill has no shorter label).</param>
/// <param name="Icon">The icon alone.</param>
public readonly record struct PillWidths(float Full, float Short, float Icon)
{
    /// <summary>The width of <paramref name="form"/>; 0 for <see cref="PillForm.Overflow"/>.</summary>
    public float For(PillForm form) => form switch
    {
        PillForm.Full => Full,
        PillForm.Short => Short,
        PillForm.Icon => Icon,
        _ => 0f,
    };
}

/// <summary>What <see cref="ActionPillFit.Fit"/> decided for a row of pills.</summary>
/// <param name="Width">The row's width as laid out, gaps included.</param>
/// <param name="Visible">How many pills stay on the row (a prefix of the list); the rest overflow into "…".</param>
public readonly record struct PillRowFit(float Width, int Visible);

/// <summary>
/// The detail pane's travel and automation row (1.10: "Go to giver", "Teleport", "Walk", "Start Questionable", "Run
/// with AutoDuty", each a labelled pill) fitted to the pane's width without cutting a label mid-letter. The pills come
/// in priority order, the first the most important. While the row is too wide it gives up, in this order and each
/// step from the least important pill still showing toward the first: full labels for short ones, then short labels
/// for icon-only pills, then whole pills, which overflow into the "…" menu. The first pill always stays on the row, so
/// the most important action is never more than one click away. Span based, so a row fitted every frame allocates
/// nothing.
/// </summary>
public static class ActionPillFit
{
    /// <summary>Space before the icon, logical pixels.</summary>
    public const float PadStartLogical = 12f;

    /// <summary>Space after the label, logical pixels.</summary>
    public const float PadEndLogical = 14f;

    /// <summary>Between the icon and the label, logical pixels.</summary>
    public const float IconGapLogical = 6f;

    /// <summary>An icon-only pill is this many times its height across, so it still reads as a pill beside the round buttons.</summary>
    public const float IconOnlyAspect = 1.45f;

    /// <summary>Between two pills on the row, logical pixels.</summary>
    public const float GapLogical = 6f;

    /// <summary>A labelled pill's width: the pads, the icon, the gap and the label.</summary>
    public static float LabelledWidth(float iconWidth, float labelWidth, float scale)
    {
        var s = float.IsFinite(scale) && scale > 0f ? scale : 1f;
        return ((PadStartLogical + IconGapLogical + PadEndLogical) * s) + Positive(iconWidth) + Positive(labelWidth);
    }

    /// <summary>
    /// A text-only pill's width (Plain's buttons): the label between the two pads, with no icon and no gap, so the label
    /// can sit centred in it.
    /// </summary>
    public static float TextOnlyWidth(float labelWidth, float scale)
    {
        var s = float.IsFinite(scale) && scale > 0f ? scale : 1f;
        return ((PadStartLogical + PadEndLogical) * s) + Positive(labelWidth);
    }

    /// <summary>An icon-only pill's width for a pill <paramref name="height"/> tall.</summary>
    public static float IconOnlyWidth(float height) => MathF.Round(Positive(height) * IconOnlyAspect);

    /// <summary>
    /// Fits the row: fills <paramref name="forms"/> with each pill's form and returns the width it takes and how many
    /// pills stay on it.
    /// </summary>
    /// <param name="available">The row's width.</param>
    /// <param name="gap">The space between two pills.</param>
    /// <param name="pills">Each pill's widths, most important first.</param>
    /// <param name="forms">Filled with each pill's form; at least as long as <paramref name="pills"/>.</param>
    public static PillRowFit Fit(float available, float gap, ReadOnlySpan<PillWidths> pills, Span<PillForm> forms)
    {
        if (forms.Length < pills.Length)
        {
            throw new ArgumentException("The forms span is shorter than the pills.", nameof(forms));
        }

        var room = float.IsFinite(available) ? MathF.Max(0f, available) : 0f;
        var space = float.IsFinite(gap) ? MathF.Max(0f, gap) : 0f;
        for (var i = 0; i < pills.Length; i++)
        {
            forms[i] = PillForm.Full;
        }

        var used = Width(pills, forms, space);
        if (used <= room)
        {
            return new PillRowFit(used, pills.Length);
        }

        // Full labels for short ones, then short labels for icons, each from the least important pill toward the first.
        for (var step = 0; step < 2; step++)
        {
            var form = step == 0 ? PillForm.Short : PillForm.Icon;
            for (var i = pills.Length - 1; i >= 0; i--)
            {
                forms[i] = form;
                used = Width(pills, forms, space);
                if (used <= room)
                {
                    return new PillRowFit(used, pills.Length);
                }
            }
        }

        // Whole pills overflow into "…", the least important first; the first pill always stays.
        var visible = pills.Length;
        while (visible > 1 && used > room)
        {
            visible--;
            forms[visible] = PillForm.Overflow;
            used = Width(pills, forms, space);
        }

        return new PillRowFit(used, visible);
    }

    private static float Width(ReadOnlySpan<PillWidths> pills, ReadOnlySpan<PillForm> forms, float gap)
    {
        var width = 0f;
        var shown = 0;
        for (var i = 0; i < pills.Length; i++)
        {
            if (forms[i] == PillForm.Overflow)
            {
                continue;
            }

            width += Positive(pills[i].For(forms[i]));
            shown++;
        }

        return shown > 1 ? width + ((shown - 1) * gap) : width;
    }

    private static float Positive(float value) => float.IsFinite(value) ? MathF.Max(0f, value) : 0f;
}
