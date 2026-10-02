using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Core.Ipc;

/// <summary>
/// What the 1.8.0 gates read besides the catalog and the states, captured with the view on the framework thread. Every
/// member is immutable (or a fresh copy), so the view stays safe to read from any thread.
/// </summary>
public sealed record IpcExtras
{
    /// <summary>Nothing captured: the reward, duty, ladder and abandoned answers are empty.</summary>
    public static readonly IpcExtras None = new();

    /// <summary>The Moonlit rewards by item (the merged catalog, the player's overrides included).</summary>
    public RewardLookup Rewards { get; init; } = RewardLookup.Empty;

    /// <summary>Which quests unlock each Duty Finder entry.</summary>
    public DutyUnlockIndex DutyUnlocks { get; init; } = DutyUnlockIndex.Empty;

    /// <summary>The class and job quest lines; null without a catalog.</summary>
    public JobLadder? Ladder { get; init; }

    /// <summary>The logged-in character's abandoned quests, newest first.</summary>
    public IReadOnlyList<AbandonedEntry> Abandoned { get; init; } = [];

    /// <summary>The collectibles the logged-in character's last capture saved; null when it saved none.</summary>
    public CollectibleLookup? SavedCollectibles { get; init; }

    /// <summary>The character's best level on a job the quest admits (<see cref="RouteLevels.For"/>); null counts every level gate as met.</summary>
    public Func<QuestRecord, byte>? LevelOf { get; init; }
}

/// <summary>The 1.8.0 gates' answers (docs/ipc.md). Total like the rest of the view: nothing here throws for any input.</summary>
public sealed partial class IpcView
{
    /// <summary>
    /// How many quests may change state in one poll before <c>Tsukimichi.QuestStateChanged</c> stays silent for that
    /// poll (a daily reset, a level-up that opens dozens): <c>StatesChanged</c> alone then says "read again".
    /// </summary>
    public const int MaxStateChangesPerTick = 64;

    /// <summary>How many routes one view keeps (one view lives until the next session change).</summary>
    public const int MaxCachedRoutes = 256;

    private static readonly uint[] NoIds = [];
    private static readonly string[] NoStrings = [];
    private static readonly UniqueRewardEntry[] NoEntries = [];
    private static readonly ConditionalWeakTable<QuestCatalog, GateIndexes> Indexes = new();

    private readonly ConcurrentDictionary<uint, uint[]> routes = new();

    /// <summary>What the 1.8.0 gates read besides the states.</summary>
    public IpcExtras Extras { get; }

    /// <summary>
    /// <c>GetStates</c>: <see cref="State"/> for each id, in order (an empty string where there is no answer); empty
    /// for a null array.
    /// </summary>
    public string[] StatesOf(uint[]? ids)
    {
        if (ids is null || ids.Length == 0)
        {
            return NoStrings;
        }

        var result = new string[ids.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            result[i] = State(ids[i]);
        }

        return result;
    }

    /// <summary>
    /// <c>GetQuestsInState</c>: the row ids of every quest whose <see cref="QuestState"/> name is
    /// <paramref name="state"/> (exactly, as <c>GetState</c> spells it), in journal order. Empty for a name that is no
    /// state and when there is no answer.
    /// </summary>
    public uint[] QuestsInState(string? state)
    {
        if (!IsReady || !TryParseState(state, out var wanted))
        {
            return NoIds;
        }

        var ids = new List<uint>();
        foreach (var quest in Index!.JournalOrder)
        {
            if (States.TryGetValue(quest.RowId, out var evaluation) && evaluation?.State == wanted)
            {
                ids.Add(quest.RowId);
            }
        }

        return ids.Count == 0 ? NoIds : ids.ToArray();
    }

    /// <summary>
    /// <c>GetQuestsInZone</c>: the quests whose giver stands in the territory (TerritoryType row id), in journal order,
    /// removed quests left out; with <paramref name="readyOnly"/> only those <see cref="IsQuestAvailable"/> (Ready, or
    /// Ready on another job). Empty when there is no answer.
    /// </summary>
    public uint[] QuestsInZone(uint territoryId, bool readyOnly)
    {
        if (!IsReady || territoryId == 0 || !Index!.ByTerritory.TryGetValue(territoryId, out var all))
        {
            return NoIds;
        }

        if (!readyOnly)
        {
            return (uint[])all.Clone();
        }

        var ids = new List<uint>();
        foreach (var rowId in all)
        {
            if (States.TryGetValue(rowId, out var evaluation) && evaluation?.State is QuestState.Ready or QuestState.ReadyOnOtherJob)
            {
                ids.Add(rowId);
            }
        }

        return ids.Count == 0 ? NoIds : ids.ToArray();
    }

