using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>
/// Answers "does the viewed character have this unique reward?" for the Moonlit pane and "is the live character
/// attuned to this aether current?" for the Flight pane.
/// <para>
/// Kinds the client keeps an unlock flag for (emote, minion, mount, orchestrion roll, ornament, Triple Triad card,
/// aether current, duty) are read from ClientStructs, which is only possible for the live character and only on the
/// framework thread (Dalamud draws on that thread, so calling from <c>Draw</c> is fine). Kinds that simply follow the
/// quest (action, trait, job, blue mage spell, system unlock) are answered from the viewed snapshot's completion bit,
/// so they work for stored characters too. Items, gear and the rest return null (unknown).
/// </para>
/// <para>
/// Titles and achievements read the game's own state for the live character once it is loaded: the title list
/// (<c>TitleList.DataReceived</c>) and the completed-achievement bitmap (<c>Achievement.IsLoaded</c>), which the client
/// only fills after the Titles or Achievements window has been opened this session. Until then, and for stored
/// characters, they are derived from the quests the achievement names (<see cref="AchievementQuests.EarnedFromQuests"/>):
/// all of them done for a "complete every quest" achievement, any one for "complete any one"; only when the sheet
/// cannot tell does the entry's own quest bit decide. <see cref="AchievementStateVersion"/> moves when the live state
/// loads and whenever the number of completed achievements changes (one earned while the pane is open), so the
/// Moonlit pane reads them again.
/// </para>
/// <para>
/// Results are memoized per (kind, reward id, quest) and dropped whenever <see cref="SessionState.Version"/> changes.
/// Attuning a current changes nothing in the snapshot, so the Flight pane also calls
/// <see cref="InvalidateAetherCurrents"/> when it is shown, on a zone change and every few seconds while live. A
/// ClientStructs failure is logged once and reads as unknown.
/// </para>
/// </summary>
public sealed class RewardUnlockReader
{
    /// <summary><c>ContentFinderCondition.ContentLinkType</c> value whose <c>Content</c> is an InstanceContent row.</summary>
    private const byte InstanceContentLink = 1;

    private readonly SessionState session;
    private readonly IDataManager data;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly Dictionary<(RewardKind Kind, uint RewardId, uint QuestRowId), bool?> memo = [];

    private Dictionary<uint, uint>? instanceByCondition;
    private Dictionary<uint, (byte Type, IReadOnlyList<uint> Quests)>? achievementQuests;
    private Dictionary<uint, List<uint>>? achievementsByTitle;
    private int memoVersion = -1;
    private bool warned;
    private (bool Achievements, bool Titles, int Completed) liveAchievementState;
    private int achievementStateVersion;

