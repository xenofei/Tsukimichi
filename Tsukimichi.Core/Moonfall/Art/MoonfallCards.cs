using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// A power's companion (spec-rich2.md §3, characters.md; decision 22): the real FFXIV character who carries it, shown
/// with their Triple Triad card from the player's install, and the accent colour their power is drawn in.
/// </summary>
/// <param name="Power">The power.</param>
/// <param name="CardIcon">The card's icon id (<c>ui/icon/087000/0870NN_hr1.tex</c>).</param>
/// <param name="Accent">The companion's colour: their portrait's glow, their power's name, the turns-left gems, the effect.</param>
/// <param name="Face">The round portrait's square crop of the 208 × 256 card, hr pixels (the plugin's card crop).</param>
public sealed record MoonfallCard(MoonfallPower Power, uint CardIcon, Vector3 Accent, (int X, int Y, int Size) Face)
{
    /// <summary>How many turns the power lasts (<see cref="MoonfallPowers.Shots"/>): one turns-left gem each.</summary>
    public int Turns => MoonfallPowers.Shots(Power);

    /// <summary>The card's game path at hr1.</summary>
    public string CardPath => MoonfallCards.CardPath(CardIcon);
}

/// <summary>The eleven companions (r2cast.CAST): their cards, accents (at least 25° apart in OKLab hue, off the gilt) and face crops.</summary>
public static class MoonfallCards
{
    /// <summary>The card family's face box (giver_portraits.json crops.TripleTriadCard): x, y, side in hr pixels.</summary>
    public static readonly (int X, int Y, int Size) CardFace = (28, 23, 135);

    /// <summary>The art inside the card's gilt border (PortraitSources.ArtBounds(TripleTriadCard)), as fractions of the card.</summary>
    public static readonly Vector4 ArtBounds = new(0.067f, 0.055f, 0.933f, 0.945f);

    /// <summary>A card's size, hr pixels.</summary>
    public const int CardWidth = 208;

    /// <inheritdoc cref="CardWidth"/>
    public const int CardHeight = 256;

    private static readonly MoonfallCard[] All =
    [
        new(MoonfallPower.SuperGuide, 87056, MoonfallColor.Hex("#5DDAE0"), CardFace),
        new(MoonfallPower.Multiball, 87059, MoonfallColor.Hex("#66A5FF"), CardFace),
        new(MoonfallPower.Wings, 87058, MoonfallColor.Hex("#E69461"), CardFace),
        new(MoonfallPower.Burst, 87067, MoonfallColor.Hex("#F9786C"), CardFace),
        new(MoonfallPower.Flippers, 87065, MoonfallColor.Hex("#73C881"), CardFace),
        new(MoonfallPower.Gate, 87050, MoonfallColor.Hex("#9E94FD"), CardFace),
        new(MoonfallPower.Bloom, 87066, MoonfallColor.Hex("#B5CA4E"), CardFace),
        new(MoonfallPower.Draw, 87019, MoonfallColor.Hex("#EE83AF"), CardFace),
        new(MoonfallPower.Fireball, 87049, MoonfallColor.Hex("#CB82E7"), CardFace),
        new(MoonfallPower.Path, 87060, MoonfallColor.Hex("#70D3BB"), CardFace),
        // The moogle keeps its pom-pom and wings in the ring, so it never reads as a cat.
        new(MoonfallPower.Bolt, 87020, MoonfallColor.Hex("#5DCDFA"), (14, 14, 180)),
    ];

    /// <summary>Every companion, in power order.</summary>
    public static IReadOnlyList<MoonfallCard> Cast => All;

    /// <summary>The companion who carries <paramref name="power"/>; null for none.</summary>
    public static MoonfallCard? For(MoonfallPower power) => power is > MoonfallPower.None and <= MoonfallPower.Bolt ? All[(int)power - 1] : null;

    /// <summary>A Triple Triad card's game path (hr1).</summary>
    public static string CardPath(uint icon) => $"ui/icon/{icon / 1000 * 1000:D6}/{icon:D6}_hr1.tex";
}
