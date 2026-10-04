using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Ui;

/// <summary>
/// The moon icon's face, drawn by the theme (feature plan v8 H1 and H2; spec-1.22 H1 "The face", H2;
/// moon-icon-1.22.png, icon-particles-1.22.png). The recipes are the mock's (<c>docs/design/v8/mock-src/v722.js</c>,
/// <c>face22</c> and <c>mi22</c>) in its 64-unit box, where the icon's radius is 32 units; the inks are
/// <see cref="MoonIconInks"/>:
/// <list type="bullet">
/// <item>Medallion: a brass rim over a lapis well, three faint stars in it.</item>
/// <item>Classic: the 1.11 flat disc with a hairline (no metal, as Classic's glyphs).</item>
/// <item>Ishgard Glass: a lead came rim and two glass tones split by a came line, the crescent leaded too.</item>
/// <item>Aether Crystal: a silver rim with four facet chips, and faint facets in the well and on the crescent.</item>
/// <item>Astrologian's Orrery: an astrolabe rim with an inner line, and a three-star constellation.</item>
/// <item>Sumi to Kinpaku: a lacquer rim with a kirikane line and a gofun hairline, and a gloss band on the well.</item>
/// </list>
/// The rim is the frame kit's resting metal, never the act-now gilt: the icon asks for nothing. In every theme the
/// crescent is lit on its upper-left limb, with a faint earthshine on the dark part, and no seas or craters (a crescent
/// this small would read as cheese). The shadow falls straight down. Quiet draws the face with a hairline and no rim,
/// Plain the flat glyph with no shadow or glow. The hover lifts the face and adds a cool moonlight glow
/// (<see cref="MoonIconHover"/>); the particles come from <see cref="IconParticles"/>. Everything is circles, paths and
/// a few small vertex-coloured meshes into the draw list given, with no allocation.
/// </summary>
internal static class MoonIconFace
{
    // The mock's box: 64 units across, the icon's radius 32.
    private const float Unit = 32f;

    // The crescent: the moon's disc, and the dark disc offset toward the lower right that leaves its upper-left limb lit.
    private const float MoonRadius = 15f;
    private const float ShadowRadius = 14.2f;
    private const int ArcPoints = 24;
    private const int Segments = 36;
    private static readonly Vector2 ShadowOffset = new(5.6f, 4.4f);

    private static readonly Vector4 White = Vector4.One;

    private enum FaceExtra
    {
        None,
        Stars,
        Came,
        Facets,
        Constellation,
        Gloss,
    }

    /// <summary>What the face is drawn as this frame.</summary>
    /// <param name="Theme">The theme whose face it is.</param>
    /// <param name="Kit">The frame kit whose resting metal makes the rim.</param>
    /// <param name="Flair">The Decoration level in effect (high contrast already capped at Quiet).</param>
    public readonly record struct Look(ThemeId Theme, FrameKitId Kit, Flair Flair);

