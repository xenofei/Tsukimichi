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
/// The first pass for a character resolves the whole catalog, which took a visible slice of a frame; the capture
/// still happens here (ClientStructs reads stay on the framework thread), but the catalog-wide resolve and the
/// sidecar read run on a worker over an immutable snapshot, and the result is committed and published on the
/// framework thread when it lands. Every later poll diffs incrementally. What the poller keeps between polls lives
/// in <see cref="PollerMemory"/>. Runs entirely inside <see cref="IFramework.Update"/> apart from that worker.
/// </para>
/// </summary>
public sealed class StatePoller : IDisposable
{
    public static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    /// <summary>Above this many changed quests a full resolve is cheaper than walking dependents.</summary>
    public const int FullResolveThreshold = 200;

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

    /// <summary>The first pass in flight on a worker; null while none is. Touched only on the framework thread.</summary>
    private FirstPass? pendingFirst;

    private bool firstCaptureLogged;
    private bool acceptedSinceWarned;
    private bool abandonedWarned;
    private DateTime lastListenerWarningUtc = DateTime.MinValue;
    private bool wasReady;
    private bool saveWarned;
    private bool disposed;

    /// <summary>Flushes on the writer that have not landed yet; a periodic flush waits for them.</summary>
    private int flushesInFlight;

