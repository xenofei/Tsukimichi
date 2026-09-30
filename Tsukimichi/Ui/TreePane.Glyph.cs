using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The node glyph: the one place a Journal node's icon is drawn, for the tree's rows and for the strip. Under Full and
/// Quiet flair it is the orbit of the Moon Road design (design v4 §6.2-6.3, <see cref="Orbit"/>): the node's official
/// icon, or its gap glyph from the ornament atlas, inside a ring whose gold arc is the completion, with a moon bead at
/// the arc's head. The moon halo of 1.1 (the filling moon in its progress ring, T10) stays under Plain flair, for a node
/// without an icon, and while the icon's texture is still loading, so a row never shows an empty square.
/// </summary>
public sealed partial class TreePane
{
    /// <summary>
    /// Draws <paramref name="node"/>'s glyph centred on <paramref name="center"/>: the orbit (or the halo) filled to
    /// <paramref name="fraction"/> (dimmed once complete) and, with <paramref name="readyDot"/>, the gold Ready mark on the
    /// ring's upper right, which stands for the Ready pill where the row has no room for it. Under the high-contrast
    /// palette the orbit's lines are opaque, its arc 3 px on a Night gap, and official icons are keylined (§10.2).
    /// </summary>
    private void DrawNodeGlyph(ImDrawListPtr dl, Node node, Vector2 center, float radius, float fraction, bool readyDot)
    {
        if (Theme.Flair != Flair.Plain && !node.Icon.IsEmpty)
        {
            var box = 2f * radius;
            var min = new Vector2(MathF.Round(center.X - radius), MathF.Round(center.Y - radius));
            if (Orbit.TryDraw(dl, textures, min, box, node.Icon, fraction, readyDot, highContrast: Theme.Glyphs.HighContrast))
            {
                return;
            }
        }

        MoonGlyph.DrawHalo(dl, center, radius, fraction, onCard: false, dimComplete: node.Complete);
        if (readyDot)
        {
            DrawReadyDot(dl, center, radius);
        }
    }

    /// <summary>The Ready dot on the halo: a Moon disc on the ring at its upper right, ringed in Night so it reads over the arc.</summary>
    private static void DrawReadyDot(ImDrawListPtr dl, Vector2 center, float radius)
    {
        var dot = center + (new Vector2(0.7071f, -0.7071f) * radius);
        var r = MathF.Max(2.5f, UiMetrics.Px(3f));
        dl.AddCircleFilled(dot, r + MathF.Max(1f, UiMetrics.Px(1f)), Theme.NightU32);
        dl.AddCircleFilled(dot, r, Theme.MoonU32);
    }
}
