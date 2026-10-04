using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// "Switch gearset" (feature plan v7, C8), game side: reads the logged-in character's gearsets
/// (<c>RaptureGearsetModule</c>) and equips one through the game's own gearset function (<c>EquipGearset</c>, what
/// <c>/gearset change</c> runs), only on the player's click of an explicit button and only for a quest that needs a
/// specific job (<see cref="GearsetChoice.Needed"/>). Never automatic.
/// <para>
/// Safety: nothing is read or called while the shared <see cref="HookGate"/> holds the game hooks; the switch is refused,
/// saying why, in combat, while casting, in a duty, between areas, in a cutscene or while busy with an NPC or a craft.
/// Framework thread only (ImGui draws on it).
/// </para>
/// </summary>
public sealed unsafe class GearsetSwitcher
{
    /// <summary>Milliseconds a read of the gearset list is reused.</summary>
    private const long CacheMs = 1000;

    private readonly ICondition condition;
    private readonly IClientState clientState;
    private readonly HookGate gate;
    private readonly IPluginLog log;
    private readonly List<GearsetInfo> gearsets = [];
    private long readAt = long.MinValue;
    private bool readWarned;

    public GearsetSwitcher(ICondition condition, IClientState clientState, HookGate gate, IPluginLog log)
    {
        this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>The character's gearsets (empty while the hooks are paused or nobody is logged in); read at most once a second.</summary>
    public IReadOnlyList<GearsetInfo> Gearsets()
    {
        var now = Environment.TickCount64;
        if (now - readAt < CacheMs)
        {
            return gearsets;
        }

        readAt = now;
        gearsets.Clear();
        if (!gate.HooksAllowed || !clientState.IsLoggedIn)
        {
            return gearsets;
        }

        try
        {
            var module = RaptureGearsetModule.Instance();
            if (module == null)
            {
                return gearsets;
            }

            var entries = module->Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                ref var entry = ref entries[i];
                if (!entry.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists) || entry.ClassJob == 0)
                {
                    continue;
                }

                gearsets.Add(new GearsetInfo(i, entry.ClassJob, entry.ItemLevel, entry.NameString));
            }
        }
        catch (Exception ex)
        {
            gearsets.Clear();
            if (!readWarned)
            {
                readWarned = true;
                log.Warning(ex, "Reading the gearsets failed; Switch gearset is not offered");
            }
        }

        return gearsets;
    }

    /// <summary>Why a switch cannot run now, as a tooltip clause; null when it can.</summary>
    public string? Blocker()
    {
        if (!gate.HooksAllowed)
        {
            return Strings.GearsetSwitchPaused;
        }

        if (!clientState.IsLoggedIn)
        {
            return Strings.GearsetSwitchLoggedOut;
        }

        if (condition[ConditionFlag.InCombat])
        {
            return Strings.TravelReasonCombat;
        }

        if (condition[ConditionFlag.Casting] || condition[ConditionFlag.Casting87])
        {
            return Strings.TravelReasonCasting;
        }

        if (condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51])
        {
            return Strings.TravelReasonLoading;
        }

        if (condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.WatchingCutscene] || condition[ConditionFlag.WatchingCutscene78])
        {
            return Strings.TravelReasonCutscene;
        }

        if (condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95])
        {
            return Strings.TravelReasonDuty;
        }

        if (condition[ConditionFlag.OccupiedInQuestEvent] || condition[ConditionFlag.OccupiedInEvent] || condition[ConditionFlag.Occupied]
            || condition[ConditionFlag.Occupied30] || condition[ConditionFlag.Occupied33] || condition[ConditionFlag.Occupied38]
            || condition[ConditionFlag.Occupied39] || condition[ConditionFlag.OccupiedSummoningBell] || condition[ConditionFlag.Crafting])
        {
            return Strings.TravelReasonOccupied;
        }

        return null;
    }

    /// <summary>
    /// Equips <paramref name="gearset"/> through the game's gearset function, on the player's click. False (and a log
    /// line) when it is refused now (<see cref="Blocker"/>), the gearset is gone or the call failed.
    /// </summary>
    public bool Switch(GearsetInfo gearset)
    {
        if (Blocker() is { } reason)
        {
            log.Information("Switch gearset to {Id} not started: {Reason}", gearset.Id + 1, reason);
            return false;
        }

        try
        {
            var module = RaptureGearsetModule.Instance();
            if (module == null || !module->IsValidGearset(gearset.Id))
            {
                return false;
            }

            // The game answers the request itself (a log message when it refuses); the return value is only logged.
            var result = module->EquipGearset(gearset.Id, 0);
            readAt = long.MinValue;
            log.Debug("Switch gearset to {Id} ({Job}) requested; the game answered {Result}", gearset.Id + 1, gearset.Job, result);
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Switch gearset to {Id} failed", gearset.Id + 1);
            return false;
        }
    }
}