    /// <summary><c>GetFirstBlocker</c>: <see cref="IpcBlocker.Of"/> for the quest; <see cref="IpcBlocker.NoAnswer"/> when there is no answer.</summary>
    public IpcBlocker FirstBlocker(uint id) =>
        Catalog is { } catalog && TryEvaluate(id, out _, out var evaluation) ? IpcBlocker.Of(evaluation, catalog, States) : IpcBlocker.NoAnswer;

    /// <summary>
    /// <c>GetRoute</c>: the unlock route to the quest (<see cref="UnlockRoute"/>, the route window's), as the row ids of
    /// its steps in order, the target last. Empty when the quest is done or there is no answer. Built once per quest
    /// per view, that is per session change; a fresh array per call.
    /// </summary>
    public uint[] Route(uint id)
    {
        if (!IsReady || Find(id) is not { } quest)
        {
            return NoIds;
        }

        if (!routes.TryGetValue(quest.RowId, out var steps))
        {
            steps = BuildRoute(quest);
            if (routes.Count < MaxCachedRoutes)
            {
                routes.TryAdd(quest.RowId, steps);
            }
        }

        return steps.Length == 0 ? NoIds : (uint[])steps.Clone();
    }

    /// <summary>
    /// <c>GetQuestsForItem</c>: the quests that reward the item (a sheet reward or a Moonlit unlock item; HQ and
    /// collectable ids are folded to the base item), in journal order, removed quests left out. Data only: answers
    /// from the catalog alone, logged in or not.
    /// </summary>
    public uint[] QuestsForItem(uint itemId)
    {
        var item = RewardLookup.NormalizeItemId(itemId);
        if (Catalog is null || Index is not { } index || item == 0)
        {
            return NoIds;
        }

        var found = new HashSet<uint>();
        if (index.ByRewardItem.TryGetValue(item, out var fromSheet))
        {
            found.UnionWith(fromSheet);
        }

        foreach (var entry in Extras.Rewards.ByItem(item))
        {
            if (Catalog.GetByRowId(entry.QuestRowId) is { IsRemoved: false })
            {
                found.Add(entry.QuestRowId);
            }
        }

        return index.InJournalOrder(found);
    }

    /// <summary>
    /// <c>GetMoonlitStatus</c>: whether the item is a Moonlit reward (a reward only a quest gives), whether the
    /// logged-in character owns it, and how that is known: <c>"live"</c> (read from the game now), <c>"saved"</c>
    /// (the character's last capture), <c>"quest"</c> (the reward follows its quest's completion) or
    /// <c>"unknown"</c> (owned then reads false). <c>(false, false, "")</c> for an item that is no Moonlit reward or
    /// when there is no catalog.
    /// </summary>
    /// <param name="live">The game's own answer for an entry, or null when it cannot be read here (the plugin passes it only on the framework thread with the character logged in).</param>
    public (bool Unique, bool Owned, string Confidence) MoonlitStatus(uint itemId, Func<UniqueRewardEntry, bool?>? live = null)
    {
        IReadOnlyList<UniqueRewardEntry> entries = Catalog is null ? NoEntries : Extras.Rewards.ByItem(itemId);
        if (entries.Count == 0)
        {
            return (false, false, string.Empty);
        }

        (bool Owned, string Confidence)? best = null;
        foreach (var entry in entries)
        {
            if (Owned(entry, live) is not { } answer)
            {
                continue;
            }

            if (answer.Owned)
            {
                return (true, true, answer.Confidence);
            }

            best ??= answer;
        }

        return best is { } no ? (true, false, no.Confidence) : (true, false, MoonlitConfidence.Unknown);
    }

    /// <summary>
    /// <c>GetUnlockQuests</c>: the quests that unlock the Duty Finder entry (ContentFinderCondition row id), the
    /// Duty Finder hint's list, in journal order. Data only: answers from the catalog alone, logged in or not.
    /// </summary>
    public uint[] UnlockQuests(uint contentFinderConditionId)
    {
        if (Catalog is null || Index is not { } index || contentFinderConditionId == 0)
        {
            return NoIds;
        }

        var found = new HashSet<uint>();
        foreach (var quest in Extras.DutyUnlocks.Resolve(contentFinderConditionId, Catalog))
        {
            found.Add(quest.RowId);
        }

        return index.InJournalOrder(found);
    }

