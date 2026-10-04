using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Game;

/// <summary>What the portrait pack service is doing.</summary>
public enum PortraitPackPhase : byte
{
    Idle,
    Downloading,
    Installing,
    Removing,
}

/// <summary>
/// The optional portrait pack (feature plan v7 F4, decision 8): what this build offers
/// (<see cref="PortraitPackOffer"/>, shipped in <c>Data/portrait_pack.json</c>), the installed pack, and the download,
/// install and removal, each on a worker so the framework thread never waits. Nothing here touches the network until
/// <see cref="StartDownload"/>, which only the player's click calls: Download in the Settings confirmation, or Download
/// portraits in the first-run offer (<c>Ui/PortraitPackOfferWindow.cs</c>). At load the installed pack is read and what an
/// interrupted run left is cleaned up, off the frame. State is read by the Settings row every frame; it is written by
/// the worker, so every field is volatile or swapped whole.
/// </summary>
public sealed class PortraitPackService : IDisposable
{
    private readonly PortraitPackStore store;
    private readonly Func<PortraitPackOffer, IPortraitPackTransport> transport;
    private readonly IPluginLog log;
    private readonly object gate = new();

    /// <summary>Cancelled when the plugin unloads: stops the load worker.</summary>
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? running;
    private volatile PortraitPack? installed;
    private volatile bool damaged;
    private volatile bool loaded;
    private volatile int phase;
    private long received;
    private volatile bool disposed;
    private Task? work;
    private Task? loading;
    private long startedTicks;
    private int arrival;

    /// <param name="transport">Makes the network client for the offer when a confirmed download starts (never before).</param>
    /// <param name="installedChanged">
    /// Called on a worker after the pack changed: installed (a first install, an update or a repair) or removed, and
    /// whether the player started it by picking Game art + pack (<see cref="PortraitPackStatus.ModeAfter"/>).
    /// </param>
    public PortraitPackService(PluginPaths paths, string clientGameVersion, Func<PortraitPackOffer, IPortraitPackTransport> transport, IPluginLog log, Action<PortraitPackChange, bool>? installedChanged = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        InstalledChanged = installedChanged;
        ClientGameVersion = clientGameVersion ?? string.Empty;
        store = new PortraitPackStore(paths.PortraitPackDir);
        Offer = PortraitPackOffer.Load(paths.PortraitPackOfferFile, out var warning);
        if (warning is not null)
        {
            log.Warning("Portrait pack: {Warning}", warning);
        }
    }

    private Action<PortraitPackChange, bool>? InstalledChanged { get; }

    /// <summary>The pack this build offers; null when it offers none.</summary>
    public PortraitPackOffer? Offer { get; }

    /// <summary>The game version the client runs, for the "built for an older game" line.</summary>
    public string ClientGameVersion { get; }

    /// <summary>The installed pack; null while none is (or while it loads at start).</summary>
    public PortraitPack? Installed => installed;

    /// <summary>The installed pack has been read at start (<see cref="LoadAsync"/>): only then does no pack mean none is installed.</summary>
    public bool Loaded => loaded;

    /// <summary>Where things stand for the Settings row.</summary>
    public PortraitPackState State => PortraitPackStatus.Of(Offer, installed, damaged);

    public PortraitPackPhase Phase => (PortraitPackPhase)phase;

    /// <summary>A download, install or removal is running.</summary>
    public bool Busy => Phase != PortraitPackPhase.Idle;

    /// <summary>The running download's progress.</summary>
    public PortraitPackProgress Progress => new(Interlocked.Read(ref received), Offer?.Size ?? 0);

    /// <summary>About how many seconds the running download has left at its pace so far; null until it can tell.</summary>
    public int? SecondsLeft
    {
        get
        {
            var progress = Progress;
            var elapsed = (Environment.TickCount64 - Interlocked.Read(ref startedTicks)) / 1000d;
            if (progress.Received <= 0 || elapsed < 1d || progress.Total <= progress.Received)
            {
                return null;
            }

            return (int)Math.Ceiling((progress.Total - progress.Received) * elapsed / progress.Received);
        }
    }

    /// <summary>What the last download received (bytes, SHA-256), for Copy report after a fingerprint mismatch.</summary>
    public PortraitPackReceipt? LastReceipt { get; private set; }

