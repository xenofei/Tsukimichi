using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;

namespace Tsukimichi.Data;

/// <summary>
/// Builds the quest catalog from the game sheets on a thread-pool thread.
/// Only <see cref="IDataManager.Excel"/> is touched off-thread (Lumina sheet reads are thread-safe); no other Dalamud
/// service is used from the worker, and <see cref="IPluginLog"/> is thread-safe.
/// </summary>
/// <param name="curated">The curated overlay the refiler reads; <see cref="CuratedData.Empty"/> runs the rules alone.</param>
/// <param name="patches"><c>quest_patches.json</c>, for <see cref="Core.Model.QuestRecord.AddedIn"/>; null leaves every patch unknown.</param>
public sealed class LuminaCatalogLoader(IDataManager data, IPluginLog log, CuratedData curated, QuestPatches? patches = null)
{
    /// <summary>Builds the catalog and its lookups under <paramref name="filing"/>. Faults with <see cref="OperationCanceledException"/> when cancelled.</summary>
    public Task<CatalogBundle> BuildBundleAsync(ClientLanguage language, JournalFiling filing, CancellationToken ct)
        => Task.Run(() => Build(language, filing, ct), ct);

    /// <summary>Builds just the <see cref="QuestCatalog"/>; convenience over <see cref="BuildBundleAsync"/>.</summary>
    public async Task<QuestCatalog> BuildAsync(ClientLanguage language, JournalFiling filing, CancellationToken ct)
        => (await BuildBundleAsync(language, filing, ct).ConfigureAwait(false)).Catalog;

    private CatalogBundle Build(ClientLanguage language, JournalFiling filing, CancellationToken ct)
    {
        var luminaLanguage = language.ToLumina();
        log.Debug("Catalog build starting ({Language}, {Filing} filing)", luminaLanguage, filing);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var bundle = CatalogMapper.Map(data.Excel, luminaLanguage, ct, line => log.Debug("Catalog: {Line}", line), filing, curated, patches);
            log.Information(
                "Catalog built: {Count} quests in {Elapsed} ms ({Language}, {Filing} filing)",
                bundle.Catalog.Count,
                stopwatch.ElapsedMilliseconds,
                bundle.Language,
                filing);
            return bundle;
        }
        catch (OperationCanceledException)
        {
            log.Debug("Catalog build cancelled after {Elapsed} ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Catalog build failed after {Elapsed} ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
