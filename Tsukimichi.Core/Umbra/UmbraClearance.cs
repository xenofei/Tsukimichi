using System.Numerics;

namespace Tsukimichi.Core.Umbra;

/// <summary>Where a clearance came from, for Settings › About's Umbra line.</summary>
public enum ClearanceSource : byte
{
    /// <summary>Umbra is not running, or its bar holds no edge: nothing moves.</summary>
    None,

    /// <summary>Read from Umbra's saved settings.</summary>
    Read,

    /// <summary>Umbra runs but its settings can't be read: the assumed top bar (spec-1.22 M3).</summary>
    Assumed,
}

/// <summary>
/// How far Tsukimichi's floating surfaces keep from Umbra's toolbar (plan v8 M3; spec-1.22 M3 "Keeping clear"), in
/// screen pixels from the top and the bottom of the game's viewport. The moon icon, the Todo overlay and the Needs you
/// panel keep their top edges at the bar's height + <see cref="GapLogical"/> under a top bar, and their bottom edges at
/// the bar's top − <see cref="GapLogical"/> over a bottom one. A floating or auto-hidden bar holds no edge, so nothing
/// moves. Pure.
/// </summary>
/// <param name="Top">Pixels from the viewport's top that a surface's top edge keeps below (0: none).</param>
/// <param name="Bottom">Pixels from the viewport's bottom that a surface's bottom edge keeps above (0: none).</param>
/// <param name="Source">Whether the bar was read or assumed.</param>
/// <param name="TopAligned">The bar sits at the top (for the About line); meaningless with <see cref="ClearanceSource.None"/>.</param>
public readonly record struct UmbraClearance(float Top, float Bottom, ClearanceSource Source, bool TopAligned)
{
    /// <summary>The gap between the bar and a surface, in Tsukimichi's logical px (spec-1.22 M3: 8).</summary>
    public const float GapLogical = 8f;

    /// <summary>No clearance.</summary>
    public static readonly UmbraClearance None = new(0f, 0f, ClearanceSource.None, TopAligned: true);

    /// <summary>Whether anything keeps clear.</summary>
    public bool Any => Top > 0f || Bottom > 0f;

    /// <summary>
    /// The clearance for Umbra's bar: none while Umbra is not loaded; the read bar when it holds an edge (none when it
    /// does not); the assumed top bar of <paramref name="assumedHeight"/> logical px when Umbra runs but its settings
    /// could not be read.
    /// </summary>
    /// <param name="umbraLoaded">Umbra is installed and loaded.</param>
    /// <param name="toolbar">The bar as read from Umbra's settings; null when they could not be read.</param>
    /// <param name="assumedHeight">The bar height assumed when unread (Settings: 32 by default); 0 assumes no bar.</param>
    /// <param name="uiScale">Tsukimichi's own UI scale, for the gap.</param>
    public static UmbraClearance For(bool umbraLoaded, UmbraToolbar? toolbar, float assumedHeight, float uiScale)
    {
        if (!umbraLoaded)
        {
            return None;
        }

        var source = toolbar is null ? ClearanceSource.Assumed : ClearanceSource.Read;
        var bar = toolbar ?? UmbraToolbar.Assumed with { Height = (int)MathF.Round(Math.Clamp(assumedHeight, 0f, 512f)) };
        if (!bar.HoldsEdge)
        {
            return None with { Source = source, TopAligned = bar.TopAligned };
        }

        var gap = GapLogical * Math.Clamp(uiScale, 0.5f, 4f);
        var band = MathF.Round((bar.Height + Math.Max(0, bar.YOffset)) * bar.Scale) + gap;
        return bar.TopAligned
            ? new UmbraClearance(band, 0f, source, TopAligned: true)
            : new UmbraClearance(0f, band, source, TopAligned: false);
    }

    /// <summary>
    /// <paramref name="position"/> moved the least it must so a <paramref name="size"/> surface keeps clear inside a
    /// viewport from <paramref name="viewportTop"/> of <paramref name="viewportHeight"/>: down below a top bar, up above
    /// a bottom one. A surface taller than the room left keeps its top clear. The position itself when it already is.
    /// </summary>
    public Vector2 Apply(Vector2 position, Vector2 size, float viewportTop, float viewportHeight)
    {
        var y = position.Y;
        if (Bottom > 0f)
        {
            var maxTop = viewportTop + viewportHeight - Bottom - size.Y;
            y = MathF.Min(y, maxTop);
        }

        if (Top > 0f)
        {
            y = MathF.Max(y, viewportTop + Top);
        }

        return y == position.Y ? position : position with { Y = y };
    }
}

/// <summary>
/// One floating surface's place under the clearance (spec-1.22 M3 "The saved place: never rewritten"): the player's own
/// place is remembered when the clearance moves the surface, and given back when the clearance goes (Umbra off, its bar
/// floating or hidden), unless the player moved the surface in between, which makes the new place theirs. Pure.
/// </summary>
/// <param name="Original">The player's place before the clearance moved it; null while it has not.</param>
/// <param name="Moved">Where the clearance put it; null while it has not.</param>
public readonly record struct ClearedPlace(Vector2? Original, Vector2? Moved)
{
    /// <summary>Nothing moved.</summary>
    public static readonly ClearedPlace Untouched = default;

    /// <summary>
    /// The place to set this frame (null: leave it) and what to remember. Never moves a surface the player is dragging:
    /// a drag makes its place the player's, so the remembered one is forgotten.
    /// </summary>
    /// <param name="current">Where the surface is now.</param>
    /// <param name="size">Its size.</param>
    /// <param name="viewportTop">The viewport's top in screen px.</param>
    /// <param name="viewportHeight">The viewport's height.</param>
    /// <param name="clearance">The clearance in effect.</param>
    /// <param name="dragging">The player is moving the surface.</param>
    public (Vector2? SetTo, ClearedPlace Next) Step(Vector2 current, Vector2 size, float viewportTop, float viewportHeight, UmbraClearance clearance, bool dragging)
    {
        if (dragging)
        {
            return (null, Untouched);
        }

        // Moved by something other than the clearance (Reset position, a drag we did not see): the place is the player's.
        var state = Moved is { } moved && Vector2.DistanceSquared(moved, current) > 0.25f ? Untouched : this;
        var target = clearance.Apply(current, size, viewportTop, viewportHeight);
        if (target != current)
        {
            return (target, new ClearedPlace(state.Original ?? current, target));
        }

        if (state.Original is { } original && state.Moved is not null)
        {
            // The clearance left: back to the player's own place, unless that place is still inside the band.
            var back = clearance.Apply(original, size, viewportTop, viewportHeight);
            if (back != current)
            {
                return back == original ? (original, Untouched) : (back, new ClearedPlace(original, back));
            }

            return (null, back == original ? Untouched : state);
        }

        return (null, state);
    }
}
