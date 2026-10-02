using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>
/// Answers "does the viewed character have this unique reward?" for the Moonlit pane and "is the live character
/// attuned to this aether current?" for the Flight pane.
/// <para>
/// Kinds the client keeps an unlock flag for (<see cref="Collectibles.StoredKinds"/>: emote, minion, mount, orchestrion
/// roll, ornament, Triple Triad card, barding, hairstyle, aether current, duty) are read through Dalamud's
/// <c>IUnlockState</c> (<see cref="CollectibleReader"/>) for the live character on the framework thread (Dalamud draws
/// on that thread, so calling from <c>Draw</c> is fine). For a stored character, or one live in another game client,
/// they come from what its snapshot saved at its last capture (<see cref="CharacterSnapshot.Collectibles"/>), and
/// read unknown only when that snapshot predates 1.5 (<see cref="Collectibles.Obtained"/>). Kinds that simply follow the
/// quest (action, trait, job, blue mage spell, system unlock) are answered from the viewed snapshot's completion bit.
/// Relic and special weapons (<see cref="RewardKind.ArtifactGear"/>) read owned for the live character when Allagan
/// Tools is loaded and counts the item anywhere it looks (bags, armoury, saddlebag, armoire, glamour dresser,
/// retainers; 1.6.0), and unknown otherwise: a count of none cannot tell a weapon never had from one sold or
/// desynthesised. Other items, gear and the rest return null (unknown).
/// </para>
/// <para>
/// Titles and achievements read the game's own state for the live character once it is loaded: the title list
/// (<c>TitleList.DataReceived</c>) and the completed-achievement bitmap (<c>Achievement.IsLoaded</c>), which the client
/// only fills after the Titles or Achievements window has been opened this session. Until then, and for stored
/// characters, they are derived from the quests the achievement names (<see cref="AchievementQuests.EarnedFromQuests"/>):
/// all of them done for a "complete every quest" achievement, any one for "complete any one"; only when the sheet
/// cannot tell does the entry's own quest bit decide. <see cref="LiveStateVersion"/> moves when the live state
/// loads and whenever the number of completed achievements changes (one earned while the pane is open), so the
/// Moonlit pane reads them again.
/// </para>
/// <para>
/// Results are memoized per (kind, reward id, quest) and dropped whenever <see cref="SessionState.Version"/> changes,
/// and when the game reports a new unlock (<see cref="CollectibleReader.Generation"/>, which also moves
/// <see cref="LiveStateVersion"/>), so a mount just learned reads owned at once rather than at the next save.
/// Attuning a current changes nothing in the snapshot, so the Flight pane also calls
/// <see cref="InvalidateAetherCurrents"/> when it is shown, on a zone change and every few seconds while live. A
/// read failure is logged once and reads as unknown.
/// </para>
/// </summary>
public sealed class RewardUnlockReader
{
    private readonly SessionState session;
    private readonly IDataManager data;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly CollectibleReader flags;
    private readonly Dictionary<(RewardKind Kind, uint RewardId, uint QuestRowId), bool?> memo = [];

    private Dictionary<uint, (byte Type, IReadOnlyList<uint> Quests)>? achievementQuests;
    private Dictionary<uint, List<uint>>? achievementsByTitle;
    private int memoVersion = -1;
    private int memoGeneration = -1;
    private int allaganGeneration = -1;
    private long allaganCheckedAt = long.MinValue;
    private bool warned;
    private (bool Achievements, bool Titles, int Completed) liveAchievementState;
    private int liveGeneration = -1;
    private int liveStateVersion;

    // The viewed snapshot's saved collectibles, indexed once per snapshot instance.
    private CharacterSnapshot? storedFor;
    private CollectibleLookup? stored;

