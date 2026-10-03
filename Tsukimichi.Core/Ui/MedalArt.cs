using System.Collections.Concurrent;
using System.Numerics;
using Tsukimichi.Core.Model;
using M = Tsukimichi.Core.Ui.GlyphTokens.Medallion;
using X = Tsukimichi.Core.Ui.GlyphTokens.MedallionDetail;

namespace Tsukimichi.Core.Ui;

/// <summary>What a medal's badge holds (concept.md §4–6): one frame, four contents.</summary>
public enum MedalBadge : byte
{
    None,

    /// <summary>Ready: an open padlock in warm gilt on deep lapis.</summary>
    Open,

    /// <summary>In journal: an open book in gilt on Tide silk.</summary>
    Journal,

    /// <summary>Blocked: a closed padlock in cool pewter on night enamel.</summary>
    Closed,

    /// <summary>Ready on another job: the game's job icon on its role's seat (drawn by the plugin).</summary>
    Job,
}

/// <summary>
/// "Menphina's Medallion" (feature plan v6 G1; docs/design/moon-v6/round5/medallion-r5/concept.md) as vector meshes:
/// the geometry table the procedural medals are built from, transcribed from the approved generator
/// (<c>medallion-r5/_src/gen5.py</c>) in its 128-unit box, and the builders that turn it into <see cref="MedalMesh"/>es.
///
/// <para>The meshes are the row tier — every medal under 32 px, drawn with no badge, the badge content going beside the
/// medal at text height (<see cref="RowGlyph"/>) — and they also stand in at hero sizes wherever the pre-rendered atlas
/// cannot: under high contrast (a token swap, <see cref="MedalTokens"/>), at Flair Plain, and while the atlas loads.
/// They keep the design's shapes, gradients and key-lit metal; the blurs (cast shadows, silver linings, the soft
/// crest glints) are left to the atlas, since at row size they are under a pixel.</para>
///
/// <para>Meshes are built on first use per state and palette and cached; nothing is built per frame.</para>
/// </summary>
public static class MedalArt
{
    /// <summary>The medal's centre in its 128-unit box.</summary>
    public static readonly Vector2 Center = new(64f, 64f);

    public const float KeylineRadius = 63.2f;
    public const float RimOuter = 61.5f;
    public const float RimCrest = 57.2f;
    public const float RimInner = 53.0f;
    public const float WellRadius = 52.4f;

    /// <summary>The badge frame (concept.md, "Job-badge frame spec"): centre, keyline, ring and seat radii, and the job icon's square.</summary>
    public static readonly Vector2 BadgeCenter = new(95f, 95f);

    public const float BadgeKeyline = 24f;
    public const float BadgeOuter = 23.1f;
    public const float BadgeCrest = 21.5f;
    public const float BadgeSeat = 19.9f;
    public const float JobIconSlot = 35.5f;

    /// <summary>The lock glyphs' shared body position, below the seat's centre (gen5 <c>LOCK_OY</c>).</summary>
    public const float LockOffsetY = -0.2f;

    /// <summary>Ready's crescent (also In journal's and Ready on another job's): centre, radius, terminator and tilt.</summary>
    public static readonly Vector2 SceneMoon = new(49f, 42f);

    public const float SceneMoonRadius = 29f;
    public const float SceneMoonTerminator = -0.18f;
    public const float SceneMoonTilt = 28f;

    /// <summary>The horizon of Ready's and In journal's scenes.</summary>
    public const float Horizon = 80f;

    /// <summary>The crescent's lit centroid (gen5 <c>GLYPH_LIT_C</c>): the road runs under it.</summary>
    public static readonly Vector2 SceneLitCentroid = new(61.823f, 48.820f);

    /// <summary>Where the sun has set, under the lit limb's direction: Ready's afterglow sits on the horizon here.</summary>
    public const float SunX = 120.468f;

    /// <summary>Blocked's new moon behind cloud, and its thin sunlit limb.</summary>
    public static readonly Vector2 BlockedMoon = new(72f, 44f);

    public const float BlockedMoonRadius = 25f;
    public const float BlockedTerminator = -0.62f;
    public const float BlockedTilt = -12f;

    /// <summary>Done this cycle's waning half moon (lit on the left), inside the repeat arrow.</summary>
    public static readonly Vector2 DoneMoon = new(64f, 64f);

    public const float DoneMoonRadius = 22.5f;

    /// <summary>The repeat arrow: a spiral from radius 33 at 132° to 39 at 385°, 8 wide.</summary>
    public const float ArrowInnerRadius = 33f;
    public const float ArrowOuterRadius = 39f;
    public const float ArrowWidth = 8f;

    /// <summary>Completed's full moon and its check, struck through the rim.</summary>
    public static readonly Vector2 CompletedMoon = new(60f, 60f);

    public const float CompletedMoonRadius = 35f;

    /// <summary>The check's polyline (round caps and join) and its widths: the keyline under the gilt.</summary>
    public static readonly Vector2[] Check = [new(64f, 86f), new(78f, 100f), new(117.5f, 40f)];

    public const float CheckKeylineWidth = 15f;
    public const float CheckWidth = 10f;

    /// <summary>Dalamud, shattered (Locked out): the disc the shards come from.</summary>
    public const float DalamudRadius = 37.5f;

    /// <summary>Not checked's veiled moon: the question mark's bowl is its lit limb.</summary>
    public static readonly Vector2 VeiledMoon = new(64f, 53f);

    public const float VeiledMoonRadius = 21f;

    /// <summary>The direction toward the key light (upper left), in screen space.</summary>
    public static readonly Vector2 Light = new(-0.6f, -0.8f);

    /// <summary>The share of segments the row tier's meshes are built with (<see cref="MeshBuilder(float, float)"/>).</summary>
    public const float RowDetail = 0.4f;

    /// <summary>How wide the keyline shows through the bezel's crest at row size, in device px (see <see cref="Frame"/>).</summary>
    public const float CrestSeamPx = 1f / 6f;

    /// <summary>The smallest row medal built for its own size; anything smaller is drawn with this one's mesh.</summary>
    public const int RowMinPx = 8;

    /// <summary>The medals by state, palette and drawn size: 0 for the hero mesh, a whole number of px for a row one.</summary>
    private static readonly ConcurrentDictionary<(QuestState, MedalTokens, int), MedalMesh> Medals = new();
    private static readonly ConcurrentDictionary<(MedalBadge, JobSeat, MedalTokens), MedalMesh> Badges = new();
    private static readonly ConcurrentDictionary<(MedalBadge, MedalTokens), MedalMesh> RowGlyphs = new();

    /// <summary>The badge a state's hero medal carries.</summary>
    public static MedalBadge BadgeOf(QuestState state) => state switch
    {
        QuestState.Ready => MedalBadge.Open,
        QuestState.ReadyOnOtherJob => MedalBadge.Job,
        QuestState.Accepted => MedalBadge.Journal,
        QuestState.Blocked => MedalBadge.Closed,
        _ => MedalBadge.None,
    };

    /// <summary>
    /// The medal of <paramref name="state"/> without its badge (the row tier, and the ground a hero badge sits on), to
    /// draw <paramref name="sizePx"/> device px across. Under <see cref="MedalLayout.RowTierMaxPx"/> it is a row mesh
    /// built for that whole-pixel size, with <see cref="RowDetail"/> of the segments and its sub-pixel detail drawn as
    /// coverage (<see cref="MeshBuilder(float, float)"/>); otherwise (and for 0) the one hero mesh, for any size.
    /// </summary>
    public static MedalMesh Medal(QuestState state, MedalTokens tokens, float sizePx = 0f)
    {
        if (!Enum.IsDefined(state))
        {
            state = QuestState.Unknown;
        }

        var px = sizePx > 0f && sizePx < MedalLayout.RowTierMaxPx
            ? Math.Clamp((int)MathF.Round(sizePx), RowMinPx, (int)MedalLayout.RowTierMaxPx - 1)
            : 0;
        return Medals.GetOrAdd((state, tokens, px), static key => BuildMedal(key.Item1, key.Item2, key.Item3));
    }

    /// <summary>The badge at hero size: its shadow, keyline, ring and seat, and the lock or book (a job's icon is the plugin's).</summary>
    public static MedalMesh Badge(MedalBadge badge, JobSeat seat, MedalTokens tokens) =>
        Badges.GetOrAdd((badge, badge == MedalBadge.Job ? seat : JobSeat.Hand, tokens), static key => BuildBadge(key.Item1, key.Item2, key.Item3));

    /// <summary>
    /// A badge's content alone, for the row fallback beside a medal under 32 px (round 5 <c>_row/badge-*.svg</c>): built
    /// in the same 128-unit box as the medals (the glyph's 34-unit box scaled to it), so it draws the same way.
    /// Empty for <see cref="MedalBadge.None"/> and <see cref="MedalBadge.Job"/>.
    /// </summary>
    public static MedalMesh RowGlyph(MedalBadge badge, MedalTokens tokens) =>
        RowGlyphs.GetOrAdd((badge, tokens), static key => BuildRowGlyph(key.Item1, key.Item2));

    // ------------------------------------------------------------------ geometry the tests read

