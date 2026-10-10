namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// The style shots (plan v9 G4): each one is watched for per ball as it flies (its long legs, the wall or bucket it last
/// bounced off, its slide) or per shot (its oranges), paid at most once a shot, added to the shot's score and raised as
/// <see cref="MoonfallEventKind.StyleShot"/> for the window. Every trigger cites its line in the research or is [J] in
/// <see cref="MoonfallRules"/>.
/// </summary>
public sealed partial class MoonfallGame
{
    private long styleBonus;
    private int styleAwarded;
    private int shotOranges;

    /// <summary>The oranges the level dealt (Orange Sweep's need is set by them [O]).</summary>
    private int orangesDealt;

    // The Long Shot watch [O], per shot: waiting for the shot's first peg, holding a non-blue first peg, or settled.
    private const int LongWaiting = 0;
    private const int LongHolding = 1;
    private const int LongSettled = 2;
    private int longState;
    private double longX;
    private double longY;

    /// <summary>The game tick a blue peg was grazed after the first, or −1.</summary>
    private long longBlueTick;

    /// <summary>This shot's style bonuses so far (part of <see cref="ShotScore"/>).</summary>
    public long StyleBonus => styleBonus;

    /// <summary>Whether <paramref name="kind"/> has been paid this shot.</summary>
    public bool StyleShotThisShot(MoonfallStyleShot kind) => (styleAwarded & (1 << (int)kind)) != 0;

    /// <summary>A style shot's bonus [R §3 l.75–85].</summary>
    public static int StyleShotBonus(MoonfallStyleShot kind) => kind switch
    {
        MoonfallStyleShot.OnePegCatch => MoonfallRules.OnePegCatchBonus,
        MoonfallStyleShot.LongShot => MoonfallRules.LongShotBonus,
        MoonfallStyleShot.SuperLongShot => MoonfallRules.SuperLongShotBonus,
        MoonfallStyleShot.DoubleLongShot => MoonfallRules.DoubleLongShotBonus,
        MoonfallStyleShot.OffTheWall => MoonfallRules.OffTheWallBonus,
        MoonfallStyleShot.RimShot => MoonfallRules.RimShotBonus,
        MoonfallStyleShot.LuckyBounce => MoonfallRules.LuckyBounceBonus,
        MoonfallStyleShot.OrangeSweep => MoonfallRules.OrangeSweepBonus,
        MoonfallStyleShot.LongSlide => MoonfallRules.LongSlideBonus,
        MoonfallStyleShot.ClearNight => MoonfallRules.ClearNightBonus,
        MoonfallStyleShot.LiveWire => MoonfallRules.LiveWireBonus,
        _ => 0,
    };

    /// <summary>The oranges one shot must light for an Orange Sweep on this level (<see cref="MoonfallRules.OrangeSweepNeeds"/>).</summary>
    public int OrangeSweepNeeds => MoonfallRules.OrangeSweepNeeds(orangesDealt);

    private void StartShotStyle()
    {
        styleBonus = 0;
        styleAwarded = 0;
        shotOranges = 0;
        longState = LongWaiting;
        longX = 0;
        longY = 0;
        longBlueTick = -1;
    }

    /// <summary>Pays <paramref name="kind"/> once this shot, and raises it for the window. It never counts towards a free ball [O].</summary>
    private void Award(MoonfallStyleShot kind, int peg, double x, double y)
    {
        var bit = 1 << (int)kind;
        if ((styleAwarded & bit) != 0)
        {
            return;
        }

        styleAwarded |= bit;
        var bonus = Duel ? DuelStyleShotBonus(kind) : StyleShotBonus(kind);
        styleBonus += bonus;
        Post(MoonfallEventKind.StyleShot, peg, bonus, (int)kind, x, y);
    }

