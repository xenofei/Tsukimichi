using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Ui;

/// <summary>
/// The reward moments in play (spec-rich2.md §3–4), in the rich chrome: the power firing (the companion's card slides
/// into the window's margin at 1280 and wider, or the power's name rides a laurelled ribbon along the top rail on a small
/// window, and the effect is light at the green, its outer edge in the companion's colour), the style shots' ribbons placed
/// clear of every live peg (<see cref="MoonfallRibbons"/>), Fever (the sky lifts, the moon swells, moondust bursts once
/// from the last orange, the cups light, FULL MOON on the laurel ribbon whose plate fades to 35% once it has landed) and
/// the tally (LEVEL CLEAR, ACED, NEW BEST). Under Reduce motion the card fades instead of sliding and every moment shows
/// its end state at once.
/// </summary>
public sealed partial class MoonfallWindow
{
    /// <summary>How long the power's card shows: 0.25 s in, 1.2 s, 0.25 s out.</summary>
    private const double PowerMomentSeconds = 1.7;

    private const int RibbonCapacity = 4;

    private double feverAt = double.NegativeInfinity;
    private Vector2 feverBurst;
    private double powerFiredAt = double.NegativeInfinity;
    private MoonfallPower powerFired;
    private Vector2 powerFiredAtPeg;
    private readonly int[] powerUses = new int[MoonfallPowers.Count + 1];
    private bool cardInMargin;
    private bool wonAced;
    private bool wonNewBest;
    private long wonPreviousBest;

    private readonly string[] powerUpper = new string[MoonfallPowers.Count + 1];
    private int powerUpperFor = -1;
    private readonly Ribbon[] ribbons = new Ribbon[RibbonCapacity];
    private int ribbonNext;
    private readonly Vector3[] livePieces = new Vector3[(MoonfallLevelLoader.MaxPegs * 3) + 8];
    private readonly Vector4[] takenRibbons = new Vector4[RibbonCapacity];

    /// <summary>A style shot's (or the drum's) ribbon: where it was placed (its centre and size, units), its lines.</summary>
    private readonly record struct Ribbon(double At, string Title, string Value, Vector2 Centre, Vector2 Size, bool Placed);

    /// <summary>A power's name in capitals, as the ribbons and the card set it (made once per language).</summary>
    private string PowerUpper(MoonfallPower power)
    {
        if (powerUpperFor != Localization.Loc.Version)
        {
            powerUpperFor = Localization.Loc.Version;
            for (var i = 0; i < powerUpper.Length; i++)
            {
                powerUpper[i] = Strings.MoonfallPowerName((MoonfallPower)i).ToUpper(CultureInfo.CurrentCulture);
            }
        }

        return (uint)power < (uint)powerUpper.Length ? powerUpper[(int)power] : string.Empty;
    }

    private void ClearMoments()
    {
        feverAt = double.NegativeInfinity;
        powerFiredAt = double.NegativeInfinity;
        Array.Clear(powerUses);
        Array.Clear(ribbons);
        ribbonNext = 0;
        wonAced = false;
        wonNewBest = false;
    }

    /// <summary>The events the moments read.</summary>
    private void NoteMomentEvent(in MoonfallEvent e)
    {
        switch (e.Kind)
        {
            case MoonfallEventKind.FeverHit:
                feverAt = boardClock;
                feverBurst = new Vector2((float)e.X, (float)e.Y);
                break;

            case MoonfallEventKind.PowerTriggered:
                powerFiredAt = boardClock;
                powerFired = (MoonfallPower)e.Value;
                powerFiredAtPeg = new Vector2((float)e.X, (float)e.Y);
                if ((uint)powerFired < (uint)powerUses.Length)
                {
                    powerUses[(int)powerFired]++;
                }

                break;
        }
    }

