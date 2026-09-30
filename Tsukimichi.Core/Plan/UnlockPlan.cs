using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Plan;

/// <summary>One quest of the plan with its state for the character.</summary>
/// <param name="Name">The name as the spoiler shield prints it (<see cref="BlockerNames.QuestName"/>).</param>
/// <param name="StatusText">The Status cell: the state word, then the decisive blocker (<see cref="BlockerText.StatusText"/>).</param>
/// <param name="Unlocks">What the quest unlocks, primary kind first (<see cref="UnlockTags.For"/>); never empty.</param>
public sealed record PlanEntry(QuestRecord Quest, string Name, QuestState State, string StatusText, IReadOnlyList<PlanUnlock> Unlocks)
{
    /// <summary>The first kind by precedence: the pill drawn first.</summary>
    public UnlockKind PrimaryKind => Unlocks.Count > 0 ? Unlocks[0].Kind : UnlockKind.Other;

    /// <summary>Every kind the quest carries as a <see cref="UnlockKinds.Bit"/> mask.</summary>
    public ushort KindMask
    {
        get
        {
            ushort mask = 0;
            foreach (var unlock in Unlocks)
            {
                mask |= UnlockKinds.Bit(unlock.Kind);
            }

            return mask;
        }
    }

    /// <summary>Can be started now, on this job or another.</summary>
    public bool IsReady => State is QuestState.Ready or QuestState.ReadyOnOtherJob;
}

/// <summary>The plan quests one zone's givers hand out, in story order.</summary>
/// <param name="TerritoryId">The givers' TerritoryType row id; 0 for quests without a known giver.</param>
/// <param name="MapId">The first entry's giver's Map row id, for the zone's name; 0 when unknown.</param>
public sealed record PlanZone(uint TerritoryId, uint MapId, IReadOnlyList<PlanEntry> Entries);

/// <summary>One expansion's zones, in story order.</summary>
/// <param name="Expansion">ExVersion row id (0 = A Realm Reborn).</param>
/// <param name="Name">The expansion's name (<see cref="BlockerNames.Expansion"/>).</param>
public sealed record PlanExpansion(byte Expansion, string Name, IReadOnlyList<PlanZone> Zones)
{
    /// <summary>Quests in the expansion.</summary>
    public int Count { get; } = Zones.Sum(static z => z.Entries.Count);

    /// <summary>Quests that can be started now.</summary>
    public int ReadyCount { get; } = Zones.Sum(static z => z.Entries.Count(static e => e.IsReady));

    /// <summary>Every entry, zone by zone.</summary>
    public IEnumerable<PlanEntry> Entries => Zones.SelectMany(static z => z.Entries);
}

/// <summary>What the plan view shows.</summary>
/// <param name="Kinds">Kinds to keep (<see cref="UnlockKinds.Mask"/>); an entry stays when any of its kinds is in the mask.</param>
/// <param name="ReadyOnly">Keep only quests that can be started now (Ready, or Ready on another job).</param>
/// <param name="MaxExpansion">Keep expansions at or below this one (Sprout mode's reach); null keeps every one.</param>
public sealed record PlanFilter(ushort Kinds = UnlockKinds.AllMask, bool ReadyOnly = false, byte? MaxExpansion = null)
{
    public static readonly PlanFilter None = new();

    public bool Keeps(PlanEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return (entry.KindMask & Kinds) != 0
               && (!ReadyOnly || entry.IsReady)
               && (MaxExpansion is not { } max || entry.Quest.Expansion <= max);
    }
}

/// <summary>
/// "Clear my blues" (P3): every unlock quest a character has not done yet, grouped by expansion, then by zone, both in
/// story order, each with its state, its status line and what it unlocks. A quest is left out once it is Completed
/// (or done this cycle), when it is Locked out (Foreclosed: a sibling quest or another Grand Company took its place,
/// nothing can be done about it), and whenever <see cref="UnlockTags"/> does not list it (removed from the game,
/// seasonal). A character without states (browse mode) sees every plan quest as Not checked.
/// <para>
/// Story order: expansions by ExVersion. Within an expansion the zone is the giver's territory, and zones are ordered
/// by the lowest level among their quests, the journal position of that quest breaking ties; within a zone quests go
/// by level, then journal position. Level leads rather than the journal alone because the journal ranks whole genres
/// ahead of the zone sidequests (A Realm Reborn's Primal and Bahamut quests, all level 50, come first), so journal
/// order alone would open the plan with endgame content. <see cref="Filter"/> regroups what it keeps under the same
/// rules, so a filtered plan's first zone is the one whose remaining quests start lowest.
/// </para>
/// Immutable; build once per session version.
/// </summary>
public sealed class UnlockPlan
{
    public static readonly UnlockPlan Empty = new([], []);

