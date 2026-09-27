using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Tsukimichi.Core.Model;
using Tsukimichi.GameData;

namespace Tsukimichi.Data;

/// <summary>
/// Builds the quest catalog from the game sheets on a thread-pool thread.
/// Only <see cref="IDataManager.Excel"/> is touched off-thread (Lumina sheet reads are thread-safe); no other Dalamud
/// service is used from the worker, and <see cref="IPluginLog"/> is thread-safe.
/// </summary>
public sealed class LuminaCatalogLoader(IDataManager data, IPluginLog log)
{
    /// <summary>Builds the catalog and its lookups. Faults with <see cref="OperationCanceledException"/> when cancelled.</summary>
    public Task<CatalogBundle> BuildBundleAsync(ClientLanguage language, CancellationToken ct)
        => Task.Run(() => Build(language, ct), ct);

    /// <summary>Builds just the <see cref="QuestCatalog"/>; convenience over <see cref="BuildBundleAsync"/>.</summary>
    public async Task<QuestCatalog> BuildAsync(ClientLanguage language, CancellationToken ct)
        => (await BuildBundleAsync(language, ct).ConfigureAwait(false)).Catalog;

    private CatalogBundle Build(ClientLanguage language, CancellationToken ct)
    {
        var luminaLanguage = language.ToLumina();
        log.Debug("Catalog build starting ({Language})", luminaLanguage);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var bundle = CatalogMapper.Map(data.Excel, luminaLanguage, ct, line => log.Debug("Catalog: {Line}", line));
            log.Information(
                "Catalog built: {Count} quests in {Elapsed} ms ({Language})",
                bundle.Catalog.Count,
                stopwatch.ElapsedMilliseconds,
                bundle.Language);
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
