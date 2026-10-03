using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Unique;

/// <summary>One cell of the collection grid: whether a character owns a reward, as its snapshot saved it.</summary>
public enum OwnedCell : byte
{
    /// <summary>The snapshot saved no answer for it (a file from before 1.5, or a flag the client could not read).</summary>
    Unknown,
    Owned,
    Missing,
}

/// <summary>One reward row of the collection grid: the reward, one cell per character column, and how many own it.</summary>
public sealed record CollectionGridRow(RewardKind Kind, uint RewardId, string Name, uint QuestRowId, OwnedCell[] Cells, int OwnedCount)
{
    /// <summary>The entry the row was made from, for its icon (UI-5d); null for a row built by hand.</summary>
    public UniqueRewardEntry? Entry { get; init; }
}

/// <summary>One unlock quest row of the grid's quest mode: the quest and its state per character column (null: not known yet).</summary>
public sealed record QuestGridRow(QuestRecord Quest, string Name, QuestState?[] Cells, int DoneCount);

/// <summary>
/// "Who has it" (1.8.0, R7 C, R5 F2): the Moonlit collectibles × the characters, from the owned answers each snapshot
/// saved (decision 9), and the unlock quests × the characters from their evaluations. Pure: the Characters pane builds
/// it once per input change and draws only the rows in view.
/// </summary>
public static class CollectionGrid
{
    /// <summary>The collectible kinds present in <paramref name="entries"/>, in <see cref="Collectibles.StoredKinds"/> order (the grid's kind filter).</summary>
    public static List<RewardKind> Kinds(IEnumerable<UniqueRewardEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var present = entries.Where(static e => Collectibles.IsStored(e.Kind) && e.RewardId != 0).Select(static e => e.Kind).ToHashSet();
        return Collectibles.StoredKinds.Where(present.Contains).ToList();
    }

    /// <summary>
    /// The reward rows: every collectible of a saved kind (<see cref="Collectibles.IsStored"/>) once, by kind then name,
    /// each with one cell per entry of <paramref name="columns"/> (a character's saved answers; null when it saved none).
    /// <paramref name="kind"/> keeps one kind; <paramref name="missingOnAny"/> keeps the rows some character is known
    /// to lack; <paramref name="search"/> keeps the rows whose name holds every word typed.
    /// </summary>
    public static List<CollectionGridRow> Rewards(
        IEnumerable<UniqueRewardEntry> entries,
        IReadOnlyList<CollectibleLookup?> columns,
        RewardKind? kind = null,
        bool missingOnAny = false,
        string? search = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(columns);
        var words = Words(search);
        var seen = new HashSet<(RewardKind, uint)>();
        var rows = new List<CollectionGridRow>();
        foreach (var entry in entries)
        {
            if (!Collectibles.IsStored(entry.Kind) || entry.RewardId == 0 || (kind is { } only && entry.Kind != only)
                || !seen.Add((entry.Kind, entry.RewardId)) || !HasWords(entry.RewardName, words))
            {
                continue;
            }

            var cells = new OwnedCell[columns.Count];
            var owned = 0;
            var anyMissing = false;
            for (var c = 0; c < cells.Length; c++)
            {
                var answer = columns[c]?.Owns(entry.Kind, entry.RewardId);
                cells[c] = answer switch
                {
                    true => OwnedCell.Owned,
                    false => OwnedCell.Missing,
                    null => OwnedCell.Unknown,
                };
                owned += answer == true ? 1 : 0;
                anyMissing |= answer == false;
            }

            if (missingOnAny && !anyMissing)
            {
                continue;
            }

            rows.Add(new CollectionGridRow(entry.Kind, entry.RewardId, entry.RewardName, entry.QuestRowId, cells, owned) { Entry = entry });
        }

        rows.Sort(static (a, b) =>
        {
            var byKind = KindOrder(a.Kind).CompareTo(KindOrder(b.Kind));
            if (byKind != 0)
            {
                return byKind;
            }

            var byName = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
            return byName != 0 ? byName : a.RewardId.CompareTo(b.RewardId);
        });
        return rows;
    }

    /// <summary>
    /// The quest rows, in the order given, each with one cell per entry of <paramref name="columns"/> (a character's
    /// states by row id; null while not resolved). <paramref name="notDoneOnAny"/> keeps the quests some character is
    /// known not to have completed; <paramref name="search"/> matches the displayed <paramref name="name"/>.
    /// </summary>
    public static List<QuestGridRow> Quests(
        IEnumerable<QuestRecord> quests,
        IReadOnlyList<IReadOnlyDictionary<uint, QuestState>?> columns,
        Func<QuestRecord, string> name,
        bool notDoneOnAny = false,
        string? search = null)
    {
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(name);
        var words = Words(search);
        var rows = new List<QuestGridRow>();
        foreach (var quest in quests)
        {
            var label = name(quest);
            if (!HasWords(label, words))
            {
                continue;
            }

            var cells = new QuestState?[columns.Count];
            var done = 0;
            var anyOpen = false;
            for (var c = 0; c < cells.Length; c++)
            {
                QuestState? state = columns[c] is { } states ? states.TryGetValue(quest.RowId, out var s) ? s : QuestState.Unknown : null;
                cells[c] = state;
                done += state == QuestState.Completed ? 1 : 0;
                anyOpen |= state is { } known && known != QuestState.Completed && known != QuestState.Unknown;
            }

            if (notDoneOnAny && !anyOpen)
            {
                continue;
            }

            rows.Add(new QuestGridRow(quest, label, cells, done));
        }

        return rows;
    }

    private static int KindOrder(RewardKind kind)
    {
        for (var i = 0; i < Collectibles.StoredKinds.Count; i++)
        {
            if (Collectibles.StoredKinds[i] == kind)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static string[] Words(string? search) =>
        string.IsNullOrWhiteSpace(search) ? [] : search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool HasWords(string text, string[] words)
    {
        foreach (var word in words)
        {
            if (!text.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
