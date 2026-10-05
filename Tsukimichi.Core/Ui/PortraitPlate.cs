using System.Numerics;
using Tsukimichi.Core.Portraits;

namespace Tsukimichi.Core.Ui;

/// <summary>Settings › General › Look › Giver portraits (1.15 design spec A8).</summary>
public enum GiverPortraitMode : byte
{
    /// <summary>No plates anywhere: the Giver card, the stops and the routes look as they did before 1.15.</summary>
    Off = 0,

    /// <summary>The game's own art only, read from the install at runtime.</summary>
    GameArt = 1,

    /// <summary>
    /// The game's art, and the giver photos that ship with the plugin where it has none (feature plan v7 F4; "Game art +
    /// photos", the default). Reads as <see cref="GameArt"/> if the photos could not be read.
    /// </summary>
    GameArtAndPack = 2,
}

/// <summary>What a portrait plate shows (1.15 design spec A6): the face, or one of the fallbacks, in their order.</summary>
public enum PortraitShow : byte
{
    /// <summary>The giver's face from the game's art.</summary>
    Face,

    /// <summary>The allied society's emblem, a square tile at 64 % of the plate.</summary>
    SocietyEmblem,

    /// <summary>A race silhouette (<see cref="PortraitAtlasLayout.Silhouette"/>).</summary>
    Silhouette,

    /// <summary>One or two initials in the Title face (<see cref="PortraitPlate.InitialLetters"/> says how many).</summary>
    Initials,

    /// <summary>The neutral moon disc: a non-humanoid giver, an unknown one, or initials with no room for 9 px caps.</summary>
    MoonDisc,
}

/// <summary>
/// The giver portrait plate's rules (1.15 design spec A4–A7), pure so they are tested: the sizes and gaps per place, the
/// plate geometry in its 72-unit box, which face or fallback a plate shows at a size, the spoiler rule for faces, the
/// tooltip's size and the 0.3 s fade. Every portrait, avatar and fallback sits on the same plate, so nothing reads as
/// missing. Nothing here allocates.
/// </summary>
public static class PortraitPlate
{
    // ------------------------------------------------------------------ sizes (logical px)

    /// <summary>The Giver card's plate at Full.</summary>
    public const float FullSize = 72f;

    /// <summary>The Giver card's plate at Quiet.</summary>
    public const float QuietSize = 64f;

    /// <summary>Plain's inline plate, before the name in the Giver line.</summary>
    public const float PlainSize = 18f;

    /// <summary>The avatar beside each stop in Next stops, the Route window and the Tonight card.</summary>
    public const float AvatarSize = 24f;

    /// <summary>The Journal table's Giver column.</summary>
    public const float ColumnSize = 20f;

    /// <summary>The hover tooltip's plate, at most (<see cref="TooltipSize"/>).</summary>
    public const float TooltipMax = 128f;

    /// <summary>The tooltip never shows a face larger than this times its box's hr side, so low-res art is never stretched to mush.</summary>
    public const float TooltipBoxFactor = 1.6f;

    /// <summary>The gap between the card's plate and its name, at Full.</summary>
    public const float FullGap = 14f;

    /// <summary>The gap at Quiet.</summary>
    public const float QuietGap = 12f;

    /// <summary>Plain's gap between the inline plate and the name.</summary>
    public const float PlainGap = 6f;

    /// <summary>The gap after an avatar.</summary>
    public const float AvatarGap = 10f;

    /// <summary>The Journal column's gap between the avatar and the giver's name.</summary>
    public const float ColumnGap = 6f;

    /// <summary>The Journal Giver column's width for the name after the avatar, logical px (the cell ellipsises longer names).</summary>
    public const float ColumnNameLogical = 132f;

    /// <summary>The Giver card's plate size at a Decoration level.</summary>
    public static float CardSize(Flair flair) => flair switch
    {
        Flair.Full => FullSize,
        Flair.Quiet => QuietSize,
        _ => PlainSize,
    };

    /// <summary>The gap after the card's plate at a Decoration level.</summary>
    public static float CardGap(Flair flair) => flair switch
    {
        Flair.Full => FullGap,
        Flair.Quiet => QuietGap,
        _ => PlainGap,
    };

    // ------------------------------------------------------------------ the plate (in its 72-unit box)

    /// <summary>The box every plate value is measured in.</summary>
    public const float Units = 72f;

    /// <summary>The well's radius.</summary>
    public const float WellRadius = 35f;

    /// <summary>The face's clip radius (<c>AddImageRounded</c> with rounding = half the face square).</summary>
    public const float FaceRadius = 34.5f;

    /// <summary>The keyline's radius (1 px brass at Full, silver at Quiet, a line at Plain).</summary>
    public const float KeylineRadius = 34.9f;

