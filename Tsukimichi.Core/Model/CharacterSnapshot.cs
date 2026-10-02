using System.Text.Json.Serialization;

namespace Tsukimichi.Core.Model;

/// <summary>
/// A quest the character currently has in the journal, with its step and, when the client says, the ClassJob it was
/// accepted on (<c>QuestWork.AcceptClassJob</c>). <paramref name="AcceptClassJob"/> is additive at schema v1: 0 means
/// unknown (files written before it was read, allied-society dailies), and 0 is not written to disk.
/// </summary>
public readonly record struct AcceptedQuest(
    ushort QuestId,
    byte Sequence,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] byte AcceptClassJob = 0);

/// <summary>
/// Standing with one allied society (beast tribe). <paramref name="Rank"/> is the real rank (a BeastReputationRank row
/// id), never the client's raw byte: build one from the client with <see cref="FromClient"/>.
/// <paramref name="RankedUpToday"/> is additive at schema v1: false in files written before it was read, and false is
/// not written to disk.
/// </summary>
public readonly record struct TribeStanding(
    byte Rank,
    ushort Value,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool RankedUpToday = false)
{
    /// <summary>The high bit of the client's rank byte (<c>BeastReputationWork.Rank</c>): set on the day the rank went up.</summary>
    public const byte RankedUpTodayBit = 0x80;

    /// <summary>
    /// The standing for the client's raw rank byte: the rank-up bit is masked off <see cref="Rank"/> and kept as
    /// <see cref="RankedUpToday"/>, so a rank-up day never reads as rank 128 and up.
    /// </summary>
    public static TribeStanding FromClient(byte rawRank, ushort value, bool rankedUpToday = false) =>
        new((byte)(rawRank & ~RankedUpTodayBit), value, rankedUpToday || (rawRank & RankedUpTodayBit) != 0);

    /// <summary>This standing with a stray rank-up bit moved out of <see cref="Rank"/>; unchanged when the rank is already real.</summary>
    public TribeStanding Masked() => Rank < RankedUpTodayBit ? this : FromClient(Rank, Value, RankedUpToday);
}

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

    /// <summary>
    /// The QuestRepeatFlag rows the client has set (<c>QuestManager.IsQuestRepeatFlagSet</c>), ascending: a repeatable
    /// that carries one (<see cref="QuestRecord.RepeatFlag"/>) was turned in this cycle, and the game clears the flag
    /// at the quest's daily or weekly reset. Additive at schema v1: empty in files written before it was read, which
    /// reads as no flag set (the behaviour of those builds). Not written while empty.
    /// </summary>
    [OmitWhenEmpty]
    public IReadOnlyList<byte> RepeatFlags { get; init; } = [];

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

    /// <summary>
    /// Phase of each running festival, parallel to <see cref="ActiveFestivals"/> (<c>GameMain.Festival.Phase</c>).
    /// Additive at schema v1: empty in files written before it was read, and an id past the end of this list has an
    /// unknown phase, which never blocks a quest. Not written while empty, so an older file round-trips unchanged.
    /// </summary>
    [OmitWhenEmpty]
    public IReadOnlyList<ushort> ActiveFestivalPhases { get; init; } = [];

    /// <summary>
    /// Custom delivery satisfaction rank per client, keyed by SatisfactionNpc row id (1-based; every slot the client
    /// holds, rank 0 included, so a client not yet unlocked reads rank 0). Additive at schema v1: empty when the
    /// plugin did not read the ranks or every slot read 0 (see <see cref="Runtime.SatisfactionRankSlots"/>), and a
    /// missing client is "not checked". Not written while empty.
    /// </summary>
    [OmitWhenEmpty]
    public IReadOnlyDictionary<byte, byte> SatisfactionRanks { get; init; } = new Dictionary<byte, byte>();

    /// <summary>
    /// Delivery Moogle carrier level (<c>PlayerState.DeliveryLevel</c>). Additive at schema v1: null when the plugin did
    /// not read it (files written before 0.6.2), which reads as "not checked"; null is not written. A captured 0 is a
    /// real level (the character has not unlocked the Delivery Moogle) and blocks every carrier-level gate.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? CarrierLevel { get; init; }

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

    /// <summary>Whether the client had QuestRepeatFlag row <paramref name="flag"/> set; false for 0 and for a flag not captured.</summary>
    public bool IsRepeatFlagSet(byte flag)
    {
        if (flag == 0)
        {
            return false;
        }

        foreach (var set in RepeatFlags)
        {
            if (set == flag)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A repeatable turned in this cycle by the client's cycle data: its allied society daily slot reads completed
    /// (<see cref="DailyDone"/>), or the repeat flag it carries is set (<see cref="RepeatFlags"/>).
    /// </summary>
    public bool IsDoneThisCycle(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return DailyDone.ContainsKey(quest.QuestId) || IsRepeatFlagSet(quest.RepeatFlag);
    }

    /// <summary>The phase of a running festival, or null when it is not running or its phase was not captured.</summary>
    public ushort? FestivalPhase(ushort festival)
    {
        for (var i = 0; i < ActiveFestivals.Count; i++)
        {
            if (ActiveFestivals[i] == festival)
            {
                return i < ActiveFestivalPhases.Count ? ActiveFestivalPhases[i] : null;
            }
        }

        return null;
    }

    /// <summary>
    /// This snapshot with every allied society rank masked (<see cref="TribeStanding.Masked"/>). Builds before 1.4.2
    /// saved the client's raw rank byte, so a file written on a rank-up day holds 128 + rank; the store repairs it on
    /// load. Returns this instance when no rank needs it.
    /// </summary>
    public CharacterSnapshot WithMaskedTribeRanks()
    {
        var dirty = false;
        foreach (var standing in Tribes.Values)
        {
            if (standing.Rank >= TribeStanding.RankedUpTodayBit)
            {
                dirty = true;
                break;
            }
        }

        if (!dirty)
        {
            return this;
        }

        var tribes = new Dictionary<byte, TribeStanding>(Tribes.Count);
        foreach (var (tribe, standing) in Tribes)
        {
            tribes[tribe] = standing.Masked();
        }

        return this with { Tribes = tribes };
    }

    /// <summary>The satisfaction rank held with a custom delivery client, or null when the plugin did not read it.</summary>
    public byte? SatisfactionRank(byte npc) => SatisfactionRanks.TryGetValue(npc, out var rank) ? rank : null;
}

/// <summary>
/// Marks a collection property that the storage serializer leaves out of the file while it is empty, so a field
/// added after a schema version was frozen keeps an older file byte-identical on a save (the reader defaults it).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class OmitWhenEmptyAttribute : Attribute;
