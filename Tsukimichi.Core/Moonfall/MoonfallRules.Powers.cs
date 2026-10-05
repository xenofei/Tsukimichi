namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// The powers (plan v9 G5) and the style shots (G4). Each value cites its line in
/// <c>docs/research/plan-v9/peg-mechanics.md</c> as <c>[R §n l.N]</c>, or is <c>[J]</c>, set by judgement where the
/// research is silent or in conflict, with the reasoning beside it. <c>docs/design/v9/moonfall-powers.md</c> lists them
/// all with our names for the original's.
/// </summary>
public static partial class MoonfallRules
{
    // ---- When a power acts [R §5 l.103] ----
    // A green peg lit by any means triggers the level's power ("Power activates when the ball hits a green peg").
    // [J] A green lit by another power (a burst, a bolt, the nearest-orange bloom) triggers it too: it is lit like any
    // other peg, and the level has only two greens, so the chain is bounded.
    // [J] Stacking: a second green adds the power's shots to those left (the counter adds up, never resets), so no
    // green hit is ever wasted. Powers that act once at the hit (a twin ball, a burst, a bloom, a draw) act again.

    /// <summary>Super Guide: the guide shows the path through its first bounce for 3 shots, from the next shot [R §1 l.41, §5 l.107].</summary>
    public const int SuperGuideShots = 3;

    /// <summary>
    /// [J] How far (px along the path) the Super Guide's line runs past the first bounce when it meets nothing. The
    /// research says only "through one bounce" [R §1 l.41]; 300 px is half the board's height, long enough to show where
    /// the bounce sends the ball and short of solving the whole shot.
    /// </summary>
    public const double SuperGuideMaxLength = 300;

    /// <summary>The most points the Super Guide's line writes (one every <see cref="GuideDotSpacing"/>, plus its two ends).</summary>
    public const int SuperGuideMaxPoints = 20;

    /// <summary>
    /// [J] The most balls in play at once. A twin ball springs from each green [R §5 l.108] and a level has two greens,
    /// so three is the most the rules can make; a twin beyond it is not made.
    /// </summary>
    public const int MaxBalls = 3;

    /// <summary>
    /// [J] The twin ball leaves the top of the green peg with the hitting ball's velocity mirrored left to right, so the
    /// two fan out from the peg as a pair. A ball that met the peg nearly head-on has almost no sideways speed to
    /// mirror; the twin then takes at least this much (px/s) to the side that is open, so the two never fly as one.
    /// </summary>
    public const double TwinMinSideSpeed = 80;

    /// <summary>Brass Wings (the original's Pyramid): the bucket is widened for 5 turns [R §5 l.109].</summary>
    public const int WingsShots = 5;

    /// <summary>
    /// [J] The winged bucket's mouth (px). The research says the frame "widens" the bucket [R §5 l.109] without a size;
    /// twice the plain mouth (104 [M 5]) reads at once as a different bucket and still leaves the sweep's ends open.
    /// The rims move out with it: a ball over the wider mouth is caught, one on a wing's tip bounces.
    /// </summary>
    public const double WingsMouth = BucketMouth * 2;

    /// <summary>
    /// [J] Lunar Burst (the original's Space Blast) lights every peg whose surface is within this distance of the green
    /// peg's centre [R §5 l.110: "within a radius (about 4 pegs wide)"]. Read as four peg diameters (4 × 20 px), not
    /// four pegs across in all: Moonfall's levels set pegs 34–45 px apart, so an 80 px diameter would reach only the
    /// green's next-door pegs, which is no blast. 80 px lights about a dozen pegs in a typical field.
    /// </summary>
    public const double BurstRadius = 80;

    /// <summary>
    /// [J] Flippers last 3 turns. The sources conflict [R §5 l.111: "patent: lasts the shot; wiki: 3 turns"]; the wiki's
    /// count is the released game's, and a power the player steers is worth more than one shot to learn.
    /// </summary>
    public const int FlippersShots = 3;

