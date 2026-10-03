using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>What a mix warning is about, in the order the page lists them.</summary>
public enum MixWarningKind : byte
{
    /// <summary>Two states from two sets are under 10 in some vision mode: hard to tell apart in a list.</summary>
    Hard,

    /// <summary>Two states from two sets are under their mode's bar (12 greyscale and deuteranopia, 11 Machado): they look alike.</summary>
    Close,

    /// <summary>Ready no longer leads the loudest other state by 1.25×.</summary>
    ReadyLead,

    /// <summary>Completed draws more than 0.80× of Ready's eye.</summary>
    CompletedRecedes,
}

/// <summary>
/// One thing a mix misses (spec-1.17 §A3).
/// </summary>
/// <param name="A">A pair's first state (state order); Ready for <see cref="MixWarningKind.ReadyLead"/>; Completed for <see cref="MixWarningKind.CompletedRecedes"/>.</param>
/// <param name="B">A pair's second state; the loudest other state for Ready's lead; Ready for Completed's share.</param>
/// <param name="Value">The pair's value in <see cref="Mode"/>, Ready's lead, or Completed's share of Ready.</param>
/// <param name="Mode">The vision mode a pair warning names (its value furthest under that mode's bar).</param>
/// <param name="Tier">Where Ready's lead or Completed's share misses (its worse tier); <see cref="MixTier.Row"/> for a pair.</param>
public readonly record struct MixWarning(MixWarningKind Kind, QuestState A, QuestState B, float Value, VisionMode Mode, MixTier Tier)
{
    /// <summary>Whether this is about two states looking alike.</summary>
    public bool IsPair => Kind is MixWarningKind.Hard or MixWarningKind.Close;

    /// <summary>Whether <paramref name="other"/> is the same complaint (the same pair, or the same ratio), whatever its numbers.</summary>
    public bool SameAs(MixWarning other) => IsPair ? other.IsPair && A == other.A && B == other.B : Kind == other.Kind;
}

/// <summary>
/// A mix judged (spec-1.17 §A3): its warnings and Ready's numbers at both tiers. <see cref="Mixed"/> is false when the
/// column draws from one set, which passed its own gates in the build, so there is nothing for the mix to say;
/// <see cref="Measured"/> is false when a set in it has no numbers yet.
/// </summary>
public sealed class MixVerdict
{
    internal MixVerdict(GlyphSetId[] column, bool mixed, bool measured, IReadOnlyList<MixWarning> warnings, float[] leads, QuestState[] next, float[] completed)
    {
        Column = column;
        Mixed = mixed;
        Measured = measured;
        Warnings = warnings;
        this.leads = leads;
        this.next = next;
        this.completed = completed;
    }

    private readonly float[] leads;
    private readonly QuestState[] next;
    private readonly float[] completed;

    /// <summary>The column judged: the set of each state, in state order.</summary>
    public IReadOnlyList<GlyphSetId> Column { get; }

    /// <summary>Whether the column draws from more than one set.</summary>
    public bool Mixed { get; }

    /// <summary>Whether every set in the column has numbers (when false, nothing is judged and the ratios are 0).</summary>
    public bool Measured { get; }

    /// <summary>The warnings, hard pairs first, then close pairs (each by value), then Ready's lead and Completed's share.</summary>
    public IReadOnlyList<MixWarning> Warnings { get; }

    /// <summary>Whether the mix reads well: nothing to warn about.</summary>
    public bool Ok => Warnings.Count == 0;

    /// <summary>Ready's lead over the loudest other state at <paramref name="tier"/>, at two decimals.</summary>
    public float Lead(MixTier tier) => leads[(int)tier];

    /// <summary>The loudest state after Ready at <paramref name="tier"/>.</summary>
    public QuestState Next(MixTier tier) => next[(int)tier];

    /// <summary>Completed's salience as a share of Ready's at <paramref name="tier"/>, at two decimals.</summary>
    public float CompletedOfReady(MixTier tier) => completed[(int)tier];

    /// <summary>Ready's smaller lead of the two tiers ("Ready leads (1.35×)").</summary>
    public float LeastLead => MathF.Min(leads[0], leads[1]);

    /// <summary>The set drawing <paramref name="state"/>.</summary>
    public GlyphSetId SetFor(QuestState state) => Column[AppearanceStates.Index(state)];

