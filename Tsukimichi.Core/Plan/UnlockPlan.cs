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

    /// <summary>How soon it is worth doing (feature plan v7 P4, <see cref="UnlockTiers.Classify"/>); Systems when the plan was built without tiers.</summary>
    public UnlockTier Tier { get; init; } = UnlockTier.Systems;

    /// <summary>
    /// The player set it aside for this character (P4 "Set aside", <c>user/characters.json</c>): it leaves every count.
    /// Only a plan's <see cref="UnlockPlan.SetAside"/> lists such entries, and a view that keeps one in place
    /// (<see cref="UnlockPlan.Filter(PlanFilter, IReadOnlySet{uint})"/>) until it is rebuilt.
    /// </summary>
    public bool IsSetAside { get; init; }
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
    /// <summary>Quests in the expansion; set-aside entries a view keeps in place never count.</summary>
    public int Count { get; } = Zones.Sum(static z => z.Entries.Count(static e => !e.IsSetAside));

    /// <summary>Quests that can be started now (set-aside entries left out).</summary>
    public int ReadyCount { get; } = Zones.Sum(static z => z.Entries.Count(static e => e.IsReady && !e.IsSetAside));

    /// <summary>Set-aside entries the block lists (the Set aside view's own count).</summary>
    public int SetAsideCount { get; } = Zones.Sum(static z => z.Entries.Count(static e => e.IsSetAside));

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
/// <para>
/// Region order (1.6.0): given each zone's region (<c>regionOf</c>, the map's PlaceNameRegion: La Noscea, Thanalan,
/// Coerthas…), zones whose lowest level falls in one <see cref="LevelBand"/>-level band are walked region by region,
/// so the plan does not criss-cross the map: within a band a region comes where its first zone would, and its other
/// zones follow it at once, in the order above. Zones without a known region stand alone, so without regions the
/// order is exactly the level order above.
/// </para>
/// Immutable; build once per session version.
/// </summary>
public sealed class UnlockPlan
{
    public static readonly UnlockPlan Empty = new([], [], null, [], 0, Evaluation.Expansions.Name);

    /// <summary>Levels per band within which zones are ordered by region (see the class summary).</summary>
    public const int LevelBand = 10;

    private readonly PlanEntry[] entries;
    private readonly Func<uint, string>? regionOf;
    private readonly Func<byte, string> expansionName;

    // The entries of the other side (the set-aside ones for the plan, the active ones for its Set aside view): a view
    // may keep one of them in place, by id, until it is rebuilt.
    private readonly PlanEntry[] others;
    private UnlockPlan? setAsideView;
    private IReadOnlyList<PlanTierGroup>? tierGroups;

    private UnlockPlan(PlanEntry[] entries, PlanExpansion[] expansions, Func<uint, string>? regionOf, PlanEntry[] others, int setAsideCount, Func<byte, string> expansionName)
    {
        this.entries = entries;
        this.regionOf = regionOf;
        this.others = others;
        this.expansionName = expansionName;
        Expansions = expansions;
        Count = entries.Count(static e => !e.IsSetAside);
        ReadyCount = entries.Count(static e => e.IsReady && !e.IsSetAside);
        SetAsideCount = setAsideCount;
    }

    /// <summary>Expansions with at least one quest, in order.</summary>
    public IReadOnlyList<PlanExpansion> Expansions { get; }

    /// <summary>Every entry in plan order.</summary>
    public IReadOnlyList<PlanEntry> Entries => entries;

    /// <summary>Quests left; a set-aside entry a view keeps in place never counts.</summary>
    public int Count { get; }

    /// <summary>Entries that can be started now (set-aside ones never count).</summary>
    public int ReadyCount { get; }

    /// <summary>Quests the player set aside for the character (P4), out of every other count.</summary>
    public int SetAsideCount { get; }

    public bool IsEmpty => entries.Length == 0;

    /// <summary>
    /// The quests set aside, grouped as the plan is (the Set aside filter's list, with Bring back on each row). Its own
    /// <see cref="Filter(PlanFilter, IReadOnlySet{uint})"/> can keep a quest just brought back in place. Ask it of the
    /// plan <see cref="Build"/> returned (or a filter of it), not of a Set aside view.
    /// </summary>
    public UnlockPlan SetAside => setAsideView ??= Group(
        [.. others.Where(static e => e.IsSetAside)],
        expansionName,
        regionOf,
        [.. entries.Where(static e => !e.IsSetAside)],
        SetAsideCount);

    /// <summary>
    /// The entries by tier for the Do first sort (P4): one group per tier that has any, in tier order
    /// (<see cref="UnlockTiers.All"/>), each in plan order.
    /// </summary>
    public IReadOnlyList<PlanTierGroup> TierGroups => tierGroups ??= BuildTierGroups(entries);

