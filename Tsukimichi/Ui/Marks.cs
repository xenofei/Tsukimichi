using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>A non-moon mark: yes / no / not known, and the live or snapshot pip.</summary>
public enum Mark
{
    /// <summary>Met, obtained, attuned: a MoonDim check (done, so quieter than actionable gold).</summary>
    Check,

    /// <summary>Unmet, not obtained, not attuned: a Dusk cross.</summary>
    Cross,

    /// <summary>Not readable: a short Dusk dash.</summary>
    Unknown,

    /// <summary>Live character: a filled Moon pip, static (accessibility B5; only T13's motion may animate it).</summary>
    LivePip,

    /// <summary>A stored snapshot: a hollow Dusk pip.</summary>
    SnapshotPip,

    /// <summary>A character live in another game client (multibox, D11): a Moon ring around a small Moon dot, static.</summary>
    ElsewherePip,

    /// <summary>
    /// A requirement the character does not meet (feature plan v4 L8): an Eclipse cross, bolder than <see cref="Cross"/>,
    /// so an unmet line reads apart from the quiet met ones.
    /// </summary>
    Unmet,

    /// <summary>
    /// A requirement Tsukimichi can't check (1.19 C3): a hollow Dusk ring, 9 px across at a 1.5 px stroke (2 px in the
    /// high-contrast palette), the third verdict beside check and cross. Never read as unmet.
    /// </summary>
    CantCheck,
}

/// <summary>
/// Check, cross, dash and pip marks drawn with ImDrawList lines, for the places that used to borrow a state moon:
/// requirement lines, reward "obtained" cells, current attunement and the live / snapshot indicator. A moon means a
/// quest state or a completion fraction, nothing else (accessibility panel B2).
/// </summary>
public static class Marks
{
    // Check stroke in fractions of the box: short leg down-right, long leg up-right.
    private static readonly Vector2 CheckA = new(-0.30f, 0.00f);
    private static readonly Vector2 CheckB = new(-0.10f, 0.22f);
    private static readonly Vector2 CheckC = new(0.30f, -0.24f);
    private const float CrossHalf = 0.22f;
    private const float DashHalf = 0.20f;
    private const float PipRadius = 0.18f;

    /// <summary>
    /// The mark centred at <paramref name="center"/> inside a <paramref name="size"/> px box, in the colours of the glyph
    /// palette in effect (<see cref="Theme.Glyphs"/>). The high-contrast palette draws its brighter or darker rungs at
    /// a thicker stroke over a 1 px keyline in the palette's ground, so the mark reads on any surface.
    /// </summary>
    public static void Draw(ImDrawListPtr dl, Vector2 center, float size, Mark mark)
    {
        if (!(size > 1f)) return;

        var palette = Theme.Glyphs;
        var colors = palette.Marks;
        var stroke = MathF.Max(1.5f, 0.11f * size) * colors.StrokeScale;
        var keyline = palette.HighContrast ? 1f : 0f;
        var ground = Theme.U32(palette.Ground);
        switch (mark)
        {
            case Mark.Check:
                if (keyline > 0f) Check(dl, center, size, ground, stroke + 2f * keyline);
                Check(dl, center, size, palette.HighContrast ? Theme.U32(colors.Check) : Theme.GoldDimU32, stroke);
                break;

            case Mark.Cross:
                if (keyline > 0f) Cross(dl, center, size, ground, stroke + 2f * keyline);
                Cross(dl, center, size, palette.HighContrast ? Theme.U32(colors.Cross) : Theme.U32(Theme.Surface.TextTertiary), stroke);
                break;

            case Mark.Unmet:
                var bold = stroke * 1.25f;
                if (keyline > 0f) Cross(dl, center, size, ground, bold + 2f * keyline);
                Cross(dl, center, size, palette.HighContrast ? Theme.U32(colors.Cross) : Theme.DangerTextU32, bold);
                break;

            case Mark.Unknown:
                var h = DashHalf * size;
                if (keyline > 0f) dl.AddLine(center + new Vector2(-h - keyline, 0f), center + new Vector2(h + keyline, 0f), ground, stroke + 2f * keyline);
                dl.AddLine(center + new Vector2(-h, 0f), center + new Vector2(h, 0f), palette.HighContrast ? Theme.U32(colors.Dash) : Theme.U32(Theme.Surface.TextTertiary), stroke);
                break;

            case Mark.CantCheck:
                var hollowStroke = palette.HighContrast ? 2f : 1.5f;
                var hollow = MathF.Min(UiMetrics.Px(4.5f), size * 0.5f) - (hollowStroke * 0.5f);
                if (keyline > 0f) dl.AddCircle(center, hollow, ground, 0, hollowStroke + 2f * keyline);
                dl.AddCircle(center, hollow, palette.HighContrast ? Theme.U32(colors.Dash) : Theme.U32(Theme.Surface.TextTertiary), 0, hollowStroke);
                break;

            case Mark.LivePip:
                var live = MathF.Max(2.5f, PipRadius * size);
                if (keyline > 0f) dl.AddCircleFilled(center, live + keyline, ground);
                dl.AddCircleFilled(center, live, palette.HighContrast ? Theme.U32(colors.LivePip) : Theme.GoldU32);
                break;

            case Mark.SnapshotPip:
                var r = MathF.Max(2.5f, PipRadius * size);
                var ring = 1.5f * colors.StrokeScale;
                if (keyline > 0f) dl.AddCircleFilled(center, r + keyline, ground);
                dl.AddCircle(center, r - ring * 0.5f, palette.HighContrast ? Theme.U32(colors.SnapshotPip) : Theme.U32(Theme.Surface.TextTertiary), 0, ring);
                break;

            case Mark.ElsewherePip:
                var outer = MathF.Max(3.5f, PipRadius * 1.6f * size);
                var elsewhereRing = 1.5f * colors.StrokeScale;
                var ink = palette.HighContrast ? Theme.U32(colors.LivePip) : Theme.GoldU32;
                if (keyline > 0f) dl.AddCircleFilled(center, outer + keyline, ground);
                dl.AddCircle(center, outer - elsewhereRing * 0.5f, ink, 0, elsewhereRing);
                dl.AddCircleFilled(center, MathF.Max(1.5f, outer * 0.4f), ink);
                break;
        }
    }

    private static void Check(ImDrawListPtr dl, Vector2 center, float size, uint color, float stroke)
    {
        dl.PathClear();
        dl.PathLineTo(center + CheckA * size);
        dl.PathLineTo(center + CheckB * size);
        dl.PathLineTo(center + CheckC * size);
        dl.PathStroke(color, ImDrawFlags.None, stroke);
    }

    private static void Cross(ImDrawListPtr dl, Vector2 center, float size, uint color, float stroke)
    {
        var d = CrossHalf * size;
        dl.AddLine(center + new Vector2(-d, -d), center + new Vector2(d, d), color, stroke);
        dl.AddLine(center + new Vector2(-d, d), center + new Vector2(d, -d), color, stroke);
    }

    /// <summary>Reserves a <paramref name="size"/> square item at the cursor and draws the mark in it, so a tooltip can hang off it.</summary>
    public static void DrawInline(Mark mark, float size)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        Draw(ImGui.GetWindowDrawList(), pos + new Vector2(size * 0.5f), size, mark);
    }

    /// <summary>Yes / no / unknown as a mark.</summary>
    public static Mark For(bool? value) => value switch
    {
        true => Mark.Check,
        false => Mark.Cross,
        null => Mark.Unknown,
    };
}
