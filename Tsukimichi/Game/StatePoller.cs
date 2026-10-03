using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>
/// Once per <see cref="Configuration.PollInterval"/> while a character is ready and the catalog is built: capture,
/// diff, re-resolve what changed, emit events, publish to <see cref="SessionState"/> and persist (debounced).
/// Exceptions from game reads, and a first pass that faults on its worker, back the interval off exponentially to
/// <see cref="MaxBackoff"/> with a single warning (<see cref="PollSchedule"/>); exceptions from session listeners are
/// logged (rate-limited) and never affect the backoff.
/// <para>
/// A capture of the live character that suddenly loses many completed quests or empties its journal
/// (<see cref="CapturePlausibility"/>) is not committed, so it is never saved over the good snapshot: the poller
/// backs off as for a failed read, logs the counts once, and prints one chat line per load
/// (<see cref="PrintNotice"/>). The first capture of a session is judged against the stored snapshot the same way.
/// The next plausible capture commits as usual. A loss that keeps reading the same for a few minutes is real
/// (<see cref="HeldBackCaptures"/>): the saved file is copied to the backup first, then the capture is committed, with
/// a log line and a chat line.
/// </para>
/// <para>
/// The first pass for a character resolves the whole catalog, which took a visible slice of a frame; the capture
/// still happens here (ClientStructs reads stay on the framework thread), but the catalog-wide resolve and the
/// sidecar read run on a worker over an immutable snapshot, and the result is committed and published on the
/// framework thread when it lands. Later polls diff incrementally, except when a change touches quests the reverse
/// index cannot enumerate (a job or level change, a duty clear, an allowance, today's offer, a new mount: <see cref="FullPass"/>):
/// that capture's full resolve and its events run on a worker the same way, and are committed on the frame they land
/// unless the catalog, the character or the committed capture moved meanwhile, in which case the next poll captures
/// and diffs again. Until a pass lands the session keeps the previous evaluations and no other capture is taken, so
/// events stay in order. What the poller keeps between polls lives in <see cref="PollerMemory"/>. Runs entirely
/// inside <see cref="IFramework.Update"/> (subscribed by <see cref="Start"/>) apart from those workers.
/// </para>
/// </summary>
public sealed class StatePoller : IDisposable
{
    public static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    /// <summary>At most one warning per this interval when a session listener keeps throwing.</summary>
    private static readonly TimeSpan ListenerWarningInterval = TimeSpan.FromMinutes(1);

    private const uint QuestRowBase = 0x10000;

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IPluginLog log;
    private readonly GameStateReader reader;
    private readonly SnapshotService snapshots;
    private readonly SessionState session;
    private readonly Configuration config;
    private readonly SerialWriter writer;

    private readonly PollerMemory memory = new(SaveInterval);
    private readonly PollSchedule schedule = new(TimeSpan.FromSeconds(2), MaxBackoff);
    private readonly LoginReadiness readiness = new();
    private readonly HeldBackCaptures heldBack = new();

    /// <summary>The first pass in flight on a worker; null while none is. Touched only on the framework thread.</summary>
    private FirstPass? pendingFirst;

    /// <summary>A full pass (<see cref="FullPass"/>) in flight on a worker; null while none is. Framework thread only.</summary>
    private PendingFullPass? pendingFull;

    /// <summary>Set by <see cref="Start"/>: the poller is subscribed to the framework's updates.</summary>
    private bool started;

    /// <summary>
    /// The newest committed capture of a character whose memory was just reset while its state on disk may lag behind
    /// it: the catalog was rebuilt (the capture is the live state), or its save was still on the writer at a logout.
    /// The next first pass for that character continues its completion dates from it rather than from the file, and
    /// judges plausibility against it. Cleared once that save lands, or a first pass commits.
    /// </summary>
    private CharacterSnapshot? handoff;

    private bool firstCaptureLogged;
    private bool acceptedSinceWarned;
    private bool abandonedWarned;
    private DateTime lastListenerWarningUtc = DateTime.MinValue;
    private bool wasReady;
    private bool saveWarned;
    private bool implausibleNoticed;
    private bool disposed;

    /// <summary>
    /// Prints one line in chat (the plugin points it at the chat log with Tsukimichi's tag); used once per load, when a
    /// capture is first held back as implausible. Null prints nothing.
    /// </summary>
    public Action<string>? PrintNotice { get; set; }

    /// <summary>Flushes on the writer that have not landed yet; a periodic flush waits for them.</summary>
    private int flushesInFlight;

    /// <summary>The capture the newest flush in flight writes; a final flush does not queue the same one again.</summary>
    private CharacterSnapshot? queuedSnapshot;

    // The daily offer the committed evaluations were resolved with; a different one re-resolves everything.
    private DailyOffer? lastOffer;

    /// <param name="writer">The background queue every snapshot and sidecar save goes through; the framework thread never writes a file.</param>
    public StatePoller(
        IFramework framework,
        IClientState clientState,
        IPluginLog log,
        GameStateReader reader,
        SnapshotService snapshots,
        SessionState session,
        Configuration config,
        SerialWriter writer)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.config = config ?? throw new ArgumentNullException(nameof(config));