    /// <summary>The lit region of a crescent or half moon as one outline (terminator then limb), in medal units.</summary>
    public static List<Vector2> PhaseOutline(Vector2 c, float r, float k, float tiltDegrees, bool litLeft = false, int rows = 24)
    {
        var (terminator, limb) = PhaseEdges(r, k, rows);
        var m = PhaseMatrix(c, tiltDegrees, litLeft);
        var loop = new List<Vector2>(2 * terminator.Count);
        loop.AddRange(terminator.Select(p => Vector2.Transform(c + p, m)));
        for (var i = limb.Count - 1; i >= 0; i--)
        {
            loop.Add(Vector2.Transform(c + limb[i], m));
        }

        return loop;
    }

    /// <summary>The repeat arrow's outline (tapered tail, spiral band, head), gen5 <c>repeat_arrow</c>.</summary>
    public static List<Vector2> RepeatArrow(int n = 60)
    {
        float r0 = ArrowInnerRadius, r1 = ArrowOuterRadius, a0 = 132f, a1 = 385f, w = ArrowWidth, headLength = 13.5f, headWidth = 17.5f;
        var outer = new List<Vector2>(n + 1);
        var inner = new List<Vector2>(n + 1);
        for (var i = 0; i <= n; i++)
        {
            var t = (float)i / n;
            var a = a0 + (a1 - a0) * t;
            var rm = r0 + (r1 - r0) * t;
            var hw = w / 2f * (0.12f + 0.88f * MathF.Pow(MathF.Min(t / 0.42f, 1f), 0.8f));
            outer.Add(Center + MeshBuilder.Dir(a) * (rm + hw));
            inner.Add(Center + MeshBuilder.Dir(a) * (rm - hw));
        }

        var da = headLength / r1 * 180f / MathF.PI;
        var loop = new List<Vector2>(outer);
        loop.Add(Center + MeshBuilder.Dir(a1) * (r1 + headWidth / 2f));
        loop.Add(Center + MeshBuilder.Dir(a1 + da * 0.55f) * (r1 + 0.4f));
        loop.Add(Center + MeshBuilder.Dir(a1 + da) * r1);
        loop.Add(Center + MeshBuilder.Dir(a1 + da * 0.55f) * (r1 - 0.4f));
        loop.Add(Center + MeshBuilder.Dir(a1) * (r1 - headWidth / 2f));
        for (var i = inner.Count - 1; i >= 0; i--)
        {
            loop.Add(inner[i]);
        }

        return loop;
    }

    // ------------------------------------------------------------------ the medals

    private static MedalMesh BuildMedal(QuestState state, MedalTokens t, int rowPx)
    {
        var b = rowPx > 0 ? new MeshBuilder(RowDetail, rowPx) : new MeshBuilder();
        switch (state)
        {
            case QuestState.Ready:
                Scene(b, t, night: false);
                Frame(b, t);
                break;

            case QuestState.Accepted:
                Scene(b, t, night: true);
                Frame(b, t);
                Ribbon(b, t);
                break;

            case QuestState.ReadyOnOtherJob:
                RestingWell(b, t);
                if (!t.Flat)
                {
                    b.Disc(SceneMoon, SceneMoonRadius, MeshBuilder.Solid(X.RestingEarthshine, 0.3f));
                }

                SceneCrescent(b, t, night: false, state);
                Frame(b, t);
                break;

            case QuestState.Blocked:
                RestingWell(b, t);
                Blocked(b, t);
                Frame(b, t);
                break;

            case QuestState.DoneThisCycle:
                RestingWell(b, t);
                Done(b, t);
                Frame(b, t);
                break;

            case QuestState.Completed:
                RestingWell(b, t);
                Completed(b, t);
                Frame(b, t);
                CompletedCheck(b, t);
                break;

            case QuestState.Foreclosed:
                RestingWell(b, t);
                LockedOut(b, t);
                Frame(b, t);
                break;

            default:
                RestingWell(b, t);
                NotChecked(b, t);
                Frame(b, t);
                break;
        }

        return b.Build();
    }

    /// <summary>The resting enamel well (every state but Ready and In journal), with its sheen and the rim's inner shadow.</summary>
    private static void RestingWell(MeshBuilder b, MedalTokens t)
    {
        if (t.Flat)
        {
            b.Disc(Center, KeylineRadius, MeshBuilder.Solid(t.Ground), segments: 48, rings: 1);
            return;
        }

        var enamel = MeshBuilder.Linear(new Vector2(0f, 0f), new Vector2(0f, 128f), (0f, M.Enamel), (1f, M.EnamelDeep));
        b.Disc(Center, RimInner + 0.6f, MeshBuilder.Over(Sheen, enamel), segments: 48, rings: 2, aa: false);
        InnerShadow(b, Center, WellRadius, new Vector2(2.6f, 3.6f), 1.3f, 0.55f, [44f, 47.5f, 50f, 51.5f, 52.6f]);
    }

    /// <summary>The faint vitreous sheen toward the light, over every well.</summary>
    private static readonly Paint Sheen = MeshBuilder.Radial(new Vector2(44f, 40f), 40f, (0f, new Vector4(1f, 1f, 1f, 0.09f)), (1f, new Vector4(1f, 1f, 1f, 0f)));

    /// <summary>
    /// The soft shadow a raised rim casts on the upper-left inner edge of a recess (the well, a badge seat): Abyss where
    /// the recess is not covered by itself moved by <paramref name="offset"/>, blurred by <paramref name="blur"/>.
    /// </summary>
    private static void InnerShadow(MeshBuilder b, Vector2 center, float radius, Vector2 offset, float blur, float alpha, float[] radii)
    {
        var o = center + offset;
        Paint paint = p => MeshBuilder.Alpha(M.Keyline, alpha * MeshBuilder.Smooth(-blur, blur, Vector2.Distance(p, o) - radius));
        b.Band(center, radii, [paint], segments: 72, aa: false);
    }

    /// <summary>The keyline and the gilt bezel all eight medals share (gen5 <c>bezel</c>): two slopes lit from the upper left, a crest seam and specular streaks.</summary>
    private static void Frame(MeshBuilder b, MedalTokens t)
    {
        if (t.Flat)
        {
            b.Band(Center, [RimInner, RimOuter], [MeshBuilder.Solid(t.Rim)], segments: 64);
            return;
        }

        b.Annulus(Center, RimInner - 0.9f, KeylineRadius, MeshBuilder.Solid(M.Keyline, 0.92f), segments: 64);
        var from = new Vector2(14f, 10f);
        var to = new Vector2(114f, 118f);
        var outer = MeshBuilder.Linear(from, to, (0f, M.GiltHigh), (0.42f, M.GiltMid), (0.78f, M.GiltShade), (1f, M.GiltDeep));
        var inner = MeshBuilder.Linear(from, to, (0f, M.GiltDeep), (0.55f, M.GiltShade), (1f, M.Gilt));
        b.Band(Center, [RimInner, RimCrest, RimCrest, RimOuter], [inner, inner, outer, outer], segments: 72);

        Fine(b, 12f);
        TaperArc(b, Center, RimCrest + 2.3f, -168f, -102f, 2.4f, MeshBuilder.Solid(M.GiltSpecular, 0.95f));
        Fine(b, 24f);
        b.Annulus(Center, RimCrest - 0.25f, RimCrest + 0.25f, MeshBuilder.Solid(M.GiltDark, 0.45f), segments: 72);
        TaperArc(b, Center, RimInner + 1.9f, 18f, 52f, 1.6f, MeshBuilder.Solid(M.GiltSpecular, 0.45f));
        b.Detail(0f);

        // Row size only, where both are drawn as coverage: the outer slope's lip (gen5's ring at R_OUT − 1.2..0.65,
        // under a pixel at any size), and the keyline showing through the crest. The approved row art draws the two
        // slopes as separate fills over the keyline, so where they meet within a pixel the keyline shows between
        // them: a sixth of a pixel of it on average, at every size.
        if (b.Coverage)
        {
            b.Annulus(Center, RimOuter - 1.2f, RimOuter - 0.65f, MeshBuilder.Solid(M.GiltDark, 0.35f), segments: 72);
            var w = CrestSeamPx / b.PixelsPerUnit;
            b.Annulus(Center, RimCrest - w / 2f, RimCrest + w / 2f, MeshBuilder.Solid(M.Keyline, 0.92f), segments: 72);
        }
    }

    /// <summary>A crescent-shaped band along a circle from <paramref name="from"/> to <paramref name="to"/> degrees, width 0 → w → 0 (gen5 <c>taper_arc</c>).</summary>
    private static void TaperArc(MeshBuilder b, Vector2 c, float rm, float from, float to, float w, Paint paint, int n = 16)
    {
        var outer = new List<Vector2>(n + 1);
        var inner = new List<Vector2>(n + 1);
        for (var i = 0; i <= n; i++)
        {
            var t = (float)i / n;
            var a = from + (to - from) * t;
            var hw = w / 2f * MathF.Sin(MathF.PI * t);
            outer.Add(c + MeshBuilder.Dir(a) * (rm + hw));
            inner.Add(c + MeshBuilder.Dir(a) * (rm - hw));
        }

        b.Strip(outer, inner, paint);
    }

    // ------------------------------------------------------------------ Ready and In journal: the moon road scene

