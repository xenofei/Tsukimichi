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
    private int orangesAtShotStart;

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

    /// <summary>The oranges a shot must light for an Orange Sweep, given the oranges left when it was fired.</summary>
    public static int OrangeSweepNeeds(int orangesAtStart) =>
        Math.Max(MoonfallRules.OrangeSweepMinimum, (orangesAtStart + MoonfallRules.OrangeSweepShare - 1) / MoonfallRules.OrangeSweepShare);

    private void StartShotStyle()
    {
        styleBonus = 0;
        styleAwarded = 0;
        shotOranges = 0;
        orangesAtShotStart = OrangesLeft;
    }

    /// <summary>Pays <paramref name="kind"/> once this shot, and raises it for the window.</summary>
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
        AwardShotFreeBalls();
    }

    /// <summary>
    /// Ball <paramref name="b"/> touches peg <paramref name="index"/>: the peg lights (once), and the touch moves the
    /// ball's watch on: its long legs, Off the Wall and Rim Shot after a bounce, its slide, and Storm Post's bolt.
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

        // Long legs [R §3 l.76–78]: non-blue to non-blue, far apart, with at most one blue between.
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
            if (b.LegPeg >= 0 && b.LegBlues <= MoonfallRules.LongShotBluesAllowed)
            {
                var distance = MoonfallGeometry.Hypot(x - b.LegX, y - b.LegY);
                if (distance >= MoonfallRules.LongShotDistance)
                {
                    longLeg = true;
                    if (b.LegWasLong && !StyleShotThisShot(MoonfallStyleShot.DoubleLongShot))
                    {
                        Award(MoonfallStyleShot.DoubleLongShot, index, x, y);
                    }
                    else if (distance >= MoonfallRules.SuperLongShotDistance && !StyleShotThisShot(MoonfallStyleShot.SuperLongShot))
                    {
                        Award(MoonfallStyleShot.SuperLongShot, index, x, y);
                    }
                    else
                    {
                        Award(MoonfallStyleShot.LongShot, index, x, y);
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

    /// <summary>An orange lit by any means this shot counts towards an Orange Sweep [R §3 l.82].</summary>
    private void NoteOrangeLit(int index)
    {
        shotOranges++;
        var needs = OrangeSweepNeeds(orangesAtShotStart);
        if (orangesAtShotStart >= needs && shotOranges >= needs)
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
        hash.Add(orangesAtShotStart);
    }
}