    /// <summary>[J] The left flipper's pivot: just inside the wall, low enough to sit beside the bucket's sweep.</summary>
    public const double FlipperPivotX = LeftWall + 12;

    /// <summary>[J] The flippers' pivot height: 48 px over the rim's top, so a raised flipper still clears the bucket.</summary>
    public const double FlipperPivotY = 525;

    /// <summary>[J] A flipper's length from its pivot to its tip's centre: a little under a bucket's width.</summary>
    public const double FlipperLength = 95;

    /// <summary>[J] A flipper's thickness: about half a peg's diameter, enough to read as a fan at the smallest window.</summary>
    public const double FlipperThickness = 10;

    /// <summary>[J] At rest a flipper slopes 30° down towards the middle, so a ball that lands on it rolls off towards the bucket.</summary>
    public const double FlipperRestDegrees = 30;

    /// <summary>[J] Raised, it points 20° above level: a 50° swing, as a pinball flipper's.</summary>
    public const double FlipperUpDegrees = -20;

    /// <summary>[J] The swing up takes 0.10 s: the tip moves at about 830 px/s, enough to send a ball back into the pegs.</summary>
    public const double FlipperUpSeconds = 0.10;

    /// <summary>[J] It falls back in 0.15 s, a little slower than it rises, as a sprung flipper does.</summary>
    public const double FlipperDownSeconds = 0.15;

    /// <summary>
    /// [J] A flipper's bounce, normal to its face. Its swing gives the kick, so the bounce itself is soft: with the
    /// walls' 0.75 a ball falling onto a rising flipper would leave at over 1,200 px/s and slam the ceiling.
    /// </summary>
    public const double FlipperRestitution = 0.5;

    /// <summary>Moon Gate (the original's Spooky Ball): the ball falls out once and comes back in at the top, this shot only [R §5 l.112].</summary>
    public const int GateReentries = 1;

    /// <summary>
    /// [J] Where a ball comes back in: at the same x, just under the ceiling, with the velocity it fell out with (a wrap,
    /// as the research's "reappears at top" reads). Its last place is moved with it, so it is not drawn streaking up.
    /// </summary>
    public const double GateReentryY = Ceiling + BallRadius + 4;

    /// <summary>Moonbloom (the original's Flower Power) lights the closest one fifth, rounded up, of the oranges left [R §5 l.113].</summary>
    public const int BloomShare = 5;

    /// <summary>
    /// [J] Moon-Viewing Draw (the original's Lucky Spin): the drum's twelve balls, by outcome. The research names a free
    /// ball, a triple score, a "Magic Hat" (a random master) and a random power [R §5 l.114, l.120] but no odds; the hat
    /// and the random power both give another master's power, so here they are one outcome, "another power". Equal
    /// thirds are what an honest drum holds (Gyobo is "sure the drum is never rigged"); twelve balls leave room to
    /// retune without changing how a draw is made.
    /// </summary>
    public static ReadOnlySpan<byte> DrawDrum => [4, 4, 4];

    /// <summary>
    /// [J] A triple score lasts this shot and the next. The research's "lasts 2 turns" [R §5 l.114] has nothing else
    /// that could last: the free ball is had at once and another power keeps its own duration.
    /// </summary>
    public const int TripleScoreShots = 2;

    /// <summary>A triple score multiplies the shot's peg score by 3 [R §5 l.114].</summary>
    public const int TripleScoreFactor = 3;

    /// <summary>Fireball: the next shot burns through the pegs it meets, one shot [R §5 l.115].</summary>
    public const int FireballShots = 1;

    /// <summary>Sage's Path (the original's Zen Ball): the next shot is nudged onto the best path, one shot [R §5 l.116].</summary>
    public const int PathShots = 1;

    /// <summary>
    /// [J] How far the nudge may turn the shot (degrees either side of the aim). The research says "nudges" [R §5 l.116]:
    /// 4° moves the ball about 20 px by the time it reaches the middle of the board, enough to find a better line
    /// through the pegs and small enough to stay the player's shot.
    /// </summary>
    public const double PathSpreadDegrees = 4;

