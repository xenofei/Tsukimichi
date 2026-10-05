using System.Collections.Frozen;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Chains;

/// <summary>
/// One person a quest's script names (feature plan v7 N10): the ENpcResident row, the name in English (the key every
/// rule reads, whatever the client's language) and the name to print.
/// </summary>
/// <param name="NpcId">The ENpcResident row.</param>
/// <param name="Key">The English name (<c>ENpcResident.Singular</c>), which groups a character's many rows.</param>
/// <param name="Display">The name in the client's language.</param>
public readonly record struct CastName(uint NpcId, string Key, string Display);

/// <summary>
/// The curated overlay of <c>curated/story_cast.json</c>: names to join under one character (two spellings the game
/// uses for the same person) and names to keep out (a wrong match). Keys are English names as the game writes them.
/// </summary>
/// <param name="Aliases">A name the game uses to the character's name it joins ("Nero tol Scaeva" to "Nero").</param>
/// <param name="Blocks">Names that are never story characters.</param>
public sealed record StoryCastCuration(IReadOnlyDictionary<string, string> Aliases, IReadOnlySet<string> Blocks)
{
    public static readonly StoryCastCuration Empty = new(FrozenDictionary<string, string>.Empty, FrozenSet<string>.Empty);
}

/// <summary>A recurring story character: an id within its <see cref="StoryCast"/>, the name to print and the main scenario quests that feature them.</summary>
/// <param name="NpcIds">The character's ENpcResident rows, lowest first: the portrait index picks among them by the
/// quest (<see cref="PortraitIndex.ForCast"/>).</param>
public sealed record CastMember(int Id, string Key, string Name, IReadOnlyList<uint> StoryQuests, IReadOnlyList<uint> NpcIds);

/// <summary>Who of a quest's cast the viewed character has met, in script order, and how many others ("familiar faces").</summary>
public readonly record struct CastLine(IReadOnlyList<CastMember> Met, int Familiar)
{
    public static readonly CastLine None = new([], 0);

    public bool IsEmpty => Met.Count == 0 && Familiar == 0;
}

/// <summary>
/// Who's in it (feature plan v7 N10): the recurring story characters each quest features, from its script's actors
/// and listeners (read by <c>QuestCastReader</c> in Tsukimichi.GameData). A character is <b>recurring</b> when it is
/// named in at least <see cref="MinStoryQuests"/> main scenario quests; generic names ("serpent officer", "Temple
/// Knight guard": <see cref="PortraitNames.IsGeneric"/>) are dropped, the curated aliases join two names of one person
/// and the curated blocks drop wrong matches. An era name (the Crystal Exarch) is a character of its own, never joined
/// to the name the story reveals later.
/// <para>
/// <b>Met.</b> A character is named only once the viewed character has completed a main scenario quest that features
/// them (<see cref="HasMet"/>); everyone else is "a familiar face". Immutable once built; lookups allocate nothing.
/// </para>
/// </summary>
public sealed class StoryCast
{
    /// <summary>Main scenario quests a name needs to be a recurring character (about 120 on 2026.09.15).</summary>
    public const int MinStoryQuests = 8;

    public static readonly StoryCast Empty = new([], FrozenDictionary<uint, int[]>.Empty, FrozenDictionary<uint, uint[][]>.Empty);

    private readonly FrozenDictionary<uint, int[]> byQuest;

    /// <summary>Per quest, beside <see cref="byQuest"/>'s member ids: each member's ENpcResident rows the quest's script names.</summary>
    private readonly FrozenDictionary<uint, uint[][]> rowsByQuest;

    private StoryCast(IReadOnlyList<CastMember> members, FrozenDictionary<uint, int[]> byQuest, FrozenDictionary<uint, uint[][]> rowsByQuest)
    {
        Members = members;
        this.byQuest = byQuest;
        this.rowsByQuest = rowsByQuest;
    }

    /// <summary>Every recurring character, most story quests first.</summary>
    public IReadOnlyList<CastMember> Members { get; }

    /// <summary>How many quests feature a recurring character.</summary>
    public int QuestCount => byQuest.Count;

    /// <summary>Whether the quest features a recurring character.</summary>
    public bool HasCast(uint rowId) => byQuest.ContainsKey(rowId);

    /// <summary>The recurring characters a quest features, in script order; empty for none.</summary>
    public IReadOnlyList<CastMember> Of(uint rowId)
    {
        if (!byQuest.TryGetValue(rowId, out var ids))
        {
            return [];
        }

        var members = new CastMember[ids.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            members[i] = Members[ids[i]];
        }

        return members;
    }

    /// <summary>
    /// The ENpcResident rows of <paramref name="member"/> the quest's script names (the look the quest shows them in),
    /// lowest first; empty when the quest does not feature them. Allocates nothing.
    /// </summary>
    public IReadOnlyList<uint> RowsIn(uint rowId, CastMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (byQuest.TryGetValue(rowId, out var ids) && rowsByQuest.TryGetValue(rowId, out var rows))
        {
            for (var i = 0; i < ids.Length; i++)
            {
                if (ids[i] == member.Id)
                {
                    return rows[i];
                }
            }
        }

        return [];
    }

    /// <summary>A recurring character by its English name; null when the name is not one.</summary>
    public CastMember? Find(string key)
    {
        foreach (var member in Members)
        {
            if (string.Equals(member.Key, key, StringComparison.Ordinal))
            {
                return member;
            }
        }

        return null;
    }

