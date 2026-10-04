using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Core.Plan;

/// <summary>
/// One allied society the character has unlocked, as the board shows it (R6 E).
/// </summary>
/// <param name="Tribe">The BeastTribe row id.</param>
/// <param name="Rank">The rank (a BeastReputationRank row id).</param>
/// <param name="Reputation">Reputation within the rank.</param>
/// <param name="RankMax">
/// The reputation that maxes the rank (<see cref="TribeRanks.MaxReputation"/>, the 1.5 rank-up thresholds); 0 at the
/// last rank (nothing to max) and null for a rank the table does not know.
/// </param>
/// <param name="RankedUpToday">The rank went up today (the game holds the society's quests back until the reset).</param>
/// <param name="DoneToday">The society's dailies turned in this cycle.</param>
/// <param name="OfferedToday">
/// How many dailies the society offers today, when the plugin read every giver of it today (the 1.5 daily offer,
/// <see cref="DailyOfferBook"/>); null while unknown, which the board words as "unknown until you visit the givers".
/// </param>
/// <param name="Giver">
/// A daily of the society's main giver at the character's rank (the giver with the most dailies open to it), for the
/// zone and the Teleport; null when no daily of the society has a giver.
/// </param>
public sealed record AlliedSocietyRow(
    byte Tribe,
    byte Rank,
    ushort Reputation,
    ushort? RankMax,
    bool RankedUpToday,
    int DoneToday,
    int? OfferedToday,
    QuestRecord? Giver)
{
    /// <summary>
    /// A daily of this society still in the journal from before the last reset (1.19.0, C5,
    /// <see cref="AlliedCarryover"/>): no daily can be accepted until it is turned in. Null when none.
    /// </summary>
    public QuestRecord? Carried { get; init; }

    /// <summary>
    /// The rank-up quest waits and ranking up opens the 3 bonus dailies that day (<see cref="AlliedCarryover.RankUpReady"/>,
    /// <see cref="AlliedCarryover.RankUpOpensBonus"/>): worth keeping 3 allowances for.
    /// </summary>
    public bool RankUpBonus { get; init; }

    /// <summary>The rank's reputation is full: the rank-up quest (or the next rank) waits.</summary>
    public bool Maxed => RankMax is > 0 && Reputation >= RankMax;

    /// <summary>The last rank: no reputation left to earn.</summary>
    public bool AtLastRank => RankMax == 0;

    /// <summary>How far the rank's reputation is filled, 0 to 1; 1 at the last rank, 0 when the maximum is unknown.</summary>
    public float Fraction => RankMax switch
    {
        0 => 1f,
        > 0 and var max => Math.Clamp((float)Reputation / max, 0f, 1f),
        _ => 0f,
    };
}

/// <summary>The allied society board: every unlocked society, the allowances left today and when the day resets.</summary>
/// <param name="Rows">The unlocked societies, by BeastTribe id.</param>
/// <param name="AllowancesLeft">Allied society quest allowances left today (shared by every society).</param>
/// <param name="NextReset">The next daily reset (<see cref="GameResets.NextDaily"/>), UTC.</param>
public sealed record AlliedSocietyBoardModel(IReadOnlyList<AlliedSocietyRow> Rows, byte AllowancesLeft, DateTime NextReset)
{
    public static readonly AlliedSocietyBoardModel Empty = new([], 0, DateTime.MinValue);

    /// <summary>
    /// The dailies carried over the last reset (1.19.0, C5): while any is in the journal the day's allowances read 0
    /// (<see cref="AllowancesLeft"/> says so).
    /// </summary>
    public IReadOnlyList<CarriedDaily> Carried { get; init; } = [];

    /// <summary>
    /// The snapshot predates the last daily reset (a stored character): the allowances are projected from it, not read
    /// (<see cref="GameResets.AsOf"/>). <see cref="TakenUtc"/> says from when.
    /// </summary>
    public bool Projected { get; init; }

    /// <summary>When the snapshot the board was built from was captured, UTC.</summary>
    public DateTime TakenUtc { get; init; }
}

