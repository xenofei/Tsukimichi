namespace Tsukimichi.Core.Ui;

/// <summary>
/// The filter drawer's geometry and timing (plan v7 UI-2, docs/design/v7/ui/spec.md §2.1), kept free of ImGui so it is
/// tested. The drawer is exactly the tree column (no least width, so it never overlaps the list), except while the
/// tree is its icon strip, when a strip is too narrow to hold a control and the drawer takes
/// <see cref="StripFloorLogical"/>. Its height is its content's, measured on the previous frame, capped at the body;
/// past the cap the body scrolls and the header and footer stay. The tree under it fades out with the drawer's own
/// fade and is not drawn at all once the drawer is opaque, so nothing of the tree can paint over the sheet.
/// </summary>
public static class DrawerLayout
{
    /// <summary>The drawer's width while the tree is its icon strip, logical: the narrowest tree column a level ships with.</summary>
    public const float StripFloorLogical = 262f;

    /// <summary>
    /// The drawer's width in pixels: the tree column's <paramref name="treeWidth"/>, or at least
    /// <paramref name="stripFloor"/> while the tree is its strip, never past <paramref name="room"/> (the body's right
    /// edge less the column's left) and never under one pixel. A width that is not a number reads as one pixel.
    /// </summary>
    public static float Width(float treeWidth, bool treeStrip, float stripFloor, float room)
    {
        var width = float.IsFinite(treeWidth) ? treeWidth : 1f;
        if (treeStrip && float.IsFinite(stripFloor))
        {
            width = MathF.Max(width, stripFloor);
        }

        if (float.IsFinite(room))
        {
            width = MathF.Min(width, room);
        }

        return MathF.Max(1f, width);
    }

    /// <summary>
    /// The sheet's heights for a <paramref name="header"/> and <paramref name="footer"/> that always show and a body
    /// whose content measured <paramref name="content"/> on the last frame, capped at <paramref name="cap"/> (the body
    /// of the window). Content not yet measured (zero or less) takes the whole cap, for the one unseen frame that
    /// measures it. Non-finite inputs read as zero.
    /// </summary>
    public static DrawerHeights Heights(float header, float content, float footer, float cap)
    {
        header = NonNegative(header);
        footer = NonNegative(footer);
        content = NonNegative(content);
        cap = MathF.Max(1f, NonNegative(cap));
        var chrome = header + footer;
        var sheet = content > 0f ? MathF.Min(chrome + content, cap) : cap;
        var body = MathF.Max(0f, sheet - chrome);

        // Half a pixel of slack, so a measurement that rounds differently never shows a scrollbar for nothing.
        return new DrawerHeights(sheet, body, content > 0f && content > body + 0.5f);
    }

    /// <summary>
    /// The drawer's opacity <paramref name="now"/>: 0 → 1 over <paramref name="seconds"/> from
    /// <paramref name="openedAt"/>, at once under <paramref name="reduceMotion"/>. A negative
    /// <paramref name="openedAt"/> (not drawn yet) is 0, so the tree is drawn in full on the drawer's first frame.
    /// </summary>
    public static float Fade(double openedAt, double now, float seconds, bool reduceMotion)
    {
        if (openedAt < 0.0 || !double.IsFinite(openedAt) || !double.IsFinite(now))
        {
            return 0f;
        }

        if (reduceMotion || !(seconds > 0f))
        {
            return 1f;
        }

        return (float)Math.Clamp((now - openedAt) / seconds, 0.0, 1.0);
    }