    public RewardUnlockReader(SessionState session, IDataManager data, IFramework framework, IPluginLog log, CollectibleReader flags)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.flags = flags ?? throw new ArgumentNullException(nameof(flags));
    }

    /// <summary>Allagan Tools' item counts, for relic and special weapons; null leaves them unknown.</summary>
    public AllaganToolsIpc? Allagan { get; set; }

    /// <summary>Reads Settings › Integrations › "Count with Allagan Tools"; null reads as on.</summary>
    public Func<bool>? AllaganEnabled { get; set; }

    /// <summary>
    /// How often, at most, a change Allagan Tools reports (every item picked up or sold moves it) drops the memoized
    /// gear answers: re-asking for every relic row costs more than one frame should.
    /// </summary>
    public const long AllaganRefreshMs = 5000;

    /// <summary>
    /// Whether a true answer for <paramref name="entry"/> came from Allagan Tools rather than a game flag, so the pane
    /// can say "owned (per Allagan Tools)".
    /// </summary>
    public static bool OwnedPerAllagan(UniqueRewardEntry entry) => entry is { Kind: RewardKind.ArtifactGear, ItemId: not 0 };

    /// <summary>True, false, or null when the plugin cannot tell (nothing live or saved, unsupported kind, read failure).</summary>
    public bool? IsObtained(UniqueRewardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        DropStaleMemo();
        var key = (entry.Kind, entry.RewardId, entry.QuestRowId);
        if (memo.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = Read(entry);
        memo[key] = result;
        return result;
    }

    /// <summary>
    /// Whether the live character is attuned to an aether current (AetherCurrent row id), for the Flight pane. Read
    /// from <c>PlayerState</c> on the framework thread only, so it is null for stored characters, off-thread, or when
    /// the read fails; memoized per <see cref="SessionState.Version"/> like <see cref="IsObtained"/>.
    /// </summary>
    public bool? IsAetherCurrentUnlocked(uint aetherCurrentId)
    {
        DropStaleMemo();
        var key = (RewardKind.AetherCurrent, aetherCurrentId, 0u);
        if (memo.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = CanReadLive ? flags.IsUnlocked(RewardKind.AetherCurrent, aetherCurrentId) : null;
        memo[key] = result;
        return result;
    }

    /// <summary>
    /// When the viewed character's collectible answers were captured, for a character not logged in here (stored, or
    /// live in another game client): "as of" in the Moonlit pane and on the Characters tab. Null while the character is
    /// live here (the answers are current) or its snapshot saved none (written before 1.5).
    /// </summary>
    public DateTime? StoredAsOfUtc => session.IsLive ? null : StoredLookup()?.AsOfUtc;

    /// <summary>The memo goes with a new session version (another capture or character) and with a reported unlock.</summary>
    private void DropStaleMemo()
    {
        var generation = flags.Generation;
        if (memoVersion != session.Version || memoGeneration != generation)
        {
            memo.Clear();
            memoVersion = session.Version;
            memoGeneration = generation;
        }
    }

    /// <summary>The viewed snapshot's saved collectibles; rebuilt only when the session holds another snapshot instance.</summary>
    private CollectibleLookup? StoredLookup()
    {
        var snapshot = session.ViewedSnapshot;
        if (!ReferenceEquals(snapshot, storedFor))
        {
            storedFor = snapshot;
            stored = CollectibleLookup.For(snapshot);
        }

        return stored;
    }

    /// <summary>
    /// Drops the memoized aether current attunements (the Flight pane's and Moonlit's alike) so the next read asks
    /// <c>PlayerState</c> again. Attuning a field current bumps no session version, so the Flight pane calls this when
    /// it becomes visible, when the territory changes and every few seconds while a live character is viewed.
    /// </summary>
    public void InvalidateAetherCurrents() => DropMemo(RewardKind.AetherCurrent, RewardKind.AetherCurrent);

    /// <summary>
    /// Moves whenever the live character's title list or achievement list finishes loading (or goes away), the
    /// number of completed achievements changes, or the game reports a new unlock (<see cref="CollectibleReader.Generation"/>),
    /// and drops the memoized answers concerned so the next read uses the game's state. Cheap: a counter, two flags and
    /// a population count over the completed-achievement bitmap (a few hundred bytes), read on the framework thread
    /// only; the Moonlit pane checks it every frame.
    /// </summary>
    public int LiveStateVersion
    {
        get
        {
            var state = ReadAchievementState();
            if (state != liveAchievementState)
            {
                liveAchievementState = state;
                liveStateVersion++;
                DropMemo(RewardKind.Title, RewardKind.Achievement);
            }

            var generation = flags.Generation;
            if (generation != liveGeneration)
            {
                // The memo itself goes on the next read (DropStaleMemo); this only tells the pane to read again.
                liveGeneration = generation;
                liveStateVersion++;
            }

            // Allagan Tools came, went or saw an inventory change: the gear answers are asked again, at most every
            // AllaganRefreshMs, so a burst of pick-ups does not re-read every relic row each frame.
            var allagan = Allagan?.Generation ?? 0;
            var now = Environment.TickCount64;
            if (allagan != allaganGeneration && now - allaganCheckedAt >= AllaganRefreshMs)
            {
                allaganGeneration = allagan;
                allaganCheckedAt = now;
                DropMemo(RewardKind.ArtifactGear, RewardKind.ArtifactGear);
                liveStateVersion++;
            }

            return liveStateVersion;
        }
    }

    /// <summary>Whether <see cref="IsObtained"/> can currently read client flags: a live character on the framework thread.</summary>
    public bool CanReadLive => session.IsLive && framework.IsInFrameworkUpdateThread;

    private bool? Read(UniqueRewardEntry entry)
    {
        switch (entry.Kind)
        {
            case RewardKind.Action:
            case RewardKind.GeneralAction:
            case RewardKind.Trait:
            case RewardKind.ClassJob:
            case RewardKind.BlueMageSpell:
            case RewardKind.SystemUnlock:
                // The reward follows the quest; the snapshot's completion bit is QuestManager.IsQuestComplete as captured.
                return QuestBit(entry.QuestRowId);

            case RewardKind.Title:
                return ReadTitle(entry);

            case RewardKind.Achievement:
                return ReadAchievement(entry);

            case var kind when Collectibles.IsStored(kind):
                // The live flag, else what the snapshot saved (a stored character, or one live in another client).
                return Collectibles.Obtained(CanReadLive, () => flags.IsUnlocked(kind, entry.RewardId, entry.ItemId), StoredLookup(), kind, entry.RewardId);

            case RewardKind.ArtifactGear:
                return AllaganOwned(entry);

            default:
                // Item, OptionalItem, Other: no flag the plugin can read.
                return null;
        }
    }

    /// <summary>
    /// Whether titles (<paramref name="kind"/> Title) or achievements are read from the game's own state rather than
    /// worked out from quests, as of the last <see cref="LiveStateVersion"/> check. Titles also read exactly from
    /// the achievement list. False for a stored character.
    /// </summary>
    public bool ReadsExactly(RewardKind kind) => kind == RewardKind.Title
        ? liveAchievementState.Titles || liveAchievementState.Achievements
        : liveAchievementState.Achievements;

    /// <summary>
    /// A relic or special weapon the live character holds anywhere Allagan Tools looks reads owned; nothing found, a
    /// stored character, or no Allagan Tools reads unknown (<see cref="Core.HandIn.HandInActions.OwnedFromCount"/>).
    /// </summary>
    private bool? AllaganOwned(UniqueRewardEntry entry)
    {
        if (entry.ItemId == 0 || !CanReadLive || Allagan is not { } allagan || AllaganEnabled?.Invoke() == false || !allagan.Available)
        {
            return null;
        }

        return Core.HandIn.HandInActions.OwnedFromCount(allagan.CountOwned(entry.ItemId));
    }

    private bool? QuestBit(uint questRowId) => session.ViewedSnapshot?.IsCompleted(QuestRecord.ToQuestId(questRowId));

    /// <summary>The game's completed-achievement bit for the live character, else the quests the achievement names, else the entry's quest.</summary>
    private bool? ReadAchievement(UniqueRewardEntry entry) =>
        LiveAchievement(entry.RewardId) ?? EarnedFromQuests(entry.RewardId) ?? QuestBit(entry.QuestRowId);

    /// <summary>
    /// The game's title list for the live character; else the achievements that award the title (live bitmap, then
    /// their quests), obtained when any of them is; else the entry's quest.
    /// </summary>
    private unsafe bool? ReadTitle(UniqueRewardEntry entry)
    {
        if (CanReadLive && entry.RewardId <= ushort.MaxValue)
        {
            try
            {
                var ui = UIState.Instance();
                if (ui != null && ui->TitleList.DataReceived)
                {
                    return ui->TitleList.IsTitleUnlocked((ushort)entry.RewardId);
                }
            }
            catch (Exception ex)
            {
                WarnOnce(ex);
            }
        }

        if (!AchievementsByTitle().TryGetValue(entry.RewardId, out var awarding))
        {
            return QuestBit(entry.QuestRowId);
        }

        var known = true;
        foreach (var id in awarding)
        {
            switch (LiveAchievement(id) ?? EarnedFromQuests(id))
            {
                case true:
                    return true;
                case null:
                    known = false;
                    break;
            }
        }

        return known ? false : QuestBit(entry.QuestRowId);
    }

    /// <summary>The live completed-achievement bit; null when not live, not loaded yet, or unreadable.</summary>
    private unsafe bool? LiveAchievement(uint achievementId)
    {
        if (!CanReadLive || achievementId > int.MaxValue)
        {
            return null;
        }

        try
        {
            var achievement = FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement.Instance();
            return achievement != null && achievement->IsLoaded() ? achievement->IsComplete((int)achievementId) : null;
        }
        catch (Exception ex)
        {
            WarnOnce(ex);
            return null;
        }
    }

    /// <summary>Whether the viewed snapshot's quests earn the achievement (<see cref="AchievementQuests.EarnedFromQuests"/>); null when they cannot tell.</summary>
    private bool? EarnedFromQuests(uint achievementId)
    {
        var snapshot = session.ViewedSnapshot;
        if (snapshot is null || !AchievementQuestMap().TryGetValue(achievementId, out var info))
        {
            return null;
        }

        return AchievementQuests.EarnedFromQuests(info.Type, info.Quests, q => snapshot.IsCompleted(QuestRecord.ToQuestId(q)));
    }

    private unsafe (bool Achievements, bool Titles, int Completed) ReadAchievementState()
    {
        if (!CanReadLive)
        {
            return default;
        }

        try
        {
            var ui = UIState.Instance();
            var achievement = FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement.Instance();
            var loaded = achievement != null && achievement->IsLoaded();
            var completed = loaded ? CountBits(achievement->CompletedAchievements) : 0;
            return (loaded, ui != null && ui->TitleList.DataReceived, completed);
        }
        catch (Exception ex)
        {
            WarnOnce(ex);
            return default;
        }
    }

    /// <summary>How many bits are set: eight bytes at a time, then the rest.</summary>
    private static int CountBits(ReadOnlySpan<byte> bits)
    {
        var count = 0;
        var words = MemoryMarshal.Cast<byte, ulong>(bits);
        foreach (var word in words)
        {
            count += BitOperations.PopCount(word);
        }

        for (var i = words.Length * sizeof(ulong); i < bits.Length; i++)
        {
            count += BitOperations.PopCount(bits[i]);
        }

        return count;
    }

    private void DropMemo(RewardKind first, RewardKind second)
    {
        List<(RewardKind Kind, uint RewardId, uint QuestRowId)>? stale = null;
        foreach (var key in memo.Keys)
        {
            if (key.Kind == first || key.Kind == second)
            {
                (stale ??= []).Add(key);
            }
        }

        if (stale is null)
        {
            return;
        }

        foreach (var key in stale)
        {
            memo.Remove(key);
        }
    }

    private void WarnOnce(Exception ex)
    {
        if (!warned)
        {
            warned = true;
            log.Warning(ex, "Reward unlock flags could not be read; Moonlit obtained states show as unknown");
        }
    }

    /// <summary>Achievement row id to its type and the quests it names, for the achievements that name any; read once from the sheet.</summary>
    private Dictionary<uint, (byte Type, IReadOnlyList<uint> Quests)> AchievementQuestMap()
    {
        if (achievementQuests is null)
        {
            BuildAchievementMaps();
        }

        return achievementQuests!;
    }

    /// <summary>Title row id to the achievements that award it; read once from the sheet.</summary>
    private Dictionary<uint, List<uint>> AchievementsByTitle()
    {
        if (achievementsByTitle is null)
        {
            BuildAchievementMaps();
        }

        return achievementsByTitle!;
    }

    private void BuildAchievementMaps()
    {
        var quests = new Dictionary<uint, (byte Type, IReadOnlyList<uint> Quests)>();
        var byTitle = new Dictionary<uint, List<uint>>();
        try
        {
            foreach (var row in data.GetExcelSheet<Lumina.Excel.Sheets.Achievement>())
            {
                if (row.Title.RowId != 0)
                {
                    if (!byTitle.TryGetValue(row.Title.RowId, out var list))
                    {
                        byTitle[row.Title.RowId] = list = [];
                    }

                    list.Add(row.RowId);
                }

                var named = AchievementQuests.QuestsOf(row);
                if (named.Count > 0)
                {
                    quests[row.RowId] = (row.Type, named);
                }
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Achievement sheet could not be read; titles and achievements follow their quest");
        }

        achievementQuests = quests;
        achievementsByTitle = byTitle;
    }
}
