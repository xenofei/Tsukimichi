using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Game;

/// <summary>
/// Answers "does the viewed character have this unique reward?" for the Moonlit pane and "is the live character
/// attuned to this aether current?" for the Flight pane.
/// <para>
/// Kinds the client keeps an unlock flag for (emote, minion, mount, orchestrion roll, ornament, Triple Triad card,
/// aether current, duty) are read from ClientStructs, which is only possible for the live character and only on the
/// framework thread (Dalamud draws on that thread, so calling from <c>Draw</c> is fine). Kinds that simply follow the
/// quest (action, trait, job, blue mage spell, system unlock, title, achievement) are answered from the viewed
/// snapshot's completion bit, so they work for stored characters too. Items, gear and the rest return null (unknown).
/// </para>
/// <para>
/// Results are memoized per (kind, reward id, quest) and dropped whenever <see cref="SessionState.Version"/> changes.
/// A ClientStructs failure is logged once and reads as unknown.
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
    private int memoVersion = -1;
    private bool warned;

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
            case RewardKind.Title:
            case RewardKind.Achievement:
                // The reward follows the quest; the snapshot's completion bit is QuestManager.IsQuestComplete as captured.
                return session.ViewedSnapshot?.IsCompleted(QuestRecord.ToQuestId(entry.QuestRowId));

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
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "Reward unlock flags could not be read; Moonlit obtained states show as unknown");
            }

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
