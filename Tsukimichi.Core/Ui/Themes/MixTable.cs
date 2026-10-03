using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// The vision modes the theme build measures a pair of moons under (<c>tools/themes/build_themes.py</c>'s
/// <c>ALL_MODES</c>, in that order): greyscale and Viénot deuteranopia, the round 5 gate, and Machado 2009's
/// deuteranopia, protanopia and tritanopia, the colour-vision gate.
/// </summary>
public enum VisionMode : byte
{
    /// <summary>Greyscale (luminance only).</summary>
    Grey,

    /// <summary>Viénot deuteranopia (the round 5 gate's second mode).</summary>
    Deut,

    /// <summary>Machado 2009 deuteranopia.</summary>
    MachadoDeut,

    /// <summary>Machado 2009 protanopia.</summary>
    MachadoProt,

    /// <summary>Machado 2009 tritanopia.</summary>
    MachadoTrit,
}

/// <summary>Where a mix's Ready lead is judged: in list rows (the row tier at 16 px) or at 48 px and up (the hero tier).</summary>
public enum MixTier : byte
{
    /// <summary>The row tier: list rows, 16 px.</summary>
    Row,

    /// <summary>The 48 px hero tier's art: medals at 48 px and up.</summary>
    Hero,
}

/// <summary>
/// The numbers the per-state mix is judged on (plan v7 T10; spec-1.17 §A3 with the realism supervisor's per-mode
/// ruling), compiled from each set's <c>metrics.json</c> by <c>tools/themes/build_themes.py</c> into
/// <c>MixTable.g.cs</c> (the JSON is not packaged): for every measured set, its own pairs per vision mode and its
/// salience per state; for every two sets, each state of one beside each other state of the other, per vision mode.
/// Distinctness is the build's units (round 5's summed blurred luminance difference) at the row tier, 16 px, on Night,
/// with every face framed in the neutral kit for the cross pairs; salience is greyscale on Night at 16 px, row tier
/// and the 48 px hero tier. A set joins the table, and so the per-state lists, once the build has measured it against
/// every other set (the owner's decision for the Orrery). Pure lookups.
/// </summary>
public static partial class MixTable
{
    /// <summary>How many vision modes each pair is measured under.</summary>
    public const int ModeCount = 5;

    /// <summary>A pair of moons from two sets under this in any mode is "hard to tell apart".</summary>
    public const float HardBar = 10f;

    /// <summary>Ready must lead the loudest other state by this much, at the row tier and at 48 px.</summary>
    public const float ReadyLeadBar = 1.25f;

    /// <summary>Completed must stay at or under this share of Ready's salience, at the row tier and at 48 px.</summary>
    public const float CompletedOfReadyBar = 0.80f;

    private const int StateCount = AppearanceStates.Count;
    private const int OwnPairCount = StateCount * (StateCount - 1) / 2;

    /// <summary>Every mode, in <see cref="VisionMode"/> order.</summary>
    public static readonly IReadOnlyList<VisionMode> Modes = [VisionMode.Grey, VisionMode.Deut, VisionMode.MachadoDeut, VisionMode.MachadoProt, VisionMode.MachadoTrit];

    /// <summary>The measured sets, in <see cref="GlyphSetId"/> order.</summary>
    public static IReadOnlyList<GlyphSetId> Sets => MeasuredSets;

    /// <summary>
    /// The "close" bar of <paramref name="mode"/>: the bar each set's own gates use, 12 in greyscale and Viénot
    /// deuteranopia, 11 under Machado's three.
    /// </summary>
    public static float CloseBar(VisionMode mode) => mode is VisionMode.Grey or VisionMode.Deut ? 12f : 11f;

    /// <summary>
    /// Whether <paramref name="value"/> is under <paramref name="bar"/> as the build judges it: at one decimal, on the
    /// two decimals it records (so 11.96 reads 12.0 and passes, and 11.95 reads 11.9 and does not).
    /// </summary>
    public static bool Under(float value, float bar) => value < bar - 0.045f;

    /// <summary>Whether a ratio misses (is under) <paramref name="bar"/>, judged at two decimals as the build judges ratios.</summary>
    public static bool RatioUnder(float ratio, float bar) => Round2(ratio) < bar - 0.0001f;

    /// <summary>Whether a ratio is over <paramref name="bar"/>, judged at two decimals.</summary>
    public static bool RatioOver(float ratio, float bar) => Round2(ratio) > bar + 0.0001f;

