namespace Tsukimichi.Core.Model;

/// <summary>A quest the character currently has in the journal, with its step.</summary>
public readonly record struct AcceptedQuest(ushort QuestId, byte Sequence);

/// <summary>Standing with one allied society (beast tribe).</summary>
public readonly record struct TribeStanding(byte Rank, ushort Value);

/// <summary>
/// Everything the evaluator needs about one character, captured on the framework thread and persisted per ContentId.
/// Unknown quest ids are preserved through storage; the model does not validate them against a catalog.
/// </summary>
public sealed record CharacterSnapshot
{
    /// <summary>Schema version written by this build. Bump when a field changes meaning; the store migrates on read.</summary>
    public const int CurrentSchemaVersion = 1;

    public ulong ContentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public uint World { get; init; }
    public DateTime TakenUtc { get; init; }
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Completion bitmask as held by the client; bit index is the quest id.</summary>
    public byte[] CompletedBits { get; init; } = [];

    public IReadOnlyList<AcceptedQuest> Accepted { get; init; } = [];

    /// <summary>Repeatable quests done this cycle: quest id to the flag byte from the client.</summary>
    public IReadOnlyDictionary<ushort, byte> DailyDone { get; init; } = new Dictionary<ushort, byte>();

    /// <summary>Unsynced level per ClassJob row id.</summary>
    public IReadOnlyDictionary<byte, short> JobLevels { get; init; } = new Dictionary<byte, short>();

    public byte GrandCompany { get; init; }

    /// <summary>Rank per Grand Company, indexed by Grand Company id.</summary>
    public byte[] GcRanks { get; init; } = [];

    public IReadOnlyDictionary<byte, TribeStanding> Tribes { get; init; } = new Dictionary<byte, TribeStanding>();
    public byte TribeAllowance { get; init; }
    public byte LeveAllowance { get; init; }

    public IReadOnlyList<uint> UnlockedInstances { get; init; } = [];
    public IReadOnlyList<ushort> ActiveFestivals { get; init; } = [];
    public byte MaxExpansion { get; init; }
    public byte LevelCap { get; init; }

    /// <summary>False until the client has fetched achievements (Achievements window opened once per session).</summary>
    public bool AchievementsLoaded { get; init; }
    public IReadOnlyList<uint> CompletedAchievements { get; init; } = [];

    public byte CurrentJob { get; init; }

    /// <summary>Reads the completion bit for a quest id; false when the bitmask is shorter than the id.</summary>
    public bool IsCompleted(ushort questId)
    {
        var index = questId >> 3;
        return index < CompletedBits.Length && (CompletedBits[index] & (1 << (questId & 7))) != 0;
    }
}