    private static readonly (float Y, float Height, (float X0, float X1, float Opacity, float HeightScale)[] Dashes)[] RoadRows =
    [
        (81.5f, 1.2f, [(-4.5f, 4.0f, .46f, 1f)]),
        (84.0f, 1.6f, [(-7.5f, 4.5f, .52f, 1f), (7.5f, 13.5f, .40f, .7f)]),
        (87.4f, 2.0f, [(-10.5f, -1.0f, .56f, 1f), (2.0f, 10.5f, .44f, .8f)]),
        (91.6f, 2.5f, [(-10.0f, 10.0f, .62f, 1f)]),
        (96.4f, 3.0f, [(-15.0f, 0.5f, .66f, 1f), (4.0f, 14.0f, .52f, .6f)]),
        (101.8f, 3.5f, [(-13.0f, 13.0f, .72f, 1f)]),
        (107.7f, 4.0f, [(-24.0f, -14.5f, .46f, .45f), (-11.0f, 9.0f, .76f, 1f), (12.0f, 23.0f, .58f, .6f)]),
        (113.8f, 4.5f, [(-15.5f, 15.5f, .74f, 1f)]),
    ];

    /// <summary>Ready's lit lapis scene, or In journal's quieter night (gen5 <c>moon_scene</c>): sky over sea, the crescent and its road.</summary>
    private static void Scene(MeshBuilder b, MedalTokens t, bool night)
    {
        var state = night ? QuestState.Accepted : QuestState.Ready;
        if (t.Flat)
        {
            b.Disc(Center, KeylineRadius, MeshBuilder.Solid(t.Ground), segments: 48, rings: 1);
            SceneCrescent(b, t, night, state);
            var ink = t.Ink(state);
            b.Polygon(Rect(14f, Horizon - 1f, 114f, Horizon + 1f), MeshBuilder.Solid(ink));
            Road(b, MeshBuilder.Solid(ink), opacity: false);
            return;
        }

        var (skyTop, skyHorizon, seaHorizon, seaBottom) = night
            ? (M.NightSkyTop, X.JournalSkyHorizon, X.JournalSeaHorizon, X.JournalSeaBottom)
            : (M.LapisSkyTop, M.LapisSkyHorizon, M.LapisSeaHorizon, M.LapisSeaBottom);
        var moonGlow = night ? 0.12f : 0.26f;

        Paint sky = MeshBuilder.Linear(new Vector2(0f, 12f), new Vector2(0f, Horizon), (0f, skyTop), (1f, skyHorizon));
        if (!night)
        {
            var afterglow = MeshBuilder.Radial(new Vector2(SunX, Horizon), new Vector2(52f, 30f),
                (0f, MeshBuilder.Alpha(M.Afterglow, 0.55f)), (0.55f, MeshBuilder.Alpha(M.Afterglow, 0.16f)), (1f, MeshBuilder.Alpha(M.Afterglow, 0f)));
            sky = MeshBuilder.Over(afterglow, sky);
        }

        var bloom = MeshBuilder.Radial(SceneLitCentroid, 40f, (0f, MeshBuilder.Alpha(M.MoonstoneHigh, moonGlow)), (1f, MeshBuilder.Alpha(M.MoonstoneHigh, 0f)));
        sky = MeshBuilder.Over(Sheen, MeshBuilder.Over(bloom, sky));

        Paint sea = MeshBuilder.Linear(new Vector2(0f, Horizon), new Vector2(0f, 117f), (0f, seaHorizon), (1f, seaBottom));
        var fresnel = MeshBuilder.Linear(new Vector2(0f, Horizon), new Vector2(0f, Horizon + 12f),
            (0f, MeshBuilder.Alpha(skyHorizon, night ? 0.3f : 0.55f)), (1f, MeshBuilder.Alpha(skyHorizon, 0f)));
        sea = MeshBuilder.Over(fresnel, sea);
        if (!night)
        {
            var reflection = MeshBuilder.Radial(new Vector2(SunX, Horizon), new Vector2(40f, 9f),
                (0f, MeshBuilder.Alpha(M.Afterglow, 0.3f)), (1f, MeshBuilder.Alpha(M.Afterglow, 0f)));
            sea = MeshBuilder.Over(reflection, sea);
        }

        var column = MeshBuilder.Radial(new Vector2(SceneLitCentroid.X, 118f), new Vector2(22f, 40f),
            (0f, MeshBuilder.Alpha(M.MoonstoneHigh, moonGlow)), (1f, MeshBuilder.Alpha(M.MoonstoneHigh, 0f)));
        sea = MeshBuilder.Over(Sheen, MeshBuilder.Over(column, sea));

        // Rows at even angles round the well (so its outline stays under the rim), plus the gradient stops and a break at the horizon.
        var radius = RimInner + 0.6f;
        var rows = new List<float>();
        var bands = b.Segments(16);
        for (var i = 0; i <= bands; i++)
        {
            rows.Add(Center.Y - radius * MathF.Cos(MathF.PI * i / bands));
        }

        rows.AddRange([12f, Horizon, Horizon, Horizon + 4f, Horizon + 8f, Horizon + 12f, 117f]);
        rows.Sort();
        b.Rows(Center, radius, rows, sky, sea, Horizon, columns: 18);

        if (night)
        {
            b.Disc(SceneMoon, SceneMoonRadius, MeshBuilder.Solid(X.JournalEarthshine, 0.3f));
        }

        SceneCrescent(b, t, night, state);

        // The horizon: a fine moonlit line, brightest where the road meets it.
        Fine(b, 20f);
        var at = (SceneLitCentroid.X - 12f) / 104f;
        var line = MeshBuilder.Linear(new Vector2(12f, 0f), new Vector2(116f, 0f),
            (0f, MeshBuilder.Alpha(M.MoonstoneHigh, 0.06f)), (at, MeshBuilder.Alpha(M.MoonstoneHigh, night ? 0.4f : 0.7f)), (1f, MeshBuilder.Alpha(M.MoonstoneHigh, 0.06f)));
        b.Strip(HorizontalLine(14f, 114f, Horizon - 0.5f, 10), HorizontalLine(14f, 114f, Horizon + 0.5f, 10), line);
        Fine(b, 14f);
        Road(b, MeshBuilder.Solid(night ? M.Moonstone : M.MoonstoneHigh), opacity: true);
        b.Detail(0f);
        InnerShadow(b, Center, WellRadius, new Vector2(2.6f, 3.6f), 1.3f, 0.55f, [44f, 47.5f, 50f, 51.5f, 52.6f]);
    }

    /// <summary>The crescent shared by Ready, In journal (dimmed) and Ready on another job.</summary>
    private static void SceneCrescent(MeshBuilder b, MedalTokens t, bool night, QuestState state)
    {
        if (t.Flat)
        {
            b.Polygon(PhaseOutline(SceneMoon, SceneMoonRadius, SceneMoonTerminator, SceneMoonTilt, rows: b.Segments(24)), MeshBuilder.Solid(t.Ink(state)));
            return;
        }

        if (night)
        {
            Crescent(b, SceneMoon, SceneMoonRadius, SceneMoonTerminator, SceneMoonTilt, false, M.MoonstoneMid, M.Moonstone, M.MoonstoneHigh, M.MoonstoneDeep, M.MoonstoneHigh);
        }
        else
        {
            Crescent(b, SceneMoon, SceneMoonRadius, SceneMoonTerminator, SceneMoonTilt, false, M.Moonstone, M.MoonstoneHigh, M.MoonstoneSpecular, M.MoonstoneMid, M.MoonstoneSpecular);
        }
    }

    /// <summary>The road: ripple streaks under the lit centroid, foreshortened toward the horizon (gen5 <c>GLYPH_ROWS</c>, <c>streak</c>).</summary>
    private static void Road(MeshBuilder b, Paint paint, bool opacity)
    {
        foreach (var (y, height, dashes) in RoadRows)
        {
            foreach (var (x0, x1, o, hs) in dashes)
            {
                var (top, bottom) = Streak(SceneLitCentroid.X + x0, SceneLitCentroid.X + x1, y, height * hs, n: b.Segments(16) / 2);
                if (opacity)
                {
                    var c = paint(default);
                    b.Strip(top, bottom, MeshBuilder.Solid(c, o));
                }
                else
                {
                    b.Strip(top, bottom, paint);
                }
            }
        }
    }

    /// <summary>A ripple streak: a needle of light, flat through the middle, drawn to fine points, its lower edge flatter.</summary>
    private static (List<Vector2> Top, List<Vector2> Bottom) Streak(float x0, float x1, float yc, float h, float skew = 0.44f, int n = 8)
    {
        var top = new List<Vector2>(n + 1);
        var bottom = new List<Vector2>(n + 1);
        var e = MathF.Log(0.5f) / MathF.Log(skew);
        for (var i = 0; i <= n; i++)
        {
            var t = (float)i / n;
            var u = 2f * MathF.Pow(t, e) - 1f;
            var profile = MathF.Pow(1f - MathF.Pow(MathF.Abs(u), 3.2f), 1.1f);
            var x = x0 + (x1 - x0) * t;
            top.Add(new Vector2(x, yc - h * 0.56f * profile));
            bottom.Add(new Vector2(x, yc + h * 0.44f * profile));
        }

        return (top, bottom);
    }