    private static PlanTierGroup[] BuildTierGroups(PlanEntry[] entries)
    {
        var groups = new List<PlanTierGroup>(UnlockTiers.All.Length);
        foreach (var tier in UnlockTiers.All)
        {
            var list = entries.Where(e => e.Tier == tier).ToArray();
            if (list.Length > 0)
            {
                groups.Add(new PlanTierGroup(tier, list));
            }
        }

        return [.. groups];
    }

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
    /// <param name="regionOf">A zone's region by its giver's Map row id (empty when unknown); null orders zones by level alone.</param>
    /// <param name="spoilers">
    /// The wider spoiler shield (1.20.0 N6): a duty, place or feature the story has not introduced is named by its
    /// placeholder in the pills, tooltips and copies; null names all.
    /// </param>
    /// <param name="tierOf">Each quest's P4 tier (<see cref="UnlockTiers.Classify"/>); null leaves every entry Systems.</param>
    /// <param name="setAside">The quests the player set aside for the character (P4): kept out of the entries and every count, listed by <see cref="SetAside"/>.</param>
    public static UnlockPlan Build(
        UnlockTags tags,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        BlockerNames names,
        Func<uint, string>? regionOf = null,
        Query.SpoilerMask? spoilers = null,
        Func<QuestRecord, IReadOnlyList<PlanUnlock>, UnlockTier>? tierOf = null,
        IReadOnlySet<uint>? setAside = null)
    {
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(names);

        var list = new List<PlanEntry>(tags.Count);
        var aside = new List<PlanEntry>();
        foreach (var quest in tags.Quests)
        {
            states.TryGetValue(quest.RowId, out var evaluation);
            var state = evaluation?.State ?? QuestState.Unknown;
            if (!IsLeft(state))
            {
                continue;
            }

            var unlocks = tags.For(quest.RowId);
            var isAside = setAside?.Contains(quest.RowId) == true;
            var entry = new PlanEntry(quest, names.QuestName(quest), state, BlockerText.StatusText(evaluation, quest, names, states), Shield(unlocks, spoilers))
            {
                Tier = tierOf?.Invoke(quest, unlocks) ?? UnlockTier.Systems,
                IsSetAside = isAside,
            };
            (isAside ? aside : list).Add(entry);
        }

        return Group(list, names.Expansion, regionOf, [.. aside], aside.Count);
    }

    /// <summary>The unlocks with each name the shield masks replaced by its placeholder; the list itself when none is.</summary>
    private static IReadOnlyList<PlanUnlock> Shield(IReadOnlyList<PlanUnlock> unlocks, Query.SpoilerMask? spoilers)
    {
        if (spoilers is not { MasksNames: true })
        {
            return unlocks;
        }

        PlanUnlock[]? shielded = null;
        for (var i = 0; i < unlocks.Count; i++)
        {
            var unlock = unlocks[i];
            var kind = KindOf(unlock.Kind);
            if (unlock.Name.Length == 0 || !spoilers.IsNameMasked(kind, unlock.Name))
            {
                continue;
            }

            shielded ??= [.. unlocks];
            shielded[i] = unlock with { Name = spoilers.Name(kind, unlock.Name) };
        }

        return shielded ?? unlocks;
    }

    /// <summary>The kind of name the shield places a plan unlock by: a duty, flying in a zone (an area), anything else a reward.</summary>
    private static Query.SpoilerKind KindOf(UnlockKind kind) => kind switch
    {
        UnlockKind.Dungeon or UnlockKind.Trial or UnlockKind.NormalRaid or UnlockKind.AllianceRaid or UnlockKind.FieldOperation => Query.SpoilerKind.Duty,
        UnlockKind.Flying => Query.SpoilerKind.Area,
        _ => Query.SpoilerKind.Reward,
    };

    /// <summary>The entries <paramref name="filter"/> keeps, regrouped in story order.</summary>
    public UnlockPlan Filter(PlanFilter filter) => Filter(filter, null);

    /// <summary>
    /// The entries <paramref name="filter"/> keeps, regrouped in story order, plus the entries of the other side whose
    /// ids <paramref name="keepInPlace"/> names (P4: a quest just set aside, or just brought back, keeps its row where
    /// it was until the view is rebuilt). Grouping is by story order alone, so such an entry lands exactly where it
    /// stood; it never counts (<see cref="PlanEntry.IsSetAside"/> on the plan, the reverse on its Set aside view, whose
    /// counts are <see cref="PlanExpansion.SetAsideCount"/>).
    /// </summary>
    public UnlockPlan Filter(PlanFilter filter, IReadOnlySet<uint>? keepInPlace)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var linger = keepInPlace is { Count: > 0 } && others.Length > 0;
        if (filter == PlanFilter.None && !linger)
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

