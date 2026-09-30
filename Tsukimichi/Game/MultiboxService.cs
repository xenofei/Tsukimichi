using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Multibox;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>
/// Multibox sharing (D11). Every game client runs its own copy of the plugin, and they share the config folder; this
/// service is how one copy sees the others, through that folder only (no other process's memory is read, nothing is
/// sent to another client, and Dalamud's IPC does not cross processes anyway).
/// <list type="bullet">
/// <item>Heartbeat: while a character is logged in here, <c>characters/&lt;contentId&gt;.live.json</c> holds this process
/// id, the login time, the name and the world, refreshed every <see cref="LiveClients.RefreshInterval"/>, and it is
/// deleted at logout and unload. Another client reads a heartbeat older than <see cref="LiveClients.StaleAfter"/> as gone.</item>
/// <item>Watching: a <see cref="FileSystemWatcher"/> on <c>characters/</c> and <c>user/</c>, with a look every
/// <see cref="PollInterval"/> because watchers drop events. Snapshots another client saved are read on a worker and
/// handed to the session on the framework thread; the stored character on view is resolved again on a worker.</item>
/// <item>Ownership: if the files ever say another client holds this client's own character (impossible in the game),
/// the newer login keeps writing and the other stops, with a warning (<see cref="LiveClients.Decide"/>).</item>
/// </list>
/// All file work runs on one background loop; the framework thread only swaps in finished results.
/// </summary>
public sealed class MultiboxService : IDisposable
{
    /// <summary>The polling fallback: how often the folder is looked at even when the watcher reported nothing.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan LoopTick = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan WatcherRetry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TempSweepInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TempMaxAge = TimeSpan.FromHours(1);
    private static readonly TimeSpan DisposeWait = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LoopWarningInterval = TimeSpan.FromMinutes(1);

    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly SessionState session;
    private readonly SnapshotService snapshots;
    private readonly PluginPaths paths;
    private readonly ClientIdentity me;
    private readonly CancellationTokenSource lifetime = new();
    private readonly ConcurrentQueue<ScanOutcome> outcomes = new();
    private readonly Task loop;

    // Set on the framework thread, read by the loop.
    private LiveInfo? live;

    // Flags raised by the watchers (thread pool), the framework thread and "Delete all data"; taken by the loop.
    private int charactersDirty = 1;
    private int userDirty;
    private int forceBeat;
    private int watcherBroken;

    // Raised by the loop, taken on the framework thread.
    private int userChanged;

    /// <summary>The logged-in character another client holds with a newer claim; 0 while none. Written by the loop.</summary>
    private ulong blockedContentId;

    // Loop-only state.
    private readonly JsonSnapshotStore workerStore;
    private Dictionary<ulong, FileStamp> known = [];
    private IReadOnlyDictionary<ulong, Heartbeat> lastPublished = new Dictionary<ulong, Heartbeat>();
    private readonly Dictionary<string, FileStamp?> userStamps = new(StringComparer.OrdinalIgnoreCase);
    private ulong? beatContentId;
    private DateTime beatSinceUtc;
    private DateTime nextBeatUtc;
    private DateTime nextScanUtc;
    private DateTime nextTempSweepUtc;
    private DateTime nextWatcherRetryUtc;
    private bool duplicateWarned;
    private DateTime lastLoopWarningUtc = DateTime.MinValue;
    private FileSystemWatcher? charactersWatcher;
    private FileSystemWatcher? userWatcher;

    // Framework-thread state.
    private Task<StoredRefresh>? refresh;
    private bool disposed;

    public MultiboxService(IFramework framework, IPluginLog log, SessionState session, SnapshotService snapshots, PluginPaths paths)
    {
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        me = session.Me;
        workerStore = new JsonSnapshotStore(paths.ConfigDir);

        snapshots.MayWrite = MayWrite;
        session.DataDeleted += OnDataDeleted;
        framework.Update += OnUpdate;
        StartWatchers();
        loop = Task.Run(() => RunAsync(lifetime.Token));
    }

