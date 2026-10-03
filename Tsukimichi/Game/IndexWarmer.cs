using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>
/// No first-open freezes (feature plan v6 A11): every sheet index a pane used to build on its first draw is built here
/// instead, on workers started when the plugin loads, in the client's language. The panes only read
/// <see cref="WarmedValue{T}.Value"/> and show their light "loading" state for the moment a build takes; nothing on the
/// draw thread builds or waits. The indexes:
/// <list type="bullet">
/// <item><see cref="Flight"/>: the flying zones and their aether currents (the Flight tab);</item>
/// <item><see cref="Duties"/>: the duty kinds (the Plan tab, the unlocks index);</item>
/// <item><see cref="Aetherytes"/>: aetherytes and aethernet shards (travel, the interior warm-up);</item>
/// <item><see cref="DutyRuns"/>: the duties AutoDuty can run (the Duties section, the catch-up);</item>
/// <item><see cref="RewardArt"/>: every Moonlit reward's own game art (G6);</item>
/// <item><see cref="PaneIcons"/>: the role, society, Grand Company, achievement and Duty Finder icons of the Characters,
/// Journal table and Plan panes (UI-5d).</item>
/// </list>
/// When the last one lands, one log line gives each build's time: the cost the first open of each pane paid before.
/// </summary>
public sealed class IndexWarmer
{
    private readonly IPluginLog log;

    /// <param name="data">The game's sheets.</param>
    /// <param name="session">The shipped and curated unique rewards, whose art is read.</param>
    /// <param name="allZonesFormat">The Flight list's "all zones" label, read on this thread (the UI language).</param>
    public IndexWarmer(IDataManager data, SessionState session, string allZonesFormat, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(session);
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        var language = data.Language.ToLumina();
        Flight = new WarmedValue<FlightIndex>(
            () => FlightIndex.Build(data.Excel, language, allZonesFormat),
            ex => log.Warning(ex, "Flight index could not be built; the Flight view is empty"));
        Duties = new WarmedValue<PlanDuties>(
            () => DutyIndex.Build(data.Excel, language),
            ex => log.Warning(ex, "Duty kinds could not be read; the plan and the unlocks index file every duty under Other"));
        Aetherytes = new WarmedValue<AetheryteIndex>(
            () => AetheryteIndex.Build(data.Excel, language),
            ex => log.Warning(ex, "Aetheryte index could not be built; teleport to giver is unavailable"));
        DutyRuns = new WarmedValue<DutyRunIndex>(
            () => DutyRunSheets.Build(data.Excel, language),
            ex => log.Warning(ex, "Duty index unavailable; the Duties section is hidden"));
        RewardArt = new WarmedValue<RewardArtIndex>(
            () =>
            {
                // The shipped entries with the curated ones merged in, without verdicts: verdicts never add a reward.
                var rewards = UniqueRewardCatalog.Build(session.UniqueRewards, new Dictionary<uint, UniqueOverride>(), session.Curated);
                return RewardArtIndex.Build(
                    data.Excel,
                    language,
                    rewards.All,
                    icon => data.FileExists(RewardArtIndex.IconPath(icon)),
                    message => log.Warning("{Message}", message));
            },
            ex => log.Warning(ex, "Reward art could not be read; Moonlit shows the rewards' item icons"));
        PaneIcons = new WarmedValue<PaneIconSheets>(
            () => PaneIconSheets.Build(data.Excel, message => log.Warning("{Message}", message)),
            ex => log.Warning(ex, "Pane icons could not be read; the Characters, Journal and Plan rows keep their stand-ins"));
    }

    public WarmedValue<FlightIndex> Flight { get; }

    public WarmedValue<PlanDuties> Duties { get; }

    public WarmedValue<AetheryteIndex> Aetherytes { get; }

    public WarmedValue<DutyRunIndex> DutyRuns { get; }

    public WarmedValue<RewardArtIndex> RewardArt { get; }

    public WarmedValue<PaneIconSheets> PaneIcons { get; }

    /// <summary>Whether every index has landed (or failed).</summary>
    public bool IsDone => Flight.IsDone && Duties.IsDone && Aetherytes.IsDone && DutyRuns.IsDone && RewardArt.IsDone && PaneIcons.IsDone;

    /// <summary>Starts every build on the thread pool, once; the returned task ends when all have landed and logged.</summary>
    public Task Start()
    {
        var started = Stopwatch.GetTimestamp();
        var all = Task.WhenAll(Flight.Start(), Duties.Start(), Aetherytes.Start(), DutyRuns.Start(), RewardArt.Start(), PaneIcons.Start());
        return all.ContinueWith(
            _ => log.Information(
                "Indexes warmed off the frame in {Total:F0} ms: flight {Flight:F0} ms, duty kinds {Duties:F0} ms, aetherytes {Aetherytes:F0} ms, AutoDuty duties {DutyRuns:F0} ms, reward art {Art:F0} ms ({ArtCount} icons, {Pictures} pictures), pane icons {PaneIcons:F0} ms; the first open of a pane builds none of them",
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Flight.BuildMs,
                Duties.BuildMs,
                Aetherytes.BuildMs,
                DutyRuns.BuildMs,
                RewardArt.BuildMs,
                RewardArt.Value?.Count ?? 0,
                RewardArt.Value?.ArtCount ?? 0,
                PaneIcons.BuildMs),
            TaskScheduler.Default);
    }
}
