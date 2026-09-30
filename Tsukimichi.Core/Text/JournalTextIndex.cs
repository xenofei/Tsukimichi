using System.Text;

namespace Tsukimichi.Core.Text;

/// <summary>
/// The journal text search index (P9): every word the journal entries and objectives of a quest contain
/// (<see cref="JournalTokenizer"/>) mapped to the quest row ids that contain it. It holds words and ids only, never a
/// sentence, so the file it is saved to is an index and not a copy of the game's text. Built off the framework thread
/// from the client's own quest text sheets, saved once per game version and language (<see cref="JournalIndexStore"/>),
/// and matched on the framework thread: a query term matches every word it starts ("ishga" finds "ishgard" and
/// "ishgardian"), a two-character pair of a script without spaces matches exactly, a lone character of such a script
/// matches every pair holding it ("竜" finds "竜騎士"), and a quest matches when every term does. Immutable once built; safe to read from any thread.
/// </summary>
public sealed class JournalTextIndex
{
    /// <summary>Bumped when the file layout or the tokenizer's rules change, so an older file is rebuilt.</summary>
    public const int FormatVersion = 2;

    /// <summary>The first character of the scripts without spaces (Hiragana); every CJK word sorts at or after it.</summary>
    private const string CjkStart = "぀";

    private static readonly byte[] Magic = "TSJI"u8.ToArray();

    // Sorted ordinally; postings[i] holds the ascending row ids of words[i].
    private readonly string[] words;
    private readonly uint[][] postings;

    private JournalTextIndex(string gameVersion, string language, int questCount, string[] words, uint[][] postings)
    {
        GameVersion = gameVersion;
        Language = language;
        QuestCount = questCount;
        this.words = words;
        this.postings = postings;
    }

    /// <summary>The game version the index was built from; a different running version means a rebuild.</summary>
    public string GameVersion { get; }

    /// <summary>The client language the text was read in ("en", "ja", ...).</summary>
    public string Language { get; }

    /// <summary>Quests with at least one word indexed.</summary>
    public int QuestCount { get; }

    /// <summary>Distinct words.</summary>
    public int WordCount => words.Length;

    /// <summary>Word-to-quest links in all.</summary>
    public long PostingCount
    {
        get
        {
            long total = 0;
            foreach (var list in postings)
            {
                total += list.Length;
            }

            return total;
        }
    }

    /// <summary>
    /// Row ids of the quests whose journal contains every term of <paramref name="query"/> (as a word prefix, see the
    /// class summary), among those <paramref name="keep"/> accepts. Null when the query has no term long enough to
    /// search the journal for; then only the ordinary name search applies.
    /// </summary>
    public IReadOnlySet<uint>? Match(string? query, Func<uint, bool>? keep = null)
    {
        var terms = JournalTokenizer.QueryTerms(query);
        if (terms.Count == 0)
        {
            return null;
        }

        HashSet<uint>? result = null;
        foreach (var term in terms)
        {
            var hits = TermHits(term);
            if (result is null)
            {
                result = hits;
            }
            else
            {
                result.IntersectWith(hits);
            }

            if (result.Count == 0)
            {
                return result;
            }
        }

        if (keep is not null)
        {
            result!.RemoveWhere(id => !keep(id));
        }

        return result;
    }

    /// <summary>
    /// Quests containing a word that <paramref name="term"/> starts. A CJK pair is stored as a word of its own, so a
    /// pair term matches only that pair; a lone CJK character matches every pair it begins or ends, and itself.
    /// </summary>
    private HashSet<uint> TermHits(string term)
    {
        var hits = new HashSet<uint>();
        for (var i = LowerBound(term); i < words.Length && words[i].StartsWith(term, StringComparison.Ordinal); i++)
        {
            hits.UnionWith(postings[i]);
        }

        if (term.Length == 1 && JournalTokenizer.IsCjkWord(term))
        {
            // The pairs that end with the character: the last one of a run ("士" in "竜騎士") starts no pair.
            var c = term[0];
            for (var i = LowerBound(CjkStart); i < words.Length; i++)
            {
                var word = words[i];
                if (word.Length == 2 && word[1] == c && JournalTokenizer.IsCjkWord(word))
                {
                    hits.UnionWith(postings[i]);
                }
            }
        }

        return hits;
    }

