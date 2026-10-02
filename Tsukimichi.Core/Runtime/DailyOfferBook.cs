using System.Runtime.CompilerServices;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// The allied society dailies offered today, as far as they are known (decision 5 of feature plan v5): the quest ids
/// (<see cref="Quests"/>) and the societies they speak for (<see cref="Tribes"/>). A daily of a society in
/// <see cref="Tribes"/> that is not in <see cref="Quests"/> is not offered today; a society outside it is unknown and
/// its dailies are not held back.
/// </summary>
public sealed record DailyOffer(IReadOnlySet<ushort> Quests, IReadOnlySet<byte> Tribes)
{
    public static readonly DailyOffer None = new(new HashSet<ushort>(), new HashSet<byte>());

    /// <summary>Whether no society's offer is known.</summary>
    public bool IsEmpty => Tribes.Count == 0;

    /// <summary>Whether two offers hold the same quests for the same societies.</summary>
    public bool SameAs(DailyOffer? other) =>
        other is not null && Quests.Count == other.Quests.Count && Tribes.Count == other.Tribes.Count
        && Quests.SetEquals(other.Quests) && Tribes.SetEquals(other.Tribes);
}

/// <summary>
/// What each allied society quest giver offers today, gathered as the plugin reads it: the game computes a giver's
/// offer (<c>DailyQuestMap.CalculateAvailableQuests</c>) only for the givers whose quests it has loaded, so the book
/// keeps each giver's answer for the rest of the day and assembles the societies whose every giver is known. An
/// answer holds for the daily cycle it was read in (<see cref="GameResets.LastDaily"/>), for the character it was read
/// for, for the daily seed it was computed with (<c>QuestManager.DailyQuestSeed</c>: the server's pick for the day,
/// which may arrive after the reset or differ from the local clock's idea of the day) and for the standing it was
/// computed at (rank and the ranked-up-today mark, both of which change the offer); anything else drops it. A seed
/// other than the one the answers were computed with drops them all. Pure bookkeeping, no game access; not
/// thread-safe (the framework thread owns it).
/// </summary>
public sealed class DailyOfferBook
{
    // Per catalog: each daily giver (the issuer of an allied society daily in a daily pool) with its society and the
    // lowest rank its dailies ask for.
    private static readonly ConditionalWeakTable<QuestCatalog, IReadOnlyDictionary<byte, Giver[]>> GiversByTribe = [];

    private readonly Dictionary<uint, Answer> answers = [];
    private readonly HashSet<byte> mismatchReported = [];
    private readonly List<byte> mismatched = [];
    private ulong contentId;
    private DateTime cycle;
    private byte seed;

    /// <summary>How many givers have an answer held now.</summary>
    public int Count => answers.Count;

    /// <summary>Forgets every answer (a logout, another character, a catalog change).</summary>
    public void Clear()
    {
        answers.Clear();
        mismatchReported.Clear();
        mismatched.Clear();
    }

    /// <summary>
    /// Notes the game's current daily seed: answers computed with another seed are dropped. The reader calls it on
    /// every read, so a seed that changes while no giver is loaded still retires the old answers.
    /// </summary>
    public void Observe(ulong character, DateTime nowUtc, byte dailySeed)
    {
        Roll(character, nowUtc);
        if (dailySeed != seed)
        {
            answers.Clear();
            mismatchReported.Clear();
            seed = dailySeed;
        }
    }

    /// <summary>
    /// Whether <paramref name="giver"/> already has an answer valid for this character, this cycle, this daily seed
    /// and this standing; the reader skips asking the game again.
    /// </summary>
    public bool Knows(ulong character, DateTime nowUtc, byte dailySeed, uint giver, TribeStanding standing)
    {
        Observe(character, nowUtc, dailySeed);
        return answers.TryGetValue(giver, out var answer) && answer.Seed == dailySeed
            && answer.Rank == standing.Rank && answer.RankedUpToday == standing.RankedUpToday;
    }

    /// <summary>Records what <paramref name="giver"/> offers today at <paramref name="standing"/>, computed with <paramref name="dailySeed"/>.</summary>
    public void Record(ulong character, DateTime nowUtc, byte dailySeed, uint giver, byte tribe, TribeStanding standing, IEnumerable<ushort> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        Observe(character, nowUtc, dailySeed);
        answers[giver] = new Answer(tribe, dailySeed, standing.Rank, standing.RankedUpToday, [.. quests]);
    }

    /// <summary>
    /// Today's offer for <paramref name="snapshot"/>'s character: every society for which at least one giver was read
    /// at its current standing and every other giver is either read too or offers nothing at that rank (all its
    /// dailies ask for a higher one). <see cref="DailyOffer.None"/> when nothing is known, for another character, or
    /// once the daily reset passed since the answers were read.
    /// </summary>
    public DailyOffer Offer(QuestCatalog catalog, CharacterSnapshot snapshot, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);
        Roll(snapshot.ContentId, nowUtc);
        if (answers.Count == 0)
        {
            return DailyOffer.None;
        }

