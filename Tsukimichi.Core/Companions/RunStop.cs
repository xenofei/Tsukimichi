using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Travel;

namespace Tsukimichi.Core.Companions;

/// <summary>Why a hand-off Tsukimichi started stopped: the eight reasons of the "Why it stopped" card (plan v7, 1.18.0, A2).</summary>
public enum StopReason
{
    /// <summary>The run did what it was asked and stopped (one quest done, a stop condition met).</summary>
    Finished,

    /// <summary>The player stopped it from Tsukimichi (a Stop button, <c>/tsuki stop</c>).</summary>
    Player,

    /// <summary>The duty guard (A3) stopped Questionable before a duty with other players.</summary>
    DutyGuard,

    /// <summary>The character was knocked out, and Tsukimichi stopped its own hand-offs.</summary>
    KnockedOut,

    /// <summary>Travel made no progress and its recovery could not fix it.</summary>
    Stuck,

    /// <summary>Travel found no path to the giver (or never started walking).</summary>
    NoPath,

    /// <summary>The plugin a hand-off needs is not loaded (installed but off, or not installed).</summary>
    MissingPlugin,

    /// <summary>The hand-off stopped by itself, or with an error, before its work was done.</summary>
    Error,
}

/// <summary>Which hand-off a card is about; it picks the card's game icon.</summary>
public enum StopHandOff
{
    Questionable,
    AutoDuty,
    Travel,
    Artisan,
}

/// <summary>
/// The card's one colour cue (spec-1.18, "Colour language"): gold finished well, silver you did this, copper it needs
/// you. Always a bar or a dot beside words that say the same; never the only carrier.
/// </summary>
public enum StopCue
{
    Gold,
    Silver,
    Copper,
}

/// <summary>
/// A card's safe fixes (spec-1.18 A2, "What counts as safe"): each starts the same kind of hand-off again or opens a
/// Tsukimichi or game view. None answers the game for the player, and none chains a second run.
/// </summary>
public enum StopFix
{
    None,

    /// <summary>Finished: start Questionable on the next quest, one quest (A6's default).</summary>
    StartNext,

    /// <summary>You stopped it: start Questionable again on the same quest, resuming at its step.</summary>
    StartAgain,

    /// <summary>Duty guard: open the Duty Finder on the duty.</summary>
    ShowDuty,

    /// <summary>Duty guard: restart Questionable once, after the player has cleared the duty themselves.</summary>
    KeepGoingAfterDuty,

    /// <summary>Knocked out: start the same hand-off again, enabled once the character is up and out of combat.</summary>
    TryAgain,

    /// <summary>Stuck or no path: reload vnavmesh's navmesh, then walk the same plan again.</summary>
    ReloadAndRetry,

    /// <summary>Stuck: open the map with a flag where the character stands.</summary>
    FlagSpot,

    /// <summary>No path: teleport to the aetheryte nearest the giver (Lifestream).</summary>
    TeleportCloser,

    /// <summary>Missing plugin: open Settings › Companions › Setup.</summary>
    OpenSetup,

    /// <summary>Error: open the quest in the game's journal, where its objective and map are.</summary>
    OpenJournal,
}

/// <summary>
/// The rules of the "Why it stopped" card (plan v7, 1.18.0, A2; docs/design/v7/ui/spec-1.18.md): each reason's cue, its
/// fixes, whether it needs the player (it then stays until dismissed) and how a run's end maps to a reason. Pure.
/// </summary>
public static class RunStopClassifier
{
    /// <summary>
    /// The resource key of the reason's title ("StopCardTitleStuck"). Every reason has one with words in it: the card's
    /// copper bar is never the only carrier (spec-1.18, the supervisor's ruling), and a test holds the resource file to it.
    /// </summary>
    public static string TitleKey(StopReason reason) => "StopCardTitle" + reason;

    /// <summary>The reasons, in the spec's order.</summary>
    public static readonly StopReason[] All =
    [
        StopReason.Finished, StopReason.Player, StopReason.DutyGuard, StopReason.KnockedOut,
        StopReason.Stuck, StopReason.NoPath, StopReason.MissingPlugin, StopReason.Error,
    ];

    /// <summary>
    /// How long after a Questionable run's end a knock-out or a stall still explains it: Questionable stops a few
    /// seconds after the character falls (or after Tsukimichi stops it for the fall or the stall).
    /// </summary>
    public const double TroubleExplainsSeconds = 20.0;

    /// <summary>The reason's colour cue.</summary>
    public static StopCue Cue(StopReason reason) => reason switch
    {
        StopReason.Finished => StopCue.Gold,
        StopReason.Player => StopCue.Silver,
        _ => StopCue.Copper,
    };