    /// <summary>In journal's Tide-silk ribbon, wrapped over the rim and dropping into the well (gen5 <c>in_journal</c>).</summary>
    private static void Ribbon(MeshBuilder b, MedalTokens t)
    {
        const float x0 = 24.5f;
        const float w = 17.5f;
        const float y1 = 76f;
        var aLeft = MathF.Atan2(64f - MathF.Sqrt(66f * 66f - (64f - x0) * (64f - x0)) - 64f, x0 - 64f) * 180f / MathF.PI;
        var aRight = MathF.Atan2(64f - MathF.Sqrt(66f * 66f - (64f - x0 - w) * (64f - x0 - w)) - 64f, x0 + w - 64f) * 180f / MathF.PI;
        var loop = MeshBuilder.Arc(Center, 66f, aLeft, aRight, 8).ToList();
        loop.Add(new Vector2(x0 + w, y1));
        loop.Add(new Vector2(x0 + w / 2f, y1 - 7f));
        loop.Add(new Vector2(x0, y1));

        if (t.Flat)
        {
            b.Polygon(MeshBuilder.Grow(loop, 1.6f), MeshBuilder.Solid(t.Ground));
            b.Polygon(loop, MeshBuilder.Solid(t.Bright));
            return;
        }

        b.Polygon(MeshBuilder.Grow(loop, 1.1f), MeshBuilder.Solid(M.Keyline));
        b.Polygon(loop, MeshBuilder.Linear(new Vector2(x0, 0f), new Vector2(x0 + w, 0f), (0f, M.RibbonHigh), (0.3f, M.Ribbon), (1f, M.RibbonDeep)));

        // Where the silk lies over the rim: its fold outside the keyline, the crest's highlight and the inner slope's shade.
        RibbonBand(b, KeylineRadius, 66f, x0, w, MeshBuilder.Solid(M.RibbonDeep));
        RibbonBand(b, 58.5f, 60.5f, x0, w, MeshBuilder.Solid(M.RibbonHigh, 0.5f));
        RibbonBand(b, RimInner, 56f, x0, w, MeshBuilder.Solid(M.Keyline, 0.3f));
    }

    /// <summary>The part of an annulus over the ribbon's width (its top, where both lie across the rim).</summary>
    private static void RibbonBand(MeshBuilder b, float inner, float outer, float x0, float w, Paint paint)
    {
        var mid = (inner + outer) / 2f;
        var from = -MathF.Acos((x0 - 64f) / mid) * 180f / MathF.PI;
        var to = -MathF.Acos((x0 + w - 64f) / mid) * 180f / MathF.PI;
        b.Annulus(Center, inner, outer, paint, from, to, segments: 8, aa: false);
    }

    // ------------------------------------------------------------------ Blocked: clouds drift across a new moon

    private static readonly (Vector2 Center, float Radius)[] BackBumps =
        [(new(45f, 34f), 6.5f), (new(55.5f, 28.5f), 9f), (new(66.5f, 31f), 8f), (new(77f, 34.5f), 6f)];

    private static readonly (float X0, float X1, float Top, float Bottom) BackBase = (38f, 84f, 31f, 41f);

    private static readonly (Vector2 Center, float Radius)[] FrontBumps =
        [(new(24f, 70f), 9f), (new(37f, 60f), 12.5f), (new(53f, 55f), 13.5f), (new(68.5f, 58f), 11.5f), (new(81f, 64f), 9f), (new(91.5f, 69.5f), 6.5f)];

    private static readonly (float X0, float X1, float Top, float Bottom) FrontBase = (15f, 98f, 62f, 78f);

    private static void Blocked(MeshBuilder b, MedalTokens t)
    {
        if (t.Flat)
        {
            b.Polygon(PhaseOutline(BlockedMoon, BlockedMoonRadius, BlockedTerminator, BlockedTilt), MeshBuilder.Solid(t.Rim));
            Cloud(b, t, BackBumps, BackBase, 1f);
            Cloud(b, t, FrontBumps, FrontBase, 1f);
            return;
        }

        b.Disc(BlockedMoon, BlockedMoonRadius, MeshBuilder.Radial(BlockedMoon - new Vector2(8f, 9f), BlockedMoonRadius * 1.5f, (0f, X.Ashen), (1f, X.AshenDeep)));
        Crescent(b, BlockedMoon, BlockedMoonRadius, BlockedTerminator, BlockedTilt, false, M.MoonstoneMid, M.Moonstone, M.MoonstoneHigh, M.MoonstoneDeep, M.MoonstoneHigh);
        Cloud(b, t, BackBumps, BackBase, 0.9f);
        Cloud(b, t, FrontBumps, FrontBase, 1f);
    }

    /// <summary>
    /// A cumulus in relief (gen5 <c>cloud</c>): billows on a rounded base bar, lighter on top, each billow rounded by its
    /// own highlight, a lit rim where the outline faces the light and a shaded rim where it faces away. The highlights
    /// and lit rims are held at 75 % (a new moon is the only light).
    /// </summary>
    private static void Cloud(MeshBuilder b, MedalTokens t, (Vector2 Center, float Radius)[] bumps, (float X0, float X1, float Top, float Bottom) bar, float tone)
    {
        const float lit = 0.75f;
        var top = bumps.Min(static p => p.Center.Y - p.Radius);
        var body = t.Flat
            ? MeshBuilder.Solid(t.Bright)
            : MeshBuilder.Linear(new Vector2(0f, top), new Vector2(0f, bar.Bottom), (0f, M.Cloud), (0.7f, M.CloudShade), (1f, M.CloudDeep));
        var stadium = Stadium(bar.X0, bar.X1, bar.Top, bar.Bottom);
        if (t.Flat)
        {
            // The ground keyline round the union, so the bright cloud stands off the bright moon limb.
            foreach (var (c, r) in bumps)
            {
                b.Disc(c, r + 1.6f, MeshBuilder.Solid(t.Ground), segments: 32, rings: 1);
            }

            b.Polygon(MeshBuilder.Grow(stadium, 1.6f), MeshBuilder.Solid(t.Ground));
        }

        b.Polygon(stadium, body);
        foreach (var (c, r) in bumps)
        {
            b.Disc(c, r, body, segments: 32, rings: 2);
        }

        if (t.Flat)
        {
            return;
        }

        foreach (var (c, r) in bumps)
        {
            var highlight = MeshBuilder.Radial(c - new Vector2(0.35f * r, 0.42f * r), 1.05f * r,
                (0f, MeshBuilder.Alpha(M.CloudHigh, 0.62f * tone * lit)), (1f, MeshBuilder.Alpha(M.CloudHigh, 0f)));
            b.Disc(c, r, highlight, segments: 32, rings: 3, aa: false);
        }

        // The rims, only where an outline is the union's own (not buried in another billow or the bar).
        bool Inside(Vector2 p, int self)
        {
            for (var i = 0; i < bumps.Length; i++)
            {
                if (i != self && Vector2.Distance(p, bumps[i].Center) < bumps[i].Radius - 0.05f)
                {
                    return true;
                }
            }

            return self != -1 && p.X > bar.X0 + 0.05f && p.X < bar.X1 - 0.05f && p.Y > bar.Top + 0.05f && p.Y < bar.Bottom - 0.05f;
        }

        Fine(b, 24f);
        Rims(b, bumps, Inside, new Vector2(-1.4f, -1.9f), MeshBuilder.Solid(M.CloudDeep, 0.75f));
        Rims(b, bumps, Inside, new Vector2(0.75f, 1f), MeshBuilder.Solid(M.CloudHigh, 0.7f * lit));
        b.Detail(0f);
    }

    /// <summary>
    /// The band a shape minus itself moved by <paramref name="shift"/> leaves on its billows' outlines (an SVG mask rim):
    /// as deep as the outline faces away from the shift, drawn only along outline the union owns.
    /// </summary>
    private static void Rims(MeshBuilder b, (Vector2 Center, float Radius)[] bumps, Func<Vector2, int, bool> inside, Vector2 shift, Paint paint)
    {
        for (var i = 0; i < bumps.Length; i++)
        {
            var (c, r) = bumps[i];
            var outer = new List<Vector2>();
            var inner = new List<Vector2>();
            for (var k = 0; k <= 90; k++)
            {
                var n = MeshBuilder.Dir(k * 4f);
                var p = c + n * r;
                var depth = Vector2.Dot(n, -shift);
                if (depth > 0.05f && !inside(p, i))
                {
                    outer.Add(p);
                    inner.Add(p - n * depth);
                    continue;
                }

                if (outer.Count >= 2)
                {
                    b.Strip(outer, inner, paint);
                }

                outer.Clear();
                inner.Clear();
            }

            if (outer.Count >= 2)
            {
                b.Strip(outer, inner, paint);
            }
        }
    }

    // ------------------------------------------------------------------ Done this cycle

