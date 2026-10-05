using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Ui;

/// <summary>Moonfall's board: the placeholder shapes, the camera, the popups and the tally (see <see cref="MoonfallWindow"/>).</summary>
public sealed partial class MoonfallWindow
{
    private const int PopupCapacity = 64;
    private const int RingCapacity = 32;
    private const double FreeBallSeconds = 1.2;
    private const double RingSeconds = 0.22;

    private static readonly Vector2 BoardCentre = new((float)(MoonfallRules.Width * 0.5), (float)(MoonfallRules.Height * 0.5));

    /// <summary>Popup texts by value: the few values pegs score (10 to 5,000) are formatted once per culture.</summary>
    private readonly Dictionary<long, string> valueTexts = [];

    private readonly Popup[] popups = new Popup[PopupCapacity];
    private readonly Ring[] rings = new Ring[RingCapacity];
    private readonly (double X, double Y)[] guide = new (double X, double Y)[MoonfallRules.GuideMaxDots];
    private int popupNext;
    private int ringNext;
    private double boardClock;
    private double freeBallUntil = double.NegativeInfinity;
    private MoonfallBoardPress boardPress;

    private string shotText = string.Empty;
    private (long Value, int Count) shotFor = (-1, -1);

    private string[] feverTexts = [];
    private string perfectText = string.Empty;
    private CultureInfo? textsCulture;

    /// <summary>The pegs' inks for the palette in effect (<see cref="PegInks.For"/>), worked out once per palette.</summary>
    private PegInks inks;
    private uint litOutline;
    private UiPalette? inksFor;

    private readonly record struct Popup(double At, double X, double Y, double Below, string Text);

    private readonly record struct Ring(double At, double X, double Y);

    /// <summary>The board's place on screen this frame and its camera.</summary>
    private readonly record struct View(Vector2 Origin, float Scale, float Zoom, Vector2 Focus)
    {
        public Vector2 Map(double x, double y) =>
            Origin + ((BoardCentre + ((new Vector2((float)x, (float)y) - Focus) * Zoom)) * Scale);

        public float Size(double length) => (float)length * Scale * Zoom;

        public (double X, double Y) Unmap(Vector2 screen)
        {
            var board = Focus + ((((screen - Origin) / Scale) - BoardCentre) / Zoom);
            return (board.X, board.Y);
        }
    }

    private void ClearEffects()
    {
        Array.Clear(popups);
        Array.Clear(rings);
        popupNext = 0;
        ringNext = 0;
        freeBallUntil = double.NegativeInfinity;
    }

    /// <summary>Drops the formatted numbers when the culture changes (a language switch), so they are made again in it.</summary>
    private void RefreshTexts()
    {
        var culture = CultureInfo.CurrentCulture;
        if (ReferenceEquals(culture, textsCulture))
        {
            return;
        }

        textsCulture = culture;
        valueTexts.Clear();
        var values = MoonfallRules.FeverBucketValues;
        feverTexts = new string[values.Length];
        for (var k = 0; k < values.Length; k++)
        {
            feverTexts[k] = values[k].ToString("N0", culture);
        }

        perfectText = MoonfallRules.PerfectFeverBucketValue.ToString("N0", culture);
    }

    private void RefreshInks()
    {
        var palette = Theme.Palette;
        if (!ReferenceEquals(palette, inksFor))
        {
            inksFor = palette;
            inks = PegInks.For(palette);
            litOutline = Theme.U32(PegInks.LitOutline(palette));
        }
    }

    private void AddPopup(int peg, long value, double x, double y)
    {
        RefreshTexts();
        if (!valueTexts.TryGetValue(value, out var text))
        {
            text = value.ToString("N0", textsCulture);
            valueTexts[value] = text;
        }

        var below = game is { } g && peg >= 0 && peg < g.PegCount ? Below(g.Peg(peg)) : MoonfallRules.PegRadius;
        popups[popupNext] = new Popup(boardClock, x, y, below, text);
        popupNext = (popupNext + 1) % PopupCapacity;
    }

    /// <summary>How far below its centre a peg's popup sits: under its body ([M 9]: "just below the peg").</summary>
    private static double Below(in MoonfallPegView peg) => peg.Shape == PegShape.Round ? peg.Radius + 2 : (peg.Thickness * 0.5) + 2;

