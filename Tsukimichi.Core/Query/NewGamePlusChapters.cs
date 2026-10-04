namespace Tsukimichi.Core.Query;

/// <summary>
/// One quest of a New Game+ part, in the part's order: the Quest sheet row id and the <c>QuestRedoParam</c> variant
/// mask (0 for a quest every character plays; 1, 2, 4… for the start city's or Grand Company's own quests, which stand
/// side by side in the list).
/// </summary>
public readonly record struct NewGamePlusStep(uint Quest, byte Variant);

/// <summary>
/// One <c>QuestRedo</c> row: a stretch of a chapter (up to 32 quests). <paramref name="Next"/> is the row that follows
/// it (the sheet's link column; 0 at the chapter's end). A chapter split by start city has one first row per city,
/// each leading into the shared rows after it.
/// </summary>
public sealed record NewGamePlusPart(uint RowId, uint Chapter, uint Next, IReadOnlyList<NewGamePlusStep> Quests);

/// <summary>Where a quest sits in its New Game+ chapter: "Shadowbringers - Part 2 · quest 87 of 112".</summary>
/// <param name="Chapter">The <c>QuestRedoChapterUI</c> row id.</param>
/// <param name="Name">The chapter's name as the game writes it ("Shadowbringers - Part 2").</param>
/// <param name="Index">The quest's place in the chapter, from 1.</param>
/// <param name="Count">The quests the chapter takes this character through.</param>
public readonly record struct NewGamePlusPosition(uint Chapter, string Name, int Index, int Count);

/// <summary>
/// The game's New Game+ chapters as parts (feature plan v7, 1.19.0, C4): read from <c>QuestRedo</c> and
/// <c>QuestRedoChapterUI</c> by GameData, interpreted here. <see cref="Position"/> says where a quest sits in its
/// chapter for the status bar's New Game+ line. Immutable; pure.
/// </summary>
public sealed class NewGamePlusChapters
{
    /// <summary>No chapter read (a frozen fixture, or a sheet that failed): every position is unknown.</summary>
    public static readonly NewGamePlusChapters Empty = new([], new Dictionary<uint, string>());

    private readonly Dictionary<uint, NewGamePlusPart> parts;
    private readonly Dictionary<uint, List<NewGamePlusPart>> byQuest = [];
    private readonly IReadOnlyDictionary<uint, string> names;

    public NewGamePlusChapters(IEnumerable<NewGamePlusPart> parts, IReadOnlyDictionary<uint, string> names)
    {
        ArgumentNullException.ThrowIfNull(parts);
        this.names = names ?? throw new ArgumentNullException(nameof(names));
        this.parts = [];
        foreach (var part in parts)
        {
            this.parts[part.RowId] = part;
            foreach (var step in part.Quests)
            {
                if (!byQuest.TryGetValue(step.Quest, out var list))
                {
                    byQuest[step.Quest] = list = [];
                }

                list.Add(part);
            }
        }

        foreach (var list in byQuest.Values)
        {
            list.Sort(static (a, b) => a.RowId.CompareTo(b.RowId));
        }
    }

    /// <summary>The parts read.</summary>
    public int Count => parts.Count;

    /// <summary>The chapter's name ("A Realm Reborn - Part 1"); empty for a chapter the sheet does not name.</summary>
    public string ChapterName(uint chapter) => names.GetValueOrDefault(chapter, string.Empty);

    /// <summary>Whether some chapter lists the quest.</summary>
    public bool Lists(uint questRowId) => byQuest.ContainsKey(questRowId);

