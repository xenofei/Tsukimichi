namespace Tsukimichi.Core.Model;

/// <summary>
/// One quest as read from the game sheets. Immutable; built once per catalog load.
/// Init-only properties so loaders and tests can use object initializers, and JSON round-trips without a custom converter.
/// </summary>
public sealed record QuestRecord
{
    /// <summary>Row id in the Quest sheet (65536 + n).</summary>
    public uint RowId { get; init; }

    /// <summary>Low 16 bits of <see cref="RowId"/>; every runtime lookup uses this.</summary>
    public ushort QuestId { get; init; }

    /// <summary>Internal script id, e.g. ManSea001_00107.</summary>
    public string InternalId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public JournalRef Journal { get; init; } = JournalRef.None;

    public byte Expansion { get; init; }
    /// <summary>
    /// Acceptance level from <c>Quest.ClassJobLevel[0]</c>: the level a job must reach to take the quest. The
    /// evaluator gates on this; the interface shows <see cref="DisplayLevel"/>.
    /// </summary>
    public byte Level { get; init; }

    public byte LevelMax { get; init; }

    /// <summary><c>Quest.QuestLevelOffset</c>: what the journal adds to <see cref="Level"/> for the level it prints.</summary>
    public byte LevelOffset { get; init; }

    /// <summary>
    /// The level the game journal and the Lodestone print: <see cref="Level"/> plus <see cref="LevelOffset"/> (at most
    /// 117 in the current sheets, so the byte never wraps). Every level the interface shows, sorts or filters on is
    /// this one; requirements still use <see cref="Level"/>.
    /// </summary>
    public byte DisplayLevel => (byte)(Level + LevelOffset);

    public uint ClassJobCategory { get; init; }
    public uint ClassJobCategory1 { get; init; }
    public uint ClassJobRequired { get; init; }

    public Prereq PreviousQuests { get; init; } = Prereq.None;

    /// <summary>Quests that foreclose this one once completed.</summary>
    public uint[] QuestLocks { get; init; } = [];

    public uint[] InstanceContentRequired { get; init; } = [];
    public JoinKind InstanceJoin { get; init; } = JoinKind.All;

    public byte GrandCompany { get; init; }
    public byte GrandCompanyRank { get; init; }
    public byte BeastTribe { get; init; }
    public byte BeastRank { get; init; }
    public ushort BeastValue { get; init; }

    public bool IsRepeatable { get; init; }
    public byte RepeatInterval { get; init; }
    public byte DailyPool { get; init; }
    public ushort Festival { get; init; }

    public bool MountRequired { get; init; }
    public bool HouseRequired { get; init; }
    public uint[] AcceptConditions { get; init; } = [];

    public Issuer? Issuer { get; init; }

    /// <summary>Journal banner artwork icon id; zero when the quest has none.</summary>
    public uint Icon { get; init; }

    /// <summary>Small icon the journal shows beside special quests (seasonal events, promotions); zero for ordinary quests.</summary>
    public uint IconSpecial { get; init; }

    /// <summary>
    /// Journal icon family from <c>Quest.EventIconType</c>: 3 is the ordinary side quest, 8 the blue "+" feature
    /// quest (see <c>FeaturePresets.FeatureEventIconType</c>); zero when the sheet has none.
    /// </summary>
    public byte EventIconType { get; init; }

    public IReadOnlyList<RewardRef> Rewards { get; init; } = [];
    public uint ExpFactor { get; init; }
    public uint Gil { get; init; }

    /// <summary>Quests with no journal genre are hidden, removed or legacy; the UI buckets them as Unlisted.</summary>
    public bool IsUnlisted => Journal.GenreId == 0;

    /// <summary>Computes the runtime quest id from a Quest sheet row id.</summary>
    public static ushort ToQuestId(uint rowId) => (ushort)(rowId & 0xFFFF);
}
