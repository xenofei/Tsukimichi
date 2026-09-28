using System.Collections.Frozen;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Unique;

/// <summary>One line of the Moonlit pane: an entry and whether the viewed character has its reward; null means the plugin cannot tell.</summary>
public readonly record struct UniqueRewardRow(UniqueRewardEntry Entry, bool? Obtained);

/// <summary>Progress over a kind (or the whole view). <see cref="Total"/> counts every entry, <see cref="Unknown"/> included.</summary>
public readonly record struct UniqueRewardCounts(int Obtained, int Total, int Unknown);

/// <summary>A reward kind present in the unique view and how many entries it holds.</summary>
public readonly record struct UniqueRewardKind(RewardKind Kind, int Count);

/// <summary>
/// Every quest-exclusive reward the plugin knows about, merged from the shipped <c>unique_quests.json</c>, the curated
/// unlock files (in case the shipped file predates them) and the user's overrides. Immutable; rebuild when any input changes.
/// <para>
/// The <b>unique view</b> (<see cref="All"/>, <see cref="ByKind"/>, <see cref="Kinds"/>, <see cref="View"/>,
/// <see cref="Counts"/>) omits quests the user marked not unique; <see cref="ForQuest"/> still returns their entries so
/// the detail pane can show what the override hides, and <see cref="Hidden"/> lists them all so the Moonlit pane can
/// offer them back under its "Yours" filter. A quest the user marked unique that had no entry gains one of kind
/// <see cref="RewardKind.Other"/> at <see cref="Confidence.UserOverride"/>, named after the note.
/// Obtained state is not stored here: the caller answers it per entry through <see cref="View"/> and <see cref="Counts"/>.
/// </para>
/// </summary>
public sealed class UniqueRewardCatalog
{
    /// <summary><see cref="UniqueRewardEntry.Source"/> of entries created from a user override.</summary>
    public const string UserSource = "user";

    /// <summary>Name given to a user-added entry whose override carries no note.</summary>
    public const string DefaultUserRewardName = "Marked unique by you";

    /// <summary>Name given to the stand-in entry of a quest hidden by a "not unique" override although no source lists it.</summary>
    public const string DefaultHiddenRewardName = "Hidden by you";

    private static readonly UniqueRewardEntry[] NoEntries = [];

    public static readonly UniqueRewardCatalog Empty = new(
        NoEntries,
        FrozenDictionary<uint, IReadOnlyList<UniqueRewardEntry>>.Empty,
        FrozenDictionary<RewardKind, IReadOnlyList<UniqueRewardEntry>>.Empty,
        [],
        FrozenSet<uint>.Empty,
        NoEntries);

    private readonly UniqueRewardEntry[] all;
    private readonly FrozenDictionary<uint, IReadOnlyList<UniqueRewardEntry>> byQuest;
    private readonly FrozenSet<uint> uniqueQuests;

    private UniqueRewardCatalog(
        UniqueRewardEntry[] all,
        FrozenDictionary<uint, IReadOnlyList<UniqueRewardEntry>> byQuest,
        FrozenDictionary<RewardKind, IReadOnlyList<UniqueRewardEntry>> byKind,
        UniqueRewardKind[] kinds,
        FrozenSet<uint> uniqueQuests,
        UniqueRewardEntry[] hidden)
    {
        this.all = all;
        this.byQuest = byQuest;
        this.uniqueQuests = uniqueQuests;
        ByKind = byKind;
        Kinds = kinds;
        Hidden = hidden;
    }

    /// <summary>Number of entries in the unique view.</summary>
    public int Count => all.Length;

    /// <summary>Every entry in the unique view: shipped order, then curated additions, then user additions.</summary>
    public IReadOnlyList<UniqueRewardEntry> All => all;

    /// <summary>The unique view grouped by kind; only kinds with at least one entry have a key.</summary>
    public IReadOnlyDictionary<RewardKind, IReadOnlyList<UniqueRewardEntry>> ByKind { get; }

    /// <summary>Kinds present in the unique view with their entry counts, in <see cref="RewardKind"/> order.</summary>
    public IReadOnlyList<UniqueRewardKind> Kinds { get; }