    /// <summary>Whether the card needs the player: it then stays until dismissed; the others fade after <see cref="StopDock.FadeAfterSeconds"/>.</summary>
    public static bool NeedsYou(StopReason reason) => Cue(reason) == StopCue.Copper;

    /// <summary>
    /// The card's fixes, at most two, the first the primary (unless <see cref="ReportIsPrimary"/>). Every one is safe by
    /// construction (<see cref="StopFix"/>); a card with none still has Copy report.
    /// </summary>
    public static (StopFix First, StopFix Second) Fixes(StopReason reason) => reason switch
    {
        StopReason.Finished => (StopFix.StartNext, StopFix.None),
        StopReason.Player => (StopFix.StartAgain, StopFix.None),
        StopReason.DutyGuard => (StopFix.ShowDuty, StopFix.KeepGoingAfterDuty),
        StopReason.KnockedOut => (StopFix.TryAgain, StopFix.None),
        StopReason.Stuck => (StopFix.ReloadAndRetry, StopFix.FlagSpot),
        StopReason.NoPath => (StopFix.ReloadAndRetry, StopFix.TeleportCloser),
        StopReason.MissingPlugin => (StopFix.OpenSetup, StopFix.None),
        _ => (StopFix.OpenJournal, StopFix.None),
    };

    /// <summary>On the error card Copy report is the primary action: the fix the player needs is the author's.</summary>
    public static bool ReportIsPrimary(StopReason reason) => reason == StopReason.Error;

    /// <summary>
    /// The reason for a Questionable run's end (its receipt, A4): done as asked is Finished, Tsukimichi's Stop is the
    /// player's, the duty guard's stop is the guard's. A run that ended before its quest was done, or on its own, takes
    /// <paramref name="recentTrouble"/> (a knock-out or a stall within <see cref="TroubleExplainsSeconds"/> of it, which
    /// Tsukimichi saw), else it is an error.
    /// </summary>
    public static StopReason FromReceipt(QuestionableRunEnd end, StopReason? recentTrouble) => end switch
    {
        QuestionableRunEnd.SingleQuestDone or QuestionableRunEnd.AfterQuests or QuestionableRunEnd.AtTime or QuestionableRunEnd.AfterCurrent => StopReason.Finished,
        QuestionableRunEnd.StoppedFromTsukimichi => StopReason.Player,
        QuestionableRunEnd.BeforeDutyWithPlayers => StopReason.DutyGuard,
        _ => recentTrouble ?? StopReason.Error,
    };

    /// <summary>
    /// Whether a run's end gets a card: one Tsukimichi started, or one a stop condition the player set ended. A run
    /// started in Questionable's own window and left alone ends without one, as it ends without a chat line.
    /// </summary>
    public static bool CardFor(QuestionableRunEnd end, QuestionableRunOrigin origin) =>
        origin != QuestionableRunOrigin.Elsewhere || end is QuestionableRunEnd.AfterQuests or QuestionableRunEnd.AtTime or QuestionableRunEnd.AfterCurrent;

    /// <summary>
    /// The reason for a walk that travel recovery (A8) gave up on: no progress is Stuck; a walk that ended short, never
    /// started or had no navmesh is No path; anything else an error.
    /// </summary>
    public static StopReason FromTravel(GoToGiverFailure failure) => failure switch
    {
        GoToGiverFailure.Stuck => StopReason.Stuck,
        GoToGiverFailure.WalkStoppedShort or GoToGiverFailure.PathNotReady or GoToGiverFailure.WalkDidNotStart or GoToGiverFailure.WalkRefused or GoToGiverFailure.WalkTimedOut => StopReason.NoPath,
        _ => StopReason.Error,
    };

    /// <summary>
    /// The first quest that <paramref name="finishedRowId"/> leads to and the character can take now
    /// (<paramref name="ready"/>), in catalog order; 0 when none. Reads the catalog's prerequisites, so it runs once
    /// per card, not per frame.
    /// </summary>
    public static uint NextQuest(Model.QuestCatalog catalog, uint finishedRowId, Func<uint, bool> ready)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(ready);
        if (finishedRowId == 0)
        {
            return 0;
        }

        foreach (var quest in catalog.All)
        {
            if (quest.IsRetired || quest.RowId == finishedRowId || Array.IndexOf(catalog.PrerequisitesOf(quest).QuestIds, finishedRowId) < 0)
            {
                continue;
            }

            if (ready(quest.RowId))
            {
                return quest.RowId;
            }
        }