        snapshots.LoggingOut += OnLoggingOut;
        session.CharacterForgotten += OnCharacterForgotten;
        session.DataDeleted += OnDataDeleted;
    }

    /// <summary>
    /// Starts polling. The plugin calls it last in its constructor, once every surface the session wakes and every
    /// gate the game reads obey is in place: an update that ran earlier could read the game before the hook gate was
    /// set, or publish to a session half of whose listeners had not joined yet.
    /// </summary>
    public void Start()
    {
        if (disposed || started)
        {
            return;
        }

        started = true;
        framework.Update += OnUpdate;
    }

    /// <summary>
    /// Today's allied society offer for the live character (decision 5); null leaves every daily's offer unknown,
    /// which holds none back. Set once by the plugin.
    /// </summary>
    public DailyOfferReader? DailyOffers { get; set; }

    /// <summary>
    /// Hands the last capture (if it changed since the last save) and the accepted-time and abandoned sidecars (if
    /// they changed) to the background writer: the framework thread never waits on the disk or on another client
    /// reading the file. The outcome lands in <see cref="FlushLanded"/> on the framework thread. A failure is warned
    /// once, retried no sooner than <see cref="SaveInterval"/> later, and the recovery is logged.
    /// </summary>
    /// <param name="final">
    /// Logout, unload or another character: queued even while an earlier flush is on the writer (the writer keeps
    /// them in order). A periodic flush waits for the one in flight instead of piling up behind a slow disk.
    /// </param>
    public void Flush(bool final = false)
    {
        if (memory.Last is not { } last || (flushesInFlight > 0 && !final))
        {
            return;
        }

        // Multibox (D11): another game client holds this character with a newer login and writes its files; the
        // multibox service logged the warning. The changes stay pending and are written if this client owns it again.
        if (!snapshots.CanWrite(last.ContentId))
        {
            return;
        }

        var saveSnapshot = memory.Saves.Pending && !ReferenceEquals(last, queuedSnapshot);
        // Copies: the poller keeps changing its own maps while the writer serializes these.
        var accepted = memory.AcceptedSinceDirty ? new Dictionary<ushort, DateTime>(memory.AcceptedSince) : null;
        var abandoned = memory.AbandonedDirty ? new Dictionary<ushort, AbandonedEntry>(memory.Abandoned) : null;
        if (!saveSnapshot && accepted is null && abandoned is null)
        {
            return;
        }

        // Cleared now so the next change marks them again; a failed write sets them back when it lands.
        memory.AcceptedSinceDirty = false;
        memory.AbandonedDirty = false;
        var acceptedPath = AcceptedSincePath(last.ContentId);
        var abandonedPath = AbandonedPath(last.ContentId);
        flushesInFlight++;
        if (saveSnapshot)
        {
            queuedSnapshot = last;
        }

        writer.Enqueue(
            () => new FlushOutcome(
                saveSnapshot ? Attempt(() => snapshots.WriteToStore(last)) : null,
                accepted is null ? null : Attempt(() => AcceptedSince.Save(acceptedPath, accepted)),
                abandoned is null ? null : Attempt(() => AbandonedLedger.Save(abandonedPath, abandoned))),
            (outcome, error) => FlushLanded(last, saveSnapshot, accepted is not null, abandoned is not null, outcome, error));
    }

    /// <summary>Runs one write on the writer; its exception, or null when it succeeded.</summary>
    private static Exception? Attempt(Action write)
    {
        try
        {
            write();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>Framework thread: a flush landed. The debouncer, the dirty flags and the warnings follow its outcome.</summary>
    private void FlushLanded(CharacterSnapshot saved, bool snapshotQueued, bool acceptedQueued, bool abandonedQueued, FlushOutcome? outcome, Exception? error)
    {
        flushesInFlight = Math.Max(0, flushesInFlight - 1);
        if (snapshotQueued && ReferenceEquals(queuedSnapshot, saved))
        {
            queuedSnapshot = null;
        }

        if (snapshotQueued && error is null && outcome?.Snapshot is null && ReferenceEquals(handoff, saved))
        {
            // On disk now: a first pass reads it from the file, where another client's later save would also be.
            handoff = null;
        }

        if (disposed)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var sameCharacter = memory.Last?.ContentId == saved.ContentId;
        if (snapshotQueued)
        {
            var snapshotError = error ?? outcome?.Snapshot;
            var saves = memory.Saves;
            if (snapshotError is null)
            {
                saves.MarkSaved(now);
                snapshots.Record(saved);
                if (memory.Last is { } newer && !ReferenceEquals(newer, saved))
                {
                    // A later capture was committed while this one was on the writer: it is still to be written.
                    saves.MarkDirty();
                }

                if (saveWarned)
                {
                    saveWarned = false;
                    log.Information("Snapshot save recovered for {ContentId}", saved.ContentId);
                }
            }
            else
            {
                saves.MarkFailed(now);
                if (!saveWarned)
                {
                    saveWarned = true;
                    log.Warning(snapshotError, "Snapshot save failed for {ContentId}; retrying every {Seconds} s until it succeeds", saved.ContentId, SaveInterval.TotalSeconds);
                }
                else
                {
                    log.Debug(snapshotError, "Snapshot save still failing for {ContentId} ({Failures} attempts)", saved.ContentId, saves.Failures);
                }
            }
        }

        if (acceptedQueued)
        {
            if ((error ?? outcome?.Accepted) is { } acceptedError)
            {
                // Derived data: the next flush retries; the Stalled preset just reads a stale file until then.
                memory.AcceptedSinceDirty |= sameCharacter;
                if (!acceptedSinceWarned)
                {
                    acceptedSinceWarned = true;
                    log.Warning(acceptedError, "Accepted-time sidecar save failed for {ContentId}", saved.ContentId);
                }
            }
            else
            {
                acceptedSinceWarned = false;
            }
        }

        if (abandonedQueued)
        {
            if ((error ?? outcome?.Abandoned) is { } abandonedError)
            {
                // Stays dirty: the next flush retries.
                memory.AbandonedDirty |= sameCharacter;
                if (!abandonedWarned)
                {
                    abandonedWarned = true;
                    log.Warning(abandonedError, "Abandoned-quest sidecar save failed for {ContentId}", saved.ContentId);
                }
            }
            else
            {
                abandonedWarned = false;
            }
        }
    }

    private string AcceptedSincePath(ulong contentId) => AcceptedSince.PathFor(session.Paths.CharactersDir, contentId);

    private string AbandonedPath(ulong contentId) => AbandonedLedger.PathFor(session.Paths.CharactersDir, contentId);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        framework.Update -= OnUpdate;
        snapshots.LoggingOut -= OnLoggingOut;
        session.CharacterForgotten -= OnCharacterForgotten;
        session.DataDeleted -= OnDataDeleted;
        DiscardPasses();
        Flush(final: true);
    }

    private void OnUpdate(IFramework _)
    {
        if (disposed)
        {
            return;
        }

        var ready = snapshots.CharacterReady && clientState.IsLoggedIn && session.Bundle is not null;
        if (!ready)
        {
            if (wasReady)
            {
                wasReady = false;
                DiscardPasses();
                Flush(final: true);
                // The save just queued may land after the next first pass reads the file; until it lands, that pass
                // continues from the capture itself.
                handoff = queuedSnapshot;
                memory.Reset();
                readiness.Reset();
                heldBack.Reset();
                DailyOffers?.Clear();
                lastOffer = null;
                Notify(session.ClearLive);
            }

            return;
        }

        var now = DateTime.UtcNow;
        if (pendingFirst is { } pending)
        {
            // No capture while the first pass is on the worker: there is nothing to diff against yet.
            if (pending.Task.IsCompleted)
            {
                pendingFirst = null;
                CommitFirstPass(pending, now);
            }

            return;
        }

        if (pendingFull is { } full)
        {
            // No capture while a full pass is on the worker: the next one diffs against what that pass commits.
            if (full.Task.IsCompleted)
            {
                pendingFull = null;
                CommitFullPass(full, now);
            }

            return;
        }

        var first = !wasReady;
        wasReady = true;
        if (!first && !schedule.IsDue(now, config.PollInterval))
        {
            return;
        }

        schedule.Attempt(now);

        // Game reads and the state commit: only these drive the backoff. The stopwatch covers capture, diff and
        // resolve; publishing to the session (and the UI it wakes) is not part of a poll's own cost.
        PollResult? result = null;
        var failed = false;
        var started = Stopwatch.GetTimestamp();
        try
        {
            result = Poll(now);
        }
        catch (ImplausibleCaptureException ex)
        {
            failed = true;
            HoldBack(ex.Result, now);
        }
        catch (Exception ex)
        {
            failed = true;
            if (schedule.Fail(now))
            {
                log.Warning(ex, "Game state read failed; retrying in {Seconds} s and backing off to {Max} s", schedule.Wait.TotalSeconds, MaxBackoff.TotalSeconds);
            }
        }

        // A poll that only started a first or full pass has not succeeded yet: its outcome, the backoff and the
        // poller's health wait for the commit. Poll throws before it starts one, so a failed poll is always settled.
        var settled = pendingFirst is null && pendingFull is null;
        if (!failed)
        {
            session.RecordPoll(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (settled)
            {
                RecordSuccess();
            }
        }

        // Session listeners (the UI) run apart from the reads: a throwing subscriber is not a game-read failure.
        Notify(() =>
        {
            if (settled)
            {
                session.SetPollerHealthy(!failed);
            }

            if (result is { } r)
            {
                Publish(r);
            }
        });

        if (memory.Saves.ShouldSave(now))
        {
            Flush();
        }
    }

    /// <summary>
    /// Reads the client, diffs against the last capture and commits the new state. Returns null when nothing changed,
    /// and also when the capture became a first pass or a full pass: those are resolved on a worker and committed by
    /// <see cref="CommitFirstPass"/> or <see cref="CommitFullPass"/>. Any exception here means the game read failed;
    /// the committed state is untouched.
    /// </summary>
    /// <param name="deferFull">
    /// False resolves a full pass here rather than on a worker: the logout's last capture, which must be committed
    /// before the character goes.
    /// </param>
    /// <param name="loggingOut">The logout's last capture: it keeps the committed gear read (<see cref="GateItemGuard"/>).</param>
    private PollResult? Poll(DateTime now, bool deferFull = true, bool loggingOut = false)
    {
        var bundle = session.Bundle!;
        if (memory.IsStaleFor(bundle))
        {
            // A catalog retry replaced the bundle: the committed evaluations belong to the old one, so a diff against
            // them would find nothing and never re-resolve. Persist what there is and start over with a first pass.
            log.Debug("Catalog instance changed; the next poll is a first pass");
            Flush(final: true);
            // Still the live character: its last capture is newer than the file until the save lands.
            handoff = memory.Last;
            memory.Reset();
        }

        var catalog = bundle.Catalog;
        var captureStarted = Stopwatch.GetTimestamp();
        var snapshot = reader.Capture(catalog, bundle.Jobs, memory.Last?.CompletedBits);
        var captureMs = Stopwatch.GetElapsedTime(captureStarted).TotalMilliseconds;
        // Today's allied society offer, as far as the game's own calculation could be read (DailyOfferReader): an
        // unknown society's dailies are not held back. A change of the offer re-resolves everything below.
        // lastOffer follows only a commit: a capture held back (or a poll that throws) leaves the change to the next one.
        var offer = DailyOffers?.Read(catalog, snapshot, now) ?? DailyOffer.None;
        var offerChanged = !offer.SameAs(lastOffer);
        var context = session.BaseContext.WithDailyOffer(offer);

        var last = memory.Last;
        if (last is not null && last.ContentId == snapshot.ContentId)
        {
            // Containers already cleared (at logout, or for a moment on the way) read as no relic weapon at all: the
            // logout keeps the committed read, and a sudden empty read counts only once it persists (GateItemGuard).
            var gateItems = GateItemGuard.Settle(last.GateItems, snapshot.GateItems, loggingOut);
            if (!ReferenceEquals(gateItems, snapshot.GateItems))
            {
                snapshot = snapshot with { GateItems = gateItems };
            }
        }

        if (last is null || memory.States is null || last.ContentId != snapshot.ContentId)
        {
            // Right after login the client can report a loaded player with no quest data yet (all-zero mask, empty
            // journal). Committing that would wipe the accepted times and announce every quest on the next poll, so
            // such a capture is refused until it settles; the next poll captures again.
            if (!IsSettled(snapshot, now))
            {
                return null;
            }

            if (last is not null && last.ContentId != snapshot.ContentId)
            {
                // Another character arrived without a not-ready gap in between: persist the previous one before dropping it.
                Flush(final: true);
            }

            memory.Reset();
            StartFirstPass(snapshot, bundle, context, offer, captureMs);
            return null;
        }

        var states = memory.States;
        // Completion dates (decision 9) ride along with the capture: carried over, and stamped for quests just completed.
        snapshot = CompletionDates.Carry(last, snapshot);
        var diff = SnapshotDiff.Compute(last, snapshot);
        if (diff.IsEmpty && !offerChanged)
        {
            return null;
        }

        // The plausibility guard (1.5.0): a capture that lost many completed quests at once, or emptied the journal,
        // is held back before anything (the accepted times, the abandoned ledger, the commit) takes it in.
        // A loss that keeps reading the same for a few minutes is real and is taken in after all, when it is committed:
        // until then the watch stays accepted (HeldBackCaptures.Accepted), so a deferred pass dropped at logout, or one
        // that faulted, leaves the next capture of the same loss (the logout's own included) accepted at once.
        // A New Game+ replay (1.11.0, C4a) is not held back: the replayed quests keep their completion from the last
        // capture and the rest of the capture is judged and committed as usual, so a chapter never freezes tracking.
        var (judged, plausibility) = CapturePlausibility.Judge(last, snapshot, catalog, bundle.NewGamePlus);
        if (!ReferenceEquals(judged, snapshot))
        {
            snapshot = judged;
            diff = SnapshotDiff.Compute(last, snapshot);
            if (diff.IsEmpty && !offerChanged)
            {
                if (heldBack.Count > 0)
                {
                    heldBack.Reset();
                }

                return null;
            }
        }

        PlausibilityResult? accepted = null;
        if (!plausibility.Plausible)
        {
            if (!heldBack.Observe(last, snapshot, plausibility, now))
            {
                throw new ImplausibleCaptureException(plausibility);
            }

            accepted = plausibility;
        }
        else if (heldBack.Count > 0)
        {
            heldBack.Reset();
        }

        // A level change touches every level-gated quest, which the reverse index cannot enumerate by job, and a job
        // change, a duty clear, a new offer or a new mount touch quests it cannot enumerate at all: everything is resolved, on a
        // worker (14-17 ms over the whole catalog was a dropped frame on every gearset change).
        var full = FullPass.Needed(diff, offerChanged);
        if (full && deferFull)
        {
            StartFullPass(last, snapshot, diff, states, bundle, context, offer, now, accepted);
            return null;
        }

        var resolved = full
            ? StateResolver.ResolveAll(catalog, snapshot, context)
            : StateResolver.ResolveDependents(states, ChangedRows(diff, catalog, session.Index!), session.Index!, catalog, snapshot, context, changedFestivals: diff.ChangedFestivals);

        var events = QuestEvents.Derive(diff, last, snapshot, catalog, states, resolved, now);
        if (accepted is { } loss)
        {
            AcceptHeldBack(loss, snapshot.ContentId, now);
        }

        return Take(last, snapshot, diff, resolved, events, bundle, context, offer, now);
    }

    /// <summary>
    /// Commits a diffed capture with its evaluations and events, on the frame it was polled or on the frame its full
    /// pass landed: the accepted times and the abandoned ledger take the change in, then the memory and the offer.
    /// </summary>
    private PollResult Take(
        CharacterSnapshot last,
        CharacterSnapshot snapshot,
        SnapshotDiff diff,
        IReadOnlyDictionary<uint, QuestEvaluation> resolved,
        IReadOnlyList<QuestEvent> events,
        CatalogBundle bundle,
        EvalContext context,
        DailyOffer offer,
        DateTime now)
    {
        memory.AcceptedSinceDirty |= AcceptedSince.Apply(memory.AcceptedSince, last, snapshot, diff, now);

        // Abandoned quests are recorded (and re-accepted or completed ones dropped) with the step they had reached; the
        // sidecar goes to disk with the snapshot the commit below marks dirty, through the same save debouncer.
        memory.AbandonedDirty |= AbandonedLedger.Apply(memory.Abandoned, events, last, bundle.Catalog);

        memory.Commit(snapshot, resolved, bundle);
        lastOffer = offer;
        return new PollResult(snapshot, resolved, context, events);
    }

    /// <summary>
    /// Hands a capture's full resolve and its events to a worker (<see cref="FullPass.Run"/>). Everything it reads is
    /// immutable: the capture and the one it was diffed against, the diff, the committed evaluations (replaced on a
    /// commit, never changed in place), the catalog and the context. The capture already passed the plausibility
    /// guard; the accepted times, the ledger and the memory take it in only when <see cref="CommitFullPass"/> does.
    /// </summary>
    /// <param name="accepted">A held-back loss the guard now takes in: announced and backed up when the pass commits.</param>
    private void StartFullPass(
        CharacterSnapshot last,
        CharacterSnapshot snapshot,
        SnapshotDiff diff,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        CatalogBundle bundle,
        EvalContext context,
        DailyOffer offer,
        DateTime now,
        PlausibilityResult? accepted)
    {
        var catalog = bundle.Catalog;
        var task = Task.Run(() => FullPass.Run(catalog, last, snapshot, diff, context, states, now));
        pendingFull = new PendingFullPass(last, snapshot, diff, bundle, context, offer, now, task, Stopwatch.GetTimestamp(), accepted);
    }

    /// <summary>
    /// Takes a finished full pass: commits and publishes it when the catalog, the character and the committed capture
    /// are the ones it was computed from (<see cref="FullPass.Judge"/>); otherwise drops it, and the next poll captures
    /// again and diffs against what is committed then.
    /// </summary>
    private void CommitFullPass(PendingFullPass pending, DateTime now)
    {
        if (!pending.Task.IsCompletedSuccessfully)
        {
            // Pure Core work on immutable inputs: a failure is a bug, handled like a first pass that faulted.
            if (schedule.Fail(now))
            {
                log.Warning(pending.Task.Exception?.GetBaseException(), "Re-evaluation failed; retrying in {Seconds} s and backing off to {Max} s", schedule.Wait.TotalSeconds, MaxBackoff.TotalSeconds);
            }

            Notify(() => session.SetPollerHealthy(false));
            return;
        }

        var verdict = FullPass.Judge(pending.Bundle, session.Bundle, pending.Snapshot.ContentId, reader.ContentId, pending.Base, memory.Last);
        if (verdict != FullPassVerdict.Commit)
        {
            log.Debug("Re-evaluation for {ContentId} dropped ({Verdict}); the next poll captures again", pending.Snapshot.ContentId, verdict);
            return;
        }

        var result = pending.Task.Result;
        if (pending.Accepted is { } loss)
        {
            // Only now, with the commit: a pass dropped or faulted leaves the watch accepted for the next capture.
            AcceptHeldBack(loss, pending.Snapshot.ContentId, now);
        }

        var poll = Take(pending.Base, pending.Snapshot, pending.Diff, result.States, result.Events, pending.Bundle, pending.Context, pending.Offer, pending.CapturedUtc);
        log.Debug(
            "Re-evaluation for {ContentId}: {Count} quests in {ResolveMs:F1} ms on a worker, committed {TotalMs:F0} ms after the capture",
            pending.Snapshot.ContentId,
            result.States.Count,
            result.ResolveMs,
            Stopwatch.GetElapsedTime(pending.StartedTimestamp).TotalMilliseconds);

        RecordSuccess();
        Notify(() =>
        {
            session.SetPollerHealthy(true);
            Publish(poll);
        });

        if (memory.Saves.ShouldSave(now))
        {
            Flush();
        }
    }

    /// <summary>
    /// Hands the catalog-wide resolve and the sidecar read to a worker. Everything it touches is immutable or its
    /// own: the capture, the catalog, the base context (whose festival hook only reads a map and the clock) and a
    /// fresh warnings list. <see cref="OnUpdate"/> polls the task and <see cref="CommitFirstPass"/> takes the result.
    /// </summary>
    private void StartFirstPass(CharacterSnapshot snapshot, CatalogBundle bundle, EvalContext context, DailyOffer offer, double captureMs)
    {
        var catalog = bundle.Catalog;
        var sidecarPath = AcceptedSincePath(snapshot.ContentId);
        var abandonedPath = AbandonedPath(snapshot.ContentId);
        var seed = handoff is { } h && h.ContentId == snapshot.ContentId ? h : null;
        var task = Task.Run(() =>
        {
            // The capture is judged against the stored snapshot as a later one is against the last (the plausibility
            // guard); a stored file that cannot be read is not judged against. A login mid-chapter of New Game+
            // (1.11.0, C4a) keeps the replayed quests' completion from the stored snapshot and goes live with the rest.
            var stored = seed ?? snapshots.ReadStored(snapshot.ContentId).Value;
            var (capture, plausibility) = CapturePlausibility.Judge(stored, snapshot, catalog, bundle.NewGamePlus);

            var started = Stopwatch.GetTimestamp();
            var states = StateResolver.ResolveAll(catalog, capture, context);
            var resolveMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            // Accepted times survive across sessions in the sidecar; quests that entered the journal while the
            // plugin was not watching are stamped at commit time, the earliest moment they are known to be there.
            var warnings = new List<string>();
            var acceptedSince = AcceptedSince.Load(sidecarPath, warnings);
            var abandoned = AbandonedLedger.Load(abandonedPath, warnings);

            // Completion dates continue from the dates file; quests completed while the plugin was not watching are
            // dated "between the stored capture and now" (decision 9). A dates file that exists but cannot be read
            // right now throws here: starting over would save an empty record over it, so the pass fails and is
            // retried on the usual backoff.
            var dated = seed is not null
                ? CompletionDates.Resume(seed, capture)
                : CompletionDates.BeginFrom(snapshots.ReadStoredDates(snapshot.ContentId), capture, warnings);

            return new FirstPassResult(dated, stored, plausibility, states, acceptedSince, abandoned, warnings, resolveMs);
        });

        pendingFirst = new FirstPass(snapshot, bundle, context, offer, task, Stopwatch.GetTimestamp(), captureMs);
        log.Debug("First evaluation for {Name} ({ContentId}) started on a worker", snapshot.Name, snapshot.ContentId);
    }

    /// <summary>Takes a finished first pass: commits, publishes and flushes it, or drops it when the world moved on.</summary>
    private void CommitFirstPass(FirstPass pending, DateTime now)
    {
        if (!pending.Task.IsCompletedSuccessfully)
        {
            // Pure Core work on immutable inputs: a failure here is a bug, not a game read. It shares the read
            // failures' schedule: the next first pass starts a backoff step after the fault (not every second),
            // the warning is logged once, and the poller reads unhealthy until a pass commits.
            if (schedule.Fail(now))
            {
                log.Warning(pending.Task.Exception?.GetBaseException(), "First evaluation failed; retrying in {Seconds} s and backing off to {Max} s", schedule.Wait.TotalSeconds, MaxBackoff.TotalSeconds);
            }

            Notify(() => session.SetPollerHealthy(false));
            return;
        }

        if (!ReferenceEquals(pending.Bundle, session.Bundle))
        {
            log.Debug("Catalog changed during the first evaluation; it will run again");
            return;
        }

        if (pending.Snapshot.ContentId != reader.ContentId)
        {
            // Another character arrived without a not-ready gap while the pass was on the worker: publishing the
            // previous one as live, even for one interval, would show the wrong character. The next poll starts over.
            log.Debug("Character changed during the first evaluation; the next poll starts another");
            return;
        }

        var result = pending.Task.Result;
        foreach (var warning in result.Warnings)
        {
            log.Warning("Character sidecar: {Warning}", warning);
        }

        var snapshot = result.Snapshot;
        if (!result.Plausibility.Plausible)
        {
            // Nothing is committed: the next poll captures again and starts another first pass, as for a read failure.
            if (result.Stored is null || !heldBack.Observe(result.Stored, snapshot, result.Plausibility, now))
            {
                HoldBack(result.Plausibility, now);
                Notify(() => session.SetPollerHealthy(false));
                return;
            }

            AcceptHeldBack(result.Plausibility, snapshot.ContentId, now);
        }
        else if (heldBack.Count > 0)
        {
            heldBack.Reset();
        }

        var dirty = AcceptedSince.Reconcile(result.AcceptedSince, snapshot, now);
        memory.Commit(snapshot, result.States, pending.Bundle);
        lastOffer = pending.Offer;
        handoff = null;
        memory.SetAcceptedSince(result.AcceptedSince, dirty);
        // Quests abandoned earlier and taken up again, or completed, while the plugin was not watching leave the list.
        memory.SetAbandoned(result.Abandoned, AbandonedLedger.Reconcile(result.Abandoned, snapshot));

        var sinceCaptureMs = Stopwatch.GetElapsedTime(pending.StartedTimestamp).TotalMilliseconds + pending.CaptureMs;
        if (!firstCaptureLogged)
        {
            firstCaptureLogged = true;
            log.Information(
                "First evaluation for {Name} ({ContentId}): capture {CaptureMs:F1} ms on the framework thread, {Count} quests resolved in {ResolveMs:F1} ms on a worker, committed {TotalMs:F0} ms after the capture",
                snapshot.Name,
                snapshot.ContentId,
                pending.CaptureMs,
                result.States.Count,
                result.ResolveMs,
                sinceCaptureMs);
        }
        else
        {
            log.Debug("First evaluation for {Name} ({ContentId}): {Count} quests in {ResolveMs:F1} ms on a worker", snapshot.Name, snapshot.ContentId, result.States.Count, result.ResolveMs);
        }

        RecordSuccess();
        Notify(() =>
        {
            session.SetPollerHealthy(true);
            Publish(new PollResult(snapshot, result.States, pending.Context, []));
        });
        Flush();
    }

    /// <summary>
    /// A capture was held back as implausible: the committed state and the saved files stay as they are, the next
    /// capture waits a backoff step, the counts are logged once per run of such captures, and the chat line is printed
    /// once per load.
    /// </summary>
    private void HoldBack(PlausibilityResult result, DateTime now)
    {
        var note = result.LogNote ?? result.Verdict.ToString();
        if (schedule.Fail(now))
        {
            log.Warning(
                "Capture not saved: {Note}; keeping the last saved state and capturing again in {Seconds} s (backing off to {Max} s)",
                note,
                schedule.Wait.TotalSeconds,
                MaxBackoff.TotalSeconds);
        }
        else
        {
            log.Debug("Capture held back again: {Note}", note);
        }

        if (implausibleNoticed || PrintNotice is not { } print)
        {
            return;
        }

        implausibleNoticed = true;
        try
        {
            print(Ui.Strings.PlausibilitySkippedNotice);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Chat print failed");
        }
    }

    /// <summary>
    /// A held-back loss read the same long enough (<see cref="HeldBackCaptures"/>) and is about to be committed, on this
    /// frame (never before a deferred pass lands): what is pending is written first, the saved file is copied to the
    /// backup on the writer before the capture's own save, the acceptance is logged and printed in chat, and the watch ends.
    /// </summary>
    private void AcceptHeldBack(PlausibilityResult result, ulong contentId, DateTime now)
    {
        var minutes = heldBack.SinceUtc is { } since ? (now - since).TotalMinutes : 0;
        var captures = heldBack.Count;
        heldBack.Reset();
        log.Warning(
            "Capture accepted after reading the same for {Minutes:F1} min over {Captures} captures: {Note}; the earlier save is copied to the backup first",
            minutes,
            captures,
            result.LogNote ?? result.Verdict.ToString());

        Flush(final: true);
        PreserveBackup(contentId);
        if (PrintNotice is { } print)
        {
            try
            {
                print(Ui.Strings.PlausibilityAcceptedNotice);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Chat print failed");
            }
        }
    }

    /// <summary>Queues a copy of the character's saved file to its backup, ahead of any later save on the writer.</summary>
    private void PreserveBackup(ulong contentId)
    {
        if (!snapshots.CanWrite(contentId))
        {
            return;
        }

        writer.Enqueue(
            () => snapshots.BackupNow(contentId),
            (copied, error) =>
            {
                if (error is not null)
                {
                    log.Warning(error, "Backup before saving the accepted capture failed for {ContentId}", contentId);
                }
                else
                {
                    log.Information(
                        copied
                            ? "Backup of {ContentId} refreshed from the save before the accepted capture"
                            : "Backup of {ContentId} not refreshed (no readable save, or the backup holds more progress); it is kept as it was",
                        contentId);
                }
            });
    }

    /// <summary>Thrown by <see cref="Poll"/> for a capture <see cref="CapturePlausibility"/> rejects; never leaves the poller.</summary>
    private sealed class ImplausibleCaptureException(PlausibilityResult result) : Exception(result.LogNote ?? result.Verdict.ToString())
    {
        public PlausibilityResult Result { get; } = result;
    }

    /// <summary>A poll committed, or a first pass did: the backoff and its warning latch clear, and a recovery is logged.</summary>
    private void RecordSuccess()
    {
        var recovered = schedule.Succeed();
        if (recovered > 0)
        {
            log.Information("Poller recovered after {Failures} failure(s)", recovered);
        }
    }

    /// <summary>Forgets a first or full pass in flight; the worker finishes on its own and its result is ignored.</summary>
    private void DiscardPasses()
    {
        if (pendingFirst is { } pending)
        {
            pendingFirst = null;
            pending.Task.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }

        if (pendingFull is { } full)
        {
            pendingFull = null;
            full.Task.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    private void Publish(PollResult result)
    {
        session.SetLive(result.Snapshot, result.States, result.Context, memory.AcceptedSince, memory.Abandoned);
        session.AddEvents(result.Snapshot.ContentId, result.Events);
    }

    /// <summary>
    /// <see cref="LoginReadiness"/> for a first-pass capture. The stored snapshot is consulted only when the capture
    /// looks empty, so the usual login reads no file; an unreadable store simply counts as "nothing stored".
    /// </summary>
    private bool IsSettled(CharacterSnapshot capture, DateTime now)
    {
        CharacterSnapshot? stored = null;
        if (LoginReadiness.LooksEmpty(capture))
        {
            try
            {
                stored = snapshots.Load(capture.ContentId);
            }
            catch (Exception ex)
            {
                log.Debug(ex, "Stored snapshot unavailable while judging the first capture");
            }
        }

        var wasWaiting = readiness.WaitingSinceUtc is not null;
        var wasOverdue = readiness.Overdue;
        switch (readiness.Check(capture, stored, now))
        {
            case LoginVerdict.NotReady:
                if (!wasWaiting)
                {
                    log.Debug("First capture for {ContentId} has no quest data yet; waiting up to {Seconds} s for the client to settle", capture.ContentId, readiness.MaxWait.TotalSeconds);
                }
                else if (readiness.Overdue && !wasOverdue)
                {
                    log.Warning("First capture for {ContentId} still has no quest data after {Seconds} s while the stored snapshot has; keeping the stored character and waiting for the client", capture.ContentId, readiness.MaxWait.TotalSeconds);
                }

                return false;

            case LoginVerdict.ReadyAfterTimeout:
                log.Information("First capture for {ContentId} still has no quest data after {Seconds} s and nothing is stored; committing it as an empty character", capture.ContentId, readiness.MaxWait.TotalSeconds);
                return true;

            default:
                return true;
        }
    }

    /// <summary>Runs a session update; a throwing listener is logged at most once per <see cref="ListenerWarningInterval"/>.</summary>
    private void Notify(Action publish)
    {
        try
        {
            publish();
        }
        catch (Exception ex)
        {
            var now = DateTime.UtcNow;
            if (now - lastListenerWarningUtc >= ListenerWarningInterval)
            {
                lastListenerWarningUtc = now;
                log.Warning(ex, "A session listener threw; game state was committed and polling continues");
            }
            else
            {
                log.Debug(ex, "Session listener threw again");
            }
        }
    }

    /// <summary>
    /// Changed quest ids as catalog row ids. A changed id the catalog does not list (an unnamed prerequisite) still
    /// affects its dependents, so those rows are added directly.
    /// </summary>
    private static HashSet<uint> ChangedRows(SnapshotDiff diff, QuestCatalog catalog, ReversePrereqIndex index)
    {
        var rows = new HashSet<uint>();
        foreach (var questId in diff.ChangedQuestIds)
        {
            if (catalog.TryGetByQuestId(questId, out var quest))
            {
                rows.Add(quest.RowId);
            }
            else
            {
                rows.UnionWith(index.Dependents(QuestRowBase | questId));
            }
        }

        return rows;
    }

    /// <summary>
    /// A last diff before the character goes away. A first pass still on the worker, or none committed yet, has
    /// nothing to diff against and nothing worth saving, so it is dropped rather than resolved on the way out. A full
    /// pass still on the worker is dropped too: the last capture is taken again and resolved here, so it is committed
    /// and saved before the character goes.
    /// </summary>
    private void OnLoggingOut()
    {
        if (disposed)
        {
            return;
        }

        DiscardPasses();
        if (wasReady && !memory.IsEmpty && session.Bundle is not null && reader.IsPlayerLoaded())
        {
            PollResult? result = null;
            try
            {
                result = Poll(DateTime.UtcNow, deferFull: false, loggingOut: true);
            }
            catch (Exception ex)
            {
                log.Debug(ex, "Logout capture skipped");
            }

            if (result is { } r)
            {
                Notify(() => Publish(r));
            }
        }

        Flush(final: true);
    }

    /// <summary>
    /// Forgetting the live character deletes its snapshot and sidecar while both stay in memory here; marking both
    /// dirty writes the pair again on the next flush, so the two never drift apart (see <see cref="PollerMemory.OnCharacterForgotten"/>).
    /// </summary>
    private void OnCharacterForgotten(ulong contentId)
    {
        if (!disposed)
        {
            memory.OnCharacterForgotten(contentId);
            if (handoff?.ContentId == contentId)
            {
                handoff = null;
            }
        }
    }

    /// <summary>
    /// Every stored file is gone: the memory goes with it, so the next poll is a first pass that writes the snapshot
    /// and a fresh sidecar rather than a diff that reappears on disk piecemeal.
    /// </summary>
    private void OnDataDeleted()
    {
        if (!disposed)
        {
            DiscardPasses();
            memory.OnDataDeleted();
            readiness.Reset();
            heldBack.Reset();
            handoff = null;
        }
    }

    private readonly record struct PollResult(
        CharacterSnapshot Snapshot,
        IReadOnlyDictionary<uint, QuestEvaluation> States,
        EvalContext Context,
        IReadOnlyList<QuestEvent> Events);

    /// <summary>A first pass on the worker: what was captured, what it was resolved against and the task doing it.</summary>
    private sealed record FirstPass(
        CharacterSnapshot Snapshot,
        CatalogBundle Bundle,
        EvalContext Context,
        DailyOffer Offer,
        Task<FirstPassResult> Task,
        long StartedTimestamp,
        double CaptureMs);

    /// <summary>
    /// A full pass on the worker: the committed capture it was diffed against (<paramref name="Base"/>), the capture,
    /// the diff, what it resolves against, the offer it was read with, the poll's time, the task doing it, and the
    /// held-back loss it takes in (<paramref name="Accepted"/>, null for an ordinary capture).
    /// </summary>
    private sealed record PendingFullPass(
        CharacterSnapshot Base,
        CharacterSnapshot Snapshot,
        SnapshotDiff Diff,
        CatalogBundle Bundle,
        EvalContext Context,
        DailyOffer Offer,
        DateTime CapturedUtc,
        Task<FullPassResult> Task,
        long StartedTimestamp,
        PlausibilityResult? Accepted);

    /// <summary>What each part of a flush threw on the writer; null for a part that was written or not queued.</summary>
    private sealed record FlushOutcome(Exception? Snapshot, Exception? Accepted, Exception? Abandoned);

    /// <param name="Snapshot">The capture with its completion dates (<see cref="CompletionDates.Begin"/>); what is committed.</param>
    /// <param name="Stored">What the capture was judged against: the stored snapshot, or this session's last capture; null when none could be read.</param>
    /// <param name="Plausibility">The plausibility guard's verdict on the capture against <paramref name="Stored"/>.</param>
    private sealed record FirstPassResult(
        CharacterSnapshot Snapshot,
        CharacterSnapshot? Stored,
        PlausibilityResult Plausibility,
        Dictionary<uint, QuestEvaluation> States,
        Dictionary<ushort, DateTime> AcceptedSince,
        Dictionary<ushort, AbandonedEntry> Abandoned,
        List<string> Warnings,
        double ResolveMs);
}
