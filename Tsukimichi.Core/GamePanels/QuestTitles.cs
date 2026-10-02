using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.GamePanels;

/// <summary>
/// Quest titles as a game window prints them, made comparable with the catalog's names (1.7.0, the panels beside the
/// quest-offer, quest-complete and Journal windows). The game draws a title through its own font: private-use glyphs
/// (the level and event icons, U+E000 to U+F8FF), soft hyphens and other format characters the German and French
/// clients break long words with, typographic apostrophes and quotes, narrow no-break spaces before French
/// punctuation, full-width forms in the Japanese client. <see cref="Normalize"/> folds all of them away so the text a
/// window shows and the name the sheet holds compare equal in every client language. Pure.
/// </summary>
public static class QuestTitles
{
    /// <summary>
    /// The comparable form of a title: compatibility-normalised (NFKC, so full-width letters and the ideographic space
    /// read as their plain forms), format, control and private-use characters dropped, apostrophes, quotes and dashes
    /// unified, every run of whitespace one space, trimmed, and lower-cased culture-invariantly. Empty for null or a
    /// text that holds nothing comparable.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string folded;
        try
        {
            folded = text.Normalize(NormalizationForm.FormKC);
        }
        catch (ArgumentException)
        {
            // An unpaired surrogate cut out of a payload: compare what is there.
            folded = text;
        }

        var builder = new StringBuilder(folded.Length);
        var space = false;
        foreach (var c in folded)
        {
            var category = char.GetUnicodeCategory(c);
            if (char.IsWhiteSpace(c) || category == UnicodeCategory.SpaceSeparator)
            {
                space = builder.Length > 0;
                continue;
            }

            if (category is UnicodeCategory.Format or UnicodeCategory.Control or UnicodeCategory.PrivateUse or UnicodeCategory.Surrogate)
            {
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(Fold(c));
        }

        return builder.ToString();
    }

    private static char Fold(char c) => c switch
    {
        '‘' or '’' or '‚' or '‛' or 'ʼ' or '´' or '`' or '′' => '\'',
        '“' or '”' or '„' or '‟' or '«' or '»' or '″' => '"',
        '‐' or '‑' or '‒' or '–' or '—' or '―' or '−' => '-',
        '…' => '.',
        _ => char.ToLowerInvariant(c),
    };
}

/// <summary>
/// Every quest the game can still offer, by its normalised title (<see cref="QuestTitles.Normalize"/>): the
/// catalog-wide fallback when the quest a window shows is none of the expected candidates. Retired quests are left out
/// (no NPC offers them); quests sharing a title (the three cities' versions of one quest, a repeatable's yearly
/// copies) are listed together in catalog order. Immutable; built once per catalog.
/// </summary>
public sealed class QuestTitleIndex
{
    public static readonly QuestTitleIndex Empty = new(QuestCatalog.Empty, FrozenDictionary<string, QuestRecord[]>.Empty);

    private static readonly QuestRecord[] None = [];

    private readonly FrozenDictionary<string, QuestRecord[]> byTitle;

    private QuestTitleIndex(QuestCatalog catalog, FrozenDictionary<string, QuestRecord[]> byTitle)
    {
        Catalog = catalog;
        this.byTitle = byTitle;
    }

    /// <summary>The catalog the index was built from, so a holder can tell when to rebuild it.</summary>
    public QuestCatalog Catalog { get; }

    /// <summary>How many distinct titles the index holds.</summary>
    public int Count => byTitle.Count;

    public static QuestTitleIndex Build(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var map = new Dictionary<string, List<QuestRecord>>(StringComparer.Ordinal);
        foreach (var quest in catalog.All)
        {
            if (quest.IsRetired)
            {
                continue;
            }

            var title = QuestTitles.Normalize(quest.Name);
            if (title.Length == 0)
            {
                continue;
            }

            if (!map.TryGetValue(title, out var list))
            {
                list = [];
                map[title] = list;
            }

            list.Add(quest);
        }

        return new(catalog, map.ToFrozenDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal));
    }

    /// <summary>The quests whose normalised title is <paramref name="normalizedTitle"/>; empty when none.</summary>
    public IReadOnlyList<QuestRecord> Find(string normalizedTitle) =>
        string.IsNullOrEmpty(normalizedTitle) ? None : byTitle.GetValueOrDefault(normalizedTitle) ?? None;
}

/// <summary>Where <see cref="QuestIdentifier.Identify"/> found the quest.</summary>
public enum TitleMatchSource
{
    /// <summary>Nothing the window shows names a known quest.</summary>
    None,

    /// <summary>One of the expected candidates (the targeted NPC's quests, the journal's quests) carries the title.</summary>
    Candidate,

    /// <summary>No candidate did; the title matched across the whole catalog.</summary>
    Catalog,
}

/// <summary>The quest a game window shows, and how sure the match is.</summary>
/// <param name="Quest">The quest; null when <see cref="Source"/> is <see cref="TitleMatchSource.None"/>.</param>
/// <param name="Source">Where it was found.</param>
/// <param name="Ambiguity">How many quests carried the title (1 for an exact match); the first preferred one was taken.</param>
public readonly record struct TitleMatch(QuestRecord? Quest, TitleMatchSource Source, int Ambiguity)
{
    public static readonly TitleMatch NotFound = new(null, TitleMatchSource.None, 0);
}

