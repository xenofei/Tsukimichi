using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>
/// Reads everything a <see cref="CharacterSnapshot"/> needs from the client (QuestManager, PlayerState, UIState,
/// GameMain, SatisfactionSupplyManager) and from <see cref="IPlayerState"/>. Every method that touches ClientStructs
/// must run on the framework thread; <see cref="Capture"/> checks and throws otherwise.
/// </summary>
public sealed class GameStateReader
{
    /// <summary>Bytes in the snapshot bitmask: room for every 16-bit quest id.</summary>
    public const int CompletedBitmaskBytes = 8192;

    /// <summary>Allied societies the client tracks (BeastReputation has 20 slots; ids are 1-based).</summary>
    public const byte TribeCount = 20;

    /// <summary>
    /// Custom delivery clients the client tracks: <c>SatisfactionSupplyManager.SatisfactionRanks</c> has 12 slots, one
    /// per SatisfactionNpc row 1..12, so slot i is row i + 1. A 13th client in a later patch overflows the fixed array
    /// until ClientStructs updates; its quests then read "not checked" rather than a wrong rank.
    /// </summary>
    public const int SatisfactionNpcSlots = 12;

    /// <summary>Prefix of the once-per-login log line comparing the three festival arrays the client keeps.</summary>
    public const string FestivalProbePrefix = "[festival probe]";

    /// <summary>Grand Companies (Maelstrom, Twin Adder, Immortal Flames); ids are 1-based.</summary>
    private const int GrandCompanyCount = 3;

    private readonly IFramework framework;
    private readonly IPlayerState playerState;
    private readonly IDataManager data;
    private readonly IPluginLog log;

    /// <summary>Scratch bitmask reused across captures; copied out only when the capture differs from the previous one.</summary>
    private readonly byte[] completedScratch = new byte[CompletedBitmaskBytes];

    private (byte RowId, int ExpIndex)[]? jobExpIndex;
    private CatalogIds? catalogIds;
    private bool measured;
    private bool festivalsWarned;
    private bool satisfactionWarned;
    private bool outOfRangeLogged;
    private ulong festivalProbeContentId;
    private IReadOnlyList<ushort> festivalProbeIds = [];
    private IReadOnlyList<ushort> festivalProbePhases = [];

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
    /// Whether <see cref="Capture"/> would succeed right now: the player is loaded and has a content id. After
    /// <see cref="IClientState.Login"/> the content id can lag the loaded flag by a few ticks. Framework thread only.
    /// </summary>
    public bool IsCharacterReadable() => IsPlayerLoaded() && playerState.ContentId != 0;

    /// <summary>The content id of the character the client has now (what <see cref="Capture"/> would stamp), 0 while none. Framework thread only.</summary>
    public ulong ContentId => playerState.ContentId;

    /// <summary>
    /// Captures the logged-in character. Framework thread only.
    /// Completion is read per catalog quest id (plus every previous-quest and lock id the catalog references) through
    /// <see cref="QuestManager.IsQuestComplete(ushort)"/> and written into a bitmask laid out as
    /// <see cref="CharacterSnapshot.IsCompleted"/> reads it, so the snapshot never depends on the client's bit order.
    /// Throws when the character is not fully loaded (including a content id of 0), so no snapshot is ever produced
    /// for an unidentified character.
    /// </summary>
    /// <param name="catalog">Decides which quest and duty ids are read.</param>
    /// <param name="jobs">Category membership; not needed for the capture itself (levels are read per ClassJob row) and kept for callers that hold the bundle.</param>
    public CharacterSnapshot Capture(QuestCatalog catalog, ClassJobCategoryLookup? jobs) => Capture(catalog, jobs, null);

    /// <summary>
    /// As <see cref="Capture(QuestCatalog, ClassJobCategoryLookup?)"/>, but when the completion mask equals
    /// <paramref name="previousCompleted"/> the returned snapshot shares that array instead of allocating a copy, so
    /// an unchanged poll costs no 8 KB allocation and the diff can short-circuit on reference equality.
    /// </summary>
    public unsafe CharacterSnapshot Capture(QuestCatalog catalog, ClassJobCategoryLookup? jobs, byte[]? previousCompleted)
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

        var contentId = playerState.ContentId;
        if (contentId == 0)
        {
            throw new InvalidOperationException("Player content id is not available yet.");
        }

        var ids = IdsFor(catalog);
        var stopwatch = measured ? null : Stopwatch.StartNew();

        // Completion bits. The client mask is finite; ids past its end are not completable and must not be looked up,
        // since IsQuestComplete does no bounds check of its own.
        var completed = completedScratch;
        Array.Clear(completed);
        var maskBits = qm->CompletedQuests.Length * 8;
        var outOfRange = 0;
        foreach (var questId in ids.QuestIds)
        {
            if (questId >= maskBits)
            {
                outOfRange++;
                continue;
            }

            if (QuestManager.IsQuestComplete(questId))
            {
                completed[questId >> 3] |= (byte)(1 << (questId & 7));
            }
        }

