using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// A button guarded by a <see cref="ConfirmGate"/>: Shift and a click confirm at once; otherwise the button must be
/// held for the gate's duration while a Moon arc, stroked along the button's outline with <c>PathArcTo</c> and
/// <c>PathStroke</c>, closes clockwise from the top centre over a Veil track. Under Reduce motion the arc gives way to
/// a countdown in the label ("Hold… (0.4 s)"). The label must end in <c>###</c> plus an id so the countdown text can
/// replace it without changing the ImGui id (see <see cref="IdSuffix"/>). Nothing here allocates per frame: the
/// countdown labels are built once.
/// </summary>
internal static class HoldButton
{
    /// <summary>Every guarded label ends with this so its id survives the countdown text.</summary>
    public const string IdSuffix = "###hold";

    private const float OutsetLogical = 2f;
    private const float ThicknessLogical = 2f;

    private static readonly uint TrackU32 = Theme.WithAlpha(Theme.Veil, 0.9f);

    // Index = tenths of a second left, 1..HoldTenths; the countdown text shown under Reduce motion.
    private static readonly string[] Countdown = BuildCountdown();
    private static readonly int HoldTenths = Countdown.Length - 1;

    /// <summary>
    /// Draws the button and advances the gate. Returns true on the frame the action is confirmed; the button stays
    /// the last item so the caller can hang a tooltip on it.
    /// </summary>
    /// <param name="label">Visible text followed by <see cref="IdSuffix"/>.</param>
    /// <param name="scale">Pixels per logical unit for the arc's outset and thickness.</param>
    public static bool Draw(string label, ConfirmGate gate, bool reduceMotion, float scale)
    {
        ArgumentNullException.ThrowIfNull(gate);
        var io = ImGui.GetIO();
        var chord = io.KeyShift;

        // A fixed width keeps the row still when the countdown text replaces the label.
        var padding = ImGui.GetStyle().FramePadding;
        var width = MathF.Max(ImGui.CalcTextSize(label, true, -1f).X, ImGui.CalcTextSize(Countdown[HoldTenths], true, -1f).X) + padding.X * 2f;
        var text = reduceMotion && gate.Holding ? Countdown[CountdownIndex(gate)] : label;
        ImGui.Button(text, new Vector2(width, 0f));
        var active = ImGui.IsItemActive();

        var confirmed = gate.Update(chord, active, io.DeltaTime);
        if (!reduceMotion && gate.Holding)
        {
            var rounding = ImGui.GetStyle().FrameRounding;
            DrawArc(ImGui.GetWindowDrawList(), ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), rounding, gate.Progress, scale);
        }

        return confirmed;
    }

    /// <summary>Which countdown label to show: the tenths of a second still to hold, never below one.</summary>
    private static int CountdownIndex(ConfirmGate gate)
    {
        var tenths = (int)MathF.Ceiling(gate.Remaining * 10f - 0.0001f);
        return Math.Clamp(tenths, 1, HoldTenths);
    }

    /// <summary>
    /// The track (full outline in Veil) and the Moon arc over the first <paramref name="fraction"/> of the outline,
    /// walked clockwise from the top centre: straight edges as lines, rounded corners as quarter arcs.
    /// </summary>
    public static void DrawArc(ImDrawListPtr dl, Vector2 itemMin, Vector2 itemMax, float frameRounding, float fraction, float scale)
    {
        var outset = OutsetLogical * scale;
        var thickness = MathF.Max(1.5f, ThicknessLogical * scale);
        var min = itemMin - new Vector2(outset, outset);
        var max = itemMax + new Vector2(outset, outset);
        var w = max.X - min.X;
        var h = max.Y - min.Y;
        if (w <= 0f || h <= 0f)
        {
            return;
        }

        var r = MathF.Max(0f, MathF.Min(frameRounding + outset, MathF.Min(w, h) * 0.5f));
        dl.AddRect(min, max, TrackU32, r, ImDrawFlags.RoundCornersAll, thickness);
        if (fraction <= 0f)
        {
            return;
        }

        var quarter = MathF.PI * 0.5f;
        var arcLength = quarter * r;
        var total = 2f * (w + h) - 8f * r + 4f * arcLength;
        var budget = Math.Clamp(fraction, 0f, 1f) * total;
        var segments = Math.Max(3, (int)MathF.Ceiling(r / 3f));

        var topCentre = new Vector2(min.X + w * 0.5f, min.Y);
        dl.PathClear();
        dl.PathLineTo(topCentre);

        // Each step returns false once the budget ran out inside it, which ends the walk.
        if (Edge(dl, topCentre, new Vector2(max.X - r, min.Y), ref budget)
            && Corner(dl, new Vector2(max.X - r, min.Y + r), r, -quarter, arcLength, segments, ref budget)
            && Edge(dl, new Vector2(max.X, min.Y + r), new Vector2(max.X, max.Y - r), ref budget)
            && Corner(dl, new Vector2(max.X - r, max.Y - r), r, 0f, arcLength, segments, ref budget)
            && Edge(dl, new Vector2(max.X - r, max.Y), new Vector2(min.X + r, max.Y), ref budget)
            && Corner(dl, new Vector2(min.X + r, max.Y - r), r, quarter, arcLength, segments, ref budget)
            && Edge(dl, new Vector2(min.X, max.Y - r), new Vector2(min.X, min.Y + r), ref budget)
            && Corner(dl, new Vector2(min.X + r, min.Y + r), r, 2f * quarter, arcLength, segments, ref budget))
        {
            Edge(dl, new Vector2(min.X + r, min.Y), topCentre, ref budget);
        }

        dl.PathStroke(Theme.MoonU32, ImDrawFlags.None, thickness);
    }

    /// <summary>Adds a straight run up to the budget; false once the budget ran out inside it.</summary>
    private static bool Edge(ImDrawListPtr dl, Vector2 from, Vector2 to, ref float budget)
    {
        var length = Vector2.Distance(from, to);
        if (length <= 0f)
        {
            return true;
        }

        if (budget >= length)
        {
            dl.PathLineTo(to);
            budget -= length;
            return true;
        }

        dl.PathLineTo(Vector2.Lerp(from, to, budget / length));
        budget = 0f;
        return false;
    }

    /// <summary>Adds a quarter arc (or the part of it the budget allows); false once the budget ran out inside it.</summary>
    private static bool Corner(ImDrawListPtr dl, Vector2 centre, float radius, float startAngle, float arcLength, int segments, ref float budget)
    {
        if (radius <= 0f || arcLength <= 0f)
        {
            return true;
        }

        var quarter = MathF.PI * 0.5f;
        if (budget >= arcLength)
        {
            dl.PathArcTo(centre, radius, startAngle, startAngle + quarter, segments);
            budget -= arcLength;
            return true;
        }

        dl.PathArcTo(centre, radius, startAngle, startAngle + quarter * (budget / arcLength), segments);
        budget = 0f;
        return false;
    }

    private static string[] BuildCountdown()
    {
        var tenths = (int)MathF.Round(ConfirmGate.DefaultHoldSeconds * 10f);
        var labels = new string[tenths + 1];
        labels[0] = string.Empty;
        for (var i = 1; i <= tenths; i++)
        {
            labels[i] = string.Format(CultureInfo.InvariantCulture, Strings.VerdictHoldCountdownFormat, i / 10f) + IdSuffix;
        }

        return labels;
    }
}
