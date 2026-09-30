using System.Globalization;
using System.Text;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Verify.Game;
using Tsukimichi.Verify.Output;
using Tsukimichi.Verify.Sources;

namespace Tsukimichi.Verify.Verify;

/// <summary>Where a quest's patch came from in a seed run.</summary>
internal enum PatchOrigin
{
    None,
    GarlandPatchDocument,
    GarlandQuestDocument,
    PreviousFile,
}

/// <summary>One quest's outcome in a seed run.</summary>
internal sealed record PatchSeedRow(uint RowId, string Name, byte Expansion, bool Retired, string Patch, PatchOrigin Origin);

/// <summary>A disagreement or doubt the seed run found; <paramref name="Kind"/> names the check.</summary>
internal sealed record PatchFinding(string Kind, uint RowId, string Name, string Detail);

/// <summary>
/// <c>Tsukimichi.Verify patches</c> (P8): builds <c>Tsukimichi/Data/quest_patches.json</c> from Garland Tools' patch
/// data for every named quest in the catalog and cross-checks it against the game data. The patch documents cover the
/// catalog in about forty requests; the per-quest documents fill the gaps (from the cache first, then politely
/// fetched, resumable). A quest Garland cannot place keeps the patch the previous file gave it, else stays unknown.
/// <para>
/// The game data has no patch column on the Quest sheet (checked against Lumina.Excel 7.5.0's schema: only
/// <c>Item.PatchNumber</c> exists, and the MSQ chapter sheets name parts, not patches), so the cross-checks are
/// structural: a quest cannot be older than its expansion (<c>Quest.Expansion</c> + 2 is the lowest major patch), the
/// name Garland lists must be the catalog's (a reused row id would not be), and the per-quest document must agree
/// with the patch document.
/// </para>
/// </summary>
internal sealed class PatchSeeder(GameCatalog game, GarlandPatchSource garland, QuestPatches previous, TextWriter log)
{
    public List<PatchSeedRow> Rows { get; } = [];
    public List<PatchFinding> Findings { get; } = [];
    public List<(string Series, int Quests, bool Fetched)> SeriesStatus { get; } = [];
    public string GarlandCurrent { get; private set; } = string.Empty;
    public int QuestDocumentsCached { get; private set; }
    public int QuestDocumentsFetched { get; private set; }
    public int QuestDocumentsPending { get; private set; }
    public int QuestDocumentsAgree { get; private set; }