    /// <summary>
    /// Draws the icon centred on <paramref name="centre"/> with its <paramref name="radius"/> in px: the particles
    /// behind it and the Orrery's far orbit, the glow and the shadow at <paramref name="hover"/> (0 at rest, 1 hovered),
    /// the face lifted by <paramref name="rise"/> px, the particles in front, and the <paramref name="dot"/> in its ink
    /// (none when null). <paramref name="scale"/> is the layout scale (px per logical px) for the shadow and the dot.
    /// </summary>
    public static void Draw(ImDrawListPtr dl, Vector2 centre, float radius, in Look look, float hover, float rise, ReadOnlySpan<IconParticle> particles, bool orbit, Vector4? dot, float scale)
    {
        if (!(radius > 0f) || !float.IsFinite(radius))
        {
            return;
        }

        var u = radius / Unit;
        var lifted = centre - new Vector2(0f, MathF.Max(0f, rise));
        var flair = look.Flair;
        var h = Math.Clamp(hover, 0f, 1f);

        if (orbit)
        {
            Orbit(dl, lifted, radius, u, back: true);
        }

        foreach (var p in particles)
        {
            if (p.Behind)
            {
                Particle(dl, lifted, radius, u, p);
            }
        }

        // The glow: cool moonlight round the lifted face, never at Plain; under Reduce motion it is simply there.
        var glow = MoonIconHover.GlowStrength(flair) * h;
        if (glow > 0f)
        {
            SoftDisc(dl, lifted, 24f * u, 44f * u, MoonIconInks.Glow, MoonIconHover.GlowCoreAlpha * glow);
        }

        if (MoonIconHover.Shadow(flair))
        {
            var (offset, blur, alpha) = MoonIconHover.ShadowAt(h);
            var r = 31f * u;
            SoftDisc(dl, centre + new Vector2(0f, offset * scale), MathF.Max(0f, r - (blur * scale)), r + (blur * scale * 1.5f), MoonIconInks.NightInk, alpha);
        }

        var face = MoonIconInks.Face(look.Theme);
        var extra = ExtraOf(look.Theme);
        if (flair == Flair.Plain)
        {
            Plain(dl, lifted, u);
            if (h > 0f)
            {
                dl.AddCircle(lifted, 31f * u, Theme.WithAlpha(MoonIconInks.PlainInk, 0.8f * h), 0, MoonIconHover.PlainRingLogical * scale);
            }
        }
        else if (flair == Flair.Full && look.Theme != ThemeId.Classic)
        {
            Rimmed(dl, lifted, u, face, extra, look.Kit);
        }
        else
        {
            Flat(dl, lifted, u, face, extra, look.Theme == ThemeId.Classic ? MoonIconInks.PlainInk : MoonIconInks.QuietLine);
        }

        foreach (var p in particles)
        {
            if (!p.Behind)
            {
                Particle(dl, lifted, radius, u, p);
            }
        }

        if (orbit)
        {
            Orbit(dl, lifted, radius, u, back: false);
        }

        if (dot is { } ink)
        {
            Dot(dl, lifted + (new Vector2(22.6f, -22.6f) * u), ink, scale);
        }
    }

    private static FaceExtra ExtraOf(ThemeId theme) => theme switch
    {
        ThemeId.Classic => FaceExtra.None,
        ThemeId.IshgardGlass => FaceExtra.Came,
        ThemeId.AetherCrystal => FaceExtra.Facets,
        ThemeId.Orrery => FaceExtra.Constellation,
        ThemeId.Sumi => FaceExtra.Gloss,
        _ => FaceExtra.Stars,
    };

    /// <summary>The flat glyph (Plain): a dark disc, a hairline ring and a flat crescent; no shadow or glow.</summary>
    private static void Plain(ImDrawListPtr dl, Vector2 c, float u)
    {
        dl.AddCircleFilled(c, 28f * u, Theme.U32(MoonIconInks.PlainWell), 0);
        dl.AddCircle(c, 28.5f * u, Theme.WithAlpha(MoonIconInks.PlainInk, 0.6f), 0, 1.6f * u);
        Crescent(dl, c, u, MoonIconInks.PlainInk, MoonIconInks.PlainInk);
    }

    /// <summary>Classic at any level, and every theme at Quiet: the well with a hairline, no metal.</summary>
    private static void Flat(ImDrawListPtr dl, Vector2 c, float u, MoonIconFaceInks face, FaceExtra extra, Vector4 line)
    {
        WellDisc(dl, c, 29.5f * u, face.Well);
        dl.AddCircle(c, 29.5f * u, Theme.WithAlpha(line, 0.62f), 0, 1.3f * u);
        Extras(dl, c, u, extra);
        Moon(dl, c, u, face, extra);
    }