    /// <summary>[J] The search tries every 0.5° in that spread: 17 shots, each a distinct line through a field of 20 px pegs.</summary>
    public const double PathStepDegrees = 0.5;

    /// <summary>
    /// [J] The search's cost cap: the physics sub-steps all candidate flights may take together, shared out evenly. A
    /// shot typically takes 3–4 sub-steps a tick, so each of the 17 flights is followed about 3.5 s, past where most
    /// shots end. The search is about 2 ms on the largest shipped level and up to about 30 ms on a board of 400 pegs, so
    /// it is spread one flight a game tick while the ball waits in the barrel (<c>MoonfallGame.BeginPath</c>): a tick's
    /// share is capped at a seventeenth of this.
    /// </summary>
    public const int PathSubStepBudget = 24_000;

    /// <summary>
    /// [J] What a candidate flight is worth: its shot score by the game's own rule (peg values × pegs), plus this for each
    /// orange, so the search weights the oranges as the research says [R §5 l.116] beyond their peg value.
    /// </summary>
    public const long PathOrangeWeight = 2_000;

    /// <summary>[J] And this for a catch in the bucket, a free ball: worth about four oranges, the "weighting the bucket" of [R §5 l.116].</summary>
    public const long PathCatchWeight = 10_000;

    /// <summary>
    /// [J] Storm Post (the original's Electrobolt) is armed by the green and acts on the next shot, one shot: the bolt
    /// starts at "the first peg hit" [R §5 l.117], which on the green's own shot has already been and gone. The research
    /// gives no duration.
    /// </summary>
    public const int BoltShots = 1;

    /// <summary>
    /// [J] The bolt runs from the first peg the ball lights to the bucket's centre and lights every peg whose centre is
    /// within this distance of its line [R §5 l.117: "clearing pegs in its path"], jumping peg to peg in order along it.
    /// 26 px is a peg's radius plus a ball's diameter and a little: the pegs a ball rolling down that line would touch.
    /// </summary>
    public const double BoltReach = 26;

    /// <summary>The most points the bolt's drawn path keeps (its two ends and the pegs between).</summary>
    public const int BoltMaxPoints = 48;

    // ---- Style shots [R §3 l.73–87] ----
    // [J] Each style shot is paid at most once a shot; its bonus is added to the shot's score after the peg score
    // (values × pegs), and is not multiplied by the Fever meter [R §3 l.87, unverified], nor by a triple score. It counts
    // towards the free balls, which come from the shot's score [R §3 l.70, P: "adds any style points to the shot score"].

    /// <summary>One-Peg Catch (the original's "Free Ball Skillz"): one peg, then the bucket [R §3 l.75].</summary>
    public const int OnePegCatchBonus = 5_000;

    /// <summary>Long Shot [R §3 l.76].</summary>
    public const int LongShotBonus = 25_000;

    /// <summary>Super Long Shot [R §3 l.77].</summary>
    public const int SuperLongShotBonus = 50_000;

    /// <summary>Double Long Shot [R §3 l.78].</summary>
    public const int DoubleLongShotBonus = 25_000;

    /// <summary>Off the Wall [R §3 l.79].</summary>
    public const int OffTheWallBonus = 25_000;

    /// <summary>Rim Shot (the original's "Kick the Bucket") [R §3 l.80].</summary>
    public const int RimShotBonus = 25_000;

    /// <summary>Lucky Bounce [R §3 l.81].</summary>
    public const int LuckyBounceBonus = 25_000;

    /// <summary>Orange Sweep (the original's "Orange Attack") [R §3 l.82].</summary>
    public const int OrangeSweepBonus = 50_000;

    /// <summary>Long Slide (the original's "Extreme Slide") [R §3 l.83].</summary>
    public const int LongSlideBonus = 50_000;

    /// <summary>Clear Night (the original's "Cool Clear") [R §3 l.84].</summary>
    public const int ClearNightBonus = 50_000;