    /// <summary>A style shot's (or the drum's) ribbon, placed now by the clearance rule (6 units from every live peg); false when the rich chrome is off.</summary>
    private bool AddRibbon(MoonfallGame g, string title, string value)
    {
        if (!richHud)
        {
            return false;
        }

        // The ribbon's size from its words: the title in Jupiter 22, the value in TrumpGothic 18 below it (units).
        var titleWidth = MeasureText(MoonfallFace.Jupiter, 22f, title, 2f);
        var size = new Vector2(MathF.Max(titleWidth, MeasureText(MoonfallFace.Trump, 18f, value)) + 52f, value.Length > 0 ? 54f : 32f);
        var n = 0;
        for (var i = 0; i < g.PegCount && n < livePieces.Length - 3; i++)
        {
            var peg = g.Peg(i);
            if (peg.Cleared)
            {
                continue;
            }

            if (peg.Shape == PegShape.Round)
            {
                livePieces[n++] = new Vector3((float)peg.X, (float)peg.Y, (float)peg.Radius);
            }
            else if (peg.Shape == PegShape.Line)
            {
                // A straight brick as three circles along it (its ends and middle) of its half-thickness plus half its length's step.
                var half = (float)(peg.Thickness * 0.5);
                var len = (float)Math.Sqrt(((peg.X2 - peg.X) * (peg.X2 - peg.X)) + ((peg.Y2 - peg.Y) * (peg.Y2 - peg.Y)));
                livePieces[n++] = new Vector3((float)peg.X, (float)peg.Y, half);
                livePieces[n++] = new Vector3((float)((peg.X + peg.X2) * 0.5), (float)((peg.Y + peg.Y2) * 0.5), half + (len * 0.25f));
                livePieces[n++] = new Vector3((float)peg.X2, (float)peg.Y2, half);
            }
            else
            {
                var mid = peg.StartRadians + (peg.SweepRadians * 0.5);
                var reach = (float)((peg.Radius * Math.Sin(Math.Min(Math.PI, peg.SweepRadians) * 0.5)) + (peg.Thickness * 0.5));
                livePieces[n++] = new Vector3((float)(peg.X + (peg.Radius * Math.Cos(mid))), (float)(peg.Y + (peg.Radius * Math.Sin(mid))), reach);
            }
        }

        var taken = 0;
        foreach (var r in ribbons)
        {
            if (r.Title is not null && r.Placed && boardClock - r.At < StyleSeconds)
            {
                takenRibbons[taken++] = new Vector4(r.Centre - (r.Size / 2), (r.Centre + (r.Size / 2)).X, (r.Centre + (r.Size / 2)).Y);
            }
        }

        var place = MoonfallRibbons.Place(livePieces.AsSpan(0, n), size, takenRibbons.AsSpan(0, taken));
        ribbons[ribbonNext] = new Ribbon(boardClock, title, value, place ?? new Vector2(400, 62), size, place is not null);
        ribbonNext = (ribbonNext + 1) % RibbonCapacity;
        return true;
    }

    // ---- Drawn on the board ----

    /// <summary>The power's effect at the green: a white-gold flash and ring, only its outer edge in the companion's colour (expanding once).</summary>
    private void PowerFlash(in ArtPen p)
    {
        var age = boardClock - powerFiredAt;
        if (age is < 0 or > 0.6 || MoonfallCards.For(powerFired) is not { } companion)
        {
            return;
        }

        var t = (float)(age / 0.6);
        var still = motion == MoonfallMotionLevel.Still;
        var r = still ? 30f : 22f + (12f * t);
        var fade = 1f - t;
        var v = p.View;
        var at = v.Map(powerFiredAtPeg.X, powerFiredAtPeg.Y);
        ref readonly var soft = ref p.Atlas[MoonfallSprite.Soft];
        Put(p, soft, powerFiredAtPeg.X, powerFiredAtPeg.Y, 14f / 4f, Ink(MoonfallColor.Hex("#FFF4DC"), 0.30f * fade));
        p.Dl.AddCircle(at, v.Size(r), Ink(MoonfallColor.Hex("#FFF4DC"), 0.55f * fade), 48, MathF.Max(1f, v.Size(3.0)));
        p.Dl.AddCircle(at, v.Size(r + 4), Ink(companion.Accent, 0.40f * fade), 48, MathF.Max(1f, v.Size(2.0)));
        if (!still)
        {
            // Sparks round the ring, the same for the same power.
            for (var k = 0; k < 14; k++)
            {
                var a = MoonfallNoise.Lattice(k, (int)powerFired, 71) * 2 * MathF.PI;
                var d = 16f + (28f * MoonfallNoise.Lattice(k, (int)powerFired, 72)) + (6f * t);
                Put(p, soft, powerFiredAtPeg.X + (MathF.Cos(a) * d), powerFiredAtPeg.Y + (MathF.Sin(a) * d), 2f / 4f, Ink(MoonfallColor.Hex("#FFE6C8"), 0.8f * fade));
            }
        }
    }

