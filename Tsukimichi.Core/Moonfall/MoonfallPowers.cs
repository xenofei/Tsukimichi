namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// The eleven powers (plan v9 G5), one per character, by stable ids that name what each one does. The names the player
/// sees live in the window's strings (decision 8 may still rename them); the research's row for each is in
/// <c>peg-mechanics.md</c> §5, lines 107–117. Never renumber: progress and tests may hold these values.
/// </summary>
public enum MoonfallPower : byte
{
    /// <summary>No power: a green peg scores and nothing else.</summary>
    None = 0,

    /// <summary>Super Guide: the aim guide runs on through its first bounce [R §5 l.107].</summary>
    SuperGuide = 1,

    /// <summary>Multiball: a twin ball springs from the green peg [R §5 l.108].</summary>
    Multiball = 2,

    /// <summary>Brass Wings (the original's Pyramid): the bucket widens [R §5 l.109].</summary>
    Wings = 3,

    /// <summary>Lunar Burst (the original's Space Blast): every peg near the green lights [R §5 l.110].</summary>
    Burst = 4,

    /// <summary>Flippers at the foot's corners, worked by the player [R §5 l.111].</summary>
    Flippers = 5,

    /// <summary>Moon Gate (the original's Spooky Ball): a ball that falls out comes back in at the top [R §5 l.112].</summary>
    Gate = 6,

    /// <summary>Moonbloom (the original's Flower Power): the nearest fifth of the oranges left light [R §5 l.113].</summary>
    Bloom = 7,

    /// <summary>Moon-Viewing Draw (the original's Lucky Spin): a drum draws a free ball, a triple score or another power [R §5 l.114].</summary>
    Draw = 8,

    /// <summary>Fireball: the ball burns through pegs without bouncing [R §5 l.115].</summary>
    Fireball = 9,

    /// <summary>Sage's Path (the original's Zen Ball): the shot is nudged onto the best path [R §5 l.116].</summary>
    Path = 10,

    /// <summary>Storm Post (the original's Electrobolt): a bolt from the first peg hit to the bucket lights the pegs on its way [R §5 l.117].</summary>
    Bolt = 11,
}

/// <summary>When a power acts, from the green peg that triggers it.</summary>
public enum MoonfallPowerTiming : byte
{
    /// <summary>Once, at the hit (a twin ball, a burst, a bloom, a draw, a gate for this shot).</summary>
    Instant,

    /// <summary>From the hit, for a number of shots counting this one (wings, flippers).</summary>
    AtOnce,

    /// <summary>From the next shot, for a number of shots (Super Guide, Fireball, Sage's Path, Storm Post).</summary>
    NextShot,
}

/// <summary>What the drum gives (<see cref="MoonfallPower.Draw"/>).</summary>
public enum MoonfallDrawOutcome : byte
{
    FreeBall,
    TripleScore,
    AnotherPower,
}

/// <summary>
/// The style shots (plan v9 G4) by stable ids: plain names where the original's are general terms, our own where they
/// are distinctive (decision 2; the mapping is in <c>docs/design/v9/moonfall-powers.md</c>). Never renumber.
/// </summary>
public enum MoonfallStyleShot : byte
{
    /// <summary>One peg, then the bucket (the original's "Free Ball Skillz") [R §3 l.75].</summary>
    OnePegCatch = 0,

    /// <summary>Two non-blue pegs a third of the screen apart [R §3 l.76].</summary>
    LongShot = 1,

    /// <summary>Two non-blue pegs two thirds of the screen apart [R §3 l.77].</summary>
    SuperLongShot = 2,

    /// <summary>A long leg straight after another [R §3 l.78].</summary>
    DoubleLongShot = 3,

    /// <summary>A wall, then a non-blue peg a fifth of the screen away [R §3 l.79].</summary>
    OffTheWall = 4,

    /// <summary>Off the bucket and into the last orange (the original's "Kick the Bucket") [R §3 l.80].</summary>
    RimShot = 5,

    /// <summary>Off the bucket, up, and back into it [R §3 l.81].</summary>
    LuckyBounce = 6,

    /// <summary>A large share of the oranges in one shot (the original's "Orange Attack") [R §3 l.82].</summary>
    OrangeSweep = 7,

    /// <summary>Twelve pegs in one slide (the original's "Extreme Slide") [R §3 l.83].</summary>
    LongSlide = 8,

    /// <summary>Every peg lit by the end of the Full Moon shot (the original's "Cool Clear") [R §3 l.84].</summary>
    ClearNight = 9,

    /// <summary>A bolt that lights twelve pegs (the original's "Shock It To Me") [R §3 l.85].</summary>
    LiveWire = 10,
}

/// <summary>The powers' table and the drum: pure, allocation-free.</summary>
public static class MoonfallPowers
{
    /// <summary>How many powers there are (ids 1 to 11); arrays indexed by power are this long plus one.</summary>
    public const int Count = 11;

    /// <summary>How many style shots there are.</summary>
    public const int StyleCount = 11;

    /// <summary>When <paramref name="power"/> acts.</summary>
    public static MoonfallPowerTiming Timing(MoonfallPower power) => power switch
    {
        MoonfallPower.SuperGuide or MoonfallPower.Fireball or MoonfallPower.Path or MoonfallPower.Bolt => MoonfallPowerTiming.NextShot,
        MoonfallPower.Wings or MoonfallPower.Flippers => MoonfallPowerTiming.AtOnce,
        _ => MoonfallPowerTiming.Instant,
    };

