using System.Text.RegularExpressions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.GameData;

/// <summary>
/// Second pass over the mapped records: files the quests the sheet leaves without a journal genre, following the
/// seven rules of docs/data/unlisted-report.md section 4 in order, then the curated overrides. Pure over
/// <see cref="QuestRecord"/>s, so it runs on a frozen catalog as well as on the live sheets, and under
/// <see cref="JournalFiling.Legacy"/> it is simply not called.
/// <list type="number">
/// <item>Placeholder issuer (<see cref="PlaceholderIssuer"/>) or the hidden flag: retired, listed or not; keeps the genre it carries (0 for the sheet's genre-less rows).</item>
/// <item>Class or job intro quasi-quest (<c>Cls…001</c>, <c>Cls…999</c>, <c>Job…299</c>): the genre of its first listed successor; the class intros stay out of the genre's counts, the job intros count (<see cref="ClassIntroRule"/>).</item>
/// <item>A Grand Company: that company's Grand Company Quests genre (<see cref="GrandCompanyGenreBase"/> + company).</item>
/// <item>The first listed prerequisite, walking through unlisted ones in slot order, outside the main scenario sections and in the quest's own expansion: its genre.</item>
/// <item>Else the first listed successor, then a quest-lock partner, under the same constraint.</item>
/// <item>Else the dominant genre of the listed regional sidequests (categories 59–85) issued from the same territory; a tie stays unlisted.</item>
/// <item>Else unlisted.</item>
/// </list>
/// A row rule 4 files that is a hidden progress tracker (<see cref="IsProgressTracker"/>) is filed there as
/// <see cref="TrackerRule"/> instead: listed under the genre, but out of every count, never a feature quest and never
/// a chain step (<see cref="QuestRecord.IsProgressTracker"/>).
/// Curated <c>retired_quests.json</c> retires rows the sheet does not mark (they keep their genre) and carries the
/// patch note for the ones rule 1 already retires; curated <c>refile_overrides.json</c> pins a quest to a genre after
/// the rules. A quest a curated file decided marks <see cref="QuestRecord.RefiledFrom"/> with <see cref="CuratedRule"/>.
/// </summary>
public static partial class JournalRefiler
{
    /// <summary>The empty <c>ENpcResident</c> row the game moves a quest's issuer to when it retires the quest.</summary>
    public const uint PlaceholderIssuer = 1034221;

    /// <summary><see cref="QuestRecord.RefiledFrom"/> value for a filing a curated file decided rather than a rule.</summary>
    public const byte CuratedRule = 8;

    /// <summary>The Grand Company Quests genres are 234 Maelstrom, 235 Twin Adder, 236 Immortal Flames: this plus the GrandCompany row id.</summary>
    public const uint GrandCompanyGenreBase = 233;

    /// <summary>JournalCategory ids of the regional sidequest categories rule 6 votes over.</summary>
    public const uint RegionalCategoryMin = 59;
    public const uint RegionalCategoryMax = 85;

    /// <summary>Rules that keep a quest out of a journal node: retired, and no signal.</summary>
    public const byte RetiredRule = 1;
    public const byte UnlistedRule = 7;

    /// <summary>
    /// Rule 2, the class and job intros, filed under their class's genre. The class intros (<c>Cls…001</c>/<c>999</c>,
    /// see <see cref="IsStartingClassIntro"/>) get <see cref="QuestRecord.CountsInTotals"/> false, because the class a
    /// character started as never gets its intro (the starter is handed "Way of the …" directly; the intro is only
    /// offered to a character switching in), so counting it would keep that genre one short. The job intros
    /// (<c>Job…299</c>: A Dark Spectacle, So You Want to Be a Machinist, What's Your Sign) count as usual: nobody
    /// starts as a job, so every character can complete them.
    /// </summary>
    public const byte ClassIntroRule = 2;

    /// <summary>
    /// A hidden progress tracker (<see cref="IsProgressTracker"/>), filed under the genre rule 4 finds and kept out of
    /// the counts; see <see cref="QuestRecord.IsProgressTracker"/>.
    /// </summary>
    public const byte TrackerRule = QuestRecord.ProgressTrackerRule;

    /// <summary>ClassJobCategory row "All Classes": every class and job, the adventurer and the limited jobs included.</summary>
    public const uint AllClassesCategory = 1;

    /// <summary>The lowest prerequisite level that marks a level-1 row behind it as a tracker rather than a starter quest.</summary>
    public const byte TrackerPrerequisiteLevel = 50;