    /// <param name="fetchQuestDocuments">Fetch a per-quest document for each quest the patch documents do not list (the cache is read either way).</param>
    /// <param name="questDocumentLimit">At most this many per-quest fetches in this run (0 = no limit); the rest wait for the next run.</param>
    public async Task<QuestPatches> RunAsync(bool fetchQuestDocuments, int questDocumentLimit, CancellationToken ct)
    {
        var (series, current) = await garland.GetSeriesAsync(ct);
        GarlandCurrent = current;
        log.WriteLine($"patches: Garland tracks {series.Count} patch series, current {current}");

        // Row id → (patch, name) from the patch documents; a quest listed twice keeps its oldest patch (first seen).
        var index = new Dictionary<uint, GarlandPatchQuest>();
        foreach (var s in series)
        {
            var quests = await garland.GetSeriesQuestsAsync(s, ct);
            SeriesStatus.Add((s, quests?.Count ?? 0, quests is not null));
            if (quests is null)
            {
                log.WriteLine($"patches: series {s} not available; rerun to resume");
                continue;
            }

            foreach (var q in quests)
            {
                if (index.TryGetValue(q.RowId, out var seen))
                {
                    if (PatchVersion.Compare(seen.Patch, q.Patch) != 0)
                    {
                        Findings.Add(new PatchFinding("listedTwice", q.RowId, q.Name, $"Garland lists it under {seen.Patch} and {q.Patch}; the older is kept"));
                    }

                    if (PatchVersion.Compare(q.Patch, seen.Patch) < 0)
                    {
                        index[q.RowId] = q;
                    }
                }
                else
                {
                    index[q.RowId] = q;
                }
            }
        }

        log.WriteLine($"patches: {index.Count} quests listed across the patch documents");

        var cachedDocs = garland.CachedQuestDocuments();
        QuestDocumentsCached = cachedDocs.Count;
        foreach (var (id, docPatch) in cachedDocs)
        {
            if (!index.TryGetValue(id, out var listed))
            {
                continue;
            }

            if (PatchVersion.Compare(docPatch, listed.Patch) == 0)
            {
                QuestDocumentsAgree++;
            }
            else
            {
                Findings.Add(new PatchFinding("documentsDisagree", id, listed.Name, $"patch document {listed.Patch}, quest document {docPatch}; the patch document is kept"));
            }
        }

        var catalog = game.Catalog.All.OrderBy(q => q.RowId).ToList();
        var fromQuestDocs = new Dictionary<uint, string>();
        var missing = catalog.Where(q => !index.ContainsKey(q.RowId)).ToList();
        log.WriteLine($"patches: {missing.Count} catalog quests are not in a patch document; {missing.Count(q => cachedDocs.ContainsKey(q.RowId))} of them have a cached quest document");
        foreach (var quest in missing)
        {
            if (cachedDocs.TryGetValue(quest.RowId, out var cached))
            {
                fromQuestDocs[quest.RowId] = cached;
                continue;
            }

            if (!fetchQuestDocuments || (questDocumentLimit > 0 && QuestDocumentsFetched >= questDocumentLimit))
            {
                QuestDocumentsPending++;
                continue;
            }

            var patch = await garland.GetQuestPatchAsync(quest.RowId, ct);
            if (patch is null)
            {
                QuestDocumentsPending++;
                continue;
            }

            QuestDocumentsFetched++;
            if (patch.Length > 0)
            {
                fromQuestDocs[quest.RowId] = patch;
            }

            if (QuestDocumentsFetched % 25 == 0)
            {
                log.WriteLine($"patches: {QuestDocumentsFetched} quest documents fetched");
            }
        }

        var merged = new Dictionary<uint, string>();
        foreach (var quest in catalog)
        {
            string patch;
            PatchOrigin origin;
            if (index.TryGetValue(quest.RowId, out var listed))
            {
                (patch, origin) = (listed.Patch, PatchOrigin.GarlandPatchDocument);
                if (!SameName(listed.Name, quest.Name))
                {
                    Findings.Add(new PatchFinding("nameDiffers", quest.RowId, quest.Name, $"Garland names row {quest.RowId} \"{listed.Name}\""));
                }
            }
            else if (fromQuestDocs.TryGetValue(quest.RowId, out var doc))
            {
                (patch, origin) = (doc, PatchOrigin.GarlandQuestDocument);
            }
            else if (previous.For(quest.RowId) is { Length: > 0 } kept)
            {
                (patch, origin) = (kept, PatchOrigin.PreviousFile);
            }
            else
            {
                (patch, origin) = (string.Empty, PatchOrigin.None);
            }

            var before = previous.For(quest.RowId);
            if (before.Length > 0 && patch.Length > 0 && PatchVersion.Compare(before, patch) != 0)
            {
                Findings.Add(new PatchFinding("changedFromPrevious", quest.RowId, quest.Name, $"previous file {before}, now {patch} ({Origin(origin)})"));
            }

            if (patch.Length > 0 && MajorOf(patch) < quest.Expansion + 2)
            {
                Findings.Add(new PatchFinding("olderThanExpansion", quest.RowId, quest.Name, $"patch {patch} predates the quest's expansion (Quest.Expansion {quest.Expansion}, first patch {quest.Expansion + 2}.0)"));
            }

            merged[quest.RowId] = patch;
            Rows.Add(new PatchSeedRow(quest.RowId, quest.Name, quest.Expansion, quest.IsRetired, patch, origin));
        }

        // A quest the game dropped since the previous file keeps its line, so a later return is not taken for new.
        foreach (var (id, patch) in previous.ByRowId)
        {
            merged.TryAdd(id, patch);
        }

        var assigned = Rows.Count(r => r.Origin is PatchOrigin.GarlandPatchDocument or PatchOrigin.GarlandQuestDocument);
        // A rerun against the same game version (resuming, or re-deriving offline) replaces its own line.
        var history = previous.History
            .Where(h => !(h.Source == QuestPatches.SourceGarland && h.GameVersion == game.GameVersion))
            .Append(new QuestPatchesRun(game.GameVersion, QuestPatches.SourceGarland, current, assigned))
            .ToList();
        return new QuestPatches(game.GameVersion, merged, history);
    }