        if (outOfRange > 0 && !outOfRangeLogged)
        {
            outOfRangeLogged = true;
            log.Debug("{Count} catalog quest id(s) lie beyond the client's {Bits}-bit completion mask and read as not complete", outOfRange, maskBits);
        }

        var completedBits = previousCompleted is not null && completed.AsSpan().SequenceEqual(previousCompleted)
            ? previousCompleted
            : (byte[])completed.Clone();

        // Journal. QuestWork.AcceptClassJob is the ClassJob the quest was accepted on; the evaluator uses it as the
        // client's evidence that a job may take its base class's quests.
        var normal = qm->NormalQuests;
        var daily = qm->DailyQuests;
        var accepted = new List<AcceptedQuest>(normal.Length + daily.Length);
        for (var i = 0; i < normal.Length; i++)
        {
            var work = normal[i];
            if (work.QuestId != 0)
            {
                accepted.Add(new AcceptedQuest(work.QuestId, work.Sequence, work.AcceptClassJob));
            }
        }

        // Allied society dailies live in their own 12-slot array (DailyQuestWork: QuestId and Flags only, no step),
        // which holds the dailies accepted today, not the day's offer. One still in progress is in the journal and
        // joins Accepted with step 0; one already turned in keeps its slot with the completed flag and is done this cycle.
        var dailyDone = new Dictionary<ushort, byte>();
        for (var i = 0; i < daily.Length; i++)
        {
            var work = daily[i];
            if (work.QuestId == 0)
            {
                continue;
            }

            if (qm->IsDailyQuestCompleted(work.QuestId))
            {
                dailyDone[work.QuestId] = work.Flags;
            }
            else if (qm->IsQuestAccepted(work.QuestId) && !ContainsQuest(accepted, work.QuestId))
            {
                accepted.Add(new AcceptedQuest(work.QuestId, 0));
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

        var (festivalIds, festivalPhases) = ReadActiveFestivals();

        // The probe: once per character, and again whenever the captured (id, phase) pairs change within the session
        // (a chapter opening mid-event is the case it exists for).
        if (festivalProbeContentId != contentId
            || !Same(festivalIds, festivalProbeIds)
            || !Same(festivalPhases, festivalProbePhases))
        {
            festivalProbeContentId = contentId;
            festivalProbeIds = festivalIds;
            festivalProbePhases = festivalPhases;
            LogFestivalProbe(ps);
        }

        var snapshot = new CharacterSnapshot
        {
            ContentId = contentId,
            Name = playerState.CharacterName,
            World = playerState.HomeWorld.RowId,
            TakenUtc = DateTime.UtcNow,
            CompletedBits = completedBits,
            Accepted = accepted,
            DailyDone = dailyDone,
            JobLevels = jobLevels,
            GrandCompany = grandCompany,
            GcRanks = gcRanks,
            Tribes = tribes,
            TribeAllowance = (byte)Math.Min(qm->GetBeastTribeAllowance(), byte.MaxValue),
            LeveAllowance = qm->NumLeveAllowances,
            UnlockedInstances = unlockedInstances,
            ActiveFestivals = festivalIds,
            ActiveFestivalPhases = festivalPhases,
            SatisfactionRanks = ReadSatisfactionRanks(),
            // PlayerState.DeliveryLevel ("Carrier Level of Delivery Moogle Quests"), surfaced by Dalamud. Always
            // stored, 0 included: 0 is the real level of a character who has not unlocked the Delivery Moogle, and
            // it blocks every carrier-level gate (the lowest is 7). Only an older file (no field) reads "not checked".
            CarrierLevel = playerState.DeliveryLevel,
            // Account entitlement caps: PlayerState.MaxExpansion is the ExVersion row the account owns up to,
            // PlayerState.MaxLevel the level cap that comes with it. 0 means the client has not said (a snapshot
            // written by an older build reads the same), and the evaluator treats 0 as "not checked", never as level 0.
            MaxExpansion = ps->MaxExpansion,
            LevelCap = ps->MaxLevel,
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

    /// <summary>What changed between two captures; see <see cref="SnapshotDiff.Compute"/>.</summary>
    public static SnapshotDiff Diff(CharacterSnapshot old, CharacterSnapshot @new) => SnapshotDiff.Compute(old, @new);

    /// <summary>
    /// Running festivals as (id, phase) pairs from <c>GameMain.ActiveFestivals</c> (8 slots of
    /// <c>GameMain.Festival { ushort Id; ushort Phase }</c>), ids and phases in two parallel lists; a repeated id
    /// keeps its first slot. The client holds two more copies (<see cref="LogFestivalProbe"/>); GameMain's is read
    /// until the probe log from a live phased event says another one drives the quest givers.
    /// </summary>
    private unsafe (List<ushort> Ids, List<ushort> Phases) ReadActiveFestivals()
    {
        var ids = new List<ushort>();
        var phases = new List<ushort>();
        var gm = GameMain.Instance();
        if (gm == null)
        {
            if (!festivalsWarned)
            {
                festivalsWarned = true;
                log.Warning("GameMain is not available; active festivals will read as none");
            }

            return (ids, phases);
        }

        var festivals = gm->ActiveFestivals;
        for (var i = 0; i < festivals.Length; i++)
        {
            var festival = festivals[i];
            if (festival.Id != 0 && !ids.Contains(festival.Id))
            {
                ids.Add(festival.Id);
                phases.Add(festival.Phase);
            }
        }

        return (ids, phases);
    }

    /// <summary>
    /// Custom delivery satisfaction rank per client from <c>SatisfactionSupplyManager.SatisfactionRanks</c>, keyed by
    /// SatisfactionNpc row id (slot + 1), every slot included so a client not yet unlocked reads rank 0. Empty when the
    /// manager is not available or every slot reads 0 (the server may not have sent the ranks yet; see
    /// <see cref="SatisfactionRankSlots.ToRanks"/>), which the evaluator reads as "not checked".
    /// </summary>
    private unsafe Dictionary<byte, byte> ReadSatisfactionRanks()
    {
        var manager = SatisfactionSupplyManager.Instance();
        if (manager == null)
        {
            if (!satisfactionWarned)
            {
                satisfactionWarned = true;
                log.Warning("SatisfactionSupplyManager is not available; custom delivery ranks will read as not checked");
            }

            return new Dictionary<byte, byte>(SatisfactionNpcSlots);
        }

        return SatisfactionRankSlots.ToRanks(manager->SatisfactionRanks);
    }

    /// <summary>
    /// Once per character and again whenever the captured (id, phase) pairs change, the three festival arrays the
    /// client keeps, with phases, so the owner can compare them during a phased event and pick the one the quest
    /// givers follow: <c>GameMain.ActiveFestivals</c> (what the snapshot reads), <c>PlayerState.ActiveFestivalIds</c> / <c>ActiveFestivalPhases</c> (the server-sent per-character
    /// state) and <c>EventFramework.Festivals</c>. Logged at Information under <see cref="FestivalProbePrefix"/>.
    /// </summary>
    private unsafe void LogFestivalProbe(PlayerState* ps)
    {
        var gm = GameMain.Instance();
        var ef = EventFramework.Instance();
        var gameMain = gm == null ? "unavailable" : FormatFestivals(gm->ActiveFestivals);
        var playerStateText = FormatFestivals(ps->ActiveFestivalIds, ps->ActiveFestivalPhases);
        var eventFramework = ef == null ? "unavailable" : FormatFestivals(ef->Festivals);
        log.Information(
            "{Prefix} GameMain.ActiveFestivals [{GameMain}]; PlayerState.ActiveFestivalIds/Phases [{PlayerState}]; EventFramework.Festivals [{EventFramework}] (id/phase; GameMain is what the snapshot reads)",
            FestivalProbePrefix,
            gameMain,
            playerStateText,
            eventFramework);
    }

    private static bool Same(IReadOnlyList<ushort> a, IReadOnlyList<ushort> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatFestivals(Span<GameMain.Festival> festivals)
    {
        var parts = new List<string>(festivals.Length);
        for (var i = 0; i < festivals.Length; i++)
        {
            if (festivals[i].Id != 0)
            {
                parts.Add($"{festivals[i].Id}/{festivals[i].Phase}");
            }
        }

        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }

    private static string FormatFestivals(Span<ushort> ids, Span<ushort> phases)
    {
        var parts = new List<string>(ids.Length);
        for (var i = 0; i < ids.Length; i++)
        {
            if (ids[i] != 0)
            {
                parts.Add(i < phases.Length ? $"{ids[i]}/{phases[i]}" : $"{ids[i]}/?");
            }
        }

        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }

    /// <summary>
    /// ClassJob row id to ExpArrayIndex, read once from the sheet. Rows without a name (placeholder rows the sheet
    /// carries past the last real job) are skipped even when they point at an exp slot, so a snapshot never lists a
    /// "Job 44".
    /// </summary>
    private (byte RowId, int ExpIndex)[] JobExpIndex()
    {
        if (jobExpIndex is not null)
        {
            return jobExpIndex;
        }

        var list = new List<(byte, int)>();
        foreach (var job in data.GetExcelSheet<ClassJob>())
        {
            if (job.RowId == 0 || job.RowId > byte.MaxValue || job.ExpArrayIndex < 0 || job.Name.IsEmpty)
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

    /// <summary>Linear scan; the journal holds at most 30 quests plus 12 dailies, so a set is not worth its allocation.</summary>
    private static bool ContainsQuest(List<AcceptedQuest> accepted, ushort questId)
    {
        foreach (var quest in accepted)
        {
            if (quest.QuestId == questId)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record CatalogIds(QuestCatalog Catalog, ushort[] QuestIds, uint[] InstanceIds);
}
