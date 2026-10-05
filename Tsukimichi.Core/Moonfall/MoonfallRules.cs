namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// Every number Moonfall plays by (feature plan v9 G1–G3), in the original's 800×600 playfield units: x to the right,
/// y down, px and seconds; angles in degrees with 0 straight down and positive to the right. Each value names its source:
/// <c>[M n]</c> is row n of the summary table in <c>docs/research/plan-v9/peg-measurements.md</c> (measured from 60 fps
/// footage), <c>[R §n]</c> is section n of <c>peg-mechanics.md</c> (public sources and PopCap's patents), and
/// <c>[J]</c> is a value the footage could not pin down, set by judgement (plan v9 "Your answers", G10: no tuning build),
/// with the reasoning beside it.
/// </summary>
public static class MoonfallRules
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
    /// [J] The aim limit either side of straight down. Play reached ±81° and never the stop [M 2]. 85° keeps every angle
    /// the footage shows, leaves room to graze the side walls, and stops short of a level shot, which a barrel that hangs
    /// from the top of the board could not fire.
    /// </summary>
    public const double AimLimitDegrees = 85;

    // ---- Pegs and bounces ----

    /// <summary>
    /// The default round peg's collision radius. The sprite is 9 px to the ring and about 10 with its outline, and the
    /// ball-to-peg centre distance at contact is 16.7 ± 1.0 [M 3]; radius 10 (contact at 16) is the drawn size and sits
    /// inside the measured contact range. A level may give a peg its own radius.
    /// </summary>
    public const double PegRadius = 10;

    /// <summary>A brick's thickness: about 20 px [M 3, R §2 "a width of 20"].</summary>
    public const double BrickThickness = 20;

    /// <summary>The normal restitution off a peg or brick: 0.80 ± 0.02 [M 4].</summary>
    public const double PegNormalRestitution = 0.80;

    /// <summary>The tangential ratio off a peg or brick: 0.95 ± 0.05, so little or no friction [M 4]. No spin [M 4].</summary>
    public const double PegTangentRestitution = 0.95;

    /// <summary>A wall scales the whole velocity by 0.75 ± 0.02, both components [M 4].</summary>
    public const double WallRestitution = 0.75;

    /// <summary>
    /// [J] Below this approach speed (px/s, along the contact normal) a contact is resting, not a bounce: the approach is
    /// cancelled with no rebound and no tangential loss. Gravity adds 5 px/s a tick, so a ball lying on a peg would
    /// otherwise chatter with ever smaller bounces and lose 5% of its slide every tick, and could never roll off a curve
    /// (the original's balls slide the length of a brick arc). Twenty px/s is four ticks of gravity: well under any
    /// bounce the footage measured, well over a resting ball's chatter.
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

    // ---- Stuck ball [R §1, M 6: not measured] ----

    /// <summary>
    /// [J] The stuck-ball rule. The original "may remove a peg before the end of the turn to keep the ball in play" [R §1]
    /// and the footage never shows it [M 6]. Moonfall calls the ball stuck when its centre has stayed within
    /// <see cref="StuckRadius"/> of one spot for <see cref="StuckTicks"/>: it then clears early the lit peg or brick it is
    /// touching that was hit last (the one holding it up most recently), and starts watching again, so a ball wedged
    /// between several pegs loses one every 1.5 s until it falls free. The peg still counts for the shot.
    /// 1.5 s of game time is three times the longest pause a free ball shows at the top of a bounce, so no moving ball is
    /// ever called stuck, and short enough that a wedge never feels like a hang. A ball rocking in a hollow is caught by
    /// the rule's second half, <see cref="StuckSinkTicks"/>.
    /// </summary>
    public const int StuckTicks = 150;

    /// <summary>[J] How far (px) the ball may wander and still count as staying put: under one ball diameter plus the chatter of a resting contact.</summary>
    public const double StuckRadius = 15;

    /// <summary>
    /// [J] The second half of the stuck rule: a ball that rocks to and fro in a hollow (a bowl of bricks, a pocket wider
    /// than <see cref="StuckRadius"/>) never stays in one spot, yet never gets lower either. When the ball has not come
    /// <see cref="StuckRadius"/> lower than its lowest point for 3 s of game time, the lit peg or brick it is touching
    /// (the last hit) clears early in the same way. A free ball always gains depth within that: the highest bounce the
    /// launch speed allows rises and falls back in about 1.6 s.
    /// </summary>
    public const int StuckSinkTicks = 300;

    /// <summary>[J] A peg within this distance (px) of contact counts as touching the stuck ball.</summary>
    public const double StuckTouchSlack = 2;

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

    /// <summary>The first level (1-based) with green pegs [R §2].</summary>
    public const int FirstGreenLevel = 3;

    /// <summary>10 balls a level [R §6, P].</summary>
    public const int BallsPerLevel = 10;

    /// <summary>A shot's score reaching each of these gives a free ball [R §3, P].</summary>
    public static ReadOnlySpan<int> FreeBallThresholds => [25_000, 75_000, 125_000];

    /// <summary>[R §3, CB, unverified] Each ball left at the end of a won level is worth 10,000.</summary>
    public const int UnusedBallBonus = 10_000;

    /// <summary>
    /// The Fever meter's multiplier by the orange pegs still unlit [R §3]: ×2 at 15 left, ×3 at 10, ×5 at 6, ×10 at 3.
    /// [J] A peg scores the multiplier in force when it is touched, before that touch counts: the 10th orange hit on a
    /// fresh level (16 left before it) scores ×1 and lifts the meter to ×2 for the pegs after it. The research gives the
    /// steps, not the instant; this reading means the meter never pays for the hit that moves it.
    /// </summary>
    public static int Multiplier(int orangesLeft) => orangesLeft switch
    {
        > 15 => 1,
        > 10 => 2,
        > 6 => 3,
        > 3 => 5,
        _ => 10,
    };

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

    /// <summary>The level's format version this build reads (<see cref="MoonfallLevelLoader"/>).</summary>
    public const int LevelFormatVersion = 1;
}

/// <summary>A peg's colour, which sets its value and its role [R §2].</summary>
public enum PegColour : byte
{
    Blue,
    Orange,
    Green,
    Purple,
}