    /// <summary>
    /// Entries kept out of the unique view by a "not unique" override, in source order (shipped, curated), one stand-in
    /// of kind <see cref="RewardKind.Other"/> per hidden quest no source lists. Never overlaps <see cref="All"/>.
    /// </summary>
    public IReadOnlyList<UniqueRewardEntry> Hidden { get; }

    /// <summary>Entries of one kind in the unique view; empty when none.</summary>
    public IReadOnlyList<UniqueRewardEntry> Entries(RewardKind kind) => ByKind.GetValueOrDefault(kind) ?? NoEntries;

    /// <summary>Every entry for a quest (by row id), including those an override hides from the unique view.</summary>
    public IReadOnlyList<UniqueRewardEntry> ForQuest(uint rowId) => byQuest.GetValueOrDefault(rowId) ?? NoEntries;

    /// <summary>Whether the quest has at least one entry in the unique view.</summary>
    public bool IsUnique(uint rowId) => uniqueQuests.Contains(rowId);

    /// <summary>Rows of the unique view for one kind, or every kind when <paramref name="kind"/> is null, each paired with the caller's verdict.</summary>
    /// <param name="isObtained">Whether the viewed character has the reward: true, false, or null when it cannot be told.</param>
    public IReadOnlyList<UniqueRewardRow> View(RewardKind? kind, Func<UniqueRewardEntry, bool?> isObtained)
    {
        ArgumentNullException.ThrowIfNull(isObtained);
        var entries = kind is { } k ? Entries(k) : all;
        if (entries.Count == 0)
        {
            return [];
        }

        var rows = new UniqueRewardRow[entries.Count];
        for (var i = 0; i < rows.Length; i++)
        {
            rows[i] = new UniqueRewardRow(entries[i], isObtained(entries[i]));
        }

        return rows;
    }

    /// <summary>Obtained, total and unknown counts over one kind, or the whole unique view when <paramref name="kind"/> is null.</summary>
    public UniqueRewardCounts Counts(RewardKind? kind, Func<UniqueRewardEntry, bool?> isObtained)
    {
        ArgumentNullException.ThrowIfNull(isObtained);
        var entries = kind is { } k ? Entries(k) : all;
        var obtained = 0;
        var unknown = 0;
        foreach (var entry in entries)
        {
            switch (isObtained(entry))
            {
                case true:
                    obtained++;
                    break;
                case null:
                    unknown++;
                    break;
            }
        }

        return new UniqueRewardCounts(obtained, entries.Count, unknown);
    }

    /// <summary>
    /// Merges the three sources. Entries with the same quest, kind and reward id collapse to one, the higher
    /// <see cref="Confidence"/> winning in the position of the first; curated unlocks the shipped file already carries
    /// are therefore not duplicated. Shipped entries listed in <see cref="CuratedData.OnlineStore"/> gain
    /// <see cref="OtherSource.OnlineStore"/> when the shipped file does not carry it yet.
    /// </summary>
    public static UniqueRewardCatalog Build(
        UniqueRewardsData data,
        IReadOnlyDictionary<uint, UniqueOverride> overrides,
        CuratedData curated)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(curated);

        // curated/online_store.json marks shipped entries the Online Store also sells, by store item or by the
        // collectible it unlocks, in case the shipped file predates the curated entry.
        var storeItems = curated.OnlineStore;
        var storeRewards = new HashSet<(RewardKind Kind, uint RewardId)>();
        foreach (var store in storeItems.Values)
        {
            storeRewards.Add((store.Kind, store.RewardId));
        }

        var merged = new Merger();
        foreach (var entry in data.Entries)
        {
            var sold = storeItems.Count != 0
                       && ((entry.ItemId != 0 && storeItems.ContainsKey(entry.ItemId)) || storeRewards.Contains((entry.Kind, entry.RewardId)));
            merged.Add(sold ? entry.WithOtherSource(OtherSource.OnlineStore) : entry);
        }

        foreach (var (rowId, unlock) in curated.SystemUnlocks)
        {
            merged.Add(new UniqueRewardEntry(rowId, RewardKind.SystemUnlock, 0, 0, unlock.Label, Confidence.Curated, "curated/" + CuratedData.SystemUnlocksFileName));
        }

