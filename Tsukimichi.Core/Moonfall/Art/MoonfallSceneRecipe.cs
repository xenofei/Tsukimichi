using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>Where a scene's painting comes from.</summary>
public enum MoonfallSourceKind : byte
{
    /// <summary>A texture read from the player's install at runtime (<c>ui/loadingimage/…</c>, <c>ui/map/…</c>); nothing shipped.</summary>
    Game,

    /// <summary>One of Moonfall's own paintings, shipped in <c>assets/moonfall/scenes/&lt;name&gt;.png</c>.</summary>
    Picture,
}

/// <summary>A scene's painting and how it is cut to the 800 × 600 board (the design's <c>scene_official.build</c>).</summary>
/// <param name="Kind">Game texture or shipped picture.</param>
/// <param name="Path">The game path, or the picture's name.</param>
/// <param name="Mirror">Mirrored left to right first (so a painting lit from the right is lit from the upper left).</param>
/// <param name="PadTop">Rows added above, before cropping.</param>
/// <param name="PadLeft">Columns added at the left, before cropping.</param>
/// <param name="PadReflect">Padding mirrors the edge (true) or repeats it (false).</param>
/// <param name="Crop">(x, y, w, h) in source pixels (after mirroring, before padding is added: x and y may be negative into the padding); null takes the whole picture.</param>
public sealed record MoonfallSceneSource(MoonfallSourceKind Kind, string Path, bool Mirror, int PadTop, int PadLeft, bool PadReflect, (float X, float Y, float W, float H)? Crop);

/// <summary>One factor of a mask: a band of a quantity, smoothstepped, optionally inverted and scaled.</summary>
public enum MoonfallMaskKind : byte
{
    /// <summary>The graded scene's OKLab lightness (blurred by <see cref="MoonfallMaskTerm.Blur"/> units) from a to b.</summary>
    Lum,

    /// <summary>Board y from a to b.</summary>
    Y,

    /// <summary>Board x from a to b.</summary>
    X,

    /// <summary>Inside a disc (x, y, r) with a feather f: 1 inside, 0 outside.</summary>
    Disc,

    /// <summary>Near the pieces: 1 within b units of a piece's edge, 0 beyond a.</summary>
    Near,
}

/// <summary>One factor of a mask (<see cref="MoonfallMaskKind"/>): <c>scale × v</c>, or <c>1 − scale × v</c> when inverted.</summary>
public sealed record MoonfallMaskTerm(MoonfallMaskKind Kind, float[] Args, float Blur, bool Invert, float Scale);

/// <summary>A second jewel pushed into a region (<see cref="MoonfallJewelRegion"/>), where = the product of its terms × weight.</summary>
public sealed record MoonfallRegionRecipe(Vector3 Hue, float Chroma, float Weight, IReadOnlyList<MoonfallMaskTerm> Where);

/// <summary>The level's palette (<see cref="MoonfallJewelGrade"/>) as a recipe writes it.</summary>
public sealed record MoonfallPaletteRecipe
{
    public required IReadOnlyList<(float Y, Vector3 Colour)> Bands { get; init; }

    public IReadOnlyList<(float L, Vector3 Colour)> ValueHues { get; init; } = [];

    public float Mix { get; init; } = 0.5f;

    public float Chroma { get; init; } = 1f;

    public float Floor { get; init; } = 0.024f;

    public float Keep { get; init; } = 0.30f;

    public float KeepHigh { get; init; } = 0.75f;

    public IReadOnlyList<MoonfallRegionRecipe> Regions { get; init; } = [];

    /// <summary>Where the palette applies (the product of these terms); empty: everywhere.</summary>
    public IReadOnlyList<MoonfallMaskTerm> Where { get; init; } = [];
}

/// <summary>A light layer, applied in the recipe's order after the palette.</summary>
public abstract record MoonfallLightRecipe;

/// <summary>Light shafts from beyond the upper left (dress2.shafts; F4: k at most 0.08). <see cref="Moving"/> shafts breathe and drift in play.</summary>
public sealed record MoonfallShafts(Vector2 Origin, float[] Angles, float[] Widths, float K, Vector3 Colour, int Seed, float Reach, float Near, bool Moving) : MoonfallLightRecipe;