    /// <summary>Raised on the framework thread when <c>user/pins.json</c> or <c>user/overrides.json</c> changed on disk (another client's save, or this one's).</summary>
    public event Action? UserFilesChanged;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        framework.Update -= OnUpdate;
        session.DataDeleted -= OnDataDeleted;
        snapshots.MayWrite = null;
        lifetime.Cancel();
        try
        {
            if (!loop.Wait(DisposeWait))
            {
                log.Warning("Multibox loop did not stop within {Seconds} s", DisposeWait.TotalSeconds);
            }
        }
        catch (AggregateException)
        {
            // Cancelled; anything else was logged by the loop.
        }

        StopWatchers();

        // Unloading: this client no longer holds anyone. Its own heartbeat goes; another client's never does.
        var own = beatContentId ?? Volatile.Read(ref live)?.ContentId;
        if (own is { } id)
        {
            try
            {
                HeartbeatFile.DeleteIfAllowed(paths.CharactersDir, id, me, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                log.Debug(ex, "Heartbeat of {ContentId} not removed at unload", id);
            }
        }

        lifetime.Dispose();
    }

    /// <summary>Whether this client may write a character's files: false only while another client holds its own character with a newer claim.</summary>
    private bool MayWrite(ulong contentId) => Volatile.Read(ref blockedContentId) != contentId;

    private void OnDataDeleted()
    {
        // "Delete all data" removed this client's heartbeat with the rest: write it again now, and look at the folder.
        Interlocked.Exchange(ref forceBeat, 1);
        Interlocked.Exchange(ref charactersDirty, 1);
    }

    // ---------------------------------------------------------------- framework thread

