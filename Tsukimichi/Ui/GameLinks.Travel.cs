using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Utility;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Travel;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>Why Teleport cannot start now; <see cref="None"/> when it can.</summary>
public enum TeleportBlock
{
    None,
    NoGiverPlace,
    NoAetheryte,
    NoLifestream,
    NotAttuned,
    Busy,
}

/// <summary>
/// What Teleport would do for a quest: the attuned aetheryte nearest the giver (<paramref name="Target"/>), the
/// special zone it serves (<paramref name="Special"/>), and whether the player already stands closer to the giver than
/// that aetheryte (<paramref name="AlreadyHere"/>, the button then de-emphasises).
/// </summary>
public readonly record struct TeleportCheck(TeleportBlock Block, AetheryteInfo? Target, TravelSpecial Special, bool AlreadyHere)
{
    public bool Ready => Block == TeleportBlock.None;
}

/// <summary>Why Walk to giver cannot start now; <see cref="None"/> when it can.</summary>
public enum WalkBlock
{
    None,
    NoVnavmesh,
    NoGiverPlace,
    NotInZone,
    Loading,
    Combat,
    Cutscene,
    Busy,

    /// <summary>vnavmesh has no navmesh for the zone yet; <see cref="WalkCheck.Progress"/> says how far its build is.</summary>
    Preparing,

    /// <summary>The character is casting (a teleport among others).</summary>
    Casting,

    /// <summary>vnavmesh is already walking for another plugin.</summary>
    Moving,

    /// <summary>Questionable is running and drives the character.</summary>
    QuestionableRunning,

    /// <summary>AutoDuty is running and drives the character.</summary>
    AutoDutyRunning,
}

/// <summary>
/// What the Walk button shows: whether a walk can start, or (<paramref name="Stoppable"/>) that something is moving
/// and the button is Stop; <paramref name="Progress"/> is vnavmesh's build progress (0..1, negative when unknown).
/// </summary>
public readonly record struct WalkCheck(WalkBlock Block, bool Stoppable, float Progress)
{
    public bool Ready => Block == WalkBlock.None && !Stoppable;
}

/// <summary>Why Go to giver cannot start now; <see cref="None"/> when it can.</summary>
public enum GoToBlock
{
    None,
    NoVnavmesh,
    NoGiverPlace,
    NoLifestream,
    NoAetheryte,
    NotAttuned,

    /// <summary>The giver's zone is entered through a conversation (Island Sanctuary, the Occult Crescent): Teleport only.</summary>
    Conversation,
    Busy,
    Loading,
    Combat,
    Cutscene,

    /// <summary>The character is casting (a teleport among others).</summary>
    Casting,

    /// <summary>vnavmesh is already walking for another plugin.</summary>
    Moving,

    /// <summary>Questionable is running and drives the character.</summary>
    QuestionableRunning,

    /// <summary>AutoDuty is running and drives the character.</summary>
    AutoDutyRunning,
}

/// <summary>What Go to giver would do (<paramref name="Plan"/>), or why not; <paramref name="Stoppable"/> while a chain or walk runs.</summary>
public readonly record struct GoToCheck(GoToBlock Block, GoToGiverPlan? Plan, bool Stoppable)
{
    public bool Ready => Block == GoToBlock.None && !Stoppable && Plan is not null;
}

/// <summary>Whether the aethernet hop is offered (<see cref="Hidden"/> when it does not apply) and why it waits.</summary>
public enum HopBlock
{
    Hidden,
    None,
    NoLifestream,
    Busy,

    /// <summary>The player is in the city but not standing at its aetheryte or a shard (Lifestream needs one).</summary>
    NotAtAetheryte,
}

/// <summary>The hop toward a giver in a city: to <paramref name="Shard"/>, or to the Firmament; <paramref name="City"/> is the network's aetheryte.</summary>
public readonly record struct HopCheck(HopBlock Block, AetheryteInfo? Shard, bool Firmament, AetheryteInfo? City)
{
    public bool Visible => Block != HopBlock.Hidden;

    public bool Ready => Block == HopBlock.None;
}

