using System.Collections.Frozen;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Sources;

namespace Tsukimichi.Core.Triad;

/// <summary>
/// One NPC who plays Triple Triad (feature plan v7, 1.21.0 P6): a <c>TripleTriad</c> row with the <c>ENpcBase</c> whose
/// event data opens the match, where that NPC stands, the quests the game wants done before it plays you, and the
/// cards it can hand out.
/// </summary>
/// <param name="ResidentId">The <c>TripleTriad</c> (and <c>TripleTriadResident</c>) row id; what the game's beaten flag is read by.</param>
/// <param name="NpcId">The <c>ENpcBase</c>/<c>ENpcResident</c> row id of the NPC.</param>
/// <param name="Name">The NPC's name as the game writes it ("Elaisse").</param>
/// <param name="Spot">Where the NPC stands (its <c>Level</c> row, else the zone's event layout); null when the data does not say.</param>
/// <param name="Gate">
/// <c>TripleTriad.PreviousQuest</c> with <c>PreviousQuestJoin</c> (2 any, else all): the quests that open the match.
/// <see cref="Prereq.None"/> for an opponent that plays anyone.
/// </param>
/// <param name="Cards">The <c>TripleTriadCard</c> row ids of <c>TripleTriad.ItemPossibleReward</c>, distinct, in sheet order.</param>
public sealed record TriadOpponent(uint ResidentId, uint NpcId, string Name, WorldSpot? Spot, Prereq Gate, IReadOnlyList<uint> Cards)
{
    /// <summary>The game wants a quest done first.</summary>
    public bool QuestGated => !Gate.IsEmpty;

    /// <summary>The zone the NPC stands in; empty when unplaced.</summary>
    public string Zone => Spot?.Zone ?? string.Empty;
}

/// <summary>
/// Every Triple Triad opponent the sheets place on an NPC (1.21.0 P6), by resident row, with a reverse index from a
/// quest to the opponents it opens (the detail pane's Unlocks line). Built once per language by
/// <c>Tsukimichi.GameData.TriadReader</c>; immutable, lookups allocate nothing.
/// </summary>
public sealed class TriadOpponents
{
    /// <summary>No opponents: the sheets have not been read (or could not be).</summary>
    public static readonly TriadOpponents Empty = new([]);

    private static readonly IReadOnlyList<TriadOpponent> None = [];

    private readonly TriadOpponent[] all;
    private readonly FrozenDictionary<uint, TriadOpponent[]> byQuest;

    public TriadOpponents(IEnumerable<TriadOpponent> opponents)
    {
        ArgumentNullException.ThrowIfNull(opponents);
        all = opponents.Where(static o => o is not null).DistinctBy(static o => o.ResidentId).OrderBy(static o => o.ResidentId).ToArray();
        var quests = new Dictionary<uint, List<TriadOpponent>>();
        var cards = new SortedSet<uint>();
        foreach (var opponent in all)
        {
            foreach (var quest in opponent.Gate.QuestIds)
            {
                if (quest == 0)
                {
                    continue;
                }

                if (!quests.TryGetValue(quest, out var list))
                {
                    quests[quest] = list = [];
                }

                if (!list.Contains(opponent))
                {
                    list.Add(opponent);
                }
            }

            cards.UnionWith(opponent.Cards.Where(static c => c != 0));
        }

        byQuest = quests.ToFrozenDictionary(static kv => kv.Key, static kv => kv.Value.ToArray());
        Watched = all.Select(static o => o.ResidentId).ToArray();
        WatchedCards = [.. cards];
        Fingerprint = GateItemCapture.Fingerprint([.. Watched, .. WatchedCards]);
        QuestGatedCount = all.Count(static o => o.QuestGated);
    }

    /// <summary>Every opponent, by resident row id.</summary>
    public IReadOnlyList<TriadOpponent> All => all;

    public int Count => all.Length;

    /// <summary>Opponents the game keeps behind a quest (106 of 142 on the 2026.09.15 client).</summary>
    public int QuestGatedCount { get; }

    /// <summary>The resident row ids a capture reads the beaten flag of, ascending.</summary>
    public IReadOnlyList<uint> Watched { get; }

    /// <summary>The card row ids the opponents hand out, ascending and distinct: those a capture reads ownership of.</summary>
    public IReadOnlyList<uint> WatchedCards { get; }

    /// <summary>
    /// <see cref="GateItemCapture.Fingerprint"/> of <see cref="Watched"/> then <see cref="WatchedCards"/>: a capture made
    /// against another list (<see cref="TriadRecordCapture.Watch"/>) is not read.
    /// </summary>
    public uint Fingerprint { get; }

    /// <summary>The opponents whose gate names the quest (any join or all), by resident row id; empty for none.</summary>
    public IReadOnlyList<TriadOpponent> OpenedBy(uint questRowId) => byQuest.TryGetValue(questRowId, out var list) ? list : None;
}