    /// <summary>Full's outer Abyss ring at .6, outside the brass keyline.</summary>
    public const float OuterRingRadius = 35.6f;

    public const float OuterRingAlpha = 0.6f;

    /// <summary>The Quiet hairline's alpha (Silver #C3CBDF).</summary>
    public const float QuietKeylineAlpha = 0.62f;

    /// <summary>From this plate size (logical px) up, Full draws the lip shadow and the moonlight wash; below, only the well, the face and the keyline.</summary>
    public const float ShadeMinLogical = 32f;

    /// <summary>The society emblem's square, as a share of the plate (46 of 72 units).</summary>
    public const float EmblemShare = 0.64f;

    /// <summary>The emblem's alpha: ungraded, but a touch under opaque.</summary>
    public const float EmblemAlpha = 0.92f;

    /// <summary>The emblem's soft down-right shadow from <see cref="ShadeMinLogical"/>: Abyss at .45, offset 0.8 / 1.1 px, blur 1 px.</summary>
    public const float EmblemShadowAlpha = 0.45f;

    public static readonly Vector2 EmblemShadowOffset = new(0.8f, 1.1f);

    /// <summary>The initials' ink (#E9E4D2) and alpha.</summary>
    public const uint InitialsHex = 0xE9E4D2;

    public const float InitialsAlpha = 0.92f;

    /// <summary>The face square: <see cref="FaceRadius"/> × 2 at (1.5, 1.5), so the crop box maps to 69 × 69.</summary>
    public static (Vector2 Min, Vector2 Max) FaceRect(Vector2 min, float size)
    {
        var inset = size * ((WellRadius - FaceRadius) + 1f) / Units;
        return (min + new Vector2(inset), min + new Vector2(size - inset));
    }

    /// <summary>The emblem's square, centred on the plate at <see cref="EmblemShare"/>.</summary>
    public static (Vector2 Min, Vector2 Max) EmblemRect(Vector2 min, float size)
    {
        var side = size * EmblemShare;
        var at = min + new Vector2((size - side) * 0.5f);
        return (at, at + new Vector2(side));
    }

    // ------------------------------------------------------------------ what a plate shows

    /// <summary>From this plate size (logical px) up, initials are two letters when the name has two.</summary>
    public const float TwoInitialsMinLogical = 32f;

    /// <summary>From this size up to <see cref="TwoInitialsMinLogical"/>, one initial with caps of at least <see cref="InitialCapsMinLogical"/>; below, the moon disc.</summary>
    public const float OneInitialMinLogical = 20f;

    /// <summary>The least cap height of a lone initial on a small plate.</summary>
    public const float InitialCapsMinLogical = 9f;

    /// <summary>The Title face's cap height as a share of its size (Jupiter, Marcellus in the mock: about .70 em).</summary>
    public const float CapHeightEm = 0.70f;

    /// <summary>
    /// How many letters of <paramref name="initials"/> a plate <paramref name="logicalSize"/> across shows: both (up to
    /// two) from <see cref="TwoInitialsMinLogical"/>, the first alone from <see cref="OneInitialMinLogical"/>, none below
    /// (the moon disc stands in: there is no room for 9 px caps).
    /// </summary>
    public static int InitialLetters(string? initials, float logicalSize)
    {
        var length = initials?.Length ?? 0;
        if (length == 0 || !(logicalSize >= OneInitialMinLogical))
        {
            return 0;
        }

        return logicalSize >= TwoInitialsMinLogical ? Math.Min(2, length) : 1;
    }

    /// <summary>
    /// The initials' font size in logical px on a plate <paramref name="logicalSize"/> across: 26 of 72 units for two
    /// letters and 30 for one from <see cref="TwoInitialsMinLogical"/>; on a smaller plate, the size whose caps are
    /// <see cref="InitialCapsMinLogical"/> high (about 13 px).
    /// </summary>
    public static float InitialsFontLogical(int letters, float logicalSize)
    {
        if (logicalSize >= TwoInitialsMinLogical)
        {
            return logicalSize * (letters > 1 ? 26f : 30f) / Units;
        }

        return MathF.Ceiling(InitialCapsMinLogical / CapHeightEm);
    }

    /// <summary>
    /// Whether a face from <paramref name="faceEra"/> may show for a quest (1.15 design spec A6, item 4): never for a
    /// quest the spoiler shield masks, and, while the shield is on, never a face from a later expansion than the
    /// character's story has reached (<paramref name="reachExpansion"/>, <see cref="Query.SpoilerMask.ReachExpansion"/>),
    /// so a future-expansion reveal stays hidden until the quest is unmasked.
    /// </summary>
    public static bool FaceAllowed(byte faceEra, bool questMasked, bool shieldOn, byte reachExpansion) =>
        !questMasked && (!shieldOn || faceEra <= reachExpansion);