/// <summary>A soft pool of light (dress2.glow), board units.</summary>
public sealed record MoonfallGlow(float X, float Y, float R, Vector3 Colour, float K) : MoonfallLightRecipe;

/// <summary>The moon's scattered light (rich_lib.moon_glow), board units (it may sit beyond the board).</summary>
public sealed record MoonfallMoonGlow(float X, float Y, float RCore, float RWide, float KCore, float KWide, Vector3 Colour) : MoonfallLightRecipe;

/// <summary>An emissive full moon painted on the scene (rich_lib.moon_emissive); it is also the moon Fever swells.</summary>
public sealed record MoonfallMoon(float X, float Y, float R, int Seed) : MoonfallLightRecipe;

/// <summary>A curtain of aurora over the sea (dress2.aurora): its middle line y, strength k.</summary>
public sealed record MoonfallAurora(float Y, float K) : MoonfallLightRecipe;

/// <summary>A nebula in the scene's empty black (dress2.nebula), strength k.</summary>
public sealed record MoonfallNebula(float K, int Seed) : MoonfallLightRecipe;

/// <summary>A chart's compass rose engraved in gilt light (dress2.compass_rose), kept 6 units from every piece.</summary>
public sealed record MoonfallCompassRose(float X, float Y, float R) : MoonfallLightRecipe;

/// <summary>A chart's neat-line on the frame's own edge with its degree ticks (dress2.chart_border), kept out of the launcher's span.</summary>
public sealed record MoonfallNeatline(float Inset) : MoonfallLightRecipe;

/// <summary>An engraved dashed route (scene_airship_road.dashed), through Catmull-Rom smoothed points.</summary>
public sealed record MoonfallRoute(Vector2[] Points, int Smooth, Vector3 Colour, float Width, float Dash, float Gap, float Alpha) : MoonfallLightRecipe;

/// <summary>A framing shape, drawn into its group's silhouette.</summary>
public abstract record MoonfallShapeRecipe;

/// <summary>A drooping frond of leaves (dress2.frond): laurel, oak, fir, fern or willow.</summary>
public sealed record MoonfallFrond(string Style, float X, float Y, float Length, float Angle, float Droop, float Leaf, int Leaves, int Seed, float Width, int Twigs) : MoonfallShapeRecipe;

/// <summary>A trunk with bark (dress2.trunk).</summary>
public sealed record MoonfallTrunk(float X, float Y0, float Y1, float W0, float W1, float Lean, int Seed) : MoonfallShapeRecipe;

/// <summary>Firs (dress2.pines): (x, base y, height, width) each, shortened until clear.</summary>
public sealed record MoonfallPines(Vector4[] Trees, int Seed) : MoonfallShapeRecipe;

/// <summary>An outcrop under a jagged ridge (dress2.outcrop), settled until clear.</summary>
public sealed record MoonfallOutcrop(Vector2[] Ridge, int Seed, float Rough) : MoonfallShapeRecipe;

/// <summary>An irregular drifting rock (the exp-p3 recipe's rocks).</summary>
public sealed record MoonfallRock(float X, float Y, float R, int Seed) : MoonfallShapeRecipe;

/// <summary>A faceted crystal spire (dress2.crystal): lit stone drawn directly, counted as framing.</summary>
public sealed record MoonfallCrystal(float X, float Y, float H, float W, float Tilt, Vector3 Body, Vector3 Lit) : MoonfallShapeRecipe;

/// <summary>A rope hanging between two points with a sag (the exp-p2 recipe's rigging); thin, so it catches no rim.</summary>
public sealed record MoonfallRope(Vector2 From, Vector2 To, float Sag, float Width) : MoonfallShapeRecipe;

/// <summary>A group of framing shapes shaded as one dark foreground silhouette (dress2.silhouette).</summary>
public sealed record MoonfallFramingGroup(Vector3 Body, Vector3 Inner, float InnerK, Vector3 Rim, float RimK, float RimWidth, Vector3? Snow, int Seed, IReadOnlyList<MoonfallShapeRecipe> Shapes);

/// <summary>A small light (dress2.points): a core and a halo, left out when it would come within 8 units of a piece.</summary>
public sealed record MoonfallSmallLight(float X, float Y, float Size, Vector3 Colour, float Core, float K, float Halo, float HaloK, bool Flicker);