    private void AddRing(double x, double y)
    {
        rings[ringNext] = new Ring(boardClock, x, y);
        ringNext = (ringNext + 1) % RingCapacity;
    }

    // ---- The board ----

    private void DrawBoard(MoonfallGame g)
    {
        var avail = ImGui.GetContentRegionAvail();
        var start = ImGui.GetCursorScreenPos();
        if (avail.X < 8f || avail.Y < 8f)
        {
            return;
        }

        var scale = MathF.Min(avail.X / (float)MoonfallRules.Width, avail.Y / (float)MoonfallRules.Height);
        var size = new Vector2((float)MoonfallRules.Width, (float)MoonfallRules.Height) * scale;
        var origin = start + ((avail - size) * 0.5f);
        origin = new Vector2(MathF.Round(origin.X), MathF.Round(origin.Y));

        // The camera: Full Moon's zoom (never under Reduce motion), centred on the ball and the last orange.
        var zoom = UiMetrics.ReduceMotion ? 1f : (float)g.Zoom;
        var pull = Math.Clamp(zoom - 1f, 0f, 1f);
        var focus = BoardCentre + ((new Vector2((float)g.FocusX, (float)g.FocusY) - BoardCentre) * pull);
        var view = new View(origin, scale, zoom, focus);

        // The board takes the clicks, except while the tally's buttons are over it.
        var over = g.Phase is MoonfallPhase.Won or MoonfallPhase.Lost;
        SoundSettingInput(origin, size, !over);
        ImGui.SetCursorScreenPos(origin);
        var clicked = false;
        var hovered = false;
        if (over)
        {
            ImGui.Dummy(size);
        }
        else
        {
            clicked = ImGui.InvisibleButton("##moonfallBoard", size);
            hovered = ImGui.IsItemHovered();

            // A click shoots only if its press began while aiming (MoonfallBoardPress): a hold for the flippers that
            // outlasts the ball does not fire the next one.
            if (ImGui.IsItemActivated())
            {
                boardPress.Pressed(g.Phase, pause.Paused);
            }
        }

        boardHovered = hovered;

        if (hovered && !pause.Paused && g.Phase == MoonfallPhase.Aiming)
        {
            var (x, y) = new View(origin, scale, 1f, BoardCentre).Unmap(ImGui.GetMousePos());
            aim = MoonfallGame.AimAt(x, y);
        }

        if (clicked)
        {
            var shoot = boardPress.Released(g.Phase, pause.Paused);
            if (pause.Paused)
            {
                pause.TryResume();
            }
            else if (shoot)
            {
                if (g.Shoot(aim))
                {
                    SoundShot();
                }
            }
        }

        RefreshTexts();
        RefreshInks();
        var alpha = g.Alpha;
        var dl = ImGui.GetWindowDrawList();
        // The window's margins carry the level's scene, blurred (rich chrome only), and a wide margin takes the power's card.
        cardInMargin = MoonfallHud.CardInMargin(origin.X - start.X);
        DrawMargins(dl, start, start + avail, origin, size);
        dl.PushClipRect(origin, origin + size, true);
        // The art set when it is in (MoonfallWindow.Art.cs); these primitives while it loads, or if it is missing or broken.
        var drewArt = DrawBoardArt(dl, view, g, alpha, origin, size);
        if (!drewArt)
        {
            DrawGround(dl, view, origin, size);
            DrawBucket(dl, view, g, alpha);
            DrawPegs(dl, view, g, alpha);
            DrawLauncher(dl, view, g, alpha);
            DrawRings(dl, view);
        }

        // Power effects (MoonfallWindow.Powers.cs) draw over the board in both the art and the primitive paths.
        DrawPowers(dl, view, g, alpha, drewArt);

        DrawPopups(dl, view);
        if (!richHud)
        {
            DrawStylePopups(dl, origin, size);
        }

        DrawShotTally(dl, origin, scale, g);
        if (!richHud)
        {
            DrawBanner(dl, origin, size, g);
        }

        if (pause.Paused)
        {
            DrawPaused(dl, origin, size);
            DrawSoundSetting(dl);
        }

        dl.PopClipRect();
        var richArt = richHud && gameArt?.Chrome is not null && gameArt.ChromeTexture is not null && art?.Atlas is not null && art.Sheet(scale > ArtTwoXAbove, out _) is not null;
        if (richArt && cardInMargin)
        {
            PowerCard(dl, start, origin, gameArt!.Chrome!, gameArt.ChromeTexture!.Handle, new ArtPen(dl, view, art!.Atlas!, art.Sheet(scale > ArtTwoXAbove, out _)!.Handle));
        }

        if (over)
        {
            if (richArt)
            {
                RichEnd(dl, origin, size, scale, g, gameArt!.Chrome!, gameArt.ChromeTexture!.Handle,
                    new ArtPen(dl, view, art!.Atlas!, art.Sheet(scale > ArtTwoXAbove, out _)!.Handle));
            }
            else
            {
                DrawEnd(dl, origin, size, g);
            }
        }
    }

