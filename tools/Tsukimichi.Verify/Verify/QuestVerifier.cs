using System.Text.RegularExpressions;
using Tsukimichi.Core.Model;
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
    IReadOnlyList<UniqueRewardEntry> uniqueEntries,
    TextWriter log)
{
    private readonly Dictionary<uint, WikiPage> wikiByRow = [];
    private readonly Dictionary<uint, string> wikiAmbiguity = [];
    private readonly Dictionary<uint, string> wikiMissing = [];
    private readonly Dictionary<uint, LodestoneListing> listingByRow = [];
    private readonly Dictionary<uint, string> lodestoneAmbiguity = [];
    private readonly Dictionary<(uint Section, uint Category), (List<LodestoneListing>? Rows, int Total, string Url)> listings = [];
    private readonly Dictionary<uint, LodestonePage?> pagesByRow = [];
    private readonly Dictionary<uint, GarlandQuest?> garlandByRow = [];
    private readonly Dictionary<string, List<uint>> rowsByName = new(StringComparer.Ordinal);
    private readonly HashSet<string> cfcNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ushort, FestivalWindow> festivals = [];

    public IReadOnlyDictionary<ushort, FestivalWindow> Festivals => festivals;
    public int LodestoneListed { get; private set; }
    public int LodestonePagesFetched { get; private set; }
    public int WikiPagesFound { get; private set; }

    [GeneratedRegex(@"https?://[a-z]{2}\.finalfantasyxiv\.com/lodestone/(?:special|topics)/[^\s\]|<>""']+")]
    private static partial Regex LodestoneAnnouncement();

    [GeneratedRegex(@"^\s*\d+\s+(.*)$")]
    private static partial Regex LeadingCount();

    public async Task<List<QuestRow>> RunAsync(IReadOnlyList<QuestRecord> selection, Action<List<QuestRow>, int> checkpoint, CancellationToken ct)
    {
        foreach (var q in game.Catalog.All)
        {
            var key = Names.Canon(q.Name);
            if (!rowsByName.TryGetValue(key, out var list))
            {
                rowsByName[key] = list = [];
            }

            list.Add(q.RowId);
        }

        foreach (var name in game.ContentFinderConditionNames.Values)
        {
            cfcNames.Add(Names.Clean(name));
        }

        await WikiPassAsync(selection, ct);
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

        // First the rows whose wiki page names the Lodestone id.
        foreach (var quest in selection)
        {
            if (wikiByRow.TryGetValue(quest.RowId, out var page) && page.QuestInfobox.TryGetValue("id-edb", out var edb))
            {
                var id = edb.Trim().ToLowerInvariant();
                if (id.Length > 0 && byId.TryGetValue(id, out var listing) && claimed.TryAdd(id, quest.RowId))
                {
                    listingByRow[quest.RowId] = listing;
                }
            }
        }

        // Then by (category, name), narrowed by level and area when the name repeats inside the category.
        foreach (var quest in selection)
        {
            if (listingByRow.ContainsKey(quest.RowId) || quest.IsUnlisted)
            {
                continue;
            }

            if (!byCategoryName.TryGetValue((quest.Journal.CategoryId, Names.Canon(quest.Name)), out var candidates))
            {
                continue;
            }

            var free = candidates.Where(c => !claimed.ContainsKey(c.LodestoneId)).ToList();
            if (free.Count == 0)
            {
                lodestoneAmbiguity[quest.RowId] = $"the {candidates.Count} listing(s) of this name in category {quest.Journal.CategoryId} are claimed by other rows";
                continue;
            }

            if (free.Count > 1)
            {
                var level = GameCatalog.DisplayLevel(quest);
                var narrowed = free.Where(c => c.Level == level).ToList();
                if (narrowed.Count > 1)
                {
                    var area = Names.Canon(game.ExtrasOf(quest.RowId).PlaceName);
                    var byArea = narrowed.Where(c => Names.Canon(c.Area) == area).ToList();
                    if (byArea.Count >= 1)
                    {
                        narrowed = byArea;
                    }
                }

                if (narrowed.Count != 1)
                {
                    lodestoneAmbiguity[quest.RowId] = $"{free.Count} listings share this name in category {quest.Journal.CategoryId}; level/area do not single one out";
                    continue;
                }

                free = narrowed;
            }

            claimed[free[0].LodestoneId] = quest.RowId;
            listingByRow[quest.RowId] = free[0];
        }

        log.WriteLine($"lodestone: {all.Count} listed ids, {listingByRow.Count} mapped to rows, {lodestoneAmbiguity.Count} ambiguous");
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

    private sealed record Draft(string Fact, string Catalog, string Source, string SourceValue, string SourceRef, Verdict Verdict, string Reason, string FixedIn = "", bool Disagree = false);

    private List<(QuestRecord Quest, Draft Draft)> Compare(QuestRecord quest)
    {
        var drafts = new List<Draft>();
        var extras = game.ExtrasOf(quest.RowId);
        var displayLevel = GameCatalog.DisplayLevel(quest);
        var prereqNames = quest.PreviousQuests.QuestIds.Select(id => game.Catalog.GetByRowId(id)?.Name ?? $"row {id}").ToList();
        var prereqCatalog = (quest.PreviousQuests.IsEmpty ? string.Empty : (quest.PreviousQuests.Join == JoinKind.Any ? "any:" : "all:")) + Names.Join(quest.PreviousQuests.QuestIds);
        var dutyCatalog = (quest.InstanceContentRequired.Length > 1 ? (quest.InstanceJoin == JoinKind.Any ? "any:" : "all:") : string.Empty) + Names.Join(extras.InstanceContentNames);
        var rewardNames = quest.Rewards.Where(r => r.Kind is RewardKind.Item or RewardKind.OptionalItem or RewardKind.ArtifactGear).Select(r => r.Name).ToList();
        var unlockNames = uniqueEntries.Where(e => e.QuestRowId == quest.RowId && e.Kind == RewardKind.DutyUnlock).Select(e => e.RewardName).ToList();
        var unlockCatalog = Names.Join(unlockNames);

        // ---- Lodestone
        var lodestoneRef = string.Empty;
        LodestonePage? page = null;
        if (quest.IsUnlisted || quest.Journal.SectionId == 255)
        {
            drafts.Add(new Draft(Facts.Listed, "unlisted", SourceNames.Lodestone, string.Empty, LodestoneSource.Base, Verdict.NotModeled, "journal genre 0; the Lodestone lists journal categories only"));
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
            else
            {
                drafts.Add(new Draft(Facts.Listed, "listed", SourceNames.Lodestone, listing.LodestoneId, lodestoneRef, Verdict.Match, string.Empty));
            }
        }
        else
        {
            var (_, total, url) = listings.GetValueOrDefault((quest.Journal.SectionId, quest.Journal.CategoryId));
            var catalogCount = game.Catalog.ByCategory.TryGetValue(quest.Journal.CategoryId, out var cat) ? cat.Count : 0;
            if (lodestoneAmbiguity.TryGetValue(quest.RowId, out var why))
            {
                drafts.Add(new Draft(Facts.Listed, "listed", SourceNames.Lodestone, string.Empty, url ?? LodestoneSource.Base, Verdict.Ambiguous, why));
            }
            else if (listings.TryGetValue((quest.Journal.SectionId, quest.Journal.CategoryId), out var l) && l.Rows is null)
            {
                drafts.Add(new Draft(Facts.Listed, "listed", SourceNames.Lodestone, string.Empty, l.Url, Verdict.Unresolved, "category listing could not be fetched"));
            }
            else
            {
                var maxMapped = MaxMappedRowInCategory(quest.Journal.CategoryId);
                var lag = quest.RowId > maxMapped && maxMapped > 0;
                drafts.Add(new Draft(Facts.Listed, "listed", SourceNames.Lodestone, string.Empty, url ?? LodestoneSource.Base, lag ? Verdict.SourceLagging : Verdict.NotListed,
                    lag
                        ? $"newer than the Lodestone's newest listed row in this category ({maxMapped}); Lodestone lists {total} of the catalog's {catalogCount}"
                        : $"not in the Lodestone listing for section {quest.Journal.SectionId} category {quest.Journal.CategoryId} ({total} listed, catalog {catalogCount})"));
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
                && (Names.Canon(page.StartingClass) == Names.Canon(extras.ClassJobCategoryName) || Names.Canon(game.Bundle.Names.ClassJobAbbreviations.FirstOrDefault(kv => Names.Canon(game.Bundle.Names.ClassJob(kv.Key)) == Names.Canon(page.StartingClass)).Value ?? "\0") == Names.Canon(extras.ClassJobCategoryName));
            var startOk = Names.Canon(page.StartingClass) == Names.Canon(extras.ClassJobRequiredName) || (page.StartingClass.Length == 0 && classShownAsRequired) || startIsCategory;
            drafts.Add(Compare(Facts.StartingClass, extras.ClassJobRequiredName.Length > 0 ? extras.ClassJobRequiredName : (startIsCategory ? "(category " + extras.ClassJobCategoryName + ")" : string.Empty), SourceNames.Lodestone, page.StartingClass.Length > 0 ? page.StartingClass : (classShownAsRequired ? page.ClassJobText : string.Empty), lodestoneRef, startOk,
                classShownAsRequired ? "required class shown in the Class/Job line" : startIsCategory ? "no ClassJobRequired; the single-class ClassJobCategory is the starting class" : string.Empty));

            var gcOk = extras.GrandCompanyName.Length == 0
                ? page.GrandCompany.Length == 0
                : Names.Canon(page.GrandCompany).StartsWith(Names.Canon(extras.GrandCompanyName), StringComparison.Ordinal);
            drafts.Add(Compare(Facts.GrandCompany, extras.GrandCompanyName, SourceNames.Lodestone, page.GrandCompany, lodestoneRef, gcOk));

            var genreOk = Names.Canon(page.ContentType) == Names.Canon(quest.Journal.GenreName) || Names.Canon(page.ContentType) == Names.Canon(quest.Journal.CategoryName);
            drafts.Add(Compare(Facts.Genre, quest.Journal.GenreName, SourceNames.Lodestone, page.ContentType, lodestoneRef, genreOk));

            var listing = listingByRow[quest.RowId];
            var sectionOk = listing.SectionId == quest.Journal.SectionId && listing.CategoryId == quest.Journal.CategoryId;
            drafts.Add(Compare(Facts.Section, $"{quest.Journal.SectionId}/{quest.Journal.CategoryId} {quest.Journal.SectionName} > {quest.Journal.CategoryName}", SourceNames.Lodestone,
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

        if (quest.IsUnlisted)
        {
            // Unlisted (hidden, removed, legacy) rows: only identity facts are compared, as qa-data-engineer §1.4 asks; the rest would only pollute the counts.
            drafts = drafts.Select(d => d.Fact is Facts.Listed or Facts.Name or Facts.DisplayLevel or Facts.Retired || !d.Disagree
                ? d
                : d with { Verdict = Verdict.NotModeled, Disagree = false, Reason = "unlisted row; only identity facts are compared (" + d.Reason + ")" }).ToList();
        }

        // ---- wiki
        if (wikiByRow.TryGetValue(quest.RowId, out var wikiPage))
        {
            var box = wikiPage.QuestInfobox;
            var wref = wikiPage.Url;
            drafts.Add(new Draft(Facts.Listed, quest.IsUnlisted ? "unlisted" : "listed", SourceNames.Wiki, wikiPage.Title, wref, Verdict.Match, box.ContainsKey("id-gt") ? "matched by id-gt" : "matched by title"));

            var title = WikiSource.StripMarkup(box.GetValueOrDefault("title", wikiPage.Title));
            var baseTitle = BaseName(title);
            var baseName = BaseName(quest.Name);
            var nameOk = Names.Canon(quest.Name) == Names.Canon(title) || Names.Canon(quest.Name) == Names.Canon(baseTitle) || Names.Canon(baseName) == Names.Canon(title) || Names.Canon(baseName) == Names.Canon(baseTitle);
            drafts.Add(Compare(Facts.Name, quest.Name, SourceNames.Wiki, title, wref, nameOk, Names.Canon(quest.Name) == Names.Canon(title) ? string.Empty : "matched without the parenthetical variant suffix"));

            var levelText = Names.Clean(box.GetValueOrDefault("level", string.Empty));
            if (int.TryParse(levelText, out var wikiLevel))
            {
                drafts.Add(Compare(Facts.DisplayLevel, displayLevel.ToString(), SourceNames.Wiki, wikiLevel.ToString(), wref, wikiLevel == displayLevel));
            }
            else
            {
                drafts.Add(new Draft(Facts.DisplayLevel, displayLevel.ToString(), SourceNames.Wiki, levelText, wref, Verdict.NotModeled, "no numeric level in the infobox"));
            }

            var wikiSection = WikiSource.StripMarkup(box.GetValueOrDefault("journal-section", string.Empty));
            var wikiCategory = WikiSource.StripMarkup(box.GetValueOrDefault("journal-category", string.Empty));
            if (wikiCategory.Length == 0 && wikiSection.Length == 0)
            {
                drafts.Add(new Draft(Facts.Section, $"{quest.Journal.SectionName} > {quest.Journal.CategoryName}", SourceNames.Wiki, string.Empty, wref, Verdict.NotModeled, "no journal fields in the infobox"));
            }
            else if (quest.IsUnlisted)
            {
                drafts.Add(new Draft(Facts.Section, "unlisted", SourceNames.Wiki, $"{wikiSection} > {wikiCategory}", wref, Verdict.NotModeled, "catalog has no journal genre for this row"));
            }
            else
            {
                var catOk = wikiCategory.Length == 0 || SameJournalName(wikiCategory, quest.Journal.CategoryName);
                var secOk = wikiSection.Length == 0 || SameJournalName(wikiSection, quest.Journal.SectionName);
                drafts.Add(Compare(Facts.Section, $"{quest.Journal.SectionName} > {quest.Journal.CategoryName}", SourceNames.Wiki, $"{wikiSection} > {wikiCategory}", wref, catOk && secOk));
            }

            var expansion = WikiSource.ExpansionOf(box.GetValueOrDefault("release"), box.GetValueOrDefault("patch"));
            if (expansion < 0)
            {
                drafts.Add(new Draft(Facts.Expansion, $"{quest.Expansion} {extras.ExpansionName}", SourceNames.Wiki, string.Empty, wref, Verdict.NotModeled, "no release or patch in the infobox"));
            }
            else
            {
                drafts.Add(Compare(Facts.Expansion, $"{quest.Expansion} {extras.ExpansionName}", SourceNames.Wiki, $"{expansion} ({Names.Clean(box.GetValueOrDefault("release", string.Empty))} / patch {Names.Clean(box.GetValueOrDefault("patch", string.Empty))})", wref, expansion == quest.Expansion));
            }

            var wikiPrereqs = WikiSource.SplitNames(box.GetValueOrDefault("prev-quest")).Concat(WikiSource.SplitNames(box.GetValueOrDefault("req-quest"))).ToList();
            if (wikiPrereqs.Count == 0 && !quest.PreviousQuests.IsEmpty)
            {
                drafts.Add(new Draft(Facts.Prereqs, prereqCatalog, SourceNames.Wiki, string.Empty, wref, Verdict.NotModeled, "infobox prev-quest/req-quest not filled in"));
            }
            else
            {
                drafts.Add(Compare(Facts.Prereqs, prereqCatalog, SourceNames.Wiki, Names.Join(wikiPrereqs), wref, PrereqsConsistent(quest, wikiPrereqs, out var prereqWhy), prereqWhy));
            }

            var wikiDuties = WikiSource.LinkedNames(box.GetValueOrDefault("requirements")).Where(n => cfcNames.Contains(n)).ToList();
            if (wikiDuties.Count > 0 || extras.InstanceContentNames.Count > 0)
            {
                drafts.Add(Compare(Facts.Duties, dutyCatalog, SourceNames.Wiki, Names.Join(wikiDuties), wref, Names.SameSet(wikiDuties, extras.InstanceContentNames)));
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

            drafts.Add(Compare(Facts.Rewards, Names.Join(rewardNames), SourceNames.Wiki, Names.Join(wikiRewards), wref, RewardsConsistent(rewardNames, wikiRewards, out var wikiRewardWhy), wikiRewardWhy));

            var wikiUnlocks = WikiSource.Unlocks(box.GetValueOrDefault("unlocks")).Where(u => WikiSource.DutyCodes.Contains(u.Code)).Select(u => u.Name).ToList();
            if (wikiUnlocks.Count > 0 || unlockNames.Count > 0)
            {
                drafts.Add(Compare(Facts.DutyUnlock, unlockCatalog, SourceNames.Wiki, Names.Join(wikiUnlocks), wref, DutyUnlockConsistent(unlockNames, wikiUnlocks, out var unlockWhy), unlockWhy));
            }

            var retired = wikiPage.RetiredPatch;
            if (retired is null)
            {
                drafts.Add(new Draft(Facts.Retired, quest.IsUnlisted ? "unlisted" : "live", SourceNames.Wiki, "live", wref, Verdict.Match, string.Empty));
            }
            else if (quest.IsUnlisted)
            {
                drafts.Add(new Draft(Facts.Retired, "unlisted", SourceNames.Wiki, "retired " + retired, wref, Verdict.Match, "retired on the wiki and unlisted in the sheet"));
            }
            else
            {
                drafts.Add(new Draft(Facts.Retired, "live", SourceNames.Wiki, "retired " + retired, wref, Verdict.CatalogWrong, $"wiki marks the quest retired in patch {retired}; the catalog still lists it under {quest.Journal.CategoryName}", "0.6.1 (T1 retired_quests.json)"));
            }
        }
        else if (wikiAmbiguity.TryGetValue(quest.RowId, out var ambiguous))
        {
            drafts.Add(new Draft(Facts.Listed, quest.IsUnlisted ? "unlisted" : "listed", SourceNames.Wiki, string.Empty, WikiSource.PageUrl(quest.Name), Verdict.Ambiguous, ambiguous));
        }
        else
        {
            drafts.Add(new Draft(Facts.Listed, quest.IsUnlisted ? "unlisted" : "listed", SourceNames.Wiki, string.Empty, WikiSource.PageUrl(quest.Name), Verdict.NotListed, wikiMissing.GetValueOrDefault(quest.RowId, "no wiki page")));
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
                var ok = curatedNames.Any(n => Names.Canon(n) == Names.Canon(g.InstanceName));
                drafts.Add(Compare(Facts.DutyUnlock, Names.Join(curatedNames), SourceNames.Garland, g.InstanceName, gref, ok, ok ? string.Empty : $"Garland reward.instance {g.InstanceId} = {g.InstanceName}"));
            }
        }

        return drafts.Select(d => (quest, d)).ToList();
    }

    private static Draft Compare(string fact, string catalog, string source, string sourceValue, string sourceRef, bool ok, string reason = "")
        => ok
            ? new Draft(fact, catalog, source, sourceValue, sourceRef, Verdict.Match, reason)
            : new Draft(fact, catalog, source, sourceValue, sourceRef, Verdict.Unresolved, reason.Length > 0 ? reason : "source disagrees with the catalog", Disagree: true);

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

    private static string BaseName(string name) => Regex.Replace(name, @"\s*\([^)]*\)\s*$", string.Empty);

    /// <summary>Shards, crystals and clusters: the mapper files crystal reward slots as nameless Other rewards, so they are excluded from the item comparison on both sides (noted in verification-full.md).</summary>
    private static bool IsCrystal(string name)
    {
        var n = Names.Canon(name);
        return n.EndsWith(" shard", StringComparison.Ordinal) || n.EndsWith(" crystal", StringComparison.Ordinal) || n.EndsWith(" cluster", StringComparison.Ordinal);
    }

    /// <summary>A source's quest name resolved against the catalog: as written, else without the wiki's disambiguation suffix ("Close to Home (Gridania)", "Brotherhood of Ash (Quest)").</summary>
    private string ResolveName(string name)
    {
        var c = Names.Canon(name);
        if (rowsByName.ContainsKey(c))
        {
            return c;
        }

        var b = Names.Canon(BaseName(name));
        return rowsByName.ContainsKey(b) ? b : c;
    }

    /// <summary>
    /// A source's prerequisite list agrees with the catalog when it names the same quests (All join), any subset of an
    /// Any join, or only quests that are transitive prerequisites of the catalog's set (the wiki often names the story
    /// predecessor rather than the sheet's hard requirement). Anything the catalog cannot reach is a disagreement.
    /// </summary>
    private bool PrereqsConsistent(QuestRecord quest, List<string> sourceNames, out string reason)
    {
        reason = string.Empty;
        var source = sourceNames.Select(ResolveName).Where(n => n.Length > 0).Distinct().ToList();
        var catalog = quest.PreviousQuests.QuestIds.Select(id => Names.Canon(game.Catalog.GetByRowId(id)?.Name ?? string.Empty)).Where(n => n.Length > 0).Distinct().ToHashSet();

        if (source.Count == 0)
        {
            if (catalog.Count == 0)
            {
                return true;
            }

            reason = "source lists no prerequisite; catalog has " + catalog.Count;
            return false;
        }

        if (catalog.Count == 0)
        {
            reason = "catalog has no PreviousQuest; source names " + string.Join("; ", sourceNames);
            return false;
        }

        var unknown = source.Where(n => !rowsByName.ContainsKey(n)).ToList();
        var extra = source.Where(n => !catalog.Contains(n) && rowsByName.ContainsKey(n)).ToList();
        var missing = catalog.Where(n => !source.Contains(n)).ToList();

        if (extra.Count > 0)
        {
            var ancestors = AncestorNames(quest);
            var unreachable = extra.Where(n => !ancestors.Contains(n)).ToList();
            if (unreachable.Count > 0)
            {
                reason = "source names quests the catalog does not require, directly or transitively: " + string.Join("; ", unreachable);
                return false;
            }

            reason = "source names transitive prerequisites (" + string.Join("; ", extra) + ")";
        }

        if (unknown.Count > 0)
        {
            reason = "source names quests not in the catalog: " + string.Join("; ", unknown);
            return false;
        }

        if (missing.Count > 0)
        {
            if (quest.PreviousQuests.Join == JoinKind.Any)
            {
                reason = (reason.Length > 0 ? reason + "; " : string.Empty) + "any-join: source names " + (source.Count) + " of " + catalog.Count;
                return source.Any(catalog.Contains) || extra.Count > 0;
            }

            if (source.All(catalog.Contains) || extra.Count > 0)
            {
                reason = (reason.Length > 0 ? reason + "; " : string.Empty) + "source lists a subset (" + missing.Count + " catalog prerequisite(s) not named)";
                return true;
            }
        }

        return true;
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
            foreach (var p in q.PreviousQuests.QuestIds)
            {
                stack.Push(p);
            }
        }

        return names;
    }

    /// <summary>Item rewards: exact set, or the source lists a subset of the catalog's (class-variant gear is often abbreviated).</summary>
    private static bool RewardsConsistent(List<string> catalog, List<string> source, out string reason)
    {
        reason = string.Empty;
        var c = catalog.Select(Names.Canon).Where(n => n.Length > 0).ToHashSet();
        var s = source.Select(Names.Canon).Where(n => n.Length > 0).ToHashSet();
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

    private static bool DutyUnlockConsistent(List<string> catalog, List<string> source, out string reason)
    {
        reason = string.Empty;
        var c = catalog.Select(Names.Canon).Where(n => n.Length > 0).ToHashSet();
        var s = source.Select(Names.Canon).Where(n => n.Length > 0).ToHashSet();
        if (c.SetEquals(s))
        {
            return true;
        }

        var extra = s.Where(n => !c.Contains(n)).ToList();
        var missing = c.Where(n => !s.Contains(n)).ToList();
        if (extra.Count == 0)
        {
            reason = "wiki names " + s.Count + " of the catalog's " + c.Count + " duty unlocks";
            return true;
        }

        if (missing.Count == 0 && c.Count > 0)
        {
            // The wiki lists the whole series (Coil turns 1-4) where the sheet's InstanceContentUnlock names the first; later turns unlock on clears.
            reason = "wiki also names " + string.Join("; ", extra) + " (progressive unlocks beyond the catalog's " + string.Join("; ", c) + ")";
            return true;
        }

        reason = "wiki names duty unlocks the catalog lacks: " + string.Join("; ", extra) + (missing.Count > 0 ? "; catalog-only: " + string.Join("; ", missing) : string.Empty);
        return false;
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