    /// <summary>
    /// <c>GetAbandoned</c>: the quests the logged-in character abandoned mid-way and has not taken up again, newest
    /// first, each with the step it was left at (0 when unknown) and when, as Unix seconds (UTC). Empty when there is
    /// no answer.
    /// </summary>
    public (uint RowId, byte Step, long AbandonedUnixSeconds)[] Abandoned()
    {
        if (!IsReady || Extras.Abandoned.Count == 0)
        {
            return [];
        }

        var result = new (uint, byte, long)[Extras.Abandoned.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var entry = Extras.Abandoned[i];
            var utc = entry.AbandonedUtc.Kind == DateTimeKind.Local ? entry.AbandonedUtc.ToUniversalTime() : DateTime.SpecifyKind(entry.AbandonedUtc, DateTimeKind.Utc);
            result[i] = (entry.RowId, entry.Sequence, new DateTimeOffset(utc).ToUnixTimeSeconds());
        }

        return result;
    }

    /// <summary>
    /// <c>GetNextJobQuest</c>: the first quest of the class's or job's quest line (ClassJob row id; a job's line starts
    /// with its class's quests) that is neither done nor locked out, the Characters tab's "next"; 0 once the line is
    /// done, for a ClassJob without quests and when there is no answer.
    /// </summary>
    public uint NextJobQuest(uint classJobId)
    {
        if (!IsReady || Extras.Ladder is not { } ladder || ladder.ForJob(classJobId) is not { } entry)
        {
            return 0;
        }

        return ladder.Progress(entry, States, 0).NextRowId ?? 0;
    }

    /// <summary>
    /// The per-quest changes <c>Tsukimichi.QuestStateChanged</c> announces: every quest whose <see cref="QuestState"/>
    /// differs between the two sets (a quest new to <paramref name="current"/> reads "" as its old state, one gone ""
    /// as its new one), ascending by row id. Null when there are more than <paramref name="max"/> (a flood: send none).
    /// The same instance never changes.
    /// </summary>
    public static IReadOnlyList<(uint RowId, string From, string To)>? StateChanges(
        IReadOnlyDictionary<uint, QuestEvaluation>? previous,
        IReadOnlyDictionary<uint, QuestEvaluation>? current,
        int max = MaxStateChangesPerTick)
    {
        previous ??= NoStates;
        current ??= NoStates;
        if (ReferenceEquals(previous, current))
        {
            return [];
        }

        var changes = new List<(uint, string, string)>();
        foreach (var (rowId, after) in current)
        {
            var from = previous.TryGetValue(rowId, out var before) && before is not null ? before.State.ToString() : string.Empty;
            var to = after?.State.ToString() ?? string.Empty;
            if (!string.Equals(from, to, StringComparison.Ordinal) && !Add(changes, (rowId, from, to), max))
            {
                return null;
            }
        }

        foreach (var (rowId, before) in previous)
        {
            if (!current.ContainsKey(rowId) && before is not null && !Add(changes, (rowId, before.State.ToString(), string.Empty), max))
            {
                return null;
            }
        }

        changes.Sort(static (a, b) => a.Item1.CompareTo(b.Item1));
        return changes;
    }

    /// <summary>A <see cref="QuestState"/> by its exact member name; a number or an unknown name is not one.</summary>
    public static bool TryParseState(string? name, out QuestState state)
    {
        state = default;
        return !string.IsNullOrEmpty(name) && char.IsLetter(name[0])
            && Enum.TryParse(name, ignoreCase: false, out state) && Enum.IsDefined(state);
    }

    private static bool Add(List<(uint, string, string)> changes, (uint, string, string) change, int max)
    {
        if (changes.Count >= max)
        {
            return false;
        }

        changes.Add(change);
        return true;
    }

    private GateIndexes? Index => Catalog is { } catalog ? Indexes.GetValue(catalog, static c => new GateIndexes(c)) : null;

    private uint[] BuildRoute(QuestRecord quest)
    {
        var route = UnlockRoute.Build(RouteTarget.ForQuest(quest.RowId, quest.Name), Catalog!, States, null, Extras.LevelOf);
        if (route.Steps.Count == 0)
        {
            return NoIds;
        }

        var ids = new uint[route.Steps.Count];
        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = route.Steps[i].RowId;
        }