    /// <summary>Live Wire (the original's "Shock It To Me") [R §3 l.85].</summary>
    public const int LiveWireBonus = 25_000;

    /// <summary>
    /// A long leg: a ball lights a non-blue peg, then another at least a third of the screen's width (800 / 3) away
    /// [R §3 l.76]. [J] Measured as the straight distance between the two pegs, which is what the player sees; the
    /// path the ball took between them is not counted, so a wall bounce does not make a short hop long.
    /// </summary>
    public const double LongShotDistance = Width / 3;

    /// <summary>A super long leg: two thirds of the screen's width [R §3 l.77].</summary>
    public const double SuperLongShotDistance = Width * 2 / 3;

    /// <summary>
    /// [J] "Next or soon after" [R §3 l.76]: the second non-blue peg may come after at most one blue peg. One stray blue
    /// on the way still reads as one long flight; two or more is a run through the field, not a long shot.
    /// </summary>
    public const int LongShotBluesAllowed = 1;

    /// <summary>
    /// Off the Wall: from a wall bounce to a non-blue peg a fifth of the screen's width (160 px) away [R §3 l.79]. [J] The
    /// peg must be the first the ball lights after the wall.
    /// </summary>
    public const double OffTheWallDistance = Width / 5;

    /// <summary>
    /// Lucky Bounce: off the bucket "for a certain time or 1/4 screen height", then caught [R §3 l.81]. [J] The time is
    /// 0.5 s from the first rim bounce since the ball last lit a peg: any real bounce off a rim takes longer than that to
    /// come back down, while a ball that rattles on a rim and drops in does not.
    /// </summary>
    public const int LuckyBounceTicks = 50;

    /// <summary>Or the ball rises a quarter of the screen's height above that bounce [R §3 l.81].</summary>
    public const double LuckyBounceRise = Height / 4;

    /// <summary>
    /// [J] Orange Sweep: a shot lights at least a third (rounded up) of the oranges left when it was fired, and never
    /// fewer than <see cref="OrangeSweepMinimum"/>. The research says only "a large share … depends on how many remain"
    /// [R §3 l.82]; a third is 9 of a fresh level's 25, a rare and memorable shot, and 4 of the last 10.
    /// </summary>
    public const int OrangeSweepShare = 3;

    /// <summary>[J] At least three oranges, so the last two or three are not a sweep for the asking.</summary>
    public const int OrangeSweepMinimum = 3;

    /// <summary>Long Slide: a ball slides along pegs and lights 12 [R §3 l.83].</summary>
    public const int LongSlidePegs = 12;

    /// <summary>
    /// [J] A slide is unbroken while each touch is soft (<see cref="SlideBounceSpeed"/>) and the ball touches a peg again
    /// within this many game ticks (0.2 s). A ball running down bricks chatters in small hops of 0.05–0.1 s (traced on a
    /// 22° row of bricks), so the gap allows twice that; a ball that leaves the pegs for longer has flown, not slid.
    /// </summary>
    public const int SlideGapTicks = 20;

    /// <summary>
    /// [J] A touch that meets the peg faster than this (px/s along the contact's normal) is a bounce and starts the count
    /// again. A ball sliding along bricks meets each at 20–60 px/s; one bouncing between pegs meets them at hundreds.
    /// </summary>
    public const double SlideBounceSpeed = 80;

    /// <summary>
    /// Clear Night: every peg lit or gone by the end of the Full Moon shot, which lit at least two pegs [R §3 l.84]. [J]
    /// Checked as the ball lands in a Fever bucket, which covers both readings the research found: everything lit at
    /// the last orange, and the last orange then everything left.
    /// </summary>
    public const int ClearNightMinPegs = 2;

    /// <summary>
    /// Live Wire: a bolt lights 12 pegs or more [R §3 l.85: "Electrobolt shot lights 12+ pegs"]. [J] Counted as the pegs
    /// the bolt itself lights, which is what the style shot rewards: aiming the first hit so the bolt crosses a crowd.
    /// </summary>
    public const int LiveWirePegs = 12;
}
