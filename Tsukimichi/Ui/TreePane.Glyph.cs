using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// The node glyph: the one place a Journal node's icon is drawn, for the tree's rows and for the strip. Today it is the
/// moon halo (the filling moon in its progress ring, T10). The orbit of the Moon Road design (design v4 §6.2-6.3: the
/// node's official icon inside the ring, a bead at the arc's head) plugs in here: the icon task resolves
/// <c>Node.IconId</c> once per catalog, and this method draws the icon when it is set and the halo otherwise, so no
/// caller changes.
/// </summary>
public sealed partial class TreePane
{
    /// <summary>
    /// Draws <paramref name="node"/>'s glyph centred on <paramref name="center"/>: the halo filled to
    /// <paramref name="fraction"/> (dimmed once complete) and, with <paramref name="readyDot"/>, the gold Ready dot on
    /// the ring's upper right, which stands for the Ready pill where the row has no room for it.
    /// </summary>
    private static void DrawNodeGlyph(ImDrawListPtr dl, Node node, Vector2 center, float radius, float fraction, bool readyDot)
    {
        // The seam for the orbit icon (node.IconId != 0): until the icon task lands every node draws the halo.
        MoonGlyph.DrawHalo(dl, center, radius, fraction, onCard: false, dimComplete: node.Complete);
        if (readyDot)
        {
            DrawReadyDot(dl, center, radius);
        }
    }

    /// <summary>The Ready dot: a Moon disc on the ring at 2 o'clock, ringed in Night so it reads over the arc.</summary>
    private static void DrawReadyDot(ImDrawListPtr dl, Vector2 center, float radius)
    {
        var dot = center + (new Vector2(0.7071f, -0.7071f) * radius);
        var r = MathF.Max(2.5f, UiMetrics.Px(3f));
        dl.AddCircleFilled(dot, r + MathF.Max(1f, UiMetrics.Px(1f)), Theme.NightU32);
        dl.AddCircleFilled(dot, r, Theme.MoonU32);
    }
}
