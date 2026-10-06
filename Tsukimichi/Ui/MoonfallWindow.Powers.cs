using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Moonfall;

namespace Tsukimichi.Ui;

/// <summary>
/// Moonfall's powers and style shots in the window (plan v9 G4, G5, stage 2): the character for the level (by stage in
/// Adventure, the player's pick in Quick Play), the active powers with their shots left, the flippers' button, and the
/// placeholder drawing for each power and the style-shot popups. The drawing lives in its own methods here, so the art
/// track can replace it without touching the board.
/// </summary>
public sealed partial class MoonfallWindow
{
    private const int EffectCapacity = 16;
    private const int StylePopupCapacity = 4;
    private const double BurstSeconds = 0.35;
    private const double GateSeconds = 0.4;
    private const double BoltSeconds = 0.45;
    private const double StyleSeconds = 1.8;

    private bool boardHovered;

    private readonly Effect[] effects = new Effect[EffectCapacity];
    private int effectNext;
    private readonly StylePopup[] stylePopups = new StylePopup[StylePopupCapacity];
    private int stylePopupNext;
    private readonly (double X, double Y)[] superGuide = new (double X, double Y)[MoonfallRules.SuperGuideMaxPoints];
    private double boltShownAt = double.NegativeInfinity;

    private enum EffectKind : byte
    {
        None,
        Burst,
        Gate,
    }

    private readonly record struct Effect(EffectKind Kind, double At, double X, double Y);

    private readonly record struct StylePopup(double At, string Text);

    private void ClearPowerEffects()
    {
        Array.Clear(effects);
        Array.Clear(stylePopups);
        effectNext = 0;
        stylePopupNext = 0;
        boltShownAt = double.NegativeInfinity;
    }

    // ---- Input ----

    /// <summary>
    /// The flippers' button: the left mouse button held over the board, and no key. Dalamud hands every key on to the
    /// game as well (<see cref="Keyboard"/>), so a key here would also jump, cast or type into the chat; the mouse over
    /// the board is the window's own. A press that outlasts the ball never fires the next one (<see cref="MoonfallBoardPress"/>).
    /// </summary>
    private void FeedFlippers(MoonfallGame g)
    {
        var focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        var held = focused && boardHovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        g.SetFlippers(held && g.FlippersOut);
    }

    // ---- Events ----

    private void NotePowerEvent(in MoonfallEvent e)
    {
        switch (e.Kind)
        {
            case MoonfallEventKind.PowerTriggered when (MoonfallPower)e.Value == MoonfallPower.Burst:
                AddEffect(EffectKind.Burst, e.X, e.Y);
                break;

            case MoonfallEventKind.BallReentered:
                AddEffect(EffectKind.Gate, e.X, e.Y);
                break;

            case MoonfallEventKind.BoltStruck:
                boltShownAt = boardClock;
                break;

            case MoonfallEventKind.StyleShot:
                var kind = (MoonfallStyleShot)e.Count;
                if (game is not { } styled || !AddRibbon(styled, Strings.MoonfallStyleShotName(kind).ToUpper(CultureInfo.CurrentCulture),
                    string.Format(CultureInfo.CurrentCulture, Strings.MoonfallStyleValueFormat, e.Value.ToString("N0", CultureInfo.CurrentCulture))))
                {
                    AddStylePopup(string.Format(CultureInfo.CurrentCulture, Strings.MoonfallStyleShotFormat, Strings.MoonfallStyleShotName(kind), e.Value.ToString("N0", CultureInfo.CurrentCulture)));
                }

                break;

            case MoonfallEventKind.Drawn:
                var drawn = (MoonfallDrawOutcome)e.Value switch
                {
                    MoonfallDrawOutcome.FreeBall => Strings.MoonfallDrawFreeBall,
                    MoonfallDrawOutcome.TripleScore => Strings.MoonfallDrawTriple,
                    _ => string.Format(CultureInfo.CurrentCulture, Strings.MoonfallDrawPowerFormat, Strings.MoonfallPowerName((MoonfallPower)e.Count)),
                };
                if (game is not { } drew || !AddRibbon(drew, drawn.ToUpper(CultureInfo.CurrentCulture), string.Empty))
                {
                    AddStylePopup(drawn);
                }

                break;
        }
    }