    /// <summary>Whether the character has been met: some main scenario quest that features them is completed.</summary>
    public static bool HasMet(CastMember member, Func<uint, bool> completed)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(completed);
        foreach (var rowId in member.StoryQuests)
        {
            if (completed(rowId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A quest's cast split into the characters met (named) and the rest (counted as familiar faces).</summary>
    public CastLine Line(uint rowId, Func<uint, bool> completed)
    {
        ArgumentNullException.ThrowIfNull(completed);
        if (!byQuest.TryGetValue(rowId, out var ids))
        {
            return CastLine.None;
        }

        var met = new List<CastMember>(ids.Length);
        var familiar = 0;
        foreach (var id in ids)
        {
            var member = Members[id];
            if (HasMet(member, completed))
            {
                met.Add(member);
            }
            else
            {
                familiar++;
            }
        }

        return new CastLine(met, familiar);
    }

    /// <summary>
    /// Finds the recurring characters and the quests that feature them.
    /// </summary>
    /// <param name="catalog">The quest catalog: which quests are main scenario quests, and which exist at all.</param>
    /// <param name="scripts">Each quest's named people, in script order (actors, then listeners); duplicates allowed.</param>
    /// <param name="curation">The curated aliases and blocks; null for none.</param>
    /// <param name="minStoryQuests">Main scenario quests a name needs; <see cref="MinStoryQuests"/> unless a test says otherwise.</param>
    public static StoryCast Build(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, IReadOnlyList<CastName>> scripts,
        StoryCastCuration? curation = null,
        int minStoryQuests = MinStoryQuests)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(scripts);
        curation ??= StoryCastCuration.Empty;

        // Each quest's distinct characters by their joined English name, in script order.
        var questKeys = new Dictionary<uint, List<string>>(scripts.Count);
        var storyQuests = new Dictionary<string, List<uint>>(StringComparer.Ordinal);
        var display = new Dictionary<string, string>(StringComparer.Ordinal);
        var npcIds = new Dictionary<string, SortedSet<uint>>(StringComparer.Ordinal);
        var questRows = new Dictionary<(uint Quest, string Key), SortedSet<uint>>();
        foreach (var quest in catalog.All)
        {
            if (!scripts.TryGetValue(quest.RowId, out var names) || quest.IsRetired)
            {
                continue;
            }

            var msq = FeaturePresets.IsMainScenario(quest);
            List<string>? keys = null;
            foreach (var name in names)
            {
                if (KeyOf(name.Key, curation) is not { } key)
                {
                    continue;
                }

                keys ??= [];

                // Every row the script names: the character's rows (each a look of theirs), and the quest's own.
                if (!questRows.TryGetValue((quest.RowId, key), out var inQuest))
                {
                    questRows[(quest.RowId, key)] = inQuest = [];
                }

                inQuest.Add(name.NpcId);
                if (!npcIds.TryGetValue(key, out var ids))
                {
                    npcIds[key] = ids = [];
                }

                ids.Add(name.NpcId);
                if (keys.Contains(key))
                {
                    continue;
                }

                keys.Add(key);
                // The joined name prints as the game writes it; until it is seen itself, as its alias does.
                if (string.Equals(name.Key, key, StringComparison.Ordinal) || !display.ContainsKey(key))
                {
                    display[key] = name.Display.Length > 0 ? name.Display : name.Key;
                }

                if (msq)
                {
                    if (!storyQuests.TryGetValue(key, out var list))
                    {
                        storyQuests[key] = list = [];
                    }

                    list.Add(quest.RowId);
                }
            }

            if (keys is not null)
            {
                questKeys[quest.RowId] = keys;
            }
        }

        var recurring = storyQuests
            .Where(kv => kv.Value.Count >= minStoryQuests)
            .OrderByDescending(kv => kv.Value.Count)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .ToList();
        if (recurring.Count == 0)
        {
            return Empty;
        }

        var members = new CastMember[recurring.Count];
        var idOf = new Dictionary<string, int>(recurring.Count, StringComparer.Ordinal);
        for (var i = 0; i < members.Length; i++)
        {
            var (key, quests) = recurring[i];
            members[i] = new CastMember(i, key, display[key], quests.ToArray(), npcIds[key].ToArray());
            idOf[key] = i;
        }

        var byQuest = new Dictionary<uint, int[]>();
        var rowsByQuest = new Dictionary<uint, uint[][]>();
        foreach (var (rowId, keys) in questKeys)
        {
            List<int>? ids = null;
            List<uint[]>? rows = null;
            foreach (var key in keys)
            {
                if (idOf.TryGetValue(key, out var id))
                {
                    (ids ??= []).Add(id);
                    (rows ??= []).Add(questRows[(rowId, key)].ToArray());
                }
            }

            if (ids is not null && rows is not null)
            {
                byQuest[rowId] = ids.ToArray();
                rowsByQuest[rowId] = rows.ToArray();
            }
        }

        return new StoryCast(members, byQuest.ToFrozenDictionary(), rowsByQuest.ToFrozenDictionary());
    }

    /// <summary>The character a name stands for: its alias's target, null for a generic or blocked name.</summary>
    private static string? KeyOf(string name, StoryCastCuration curation)
    {
        if (PortraitNames.IsGeneric(name) || curation.Blocks.Contains(name))
        {
            return null;
        }

        var key = curation.Aliases.TryGetValue(name, out var joined) ? joined : name;
        return curation.Blocks.Contains(key) ? null : key;
    }
}