    /// <summary>
    /// What a plate <paramref name="logicalSize"/> across shows for <paramref name="portrait"/>: the face when there is
    /// art and <paramref name="faceAllowed"/>; else the fallback the index chose. A face the shield hides falls back to
    /// the race silhouette even for a named giver (initials only when the race is unknown), so nothing of who the giver
    /// is leaks beyond the name the shield already shows. Initials with no room for 9 px caps, and a silhouette of a
    /// race the atlas does not have, are the moon disc.
    /// </summary>
    public static PortraitShow Choose(in PortraitRef portrait, bool faceAllowed, float logicalSize)
    {
        if (portrait.HasArt && faceAllowed)
        {
            return PortraitShow.Face;
        }

        var fallback = portrait.Fallback;
        var kind = fallback.Kind;
        if (portrait.HasArt && kind == PortraitFallbackKind.Initials && fallback.Humanoid)
        {
            kind = PortraitFallbackKind.Silhouette;
        }

        return Fallback(kind, fallback, logicalSize);
    }

    /// <summary>
    /// <paramref name="portrait"/> for a quest the spoiler shield masks: its fallback is never the allied society's
    /// emblem, which would tell whose story the masked quest belongs to, but the race silhouette (the moon disc for a
    /// giver that is not one of the playable races). Every other fallback is kept; the face never shows for a masked
    /// quest anyway (<see cref="FaceAllowed"/>).
    /// </summary>
    public static PortraitRef ForMaskedQuest(in PortraitRef portrait) =>
        portrait.Fallback.Kind == PortraitFallbackKind.SocietyEmblem
            ? portrait with { Fallback = portrait.Fallback with { Kind = PortraitFallbackKind.Silhouette, SocietyIcon = 0 } }
            : portrait;

    /// <summary>The fallback <paramref name="kind"/> as drawn at <paramref name="logicalSize"/>.</summary>
    public static PortraitShow Fallback(PortraitFallbackKind kind, in PortraitFallback fallback, float logicalSize) => kind switch
    {
        PortraitFallbackKind.SocietyEmblem when fallback.SocietyIcon != 0 => PortraitShow.SocietyEmblem,
        PortraitFallbackKind.SocietyEmblem or PortraitFallbackKind.Silhouette when fallback.Race is >= 1 and <= 8 => PortraitShow.Silhouette,
        PortraitFallbackKind.Initials when InitialLetters(fallback.Initials, logicalSize) > 0 => PortraitShow.Initials,
        _ => PortraitShow.MoonDisc,
    };

    /// <summary>
    /// The hover tooltip's plate, logical px: <see cref="TooltipMax"/>, but for a face at most
    /// <see cref="TooltipBoxFactor"/> × its crop box's hr side (Tataru's 75 px card box shows at 120 px).
    /// </summary>
    public static float TooltipSize(in PortraitRef portrait, bool face)
    {
        if (!face || !portrait.HasArt)
        {
            return TooltipMax;
        }

        if (portrait.Source == PortraitSource.Pack)
        {
            // A pack photo is never drawn above its own head box (its source pixels, 1.0x; spec-1.20 F4).
            return portrait.SourceBox > 0 ? MathF.Min(TooltipMax, portrait.SourceBox) : TooltipMax;
        }

        var side = portrait.Crop.ToBox(portrait.Source).Side;
        return side > 0f && float.IsFinite(side) ? MathF.Min(TooltipMax, MathF.Round(side * TooltipBoxFactor)) : TooltipMax;
    }

    // ------------------------------------------------------------------ the graded copy and the fade

    /// <summary>The side of the small graded copy the avatars, the column and Plain draw (box-filtered from the full crop).</summary>
    public const int SmallCopySide = 48;

    /// <summary>
    /// Whether a face drawn <paramref name="drawnPx"/> device px across takes the small graded copy: at or under its side,
    /// so no face is shrunk on the GPU by much more than 2× (image textures have one mip level).
    /// </summary>
    public static bool UseSmallCopy(float drawnPx) => drawnPx <= SmallCopySide;

    /// <summary>
    /// The face's opacity <paramref name="elapsed"/> seconds after it could first be drawn for a new giver: ease-out cubic
    /// over <see cref="MotionTokens.ArtFade"/>. 1 at and after the end; 0 before the start.
    /// </summary>
    public static float FadeAlpha(double elapsed)
    {
        if (!(elapsed > 0d))
        {
            return 0f;
        }

        var t = (float)Math.Min(1d, elapsed / MotionTokens.ArtFade);
        var u = 1f - t;
        return 1f - (u * u * u);
    }
}