        return 0;
    }
}

/// <summary>
/// One "Why it stopped" card: the reason, the hand-off and the words, composed once by the plugin when it is raised
/// (the card only draws them). <see cref="Key"/> says when a second raise is the same stop (the duty guard's own
/// card and the run receipt that follows it), so it refreshes the card instead of replacing it.
/// </summary>
public sealed record StopCard(StopReason Reason, StopHandOff HandOff)
{
    /// <summary>The card's title in plain words ("Travel got stuck"). Never empty: the cue is never the only carrier.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>One or two sentences: why, in plain words.</summary>
    public string Why { get; init; } = string.Empty;

    /// <summary>Quest · step · zone, or what was running; empty for none.</summary>
    public string Context { get; init; } = string.Empty;

    /// <summary>The run receipt's span line on a Finished card ("1 h 12 min · 3 quests done"); empty for none.</summary>
    public string Receipt { get; init; } = string.Empty;

    /// <summary>The quest the run worked on; 0 when none.</summary>
    public uint QuestRowId { get; init; }

    /// <summary>The quest a Finished card's Start the next one starts; 0 hides the fix.</summary>
    public uint NextRowId { get; init; }

    /// <summary>A duty guard card's duty (ContentFinderCondition row; 0 when unknown).</summary>
    public uint DutyId { get; init; }

    /// <summary>A duty guard card's duty (InstanceContent row, for "cleared"; 0 when unknown).</summary>
    public uint InstanceContentId { get; init; }

    /// <summary>The duty's name as the card says it; empty when unknown or hidden by the spoiler shield.</summary>
    public string DutyName { get; init; } = string.Empty;

    /// <summary>The territory the hand-off stopped in, for the report and Flag the spot; 0 when unknown.</summary>
    public uint TerritoryId { get; init; }

    /// <summary>Where the character stood (world units), for Flag the spot and the report; null when unknown.</summary>
    public System.Numerics.Vector3? Position { get; init; }

    /// <summary>The report's short stop line ("error · stopped on its own"), in English like the rest of the report.</summary>
    public string ReportReason { get; init; } = string.Empty;

    /// <summary>Questionable's sequence when it stopped, for the report and the per-step count; null when unknown.</summary>
    public byte? Sequence { get; init; }

    /// <summary>Questionable's step within the sequence (0-based); null when unknown.</summary>
    public int? Step { get; init; }

    /// <summary>Whether a single-quest run (A6) stopped; the restart keeps the same mode.</summary>
    public bool SingleQuest { get; init; } = true;

    /// <summary>The zone's name at the moment it stopped, for the report; empty when unknown.</summary>
    public string Zone { get; init; } = string.Empty;

    /// <summary>The character's job abbreviation at the moment it stopped ("WHM"), for the report; empty when unknown.</summary>
    public string Job { get; init; } = string.Empty;

    /// <summary>The character's level at the moment it stopped, for the report; 0 when unknown.</summary>
    public int Level { get; init; }

    /// <summary>The two values that make two raises the same stop.</summary>
    public (StopReason Reason, StopHandOff HandOff, uint Quest) Key => (Reason, HandOff, QuestRowId);

    /// <summary>The card's cue.</summary>
    public StopCue Cue => RunStopClassifier.Cue(Reason);

    /// <summary>Whether the card stays until dismissed.</summary>
    public bool NeedsYou => RunStopClassifier.NeedsYou(Reason);

    /// <summary>
    /// The card with the character as it is at the moment it stopped, so Copy report later says where and as what it
    /// stopped, not where the player stands when they copy it. Fills only what the card lacks: the territory when it has
    /// none (<paramref name="territoryId"/>), the zone's name for the card's own territory, the job and the level.
    /// </summary>
    /// <param name="territoryId">The territory the character is in now; 0 when unknown.</param>
    /// <param name="zoneName">A territory's zone name (empty when unknown).</param>
    /// <param name="job">The job abbreviation now ("WHM"); empty when unknown.</param>
    /// <param name="level">The level now; 0 when unknown.</param>
    public StopCard WithCharacter(uint territoryId, Func<uint, string> zoneName, string job, int level)
    {
        ArgumentNullException.ThrowIfNull(zoneName);
        var territory = TerritoryId != 0 ? TerritoryId : territoryId;
        return this with
        {
            TerritoryId = territory,
            Zone = Zone.Length > 0 ? Zone : territory != 0 ? zoneName(territory) ?? string.Empty : string.Empty,
            Job = Job.Length > 0 ? Job : job ?? string.Empty,
            Level = Level > 0 ? Level : level,
        };
    }
}

