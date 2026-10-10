namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// Every number Moonfall plays by (feature plan v9 G1–G3), in the original's 800×600 playfield units: x to the right,
/// y down, px and seconds; angles in degrees with 0 straight down and positive to the right. Each value names its source:
/// <c>[M n]</c> is row n of the summary table in <c>docs/research/plan-v9/peg-measurements.md</c> (measured from 60 fps
/// footage), <c>[R §n]</c> is section n of <c>peg-mechanics.md</c> (public sources and PopCap's patents), and
/// <c>[J]</c> is a value the footage could not pin down, set by judgement (plan v9 "Your answers", G10: no tuning build),
/// with the reasoning beside it. <c>[O]</c> is the original game's own value, from the reference study the owner's
/// decision of 9 October 2026 adopted ("Moonfall plays by Peggle's real values"; <c>docs/design/v9/spec-moonfall.md</c>,
/// "The original's values"): where it differs from a measured or judged value, it wins.
/// </summary>
public static partial class MoonfallRules
{
    // ---- Time [M 0] ----

    /// <summary>The simulation runs at a fixed 100 Hz [M 0]: every physics and timing value below is per 10 ms tick.</summary>
    public const int TicksPerSecond = 100;

    /// <summary>One tick in seconds.</summary>
    public const double TickSeconds = 1.0 / TicksPerSecond;

    // ---- Playfield ----

    public const double Width = 800;
    public const double Height = 600;

    /// <summary>
    /// The side walls' surfaces. The ball's centre turns at x = 81.5 and 718.5 [M 3], so with a 6 px ball the walls stand
    /// at 75.5 and 724.5.
    /// </summary>
    public const double LeftWall = 75.5;

    /// <summary>The right wall's surface (see <see cref="LeftWall"/>).</summary>
    public const double RightWall = 724.5;

    /// <summary>
    /// [J] The ceiling's surface. The footage never shows a ball reaching the top of the board; a ceiling at the window's
    /// top edge keeps a ball bounced upwards in play rather than losing it off screen, and is a wall like the sides.
    /// </summary>
    public const double Ceiling = 0;

    /// <summary>A ball whose centre passes this line has left the board [R §1: "falls below the board"].</summary>
    public const double FloorExit = Height + BallRadius;

    // ---- Ball and launcher ----

    /// <summary>The ball's radius: its sprite is 11–12 px across [M 3].</summary>
    public const double BallRadius = 6;

    /// <summary>Gravity, 499.6 ± 0.5 px/s² measured over 144 arcs [M 1]; 0.05 px per tick².</summary>
    public const double Gravity = 500;

    /// <summary>
    /// The speed the ball leaves the barrel at: 392 ± 5 measured, 393–398 on the shots least sensitive to the pivot
    /// estimate [M 2]. 395 is the middle of those best shots, as plan v9 asks.
    /// </summary>
    public const double LaunchSpeed = 395;

    /// <summary>The launcher's pivot [M 2, from stills].</summary>
    public const double LauncherX = 400;

    /// <summary>The launcher's pivot height [M 2].</summary>
    public const double LauncherY = 87;

    /// <summary>How far from the pivot the ball leaves the barrel [M 2].</summary>
    public const double BarrelLength = 73;

    /// <summary>
    /// The aim limit either side of straight down: 97° [O: the launcher refuses only the upward cone within 83° of
    /// straight up, so it fires anywhere from 7° above level on one side, through straight down, to 7° above level on the
    /// other]. A flat or slightly rising shot reaches the high side pegs and the top corners. (Before the owner's Peggle
    /// decision of 9 October 2026 it was 85°, set by judgement from footage that reached ±81°.)
    /// </summary>
    public const double AimLimitDegrees = 97;

    /// <summary>
    /// The ball's speed limit: 15 px a tick, 1,500 px/s [O]. Gravity and the launcher never come near it; a flipper's
    /// kick can, and is held to it.
    /// </summary>
    public const double MaxBallSpeed = 1500;

