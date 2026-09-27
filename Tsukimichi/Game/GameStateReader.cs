using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>
/// Reads everything a <see cref="CharacterSnapshot"/> needs from the client (QuestManager, PlayerState, UIState,
/// GameMain) and from <see cref="IPlayerState"/>. Every method that touches ClientStructs must run on the framework
/// thread; <see cref="Capture"/> checks and throws otherwise.
/// </summary>
public sealed class GameStateReader
{
    /// <summary>Bytes in the snapshot bitmask: room for every 16-bit quest id.</summary>
    public const int CompletedBitmaskBytes = 8192;

    /// <summary>Allied societies the client tracks (BeastReputation has 20 slots; ids are 1-based).</summary>
    public const byte TribeCount = 20;

    /// <summary>Grand Companies (Maelstrom, Twin Adder, Immortal Flames); ids are 1-based.</summary>
    private const int GrandCompanyCount = 3;

    private readonly IFramework framework;
    private readonly IPlayerState playerState;
    private readonly IDataManager data;
    private readonly IPluginLog log;

    private (byte RowId, int ExpIndex)[]? jobExpIndex;
    private CatalogIds? catalogIds;
    private bool measured;
    private bool festivalsWarned;

    public GameStateReader(IFramework framework, IPlayerState playerState, IDataManager data, IPluginLog log)
    {
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.playerState = playerState ?? throw new ArgumentNullException(nameof(playerState));
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// Achievement ids worth recording (those curated data gates quests on). Empty in V1, so
    /// <see cref="CharacterSnapshot.CompletedAchievements"/> stays empty while <see cref="CharacterSnapshot.AchievementsLoaded"/> is still read.
    /// </summary>
    public IReadOnlyList<uint> AchievementIds { get; set; } = [];

    /// <summary>Whether the client has a loaded character to read; cheap, framework thread only.</summary>
    public unsafe bool IsPlayerLoaded()
    {
        var ps = PlayerState.Instance();
        return ps != null && ps->IsLoaded && playerState.IsLoaded;
    }

    /// <summary>
    /// Captures the logged-in character. Framework thread only.
    /// Completion is read per catalog quest id (plus every previous-quest and lock id the catalog references) through
    /// <see cref="QuestManager.IsQuestComplete(ushort)"/> and written into a bitmask laid out as
    /// <see cref="CharacterSnapshot.IsCompleted"/> reads it, so the snapshot never depends on the client's bit order.
    /// </summary>
    /// <param name="catalog">Decides which quest and duty ids are read.</param>
    /// <param name="jobs">Category membership; not needed for the capture itself (levels are read per ClassJob row) and kept for callers that hold the bundle.</param>
    public unsafe CharacterSnapshot Capture(QuestCatalog catalog, ClassJobCategoryLookup? jobs)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _ = jobs;
        if (!framework.IsInFrameworkUpdateThread)
        {
            throw new InvalidOperationException("GameStateReader.Capture must run on the framework thread.");
        }

        var qm = QuestManager.Instance();
        var ps = PlayerState.Instance();
        var ui = UIState.Instance();
        if (qm == null || ps == null || ui == null)
        {
            throw new InvalidOperationException("Client state singletons are not available.");
        }

        if (!ps->IsLoaded || !playerState.IsLoaded)
        {
            throw new InvalidOperationException("Player state is not loaded.");
        }

        var ids = IdsFor(catalog);
        var stopwatch = measured ? null : Stopwatch.StartNew();

        // Completion bits.
        var completed = new byte[CompletedBitmaskBytes];
        foreach (var questId in ids.QuestIds)
        {
            if (QuestManager.IsQuestComplete(questId))
            {
                completed[questId >> 3] |= (byte)(1 << (questId & 7));
            }
        }

        // Journal.
        var normal = qm->NormalQuests;
        var accepted = new List<AcceptedQuest>(normal.Length);
        for (var i = 0; i < normal.Length; i++)
        {
            var work = normal[i];
            if (work.QuestId != 0)
            {
                accepted.Add(new AcceptedQuest(work.QuestId, work.Sequence));
            }
        }

        // Allied society dailies done this cycle.
        var dailyDone = new Dictionary<ushort, byte>();
        var daily = qm->DailyQuests;
        for (var i = 0; i < daily.Length; i++)
        {
            var work = daily[i];
            if (work.QuestId != 0 && qm->IsDailyQuestCompleted(work.QuestId))
            {
                dailyDone[work.QuestId] = work.Flags;
            }
        }

        // Unsynced levels per ClassJob row id.
        var levels = ps->ClassJobLevels;
        var jobLevels = new Dictionary<byte, short>();
        foreach (var (rowId, expIndex) in JobExpIndex())
        {
            if (expIndex < 0 || expIndex >= levels.Length)
            {
                continue;
            }

            var level = levels[expIndex];
            if (level > 0)
            {
                jobLevels[rowId] = level;
            }
        }

        // Grand Company and ranks, indexed by Grand Company id (slot 0 unused).
        var gcRanks = new byte[GrandCompanyCount + 1];
        var ranks = ps->GCRanks;
        for (var gc = 1; gc <= GrandCompanyCount && gc - 1 < ranks.Length; gc++)
        {
            gcRanks[gc] = ranks[gc - 1];
        }

        var grandCompany = ps->GrandCompany;
        if (grandCompany is >= 1 and <= GrandCompanyCount)
        {
            gcRanks[grandCompany] = ps->GetGrandCompanyRank();
        }

        // Allied society standing.
        var tribes = new Dictionary<byte, TribeStanding>();
        for (byte tribe = 1; tribe <= TribeCount; tribe++)
        {
            var rep = qm->GetBeastReputationById(tribe);
            if (rep != null && (rep->Rank != 0 || rep->Value != 0))
            {
                tribes[tribe] = new TribeStanding(rep->Rank, rep->Value);
            }
        }

        // Duties the catalog gates on.
        var unlockedInstances = new List<uint>();
        foreach (var instanceId in ids.InstanceIds)
        {
            if (UIState.IsInstanceContentCompleted(instanceId))
            {
                unlockedInstances.Add(instanceId);
            }
        }

        // Achievements: only loaded after the player opens the achievements window once per session.
        var achievementsLoaded = ui->Achievement.IsLoaded();
        var completedAchievements = new List<uint>();
        if (achievementsLoaded)
        {
            foreach (var achievementId in AchievementIds)
            {
                if (achievementId <= int.MaxValue && ui->Achievement.IsComplete((int)achievementId))
                {
                    completedAchievements.Add(achievementId);
                }
            }
        }

        var snapshot = new CharacterSnapshot
        {
            ContentId = playerState.ContentId,
            Name = playerState.CharacterName,
            World = playerState.HomeWorld.RowId,
            TakenUtc = DateTime.UtcNow,
            CompletedBits = completed,
            Accepted = accepted,
            DailyDone = dailyDone,
            JobLevels = jobLevels,
            GrandCompany = grandCompany,
            GcRanks = gcRanks,
            Tribes = tribes,
            TribeAllowance = (byte)Math.Min(qm->GetBeastTribeAllowance(), byte.MaxValue),
            LeveAllowance = qm->NumLeveAllowances,
            UnlockedInstances = unlockedInstances,
            ActiveFestivals = ReadActiveFestivals(),
            MaxExpansion = 0, // DRAFT-NEEDED E: PlayerState.MaxExpansion / MaxLevel exist but their semantics are unverified.
            LevelCap = 0,
            AchievementsLoaded = achievementsLoaded,
            CompletedAchievements = completedAchievements,
            CurrentJob = ps->CurrentClassJobId,
        };

        if (stopwatch is not null)
        {
            measured = true;
            log.Debug(
                "First capture: {Quests} quest ids, {Instances} duties, {Jobs} jobs in {Elapsed:F2} ms",
                ids.QuestIds.Length,
                ids.InstanceIds.Length,
                jobLevels.Count,
                stopwatch.Elapsed.TotalMilliseconds);
        }

        return snapshot;
    }