    /// <summary>A ratio at two decimals, as it is judged and shown.</summary>
    public static float Round2(float ratio) => (float)Math.Round(ratio, 2, MidpointRounding.AwayFromZero);

    /// <summary>Whether <paramref name="set"/> has numbers in the table.</summary>
    public static bool IsMeasured(GlyphSetId set) => IndexOf(set) >= 0;

    /// <summary>
    /// How far apart <paramref name="a"/> drawn from <paramref name="setA"/> reads beside <paramref name="b"/> drawn from
    /// <paramref name="setB"/> under <paramref name="mode"/>: one set's own pair when the sets are the same, else the
    /// cross-set pair. False for the same state twice or a set that is not measured.
    /// </summary>
    public static bool TryPair(GlyphSetId setA, QuestState a, GlyphSetId setB, QuestState b, VisionMode mode, out float value)
    {
        value = 0f;
        var ia = IndexOf(setA);
        var ib = IndexOf(setB);
        var sa = AppearanceStates.Index(a);
        var sb = AppearanceStates.Index(b);
        if (ia < 0 || ib < 0 || sa == sb || (uint)mode >= ModeCount)
        {
            return false;
        }

        if (ia == ib)
        {
            var (lo, hi) = sa < sb ? (sa, sb) : (sb, sa);
            value = OwnPairs[(((ia * ModeCount) + (int)mode) * OwnPairCount) + CombinationIndex(lo, hi)];
            return true;
        }

        if (ia > ib)
        {
            (ia, ib) = (ib, ia);
            (sa, sb) = (sb, sa);
        }

        value = CrossPairs[(((SetPairIndex(ia, ib) * ModeCount) + (int)mode) * StateCount * StateCount) + (sa * StateCount) + sb];
        return !float.IsNaN(value);
    }

    /// <summary>
    /// The mode in which the pair comes closest to (or furthest under) its bar, with its value: the heat table's
    /// "Worst" and the mode a warning names. False as for <see cref="TryPair"/>.
    /// </summary>
    public static bool TryWorst(GlyphSetId setA, QuestState a, GlyphSetId setB, QuestState b, out float value, out VisionMode mode)
    {
        value = 0f;
        mode = VisionMode.Grey;
        var found = false;
        var margin = float.PositiveInfinity;
        foreach (var m in Modes)
        {
            if (TryPair(setA, a, setB, b, m, out var v) && v - CloseBar(m) < margin)
            {
                margin = v - CloseBar(m);
                value = v;
                mode = m;
                found = true;
            }
        }

        return found;
    }

    /// <summary>The lowest value of the pair over every mode (the "hard to tell apart" check: under 10 in any mode), with its mode.</summary>
    public static bool TryLowest(GlyphSetId setA, QuestState a, GlyphSetId setB, QuestState b, out float value, out VisionMode mode)
    {
        value = float.PositiveInfinity;
        mode = VisionMode.Grey;
        var found = false;
        foreach (var m in Modes)
        {
            if (TryPair(setA, a, setB, b, m, out var v) && v < value)
            {
                value = v;
                mode = m;
                found = true;
            }
        }

        return found;
    }

    /// <summary><paramref name="state"/>'s salience drawn from <paramref name="set"/> (neutral kit, greyscale, Night, 16 px) at <paramref name="tier"/>.</summary>
    public static bool TrySalience(GlyphSetId set, QuestState state, MixTier tier, out float value)
    {
        var i = IndexOf(set);
        if (i < 0 || (uint)tier > (uint)MixTier.Hero)
        {
            value = 0f;
            return false;
        }

        value = NeutralSalience[(((i * 2) + (int)tier) * StateCount) + AppearanceStates.Index(state)];
        return true;
    }

    private static int IndexOf(GlyphSetId set) => Array.IndexOf(MeasuredSets, set);

    /// <summary>The place of (lo, hi), lo &lt; hi, in itertools.combinations order over the eight states.</summary>
    private static int CombinationIndex(int lo, int hi) => (lo * ((2 * StateCount) - lo - 1) / 2) + (hi - lo - 1);

    /// <summary>The place of the set pair (i, j), i &lt; j, in the table's order (each set with every later one).</summary>
    private static int SetPairIndex(int i, int j) => (i * ((2 * MeasuredSets.Length) - i - 1) / 2) + (j - i - 1);
}