        return ids;
    }

    /// <summary>One entry's owned answer and how it is known; null when nothing can tell.</summary>
    private (bool Owned, string Confidence)? Owned(UniqueRewardEntry entry, Func<UniqueRewardEntry, bool?>? live)
    {
        if (FollowsQuest(entry.Kind))
        {
            return States.TryGetValue(entry.QuestRowId, out var evaluation) && evaluation is not null
                ? (evaluation.State == QuestState.Completed, MoonlitConfidence.Quest)
                : null;
        }

        if (live?.Invoke(entry) is { } read)
        {
            return (read, MoonlitConfidence.Live);
        }

        return Extras.SavedCollectibles?.Owns(entry.Kind, entry.RewardId) is { } saved ? (saved, MoonlitConfidence.Saved) : null;
    }

    /// <summary>Rewards granted by turning the quest in, which the completion flag answers for (as the Moonlit tab reads them).</summary>
    private static bool FollowsQuest(RewardKind kind) =>
        kind is RewardKind.Action or RewardKind.GeneralAction or RewardKind.Trait or RewardKind.ClassJob or RewardKind.BlueMageSpell or RewardKind.SystemUnlock;

    /// <summary>Per-catalog lookups the gates share, built once per catalog on first use.</summary>
    private sealed class GateIndexes
    {
        public GateIndexes(QuestCatalog catalog)
        {
            var order = new List<QuestRecord>(catalog.All);
            order.Sort(static (a, b) =>
            {
                var byJournal = a.Journal.SortKey.CompareTo(b.Journal.SortKey);
                return byJournal != 0 ? byJournal : a.RowId.CompareTo(b.RowId);
            });
            JournalOrder = order.ToArray();

            var rank = new Dictionary<uint, int>(order.Count);
            var byTerritory = new Dictionary<uint, List<uint>>();
            var byItem = new Dictionary<uint, List<uint>>();
            for (var i = 0; i < order.Count; i++)
            {
                var quest = order[i];
                rank[quest.RowId] = i;
                if (quest.IsRemoved)
                {
                    continue;
                }

                if (quest.Issuer is { TerritoryId: not 0 } issuer)
                {
                    Add(byTerritory, issuer.TerritoryId, quest.RowId);
                }

                foreach (var reward in quest.Rewards)
                {
                    var item = reward.ItemId != 0 ? reward.ItemId
                        : reward.Kind is RewardKind.Item or RewardKind.OptionalItem ? reward.Id
                        : 0;
                    if (item != 0 && (!byItem.TryGetValue(item, out var list) || list[^1] != quest.RowId))
                    {
                        Add(byItem, item, quest.RowId);
                    }
                }
            }

            Rank = rank.ToFrozenDictionary();
            ByTerritory = byTerritory.ToFrozenDictionary(static kv => kv.Key, static kv => kv.Value.ToArray());
            ByRewardItem = byItem.ToFrozenDictionary(static kv => kv.Key, static kv => kv.Value.ToArray());
        }

        /// <summary>Every quest, in journal order (section, category, genre, then row id), as the export lists them.</summary>
        public QuestRecord[] JournalOrder { get; }

        public FrozenDictionary<uint, int> Rank { get; }

        public FrozenDictionary<uint, uint[]> ByTerritory { get; }

        public FrozenDictionary<uint, uint[]> ByRewardItem { get; }

        public uint[] InJournalOrder(HashSet<uint> rowIds)
        {
            if (rowIds.Count == 0)
            {
                return NoIds;
            }

            var ids = rowIds.ToArray();
            Array.Sort(ids, (a, b) => Rank.GetValueOrDefault(a, int.MaxValue).CompareTo(Rank.GetValueOrDefault(b, int.MaxValue)) is var c && c != 0 ? c : a.CompareTo(b));
            return ids;
        }

        private static void Add(Dictionary<uint, List<uint>> map, uint key, uint rowId)
        {
            if (!map.TryGetValue(key, out var list))
            {
                list = [];
                map[key] = list;
            }

            list.Add(rowId);
        }
    }
}

/// <summary>The confidence words of <c>Tsukimichi.GetMoonlitStatus</c> (docs/ipc.md). Stable; a later release may add one.</summary>
public static class MoonlitConfidence
{
    /// <summary>Read from the game now (the character is logged in on this client).</summary>
    public const string Live = "live";

    /// <summary>From the character's last capture (read off the framework thread, or not readable live).</summary>
    public const string Saved = "saved";

    /// <summary>The reward comes with its quest, so the quest's completion answers.</summary>
    public const string Quest = "quest";

    /// <summary>Nothing can tell (an item reward, or a capture from before 1.5); owned reads false.</summary>
    public const string Unknown = "unknown";
}