    private static void Done(MeshBuilder b, MedalTokens t)
    {
        var arrow = RepeatArrow();
        if (t.Flat)
        {
            b.Polygon(PhaseOutline(DoneMoon, DoneMoonRadius, 0f, 0f, litLeft: true), MeshBuilder.Solid(t.Ink(QuestState.DoneThisCycle)));
            b.Polygon(MeshBuilder.Grow(arrow, 1.2f), MeshBuilder.Solid(t.Ground));
            b.Polygon(arrow, MeshBuilder.Solid(t.Bright));
            return;
        }

        b.Disc(DoneMoon, DoneMoonRadius, MeshBuilder.Solid(X.HalfMoonDark));

        // The lit half's gradient is in the half's own (unmirrored) space, as gen5's userSpaceOnUse gradient is.
        var body = MeshBuilder.Linear(new Vector2(41.5f, 41.5f), new Vector2(64f, 86.5f), (0f, M.MoonstoneSpecular), (0.55f, M.MoonstoneHigh), (1f, M.Moonstone));
        PhaseStrip(b, DoneMoon, DoneMoonRadius, 0f, 0f, litLeft: true, body, M.MoonstoneMid, 0.85f, 1.2f + 0.7f, M.MoonstoneSpecular, 0.9f, 1.8f);

        b.Polygon(MeshBuilder.Grow(arrow, 0.65f), MeshBuilder.Solid(M.Keyline));
        b.Polygon(arrow, MeshBuilder.Linear(new Vector2(20f, 18f), new Vector2(104f, 106f), (0f, M.GiltHigh), (0.5f, M.Gilt), (1f, M.GiltMid)));

        // Bevels riding the spiral: the outer edge lit where it faces up-left, the inner edge lit on the right.
        Fine(b, 24f);
        SpiralTaper(b, ArrowWidth / 2f - 0.5f, 175f, 268f, 1.6f, MeshBuilder.Solid(M.GiltSpecular, 0.9f));
        SpiralTaper(b, -ArrowWidth / 2f + 0.4f, 320f, 384f, 1.1f, MeshBuilder.Solid(M.GiltSpecular, 0.4f));
        SpiralTaper(b, -ArrowWidth / 2f + 0.5f, 175f, 268f, 1.4f, MeshBuilder.Solid(M.GiltDeep, 0.55f));
        b.Detail(0f);
    }

    private static void SpiralTaper(MeshBuilder b, float offset, float from, float to, float w, Paint paint, int n = 24)
    {
        float r0 = ArrowInnerRadius, r1 = ArrowOuterRadius, a0 = 132f, a1 = 385f;
        var outer = new List<Vector2>(n + 1);
        var inner = new List<Vector2>(n + 1);
        for (var i = 0; i <= n; i++)
        {
            var t = (float)i / n;
            var a = from + (to - from) * t;
            var rm = r0 + (r1 - r0) * (a - a0) / (a1 - a0) + offset;
            var hw = w / 2f * MathF.Sin(MathF.PI * t);
            outer.Add(Center + MeshBuilder.Dir(a) * (rm + hw));
            inner.Add(Center + MeshBuilder.Dir(a) * (rm - hw));
        }

        b.Strip(outer, inner, paint);
    }

    // ------------------------------------------------------------------ Completed

    private static readonly (float X, float Y, float Rx, float Ry, float Degrees, float Opacity)[] FullMaria =
    [
        (-.30f, -.36f, .30f, .22f, -18f, .36f), (.12f, -.30f, .17f, .15f, 0f, .34f), (.28f, -.02f, .22f, .17f, 20f, .32f),
        (.70f, -.18f, .10f, .08f, 0f, .36f), (.56f, .24f, .10f, .17f, -15f, .28f), (.26f, .36f, .09f, .10f, 0f, .24f),
        (-.22f, .30f, .17f, .13f, 10f, .24f), (-.58f, -.02f, .22f, .36f, 8f, .26f),
    ];

    private static void Completed(MeshBuilder b, MedalTokens t)
    {
        var c = CompletedMoon;
        var r = CompletedMoonRadius;
        if (t.Flat)
        {
            b.Disc(c, r, MeshBuilder.Solid(t.Ink(QuestState.Completed)));
            return;
        }

        b.Disc(c, r, MeshBuilder.Radial(c - new Vector2(0.38f * r, 0.42f * r), 1.55f * r, (0f, X.FullMoon), (0.55f, X.FullMoonMid), (1f, X.FullMoonDeep)), segments: 56, rings: 4);

        Fine(b, 24f);
        foreach (var (x, y, rx, ry, degrees, o) in FullMaria)
        {
            var centre = c + new Vector2(x, y) * r;
            var radii = new Vector2(rx, ry) * r * 1.35f;
            b.Ellipse(centre, radii, degrees, MeshBuilder.Radial(centre, radii,
                (0f, MeshBuilder.Alpha(M.MoonstoneMid, o)), (0.55f, MeshBuilder.Alpha(M.MoonstoneMid, o * 0.8f)), (1f, MeshBuilder.Alpha(M.MoonstoneMid, 0f))), segments: 20, rings: 2, aa: false);
        }

        b.Detail(0f);

        // The key-lit limb band, fading toward the lower right.
        var limb = MeshBuilder.Linear(new Vector2(40.05f, 36.7f), new Vector2(79.95f, 86.6f),
            (0f, MeshBuilder.Alpha(M.MoonstoneSpecular, 0.95f)), (0.5f, MeshBuilder.Alpha(M.Moonstone, 0.45f)), (1f, MeshBuilder.Alpha(M.Moonstone, 0f)));
        var clear = MeshBuilder.Solid(M.Moonstone, 0f);
        b.Band(c, [r - 4.2f, r - 3f, r - 0.9f], [clear, limb, limb], segments: 56, aa: false);
    }

    private static void CompletedCheck(MeshBuilder b, MedalTokens t)
    {
        if (t.Flat)
        {
            b.Polygon(MeshBuilder.Stroke(Check, CheckKeylineWidth), MeshBuilder.Solid(t.Ground));
            b.Polygon(MeshBuilder.Stroke(Check, CheckWidth), MeshBuilder.Solid(t.Bright));
            return;
        }

        b.Polygon(MeshBuilder.Stroke(Check, CheckKeylineWidth), MeshBuilder.Solid(M.Keyline));
        b.Polygon(MeshBuilder.Stroke(Check, CheckWidth), MeshBuilder.Linear(new Vector2(64f, 40f), new Vector2(117f, 100f),
            (0f, M.GiltHigh), (0.5f, M.Gilt), (1f, GlyphTokens.Gilt)));
        Fine(b, 20f);
        b.Polygon(MeshBuilder.Stroke([new Vector2(62.3f, 83.3f), new Vector2(76.5f, 97.4f), new Vector2(115.1f, 38.3f)], 1.5f), MeshBuilder.Solid(M.GiltSpecular, 0.85f));
        b.Detail(0f);
    }

    // ------------------------------------------------------------------ Locked out: Dalamud, shattered

    private static readonly float[] ShardAngles = [-122f, -64f, -14f, 31f, 84f, 141f, 197f];
    private static readonly float[] ShardPush = [2.0f, 2.8f, 2.2f, 2.6f, 6.0f, 2.4f, 2.0f];
    private static readonly float[] ShardTurn = [-1.5f, 1.5f, -1.0f, 2.0f, 9.0f, -2.0f, 1.5f];
    private static readonly float[] CrackJog = [1.8f, -2.2f, 2.0f, -1.6f, 2.4f, -2.0f, 1.6f];
    private static readonly Vector2 Impact = new(59f, 57f);