    /// <summary>
    /// Ball <paramref name="b"/> touches peg <paramref name="index"/>: the peg lights (once), and the touch moves the
    /// watches on: the shot's Long Shot, the ball's long legs, Off the Wall and Rim Shot after a bounce, its slide, and
    /// Storm Post's bolt.
    /// </summary>
    private void Touch(ref Ball b, int index)
    {
        var colour = bodies[index].Colour;
        if (!Light(index))
        {
            return;
        }

        var x = bodies[index].BoundX;
        var y = bodies[index].BoundY;
        var coloured = colour != PegColour.Blue;

        // Off the Wall [R §3 l.79]: the first peg lit after a wall, non-blue, a fifth of the screen from the bounce.
        if (b.AfterWall)
        {
            if (coloured && MoonfallGeometry.Hypot(x - b.WallX, y - b.WallY) >= MoonfallRules.OffTheWallDistance)
            {
                Award(MoonfallStyleShot.OffTheWall, index, x, y);
            }

            b.AfterWall = false;
        }

        // Rim Shot [R §3 l.80]: the first peg lit after a bounce off the bucket is the last orange.
        if (b.AfterRim)
        {
            if (colour == PegColour.Orange && feverHit && OrangesLeft == 0)
            {
                Award(MoonfallStyleShot.RimShot, index, x, y);
            }

            b.AfterRim = false;
        }

        // Lucky Bounce [R §3 l.81] counts from the first rim bounce since the ball last lit a peg.
        b.RimArmed = false;

        WatchLongShot(index, coloured, x, y);

        // Long legs for Double Long Shot [R §3 l.76–78]: non-blue to non-blue, far apart, with at most one blue between;
        // two in a row pay it.
        if (!coloured)
        {
            if (b.LegPeg >= 0)
            {
                b.LegBlues++;
            }
        }
        else
        {
            var longLeg = false;
            if (b.LegPeg >= 0 && b.LegBlues <= MoonfallRules.DoubleLongShotBluesAllowed)
            {
                var distance = MoonfallGeometry.Hypot(x - b.LegX, y - b.LegY);
                if (distance >= MoonfallRules.DoubleLongShotLegDistance)
                {
                    longLeg = true;
                    if (b.LegWasLong)
                    {
                        Award(MoonfallStyleShot.DoubleLongShot, index, x, y);
                    }
                }
            }

            b.LegPeg = index;
            b.LegX = x;
            b.LegY = y;
            b.LegBlues = 0;
            b.LegWasLong = longLeg;
        }

        // Long Slide [R §3 l.83]: pegs lit while the ball keeps touching pegs (a Fireball touches none, it burns them).
        if (!PowerActive(MoonfallPower.Fireball))
        {
            b.SlideRun++;
            if (b.SlideRun >= MoonfallRules.LongSlidePegs)
            {
                Award(MoonfallStyleShot.LongSlide, index, x, y);
            }
        }

        // Storm Post [R §5 l.117]: the bolt leaves from the first peg the ball lights this shot.
        if (PowerActive(MoonfallPower.Bolt) && !boltFired)
        {
            FireBolt(index);
        }
    }

    /// <summary>
    /// Long Shot and Super Long Shot [O]: the shot's first two pegs touched are both non-blue and more than
    /// <see cref="MoonfallRules.LongShotDistance"/> (or <see cref="MoonfallRules.SuperLongShotDistance"/>) apart; one blue
    /// grazed between them is forgiven when the second comes within <see cref="MoonfallRules.LongShotBlueGraceTicks"/>.
    /// Settled at the second peg either way, so it is paid at most once a shot, and only one of the two.
    /// </summary>
    private void WatchLongShot(int index, bool coloured, double x, double y)
    {
        switch (longState)
        {
            case LongWaiting:
                longState = coloured ? LongHolding : LongSettled;
                longX = x;
                longY = y;
                return;

            case LongHolding when !coloured:
                // One blue grazed on the way is forgiven if the second non-blue follows soon; a second blue settles it.
                if (longBlueTick >= 0)
                {
                    longState = LongSettled;
                }
                else
                {
                    longBlueTick = gameTick;
                }

                return;

            case LongHolding:
                longState = LongSettled;
                if (longBlueTick >= 0 && gameTick - longBlueTick >= MoonfallRules.LongShotBlueGraceTicks)
                {
                    return;
                }

                var dx = x - longX;
                var dy = y - longY;
                var d2 = (dx * dx) + (dy * dy);
                if (d2 > MoonfallRules.SuperLongShotDistance * MoonfallRules.SuperLongShotDistance)
                {
                    Award(MoonfallStyleShot.SuperLongShot, index, x, y);
                }
                else if (d2 > MoonfallRules.LongShotDistance * MoonfallRules.LongShotDistance)
                {
                    Award(MoonfallStyleShot.LongShot, index, x, y);
                }

                return;
        }
    }