    /// <summary>
    /// Two balls in play (a twin) meet as equal masses: the part of their velocities along the line between their
    /// centres is exchanged with a factor of 0.95 [O]. [J] Read as the exchange's impulse scaled by 0.95, so the pair
    /// keeps its momentum and loses a little of the swap.
    /// </summary>
    public const double BallExchange = 0.95;

    // ---- Pegs and bounces ----

    /// <summary>
    /// The default round peg's collision radius. The sprite is 9 px to the ring and about 10 with its outline, and the
    /// ball-to-peg centre distance at contact is 16.7 ± 1.0 [M 3]; radius 10 (contact at 16) is the drawn size and sits
    /// inside the measured contact range. A level may give a peg its own radius.
    /// </summary>
    public const double PegRadius = 10;

    /// <summary>A brick's thickness: about 20 px [M 3, R §2 "a width of 20"].</summary>
    public const double BrickThickness = 20;

    /// <summary>
    /// A round peg's bounciness, 0.9 [O]: a firm contact takes twice this share of the approach away, so the ball leaves
    /// at 2 × 0.9 − 1 = 0.80 of it (<see cref="PegNormalRestitution"/>), as the footage measured (0.80 ± 0.02 [M 4]).
    /// </summary>
    public const double PegBounciness = 0.9;

    /// <summary>The normal restitution off a round peg: 0.80 [O, M 4] (<see cref="PegBounciness"/>).</summary>
    public const double PegNormalRestitution = (2 * PegBounciness) - 1;

    /// <summary>
    /// A round peg keeps the ball's speed along its surface whole [O] (the footage read 0.95 ± 0.05 [M 4], within its
    /// error). No spin [M 4].
    /// </summary>
    public const double PegTangentRestitution = 1.0;

    /// <summary>
    /// A soft contact on a round peg rolls rather than bounces [O]: when the bounciness times the approach is at most
    /// 1 px a tick (100 px/s), the peg takes only that share away, so the ball keeps a tenth of its approach, stays on
    /// the peg and rolls round it. In approach speed that is up to 100 / 0.9 ≈ 111 px/s.
    /// </summary>
    public const double PegRollSpeed = 100 / PegBounciness;

    /// <summary>
    /// A brick (straight or curved) bounces the ball by reflecting it and then keeping 0.82 of the whole velocity [O],
    /// so a ball loses 18% of its run along a brick at each bounce.
    /// </summary>
    public const double BrickRestitution = 0.82;

    /// <summary>
    /// A contact with a brick slower than this (px/s, along the normal) does not bounce: the approach is taken away and
    /// the ball slides on along the brick with its speed along it whole [O: 1.4 px a tick].
    /// </summary>
    public const double BrickSlideSpeed = 140;

    /// <summary>
    /// On a brick face within about 8° of level or upright (a normal component over 0.99) the approach counts five times
    /// over for the slide test [O], so the ball slides only under 140 / 5 = 28 px/s and otherwise bounces.
    /// </summary>
    public const double BrickAxisSlideFactor = 5;

    /// <summary>The normal component over which a brick face counts as level or upright (<see cref="BrickAxisSlideFactor"/>) [O].</summary>
    public const double BrickAxisNormal = 0.99;

    /// <summary>A wall scales the whole velocity by 0.75 ± 0.02, both components [M 4; O the same, 0.75].</summary>
    public const double WallRestitution = 0.75;

    /// <summary>
    /// [J] Below this approach speed (px/s, along the contact normal) a contact with a wall, the ceiling, a rim or a
    /// flipper is resting, not a bounce: the approach is cancelled with no rebound and no loss along the surface.
    /// Gravity adds 5 px/s a tick, so a ball lying against one would otherwise chatter with ever smaller bounces. Twenty
    /// px/s is four ticks of gravity: well under any bounce the footage measured, well over a resting ball's chatter.
    /// (Pegs and bricks have the original's own rules: <see cref="PegRollSpeed"/>, <see cref="BrickSlideSpeed"/>.)
    /// </summary>
    public const double RestingSpeed = 20;