    /// <summary>How many shots one green gives <paramref name="power"/> (1 for the ones that act at the hit).</summary>
    public static int Shots(MoonfallPower power) => power switch
    {
        MoonfallPower.SuperGuide => MoonfallRules.SuperGuideShots,
        MoonfallPower.Wings => MoonfallRules.WingsShots,
        MoonfallPower.Flippers => MoonfallRules.FlippersShots,
        MoonfallPower.Fireball => MoonfallRules.FireballShots,
        MoonfallPower.Path => MoonfallRules.PathShots,
        MoonfallPower.Bolt => MoonfallRules.BoltShots,
        MoonfallPower.None => 0,
        _ => 1,
    };

    /// <summary>
    /// One turn of the drum (<see cref="MoonfallRules.DrawDrum"/>): the outcome and, for
    /// <see cref="MoonfallDrawOutcome.AnotherPower"/>, which one, picked evenly from the other ten (never the drum
    /// itself, so a draw cannot draw again).
    /// </summary>
    public static MoonfallDrawOutcome Draw(ref MoonfallRandom random, out MoonfallPower power)
    {
        var drum = MoonfallRules.DrawDrum;
        var total = 0;
        foreach (var slots in drum)
        {
            total += slots;
        }

        var pick = random.Next(total);
        var outcome = MoonfallDrawOutcome.AnotherPower;
        for (var k = 0; k < drum.Length; k++)
        {
            if (pick < drum[k])
            {
                outcome = (MoonfallDrawOutcome)k;
                break;
            }

            pick -= drum[k];
        }

        power = MoonfallPower.None;
        if (outcome == MoonfallDrawOutcome.AnotherPower)
        {
            // Ids 1–11 less the drum (8): ten to choose from.
            var other = 1 + random.Next(Count - 1);
            power = other >= (int)MoonfallPower.Draw ? (MoonfallPower)(other + 1) : (MoonfallPower)other;
        }

        return outcome;
    }
}

/// <summary>
/// Who carries which power where (plan v9 G5, G7): Adventure's stages are five levels each, one character a stage,
/// as the original's were ("5 per Master … plus 5 Master levels where the player chooses any Master" [R §6 l.126]).
/// After the characters' stages comes one stage where the player picks. Quick Play lets the player pick any of the
/// eleven on any level (unlocking them stage by stage is G7's, with the modes).
/// </summary>
public static class MoonfallCharacters
{
    /// <summary>Levels a stage [R §6 l.126].</summary>
    public const int LevelsPerStage = 5;

    /// <summary>
    /// The base campaign's characters, a stage each, in the original's order of powers [R §5 l.107–116]: the guide that
    /// teaches the aim first, then the twin ball, the wings, the burst, the flippers, the gate, the bloom, the draw,
    /// Fireball and the path. Ten stages of five, then five levels where the player picks: 55 [R §6 l.126].
    /// </summary>
    public static ReadOnlySpan<byte> BaseOrder =>
    [
        (byte)MoonfallPower.SuperGuide,
        (byte)MoonfallPower.Multiball,
        (byte)MoonfallPower.Wings,
        (byte)MoonfallPower.Burst,
        (byte)MoonfallPower.Flippers,
        (byte)MoonfallPower.Gate,
        (byte)MoonfallPower.Bloom,
        (byte)MoonfallPower.Draw,
        (byte)MoonfallPower.Fireball,
        (byte)MoonfallPower.Path,
    ];

    /// <summary>
    /// [J] The expansion's: the same ten (the research does not give the second game's order), then the new character,
    /// Storm Post's courier, then the stage where the player picks: 11 × 5 + 5 = 60, the second campaign's size.
    /// </summary>
    public static ReadOnlySpan<byte> ExpansionOrder =>
    [
        (byte)MoonfallPower.SuperGuide,
        (byte)MoonfallPower.Multiball,
        (byte)MoonfallPower.Wings,
        (byte)MoonfallPower.Burst,
        (byte)MoonfallPower.Flippers,
        (byte)MoonfallPower.Gate,
        (byte)MoonfallPower.Bloom,
        (byte)MoonfallPower.Draw,
        (byte)MoonfallPower.Fireball,
        (byte)MoonfallPower.Path,
        (byte)MoonfallPower.Bolt,
    ];

    /// <summary>The stage (from 1) of the level at <paramref name="levelIndex"/> (from 0).</summary>
    public static int Stage(int levelIndex) => (Math.Max(0, levelIndex) / LevelsPerStage) + 1;

    /// <summary>The level's place in its stage (from 1).</summary>
    public static int LevelInStage(int levelIndex) => (Math.Max(0, levelIndex) % LevelsPerStage) + 1;

    /// <summary>
    /// The power Adventure gives the level at <paramref name="levelIndex"/> in <paramref name="campaign"/>, or
    /// <see cref="MoonfallPower.None"/> on a level where the player picks (<see cref="PlayerPicks"/>).
    /// </summary>
    public static MoonfallPower AdventurePower(MoonfallCampaignKind campaign, int levelIndex)
    {
        var order = campaign == MoonfallCampaignKind.Expansion ? ExpansionOrder : BaseOrder;
        var stage = Math.Max(0, levelIndex) / LevelsPerStage;
        return stage < order.Length ? (MoonfallPower)order[stage] : MoonfallPower.None;
    }

    /// <summary>Whether Adventure lets the player pick the character on this level: the stage after every character's.</summary>
    public static bool PlayerPicks(MoonfallCampaignKind campaign, int levelIndex) =>
        AdventurePower(campaign, levelIndex) == MoonfallPower.None;
}