/// <summary>
/// Where the card is (spec-1.18 A2, "How long it stays"): one card at a time, in the notice dock and the Todo overlay.
/// A new stop replaces the card, except that a calm card (Finished, You stopped it) never pushes out a card that needs
/// the player; the same stop raised twice refreshes it. Cards that need the player stay until dismissed or until another
/// hand-off starts; the calm ones fade after <see cref="FadeAfterSeconds"/> on screen. The clock counts only while a
/// surface shows the card, capped per step like <see cref="Ui.NoticeQueue"/>'s, and stands still under the pointer. A
/// card is folded to the status bar when the player selects another quest, and unfolded on request. It fades in over
/// <see cref="Ui.MotionTokens.Rise"/> and out over <see cref="LeaveSeconds"/>; under Reduce motion both are at once.
/// Fed with a steady clock in seconds. Pure, so the rules are tested.
/// </summary>
public sealed class StopDock
{
    /// <summary>How long a calm card stays on screen.</summary>
    public const double FadeAfterSeconds = 30.0;

    /// <summary>How long a card takes to fade out (spec: <c>Leave</c>, 0.12 s).</summary>
    public const double LeaveSeconds = 0.12;

    /// <summary>The most one tick takes off a card's clock.</summary>
    public const double MaxStepSeconds = 0.1;

    private double raisedAt;
    private double remaining;
    private double lastTick = double.NaN;
    private double leavingAt = double.NaN;
    private uint? selectionAtRaise;
    private bool selectionKnown;

    /// <summary>The card, or null when none shows; a leaving card stays here until it has faded.</summary>
    public StopCard? Current { get; private set; }

    /// <summary>Moves whenever the card is raised, refreshed or goes, so a surface knows to lay it out again.</summary>
    public int Version { get; private set; }

    /// <summary>Whether the card is folded to the status bar (another quest is selected).</summary>
    public bool Folded { get; private set; }

    /// <summary>Whether the card is fading out.</summary>
    public bool Leaving => !double.IsNaN(leavingAt);

    /// <summary>When the card was raised, on the dock's clock ("2 min ago").</summary>
    public double RaisedAt => raisedAt;

    /// <summary>Seconds left before a calm card fades; infinite for one that needs the player, 0 with none.</summary>
    public double Remaining => Current is null ? 0.0 : Current.NeedsYou ? double.PositiveInfinity : remaining;

    /// <summary>
    /// Shows <paramref name="card"/> at <paramref name="now"/>. Returns false when it was not shown: a calm card while
    /// a card that needs the player is up. A card without a title is refused outright (it throws): its cue would be
    /// the only carrier of its meaning.
    /// </summary>
    public bool Raise(StopCard card, double now)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (string.IsNullOrWhiteSpace(card.Title))
        {
            throw new ArgumentException("A stop card needs a title in words beside its colour cue.", nameof(card));
        }
        if (Current is { } shown && !Leaving)
        {
            if (shown.Key == card.Key)
            {
                // The same stop again (the guard's own card, then the receipt): the newer words, the same place in time.
                Current = Merge(shown, card);
                Version++;
                return true;
            }

            if (shown.NeedsYou && !card.NeedsYou)
            {
                return false;
            }
        }

