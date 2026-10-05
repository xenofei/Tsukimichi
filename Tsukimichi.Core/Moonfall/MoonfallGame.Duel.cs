namespace Tsukimichi.Core.Moonfall;

/// <summary>What one probe flight touched (<see cref="MoonfallGame.WeighShot"/>).</summary>
/// <param name="Value">The base values of the unlit pegs it touched, summed (a blue 10, an orange 100, a purple 500).</param>
/// <param name="Pegs">How many unlit pegs it touched.</param>
/// <param name="Oranges">How many of those were orange.</param>
/// <param name="Caught">It landed in the bucket.</param>
/// <param name="SubSteps">The physics sub-steps it took (never more than the budget).</param>
public readonly record struct MoonfallShotWeight(long Value, int Pegs, int Oranges, bool Caught, int SubSteps);

/// <summary>
/// The duel's rules inside the engine (plan v9 G7, <see cref="MoonfallRuleSet.Duel"/>): one green at a time, the
/// duel's values, and each side's powers kept apart. Which side shoots, the turn order and the sides' scores are kept by
/// <see cref="MoonfallDuel"/>, which hands the launcher over between turns (<see cref="HandOver"/>). Nothing here
/// allocates after the game is made.
/// </summary>
public sealed partial class MoonfallGame
{
    /// <summary>Each side's shots left of each power while the other side shoots: side × (power id).</summary>
    private readonly int[] sidePowerShots;

    /// <summary>Each side's triple scores left (the drum's) while the other side shoots.</summary>
    private int sideTriple0;
    private int sideTriple1;

    /// <summary>Greens a duel still has to deal: one appears the turn after the one standing is gone [R §2 l.47].</summary>
    private int duelGreensLeft;

    /// <summary>The rules this game is played by.</summary>
    public MoonfallRuleSet RuleSet { get; }

    /// <summary>Whether this is a duel (<see cref="MoonfallRuleSet.Duel"/>).</summary>
    public bool Duel => RuleSet == MoonfallRuleSet.Duel;

    /// <summary>The side at the launcher in a duel (0 or 1); always 0 outside one.</summary>
    public int Side { get; private set; }

    /// <summary>
    /// A duel hands the launcher to <paramref name="side"/>, whose green pegs trigger <paramref name="power"/> (its
    /// companion's): the powers the other side charged wait for its next turn, and this side's come back. Only between
    /// shots (<see cref="MoonfallPhase.Aiming"/>); false otherwise, or outside a duel.
    /// </summary>
    public bool HandOver(int side, MoonfallPower power)
    {
        if (!Duel || Phase != MoonfallPhase.Aiming || side is < 0 or > 1 || power is < MoonfallPower.None or > MoonfallPower.Bolt)
        {
            return false;
        }

        if (side != Side)
        {
            var width = MoonfallPowers.Count + 1;
            Array.Copy(powerShots, 0, sidePowerShots, Side * width, width);
            Array.Copy(sidePowerShots, side * width, powerShots, 0, width);
            if (Side == 0)
            {
                sideTriple0 = tripleShots;
            }
            else
            {
                sideTriple1 = tripleShots;
            }

            tripleShots = side == 0 ? sideTriple0 : sideTriple1;
            Side = side;
        }

        Power = power;
        return true;
    }

    /// <summary>
    /// <see cref="Advance"/> in two halves, for a driver that acts between real ticks (a duel's opponent shoots on an
    /// exact tick): this adds <paramref name="realSeconds"/> of wall-clock time (at most 0.25 s, as Advance) without
    /// running it; <see cref="TickDue"/> and <see cref="TakeTick"/> then run it a tick at a time. The same time in either
    /// form runs the same ticks, and <see cref="Alpha"/> reads the remainder as it does after Advance.
    /// </summary>
    public void AddTime(double realSeconds)
    {
        if (double.IsFinite(realSeconds) && realSeconds > 0)
        {
            realAccumulator += Math.Min(realSeconds, 0.25);
        }
    }

    /// <summary>Whether a whole real tick of the time added (<see cref="AddTime"/>) is waiting.</summary>
    public bool TickDue => realAccumulator >= MoonfallRules.TickSeconds - 1e-9;

    /// <summary>Runs one waiting real tick (<see cref="AddTime"/>); false when none is due.</summary>
    public bool TakeTick()
    {
        if (!TickDue)
        {
            return false;
        }

        realAccumulator -= MoonfallRules.TickSeconds;
        Tick();
        return true;
    }