    /// <summary>The faces of a pack that just landed, once (the status bar's "Portrait pack installed" note); 0 otherwise.</summary>
    public int TakeArrival() => Interlocked.Exchange(ref arrival, 0);

    /// <summary>The last run's result (<see cref="PortraitPackFailure.None"/> after a success), and when it finished.</summary>
    public PortraitPackFailure LastResult { get; private set; }

    /// <summary>Whether the last run was a removal (the result line says "removed" rather than "installed").</summary>
    public bool LastWasRemoval { get; private set; }

    /// <summary>When the last run finished; default when nothing has run since load.</summary>
    public DateTime LastFinishedUtc { get; private set; }

    /// <summary>Whether the installed pack was built for an older game version than the client runs (still used).</summary>
    public bool ForOlderGame => installed is { } pack && PortraitPackStatus.ForOlderGame(pack.GameVersion, ClientGameVersion);

    /// <summary>
    /// Reads the installed pack and cleans up after an interrupted run, on a worker. <see cref="Dispose"/> cancels it
    /// and waits for it, so none of it runs on after unload.
    /// </summary>
    public Task LoadAsync()
    {
        var cancellation = lifetime.Token;
        var task = Task.Run(
            () =>
            {
                try
                {
                    store.CleanUp();
                    var pack = store.Load(out var broken, cancellation);
                    if (disposed)
                    {
                        return;
                    }

                    installed = pack;
                    damaged = broken;
                    loaded = true;
                    if (broken)
                    {
                        log.Warning("Portrait pack: the installed pack is incomplete; Settings offers to download it again or remove it");
                    }
                    else if (pack is not null)
                    {
                        log.Information("Portrait pack {Tag}: {Givers} givers, built for game {Game}", pack.Tag, pack.Givers, pack.GameVersion);
                    }
                }
                catch (OperationCanceledException)
                {
                    // The plugin is unloading.
                }
                catch (Exception ex)
                {
                    if (!disposed)
                    {
                        // Loaded stays false: the first-run offer cannot tell "no pack" from "unread", so it waits until next start.
                        log.Warning(ex, "Portrait pack could not be read; givers show game art and fallbacks, and the first-run pack offer waits until the next start");
                    }
                }
            },
            cancellation);
        loading = task;
        return task;
    }

    /// <summary>
    /// Downloads, checks and installs the offered pack on a worker. The one place the plugin goes online: call it only
    /// from the player's click (the Settings confirmation, or the first-run offer). False when nothing is offered or a run is already going.
    /// <paramref name="askedForPack"/>: the player opened the confirmation by picking Game art + pack, so Giver portraits
    /// switches to it once the pack is in, even over an installed or damaged one.
    /// </summary>
    public bool StartDownload(bool askedForPack = false)
    {
        var offer = Offer;
        if (offer is null || disposed)
        {
            return false;
        }

        CancellationTokenSource cts;
        lock (gate)
        {
            if (Busy)
            {
                return false;
            }

            cts = new CancellationTokenSource();
            running = cts;
            Interlocked.Exchange(ref received, 0);
            Interlocked.Exchange(ref startedTicks, Environment.TickCount64);
            LastReceipt = null;
            phase = (int)PortraitPackPhase.Downloading;
        }

        work = Task.Run(() => DownloadAndInstall(offer, askedForPack, cts.Token));
        return true;
    }

    /// <summary>Stops a running download or install; the pack in use (if any) stays as it was.</summary>
    public void Cancel()
    {
        lock (gate)
        {
            running?.Cancel();
        }
    }

    /// <summary>Removes the installed pack on a worker (asked for by the Settings confirmation).</summary>
    public bool StartRemove()
    {
        lock (gate)
        {
            if (Busy || disposed)
            {
                return false;
            }

            phase = (int)PortraitPackPhase.Removing;
        }

        work = Task.Run(() =>
        {
            var removed = false;
            try
            {
                // Out of use first, so no texture is loading from the folder as it goes.
                installed = null;
                removed = store.Remove();
                if (removed)
                {
                    damaged = false;
                    log.Information("Portrait pack removed");
                }
                else
                {
                    // current.json could not be deleted: nothing was removed, and the pack stays in use.
                    RestoreAfterFailedRemove();
                    log.Warning("Portrait pack could not be removed: its files are in use or read-only; the pack stays installed");
                }
            }
            catch (Exception ex)
            {
                RestoreAfterFailedRemove();
                log.Warning(ex, "Portrait pack could not be removed completely; the rest goes at the next start");
            }
            finally
            {
                Finish(removed ? PortraitPackFailure.None : PortraitPackFailure.DiskError, removal: true);
            }

            if (removed && !disposed)
            {
                InstalledChanged?.Invoke(PortraitPackChange.Removed, false);
            }
        });
        return true;
    }

