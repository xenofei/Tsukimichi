using Tsukimichi.Core.Model;
using Tsukimichi.Verify.Game;
using Tsukimichi.Verify.Sources;

namespace Tsukimichi.Verify.Verify;

/// <summary>
/// Checks every unique_quests.json entry's "only a quest gives this" claim: FFXIV Collect dumps for the collectible
/// kinds, the wiki item page's Acquisition section for item kinds, Garland's reward.instance plus the wiki's unlocks
/// field for duty unlocks. Structural sheet links (achievements, titles, actions, traits, aether currents, spells,
/// class unlocks, system unlocks) have no external source that models exclusivity and are reported as notModeled.
/// </summary>
internal sealed class RewardVerifier(
    GameCatalog game,
    CollectSource collect,
    WikiSource wiki,
    GarlandSource garland,
    IReadOnlyDictionary<uint, List<uint>> curatedDutyUnlocks,
    TextWriter log)
{
    private static readonly HashSet<string> QuestOnlyCollectSources = new(StringComparer.OrdinalIgnoreCase) { "Quest", "Tribal", "Event" };
    private static readonly HashSet<string> QuestOnlyWikiSections = new(StringComparer.OrdinalIgnoreCase) { "Quests", "Quest", "Seasonal Events", "Seasonal Event", "Events", "Event", "Quest Reward", "Quest Rewards" };

    public async Task<List<RewardRow>> RunAsync(IReadOnlyList<UniqueRewardEntry> entries, CancellationToken ct)
    {
        var rows = new List<RewardRow>();
        var questName = (uint id) => game.Catalog.GetByRowId(id)?.Name ?? string.Empty;

        // Collect-covered kinds: one dump per kind, then an id/name join.
        foreach (var kind in CollectSource.Paths.Keys)
        {
            var of = entries.Where(e => e.Kind == kind).ToList();
            if (of.Count == 0)
            {
                continue;
            }

            var dump = await collect.DumpAsync(kind, ct);
            foreach (var e in of)
            {
                rows.Add(CompareCollect(e, questName(e.QuestRowId), dump));
            }
        }

        // Item kinds: the wiki item page.
        var items = entries.Where(e => e.Kind is RewardKind.Item or RewardKind.OptionalItem or RewardKind.ArtifactGear).ToList();
        if (items.Count > 0)
        {
            log.WriteLine($"rewards: fetching {items.Select(e => e.RewardName).Distinct().Count()} wiki item pages for {items.Count} item entries");
            var pages = await wiki.GetPagesByIdAsync(items.Select(e => (e.RewardName, e.ItemId)).Distinct(), questPages: false, ct);
            foreach (var e in items)
            {
                pages.TryGetValue(Names.WikiTitle(e.RewardName), out var page);
                rows.Add(CompareItem(e, questName(e.QuestRowId), page));
            }
        }

        // Duty unlocks: Garland for the curated ones, the wiki quest page's unlocks field for all.
        var unlocks = entries.Where(e => e.Kind == RewardKind.DutyUnlock).ToList();
        if (unlocks.Count > 0)
        {
            var questPages = await wiki.GetPagesByIdAsync(unlocks.Select(e => (questName(e.QuestRowId), e.QuestRowId)).Where(w => w.Item1.Length > 0).Distinct(), questPages: true, ct);
            foreach (var e in unlocks)
            {
                var name = questName(e.QuestRowId);
                questPages.TryGetValue(Names.WikiTitle(name), out var page);
                var wikiRow = CompareDutyUnlockWiki(e, name, page);
                rows.Add(wikiRow);
                if (e.Source.StartsWith("curated/", StringComparison.Ordinal) && curatedDutyUnlocks.ContainsKey(e.QuestRowId))
                {
                    rows.Add(await CompareDutyUnlockGarlandAsync(e, name, wikiRow.Verdict == Verdict.Match, ct));
                }
            }
        }

        // Everything else is a sheet link with no external exclusivity model.
        foreach (var e in entries)
        {
            if (CollectSource.Paths.ContainsKey(e.Kind) || e.Kind is RewardKind.Item or RewardKind.OptionalItem or RewardKind.ArtifactGear or RewardKind.DutyUnlock)
            {
                continue;
            }

            rows.Add(new RewardRow(e.QuestRowId, questName(e.QuestRowId), e.Kind.ToString(), e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Sheet, e.Source, "sheet:" + e.Source.Split(';')[0], Verdict.NotModeled,
                $"{e.Kind} is a structural sheet link ({e.Source.Split(';')[0]}); no external source models its exclusivity", string.Empty));
        }

        return rows;
    }

    /// <summary>The SystemReward[1] coverage list (qa-data-engineer §1.8): every quest with a system-unlock id, and whether system_unlocks.json names it.</summary>
    public IEnumerable<(uint RowId, string Name, uint SystemReward, bool Covered)> SystemRewardCoverage(IReadOnlyList<UniqueRewardEntry> entries)
    {
        var covered = entries.Where(e => e.Kind == RewardKind.SystemUnlock).Select(e => e.QuestRowId).ToHashSet();
        foreach (var q in game.Catalog.All)
        {
            var sr = game.ExtrasOf(q.RowId).SystemRewardUnlock;
            if (sr != 0)
            {
                yield return (q.RowId, q.Name, sr, covered.Contains(q.RowId));
            }
        }
    }

    private static string Claim(UniqueRewardEntry e)
    {
        var other = OtherSource(e);
        return other.Length > 0 ? $"quest only; otherSource={other}" : "quest only";
    }

    private static string OtherSource(UniqueRewardEntry e)
    {
        foreach (var part in e.Source.Split(';'))
        {
            if (part.StartsWith("otherSource=", StringComparison.Ordinal))
            {
                return part["otherSource=".Length..];
            }
        }

        return string.Empty;
    }

    private RewardRow CompareCollect(UniqueRewardEntry e, string questName, IReadOnlyList<CollectEntry>? dump)
    {
        var kind = e.Kind.ToString();
        var path = CollectSource.Paths[e.Kind];
        if (dump is null)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Collect, string.Empty, CollectSource.DumpUrl(path, 1), Verdict.Unresolved, "Collect dump not available", string.Empty);
        }

        var hit = CollectSource.Find(dump, e);
        if (hit is null)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Collect, string.Empty, CollectSource.DumpUrl(path, 1), Verdict.NotListed, "no Collect entry by item id or name", string.Empty);
        }

        var types = hit.Sources.Select(s => s.Type).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var summary = string.Join(";", hit.Sources.Select(s => s.Type + (s.RelatedId is { } rid && s.RelatedType == "Quest" ? $"({rid})" : string.Empty)).Distinct());
        var questSources = hit.Sources.Where(s => s.Type.Equals("Quest", StringComparison.OrdinalIgnoreCase)).ToList();
        var nonQuest = types.Where(t => !QuestOnlyCollectSources.Contains(t)).ToList();
        var other = OtherSource(e);
        // Collect links one quest row; the city and legacy variants of that quest share its name and hand out the same reward.
        var namesQuest = questSources.Any(s => s.RelatedId == e.QuestRowId
            || (s.RelatedId is { } rid && rid <= uint.MaxValue && game.Catalog.GetByRowId((uint)rid) is { } other && Names.Canon(other.Name) == Names.Canon(questName)));
        var questNote = questSources.Count == 0 ? "Collect lists no Quest source" : namesQuest
            ? (questSources.Any(s => s.RelatedId == e.QuestRowId) ? string.Empty : "Collect links a same-name variant of this quest")
            : $"Collect's Quest source is a different quest ({string.Join("; ", questSources.Select(s => s.Text + " (" + s.RelatedId + ")"))})";

        if (hit.Sources.Count == 0)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Collect, summary, hit.Url, Verdict.NotModeled, "Collect lists no sources for this entry", string.Empty);
        }

        if (nonQuest.Count == 0)
        {
            return namesQuest || questSources.Count == 0 && types.Count > 0
                ? new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Collect, summary, hit.Url, Verdict.Match, questSources.Count == 0 ? "quest-line (" + string.Join(";", types) + ") only" : questNote, string.Empty)
                : new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Collect, summary, hit.Url, Verdict.Ambiguous, questNote, string.Empty);
        }

        var premium = nonQuest.Any(t => t.Equals("Premium", StringComparison.OrdinalIgnoreCase));
        if (other.Length > 0)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Collect, summary, hit.Url, Verdict.Match, $"other source already marked ({other}); Collect: {string.Join(";", nonQuest)}", string.Empty);
        }

        var fixedIn = premium ? "T4 (online_store.json)" : "T4";
        var reason = premium
            ? "also sold on the Online Store (Collect: Premium); the entry carries no otherSource"
            : "Collect lists non-quest sources (" + string.Join(";", nonQuest) + ") and the entry carries no otherSource";
        return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Collect, summary, hit.Url, Verdict.CatalogWrong, reason + (questNote.Length > 0 ? "; " + questNote : string.Empty), fixedIn);
    }

    private static RewardRow CompareItem(UniqueRewardEntry e, string questName, WikiPage? page)
    {
        var kind = e.Kind.ToString();
        var url = page?.Url ?? WikiSource.PageUrl(e.RewardName);
        if (page is null || page.Missing)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, string.Empty, url, Verdict.NotListed, "no wiki page for this item name", string.Empty);
        }

        if (page.IsDisambiguation)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, string.Empty, url, Verdict.Ambiguous, "item name is a disambiguation page", string.Empty);
        }

        var box = page.ItemInfobox;
        if (box.Count == 0)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, string.Empty, url, Verdict.NotListed, "wiki page is not an item page", string.Empty);
        }

        if (page.IdGt != 0 && e.ItemId != 0 && page.IdGt != e.ItemId)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, $"id-gt={page.IdGt}", url, Verdict.Ambiguous, $"wiki page of this name describes item {page.IdGt}, not item {e.ItemId}", string.Empty);
        }

        var acq = WikiSource.Acquisition(page.Text);
        var other = OtherSource(e);
        var sections = string.Join(";", acq.Sections);
        var value = (acq.OnlineStore ? "onlinestore;" : string.Empty) + sections + (acq.QuestRows.Count > 0 ? " quests=" + string.Join(";", acq.QuestRows) : string.Empty);
        if (!acq.HasSection)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, value, url, Verdict.NotModeled, "item page has no Acquisition section", string.Empty);
        }

        // "From Items" is the coffer the quest hands out; "Exchange" is either the Calamity Salvager re-buy of gear already earned or a
        // relic-step upgrade trade, which cannot be told apart from the heading alone.
        var exchange = acq.Sections.Where(s => s.Equals("Exchange", StringComparison.OrdinalIgnoreCase) || s.Equals("Exchanges", StringComparison.OrdinalIgnoreCase)).ToList();
        var nonQuest = acq.Sections.Where(s => !QuestOnlyWikiSections.Contains(s) && !exchange.Contains(s) && !s.Equals("From Items", StringComparison.OrdinalIgnoreCase)).ToList();
        var namesQuest = acq.QuestRows.Any(q => Names.Canon(q) == Names.Canon(questName));
        if (acq.OnlineStore || nonQuest.Count > 0)
        {
            if (other.Length > 0)
            {
                return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, value, url, Verdict.Match, $"other source already marked ({other})", string.Empty);
            }

            var what = (acq.OnlineStore ? "Online Store" : string.Empty) + (nonQuest.Count > 0 ? (acq.OnlineStore ? "; " : string.Empty) + string.Join("; ", nonQuest) : string.Empty);
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, value, url, Verdict.CatalogWrong, "wiki lists a non-quest acquisition (" + what + ") and the entry carries no otherSource", "T4");
        }

        if (exchange.Count > 0 && other.Length == 0)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, value, url, Verdict.Ambiguous, "wiki lists an Exchange under Acquisition (Calamity Salvager re-buy of earned gear, or an upgrade trade); needs a human look", string.Empty);
        }

        if (acq.QuestRows.Count > 0 && !namesQuest)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, value, url, Verdict.Ambiguous, "quest-only, but the wiki's quest list does not name this quest", string.Empty);
        }

        return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, value, url, Verdict.Match, acq.QuestRows.Count == 0 ? "acquisition sections are quest-only; no quest list template" : string.Empty, string.Empty);
    }

    private RewardRow CompareDutyUnlockWiki(UniqueRewardEntry e, string questName, WikiPage? page)
    {
        var kind = e.Kind.ToString();
        var url = page?.Url ?? WikiSource.PageUrl(questName);
        if (page is null || page.Missing || page.IsDisambiguation || page.QuestInfobox.Count == 0)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, string.Empty, url, page is { IsDisambiguation: true } ? Verdict.Ambiguous : Verdict.NotListed, page is { IsDisambiguation: true } ? "quest name is a disambiguation page with no target carrying id-gt=" + e.QuestRowId : "no wiki quest page", string.Empty);
        }

        var variantNote = string.Empty;
        if (page.IdGt != 0 && page.IdGt != e.QuestRowId)
        {
            // The city and legacy variants share one wiki page; they unlock the same duty, so the page still answers the question.
            var other = game.Catalog.GetByRowId(page.IdGt);
            if (other is null || Names.Canon(other.Name) != Names.Canon(questName))
            {
                return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, $"id-gt={page.IdGt}", url, Verdict.Ambiguous, $"wiki page of this name describes quest {page.IdGt}, which is not a same-name variant", string.Empty);
            }

            variantNote = $"same-name variant page (id-gt={page.IdGt})";
        }

        var unlocks = WikiSource.Unlocks(page.QuestInfobox.GetValueOrDefault("unlocks")).Where(u => WikiSource.DutyCodes.Contains(u.Code)).Select(u => u.Name).ToList();
        var value = Names.Join(unlocks);
        if (unlocks.Count == 0)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, value, url, Verdict.NotModeled, "wiki infobox names no duty unlock", string.Empty);
        }

        var cfcName = game.ContentFinderConditionNames.GetValueOrDefault(e.RewardId, e.RewardName);
        var ok = unlocks.Any(u => DutyNames.Same(u, cfcName) || DutyNames.Same(u, e.RewardName));
        if (ok)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, value, url, Verdict.Match, variantNote, string.Empty);
        }

        // "Palace of the Dead" for "the Palace of the Dead (Floors 1-10)", "Frontline" for "the Borderland Ruins (Secure)": the wiki names the series.
        var series = unlocks.Any(u => DutyNames.Series(u, cfcName));
        return series
            ? new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, value, url, Verdict.Match, "wiki names the duty series rather than the ContentFinderCondition" + (variantNote.Length > 0 ? "; " + variantNote : string.Empty), string.Empty)
            : new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Wiki, value, url, Verdict.Ambiguous, $"wiki names a different duty or a series name that does not contain the catalog's ({cfcName}); needs a human look", string.Empty);
    }

    private async Task<RewardRow> CompareDutyUnlockGarlandAsync(UniqueRewardEntry e, string questName, bool wikiConfirms, CancellationToken ct)
    {
        var kind = e.Kind.ToString();
        var url = GarlandSource.SiteUrl(e.QuestRowId);
        var g = await garland.GetAsync(e.QuestRowId, ct);
        if (g is null)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Garland, string.Empty, url, Verdict.Unresolved, "Garland document not fetched", string.Empty);
        }

        if (!g.Found)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Garland, string.Empty, url, Verdict.NotListed, "no Garland document", string.Empty);
        }

        if (g.InstanceId == 0)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Garland, string.Empty, url, Verdict.NotModeled, "Garland has no reward.instance (script-driven unlock)", string.Empty);
        }

        var cfcName = game.ContentFinderConditionNames.GetValueOrDefault(e.RewardId, e.RewardName);
        var ok = DutyNames.Same(g.InstanceName, cfcName) || DutyNames.Same(g.InstanceName, e.RewardName);
        if (ok)
        {
            return new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Garland, g.InstanceName, url, Verdict.Match, string.Empty, string.Empty);
        }

        // Garland carries one reward.instance (the sheet's InstanceContentUnlock); a quest that also unlocks a second duty by script shows that one on the wiki.
        return wikiConfirms
            ? new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Garland, g.InstanceName, url, Verdict.SourceWrong, $"Garland's single reward.instance {g.InstanceId} names {g.InstanceName}; the wiki confirms the catalog's {cfcName} as a second unlock of this quest", string.Empty)
            : new RewardRow(e.QuestRowId, questName, kind, e.RewardId, e.ItemId, e.RewardName, Claim(e), SourceNames.Garland, g.InstanceName, url, Verdict.Unresolved, $"Garland reward.instance {g.InstanceId} names {g.InstanceName}; catalog says {cfcName}", string.Empty);
    }
}