    /// <summary>
    /// The sheet's metrics at <paramref name="flair"/>, logical px (spec §2.2): Full is spacious with a moon-road divider
    /// under its header, Quiet a little tighter, Plain the ledger's dense bands.
    /// </summary>
    public static DrawerMetrics MetricsFor(Flair flair) => flair switch
    {
        Flair.Full => new DrawerMetrics(Header: 52f, Divider: 12f, Footer: 48f, PadX: 14f, SectionGap: 12f, SectionHead: 30f, HeadGap: 4f, ToggleRow: 36f, SmallRow: 30f, SummaryLine: 30f, Button: 26f, Field: 28f, Chip: 26f, Segment: 26f, KindSegment: 22f, Rounding: 6f),
        Flair.Quiet => new DrawerMetrics(Header: 46f, Divider: 0f, Footer: 42f, PadX: 12f, SectionGap: 10f, SectionHead: 26f, HeadGap: 4f, ToggleRow: 34f, SmallRow: 30f, SummaryLine: 30f, Button: 26f, Field: 28f, Chip: 26f, Segment: 26f, KindSegment: 22f, Rounding: 6f),
        _ => new DrawerMetrics(Header: 26f, Divider: 0f, Footer: 24f, PadX: 8f, SectionGap: 4f, SectionHead: 20f, HeadGap: 2f, ToggleRow: 24f, SmallRow: 24f, SummaryLine: 22f, Button: 20f, Field: 20f, Chip: 22f, Segment: 22f, KindSegment: 20f, Rounding: 0f),
    };

    /// <summary>The tree's opacity under a drawer at <paramref name="drawerFade"/>: it fades out as the drawer fades in.</summary>
    public static float TreeAlpha(float drawerFade) => float.IsFinite(drawerFade) ? 1f - Math.Clamp(drawerFade, 0f, 1f) : 1f;

    /// <summary>Whether the tree is skipped under a drawer at <paramref name="drawerFade"/>: once the drawer is opaque.</summary>
    public static bool TreeHidden(float drawerFade) => drawerFade >= 1f;

    private static float NonNegative(float value) => float.IsFinite(value) ? MathF.Max(0f, value) : 0f;
}

/// <summary>The drawer's heights from <see cref="DrawerLayout.Heights"/>, in pixels.</summary>
/// <param name="Sheet">The whole sheet, header and footer included.</param>
/// <param name="Body">The scrolling body between them.</param>
/// <param name="Scrolls">Whether the body's content is taller than the body.</param>
public readonly record struct DrawerHeights(float Sheet, float Body, bool Scrolls);

/// <summary>The drawer's sizes at one level from <see cref="DrawerLayout.MetricsFor"/>, logical px (a row never shrinks under its text).</summary>
/// <param name="Header">The header row: glyph, title, count, pin and close.</param>
/// <param name="Divider">The moon-road divider's band under the header (Full only).</param>
/// <param name="Footer">The footer: "Showing N of M" and Reset.</param>
/// <param name="PadX">The sheet's side padding.</param>
/// <param name="SectionGap">The space above a section head.</param>
/// <param name="SectionHead">A section head's row (Show, Quick views, Advanced).</param>
/// <param name="HeadGap">The space between a section head and its first row.</param>
/// <param name="ToggleRow">A Show row: label, caption and toggle.</param>
/// <param name="SmallRow">A More toggle's row and a reward kind's row.</param>
/// <param name="SummaryLine">One of Advanced's seven summary lines.</param>
/// <param name="Button">The header's pin and close buttons.</param>
/// <param name="Field">A field pill: the stepper, the Added in combo, a level.</param>
/// <param name="Chip">A state chip.</param>
/// <param name="Segment">An expansion or job segment row.</param>
/// <param name="KindSegment">A reward kind's Hide · Show · Only segment.</param>
/// <param name="Rounding">The sheet's bottom-right radius and a row's hover radius.</param>
public readonly record struct DrawerMetrics(
    float Header,
    float Divider,
    float Footer,
    float PadX,
    float SectionGap,
    float SectionHead,
    float HeadGap,
    float ToggleRow,
    float SmallRow,
    float SummaryLine,
    float Button,
    float Field,
    float Chip,
    float Segment,
    float KindSegment,
    float Rounding);