    /// <summary>Brass Wings in the rich chrome: the PvP emblem's gilt wings bolted to the bucket's rims, the companion's glow along them.</summary>
    private void RichWings(in ChromePen c, in ArtPen p, MoonfallGame g, double alpha)
    {
        if (!g.WingsOpen || g.Fever || c.Sheet[MoonfallChromePart.Wing] is not { } wing || MoonfallCards.For(MoonfallPower.Wings) is not { } cid)
        {
            return;
        }

        var bx = g.BucketXAt(alpha);
        const double W = 56;
        var h = W * wing.H / wing.W;
        ref readonly var soft = ref p.Atlas[MoonfallSprite.Soft];
        foreach (var side in (ReadOnlySpan<int>)[-1, 1])
        {
            var x = side < 0 ? bx - 65.5 - W + 4 : bx + 65.5 - 4;
            Put(p, soft, x + (W / 2), 566, (float)(W * 0.7 / 4), Ink(cid.Accent, 0.45f));
            Part(c, MoonfallChromePart.Wing, x, 572 - (h * 0.62), x + W, 572 - (h * 0.62) + h, uint.MaxValue, flipX: side > 0);
        }
    }

    /// <summary>
    /// screens2.banner: Triple Triad's laurel split to frame the words, joined by one ribbon of its own colour behind them
    /// with gilt rules above and below; the words in Jupiter, spaced. Units; <paramref name="plate"/> fades the ribbon only.
    /// </summary>
    private void Banner(in ChromePen c, double cx, double cy, string text, float size, string? sub, float subSize, Vector3 accent, float plate, float alpha,
        bool laurel, double maxWidth, float tracking, bool subIsNumber = false)
    {
        var v = c.View;
        var dl = c.Dl;
        var px = NamePx(v, size, MoonfallFace.Jupiter);
        // Widths in board units: a title raised to the floor in a small window takes more of the board.
        var tw = MeasureText(MoonfallFace.Jupiter, px, text, v.Size(tracking)) / v.Size(1);
        var bandH = (size * 1.0) + (sub is not null ? subSize * 1.9 : 0);
        var by0 = cy - (size * 0.55);
        // The plate holds the title and the line under it (raised to the text floor in a small window, the line can be the wider).
        // The line under the title is a label (its floor 7 px caps) or a value (a number's floor, 8 px).
        var subPx = sub is null ? 0f : subIsNumber ? NumberPx(v, subSize, MoonfallFace.Axis) : NamePx(v, subSize, MoonfallFace.Axis);
        var sw = sub is not null ? MeasureText(MoonfallFace.Axis, subPx, sub) / v.Size(1) : 0;
        var half = (Math.Max(tw, sw) / 2) + 26;
        if (plate > 0)
        {
            // The plate's shadow, feathered at both ends (no hard band under it).
            var shadow = Ink(Vector3.Zero, 0.30f * plate);
            var none = Ink(Vector3.Zero, 0f);
            var shadeMid = by0 + 6 + ((bandH + 4) * 0.6);
            dl.AddRectFilledMultiColor(v.Map(cx - half - 14, by0 + 6), v.Map(cx + half + 14, shadeMid), none, none, shadow, shadow);
            dl.AddRectFilledMultiColor(v.Map(cx - half - 14, shadeMid), v.Map(cx + half + 14, by0 + bandH + 16), shadow, shadow, none, none);
            var top = Ink(MoonfallColor.Hex("#7A3A12"), 0.94f * plate);
            var middle = Ink(MoonfallColor.Hex("#5A2A10"), 0.94f * plate);
            var foot = Ink(MoonfallColor.Hex("#3A1A08"), 0.94f * plate);
            dl.AddRectFilledMultiColor(v.Map(cx - half, by0), v.Map(cx + half, by0 + (bandH / 2)), top, top, middle, middle);
            dl.AddRectFilledMultiColor(v.Map(cx - half, by0 + (bandH / 2)), v.Map(cx + half, by0 + bandH), middle, middle, foot, foot);
            var gilt = Ink(MoonfallColor.Hex("#E3B865"), MathF.Max(plate, 0.6f) * alpha);
            foreach (var yy in (ReadOnlySpan<double>)[by0 + 2, by0 + bandH - 2])
            {
                dl.AddRectFilled(v.Map(cx - half, yy - 1.1), v.Map(cx + half, yy + 1.1), gilt, v.Size(1));
            }
        }

        if (laurel && c.Sheet[MoonfallChromePart.Laurel] is { } l)
        {
            // The laurel's two halves (left of its plaque, right of it), scaled so the whole banner keeps within maxWidth.
            var s = (size * 1.9) / l.H;
            const float Gap = 8;
            const float LeftW = 262, RightFrom = 380;
            // Each side within half of maxWidth (the halves differ in width), so neither tail crosses its neighbour.
            var widest = MathF.Max(LeftW, l.W - RightFrom);
            while (tw + (2 * Gap) + (2 * widest * s) > maxWidth && s > 0.05)
            {
                s *= 0.95;
            }

            if (tw + (2 * Gap) + (2 * widest * s) > maxWidth)
            {
                // No room even for a small laurel: the plate alone.
                s = 0;
            }

            var top = cy - (l.H * s * 0.52);
            // The ribbon's tails fade with the plate (Fever's plate settles to 35% so the lit pegs show through).
            var tint = Ink(Vector3.One, alpha * (plate > 0 ? MathF.Max(plate, 0.35f) : 1f));
            if (s > 0)
            {
                Part(c, MoonfallChromePart.Laurel, cx - (tw / 2) - Gap - (LeftW * s), top, cx - (tw / 2) - Gap, top + (l.H * s), tint, u1: LeftW);
                Part(c, MoonfallChromePart.Laurel, cx + (tw / 2) + Gap, top, cx + (tw / 2) + Gap + ((l.W - RightFrom) * s), top + (l.H * s), tint, u0: RightFrom);
            }
        }

        DrawText(dl, MoonfallFace.Jupiter, px, v.Map(cx, cy), Anchor.Centre, Ink(GoldHiInk, alpha), text, Ink(MoonfallColor.Hex("#140A02"), alpha), v.Size(1.6), v.Size(tracking));
        if (sub is not null)
        {
            DrawText(dl, MoonfallFace.Axis, subPx, v.Map(cx, cy + (size * 0.42) + (subSize * 0.75)), Anchor.Centre, Ink(Cream, alpha), sub,
                Ink(MoonfallColor.Hex("#140A02"), alpha), v.Size(1.0));
        }
    }