    private static void DrawGround(ImDrawListPtr dl, in View view, Vector2 origin, Vector2 size)
    {
        // Outside the walls, the deepest tone; the play area the window's; the walls a line.
        dl.AddRectFilled(origin, origin + size, Theme.U32(Theme.Surface.Deep));
        var top = view.Map(MoonfallRules.LeftWall, -400);
        var bottom = view.Map(MoonfallRules.RightWall, MoonfallRules.Height + 400);
        dl.AddRectFilled(top, bottom, Theme.U32(Theme.Surface.Window));
        var wall = Theme.U32(Theme.Surface.StrongLine);
        var thickness = MathF.Max(1f, view.Size(2));
        dl.AddLine(view.Map(MoonfallRules.LeftWall, -400), view.Map(MoonfallRules.LeftWall, MoonfallRules.Height + 400), wall, thickness);
        dl.AddLine(view.Map(MoonfallRules.RightWall, -400), view.Map(MoonfallRules.RightWall, MoonfallRules.Height + 400), wall, thickness);
    }

    private void DrawBucket(ImDrawListPtr dl, in View view, MoonfallGame g, double alpha)
    {
        var rim = Theme.U32(Theme.GoldDeep);
        var mouth = Theme.U32(Theme.Surface.Sunken);
        var floor = MoonfallRules.Height + 20;
        if (!g.Fever)
        {
            var x = g.BucketXAt(alpha);
            var half = MoonfallRules.BucketMouth * 0.5;
            dl.AddRectFilled(view.Map(x - half, MoonfallRules.BucketTop), view.Map(x + half, floor), mouth);
            var r = MoonfallBucket.RimRadius;
            foreach (var side in (ReadOnlySpan<double>)[-1.0, 1.0])
            {
                var cx = x + (side * MoonfallBucket.RimOffset);
                dl.AddRectFilled(view.Map(cx - r, MoonfallRules.BucketTop), view.Map(cx + r, floor), rim, view.Size(r));
            }

            return;
        }

        // Full Moon: the five buckets rise across the floor with their bonuses.
        var shown = (float)g.FeverBucketsShown;
        var values = MoonfallRules.FeverBucketValues;
        var lift = (1 - shown) * 30;
        for (var k = 0; k < values.Length; k++)
        {
            var left = MoonfallRules.LeftWall + (k * MoonfallBucket.FeverBucketWidth);
            var min = view.Map(left + 2, MoonfallRules.BucketTop + lift);
            var max = view.Map(left + MoonfallBucket.FeverBucketWidth - 2, floor);
            var lit = g.FeverBucket == k;
            dl.AddRectFilled(min, max, Theme.WithAlpha(lit ? Theme.Gold : Theme.Surface.CoolDeep, shown * (lit ? 0.85f : 0.7f)), view.Size(4));
            var text = g.Perfect ? perfectText : feverTexts[k];
            var textSize = ImGui.CalcTextSize(text);
            var centre = view.Map(left + (MoonfallBucket.FeverBucketWidth * 0.5), MoonfallRules.BucketTop + lift + 11);
            dl.AddText(centre - (textSize * 0.5f), Theme.WithAlpha(lit ? Theme.OnGold : Theme.Surface.Text, shown), text);
        }
    }

    /// <summary>A peg's colour from the palette's peg role (placeholders until the art track's pegs).</summary>
    private Vector4 PegTone(PegColour colour) => colour switch
    {
        PegColour.Orange => inks.Orange,
        PegColour.Green => inks.Green,
        PegColour.Purple => inks.Purple,
        _ => inks.Blue,
    };

    private uint PegInk(PegColour colour)
    {
        RefreshInks();
        return Theme.U32(PegTone(colour));
    }