        Current = card;
        raisedAt = now;
        remaining = FadeAfterSeconds;
        leavingAt = double.NaN;
        Folded = false;
        selectionKnown = false;
        Version++;
        return true;
    }

    /// <summary>The player's ×: the card fades out.</summary>
    public void Dismiss(double now)
    {
        if (Current is not null && !Leaving)
        {
            leavingAt = now;
        }
    }

    /// <summary>Another hand-off started: the card is about the last run, so it goes.</summary>
    public void HandOffStarted(double now) => Dismiss(now);

    /// <summary>
    /// The quest selected in the main window this frame (null with none). The first call after a raise remembers it;
    /// selecting another quest afterwards folds the card to the status bar.
    /// </summary>
    public void NoteSelection(uint? selected)
    {
        if (Current is null)
        {
            return;
        }

        if (!selectionKnown)
        {
            selectionKnown = true;
            selectionAtRaise = selected;
            return;
        }

        if (selected != selectionAtRaise && selected is not null && selected != Current.QuestRowId)
        {
            Folded = true;
            selectionAtRaise = selected;
        }
    }

    /// <summary>Opens a folded card again (its status-bar note was clicked).</summary>
    public void Unfold() => Folded = false;

    /// <summary>
    /// Advances the clock: a calm card that a surface showed (<paramref name="shown"/>) uses the time unless
    /// <paramref name="paused"/>, and starts leaving when it runs out; a leaving card goes once faded. Returns whether
    /// a card remains.
    /// </summary>
    public bool Tick(double now, bool shown, bool paused, bool reduceMotion)
    {
        var step = double.IsFinite(lastTick) ? Math.Min(now - lastTick, MaxStepSeconds) : 0.0;
        lastTick = now;
        if (Current is not { } card)
        {
            return false;
        }

        if (Leaving)
        {
            if (reduceMotion || now - leavingAt >= LeaveSeconds)
            {
                Current = null;
                leavingAt = double.NaN;
                Folded = false;
                Version++;
            }

            return Current is not null;
        }

        if (!card.NeedsYou && shown && !paused && step > 0.0)
        {
            remaining -= step;
            if (remaining <= 0.0)
            {
                leavingAt = now;
            }
        }

        return true;
    }

    /// <summary>The card's opacity at <paramref name="now"/>: rising in over Rise, fading out over Leave; 1 under Reduce motion until gone.</summary>
    public float Alpha(double now, bool reduceMotion)
    {
        if (Current is null)
        {
            return 0f;
        }

        if (Leaving)
        {
            return reduceMotion ? 0f : (float)Math.Clamp(1.0 - ((now - leavingAt) / LeaveSeconds), 0.0, 1.0);
        }

        return reduceMotion ? 1f : (float)Math.Clamp((now - raisedAt) / Ui.MotionTokens.Rise, 0.0, 1.0);
    }

    /// <summary>Whether the card's buttons act at <paramref name="now"/>: fully in and not leaving.</summary>
    public bool Interactive(double now, bool reduceMotion) => Current is not null && !Leaving && Alpha(now, reduceMotion) >= 1f;

    /// <summary>
    /// The newer card's words over the older card's, keeping what only the older one knew (the duty) and the character as
    /// it was when it first stopped (the job and level); the zone follows the newer card's territory.
    /// </summary>
    private static StopCard Merge(StopCard older, StopCard newer) => newer with
    {
        Why = older.DutyId != 0 && older.Why.Length > 0 ? older.Why : newer.Why.Length > 0 ? newer.Why : older.Why,
        Context = newer.Context.Length > 0 ? newer.Context : older.Context,
        Receipt = newer.Receipt.Length > 0 ? newer.Receipt : older.Receipt,
        DutyId = older.DutyId != 0 ? older.DutyId : newer.DutyId,
        InstanceContentId = older.InstanceContentId != 0 ? older.InstanceContentId : newer.InstanceContentId,
        DutyName = older.DutyName.Length > 0 ? older.DutyName : newer.DutyName,
        TerritoryId = newer.TerritoryId != 0 ? newer.TerritoryId : older.TerritoryId,
        Position = newer.Position ?? older.Position,
        Sequence = newer.Sequence ?? older.Sequence,
        Step = newer.Step ?? older.Step,
        Zone = newer.Zone.Length > 0 ? newer.Zone : older.Zone,
        Job = older.Job.Length > 0 ? older.Job : newer.Job,
        Level = older.Level > 0 ? older.Level : newer.Level,
    };
}

/// <summary>
/// The automatic starts the "Why it stopped" card's fixes leave waiting (plan v7, 1.18.0, A2): "Keep going after it"
/// (<see cref="WaitingFor"/>, the card whose duty it waits for) restarts Questionable once the player has cleared the
/// duty, and "Reload navmesh and retry" (<see cref="Retry"/>, what to start) starts the same way again once vnavmesh has
/// rebuilt the zone. One Stop (<c>/tsuki stop</c>, Stop all) cancels both (<see cref="CancelAll"/>), so nothing starts on
/// its own after the player said stop. Pure.
/// </summary>
/// <typeparam name="TRetry">What a retry starts (a travel plan, or Questionable on a quest).</typeparam>
public sealed class PendingStarts<TRetry>
    where TRetry : class
{
    /// <summary>The card "Keep going after it" waits on; null when it does not wait.</summary>
    public StopCard? WaitingFor { get; set; }

    /// <summary>What "Reload navmesh and retry" starts once vnavmesh is ready; null when nothing waits.</summary>
    public TRetry? Retry { get; set; }

    /// <summary>Whether an automatic start waits.</summary>
    public bool Any => WaitingFor is not null || Retry is not null;

    /// <summary>Cancels both; true when one was waiting.</summary>
    public bool CancelAll()
    {
        var any = Any;
        WaitingFor = null;
        Retry = null;
        return any;
    }
}