    /// <summary>The style shots' and the drum's ribbons, small and without laurel, in the open sky where the rule placed them (1.8 s each).</summary>
    private void Ribbons(in ChromePen board)
    {
        // Placed among the live pegs, they hold their place on the board (the zoom never runs during an ordinary shot).
        var c = board;
        foreach (var r in ribbons)
        {
            var age = boardClock - r.At;
            if (r.Title is null || age < 0 || age > StyleSeconds)
            {
                continue;
            }

            var fade = age < StyleSeconds - 0.3 ? 1f : (float)((StyleSeconds - age) / 0.3);
            var accent = MoonfallColor.Hex("#FFB45E");
            Banner(c, r.Centre.X, r.Centre.Y - (r.Value.Length > 0 ? 8 : 0), r.Title, 22f, r.Value.Length > 0 ? r.Value : null, 18f, accent, fade, fade, laurel: false, r.Size.X, 2f, subIsNumber: true);
        }
    }

    /// <summary>
    /// Fever on the board: moondust bursting once from the last orange (Full only; 70 motes, kept 6 units clear of every
    /// piece), FULL MOON on the laurel ribbon (its plate fading to 35% once Fever has landed; the lettering stays).
    /// </summary>
    private void FeverMoment(in ChromePen c, in ArtPen p, MoonfallGame g)
    {
        var since = boardClock - feverAt;
        if (!g.Fever || since < 0)
        {
            return;
        }

        var still = motion == MoonfallMotionLevel.Still;
        if (motion == MoonfallMotionLevel.Full && since < 1.5 && sceneFor?.Layers.Clearance is { } clearance)
        {
            var t = (float)(since / 1.5);
            ref readonly var soft = ref p.Atlas[MoonfallSprite.Soft];
            for (var k = 0; k < 70; k++)
            {
                var a = MoonfallNoise.Lattice(k, 1, 311) * 2 * MathF.PI;
                var speed = 40f + (70f * MoonfallNoise.Lattice(k, 2, 311));
                var d = speed * (1 - ((1 - t) * (1 - t)));
                var x = feverBurst.X + (MathF.Cos(a) * d);
                var y = feverBurst.Y + (MathF.Sin(a) * d) + (10 * t * t);
                if (clearance.At(x, y) < 6)
                {
                    continue;
                }

                var al = MathF.Pow(1 - (t * 0.8f), 1.2f) * (0.6f + (0.4f * MoonfallNoise.Lattice(k, 3, 311)));
                Put(p, soft, x, y, 2.4f / 4f, Ink(MoonfallColor.Hex("#FFE9C8"), al));
            }
        }

        if (!g.BannerVisible)
        {
            return;
        }

        var accent = MoonfallCards.For(g.Power)?.Accent ?? MoonfallColor.Hex("#FFD27A");
        var alpha = still ? 1f : (float)Math.Clamp((since - 0.15) / 0.6, 0, 1);
        var plate = since < 1.5 && !still ? 1f : 0.35f;
        var text = g.Perfect ? Strings.MoonfallBannerPerfectMoon : Strings.MoonfallBannerFullMoon;
        var small = c.View.Scale < 1f;
        // The banner holds its place on the window while Full Moon's camera moves.
        var flat = c with { View = new View(c.View.Origin, c.View.Scale, 1f, BoardCentre) };
        Banner(flat, 400, 222, text, small ? 52f : 66f, Strings.MoonfallBannerFullMoonSub, small ? 18f : 16f, accent, plate * alpha, alpha, laurel: true, small ? 640 : 600, small ? 4f : 6f);
    }