    /// <summary>
    /// Where <paramref name="questRowId"/> sits in its chapter; null when no chapter lists it or the chapter has no
    /// name. <paramref name="hint"/> is what the game's New Game+ HUD says the chapter is: a chapter or a part listing
    /// the quest is preferred when it matches either id; otherwise the lowest part listing it is taken. The count walks
    /// the chapter's parts from its first to its last through that part, and counts the quests every character plays
    /// plus the quest's own variant (the start city's), so a city's three versions of one step count once.
    /// </summary>
    public NewGamePlusPosition? Position(uint questRowId, uint hint = 0)
    {
        if (!byQuest.TryGetValue(questRowId, out var candidates))
        {
            return null;
        }

        var part = candidates[0];
        if (hint != 0)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.RowId == hint || candidate.Chapter == hint)
                {
                    part = candidate;
                    break;
                }
            }
        }

        var name = ChapterName(part.Chapter);
        if (name.Length == 0)
        {
            return null;
        }

        var path = Path(part);
        byte variant = 0;
        foreach (var step in part.Quests)
        {
            if (step.Quest == questRowId)
            {
                variant = step.Variant;
                break;
            }
        }

        var bit = LowestBit(variant);
        if (bit == 0)
        {
            // A step every character plays: count the first variant met along the path, as one city would play it.
            foreach (var p in path)
            {
                foreach (var step in p.Quests)
                {
                    if (bit == 0 && step.Variant != 0)
                    {
                        bit = LowestBit(step.Variant);
                    }
                }
            }
        }

        var seen = new HashSet<uint>();
        var index = 0;
        var count = 0;
        foreach (var p in path)
        {
            foreach (var step in p.Quests)
            {
                var plays = step.Quest == questRowId || step.Variant == 0 || (step.Variant & bit) != 0;
                if (!plays || !seen.Add(step.Quest))
                {
                    continue;
                }

                count++;
                if (step.Quest == questRowId && index == 0)
                {
                    index = count;
                }
            }
        }

        return index == 0 ? null : new NewGamePlusPosition(part.Chapter, name, index, count);
    }

    /// <summary>
    /// The position to show when the game's HUD could not be read but the capture shows a replay
    /// (<paramref name="replaying"/>, the quests whose completion the replay cleared): the earliest of them in the chapter
    /// that lists most of them. Null when no chapter lists any.
    /// </summary>
    public NewGamePlusPosition? Guess(IEnumerable<uint> replaying)
    {
        ArgumentNullException.ThrowIfNull(replaying);
        var votes = new Dictionary<uint, int>();
        var quests = new List<uint>();
        foreach (var quest in replaying)
        {
            if (!byQuest.TryGetValue(quest, out var list))
            {
                continue;
            }

            quests.Add(quest);
            foreach (var chapter in list.Select(static p => p.Chapter).Distinct())
            {
                votes[chapter] = votes.GetValueOrDefault(chapter) + 1;
            }
        }

        if (votes.Count == 0)
        {
            return null;
        }

        var best = votes.OrderByDescending(static kv => kv.Value).ThenBy(static kv => kv.Key).First().Key;
        NewGamePlusPosition? earliest = null;
        foreach (var quest in quests)
        {
            if (Position(quest, best) is { } position && position.Chapter == best && (earliest is null || position.Index < earliest.Value.Index))
            {
                earliest = position;
            }
        }

        return earliest;
    }

    /// <summary>The chapter's parts from its first to its last through <paramref name="part"/>, following the sheet's links.</summary>
    private List<NewGamePlusPart> Path(NewGamePlusPart part)
    {
        var back = new List<NewGamePlusPart>();
        var seen = new HashSet<uint> { part.RowId };
        var current = part;
        while (true)
        {
            NewGamePlusPart? previous = null;
            foreach (var candidate in parts.Values)
            {
                if (candidate.Next == current.RowId && candidate.Chapter == current.Chapter && (previous is null || candidate.RowId < previous.RowId))
                {
                    previous = candidate;
                }
            }

            if (previous is null || !seen.Add(previous.RowId))
            {
                break;
            }

            back.Add(previous);
            current = previous;
        }

        back.Reverse();
        back.Add(part);
        current = part;
        while (current.Next != 0 && parts.TryGetValue(current.Next, out var next) && next.Chapter == part.Chapter && seen.Add(next.RowId))
        {
            back.Add(next);
            current = next;
        }

        return back;
    }

    private static byte LowestBit(byte value) => (byte)(value & -value);
}
