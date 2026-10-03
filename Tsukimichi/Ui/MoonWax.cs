using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Ui;

/// <summary>
/// The first Moon Road moment of feature plan v6 (U8a): when a quest the player can see is completed, its moon waxes
/// from the half moon to full over <see cref="Core.Ui.MotionTokens.Wax"/> before it settles on the Completed glyph. A
/// one-shot, never looping, off under Reduce motion (<see cref="Motion.Wax"/> is -1 then). This is the transition hook
/// only: the glyph itself stays <see cref="MoonGlyph"/>'s, and a renderer that draws its own waxing art reads
/// <see cref="Motion.Wax"/> the same way.
/// </summary>
public static class MoonWax
{
    /// <summary>
    /// Draws the state moon of quest <paramref name="rowId"/> as <see cref="MoonGlyph.Draw"/> does, or, while its wax
    /// plays, the filling moon at the wax's lit fraction. The high-contrast palette keeps its flat Completed glyph.
    /// </summary>
    public static void Draw(ImDrawListPtr dl, Vector2 center, float radius, QuestState state, uint rowId, byte job = 0)
    {
        var lit = state == QuestState.Completed && !Theme.Glyphs.HighContrast ? Motion.Wax(rowId) : -1f;
        if (lit < 0f)
        {
            MoonGlyph.Draw(dl, center, radius, state, job);
            return;
        }

        MoonGlyph.DrawFilling(dl, center, radius, lit);
    }

    /// <summary>
    /// The halo moment (feature plan v6 M1) at <paramref name="progress"/> (0..1 over <see cref="Core.Ui.MotionTokens.Halo"/>):
    /// one soft Moon ring that swells <see cref="Core.Ui.MotionTokens.HaloLogical"/> px past the moon's rim while it fades
    /// out, once, when the quest becomes Ready or is completed. Nothing is drawn outside 0..1. Never brighter than
    /// <see cref="Core.Ui.MotionTokens.MomentPeak"/>.
    /// </summary>
    public static void DrawHalo(ImDrawListPtr dl, Vector2 center, float radius, float progress)
    {
        var alpha = Core.Ui.MotionTokens.MomentAlpha(progress);
        if (alpha <= 0f)
        {
            return;
        }

        var grow = UiMetrics.Px(Core.Ui.MotionTokens.HaloLogical) * Core.Ui.MotionMath.EaseOutCubic(progress);
        var thickness = System.MathF.Max(1.5f, UiMetrics.Px(1.5f));
        dl.AddCircle(center, radius + grow, Theme.WithAlpha(Theme.Gold, alpha), 32, thickness);
    }
}