    /// <summary>Full: the kit's rim, a dark inner ring, the well and its highlight, the theme's marks and the crescent.</summary>
    private static void Rimmed(ImDrawListPtr dl, Vector2 c, float u, MoonIconFaceInks face, FaceExtra extra, FrameKitId kit)
    {
        var rim = MoonIconInks.Rim(kit);
        dl.AddCircleFilled(c, 31f * u, Theme.U32(rim[1].Color), 0);
        LinearDisc(dl, c, 30.6f * u, rim);
        dl.AddCircle(c, 31.4f * u, Theme.WithAlpha(MoonIconInks.NightInk, 0.6f), 0, 0.8f * u);
        if (kit == FrameKitId.Kirikane)
        {
            dl.AddCircle(c, 29.2f * u, Theme.U32(MoonIconInks.Kirikane), 0, 0.9f * u);
            dl.AddCircle(c, 31f * u, Theme.WithAlpha(MoonIconInks.Gofun, 0.34f), 0, 0.5f * u);
        }
        else if (kit == FrameKitId.Astrolabe)
        {
            dl.AddCircle(c, 28.4f * u, Theme.U32(rim[2].Color), 0, 0.9f * u);
        }
        else if (kit == FrameKitId.Silver)
        {
            FacetChips(dl, c, u, rim);
        }

        dl.AddCircleFilled(c, 27.7f * u, Theme.WithAlpha(MoonIconInks.NightInk, 0.75f), 0);
        WellDisc(dl, c, 27f * u, face.Well);

        // The rim's lit inner edge, upper left.
        dl.PathArcTo(c, 27.8f * u, 206.2f * MathF.PI / 180f, 243.8f * MathF.PI / 180f, 10);
        dl.PathStroke(Theme.WithAlpha(White, kit == FrameKitId.Kirikane ? 0.25f : 0.38f), ImDrawFlags.None, 1.2f * u);

        Extras(dl, c, u, extra);
        Moon(dl, c, u, face, extra);
        if (extra == FaceExtra.Came)
        {
            // The crescent's own lead, along its lit limb.
            OuterLimb(dl, c, u, MoonRadius - 0.4f, MoonIconInks.Rim(FrameKitId.Came)[^1].Color, 1f, 1f);
        }
    }

    /// <summary>The earthshine and the crescent, then the faint facet line Aether Crystal cuts into it.</summary>
    private static void Moon(ImDrawListPtr dl, Vector2 c, float u, MoonIconFaceInks face, FaceExtra extra)
    {
        dl.AddCircleFilled(c, MoonRadius * u, Theme.WithAlpha(face.Earth, 0.38f), 0);
        Crescent(dl, c, u, face.Moon[0].Color, face.Moon[^1].Color);
        if (extra == FaceExtra.Facets)
        {
            Polyline(dl, c, u, Theme.WithAlpha(MoonIconInks.Facet, 0.55f), 0.6f, (21f, 23.5f), (23.6f, 29f), (21.5f, 36f));
        }
    }

    /// <summary>Each theme's marks inside the well (the mock's <c>extra</c>), kept inside it.</summary>
    private static void Extras(ImDrawListPtr dl, Vector2 c, float u, FaceExtra extra)
    {
        switch (extra)
        {
            case FaceExtra.Stars:
                Dot(dl, c, u, 18f, 44f, 0.7f, MoonIconInks.CoolStar, 0.55f);
                Dot(dl, c, u, 46f, 47f, 0.6f, MoonIconInks.CoolStar, 0.45f);
                Dot(dl, c, u, 49f, 20f, 0.55f, MoonIconInks.WarmStar, 0.5f);
                break;
            case FaceExtra.Came:
                LowerGlass(dl, c, u);
                dl.AddLine(At(c, u, 14f, 14f), At(c, u, 19f, 19f), Theme.WithAlpha(White, 0.22f), 2f * u);
                break;
            case FaceExtra.Facets:
                var facet = Theme.WithAlpha(MoonIconInks.Facet, 0.16f);
                Polyline(dl, c, u, facet, 0.8f, (10f, 40f), (32f, 54f), (54f, 38f));
                Polyline(dl, c, u, facet, 0.8f, (32f, 54f), (32f, 58.5f));
                Polyline(dl, c, u, facet, 0.8f, (20f, 12f), (32f, 20f), (44f, 12f));
                break;
            case FaceExtra.Constellation:
                var star = MoonIconInks.ConstellationStar;
                Polyline(dl, c, u, Theme.WithAlpha(MoonIconInks.Orbit, 0.35f), 0.6f, (38f, 48f), (44f, 45f), (49f, 49f));
                Dot(dl, c, u, 38f, 48f, 0.9f, star, 0.8f);
                Dot(dl, c, u, 44f, 45f, 0.7f, star, 0.7f);
                Dot(dl, c, u, 49f, 49f, 1f, star, 0.85f);
                break;
            case FaceExtra.Gloss:
                dl.AddLine(At(c, u, 10.5f, 21.5f), At(c, u, 21.5f, 10.5f), Theme.WithAlpha(White, 0.07f), 7f * u);
                break;
        }
    }

