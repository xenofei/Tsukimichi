using System.Numerics;
using Dalamud.Interface.Utility;
using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Game;

/// <summary>
/// Where Umbra's toolbar is, for Tsukimichi's floating surfaces (plan v8 M3; spec-1.22 M3 "Keeping clear"): the moon
/// icon, the Todo overlay and the Needs you panel keep <see cref="Clearance"/> from the game viewport's top or bottom.
/// <see cref="UmbraProbe"/> keeps it current (read-only from Umbra's saved settings, or the assumed bar when they can't
/// be read); with Umbra not running it is <see cref="UmbraClearance.None"/> and nothing moves. Framework thread.
/// </summary>
public static class UmbraLayout
{
    /// <summary>The clearance in effect, in screen px; <see cref="UmbraClearance.None"/> without Umbra.</summary>
    public static UmbraClearance Clearance { get; internal set; } = UmbraClearance.None;

    /// <summary>Moves whenever <see cref="Clearance"/> changes.</summary>
    public static int Revision { get; internal set; }

    /// <summary>
    /// Umbra's toolbar band on screen, (min, max) in screen px: the top band under a top bar, the bottom band over a
    /// bottom one, the gap included; null when nothing keeps clear.
    /// </summary>
    public static (Vector2 Min, Vector2 Max)? Band
    {
        get
        {
            var c = Clearance;
            if (!c.Any)
            {
                return null;
            }

            var viewport = ImGuiHelpers.MainViewport;
            return c.Top > 0f
                ? (viewport.Pos, viewport.Pos + new Vector2(viewport.Size.X, c.Top))
                : (viewport.Pos + new Vector2(0f, viewport.Size.Y - c.Bottom), viewport.Pos + viewport.Size);
        }
    }

    /// <summary>
    /// <paramref name="position"/> kept clear of Umbra's bar for a surface of <paramref name="size"/> on the main
    /// viewport: the position itself when it already is, or when Umbra's bar holds no edge.
    /// </summary>
    public static Vector2 KeepClear(Vector2 position, Vector2 size)
    {
        var c = Clearance;
        if (!c.Any)
        {
            return position;
        }

        var viewport = ImGuiHelpers.MainViewport;
        return c.Apply(position, size, viewport.Pos.Y, viewport.Size.Y);
    }

    /// <summary>
    /// For a surface the player places (the moon icon, the Todo overlay): where to set it this frame, null to leave it,
    /// remembering the player's own place in <paramref name="place"/> so it comes back when the bar leaves
    /// (<see cref="ClearedPlace.Step"/>). Never moves a surface being dragged.
    /// </summary>
    public static Vector2? Place(ref ClearedPlace place, Vector2 current, Vector2 size, bool dragging)
    {
        var viewport = ImGuiHelpers.MainViewport;
        var (setTo, next) = place.Step(current, size, viewport.Pos.Y, viewport.Size.Y, Clearance, dragging);
        place = next;
        return setTo;
    }
}