    private void DrawPegs(ImDrawListPtr dl, in View view, MoonfallGame g, double alpha)
    {
        var glow = Theme.ShowGlow;
        var edge = Theme.U32(Theme.Surface.Deep);
        var outline = litOutline;
        var surface = Theme.Surface;
        for (var i = 0; i < g.PegCount; i++)
        {
            var peg = g.Peg(i, alpha);
            if (peg.Cleared)
            {
                continue;
            }

            // A lit peg moves towards the text colour (brighter on a dark board, deeper on a light one) and takes a gold
            // outline, so it reads as lit on every palette; it waits to clear at the end of the turn.
            var tone = PegTone(peg.Colour);
            var fill = peg.Lit ? Theme.U32(PegInks.Lit(tone, surface)) : Theme.U32(tone);
            var rim = MathF.Max(1.5f, view.Size(1.6));
            switch (peg.Shape)
            {
                case PegShape.Line:
                {
                    var a = view.Map(peg.X, peg.Y);
                    var b = view.Map(peg.X2, peg.Y2);
                    var thick = view.Size(peg.Thickness);
                    if (peg.Lit && glow)
                    {
                        dl.AddLine(a, b, Theme.WithAlpha(Theme.Scene.Moonlight, 0.18f), thick * 1.6f);
                    }

                    if (peg.Lit)
                    {
                        dl.AddLine(a, b, outline, thick + (2 * rim));
                        dl.AddCircleFilled(a, (thick * 0.5f) + rim, outline, 16);
                        dl.AddCircleFilled(b, (thick * 0.5f) + rim, outline, 16);
                    }

                    dl.AddLine(a, b, fill, thick);
                    dl.AddCircleFilled(a, thick * 0.5f, fill, 16);
                    dl.AddCircleFilled(b, thick * 0.5f, fill, 16);
                    break;
                }

                case PegShape.Arc:
                {
                    var centre = view.Map(peg.X, peg.Y);
                    var radius = view.Size(peg.Radius);
                    var thick = view.Size(peg.Thickness);
                    var segments = Math.Max(6, (int)(peg.SweepRadians * 12));
                    if (peg.Lit && glow)
                    {
                        dl.PathArcTo(centre, radius, (float)peg.StartRadians, (float)(peg.StartRadians + peg.SweepRadians), segments);
                        dl.PathStroke(Theme.WithAlpha(Theme.Scene.Moonlight, 0.18f), ImDrawFlags.None, thick * 1.6f);
                    }

                    if (peg.Lit)
                    {
                        dl.PathArcTo(centre, radius, (float)peg.StartRadians, (float)(peg.StartRadians + peg.SweepRadians), segments);
                        dl.PathStroke(outline, ImDrawFlags.None, thick + (2 * rim));
                    }

                    dl.PathArcTo(centre, radius, (float)peg.StartRadians, (float)(peg.StartRadians + peg.SweepRadians), segments);
                    dl.PathStroke(fill, ImDrawFlags.None, thick);
                    foreach (var end in (ReadOnlySpan<double>)[peg.StartRadians, peg.StartRadians + peg.SweepRadians])
                    {
                        var cap = view.Map(peg.X + (peg.Radius * Math.Cos(end)), peg.Y + (peg.Radius * Math.Sin(end)));
                        if (peg.Lit)
                        {
                            dl.AddCircleFilled(cap, (thick * 0.5f) + rim, outline, 16);
                        }

                        dl.AddCircleFilled(cap, thick * 0.5f, fill, 16);
                    }

                    break;
                }

                default:
                {
                    var centre = view.Map(peg.X, peg.Y);
                    var radius = view.Size(peg.Radius);
                    if (peg.Lit && glow)
                    {
                        dl.AddCircleFilled(centre, radius * 1.7f, Theme.WithAlpha(Theme.Scene.Moonlight, 0.16f), 24);
                    }

                    dl.AddCircleFilled(centre, radius, fill, 24);
                    dl.AddCircle(centre, radius, peg.Lit ? outline : edge, 24, peg.Lit ? rim : MathF.Max(1f, view.Size(1.2)));
                    break;
                }
            }
        }
    }