        var quests = new HashSet<ushort>();
        var tribes = new HashSet<byte>();
        foreach (var (tribe, givers) in GiversByTribe.GetValue(catalog, BuildGivers))
        {
            var standing = snapshot.Tribes.GetValueOrDefault(tribe);
            var read = 0;
            var complete = true;
            foreach (var giver in givers)
            {
                if (answers.TryGetValue(giver.NpcId, out var answer) && answer.Tribe == tribe && answer.Seed == seed
                    && answer.Rank == standing.Rank && answer.RankedUpToday == standing.RankedUpToday)
                {
                    read++;
                }
                else if (giver.LowestRank <= standing.Rank)
                {
                    complete = false;
                    break;
                }
            }

            if (!complete || read == 0)
            {
                continue;
            }

            var offered = new HashSet<ushort>();
            foreach (var giver in givers)
            {
                if (answers.TryGetValue(giver.NpcId, out var answer))
                {
                    offered.UnionWith(answer.Quests);
                }
            }

            // The client's own evidence: a daily taken today (in the journal or turned in) was offered. An offer that
            // misses one is wrong, and the society reads as unknown rather than hold back a daily the giver lists.
            if (MissesTaken(givers, offered, snapshot))
            {
                if (mismatchReported.Add(tribe))
                {
                    mismatched.Add(tribe);
                }

                continue;
            }

            tribes.Add(tribe);
            quests.UnionWith(offered);
        }

        return tribes.Count == 0 ? DailyOffer.None : new DailyOffer(quests, tribes);
    }

    /// <summary>
    /// The societies whose computed offer missed a daily the character took today, each reported once per daily cycle
    /// since the last read of this property (the reader logs them); reading it empties it.
    /// </summary>
    public IReadOnlyList<byte> Mismatched
    {
        get
        {
            if (mismatched.Count == 0)
            {
                return [];
            }

            var list = mismatched.ToArray();
            mismatched.Clear();
            return list;
        }
    }

    private static bool MissesTaken(Giver[] givers, HashSet<ushort> offered, CharacterSnapshot snapshot)
    {
        foreach (var giver in givers)
        {
            foreach (var daily in giver.Dailies)
            {
                if (offered.Contains(daily.QuestId))
                {
                    continue;
                }

                if (snapshot.DailyDone.ContainsKey(daily.QuestId))
                {
                    return true;
                }

                foreach (var accepted in snapshot.Accepted)
                {
                    if (accepted.QuestId == daily.QuestId)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The allied society dailies (in a daily pool) whose giver is <paramref name="giver"/>, from the catalog: the
    /// reader checks the game's loaded quest list for the giver against it before trusting an answer.
    /// </summary>
    public static IReadOnlyList<QuestRecord> DailiesOf(QuestCatalog catalog, uint giver)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        foreach (var givers in GiversByTribe.GetValue(catalog, BuildGivers).Values)
        {
            foreach (var g in givers)
            {
                if (g.NpcId == giver)
                {
                    return g.Dailies;
                }
            }
        }

        return [];
    }

    /// <summary>Drops every answer when the character or the daily cycle changed since they were read.</summary>
    private void Roll(ulong character, DateTime nowUtc)
    {
        var current = GameResets.LastDaily(nowUtc);
        if (character != contentId || current != cycle)
        {
            answers.Clear();
            mismatchReported.Clear();
            contentId = character;
            cycle = current;
        }
    }

    private static IReadOnlyDictionary<byte, Giver[]> BuildGivers(QuestCatalog catalog)
    {
        var byGiver = new Dictionary<uint, (byte Tribe, List<QuestRecord> Dailies)>();
        var orphaned = new HashSet<byte>();
        foreach (var quest in catalog.All)
        {
            if (!quest.IsAlliedSocietyDaily || quest.DailyPool == 0 || quest.IsRemoved)
            {
                continue;
            }

            if (quest.Issuer is not { NpcId: not 0 } issuer)
            {
                // A daily without a giver could never be read: its society's offer is never complete.
                orphaned.Add(quest.BeastTribe);
                continue;
            }

            if (!byGiver.TryGetValue(issuer.NpcId, out var entry))
            {
                entry = (quest.BeastTribe, []);
                byGiver[issuer.NpcId] = entry;
            }

            entry.Dailies.Add(quest);
        }

        var result = new Dictionary<byte, List<Giver>>();
        foreach (var (npc, (tribe, dailies)) in byGiver)
        {
            if (orphaned.Contains(tribe))
            {
                continue;
            }

            if (!result.TryGetValue(tribe, out var list))
            {
                list = [];
                result[tribe] = list;
            }

            list.Add(new Giver(npc, dailies.Min(q => q.BeastRank), [.. dailies]));
        }

        return result.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    private sealed record Giver(uint NpcId, byte LowestRank, QuestRecord[] Dailies);

    private sealed record Answer(byte Tribe, byte Seed, byte Rank, bool RankedUpToday, ushort[] Quests);
}