    public RewardUnlockReader(SessionState session, IDataManager data, IFramework framework, IPluginLog log)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>True, false, or null when the plugin cannot tell (no live character, unsupported kind, read failure).</summary>
    public bool? IsObtained(UniqueRewardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (memoVersion != session.Version)
        {
            memo.Clear();
            memoVersion = session.Version;
        }

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
        if (memoVersion != session.Version)
        {
            memo.Clear();
            memoVersion = session.Version;
        }

        var key = (RewardKind.AetherCurrent, aetherCurrentId, 0u);
        if (memo.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = ReadLive(RewardKind.AetherCurrent, aetherCurrentId);
        memo[key] = result;
        return result;
    }

    /// <summary>
    /// Drops the memoized aether current attunements (the Flight pane's and Moonlit's alike) so the next read asks
    /// <c>PlayerState</c> again. Attuning a field current bumps no session version, so the Flight pane calls this when
    /// it becomes visible, when the territory changes and every few seconds while a live character is viewed.
    /// </summary>
    public void InvalidateAetherCurrents() => DropMemo(RewardKind.AetherCurrent, RewardKind.AetherCurrent);

    /// <summary>
    /// Moves whenever the live character's title list or achievement list finishes loading (or goes away), or the
    /// number of completed achievements changes, and drops the memoized title and achievement answers so the next read
    /// uses the game's state. Cheap: two flags and a population count over the completed-achievement bitmap (a few
    /// hundred bytes), read on the framework thread only; the Moonlit pane checks it every frame.
    /// </summary>
    public int AchievementStateVersion
    {
        get
        {
            var state = ReadAchievementState();
            if (state != liveAchievementState)
            {
                liveAchievementState = state;
                achievementStateVersion++;
                DropMemo(RewardKind.Title, RewardKind.Achievement);
            }

            return achievementStateVersion;
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

            case RewardKind.Emote:
            case RewardKind.Minion:
            case RewardKind.Mount:
            case RewardKind.Orchestrion:
            case RewardKind.Ornament:
            case RewardKind.TripleTriadCard:
            case RewardKind.AetherCurrent:
            case RewardKind.Instance:
            case RewardKind.DutyUnlock:
                return ReadLive(entry.Kind, entry.RewardId);

            default:
                // Item, OptionalItem, ArtifactGear, Other, Barding, Hairstyle: no flag the plugin can read.
                return null;
        }
    }

    /// <summary>
    /// Whether titles (<paramref name="kind"/> Title) or achievements are read from the game's own state rather than
    /// worked out from quests, as of the last <see cref="AchievementStateVersion"/> check. Titles also read exactly from
    /// the achievement list. False for a stored character.
    /// </summary>
    public bool ReadsExactly(RewardKind kind) => kind == RewardKind.Title
        ? liveAchievementState.Titles || liveAchievementState.Achievements
        : liveAchievementState.Achievements;

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

    private unsafe bool? ReadLive(RewardKind kind, uint id)
    {
        if (!CanReadLive)
        {
            return null;
        }

        try
        {
            var ui = UIState.Instance();
            var ps = PlayerState.Instance();
            if (ui == null || ps == null)
            {
                return null;
            }

            switch (kind)
            {
                case RewardKind.Emote:
                    return id <= ushort.MaxValue ? ui->IsEmoteUnlocked((ushort)id) : null;
                case RewardKind.Minion:
                    return ui->IsCompanionUnlocked(id);
                case RewardKind.TripleTriadCard:
                    return id <= ushort.MaxValue ? ui->IsTripleTriadCardUnlocked((ushort)id) : null;
                case RewardKind.Mount:
                    return ps->IsMountUnlocked(id);
                case RewardKind.Orchestrion:
                    return ps->IsOrchestrionRollUnlocked(id);
                case RewardKind.Ornament:
                    return ps->IsOrnamentUnlocked(id);
                case RewardKind.AetherCurrent:
                    return ps->IsAetherCurrentUnlocked(id);
                case RewardKind.Instance:
                    return UIState.IsInstanceContentUnlocked(id);
                case RewardKind.DutyUnlock:
                    return InstanceForCondition(id) is { } instance ? UIState.IsInstanceContentUnlocked(instance) : null;
                default:
                    return null;
            }
        }
        catch (Exception ex)
        {
            WarnOnce(ex);
            return null;
        }
    }

    /// <summary>ContentFinderCondition row id to InstanceContent row id, read once from the sheet. Null for other content types.</summary>
    private uint? InstanceForCondition(uint conditionId)
    {
        if (instanceByCondition is null)
        {
            var map = new Dictionary<uint, uint>();
            try
            {
                foreach (var row in data.GetExcelSheet<ContentFinderCondition>())
                {
                    if (row.ContentLinkType == InstanceContentLink && row.Content.RowId != 0)
                    {
                        map[row.RowId] = row.Content.RowId;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "ContentFinderCondition sheet could not be read; duty unlock states show as unknown");
            }

            instanceByCondition = map;
        }

        return instanceByCondition.TryGetValue(conditionId, out var instance) ? instance : null;
    }
}