/// <summary>
/// Travel to quest givers (feature plan v5, 1.6.0), the API every pane and the route window call. Teleport goes
/// through Lifestream only (decision 2): it knows attunement and the gil cost, says "already here", and without
/// Lifestream stays visible and names it. Walk to giver and Go to giver move the character through vnavmesh and only
/// on an explicit click (decision 1), each with Stop. The checks are cheap enough to run per frame for the visible
/// rows (a handful of aetherytes per zone, cached attunement); the tooltips are composed only on hover.
/// </summary>
public sealed partial class GameLinks
{
    private const string Separator = " · ";

    private readonly TravelClickGuard clickGuard = new();
    private AetheryteIndex? aetherytes;
    private Func<uint, bool>? isAttuned;
    private Func<uint, bool>? isShardAttuned;

    /// <summary>Whether Questionable is running (its live status); Walk and Go to giver wait meanwhile. Unset reads as not running.</summary>
    public Func<bool>? QuestionableRunning { get; set; }

    /// <summary>Whether AutoDuty is not stopped; Walk and Go to giver wait meanwhile. Unset reads as stopped.</summary>
    public Func<bool>? AutoDutyRunning { get; set; }

    /// <summary>
    /// Moves whenever attunement (aetherytes, their cost, aethernet shards) may have changed
    /// (<see cref="TravelService.AttunementRevision"/>); 0 without travel. A cache of teleport targets keys on it.
    /// </summary>
    public int AttunementRevision => Travel?.AttunementRevision ?? 0;

    /// <summary>Lifestream's IPC, attached by the plugin; null (no teleport) until then.</summary>
    public LifestreamIpc? Lifestream { get; set; }

    /// <summary>Attunement, the player's place and the Go to giver chain, attached by the plugin; null (no walking) until then.</summary>
    public TravelService? Travel { get; set; }

    /// <summary>Settings › Integrations › Show Walk to giver; shown when unset.</summary>
    public Func<bool>? ShowWalk { get; set; }

    /// <summary>Settings › Integrations › Show Go to giver; shown when unset.</summary>
    public Func<bool>? ShowGoTo { get; set; }

