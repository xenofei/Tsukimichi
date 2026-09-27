using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Game;

/// <summary>
/// Once per <see cref="Configuration.PollInterval"/> while a character is ready and the catalog is built: capture,
/// diff, re-resolve what changed, emit events, publish to <see cref="SessionState"/> and persist (debounced).
/// Exceptions back the interval off exponentially to <see cref="MaxBackoff"/> with a single warning.
/// Runs entirely inside <see cref="IFramework.Update"/>.
/// </summary>
public sealed class StatePoller : IDisposable
{
    public static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    /// <summary>Above this many changed quests a full resolve is cheaper than walking dependents.</summary>
    public const int FullResolveThreshold = 200;

    private const uint QuestRowBase = 0x10000;

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IPluginLog log;
    private readonly GameStateReader reader;
    private readonly SnapshotService snapshots;
    private readonly SessionState session;
    private readonly Configuration config;

    private readonly SaveDebouncer saves = new(SaveInterval);
    private readonly PollBackoff backoff = new(TimeSpan.FromSeconds(2), MaxBackoff);
    private readonly HashSet<byte> pendingLevelChanges = [];

    private CharacterSnapshot? last;
    private IReadOnlyDictionary<uint, QuestEvaluation>? states;
    private HashSet<ushort> lastOffer = [];
    private DateTime lastPollUtc = DateTime.MinValue;
    private bool wasReady;
    private bool warned;
    private bool disposed;

    public StatePoller(
        IFramework framework,
        IClientState clientState,
        ICondition condition,
        IPluginLog log,
        GameStateReader reader,
        SnapshotService snapshots,
        SessionState session,
        Configuration config)
    {
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.config = config ?? throw new ArgumentNullException(nameof(config));

        snapshots.LoggingOut += OnLoggingOut;
        framework.Update += OnUpdate;
    }

    /// <summary>Writes the last capture if it changed since the last save.</summary>
    public void Flush()
    {
        if (last is null || !saves.Pending)
        {
            return;
        }

        try
        {
            snapshots.Save(last);
            saves.MarkSaved(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Snapshot save failed for {ContentId}", last.ContentId);
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
                session.ClearLive();
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
        try
        {
            Poll(now);
            if (backoff.IsActive)
            {
                log.Information("Poller recovered after {Failures} failure(s)", backoff.Failures);
            }

            backoff.Reset();
            warned = false;
            session.SetPollerHealthy(true);
        }
        catch (Exception ex)
        {
            backoff.RecordFailure();
            session.SetPollerHealthy(false);
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "Game state read failed; retrying in {Seconds} s and backing off to {Max} s", backoff.Current.TotalSeconds, MaxBackoff.TotalSeconds);
            }
        }

        if (saves.ShouldSave(now))
        {
            Flush();
        }
    }

    private void Poll(DateTime now)
    {
        var bundle = session.Bundle!;
        var catalog = bundle.Catalog;
        var snapshot = reader.Capture(catalog, bundle.Jobs);
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
                return;
            }

            // Level sync inside a duty must not churn the level-indexed rows; apply those changes after leaving.
            IReadOnlyCollection<byte> levelChanges;
            if (condition[ConditionFlag.BoundByDuty])
            {
                pendingLevelChanges.UnionWith(diff.ChangedJobs);
                levelChanges = [];
            }
            else
            {
                pendingLevelChanges.UnionWith(diff.ChangedJobs);
                levelChanges = [.. pendingLevelChanges];
                pendingLevelChanges.Clear();
            }

            var full = diff.OtherChanged || offerChanged || diff.ChangedQuestIds.Count > FullResolveThreshold;
            resolved = full
                ? StateResolver.ResolveAll(catalog, snapshot, context)
                : StateResolver.ResolveDependents(states!, ChangedRows(diff, catalog, session.Index!), session.Index!, catalog, snapshot, context, levelChanges, diff.ChangedFestivals);

            events = QuestEvents.Derive(diff, last!, snapshot, catalog, states!, resolved, now);
        }

        last = snapshot;
        states = resolved;
        lastOffer = offer;
        saves.MarkDirty();

        session.SetLive(snapshot, resolved, context);
        session.AddEvents(events);

        if (firstPass)
        {
            Flush();
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
            if (catalog.TryGet(questId, out var quest))
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
            try
            {
                Poll(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                log.Debug(ex, "Logout capture skipped");
            }
        }

        Flush();
    }

    private void Reset()
    {
        last = null;
        states = null;
        lastOffer = [];
        pendingLevelChanges.Clear();
    }
}