    /// <summary>After a removal that failed: whatever is still installed is read back, so the row and the plates show it.</summary>
    private void RestoreAfterFailedRemove()
    {
        try
        {
            installed = store.Load(out var broken);
            damaged = broken;
        }
        catch (Exception ex) when (ex is OperationCanceledException or System.IO.IOException or UnauthorizedAccessException)
        {
            installed = null;
        }
    }

    private async Task DownloadAndInstall(PortraitPackOffer offer, bool askedForPack, CancellationToken cancellation)
    {
        // What this install will be, for Giver portraits: read before the pack in use changes.
        var change = PortraitPackStatus.ChangeOf(installed is not null, damaged);
        var result = PortraitPackFailure.DiskError;
        IPortraitPackTransport? source = null;
        var download = store.NewDownloadFile();
        try
        {
            source = transport(offer);
            var progress = new Reporter(this);
            var receipt = new PortraitPackReceipt();
            LastReceipt = receipt;
            log.Information("Portrait pack: downloading {Size} from {Uri} (the player confirmed)", PortraitPackOffer.SizeText(offer.Size), offer.DownloadUri);
            result = await PortraitPackDownload.RunAsync(source, offer, download, progress, cancellation, receipt).ConfigureAwait(false);
            if (result == PortraitPackFailure.None)
            {
                phase = (int)PortraitPackPhase.Installing;
                result = store.Install(download, offer, cancellation, out var pack, out var detail);
                if (result == PortraitPackFailure.None && pack is not null)
                {
                    installed = pack;
                    damaged = false;
                    Interlocked.Exchange(ref arrival, pack.Faces);
                    log.Information("Portrait pack {Tag} installed: {Givers} givers", pack.Tag, pack.Givers);
                }
                else
                {
                    log.Warning("Portrait pack refused at install: {Result} {Detail}", result, detail ?? string.Empty);
                }
            }
            else
            {
                log.Warning("Portrait pack download stopped: {Result}", result);
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Portrait pack download failed");
            result = PortraitPackFailure.DiskError;
        }
        finally
        {
            (source as IDisposable)?.Dispose();
            PortraitPackStore.TryDelete(download);
            lock (gate)
            {
                running?.Dispose();
                running = null;
            }

            Finish(result, removal: false);
        }

        if (result == PortraitPackFailure.None && !disposed)
        {
            InstalledChanged?.Invoke(change, askedForPack);
        }
    }

    private void Finish(PortraitPackFailure result, bool removal)
    {
        LastResult = result;
        LastWasRemoval = removal;
        LastFinishedUtc = DateTime.UtcNow;
        phase = (int)PortraitPackPhase.Idle;
    }

    /// <summary>
    /// Cancels a running download and the load worker and waits a moment (two seconds at most, together) for both, so
    /// nothing of them runs on after unload.
    /// </summary>
    public void Dispose()
    {
        disposed = true;
        Cancel();
        lifetime.Cancel();
        var deadline = Environment.TickCount64 + 2000;
        foreach (var task in new[] { work, loading })
        {
            if (task is null)
            {
                continue;
            }

            try
            {
                task.Wait(TimeSpan.FromMilliseconds(Math.Max(0, deadline - Environment.TickCount64)));
            }
            catch (AggregateException)
            {
                // Logged by the worker, or cancelled before it started.
            }
        }

        lifetime.Dispose();
    }

    /// <summary>Writes progress where the Settings row reads it (no synchronisation context: the worker reports straight in).</summary>
    private sealed class Reporter(PortraitPackService owner) : IProgress<PortraitPackProgress>
    {
        public void Report(PortraitPackProgress value) => Interlocked.Exchange(ref owner.received, value.Received);
    }
}