    private static void LockedOut(MeshBuilder b, MedalTokens t)
    {
        const float R = DalamudRadius;
        b.Disc(Center, R + 0.4f, MeshBuilder.Solid(t.Flat ? t.Ground : M.DalamudSocket));
        if (!t.Flat)
        {
            Fine(b, 20f);
            TaperArc(b, Center, R - 0.5f, 15f, 140f, 1.8f, MeshBuilder.Solid(M.DalamudCrack, 0.42f));
            b.Detail(0f);
        }

        // Each crack runs from the impact and bends twice (gen5 locked_out).
        var n = ShardAngles.Length;
        var cracks = new (Vector2 M1, Vector2 M2, Vector2 End, Vector2 Exit)[n];
        for (var i = 0; i < n; i++)
        {
            var u = MeshBuilder.Dir(ShardAngles[i]);
            var end = RayHit(Impact, u, R + 3f);
            var length = Vector2.Distance(end, Impact);
            var j = CrackJog[i];
            var m1 = Impact + u * length * 0.38f + new Vector2(-j * u.Y, j * u.X);
            var m2 = Impact + u * length * 0.7f + new Vector2(j * 0.6f * u.Y, -j * 0.6f * u.X);
            cracks[i] = (m1, m2, end, SegmentExit(m2, end, Center, R));
        }

        var body = t.Flat
            ? MeshBuilder.Solid(t.Ink(QuestState.Foreclosed))
            : MeshBuilder.Linear(Center - new Vector2(R), Center + new Vector2(R), (0f, M.Dalamud), (0.55f, M.DalamudShade), (1f, M.DalamudDeep));
        var limb = MeshBuilder.Linear(Center - new Vector2(R), Center + new Vector2(R),
            (0f, MeshBuilder.Alpha(M.DalamudCrack, 0.75f)), (0.45f, MeshBuilder.Alpha(M.DalamudCrack, 0.15f)), (0.7f, MeshBuilder.Alpha(M.DalamudCrack, 0f)));
        var clear = MeshBuilder.Solid(M.DalamudCrack, 0f);

        for (var i = 0; i < n; i++)
        {
            var c0 = cracks[i];
            var c1 = cracks[(i + 1) % n];
            var a0 = ShardAngles[i];
            var a1 = ShardAngles[(i + 1) % n];
            if (a1 < a0)
            {
                a1 += 360f;
            }

            // The shard's turn pivots on the centroid of its unclipped outline, as gen5's does.
            var b0 = Degrees(c0.End - Center);
            var b1 = Degrees(c1.End - Center);
            while (b1 < b0)
            {
                b1 += 360f;
            }

            var raw = new List<Vector2> { Impact, c0.M1, c0.M2 };
            raw.AddRange(MeshBuilder.Arc(Center, R + 3f, b0, b1, 16));
            raw.Add(c1.M2);
            raw.Add(c1.M1);
            var g = raw.Aggregate(Vector2.Zero, static (s, p) => s + p) / raw.Count;

            var e0 = Degrees(c0.Exit - Center);
            var e1 = Degrees(c1.Exit - Center);
            while (e1 < e0)
            {
                e1 += 360f;
            }

            var shard = new List<Vector2> { Impact, c0.M1, c0.M2 };
            shard.AddRange(MeshBuilder.Arc(Center, R, e0, e1, 16));
            shard.Add(c1.M2);
            shard.Add(c1.M1);

            var mid = (a0 + a1) / 2f;
            var push = MeshBuilder.Dir(mid) * ShardPush[i];
            b.Transform(Matrix3x2.CreateRotation(ShardTurn[i] * MathF.PI / 180f, g) * Matrix3x2.CreateTranslation(push));
            b.Polygon(shard, body);
            if (!t.Flat)
            {
                b.Band(Center, [R - 2.8f, R - 2.1f, R - 0.4f], [clear, limb, limb], e0, e1, segments: 16, aa: false);
                Fine(b, 20f);
                FractureEdges(b, shard, 3, shard.Count - 2);
                b.Detail(0f);
            }

            b.Transform(Matrix3x2.Identity);
        }
    }

    /// <summary>
    /// The fracture faces of a shard: an edge whose outward normal turns toward the key light catches it (a fine pale
    /// edge), one turned away is in shade; the limb arc (vertices <paramref name="arcFrom"/>..<paramref name="arcTo"/>) is left out.
    /// </summary>
    private static void FractureEdges(MeshBuilder b, List<Vector2> shard, int arcFrom, int arcTo)
    {
        var sign = MeshBuilder.SignedArea(shard) >= 0f ? 1f : -1f;
        for (var k = 0; k < shard.Count; k++)
        {
            var next = (k + 1) % shard.Count;
            if (k >= arcFrom && next <= arcTo && next > k)
            {
                continue;
            }

            var p0 = shard[k];
            var p1 = shard[next];
            var d = p1 - p0;
            var len = d.Length();
            if (len < 0.01f)
            {
                continue;
            }

            var normal = sign * new Vector2(d.Y, -d.X) / len;
            var facing = Vector2.Dot(normal, Light);
            if (MathF.Abs(facing) <= 0.15f)
            {
                continue;
            }

            var (width, paint) = facing > 0f
                ? (0.9f + 0.9f * facing, MeshBuilder.Solid(M.DalamudCrack, 0.4f + 0.4f * facing))
                : (0.9f - 0.8f * facing, MeshBuilder.Solid(M.Keyline, 0.55f));
            var inward = -normal * (width * 0.5f);
            b.Strip([p0, p1], [p0 + inward, p1 + inward], paint);
        }
    }

    // ------------------------------------------------------------------ Not checked

    private static void NotChecked(MeshBuilder b, MedalTokens t)
    {
        var (mark, dot) = QuestionMark();
        if (t.Flat)
        {
            var ink = MeshBuilder.Solid(t.Ink(QuestState.Unknown));
            b.Polygon(mark, ink);
            b.Disc(dot.Center, dot.Radius, ink, segments: 24, rings: 1);
            return;
        }

        var v = VeiledMoon;
        b.Disc(v, VeiledMoonRadius + 4.5f, MeshBuilder.Radial(v - new Vector2(4f, 5f), VeiledMoonRadius + 10f,
            (0f, MeshBuilder.Alpha(X.Veiled, 0.9f)), (1f, MeshBuilder.Alpha(X.VeiledDeep, 0.9f))), segments: 40, rings: 2);
        Fine(b, 24f);
        foreach (var (centre, radii, degrees, o) in new (Vector2, Vector2, float, float)[]
                 {
                     (v + new Vector2(-7f, -6f), new Vector2(7.5f, 5.5f), -18f, 0.5f),
                     (v + new Vector2(3f, -6f), new Vector2(4.5f, 4f), 0f, 0.4f),
                     (v + new Vector2(-9f, 6f), new Vector2(5.5f, 7.5f), 8f, 0.4f),
                 })
        {
            var soft = radii * 1.3f;
            b.Ellipse(centre, soft, degrees, MeshBuilder.Radial(centre, soft,
                (0f, MeshBuilder.Alpha(X.VeiledMaria, o)), (0.6f, MeshBuilder.Alpha(X.VeiledMaria, o * 0.7f)), (1f, MeshBuilder.Alpha(X.VeiledMaria, 0f))), segments: 20, rings: 2, aa: false);
        }

        b.Detail(0f);

        // Moon-silver lit like a crescent: brightest along the outer limb, a soft terminator on the inner edge.
        var gradient = MeshBuilder.Linear(new Vector2(v.X - VeiledMoonRadius, v.Y - VeiledMoonRadius - 6f), new Vector2(v.X + VeiledMoonRadius, dot.Center.Y),
            (0f, M.MoonstoneSpecular), (0.5f, M.MoonstoneHigh), (1f, M.MoonstoneMid));
        Paint lit = p =>
        {
            var c = gradient(p);
            var rho = Vector2.Distance(p, v);
            c = ColorMath.Mix(c, M.MoonstoneMid, 0.85f * (1f - MeshBuilder.Smooth(0f, 2.6f, MathF.Abs(rho - (VeiledMoonRadius - 5.2f)))));
            return ColorMath.Mix(c, M.MoonstoneSpecular, 1f - MeshBuilder.Smooth(0f, 2.1f, MathF.Abs(rho - (VeiledMoonRadius + 5.2f))));
        };
        b.Polygon(mark, lit);
        b.Disc(dot.Center, dot.Radius, MeshBuilder.Radial(dot.Center - new Vector2(2f, 2.2f), dot.Radius * 1.7f,
            (0f, M.MoonstoneSpecular), (0.6f, M.Moonstone), (1f, M.MoonstoneMid)), segments: 24, rings: 2);
    }

    /// <summary>The question mark whose bowl is the veiled moon's lit limb (gen5 <c>not_checked</c>), and its dot, a tiny full moon.</summary>
    private static (List<Vector2> Mark, (Vector2 Center, float Radius) Dot) QuestionMark()
    {
        var v = VeiledMoon;
        var r = VeiledMoonRadius;
        var points = new List<Vector2>();
        var widths = new List<float>();
        const float a0 = 196f;
        const float a1 = 398f;
        const int n = 32;
        for (var i = 0; i <= n; i++)
        {
            var t = (float)i / n;
            points.Add(v + MeshBuilder.Dir(a0 + (a1 - a0) * t) * r);
            widths.Add(t < 0.55f ? 13f * MathF.Pow(MathF.Sin(MathF.PI / 2f * MathF.Min(t / 0.55f, 1f)), 0.85f) : 13f - 3.6f * (t - 0.55f) / 0.45f);
        }

        var e = points[^1];
        var tangent = new Vector2(-MathF.Sin(a1 * MathF.PI / 180f), MathF.Cos(a1 * MathF.PI / 180f));
        var p3 = new Vector2(v.X, v.Y + r + 10f);
        var bezier = MeshBuilder.Cubic(e, e + tangent * 7f, new Vector2(v.X, v.Y + r + 3f), p3, 10).ToList();
        for (var i = 0; i < bezier.Count; i++)
        {
            points.Add(bezier[i]);
            widths.Add(9.4f - 0.5f * (i + 1f) / bezier.Count);
        }

        for (var i = 1; i <= 4; i++)
        {
            points.Add(new Vector2(v.X, p3.Y + 1.2f * i));
            widths.Add(8.9f);
        }

        // gen5 tapered_path: each point offset along the normal of its neighbours' chord.
        var left = new List<Vector2>(points.Count);
        var right = new List<Vector2>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            var d = points[Math.Min(i + 1, points.Count - 1)] - points[Math.Max(i - 1, 0)];
            var len = d.Length();
            var normal = len > 0f ? new Vector2(-d.Y, d.X) / len : Vector2.Zero;
            left.Add(points[i] + normal * widths[i] / 2f);
            right.Add(points[i] - normal * widths[i] / 2f);
        }

        var end = points[^1];
        var loop = new List<Vector2>(left);
        loop.AddRange(MeshBuilder.Arc(end, 4.45f, 0f, 180f, 8).Skip(1).SkipLast(1).Reverse());
        for (var i = right.Count - 1; i >= 0; i--)
        {
            loop.Add(right[i]);
        }