    /// <summary>
    /// [J] A tick's motion is split so the ball never moves more than this in one sub-step (px), which keeps it from
    /// passing through a peg or a brick: contact distances are 16 px and more, and the fastest ball moves about 8 px a
    /// tick. The sub-steps follow the exact parabola, so they change no arc.
    /// </summary>
    public const double MaxSubStep = 2;

    // ---- End of turn [M 7] ----

    /// <summary>From the ball leaving the board (or the bucket) to the first lit peg clearing: 0.57 ± 0.03 s [M 7].</summary>
    public const int ClearDelayTicks = 57;

    /// <summary>Then one lit peg clears every 50 ms, in the order they were hit [M 7].</summary>
    public const int ClearIntervalTicks = 5;

    /// <summary>From the last peg clearing to the next ball in the launcher: 0.8 s (85.70 − 84.90 s in [M 7]'s Nights turn).</summary>
    public const int NextBallTicks = 80;

    /// <summary>
    /// From the last peg clearing to the score counter starting: 1.0–1.4 s [M 7]; 1.2 s, the middle, so the new ball is
    /// in the launcher first, as in the footage.
    /// </summary>
    public const int CountUpHoldTicks = 120;

    // ---- Stuck ball [O; R §1] ----

    /// <summary>
    /// The stuck-ball rule [O]. Every game tick each ball's stuck count goes up by one; every
    /// <see cref="StuckSampleTicks"/> its place is noted, the last <see cref="StuckSamples"/> notes kept (10 to 60 ticks
    /// back). A ball <see cref="StuckRadius"/> or more from any of them is moving, and its count starts again. When every
    /// ball in play has counted <see cref="StuckTicks"/> (0.5 s of game time), every peg lit this shot and still standing
    /// clears at once (they still count for the shot), and the counts start again. A free ball is never called stuck:
    /// even at the top of a bounce it moves 2.5 px in 0.1 s. (Before the owner's Peggle decision of 9 October 2026 the
    /// rule waited 1.5 s within 15 px and cleared one touching peg at a time.)
    /// </summary>
    public const int StuckTicks = 50;

    /// <summary>How far (px) from a noted place counts as moving [O].</summary>
    public const double StuckRadius = 2;

    /// <summary>A ball's place is noted every this many game ticks [O].</summary>
    public const int StuckSampleTicks = 10;

    /// <summary>The noted places kept, so the watch looks back 10 to 60 ticks [O].</summary>
    public const int StuckSamples = 6;

    /// <summary>
    /// [J] A second watch, Moonfall's own: a ball that rocks to and fro in a hollow (a bowl of curved bricks, along which
    /// a ball slides with its speed whole) never stays within <see cref="StuckRadius"/>, yet never gets lower either. When
    /// every ball in play has gone 3 s of game time without coming <see cref="StuckSinkDepth"/> lower than its lowest
    /// point (or is stuck by the first watch), the shot's lit pegs clear in the same way. A free ball always gains depth
    /// within that: the highest bounce the launch speed allows rises and falls back in about 1.6 s. The original's
    /// fallback for this case was not decoded, so the watch stays.
    /// </summary>
    public const int StuckSinkTicks = 300;

    /// <summary>[J] How much lower (px) counts as sinking for <see cref="StuckSinkTicks"/>: under one ball diameter plus a resting contact's chatter.</summary>
    public const double StuckSinkDepth = 15;

    /// <summary>
    /// [J] Only a slow ball counts towards <see cref="StuckSinkTicks"/>: below 120 px/s. A ball running fast along a long
    /// flat shelf gets no lower either, but it is going somewhere; a ball rocking in a hollow spends its time near the
    /// turning points, well under this, so it is still caught.
    /// </summary>
    public const double StuckSinkSpeed = 120;

    // ---- Bucket [M 5] ----

    /// <summary>The bucket's centre: x(t) = 401.5 + 260·sin(2π t / 6 s), a pure sine [M 5].</summary>
    public const double BucketCentre = 401.5;

    /// <summary>The sweep's amplitude [M 5].</summary>
    public const double BucketAmplitude = 260;