    private void DrawLauncher(ImDrawListPtr dl, in View view, MoonfallGame g, double alpha)
    {
        var pivot = view.Map(MoonfallRules.LauncherX, MoonfallRules.LauncherY);
        var (dx, dy) = MoonfallGame.Direction(aim);
        var aiming = g.Phase == MoonfallPhase.Aiming && g.BallsLeft > 0;

        // The guide first, under the barrel: dots along the path to the first peg it would touch.
        if (aiming && !pause.Paused)
        {
            var n = g.Guide(aim, guide);
            var dot = MathF.Max(1.5f, view.Size(2.2));
            var ink = Theme.WithAlpha(Theme.Surface.Text, 0.8f);
            for (var k = 1; k < n; k++)
            {
                dl.AddCircleFilled(view.Map(guide[k].X, guide[k].Y), dot, ink, 10);
            }
        }

        dl.AddLine(pivot, view.Map(MoonfallRules.LauncherX + (dx * 40), MoonfallRules.LauncherY + (dy * 40)), Theme.U32(Theme.Surface.TextSecondary), view.Size(10));
        dl.AddCircleFilled(pivot, view.Size(16), Theme.U32(Theme.Surface.Raised), 32);
        dl.AddCircle(pivot, view.Size(16), Theme.U32(Theme.Gold), 32, MathF.Max(1f, view.Size(2)));

        if (aiming)
        {
            DrawBall(dl, view, MoonfallRules.LauncherX + (dx * MoonfallRules.BarrelLength), MoonfallRules.LauncherY + (dy * MoonfallRules.BarrelLength));
        }
        else if (g.BallInPlay)
        {
            var (x, y) = g.BallAt(alpha);
            DrawBall(dl, view, x, y);
        }

        if (boardClock < freeBallUntil)
        {
            var fade = (float)Math.Clamp((freeBallUntil - boardClock) / 0.3, 0, 1);
            var text = Strings.MoonfallFreeBall;
            var at = view.Map(MoonfallRules.LauncherX + 30, MoonfallRules.LauncherY - 30);
            dl.AddText(at, Theme.WithAlpha(Theme.GoldHigh, fade), text);
        }
    }

    private static void DrawBall(ImDrawListPtr dl, in View view, double x, double y)
    {
        var centre = view.Map(x, y);
        var radius = view.Size(MoonfallRules.BallRadius);
        dl.AddCircleFilled(centre, radius, Theme.U32(Theme.Surface.Text), 20);
        dl.AddCircle(centre, radius, Theme.U32(Theme.GoldDeep), 20, MathF.Max(1f, view.Size(1.2)));
    }

    private void DrawRings(ImDrawListPtr dl, in View view)
    {
        if (!Theme.UiMotion)
        {
            return;
        }

        for (var k = 0; k < RingCapacity; k++)
        {
            var ring = rings[k];
            var age = boardClock - ring.At;
            if (ring.At <= 0 || age < 0 || age > RingSeconds)
            {
                continue;
            }

            var t = (float)(age / RingSeconds);
            dl.AddCircle(view.Map(ring.X, ring.Y), view.Size(10 + (10 * t)), Theme.WithAlpha(Theme.Scene.Moonlight, 0.7f * (1 - t)), 24, MathF.Max(1f, view.Size(2)));
        }
    }

    private void DrawPopups(ImDrawListPtr dl, in View view)
    {
        var gone = MoonfallRules.PopupSolidSeconds + MoonfallRules.PopupFadeSeconds;
        for (var k = 0; k < PopupCapacity; k++)
        {
            var popup = popups[k];
            var age = boardClock - popup.At;
            if (popup.Text is null || age < 0 || age > gone)
            {
                continue;
            }

            // [M 9]: on the hit, just below the peg, still; solid 0.40 s, then a 0.08 s fade.
            var alpha = age <= MoonfallRules.PopupSolidSeconds ? 1f : (float)(1 - ((age - MoonfallRules.PopupSolidSeconds) / MoonfallRules.PopupFadeSeconds));
            var size = ImGui.CalcTextSize(popup.Text);
            var at = view.Map(popup.X, popup.Y + popup.Below);
            dl.AddText(new Vector2(at.X - (size.X * 0.5f), at.Y), Theme.WithAlpha(Theme.Surface.Text, alpha), popup.Text);
        }
    }

