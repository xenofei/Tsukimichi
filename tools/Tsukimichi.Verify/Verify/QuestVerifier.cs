using System.Text.RegularExpressions;
using Tsukimichi.Core.Model;
using Tsukimichi.GameData;
using Tsukimichi.Verify.Game;
using Tsukimichi.Verify.Sources;

namespace Tsukimichi.Verify.Verify;

/// <summary>Festival window seed collected from the wiki while verifying seasonal quests.</summary>
internal sealed record FestivalWindow(ushort FestivalId, string EventName, string Start, string End, string WikiUrl, string? LodestoneUrl, List<uint> QuestRowIds);

/// <summary>Fact names as they appear in the CSV.</summary>
internal static class Facts
{
    public const string Listed = "listed";
    public const string Name = "name";
    public const string DisplayLevel = "displayLevel";
    public const string RawLevel = "rawLevel";
    public const string ClassJob = "classJob";
    public const string StartingClass = "startingClass";
    public const string GrandCompany = "grandCompany";
    public const string Genre = "genre";
    public const string Section = "section";
    public const string Expansion = "expansion";
    public const string Prereqs = "prereqs";
    public const string Duties = "duties";
    public const string Rewards = "rewards";
    public const string DutyUnlock = "dutyUnlock";
    public const string Retired = "retired";
}