/// <summary>Fireflies picked in <see cref="Region"/> where their whole wander and halo keep 8 units from every piece (F5).</summary>
public sealed record MoonfallFireflies(int Count, int Seed, Vector4 Region, Vector3 Colour);

/// <summary>A mist layer scrolling across a band of the board, wrapping at its tile width.</summary>
public sealed record MoonfallMist(float Y0, float Y1, float Speed, float Alpha, float Cell, int Seed, Vector3 Colour);

/// <summary>What moves in play (spec-rich2.md §4–5): all of it still under Reduce motion.</summary>
public sealed record MoonfallMotionRecipe
{
    /// <summary>Moondust motes drifting in the moving shafts.</summary>
    public int Dust { get; init; }

    /// <summary>Stars twinkling on the scene's own bright points in this region (x0, y0, x1, y1); 0 stars for none.</summary>
    public int Stars { get; init; }

    public Vector4 StarRegion { get; init; } = new(75, 41, 725, 330);

    public IReadOnlyList<MoonfallMist> Mist { get; init; } = [];
}

/// <summary>The chrome's palette for the level (r2lib.PALETTES): the rails' enamel and the margins.</summary>
public sealed record MoonfallChromePalette(Vector3 Sky, Vector3 Deep, Vector3 Jewel1, Vector3 Jewel2)
{
    /// <summary>The Medallion's lapis (the chart palette, base-p1).</summary>
    public static MoonfallChromePalette Medallion { get; } =
        new(MoonfallColor.Hex("#0E1C4E"), MoonfallColor.Hex("#070C24"), MoonfallColor.Hex("#1D4DB8"), MoonfallColor.Hex("#137C86"));
}

/// <summary>
/// A level's scene recipe (format <c>moonfall-scene</c>, version 1; docs/design/v9/scene-recipe.md): its painting, the
/// grade, the palette, the light, the framing, the small lights, the veil and what moves. Shipped as JSON in
/// <c>Tsukimichi.Core/Moonfall/Levels/scenes/&lt;name&gt;.json</c> and read by <see cref="MoonfallSceneRecipeLoader"/>;
/// built at load, off the framework thread, by <see cref="MoonfallSceneBuilder"/>.
/// </summary>
public sealed record MoonfallSceneRecipe
{
    public required string Name { get; init; }

    public required MoonfallSceneSource Source { get; init; }

    /// <summary>A shipped picture drawn instead when the game texture is missing or changed; null: the night sky.</summary>
    public string? Fallback { get; init; }

    /// <summary>The night grade; null for a picture already painted in the night's values.</summary>
    public MoonfallNightGrade? Grade { get; init; }

    public float Vignette { get; init; }

    public float Grain { get; init; }

    public MoonfallPaletteRecipe? Palette { get; init; }

    /// <summary>Layers painted on the graded scene before the vignette and the palette (the palette recolours them).</summary>
    public IReadOnlyList<MoonfallLightRecipe> Paint { get; init; } = [];

    /// <summary>Layers of light after the palette (it keeps their colour).</summary>
    public IReadOnlyList<MoonfallLightRecipe> Light { get; init; } = [];

    public IReadOnlyList<MoonfallFramingGroup> Framing { get; init; } = [];

    public IReadOnlyList<MoonfallSmallLight> Lights { get; init; } = [];

    public MoonfallFireflies? Fireflies { get; init; }

    /// <summary>How much the scene recedes behind and round the layout (0.20 on our dark paintings, 0.40–0.44 on official ones).</summary>
    public float Veil { get; init; } = 0.30f;

    public MoonfallMotionRecipe Motion { get; init; } = new();

    public MoonfallChromePalette Chrome { get; init; } = MoonfallChromePalette.Medallion;

    /// <summary>The moon Fever swells: the recipe's painted moon, or one it names; null for none (the sky still lifts).</summary>
    public MoonfallMoon? Moon { get; init; }

    /// <summary>The levels (ids) whose files name no scene that take this one (<see cref="MoonfallSceneRecipeLoader.Pick"/>).</summary>
    public IReadOnlyList<string> Levels { get; init; } = [];

    /// <summary>Whether this is the scene a level with no scene and no recipe naming it takes (one recipe at most).</summary>
    public bool Default { get; init; }
}
