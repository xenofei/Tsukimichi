using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Rewards;

/// <summary>
/// The <c>ParamGrow</c> columns Tsukimichi reads, by level (the sheet's row id is the level): <c>QuestExpModifier</c>
/// and <c>ScaledQuestXP</c> for the quest EXP formula, and <c>ExpToNext</c> for "5% of a level" (feature plan v7, C8).
/// The plugin reads them from the game at catalog build (<c>CatalogMapper</c>); Core cannot read the sheets, so a
/// catalog built without them (the test fixture) has <see cref="Empty"/>, and every quest's EXP is then unknown rather
/// than guessed. Immutable.
/// </summary>
public sealed class QuestExpTable
{
    /// <summary>No rows: every EXP reads <see cref="ExpKind.Unknown"/>.</summary>
    public static readonly QuestExpTable Empty = new([], [], []);

    private readonly uint[] modifiers;
    private readonly uint[] scales;
    private readonly int[] toNext;

    private QuestExpTable(uint[] modifiers, uint[] scales, int[] toNext)
    {
        this.modifiers = modifiers;
        this.scales = scales;
        this.toNext = toNext;
    }

    /// <summary>Whether the table has no level at all.</summary>
    public bool IsEmpty => modifiers.Length == 0;

    /// <summary>The highest level the table has a row for; 0 when empty.</summary>
    public int MaxLevel => Math.Max(0, modifiers.Length - 1);

    /// <summary>
    /// Builds the table from (level, <c>QuestExpModifier</c>, <c>ScaledQuestXP</c>) rows. Rows past the last level the
    /// game fills (the sheet carries zero rows beyond the level cap) simply read as unknown; a level given twice keeps
    /// the last value.
    /// </summary>
    public static QuestExpTable From(IEnumerable<(int Level, uint Modifier, uint Scale)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return From(rows.Select(static r => (r.Level, r.Modifier, r.Scale, 0)));
    }

    /// <summary>
    /// Builds the table from (level, <c>QuestExpModifier</c>, <c>ScaledQuestXP</c>, <c>ExpToNext</c>) rows, as
    /// <see cref="From(IEnumerable{ValueTuple{int, uint, uint}})"/> does; a level whose <c>ExpToNext</c> is 0 (the
    /// level cap, or a row past it) has no "share of a level" (<see cref="ExpToNext"/>).
    /// </summary>
    public static QuestExpTable From(IEnumerable<(int Level, uint Modifier, uint Scale, int ExpToNext)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var list = rows.Where(static r => r.Level is >= 0 and <= byte.MaxValue).ToList();
        if (list.Count == 0)
        {
            return Empty;
        }

        var size = list.Max(static r => r.Level) + 1;
        var modifiers = new uint[size];
        var scales = new uint[size];
        var toNext = new int[size];
        foreach (var (level, modifier, scale, next) in list)
        {
            modifiers[level] = modifier;
            scales[level] = scale;
            toNext[level] = Math.Max(0, next);
        }

        return new QuestExpTable(modifiers, scales, toNext);
    }

    /// <summary>
    /// The EXP a job at <paramref name="level"/> needs to reach the next level (<c>ParamGrow.ExpToNext</c>); null when
    /// the level is unknown (0 or outside the table) or the row has none (the level cap: there is no next level).
    /// </summary>
    public int? ExpToNext(int level) =>
        level > 0 && level < toNext.Length && toNext[level] > 0 ? toNext[level] : null;

    /// <summary>
    /// The EXP a quest of <paramref name="expFactor"/> gives at <paramref name="level"/>:
    /// <c>floor(ExpFactor × QuestExpModifier × ScaledQuestXP / 100)</c> of that level's row. Null when the table has no
    /// row there, or the row is zero (a level past the cap).
    /// </summary>
    public ulong? At(int level, uint expFactor)
    {
        if (level < 0 || level >= modifiers.Length || modifiers[level] == 0 || scales[level] == 0)
        {
            return null;
        }

        return (ulong)expFactor * modifiers[level] * scales[level] / 100UL;
    }
}

/// <summary>
/// An amount of EXP as a share of the level it is earned at (feature plan v7, C8: "50,700 EXP (5% of a level)"):
/// <paramref name="Percent"/> rounded to a whole percent, or <paramref name="UnderOne"/> when it is less than 1%
/// (shown as "&lt;1%"; <paramref name="Percent"/> is then 0).
/// </summary>
public readonly record struct LevelShare(int Percent, bool UnderOne);

/// <summary>What is known about a quest's EXP reward (<see cref="QuestExp.For"/>).</summary>
public enum ExpKind : byte
{
    /// <summary>The quest gives no EXP (<see cref="QuestRecord.ExpFactor"/> is 0).</summary>
    None,

    /// <summary>One amount, the same for every character: <see cref="ExpReward.Min"/>.</summary>
    Fixed,

    /// <summary>
    /// A quest under Quest Sync (<see cref="QuestRecord.LevelMax"/> above its level): the amount follows the level of
    /// the job it is done on, from <see cref="ExpReward.Min"/> at the quest's level to <see cref="ExpReward.Max"/> at
    /// <see cref="QuestRecord.LevelMax"/>.
    /// </summary>
    Range,