/// <summary>
/// Compares every selected quest against the Lodestone (official), the wiki (human-curated) and, for curated duty
/// unlocks, Garland Tools. Produces the long-form rows; the reconciliation rules of
/// docs/review/panel/qa-data-engineer.md §1.4 decide between catalogWrong, sourceWrong and unresolved.
/// </summary>
internal sealed partial class QuestVerifier(
    GameCatalog game,
    LodestoneSource lodestone,
    WikiSource wiki,
    GarlandSource garland,
    IReadOnlyDictionary<uint, List<uint>> curatedDutyUnlocks,
    IReadOnlyDictionary<uint, string> curatedSystemUnlocks,
    IReadOnlyList<UniqueRewardEntry> uniqueEntries,
    TextWriter log)
{
    private readonly Dictionary<uint, WikiPage> wikiByRow = [];
    private readonly Dictionary<uint, string> wikiAmbiguity = [];
    private readonly Dictionary<uint, string> wikiMissing = [];
    private readonly Dictionary<uint, LodestoneListing> listingByRow = [];
    private readonly Dictionary<uint, string> lodestoneAmbiguity = [];
    private readonly Dictionary<uint, string> twinNotes = [];
    private readonly Dictionary<(uint Section, uint Category), (List<LodestoneListing>? Rows, int Total, string Url)> listings = [];
    private readonly Dictionary<uint, LodestonePage?> pagesByRow = [];
    private readonly Dictionary<uint, GarlandQuest?> garlandByRow = [];
    private readonly Dictionary<string, List<uint>> rowsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<uint>> rowsByBaseName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<uint>> rowsByLooseName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> nameAliases = new(StringComparer.Ordinal);
    private readonly HashSet<string> cfcNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ushort, FestivalWindow> festivals = [];

    public IReadOnlyDictionary<ushort, FestivalWindow> Festivals => festivals;
    public int LodestoneListed { get; private set; }
    public int LodestonePagesFetched { get; private set; }
    public int WikiPagesFound { get; private set; }
    public int NameAliasesResolved => nameAliases.Count;

    [GeneratedRegex(@"https?://[a-z]{2}\.finalfantasyxiv\.com/lodestone/(?:special|topics)/[^\s\]|<>""']+")]
    private static partial Regex LodestoneAnnouncement();

    [GeneratedRegex(@"^\s*\d+\s+(.*)$")]
    private static partial Regex LeadingCount();

    public async Task<List<QuestRow>> RunAsync(IReadOnlyList<QuestRecord> selection, Action<List<QuestRow>, int> checkpoint, CancellationToken ct)
    {
        foreach (var q in game.Catalog.All)
        {
            Index(rowsByName, Names.Canon(q.Name), q.RowId);
            Index(rowsByBaseName, Names.Canon(Names.Base(q.Name)), q.RowId);
            Index(rowsByLooseName, Names.LooseKey(q.Name), q.RowId);
        }

        foreach (var name in game.ContentFinderConditionNames.Values)
        {
            cfcNames.Add(Names.Clean(name));
        }

        await WikiPassAsync(selection, ct);
        await UnknownNamesPassAsync(selection, ct);
        await LodestoneListingsAsync(selection, ct);
        await FestivalPassAsync(selection, ct);
        await GarlandPassAsync(selection, ct);

        // The long loop: one Lodestone page per listed quest. Checkpoint every 100 so an interrupted run leaves usable CSVs.
        var rows = new List<QuestRow>();
        var done = 0;
        foreach (var quest in selection)
        {
            ct.ThrowIfCancellationRequested();
            if (listingByRow.TryGetValue(quest.RowId, out var listing) && !pagesByRow.ContainsKey(quest.RowId))
            {
                var page = await lodestone.GetPageAsync(listing.LodestoneId, ct);
                pagesByRow[quest.RowId] = page;
                if (page is not null)
                {
                    LodestonePagesFetched++;
                }
            }

            rows.AddRange(Reconcile(Compare(quest)));
            done++;
            if (done % 100 == 0)
            {
                checkpoint(rows, done);
                log.WriteLine($"quests: {done}/{selection.Count} compared ({rows.Count} rows)");
            }
        }

        return rows;
    }

    // ---- wiki --------------------------------------------------------------------------------------------------

    private async Task WikiPassAsync(IReadOnlyList<QuestRecord> selection, CancellationToken ct)
    {
        var pages = await wiki.GetPagesAsync(selection.Select(q => q.Name), ct);
        var byGt = IndexByGt(pages.Values);

        // Second pass: disambiguation targets and same-name variants that the first page did not cover.
        var extra = new List<string>();
        foreach (var quest in selection)
        {
            if (byGt.ContainsKey(quest.RowId))
            {
                continue;
            }

            if (pages.TryGetValue(Names.WikiTitle(quest.Name), out var page) && !page.Missing && page.IsDisambiguation)
            {
                extra.AddRange(page.DisambiguationTargets());
            }
            else if (page is { Missing: false } && page.QuestInfobox.Count == 0)
            {
                // The title belongs to something else (an action, a zone, an expansion); the wiki files the quest as "Name (Quest)".
                extra.Add($"{quest.Name} (Quest)");
            }

            var siblings = rowsByName.GetValueOrDefault(Names.Canon(quest.Name), []);
            if (siblings.Count > 1)
            {
                var x = game.ExtrasOf(quest.RowId);
                if (x.PlaceName.Length > 0)
                {
                    extra.Add($"{quest.Name} ({x.PlaceName})");
                }

                if (x.GrandCompanyName.Length > 0)
                {
                    extra.Add($"{quest.Name} ({x.GrandCompanyName})");
                }

                if (x.ClassJobRequiredName.Length > 0)
                {
                    extra.Add($"{quest.Name} ({x.ClassJobRequiredName})");
                }
            }
        }

        extra = extra.Where(t => !pages.ContainsKey(Names.WikiTitle(t))).Distinct(StringComparer.Ordinal).ToList();
        if (extra.Count > 0)
        {
            log.WriteLine($"wiki: second pass for {extra.Count} disambiguation/variant titles");
            foreach (var (k, v) in await wiki.GetPagesAsync(extra, ct))
            {
                pages.TryAdd(k, v);
            }

            byGt = IndexByGt(pages.Values);
        }

        foreach (var quest in selection)
        {
            if (byGt.TryGetValue(quest.RowId, out var exact))
            {
                wikiByRow[quest.RowId] = exact;
                continue;
            }

            var title = Names.WikiTitle(quest.Name);
            if (!pages.TryGetValue(title, out var page) || page.Missing)
            {
                wikiMissing[quest.RowId] = "no wiki page with this title";
                continue;
            }

            if (page.IsDisambiguation)
            {
                wikiAmbiguity[quest.RowId] = "disambiguation page; no target carries id-gt=" + quest.RowId;
                continue;
            }

            var box = page.QuestInfobox;
            if (box.Count == 0)
            {
                wikiMissing[quest.RowId] = "wiki page with this title is not a quest page";
                continue;
            }

            if (box.TryGetValue("id-gt", out var gt) && uint.TryParse(gt.Trim(), out var otherRow) && otherRow != quest.RowId)
            {
                wikiAmbiguity[quest.RowId] = $"wiki page carries id-gt={otherRow} (another row of the same name)";
                continue;
            }

            var siblings = rowsByName.GetValueOrDefault(Names.Canon(quest.Name), []);
            if (siblings.Count > 1)
            {
                wikiAmbiguity[quest.RowId] = $"name shared by {siblings.Count} rows and the wiki page has no id-gt";
                continue;
            }

            wikiByRow[quest.RowId] = page;
        }

        WikiPagesFound = wikiByRow.Count;
        log.WriteLine($"wiki: {wikiByRow.Count} quests matched, {wikiAmbiguity.Count} ambiguous, {wikiMissing.Count} missing");
    }

    private static void Index(Dictionary<string, List<uint>> map, string key, uint rowId)
    {
        if (key.Length == 0)
        {
            return;
        }

        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = [];
        }

        list.Add(rowId);
    }

    /// <summary>
    /// Prerequisite names the wiki uses that no catalog row carries (renamed quests, special-character titles, the
    /// wiki's own disambiguation spellings): their wiki pages name the row through <c>id-gt</c>, which becomes an alias.
    /// </summary>
    private async Task UnknownNamesPassAsync(IReadOnlyList<QuestRecord> selection, CancellationToken ct)
    {
        var unknown = new HashSet<string>(StringComparer.Ordinal);
        foreach (var quest in selection)
        {
            if (!wikiByRow.TryGetValue(quest.RowId, out var page))
            {
                continue;
            }

            var box = page.QuestInfobox;
            foreach (var name in WikiSource.SplitNames(box.GetValueOrDefault("prev-quest")).Concat(WikiSource.SplitNames(box.GetValueOrDefault("req-quest"))))
            {
                // Anything but an exact catalog name: a disambiguated title ("Cleaning House (Level 56)") may be a renamed row, not the base-named one.
                if (!rowsByName.ContainsKey(Names.Canon(name)))
                {
                    unknown.Add(name);
                }
            }
        }

        if (unknown.Count == 0)
        {
            return;
        }

        log.WriteLine($"wiki: looking up {unknown.Count} prerequisite names that are not exact catalog names");
        var pages = await wiki.GetPagesAsync(unknown, ct);
        foreach (var name in unknown)
        {
            if (!pages.TryGetValue(Names.WikiTitle(name), out var page) || page.Missing)
            {
                continue;
            }

            var id = page.QuestInfobox.Count > 0 ? page.IdGt : 0;
            if (id != 0 && game.Catalog.GetByRowId(id) is { } row && Names.Canon(row.Name) != Names.Canon(name))
            {
                nameAliases[Names.Canon(name)] = Names.Canon(row.Name);
            }
        }

        log.WriteLine($"wiki: {nameAliases.Count} of {unknown.Count} resolved to another row name through the page's id-gt");
    }

    private static Dictionary<uint, WikiPage> IndexByGt(IEnumerable<WikiPage> pages)
    {
        var byGt = new Dictionary<uint, WikiPage>();
        foreach (var page in pages)
        {
            if (page.Missing)
            {
                continue;
            }

            var box = page.QuestInfobox;
            if (box.TryGetValue("id-gt", out var gt) && uint.TryParse(gt.Trim(), out var id))
            {
                byGt.TryAdd(id, page);
            }
        }

        return byGt;
    }

    // ---- lodestone listings ---------------------------------------------------------------------------------------

    private async Task LodestoneListingsAsync(IReadOnlyList<QuestRecord> selection, CancellationToken ct)
    {
        var categories = selection
            .Select(game.SheetRecord)
            .Where(q => !q.IsUnlisted && q.Journal.SectionId != 255)
            .Select(q => (q.Journal.SectionId, q.Journal.CategoryId))
            .Distinct()
            .OrderBy(k => k.SectionId).ThenBy(k => k.CategoryId)
            .ToList();
        log.WriteLine($"lodestone: enumerating {categories.Count} categories");
        var all = new List<LodestoneListing>();
        foreach (var (section, category) in categories)
        {
            var (rows, total, url) = await lodestone.ListCategoryAsync(section, category, ct);
            listings[(section, category)] = (rows, total, url);
            if (rows is not null)
            {
                all.AddRange(rows);
                if (rows.Count != total)
                {
                    log.WriteLine($"lodestone: category {section}/{category} listed {rows.Count} of {total}");
                }
            }
        }

        LodestoneListed = all.Count;
        var byId = new Dictionary<string, LodestoneListing>(StringComparer.Ordinal);
        foreach (var l in all)
        {
            byId.TryAdd(l.LodestoneId, l);
        }

        var byCategoryName = all
            .GroupBy(l => (l.CategoryId, Names.Canon(l.Name)))
            .ToDictionary(g => g.Key, g => g.ToList());
        var claimed = new Dictionary<string, uint>(StringComparer.Ordinal);

        // First the rows whose wiki page names the Lodestone id, when the catalog has one row of that name: a wiki page
        // shared by same-name twins names one twin's id-edb for both (Way of the Archer: id-gt 65557, id-edb of 65667).
        foreach (var quest in selection)
        {
            if (rowsByName.GetValueOrDefault(Names.Canon(quest.Name), []).Count > 1)
            {
                continue;
            }

            if (wikiByRow.TryGetValue(quest.RowId, out var page) && page.QuestInfobox.TryGetValue("id-edb", out var edb))
            {
                var id = edb.Trim().ToLowerInvariant();
                if (id.Length > 0 && byId.TryGetValue(id, out var listing) && claimed.TryAdd(id, quest.RowId))
                {
                    listingByRow[quest.RowId] = listing;
                }
            }
        }

        // Then by (category, name). Where the name repeats inside the category the candidate pages are fetched and
        // scored on level, class line, starting class, rewards, area and Grand Company; a row takes a page only when
        // it is that page's best row and the page is that row's best page.
        var groups = selection
            .Select(game.SheetRecord)
            .Where(q => !listingByRow.ContainsKey(q.RowId) && !q.IsUnlisted)
            .GroupBy(q => (q.Journal.CategoryId, Name: Names.Canon(q.Name)));
        var scored = 0;
        foreach (var group in groups)
        {
            var quests = group.ToList();
            if (!byCategoryName.TryGetValue(group.Key, out var candidates))
            {
                continue;
            }

            var free = candidates.Where(c => !claimed.ContainsKey(c.LodestoneId)).ToList();
            if (free.Count == 0)
            {
                foreach (var quest in quests)
                {
                    lodestoneAmbiguity[quest.RowId] = $"the {candidates.Count} listing(s) of this name in category {quest.Journal.CategoryId} are claimed by other rows";
                }

                continue;
            }

            if (quests.Count == 1 && free.Count == 1)
            {
                claimed[free[0].LodestoneId] = quests[0].RowId;
                listingByRow[quests[0].RowId] = free[0];
                continue;
            }

            var pages = new Dictionary<string, LodestonePage?>(StringComparer.Ordinal);
            foreach (var c in free)
            {
                pages[c.LodestoneId] = await lodestone.GetPageAsync(c.LodestoneId, ct);
                scored++;
            }

            var scores = quests.ToDictionary(q => q.RowId, q => free.ToDictionary(c => c.LodestoneId, c => Score(q, c, pages[c.LodestoneId]), StringComparer.Ordinal));
            var open = quests.Select(q => q.RowId).ToHashSet();
            var progress = true;
            while (progress && open.Count > 0)
            {
                progress = false;
                foreach (var rowId in open.ToList())
                {
                    var mine = scores[rowId].Where(kv => !claimed.ContainsKey(kv.Key)).OrderByDescending(kv => kv.Value).ToList();
                    if (mine.Count == 0)
                    {
                        break;
                    }

                    var best = mine[0];
                    if (mine.Count > 1 && mine[1].Value == best.Value)
                    {
                        continue;
                    }

                    // The page must prefer this row too.
                    var rivals = open.Where(r => r != rowId).Select(r => scores[r][best.Key]).ToList();
                    if (rivals.Count > 0 && rivals.Max() >= best.Value)
                    {
                        continue;
                    }

                    claimed[best.Key] = rowId;
                    listingByRow[rowId] = free.First(c => c.LodestoneId == best.Key);
                    pagesByRow[rowId] = pages[best.Key];
                    open.Remove(rowId);
                    progress = true;
                }
            }

            // Rows identical in every modeled fact (the city-start variants of Close to Home differ only by InternalId) tie on
            // every page. Any pairing checks the same facts, so a twin group takes its best pages in id order and says so.
            foreach (var twins in open.GroupBy(r => string.Join(",", free.Select(c => scores[r][c.LodestoneId]))).Select(g => g.OrderBy(r => r).ToList()).ToList())
            {
                var first = scores[twins[0]];
                var best = first.Where(kv => !claimed.ContainsKey(kv.Key)).Select(kv => kv.Value).DefaultIfEmpty(int.MinValue).Max();
                var pages0 = first.Where(kv => !claimed.ContainsKey(kv.Key) && kv.Value == best).Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
                if (best <= 0 || pages0.Count < twins.Count)
                {
                    continue;
                }

                for (var i = 0; i < twins.Count; i++)
                {
                    claimed[pages0[i]] = twins[i];
                    listingByRow[twins[i]] = free.First(c => c.LodestoneId == pages0[i]);
                    pagesByRow[twins[i]] = pages[pages0[i]];
                    twinNotes[twins[i]] = $"{twins.Count} rows of this name are identical in every modeled fact and tie on {pages0.Count} pages; paired in id order";
                    open.Remove(twins[i]);
                }
            }

            foreach (var rowId in open)
            {
                var quest = quests.First(q => q.RowId == rowId);
                lodestoneAmbiguity[rowId] = $"{free.Count} listings share this name in category {quest.Journal.CategoryId}; level, class, starting class, rewards and area do not single one out";
            }
        }

        log.WriteLine($"lodestone: {all.Count} listed ids, {listingByRow.Count} mapped to rows ({scored} candidate pages scored for repeated names), {lodestoneAmbiguity.Count} ambiguous");
    }

    /// <summary>How well one Lodestone page fits a catalog row; used only when several rows share a name inside one journal category.</summary>
    private int Score(QuestRecord q, LodestoneListing l, LodestonePage? p)
    {
        if (p is null || !p.Parsed)
        {
            return -100;
        }

        var x = game.ExtrasOf(q.RowId);
        var s = l.Level == GameCatalog.DisplayLevel(q) ? 2 : -3;
        s += Names.Canon(l.Area) == Names.Canon(x.PlaceName) ? 1 : 0;
        var classOk = x.ClassJobRequiredName.Length > 0
            ? Names.Canon(p.ClassJobText) == Names.Canon(x.ClassJobRequiredName) || Names.Canon(p.ClassJobText) == Names.Canon(x.ClassJobRequiredAbbreviation)
            : SameClassCategory(p.ClassJobText, x.ClassJobCategoryName) || Names.Canon(p.ClassJobText) == Names.Canon(x.ClassJobCategoryName);
        s += classOk ? 2 : -2;
        var expectedStart = x.ClassJobRequiredName.Length > 0 ? x.ClassJobRequiredName : SingleClassOf(x.ClassJobCategoryName);
        s += Names.Canon(p.StartingClass) == Names.Canon(expectedStart) ? 2 : -2;
        var rewardNames = q.Rewards.Where(r => r.Kind is RewardKind.Item or RewardKind.OptionalItem or RewardKind.ArtifactGear).Select(r => r.Name).ToList();
        var pageRewards = p.Rewards.Concat(p.OptionalRewards).Where(r => !IsCrystal(r)).ToList();
        s += Names.SameSet(rewardNames, pageRewards) ? 2 : RewardsConsistent(rewardNames, pageRewards, out _) ? 0 : -2;
        var gcOk = x.GrandCompanyName.Length == 0 ? p.GrandCompany.Length == 0 : Names.Canon(p.GrandCompany).StartsWith(Names.Canon(x.GrandCompanyName), StringComparison.Ordinal);
        s += gcOk ? 1 : -2;
        // Identical twins (a legacy duplicate row) tie on every fact; the wiki's id-edb for the row it carries breaks the tie.
        if (wikiByRow.TryGetValue(q.RowId, out var w) && w.QuestInfobox.TryGetValue("id-edb", out var edb) && string.Equals(edb.Trim(), l.LodestoneId, StringComparison.OrdinalIgnoreCase))
        {
            s += 1;
        }

        return s;
    }

    /// <summary>The class a single-class ClassJobCategory ("ARC", "Lancer") names, or empty for a multi-class category.</summary>
    private string SingleClassOf(string categoryName)
    {
        var c = Names.Canon(categoryName);
        if (c.Length == 0)
        {
            return string.Empty;
        }

        foreach (var (id, abbreviation) in game.Bundle.Names.ClassJobAbbreviations)
        {
            var full = game.Bundle.Names.ClassJob(id);
            if (Names.Canon(abbreviation) == c || Names.Canon(full) == c)
            {
                return full;
            }
        }

        return string.Empty;
    }

    /// <summary>Highest row id mapped to a listing in the quest's category; a higher unmapped row is a Lodestone lag, not an omission.</summary>
    private uint MaxMappedRowInCategory(uint categoryId)
    {
        uint max = 0;
        foreach (var (rowId, listing) in listingByRow)
        {
            if (listing.CategoryId == categoryId && rowId > max)
            {
                max = rowId;
            }
        }

        return max;
    }

    // ---- festivals ---------------------------------------------------------------------------------------------

    private async Task FestivalPassAsync(IReadOnlyList<QuestRecord> selection, CancellationToken ct)
    {
        var eventTitles = new Dictionary<ushort, string>();
        foreach (var quest in selection)
        {
            if (quest.Festival == 0 || !wikiByRow.TryGetValue(quest.RowId, out var page))
            {
                continue;
            }

            var box = page.QuestInfobox;
            var eventName = WikiSource.StripMarkup(box.GetValueOrDefault("event", string.Empty));
            if (eventName.Length == 0)
            {
                eventName = WikiSource.StripMarkup(box.GetValueOrDefault("quest-line", string.Empty));
            }

            var window = page.EventWindow;
            if (!festivals.TryGetValue(quest.Festival, out var existing))
            {
                festivals[quest.Festival] = existing = new FestivalWindow(quest.Festival, eventName, window?.Start ?? string.Empty, window?.End ?? string.Empty, page.Url, null, []);
            }

            existing.QuestRowIds.Add(quest.RowId);
            if (existing.EventName.Length == 0 && eventName.Length > 0)
            {
                festivals[quest.Festival] = existing = existing with { EventName = eventName };
            }

            if (existing.End.Length == 0 && window is { } w && w.End.Length > 0)
            {
                festivals[quest.Festival] = existing = existing with { Start = w.Start, End = w.End, WikiUrl = page.Url };
            }

            if (eventName.Length > 0 && !eventTitles.ContainsKey(quest.Festival))
            {
                eventTitles[quest.Festival] = eventName;
            }
        }

        if (eventTitles.Count == 0)
        {
            return;
        }

        // The event page usually cites the Lodestone announcement and carries the dates when the quest pages do not.
        var pages = await wiki.GetPagesAsync(eventTitles.Values, ct);
        foreach (var (festivalId, title) in eventTitles)
        {
            if (!pages.TryGetValue(Names.WikiTitle(title), out var page) || page.Missing)
            {
                continue;
            }

            var f = festivals[festivalId];
            var announcement = LodestoneAnnouncement().Match(page.Text);
            if (announcement.Success)
            {
                f = f with { LodestoneUrl = announcement.Value };
            }

            if (f.End.Length == 0)
            {
                var box = WikiSource.Infobox(page.Text, "Seasonal event infobox");
                if (box.Count == 0)
                {
                    box = WikiSource.Infobox(page.Text, "Event infobox");
                }

                var end = WikiSource.StripMarkup(box.GetValueOrDefault("enddate", box.GetValueOrDefault("end", string.Empty)));
                var start = WikiSource.StripMarkup(box.GetValueOrDefault("startdate", box.GetValueOrDefault("start", string.Empty)));
                if (end.Length > 0)
                {
                    f = f with { Start = start, End = end, WikiUrl = page.Url };
                }
            }

            festivals[festivalId] = f;
        }

        log.WriteLine($"festivals: {festivals.Count} festival ids seen, {festivals.Values.Count(f => f.End.Length > 0)} with an end date, {festivals.Values.Count(f => f.LodestoneUrl is not null)} with a Lodestone announcement URL");
    }

    // ---- garland -----------------------------------------------------------------------------------------------

    private async Task GarlandPassAsync(IReadOnlyList<QuestRecord> selection, CancellationToken ct)
    {
        foreach (var quest in selection)
        {
            if (curatedDutyUnlocks.ContainsKey(quest.RowId))
            {
                garlandByRow[quest.RowId] = await garland.GetAsync(quest.RowId, ct);
            }
        }

        if (garlandByRow.Count > 0)
        {
            log.WriteLine($"garland: {garlandByRow.Count} curated duty-unlock quests fetched");
        }
    }

    // ---- comparison --------------------------------------------------------------------------------------------

    /// <param name="Settle">1.22.0: the verdict a disagreement takes when no other source decides it (it would otherwise
    /// stay unresolved), with <paramref name="SettleReason"/>: a wiki prerequisite the game cannot be checking
    /// (sourceWrong), a wiki duty a curated game gate models (notModeled).</param>
    private sealed record Draft(string Fact, string Catalog, string Source, string SourceValue, string SourceRef, Verdict Verdict, string Reason, string FixedIn = "", bool Disagree = false, Verdict? Settle = null, string? SettleReason = null);

    private List<(QuestRecord Quest, Draft Draft)> Compare(QuestRecord quest)
    {
        var drafts = new List<Draft>();
        var sheet = game.SheetRecord(quest);
        var extras = game.ExtrasOf(quest.RowId);
        var displayLevel = GameCatalog.DisplayLevel(quest);
        var prereqNames = quest.PreviousQuests.QuestIds.Select(id => game.Catalog.GetByRowId(id)?.Name ?? $"row {id}").ToList();
        var acceptQuests = AcceptQuests(quest);
        var curatedQuests = acceptQuests.Where(id => game.Catalog.ExtraPrerequisitesOf(quest.RowId).Contains(id) && !quest.AcceptConditions.Contains(id)).ToList();
        var conditionQuests = acceptQuests.Except(curatedQuests).ToList();
        var prereqCatalog = string.Join(' ', new[]
        {
            quest.PreviousQuests.IsEmpty ? string.Empty : (quest.PreviousQuests.Join == JoinKind.Any ? "any:" : "all:") + Names.Join(quest.PreviousQuests.QuestIds),
            conditionQuests.Count > 0 ? "accept:" + Names.Join(conditionQuests) : string.Empty,
            curatedQuests.Count > 0 ? "extra:" + Names.Join(curatedQuests) : string.Empty,
        }.Where(part => part.Length > 0));
        var dutyCatalog = (quest.InstanceContentRequired.Length > 1 ? (quest.InstanceJoin == JoinKind.Any ? "any:" : "all:") : string.Empty) + Names.Join(extras.InstanceContentNames);
        var rewardNames = quest.Rewards.Where(r => r.Kind is RewardKind.Item or RewardKind.OptionalItem or RewardKind.ArtifactGear).Select(r => r.Name).ToList();
        var unlockNames = DutyUnlocksOf(quest);
        var unlockCatalog = Names.Join(unlockNames);

        // ---- Lodestone
        var lodestoneRef = string.Empty;
        LodestonePage? page = null;
        if (sheet.IsUnlisted || sheet.Journal.SectionId == 255)
        {
            drafts.Add(new Draft(Facts.Listed, "unlisted", SourceNames.Lodestone, string.Empty, LodestoneSource.Base, Verdict.NotModeled,
                "journal genre 0; the Lodestone lists journal categories only" + (quest.RefiledFrom != 0 && !quest.IsRemoved ? $" (the plugin files it under {quest.Journal.CategoryName}, refiling rule {quest.RefiledFrom})" : string.Empty)));
        }
        else if (listingByRow.TryGetValue(quest.RowId, out var listing))
        {
            lodestoneRef = LodestoneSource.PageUrl(listing.LodestoneId);
            pagesByRow.TryGetValue(quest.RowId, out page);
            if (page is null)
            {
                drafts.Add(new Draft(Facts.Listed, "listed", SourceNames.Lodestone, listing.LodestoneId, lodestoneRef, Verdict.Unresolved, "listed, but the quest page was not fetched (offline or host gave up)"));
            }
            else if (!page.Parsed)
            {
                drafts.Add(new Draft(Facts.Listed, "listed", SourceNames.Lodestone, listing.LodestoneId, lodestoneRef, Verdict.Unresolved, "quest page did not parse (layout drift or error page)"));
                page = null;
            }
            else if (quest.IsRetired)
            {
                drafts.Add(new Draft(Facts.Listed, "retired", SourceNames.Lodestone, listing.LodestoneId, lodestoneRef, Verdict.SourceLagging,
                    $"the catalog retires this row (rule {quest.RefiledFrom}: {(quest.RefiledFrom == 1 ? "placeholder issuer or hidden flag in the sheet" : "curated/retired_quests.json")}); the Lodestone database still lists its page"));
            }
            else
            {
                drafts.Add(new Draft(Facts.Listed, "listed", SourceNames.Lodestone, listing.LodestoneId, lodestoneRef, Verdict.Match, twinNotes.GetValueOrDefault(quest.RowId, string.Empty)));
            }
        }
        else
        {
            var (_, total, url) = listings.GetValueOrDefault((sheet.Journal.SectionId, sheet.Journal.CategoryId));
            var catalogCount = game.SheetRecords.Values.Count(r => r.Journal.CategoryId == sheet.Journal.CategoryId);
            if (lodestoneAmbiguity.TryGetValue(quest.RowId, out var why))
            {
                drafts.Add(new Draft(Facts.Listed, "listed", SourceNames.Lodestone, string.Empty, url ?? LodestoneSource.Base, Verdict.Ambiguous, why));
            }
            else if (listings.TryGetValue((sheet.Journal.SectionId, sheet.Journal.CategoryId), out var l) && l.Rows is null)
            {
                drafts.Add(new Draft(Facts.Listed, "listed", SourceNames.Lodestone, string.Empty, l.Url, Verdict.Unresolved, "category listing could not be fetched"));
            }
            else if (quest.IsRetired)
            {
                drafts.Add(new Draft(Facts.Listed, "retired", SourceNames.Lodestone, "not listed", url ?? LodestoneSource.Base, Verdict.Match,
                    $"the catalog retires this row (rule {quest.RefiledFrom}) and the Lodestone listing for section {sheet.Journal.SectionId} category {sheet.Journal.CategoryId} omits it"));
            }
            else if (wikiByRow.TryGetValue(quest.RowId, out var retiredPage) && retiredPage.RetiredPatch is { } retiredIn)
            {
                drafts.Add(new Draft(Facts.Listed, "listed", SourceNames.Lodestone, "not listed", url ?? LodestoneSource.Base, Verdict.NotListed,
                    $"not in the Lodestone listing for section {sheet.Journal.SectionId} category {sheet.Journal.CategoryId}; the wiki marks the quest retired in patch {retiredIn} (see the retired row)"));
            }
            else
            {
                var maxMapped = MaxMappedRowInCategory(sheet.Journal.CategoryId);
                var lag = quest.RowId > maxMapped && maxMapped > 0;
                drafts.Add(new Draft(Facts.Listed, "listed", SourceNames.Lodestone, string.Empty, url ?? LodestoneSource.Base, lag ? Verdict.SourceLagging : Verdict.NotListed,
                    lag
                        ? $"newer than the Lodestone's newest listed row in this category ({maxMapped}); Lodestone lists {total} of the catalog's {catalogCount}"
                        : $"not in the Lodestone listing for section {sheet.Journal.SectionId} category {sheet.Journal.CategoryId} ({total} listed, catalog {catalogCount})"));
            }
        }

        if (page is not null)
        {
            drafts.Add(Compare(Facts.Name, quest.Name, SourceNames.Lodestone, page.Name, lodestoneRef, Names.Canon(quest.Name) == Names.Canon(page.Name)));
            drafts.Add(Compare(Facts.DisplayLevel, displayLevel.ToString(), SourceNames.Lodestone, page.Level.ToString(), lodestoneRef, page.Level == displayLevel));

            if (page.ClassJobLevel >= 0)
            {
                drafts.Add(Compare(Facts.RawLevel, quest.Level.ToString(), SourceNames.Lodestone, page.ClassJobLevel.ToString(), lodestoneRef, page.ClassJobLevel == quest.Level));
            }
            else
            {
                drafts.Add(new Draft(Facts.RawLevel, quest.Level.ToString(), SourceNames.Lodestone, string.Empty, lodestoneRef, Verdict.NotModeled, "no Class/Job level on the page"));
            }

            // The Lodestone shows a required class ("Botanist Lv. 5") in the Class/Job line and reserves "Starting Class" for the city-start variants.
            var classShownAsRequired = extras.ClassJobRequiredName.Length > 0
                && (Names.Canon(page.ClassJobText) == Names.Canon(extras.ClassJobRequiredName) || Names.Canon(page.ClassJobText) == Names.Canon(extras.ClassJobRequiredAbbreviation));
            if (page.ClassJobText.Length > 0)
            {
                var classOk = SameClassCategory(page.ClassJobText, extras.ClassJobCategoryName) || classShownAsRequired;
                drafts.Add(Compare(Facts.ClassJob, extras.ClassJobCategoryName, SourceNames.Lodestone, page.ClassJobText, lodestoneRef, classOk, classShownAsRequired ? "Lodestone shows the required class (ClassJobRequired) instead of the category" : string.Empty));
            }
            else
            {
                drafts.Add(new Draft(Facts.ClassJob, extras.ClassJobCategoryName, SourceNames.Lodestone, string.Empty, lodestoneRef, Verdict.NotModeled, "no Class/Job line on the page"));
            }

            // City-start MSQ variants carry no ClassJobRequired; their single-class ClassJobCategory ("LNC") is what the Lodestone prints as Starting Class.
            var startIsCategory = page.StartingClass.Length > 0 && extras.ClassJobRequiredName.Length == 0
                && Names.Canon(page.StartingClass) == Names.Canon(SingleClassOf(extras.ClassJobCategoryName));
            var startOk = Names.Canon(page.StartingClass) == Names.Canon(extras.ClassJobRequiredName) || (page.StartingClass.Length == 0 && classShownAsRequired) || startIsCategory;
            if (!startOk && page.StartingClass.Length > 0 && extras.ClassJobRequiredName.Length == 0 && SingleClassOf(extras.ClassJobCategoryName).Length == 0)
            {
                // The city-start variants (Close to Home ×3 per city) carry ClassJobCategory "All Classes" and no ClassJobRequired; the sheet has no field that names the starting class the script assigns.
                drafts.Add(new Draft(Facts.StartingClass, string.Empty, SourceNames.Lodestone, page.StartingClass, lodestoneRef, Verdict.NotModeled, $"the sheet has no starting-class field for this variant (ClassJobCategory {extras.ClassJobCategoryName}, no ClassJobRequired; the variants differ only by InternalId {quest.InternalId})"));
            }
            else
            {
                drafts.Add(Compare(Facts.StartingClass, extras.ClassJobRequiredName.Length > 0 ? extras.ClassJobRequiredName : (startIsCategory ? "(category " + extras.ClassJobCategoryName + ")" : string.Empty), SourceNames.Lodestone, page.StartingClass.Length > 0 ? page.StartingClass : (classShownAsRequired ? page.ClassJobText : string.Empty), lodestoneRef, startOk,
                    classShownAsRequired ? "required class shown in the Class/Job line" : startIsCategory ? "no ClassJobRequired; the single-class ClassJobCategory is the starting class" : string.Empty));
            }

            var gcOk = extras.GrandCompanyName.Length == 0
                ? page.GrandCompany.Length == 0
                : Names.Canon(page.GrandCompany).StartsWith(Names.Canon(extras.GrandCompanyName), StringComparison.Ordinal);
            drafts.Add(Compare(Facts.GrandCompany, extras.GrandCompanyName, SourceNames.Lodestone, page.GrandCompany, lodestoneRef, gcOk));

            var genreOk = Names.Canon(page.ContentType) == Names.Canon(sheet.Journal.GenreName) || Names.Canon(page.ContentType) == Names.Canon(sheet.Journal.CategoryName);
            drafts.Add(Compare(Facts.Genre, sheet.Journal.GenreName, SourceNames.Lodestone, page.ContentType, lodestoneRef, genreOk));

            var listing = listingByRow[quest.RowId];
            var sectionOk = listing.SectionId == sheet.Journal.SectionId && listing.CategoryId == sheet.Journal.CategoryId;
            drafts.Add(Compare(Facts.Section, $"{sheet.Journal.SectionId}/{sheet.Journal.CategoryId} {sheet.Journal.SectionName} > {sheet.Journal.CategoryName}", SourceNames.Lodestone,
                $"{listing.SectionId}/{listing.CategoryId} {game.SectionNames.GetValueOrDefault(listing.SectionId, string.Empty)} > {game.CategoryNames.GetValueOrDefault(listing.CategoryId, string.Empty)}", lodestoneRef, sectionOk));

            // Quest/Duty: entries that are duty names compare with InstanceContent; anything else is a quest name.
            var dutyLines = page.QuestDuty.Where(l => cfcNames.Contains(l)).ToList();
            var questLines = page.QuestDuty.Where(l => !cfcNames.Contains(l)).ToList();
            var dutySource = (dutyLines.Count > 1 ? (page.AllOfTheAbove ? "all:" : "any:") : string.Empty) + Names.Join(dutyLines);
            var dutiesOk = Names.SameSet(dutyLines, extras.InstanceContentNames) && (dutyLines.Count <= 1 || (page.AllOfTheAbove == (quest.InstanceJoin == JoinKind.All)));
            drafts.Add(Compare(Facts.Duties, dutyCatalog, SourceNames.Lodestone, dutySource, lodestoneRef, dutiesOk));

            if (questLines.Count == 0)
            {
                // The Lodestone's Quest/Duty line names duties only, so an empty line cannot confirm the absence of a quest prerequisite either.
                drafts.Add(new Draft(Facts.Prereqs, prereqCatalog, SourceNames.Lodestone, string.Empty, lodestoneRef, Verdict.NotModeled, "Lodestone Quest/Duty names duties only"));
            }
            else
            {
                drafts.Add(Compare(Facts.Prereqs, prereqCatalog, SourceNames.Lodestone, Names.Join(questLines), lodestoneRef, PrereqsConsistent(quest, questLines, out var why), why));
            }

            var pageRewards = page.Rewards.Concat(page.OptionalRewards).Where(r => !IsCrystal(r)).ToList();
            drafts.Add(Compare(Facts.Rewards, Names.Join(rewardNames), SourceNames.Lodestone, Names.Join(pageRewards), lodestoneRef, RewardsConsistent(rewardNames, pageRewards, out var rewardWhy), rewardWhy));
        }

        // ---- wiki
        if (wikiByRow.TryGetValue(quest.RowId, out var wikiPage))
        {
            var box = wikiPage.QuestInfobox;
            var wref = wikiPage.Url;
            drafts.Add(new Draft(Facts.Listed, ListedValue(quest), SourceNames.Wiki, wikiPage.Title, wref, Verdict.Match, box.ContainsKey("id-gt") ? "matched by id-gt" : "matched by title"));

            var title = WikiSource.StripMarkup(box.GetValueOrDefault("title", wikiPage.Title));
            var baseTitle = BaseName(title);
            var baseName = BaseName(quest.Name);
            var nameOk = Names.Canon(quest.Name) == Names.Canon(title) || Names.Canon(quest.Name) == Names.Canon(baseTitle) || Names.Canon(baseName) == Names.Canon(title) || Names.Canon(baseName) == Names.Canon(baseTitle);
            drafts.Add(Compare(Facts.Name, quest.Name, SourceNames.Wiki, title, wref, nameOk, Names.Canon(quest.Name) == Names.Canon(title) ? string.Empty : "matched without the parenthetical variant suffix"));

            var levelText = Names.Clean(box.GetValueOrDefault("level", string.Empty));
            if (int.TryParse(levelText, out var wikiLevel))
            {
                var gateLevel = quest.PreviousQuests.QuestIds.Select(game.Catalog.GetByRowId).OfType<QuestRecord>().Select(GameCatalog.DisplayLevel).DefaultIfEmpty(0).Max();
                if (wikiLevel > displayLevel && wikiLevel == gateLevel)
                {
                    drafts.Add(new Draft(Facts.DisplayLevel, displayLevel.ToString(), SourceNames.Wiki, wikiLevel.ToString(), wref, Verdict.NotModeled,
                        $"the wiki quotes the level of the prerequisite that gates the quest (Lv {gateLevel}); the sheet's own level is {displayLevel}, and the plugin gates on the prerequisite"));
                }
                else
                {
                    drafts.Add(Compare(Facts.DisplayLevel, displayLevel.ToString(), SourceNames.Wiki, wikiLevel.ToString(), wref, wikiLevel == displayLevel));
                }
            }
            else
            {
                drafts.Add(new Draft(Facts.DisplayLevel, displayLevel.ToString(), SourceNames.Wiki, levelText, wref, Verdict.NotModeled, "no numeric level in the infobox"));
            }

            var wikiSection = WikiSource.StripMarkup(box.GetValueOrDefault("journal-section", string.Empty));
            var wikiCategory = WikiSource.StripMarkup(box.GetValueOrDefault("journal-category", string.Empty));
            if (wikiCategory.Length == 0 && wikiSection.Length == 0)
            {
                drafts.Add(new Draft(Facts.Section, $"{sheet.Journal.SectionName} > {sheet.Journal.CategoryName}", SourceNames.Wiki, string.Empty, wref, Verdict.NotModeled, "no journal fields in the infobox"));
            }
            else if (sheet.IsUnlisted)
            {
                drafts.Add(new Draft(Facts.Section, "unlisted", SourceNames.Wiki, $"{wikiSection} > {wikiCategory}", wref, Verdict.NotModeled,
                    "the sheet gives this row no journal genre" + (quest.RefiledFrom != 0 && !quest.IsRemoved ? $"; the plugin files it under {quest.Journal.SectionName} > {quest.Journal.CategoryName} (refiling rule {quest.RefiledFrom})" : string.Empty)));
            }
            else
            {
                var catOk = wikiCategory.Length == 0 || SameJournalName(wikiCategory, sheet.Journal.CategoryName);
                var secOk = wikiSection.Length == 0 || SameJournalName(wikiSection, sheet.Journal.SectionName);
                drafts.Add(Compare(Facts.Section, $"{sheet.Journal.SectionName} > {sheet.Journal.CategoryName}", SourceNames.Wiki, $"{wikiSection} > {wikiCategory}", wref, catOk && secOk));
            }

            var expansion = WikiSource.ExpansionOf(box.GetValueOrDefault("release"), box.GetValueOrDefault("patch"));
            if (expansion < 0)
            {
                drafts.Add(new Draft(Facts.Expansion, $"{quest.Expansion} {extras.ExpansionName}", SourceNames.Wiki, string.Empty, wref, Verdict.NotModeled, "no release or patch in the infobox"));
            }
            else
            {
                var wikiExpansion = $"{expansion} ({Names.Clean(box.GetValueOrDefault("release", string.Empty))} / patch {Names.Clean(box.GetValueOrDefault("patch", string.Empty))})";
                if (expansion > quest.Expansion)
                {
                    // The sheet's Expansion column is the era of the quest's zone and journal home (seasonal quests in the
                    // ARR cities, Blue Mage quests, patch-added sidequests in old zones keep the older value); the wiki
                    // records the release patch. The plugin files by the sheet's value, so the two are different facts.
                    drafts.Add(new Draft(Facts.Expansion, $"{quest.Expansion} {extras.ExpansionName}", SourceNames.Wiki, wikiExpansion, wref, Verdict.NotModeled,
                        "wiki records the release patch; the sheet's Expansion column is the content era of the quest's zone, which the plugin files by"));
                }
                else
                {
                    drafts.Add(Compare(Facts.Expansion, $"{quest.Expansion} {extras.ExpansionName}", SourceNames.Wiki, wikiExpansion, wref, expansion == quest.Expansion));
                }
            }

            var wikiPrereqs = WikiSource.SplitNames(box.GetValueOrDefault("prev-quest")).Concat(WikiSource.SplitNames(box.GetValueOrDefault("req-quest"))).ToList();
            if (wikiPrereqs.Count == 0 && (!quest.PreviousQuests.IsEmpty || acceptQuests.Count > 0))
            {
                drafts.Add(new Draft(Facts.Prereqs, prereqCatalog, SourceNames.Wiki, string.Empty, wref, Verdict.NotModeled, "infobox prev-quest/req-quest not filled in"));
            }
            else
            {
                drafts.Add(SharedPage(quest, Compare(Facts.Prereqs, prereqCatalog, SourceNames.Wiki, Names.Join(wikiPrereqs), wref, PrereqsConsistent(quest, wikiPrereqs, out var prereqWhy), prereqWhy),
                    sibling => PrereqsConsistent(sibling, wikiPrereqs, out _) == Consistency.Agree));
            }

            var wikiDuties = WikiSource.LinkedNames(box.GetValueOrDefault("requirements")).Where(n => cfcNames.Contains(n)).ToList();
            if (wikiDuties.Count > 0 || extras.InstanceContentNames.Count > 0)
            {
                var dutyDraft = Compare(Facts.Duties, dutyCatalog, SourceNames.Wiki, Names.Join(wikiDuties), wref, Names.SameSet(wikiDuties, extras.InstanceContentNames));
                drafts.Add(dutyDraft.Disagree && DutiesAgainstGate(quest, extras.InstanceContentNames, wikiDuties) is { } gateReason
                    ? dutyDraft with { Settle = Verdict.NotModeled, SettleReason = gateReason }
                    : dutyDraft);
            }

            var wikiRewards = new List<string>();
            foreach (var (key, value) in box)
            {
                if (key.StartsWith("reward", StringComparison.Ordinal) && !key.StartsWith("overridereward", StringComparison.Ordinal) && !key.Contains('('))
                {
                    foreach (var part in WikiSource.SplitNames(value))
                    {
                        var m = LeadingCount().Match(part);
                        var name = m.Success ? m.Groups[1].Value : part;
                        if (!IsCrystal(name))
                        {
                            wikiRewards.Add(name);
                        }
                    }
                }
            }

            drafts.Add(SharedPage(quest, Compare(Facts.Rewards, Names.Join(rewardNames), SourceNames.Wiki, Names.Join(wikiRewards), wref, RewardsConsistent(rewardNames, wikiRewards, out var wikiRewardWhy), wikiRewardWhy),
                sibling => RewardsConsistent(sibling.Rewards.Where(r => r.Kind is RewardKind.Item or RewardKind.OptionalItem or RewardKind.ArtifactGear).Select(r => r.Name).ToList(), wikiRewards, out _)));

            var wikiUnlocks = WikiSource.Unlocks(box.GetValueOrDefault("unlocks")).Where(u => WikiSource.DutyCodes.Contains(u.Code)).Select(u => u.Name).ToList();
            if (wikiUnlocks.Count > 0 || unlockNames.Count > 0)
            {
                drafts.Add(Compare(Facts.DutyUnlock, unlockCatalog, SourceNames.Wiki, Names.Join(wikiUnlocks), wref, DutyUnlockConsistent(quest, unlockNames, wikiUnlocks, out var unlockWhy), unlockWhy));
            }

            var retired = wikiPage.RetiredPatch;
            if (retired is null && quest.IsRetired)
            {
                drafts.Add(new Draft(Facts.Retired, "retired", SourceNames.Wiki, "live", wref, Verdict.Unresolved,
                    $"the catalog retires this row (rule {quest.RefiledFrom}); the wiki does not mark it removed", Disagree: true));
            }
            else if (retired is null)
            {
                drafts.Add(new Draft(Facts.Retired, ListedValue(quest), SourceNames.Wiki, "live", wref, Verdict.Match, string.Empty));
            }
            else if (quest.IsRetired)
            {
                drafts.Add(new Draft(Facts.Retired, "retired", SourceNames.Wiki, "retired " + retired, wref, Verdict.Match, $"retired in the catalog (rule {quest.RefiledFrom}) and on the wiki"));
            }
            else if (quest.IsUnlisted)
            {
                drafts.Add(new Draft(Facts.Retired, "unlisted", SourceNames.Wiki, "retired " + retired, wref, Verdict.Match, "retired on the wiki and unlisted in the sheet"));
            }
            else
            {
                drafts.Add(listingByRow.ContainsKey(quest.RowId)
                    ? new Draft(Facts.Retired, "live", SourceNames.Wiki, "retired " + retired, wref, Verdict.Unresolved,
                        $"wiki marks the quest retired in patch {retired}, but the Lodestone still lists it; the catalog lists it under {quest.Journal.CategoryName}", Disagree: true)
                    : new Draft(Facts.Retired, "live", SourceNames.Wiki, "retired " + retired, wref, Verdict.CatalogWrong,
                        $"wiki marks the quest retired in patch {retired} and the Lodestone listing omits it; the catalog still lists it under {quest.Journal.CategoryName}", "curated/retired_quests.json"));
            }
        }
        else if (wikiAmbiguity.TryGetValue(quest.RowId, out var ambiguous))
        {
            drafts.Add(new Draft(Facts.Listed, ListedValue(quest), SourceNames.Wiki, string.Empty, WikiSource.PageUrl(quest.Name), Verdict.Ambiguous, ambiguous));
        }
        else
        {
            drafts.Add(new Draft(Facts.Listed, ListedValue(quest), SourceNames.Wiki, string.Empty, WikiSource.PageUrl(quest.Name), Verdict.NotListed, wikiMissing.GetValueOrDefault(quest.RowId, "no wiki page")));
        }

        // ---- garland (curated duty unlocks only)
        if (curatedDutyUnlocks.TryGetValue(quest.RowId, out var cfcIds))
        {
            var curatedNames = cfcIds.Select(id => game.ContentFinderConditionNames.GetValueOrDefault(id, $"CFC {id}")).ToList();
            var gref = GarlandSource.SiteUrl(quest.RowId);
            garlandByRow.TryGetValue(quest.RowId, out var g);
            if (g is null)
            {
                drafts.Add(new Draft(Facts.DutyUnlock, Names.Join(curatedNames), SourceNames.Garland, string.Empty, gref, Verdict.Unresolved, "Garland document not fetched"));
            }
            else if (!g.Found)
            {
                drafts.Add(new Draft(Facts.DutyUnlock, Names.Join(curatedNames), SourceNames.Garland, string.Empty, gref, Verdict.NotListed, "no Garland document"));
            }
            else if (g.InstanceId == 0)
            {
                drafts.Add(new Draft(Facts.DutyUnlock, Names.Join(curatedNames), SourceNames.Garland, string.Empty, gref, Verdict.NotModeled, "Garland has no reward.instance for this quest (the sheet has no InstanceContentUnlock; the unlock is script-driven)"));
            }
            else
            {
                var ok = curatedNames.Any(n => DutyNames.Same(n, g.InstanceName));
                if (!ok && WikiDutyUnlocksOf(quest.RowId).Any(w => DutyNames.Same(w, g.InstanceName)))
                {
                    drafts.Add(new Draft(Facts.DutyUnlock, Names.Join(curatedNames), SourceNames.Garland, g.InstanceName, gref, Verdict.NotModeled,
                        $"Garland reward.instance {g.InstanceId} and the wiki both name {g.InstanceName} as a further unlock of this quest; curated/duty_unlocks.json lists only {Names.Join(curatedNames)} (candidate addition for duty_unlocks.json)"));
                }
                else
                {
                    drafts.Add(Compare(Facts.DutyUnlock, Names.Join(curatedNames), SourceNames.Garland, g.InstanceName, gref, ok, ok ? string.Empty : $"Garland reward.instance {g.InstanceId} = {g.InstanceName}"));
                }
            }
        }

        if (quest.IsRemoved)
        {
            // Unlisted and retired (hidden, removed, legacy) rows: only identity facts are compared, as qa-data-engineer §1.4 asks; the rest would only pollute the counts.
            drafts = drafts.Select(d => d.Fact is Facts.Listed or Facts.Name or Facts.DisplayLevel or Facts.Retired || !d.Disagree
                ? d
                : d with { Verdict = Verdict.NotModeled, Disagree = false, Reason = (quest.IsRetired ? "retired row" : "unlisted row") + "; only identity facts are compared (" + d.Reason + ")" }).ToList();
        }
        else if (wikiByRow.TryGetValue(quest.RowId, out var stubPage) && stubPage.RetiredPatch is { } stubRetiredIn)
        {
            // A row the wiki marks retired but the sheet still files: the sheet keeps a stub (rewards and duties stripped). The retired row carries the finding.
            drafts = drafts.Select(d => d.Fact is Facts.Listed or Facts.Name or Facts.DisplayLevel or Facts.Retired || !d.Disagree
                ? d
                : d with { Verdict = Verdict.NotModeled, Disagree = false, Reason = $"retired per the wiki (patch {stubRetiredIn}); the sheet row is a stub, see the retired row (" + d.Reason + ")" }).ToList();
        }

        return drafts.Select(d => (quest, d)).ToList();
    }

    private static Draft Compare(string fact, string catalog, string source, string sourceValue, string sourceRef, bool ok, string reason = "")
        => ok
            ? new Draft(fact, catalog, source, sourceValue, sourceRef, Verdict.Match, reason)
            : new Draft(fact, catalog, source, sourceValue, sourceRef, Verdict.Unresolved, reason.Length > 0 ? reason : "source disagrees with the catalog", Disagree: true);

    /// <summary>
    /// The wiki keeps one page for same-name variants (the start-as-archer and join-as-archer "Way of the Archer") and
    /// its <c>id-gt</c> names one of them. A disagreement on such a page whose value fits a sibling row is the sibling's
    /// fact, not a finding against this row: ambiguous, with the sibling named.
    /// </summary>
    private Draft SharedPage(QuestRecord quest, Draft draft, Func<QuestRecord, bool> siblingAgrees)
    {
        if (!draft.Disagree)
        {
            return draft;
        }

        var siblings = rowsByName.GetValueOrDefault(Names.Canon(quest.Name), []).Where(id => id != quest.RowId).Select(game.Catalog.GetByRowId).OfType<QuestRecord>().ToList();
        var fits = siblings.FirstOrDefault(siblingAgrees);
        return fits is null
            ? draft
            : draft with { Verdict = Verdict.Ambiguous, Disagree = false, Reason = $"wiki page is shared by {siblings.Count + 1} rows of this name; its value fits row {fits.RowId} ({fits.InternalId}), not this one ({quest.InternalId})" };
    }

    /// <summary>Outcome of a rule: the source agrees, disagrees, or describes something the sheet expresses differently (reported as notModeled with the reason).</summary>
    private enum Consistency
    {
        Agree,
        Disagree,
        NotModeled,

        /// <summary>The source contradicts the sheet in a way the sheet itself rules out (a successor named as a prerequisite).</summary>
        SourceWrong,

        /// <summary>
        /// 1.22.0: a disagreement the game's data settles when nothing else does: the source names quests neither the sheet
        /// nor the quest's script constants name (<see cref="ScriptNamesNone"/>). Still a disagreement first, so a shared
        /// wiki page and a second source decide before it.
        /// </summary>
        DisagreeScriptSilent,
    }

    private static Draft Compare(string fact, string catalog, string source, string sourceValue, string sourceRef, Consistency c, string reason = "")
        => c switch
        {
            Consistency.NotModeled => new Draft(fact, catalog, source, sourceValue, sourceRef, Verdict.NotModeled, reason),
            Consistency.SourceWrong => new Draft(fact, catalog, source, sourceValue, sourceRef, Verdict.SourceWrong, reason),
            Consistency.DisagreeScriptSilent => Compare(fact, catalog, source, sourceValue, sourceRef, false, reason) with { Settle = Verdict.SourceWrong, SettleReason = reason },
            _ => Compare(fact, catalog, source, sourceValue, sourceRef, c == Consistency.Agree, reason),
        };

    private static bool SameJournalName(string wikiName, string sheetName)
    {
        var w = Names.Canon(wikiName);
        var s = Names.Canon(sheetName);
        if (w == s)
        {
            return true;
        }

        // The wiki appends "Quests" ("Seventh Umbral Era Main Scenario Quests" vs the sheet's "Seventh Umbral Era"), drops the
        // section prefix the sheet repeats ("Chronicles of a New Era - Bahamut" vs "Bahamut Quests") and spells Pandæmonium with a ligature.
        static string Strip(string x)
        {
            x = x.Replace("æ", "ae").Replace(" main scenario quests", string.Empty).Replace(" quests", string.Empty).Trim();
            var dash = x.IndexOf(" - ", StringComparison.Ordinal);
            if (dash > 0)
            {
                x = x[(dash + 3)..].Trim();
            }

            return x;
        }

        var sw = Strip(w);
        var ss = Strip(s);
        return sw == ss || sw.StartsWith(ss, StringComparison.Ordinal) || ss.StartsWith(sw, StringComparison.Ordinal);
    }

    /// <summary>Lodestone Class/Job phrasing versus the ClassJobCategory sheet name: "Any Class or Job" is "All Classes", "Any Disciple of War or Magic (excluding limited jobs)" is "Disciples of War or Magic".</summary>
    private static bool SameClassCategory(string lodestoneText, string sheetName)
    {
        static string Norm(string x)
        {
            x = Names.Canon(x).Replace("(excluding limited jobs)", string.Empty).Replace("classes and jobs", "classes").Replace("class or job", "classes").Replace("disciples", "disciple").Trim();
            if (x.StartsWith("any ", StringComparison.Ordinal) || x.StartsWith("all ", StringComparison.Ordinal))
            {
                x = x[4..];
            }

            return Regex.Replace(x, @"\s+", " ").Trim();
        }

        return Norm(lodestoneText) == Norm(sheetName);
    }

    private static string BaseName(string name) => Names.Base(name);

    /// <summary>Shards, crystals and clusters: the mapper files crystal reward slots as nameless Other rewards, so they are excluded from the item comparison on both sides (noted in verification-full.md).</summary>
    private static bool IsCrystal(string name)
    {
        var n = Names.Canon(name);
        return n.EndsWith(" shard", StringComparison.Ordinal) || n.EndsWith(" crystal", StringComparison.Ordinal) || n.EndsWith(" cluster", StringComparison.Ordinal);
    }

    /// <summary>
    /// Canonical catalog names a source's quest name may denote: as written; through a wiki alias (a renamed or
    /// special-character title whose page names the row); without the wiki's disambiguation suffix ("Close to Home
    /// (Gridania)", "Brotherhood of Ash (Quest)"); as the base of several variant rows ("A Pup No Longer" for the three
    /// Grand Company rows); or by loose key ("Α Test of Wιll", "Best-laid Schemes"). Empty when nothing in the catalog matches.
    /// </summary>
    private List<string> ResolveNames(string name)
    {
        var c = Names.Canon(name);
        if (rowsByName.ContainsKey(c))
        {
            return [c];
        }

        if (nameAliases.TryGetValue(c, out var alias))
        {
            return [alias];
        }

        var b = Names.Canon(Names.Base(name));
        if (rowsByName.ContainsKey(b))
        {
            return [b];
        }

        if (rowsByBaseName.TryGetValue(b, out var variants))
        {
            return variants.Select(id => Names.Canon(game.Catalog.GetByRowId(id)?.Name ?? string.Empty)).Where(n => n.Length > 0).Distinct().ToList();
        }

        var a = Names.LooseKey(name);
        if (a.Length > 0 && rowsByLooseName.TryGetValue(a, out var folded))
        {
            return folded.Select(id => Names.Canon(game.Catalog.GetByRowId(id)?.Name ?? string.Empty)).Where(n => n.Length > 0).Distinct().ToList();
        }

        return [];
    }

    private HashSet<uint>? msqSections;

    /// <summary>Journal sections "Main Scenario (A Realm Reborn through Endwalker)" and "Main Scenario (Dawntrail)".</summary>
    private bool IsMainScenario(QuestRecord q)
    {
        msqSections ??= game.SectionNames.Where(kv => Names.Canon(kv.Value).StartsWith("main scenario", StringComparison.Ordinal)).Select(kv => kv.Key).ToHashSet();
        return msqSections.Contains(q.Journal.SectionId) && !q.IsRemoved;
    }

    /// <summary>The catalog's filing state as the listed/retired rows print it.</summary>
    private static string ListedValue(QuestRecord q) => q.IsRetired ? "retired" : q.IsUnlisted ? "unlisted" : "listed";

    /// <summary>
    /// Quest rows named by QuestAcceptAdditionCondition or by <c>curated/extra_prerequisites.json</c> that the evaluator
    /// judges on top of PreviousQuest: exactly what <see cref="QuestCatalog.PrerequisitesOf"/> adds, so a source naming
    /// one is a match only for a prerequisite the plugin checks. A value that is no catalog quest stays out (listed,
    /// not checked), as does one PreviousQuest already names.
    /// </summary>
    private List<uint> AcceptQuests(QuestRecord q) => game.Catalog.PrerequisitesOf(q).QuestIds.Except(q.PreviousQuests.QuestIds).ToList();

    /// <summary>Every duty the catalog says a quest unlocks: the sheet's InstanceContentUnlock, the ContentFinderCondition unlock criteria and curated/duty_unlocks.json (all three reach unique_quests.json).</summary>
    private List<string> DutyUnlocksOf(QuestRecord q)
    {
        var names = q.Rewards.Where(r => r.Kind == RewardKind.Instance && r.Name.Length > 0).Select(r => r.Name).ToList();
        names.AddRange(uniqueEntries.Where(e => e.QuestRowId == q.RowId && e.Kind == RewardKind.DutyUnlock).Select(e => e.RewardName));
        if (curatedDutyUnlocks.TryGetValue(q.RowId, out var cfcIds))
        {
            names.AddRange(cfcIds.Select(id => game.ContentFinderConditionNames.GetValueOrDefault(id, string.Empty)).Where(n => n.Length > 0));
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// 1.22.0: the wiki's required duties against the sheet's, where a curated game gate (<c>curated/game_gates.json</c>)
    /// models the difference. When every duty the wiki names beyond the sheet's appears in the quest's gate phrase
    /// ("three unique final bosses of the Merchant's Tale defeated") and the wiki names every duty the sheet requires,
    /// the wiki is describing that gate, which the gates fact and the catalog carry: the reason for a notModeled row.
    /// Null when the sets agree, when no gate names the extra duties, or when the sheet requires a duty the wiki leaves out.
    /// </summary>
    private string? DutiesAgainstGate(QuestRecord quest, IReadOnlyList<string> sheetDuties, IReadOnlyList<string> wikiDuties)
    {
        if (Names.SameSet(wikiDuties, sheetDuties) || game.Catalog.GameGateOf(quest.RowId) is not { } gate)
        {
            return null;
        }

        var sheet = sheetDuties.Select(Names.Canon).ToHashSet();
        var wiki = wikiDuties.Select(Names.Canon).ToHashSet();
        var extra = wikiDuties.Where(d => !sheet.Contains(Names.Canon(d))).ToList();
        var phrase = Names.Canon(gate.Gate);
        if (extra.Count == 0 || !sheet.All(wiki.Contains) || !extra.All(d => phrase.Contains(Names.Canon(d), StringComparison.Ordinal)))
        {
            return null;
        }

        return $"the wiki names {string.Join("; ", extra)} as the curated game gate (curated/game_gates.json: {gate.Gate}), not a duty the sheet requires";
    }

    /// <summary>
    /// 1.22.0, the game's data wins over a wiki-only prerequisite: the quests a source names that neither the sheet
    /// (previous quests, accept conditions, through any ancestor) requires nor the quest's script constants
    /// (<c>Quest.QuestParams</c>, where a script names every quest it checks) name. Null when any of them is named by the
    /// script, or a name resolves to no quest: then the finding stays open.
    /// </summary>
    private string? ScriptNamesNone(QuestRecord quest, IReadOnlyList<string> names)
    {
        var constants = game.ScriptConstants(quest.RowId);
        foreach (var name in names)
        {
            var rows = ResolveNames(name).SelectMany(r => rowsByName.GetValueOrDefault(r, [])).ToList();
            if (rows.Count == 0 || rows.Any(id => QuestScriptConstants.NamesQuest(constants, id)))
            {
                return null;
            }
        }

        return $"the sheet does not require {string.Join("; ", names)} and the quest's script constants (Quest.QuestParams) name none of them, so the game does not check them: the source is wrong";
    }

    /// <summary>Duties the wiki says a quest unlocks (its <c>unlocks</c> field), for script-driven unlocks the sheet does not link.</summary>
    private List<string> WikiDutyUnlocksOf(uint rowId)
        => wikiByRow.TryGetValue(rowId, out var page)
            ? WikiSource.Unlocks(page.QuestInfobox.GetValueOrDefault("unlocks")).Where(u => WikiSource.DutyCodes.Contains(u.Code)).Select(u => u.Name).ToList()
            : [];

    /// <summary>
    /// A source's prerequisite list against the sheet's PreviousQuest. Agrees when it names the same quests (All join),
    /// any subset of an Any join, or only quests that are transitive prerequisites of the catalog's set (the wiki often
    /// names the story predecessor rather than the sheet's hard requirement). Names the sheet expresses through another
    /// requirement kind (allied-society rank, a required duty's unlock quest, main-scenario area access, custom delivery
    /// unlocks) are notModeled with the reason. Anything else the catalog cannot reach is a disagreement.
    /// </summary>
    private Consistency PrereqsConsistent(QuestRecord quest, List<string> sourceNames, out string reason)
    {
        reason = string.Empty;
        var resolved = sourceNames.Select(n => (Name: n, Rows: ResolveNames(n))).ToList();
        var unknown = resolved.Where(r => r.Rows.Count == 0).Select(r => r.Name).Distinct().ToList();
        var source = resolved.Where(r => r.Rows.Count > 0).ToList();
        var accept = AcceptQuests(quest);
        var catalog = quest.PreviousQuests.QuestIds.Concat(accept).Select(id => Names.Canon(game.Catalog.GetByRowId(id)?.Name ?? string.Empty)).Where(n => n.Length > 0).Distinct().ToHashSet();
        var extras = game.ExtrasOf(quest.RowId);

        if (source.Count == 0 && unknown.Count == 0)
        {
            if (catalog.Count == 0)
            {
                return Consistency.Agree;
            }

            reason = "source lists no prerequisite; catalog has " + catalog.Count;
            return Consistency.Disagree;
        }

        if (catalog.Count == 0)
        {
            var named = string.Join("; ", sourceNames);
            if (quest.BeastTribe != 0)
            {
                reason = $"sheet gates this quest by allied-society rank ({game.Bundle.Names.Tribe(quest.BeastTribe)} rank {quest.BeastRank}), not PreviousQuest; the wiki names the story predecessor ({named})";
                return Consistency.NotModeled;
            }

            if (extras.SatisfactionNpc != 0 || extras.DeliveryQuest != 0)
            {
                reason = $"sheet gates this quest by the custom-delivery client (SatisfactionNpc {extras.SatisfactionNpc}, DeliveryQuest {extras.DeliveryQuest}; requirement kind mapped in T3), not PreviousQuest; the wiki names {named}";
                return Consistency.NotModeled;
            }

            if (quest.GrandCompany != 0)
            {
                reason = $"sheet gates this quest by Grand Company membership ({extras.GrandCompanyName}, rank {quest.GrandCompanyRank}), not PreviousQuest; the wiki names the company's story predecessor ({named})";
                return Consistency.NotModeled;
            }

            var namedRows = source.SelectMany(r => r.Rows).SelectMany(n => rowsByName.GetValueOrDefault(n, [])).Select(game.Catalog.GetByRowId).OfType<QuestRecord>().ToList();
            if (namedRows.Count > 0 && namedRows.All(IsMainScenario) && !IsMainScenario(quest))
            {
                reason = $"sheet has no PreviousQuest; the wiki names main-scenario progress ({named}) that opens the area or NPC, which the sheet does not express";
                return Consistency.NotModeled;
            }

            var systems = namedRows.Where(r => curatedSystemUnlocks.ContainsKey(r.RowId)).Select(r => curatedSystemUnlocks[r.RowId]).Distinct().ToList();
            if (namedRows.Count > 0 && namedRows.All(r => curatedSystemUnlocks.ContainsKey(r.RowId)))
            {
                reason = $"sheet has no PreviousQuest; the wiki names the quest that unlocks the system this quest builds on ({string.Join("; ", systems)}; curated/system_unlocks.json), which the sheet gates through the system";
                return Consistency.NotModeled;
            }

            if (game.Curated.Quirks.ContainsKey(quest.RowId))
            {
                reason = $"sheet has no PreviousQuest; the wiki names {named}, which curated/quirks.json explains for this quest";
                return Consistency.NotModeled;
            }

            if (unknown.Count == 0 && ScriptNamesNone(quest, source.Select(r => r.Name).ToList()) is { } noCheck)
            {
                reason = "catalog has no PreviousQuest; " + noCheck;
                return Consistency.DisagreeScriptSilent;
            }

            reason = "catalog has no PreviousQuest; source names " + named;
            return Consistency.Disagree;
        }

        HashSet<string>? ancestors = null;
        var direct = new List<string>();
        var transitive = new List<string>();
        var explained = new List<string>();
        var unreachable = new List<string>();
        var requiredDuties = extras.InstanceContentNames;
        foreach (var (name, rows) in source)
        {
            if (rows.Any(catalog.Contains))
            {
                direct.Add(name);
                continue;
            }

            ancestors ??= AncestorNames(quest);
            if (rows.Any(ancestors.Contains))
            {
                transitive.Add(name);
                continue;
            }

            var rowRecords = rows.SelectMany(n => rowsByName.GetValueOrDefault(n, [])).Select(game.Catalog.GetByRowId).OfType<QuestRecord>().ToList();
            var unlocksRequiredDuty = requiredDuties.Count > 0 && rowRecords.Any(r => DutyUnlocksOf(r).Concat(WikiDutyUnlocksOf(r.RowId)).Any(d => requiredDuties.Any(rd => DutyNames.Same(d, rd))));
            if (unlocksRequiredDuty)
            {
                explained.Add($"{name} unlocks the duty the sheet requires");
                continue;
            }

            var system = rowRecords.Select(r => curatedSystemUnlocks.GetValueOrDefault(r.RowId)).FirstOrDefault(l => l is not null);
            if (system is not null)
            {
                explained.Add($"{name} unlocks the system this quest builds on ({system}; curated/system_unlocks.json), which the sheet gates through the system");
                continue;
            }

            if (rowRecords.Any(IsMainScenario) && !IsMainScenario(quest))
            {
                explained.Add($"{name} is main-scenario progress the sheet does not require (area or NPC access is not modeled)");
                continue;
            }

            if (quest.BeastTribe != 0)
            {
                explained.Add($"{name} precedes an allied-society quest the sheet gates by rank ({game.Bundle.Names.Tribe(quest.BeastTribe)} rank {quest.BeastRank})");
                continue;
            }

            if (extras.SatisfactionNpc != 0 || extras.DeliveryQuest != 0)
            {
                explained.Add($"{name} precedes a custom-delivery quest the sheet gates by client (T3)");
                continue;
            }

            unreachable.Add(name);
        }

        if (unreachable.Count > 0)
        {
            var successors = unreachable.Where(n => ResolveNames(n).SelectMany(r => rowsByName.GetValueOrDefault(r, [])).Any(id => IsAncestorOf(quest.RowId, id))).ToList();
            if (successors.Count == unreachable.Count)
            {
                reason = "source names quests that themselves require this one in the sheet (successors, not prerequisites): " + string.Join("; ", successors);
                return Consistency.SourceWrong;
            }

            if (unknown.Count == 0 && ScriptNamesNone(quest, unreachable) is { } noCheck)
            {
                reason = noCheck;
                return Consistency.DisagreeScriptSilent;
            }

            reason = "source names quests the catalog does not require, directly or transitively: " + string.Join("; ", unreachable);
            return Consistency.Disagree;
        }

        if (unknown.Count > 0)
        {
            // A name that is no quest, on a quest whose curated game gate the plugin lists as not checked (a Doman
            // reconstruction stage, Eureka progress): the source names that gate, which is modeled, just not readable.
            if (game.Catalog.GameGateOf(quest.RowId) is { Items: null, Mounts: null } gate)
            {
                reason = $"source names {string.Join("; ", unknown)}, no quest: the game gate curated/game_gates.json lists as not checked ({gate.Gate})";
                return Consistency.NotModeled;
            }

            reason = "source names quests not in the catalog: " + string.Join("; ", unknown);
            return Consistency.Disagree;
        }

        var notes = new List<string>();
        if (transitive.Count > 0)
        {
            notes.Add("source names transitive prerequisites (" + string.Join("; ", transitive) + ")");
        }

        var covered = source.Where(s => direct.Contains(s.Name)).SelectMany(s => s.Rows).ToHashSet();
        var acceptNames = accept.Select(id => Names.Canon(game.Catalog.GetByRowId(id)!.Name)).ToHashSet();
        var missing = catalog.Where(n => !covered.Contains(n)).ToList();
        var curatedNames = game.Catalog.ExtraPrerequisitesOf(quest.RowId).Select(id => Names.Canon(game.Catalog.GetByRowId(id)!.Name)).ToHashSet();
        var namedAdded = direct.SelectMany(d => source.First(s => s.Name == d).Rows).Where(acceptNames.Contains).ToList();
        if (namedAdded.Any(n => !curatedNames.Contains(n)))
        {
            notes.Add("source names accept conditions (QuestAcceptAdditionCondition) as well as PreviousQuest");
        }

        if (namedAdded.Any(curatedNames.Contains))
        {
            notes.Add("source names curated extra prerequisites (curated/extra_prerequisites.json) as well as PreviousQuest");
        }

        if (missing.Count > 0)
        {
            if (quest.PreviousQuests.Join == JoinKind.Any && missing.All(m => !acceptNames.Contains(m)))
            {
                notes.Add($"any-join: source names {direct.Count} of {catalog.Count}");
                if (direct.Count == 0 && transitive.Count == 0 && explained.Count == 0)
                {
                    reason = string.Join("; ", notes);
                    return Consistency.Disagree;
                }
            }
            else
            {
                notes.Add($"source lists a subset ({missing.Count} catalog prerequisite(s) not named)");
            }
        }

        if (explained.Count > 0)
        {
            notes.Insert(0, string.Join("; ", explained));
            reason = string.Join("; ", notes);
            return Consistency.NotModeled;
        }

        reason = string.Join("; ", notes);
        return Consistency.Agree;
    }

    /// <summary>Whether <paramref name="ancestor"/> is a PreviousQuest or accept-condition ancestor of <paramref name="rowId"/>.</summary>
    private bool IsAncestorOf(uint ancestor, uint rowId)
    {
        var seen = new HashSet<uint>();
        var stack = new Stack<uint>([rowId]);
        while (stack.Count > 0 && seen.Count < 5000)
        {
            var id = stack.Pop();
            if (!seen.Add(id) || game.Catalog.GetByRowId(id) is not { } q)
            {
                continue;
            }

            foreach (var p in q.PreviousQuests.QuestIds.Concat(AcceptQuests(q)))
            {
                if (p == ancestor)
                {
                    return true;
                }

                stack.Push(p);
            }
        }

        return false;
    }

    private HashSet<string> AncestorNames(QuestRecord quest)
    {
        var seen = new HashSet<uint>();
        var stack = new Stack<uint>(quest.PreviousQuests.QuestIds);
        var names = new HashSet<string>();
        while (stack.Count > 0 && seen.Count < 5000)
        {
            var id = stack.Pop();
            if (!seen.Add(id) || game.Catalog.GetByRowId(id) is not { } q)
            {
                continue;
            }

            names.Add(Names.Canon(q.Name));
            foreach (var p in q.PreviousQuests.QuestIds.Concat(AcceptQuests(q)))
            {
                stack.Push(p);
            }
        }

        return names;
    }

    /// <summary>Item rewards: exact set, or the source lists a subset of the catalog's (class-variant gear is often abbreviated). A wiki "(Item)" disambiguation suffix is ignored.</summary>
    private static bool RewardsConsistent(List<string> catalog, List<string> source, out string reason)
    {
        reason = string.Empty;
        var c = catalog.Select(Names.Canon).Where(n => n.Length > 0).ToHashSet();
        var s = source.Select(n => c.Contains(Names.Canon(n)) ? Names.Canon(n) : c.Contains(Names.Canon(Names.Base(n))) ? Names.Canon(Names.Base(n)) : Names.Canon(n)).Where(n => n.Length > 0).ToHashSet();
        if (c.SetEquals(s))
        {
            return true;
        }

        var extra = s.Where(n => !c.Contains(n)).ToList();
        if (extra.Count == 0)
        {
            reason = $"source lists {s.Count} of the catalog's {c.Count} item rewards";
            return true;
        }

        reason = "source names item rewards the catalog lacks: " + string.Join("; ", extra);
        return false;
    }

    private HashSet<string>? cfcKeys;

    private bool IsDutyName(string name)
    {
        cfcKeys ??= cfcNames.Select(DutyNames.Key).ToHashSet(StringComparer.Ordinal);
        return cfcKeys.Contains(DutyNames.Key(name));
    }

    /// <summary>
    /// The wiki's <c>unlocks</c> duties against the catalog's. A wiki name that matches a catalog duty or its series
    /// covers it; a wiki name that is no ContentFinderCondition at all (the "PvP" system, "Frontline" as a family) is
    /// ignored. A quest the sheet links to no duty at all is notModeled: the unlock is script-driven and the wiki's
    /// name is a candidate for curated/duty_unlocks.json.
    /// </summary>
    private Consistency DutyUnlockConsistent(QuestRecord quest, List<string> catalog, List<string> source, out string reason)
    {
        reason = string.Empty;
        var linked = quest.Rewards.Where(r => r.Kind == RewardKind.Instance).Select(r => r.Name)
            .Concat(curatedDutyUnlocks.TryGetValue(quest.RowId, out var cfcIds) ? cfcIds.Select(id => game.ContentFinderConditionNames.GetValueOrDefault(id, string.Empty)) : [])
            .Concat(uniqueEntries.Where(e => e.QuestRowId == quest.RowId && e.Kind == RewardKind.DutyUnlock && !e.Source.StartsWith("ContentFinderCondition.UnlockCriteria", StringComparison.Ordinal)).Select(e => e.RewardName))
            .Select(Names.Canon).ToHashSet();
        var criteriaOnly = catalog.Where(c => !linked.Contains(Names.Canon(c))).ToHashSet(StringComparer.Ordinal);
        var covered = new HashSet<string>(StringComparer.Ordinal);
        var extraDuties = new List<string>();
        var ignored = new List<string>();
        foreach (var name in source.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var hits = catalog.Where(c => DutyNames.Same(c, name) || DutyNames.Series(c, name)).ToList();
            if (hits.Count > 0)
            {
                foreach (var h in hits)
                {
                    covered.Add(DutyNames.Key(h));
                }
            }
            else if (IsDutyName(name))
            {
                extraDuties.Add(name);
            }
            else
            {
                ignored.Add(name);
            }
        }

        var missing = catalog.Where(c => !covered.Contains(DutyNames.Key(c))).ToList();
        var ignoredNote = ignored.Count > 0 ? "not a duty name, ignored: " + string.Join("; ", ignored) : string.Empty;
        if (extraDuties.Count == 0)
        {
            var parts = new List<string>();
            if (missing.Count > 0)
            {
                parts.Add($"wiki names {covered.Count} of the catalog's {catalog.Count} duty unlocks");
            }

            if (ignoredNote.Length > 0)
            {
                parts.Add(ignoredNote);
            }

            reason = string.Join("; ", parts);
            return Consistency.Agree;
        }

        var tail = ignoredNote.Length > 0 ? "; " + ignoredNote : string.Empty;
        if (catalog.Count == 0)
        {
            reason = "the sheet links no InstanceContentUnlock or unlock criteria to this quest and curated/duty_unlocks.json has no entry; the wiki names " + string.Join("; ", extraDuties) + " (script-driven unlock; candidate for duty_unlocks.json)" + tail;
            return Consistency.NotModeled;
        }

        if (missing.Count == 0)
        {
            // The wiki lists the whole series (Coil turns 1-4) where the sheet's InstanceContentUnlock names the first; later turns unlock on clears.
            reason = "wiki also names " + string.Join("; ", extraDuties) + " (progressive unlocks beyond the catalog's " + string.Join("; ", catalog) + ")" + tail;
            return Consistency.Agree;
        }

        if (missing.All(criteriaOnly.Contains))
        {
            // The catalog's entries come from ContentFinderCondition.UnlockCriteria (a duty that needs this quest done, not one the quest opens),
            // and the wiki's are the script-driven unlocks the sheet does not link: two facts the two sides model differently.
            reason = "catalog names duties whose ContentFinderCondition unlock criteria require this quest (" + string.Join("; ", missing) + "); the wiki names the script-driven unlocks " + string.Join("; ", extraDuties) + " (candidates for duty_unlocks.json)" + tail;
            return Consistency.NotModeled;
        }

        reason = "wiki names duty unlocks the catalog lacks: " + string.Join("; ", extraDuties) + "; catalog-only: " + string.Join("; ", missing) + tail;
        return Consistency.Disagree;
    }

    /// <summary>§1.4: a disagreement becomes sourceWrong when another independent source agrees with the catalog, catalogWrong when two sources agree against it, else unresolved.</summary>
    private static List<QuestRow> Reconcile(List<(QuestRecord Quest, Draft Draft)> drafts)
    {
        var rows = new List<QuestRow>(drafts.Count);
        foreach (var (quest, d) in drafts)
        {
            var verdict = d.Verdict;
            var reason = d.Reason;
            var fixedIn = d.FixedIn;
            if (d.Disagree)
            {
                var others = drafts.Where(o => o.Draft.Fact == d.Fact && o.Draft.Source != d.Source && o.Draft.Source != SourceNames.Garland).Select(o => o.Draft).ToList();
                var agreeing = others.Where(o => o.Verdict == Verdict.Match).ToList();
                var alsoDisagree = others.Where(o => o.Disagree && Names.Canon(o.SourceValue) == Names.Canon(d.SourceValue)).ToList();
                if (alsoDisagree.Count > 0)
                {
                    verdict = Verdict.CatalogWrong;
                    reason = $"{d.Source} and {alsoDisagree[0].Source} agree against the catalog" + (reason.Length > 0 ? ": " + reason : string.Empty);
                    fixedIn = d.Fact == Facts.DisplayLevel ? "T2" : fixedIn;
                }
                else if (agreeing.Count > 0)
                {
                    verdict = Verdict.SourceWrong;
                    reason = $"{agreeing[0].Source} agrees with the catalog" + (reason.Length > 0 ? "; " + reason : string.Empty);
                }
                else if (d.Settle is { } settle)
                {
                    verdict = settle;
                    reason = d.SettleReason ?? reason;
                }
                else
                {
                    verdict = Verdict.Unresolved;
                }
            }

            rows.Add(new QuestRow(quest.RowId, quest.Name, d.Fact, d.Catalog, d.Source, d.SourceValue, d.SourceRef, verdict, reason, fixedIn));
        }

        return rows;
    }
}
