namespace Tsukimichi.Core.Ui;

/// <summary>
/// The sprites of the giver-portrait atlas (1.15 design spec A4, A6): the sixteen race silhouettes (race by gender, in
/// ENpcBase order), the neutral moon disc, and Full's plate shade (the lip shadow and the moonlight wash in one). Every
/// sprite is the plate's whole 72-unit box, so each is drawn over the plate's square. Order is the atlas's.
/// </summary>
public enum PortraitSprite : byte
{
    HyurMale,
    HyurFemale,
    ElezenMale,
    ElezenFemale,
    LalafellMale,
    LalafellFemale,
    MiqoteMale,
    MiqoteFemale,
    RoegadynMale,
    RoegadynFemale,
    AuRaMale,
    AuRaFemale,
    HrothgarMale,
    HrothgarFemale,
    VieraMale,
    VieraFemale,

    /// <summary>A waxing crescent lit on the right over a faint disc: a giver that is not humanoid, or initials with no room.</summary>
    MoonDisc,

    /// <summary>Full's lip shadow (Abyss .55, blurred, upper left) and moonlight wash (MoonHigh .07), drawn over the face from 32 px.</summary>
    PlateShade,
}

/// <summary>
/// Layout of the giver-portrait atlas, <c>Tsukimichi/assets/ui/portraits.png</c> (1024 × 616) and
/// <c>portraits@2x.png</c> (2048 × 1232, the same layout doubled), written with <c>portraits.json</c> by
/// <c>docs/design/v7/ui/1.15/silhouettes/gen_portrait_atlas.py</c> from the approved silhouettes; a test holds this table
/// to that file and to the PNG sizes. Every sprite is drawn at each of <see cref="Tiers"/> (24, 48, 72 and 128 px at 1x),
/// a band of rows per tier, sprites 2 px apart (4 px at 2x) so bilinear sampling never bleeds a neighbour in.
/// <para>
/// Image textures have one mip level, so <see cref="Pick"/> takes the smallest cell at or above the drawn plate, from the
/// 1x tiers and then the 2x ones (as <see cref="MedalLayout.Pick"/> does): a sprite is never shrunk by more than 1.5×.
/// </para>
/// </summary>
public static class PortraitAtlasLayout
{
    public const int Width = 1024;
    public const int Height = 616;
    public const int Pad = 2;

    /// <summary>Manifest resource names (the csproj's <c>LogicalName</c>s).</summary>
    public const string ResourceName1x = "Tsukimichi.assets.ui.portraits.png";
    public const string ResourceName2x = "Tsukimichi.assets.ui.portraits@2x.png";

    /// <summary>The cell sizes at 1x, smallest first; the 2x atlas holds each at twice the size.</summary>
    public static readonly int[] Tiers = [24, 48, 72, 128];

    /// <summary>The number of sprites (every <see cref="PortraitSprite"/>).</summary>
    public const int SpriteCount = (int)PortraitSprite.PlateShade + 1;

    /// <summary>The sprite's cell at the 1x tier <paramref name="tier"/> (one of <see cref="Tiers"/>); empty for anything else.</summary>
    public static AtlasRect Rect(PortraitSprite sprite, int tier)
    {
        var i = (int)sprite;
        if (i < 0 || i >= SpriteCount)
        {
            return default;
        }

        var y = Pad;
        foreach (var cell in Tiers)
        {
            var perRow = Math.Min(SpriteCount, (Width - Pad) / (cell + Pad));
            if (cell == tier)
            {
                return new AtlasRect(Pad + (i % perRow) * (cell + Pad), y + (i / perRow) * (cell + Pad), cell, cell);
            }

            y += ((SpriteCount + perRow - 1) / perRow) * (cell + Pad);
        }

        return default;
    }

    /// <summary>Top-left and bottom-right UVs of <paramref name="rect"/> (the same in both atlases).</summary>
    public static (float U0, float V0, float U1, float V1) Uv(AtlasRect rect) =>
        ((float)rect.X / Width, (float)rect.Y / Height, (float)(rect.X + rect.Width) / Width, (float)(rect.Y + rect.Height) / Height);

    /// <summary>
    /// The texture and tier to draw a plate <paramref name="sizePx"/> device px across: the smallest 1x tier at or above
    /// it, else the smallest 2x tier at or above it, else the largest 2x tier.
    /// </summary>
    public static (int Tier, bool TwoX) Pick(float sizePx)
    {
        foreach (var cell in Tiers)
        {
            if (cell >= sizePx)
            {
                return (cell, false);
            }
        }

        foreach (var cell in Tiers)
        {
            if (cell * 2 >= sizePx)
            {
                return (cell, true);
            }
        }

        return (Tiers[^1], true);
    }

    /// <summary>
    /// The silhouette for an ENpcBase race (1 Hyur, 2 Elezen, 3 Lalafell, 4 Miqo'te, 5 Roegadyn, 6 Au Ra, 7 Hrothgar,
    /// 8 Viera) and gender (0 male, anything else female); the moon disc for any other race.
    /// </summary>
    public static PortraitSprite Silhouette(byte race, byte gender) =>
        race is >= 1 and <= 8 ? (PortraitSprite)(((race - 1) * 2) + (gender == 0 ? 0 : 1)) : PortraitSprite.MoonDisc;

    /// <summary>The sprite's key in <c>portraits.json</c> (the silhouette's file stem).</summary>
    public static string Key(PortraitSprite sprite) => sprite switch
    {
        PortraitSprite.MoonDisc => "moon-disc",
        PortraitSprite.PlateShade => "plate-shade",
        _ when (int)sprite < 16 => RaceKeys[(int)sprite / 2] + ((int)sprite % 2 == 0 ? "-male" : "-female"),
        _ => string.Empty,
    };

    private static readonly string[] RaceKeys = ["hyur", "elezen", "lalafell", "miqote", "roegadyn", "aura", "hrothgar", "viera"];
}