    /// <summary>Writes <c>quest-patches-report.md</c>: coverage, sources, per-series counts and row id ranges, and every finding.</summary>
    public void WriteReport(string path, QuestPatches result, string runDate, string userAgent, double rate, bool offline)
    {
        var sb = new StringBuilder();
        var total = Rows.Count;
        var known = Rows.Count(r => r.Patch.Length > 0);
        var live = Rows.Where(r => !r.Retired).ToList();
        var liveKnown = live.Count(r => r.Patch.Length > 0);
        sb.AppendLine("# Quest patches (P8)");
        sb.AppendLine();
        sb.AppendLine("Written by `Tsukimichi.Verify patches`; do not edit by hand. The patch each quest was added in, as shipped in `Tsukimichi/Data/quest_patches.json`, seeded from Garland Tools' patch data (facts only: ids and patch numbers) and cross-checked against the game data. After a game patch, `tools/regen.ps1 -Patch <x.y>` stamps new quest ids offline; this report is only rewritten when the seed is re-run.");
        sb.AppendLine();
        sb.AppendLine("| Field | Value |");
        sb.AppendLine("|---|---|");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Game version | `{game.GameVersion}` |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Run | {runDate}, UA `{userAgent}`, {rate:F1} s per request{(offline ? ", offline" : string.Empty)} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Garland's current patch | {GarlandCurrent} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Newest patch in the data | {result.Newest} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Coverage, every named quest | {known} / {total} ({Percent(known, total)}) |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Coverage, quests still in the game | {liveKnown} / {live.Count} ({Percent(liveKnown, live.Count)}) |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| From Garland's patch documents | {Rows.Count(r => r.Origin == PatchOrigin.GarlandPatchDocument)} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| From Garland's quest documents | {Rows.Count(r => r.Origin == PatchOrigin.GarlandQuestDocument)} ({QuestDocumentsCached} quest documents were already cached, {QuestDocumentsFetched} fetched this run, {QuestDocumentsPending} still to fetch) |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Kept from the previous file | {Rows.Count(r => r.Origin == PatchOrigin.PreviousFile)} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Unknown | {total - known} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Cached quest documents that agree with the patch documents | {QuestDocumentsAgree} |");
        sb.AppendLine();

        sb.AppendLine("## Cross-checks against the game data");
        sb.AppendLine();
        sb.AppendLine("The Quest sheet carries no patch or version column (Lumina.Excel 7.5.0's schema has `PatchNumber` on Item only; `QuestRedoChapterUI` names MSQ parts, not patches), so nothing in the game data states a quest's patch. What it can refute:");
        sb.AppendLine();
        sb.AppendLine("- **olderThanExpansion**: a quest whose patch is older than its `Quest.Expansion` allows (an Endwalker quest cannot be from 5.x). The reverse is normal: event, Gold Saucer and feature quests added in later patches often keep `Expansion` 0.");
        sb.AppendLine("- **nameDiffers**: Garland's name for the row id is not the catalog's, which would mean a reused or renamed row.");
        sb.AppendLine("- **documentsDisagree**: Garland's per-quest document and its patch document give different patches for one quest.");
        sb.AppendLine("- **listedTwice**: a quest under two patches in Garland's patch documents; the older is kept (first seen).");
        sb.AppendLine("- **changedFromPrevious**: the committed file had another patch for the quest.");
        sb.AppendLine();
        var byKind = Findings.GroupBy(f => f.Kind).ToDictionary(g => g.Key, g => g.ToList());
        sb.AppendLine("| Check | Findings |");
        sb.AppendLine("|---|---:|");
        foreach (var kind in new[] { "olderThanExpansion", "nameDiffers", "documentsDisagree", "listedTwice", "changedFromPrevious" })
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {kind} | {byKind.GetValueOrDefault(kind)?.Count ?? 0} |");
        }