/// <summary>
/// The allied society board (feature plan v5 "Planning extras", R6 E). Spoiler-safe: a society the character has not
/// unlocked (rank 0, or no standing at all) is not listed. A stored character's dailies and allowances are read as of
/// <c>nowUtc</c> (<see cref="GameResets.AsOf"/>), so a save from before the reset reads as a fresh day. Pure.
/// </summary>
public static class AlliedSocietyBoard
{
    /// <summary>Builds the board.</summary>
    /// <param name="offered">Today's offered dailies (<see cref="EvalContext.TodaysDailyOffer"/>); null when unknown.</param>
    /// <param name="offerTribes">The societies <paramref name="offered"/> speaks for (<see cref="EvalContext.DailyOfferTribes"/>); null with a known offer means every society.</param>
    /// <param name="acceptedSince">The character's accepted times (<see cref="AcceptedSince"/>), for the carried-over dailies (<see cref="AlliedCarryover.Find"/>); null reads each from the capture's time.</param>
    public static AlliedSocietyBoardModel Build(
        QuestCatalog catalog,
        CharacterSnapshot snapshot,
        IReadOnlySet<ushort>? offered,
        IReadOnlySet<byte>? offerTribes,
        DateTime nowUtc,
        IReadOnlyDictionary<ushort, DateTime>? acceptedSince = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);
        var now = GameResets.AsOf(snapshot, catalog, nowUtc);
        var carried = AlliedCarryover.Find(catalog, snapshot, acceptedSince, nowUtc);
        var dailies = DailiesByTribe(catalog);
        var rows = new List<AlliedSocietyRow>();
        foreach (var (tribe, raw) in now.Tribes.OrderBy(static kv => kv.Key))
        {
            var standing = raw.Masked();
            if (tribe == 0 || standing.Rank == 0)
            {
                continue;
            }

            var own = dailies.GetValueOrDefault(tribe) ?? [];
            var done = 0;
            var offeredCount = 0;
            foreach (var quest in own)
            {
                done += now.DailyDone.ContainsKey(quest.QuestId) ? 1 : 0;
                offeredCount += offered?.Contains(quest.QuestId) == true ? 1 : 0;
            }

            var known = offered is not null && (offerTribes is null || offerTribes.Contains(tribe));
            var row = new AlliedSocietyRow(
                tribe,
                standing.Rank,
                standing.Value,
                TribeRanks.MaxReputation(standing.Rank),
                standing.RankedUpToday,
                done,
                known ? offeredCount : null,
                MainGiver(own, standing.Rank, offered))
            {
                Carried = carried.FirstOrDefault(c => c.Quest.BeastTribe == tribe)?.Quest,
            };
            rows.Add(row with
            {
                RankUpBonus = AlliedCarryover.RankUpReady(catalog, now, row) && AlliedCarryover.RankUpOpensBonus(tribe, standing.Rank),
            });
        }

        // A daily carried over the reset holds every allowance back until it is turned in, whatever the game's count says.
        var allowances = carried.Count > 0 ? (byte)0 : now.TribeAllowance;
        return new AlliedSocietyBoardModel(rows, allowances, GameResets.NextDaily(nowUtc))
        {
            Carried = carried,
            Projected = snapshot.TakenUtc < GameResets.LastDaily(nowUtc),
            TakenUtc = snapshot.TakenUtc,
        };
    }

    /// <summary>
    /// A daily of the society's main giver at <paramref name="rank"/>: of the givers with a daily open at that rank, the
    /// one offering today's dailies when known, else the one with the most; the lowest row id of its dailies. Null when
    /// no daily has a giver.
    /// </summary>
    private static QuestRecord? MainGiver(IReadOnlyList<QuestRecord> dailies, byte rank, IReadOnlySet<ushort>? offered)
    {
        QuestRecord? best = null;
        var bestScore = (Offered: -1, Count: -1);
        foreach (var group in dailies.Where(q => q.Issuer is { NpcId: not 0 } && q.BeastRank <= rank).GroupBy(static q => q.Issuer!.NpcId))
        {
            var score = (Offered: group.Count(q => offered?.Contains(q.QuestId) == true), Count: group.Count());
            var first = group.MinBy(static q => q.RowId)!;
            if (best is null || score.CompareTo(bestScore) > 0 || (score == bestScore && first.RowId < best.RowId))
            {
                best = first;
                bestScore = score;
            }
        }

        return best ?? dailies.Where(static q => q.Issuer is { NpcId: not 0 }).MinBy(static q => q.RowId);
    }

    private static Dictionary<byte, List<QuestRecord>> DailiesByTribe(QuestCatalog catalog)
    {
        var map = new Dictionary<byte, List<QuestRecord>>();
        foreach (var quest in catalog.All)
        {
            if (!quest.IsAlliedSocietyDaily || quest.IsRemoved)
            {
                continue;
            }

            if (!map.TryGetValue(quest.BeastTribe, out var list))
            {
                map[quest.BeastTribe] = list = [];
            }

            list.Add(quest);
        }

        return map;
    }
}