        // The cap must run from the left side's end to the right side's: flip it if the sides came out the other way.
        if (Vector2.Distance(left[^1], end + new Vector2(4.45f, 0f)) < Vector2.Distance(left[^1], end - new Vector2(4.45f, 0f)))
        {
            loop = new List<Vector2>(left);
            loop.AddRange(MeshBuilder.Arc(end, 4.45f, 0f, 180f, 8).Skip(1).SkipLast(1));
            for (var i = right.Count - 1; i >= 0; i--)
            {
                loop.Add(right[i]);
            }
        }

        return (loop, (new Vector2(v.X, end.Y + 14.5f), 6.3f));
    }

    // ------------------------------------------------------------------ moons

    /// <summary>The terminator and limb edges of a lit phase in its own frame (lit toward +x, centre at the origin).</summary>
    private static (List<Vector2> Terminator, List<Vector2> Limb) PhaseEdges(float r, float k, int rows)
    {
        var terminator = new List<Vector2>(rows + 1);
        var limb = new List<Vector2>(rows + 1);
        var rx = MathF.Abs(k) * r;
        for (var i = 0; i <= rows; i++)
        {
            var v = -r * MathF.Cos(MathF.PI * i / rows);
            var h = MathF.Sqrt(MathF.Max(0f, 1f - (v / r) * (v / r)));
            var ut = k < 0f ? rx * h : -rx * h;
            terminator.Add(new Vector2(ut, v));
            limb.Add(new Vector2(r * h, v));
        }

        return (terminator, limb);
    }

    /// <summary>gen5 <c>phase()</c>'s transform: mirrored for a left-lit moon, then turned by the tilt about the centre.</summary>
    private static Matrix3x2 PhaseMatrix(Vector2 c, float tiltDegrees, bool litLeft)
    {
        var m = litLeft ? Matrix3x2.CreateScale(-1f, 1f, c) : Matrix3x2.Identity;
        return tiltDegrees != 0f ? m * Matrix3x2.CreateRotation(tiltDegrees * MathF.PI / 180f, c) : m;
    }

    /// <summary>
    /// A crescent with gen5's tone (<c>crescent</c>): the lit body brightens from <paramref name="low"/> at the terminator
    /// to <paramref name="high"/> at the limb, with a soft terminator band in <paramref name="terminator"/> and a soft limb
    /// band in <paramref name="limb"/>. Maria are left to the atlas.
    /// </summary>
    private static void Crescent(MeshBuilder b, Vector2 c, float r, float k, float tilt, bool litLeft, Vector4 low, Vector4 mid, Vector4 high, Vector4 terminator, Vector4 limb)
    {
        var rx = MathF.Abs(k) * r;
        var body = MeshBuilder.Linear(new Vector2(c.X + rx * 0.6f, 0f), new Vector2(c.X + r, 0f), (0f, low), (0.45f, mid), (1f, high));
        PhaseStrip(b, c, r, k, tilt, litLeft, body, terminator, 0.8f, r * 0.045f + 0.8f, limb, 1f, r * 0.08f + 0.8f);
    }

    /// <summary>
    /// A lit phase as a strip from terminator to limb, painted in its own (unturned) frame like an SVG gradient on a
    /// transformed path: <paramref name="body"/>, darkened toward <paramref name="terminator"/> within
    /// <paramref name="terminatorWidth"/> of the terminator and brightened toward <paramref name="limb"/> within
    /// <paramref name="limbWidth"/> of the limb.
    /// </summary>
    private static void PhaseStrip(MeshBuilder b, Vector2 c, float r, float k, float tilt, bool litLeft, Paint body,
        Vector4 terminator, float terminatorAlpha, float terminatorWidth, Vector4 limb, float limbAlpha, float limbWidth)
    {
        var rx = MathF.Abs(k) * r;
        Paint paint = p =>
        {
            var d = p - c;
            var h = MathF.Sqrt(MathF.Max(0f, 1f - (d.Y / r) * (d.Y / r)));
            var ut = k < 0f ? rx * h : -rx * h;
            var colour = body(p);
            colour = ColorMath.Mix(colour, terminator, terminatorAlpha * (1f - MeshBuilder.Smooth(0f, terminatorWidth, d.X - ut)));
            return ColorMath.Mix(colour, limb, limbAlpha * MeshBuilder.Smooth(r - limbWidth, r - 0.2f, d.Length()));
        };

        var (term, edge) = PhaseEdges(r, k, b.Segments(24));
        b.Transform(PhaseMatrix(c, tilt, litLeft));
        b.Strip(term.Select(p => c + p).ToList(), edge.Select(p => c + p).ToList(), paint, [0f, 0.12f, 0.4f, 0.75f, 0.92f, 1f]);
        b.Transform(Matrix3x2.Identity);
    }

    // ------------------------------------------------------------------ badges

    private static MedalMesh BuildBadge(MedalBadge badge, JobSeat seat, MedalTokens t)
    {
        var b = new MeshBuilder();
        if (badge == MedalBadge.None)
        {
            return b.Build();
        }

        var c = BadgeCenter;
        if (t.Flat)
        {
            b.Disc(c, BadgeKeyline, MeshBuilder.Solid(t.Ground), segments: 40, rings: 1);
            b.Band(c, [BadgeSeat, BadgeOuter], [MeshBuilder.Solid(t.Rim)], segments: 40);
        }
        else
        {
            // The badge's cast shadow (dx 1.4, dy 1.9, soft), its keyline, the two-slope gilt ring and a specular streak.
            var s = c + new Vector2(1.4f, 1.9f);
            b.Disc(s, BadgeKeyline, MeshBuilder.Solid(M.Keyline, 0.45f), segments: 40, rings: 1, aa: false);
            b.Band(s, [BadgeKeyline, BadgeKeyline + 2.6f], [MeshBuilder.Solid(M.Keyline, 0.45f), MeshBuilder.Solid(M.Keyline, 0f)], segments: 40, aa: false);
            b.Disc(c, BadgeKeyline, MeshBuilder.Solid(M.Keyline), segments: 40, rings: 1);
            var from = c - new Vector2(BadgeKeyline);
            var to = c + new Vector2(BadgeKeyline);
            var outer = MeshBuilder.Linear(from, to, (0f, M.GiltHigh), (0.45f, M.GiltMid), (1f, M.GiltDeep));
            var inner = MeshBuilder.Linear(from, to, (0f, M.GiltDeep), (0.55f, M.GiltShade), (1f, M.Gilt));
            b.Band(c, [BadgeSeat, BadgeCrest, BadgeCrest, BadgeOuter], [inner, inner, outer, outer], segments: 48);
            b.Detail(64f);
            TaperArc(b, c, BadgeCrest + 0.75f, -170f, -100f, 1.2f, MeshBuilder.Solid(M.GiltSpecular, 0.9f));
            b.Detail(0f);
        }

        var (hi, lo) = SeatColours(badge, seat);
        b.Disc(c, BadgeSeat, t.Flat ? MeshBuilder.Solid(t.Ground) : MeshBuilder.Radial(c - new Vector2(5f, 6f), BadgeSeat * 1.5f, (0f, hi), (1f, lo)), segments: 40, rings: 2);
        if (!t.Flat)
        {
            InnerShadow(b, c, BadgeSeat, new Vector2(1.4f, 1.9f), 0.8f, 0.5f, [15.5f, 17.5f, 19f, 20.1f]);
        }

        switch (badge)
        {
            case MedalBadge.Open:
            case MedalBadge.Closed:
                Lock(b, t, c + new Vector2(0f, LockOffsetY), badge == MedalBadge.Open);
                break;
            case MedalBadge.Journal:
                Book(b, t, c + new Vector2(0f, -1.3f));
                break;
        }

        return b.Build();
    }

    /// <summary>The seat enamel (light stop, dark stop) of a badge.</summary>
    public static (Vector4 Light, Vector4 Dark) SeatColours(MedalBadge badge, JobSeat seat) => badge switch
    {
        MedalBadge.Open => (X.ReadySeat, X.ReadySeatDeep),
        MedalBadge.Closed => (X.BlockedSeat, X.BlockedSeatDeep),
        MedalBadge.Journal => (X.JournalSeat, X.JournalSeatDeep),
        _ => seat switch
        {
            JobSeat.Tank => (M.Tank, M.TankDeep),
            JobSeat.Healer => (M.Healer, M.HealerDeep),
            JobSeat.Dps => (M.Dps, M.DpsDeep),
            _ => (X.Hand, X.HandDeep),
        },
    };

    private static MedalMesh BuildRowGlyph(MedalBadge badge, MedalTokens t)
    {
        var b = new MeshBuilder();

        // badge-*.svg's box is (-17, -18) to (17, 16): map it onto the medal's 128-unit box.
        const float scale = 128f / 34f;
        b.Transform(Matrix3x2.CreateTranslation(17f, 18f) * Matrix3x2.CreateScale(scale));
        switch (badge)
        {
            case MedalBadge.Open:
            case MedalBadge.Closed:
                Lock(b, t, new Vector2(0f, LockOffsetY), badge == MedalBadge.Open);
                break;
            case MedalBadge.Journal:
                Book(b, t, new Vector2(0f, -1.3f));
                break;
        }

        return b.Build();
    }

    /// <summary>
    /// An original padlock as raised metal (gen5 <c>lock_glyph</c>): a slab body lit from the upper left, a stroked
    /// shackle and a recessed keyhole. Open: warm gilt with the shackle lifted and swung free. Closed: cool pewter.
    /// </summary>
    private static void Lock(MeshBuilder b, MedalTokens t, Vector2 o, bool open)
    {
        var shackle = new List<Vector2> { o + new Vector2(-4.6f, 0f) };
        var top = open ? -11f : -6f;
        shackle.AddRange(MeshBuilder.Arc(o + new Vector2(0f, top), 4.6f, 180f, 360f, 12));
        shackle.Add(o + new Vector2(4.6f, open ? -5.5f : 0f));

        var body = new List<Vector2>();
        foreach (var (cx, cy, from) in new[] { (5.6f, 1.9f, 270f), (5.6f, 9.1f, 0f), (-5.6f, 9.1f, 90f), (-5.6f, 1.9f, 180f) })
        {
            body.AddRange(MeshBuilder.Arc(o + new Vector2(cx, cy), 1.9f, from, from + 90f, 4));
        }

        var key = new List<Vector2>();
        key.AddRange(MeshBuilder.Arc(o + new Vector2(0f, 4.65f), 1.55f, -90f, 53f, 8));
        key.Add(o + new Vector2(1.4f, 8.3f));
        key.Add(o + new Vector2(-1.4f, 8.3f));
        key.AddRange(MeshBuilder.Arc(o + new Vector2(0f, 4.65f), 1.55f, 127f, 270f, 8));

        if (t.Flat)
        {
            var ink = MeshBuilder.Solid(open ? t.Gold : t.Bright);
            b.Polygon(MeshBuilder.Stroke(shackle, 2.6f), ink);
            b.Polygon(body, ink);
            b.Polygon(key, MeshBuilder.Solid(t.Ground));
            return;
        }

        var (hi, mid, lo) = open ? (X.LockGiltHigh, M.Gilt, X.LockGiltLow) : (X.PewterHigh, X.Pewter, X.PewterDeep);
        var face = MeshBuilder.Linear(o + new Vector2(-12f, -14f), o + new Vector2(12f, 12f), (0f, hi), (0.5f, mid), (1f, lo));
        b.Polygon(MeshBuilder.Stroke(shackle, 2.6f), face);
        b.Polygon(body, face);
        b.Polygon(key, MeshBuilder.Solid(X.Keyhole, 0.88f));
    }

    /// <summary>
    /// An original journal as raised gilt (gen5 <c>book_glyph</c>): two curved pages lit from the upper left over a darker
    /// board, the right page turned slightly from the light, a spine and a small silk ribbon.
    /// </summary>
    private static void Book(MeshBuilder b, MedalTokens t, Vector2 o)
    {
        List<Vector2> Page(float side)
        {
            Vector2 P(float x, float y) => o + new Vector2(side * x, y);
            var page = new List<Vector2> { P(0f, -5f) };
            page.AddRange(MeshBuilder.Cubic(P(0f, -5f), P(-3f, -7.4f), P(-6.6f, -7.9f), P(-10f, -6.7f), 8));
            page.Add(P(-10f, 7f));
            page.AddRange(MeshBuilder.Cubic(P(-10f, 7f), P(-6.6f, 6f), P(-3.2f, 6.5f), P(0f, 8.8f), 8));
            return page;
        }

        var left = Page(1f);
        var right = Page(-1f);
        if (t.Flat)
        {
            var ink = MeshBuilder.Solid(t.Gold);
            b.Polygon(left, ink);
            b.Polygon(right, ink);
            b.Polygon(MeshBuilder.Stroke([o + new Vector2(0f, -4.9f), o + new Vector2(0f, 8.7f)], 0.9f), MeshBuilder.Solid(t.Ground));
            return;
        }

        var board = new List<Vector2> { o + new Vector2(-10.8f, -6.4f), o + new Vector2(-10.8f, 8.1f) };
        board.AddRange(MeshBuilder.Cubic(o + new Vector2(-10.8f, 8.1f), o + new Vector2(-7.1f, 7f), o + new Vector2(-3.4f, 7.6f), o + new Vector2(0f, 10f), 8));
        board.AddRange(MeshBuilder.Cubic(o + new Vector2(0f, 10f), o + new Vector2(3.4f, 7.6f), o + new Vector2(7.1f, 7f), o + new Vector2(10.8f, 8.1f), 8));
        board.Add(o + new Vector2(10.8f, -6.4f));
        board.Add(o + new Vector2(10f, -6.7f));
        board.Add(o + new Vector2(10f, 7f));
        board.AddRange(MeshBuilder.Cubic(o + new Vector2(10f, 7f), o + new Vector2(6.6f, 6f), o + new Vector2(3.2f, 6.5f), o + new Vector2(0f, 8.8f), 8));
        board.AddRange(MeshBuilder.Cubic(o + new Vector2(0f, 8.8f), o + new Vector2(-3.2f, 6.5f), o + new Vector2(-6.6f, 6f), o + new Vector2(-10f, 7f), 8));
        board.Add(o + new Vector2(-10f, -6.7f));
        b.Polygon(board, MeshBuilder.Solid(M.GiltShade));

        var face = MeshBuilder.Linear(o + new Vector2(-12f, -14f), o + new Vector2(12f, 12f), (0f, X.LockGiltHigh), (0.5f, M.Gilt), (1f, X.LockGiltLow));
        b.Polygon(left, face);
        b.Polygon(right, MeshBuilder.Over(MeshBuilder.Solid(M.GiltDark, 0.12f), face));
        b.Polygon(MeshBuilder.Stroke([o + new Vector2(0f, -4.9f), o + new Vector2(0f, 8.7f)], 0.6f), MeshBuilder.Solid(M.GiltDeep));
        b.Polygon([o + new Vector2(-0.8f, 8.5f), o + new Vector2(-0.8f, 12.6f), o + new Vector2(0f, 11.7f), o + new Vector2(0.8f, 12.6f), o + new Vector2(0.8f, 8.5f)], MeshBuilder.Solid(M.RibbonHigh));
        b.Polygon([o + new Vector2(0.8f, 8.5f), o + new Vector2(0.8f, 12.6f), o + new Vector2(0f, 11.7f)], MeshBuilder.Solid(M.Ribbon));
    }

    // ------------------------------------------------------------------ small geometry

    /// <summary>
    /// Starts fine detail (hairlines, seams, glints, crack faces, soft maria) drawn from <paramref name="minSizePx"/> up
    /// in a mesh for any size, where a feature under a pixel would draw a pixel wide; a mesh built for one size draws it
    /// at every size, as its coverage (<see cref="MeshBuilder(float, float)"/>), as the approved art does.
    /// </summary>
    private static void Fine(MeshBuilder b, float minSizePx) => b.Detail(b.Coverage ? 0f : minSizePx);

    private static List<Vector2> Rect(float x0, float y0, float x1, float y1) =>
        [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];

    private static List<Vector2> HorizontalLine(float x0, float x1, float y, int n)
    {
        var list = new List<Vector2>(n + 1);
        for (var i = 0; i <= n; i++)
        {
            list.Add(new Vector2(x0 + (x1 - x0) * i / n, y));
        }

        return list;
    }

    /// <summary>A rounded bar (an SVG rect with rx = half its height).</summary>
    private static List<Vector2> Stadium(float x0, float x1, float top, float bottom)
    {
        var r = (bottom - top) / 2f;
        var cy = top + r;
        var loop = new List<Vector2>();
        loop.AddRange(MeshBuilder.Arc(new Vector2(x1 - r, cy), r, -90f, 90f, 10));
        loop.AddRange(MeshBuilder.Arc(new Vector2(x0 + r, cy), r, 90f, 270f, 10));
        return loop;
    }

    private static float Degrees(Vector2 d) => MathF.Atan2(d.Y, d.X) * 180f / MathF.PI;

    /// <summary>Where a ray from <paramref name="from"/> along <paramref name="direction"/> leaves the circle of radius <paramref name="radius"/> round the medal's centre.</summary>
    private static Vector2 RayHit(Vector2 from, Vector2 direction, float radius)
    {
        var d = from - Center;
        var bq = Vector2.Dot(d, direction);
        var cq = d.LengthSquared() - radius * radius;
        return from + direction * (-bq + MathF.Sqrt(bq * bq - cq));
    }

    /// <summary>Where the segment <paramref name="a"/>→<paramref name="b"/> (a inside) crosses the circle; <paramref name="b"/> if it does not.</summary>
    private static Vector2 SegmentExit(Vector2 a, Vector2 b, Vector2 center, float radius)
    {
        var d = b - a;
        var f = a - center;
        var qa = d.LengthSquared();
        var qb = 2f * Vector2.Dot(f, d);
        var qc = f.LengthSquared() - radius * radius;
        var disc = qb * qb - 4f * qa * qc;
        if (qa <= 0f || disc < 0f)
        {
            return b;
        }

        var t = (-qb + MathF.Sqrt(disc)) / (2f * qa);
        return t is >= 0f and <= 1f ? a + d * t : b;
    }
}