    /// <summary>Ishgard Glass's lower glass: a deeper pane under a came line that waves across the well.</summary>
    private static void LowerGlass(ImDrawListPtr dl, Vector2 c, float u)
    {
        const int Columns = 16;
        const float WellRadius = 27f;
        var deep = Theme.WithAlpha(MoonIconInks.LowerGlass, 0.55f);
        var uv = ImGui.GetFontTexUvWhitePixel();
        dl.PrimReserve(Columns * 6, (Columns + 1) * 2);
        var i0 = dl.VtxCurrentIdx;
        for (var k = 0; k <= Columns; k++)
        {
            var x = -WellRadius + (2f * WellRadius * k / Columns);
            var floor = MathF.Sqrt(MathF.Max(0f, (WellRadius * WellRadius) - (x * x)));
            var top = MathF.Min(floor, WaveY(x + Unit) + 0.6f - Unit);
            dl.PrimWriteVtx(c + (new Vector2(x, top) * u), uv, deep);
            dl.PrimWriteVtx(c + (new Vector2(x, floor) * u), uv, deep);
        }

        for (var k = 0; k < Columns; k++)
        {
            var a = i0 + (uint)(k * 2);
            Quad(dl, a, a + 2, a + 3, a + 1);
        }

        // The came line, only where it is inside the well.
        var started = false;
        for (var k = 0; k <= Columns; k++)
        {
            var x = -WellRadius + (2f * WellRadius * k / Columns);
            var y = WaveY(x + Unit) - Unit;
            if ((x * x) + (y * y) > (WellRadius - 0.5f) * (WellRadius - 0.5f))
            {
                continue;
            }

            dl.PathLineTo(c + (new Vector2(x, y) * u));
            started = true;
        }

        if (started)
        {
            dl.PathStroke(Theme.WithAlpha(MoonIconInks.Rim(FrameKitId.Came)[^1].Color, 0.9f), ImDrawFlags.None, 1.3f * u);
        }
    }

    /// <summary>The came line's height at <paramref name="x"/> in the box: two gentle curves, up on the left, down on the right.</summary>
    private static float WaveY(float x)
    {
        // The mock's "M5 41c9-3 18-3 27 0s18 3 27 0": each half rises (or falls) 2.25 units at its middle.
        if (x <= 32f)
        {
            var t = Math.Clamp((x - 5f) / 27f, 0f, 1f);
            return 41f - (9f * t * (1f - t));
        }

        var s = Math.Clamp((x - 32f) / 27f, 0f, 1f);
        return 41f + (9f * s * (1f - s));
    }

