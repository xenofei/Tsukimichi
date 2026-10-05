using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>The board art's small geometry (feature plan v9 G8), pure so it is tested without the game.</summary>
public static class MoonfallArtMath
{
    /// <summary>
    /// The most columns <see cref="BrickColumns"/> gives a brick: two vertices each, 8,192 at most, so a brick's mesh is
    /// always one <c>PrimReserve</c> under the 65,536 vertices 16-bit indices reach (ImGui starts a new vertex offset
    /// for a reserve that would cross it, but only for one under that size).
    /// </summary>
    public const int MaxBrickColumns = 4096;

    /// <summary>
    /// The free-ball gauge's fill for a shot's <paramref name="score"/>: rising linearly to each threshold's notch
    /// (<see cref="Notch"/>; 25k, 75k and 125k at 0.2, 0.6 and 1 of the arc), full past the last.
    /// </summary>
    public static float GaugeShare(long score, ReadOnlySpan<int> thresholds)
    {
        if (score <= 0)
        {
            return 0f;
        }

        var below = 0.0;
        var shareBelow = 0f;
        for (var k = 0; k < thresholds.Length; k++)
        {
            var share = Notch(thresholds[k], thresholds);
            if (score < thresholds[k])
            {
                return shareBelow + (float)((score - below) / (thresholds[k] - below) * (share - shareBelow));
            }

            below = thresholds[k];
            shareBelow = share;
        }

        return 1f;
    }

    /// <summary>
    /// A threshold's notch on the gauge's arc: the first at 0.2 and the rest spread evenly to 1 (0.2, 0.6, 1 for three);
    /// a value between thresholds takes the next one's notch, one past the last takes 1.
    /// </summary>
    public static float Notch(long threshold, ReadOnlySpan<int> thresholds)
    {
        for (var k = 0; k < thresholds.Length; k++)
        {
            if (thresholds[k] >= threshold)
            {
                return thresholds.Length == 1 ? 1f : 0.2f + (0.8f * k / (thresholds.Length - 1));
            }
        }

        return 1f;
    }

    /// <summary>
    /// Where a ray from <paramref name="from"/> along <paramref name="dir"/> lies inside the box: its entry and exit
    /// distances (the entry 0 when it starts inside), or (0, 0) when it misses. The gauge's fill is cut as a fan of
    /// these spans, so it never samples outside its atlas region.
    /// </summary>
    public static (float Near, float Far) RayInBox(Vector2 from, Vector2 dir, Vector2 min, Vector2 max)
    {
        var near = 0f;
        var far = float.MaxValue;
        for (var axis = 0; axis < 2; axis++)
        {
            var o = axis == 0 ? from.X : from.Y;
            var d = axis == 0 ? dir.X : dir.Y;
            var lo = axis == 0 ? min.X : min.Y;
            var hi = axis == 0 ? max.X : max.Y;
            if (MathF.Abs(d) < 1e-6f)
            {
                if (o < lo || o > hi)
                {
                    return (0f, 0f);
                }

                continue;
            }

            var t0 = (lo - o) / d;
            var t1 = (hi - o) / d;
            near = MathF.Max(near, MathF.Min(t0, t1));
            far = MathF.Min(far, MathF.Max(t0, t1));
        }

        return far > near ? (near, far) : (0f, 0f);
    }

    /// <summary>
    /// The columns of a brick laid from its sprite (see <c>MoonfallWindow.ArtBrick</c>): (distance along the brick,
    /// sprite u in units). The sprite's first <c>H / 2</c> units are the start cap and its last the end cap; its middle
    /// repeats, mirrored every other time so the stone runs on without a seam, as many whole times as fit the brick's
    /// length best; the end cap is the right one after an odd count and the start cap mirrored after an even one. A
    /// brick shorter than its two caps is the start cap there and back. <paramref name="step"/> splits each repeat into
    /// pieces no longer than it (an arc's curve). However thin the sprite's middle or fine the step, a brick has at most
    /// <see cref="MaxBrickColumns"/> columns, so its mesh is one <c>PrimReserve</c> well inside 16-bit indices.
    /// </summary>
    public static void BrickColumns(float total, float spriteW, float spriteH, float thickness, float step, List<(float S, float U)> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        columns.Clear();
        if (!(total > 0) || !float.IsFinite(total) || !(spriteH > 0) || !(thickness > 0))
        {
            return;
        }

        var k = thickness / spriteH;
        var cap = spriteH * 0.5f;
        var capLength = cap * k;
        var middle = spriteW - (2 * cap);
        columns.Add((0f, 0f));
        if (total <= 2 * capLength || middle <= 0)
        {
            columns.Add((total * 0.5f, cap));
            columns.Add((total, 0f));
            return;
        }

        columns.Add((capLength, cap));
        var run = total - (2 * capLength);
        // The repeats and their pieces between the caps' three columns, capped (the conversions saturate on overflow).
        const int Between = MaxBrickColumns - 3;
        var repeats = Math.Clamp((int)MathF.Round(run / (middle * k)), 1, Between);
        var each = run / repeats;
        var pieces = Math.Clamp((int)MathF.Ceiling(each / Math.Max(step, 0.5f)), 1, Between / repeats);
        for (var j = 0; j < repeats; j++)
        {
            var forward = j % 2 == 0;
            for (var q = 1; q <= pieces; q++)
            {
                var f = q / (float)pieces;
                columns.Add((capLength + (each * (j + f)), cap + (middle * (forward ? f : 1 - f))));
            }
        }

        columns.Add((total, repeats % 2 == 1 ? spriteW : 0f));
    }
}