        foreach (var (rowId, unlock) in curated.DutyUnlocks)
        {
            foreach (var cfc in unlock.ContentFinderConditionIds)
            {
                merged.Add(new UniqueRewardEntry(rowId, RewardKind.DutyUnlock, cfc, 0, unlock.Note ?? string.Empty, Confidence.Curated, "curated/" + CuratedData.DutyUnlocksFileName));
            }
        }

        var hidden = new HashSet<uint>();
        var hiddenEntries = new List<UniqueRewardEntry>();
        foreach (var (rowId, verdict) in overrides)
        {
            if (!verdict.Unique)
            {
                hidden.Add(rowId);
                if (!merged.HasQuest(rowId))
                {
                    var name = string.IsNullOrWhiteSpace(verdict.Note) ? DefaultHiddenRewardName : verdict.Note.Trim();
                    hiddenEntries.Add(new UniqueRewardEntry(rowId, RewardKind.Other, 0, 0, name, Confidence.UserOverride, UserSource));
                }
            }
            else if (!merged.HasQuest(rowId))
            {
                var name = string.IsNullOrWhiteSpace(verdict.Note) ? DefaultUserRewardName : verdict.Note.Trim();
                merged.Add(new UniqueRewardEntry(rowId, RewardKind.Other, 0, 0, name, Confidence.UserOverride, UserSource));
            }
        }

        var entries = merged.Entries;
        if (entries.Count == 0 && hiddenEntries.Count == 0)
        {
            return Empty;
        }

        var all = new List<UniqueRewardEntry>(entries.Count);
        var byQuest = new Dictionary<uint, List<UniqueRewardEntry>>();
        var byKind = new SortedDictionary<RewardKind, List<UniqueRewardEntry>>();
        var uniqueQuests = new HashSet<uint>();
        foreach (var entry in entries)
        {
            Append(byQuest, entry.QuestRowId, entry);
            if (hidden.Contains(entry.QuestRowId))
            {
                hiddenEntries.Add(entry);
                continue;
            }

            all.Add(entry);
            uniqueQuests.Add(entry.QuestRowId);
            if (!byKind.TryGetValue(entry.Kind, out var ofKind))
            {
                ofKind = [];
                byKind[entry.Kind] = ofKind;
            }

            ofKind.Add(entry);
        }

        var kinds = new UniqueRewardKind[byKind.Count];
        var i = 0;
        foreach (var (kind, ofKind) in byKind)
        {
            kinds[i++] = new UniqueRewardKind(kind, ofKind.Count);
        }

        return new UniqueRewardCatalog(
            all.ToArray(),
            byQuest.ToFrozenDictionary(kv => kv.Key, kv => (IReadOnlyList<UniqueRewardEntry>)kv.Value.ToArray()),
            byKind.ToFrozenDictionary(kv => kv.Key, kv => (IReadOnlyList<UniqueRewardEntry>)kv.Value.ToArray()),
            kinds,
            uniqueQuests.ToFrozenSet(),
            hiddenEntries.ToArray());
    }

    private static void Append(Dictionary<uint, List<UniqueRewardEntry>> map, uint key, UniqueRewardEntry entry)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        list.Add(entry);
    }

    /// <summary>Insertion-ordered set keyed by (quest, kind, reward id) where a later entry replaces an earlier one only when more confident.</summary>
    private sealed class Merger
    {
        private readonly List<UniqueRewardEntry> entries = [];
        private readonly Dictionary<(uint Quest, RewardKind Kind, uint RewardId), int> positions = [];
        private readonly HashSet<uint> quests = [];

        public IReadOnlyList<UniqueRewardEntry> Entries => entries;

        public bool HasQuest(uint rowId) => quests.Contains(rowId);

        public void Add(UniqueRewardEntry entry)
        {
            var key = (entry.QuestRowId, entry.Kind, entry.RewardId);
            if (positions.TryGetValue(key, out var index))
            {
                var existing = entries[index];
                if (entry.Confidence > existing.Confidence)
                {
                    // A more confident claim wins, but a nameless one (a bare curated id) keeps the name already known.
                    entries[index] = string.IsNullOrWhiteSpace(entry.RewardName) && !string.IsNullOrWhiteSpace(existing.RewardName)
                        ? entry with { RewardName = existing.RewardName }
                        : entry;
                }

                return;
            }

            positions[key] = entries.Count;
            entries.Add(entry);
            quests.Add(entry.QuestRowId);
        }
    }
}
