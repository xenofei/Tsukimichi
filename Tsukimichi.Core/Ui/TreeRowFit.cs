namespace Tsukimichi.Core.Ui;

/// <summary>A Journal tree row's measured widths in pixels, each part with the gap before it; 0 for a part the row does not offer.</summary>
/// <param name="Head">The label without its expansion suffix ("Main Scenario"), bold pixel included on a section row.</param>
/// <param name="Suffix">The expansion suffix as the label carries it (" · DT"); 0 for a node without one.</param>
/// <param name="Count">The count ("12 / 104", or a percentage); 0 where the tier shows none.</param>
/// <param name="Ready">The Ready pill; 0 when nothing is ready or the tier shows a dot instead.</param>
/// <param name="Bar">The mini bar; 0 where the tier shows none.</param>
/// <param name="Pill">The expansion pill; 0 where the tier or the node offers none.</param>
public readonly record struct TreeRowWidths(float Head, float Suffix, float Count, float Ready, float Bar, float Pill);

/// <summary>What <see cref="TreeRowFit.Fit"/> decided for one Journal tree row.</summary>
/// <param name="Count">Whether the count shows.</param>
/// <param name="Ready">Whether the Ready pill shows.</param>
/// <param name="Bar">Whether the mini bar shows.</param>
/// <param name="Pill">Whether the expansion pill shows.</param>
/// <param name="SuffixInLabel">The expansion suffix follows the head in the label, never cut, since its pill does not show.</param>
/// <param name="HeadRoom">The room for the head; a longer head ends in an ellipsis before the suffix.</param>
/// <param name="LabelRoom">The room for the whole label: the head's, then the suffix's when it is in the label.</param>
public readonly record struct TreeRowLayout(bool Count, bool Ready, bool Bar, bool Pill, bool SuffixInLabel, float HeadRoom, float LabelRoom);

/// <summary>
/// Fits a Journal tree row (feature plan v4 L3) with <see cref="RowFit"/>: the count goes last, then the Ready pill,
/// the mini bar, and the expansion pill first. A section's expansion suffix ("Main Scenario · DT") is what tells its
/// row from its twin's, so like the quest table's state word it is never cut: while its pill shows it is the pill, and
/// once the pill goes the label carries it whole after the head, the head ellipsised instead, and the row is fitted
/// again for that longer label (the suffix added to the name's minimum), so the bar goes before the suffix is squeezed.
/// Span based: a row fitted every frame allocates nothing.
/// </summary>
public static class TreeRowFit
{
    private const int CountPart = 0;
    private const int ReadyPart = 1;
    private const int BarPart = 2;
    private const int PillPart = 3;
    private const int PartCount = 4;

    /// <summary>Fits one row.</summary>
    /// <param name="available">The row's width for the label and the parts.</param>
    /// <param name="nameMin">The least room the head keeps before a part is dropped (<see cref="LayoutBudgets.RowNameMinLogical"/> in pixels).</param>
    /// <param name="widths">The row's measured widths.</param>
    public static TreeRowLayout Fit(float available, float nameMin, in TreeRowWidths widths)
    {
        Span<float> parts = stackalloc float[PartCount];
        Span<bool> visible = stackalloc bool[PartCount];
        parts[CountPart] = widths.Count;
        parts[ReadyPart] = widths.Ready;
        parts[BarPart] = widths.Bar;
        parts[PillPart] = widths.Pill;

        var head = Width(widths.Head);
        var min = Width(nameMin);
        if (Width(widths.Pill) > 0f)
        {
            var withPill = RowFit.Fit(available, head, min, parts, visible);
            if (visible[PillPart])
            {
                return new TreeRowLayout(Shows(parts, visible, CountPart), Shows(parts, visible, ReadyPart), Shows(parts, visible, BarPart), Pill: true, SuffixInLabel: false, withPill.NameRoom, withPill.NameRoom);
            }

            parts[PillPart] = 0f;
        }

        // No pill: the label carries the suffix, which keeps its whole width on top of the head's minimum.
        var suffix = Width(widths.Suffix);
        var fit = RowFit.Fit(available, head + suffix, MathF.Min(head, min) + suffix, parts, visible);
        return new TreeRowLayout(
            Shows(parts, visible, CountPart),
            Shows(parts, visible, ReadyPart),
            Shows(parts, visible, BarPart),
            Pill: false,
            SuffixInLabel: suffix > 0f,
            MathF.Max(0f, fit.NameRoom - suffix),
            fit.NameRoom);
    }

    private static bool Shows(ReadOnlySpan<float> parts, ReadOnlySpan<bool> visible, int part) => visible[part] && Width(parts[part]) > 0f;

    private static float Width(float value) => float.IsFinite(value) ? MathF.Max(0f, value) : 0f;
}