    /// <summary>"900 × 18 pegs" over the floor while the turn's pegs clear, the count rising with each one.</summary>
    private void DrawShotTally(ImDrawListPtr dl, Vector2 origin, float scale, MoonfallGame g)
    {
        if (g.Phase != MoonfallPhase.Clearing || g.ShotPegs == 0)
        {
            return;
        }

        var key = (g.ShotValue, g.ClearedThisTurn);
        if (key != shotFor)
        {
            shotFor = key;
            shotText = string.Format(CultureInfo.CurrentCulture, Strings.MoonfallShotFormat, g.ShotValue.ToString("N0", CultureInfo.CurrentCulture), g.ClearedThisTurn);
        }

        if (richHud)
        {
            // In the game's sans (it carries the "×"), gilt on the game's dark edge, held still over the floor.
            var flat = new View(origin, scale, 1f, BoardCentre);
            DrawText(dl, MoonfallFace.Axis, NumberPx(flat, 18f, MoonfallFace.Axis), flat.Map(400, 530), Anchor.Centre, Ink(GoldHiInk), shotText, Ink(EdgeInk), flat.Size(0.8));
            return;
        }

        var size = ImGui.GetFontSize() * 1.2f;
        var width = ImGui.CalcTextSize(shotText).X * 1.2f;
        var at = origin + (new Vector2(400f, 530f) * scale) - new Vector2(width * 0.5f, size * 0.5f);
        dl.AddText(ImGui.GetFont(), size, at, Theme.U32(Theme.GoldHigh), shotText);
    }

    /// <summary>"Full Moon" over the board for its 2.95 s, after the last orange.</summary>
    private static void DrawBanner(ImDrawListPtr dl, Vector2 origin, Vector2 size, MoonfallGame g)
    {
        if (!g.BannerVisible)
        {
            return;
        }

        var text = g.Perfect ? Strings.MoonfallPerfectMoon : Strings.MoonfallFullMoon;
        var fontSize = MathF.Max(ImGui.GetFontSize() * 2.2f, size.Y * 0.085f);
        var width = ImGui.CalcTextSize(text).X * (fontSize / ImGui.GetFontSize());
        var at = origin + new Vector2((size.X - width) * 0.5f, (size.Y * 0.32f) - (fontSize * 0.5f));
        if (Theme.ShowGlow)
        {
            var mid = at + new Vector2(width * 0.5f, fontSize * 0.5f);
            dl.AddCircleFilled(mid, width * 0.62f, Theme.WithAlpha(Theme.Gold, 0.10f), 48);
        }

        dl.AddText(ImGui.GetFont(), fontSize, at, Theme.U32(Theme.GoldHigh), text);
    }

    private void DrawPaused(ImDrawListPtr dl, Vector2 origin, Vector2 size)
    {
        dl.AddRectFilled(origin, origin + size, Theme.WithAlpha(Theme.Scene.Scrim, 0.75f));
        var reason = PauseReasonText();
        var hint = pause.Held ? Strings.MoonfallWaitsForIt
            : pause.Shown == MoonfallPauseReason.Reopened && game is { Phase: MoonfallPhase.Aiming, Score: 0 } ? Strings.MoonfallClickToPlay
            : Strings.MoonfallClickToResume;
        var big = ImGui.GetFontSize() * 1.5f;
        var y = origin.Y + (size.Y * 0.45f);
        if (reason is not null)
        {
            var width = ImGui.CalcTextSize(reason).X * 1.5f;
            dl.AddText(ImGui.GetFont(), big, new Vector2(origin.X + ((size.X - width) * 0.5f), y - big), Theme.U32(Theme.Surface.Text), reason);
        }

        var hintWidth = ImGui.CalcTextSize(hint).X;
        dl.AddText(new Vector2(origin.X + ((size.X - hintWidth) * 0.5f), y + UiMetrics.Px(8f)), Theme.U32(Theme.Surface.TextSecondary), hint);
    }

    /// <summary>What holds the pause, in words; null when only a click is awaited after opening.</summary>
    private string? PauseReasonText() => pause.Shown switch
    {
        MoonfallPauseReason.Combat => Strings.MoonfallPausedCombat,
        MoonfallPauseReason.Duty => Strings.MoonfallPausedDuty,
        MoonfallPauseReason.Cutscene => Strings.MoonfallPausedCutscene,
        MoonfallPauseReason.Unfocused => Strings.MoonfallPausedFocus,
        MoonfallPauseReason.Reopened => null,
        _ => Strings.MoonfallPaused,
    };