    /// <summary>Teleportable aetherytes by territory and aethernet shards by city, read from the sheets on first use; empty when the read fails.</summary>
    public AetheryteIndex Aetherytes
    {
        get
        {
            if (aetherytes is null)
            {
                try
                {
                    aetherytes = AetheryteIndex.Build(data.Excel, data.Language.ToLumina());
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "Aetheryte index could not be built; teleport to giver is unavailable");
                    aetherytes = AetheryteIndex.Empty;
                }
            }

            return aetherytes;
        }
    }

    /// <summary>True while Lifestream is loaded. Teleport buttons show either way; without it they are disabled and name it.</summary>
    public bool TeleportAvailable => Lifestream?.Available == true;

    /// <summary>
    /// True while Lifestream is busy with another task, a cast or loading screen is under way (a teleport just asked
    /// for among them), or Go to giver runs; teleport controls are disabled meanwhile.
    /// </summary>
    public bool TeleportBusy => Lifestream?.IsBusy == true || Travel is { } travel && (travel.TeleportInProgress || travel.JourneyActive);

    /// <summary>True while vnavmesh is loaded.</summary>
    public bool WalkAvailable => Travel?.Vnavmesh.Available == true;

    /// <summary>Whether Walk to giver buttons are drawn at all (the setting; on by default).</summary>
    public bool WalkShown => ShowWalk?.Invoke() ?? true;

    /// <summary>Whether Go to giver buttons are drawn at all (the setting; on by default).</summary>
    public bool GoToShown => ShowGoTo?.Invoke() ?? true;

    /// <summary>True while a walk or Go to giver Tsukimichi started runs (Walk and Go to giver buttons read Stop).</summary>
    public bool IsTraveling => Travel?.JourneyActive == true;

    /// <summary>
    /// The one Stop: cancels the Go to giver or walk Tsukimichi started (never another plugin's walk). Ignored within
    /// <see cref="TravelClickGuard.WindowMs"/> of a start, so a double click on Walk does not start and stop at once.
    /// </summary>
    public void StopTravel()
    {
        if (clickGuard.Holding(Environment.TickCount64))
        {
            return;
        }

        Travel?.Stop();
    }

    /// <summary>
    /// The Stop tooltip: while Go to giver waits for its teleport cast, that the cast lands and nothing follows it;
    /// else Stop with the step under way.
    /// </summary>
    public string StopTooltip()
    {
        if (Travel is not { JourneyActive: true } travel)
        {
            return Strings.TravelStopTooltip;
        }

        if (travel.JourneyCastPending)
        {
            return Strings.TravelStopAfterCastTooltip;
        }

        var step = travel.JourneyStep switch
        {
            GoToGiverStep.Teleporting => Strings.TravelStepTeleporting,
            GoToGiverStep.Hopping => Strings.TravelStepHopping,
            GoToGiverStep.PreparingPath => Strings.TravelStepPreparing,
            _ => Strings.TravelStepWalking,
        };
        return string.Format(CultureInfo.CurrentCulture, Strings.TravelGoToStopTooltipFormat, step);
    }

    /// <summary>True while a click is still part of the double click that just started travel; it is ignored.</summary>
    private bool ClickHeld => clickGuard.Holding(Environment.TickCount64);

    private void MarkStarted() => clickGuard.Started(Environment.TickCount64);

    private Func<uint, bool> IsAttuned => isAttuned ??= id => Travel?.IsAttuned(id) ?? true;

    private Func<uint, bool> IsShardAttuned => isShardAttuned ??= id => Travel?.IsShardAttuned(id) ?? false;

    // ------------------------------------------------------------------ teleport

    /// <summary>
    /// The aetheryte Teleport goes to for the quest's giver (id and place name): the attuned one nearest the giver,
    /// or null without an issuer or an attuned aetheryte for the giver's zone.
    /// </summary>
    public (uint Id, string Name)? NearestAetheryte(QuestRecord quest) =>
        CheckTeleport(quest).Target is { } target ? (target.RowId, target.Name) : null;

    /// <summary>True when Teleport can start now: Lifestream loaded and idle, and an attuned aetheryte serves the giver.</summary>
    public bool CanTeleport(QuestRecord quest) => CheckTeleport(quest).Ready;

    /// <summary>What Teleport would do for the quest's giver now, or why it cannot.</summary>
    public TeleportCheck CheckTeleport(QuestRecord quest)
    {
        if (quest.Issuer is not { TerritoryId: > 0 } issuer)
        {
            return new TeleportCheck(TeleportBlock.NoGiverPlace, null, TravelSpecial.None, false);
        }

        var special = TravelSpecials.Classify(issuer.TerritoryId);
        var index = Aetherytes;
        var fallback = index.TerritoryDefault(issuer.TerritoryId);
        var nodes = index.NodesInTerritory(issuer.TerritoryId);
        var chosen = TravelPlanner.NearestAttuned(nodes, fallback?.Node, issuer.X, issuer.Z, IsAttuned);
        var target = chosen is { } node ? index.Find(node.RowId) : null;
        var here = Travel is { Position: { } at } travel
            && TravelPlanner.IsAlreadyHere(travel.Territory, at.X, at.Z, issuer.TerritoryId, issuer.X, issuer.Z, target?.Node);

        var block = TravelSpecials.NeedsConversation(special)
            ? !TeleportAvailable ? TeleportBlock.NoLifestream : TeleportBusy ? TeleportBlock.Busy : TeleportBlock.None
            : nodes.Count == 0 && fallback is null ? TeleportBlock.NoAetheryte
            : !TeleportAvailable ? TeleportBlock.NoLifestream
            : target is null ? TeleportBlock.NotAttuned
            : TeleportBusy ? TeleportBlock.Busy
            : TeleportBlock.None;
        return new TeleportCheck(block, target, special, here);
    }

    /// <summary>
    /// Teleports toward the quest's giver through Lifestream: to the attuned aetheryte nearest the giver, or, for
    /// Island Sanctuary and the Occult Crescent, Lifestream's <c>/li island</c> / <c>/li occult</c> (an explicit click
    /// only; the tooltip says Lifestream talks to the NPC). When Lifestream refuses, one chat line says why. False when
    /// nothing was started.
    /// </summary>
    public bool TeleportToGiver(QuestRecord quest)
    {
        var check = CheckTeleport(quest);
        if (!check.Ready || Lifestream is not { } lifestream || ClickHeld)
        {
            return false;
        }

        if (TravelSpecials.LifestreamCommand(check.Special) is { } command)
        {
            var sent = lifestream.ExecuteCommand(command);
            if (sent)
            {
                MarkStarted();
            }

            return sent;
        }

        if (check.Target is not { } target)
        {
            return false;
        }

        var started = Travel?.Teleport(target.RowId, target.Name) ?? lifestream.Teleport(target.RowId);
        if (started)
        {
            MarkStarted();
        }
        else
        {
            log.Warning("Teleport to {Aetheryte} ({AetheryteId}) for quest {RowId} did not start", target.Name, target.RowId, quest.RowId);
        }

        return started;
    }

    /// <summary>The Teleport tooltip: where it goes, the gil cost and favourite flag, the next step for a special zone, "already here", or why it cannot.</summary>
    public string TeleportTooltip(QuestRecord quest) => TeleportTooltip(quest, CheckTeleport(quest));

    /// <inheritdoc cref="TeleportTooltip(QuestRecord)"/>
    public string TeleportTooltip(QuestRecord quest, TeleportCheck check)
    {
        switch (check.Block)
        {
            case TeleportBlock.NoGiverPlace:
                return Strings.TravelNoGiverPlace;
            case TeleportBlock.NoAetheryte:
                return Strings.TeleportNoAetheryte;
            case TeleportBlock.NoLifestream:
                return NeedsLifestream();
            case TeleportBlock.NotAttuned:
                return string.Format(CultureInfo.CurrentCulture, Strings.TravelNotAttunedFormat, ZoneName(quest));
            case TeleportBlock.Busy:
                return BusyReason();
        }

        var lines = new List<string>(4);
        if (check.Special == TravelSpecial.IslandSanctuary)
        {
            lines.Add(Strings.TravelIslandTooltip);
        }
        else if (check.Special == TravelSpecial.OccultCrescent)
        {
            lines.Add(Strings.TravelOccultTooltip);
        }
        else if (check.Target is { } target)
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.TeleportTooltipFormat, target.Name));
            if (Travel?.TryGetCost(target.RowId, out var gil, out var favourite) == true)
            {
                var cost = string.Format(CultureInfo.CurrentCulture, Strings.TravelCostFormat, gil.ToString("N0", CultureInfo.CurrentCulture));
                lines.Add(favourite ? cost + Separator + Strings.TravelFavourite : cost);
            }

            if (check.Special == TravelSpecial.Firmament)
            {
                lines.Add(Strings.TravelFirmamentHint);
            }
            else if (check.Special == TravelSpecial.CosmicExploration)
            {
                lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.TravelCosmicHintFormat, ZoneName(quest)));
            }
        }

        if (check.AlreadyHere)
        {
            lines.Add(check.Target is { } t && quest.Issuer is { } issuer && t.TerritoryId == issuer.TerritoryId
                ? string.Format(CultureInfo.CurrentCulture, Strings.TravelAlreadyHereFormat, t.Name)
                : Strings.TravelAlreadyInZone);
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// A greyed travel button's tooltip when its plugin is not loaded: the companion registry's reason ("Needs
    /// Lifestream — see Settings › Integrations", "… installed but turned off", "Needs a newer …") over why it is needed.
    /// </summary>
    private static string NeedsPlugin(CompanionPlugin plugin, string why) =>
        CompanionPlugins.DisabledReason(plugin) is { } reason ? reason + "\n" + why : why;

    private static string NeedsLifestream() => NeedsPlugin(CompanionPlugin.Lifestream, Strings.TravelNeedsLifestream);

    /// <summary>Why a busy teleport or hop waits: Go to giver under way, a cast or loading screen, or Lifestream's own task.</summary>
    private string BusyReason() =>
        Travel is { JourneyActive: true } ? Strings.TravelBusyJourney
        : Travel is { TeleportInProgress: true } ? Strings.TravelBusyCasting
        : Strings.TeleportBusy;

    private static string NeedsVnavmesh() => NeedsPlugin(CompanionPlugin.Vnavmesh, Strings.TravelNeedsVnavmesh);

    /// <summary>The giver's zone name (the Map sheet's place name); empty when unknown.</summary>
    private string ZoneName(QuestRecord quest) => quest.Issuer is { } issuer ? Map(issuer.MapId)?.PlaceName ?? string.Empty : string.Empty;

    // ------------------------------------------------------------------ aethernet hop

    /// <summary>
    /// The aethernet hop toward a giver in a city, offered while the player is in that city's network: to the
    /// attuned shard nearest the giver when it beats walking from where the player stands by
    /// <see cref="TravelPlanner.HopMargin"/>, or to the Firmament from the Foundation. Lifestream needs the player at
    /// the city's aetheryte or a shard (its <c>GetActiveAetheryte</c>); elsewhere in the city the hop shows, disabled.
    /// </summary>
    public HopCheck CheckHop(QuestRecord quest)
    {
        if (Travel is not { Position: { } at } travel || quest.Issuer is not { TerritoryId: > 0 } issuer)
        {
            return default;
        }

        var index = Aetherytes;
        var firmament = TravelSpecials.Classify(issuer.TerritoryId) == TravelSpecial.Firmament;
        var city = firmament ? index.Find(TravelSpecials.FoundationAetheryte) : index.Nearest(issuer.TerritoryId, issuer.X, issuer.Z);
        if (city is not { Group: > 0 } || !InNetwork(travel.Territory, city.Group))
        {
            return default;
        }

        AetheryteInfo? shard = null;
        if (!firmament)
        {
            var here = new TravelNode(0, travel.Territory, at.X, at.Z, city.Group);
            if (TravelPlanner.ChooseShard(here, index.ShardNodesInGroup(city.Group), issuer.TerritoryId, issuer.X, issuer.Z, IsShardAttuned) is not { } best
                || index.Find(best.RowId) is not { } found)
            {
                return default;
            }

            shard = found;
        }

        HopBlock block;
        if (!TeleportAvailable)
        {
            block = HopBlock.NoLifestream;
        }
        else if (TeleportBusy)
        {
            block = HopBlock.Busy;
        }
        else if (!Lifestream!.CanReadActiveAetheryte)
        {
            // A Lifestream without GetActiveAetheryte: offer the hop and let Lifestream say if the player is not at one.
            block = HopBlock.None;
        }
        else
        {
            var active = Lifestream.ActiveAetheryte;
            var atNetwork = firmament
                ? active == TravelSpecials.FoundationAetheryte
                : active != 0 && index.Find(active)?.Group == city.Group;
            block = atNetwork ? HopBlock.None : HopBlock.NotAtAetheryte;
        }

        return new HopCheck(block, shard, firmament, city);
    }

    /// <summary>True when the territory holds the network's aetheryte or one of its shards.</summary>
    private bool InNetwork(uint territory, uint group)
    {
        if (territory == 0)
        {
            return false;
        }

        if (Aetherytes.GroupAetheryte(group)?.TerritoryId == territory)
        {
            return true;
        }

        foreach (var shard in Aetherytes.ShardNodesInGroup(group))
        {
            if (shard.TerritoryId == territory)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Takes the aethernet toward the quest's giver through Lifestream. False when nothing was started.</summary>
    public bool AethernetToGiver(QuestRecord quest)
    {
        var check = CheckHop(quest);
        if (!check.Ready || Lifestream is not { } lifestream || ClickHeld)
        {
            return false;
        }

        var started = check.Firmament ? lifestream.AethernetTeleportToFirmament() : check.Shard is { } shard && lifestream.AethernetTeleport(shard.RowId);
        if (started)
        {
            MarkStarted();
        }
        else
        {
            log.Warning("Aethernet hop for quest {RowId} did not start", quest.RowId);
        }

        return started;
    }

    /// <summary>"Aethernet to Lancers' Guild" (or the Firmament).</summary>
    public static string HopLabel(HopCheck check) =>
        string.Format(CultureInfo.CurrentCulture, Strings.TravelHopFormat, check.Firmament ? Strings.TravelFirmament : check.Shard?.Name ?? string.Empty);

    /// <summary>The hop's tooltip: where it goes, or why it waits.</summary>
    public static string HopTooltip(HopCheck check) => check.Block switch
    {
        HopBlock.NoLifestream => NeedsLifestream(),
        HopBlock.Busy => Strings.TeleportBusy,
        HopBlock.NotAtAetheryte => string.Format(CultureInfo.CurrentCulture, Strings.TravelHopStandAtFormat, check.City?.Name ?? string.Empty),
        _ => string.Format(CultureInfo.CurrentCulture, Strings.TravelHopTooltipFormat, check.Firmament ? Strings.TravelFirmament : check.Shard?.Name ?? string.Empty),
    };

    // ------------------------------------------------------------------ walk

    /// <summary>
    /// What Walk to giver would do now: start, Stop (a walk or Go to giver Tsukimichi started is under way), or why it
    /// cannot. It waits while Questionable or AutoDuty drives the character, while the character casts, and while
    /// vnavmesh walks for another plugin (whose walk it never stops).
    /// </summary>
    public WalkCheck CheckWalk(QuestRecord quest)
    {
        if (Travel is not { } travel || !travel.Vnavmesh.Available)
        {
            return new WalkCheck(WalkBlock.NoVnavmesh, false, -1f);
        }

        if (travel.JourneyActive)
        {
            return new WalkCheck(WalkBlock.None, true, -1f);
        }

        var block = quest.Issuer is not { TerritoryId: > 0 } issuer ? WalkBlock.NoGiverPlace
            : travel.BetweenAreas ? WalkBlock.Loading
            : travel.Territory != issuer.TerritoryId ? WalkBlock.NotInZone
            : AutomationBlock() is { } automation ? automation
            : travel.InCombat ? WalkBlock.Combat
            : travel.InCutscene ? WalkBlock.Cutscene
            : travel.Casting ? WalkBlock.Casting
            : travel.Walking ? WalkBlock.Moving
            : travel.LifestreamBusy ? WalkBlock.Busy
            : !travel.NavReady ? WalkBlock.Preparing
            : WalkBlock.None;
        return new WalkCheck(block, false, block == WalkBlock.Preparing ? travel.Vnavmesh.BuildProgress : -1f);
    }

    /// <summary>The plugin driving the character now, Questionable first; null when neither runs.</summary>
    private WalkBlock? AutomationBlock() =>
        QuestionableRunning?.Invoke() == true ? WalkBlock.QuestionableRunning
        : AutoDutyRunning?.Invoke() == true ? WalkBlock.AutoDutyRunning
        : null;

    /// <summary>True when Walk to giver can start now: vnavmesh loaded and ready, the player idle in the giver's zone.</summary>
    public bool CanWalk(QuestRecord quest) => CheckWalk(quest).Ready;

    /// <summary>
    /// Walks the character to the quest's giver through vnavmesh (it stops about 3 yalms away). Stop with
    /// <see cref="StopTravel"/>; leaving the zone, logging out or unloading stops it too. False when nothing was started.
    /// </summary>
    public bool WalkToGiver(QuestRecord quest)
    {
        if (ClickHeld || !CheckWalk(quest).Ready || Travel is not { } travel || quest.Issuer is not { } issuer)
        {
            return false;
        }

        travel.Start(GoToGiverPlan.WalkOnly(issuer.TerritoryId, issuer.X, issuer.Y, issuer.Z));
        if (travel.JourneyActive)
        {
            MarkStarted();
        }

        return travel.JourneyActive;
    }

    /// <summary>The Walk button's label: Stop while moving, "Preparing path… 40%" while vnavmesh builds, else <paramref name="walk"/>.</summary>
    public static string WalkLabel(WalkCheck check, string walk) =>
        check.Stoppable ? Strings.TravelStop
        : check.Block != WalkBlock.Preparing ? walk
        : check.Progress >= 0f ? string.Format(CultureInfo.CurrentCulture, Strings.TravelPreparingFormat, (int)MathF.Round(check.Progress * 100f))
        : Strings.TravelPreparing;

    /// <summary>The Walk button's tooltip: what it does, Stop, or why it cannot.</summary>
    public string WalkTooltip(QuestRecord quest, WalkCheck check)
    {
        if (check.Stoppable)
        {
            return StopTooltip();
        }

        return check.Block switch
        {
            WalkBlock.NoVnavmesh => NeedsVnavmesh(),
            WalkBlock.NoGiverPlace => Strings.TravelNoGiverPlace,
            WalkBlock.NotInZone => string.Format(CultureInfo.CurrentCulture, Strings.TravelWalkNotInZoneFormat, ZoneName(quest)),
            WalkBlock.Loading => Strings.TravelWalkLoading,
            WalkBlock.Combat => Strings.TravelWalkCombat,
            WalkBlock.Cutscene => Strings.TravelWalkCutscene,
            WalkBlock.Busy => Strings.TeleportBusy,
            WalkBlock.Preparing => WalkLabel(check, Strings.TravelPreparing),
            WalkBlock.Casting => Strings.TravelBusyCasting,
            WalkBlock.Moving => Strings.TravelBusyMoving,
            WalkBlock.QuestionableRunning => Strings.TravelBusyQuestionable,
            WalkBlock.AutoDutyRunning => Strings.TravelBusyAutoDuty,
            _ => Strings.TravelWalkTooltip,
        };
    }

    // ------------------------------------------------------------------ go to giver

    /// <summary>
    /// What Go to giver would do now: walk when the player is already closer than any teleport would bring them
    /// (after a hop when standing at the city's aetheryte); else teleport to the attuned aetheryte nearest the giver,
    /// take the aethernet to the shard nearest the giver (or to the Firmament), and walk when the trip ends in the
    /// giver's zone.
    /// </summary>
    public GoToCheck CheckGoTo(QuestRecord quest)
    {
        if (Travel is not { } travel || !travel.Vnavmesh.Available)
        {
            return new GoToCheck(GoToBlock.NoVnavmesh, null, false);
        }

        if (travel.JourneyActive)
        {
            return new GoToCheck(GoToBlock.None, null, true);
        }

        if (quest.Issuer is not { TerritoryId: > 0 } issuer)
        {
            return new GoToCheck(GoToBlock.NoGiverPlace, null, false);
        }

        var teleport = CheckTeleport(quest);
        // A conversation zone is never entered by the chain; once inside it, the walk is fine.
        var conversation = (TravelSpecials.NeedsConversation(teleport.Special) || teleport.Special == TravelSpecial.CosmicExploration)
            && travel.Territory != issuer.TerritoryId;
        var automation = AutomationBlock();
        var block = conversation ? GoToBlock.Conversation
            : travel.BetweenAreas ? GoToBlock.Loading
            : automation == WalkBlock.QuestionableRunning ? GoToBlock.QuestionableRunning
            : automation == WalkBlock.AutoDutyRunning ? GoToBlock.AutoDutyRunning
            : travel.InCutscene ? GoToBlock.Cutscene
            : travel.InCombat ? GoToBlock.Combat
            : travel.Casting ? GoToBlock.Casting
            : travel.Walking ? GoToBlock.Moving
            : travel.LifestreamBusy ? GoToBlock.Busy
            : GoToBlock.None;
        if (block != GoToBlock.None)
        {
            return new GoToCheck(block, null, false);
        }

        // Standing at the city's aetheryte (or already in the giver's zone and closer than any aetheryte there).
        var hop = CheckHop(quest);
        TravelLeg? hopLeg = hop.Ready ? HopLeg(hop) : null;
        if (teleport.AlreadyHere || hopLeg is not null)
        {
            var end = hopLeg?.TerritoryId ?? travel.Territory;
            return new GoToCheck(GoToBlock.None, Plan(issuer, null, hopLeg, end), false);
        }

        if (!teleport.Ready || teleport.Target is not { } target)
        {
            // In the giver's zone already, no teleport is needed to get going: walk from here.
            if (travel.Territory == issuer.TerritoryId && teleport.Block is TeleportBlock.NoLifestream or TeleportBlock.NotAttuned or TeleportBlock.NoAetheryte)
            {
                return new GoToCheck(GoToBlock.None, Plan(issuer, null, null, travel.Territory), false);
            }

            var reason = teleport.Block switch
            {
                TeleportBlock.NoGiverPlace => GoToBlock.NoGiverPlace,
                TeleportBlock.NoAetheryte => GoToBlock.NoAetheryte,
                TeleportBlock.NoLifestream => GoToBlock.NoLifestream,
                TeleportBlock.NotAttuned => GoToBlock.NotAttuned,
                _ => GoToBlock.Busy,
            };
            return new GoToCheck(reason, null, false);
        }

        TravelLeg? arrivalHop = null;
        if (teleport.Special == TravelSpecial.Firmament)
        {
            arrivalHop = new TravelLeg(GoToGiverPlan.FirmamentHop, TravelSpecials.FirmamentTerritory);
        }
        else if (TravelPlanner.ChooseShard(target.Node, Aetherytes.ShardNodesInGroup(target.Group), issuer.TerritoryId, issuer.X, issuer.Z, IsShardAttuned) is { } shard)
        {
            arrivalHop = new TravelLeg(shard.RowId, shard.TerritoryId);
        }

        var leg = new TravelLeg(target.RowId, target.TerritoryId);
        return new GoToCheck(GoToBlock.None, Plan(issuer, leg, arrivalHop, arrivalHop?.TerritoryId ?? target.TerritoryId), false);
    }

    private static TravelLeg? HopLeg(HopCheck hop) =>
        hop.Firmament ? new TravelLeg(GoToGiverPlan.FirmamentHop, TravelSpecials.FirmamentTerritory)
        : hop.Shard is { } shard ? new TravelLeg(shard.RowId, shard.TerritoryId)
        : null;

    /// <summary>The plan for the legs; it walks only when the trip ends in the giver's zone (vnavmesh does not cross zone lines).</summary>
    private static GoToGiverPlan Plan(Issuer issuer, TravelLeg? teleport, TravelLeg? hop, uint endTerritory) =>
        new(issuer.TerritoryId, issuer.X, issuer.Y, issuer.Z, teleport, hop, endTerritory == issuer.TerritoryId);

    /// <summary>True when Go to giver can start now.</summary>
    public bool CanGoToGiver(QuestRecord quest) => CheckGoTo(quest).Ready;

    /// <summary>
    /// Starts the Go to giver chain: teleport (Lifestream), wait for the arrival, an aethernet hop, then the walk
    /// (vnavmesh). Every step can be stopped with <see cref="StopTravel"/> and times out with a chat line. False when
    /// nothing was started.
    /// </summary>
    public bool GoToGiver(QuestRecord quest)
    {
        if (ClickHeld || CheckGoTo(quest) is not { Ready: true, Plan: { } plan } || Travel is not { } travel)
        {
            return false;
        }

        travel.Start(plan);
        if (travel.JourneyActive)
        {
            MarkStarted();
        }

        return travel.JourneyActive;
    }

    /// <summary>The Go to giver tooltip: its steps, Stop with the step under way, or why it cannot.</summary>
    public string GoToTooltip(QuestRecord quest, GoToCheck check)
    {
        if (check.Stoppable)
        {
            return StopTooltip();
        }

        switch (check.Block)
        {
            case GoToBlock.NoVnavmesh:
                return NeedsVnavmesh();
            case GoToBlock.NoGiverPlace:
                return Strings.TravelNoGiverPlace;
            case GoToBlock.NoLifestream:
                return NeedsLifestream();
            case GoToBlock.NoAetheryte:
                return Strings.TeleportNoAetheryte;
            case GoToBlock.NotAttuned:
                return string.Format(CultureInfo.CurrentCulture, Strings.TravelNotAttunedFormat, ZoneName(quest));
            case GoToBlock.Conversation:
                return string.Format(CultureInfo.CurrentCulture, Strings.TravelGoToConversationFormat, ZoneName(quest));
            case GoToBlock.Busy:
                return Strings.TeleportBusy;
            case GoToBlock.Casting:
                return Strings.TravelBusyCasting;
            case GoToBlock.Moving:
                return Strings.TravelBusyMoving;
            case GoToBlock.QuestionableRunning:
                return Strings.TravelBusyQuestionable;
            case GoToBlock.AutoDutyRunning:
                return Strings.TravelBusyAutoDuty;
            case GoToBlock.Loading:
                return Strings.TravelWalkLoading;
            case GoToBlock.Combat:
                return Strings.TravelWalkCombat;
            case GoToBlock.Cutscene:
                return Strings.TravelWalkCutscene;
        }

        var lines = new List<string>(5) { Strings.TravelGoToTooltip };
        if (check.Plan is not { } plan)
        {
            return lines[0];
        }

        if (plan.Teleport is { } teleport)
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.TravelGoToStepTeleportFormat, Aetherytes.Find(teleport.Id)?.Name ?? string.Empty));
        }

        if (plan.Hop is { } hop)
        {
            var name = hop.Id == GoToGiverPlan.FirmamentHop ? Strings.TravelFirmament : Aetherytes.Find(hop.Id)?.Name ?? string.Empty;
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.TravelGoToStepHopFormat, name));
        }

        lines.Add(plan.Walk ? Strings.TravelGoToStepWalk : string.Format(CultureInfo.CurrentCulture, Strings.TravelGoToNoWalkFormat, ZoneName(quest)));
        return string.Join('\n', lines);
    }
}