    /// <summary>
    /// The ball touches a peg this sub-step, meeting it at <paramref name="approach"/> px/s: a slide goes on while the
    /// touches are soft and come within <see cref="MoonfallRules.SlideGapTicks"/>; a hard bounce starts the count again.
    /// </summary>
    private void NotePegContact(ref Ball b, double approach = 0)
    {
        if (gameTick - b.LastPegContact > MoonfallRules.SlideGapTicks || approach > MoonfallRules.SlideBounceSpeed)
        {
            b.SlideRun = 0;
        }

        b.LastPegContact = gameTick;
    }

    private static void NoteWall(ref Ball b)
    {
        b.AfterWall = true;
        b.WallX = b.X;
        b.WallY = b.Y;
    }

    private void NoteRim(ref Ball b)
    {
        b.AfterRim = true;
        if (!b.RimArmed)
        {
            b.RimArmed = true;
            b.RimTick = gameTick;
            b.RimY = b.Y;
            b.RimPeak = b.Y;
        }
    }

    /// <summary>After a rim bounce, the highest the ball has risen (for Lucky Bounce's quarter of the screen).</summary>
    private static void WatchRise(ref Ball b)
    {
        if (b.RimArmed && b.Y < b.RimPeak)
        {
            b.RimPeak = b.Y;
        }
    }

    /// <summary>A ball is caught: One-Peg Catch and Lucky Bounce.</summary>
    private void NoteCatch(in Ball b)
    {
        // [R §3 l.75]: bounced from exactly one peg into the bucket.
        if (shotPegs == 1)
        {
            Award(MoonfallStyleShot.OnePegCatch, -1, b.X, b.Y);
        }

        // [R §3 l.81]: off the bucket for a while or a quarter of the screen up, then in.
        if (b.RimArmed && (gameTick - b.RimTick >= MoonfallRules.LuckyBounceTicks || b.RimY - b.RimPeak >= MoonfallRules.LuckyBounceRise))
        {
            Award(MoonfallStyleShot.LuckyBounce, -1, b.X, b.Y);
        }
    }

    /// <summary>An orange lit by any means this shot counts towards an Orange Sweep [R §3 l.82, O: the level's dealt oranges set the need].</summary>
    private void NoteOrangeLit(int index)
    {
        shotOranges++;
        if (shotOranges >= OrangeSweepNeeds)
        {
            Award(MoonfallStyleShot.OrangeSweep, index, bodies[index].BoundX, bodies[index].BoundY);
        }
    }

    /// <summary>Clear Night [R §3 l.84]: as the Full Moon ball lands, every peg is lit or gone and the shot lit at least two.</summary>
    private void CheckClearNight(in Ball b)
    {
        if (shotPegs < MoonfallRules.ClearNightMinPegs)
        {
            return;
        }

        for (var i = 0; i < bodies.Length; i++)
        {
            if (!bodies[i].Lit && !bodies[i].Cleared)
            {
                return;
            }
        }

        Award(MoonfallStyleShot.ClearNight, -1, b.X, b.Y);
    }

    private void AddStyleTo(ref FingerprintHash hash)
    {
        hash.Add(styleBonus);
        hash.Add(styleAwarded);
        hash.Add(shotOranges);
        hash.Add(orangesDealt);
        hash.Add(longState);
        hash.Add(longX);
        hash.Add(longY);
        hash.Add(longBlueTick);
    }
}
