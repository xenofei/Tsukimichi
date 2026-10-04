using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Core.Plan;

/// <summary>
/// An allied society daily still in the journal from before the last daily reset (feature plan v7, 1.19.0, C5).
/// </summary>
/// <param name="Quest">The daily.</param>
/// <param name="AcceptedUtc">When the journal is first known to have held it (the accepted-times sidecar, else the capture).</param>
public sealed record CarriedDaily(QuestRecord Quest, DateTime AcceptedUtc);

/// <summary>
/// Carried-over allied society dailies (feature plan v7, 1.19.0, C5). The game gives 12 allowances a day, shared by
/// every society, and up to 3 dailies a day per society (3 more on the day the society ranks up). A daily accepted
/// before the daily reset and still in the journal after it holds the next day back: until it is turned in (or
/// abandoned), no daily can be accepted, so the day's allowances read 0 (the wiki's Allied Society Quests page:
/// "Players cannot accept additional quests until they have completed or abandoned any quests they may still have from
/// a previous day"). Players miss this and lose whole days. Pure; every method takes the clock as an argument.
/// </summary>
public static class AlliedCarryover
{
    /// <summary>Dailies each society offers a day.</summary>
    public const int DailiesPerSociety = 3;

    /// <summary>The extra dailies a society offers on the day it ranks up.</summary>
    public const int RankUpBonusDailies = 3;

    /// <summary>
    /// The allied society dailies in <paramref name="snapshot"/>'s journal accepted before the last daily reset at
    /// <paramref name="nowUtc"/>, by quest row id. When a daily was accepted is the earlier of its accepted time
    /// (<paramref name="acceptedSince"/>, the character's sidecar; null when not loaded) and the capture's own time: a
    /// stored character captured before the reset with a daily in its journal therefore holds it over the reset.
    /// </summary>
    public static IReadOnlyList<CarriedDaily> Find(
        QuestCatalog catalog,
        CharacterSnapshot snapshot,
        IReadOnlyDictionary<ushort, DateTime>? acceptedSince,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);
        var reset = GameResets.LastDaily(nowUtc);
        List<CarriedDaily>? carried = null;
        foreach (var accepted in snapshot.Accepted)
        {
            if (!catalog.TryGetByQuestId(accepted.QuestId, out var quest) || !quest.IsAlliedSocietyDaily)
            {
                continue;
            }

            var taken = AsUtc(snapshot.TakenUtc);
            var since = acceptedSince is not null && acceptedSince.TryGetValue(accepted.QuestId, out var time) && AsUtc(time) < taken ? AsUtc(time) : taken;
            if (since < reset)
            {
                (carried ??= []).Add(new CarriedDaily(quest, since));
            }
        }

        if (carried is null)
        {
            return [];
        }

        carried.Sort(static (a, b) => a.Quest.RowId.CompareTo(b.Quest.RowId));
        return carried;
    }

    /// <summary>
    /// Whether the society's rank-up quest waits: the rank's reputation is full and a non-repeatable quest of the
    /// society at that rank that needs the reputation maxed is not completed. Whether ranking up then opens the bonus
    /// dailies is <see cref="RankUpOpensBonus"/>.
    /// </summary>
    public static bool RankUpReady(QuestCatalog catalog, CharacterSnapshot snapshot, AlliedSocietyRow row)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(row);
        if (!row.Maxed)
        {
            return false;
        }

        foreach (var quest in catalog.All)
        {
            if (quest.BeastTribe == row.Tribe && !quest.IsRepeatable && !quest.IsRemoved && quest.BeastReputationMaxed
                && quest.BeastRank == row.Rank && !snapshot.IsCompleted(quest.QuestId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether ranking up from <paramref name="rank"/> opens the bonus dailies that day: every rank-up does except the
    /// one to Sworn, unless the society is the Moogles or the Namazu, whose dailies go on past it (Console Games Wiki,
    /// Reputation). The ids are pinned to the game data by a test.
    /// </summary>
    public static bool RankUpOpensBonus(byte tribe, byte rank) => rank + 1 != SwornRank || BonusAtSworn.Contains(tribe);

    /// <summary>The Sworn rank's BeastReputationRank row id.</summary>
    public const byte SwornRank = 7;

    /// <summary>The societies whose rank-up to Sworn still opens bonus dailies: the Moogles (8) and the Namazu (11), BeastTribe row ids.</summary>
    public static readonly IReadOnlySet<byte> BonusAtSworn = new HashSet<byte> { 8, 11 };

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value,
    };
}