    /// <summary>Fever's cups lit from within (chrome2.fever_cups_lit): the centre brightest, their light breathing ±15% over 3 s, each value on its plate.</summary>
    private void RichFeverCups(in ArtPen p, MoonfallGame g, bool plain)
    {
        if (!g.Fever)
        {
            return;
        }

        var shown = (float)g.FeverBucketsShown;
        var lift = (1 - shown) * 30;
        var values = MoonfallRules.FeverBucketValues;
        var v = p.View;
        var accent = MoonfallCards.For(g.Power)?.Accent ?? MoonfallColor.Hex("#FFD27A");
        var breath = MoonfallMotion.Breath(boardClock, 3f, 0.15f, motion == MoonfallMotionLevel.Still);
        ref readonly var soft = ref p.Atlas[MoonfallSprite.Soft];
        for (var k = 0; k < values.Length; k++)
        {
            var cx = MoonfallRules.LeftWall + ((k + 0.5) * MoonfallBucket.FeverBucketWidth);
            var centre = k == values.Length / 2;
            if (!plain)
            {
                Put(p, soft, cx, 566 + lift, (float)(MoonfallBucket.FeverBucketWidth * 0.5 / 4), Ink(MoonfallColor.Hex("#FFD27A"), (centre ? 0.55f : 0.22f) * shown * breath));
                Put(p, soft, cx, 566 + lift, (float)(MoonfallBucket.FeverBucketWidth * 0.62 / 4), Ink(accent, (centre ? 0.20f : 0.08f) * shown));
            }

            ref readonly var cup = ref p.Atlas[centre ? MoonfallSprite.FeverCupCentre : MoonfallSprite.FeverCup];
            Put(p, cup, cx, 581 + lift, 1f, Theme.WithAlpha(Vector4.One, shown));
            var plateMin = v.Map(cx - 36, 574 + lift);
            var plateMax = v.Map(cx + 36, 591 + lift);
            p.Dl.AddRectFilled(plateMin, plateMax, Ink(MoonfallColor.Hex("#0B1230"), 0.92f * shown), v.Size(5));
            p.Dl.AddRect(plateMin, plateMax, Ink(MoonfallColor.Hex("#C9A15A"), shown), v.Size(5), ImDrawFlags.None, MathF.Max(1f, v.Size(0.9)));
            var text = g.Perfect ? perfectText : feverTexts[k];
            var lit = g.FeverBucket == k;
            DrawText(p.Dl, MoonfallFace.Trump, NumberPx(v, 18, MoonfallFace.Trump), v.Map(cx, 582.5 + lift), Anchor.Centre,
                Ink(centre || lit ? GoldHiInk : Cream, shown), text, Ink(EdgeInk, shown), v.Size(0.6));
        }
    }

