using System;
using System.Collections.Generic;
using System.Threading;
using Dalamud.Utility;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.GameData;

namespace Tsukimichi;

/// <summary>
/// What every quest opens (feature plan v6 K1): the <see cref="QuestUnlocksSource"/> every surface reads. The sheet
/// links (<see cref="UnlockLinkReader"/>) and the duty kinds are read once, on the first build's worker, in the client's
/// language; the index is rebuilt off the frame for each new catalog, from the shipped unique-reward data with the
/// curated overlay and without the user's verdicts (as the plan's tags read it).
/// </summary>
public sealed partial class Plugin
{
    private QuestUnlocksSource? questUnlocks;

    private QuestUnlocksSource BuildQuestUnlocksSource()
    {
        var session = Session;
        var data = DataManager;
        var log = Log;
        // A sheet that cannot be read leaves only its part out: the reader logs and skips a failed part, and a factory
        // that still throws gives the empty value, so the index builds without it instead of caching the exception.
        var links = new Lazy<UnlockLinks>(
            () =>
            {
                try
                {
                    return UnlockLinkReader.Read(data.Excel, data.Language.ToLumina(), log: message => log.Warning(message));
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "The sheet links of what quests open could not be read; areas, aetherytes and duty icons are left out");
                    return UnlockLinks.Empty;
                }
            },
            LazyThreadSafetyMode.ExecutionAndPublication);
        var duties = new Lazy<PlanDuties>(
            () =>
            {
                try
                {
                    return DutyIndex.Build(data.Excel, data.Language.ToLumina());
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "The duty kinds could not be read for what quests open; every duty files under Other duty");
                    return PlanDuties.Empty;
                }
            },
            LazyThreadSafetyMode.ExecutionAndPublication);
        return new QuestUnlocksSource(
            () => session.Bundle?.Catalog,
            catalog =>
            {
                var started = System.Diagnostics.Stopwatch.GetTimestamp();
                var rewards = UniqueRewardCatalog.Build(session.UniqueRewards, new Dictionary<uint, UniqueOverride>(), session.Curated);
                var index = QuestUnlocks.Build(catalog, rewards, duties.Value, links.Value, session.Curated);
                log.Debug("Quest unlocks: {Count} quests in {Elapsed:0} ms", index.Count, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                return index;
            },
            onError: ex => log.Warning(ex, "What quests open could not be worked out; the Unlocks section is left out"));
    }
}
