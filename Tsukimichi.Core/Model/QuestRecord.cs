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
    public byte Level { get; init; }
    public byte LevelMax { get; init; }
    public byte LevelOffset { get; init; }

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

    public IReadOnlyList<RewardRef> Rewards { get; init; } = [];
    public uint ExpFactor { get; init; }
    public uint Gil { get; init; }

    /// <summary>Quests with no journal genre are hidden, removed or legacy; the UI buckets them as Unlisted.</summary>
    public bool IsUnlisted => Journal.GenreId == 0;

    /// <summary>Computes the runtime quest id from a Quest sheet row id.</summary>
    public static ushort ToQuestId(uint rowId) => (ushort)(rowId & 0xFFFF);
}
