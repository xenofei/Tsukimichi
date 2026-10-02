using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Unique;

/// <summary>
/// What makes two Moonlit entries the same reward: the kind with the reward id, else the item, else (system unlocks and
/// other unnamed-id rewards) the name. A user-added entry is its own reward, whatever its note says.
/// </summary>
public readonly record struct RewardKey(RewardKind Kind, uint RewardId, uint ItemId, string Name)
{
    /// <summary>The key of <paramref name="entry"/>.</summary>
    public static RewardKey Of(UniqueRewardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.RewardId != 0)
        {
            return new RewardKey(entry.Kind, entry.RewardId, 0, string.Empty);
        }

        if (entry.ItemId != 0)
        {
            return new RewardKey(entry.Kind, 0, entry.ItemId, string.Empty);
        }

        if (string.Equals(entry.Source, UniqueRewardCatalog.UserSource, StringComparison.Ordinal))
        {
            return new RewardKey(entry.Kind, 0, 0, "\0" + entry.QuestRowId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return new RewardKey(entry.Kind, 0, 0, entry.RewardName.Trim().ToUpperInvariant());
    }
}

/// <summary>
/// One counted Moonlit reward (feature plan v5, decision 4): the entries of every quest that gives the same reward
/// (Guildhests from each city's quest, a class unlocked from three quests, an achievement nine quests award), or, for
/// a <see cref="IsChoice">choice set</see>, every per-job item of one relic or special weapon quest, of which a
/// character receives one, with the repeatable "another job" quest that hands out the same items.
/// </summary>
public sealed class MoonlitGroup
{
    internal MoonlitGroup(int index, UniqueRewardEntry[] entries, uint[] quests, int choices)
    {
        Index = index;
        Entries = entries;
        Quests = quests;
        Choices = choices;
        foreach (var entry in entries)
        {
            if (entry.SoldOnOnlineStore || entry.DropsInDuty)
            {
                FoundElsewhere = true;
                break;
            }
        }
    }

    /// <summary>Position in <see cref="MoonlitGroups.All"/>.</summary>
    public int Index { get; }

    /// <summary>Every entry the group counts once, in catalog order; never empty.</summary>
    public IReadOnlyList<UniqueRewardEntry> Entries { get; }

    /// <summary>The group's quests (row ids), each once: the first is the home quest (a choice set's quest that is not repeatable), the rest in catalog order.</summary>
    public IReadOnlyList<uint> Quests { get; }

    /// <summary>For a choice set, how many items it offers ("1 of 18"); 0 for any other group.</summary>
    public int Choices { get; }

    /// <summary>A relic or special weapon quest whose per-job items count as one reward.</summary>
    public bool IsChoice => Choices > 0;

    /// <summary>The first entry: its kind and its name stand for the group.</summary>
    public UniqueRewardEntry Primary => Entries[0];

    public RewardKind Kind => Primary.Kind;

    /// <summary>Some entry is sold on the Online Store or drops in a duty (what "Hide rewards found elsewhere" leaves out).</summary>
    public bool FoundElsewhere { get; }

    /// <summary>The first entry of <paramref name="quest"/> in the group; null when the quest is not one of the group's.</summary>
    public UniqueRewardEntry? FirstOf(uint quest)
    {
        foreach (var entry in Entries)
        {
            if (entry.QuestRowId == quest)
            {
                return entry;
            }
        }

        return null;
    }
}

/// <summary>
/// The Moonlit unique view folded into counted rewards (feature plan v5, decision 4). Entries with the same
/// <see cref="RewardKey"/> are one reward. <see cref="RewardKind.ArtifactGear"/> ("Relic &amp; special weapons") is
/// folded per quest instead: each relic, Manderville, Skysteel, Splendorous or Phantom quest offers one item per job of
/// which the character receives one, so the quest's items are one reward ("1 of 18"); quests that share an item (the
/// repeatable "another job" quests: 69381, 70189, 70262, 70308, 70343) join their base quest's reward.
/// Groups keep the order of their first entry. Immutable; build once per catalog.
/// </summary>
public sealed class MoonlitGroups
{
    public static readonly MoonlitGroups Empty = new([], 0);

    private MoonlitGroups(MoonlitGroup[] all, int entryCount)
    {
        All = all;
        EntryCount = entryCount;
    }

    /// <summary>Every group, in the order of its first entry.</summary>
    public IReadOnlyList<MoonlitGroup> All { get; }

    /// <summary>How many entries the groups hold together (the unique view's size).</summary>
    public int EntryCount { get; }

    /// <summary>Whether a kind's entries fold per quest into choice sets rather than per reward.</summary>
    public static bool IsChoiceKind(RewardKind kind) => kind == RewardKind.ArtifactGear;

    /// <summary>Folds <paramref name="entries"/> (the unique view, <see cref="UniqueRewardCatalog.All"/>) into groups.</summary>
    /// <param name="isRepeatable">Whether a quest is repeatable: a choice set's home quest is its first quest that is not. Null takes the first quest.</param>
    public static MoonlitGroups Build(IReadOnlyList<UniqueRewardEntry> entries, Func<uint, bool>? isRepeatable = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            return Empty;
        }

        // Choice sets: quests of a choice kind joined when they share an item (union-find over quest row ids).
        var parent = new Dictionary<uint, uint>();
        uint Find(uint quest)
        {
            while (parent.TryGetValue(quest, out var up) && up != quest)
            {
                var grand = parent.GetValueOrDefault(up, up);
                parent[quest] = grand;
                quest = grand;
            }

            return quest;
        }

        var itemOwner = new Dictionary<RewardKey, uint>();
        foreach (var entry in entries)
        {
            if (!IsChoiceKind(entry.Kind))
            {
                continue;
            }

            parent.TryAdd(entry.QuestRowId, entry.QuestRowId);
            var key = RewardKey.Of(entry);
            if (itemOwner.TryGetValue(key, out var owner))
            {
                var a = Find(owner);
                var b = Find(entry.QuestRowId);
                if (a != b)
                {
                    // The earlier quest stays the root, so a set keeps the order of its first entry.
                    parent[b] = a;
                }
            }
            else
            {
                itemOwner[key] = entry.QuestRowId;
            }
        }

        var order = new List<object>();
        var byKey = new Dictionary<RewardKey, List<UniqueRewardEntry>>();
        var bySet = new Dictionary<(RewardKind Kind, uint Root), List<UniqueRewardEntry>>();
        foreach (var entry in entries)
        {
            if (IsChoiceKind(entry.Kind))
            {
                var set = (entry.Kind, Find(entry.QuestRowId));
                if (!bySet.TryGetValue(set, out var members))
                {
                    members = [];
                    bySet[set] = members;
                    order.Add(set);
                }

                members.Add(entry);
                continue;
            }

            var key = RewardKey.Of(entry);
            if (!byKey.TryGetValue(key, out var same))
            {
                same = [];
                byKey[key] = same;
                order.Add(key);
            }

            same.Add(entry);
        }

        var groups = new MoonlitGroup[order.Count];
        for (var i = 0; i < groups.Length; i++)
        {
            if (order[i] is RewardKey key)
            {
                var same = byKey[key];
                groups[i] = new MoonlitGroup(i, same.ToArray(), QuestsOf(same, null), 0);
            }
            else
            {
                var members = bySet[((RewardKind, uint))order[i]];
                var items = new HashSet<RewardKey>();
                foreach (var entry in members)
                {
                    items.Add(RewardKey.Of(entry));
                }

                groups[i] = new MoonlitGroup(i, members.ToArray(), QuestsOf(members, isRepeatable), items.Count);
            }
        }

        return new MoonlitGroups(groups, entries.Count);
    }

    /// <summary>The distinct quests of <paramref name="entries"/> in order, the first one <paramref name="isRepeatable"/> rejects moved to the front.</summary>
    private static uint[] QuestsOf(List<UniqueRewardEntry> entries, Func<uint, bool>? isRepeatable)
    {
        var quests = new List<uint>();
        foreach (var entry in entries)
        {
            if (!quests.Contains(entry.QuestRowId))
            {
                quests.Add(entry.QuestRowId);
            }
        }

        if (isRepeatable is not null && quests.Count > 1 && isRepeatable(quests[0]))
        {
            var home = quests.FindIndex(q => !isRepeatable(q));
            if (home > 0)
            {
                var quest = quests[home];
                quests.RemoveAt(home);
                quests.Insert(0, quest);
            }
        }

        return quests.ToArray();
    }
}