    /// <summary>The sweep's period: 6.006 ± 0.005 s measured, 600 ticks [M 5, M 0].</summary>
    public const int BucketPeriodTicks = 600;

    /// <summary>The dark mouth's width: 104 ± 1 [M 5].</summary>
    public const double BucketMouth = 104;

    /// <summary>The rim's outer width: about 131 [M 5]; each rim is (131 − 104) / 2 = 13.5 px wide.</summary>
    public const double BucketRimWidth = 131;

    /// <summary>The rim's top: y ≈ 573 [M 5].</summary>
    public const double BucketTop = 573;

    /// <summary>
    /// [J] The catch zone. The footage does not show the rule [M 5]. Each rim is solid, a rounded post 13.5 px wide whose
    /// top is at <see cref="BucketTop"/>; the ball is caught when its centre comes down through the rim's top plus its
    /// own radius (y = 579, the moment a ball resting on the rims would touch them) while it is over the mouth
    /// (|x − centre| ≤ 52). A ball that hits a rim bounces off it, or rolls in off its inner edge and is caught then.
    /// That is the rule that matches what a player sees: a ball wholly over the dark mouth goes in, one on the rim is
    /// decided by the bounce.
    /// </summary>
    public const double CatchLine = BucketTop + BallRadius;

    /// <summary>
    /// [J] The rim never makes a pocket with a wall. The sweep [M 5] carries the outer rim to x 727 at the right end,
    /// 2.5 px past the wall at 724.5, and to 76 at the left, 0.5 px short of the wall at 75.5. In the original the bucket
    /// slides behind the side frame there, and no footage shows a ball caught between the two. Taken literally, the
    /// geometry made two traps. A ball dropped beside the wall was squeezed between the wall and the rim's side as the gap
    /// narrowed, and chattered for up to 1.3 s. A ball landing on the rim's outer shoulder wedged between it and the
    /// wall. So when the gap between a rim post and a wall is narrower than this (a ball's width, so no ball fits), the
    /// gap is closed at the rim's top. A ball above the rim rests on a flat ledge from the post's crown to the wall
    /// until the bucket pulls away. Then the gap is wider than the ball again and it drops through. If the crown has
    /// carried it inside first, it rolls into the mouth instead. Either way it waits at most about 0.6 s, well under
    /// <see cref="StuckTicks"/>. A ball already below the rim's top in that gap is past the bucket and can
    /// only fall out. The post no longer pushes it into the wall, so it falls clear.
    /// The amplitude and period stay as measured. The rim's width (medium confidence) would not help: any width narrows
    /// the gap through a ball's width on every sweep.
    /// </summary>
    public const double RimWallGap = 2 * BallRadius;

    // ---- Fever [M 8, R §4] ----

    /// <summary>The game runs at 1/10 speed in the approach to the last orange: 10.0 ± 0.3 [M 8]. Speeds are in thousandths.</summary>
    public const int ApproachSpeedMilli = 100;

    /// <summary>
    /// [J] The Fever trigger. The approach starts 0.08–0.13 s of game time before the last orange is touched [M 8], and
    /// no fixed distance explains the spread, while a look-ahead to the predicted contact fits all seven events.
    /// Moonfall starts it when, with one orange left, the ball's free-flight path touches that orange (before any other
    /// peg or wall) within 11 ticks: the measured mean (1.10 s real at 1/10 speed). If a later tick no longer predicts
    /// the touch (a mover, a graze), the approach ends and the game speeds up again.
    /// </summary>
    public const int FeverLookaheadTicks = 11;

    /// <summary>The zoom rises linearly from 1× to 2× in 0.48 s [M 8], starting with the slow motion.</summary>
    public const int ZoomInTicks = 48;

    /// <summary>The zoom holds at 2× until the hit [M 8].</summary>
    public const double ZoomMax = 2.0;

    /// <summary>After the hit the zoom falls back to 1× at −0.30× a second (3.33 s) [M 8].</summary>
    public const double ZoomOutPerSecond = 0.30;

    /// <summary>[J] A cancelled approach zooms back out as fast as it zoomed in.</summary>
    public const int ZoomCancelTicks = ZoomInTicks;

