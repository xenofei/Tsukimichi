using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>A non-moon mark: yes / no / not known, and the live or snapshot pip.</summary>
public enum Mark
{
    /// <summary>Met, obtained, attuned: a Moon check.</summary>
    Check,

    /// <summary>Unmet, not obtained, not attuned: a Dusk cross.</summary>
    Cross,

    /// <summary>Not readable: a short Dusk dash.</summary>
    Unknown,

    /// <summary>Live character: a filled Moon pip, static (accessibility B5; only T13's motion may animate it).</summary>
    LivePip,

    /// <summary>A stored snapshot: a hollow Dusk pip.</summary>
    SnapshotPip,
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

    /// <summary>The mark centred at <paramref name="center"/> inside a <paramref name="size"/> px box.</summary>
    public static void Draw(ImDrawListPtr dl, Vector2 center, float size, Mark mark)
    {
        if (!(size > 1f)) return;

        var stroke = MathF.Max(1.5f, 0.11f * size);
        switch (mark)
        {
            case Mark.Check:
                dl.PathClear();
                dl.PathLineTo(center + CheckA * size);
                dl.PathLineTo(center + CheckB * size);
                dl.PathLineTo(center + CheckC * size);
                dl.PathStroke(Theme.MoonU32, ImDrawFlags.None, stroke);
                break;

            case Mark.Cross:
                var d = CrossHalf * size;
                dl.AddLine(center + new Vector2(-d, -d), center + new Vector2(d, d), Theme.DuskU32, stroke);
                dl.AddLine(center + new Vector2(-d, d), center + new Vector2(d, -d), Theme.DuskU32, stroke);
                break;

            case Mark.Unknown:
                var h = DashHalf * size;
                dl.AddLine(center + new Vector2(-h, 0f), center + new Vector2(h, 0f), Theme.DuskU32, stroke);
                break;

            case Mark.LivePip:
                dl.AddCircleFilled(center, MathF.Max(2.5f, PipRadius * size), Theme.MoonU32);
                break;

            case Mark.SnapshotPip:
                var r = MathF.Max(2.5f, PipRadius * size);
                dl.AddCircle(center, r - 0.75f, Theme.DuskU32, 0, 1.5f);
                break;
        }
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