    /// <summary>The first warning <paramref name="state"/>'s row shows (the reserved note column), if any.</summary>
    public MixWarning? NoteFor(QuestState state)
    {
        foreach (var w in Warnings)
        {
            var shown = w.Kind switch
            {
                MixWarningKind.ReadyLead => state == QuestState.Ready,
                MixWarningKind.CompletedRecedes => state == QuestState.Completed,
                _ => w.A == state || w.B == state,
            };

            if (shown)
            {
                return w;
            }
        }

        return null;
    }
}

/// <summary>How a pair of moons reads against the bars (the glyph window's heat table).</summary>
public enum PairReading : byte
{
    /// <summary>At or over its mode's bar.</summary>
    Apart,

    /// <summary>Under its mode's bar (12 greyscale and deuteranopia, 11 Machado).</summary>
    Close,

    /// <summary>Under 10.</summary>
    Hard,
}

/// <summary>One cell of the heat table: a pair's value in <see cref="Mode"/>, how it reads, and whether its two moons come from two sets.</summary>
public readonly record struct HeatCell(float Value, VisionMode Mode, PairReading Reading, bool CrossSet);

/// <summary>A change Fix it proposes: draw <see cref="State"/> from <see cref="To"/> (it draws from <see cref="From"/> now).</summary>
/// <param name="ClearsAll">Whether the change clears every warning; false for the fallback "use the theme's own" (§A4 step 4).</param>
/// <param name="After">The mix as it would be judged after the change.</param>
public sealed record MixFix(QuestState State, GlyphSetId From, GlyphSetId To, bool ClearsAll, MixVerdict After);

/// <summary>
/// The per-state mix's rules (plan v7 T10; spec-1.17 §A3 and §A4, the realism supervisor's rulings), pure and tested:
/// <list type="bullet">
/// <item><b>Close</b>: two states whose moons come from two different sets, under their mode's bar in any vision mode
/// (greyscale and Viénot deuteranopia 12; Machado protanopia, deuteranopia and tritanopia 11), judged at one decimal.
/// <b>Hard to tell apart</b>: under 10 in any mode. Pairs within one set never warn: the set passed its own gates.</item>
/// <item><b>Ready's lead</b> must be at least 1.25× the loudest other state, and <b>Completed</b> at most 0.80× Ready,
/// at the row tier and at 48 px.</item>
/// <item><b>Fix it</b> tries every single-state change (never the state the player just picked), offers the one that
/// clears everything, preferring the set that already covers the most states, then the earliest state; failing that,
/// the theme's own moon for the state the first warning names.</item>
/// </list>
/// Everything is a lookup in <see cref="MixTable"/>; nothing renders.
/// </summary>
public static class MixRules
{
    private static readonly GlyphSetInfo[] OfferedChoices = [.. GlyphSets.All.Where(static s => s.Offered && s.Mixable && MixTable.IsMeasured(s.Id))];

    /// <summary>
    /// The sets a state's list offers, in the registry's order: offered, mixable (never Classic) and measured against
    /// every other set (spec-1.17 §A1: a set joins once its cross-set numbers exist, as the Orrery and Sumi to Kinpaku have).
    /// </summary>
    public static IReadOnlyList<GlyphSetInfo> Choices => OfferedChoices;

    /// <summary>The column <paramref name="appearance"/> draws: each state's set, in state order.</summary>
    public static GlyphSetId[] Column(ResolvedAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var column = new GlyphSetId[AppearanceStates.Count];
        foreach (var state in AppearanceStates.All)
        {
            column[AppearanceStates.Index(state)] = appearance.SetFor(state);
        }

        return column;
    }

    /// <summary>
    /// Whether the mix table judges <paramref name="appearance"/> at all: high contrast draws one shared set and Classic
    /// is whole theme only (spec-1.17 §A1), so neither is a mix.
    /// </summary>
    public static bool Applies(ResolvedAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        return !appearance.HighContrast && !appearance.Classic;
    }

    /// <summary>Judges <paramref name="column"/> (eight sets in state order).</summary>
    public static MixVerdict Evaluate(IReadOnlyList<GlyphSetId> column)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (column.Count != AppearanceStates.Count)
        {
            throw new ArgumentException("A column has a set for each of the eight states.", nameof(column));
        }

        var sets = column.ToArray();
        var mixed = false;
        var measured = true;
        foreach (var set in sets)
        {
            mixed |= set != sets[0];
            measured &= MixTable.IsMeasured(set);
        }