    /// <summary>
    /// Flies a probe ball from the launcher at <paramref name="angleDegrees"/>, as if shot <paramref name="ticksFromNow"/>
    /// game ticks from now (the bucket where it will be then, the movers where they are now), for at most
    /// <paramref name="subStepBudget"/> physics sub-steps: what it would touch. It changes nothing the game shows or
    /// plays by (only the probe's own scratch), so an opponent or a tool can weigh shots on the live game. Allocates
    /// nothing. Sage's Path's search uses the same flight.
    /// </summary>
    public MoonfallShotWeight WeighShot(double angleDegrees, int subStepBudget, int ticksFromNow = 0)
    {
        if (!double.IsFinite(angleDegrees) || subStepBudget <= 0)
        {
            return default;
        }

        var angle = Math.Clamp(angleDegrees, -MoonfallRules.AimLimitDegrees, MoonfallRules.AimLimitDegrees);
        var used = FlyProbe(angle, subStepBudget, gameTick + Math.Max(0, ticksFromNow), out var caught);
        return new MoonfallShotWeight(probe.Value, probe.Pegs, probe.Oranges, caught, used);
    }

    /// <summary>A style shot's bonus in a duel [R §3 l.75–85, in parentheses].</summary>
    public static int DuelStyleShotBonus(MoonfallStyleShot kind) => kind switch
    {
        MoonfallStyleShot.OnePegCatch => MoonfallRules.DuelOnePegCatchBonus,
        MoonfallStyleShot.LongShot => MoonfallRules.DuelLongShotBonus,
        MoonfallStyleShot.SuperLongShot => MoonfallRules.DuelSuperLongShotBonus,
        MoonfallStyleShot.DoubleLongShot => MoonfallRules.DuelDoubleLongShotBonus,
        MoonfallStyleShot.OffTheWall => MoonfallRules.DuelOffTheWallBonus,
        MoonfallStyleShot.RimShot => MoonfallRules.DuelRimShotBonus,
        MoonfallStyleShot.LuckyBounce => MoonfallRules.DuelLuckyBounceBonus,
        MoonfallStyleShot.OrangeSweep => MoonfallRules.DuelOrangeSweepBonus,
        MoonfallStyleShot.LongSlide => MoonfallRules.DuelLongSlideBonus,
        MoonfallStyleShot.ClearNight => MoonfallRules.DuelClearNightBonus,
        MoonfallStyleShot.LiveWire => MoonfallRules.DuelLiveWireBonus,
        _ => 0,
    };

    /// <summary>What Fever bucket <paramref name="bucket"/> (0–4) pays: every bucket pays the best on a perfect clear.</summary>
    public static int FeverBucketValue(int bucket, bool perfect, bool duel)
    {
        var values = duel ? MoonfallRules.DuelFeverBucketValues : MoonfallRules.FeverBucketValues;
        if (perfect)
        {
            return duel ? MoonfallRules.DuelPerfectFeverBucketValue : MoonfallRules.PerfectFeverBucketValue;
        }

        return values[Math.Clamp(bucket, 0, values.Length - 1)];
    }

    /// <summary>
    /// A duel's next green [R §2 l.47: "only one of the two greens is present at a time, the other appears next turn"]:
    /// at the start of a turn with none standing, one of the blue pegs still standing that may be green turns green.
    /// </summary>
    private void DealDuelGreen()
    {
        if (duelGreensLeft <= 0)
        {
            return;
        }

        var candidates = 0;
        for (var i = 0; i < bodies.Length; i++)
        {
            ref readonly var b = ref bodies[i];
            if (b.Lit || b.Cleared)
            {
                continue;
            }

            if (b.Colour == PegColour.Green)
            {
                return;
            }

            if (b.Colour == PegColour.Blue && b.CanBeGreen)
            {
                candidates++;
            }
        }

        if (candidates == 0)
        {
            return;
        }

        var pick = random.Next(candidates);
        for (var i = 0; i < bodies.Length; i++)
        {
            ref var b = ref bodies[i];
            if (!b.Lit && !b.Cleared && b.Colour == PegColour.Blue && b.CanBeGreen && pick-- == 0)
            {
                b.Colour = PegColour.Green;
                duelGreensLeft--;
                return;
            }
        }
    }

    private void AddDuelTo(ref FingerprintHash hash)
    {
        hash.Add((long)RuleSet);
        hash.Add(Side);
        hash.Add(duelGreensLeft);
        hash.Add(sideTriple0);
        hash.Add(sideTriple1);
        foreach (var shots in sidePowerShots)
        {
            hash.Add(shots);
        }
    }
}