    private void AddEffect(EffectKind kind, double x, double y)
    {
        effects[effectNext] = new Effect(kind, boardClock, x, y);
        effectNext = (effectNext + 1) % EffectCapacity;
    }

    private void AddStylePopup(string text)
    {
        stylePopups[stylePopupNext] = new StylePopup(boardClock, text);
        stylePopupNext = (stylePopupNext + 1) % StylePopupCapacity;
    }

    // ---- The board (placeholder shapes; the art track replaces them) ----

    /// <summary>Every power's mark on the board this frame, over the pegs and the launcher (<paramref name="art"/>: the art set drew the board).</summary>
    private void DrawPowers(ImDrawListPtr dl, in View view, MoonfallGame g, double alpha, bool art)
    {
        DrawSuperGuide(dl, view, g);
        if (!richHud)
        {
            DrawWings(dl, view, g, alpha);
        }
        DrawFlippers(dl, view, g, alpha);
        DrawBolt(dl, view, g);
        DrawPowerEffects(dl, view);
        DrawExtraBalls(dl, view, g, alpha, art);
    }

    /// <summary>Super Guide: a thin moonstone line on from where the guide's dots stop, through the bounce.</summary>
    private void DrawSuperGuide(ImDrawListPtr dl, in View view, MoonfallGame g)
    {
        if (g.Phase != MoonfallPhase.Aiming || pause.Paused || !g.GuideExtended || !PlayerAims)
        {
            return;
        }

        var n = g.GuideBeyond(aim, superGuide);
        var ink = Theme.WithAlpha(Theme.Scene.Moonlight, 0.75f);
        var thickness = MathF.Max(1f, view.Size(1.4));
        for (var k = 1; k < n; k++)
        {
            dl.AddLine(view.Map(superGuide[k - 1].X, superGuide[k - 1].Y), view.Map(superGuide[k].X, superGuide[k].Y), ink, thickness);
        }
    }

    /// <summary>Brass Wings: two brass vanes from the plain rims out to the wider rims.</summary>
    private static void DrawWings(ImDrawListPtr dl, in View view, MoonfallGame g, double alpha)
    {
        if (!g.WingsOpen || g.Fever)
        {
            return;
        }

        var x = g.BucketXAt(alpha);
        var r = MoonfallBucket.RimRadius;
        var outer = (g.BucketMouth * 0.5) + r;
        var brass = Theme.U32(Theme.Gold);
        var thickness = MathF.Max(1.5f, view.Size(3));
        foreach (var side in (ReadOnlySpan<double>)[-1.0, 1.0])
        {
            var tip = x + (side * outer);
            dl.AddLine(view.Map(x + (side * MoonfallBucket.RimOffset), MoonfallRules.BucketTop), view.Map(tip, MoonfallRules.BucketTop - 6), brass, thickness);
            dl.AddRectFilled(view.Map(tip - r, MoonfallRules.BucketTop - 6), view.Map(tip + r, MoonfallRules.Height + 20), Theme.U32(Theme.GoldDeep), view.Size(r));
        }
    }

    /// <summary>Flippers: a fan at each foot corner, from its pivot to its tip.</summary>
    private static void DrawFlippers(ImDrawListPtr dl, in View view, MoonfallGame g, double alpha)
    {
        if (!g.FlippersOut || g.Fever)
        {
            return;
        }

        var ink = Theme.U32(Theme.GoldDeep);
        var thickness = view.Size(MoonfallRules.FlipperThickness);
        foreach (var right in (ReadOnlySpan<bool>)[false, true])
        {
            var (px, py, tx, ty) = g.Flipper(right, alpha);
            var a = view.Map(px, py);
            var b = view.Map(tx, ty);
            dl.AddLine(a, b, ink, thickness);
            dl.AddCircleFilled(a, thickness * 0.5f, ink, 16);
            dl.AddCircleFilled(b, thickness * 0.5f, ink, 16);
        }
    }

