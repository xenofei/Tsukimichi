using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>
/// Once per <see cref="Configuration.PollInterval"/> while a character is ready and the catalog is built: capture,
/// diff, re-resolve what changed, emit events, publish to <see cref="SessionState"/> and persist (debounced).
/// Exceptions from game reads back the interval off exponentially to <see cref="MaxBackoff"/> with a single warning;
/// exceptions from session listeners are logged (rate-limited) and never affect the backoff.
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

    private readonly PollerMemory memory = new(SaveInterval);
    private readonly PollBackoff backoff = new(TimeSpan.FromSeconds(2), MaxBackoff);
    private readonly LoginReadiness readiness = new();

    /// <summary>The first pass in flight on a worker; null while none is. Touched only on the framework thread.</summary>
    private FirstPass? pendingFirst;

    private bool firstCaptureLogged;
    private bool acceptedSinceWarned;
    private DateTime lastPollUtc = DateTime.MinValue;
    private DateTime lastListenerWarningUtc = DateTime.MinValue;
    private bool wasReady;
    private bool warned;
    private bool saveWarned;
    private bool disposed;

    public StatePoller(
        IFramework framework,
        IClientState clientState,
        IPluginLog log,
        GameStateReader reader,
        SnapshotService snapshots,
        SessionState session,
        Configuration config)
    {
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
    /// Writes the last capture if it changed since the last save, then the accepted-time sidecar if it changed. A
    /// failure is warned once, retried no sooner than <see cref="SaveInterval"/> later, and the recovery is logged.
    /// </summary>
    public void Flush()
    {
        if (memory.Last is not { } last)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var saves = memory.Saves;
        if (saves.Pending)
        {
            try
            {
                snapshots.Save(last);
                saves.MarkSaved(now);
                if (saveWarned)
                {
                    saveWarned = false;
                    log.Information("Snapshot save recovered for {ContentId}", last.ContentId);
                }
            }
            catch (Exception ex)
            {
                saves.MarkFailed(now);
                if (!saveWarned)
                {
                    saveWarned = true;
                    log.Warning(ex, "Snapshot save failed for {ContentId}; retrying every {Seconds} s until it succeeds", last.ContentId, SaveInterval.TotalSeconds);
                }
                else
                {
                    log.Debug(ex, "Snapshot save still failing for {ContentId} ({Failures} attempts)", last.ContentId, saves.Failures);
                }
            }
        }

        if (memory.AcceptedSinceDirty)
        {
            try
            {
                AcceptedSince.Save(AcceptedSincePath(last.ContentId), memory.AcceptedSince);
                memory.AcceptedSinceDirty = false;
                acceptedSinceWarned = false;
            }
            catch (Exception ex)
            {
                // Derived data: the next journal change retries; the Stalled preset just reads a stale file until then.
                if (!acceptedSinceWarned)
                {
                    acceptedSinceWarned = true;
                    log.Warning(ex, "Accepted-time sidecar save failed for {ContentId}", last.ContentId);
                }
            }
        }
    }

    private string AcceptedSincePath(ulong contentId) => AcceptedSince.PathFor(session.Paths.CharactersDir, contentId);

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
        Flush();
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
                Flush();
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
        if (!first && now - lastPollUtc < backoff.IntervalOr(config.PollInterval))
        {
            return;
        }

        lastPollUtc = now;

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
            backoff.RecordFailure();
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "Game state read failed; retrying in {Seconds} s and backing off to {Max} s", backoff.Current.TotalSeconds, MaxBackoff.TotalSeconds);
            }
        }

        if (!failed)
        {
            session.RecordPoll(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (backoff.IsActive)
            {
                log.Information("Poller recovered after {Failures} failure(s)", backoff.Failures);
            }

            backoff.Reset();
            warned = false;
        }

        // Session listeners (the UI) run apart from the reads: a throwing subscriber is not a game-read failure.
        Notify(() =>
        {
            session.SetPollerHealthy(!failed);
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
            Flush();
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
                Flush();
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
        var task = Task.Run(() =>
        {
            var started = Stopwatch.GetTimestamp();
            var states = StateResolver.ResolveAll(catalog, snapshot, context);
            var resolveMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            // Accepted times survive across sessions in the sidecar; quests that entered the journal while the
            // plugin was not watching are stamped at commit time, the earliest moment they are known to be there.
            var warnings = new List<string>();
            var acceptedSince = AcceptedSince.Load(sidecarPath, warnings);
            return new FirstPassResult(states, acceptedSince, warnings, resolveMs);
        });

        pendingFirst = new FirstPass(snapshot, bundle, context, task, Stopwatch.GetTimestamp(), captureMs);
        log.Debug("First evaluation for {Name} ({ContentId}) started on a worker", snapshot.Name, snapshot.ContentId);
    }

    /// <summary>Takes a finished first pass: commits, publishes and flushes it, or drops it when the world moved on.</summary>
    private void CommitFirstPass(FirstPass pending, DateTime now)
    {
        if (!pending.Task.IsCompletedSuccessfully)
        {
            // Pure Core work on immutable inputs: a failure here is a bug, not a game read. Report it once and let
            // the next poll start another first pass at the backoff cadence rather than every second.
            backoff.RecordFailure();
            if (!warned)
            {
                warned = true;
                log.Warning(pending.Task.Exception?.GetBaseException(), "First evaluation failed; retrying in {Seconds} s", backoff.Current.TotalSeconds);
            }

            return;
        }

        if (!ReferenceEquals(pending.Bundle, session.Bundle))
        {
            log.Debug("Catalog changed during the first evaluation; it will run again");
            return;
        }

        var result = pending.Task.Result;
        foreach (var warning in result.Warnings)
        {
            log.Warning("Accepted times: {Warning}", warning);
        }

        var snapshot = pending.Snapshot;
        var dirty = AcceptedSince.Reconcile(result.AcceptedSince, snapshot, now);
        memory.Commit(snapshot, result.States, pending.Bundle);
        memory.SetAcceptedSince(result.AcceptedSince, dirty);

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

        Notify(() => Publish(new PollResult(snapshot, result.States, pending.Context, [])));
        Flush();
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
        session.SetLive(result.Snapshot, result.States, result.Context, memory.AcceptedSince);
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
        switch (readiness.Check(capture, stored, now))
        {
            case LoginVerdict.NotReady:
                if (!wasWaiting)
                {
                    log.Debug("First capture for {ContentId} has no quest data yet; waiting up to {Seconds} s for the client to settle", capture.ContentId, readiness.MaxWait.TotalSeconds);
                }

                return false;

            case LoginVerdict.ReadyAfterTimeout:
                log.Information("First capture for {ContentId} still has no quest data after {Seconds} s; committing it as an empty character", capture.ContentId, readiness.MaxWait.TotalSeconds);
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

        Flush();
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

    private sealed record FirstPassResult(
        Dictionary<uint, QuestEvaluation> States,
        Dictionary<ushort, DateTime> AcceptedSince,
        List<string> Warnings,
        double ResolveMs);
}
