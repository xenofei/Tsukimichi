namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// Every atlas sprite Moonfall's board draws besides the pegs and bricks (feature plan v9 G8; the atlas contract is
/// <see cref="MoonfallAtlas"/>). The drawing code names sprites only through this enum, <see cref="MoonfallSprites.Peg"/>
/// and <see cref="MoonfallSprites.Brick"/>, so the manifest check (<see cref="MoonfallAtlas.Parse"/>) covers every sprite
/// it can ask for, and a new art set drops in by replacing the files alone.
/// </summary>
public enum MoonfallSprite
{
    /// <summary>A lit peg's halo: white, tinted by its kind's <c>glow.*</c> ink. Anchor: the centre, drawn for r 10.</summary>
    Halo,

    /// <summary>The clearing's first-frame bloom round the limb, white. Anchor: the centre, drawn for r 10.</summary>
    Bloom,

    /// <summary>A unit soft light, white: the lantern's air glow, the free-ball notch bloom, a lit Fever cup. Anchor: the centre.</summary>
    Soft,

    /// <summary>A speck of moondust, white, drawn for a speck 1 unit across. Anchor: the centre.</summary>
    Speck,

    /// <summary>Plain's flat peg, white. Anchor: the centre, drawn for r 10.</summary>
    Disc,

    /// <summary>Plain's flat peg's unlit part, white (tinted by <c>flat.*.shade</c>). Anchor: the centre, drawn for r 10.</summary>
    Sliver,

    /// <summary>Plain's lit ring (and the high-contrast palettes' lit mark), white. Anchor: the centre, drawn for r 10.</summary>
    Ring,

    /// <summary>One dot of the aim guide. Anchor: the centre.</summary>
    Dot,

    /// <summary>The ball, drawn for r 6. Anchor: the centre.</summary>
    Ball,

    /// <summary>Plain's flat brick, white, laid out like the brick sprites.</summary>
    BrickFlat,

    /// <summary>The launcher's yoke from the top rail. Anchor: the pivot.</summary>
    LauncherYoke,

    /// <summary>The launcher's tube pointing straight down, turned about the pivot to the aim. Anchor: the pivot.</summary>
    LauncherTube,

    /// <summary>The launcher's hub, over the tube. Anchor: the pivot.</summary>
    LauncherHub,

    /// <summary>The free-ball gauge's empty channel. Anchor: the pivot.</summary>
    LauncherGauge,

    /// <summary>The free-ball gauge's light, drawn up to the shot's progress between the manifest's <c>gauge</c> angles. Anchor: the pivot.</summary>
    LauncherGaugeFill,

    /// <summary>Bucket A, the crescent cradle. Anchor: the bucket's centre on its rim line (y 573).</summary>
    BucketCradle,

    /// <summary>The rail bucket A rides, stretched from wall to wall. Anchor: its left end.</summary>
    BucketRail,

    /// <summary>Bucket B, the lantern boat (its reflection is this sprite mirrored). Anchor: the bucket's centre on its rim line.</summary>
    BucketBoat,

    /// <summary>Where the boat's hull meets the water. Anchor: the bucket's centre on its rim line.</summary>
    BucketBoatContact,

    /// <summary>The strip of water bucket B floats on, from wall to wall. Anchor: its left end on the waterline.</summary>
    BucketWater,

    /// <summary>The lantern's warm column on the water. Anchor: below the lantern, on the waterline.</summary>
    BucketColumn,

    /// <summary>The frame's outer bead, a nine-slice (<c>slices</c>) round the whole board.</summary>
    FrameBeadOuter,

    /// <summary>The rails' bead along the board's edge, a nine-slice (<c>slices</c>) whose bottom row is not drawn.</summary>
    FrameBeadInner,

    /// <summary>The rails' enamel mottling, a tile (Full only).</summary>
    FrameMottle,

    /// <summary>A Fever cup with its value plate. Anchor: the plate's centre.</summary>
    FeverCup,

    /// <summary>The centre Fever cup, gilded brightest. Anchor: the plate's centre.</summary>
    FeverCupCentre,

    /// <summary>The rule under the Full Moon banner. Anchor: its centre.</summary>
    FeverRule,

    /// <summary>The banner's soft band behind the pegs, white (tinted dark), stretched across the board. Anchor: its centre.</summary>
    FeverBand,
}

/// <summary>The manifest names of Moonfall's sprites (<see cref="MoonfallSprite"/>, the pegs and the bricks).</summary>
public static class MoonfallSprites
{
    /// <summary>The kinds' names in <see cref="PegColour"/> order.</summary>
    public static readonly string[] Kinds = ["blue", "orange", "green", "purple"];

    /// <summary>The names of <see cref="MoonfallSprite"/>'s sprites, in its order.</summary>
    public static readonly string[] Names =
    [
        "halo", "bloom", "soft", "speck", "disc", "sliver", "ring", "dot", "ball", "brick.flat",
        "launcher.yoke", "launcher.tube", "launcher.hub", "launcher.gauge", "launcher.gauge.fill",
        "bucket.cradle", "bucket.rail", "bucket.boat", "bucket.boat.contact", "bucket.water", "bucket.column",
        "frame.bead.outer", "frame.bead.inner", "frame.mottle",
        "fever.cup", "fever.cup.centre", "fever.rule", "fever.band",
    ];

    /// <summary>The name of <paramref name="sprite"/>.</summary>
    public static string Name(MoonfallSprite sprite) => Names[(int)sprite];

    /// <summary>A peg's sprite: <c>peg.&lt;kind&gt;.&lt;variant&gt;.&lt;unlit|lit&gt;</c>.</summary>
    public static string Peg(PegColour colour, int variant, bool lit) => $"peg.{Kinds[(int)colour]}.{variant}.{(lit ? "lit" : "unlit")}";

    /// <summary>A brick's sprite: <c>brick.&lt;kind&gt;.&lt;unlit|lit&gt;</c>.</summary>
    public static string Brick(PegColour colour, bool lit) => $"brick.{Kinds[(int)colour]}.{(lit ? "lit" : "unlit")}";

    /// <summary>Every name the board can draw with <paramref name="pegVariants"/> peg variants.</summary>
    public static IEnumerable<string> Required(int pegVariants)
    {
        foreach (var name in Names)
        {
            yield return name;
        }

        for (var kind = 0; kind < Kinds.Length; kind++)
        {
            for (var state = 0; state < 2; state++)
            {
                var lit = state == 1;
                for (var v = 0; v < pegVariants; v++)
                {
                    yield return Peg((PegColour)kind, v, lit);
                }

                yield return Brick((PegColour)kind, lit);
            }
        }
    }
}