    // ---- Outside the board ----

    /// <summary>
    /// The power firing at 1280 and wider: the companion's card slides into the window's margin beside the board, in the
    /// journal's frame, with the power named in their colour, their name and what the power does (0.25 s in, 1.2 s, 0.25 s
    /// out; it fades under Reduce motion). Nothing on the board is covered.
    /// </summary>
    private void PowerCard(ImDrawListPtr dl, Vector2 areaMin, Vector2 origin, MoonfallChromeSheet sheet, ImTextureID ui, in ArtPen board)
    {
        var age = boardClock - powerFiredAt;
        if (age is < 0 or > PowerMomentSeconds || MoonfallCards.For(powerFired) is not { } companion || gameArt?.Card(powerFired) is not { } card)
        {
            return;
        }

        var margin = origin.X - areaMin.X;
        var cw = MoonfallHud.MarginCardWidth(margin);
        var art = MoonfallCards.ArtBounds;
        var ch = cw * (art.W - art.Y) * MoonfallCards.CardHeight / ((art.Z - art.X) * MoonfallCards.CardWidth);
        var x0 = areaMin.X + 8f;
        var y0 = origin.Y + ((origin.Y - areaMin.Y) * 0f) + MathF.Max(40f, 150f * (cw / 120f));
        var still = motion == MoonfallMotionLevel.Still;
        float alpha = 1f, slide = 0f;
        var tIn = (float)Math.Clamp(age / 0.25, 0, 1);
        var tOut = (float)Math.Clamp((PowerMomentSeconds - age) / 0.25, 0, 1);
        if (still)
        {
            alpha = MathF.Min(tIn, tOut);
        }
        else
        {
            var k = MathF.Min(tIn, tOut);
            slide = (1 - (k * k * (3 - (2 * k)))) * -(cw + 24);
        }

        var x = x0 + slide;
        var screen = new View(Vector2.Zero, 1f, 1f, BoardCentre);
        var pen = new ChromePen(dl, screen, sheet, ui);
        // The companion's colour glowing round the card (the atlas's soft light, in screen pixels).
        var glowPen = board with { View = screen };
        Put(glowPen, board.Atlas[MoonfallSprite.Soft], x + (cw / 2), y0 + (ch / 2), ch * 0.85f / 4f, Ink(companion.Accent, 0.45f * alpha));
        dl.AddRectFilled(new Vector2(x + 4, y0 + 8), new Vector2(x + cw + 6, y0 + ch + 10), Ink(Vector3.Zero, 0.55f * alpha), 3f);
        dl.AddImage(card.Handle, new Vector2(x, y0), new Vector2(x + cw, y0 + ch), new Vector2(art.X, art.Y), new Vector2(art.Z, art.W), Ink(Vector3.One, alpha));
        GiltFrame(pen, x, y0, x + cw, y0 + ch, 0.36 * 0.8 * (cw / 120f), Ink(Vector3.One, alpha));
        var by = y0 + ch + (24f * (cw / 120f));
        var name = PowerUpper(powerFired);
        var namePx = MathF.Min(34f * (cw / 150f), 30f);
        var nw = MeasureText(MoonfallFace.Jupiter, namePx, name);
        if (nw > cw + 4)
        {
            namePx *= (cw + 4) / nw;
        }

        DrawText(dl, MoonfallFace.Jupiter, MathF.Max(namePx, 12f), new Vector2(x + (cw / 2), by), Anchor.Centre, Ink(Tint(companion.Accent, 0.2f), alpha), name, Ink(MoonfallColor.Hex("#140A02"), alpha), 1.6f);
        DrawText(dl, MoonfallFace.Axis, MathF.Max(15f * (cw / 150f), 12f), new Vector2(x + (cw / 2), by + 24f), Anchor.Centre, Ink(Cream, alpha), Strings.MoonfallCompanionName(powerFired), Ink(EdgeInk, alpha), 1.2f);
        // What the power does, wrapped to the card's width.
        var hint = Strings.MoonfallPowerHint(powerFired);
        var y = by + 48f;
        var linePx = MathF.Max(13f, 14f * (cw / 150f));
        var start = 0;
        while (start < hint.Length && y < areaMin.Y + 2000)
        {
            var end = start;
            var lastSpace = -1;
            while (end < hint.Length && MeasureText(MoonfallFace.Axis, linePx, hint.AsSpan(start, end - start + 1)) <= cw)
            {
                if (hint[end] == ' ')
                {
                    lastSpace = end;
                }

                end++;
            }

            if (end < hint.Length && lastSpace > start)
            {
                end = lastSpace;
            }

            if (end == start)
            {
                end = Math.Min(hint.Length, start + 1);
            }

            DrawText(dl, MoonfallFace.Axis, linePx, new Vector2(x + (cw / 2), y), Anchor.Centre, Ink(Ink2, alpha), hint.AsSpan(start, end - start).Trim(), Ink(EdgeInk, alpha), 1.2f);
            y += linePx * 1.25f;
            start = end;
            while (start < hint.Length && hint[start] == ' ')
            {
                start++;
            }
        }
    }

    /// <summary>The power firing on a small window, where there is no margin: its name rides a laurelled ribbon along the top rail; nothing covers the opening.</summary>
    private void PowerRibbon(in ChromePen c)
    {
        var age = boardClock - powerFiredAt;
        if (age is < 0 or > PowerMomentSeconds || MoonfallCards.For(powerFired) is not { } companion)
        {
            return;
        }

        var fade = (float)Math.Clamp(Math.Min(age / 0.25, (PowerMomentSeconds - age) / 0.25), 0, 1);
        // Between the name plate and the score plate: the ribbon's tails shrink to fit, never crossing the level's name.
        Banner(c, 400, 22, PowerUpper(powerFired), 30f, null, 0, companion.Accent, fade, fade, laurel: true, MoonfallHud.ScorePlate.X0 - MoonfallHud.NamePlate.X1 - 12, 4f);
    }
}
