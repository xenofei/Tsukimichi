using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Rewards;

/// <summary>
/// The two <c>ParamGrow</c> columns the quest EXP formula reads, by level (the sheet's row id is the level):
/// <c>QuestExpModifier</c> and <c>ScaledQuestXP</c>. The plugin reads them from the game at catalog build
/// (<c>CatalogMapper</c>); Core cannot read the sheets, so a catalog built without them (the test fixture) has
/// <see cref="Empty"/>, and every quest's EXP is then unknown rather than guessed. Immutable.
/// </summary>
public sealed class QuestExpTable
{
    /// <summary>No rows: every EXP reads <see cref="ExpKind.Unknown"/>.</summary>
    public static readonly QuestExpTable Empty = new([], []);

    private readonly uint[] modifiers;
    private readonly uint[] scales;

    private QuestExpTable(uint[] modifiers, uint[] scales)
    {
        this.modifiers = modifiers;
        this.scales = scales;
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
        var list = rows.Where(static r => r.Level is >= 0 and <= byte.MaxValue).ToList();
        if (list.Count == 0)
        {
            return Empty;
        }

        var size = list.Max(static r => r.Level) + 1;
        var modifiers = new uint[size];
        var scales = new uint[size];
        foreach (var (level, modifier, scale) in list)
        {
            modifiers[level] = modifier;
            scales[level] = scale;
        }

        return new QuestExpTable(modifiers, scales);
    }

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
}