    private void OnUpdate(IFramework _)
    {
        if (disposed)
        {
            return;
        }

        try
        {
            PublishLive();
            DrainOutcomes();
            TakeRefresh();
            if (Interlocked.Exchange(ref userChanged, 0) == 1)
            {
                UserFilesChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Multibox update failed");
        }
    }

    /// <summary>Tells the loop which character is logged in here; the loop writes its heartbeat.</summary>
    private void PublishLive()
    {
        var snapshot = session.LiveSnapshot;
        var current = snapshot is null ? null : new LiveInfo(snapshot.ContentId, snapshot.Name, snapshot.World);
        if (current != Volatile.Read(ref live))
        {
            Volatile.Write(ref live, current);
        }
    }

    private void DrainOutcomes()
    {
        while (outcomes.TryDequeue(out var outcome))
        {
            session.SetLiveElsewhere(outcome.LiveElsewhere);
            var taken = snapshots.ApplyExternal(outcome.Changed, outcome.Removed, session.LiveContentId);
            foreach (var fresh in taken)
            {
                if (session.PrepareStoredRefresh(fresh) is { } prepared)
                {
                    StartRefresh(fresh, prepared.Bundle, prepared.Context);
                }
            }
        }
    }

    /// <summary>Resolves a newer copy of the stored character on view on a worker; the newest request wins.</summary>
    private void StartRefresh(CharacterSnapshot fresh, CatalogBundle bundle, EvalContext context)
    {
        var dir = paths.CharactersDir;
        refresh = Task.Run(() =>
        {
            var states = StateResolver.ResolveAll(bundle.Catalog, fresh, context);
            // Derived data: an unreadable sidecar simply reads as unknown accepted times.
            var acceptedSince = AcceptedSince.Load(AcceptedSince.PathFor(dir, fresh.ContentId));
            var abandoned = AbandonedLedger.Load(AbandonedLedger.PathFor(dir, fresh.ContentId));
            return new StoredRefresh(fresh, bundle, context, states, acceptedSince, abandoned);
        });
    }

    private void TakeRefresh()
    {
        if (refresh is not { IsCompleted: true } done)
        {
            return;
        }

        refresh = null;
        if (!done.IsCompletedSuccessfully)
        {
            log.Warning(done.Exception?.GetBaseException(), "A character saved by another game client could not be evaluated");
            return;
        }

        var r = done.Result;
        session.ApplyStoredRefresh(r.Snapshot, r.Bundle, r.Context, r.States, r.AcceptedSince, r.Abandoned);
    }

    // ---------------------------------------------------------------- loop (worker)

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                Tick(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                var now = DateTime.UtcNow;
                if (now - lastLoopWarningUtc >= LoopWarningInterval)
                {
                    lastLoopWarningUtc = now;
                    log.Warning(ex, "Multibox: reading or writing the shared folder failed; retrying");
                }
            }

            try
            {
                await Task.Delay(LoopTick, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void Tick(DateTime now)
    {
        Beat(now);
        if (Interlocked.Exchange(ref charactersDirty, 0) == 1 || now >= nextScanUtc)
        {
            nextScanUtc = now + PollInterval;
            Scan(now);
            PollUserFiles();
        }

        if (Interlocked.Exchange(ref userDirty, 0) == 1)
        {
            Interlocked.Exchange(ref userChanged, 1);
        }

        if (now >= nextTempSweepUtc)
        {
            nextTempSweepUtc = now + TempSweepInterval;
            AtomicFile.DeleteStaleTemps(paths.CharactersDir, TempMaxAge, now);
            AtomicFile.DeleteStaleTemps(paths.UserDir, TempMaxAge, now);
        }

        if (Volatile.Read(ref watcherBroken) == 1 && now >= nextWatcherRetryUtc)
        {
            nextWatcherRetryUtc = now + WatcherRetry;
            StopWatchers();
            StartWatchers();
        }
    }

    /// <summary>Keeps this client's heartbeat: switches it with the logged-in character, refreshes it, and settles a clash.</summary>
    private void Beat(DateTime now)
    {
        var current = Volatile.Read(ref live);
        if (current?.ContentId != beatContentId)
        {
            if (beatContentId is { } previous)
            {
                // Logged out, or another character: this client no longer holds the previous one.
                HeartbeatFile.DeleteIfAllowed(paths.CharactersDir, previous, me, now);
                Interlocked.Exchange(ref charactersDirty, 1);
            }

            beatContentId = current?.ContentId;
            beatSinceUtc = now;
            nextBeatUtc = DateTime.MinValue;
            duplicateWarned = false;
            Volatile.Write(ref blockedContentId, 0UL);
        }

        if (Interlocked.Exchange(ref forceBeat, 0) == 1)
        {
            nextBeatUtc = DateTime.MinValue;
        }

        if (current is null || now < nextBeatUtc)
        {
            return;
        }

        nextBeatUtc = now + LiveClients.RefreshInterval;
        var onDisk = HeartbeatFile.TryRead(paths.CharactersDir, current.ContentId);
        if (LiveClients.Decide(onDisk, me, beatSinceUtc, now) == Ownership.Theirs)
        {
            Volatile.Write(ref blockedContentId, current.ContentId);
            if (!duplicateWarned)
            {
                duplicateWarned = true;
                log.Warning(
                    "{Name} ({ContentId}) is also logged in on another game client (process {Process}) since a later time; this client stops saving it",
                    current.Name,
                    current.ContentId,
                    onDisk!.ProcessId);
            }

            return;
        }

        if (Volatile.Read(ref blockedContentId) == current.ContentId)
        {
            log.Information("{Name} ({ContentId}) is no longer held by another game client; this client saves it again", current.Name, current.ContentId);
            duplicateWarned = false;
        }

        Volatile.Write(ref blockedContentId, 0UL);
        HeartbeatFile.Write(paths.CharactersDir, new Heartbeat(current.ContentId, current.Name, current.World, me.ProcessId, me.ClientId, beatSinceUtc, now));
    }

    private void Scan(DateTime now)
    {
        var skip = beatContentId is { } own ? new HashSet<ulong> { own } : [];
        var result = FolderScan.Run(paths.CharactersDir, known, workerStore, skip);
        known = new Dictionary<ulong, FileStamp>(result.Stamps);
        foreach (var warning in result.Warnings)
        {
            log.Warning("Multibox: {Warning}", warning);
        }

        var liveElsewhere = LiveClients.LiveElsewhere(result.Heartbeats, me, now, beatContentId);
        var sameLive = LiveClients.SameCharacters(liveElsewhere, lastPublished);
        if (sameLive && result.Changed.Count == 0 && result.Removed.Count == 0)
        {
            return;
        }

        foreach (var (id, heartbeat) in liveElsewhere)
        {
            if (!lastPublished.ContainsKey(id))
            {
                log.Information("Multibox: {Name} ({ContentId}) is live in another game client (process {Process})", heartbeat.Name, id, heartbeat.ProcessId);
            }
        }

        lastPublished = liveElsewhere;
        outcomes.Enqueue(new ScanOutcome(liveElsewhere, result.Changed, result.Removed));
    }

    /// <summary>The polling fallback for <c>user/</c>: a pins or overrides file whose stamp moved counts as changed.</summary>
    private void PollUserFiles()
    {
        foreach (var path in new[] { paths.PinsFile, paths.OverridesFile })
        {
            FileStamp? stamp = null;
            try
            {
                var info = new FileInfo(path);
                if (info.Exists)
                {
                    stamp = new FileStamp(info.LastWriteTimeUtc, info.Length);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (userStamps.TryGetValue(path, out var before) && before != stamp)
            {
                Interlocked.Exchange(ref userDirty, 1);
            }

            userStamps[path] = stamp;
        }
    }

    // ---------------------------------------------------------------- watchers

    private void StartWatchers()
    {
        Volatile.Write(ref watcherBroken, 0);
        charactersWatcher = Watch(paths.CharactersDir, name =>
        {
            if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Exchange(ref charactersDirty, 1);
            }
        });
        userWatcher = Watch(paths.UserDir, name =>
        {
            if (string.Equals(name, Path.GetFileName(paths.PinsFile), StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, Path.GetFileName(paths.OverridesFile), StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Exchange(ref userDirty, 1);
            }
        });
    }

    private void StopWatchers()
    {
        charactersWatcher?.Dispose();
        charactersWatcher = null;
        userWatcher?.Dispose();
        userWatcher = null;
    }

    /// <summary>A watcher on one folder (created if missing); null when the system refuses one, the polling then does the work.</summary>
    private FileSystemWatcher? Watch(string directory, Action<string> changed)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                InternalBufferSize = 64 * 1024,
            };
            watcher.Changed += (_, e) => changed(e.Name ?? string.Empty);
            watcher.Created += (_, e) => changed(e.Name ?? string.Empty);
            watcher.Deleted += (_, e) => changed(e.Name ?? string.Empty);
            watcher.Renamed += (_, e) => changed(e.Name ?? string.Empty);
            watcher.Error += (_, _) =>
            {
                // Overflow or the folder went away: look now, and set the watcher up again later.
                Interlocked.Exchange(ref charactersDirty, 1);
                Interlocked.Exchange(ref userDirty, 1);
                Volatile.Write(ref watcherBroken, 1);
            };
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException)
        {
            log.Debug(ex, "Multibox: no file watcher on {Directory}; polling every {Seconds} s instead", directory, PollInterval.TotalSeconds);
            return null;
        }
    }

    private sealed record LiveInfo(ulong ContentId, string Name, uint World);

    private sealed record ScanOutcome(IReadOnlyDictionary<ulong, Heartbeat> LiveElsewhere, IReadOnlyList<CharacterSnapshot> Changed, IReadOnlyList<ulong> Removed);

    private sealed record StoredRefresh(
        CharacterSnapshot Snapshot,
        CatalogBundle Bundle,
        EvalContext Context,
        IReadOnlyDictionary<uint, QuestEvaluation> States,
        IReadOnlyDictionary<ushort, DateTime> AcceptedSince,
        IReadOnlyDictionary<ushort, AbandonedEntry> Abandoned);
}