        var added = false;
        if (linger)
        {
            foreach (var entry in others)
            {
                if (keepInPlace!.Contains(entry.Quest.RowId) && filter.Keeps(entry))
                {
                    kept.Add(entry);
                    added = true;
                }
            }
        }

        if (kept.Count == entries.Length && !added)
        {
            return this;
        }

        return Group(kept, expansionName, regionOf, others, SetAsideCount);
    }

    private static UnlockPlan Group(List<PlanEntry> list, Func<byte, string> expansionName, Func<uint, string>? regionOf, PlanEntry[] others, int setAsideCount)
    {
        if (list.Count == 0)
        {
            return others.Length == 0 && setAsideCount == 0 ? Empty : new UnlockPlan([], [], regionOf, others, setAsideCount, expansionName);
        }

        var expansions = new List<PlanExpansion>();
        var ordered = new List<PlanEntry>(list.Count);
        foreach (var byExpansion in list.GroupBy(static e => e.Quest.Expansion).OrderBy(static g => g.Key))
        {
            var byLevel = byExpansion
                .GroupBy(static e => e.Quest.Issuer?.TerritoryId ?? 0u)
                .Select(static g => g.OrderBy(static e => e.Quest.DisplayLevel).ThenBy(static e => e.Quest.Journal.SortKey).ThenBy(static e => e.Quest.RowId).ToArray())
                .OrderBy(static z => z[0].Quest.DisplayLevel)
                .ThenBy(static z => z[0].Quest.Journal.SortKey)
                .ThenBy(static z => z[0].Quest.Issuer?.TerritoryId ?? 0u)
                .ToList();
            var zones = new List<PlanZone>();
            foreach (var byZone in OrderByRegion(byLevel, regionOf))
            {
                var first = byZone[0].Quest.Issuer;
                zones.Add(new PlanZone(first?.TerritoryId ?? 0, first?.MapId ?? 0, byZone));
                ordered.AddRange(byZone);
            }

            expansions.Add(new PlanExpansion(byExpansion.Key, expansionName(byExpansion.Key), zones));
        }

        return new UnlockPlan(ordered.ToArray(), expansions.ToArray(), regionOf, others, setAsideCount, expansionName);
    }

    /// <summary>
    /// The zones (already in level order) walked band by band and, within a band, region by region: a region comes
    /// where its first zone is, its other zones of the band right after it. A zone without a region stands alone.
    /// </summary>
    private static List<PlanEntry[]> OrderByRegion(List<PlanEntry[]> zones, Func<uint, string>? regionOf)
    {
        if (regionOf is null || zones.Count < 3)
        {
            return zones;
        }

        var result = new List<PlanEntry[]>(zones.Count);
        var i = 0;
        while (i < zones.Count)
        {
            var band = zones[i][0].Quest.DisplayLevel / LevelBand;
            var end = i;
            while (end < zones.Count && zones[end][0].Quest.DisplayLevel / LevelBand == band)
            {
                end++;
            }

            var taken = new bool[end - i];
            for (var a = i; a < end; a++)
            {
                if (taken[a - i])
                {
                    continue;
                }

                taken[a - i] = true;
                result.Add(zones[a]);
                var region = RegionOf(zones[a], regionOf);
                if (region.Length == 0)
                {
                    continue;
                }

                for (var b = a + 1; b < end; b++)
                {
                    if (!taken[b - i] && string.Equals(RegionOf(zones[b], regionOf), region, StringComparison.Ordinal))
                    {
                        taken[b - i] = true;
                        result.Add(zones[b]);
                    }
                }
            }

            i = end;
        }

        return result;
    }

    private static string RegionOf(PlanEntry[] zone, Func<uint, string> regionOf) =>
        zone[0].Quest.Issuer is { MapId: > 0 } issuer ? regionOf(issuer.MapId) ?? string.Empty : string.Empty;
}

/// <summary>One P4 tier's entries, in plan order (<see cref="UnlockPlan.TierGroups"/>).</summary>
public sealed record PlanTierGroup(UnlockTier Tier, IReadOnlyList<PlanEntry> Entries)
{
    /// <summary>Quests left in the tier (set-aside entries kept in place never count).</summary>
    public int Count { get; } = Entries.Count(static e => !e.IsSetAside);

    /// <summary>Quests of the tier that can be started now.</summary>
    public int ReadyCount { get; } = Entries.Count(static e => e.IsReady && !e.IsSetAside);
}
