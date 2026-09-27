using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Game;

/// <summary>
/// Once per <see cref="Configuration.PollInterval"/> while a character is ready and the catalog is built: capture,
/// diff, re-resolve what changed, emit events, publish to <see cref="SessionState"/> and persist (debounced).
/// Exceptions from game reads back the interval off exponentially to <see cref="MaxBackoff"/> with a single warning;
/// exceptions from session listeners are logged (rate-limited) and never affect the backoff.
/// Runs entirely inside <see cref="IFramework.Update"/>.
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

    private readonly SaveDebouncer saves = new(SaveInterval);
    private readonly PollBackoff backoff = new(TimeSpan.FromSeconds(2), MaxBackoff);

    private CharacterSnapshot? last;
    private IReadOnlyDictionary<uint, QuestEvaluation>? states;
    private HashSet<ushort> lastOffer = [];
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
        framework.Update += OnUpdate;
    }

    /// <summary>
    /// Writes the last capture if it changed since the last save. A failure is warned once, retried no sooner than
    /// <see cref="SaveInterval"/> later, and the recovery is logged.
    /// </summary>
    public void Flush()
    {
        if (last is null || !saves.Pending)
        {
            return;
        }

        var now = DateTime.UtcNow;
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

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        framework.Update -= OnUpdate;
        snapshots.LoggingOut -= OnLoggingOut;
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
                Flush();
                Reset();
                Notify(session.ClearLive);
            }

            return;
        }

        var now = DateTime.UtcNow;
        var first = !wasReady;
        wasReady = true;
        if (!first && now - lastPollUtc < backoff.IntervalOr(config.PollInterval))
        {
            return;
        }

        lastPollUtc = now;

        // Game reads and the state commit: only these drive the backoff.
        PollResult? result = null;
        var failed = false;
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

        if (result is { FirstPass: true } || saves.ShouldSave(now))
        {
            Flush();
        }
    }

    /// <summary>
    /// Reads the client, diffs against the last capture and commits the new state. Returns null when nothing changed.
    /// Any exception here means the game read failed; the committed state is untouched in that case.
    /// </summary>
    private PollResult? Poll(DateTime now)
    {
        var bundle = session.Bundle!;
        var catalog = bundle.Catalog;
        var snapshot = reader.Capture(catalog, bundle.Jobs, last?.CompletedBits);
        var offer = reader.ReadDailyOffer();
        var context = session.BaseContext with { TodaysDailyOffer = offer };

        var firstPass = last is null || states is null || last.ContentId != snapshot.ContentId;
        IReadOnlyList<QuestEvent> events = [];
        IReadOnlyDictionary<uint, QuestEvaluation> resolved;

        if (firstPass)
        {
            Reset();
            resolved = StateResolver.ResolveAll(catalog, snapshot, context);
            log.Debug("First evaluation for {Name} ({ContentId}): {Count} quests", snapshot.Name, snapshot.ContentId, resolved.Count);
        }
        else
        {
            var diff = SnapshotDiff.Compute(last!, snapshot);
            var offerChanged = !offer.SetEquals(lastOffer);
            if (diff.IsEmpty && !offerChanged)
            {
                return null;
            }

            // A level change touches every level-gated quest, which the reverse index cannot enumerate by job; level-ups
            // are rare and a full resolve costs milliseconds, so resolve everything rather than pass jobs as levels.
            var full = diff.OtherChanged
                || offerChanged
                || diff.ChangedJobs.Count > 0
                || diff.ChangedQuestIds.Count > FullResolveThreshold;
            resolved = full
                ? StateResolver.ResolveAll(catalog, snapshot, context)
                : StateResolver.ResolveDependents(states!, ChangedRows(diff, catalog, session.Index!), session.Index!, catalog, snapshot, context, changedFestivals: diff.ChangedFestivals);

            events = QuestEvents.Derive(diff, last!, snapshot, catalog, states!, resolved, now);
        }

        last = snapshot;
        states = resolved;
        lastOffer = offer;
        saves.MarkDirty();
        return new PollResult(snapshot, resolved, context, events, firstPass);
    }

    private void Publish(PollResult result)
    {
        session.SetLive(result.Snapshot, result.States, result.Context);
        session.AddEvents(result.Events);
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

    private void OnLoggingOut()
    {
        if (disposed)
        {
            return;
        }

        if (wasReady && session.Bundle is not null && reader.IsPlayerLoaded())
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

    private void Reset()
    {
        last = null;
        states = null;
        lastOffer = [];
    }

    private readonly record struct PollResult(
        CharacterSnapshot Snapshot,
        IReadOnlyDictionary<uint, QuestEvaluation> States,
        EvalContext Context,
        IReadOnlyList<QuestEvent> Events,
        bool FirstPass);
}