    /// <summary>The Fever banner appears 0.05–0.11 s after the hit [M 8]: 0.08 s.</summary>
    public const int BannerDelayTicks = 8;

    /// <summary>The banner stays 2.95 s [M 8].</summary>
    public const int BannerTicks = 295;

    /// <summary>The game runs at about half speed after the hit until the ball lands in a Fever bucket [M 8].</summary>
    public const int FeverSpeedMilli = 500;

    /// <summary>
    /// [J] How the speed goes from 1/10 to 1/2 after the hit: hidden by fireworks in the footage [M 8], with one arc at
    /// 3.3 s reading 0.42. Moonfall ramps it linearly over the zoom-out (3.33 s), which passes 0.42 at about 2.7 s and
    /// reaches the measured plateau as the zoom settles.
    /// </summary>
    public const int FeverRampTicks = 333;

    /// <summary>The five Fever buckets, left to right [R §3, P]: 10,000 / 50,000 / 100,000 / 50,000 / 10,000.</summary>
    public static ReadOnlySpan<int> FeverBucketValues => [10_000, 50_000, 100_000, 50_000, 10_000];

    /// <summary>On a perfect clear (every peg lit or gone when the last orange is hit) all five pay 100,000 [M 11, WP].</summary>
    public const int PerfectFeverBucketValue = 100_000;

    /// <summary>The Fever buckets appear about 1.4 s after the hit [M 8].</summary>
    public const int FeverBucketsShowTicks = 140;

    /// <summary>[J] The posts between the Fever buckets: thin (3 px), rising to the normal bucket's rim height.</summary>
    public const double FeverPostRadius = 3;

    // ---- Pegs and scoring [R §2, §3; M correction] ----

    public const int BlueValue = 10;
    public const int GreenValue = 10;
    public const int OrangeValue = 100;
    public const int PurpleValue = 500;

    /// <summary>25 orange pegs a level, picked at random from the pegs that may be orange [R §2, P].</summary>
    public const int OrangeCount = 25;

    /// <summary>Two green pegs a level from the third level on [R §2, P].</summary>
    public const int GreenCount = 2;

    /// <summary>The first level (1-based) with green pegs [R §2, O].</summary>
    public const int FirstGreenLevel = 3;

    /// <summary>
    /// The first level (1-based) with a purple peg: the fourth [O, the original's tutorial brings it in there]. (Before
    /// the owner's Peggle decision of 9 October 2026 every level had one.)
    /// </summary>
    public const int FirstPurpleLevel = 4;

    /// <summary>
    /// The greens are dealt from the blue pegs that may be green above this height first [O: 410 on screen]; only when
    /// those run out do lower ones take a green.
    /// </summary>
    public const double GreenHighY = 410;

    /// <summary>
    /// The two greens stand at least this far apart, centre to centre [O]. [J] If no peg that may be green is that far
    /// from the first, the second is the next in the deal all the same, so a level always gets its two.
    /// </summary>
    public const double GreenSpacing = 150;

    /// <summary>10 balls a level [R §6, P].</summary>
    public const int BallsPerLevel = 10;

    /// <summary>
    /// A shot's peg score reaching each of these gives a free ball [R §3, P, O]. Only the peg score counts (the pegs'
    /// values × their count, times a triple score): style bonuses add to the shot's score but never towards a free ball
    /// [O: "style points don't factor into getting a free ball"]. (Before the owner's Peggle decision of 9 October 2026
    /// they did.)
    /// </summary>
    public static ReadOnlySpan<int> FreeBallThresholds => [25_000, 75_000, 125_000];

    /// <summary>
    /// A shot that lights no peg may still give its ball back, on a coin flip [O]: the first such shot of a level wins
    /// it when a draw of 0–99 from the level's seeded random numbers is at most 80 (81 in 100), every later one when it is
    /// at most 60 (61 in 100). Not in a duel [O], nor for a ball the bucket already returned. [J] "Later" counts the
    /// level's earlier flips, won or lost (the original's counter was read, not traced). The original also always wins
    /// the flip early in its adventure (a profile flag not decoded); Moonfall does not.
    /// </summary>
    public const int MissReturnFirstMax = 80;

