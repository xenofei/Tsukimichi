using System.Globalization;
using System.Runtime.CompilerServices;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>
/// Lowercased search text per quest, built in one pass over a catalog. A query is a set of space-separated terms that
/// must all match: text terms match the name, reward names or internal id by substring; all-digit terms match the
/// row id or quest id exactly, or digits inside the name. Matching allocates nothing. A quest the spoiler shield masks
/// is matched by its placeholder ("main scenario quest (lv 83)") in place of its name, so typing a hidden name never
/// finds the quest.
/// </summary>
public sealed class SearchIndex
{
    private static readonly ConditionalWeakTable<QuestCatalog, SearchIndex> Cache = [];

    private readonly Dictionary<uint, Entry> entries;

    private SearchIndex(Dictionary<uint, Entry> entries)
    {
        this.entries = entries;
    }

    public int Count => entries.Count;

    /// <summary>Builds a fresh index; one pass over <see cref="QuestCatalog.All"/>.</summary>
    public static SearchIndex Build(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var entries = new Dictionary<uint, Entry>(catalog.Count);
        foreach (var quest in catalog.All)
        {
            entries[quest.RowId] = Entry.From(quest);
        }

        return new SearchIndex(entries);
    }

    /// <summary>The index for a catalog, built on first use and shared for the catalog's lifetime.</summary>
    public static SearchIndex For(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Cache.GetValue(catalog, Build);
    }

    /// <summary>Lowercases, trims and collapses whitespace so <see cref="Matches"/> can split on single spaces.</summary>
    public static string Normalize(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        var parts = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts).ToLowerInvariant();
    }

    /// <summary>True when every term of a normalized query matches the quest; an empty query matches everything.</summary>
    public bool Matches(uint rowId, string normalizedQuery) => Matches(rowId, normalizedQuery, null);

    /// <summary>
    /// <see cref="Matches(uint, string)"/> under a spoiler shield: a masked quest's name is left out of the match and
    /// its lowercased placeholder (<see cref="SpoilerMask.SearchName"/>) is matched in its place.
    /// </summary>
    public bool Matches(uint rowId, string normalizedQuery, SpoilerMask? spoilers)
    {
        if (normalizedQuery.Length == 0)
        {
            return true;
        }

        if (!entries.TryGetValue(rowId, out var entry))
        {
            return false;
        }

        var maskedName = spoilers?.SearchName(rowId);

        var rest = normalizedQuery.AsSpan();
        while (!rest.IsEmpty)
        {
            var space = rest.IndexOf(' ');
            var term = space < 0 ? rest : rest[..space];
            rest = space < 0 ? default : rest[(space + 1)..];
            if (term.IsEmpty)
            {
                continue;
            }

            if (!entry.MatchesTerm(term, maskedName))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class Entry
    {
        private readonly string name;
        private readonly string rewards;
        private readonly string internalId;
        private readonly string rowIdText;
        private readonly string questIdText;

        private Entry(string name, string rewards, string internalId, string rowIdText, string questIdText)
        {
            this.name = name;
            this.rewards = rewards;
            this.internalId = internalId;
            this.rowIdText = rowIdText;
            this.questIdText = questIdText;
        }

        public static Entry From(QuestRecord quest)
        {
            var rewards = quest.Rewards.Count == 0
                ? string.Empty
                : string.Join('\n', quest.Rewards.Select(r => r.Name)).ToLowerInvariant();
            return new Entry(
                quest.Name.ToLowerInvariant(),
                rewards,
                quest.InternalId.ToLowerInvariant(),
                quest.RowId.ToString(CultureInfo.InvariantCulture),
                quest.QuestId.ToString(CultureInfo.InvariantCulture));
        }

        /// <param name="maskedName">The lowercased placeholder matched instead of the name; null matches the name.</param>
        public bool MatchesTerm(ReadOnlySpan<char> term, string? maskedName)
        {
            var name = maskedName ?? this.name;
            if (IsAllDigits(term))
            {
                return term.SequenceEqual(rowIdText)
                    || term.SequenceEqual(questIdText)
                    || name.AsSpan().Contains(term, StringComparison.Ordinal);
            }

            return name.AsSpan().Contains(term, StringComparison.Ordinal)
                || rewards.AsSpan().Contains(term, StringComparison.Ordinal)
                || internalId.AsSpan().Contains(term, StringComparison.Ordinal);
        }

        private static bool IsAllDigits(ReadOnlySpan<char> term)
        {
            foreach (var c in term)
            {
                if (!char.IsAsciiDigit(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
