using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Game;

/// <summary>
/// The "Game updated" report (<see cref="DataFreshness"/>) for the session's current catalog, worked out once per
/// catalog instance: the main window's strip, the Added in filter's "New since data" value and the Settings › About
/// line read it. A catalog with quests the data has never seen is logged once. Framework thread.
/// </summary>
public sealed class DataFreshnessSource
{
    private readonly Func<QuestCatalog?> catalog;
    private readonly QuestPatches patches;
    private readonly string dataVersion;
    private readonly string clientVersion;
    private readonly IPluginLog log;
    private QuestCatalog? builtFor;
    private DataFreshnessReport report = DataFreshnessReport.None;

    /// <param name="catalog">The session's catalog; null while it is loading.</param>
    /// <param name="patches"><c>quest_patches.json</c> as loaded at startup.</param>
    /// <param name="dataVersion">The game version the shipped data was built from.</param>
    /// <param name="clientVersion">The client's game version, read once at load.</param>
    public DataFreshnessSource(Func<QuestCatalog?> catalog, QuestPatches patches, string dataVersion, string clientVersion, IPluginLog log)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.patches = patches ?? throw new ArgumentNullException(nameof(patches));
        this.dataVersion = dataVersion ?? string.Empty;
        this.clientVersion = clientVersion ?? string.Empty;
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>The report for the current catalog; <see cref="DataFreshnessReport.None"/> while none is built.</summary>
    public DataFreshnessReport Current
    {
        get
        {
            if (catalog() is not { } current)
            {
                return DataFreshnessReport.None;
            }

            if (!ReferenceEquals(current, builtFor))
            {
                builtFor = current;
                report = DataFreshness.Evaluate(LiveIds(current), patches, dataVersion, clientVersion);
                if (report.ShowStrip)
                {
                    log.Information(
                        "Game updated: {Count} quests are newer than the shipped data (data {DataVersion}, client {ClientVersion})",
                        report.NewQuests,
                        report.DataVersion,
                        report.ClientVersion);
                }
            }

            return report;
        }
    }

    /// <summary>The row ids of the quests still in the game: a removed quest is never "new", as in the Added in filter.</summary>
    private static IEnumerable<uint> LiveIds(QuestCatalog catalog)
    {
        foreach (var quest in catalog.All)
        {
            if (!quest.IsRemoved)
            {
                yield return quest.RowId;
            }
        }
    }
}