    /// <summary>The capture the newest flush in flight writes; a final flush does not queue the same one again.</summary>
    private CharacterSnapshot? queuedSnapshot;

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
        framework.Update += OnUpdate;
    }

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
        DiscardFirstPass();
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
                DiscardFirstPass();
                Flush(final: true);
                memory.Reset();
                readiness.Reset();
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
        catch (Exception ex)
        {
            failed = true;
            if (schedule.Fail(now))
            {
                log.Warning(ex, "Game state read failed; retrying in {Seconds} s and backing off to {Max} s", schedule.Wait.TotalSeconds, MaxBackoff.TotalSeconds);
            }
        }

        // A poll that only started a first pass has not succeeded yet: its outcome, the backoff and the poller's
        // health wait for CommitFirstPass. Poll throws before it starts one, so a failed poll is always settled.
        var settled = pendingFirst is null;
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
    /// and also when the capture became a first pass: that one is resolved on a worker and committed by
    /// <see cref="CommitFirstPass"/>. Any exception here means the game read failed; the committed state is untouched.
    /// </summary>
    private PollResult? Poll(DateTime now)
    {
        var bundle = session.Bundle!;
        if (memory.IsStaleFor(bundle))
        {
            // A catalog retry replaced the bundle: the committed evaluations belong to the old one, so a diff against
            // them would find nothing and never re-resolve. Persist what there is and start over with a first pass.
            log.Debug("Catalog instance changed; the next poll is a first pass");
            Flush(final: true);
            memory.Reset();
        }

        var catalog = bundle.Catalog;
        var captureStarted = Stopwatch.GetTimestamp();
        var snapshot = reader.Capture(catalog, bundle.Jobs, memory.Last?.CompletedBits);
        var captureMs = Stopwatch.GetElapsedTime(captureStarted).TotalMilliseconds;
        // The allied-society daily offer is not readable from the client (see GameStateReader), so the live context
        // is the base context: an in-progress daily is Accepted through the journal, a turned-in one is done this cycle.
        var context = session.BaseContext;

        var last = memory.Last;
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
            StartFirstPass(snapshot, bundle, context, captureMs);
            return null;
        }

        var states = memory.States;
        var diff = SnapshotDiff.Compute(last, snapshot);
        if (diff.IsEmpty)
        {
            return null;
        }

        memory.AcceptedSinceDirty |= AcceptedSince.Apply(memory.AcceptedSince, last, snapshot, diff, now);

        // A level change touches every level-gated quest, which the reverse index cannot enumerate by job; level-ups
        // are rare and a full resolve costs milliseconds, so resolve everything rather than pass jobs as levels.
        var full = diff.OtherChanged
            || diff.ChangedJobs.Count > 0
            || diff.ChangedQuestIds.Count > FullResolveThreshold;
        var resolved = full
            ? StateResolver.ResolveAll(catalog, snapshot, context)
            : StateResolver.ResolveDependents(states, ChangedRows(diff, catalog, session.Index!), session.Index!, catalog, snapshot, context, changedFestivals: diff.ChangedFestivals);

        var events = QuestEvents.Derive(diff, last, snapshot, catalog, states, resolved, now);
        // Abandoned quests are recorded (and re-accepted or completed ones dropped) with the step they had reached; the
        // sidecar goes to disk with the snapshot the commit below marks dirty, through the same save debouncer.
        memory.AbandonedDirty |= AbandonedLedger.Apply(memory.Abandoned, events, last, catalog);

        memory.Commit(snapshot, resolved, bundle);
        return new PollResult(snapshot, resolved, context, events);
    }

    /// <summary>
    /// Hands the catalog-wide resolve and the sidecar read to a worker. Everything it touches is immutable or its
    /// own: the capture, the catalog, the base context (whose festival hook only reads a map and the clock) and a
    /// fresh warnings list. <see cref="OnUpdate"/> polls the task and <see cref="CommitFirstPass"/> takes the result.
    /// </summary>
    private void StartFirstPass(CharacterSnapshot snapshot, CatalogBundle bundle, EvalContext context, double captureMs)
    {
        var catalog = bundle.Catalog;
        var sidecarPath = AcceptedSincePath(snapshot.ContentId);
        var abandonedPath = AbandonedPath(snapshot.ContentId);
        var task = Task.Run(() =>
        {
            var started = Stopwatch.GetTimestamp();
            var states = StateResolver.ResolveAll(catalog, snapshot, context);
            var resolveMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            // Accepted times survive across sessions in the sidecar; quests that entered the journal while the
            // plugin was not watching are stamped at commit time, the earliest moment they are known to be there.
            var warnings = new List<string>();
            var acceptedSince = AcceptedSince.Load(sidecarPath, warnings);
            var abandoned = AbandonedLedger.Load(abandonedPath, warnings);
            return new FirstPassResult(states, acceptedSince, abandoned, warnings, resolveMs);
        });

        pendingFirst = new FirstPass(snapshot, bundle, context, task, Stopwatch.GetTimestamp(), captureMs);
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

        var snapshot = pending.Snapshot;
        var dirty = AcceptedSince.Reconcile(result.AcceptedSince, snapshot, now);
        memory.Commit(snapshot, result.States, pending.Bundle);
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

    /// <summary>A poll committed, or a first pass did: the backoff and its warning latch clear, and a recovery is logged.</summary>
    private void RecordSuccess()
    {
        var recovered = schedule.Succeed();
        if (recovered > 0)
        {
            log.Information("Poller recovered after {Failures} failure(s)", recovered);
        }
    }

    /// <summary>Forgets a first pass in flight; the worker finishes on its own and its result is ignored.</summary>
    private void DiscardFirstPass()
    {
        if (pendingFirst is { } pending)
        {
            pendingFirst = null;
            pending.Task.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
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
    /// nothing to diff against and nothing worth saving, so it is dropped rather than resolved on the way out.
    /// </summary>
    private void OnLoggingOut()
    {
        if (disposed)
        {
            return;
        }

        DiscardFirstPass();
        if (wasReady && !memory.IsEmpty && session.Bundle is not null && reader.IsPlayerLoaded())
        {
            PollResult? result = null;
            try
            {
                result = Poll(DateTime.UtcNow);
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
            DiscardFirstPass();
            memory.OnDataDeleted();
            readiness.Reset();
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
        Task<FirstPassResult> Task,
        long StartedTimestamp,
        double CaptureMs);

    /// <summary>What each part of a flush threw on the writer; null for a part that was written or not queued.</summary>
    private sealed record FlushOutcome(Exception? Snapshot, Exception? Accepted, Exception? Abandoned);

    private sealed record FirstPassResult(
        Dictionary<uint, QuestEvaluation> States,
        Dictionary<ushort, DateTime> AcceptedSince,
        Dictionary<ushort, AbandonedEntry> Abandoned,
        List<string> Warnings,
        double ResolveMs);
}