        sb.AppendLine();
        var expansionMismatch = Rows.Count(r => r.Patch.Length > 0 && MajorOf(r.Patch) > r.Expansion + 2);
        sb.AppendLine(CultureInfo.InvariantCulture, $"{Rows.Count(r => r.Patch.Length > 0 && MajorOf(r.Patch) == r.Expansion + 2)} quests carry a patch of their own expansion; {expansionMismatch} carry a later expansion's patch while filed under an earlier `Quest.Expansion` (expected for events and features).");
        sb.AppendLine();

        sb.AppendLine("## Quests per series");
        sb.AppendLine();
        sb.AppendLine("Row ids rise with the patches for most genres, so each series' id range is a rough check on its own; overlaps come from ids the game fills in later.");
        sb.AppendLine();
        sb.AppendLine("| Series | Quests | Patches | Lowest row id | Highest row id | Garland document |");
        sb.AppendLine("|---|---:|---|---:|---:|---|");
        var status = SeriesStatus.ToDictionary(s => s.Series, s => s);
        foreach (var group in Rows.Where(r => r.Patch.Length > 0).GroupBy(r => PatchVersion.Series(r.Patch)).OrderByDescending(g => g.Key, PatchVersion.Comparer))
        {
            var patches = string.Join(", ", group.GroupBy(r => r.Patch).OrderBy(g => g.Key, PatchVersion.Comparer).Select(g => $"{g.Key} ({g.Count()})"));
            var doc = status.TryGetValue(group.Key, out var s) ? (s.Fetched ? $"[{group.Key}]({GarlandPatchSource.SiteUrl(group.Key)})" : "not fetched") : "none";
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {group.Key}x | {group.Count()} | {patches} | {group.Min(r => r.RowId)} | {group.Max(r => r.RowId)} | {doc} |");
        }

        sb.AppendLine();
        foreach (var kind in new[] { "olderThanExpansion", "nameDiffers", "documentsDisagree", "listedTwice", "changedFromPrevious" })
        {
            if (!byKind.TryGetValue(kind, out var list) || list.Count == 0)
            {
                continue;
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"## {kind} ({list.Count})");
            sb.AppendLine();
            sb.AppendLine("| Row id | Quest | Detail |");
            sb.AppendLine("|---:|---|---|");
            foreach (var f in list.OrderBy(f => f.RowId))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {f.RowId} | {Cell(f.Name)} | {Cell(f.Detail)} |");
            }

            sb.AppendLine();
        }

        var unknown = Rows.Where(r => r.Patch.Length == 0).ToList();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## Unknown ({unknown.Count})");
        sb.AppendLine();
        if (unknown.Count == 0)
        {
            sb.AppendLine("None.");
        }
        else
        {
            sb.AppendLine("Quests Garland could not place (no patch document lists them and their quest document has no patch, or was not fetched yet). They read as unknown in the plugin: no \"Added in\" line, never in an \"Added in\" filter or the New this patch group.");
            sb.AppendLine();
            sb.AppendLine("| Row id | Quest | Expansion | Removed from the game |");
            sb.AppendLine("|---:|---|---:|---|");
            foreach (var r in unknown)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {r.RowId} | {Cell(r.Name)} | {r.Expansion} | {(r.Retired ? "yes" : string.Empty)} |");
            }
        }

        Csv.Atomic(path, sb.ToString());
    }

    private static string Origin(PatchOrigin origin) => origin switch
    {
        PatchOrigin.GarlandPatchDocument => "Garland patch document",
        PatchOrigin.GarlandQuestDocument => "Garland quest document",
        PatchOrigin.PreviousFile => "previous file",
        _ => "none",
    };

    private static int MajorOf(string patch)
    {
        var dot = patch.IndexOf('.', StringComparison.Ordinal);
        return int.TryParse(dot < 0 ? patch : patch[..dot], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ? major : 0;
    }

    private static bool SameName(string a, string b) =>
        string.Equals(Names.Clean(a), Names.Clean(b), StringComparison.OrdinalIgnoreCase);

    private static string Percent(int part, int whole) => whole == 0 ? "n/a" : (100.0 * part / whole).ToString("F1", CultureInfo.InvariantCulture) + "%";

    private static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