    /// <summary>The coin flip's limit after a level's first flip (<see cref="MissReturnFirstMax"/>): 61 in 100 [O].</summary>
    public const int MissReturnLaterMax = 60;

    /// <summary>[R §3, CB, unverified] Each ball left at the end of a won level is worth 10,000.</summary>
    public const int UnusedBallBonus = 10_000;

    /// <summary>
    /// The Fever meter's multiplier by the orange pegs still unlit [R §3, O]: ×2 at 15 left, ×3 at 10, ×5 at 6, ×10 at 3,
    /// and ×100 once none is left [O], so every peg the Full Moon ball lights after the last orange is worth a hundred
    /// times its value (<see cref="FeverMultiplier"/>). A peg scores the multiplier in force when it is touched, before
    /// that touch counts [O]: the 10th orange hit on a fresh level (16 left before it) scores ×1 and lifts the meter to
    /// ×2 for the pegs after it, and the last orange scores ×10. (Before the owner's Peggle decision of 9 October 2026 the
    /// meter stopped at ×10.)
    /// </summary>
    public static int Multiplier(int orangesLeft) => orangesLeft switch
    {
        > 15 => 1,
        > 10 => 2,
        > 6 => 3,
        > 3 => 5,
        > 0 => 10,
        _ => FeverMultiplier,
    };

    /// <summary>The multiplier once no orange is left [O].</summary>
    public const int FeverMultiplier = 100;

    /// <summary>At ×100 a purple peg's 500 counts as 100, so it scores 10,000 like an orange [O].</summary>
    public const int FeverValueCap = 100;

    /// <summary>A peg's value as it scores at <paramref name="multiplier"/>: its base value (<see cref="BaseValue"/>), capped at ×100 (<see cref="FeverValueCap"/>), times the multiplier.</summary>
    public static int PegScore(PegColour colour, int multiplier) =>
        (multiplier >= FeverMultiplier ? Math.Min(BaseValue(colour), FeverValueCap) : BaseValue(colour)) * multiplier;

    /// <summary>A peg's base value by its colour [R §3].</summary>
    public static int BaseValue(PegColour colour) => colour switch
    {
        PegColour.Orange => OrangeValue,
        PegColour.Green => GreenValue,
        PegColour.Purple => PurpleValue,
        _ => BlueValue,
    };

    // ---- Aim guide [M 11] ----

    /// <summary>The guide's dots are about 17 px apart along the path [M 11].</summary>
    public const double GuideDotSpacing = 17;

    /// <summary>
    /// [J] The guide's longest reach when nothing is in the way (px along the path). Every guide the footage shows ended at
    /// a peg, 110–180 px out [M 11]; 200 px keeps all of those and still leaves the rest of the shot to the player.
    /// </summary>
    public const double GuideMaxLength = 200;

    /// <summary>The most dots a guide draws (<see cref="GuideMaxLength"/> / <see cref="GuideDotSpacing"/>, rounded up, plus the first).</summary>
    public const int GuideMaxDots = 13;

    // ---- Presentation [M 9] ----

    /// <summary>A peg's score popup stays solid 0.40 s, fades for 0.08 s and is gone at 0.50 s [M 9].</summary>
    public const double PopupSolidSeconds = 0.40;

    /// <summary>The popup's fade (see <see cref="PopupSolidSeconds"/>).</summary>
    public const double PopupFadeSeconds = 0.08;

    /// <summary>
    /// The newest level format version this build reads (<see cref="MoonfallLevelLoader"/>): 2 adds the optional
    /// background <c>scene</c> and each piece's <c>canBeGreen</c> (plan v9 decision 16); version 1 files still read,
    /// without either.
    /// </summary>
    public const int LevelFormatVersion = 2;
}

/// <summary>A peg's colour, which sets its value and its role [R §2].</summary>
public enum PegColour : byte
{
    Blue,
    Orange,
    Green,
    Purple,
}
