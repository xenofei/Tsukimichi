using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>The menus' backdrops, official paintings read from the player's install (spec-rich2.md §6).</summary>
public enum MoonfallBackdrop : byte
{
    /// <summary>The title: Sohm Al (<c>ui/loadingimage/-nowloading_base07.tex</c>), jewel-graded amethyst and sapphire.</summary>
    Title,

    /// <summary>The map and level select: the world map (<c>ui/map/world/01/world01_m.tex</c>) as a moonlit chart, sapphire lands and teal seas.</summary>
    Chart,

    /// <summary>
    /// The title for a story not yet in Heavensward (the owner's answer, 6 October 2026): Ul'dah
    /// (<c>ui/loadingimage/-nowloading_base02.tex</c>), A Realm Reborn's, graded as the title is.
    /// </summary>
    TitleEarly,
}

/// <summary>
/// The menus' backdrops built from the game's paintings (screens2.py <c>title_background</c> and
/// <c>chart_background</c>): the title's Sohm Al night-graded then jewel-graded in the title's amethyst and sapphire,
/// lightness kept; the world map night-graded into a chart with sapphire lands and teal seas. Pure and off the framework
/// thread; the result is uploaded once and released when its screens close.
/// </summary>
public static class MoonfallBackdrops
{
    /// <summary>The title's built size (the 1280 × 800 design; smaller windows draw it scaled).</summary>
    public const int TitleWidth = 1280;

    /// <inheritdoc cref="TitleWidth"/>
    public const int TitleHeight = 800;

    /// <summary>
    /// The chart's region of the world map in the design's coordinates (the 2048-wide map, rows from 587): the
    /// whole strip the map and level select crop from (screens2.py: 150 to 1210 across, 0 to 872 down).
    /// </summary>
    public static readonly Vector4 ChartRegion = new(150f, 0f, 1060f, 872f);

    /// <summary>The chart's built width: the region at about 1.2 pixels a map pixel (the 1280 design's).</summary>
    public const int ChartWidth = 1280;

    /// <summary>The chart's built height (the region's aspect).</summary>
    public static int ChartHeight => (int)MathF.Round(ChartWidth * ChartRegion.W / ChartRegion.Z);

    private const float ChartTop = 587f;

    /// <summary>The game path a backdrop reads.</summary>
    public static string PathOf(MoonfallBackdrop backdrop) => backdrop switch
    {
        MoonfallBackdrop.Chart => "ui/map/world/01/world01_m.tex",
        MoonfallBackdrop.TitleEarly => "ui/loadingimage/-nowloading_base02.tex",
        _ => "ui/loadingimage/-nowloading_base07.tex",
    };

    /// <summary>Builds <paramref name="backdrop"/> from its painting.</summary>
    public static MoonfallRgba Build(MoonfallBackdrop backdrop, MoonfallImage painting)
    {
        ArgumentNullException.ThrowIfNull(painting);
        var image = backdrop == MoonfallBackdrop.Chart ? Chart(painting) : Title(painting);
        return new MoonfallRgba(image.Width, image.Height, image.ToRgba(), new Vector4(0, 0, image.Width, image.Height));
    }

    /// <summary>
    /// screens2.title_background: the painting cropped to the 1280 × 800 design (its focus a third across, as the
    /// mocks frame the mountain right of the modes), night-graded, then jewel-graded with the sky toward amethyst.
    /// </summary>
    public static MoonfallImage Title(MoonfallImage painting)
    {
        ArgumentNullException.ThrowIfNull(painting);
        // The design's crop is in the 1920 × 1080 painting's pixels; a painting of another size is cropped in proportion.
        var k = painting.Width / 1920f;
        const float Ch = 1010f;
        var cw = MathF.Min(1920f, Ch * TitleWidth / TitleHeight);
        var x0 = Math.Clamp(960f - (cw * 0.30f), 0f, 1920f - cw);
        var px = MoonfallFilters.Resample(painting, x0 * k, 40f * k, cw * k, MathF.Min(Ch * k, painting.Height - (40f * k)), TitleWidth, TitleHeight);
        var night = MoonfallGrade.NightLab(px, new MoonfallNightGrade
        {
            Ceiling = 0.66f,
            Knee = 0.40f,
            Exposure = 0.95f,
            Gamma = 1.6f,
            SkyDrop = 0.25f,
            SkyTop = 0.30f,
            SkyBottom = 0.70f,
        }, 1f);
        return JewelTitle(night);
    }