        var leads = new float[2];
        var next = new QuestState[2];
        var completed = new float[2];
        if (!measured)
        {
            return new MixVerdict(sets, mixed, measured: false, [], leads, next, completed);
        }

        foreach (var tier in (ReadOnlySpan<MixTier>)[MixTier.Row, MixTier.Hero])
        {
            var t = (int)tier;
            MixTable.TrySalience(sets[AppearanceStates.Index(QuestState.Ready)], QuestState.Ready, tier, out var ready);
            var loudest = 0f;
            foreach (var state in AppearanceStates.All)
            {
                if (state != QuestState.Ready && MixTable.TrySalience(sets[AppearanceStates.Index(state)], state, tier, out var s) && s > loudest)
                {
                    loudest = s;
                    next[t] = state;
                }
            }

            MixTable.TrySalience(sets[AppearanceStates.Index(QuestState.Completed)], QuestState.Completed, tier, out var done);
            leads[t] = loudest > 0f ? MixTable.Round2(ready / loudest) : 0f;
            completed[t] = ready > 0f ? MixTable.Round2(done / ready) : 0f;
        }

        if (!mixed)
        {
            // One set: its own pairs and Ready passed its gates in the build; the Frames row speaks for its kit.
            return new MixVerdict(sets, mixed: false, measured: true, [], leads, next, completed);
        }

        var warnings = new List<MixWarning>();
        var states = AppearanceStates.All;
        for (var i = 0; i < states.Count; i++)
        {
            for (var j = i + 1; j < states.Count; j++)
            {
                var (a, b) = (states[i], states[j]);
                var (setA, setB) = (sets[i], sets[j]);
                if (setA == setB)
                {
                    continue;
                }

                if (MixTable.TryLowest(setA, a, setB, b, out var lowest, out var lowestMode) && MixTable.Under(lowest, MixTable.HardBar))
                {
                    warnings.Add(new MixWarning(MixWarningKind.Hard, a, b, lowest, lowestMode, MixTier.Row));
                }
                else if (MixTable.TryWorst(setA, a, setB, b, out var worst, out var worstMode) && MixTable.Under(worst, MixTable.CloseBar(worstMode)))
                {
                    warnings.Add(new MixWarning(MixWarningKind.Close, a, b, worst, worstMode, MixTier.Row));
                }
            }
        }

        warnings.Sort(static (x, y) => x.Kind != y.Kind ? x.Kind.CompareTo(y.Kind) : x.Value.CompareTo(y.Value));

        // Ready's lead and Completed's share: each named once, at its worse tier.
        var leadTier = leads[(int)MixTier.Hero] < leads[(int)MixTier.Row] ? MixTier.Hero : MixTier.Row;
        if (MixTable.RatioUnder(leads[(int)leadTier], MixTable.ReadyLeadBar))
        {
            warnings.Add(new MixWarning(MixWarningKind.ReadyLead, QuestState.Ready, next[(int)leadTier], leads[(int)leadTier], VisionMode.Grey, leadTier));
        }

        var completedTier = completed[(int)MixTier.Hero] > completed[(int)MixTier.Row] ? MixTier.Hero : MixTier.Row;
        if (MixTable.RatioOver(completed[(int)completedTier], MixTable.CompletedOfReadyBar))
        {
            warnings.Add(new MixWarning(MixWarningKind.CompletedRecedes, QuestState.Completed, QuestState.Ready, completed[(int)completedTier], VisionMode.Grey, completedTier));
        }

