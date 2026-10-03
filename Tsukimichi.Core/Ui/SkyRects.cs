using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>Where a sky rect is (docs/design/v7/ui/spec.md §3): the four places the Full sky shows.</summary>
public enum SkySite : byte
{
    /// <summary>The rail's gap between the last station and the foot.</summary>
    Rail,

    /// <summary>The Journal tree's empty sky under its last node.</summary>
    Tree,

    /// <summary>The table's title band, between the title and its right caption.</summary>
    Title,

    /// <summary>A Path band's right side, past its labels.</summary>
    Path,
}

/// <summary>One empty sky rect drawn this frame, in screen px: its site, a key within the site (a Path band's index) and its bounds.</summary>
public readonly record struct SkyRect(SkySite Site, int Key, Vector2 Min, Vector2 Max)
{
    public float Width => Max.X - Min.X;

    public float Height => Max.Y - Min.Y;

    /// <summary>Its area, 0 for an empty rect.</summary>
    public float Area => Width > 0f && Height > 0f ? Width * Height : 0f;
}

/// <summary>
/// The sky rects of a frame, and the choices made from them for the next (spec §3.4–§3.6): which rect hosts the
/// region constellation (the first that holds a clear box, the tree's sky before a Path band's), which the Milky Way
/// (the one continuous rect tall enough), and which a meteor (the largest). The rects are gathered as the panes draw,
/// so each choice is read one frame later, when the layout is the same. Fixed capacity; nothing allocates.
/// </summary>
public sealed class SkyRects
{
    /// <summary>The most rects a frame keeps (the rail, the tree, the title band and a path's bands).</summary>
    public const int Capacity = 32;

    private readonly SkyRect[] current = new SkyRect[Capacity];
    private readonly SkyRect[] previous = new SkyRect[Capacity];
    private int currentCount;
    private int previousCount;

    /// <summary>The rects gathered last frame.</summary>
    public ReadOnlySpan<SkyRect> Previous => previous.AsSpan(0, previousCount);

    /// <summary>Notes a sky rect drawn this frame; one past <see cref="Capacity"/> is dropped, as is an empty one.</summary>
    public void Add(SkySite site, int key, Vector2 min, Vector2 max)
    {
        if (currentCount >= Capacity || !(max.X > min.X) || !(max.Y > min.Y))
        {
            return;
        }

        current[currentCount++] = new SkyRect(site, key, min, max);
    }

    /// <summary>Ends a frame: what was gathered becomes <see cref="Previous"/>, and the next frame starts empty.</summary>
    public void Swap()
    {
        Array.Copy(current, previous, currentCount);
        previousCount = currentCount;
        currentCount = 0;
    }

    /// <summary>The index in <paramref name="rects"/> of the largest by area, or -1 for none.</summary>
    public static int Largest(ReadOnlySpan<SkyRect> rects)
    {
        var best = -1;
        var area = 0f;
        for (var i = 0; i < rects.Length; i++)
        {
            if (rects[i].Area > area)
            {
                area = rects[i].Area;
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// The index of the rect that hosts the constellation (spec §3.5): the first tree sky whose inset area holds
    /// <paramref name="box"/>, else the first Path band's that does, else -1. The rail and the title band never do.
    /// </summary>
    public static int ConstellationHost(ReadOnlySpan<SkyRect> rects, Vector2 box, float inset)
    {
        var path = -1;
        for (var i = 0; i < rects.Length; i++)
        {
            var r = rects[i];
            if (r.Width - (2f * inset) < box.X || r.Height - (2f * inset) < box.Y)
            {
                continue;
            }

            if (r.Site == SkySite.Tree)
            {
                return i;
            }

            if (r.Site == SkySite.Path && path < 0)
            {
                path = i;
            }
        }

        return path;
    }

    /// <summary>
    /// The index of the one rect the Milky Way may cross (spec §3.4): the tallest at least <paramref name="minHeight"/>
    /// px tall, or -1, so it is never split across rects.
    /// </summary>
    public static int BandHost(ReadOnlySpan<SkyRect> rects, float minHeight)
    {
        var best = -1;
        var height = 0f;
        for (var i = 0; i < rects.Length; i++)
        {
            if (rects[i].Height >= minHeight && rects[i].Height > height && rects[i].Width > 0f)
            {
                height = rects[i].Height;
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// Where the constellation's <paramref name="box"/> sits in a sky (spec §3.5): its top-left corner, against the
    /// sky's inset bottom-right, so it stays put while the sky's top moves (a tree node opening or closing).
    /// </summary>
    public static Vector2 ConstellationBox(Vector2 skyMin, Vector2 skyMax, Vector2 box, float inset) =>
        new(MathF.Max(skyMin.X + inset, skyMax.X - inset - box.X), MathF.Max(skyMin.Y + inset, skyMax.Y - inset - box.Y));

    /// <summary>
    /// A meteor's head at <paramref name="progress"/> (0..1) in a sky (spec §3.6): it starts 20 % in from the sky's top
    /// left and travels <paramref name="travel"/> px (64 × 34 logical, 28° below level), eased out cubic.
    /// </summary>
    public static Vector2 MeteorHead(Vector2 skyMin, Vector2 skyMax, Vector2 travel, float progress) =>
        skyMin + ((skyMax - skyMin) * 0.2f) + (travel * MotionMath.EaseOutCubic(Math.Clamp(progress, 0f, 1f)));
}