    /// <summary>
    /// screens2.jewel_grade for the title's palette: lightness kept exactly, the hue taken from the sky's sapphire in
    /// the shadows to amethyst in the lights, the chroma raised and the highlights kept pale.
    /// </summary>
    private static MoonfallImage JewelTitle(MoonfallImage px)
    {
        var sky = MoonfallColor.HueDirection(MoonfallColor.Hex("#1C2E8E"));
        var j2 = MoonfallColor.HueDirection(MoonfallColor.Hex("#6A4FC8"));
        int w = px.Width, h = px.Height;
        var out_ = new MoonfallImage(w, h);
        MoonfallParallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w) + x;
                var lab = MoonfallColor.ToOklab(px.R.Data[i], px.G.Data[i], px.B.Data[i]);
                var t = MoonfallColor.Smooth(0.15f, 0.55f, lab.X);
                var ta = (sky.X * (1 - t)) + (j2.X * t);
                var tb = (sky.Y * (1 - t)) + (j2.Y * t);
                var n = MathF.Sqrt((ta * ta) + (tb * tb)) + 1e-6f;
                ta /= n;
                tb /= n;
                var cc = MathF.Sqrt((lab.Y * lab.Y) + (lab.Z * lab.Z));
                var hi = MoonfallColor.Smooth(0.55f, 0.80f, lab.X);
                var cn = (0.035f + (cc * 1.6f)) * (1 - (0.7f * hi));
                var rgb = MoonfallColor.ToSrgbKeepingLightness(lab.X, (lab.Y * 0.35f) + (ta * cn * 0.65f), (lab.Z * 0.35f) + (tb * cn * 0.65f));
                out_.R.Data[i] = rgb.X;
                out_.G.Data[i] = rgb.Y;
                out_.B.Data[i] = rgb.Z;
            }
        });
        return out_;
    }

    /// <summary>
    /// screens2.chart_background: the world map's strip (<see cref="ChartRegion"/>) night-graded as a moonlit chart,
    /// then the lands pushed to sapphire and the seas to teal (the second jewel), lightness kept, and the moon's light
    /// from beyond the upper left corner.
    /// </summary>
    public static MoonfallImage Chart(MoonfallImage painting)
    {
        ArgumentNullException.ThrowIfNull(painting);
        var k = painting.Width / 2048f;
        var r = ChartRegion;
        var px = MoonfallFilters.Resample(painting, r.X * k, (ChartTop + r.Y) * k, r.Z * k, r.W * k, ChartWidth, ChartHeight);
        var night = MoonfallGrade.NightLab(px, new MoonfallNightGrade
        {
            Gamma = 1.35f,
            Exposure = 0.85f,
            Ceiling = 0.54f,
            WarmKeep = 0.25f,
            ChromaMid = 0.45f,
        }, 1f);
        int w = night.Width, h = night.Height;
        // The grade's 0.86 dimming first, then the lands and seas by lightness (the seas the lighter, flatter parts).
        for (var i = 0; i < w * h; i++)
        {
            night.R.Data[i] *= 0.86f;
            night.G.Data[i] *= 0.86f;
            night.B.Data[i] *= 0.86f;
        }

        var sea = MoonfallFilters.Blur(MoonfallGrade.Lightness(night), 3f);
        var land = MoonfallColor.HueDirection(MoonfallColor.Hex("#2B4FC0"));
        var water = MoonfallColor.HueDirection(MoonfallColor.Hex("#1FA0B0"));
        MoonfallParallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w) + x;
                var lab = MoonfallColor.ToOklab(night.R.Data[i], night.G.Data[i], night.B.Data[i]);
                var s = MoonfallColor.Smooth(0.42f, 0.55f, sea.Data[i]);
                var lit = MoonfallColor.Smooth(0.04f, 0.2f, lab.X);
                var cl = 0.07f * lit * (1 - s);
                var cs = 0.05f * lit * s;
                var rgb = MoonfallColor.ToSrgb(lab.X, (lab.Y * 0.3f) + (land.X * cl) + (water.X * cs), (lab.Z * 0.3f) + (land.Y * cl) + (water.Y * cs));
                night.R.Data[i] = rgb.X;
                night.G.Data[i] = rgb.Y;
                night.B.Data[i] = rgb.Z;
            }
        });
        MoonfallGrade.MoonGlow(night, -60f, -60f, 600f, 1400f, 0.10f, 0.05f, MoonfallColor.Hex("#B9C8F0"));
        return night;
    }
}