        return new MixVerdict(sets, mixed: true, measured: true, warnings, leads, next, completed);
    }

    /// <summary>
    /// The glyph window's heat cell for <paramref name="a"/> and <paramref name="b"/> in <paramref name="column"/>
    /// (spec-1.17 §D): under <paramref name="mode"/>, or with null ("Worst") the mode closest to its bar, and the lowest
    /// mode when that is under 10. Every pair is read against the same per-mode bars, so a set's own pair reads as the
    /// gates it passed judged it (Medallion's Blocked and Locked out at 11.1 under protanopia reads apart). False for
    /// the same state twice or a set without numbers.
    /// </summary>
    public static bool TryHeat(IReadOnlyList<GlyphSetId> column, QuestState a, QuestState b, VisionMode? mode, out HeatCell cell)
    {
        ArgumentNullException.ThrowIfNull(column);
        cell = default;
        var setA = column[AppearanceStates.Index(a)];
        var setB = column[AppearanceStates.Index(b)];
        float value;
        VisionMode at;
        if (mode is { } fixedMode)
        {
            if (!MixTable.TryPair(setA, a, setB, b, fixedMode, out value))
            {
                return false;
            }

            at = fixedMode;
        }
        else
        {
            if (!MixTable.TryWorst(setA, a, setB, b, out value, out at))
            {
                return false;
            }

            if (MixTable.TryLowest(setA, a, setB, b, out var lowest, out var lowestMode) && MixTable.Under(lowest, MixTable.HardBar))
            {
                (value, at) = (lowest, lowestMode);
            }
        }

        var reading = MixTable.Under(value, MixTable.HardBar)
            ? PairReading.Hard
            : MixTable.Under(value, MixTable.CloseBar(at)) ? PairReading.Close : PairReading.Apart;
        cell = new HeatCell(value, at, reading, setA != setB);
        return true;
    }

    /// <summary><paramref name="column"/> with <paramref name="state"/> drawn from <paramref name="set"/>, judged.</summary>
    public static MixVerdict WhatIf(IReadOnlyList<GlyphSetId> column, QuestState state, GlyphSetId set)
    {
        ArgumentNullException.ThrowIfNull(column);
        var changed = column.ToArray();
        changed[AppearanceStates.Index(state)] = set;
        return Evaluate(changed);
    }

    /// <summary>
    /// The first warning <paramref name="after"/> has that <paramref name="before"/> does not: what a state's list
    /// says beside an option before it is picked (spec-1.17 §A2). Null when the option adds nothing.
    /// </summary>
    public static MixWarning? Added(MixVerdict before, MixVerdict after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        foreach (var w in after.Warnings)
        {
            var known = false;
            foreach (var old in before.Warnings)
            {
                // A close pair turning hard is new; the same complaint at the same or a milder level is not.
                if (w.SameAs(old) && !(w.Kind == MixWarningKind.Hard && old.Kind == MixWarningKind.Close))
                {
                    known = true;
                    break;
                }
            }

            if (!known)
            {
                return w;
            }
        }

        return null;
    }

    /// <summary>
    /// Fix it (spec-1.17 §A4): the one change that clears every warning of <paramref name="verdict"/>. Every state but
    /// <paramref name="keep"/> (the state the player just picked; null for none) is tried with every
    /// <see cref="Choices"/> set; of the changes that clear everything, the set that already covers the most states wins,
    /// then the earliest state. When none does, the theme's own moon (<paramref name="themeSet"/>) for the state the
    /// first warning names, if that is a change. Null when the mix is fine or nothing can be proposed.
    /// </summary>
    public static MixFix? Fix(MixVerdict verdict, GlyphSetId themeSet, QuestState? keep)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        if (verdict.Ok || !verdict.Measured)
        {
            return null;
        }

        MixFix? best = null;
        var bestCover = -1;
        foreach (var state in AppearanceStates.All)
        {
            if (state == keep)
            {
                continue;
            }

            var from = verdict.SetFor(state);
            foreach (var choice in OfferedChoices)
            {
                if (choice.Id == from)
                {
                    continue;
                }

                var after = WhatIf(verdict.Column, state, choice.Id);
                if (!after.Ok)
                {
                    continue;
                }

                var cover = 0;
                foreach (var set in after.Column)
                {
                    cover += set == choice.Id ? 1 : 0;
                }

                // States are visited in order, so only a strictly larger cover replaces an earlier state.
                if (cover > bestCover)
                {
                    best = new MixFix(state, from, choice.Id, ClearsAll: true, after);
                    bestCover = cover;
                }
            }
        }

        if (best is not null)
        {
            return best;
        }

        // Step 4: the theme's own moon for the state the first warning names (never the player's pick).
        var first = verdict.Warnings[0];
        ReadOnlySpan<QuestState> named = first.Kind switch
        {
            MixWarningKind.ReadyLead => [first.B, QuestState.Ready],
            MixWarningKind.CompletedRecedes => [QuestState.Completed, QuestState.Ready],
            _ => [first.B, first.A],
        };

        foreach (var state in named)
        {
            if (state != keep && verdict.SetFor(state) != themeSet && MixTable.IsMeasured(themeSet))
            {
                return new MixFix(state, verdict.SetFor(state), themeSet, ClearsAll: false, WhatIf(verdict.Column, state, themeSet));
            }
        }

        return null;
    }
}