    /// <summary>The genre rank sits above the sheet's own SortKey in <see cref="JournalRef.SortKey"/>; see <c>CatalogMapper</c>.</summary>
    private const int SortKeyGenreShift = 16;
    private const int SheetSortKeyMask = 0xFFFF;

    /// <summary>Main scenario sections, never a refiling target: a hidden step of the story would still not be story.</summary>
    private static bool IsMainScenarioSection(uint sectionId) => sectionId is 0 or 1;

    [GeneratedRegex(@"^(Cls\w{3}(001|999)|Job\w{3}299)_", RegexOptions.CultureInvariant)]
    private static partial Regex ClassIntroPattern();

    /// <summary>Whether an internal id names a class or job intro quasi-quest (rule 2).</summary>
    public static bool IsClassIntro(string internalId) => internalId is not null && ClassIntroPattern().IsMatch(internalId);

    [GeneratedRegex(@"^Cls\w{3}(001|999)_", RegexOptions.CultureInvariant)]
    private static partial Regex StartingClassIntroPattern();

    /// <summary>
    /// Whether an internal id names the intro of a class a character can start as (<c>Cls…001</c>/<c>999</c>, "So You
    /// Want to Be a Gladiator"): the rule-2 rows that stay out of the totals (<see cref="ClassIntroRule"/>). The job
    /// intros (<c>Job…299</c>) are class intros for rule 2 but not for this.
    /// </summary>
    public static bool IsStartingClassIntro(string internalId) => internalId is not null && StartingClassIntroPattern().IsMatch(internalId);

    /// <summary>
    /// Files every genre-0 record and retires the curated ones; listed records come back unchanged unless a curated
    /// file names them. The result keeps the input order; the caller sorts it into a catalog.
    /// </summary>
    public static IReadOnlyList<QuestRecord> Apply(IReadOnlyList<QuestRecord> quests, CuratedData curated)
    {
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(curated);

        var index = new Index(quests, curated);
        var result = new QuestRecord[quests.Count];
        for (var i = 0; i < quests.Count; i++)
        {
            result[i] = File(quests[i], index, curated);
        }

        return result;
    }

    private static QuestRecord File(QuestRecord quest, Index index, CuratedData curated)
    {
        // Rule 1 reads the sheet alone, listed or not: a row the game moved to the placeholder issuer or flagged
        // hidden is retired and keeps whatever genre it carries, so the next patch that retires a listed quest is
        // seen without a curated entry. The curated file covers rows with no sheet signal (the 3.05 trio) and lends
        // its patch note to the others.
        if (IsRetiredRow(quest))
        {
            return quest with { IsRetired = true, RefiledFrom = RetiredRule };
        }

        if (curated.RetiredQuests.ContainsKey(quest.RowId))
        {
            return quest with { IsRetired = true, RefiledFrom = CuratedRule };
        }

        if (curated.RefileOverrides.TryGetValue(quest.RowId, out var pinned))
        {
            if (index.Assign(quest, pinned.GenreId) is { } target)
            {
                return quest with { Journal = target, RefiledFrom = CuratedRule };
            }

            // The override names a genre no listed quest holds (a typo, or a genre the sheet emptied): filing there
            // would create a nameless node under the main scenario. A listed quest keeps the sheet's filing; an
            // unlisted one stays unlisted as if no rule had placed it, and the caller's log names the row.
            return quest.IsUnlisted ? quest with { RefiledFrom = UnlistedRule } : quest;
        }

        if (!quest.IsUnlisted)
        {
            return quest;
        }

        var (rule, genre) = Decide(quest, index);
        if (genre == 0)
        {
            return quest with { RefiledFrom = rule };
        }

        // Rules 2, 4, 5 and 6 read the genre off a listed quest, so a template exists; rule 3 computes it and the
        // Grand Company genre may hold no listed quest in a future sheet.
        return index.Assign(quest, genre) is { } journal
            ? quest with { Journal = journal, RefiledFrom = rule, CountsInTotals = rule != TrackerRule && !(rule == ClassIntroRule && IsStartingClassIntro(quest.InternalId)) }
            : quest with { RefiledFrom = UnlistedRule };
    }

    /// <summary>Rule 1's predicate on the row alone.</summary>
    public static bool IsRetiredRow(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return quest.IsHidden || quest.Issuer is { NpcId: PlaceholderIssuer };
    }

