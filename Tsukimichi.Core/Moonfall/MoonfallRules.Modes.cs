namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// The modes (plan v9 G7): the duel, the challenges and the Ace scores. Each value cites its line in
/// <c>docs/research/plan-v9/peg-mechanics.md</c> as <c>[R §n l.N]</c>, or is <c>[J]</c>, set by judgement where the
/// research is silent or in conflict, with the reasoning beside it. <c>docs/design/v9/moonfall-modes.md</c> has them all.
/// </summary>
public static partial class MoonfallRules
{
    // ---- The duel [R §6 l.127–128, §2 l.47, §3 l.74–92] ----

    /// <summary>
    /// [J] Balls each side has in a duel: "5 or 6 each depending on platform" [R §6 l.127]. Five, so a duel is ten shots
    /// on one board, as long as a level of the campaign.
    /// </summary>
    public const int DuelBallsPerSide = 5;

    /// <summary>
    /// "Failing to hit any orange loses 25% of score" [R §3 l.92, §6 l.128]. [J] The shot's score, not the running
    /// total: a turn that lights no orange keeps three quarters of what it scored. Taking a quarter of everything won so
    /// far would let one weak shot undo a whole game, which the original's duels do not play like.
    /// </summary>
    public const int DuelNoOrangeKeepPercent = 75;

    /// <summary>
    /// [J] The duel's Fever buckets, left to right: "reduced bucket values" [R §6 l.128] with no number given; a quarter of
    /// the campaign's (rounded to 2,500), within the research's "about 1/5 to 1/2" for the duel's style values.
    /// </summary>
    public static ReadOnlySpan<int> DuelFeverBucketValues => [2_500, 12_500, 25_000, 12_500, 2_500];

    /// <summary>[J] A perfect clear in a duel: every bucket pays the duel's best, 25,000 (a quarter of the campaign's).</summary>
    public const int DuelPerfectFeverBucketValue = 25_000;

    /// <summary>The duel's One Peg Catch [R §3 l.75: 5,000 (2,500)].</summary>
    public const int DuelOnePegCatchBonus = 2_500;

    /// <summary>The duel's Long Shot [R §3 l.76: 25,000 (5,000)].</summary>
    public const int DuelLongShotBonus = 5_000;

    /// <summary>The duel's Super Long Shot [R §3 l.77: 50,000 (10,000)].</summary>
    public const int DuelSuperLongShotBonus = 10_000;

    /// <summary>The duel's Double Long Shot [R §3 l.78: 25,000 (10,000), "as quoted"].</summary>
    public const int DuelDoubleLongShotBonus = 10_000;

    /// <summary>The duel's Off the Wall [R §3 l.79: 25,000 (5,000)].</summary>
    public const int DuelOffTheWallBonus = 5_000;

    /// <summary>The duel's Rim Shot [R §3 l.80: 25,000 (5,000)].</summary>
    public const int DuelRimShotBonus = 5_000;

    /// <summary>[J] The duel's Lucky Bounce: the sources give 2,500 or 5,000 [R §3 l.81]; 5,000, the same as its peers at 25,000.</summary>
    public const int DuelLuckyBounceBonus = 5_000;

    /// <summary>The duel's Orange Sweep [R §3 l.82: 50,000 (5,000)].</summary>
    public const int DuelOrangeSweepBonus = 5_000;

    /// <summary>The duel's Long Slide [R §3 l.83: 50,000 (5,000)].</summary>
    public const int DuelLongSlideBonus = 5_000;

    /// <summary>[J] The duel's Clear Night: none is given [R §3 l.84]; 10,000, a fifth of its 50,000 as the Super Long Shot's.</summary>
    public const int DuelClearNightBonus = 10_000;

    /// <summary>The duel's Live Wire [R §3 l.85: 25,000 (5,000)].</summary>
    public const int DuelLiveWireBonus = 5_000;

    // ---- Challenges [R §6 l.126, l.129–130] ----

    /// <summary>[J] The most oranges a challenge may ask for: the research's largest is 45 [R §6 l.129].</summary>
    public const int MaxChallengeOranges = 45;

    /// <summary>[J] The most balls a challenge may give (the campaign's ten, and room for a generous one).</summary>
    public const int MaxChallengeBalls = 20;

    /// <summary>The most levels one challenge runs through: "multilevel runs of 2-6 random levels" [R §6 l.129].</summary>
    public const int MaxChallengeLevels = 6;

    // ---- Ace scores [R §3 l.93] ----

    /// <summary>
    /// [J] What an Ace adds to the level's score: "beating it grants a bonus" [R §3 l.93], size not found. 25,000, a
    /// style shot's worth: enough to notice, small beside the score that earned it.
    /// </summary>
    public const int AceBonus = 25_000;
}

/// <summary>The rules a game is played by: the campaign's, or a duel's (plan v9 G7).</summary>
public enum MoonfallRuleSet : byte
{
    /// <summary>One player, ten balls, the campaign's values.</summary>
    Standard,

    /// <summary>
    /// Two sides take turns on one board [R §6 l.128]: one green at a time [R §2 l.47], the duel's style and Fever
    /// values, no bonus for balls left, and each side's powers its own.
    /// </summary>
    Duel,
}