    /// <summary>Aether Crystal's four facet chips on the silver rim, the upper-left one catching the light.</summary>
    private static void FacetChips(ImDrawListPtr dl, Vector2 c, float u, InkStop[] silver)
    {
        var edge = Theme.U32(silver[^1].Color);
        var lit = Theme.U32(Vector4.Lerp(silver[0].Color, White, 0.6f));
        var rest = Theme.U32(Vector4.Lerp(silver[0].Color, silver[1].Color, 0.4f));
        for (var k = 0; k < 4; k++)
        {
            var degrees = 45f + (90f * k);
            var a = degrees * MathF.PI / 180f;
            var at = c + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * 30.2f * u);
            var s = 2.26f * u;
            var p1 = at + new Vector2(0f, -s);
            var p2 = at + new Vector2(s, 0f);
            var p3 = at + new Vector2(0f, s);
            var p4 = at + new Vector2(-s, 0f);
            dl.AddQuadFilled(p1, p2, p3, p4, k == 2 ? lit : rest);
            dl.AddQuad(p1, p2, p3, p4, edge, 0.4f * u);
        }
    }

    /// <summary>The well: a flat disc under a radial gradient lit from the upper left (the mock's 38 %, 32 %, 75 %).</summary>
    private static void WellDisc(ImDrawListPtr dl, Vector2 c, float r, InkStop[] stops)
    {
        dl.AddCircleFilled(c, r, Theme.U32(stops[1].Color), 0);
        var focus = c + (new Vector2(-0.12f, -0.18f) * 2f * r);
        RadialDisc(dl, c, r - 0.5f, focus, 1.5f * r, stops);
    }

    /// <summary>
    /// The crescent: the moon's disc less the dark disc offset to the lower right, as one strip between the lit limb and
    /// the terminator, coloured from <paramref name="high"/> at the upper left to <paramref name="low"/>.
    /// </summary>
    private static void Crescent(ImDrawListPtr dl, Vector2 c, float u, Vector4 high, Vector4 low)
    {
        var (p1, p2) = Cusps();
        var dir = Vector2.Normalize(ShadowOffset);

        // The lit limb runs the long way round the moon, away from the dark disc; the terminator the short way round
        // the dark disc, inside the moon.
        var (outerFrom, outerSweep) = Sweep(p1, p2, Vector2.Zero, -dir);
        var (innerFrom, innerSweep) = Sweep(p1 - ShadowOffset, p2 - ShadowOffset, Vector2.Zero, -dir);
        var uv = ImGui.GetFontTexUvWhitePixel();
        dl.PrimReserve(ArcPoints * 6, (ArcPoints + 1) * 2);
        var i0 = dl.VtxCurrentIdx;
        for (var k = 0; k <= ArcPoints; k++)
        {
            var t = (float)k / ArcPoints;
            var a = outerFrom + (outerSweep * t);
            var b = innerFrom + (innerSweep * t);
            var outer = new Vector2(MathF.Cos(a), MathF.Sin(a)) * MoonRadius;
            var inner = ShadowOffset + (new Vector2(MathF.Cos(b), MathF.Sin(b)) * ShadowRadius);
            dl.PrimWriteVtx(c + (outer * u), uv, MoonInk(outer, high, low));
            dl.PrimWriteVtx(c + (inner * u), uv, MoonInk(inner, high, low));
        }

        for (var k = 0; k < ArcPoints; k++)
        {
            var a = i0 + (uint)(k * 2);
            Quad(dl, a, a + 2, a + 3, a + 1);
        }

        // A soft edge along the lit limb, as the mesh itself is not anti-aliased.
        OuterLimb(dl, c, u, MoonRadius - 0.25f, Vector4.Lerp(high, low, 0.4f), 0.55f, 0.9f);
    }

    /// <summary>Where the moon's disc and the dark disc meet: the crescent's two horns.</summary>
    private static (Vector2 First, Vector2 Second) Cusps()
    {
        var d = ShadowOffset.Length();
        var dir = ShadowOffset / d;
        var along = ((MoonRadius * MoonRadius) - (ShadowRadius * ShadowRadius) + (d * d)) / (2f * d);
        var half = MathF.Sqrt(MathF.Max(0f, (MoonRadius * MoonRadius) - (along * along)));
        var perp = new Vector2(-dir.Y, dir.X);
        return ((dir * along) + (perp * half), (dir * along) - (perp * half));
    }

    /// <summary>A stroke along the crescent's lit limb.</summary>
    private static void OuterLimb(ImDrawListPtr dl, Vector2 c, float u, float radius, Vector4 ink, float alpha, float width)
    {
        var (p1, p2) = Cusps();
        var (from, sweep) = Sweep(p1, p2, Vector2.Zero, -Vector2.Normalize(ShadowOffset));
        dl.PathArcTo(c, radius * u, from, from + sweep, ArcPoints);
        dl.PathStroke(Theme.WithAlpha(ink, alpha), ImDrawFlags.None, width * u);
    }

    /// <summary>The start angle and signed sweep from <paramref name="from"/> to <paramref name="to"/> round <paramref name="centre"/> that passes <paramref name="through"/>.</summary>
    private static (float Start, float Sweep) Sweep(Vector2 from, Vector2 to, Vector2 centre, Vector2 through)
    {
        var a1 = MathF.Atan2(from.Y - centre.Y, from.X - centre.X);
        var a2 = MathF.Atan2(to.Y - centre.Y, to.X - centre.X);
        var mid = MathF.Atan2(through.Y, through.X);
        var forward = Wrap(a2 - a1);
        return Wrap(mid - a1) < forward ? (a1, forward) : (a1, forward - (2f * MathF.PI));
    }

    private static float Wrap(float angle)
    {
        var tau = 2f * MathF.PI;
        var a = angle % tau;
        return a < 0f ? a + tau : a;
    }

    private static uint MoonInk(Vector2 p, Vector4 high, Vector4 low)
    {
        // The mock's diagonal gradient across the moon's box: the high stop at 20 %, the low at the far corner.
        var t = ((p.X + p.Y) / (4f * MoonRadius)) + 0.5f;
        var k = Math.Clamp((t - 0.2f) / 0.8f, 0f, 1f);
        return Theme.U32(Vector4.Lerp(high, low, k));
    }

    private static void Particle(ImDrawListPtr dl, Vector2 c, float radius, float u, in IconParticle p)
    {
        var at = c + (new Vector2(p.X, p.Y) * radius);
        var size = p.Size * radius;
        switch (p.Kind)
        {
            case IconParticleKind.Mote:
                dl.AddCircleFilled(at, 3f * u, Theme.WithAlpha(MoonIconInks.MoteHalo, p.Alpha * 0.22f), 12);
                dl.AddCircleFilled(at, MathF.Max(1f, size), Theme.WithAlpha(MoonIconInks.Mote, p.Alpha), 8);
                break;
            case IconParticleKind.Star:
                dl.AddCircleFilled(at, size * 2.6f, Theme.WithAlpha(MoonIconInks.StarHalo, p.Alpha * 0.18f), 12);
                dl.AddCircleFilled(at, MathF.Max(0.75f, size), Theme.WithAlpha(MoonIconInks.Star, p.Alpha), 8);
                break;
            case IconParticleKind.Glint:
                dl.PathArcTo(c, IconParticles.GlintRadius * radius, p.Angle - IconParticles.GlintHalfSpan, p.Angle + IconParticles.GlintHalfSpan, 8);
                dl.PathStroke(Theme.WithAlpha(White, p.Alpha), ImDrawFlags.None, 1.5f * u);
                dl.AddCircleFilled(at, 2.8f * u, Theme.WithAlpha(MoonIconInks.FrostHalo, p.Alpha * 0.35f), 12);
                dl.AddCircleFilled(at, MathF.Max(0.75f, size), Theme.WithAlpha(White, p.Alpha), 8);
                break;
            case IconParticleKind.Shard:
                Shard(dl, at, u, p);
                break;
            case IconParticleKind.Bead:
                dl.AddCircleFilled(at, 3.4f * u, Theme.WithAlpha(MoonIconInks.Bead, p.Alpha * 0.3f), 12);
                dl.AddCircleFilled(at, MathF.Max(1f, size), Theme.WithAlpha(MoonIconInks.Bead, p.Alpha), 10);
                dl.AddCircleFilled(at - new Vector2(0.5f * u), MathF.Max(0.5f, 0.6f * u), Theme.WithAlpha(MoonIconInks.BeadHigh, p.Alpha), 6);
                break;
            case IconParticleKind.Fleck:
                Fleck(dl, at, u, p);
                break;
        }
    }

    /// <summary>A shard: a dark body and its lit upper-left facet, and when its face meets the light a flash of light (never a dark disc).</summary>
    private static void Shard(ImDrawListPtr dl, Vector2 at, float u, in IconParticle p)
    {
        var (sin, cos) = MathF.SinCos(p.Angle);
        Vector2 R(float x, float y) => at + (new Vector2((x * cos) - (y * sin), (x * sin) + (y * cos)) * u);
        var top = R(0f, -4.2f);
        var right = R(1.7f, 0f);
        var foot = R(0f, 3.2f);
        var left = R(-1.6f, -0.2f);
        if (p.Flash > 0.2f)
        {
            SoftDisc(dl, at, 0.8f * u, 3.4f * u, MoonIconInks.ShardFlash, MathF.Min(IconParticles.MaxAlpha, p.Flash * 0.8f) * (p.Alpha / IconParticles.MaxAlpha));
        }

        dl.AddQuadFilled(top, right, foot, left, Theme.WithAlpha(MoonIconInks.ShardBody, p.Alpha));
        dl.AddTriangleFilled(top, left, foot, Theme.WithAlpha(MoonIconInks.ShardLit, p.Alpha * (0.35f + (0.6f * p.Flash)) / 0.95f));
    }

    /// <summary>A gold-leaf fleck, tumbling (squashed as it turns), brighter when it tilts to the light.</summary>
    private static void Fleck(ImDrawListPtr dl, Vector2 at, float u, in IconParticle p)
    {
        var (sin, cos) = MathF.SinCos(p.Angle);
        var squash = Math.Clamp(p.Squash, 0.1f, 1f);
        Vector2 R(float x, float y)
        {
            var ry = y * squash;
            return at + (new Vector2((x * cos) - (ry * sin), (x * sin) + (ry * cos)) * u);
        }

        var ink = p.Flash > 0.4f ? MoonIconInks.LeafLit : MoonIconInks.LeafBody;
        dl.AddQuadFilled(R(-1.8f, -1.2f), R(1.6f, -1.5f), R(1.9f, 1.1f), R(-1.4f, 1.6f), Theme.WithAlpha(ink, p.Alpha));
    }

    /// <summary>The Orrery's orbit hairline: its far half before the face (which hides it), its near half after.</summary>
    private static void Orbit(ImDrawListPtr dl, Vector2 c, float radius, float u, bool back)
    {
        const int Points = 24;
        var from = back ? MathF.PI : 0f;
        for (var k = 0; k <= Points; k++)
        {
            var (x, y) = IconParticles.OrbitPoint(from + (MathF.PI * k / Points));
            dl.PathLineTo(c + (new Vector2(x, y) * radius));
        }

        dl.PathStroke(Theme.WithAlpha(MoonIconInks.Orbit, IconParticles.OrbitAlpha), ImDrawFlags.None, MathF.Max(0.75f, 0.6f * u));
    }

    /// <summary>The 8 px dot at the upper right with its 1.5 px Night ring.</summary>
    private static void Dot(ImDrawListPtr dl, Vector2 at, Vector4 ink, float scale)
    {
        var r = 4f * scale;
        dl.AddCircleFilled(at, r + (1.5f * scale), Theme.U32(MoonIconInks.DotRing), 16);
        dl.AddCircleFilled(at, r, Theme.U32(ink with { W = 1f }), 16);
    }

    private static void Dot(ImDrawListPtr dl, Vector2 c, float u, float x, float y, float r, Vector4 ink, float alpha) =>
        dl.AddCircleFilled(At(c, u, x, y), MathF.Max(0.5f, r * u), Theme.WithAlpha(ink, alpha), 6);

    /// <summary>A point of the mock's 64-unit box, on screen.</summary>
    private static Vector2 At(Vector2 c, float u, float x, float y) => c + (new Vector2(x - Unit, y - Unit) * u);

    private static void Polyline(ImDrawListPtr dl, Vector2 c, float u, uint ink, float width, params ReadOnlySpan<(float X, float Y)> points)
    {
        foreach (var (x, y) in points)
        {
            dl.PathLineTo(At(c, u, x, y));
        }

        dl.PathStroke(ink, ImDrawFlags.None, MathF.Max(0.5f, width * u));
    }

    /// <summary>
    /// A disc filled with a linear gradient from its box's upper-left corner (<paramref name="stops"/> at 0) to the
    /// lower-right one (at 1), as one mesh: a hub and two rings.
    /// </summary>
    private static void LinearDisc(ImDrawListPtr dl, Vector2 c, float r, InkStop[] stops)
    {
        var uv = ImGui.GetFontTexUvWhitePixel();
        dl.PrimReserve((Segments * 3) + (Segments * 6), 1 + (2 * Segments));
        var i0 = dl.VtxCurrentIdx;
        dl.PrimWriteVtx(c, uv, Theme.U32(Sample(stops, 0.5f)));
        for (var ring = 1; ring <= 2; ring++)
        {
            var rr = r * ring / 2f;
            for (var k = 0; k < Segments; k++)
            {
                var a = 2f * MathF.PI * k / Segments;
                var off = new Vector2(MathF.Cos(a), MathF.Sin(a)) * rr;
                // Along the disc's box diagonal, as SVG's (0,0) → (1,1): the box's corners are 0 and 1.
                var t = (((off.X + off.Y) / (2f * r)) + 1f) * 0.5f;
                dl.PrimWriteVtx(c + off, uv, Theme.U32(Sample(stops, t)));
            }
        }

        Fan(dl, i0);
    }

    /// <summary>A disc filled with a radial gradient round <paramref name="focus"/> over <paramref name="span"/> px, as one mesh.</summary>
    private static void RadialDisc(ImDrawListPtr dl, Vector2 c, float r, Vector2 focus, float span, InkStop[] stops)
    {
        var uv = ImGui.GetFontTexUvWhitePixel();
        dl.PrimReserve((Segments * 3) + (Segments * 6), 1 + (2 * Segments));
        var i0 = dl.VtxCurrentIdx;
        var inv = span > 0f ? 1f / span : 0f;
        dl.PrimWriteVtx(c, uv, Theme.U32(Sample(stops, Vector2.Distance(c, focus) * inv)));
        for (var ring = 1; ring <= 2; ring++)
        {
            var rr = r * ring / 2f;
            for (var k = 0; k < Segments; k++)
            {
                var a = 2f * MathF.PI * k / Segments;
                var p = c + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * rr);
                dl.PrimWriteVtx(p, uv, Theme.U32(Sample(stops, Vector2.Distance(p, focus) * inv)));
            }
        }

        Fan(dl, i0);
    }

    /// <summary>A soft disc: <paramref name="alpha"/> out to <paramref name="inner"/>, fading to nothing at <paramref name="outer"/>.</summary>
    private static void SoftDisc(ImDrawListPtr dl, Vector2 c, float inner, float outer, Vector4 ink, float alpha)
    {
        if (!(alpha > 0f) || !(outer > 0f))
        {
            return;
        }

        var solid = Theme.WithAlpha(ink, alpha);
        var clear = Theme.WithAlpha(ink, 0f);
        var uv = ImGui.GetFontTexUvWhitePixel();
        dl.PrimReserve((Segments * 3) + (Segments * 6), 1 + (2 * Segments));
        var i0 = dl.VtxCurrentIdx;
        dl.PrimWriteVtx(c, uv, solid);
        for (var ring = 1; ring <= 2; ring++)
        {
            var rr = ring == 1 ? MathF.Max(0f, MathF.Min(inner, outer)) : outer;
            for (var k = 0; k < Segments; k++)
            {
                var a = 2f * MathF.PI * k / Segments;
                dl.PrimWriteVtx(c + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * rr), uv, ring == 1 ? solid : clear);
            }
        }

        Fan(dl, i0);
    }

    /// <summary>The indices of a hub (at <paramref name="i0"/>) and two rings of <see cref="Segments"/> vertices.</summary>
    private static void Fan(ImDrawListPtr dl, uint i0)
    {
        for (var k = 0; k < Segments; k++)
        {
            var next = (uint)((k + 1) % Segments);
            var inner = i0 + 1;
            var outer = inner + Segments;
            Triangle(dl, i0, inner + (uint)k, inner + next);
            Quad(dl, inner + (uint)k, outer + (uint)k, outer + next, inner + next);
        }
    }

    private static void Triangle(ImDrawListPtr dl, uint a, uint b, uint c)
    {
        dl.PrimWriteIdx((ushort)a);
        dl.PrimWriteIdx((ushort)b);
        dl.PrimWriteIdx((ushort)c);
    }

    private static void Quad(ImDrawListPtr dl, uint a, uint b, uint c, uint d)
    {
        Triangle(dl, a, b, c);
        Triangle(dl, a, c, d);
    }

    /// <summary>The colour at <paramref name="t"/> along <paramref name="stops"/> (clamped to its ends).</summary>
    private static Vector4 Sample(InkStop[] stops, float t)
    {
        if (t <= stops[0].At)
        {
            return stops[0].Color;
        }

        for (var i = 1; i < stops.Length; i++)
        {
            if (t <= stops[i].At)
            {
                var a = stops[i - 1];
                var b = stops[i];
                return Vector4.Lerp(a.Color, b.Color, b.At > a.At ? (t - a.At) / (b.At - a.At) : 1f);
            }
        }

        return stops[^1].Color;
    }
}