    /// <summary>
    /// Whether a genre-0 row whose rule-4 prerequisite is <paramref name="lender"/> is a hidden progress tracker
    /// rather than a quest (docs/data/v4/tagging-audit.md finding 2). Two shapes, neither listed by the Lodestone (it
    /// lists journal categories, and the row has none) nor by the wiki (docs/data/quest-verification.csv):
    /// <list type="bullet">
    /// <item>A level-1 row open to <see cref="AllClassesCategory"/> behind a prerequisite of level
    /// <see cref="TrackerPrerequisiteLevel"/> or more: the YoRHa: Dark Apocalypse markers (A Message from Konogg …
    /// All's Well That Ends with Ale), the Resistance ones (Memoirs from the Front, An Honor to Serve) and the
    /// Ishgardian Restoration phases (The Mendicant's Court, The New Nest, Featherfall, The Risensong Quarter). No
    /// real quest takes a level-50 prerequisite and then asks for level 1; A Seaside Story, the level-1 Save the Queen
    /// quest behind Fit for a Queen, is open to Disciples of War and Magic only and stays a quest.</item>
    /// <item>A repeatable with no journal steps and no reward: the weapon service rows Recondition the Anima and
    /// Forged Anew, a dialogue the relic NPC repeats rather than a quest.</item>
    /// </list>
    /// </summary>
    public static bool IsProgressTracker(QuestRecord quest, QuestRecord lender)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(lender);
        return (quest.Level == 1 && quest.ClassJobCategory == AllClassesCategory && lender.Level >= TrackerPrerequisiteLevel)
            || (quest.IsRepeatable && quest.StepCount == 0 && quest.Rewards.Count == 0);
    }

    /// <summary>Rules 2–7 for a genre-0 quest that is not retired: the rule that fired (<see cref="TrackerRule"/> for a tracker rule 4 files) and the genre it chose (0 for rule 7).</summary>
    private static (byte Rule, uint Genre) Decide(QuestRecord quest, Index index)
    {
        if (IsClassIntro(quest.InternalId) && index.FirstListedSuccessorGenre(quest) is { } intro)
        {
            return (ClassIntroRule, intro);
        }

        if (quest.GrandCompany != 0)
        {
            return (3, GrandCompanyGenreBase + quest.GrandCompany);
        }

        if (index.PrerequisiteLender(quest) is { } prerequisite)
        {
            return (IsProgressTracker(quest, prerequisite) ? TrackerRule : (byte)4, prerequisite.Journal.GenreId);
        }

        if (index.SuccessorOrLockGenre(quest) is { } successor)
        {
            return (5, successor);
        }

        if (index.TerritoryGenre(quest) is { } territory)
        {
            return (6, territory);
        }

        return (UnlistedRule, 0);
    }

    /// <summary>Whether <paramref name="candidate"/> can lend its genre to <paramref name="quest"/> under rules 4 and 5.</summary>
    private static bool Fits(QuestRecord candidate, QuestRecord quest) =>
        !candidate.IsUnlisted && !IsMainScenarioSection(candidate.Journal.SectionId) && candidate.Expansion == quest.Expansion;

    /// <summary>Lookups over the raw records: rows, genre templates, successors, lock partners and the territory vote.</summary>
    private sealed class Index
    {
        private readonly Dictionary<uint, QuestRecord> byRowId;
        private readonly Dictionary<uint, JournalRef> templates = [];
        private readonly Dictionary<uint, List<uint>> successors = [];
        private readonly Dictionary<uint, List<uint>> lockedBy = [];
        private readonly Dictionary<uint, Dictionary<uint, int>> territoryVotes = [];

        public Index(IReadOnlyList<QuestRecord> quests, CuratedData curated)
        {
            byRowId = new Dictionary<uint, QuestRecord>(quests.Count);
            foreach (var quest in quests)
            {
                byRowId[quest.RowId] = quest;
            }

            // Successor and lock lists in row order, so "first" is the same whatever order the records arrived in.
            foreach (var quest in quests.OrderBy(q => q.RowId))
            {
                foreach (var previous in quest.PreviousQuests.QuestIds)
                {
                    Add(successors, previous, quest.RowId);
                }

                foreach (var lockId in quest.QuestLocks)
                {
                    Add(lockedBy, lockId, quest.RowId);
                }

                if (quest.IsUnlisted)
                {
                    continue;
                }

                templates.TryAdd(quest.Journal.GenreId, quest.Journal);

                // Rule 6 is a vote among the quests the game still hands out: a listed row retired by the sheet or by
                // the curated file (A Seat at the Feast in Mor Dhona) must not be the tie-breaker.
                if (IsRetiredRow(quest) || curated.RetiredQuests.ContainsKey(quest.RowId))
                {
                    continue;
                }

                var category = quest.Journal.CategoryId;
                if (category is >= RegionalCategoryMin and <= RegionalCategoryMax && quest.Issuer is { TerritoryId: not 0 } issuer)
                {
                    if (!territoryVotes.TryGetValue(issuer.TerritoryId, out var votes))
                    {
                        votes = [];
                        territoryVotes[issuer.TerritoryId] = votes;
                    }

                    votes[quest.Journal.GenreId] = votes.GetValueOrDefault(quest.Journal.GenreId) + 1;
                }
            }
        }

        /// <summary>
        /// The quest's journal reference under <paramref name="genreId"/>: the genre's names, section, category and
        /// rank from any listed quest already filed there, with the quest's own sheet SortKey in the low bits so it
        /// sorts inside the genre. Null when no listed quest holds the genre: there is no section or category to
        /// file under, and <see cref="JournalRef.None"/> would put the quest in the main scenario section.
        /// </summary>
        public JournalRef? Assign(QuestRecord quest, uint genreId)
        {
            if (!templates.TryGetValue(genreId, out var template))
            {
                return null;
            }

            var sheetKey = quest.Journal.SortKey & SheetSortKeyMask;
            var rank = template.SortKey >> SortKeyGenreShift;
            return template with { SortKey = (rank << SortKeyGenreShift) | sheetKey };
        }

        /// <summary>Rule 2: the genre of the first listed direct successor, in row order.</summary>
        public uint? FirstListedSuccessorGenre(QuestRecord quest)
        {
            if (!successors.TryGetValue(quest.RowId, out var next))
            {
                return null;
            }

            foreach (var id in next)
            {
                if (byRowId.TryGetValue(id, out var successor) && !successor.IsUnlisted)
                {
                    return successor.Journal.GenreId;
                }
            }

            return null;
        }

        /// <summary>Rule 4: depth-first through the prerequisite slots, descending only through unlisted ones; the listed quest that lends its genre.</summary>
        public QuestRecord? PrerequisiteLender(QuestRecord quest)
        {
            var visited = new HashSet<uint> { quest.RowId };
            return Walk(quest, quest.PreviousQuests.QuestIds, visited, static (q, index) => q.PreviousQuests.QuestIds);
        }

        /// <summary>Rule 5: the successors the same way, then the quest's lock partners in either direction.</summary>
        public uint? SuccessorOrLockGenre(QuestRecord quest)
        {
            var visited = new HashSet<uint> { quest.RowId };
            if (Walk(quest, SuccessorsOf(quest.RowId), visited, static (q, index) => index.SuccessorsOf(q.RowId)) is { } successor)
            {
                return successor.Journal.GenreId;
            }

            foreach (var id in quest.QuestLocks.Concat(lockedBy.GetValueOrDefault(quest.RowId) ?? []))
            {
                if (byRowId.TryGetValue(id, out var partner) && Fits(partner, quest))
                {
                    return partner.Journal.GenreId;
                }
            }

            return null;
        }

        /// <summary>Rule 6: the genre with the most listed regional sidequests issued from the quest's territory; null on a tie or no vote.</summary>
        public uint? TerritoryGenre(QuestRecord quest)
        {
            if (quest.Issuer is not { TerritoryId: not 0 } issuer || !territoryVotes.TryGetValue(issuer.TerritoryId, out var votes))
            {
                return null;
            }

            uint best = 0;
            var bestCount = 0;
            var tied = false;
            foreach (var (genre, count) in votes)
            {
                if (count > bestCount)
                {
                    best = genre;
                    bestCount = count;
                    tied = false;
                }
                else if (count == bestCount)
                {
                    tied = true;
                }
            }

            return tied ? null : best;
        }

        private IReadOnlyList<uint> SuccessorsOf(uint rowId) => successors.GetValueOrDefault(rowId) ?? [];

        /// <summary>
        /// First listed record that fits, in slot order, descending through unlisted records only; a listed record
        /// that does not fit ends its branch.
        /// </summary>
        private QuestRecord? Walk(QuestRecord quest, IReadOnlyList<uint> ids, HashSet<uint> visited, Func<QuestRecord, Index, IReadOnlyList<uint>> next)
        {
            foreach (var id in ids)
            {
                if (!visited.Add(id) || !byRowId.TryGetValue(id, out var candidate))
                {
                    continue;
                }

                if (!candidate.IsUnlisted)
                {
                    if (Fits(candidate, quest))
                    {
                        return candidate;
                    }

                    continue;
                }

                if (Walk(quest, next(candidate, this), visited, next) is { } found)
                {
                    return found;
                }
            }

            return null;
        }

        private static void Add(Dictionary<uint, List<uint>> map, uint key, uint value)
        {
            if (!map.TryGetValue(key, out var list))
            {
                list = [];
                map[key] = list;
            }

            list.Add(value);
        }
    }
}