    private readonly PlanEntry[] entries;

    private UnlockPlan(PlanEntry[] entries, PlanExpansion[] expansions)
    {
        this.entries = entries;
        Expansions = expansions;
        ReadyCount = entries.Count(static e => e.IsReady);
    }

    /// <summary>Expansions with at least one quest, in order.</summary>
    public IReadOnlyList<PlanExpansion> Expansions { get; }

    /// <summary>Every entry in plan order.</summary>
    public IReadOnlyList<PlanEntry> Entries => entries;

    public int Count => entries.Length;

    /// <summary>Entries that can be started now.</summary>
    public int ReadyCount { get; }

    public bool IsEmpty => entries.Length == 0;

    /// <summary>The expansion's block, or null when the plan has nothing left there.</summary>
    public PlanExpansion? Expansion(byte expansion)
    {
        foreach (var block in Expansions)
        {
            if (block.Expansion == expansion)
            {
                return block;
            }
        }

        return null;
    }

    /// <summary>Whether a character's state keeps a quest in the plan.</summary>
    public static bool IsLeft(QuestState state) => state is not (QuestState.Completed or QuestState.DoneThisCycle or QuestState.Foreclosed);

    /// <param name="tags">The plan quests and their unlocks for the catalog.</param>
    /// <param name="states">The character's evaluations by quest row id; empty reads every quest as Not checked.</param>
    /// <param name="names">Names for the quest (spoiler-aware), its status line and the expansion headers.</param>
    public static UnlockPlan Build(UnlockTags tags, IReadOnlyDictionary<uint, QuestEvaluation> states, BlockerNames names)
    {
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(names);

        var list = new List<PlanEntry>(tags.Count);
        foreach (var quest in tags.Quests)
        {
            states.TryGetValue(quest.RowId, out var evaluation);
            var state = evaluation?.State ?? QuestState.Unknown;
            if (!IsLeft(state))
            {
                continue;
            }

            list.Add(new PlanEntry(quest, names.QuestName(quest), state, BlockerText.StatusText(evaluation, quest, names, states), tags.For(quest.RowId)));
        }

        return Group(list, names.Expansion);
    }

    /// <summary>The entries <paramref name="filter"/> keeps, regrouped in story order.</summary>
    public UnlockPlan Filter(PlanFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter == PlanFilter.None)
        {
            return this;
        }

        var kept = new List<PlanEntry>(entries.Length);
        foreach (var entry in entries)
        {
            if (filter.Keeps(entry))
            {
                kept.Add(entry);
            }
        }

        if (kept.Count == entries.Length)
        {
            return this;
        }

        var names = new Dictionary<byte, string>();
        foreach (var block in Expansions)
        {
            names[block.Expansion] = block.Name;
        }

        return Group(kept, expansion => names.GetValueOrDefault(expansion, Evaluation.Expansions.Name(expansion)));
    }

    private static UnlockPlan Group(List<PlanEntry> list, Func<byte, string> expansionName)
    {
        if (list.Count == 0)
        {
            return Empty;
        }

        var expansions = new List<PlanExpansion>();
        var ordered = new List<PlanEntry>(list.Count);
        foreach (var byExpansion in list.GroupBy(static e => e.Quest.Expansion).OrderBy(static g => g.Key))
        {
            var zones = new List<PlanZone>();
            foreach (var byZone in byExpansion
                         .GroupBy(static e => e.Quest.Issuer?.TerritoryId ?? 0u)
                         .Select(static g => g.OrderBy(static e => e.Quest.DisplayLevel).ThenBy(static e => e.Quest.Journal.SortKey).ThenBy(static e => e.Quest.RowId).ToArray())
                         .OrderBy(static z => z[0].Quest.DisplayLevel)
                         .ThenBy(static z => z[0].Quest.Journal.SortKey)
                         .ThenBy(static z => z[0].Quest.Issuer?.TerritoryId ?? 0u))
            {
                var first = byZone[0].Quest.Issuer;
                zones.Add(new PlanZone(first?.TerritoryId ?? 0, first?.MapId ?? 0, byZone));
                ordered.AddRange(byZone);
            }

            expansions.Add(new PlanExpansion(byExpansion.Key, expansionName(byExpansion.Key), zones));
        }

        return new UnlockPlan(ordered.ToArray(), expansions.ToArray());
    }
}
