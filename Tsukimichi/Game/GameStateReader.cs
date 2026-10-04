using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Unique;
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

    /// <summary>Prefix of the log line comparing the three festival arrays the client keeps (see <see cref="LogFestivalProbe"/>).</summary>
    public const string FestivalProbePrefix = "[festival probe]";

    /// <summary>
    /// QuestRepeatFlag rows (0 to 15): <c>QuestManager.QuestRepeatFlags</c> is two bytes, one bit per row. Row 0 is
    /// "no flag".
    /// </summary>
    public const int RepeatFlagRows = 16;

    /// <summary>Grand Companies (Maelstrom, Twin Adder, Immortal Flames); ids are 1-based.</summary>
    private const int GrandCompanyCount = 3;

    /// <summary>What a capture saves when no collectible flags are wired up: one shared empty map.</summary>
    private static readonly IReadOnlyDictionary<string, CollectibleSet> NoCollectibles = new Dictionary<string, CollectibleSet>();

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
    private bool repeatFlagsWarned;
    private bool outOfRangeLogged;
    private ulong festivalProbeContentId;
    private IReadOnlyList<ushort> festivalProbeIds = [];
    private IReadOnlyList<ushort> festivalProbePhases = [];

    // The last collectible read: whose it was, the unlock generation and target list it was read under, and how many
    // captures ago. Reused (the same instance, so the diff compares it by reference) until one of them moves.
    private IReadOnlyDictionary<string, CollectibleSet> collectibles = NoCollectibles;
    private ulong collectiblesContentId;
    private int collectiblesGeneration;
    private IReadOnlyList<CollectibleTarget>? collectiblesTargets;
    private int collectiblesAge;
    private bool collectiblesMeasured;

    // CollectibleTargets with the catalog's mounts added (TargetsFor), and the list and catalog it was built from.
    private IReadOnlyList<CollectibleTarget> mountTargets = [];
    private IReadOnlyList<CollectibleTarget>? mountTargetsBase;
    private QuestCatalog? mountTargetsCatalog;

    /// <summary>Where a gear gate's weapons can be: worn, the Armoury Chest's weapon pages, the four bags (<see cref="ReadGateItems"/>).</summary>
    private static readonly InventoryType[] GateItemContainers =
    [
        InventoryType.EquippedItems, InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand,
        InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4,
    ];

    // The last gear-gate read, reused while unchanged, and the scratch lists a read fills.
    private GateItemCapture? gateItems;
    private readonly List<uint> gateEquippedScratch = new(2);
    private readonly List<uint> gateHeldScratch = new(8);
    private bool gateItemsWarned;

    // The last unlock-link read of the game gates, reused while unchanged.
    private CollectibleSet? gateUnlockLinks;
    private bool gateUnlockLinksWarned;
    // The equipped gear's item levels by slot (ReadItemLevels), the best level per job, and whether a failed read was logged.
    private readonly ushort[] slotLevelScratch = new ushort[EquippedItemLevel.SoulCrystal + 1];
    private IReadOnlyDictionary<byte, ushort> jobItemLevels = new Dictionary<byte, ushort>();
    private bool itemLevelsWarned;

    // The duties the Duties board reads (DutyBoard.Watched over the index), the index they came from, and the last
    // duty-record read: whose, under which list, how many captures ago. Reused until one of them moves.
    private DutyRunIndex? dutyWatchIndex;
    private uint[] dutyWatch = [];
    private uint dutyWatchFingerprint;
    private DutyRecordCapture? dutyRecords;
    private ulong dutyRecordsContentId;
    private int dutyRecordsAge;
    private bool dutyRecordsWarned;

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

    /// <summary>
    /// The collectible rewards whose owned state each capture saves (<see cref="CharacterSnapshot.Collectibles"/>,
    /// <see cref="Collectibles.Targets"/>), and the flags they are read through. Empty or null: none is saved.
    /// </summary>
    public IReadOnlyList<CollectibleTarget> CollectibleTargets { get; set; } = [];

    /// <inheritdoc cref="CollectibleTargets"/>
    public CollectibleReader? CollectibleFlags { get; set; }

    /// <summary>
    /// The addon kill switch (T20). The repeat flags are read by calling a game function
    /// (<c>QuestManager.IsQuestRepeatFlagSet</c>), so, like the daily offer, that call waits for the gate: while it
    /// holds the hooks, or before it is set, no flag is captured and repeat-flag quests read as before 1.5 (not done
    /// this cycle by their flag) rather than from a call into an untested game version.
    /// </summary>
    public HookGate? Gate { get; set; }

    /// <summary>
    /// The duty index (built once from the sheets) whose duties the Duties board reads the character's records of
    /// (feature plan v7 N4, <see cref="DutyBoard.Watched"/>); null, or an index not built yet, reads none.
    /// </summary>
    public Func<DutyRunIndex?>? DutyIndex { get; set; }

    /// <summary>
    /// A capture reads the collectible flags again at least this often (in captures) even when nothing says they
    /// changed; between reads it reuses the last answer, so the usual poll does no unlock reads at all.
    /// </summary>
    public const int CollectibleRefreshCaptures = 60;

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

        // The journal's slots in use (feature plan v7, C9): every quest in NormalQuests takes one, the dailies' own
        // array does not.
        var journalSlots = (byte)Math.Min(accepted.Count, byte.MaxValue);

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

        var repeatFlags = ReadRepeatFlags(qm);

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
                // The rank byte's high bit means "ranked up today" (BeastReputationWork.Rank); FromClient masks it off.
                tribes[tribe] = TribeStanding.FromClient(rep->Rank, rep->Value);
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

        var itemLevel = ReadItemLevels(ps->CurrentClassJobId);
        var snapshot = new CharacterSnapshot
        {
            ContentId = contentId,
            Name = playerState.CharacterName,
            World = playerState.HomeWorld.RowId,
            TakenUtc = DateTime.UtcNow,
            CompletedBits = completedBits,
            Accepted = accepted,
            JournalSlotsUsed = journalSlots,
            DailyDone = dailyDone,
            RepeatFlags = repeatFlags,
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
            // A quest turned in is when most collectibles arrive, so a changed completion mask reads them again too.
            Collectibles = ReadCollectibles(contentId, catalog, completedChanged: !ReferenceEquals(completedBits, previousCompleted)),
            GateItems = ReadGateItems(ids),
            GateUnlockLinks = ReadGateUnlockLinks(ui, ids),
            ItemLevel = itemLevel,
            JobItemLevels = jobItemLevels,
            DutyRecords = ReadDutyRecords(contentId, completedChanged: !ReferenceEquals(completedBits, previousCompleted)),
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

    /// <summary>
    /// The owned collectibles for the capture (decision 9): read through <see cref="CollectibleFlags"/> for each of
    /// <see cref="CollectibleTargets"/> (about 900 flag checks, each a sheet row lookup and a bit test), but only when
    /// something may have changed them: another character, an unlock the game reported, a quest just completed, a new
    /// target list, or <see cref="CollectibleRefreshCaptures"/> captures since the last read. Otherwise the last read is
    /// reused as is. The first read is timed once in the log.
    /// </summary>
    private IReadOnlyDictionary<string, CollectibleSet> ReadCollectibles(ulong contentId, QuestCatalog catalog, bool completedChanged)
    {
        var flags = CollectibleFlags;
        var targets = TargetsFor(catalog);
        if (flags is null || targets.Count == 0)
        {
            return collectibles = NoCollectibles;
        }

        var generation = flags.Generation;
        collectiblesAge++;
        if (contentId == collectiblesContentId
            && generation == collectiblesGeneration
            && ReferenceEquals(targets, collectiblesTargets)
            && !completedChanged
            && collectiblesAge < CollectibleRefreshCaptures)
        {
            return collectibles;
        }

        var started = collectiblesMeasured ? 0L : Stopwatch.GetTimestamp();
        var read = Collectibles.Read(targets, t => flags.IsUnlocked(t.Kind, t.RewardId, t.ItemId));
        if (!collectiblesMeasured)
        {
            collectiblesMeasured = true;
            log.Debug("Collectibles: {Targets} reward flags read in {Elapsed:F2} ms", targets.Count, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        collectiblesAge = 0;
        collectiblesGeneration = generation;
        collectiblesTargets = targets;
        // An unchanged read keeps the previous instance, so the diff's reference check answers the next poll at once.
        if (contentId != collectiblesContentId || !Collectibles.Same(collectibles, read))
        {
            collectibles = read;
        }

        collectiblesContentId = contentId;
        return collectibles;
    }

    /// <summary>
    /// <see cref="CollectibleTargets"/> with the mounts the catalog's quests need owned (<see cref="QuestCatalog.MountWatch"/>,
    /// 1.11.0), built once per target list and catalog, so the reuse check in <see cref="ReadCollectibles"/> still
    /// compares one instance.
    /// </summary>
    private IReadOnlyList<CollectibleTarget> TargetsFor(QuestCatalog catalog)
    {
        var targets = CollectibleTargets;
        if (targets.Count == 0)
        {
            return targets;
        }

        if (!ReferenceEquals(targets, mountTargetsBase) || !ReferenceEquals(catalog, mountTargetsCatalog))
        {
            mountTargetsBase = targets;
            mountTargetsCatalog = catalog;
            mountTargets = Collectibles.WithMounts(targets, catalog.MountWatch);
        }

        return mountTargets;
    }

    /// <summary>
    /// The unlock links the catalog's gates check (<see cref="CharacterSnapshot.GateUnlockLinks"/>,
    /// <see cref="QuestCatalog.GateUnlockLinkWatch"/>): each read from the client's unlock-link flags
    /// (<c>UIState.IsUnlockLinkUnlocked</c>, a bit test), set or not. Null when the catalog watches none or the read
    /// failed (logged once), which leaves those gates not checked. An unchanged answer returns the previous instance, so
    /// the diff sees it unchanged by reference.
    /// </summary>
    private unsafe CollectibleSet? ReadGateUnlockLinks(UIState* ui, CatalogIds ids)
    {
        if (ids.UnlockLinkWatch.Length == 0)
        {
            return gateUnlockLinks = null;
        }

        try
        {
            var owned = new List<uint>();
            var missing = new List<uint>();
            foreach (var link in ids.UnlockLinkWatch)
            {
                (ui->IsUnlockLinkUnlocked(link) ? owned : missing).Add(link);
            }

            if (gateUnlockLinks is { } previous && previous.Owned.SequenceEqual(owned) && previous.Missing.SequenceEqual(missing))
            {
                return previous;
            }

            return gateUnlockLinks = new CollectibleSet { Owned = owned, Missing = missing };
        }
        catch (Exception ex)
        {
            if (!gateUnlockLinksWarned)
            {
                gateUnlockLinksWarned = true;
                log.Warning(ex, "The unlock links of the game gates could not be read; those gates read as not checked");
            }

            return gateUnlockLinks = null;
        }
    }

    /// <summary>
    /// The gear-gate weapons on the character (<see cref="CharacterSnapshot.GateItems"/>): of the weapons the catalog's
    /// gates list (<see cref="QuestCatalog.GateItemWatch"/>), those in the main hand and off hand, and those equipped,
    /// in the Armoury Chest's main-hand and off-hand pages or in the four inventory bags. A plain read of the
    /// containers' slots (about 250, each a set lookup); the containers are fetched through the game, so the read
    /// follows the <see cref="Gate"/>. Null, so every gear gate reads "not checked", when the catalog lists no weapon,
    /// the gate holds the hooks, or a container is not loaded yet. An unchanged answer returns the previous instance, so
    /// the diff sees it unchanged by reference.
    /// </summary>
    private unsafe GateItemCapture? ReadGateItems(CatalogIds ids)
    {
        if (ids.GateWatch.Count == 0 || Gate is not { HooksAllowed: true })
        {
            return gateItems = null;
        }

        try
        {
            var inventory = InventoryManager.Instance();
            if (inventory == null)
            {
                return gateItems = null;
            }

            gateEquippedScratch.Clear();
            gateHeldScratch.Clear();
            foreach (var type in GateItemContainers)
            {
                var container = inventory->GetInventoryContainer(type);
                if (container == null || !container->IsLoaded)
                {
                    return gateItems = null;
                }

                // The equipped container's first two slots are the main hand and the off hand.
                var size = type == InventoryType.EquippedItems ? Math.Min(container->Size, 2) : container->Size;
                for (var i = 0; i < size; i++)
                {
                    var slot = container->GetInventorySlot(i);
                    if (slot == null || slot->ItemId == 0 || !ids.GateWatch.Contains(slot->ItemId))
                    {
                        continue;
                    }

                    gateHeldScratch.Add(slot->ItemId);
                    if (type == InventoryType.EquippedItems)
                    {
                        gateEquippedScratch.Add(slot->ItemId);
                    }
                }
            }

            gateEquippedScratch.Sort();
            gateHeldScratch.Sort();
            var held = gateHeldScratch.Distinct().ToArray();
            if (gateItems is { } previous && previous.Watch == ids.GateFingerprint
                && previous.Equipped.SequenceEqual(gateEquippedScratch) && previous.Held.SequenceEqual(held))
            {
                return previous;
            }

            return gateItems = new GateItemCapture(ids.GateFingerprint, [.. gateEquippedScratch], held);
        }
        catch (Exception ex)
        {
            if (!gateItemsWarned)
            {
                gateItemsWarned = true;
                log.Warning(ex, "The relic weapons could not be read from the inventory; relic weapon gates read as not checked");
            }

            return gateItems = null;
        }
    }

    /// <summary>
    /// The equipped gear's average item level (<see cref="EquippedItemLevel.Average"/>, from each slot's
    /// <c>Item.LevelItem</c>) and, in <see cref="jobItemLevels"/>, the best item level known per job: every saved
    /// gearset's (<c>RaptureGearsetModule.GearsetEntry.ItemLevel</c>, as the game keeps it) and the equipped gear's for
    /// <paramref name="currentJob"/> (feature plan v7 C7). Both are fetched through the game, so the read follows the
    /// <see cref="Gate"/>: while it holds the hooks, or while the equipped container is not loaded, the item level
    /// reads 0 (not judged) and the job levels stay as last read. An unchanged job table keeps its instance, so the
    /// diff sees it unchanged by reference.
    /// </summary>
    private unsafe ushort ReadItemLevels(byte currentJob)
    {
        if (Gate is not { HooksAllowed: true })
        {
            return 0;
        }

        try
        {
            var inventory = InventoryManager.Instance();
            var container = inventory == null ? null : inventory->GetInventoryContainer(InventoryType.EquippedItems);
            if (container == null || !container->IsLoaded)
            {
                return 0;
            }

            var items = data.GetExcelSheet<Item>();
            Array.Clear(slotLevelScratch);
            var slots = Math.Min((int)container->Size, slotLevelScratch.Length);
            for (var i = 0; i < slots; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot != null && slot->ItemId != 0 && items.GetRowOrDefault(slot->ItemId) is { } item)
                {
                    slotLevelScratch[i] = (ushort)Math.Min(item.LevelItem.RowId, ushort.MaxValue);
                }
            }

            var equipped = EquippedItemLevel.Average(slotLevelScratch);
            var levels = new Dictionary<byte, ushort>();
            var gearsets = RaptureGearsetModule.Instance();
            if (gearsets != null)
            {
                foreach (ref var entry in gearsets->Entries)
                {
                    if (!entry.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists) || entry.ClassJob == 0 || entry.ItemLevel <= 0)
                    {
                        continue;
                    }

                    var level = (ushort)entry.ItemLevel;
                    if (level > levels.GetValueOrDefault(entry.ClassJob))
                    {
                        levels[entry.ClassJob] = level;
                    }
                }
            }

            if (currentJob != 0 && equipped > levels.GetValueOrDefault(currentJob))
            {
                levels[currentJob] = equipped;
            }

            if (!SameLevels(jobItemLevels, levels))
            {
                jobItemLevels = levels;
            }

            return equipped;
        }
        catch (Exception ex)
        {
            if (!itemLevelsWarned)
            {
                itemLevelsWarned = true;
                log.Warning(ex, "The item levels could not be read; item-level walls read as not checked");
            }

            return 0;
        }
    }

    private static bool SameLevels(IReadOnlyDictionary<byte, ushort> a, Dictionary<byte, ushort> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (job, level) in b)
        {
            if (!a.TryGetValue(job, out var other) || other != level)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The Duties board's records (feature plan v7 N4): of the duties <see cref="DutyBoard.Watched"/> lists, those the
    /// character has unlocked (<c>UIState.IsInstanceContentUnlocked</c>) and cleared
    /// (<c>UIState.IsInstanceContentCompleted</c>). Up to two game calls per duty, so, like the collectibles, they are
    /// read only when something may have changed them: another character, a quest just completed (most duties open on
    /// a quest), another list, or <see cref="CollectibleRefreshCaptures"/> captures since the last read; otherwise the
    /// last read is reused as is. Null, so the board says "log in to read", while the <see cref="Gate"/> holds the
    /// hooks or the duty index has not landed.
    /// </summary>
    private DutyRecordCapture? ReadDutyRecords(ulong contentId, bool completedChanged)
    {
        if (Gate is not { HooksAllowed: true } || DutyIndex?.Invoke() is not { Count: > 0 } index)
        {
            return dutyRecords = null;
        }

        if (!ReferenceEquals(index, dutyWatchIndex))
        {
            dutyWatchIndex = index;
            dutyWatch = DutyBoard.Watched(index);
            dutyWatchFingerprint = GateItemCapture.Fingerprint(dutyWatch);
        }

        dutyRecordsAge++;
        if (dutyRecords is { } last
            && contentId == dutyRecordsContentId
            && last.Watch == dutyWatchFingerprint
            && !completedChanged
            && dutyRecordsAge < CollectibleRefreshCaptures)
        {
            return last;
        }

        try
        {
            var unlocked = new List<uint>();
            var cleared = new List<uint>();
            foreach (var id in dutyWatch)
            {
                if (UIState.IsInstanceContentCompleted(id))
                {
                    cleared.Add(id);
                    unlocked.Add(id);
                }
                else if (UIState.IsInstanceContentUnlocked(id))
                {
                    unlocked.Add(id);
                }
            }

            dutyRecordsAge = 0;
            var read = new DutyRecordCapture(dutyWatchFingerprint, unlocked, cleared);
            if (contentId != dutyRecordsContentId || !DutyRecordCapture.Same(dutyRecords, read))
            {
                dutyRecords = read;
            }

            dutyRecordsContentId = contentId;
            return dutyRecords;
        }
        catch (Exception ex)
        {
            if (!dutyRecordsWarned)
            {
                dutyRecordsWarned = true;
                log.Warning(ex, "The duty records could not be read; the Duties board waits for the next read");
            }

            return dutyRecords = null;
        }
    }

    /// <summary>What changed between two captures; see <see cref="SnapshotDiff.Compute"/>.</summary>
    public static SnapshotDiff Diff(CharacterSnapshot old, CharacterSnapshot @new) => SnapshotDiff.Compute(old, @new);

    /// <summary>
    /// The QuestRepeatFlag rows the client has set, ascending, through the game's own
    /// <c>QuestManager.IsQuestRepeatFlagSet</c> (rows 1 to <see cref="RepeatFlagRows"/> - 1; 0 is "no flag"). A
    /// repeatable carrying a set flag (<see cref="QuestRecord.RepeatFlag"/>) was turned in this cycle; the game clears
    /// it at that quest's daily or weekly reset. Empty, logged once, when the function did not resolve; empty while
    /// the <see cref="Gate"/> holds the game hooks.
    /// </summary>
    private unsafe List<byte> ReadRepeatFlags(QuestManager* qm)
    {
        var flags = new List<byte>();
        if (Gate is not { HooksAllowed: true })
        {
            return flags;
        }

        if (QuestManager.Addresses.IsQuestRepeatFlagSet.Value == 0)
        {
            if (!repeatFlagsWarned)
            {
                repeatFlagsWarned = true;
                log.Warning("QuestManager.IsQuestRepeatFlagSet did not resolve; repeat-flag quests will not read done this cycle");
            }

            return flags;
        }

        for (byte flag = 1; flag < RepeatFlagRows; flag++)
        {
            if (qm->IsQuestRepeatFlagSet(flag))
            {
                flags.Add(flag);
            }
        }

        return flags;
    }

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

        var built = new CatalogIds(catalog, [.. quests], [.. instances], new HashSet<uint>(catalog.GateItemWatch), catalog.GateItemFingerprint, catalog.GateUnlockLinkWatch);
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

    private sealed record CatalogIds(QuestCatalog Catalog, ushort[] QuestIds, uint[] InstanceIds, HashSet<uint> GateWatch, uint GateFingerprint, uint[] UnlockLinkWatch);
}