    /// <summary>Storm Post: the bolt's path, peg to peg down to the bucket, fading out.</summary>
    private void DrawBolt(ImDrawListPtr dl, in View view, MoonfallGame g)
    {
        var age = boardClock - boltShownAt;
        var points = g.BoltPoints;
        if (age < 0 || age > BoltSeconds || points.Length < 2)
        {
            return;
        }

        var fade = (float)(1 - (age / BoltSeconds));
        var ink = Theme.WithAlpha(Theme.Scene.Moonlight, 0.9f * fade);
        var thickness = MathF.Max(1.5f, view.Size(2.5));
        for (var k = 1; k < points.Length; k++)
        {
            dl.AddLine(view.Map(points[k - 1].X, points[k - 1].Y), view.Map(points[k].X, points[k].Y), ink, thickness);
        }
    }

    /// <summary>Lunar Burst's ring out to its radius, and Moon Gate's ring where a ball comes back in (none under Reduce motion).</summary>
    private void DrawPowerEffects(ImDrawListPtr dl, in View view)
    {
        if (!Theme.UiMotion)
        {
            return;
        }

        for (var k = 0; k < EffectCapacity; k++)
        {
            var effect = effects[k];
            var span = effect.Kind == EffectKind.Burst ? BurstSeconds : GateSeconds;
            var age = boardClock - effect.At;
            if (effect.Kind == EffectKind.None || age < 0 || age > span)
            {
                continue;
            }

            var t = (float)(age / span);
            var radius = effect.Kind == EffectKind.Burst ? MoonfallRules.BurstRadius * t : 14 + (10 * t);
            dl.AddCircle(view.Map(effect.X, effect.Y), view.Size(radius), Theme.WithAlpha(Theme.Scene.Moonlight, 0.8f * (1 - t)), 40, MathF.Max(1f, view.Size(2.5)));
        }
    }

    /// <summary>
    /// The twin balls on the primitive board (the first is the launcher's; on the art board <see cref="ArtBall"/> draws
    /// every ball with the ball sprite), and Fireball's ember round every ball.
    /// </summary>
    private void DrawExtraBalls(ImDrawListPtr dl, in View view, MoonfallGame g, double alpha, bool art)
    {
        var burning = g.PowerActive(MoonfallPower.Fireball);
        for (var k = 0; k < g.BallsInPlay; k++)
        {
            var (x, y) = g.BallAt(k, alpha);
            if (k > 0 && !art)
            {
                DrawBall(dl, view, x, y);
            }

            if (burning)
            {
                dl.AddCircle(view.Map(x, y), view.Size(MoonfallRules.BallRadius + 2.5), Theme.U32(PegTone(PegColour.Orange)), 20, MathF.Max(1.5f, view.Size(2)));
            }
        }
    }

    /// <summary>The style shots and the drum's prize, stacked under the top of the board for 1.8 s each.</summary>
    private void DrawStylePopups(ImDrawListPtr dl, Vector2 origin, Vector2 size)
    {
        var line = ImGui.GetFontSize() * 1.25f;
        var row = 0;
        for (var n = 0; n < StylePopupCapacity; n++)
        {
            // Newest at the bottom: walk from the oldest slot.
            var popup = stylePopups[(stylePopupNext + n) % StylePopupCapacity];
            var age = boardClock - popup.At;
            if (popup.Text is null || age < 0 || age > StyleSeconds)
            {
                continue;
            }

            var fade = age < StyleSeconds - 0.3 ? 1f : (float)((StyleSeconds - age) / 0.3);
            var fontSize = ImGui.GetFontSize() * 1.15f;
            var width = ImGui.CalcTextSize(popup.Text).X * 1.15f;
            var at = origin + new Vector2((size.X - width) * 0.5f, (size.Y * 0.16f) + (row * line));
            dl.AddText(ImGui.GetFont(), fontSize, at, Theme.WithAlpha(Theme.GoldHigh, fade), popup.Text);
            row++;
        }
    }
}
