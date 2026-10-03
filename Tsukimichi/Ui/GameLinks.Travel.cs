using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;
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

    /// <summary>Lifestream is loaded but a setting of its blocks the hand-off (<see cref="CompanionPlugins.DisabledReason"/>).</summary>
    LifestreamSetup,

    /// <summary>The nearest aetheryte is not attuned and the nearest attuned one stands in another region.</summary>
    TooFar,
}

/// <summary>
/// What Teleport would do for a quest: the attuned aetheryte nearest the goal (<paramref name="Target"/>: the giver,
/// or the way into the interior the giver stands in), the special zone it serves (<paramref name="Special"/>), and
/// whether the player already stands closer to the goal than that aetheryte (<paramref name="AlreadyHere"/>, the button
/// then de-emphasises). <see cref="Skipped"/> is the nearer aetheryte that is not attuned, when Teleport goes elsewhere.
/// </summary>
public readonly record struct TeleportCheck(TeleportBlock Block, AetheryteInfo? Target, TravelSpecial Special, bool AlreadyHere)
{
    public bool Ready => Block == TeleportBlock.None;

    /// <summary>The aetheryte nearest the goal when it is not attuned and <see cref="Target"/> is another one; else null.</summary>
    public AetheryteInfo? Skipped { get; init; }

    /// <summary>Where travel aims: the giver, or the way into the interior the giver stands in.</summary>
    public TravelGoal? Goal { get; init; }
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

    /// <summary>vnavmesh is loaded but a setting of its blocks the walk (<see cref="CompanionPlugins.DisabledReason"/>).</summary>
    VnavmeshSetup,

    /// <summary>The giver stands inside an interior whose way in the data does not place.</summary>
    NoEntrance,
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

    /// <summary>vnavmesh is loaded but a setting of its blocks the walk.</summary>
    VnavmeshSetup,

    /// <summary>Lifestream is loaded but a setting of its blocks the teleport the trip needs.</summary>
    LifestreamSetup,

    /// <summary>The nearest aetheryte is not attuned and the nearest attuned one stands in another region.</summary>
    TooFar,
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
/// through Lifestream only (decision 2): it knows attunement and the gil cost, says "already here", says when the
/// nearest aetheryte is not attuned, and without Lifestream stays visible and names it. Walk to giver and Go to giver
/// move the character through vnavmesh and only on an explicit click (decision 1), each with Stop; they mount for a long
/// walk and fly where flying is unlocked (Settings › Integrations › Travel). A giver inside an interior no teleport
/// reaches (the Waking Sands, Fortemps Manor, a story area like Zero's Domain) is travelled to by its way in
/// (<see cref="EntranceIndex"/>): the aetheryte nearest that door, a walk to it, and a note to go in. The checks are
/// cheap enough to run per frame for the visible rows (a handful of aetherytes per zone, cached attunement and doors),
/// and each is worked out once per frame and quest (<see cref="TravelMemo"/>): the detail pane, its pills and their
/// tooltips ask for the same ones several times a frame, and Go to giver builds on Teleport's and the hop's. A click
/// that starts or stops travel works them out afresh. The tooltips are composed only on hover.
/// </summary>
public sealed partial class GameLinks
{
    private const string Separator = " · ";

    private readonly TravelClickGuard clickGuard = new();
    private readonly Dictionary<uint, uint> regions = [];

    // This frame's travel checks by quest row; emptied when the frame moves on, or by a click that starts or stops travel.
    private readonly Dictionary<uint, TravelMemo> travelMemo = [];
    private int travelMemoFrame = -1;
    private AetheryteIndex? aetherytes;
    private EntranceIndex? entrances;
    private System.Threading.CancellationTokenSource? warmCancel;
    private Func<uint, bool>? isAttuned;
    private Func<uint, bool>? isShardAttuned;

    // The status line under the pills, composed once per step, target and language.
    private string? statusText;
    private (GoToGiverStep Step, bool Flying, bool Mounted, GoToGiverPlan? Plan, string Target, int Language) statusKey;

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

    /// <summary>
    /// The aetheryte index warmed at load (feature plan v6 A11); null reads the sheets on first use, as before. Set by
    /// the plugin before anything travels.
    /// </summary>
    public Core.Runtime.WarmedValue<AetheryteIndex>? AetheryteWarmup { get; set; }

    /// <summary>
    /// Teleportable aetherytes by territory and aethernet shards by city: the index warmed at load (a first use while
    /// it still builds waits for that build instead of starting another), else read from the sheets on first use; empty
    /// when the read fails.
    /// </summary>
    public AetheryteIndex Aetherytes
    {
        get
        {
            if (aetherytes is null && AetheryteWarmup is { } warmup)
            {
                // Null when the warm-up failed (logged there): empty, as a failed read always was.
                aetherytes = warmup.Wait() ?? AetheryteIndex.Empty;
            }

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

    /// <summary>
    /// The ways into interiors (doors, NPCs and zone lines from the game's layout files), resolved per territory on
    /// first use, and for every giver's territory ahead on a worker (<see cref="WarmEntrances"/>); empty when the index
    /// cannot be made, which leaves interiors to the aetheryte their zone names.
    /// </summary>
    public EntranceIndex Entrances
    {
        get
        {
            if (entrances is null)
            {
                try
                {
                    entrances = EntranceIndex.Create(data.Excel, path => data.GetFile<LgbFile>(path), Aetherytes, data.Language.ToLumina());
                    entrances.OnError = (ex, territory) =>
                        log.Warning(ex, "Interior entrance for territory {Territory} could not be read; givers there use their zone's aetheryte", territory);
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "Interior entrances unavailable; givers inside buildings use their zone's aetheryte");
                    entrances = EntranceIndex.Empty;
                }
            }

            return entrances;
        }
    }

    /// <summary>
    /// Moves when the ways into interiors became known (a warm-up ended); a cache of givers' aetherytes
    /// (<see cref="GiverAetheryte"/>) keys on it, as it answered with the zone's aetheryte meanwhile.
    /// </summary>
    public int EntranceRevision => entrances?.Revision ?? 0;

    /// <summary>
    /// Resolves the way into every interior a quest giver of <paramref name="catalog"/> stands in, on a worker thread,
    /// so Next stops and the route window never resolve dozens in one frame. Call on the framework thread after a
    /// catalog loads; the aetheryte index is built here first, as the entrances need it. Until it ends,
    /// <see cref="GiverAetheryte"/> answers with the zone's aetheryte for a giver not resolved yet, and
    /// <see cref="EntranceRevision"/> then moves.
    /// </summary>
    public void WarmEntrances(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var index = Entrances;
        if (ReferenceEquals(index, EntranceIndex.Empty))
        {
            return;
        }

        var territories = new HashSet<uint>();
        foreach (var quest in catalog.All)
        {
            if (quest.Issuer is { TerritoryId: > 0 } issuer && index.IsInterior(issuer.TerritoryId))
            {
                territories.Add(issuer.TerritoryId);
            }
        }

        warmCancel ??= new System.Threading.CancellationTokenSource();
        var token = warmCancel.Token;
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var started = System.Diagnostics.Stopwatch.GetTimestamp();
                index.Warm(territories, token);
                log.Debug("Interior entrances: {Count} resolved ahead in {Ms:F0} ms", territories.Count, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Interior entrances could not be resolved ahead; they are resolved on first use");
            }
        });
    }

    /// <summary>Stops a warm-up under way (the plugin unloads).</summary>
    public void StopWarmingEntrances()
    {
        warmCancel?.Cancel();
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
        ForgetTravelFrame();
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
            GoToGiverStep.Mounting => Strings.TravelStepMounting,
            GoToGiverStep.Landing => Strings.TravelStepLanding,
            _ when travel.JourneyFlying => Strings.TravelStepFlying,
            _ => Strings.TravelStepWalking,
        };
        return string.Format(CultureInfo.CurrentCulture, Strings.TravelGoToStopTooltipFormat, step);
    }

    /// <summary>
    /// The live status of a Walk or Go to giver under way, for the line under the detail pane's pills ("Going to giver
    /// · Flying to Varshahn…", "· Mounting…", "· Teleporting to Yedlihmad…"); null while nothing of Tsukimichi's moves.
    /// Composed once per step, target and language, so a per-frame caller costs a few field reads.
    /// </summary>
    public string? TravelStatusText()
    {
        if (Travel is not { JourneyActive: true } travel)
        {
            return null;
        }

        var step = travel.JourneyStep;
        var flying = travel.JourneyFlying;
        var mounted = step == GoToGiverStep.Walking && travel.Mounted;
        var plan = travel.JourneyPlan;
        var key = (step, flying, mounted, plan, travel.JourneyTarget, Localization.Loc.Version);
        if (statusText is not null && key == statusKey)
        {
            return statusText;
        }

        var target = travel.JourneyTarget;
        var text = step switch
        {
            GoToGiverStep.Teleporting when plan?.Teleport is { } leg && Aetherytes.Find(leg.Id) is { } aetheryte =>
                string.Format(CultureInfo.CurrentCulture, Strings.ActionTravelStepTeleportingToFormat, aetheryte.Name),
            GoToGiverStep.Teleporting => Strings.ActionTravelStepTeleporting,
            GoToGiverStep.Hopping when plan?.Hop is { } hop =>
                string.Format(CultureInfo.CurrentCulture, Strings.ActionTravelStepHoppingToFormat, hop.Id == GoToGiverPlan.FirmamentHop ? Strings.TravelFirmament : Aetherytes.Find(hop.Id)?.Name ?? string.Empty),
            GoToGiverStep.Hopping => Strings.ActionTravelStepHopping,
            GoToGiverStep.PreparingPath => Strings.ActionTravelStepPreparing,
            GoToGiverStep.Mounting => Strings.ActionTravelStepMounting,
            GoToGiverStep.Landing => Strings.ActionTravelStepLanding,
            _ when target.Length == 0 => Strings.ActionTravelStepWalking,
            _ => string.Format(
                CultureInfo.CurrentCulture,
                flying ? Strings.ActionTravelStepFlyingToFormat : mounted ? Strings.ActionTravelStepRidingToFormat : Strings.ActionTravelStepWalkingToFormat,
                target),
        };

        statusKey = key;
        statusText = string.Format(CultureInfo.CurrentCulture, Strings.ActionTravelStatusFormat, text);
        return statusText;
    }

    /// <summary>True while a click is still part of the double click that just started travel; it is ignored.</summary>
    private bool ClickHeld => clickGuard.Holding(Environment.TickCount64);

    private void MarkStarted()
    {
        clickGuard.Started(Environment.TickCount64);
        ForgetTravelFrame();
    }

    /// <summary>
    /// The quest's entry in this frame's checks, read or written in place. Emptied when the ImGui frame moved on, so a
    /// check is never older than the frame that asks; not held across another check, which may add an entry.
    /// </summary>
    private ref TravelMemo Memo(QuestRecord quest)
    {
        var frame = ImGui.GetFrameCount();
        if (frame != travelMemoFrame)
        {
            travelMemoFrame = frame;
            travelMemo.Clear();
        }

        return ref CollectionsMarshal.GetValueRefOrAddDefault(travelMemo, quest.RowId, out _);
    }

    /// <summary>Works every check out afresh on the next ask: a click is about to act on them, or just changed what they say.</summary>
    private void ForgetTravelFrame()
    {
        travelMemoFrame = -1;
    }

    /// <summary>One quest's travel checks in one frame; a null field is not worked out yet.</summary>
    private struct TravelMemo
    {
        public TravelGoal? Goal;
        public bool GoalKnown;
        public TeleportCheck? Teleport;
        public HopCheck? Hop;
        public WalkCheck? Walk;
        public GoToCheck? GoTo;
    }

    private Func<uint, bool> IsAttuned => isAttuned ??= id => Travel?.IsAttuned(id) ?? true;

    private Func<uint, bool> IsShardAttuned => isShardAttuned ??= id => Travel?.IsShardAttuned(id) ?? false;

    // ------------------------------------------------------------------ goal

    /// <summary>
    /// Where travel aims for the quest's giver from where the player stands: the giver, or, while the player is outside
    /// an interior the giver stands in, its way in. Null without a giver place.
    /// </summary>
    public TravelGoal? GoalFor(QuestRecord quest)
    {
        if (quest.Issuer is not { TerritoryId: > 0 } issuer)
        {
            return null;
        }

        ref var memo = ref Memo(quest);
        if (!memo.GoalKnown)
        {
            memo.Goal = GiverTravel.Goal(issuer, Entrances, Travel?.Territory ?? 0);
            memo.GoalKnown = true;
        }

        return memo.Goal;
    }

    /// <summary>The giver's display name, or "the giver" when the sheet has none.</summary>
    private static string GiverName(QuestRecord quest) =>
        quest.Issuer is { Name.Length: > 0 } issuer ? issuer.Name : Strings.TravelTheGiver;

    /// <summary>The name of the zone a territory is (its TerritoryType place name); empty when unknown.</summary>
    private string TerritoryName(uint territoryId)
    {
        try
        {
            return data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId)?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Territory name unavailable");
            return string.Empty;
        }
    }

    /// <summary>The region (PlaceNameRegion row) a territory belongs to, read once per territory; 0 when unknown.</summary>
    private uint RegionOf(uint territoryId)
    {
        if (regions.TryGetValue(territoryId, out var region))
        {
            return region;
        }

        try
        {
            region = data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId)?.PlaceNameRegion.RowId ?? 0;
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Territory region unavailable");
            region = 0;
        }

        regions[territoryId] = region;
        return region;
    }

    // ------------------------------------------------------------------ teleport

    /// <summary>
    /// The aetheryte Teleport goes to for the quest's giver (id and place name): the attuned one nearest the goal,
    /// or null without an issuer or an attuned aetheryte for the goal's zone.
    /// </summary>
    public (uint Id, string Name)? NearestAetheryte(QuestRecord quest) =>
        CheckTeleport(quest).Target is { } target ? (target.RowId, target.Name) : null;

    /// <summary>True when Teleport can start now: Lifestream loaded and idle, and an attuned aetheryte serves the giver.</summary>
    public bool CanTeleport(QuestRecord quest) => CheckTeleport(quest).Ready;

    /// <summary>What Teleport would do for the quest's giver now, or why it cannot.</summary>
    public TeleportCheck CheckTeleport(QuestRecord quest)
    {
        if (Memo(quest).Teleport is { } known)
        {
            return known;
        }

        var check = WorkOutTeleport(quest);
        Memo(quest).Teleport = check;
        return check;
    }

    private TeleportCheck WorkOutTeleport(QuestRecord quest)
    {
        if (quest.Issuer is not { TerritoryId: > 0 } issuer || GoalFor(quest) is not { } goal)
        {
            return new TeleportCheck(TeleportBlock.NoGiverPlace, null, TravelSpecial.None, false);
        }

        var special = TravelSpecials.Classify(issuer.TerritoryId);
        var index = Aetherytes;
        var arrival = GiverTravel.Arrival(index, issuer.TerritoryId, goal, IsAttuned);
        var target = arrival.Target is { } node ? index.Find(node.RowId) : null;
        var skipped = arrival.Substituted && arrival.Nearest is { } nearest ? index.Find(nearest.RowId) : null;

        // "Already here": in the goal's zone and closer than the arrival would be. A door the data does not place has no
        // spot to measure from, so only standing inside the giver's own zone counts there.
        var here = Travel is { Position: { } at } travel
            && (goal.Placed
                ? TravelPlanner.IsAlreadyHere(travel.Territory, at.X, at.Z, goal.Place.TerritoryId, goal.Place.X, goal.Place.Z, target?.Node)
                : travel.Territory == issuer.TerritoryId);

        // A substitute in another territory (a city sub-zone's city) must stay in the goal's region, or the trip is absurd.
        var tooFar = skipped is not null && target is { } t && t.TerritoryId != goal.Place.TerritoryId
            && RegionOf(t.TerritoryId) is var targetRegion && RegionOf(goal.Place.TerritoryId) is var goalRegion
            && targetRegion != 0 && goalRegion != 0 && targetRegion != goalRegion;

        var block = TravelSpecials.NeedsConversation(special)
            ? !TeleportAvailable ? TeleportBlock.NoLifestream : LifestreamReason() is not null ? TeleportBlock.LifestreamSetup : TeleportBusy ? TeleportBlock.Busy : TeleportBlock.None
            : arrival.Nearest is null ? TeleportBlock.NoAetheryte
            : !TeleportAvailable ? TeleportBlock.NoLifestream
            : LifestreamReason() is not null ? TeleportBlock.LifestreamSetup
            : target is null ? TeleportBlock.NotAttuned
            : tooFar ? TeleportBlock.TooFar
            : TeleportBusy ? TeleportBlock.Busy
            : TeleportBlock.None;
        return new TeleportCheck(block, target, special, here) { Skipped = skipped, Goal = goal };
    }

    /// <summary>Lifestream's own reason a loaded Lifestream cannot take the hand-off (a blocking setting); null when it can.</summary>
    private static string? LifestreamReason() => CompanionPlugins.DisabledReason(CompanionPlugin.Lifestream);

    /// <summary>vnavmesh's own reason a loaded vnavmesh cannot walk (a blocking setting, such as its navmesh auto-load off); null when it can.</summary>
    private static string? VnavmeshReason() => CompanionPlugins.DisabledReason(CompanionPlugin.Vnavmesh);

    /// <summary>
    /// Teleports toward the quest's giver through Lifestream: to the attuned aetheryte nearest the goal, or, for
    /// Island Sanctuary and the Occult Crescent, Lifestream's <c>/li island</c> / <c>/li occult</c> (an explicit click
    /// only; the tooltip says Lifestream talks to the NPC). When Lifestream refuses, one chat line says why. False when
    /// nothing was started.
    /// </summary>
    public bool TeleportToGiver(QuestRecord quest)
    {
        // A hand-off: Lifestream's setup is checked as it is now, not as last read.
        CompanionPlugins.ReadSetupNow();
        ForgetTravelFrame();
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
            case TeleportBlock.LifestreamSetup:
                return LifestreamReason() ?? NeedsLifestream();
            case TeleportBlock.NotAttuned:
                return string.Format(CultureInfo.CurrentCulture, Strings.TravelNotAttunedFormat, GoalZoneName(quest, check.Goal));
            case TeleportBlock.TooFar:
                return string.Format(CultureInfo.CurrentCulture, Strings.TravelTooFarFormat, check.Skipped?.Name ?? string.Empty, check.Target?.Name ?? string.Empty);
            case TeleportBlock.Busy:
                return BusyReason();
        }

        var lines = new List<string>(6);
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

            if (check.Skipped is { } skipped)
            {
                lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.TravelSubstitutedFormat, skipped.Name, target.Name));
            }

            if (check.Goal is { Entrance: { } door } goal)
            {
                var format = goal.Placed ? Strings.TravelInsideFormat : Strings.TravelInsideUnplacedFormat;
                lines.Add(string.Format(CultureInfo.CurrentCulture, format, GiverName(quest), TerritoryName(door.Interior), (check.Skipped ?? target).Name));
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
            lines.Add(check.Target is { } t && check.Goal is { } g && t.TerritoryId == g.Place.TerritoryId
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

    /// <summary>The goal's zone name: the outside zone of an interior's way in, else the giver's zone.</summary>
    private string GoalZoneName(QuestRecord quest, TravelGoal? goal) =>
        goal is { AtEntrance: true } g && TerritoryName(g.Place.TerritoryId) is { Length: > 0 } outside ? outside : ZoneName(quest);

    // ------------------------------------------------------------------ aethernet hop

    /// <summary>
    /// The aethernet hop toward a giver in a city (or an interior's door there), offered while the player is in that
    /// city's network: to the attuned shard nearest the goal when it beats walking from where the player stands by
    /// <see cref="TravelPlanner.HopMargin"/>, or to the Firmament from the Foundation. Lifestream needs the player at
    /// the city's aetheryte or a shard (its <c>GetActiveAetheryte</c>); elsewhere in the city the hop shows, disabled.
    /// </summary>
    public HopCheck CheckHop(QuestRecord quest)
    {
        if (Memo(quest).Hop is { } known)
        {
            return known;
        }

        var check = WorkOutHop(quest);
        Memo(quest).Hop = check;
        return check;
    }

    private HopCheck WorkOutHop(QuestRecord quest)
    {
        if (Travel is not { Position: { } at } travel || quest.Issuer is not { TerritoryId: > 0 } issuer || GoalFor(quest) is not { } goal)
        {
            return default;
        }

        var index = Aetherytes;
        var firmament = TravelSpecials.Classify(issuer.TerritoryId) == TravelSpecial.Firmament;
        if (!firmament && !goal.Placed)
        {
            return default;
        }

        var city = firmament
            ? index.Find(TravelSpecials.FoundationAetheryte)
            : GiverTravel.Arrival(index, issuer.TerritoryId, goal, static _ => true).Nearest is { } nearest ? index.Find(nearest.RowId) : null;
        if (city is not { Group: > 0 } || !InNetwork(travel.Territory, city.Group))
        {
            return default;
        }

        AetheryteInfo? shard = null;
        if (!firmament)
        {
            var here = new TravelNode(0, travel.Territory, at.X, at.Z, city.Group);
            if (TravelPlanner.ChooseShard(here, index.ShardNodesInGroup(city.Group), goal.Place.TerritoryId, goal.Place.X, goal.Place.Z, IsShardAttuned) is not { } best
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
        ForgetTravelFrame();
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
    /// cannot. It walks to the giver, or, outside an interior the giver stands in, to its way in. It waits while
    /// Questionable or AutoDuty drives the character, while the character casts, and while vnavmesh walks for another
    /// plugin (whose walk it never stops).
    /// </summary>
    public WalkCheck CheckWalk(QuestRecord quest)
    {
        if (Memo(quest).Walk is { } known)
        {
            return known;
        }

        var check = WorkOutWalk(quest);
        Memo(quest).Walk = check;
        return check;
    }

    private WalkCheck WorkOutWalk(QuestRecord quest)
    {
        if (Travel is not { } travel || !travel.Vnavmesh.Available)
        {
            return new WalkCheck(WalkBlock.NoVnavmesh, false, -1f);
        }

        if (travel.JourneyActive)
        {
            return new WalkCheck(WalkBlock.None, true, -1f);
        }

        var goal = GoalFor(quest);
        var block = goal is not { } g ? WalkBlock.NoGiverPlace
            : VnavmeshReason() is not null ? WalkBlock.VnavmeshSetup
            : travel.BetweenAreas ? WalkBlock.Loading
            : travel.Territory != g.Place.TerritoryId ? WalkBlock.NotInZone
            : !g.Placed ? WalkBlock.NoEntrance
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

    /// <summary>True when Walk to giver can start now: vnavmesh loaded and ready, the player idle in the goal's zone.</summary>
    public bool CanWalk(QuestRecord quest) => CheckWalk(quest).Ready;

    /// <summary>
    /// Walks the character to the quest's giver (or the way into the interior it stands in) through vnavmesh, which
    /// stops about 3 yalms away, mounting and flying as the settings say. Stop with <see cref="StopTravel"/>; leaving the
    /// zone, logging out or unloading stops it too. False when nothing was started.
    /// </summary>
    public bool WalkToGiver(QuestRecord quest)
    {
        // A hand-off: vnavmesh's setup is checked as it is now, not as last read.
        CompanionPlugins.ReadSetupNow();
        ForgetTravelFrame();
        if (ClickHeld || !CheckWalk(quest).Ready || Travel is not { } travel || GoalFor(quest) is not { } goal)
        {
            return false;
        }

        var plan = GoToGiverPlan.WalkOnly(goal.Place.TerritoryId, goal.Place.X, goal.Place.Y, goal.Place.Z) with
        {
            Options = travel.CurrentOptions,
            ToEntrance = goal.AtEntrance,
        };
        StartJourney(travel, plan, quest, goal);
        return travel.JourneyActive;
    }

    /// <summary>Starts a run with its status target and, for an interior's door, the note to go in.</summary>
    private void StartJourney(TravelService travel, GoToGiverPlan plan, QuestRecord quest, TravelGoal goal)
    {
        string target;
        string? note = null;
        if (goal.Entrance is { } door)
        {
            // At the door: go in. A door the data does not place: the trip ends at the aetheryte, and says so.
            var zone = TerritoryName(door.Interior);
            target = string.Format(CultureInfo.CurrentCulture, Strings.TravelTargetEntranceFormat, zone);
            note = plan.Walk ? string.Format(CultureInfo.CurrentCulture, Strings.TravelArrivedEntranceFormat, zone, GiverName(quest))
                : !goal.Placed ? string.Format(CultureInfo.CurrentCulture, Strings.TravelWalkNoEntranceFormat, GiverName(quest), zone)
                : null;
        }
        else
        {
            target = GiverName(quest);
        }

        travel.Start(plan, target, note);
        if (travel.JourneyActive)
        {
            MarkStarted();
        }
    }

    /// <summary>The Walk button's label: Stop while moving, "Preparing path… 40%" while vnavmesh builds, else <paramref name="walk"/>.</summary>
    public static string WalkLabel(WalkCheck check, string walk) =>
        check.Stoppable ? Strings.TravelStop
        : check.Block != WalkBlock.Preparing ? walk
        : check.Progress >= 0f ? string.Format(CultureInfo.CurrentCulture, Strings.TravelPreparingFormat, (int)MathF.Round(check.Progress * 100f))
        : Strings.TravelPreparing;

    /// <summary>The Walk button's tooltip: what it does (and how it moves), Stop, or why it cannot.</summary>
    public string WalkTooltip(QuestRecord quest, WalkCheck check)
    {
        if (check.Stoppable)
        {
            return StopTooltip();
        }

        switch (check.Block)
        {
            case WalkBlock.None:
                var lines = new List<string>(4);
                lines.Add(GoalFor(quest) is { Entrance: { } door }
                    ? string.Format(CultureInfo.CurrentCulture, Strings.TravelWalkEntranceTooltipFormat, TerritoryName(door.Interior), GiverName(quest))
                    : Strings.TravelWalkTooltip);
                AddMoveLines(lines);
                return string.Join('\n', lines);
            case WalkBlock.NotInZone:
                return string.Format(CultureInfo.CurrentCulture, Strings.TravelWalkNotInZoneFormat, GoalZoneName(quest, GoalFor(quest)));
            case WalkBlock.NoEntrance:
                return string.Format(CultureInfo.CurrentCulture, Strings.TravelWalkNoEntranceFormat, GiverName(quest), quest.Issuer is { } issuer ? TerritoryName(issuer.TerritoryId) : string.Empty);
        }

        return check.Block switch
        {
            WalkBlock.NoVnavmesh => NeedsVnavmesh(),
            WalkBlock.VnavmeshSetup => VnavmeshReason() ?? NeedsVnavmesh(),
            WalkBlock.NoGiverPlace => Strings.TravelNoGiverPlace,
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

    /// <summary>The lines saying how a walk moves (mount, fly, sprint) from the settings, none when it stays on foot.</summary>
    private void AddMoveLines(List<string> lines)
    {
        if (Travel?.CurrentOptions is not { } options)
        {
            return;
        }

        if (options.MountDistance > 0f)
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.TravelMoveMountFormat, (int)options.MountDistance));
            if (options.Fly)
            {
                lines.Add(Strings.TravelMoveFly);
            }
        }

        if (options.SprintInTowns)
        {
            lines.Add(Strings.TravelMoveSprint);
        }
    }

    // ------------------------------------------------------------------ go to giver

    /// <summary>
    /// What Go to giver would do now: walk when the player is already closer than any teleport would bring them
    /// (after a hop when standing at the city's aetheryte); else teleport to the attuned aetheryte nearest the goal,
    /// take the aethernet to the shard nearest the goal (or to the Firmament), and walk when the trip ends in the goal's
    /// zone and the goal has a place. The goal is the giver, or the way into the interior the giver stands in.
    /// </summary>
    public GoToCheck CheckGoTo(QuestRecord quest)
    {
        if (Memo(quest).GoTo is { } known)
        {
            return known;
        }

        var check = WorkOutGoTo(quest);
        Memo(quest).GoTo = check;
        return check;
    }

    private GoToCheck WorkOutGoTo(QuestRecord quest)
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
        var goal = teleport.Goal ?? GiverTravel.Goal(issuer, Entrances, travel.Territory);

        // A conversation zone is never entered by the chain; once inside it, the walk is fine.
        var conversation = (TravelSpecials.NeedsConversation(teleport.Special) || teleport.Special == TravelSpecial.CosmicExploration)
            && travel.Territory != issuer.TerritoryId;
        var automation = AutomationBlock();
        var block = conversation ? GoToBlock.Conversation
            : VnavmeshReason() is not null ? GoToBlock.VnavmeshSetup
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

        // Standing at the city's aetheryte (or already in the goal's zone and closer than any aetheryte there).
        var hop = CheckHop(quest);
        TravelLeg? hopLeg = hop.Ready && LifestreamReason() is null ? HopLeg(hop) : null;
        if (teleport.AlreadyHere || hopLeg is not null)
        {
            var end = hopLeg?.TerritoryId ?? travel.Territory;
            return new GoToCheck(GoToBlock.None, Plan(goal, null, hopLeg, end, travel.CurrentOptions), false);
        }

        if (!teleport.Ready || teleport.Target is not { } target)
        {
            // In the goal's zone already, no teleport is needed to get going: walk from here.
            if (travel.Territory == goal.Place.TerritoryId && goal.Placed
                && teleport.Block is TeleportBlock.NoLifestream or TeleportBlock.NotAttuned or TeleportBlock.NoAetheryte or TeleportBlock.LifestreamSetup or TeleportBlock.TooFar)
            {
                return new GoToCheck(GoToBlock.None, Plan(goal, null, null, travel.Territory, travel.CurrentOptions), false);
            }

            var reason = teleport.Block switch
            {
                TeleportBlock.NoGiverPlace => GoToBlock.NoGiverPlace,
                TeleportBlock.NoAetheryte => GoToBlock.NoAetheryte,
                TeleportBlock.NoLifestream => GoToBlock.NoLifestream,
                TeleportBlock.LifestreamSetup => GoToBlock.LifestreamSetup,
                TeleportBlock.NotAttuned => GoToBlock.NotAttuned,
                TeleportBlock.TooFar => GoToBlock.TooFar,
                _ => GoToBlock.Busy,
            };
            return new GoToCheck(reason, null, false);
        }

        TravelLeg? arrivalHop = null;
        if (teleport.Special == TravelSpecial.Firmament)
        {
            arrivalHop = new TravelLeg(GoToGiverPlan.FirmamentHop, TravelSpecials.FirmamentTerritory);
        }
        else if (goal.Placed
            && TravelPlanner.ChooseShard(target.Node, Aetherytes.ShardNodesInGroup(target.Group), goal.Place.TerritoryId, goal.Place.X, goal.Place.Z, IsShardAttuned) is { } shard)
        {
            arrivalHop = new TravelLeg(shard.RowId, shard.TerritoryId);
        }

        var leg = new TravelLeg(target.RowId, target.TerritoryId);
        return new GoToCheck(GoToBlock.None, Plan(goal, leg, arrivalHop, arrivalHop?.TerritoryId ?? target.TerritoryId, travel.CurrentOptions), false);
    }

    private static TravelLeg? HopLeg(HopCheck hop) =>
        hop.Firmament ? new TravelLeg(GoToGiverPlan.FirmamentHop, TravelSpecials.FirmamentTerritory)
        : hop.Shard is { } shard ? new TravelLeg(shard.RowId, shard.TerritoryId)
        : null;

    /// <summary>
    /// The plan for the legs; it walks only when the trip ends in the goal's zone (vnavmesh does not cross zone lines)
    /// and the goal has a place (an interior's door the data does not place has none).
    /// </summary>
    private static GoToGiverPlan Plan(TravelGoal goal, TravelLeg? teleport, TravelLeg? hop, uint endTerritory, TravelOptions options) =>
        new(goal.Place.TerritoryId, goal.Place.X, goal.Place.Y, goal.Place.Z, teleport, hop, endTerritory == goal.Place.TerritoryId && goal.Placed)
        {
            Options = options,
            ToEntrance = goal.AtEntrance,
        };

    /// <summary>True when Go to giver can start now.</summary>
    public bool CanGoToGiver(QuestRecord quest) => CheckGoTo(quest).Ready;

    /// <summary>
    /// Starts the Go to giver chain: teleport (Lifestream), wait for the arrival, an aethernet hop, then the walk
    /// (vnavmesh), mounting and flying as the settings say, to the giver or the way into its interior. Every step can be
    /// stopped with <see cref="StopTravel"/> and times out with a chat line. False when nothing was started.
    /// </summary>
    public bool GoToGiver(QuestRecord quest)
    {
        // A hand-off: Lifestream's and vnavmesh's setup are checked as they are now, not as last read.
        CompanionPlugins.ReadSetupNow();
        ForgetTravelFrame();
        if (ClickHeld || CheckGoTo(quest) is not { Ready: true, Plan: { } plan } || Travel is not { } travel || GoalFor(quest) is not { } goal)
        {
            return false;
        }

        StartJourney(travel, plan, quest, goal);
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
            case GoToBlock.VnavmeshSetup:
                return VnavmeshReason() ?? NeedsVnavmesh();
            case GoToBlock.LifestreamSetup:
                return LifestreamReason() ?? NeedsLifestream();
            case GoToBlock.NoGiverPlace:
                return Strings.TravelNoGiverPlace;
            case GoToBlock.NoLifestream:
                return NeedsLifestream();
            case GoToBlock.NoAetheryte:
                return Strings.TeleportNoAetheryte;
            case GoToBlock.NotAttuned:
                return string.Format(CultureInfo.CurrentCulture, Strings.TravelNotAttunedFormat, GoalZoneName(quest, GoalFor(quest)));
            case GoToBlock.TooFar:
                var far = CheckTeleport(quest);
                return string.Format(CultureInfo.CurrentCulture, Strings.TravelTooFarFormat, far.Skipped?.Name ?? string.Empty, far.Target?.Name ?? string.Empty);
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

        var lines = new List<string>(8) { Strings.TravelGoToTooltip };
        if (check.Plan is not { } plan)
        {
            return lines[0];
        }

        if (plan.Teleport is { } teleport)
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.TravelGoToStepTeleportFormat, Aetherytes.Find(teleport.Id)?.Name ?? string.Empty));
            if (CheckTeleport(quest) is { Skipped: { } skipped, Target: { } target })
            {
                lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.TravelSubstitutedFormat, skipped.Name, target.Name));
            }
        }

        if (plan.Hop is { } hop)
        {
            var name = hop.Id == GoToGiverPlan.FirmamentHop ? Strings.TravelFirmament : Aetherytes.Find(hop.Id)?.Name ?? string.Empty;
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.TravelGoToStepHopFormat, name));
        }

        var goal = GoalFor(quest);
        var interior = goal is { Entrance: { } door } ? TerritoryName(door.Interior) : null;
        if (plan.Walk)
        {
            lines.Add(interior is not null ? string.Format(CultureInfo.CurrentCulture, Strings.TravelGoToStepEntranceFormat, interior) : Strings.TravelGoToStepWalk);
            AddMoveLines(lines);
        }
        else if (interior is not null && goal is { Placed: false })
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.TravelGoToNoEntranceFormat, interior));
        }
        else
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.TravelGoToNoWalkFormat, interior ?? ZoneName(quest)));
        }

        return string.Join('\n', lines);
    }
}