/// <summary>
/// Which quest a game window shows (1.7.0): the quest-offer window (<c>JournalAccept</c>), the quest-complete window
/// (<c>JournalResult</c>). Neither tells its quest's id, so the window's texts are matched by title: an exact title
/// among the expected candidates (the targeted NPC's quests for an offer; the journal's quests and the ones just
/// completed for a completion), then an exact title across the catalog (<see cref="QuestTitleIndex"/>), and only then a
/// candidate's title inside a slightly longer label (never ahead of an exact title). A title several quests share is settled by
/// <c>prefer</c> (the caller's "Ready" or "in the journal"), then by order. Pure.
/// </summary>
public static class QuestIdentifier
{
    /// <summary>A candidate's title must be at least this long to be found inside a longer text (pass 3).</summary>
    public const int MinContainedLength = 4;

    /// <summary>
    /// How much longer than the title a text may be for pass 3 to look inside it: a label around a title ("Lv. 50"),
    /// not a paragraph of journal text that happens to use the title's words.
    /// </summary>
    public const int MaxLabelExtra = 24;

    /// <param name="texts">Every text read from the window (its title node, its string values); raw, not normalised.</param>
    /// <param name="candidates">The expected quests, most likely first.</param>
    /// <param name="index">The catalog-wide fallback; null skips it.</param>
    /// <param name="prefer">Breaks a tie between quests of one title; null takes the first.</param>
    public static TitleMatch Identify(IReadOnlyList<string> texts, IReadOnlyList<QuestRecord> candidates, QuestTitleIndex? index, Func<QuestRecord, bool>? prefer = null)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(candidates);

        var normalized = new List<string>(texts.Count);
        foreach (var text in texts)
        {
            var n = QuestTitles.Normalize(text);
            if (n.Length > 0 && !normalized.Contains(n))
            {
                normalized.Add(n);
            }
        }

        if (normalized.Count == 0)
        {
            return TitleMatch.NotFound;
        }

        // Pass 1: a candidate whose title is one of the texts.
        var exact = new List<QuestRecord>();
        foreach (var quest in candidates)
        {
            var title = QuestTitles.Normalize(quest.Name);
            if (title.Length > 0 && normalized.Contains(title) && !exact.Contains(quest))
            {
                exact.Add(quest);
            }
        }

        if (exact.Count > 0)
        {
            return new TitleMatch(Pick(exact, prefer), TitleMatchSource.Candidate, exact.Count);
        }

        // Pass 2: the whole catalog, by exact title. Before any contained-in match: a candidate whose title is only a
        // part of the window's title ("The Ties That Bind" inside "Where the Ties That Bind") is not the quest shown.
        if (index is not null)
        {
            var found = new List<QuestRecord>();
            foreach (var text in normalized)
            {
                foreach (var quest in index.Find(text))
                {
                    if (!found.Contains(quest))
                    {
                        found.Add(quest);
                    }
                }
            }

            if (found.Count > 0)
            {
                return new TitleMatch(Pick(found, prefer), TitleMatchSource.Catalog, found.Count);
            }
        }

        // Pass 3: a candidate whose title stands inside a slightly longer label ("Lv 50 <title>", a title with its tag).
        QuestRecord? contained = null;
        var containedLength = 0;
        var containedCount = 0;
        foreach (var quest in candidates)
        {
            var title = QuestTitles.Normalize(quest.Name);
            if (title.Length < MinContainedLength || title.Length < containedLength)
            {
                continue;
            }

            foreach (var text in normalized)
            {
                if (text.Length > title.Length && text.Length <= title.Length + MaxLabelExtra && ContainsWord(text, title))
                {
                    if (title.Length > containedLength)
                    {
                        containedLength = title.Length;
                        containedCount = 0;
                        contained = null;
                    }

                    containedCount++;
                    if (contained is null || (prefer is not null && !prefer(contained) && prefer(quest)))
                    {
                        contained = quest;
                    }

                    break;
                }
            }
        }

        return contained is null ? TitleMatch.NotFound : new TitleMatch(contained, TitleMatchSource.Candidate, containedCount);
    }

    private static QuestRecord Pick(List<QuestRecord> quests, Func<QuestRecord, bool>? prefer)
    {
        if (prefer is not null)
        {
            foreach (var quest in quests)
            {
                if (prefer(quest))
                {
                    return quest;
                }
            }
        }

        return quests[0];
    }

    /// <summary>Whether <paramref name="part"/> stands in <paramref name="text"/> on word boundaries (not inside a word).</summary>
    private static bool ContainsWord(string text, string part)
    {
        var at = text.IndexOf(part, StringComparison.Ordinal);
        while (at >= 0)
        {
            var before = at == 0 || !char.IsLetterOrDigit(text[at - 1]);
            var end = at + part.Length;
            var after = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (before && after)
            {
                return true;
            }

            at = text.IndexOf(part, at + 1, StringComparison.Ordinal);
        }

        return false;
    }
}