    private int LowerBound(string term)
    {
        int lo = 0, hi = words.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (string.CompareOrdinal(words[mid], term) < 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    // ------------------------------------------------------------------ building

    /// <summary>Collects words per quest; <see cref="Build"/> freezes them into an index.</summary>
    public sealed class Builder
    {
        private readonly Dictionary<string, List<uint>> byWord = new(StringComparer.Ordinal);
        private readonly HashSet<uint> quests = [];

        /// <summary>Adds one quest's words; a quest added twice keeps each word once.</summary>
        public void Add(uint rowId, IEnumerable<string> questWords)
        {
            ArgumentNullException.ThrowIfNull(questWords);
            var any = false;
            foreach (var word in questWords)
            {
                if (!byWord.TryGetValue(word, out var list))
                {
                    byWord[word] = list = [];
                }

                if (list.Count == 0 || list[^1] != rowId)
                {
                    list.Add(rowId);
                }

                any = true;
            }

            if (any)
            {
                quests.Add(rowId);
            }
        }

        public JournalTextIndex Build(string gameVersion, string language)
        {
            ArgumentNullException.ThrowIfNull(gameVersion);
            ArgumentNullException.ThrowIfNull(language);
            var sorted = byWord.Keys.ToArray();
            Array.Sort(sorted, StringComparer.Ordinal);
            var lists = new uint[sorted.Length][];
            for (var i = 0; i < sorted.Length; i++)
            {
                var ids = byWord[sorted[i]];
                ids.Sort();
                lists[i] = Distinct(ids);
            }

            return new JournalTextIndex(gameVersion, language, quests.Count, sorted, lists);
        }

        private static uint[] Distinct(List<uint> sortedIds)
        {
            var result = new List<uint>(sortedIds.Count);
            foreach (var id in sortedIds)
            {
                if (result.Count == 0 || result[^1] != id)
                {
                    result.Add(id);
                }
            }

            return [.. result];
        }
    }

    // ------------------------------------------------------------------ file format

    /// <summary>
    /// Writes the index: "TSJI", the format version, the game version and language, the quest count, then per word
    /// its UTF-8 bytes and its row ids as ascending deltas, all lengths and numbers as 7-bit variable-length integers.
    /// </summary>
    public void Write(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write7BitEncodedInt(FormatVersion);
        writer.Write(GameVersion);
        writer.Write(Language);
        writer.Write7BitEncodedInt(QuestCount);
        writer.Write7BitEncodedInt(words.Length);
        for (var i = 0; i < words.Length; i++)
        {
            writer.Write(words[i]);
            var list = postings[i];
            writer.Write7BitEncodedInt(list.Length);
            uint previous = 0;
            foreach (var id in list)
            {
                writer.Write7BitEncodedInt((int)(id - previous));
                previous = id;
            }
        }
    }

    /// <summary>
    /// Reads an index written by <see cref="Write"/>. Null (never an exception) when the stream is not an index, was
    /// written by another format version, or was built for another game version or language: the caller rebuilds.
    /// </summary>
    public static JournalTextIndex? Read(Stream stream, string expectedGameVersion, string expectedLanguage)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var magic = reader.ReadBytes(Magic.Length);
            if (!magic.AsSpan().SequenceEqual(Magic) || reader.Read7BitEncodedInt() != FormatVersion)
            {
                return null;
            }

            var gameVersion = reader.ReadString();
            var language = reader.ReadString();
            if (!string.Equals(gameVersion, expectedGameVersion, StringComparison.Ordinal) || !string.Equals(language, expectedLanguage, StringComparison.Ordinal))
            {
                return null;
            }

            var questCount = reader.Read7BitEncodedInt();
            var count = reader.Read7BitEncodedInt();
            if (questCount < 0 || count < 0 || count > 10_000_000)
            {
                return null;
            }

            var words = new string[count];
            var lists = new uint[count][];
            for (var i = 0; i < count; i++)
            {
                words[i] = reader.ReadString();
                if (i > 0 && string.CompareOrdinal(words[i - 1], words[i]) >= 0)
                {
                    return null;
                }

                var n = reader.Read7BitEncodedInt();
                if (n < 0 || n > 1_000_000)
                {
                    return null;
                }

                var list = new uint[n];
                uint previous = 0;
                for (var j = 0; j < n; j++)
                {
                    previous += (uint)reader.Read7BitEncodedInt();
                    list[j] = previous;
                }

                lists[i] = list;
            }

            return new JournalTextIndex(gameVersion, language, questCount, words, lists);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or FormatException or DecoderFallbackException)
        {
            return null;
        }
    }
}