    /// <summary>
    /// The formula is not known to hold, so no number is shown: allied society quests and seasonal event quests (their
    /// reward windows disagree with it), or a catalog without the <c>ParamGrow</c> rows.
    /// </summary>
    Unknown,
}

/// <summary>A quest's EXP reward; <see cref="Min"/> and <see cref="Max"/> are 0 unless <see cref="Kind"/> is Fixed or Range.</summary>
public readonly record struct ExpReward(ExpKind Kind, ulong Min, ulong Max)
{
    public static readonly ExpReward NoExp = new(ExpKind.None, 0, 0);
    public static readonly ExpReward Unknown = new(ExpKind.Unknown, 0, 0);

    /// <summary>Whether there is an amount to show.</summary>
    public bool HasAmount => Kind is ExpKind.Fixed or ExpKind.Range;
}

/// <summary>
/// EXP per quest from the game's own formula (feature plan v5, "Planning extras", R6 G):
/// <c>EXP = floor(ExpFactor × ParamGrow[L].QuestExpModifier × ParamGrow[L].ScaledQuestXP / 100)</c> with L the level the
/// journal prints (<see cref="QuestRecord.DisplayLevel"/>, the acceptance level plus its offset). Checked against the
/// reward windows the community wiki records for 19 quests from level 1 to 97: main scenario, side, feature, class and
/// crafting quests, offsets ("Quarrels with Squirrels" is level 1 + 2 and gives the level 3 amount, 240) and the
/// rounding down ("Vox Populi" 90.75 → 90, "Weapons of a Feather" 5,899.5 → 5,899). A Quest Sync quest
/// (Shadowbringers onward, <see cref="QuestRecord.LevelMax"/>) gives the formula at the job's level clamped to its
/// range, so it reads as that range ("A Cry for Help" 54,000–57,240). Allied society quests and seasonal event quests
/// do not follow it and read <see cref="ExpKind.Unknown"/>: never a confident wrong number. Pure.
/// </summary>
public static class QuestExp
{
    /// <summary>The quest's EXP reward under <paramref name="table"/>.</summary>
    public static ExpReward For(QuestRecord quest, QuestExpTable table)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(table);
        if (quest.ExpFactor == 0)
        {
            return ExpReward.NoExp;
        }

        if (quest.BeastTribe != 0 || quest.Festival != 0 || table.IsEmpty)
        {
            return ExpReward.Unknown;
        }

        var level = quest.DisplayLevel;
        if (table.At(level, quest.ExpFactor) is not { } min)
        {
            return ExpReward.Unknown;
        }

        if (quest.LevelMax > level)
        {
            return table.At(quest.LevelMax, quest.ExpFactor) is { } max && max >= min
                ? new ExpReward(ExpKind.Range, min, max)
                : ExpReward.Unknown;
        }

        return new ExpReward(ExpKind.Fixed, min, min);
    }

    /// <summary>
    /// The EXP a job at <paramref name="jobLevel"/> gets for handing the quest in (feature plan v7, C8): 0 at or above
    /// <paramref name="levelCap"/> (the character's cap; 0 when unknown, which caps nothing), the fixed amount, or under
    /// Quest Sync the formula at the job's level clamped to the quest's range. Null when the amount is not known
    /// (<see cref="ExpReward.HasAmount"/>).
    /// </summary>
    public static ulong? ForLevel(QuestRecord quest, QuestExpTable table, int jobLevel, int levelCap)
    {
        var reward = For(quest, table);
        if (!reward.HasAmount)
        {
            return null;
        }

        if (levelCap > 0 && jobLevel >= levelCap)
        {
            return 0;
        }

        if (reward.Kind == ExpKind.Fixed)
        {
            return reward.Min;
        }

        var level = Math.Clamp(jobLevel, quest.DisplayLevel, quest.LevelMax);
        return table.At(level, quest.ExpFactor) is { } amount ? Math.Clamp(amount, reward.Min, reward.Max) : reward.Min;
    }

    /// <summary>
    /// <paramref name="exp"/> as a share of what a job at <paramref name="level"/> needs to level up
    /// (<c>ParamGrow[level].ExpToNext</c>), rounded to a whole percent, half away from zero; under 1% reads
    /// <see cref="LevelShare.UnderOne"/>. Null when there is nothing to say: no EXP, an unknown level, or a level with no
    /// next one (the level cap) or no row (<see cref="QuestExpTable.ExpToNext"/>).
    /// </summary>
    public static LevelShare? ShareOfLevel(ulong exp, int level, QuestExpTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (exp == 0 || table.ExpToNext(level) is not { } next)
        {
            return null;
        }

        var hundredfold = (decimal)exp * 100m;
        if (hundredfold < next)
        {
            return new LevelShare(0, true);
        }

        var percent = Math.Round(hundredfold / next, MidpointRounding.AwayFromZero);
        return new LevelShare(percent >= int.MaxValue ? int.MaxValue : (int)percent, false);
    }
}