    /// <summary>Quest ids the allied societies offer today (done or not). Framework thread only.</summary>
    public unsafe HashSet<ushort> ReadDailyOffer()
    {
        if (!framework.IsInFrameworkUpdateThread)
        {
            throw new InvalidOperationException("GameStateReader.ReadDailyOffer must run on the framework thread.");
        }

        var offer = new HashSet<ushort>();
        var qm = QuestManager.Instance();
        if (qm == null)
        {
            return offer;
        }

        var daily = qm->DailyQuests;
        for (var i = 0; i < daily.Length; i++)
        {
            var questId = daily[i].QuestId;
            if (questId != 0)
            {
                offer.Add(questId);
            }
        }

        return offer;
    }

    /// <summary>What changed between two captures; see <see cref="SnapshotDiff.Compute"/>.</summary>
    public static SnapshotDiff Diff(CharacterSnapshot old, CharacterSnapshot @new) => SnapshotDiff.Compute(old, @new);

    private unsafe List<ushort> ReadActiveFestivals()
    {
        var result = new List<ushort>();
        var gm = GameMain.Instance();
        if (gm == null)
        {
            if (!festivalsWarned)
            {
                festivalsWarned = true;
                log.Warning("GameMain is not available; active festivals will read as none");
            }

            return result;
        }

        var festivals = gm->ActiveFestivals;
        for (var i = 0; i < festivals.Length; i++)
        {
            var id = festivals[i].Id;
            if (id != 0 && !result.Contains(id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>ClassJob row id to ExpArrayIndex, read once from the sheet.</summary>
    private (byte RowId, int ExpIndex)[] JobExpIndex()
    {
        if (jobExpIndex is not null)
        {
            return jobExpIndex;
        }

        var list = new List<(byte, int)>();
        foreach (var job in data.GetExcelSheet<ClassJob>())
        {
            if (job.RowId == 0 || job.RowId > byte.MaxValue || job.ExpArrayIndex < 0)
            {
                continue;
            }

            list.Add(((byte)job.RowId, job.ExpArrayIndex));
        }

        jobExpIndex = [.. list];
        return jobExpIndex;
    }

    /// <summary>The ids one catalog makes us read, cached per catalog instance.</summary>
    private CatalogIds IdsFor(QuestCatalog catalog)
    {
        if (catalogIds is { } cached && ReferenceEquals(cached.Catalog, catalog))
        {
            return cached;
        }

        var quests = new SortedSet<ushort>();
        var instances = new SortedSet<uint>();
        foreach (var quest in catalog.All)
        {
            quests.Add(quest.QuestId);
            foreach (var id in quest.PreviousQuests.QuestIds)
            {
                quests.Add(QuestRecord.ToQuestId(id));
            }

            foreach (var id in quest.QuestLocks)
            {
                quests.Add(QuestRecord.ToQuestId(id));
            }

            foreach (var id in quest.InstanceContentRequired)
            {
                instances.Add(id);
            }
        }

        var built = new CatalogIds(catalog, [.. quests], [.. instances]);
        catalogIds = built;
        return built;
    }

    private sealed record CatalogIds(QuestCatalog Catalog, ushort[] QuestIds, uint[] InstanceIds);
}