    // ---- The end of a level ----

    private string tallyKeyText = string.Empty;
    private MoonfallTally? tallyFor;
    private string[] tallyLines = [];
    private long tallyShown = -1;

    private void DrawEnd(ImDrawListPtr dl, Vector2 origin, Vector2 size, MoonfallGame g)
    {
        var won = g.Phase == MoonfallPhase.Won;
        var line = ImGui.GetTextLineHeightWithSpacing();
        var next = levelIndex + 1;
        var last = won && next >= campaigns[campaign].Levels.Count;
        var panel = new Vector2(MathF.Min(size.X - UiMetrics.Px(24f), UiMetrics.Px(380f)), line * ((won ? 8.5f : 5.5f) + (last ? 1.2f : 0f)));
        var min = origin + ((size - panel) * 0.5f);
        var max = min + panel;
        dl.AddRectFilled(origin, origin + size, Theme.WithAlpha(Theme.Scene.Scrim, 0.55f));
        dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Raised), UiMetrics.Px(8f));
        dl.AddRect(min, max, Theme.U32(Theme.Gold), UiMetrics.Px(8f), ImDrawFlags.None, UiMetrics.Hairline);
        var pad = UiMetrics.Px(16f);
        var x = min.X + pad;
        var y = min.Y + pad;

        dl.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 1.3f, new Vector2(x, y), Theme.U32(Theme.GoldHigh), won ? Strings.MoonfallLevelClear : Strings.MoonfallOutOfBalls);
        y += line * 1.6f;
        if (won && g.Tally is { } tally)
        {
            if (tallyFor != tally || tallyKeyText != Strings.MoonfallTallyTotal)
            {
                tallyFor = tally;
                tallyKeyText = Strings.MoonfallTallyTotal;
                tallyShown = g.ShownScore;
                tallyLines =
                [
                    Strings.MoonfallTallyLevel, tally.LevelScore.ToString("N0", CultureInfo.CurrentCulture),
                    Strings.MoonfallTallyFullMoon, tally.FeverBonus.ToString("N0", CultureInfo.CurrentCulture),
                    string.Format(CultureInfo.CurrentCulture, Strings.MoonfallTallyBallsFormat, tally.BallsLeft), tally.BallBonus.ToString("N0", CultureInfo.CurrentCulture),
                    Strings.MoonfallTallyTotal, g.ShownScore.ToString("N0", CultureInfo.CurrentCulture),
                ];
            }

            // The total counts up with the score counter.
            if (tallyShown != g.ShownScore)
            {
                tallyShown = g.ShownScore;
                tallyLines[7] = tallyShown.ToString("N0", CultureInfo.CurrentCulture);
            }

            for (var k = 0; k < tallyLines.Length; k += 2)
            {
                var total = k == 6;
                var ink = Theme.U32(total ? Theme.Gold : Theme.Surface.Text);
                dl.AddText(new Vector2(x, y), Theme.U32(total ? Theme.Surface.Text : Theme.Surface.TextSecondary), tallyLines[k]);
                var valueWidth = ImGui.CalcTextSize(tallyLines[k + 1]).X;
                dl.AddText(new Vector2(max.X - pad - valueWidth, y), ink, tallyLines[k + 1]);
                y += line;
            }
        }
        else
        {
            dl.AddText(new Vector2(x, y), Theme.U32(Theme.Surface.TextSecondary), string.Format(CultureInfo.CurrentCulture, Strings.MoonfallOrangesLeftFormat, g.OrangesLeft));
            y += line;
        }

        if (last)
        {
            dl.AddText(new Vector2(x, y + (line * 0.2f)), Theme.U32(Theme.Surface.TextSecondary), Strings.MoonfallLastLevel);
        }

        // The way on: the next level (when there is one), or this one again.
        ImGui.SetCursorScreenPos(new Vector2(x, max.Y - pad - ImGui.GetFrameHeight()));
        if (won && !last)
        {
            if (ImGui.Button(Strings.MoonfallNextLevel + "##moonfallNext"))
            {
                SoundClick();
                Go(next);
            }

            ImGui.SameLine();
        }

        if (ImGui.Button((won ? Strings.MoonfallPlayAgain : Strings.MoonfallTryAgain) + "##moonfallAgain"))
        {
            SoundClick();
            Go(RestartChoice);
        }
    }
}
