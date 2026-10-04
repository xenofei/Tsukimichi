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
/// <see cref="StartDownload"/>, which only the Settings confirmation calls. At load the installed pack is read and what an
/// interrupted run left is cleaned up, off the frame. State is read by the Settings row every frame; it is written by
/// the worker, so every field is volatile or swapped whole.
/// </summary>
public sealed class PortraitPackService : IDisposable
{
    private readonly PortraitPackStore store;
    private readonly Func<PortraitPackOffer, IPortraitPackTransport> transport;
    private readonly IPluginLog log;
    private readonly object gate = new();
    private CancellationTokenSource? running;
    private volatile PortraitPack? installed;
    private volatile bool damaged;
    private volatile int phase;
    private long received;
    private volatile bool disposed;
    private Task? work;

    /// <param name="transport">Makes the network client for the offer when a confirmed download starts (never before).</param>
    /// <param name="installedChanged">Called on a worker after a pack is installed (true) or removed (false).</param>
    public PortraitPackService(PluginPaths paths, string clientGameVersion, Func<PortraitPackOffer, IPortraitPackTransport> transport, IPluginLog log, Action<bool>? installedChanged = null)
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

    private Action<bool>? InstalledChanged { get; }

    /// <summary>The pack this build offers; null when it offers none.</summary>
    public PortraitPackOffer? Offer { get; }

    /// <summary>The game version the client runs, for the "built for an older game" line.</summary>
    public string ClientGameVersion { get; }

    /// <summary>The installed pack; null while none is (or while it loads at start).</summary>
    public PortraitPack? Installed => installed;

    /// <summary>Where things stand for the Settings row.</summary>
    public PortraitPackState State => PortraitPackStatus.Of(Offer, installed, damaged);

    public PortraitPackPhase Phase => (PortraitPackPhase)phase;

    /// <summary>A download, install or removal is running.</summary>
    public bool Busy => Phase != PortraitPackPhase.Idle;

    /// <summary>The running download's progress.</summary>
    public PortraitPackProgress Progress => new(Interlocked.Read(ref received), Offer?.Size ?? 0);

    /// <summary>The last run's result (<see cref="PortraitPackFailure.None"/> after a success), and when it finished.</summary>
    public PortraitPackFailure LastResult { get; private set; }

    /// <summary>Whether the last run was a removal (the result line says "removed" rather than "installed").</summary>
    public bool LastWasRemoval { get; private set; }

    /// <summary>When the last run finished; default when nothing has run since load.</summary>
    public DateTime LastFinishedUtc { get; private set; }

    /// <summary>Whether the installed pack was built for an older game version than the client runs (still used).</summary>
    public bool ForOlderGame => installed is { } pack && PortraitPackStatus.ForOlderGame(pack.GameVersion, ClientGameVersion);

    /// <summary>Reads the installed pack and cleans up after an interrupted run, on a worker.</summary>
    public Task LoadAsync() => Task.Run(() =>
    {
        try
        {
            store.CleanUp();
            installed = store.Load(out var broken);
            damaged = broken;
            if (broken)
            {
                log.Warning("Portrait pack: the installed pack is incomplete; Settings offers to download it again or remove it");
            }
            else if (installed is { } pack)
            {
                log.Information("Portrait pack {Tag}: {Givers} givers, built for game {Game}", pack.Tag, pack.Givers, pack.GameVersion);
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Portrait pack could not be read; givers show game art and fallbacks");
        }
    });

    /// <summary>
    /// Downloads, checks and installs the offered pack on a worker. The one place the plugin goes online: call it only
    /// from the player's confirmation. False when nothing is offered or a run is already going.
    /// </summary>
    public bool StartDownload()
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
            phase = (int)PortraitPackPhase.Downloading;
        }

        work = Task.Run(() => DownloadAndInstall(offer, cts.Token));
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
                installed = null;
                removed = store.Remove();
                damaged = !removed && damaged;
                log.Information("Portrait pack removed");
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Portrait pack could not be removed completely; the rest goes at the next start");
            }
            finally
            {
                Finish(removed ? PortraitPackFailure.None : PortraitPackFailure.DiskError, removal: true);
            }

            if (removed && !disposed)
            {
                InstalledChanged?.Invoke(false);
            }
        });
        return true;
    }

    private async Task DownloadAndInstall(PortraitPackOffer offer, CancellationToken cancellation)
    {
        var result = PortraitPackFailure.DiskError;
        IPortraitPackTransport? source = null;
        var download = store.NewDownloadFile();
        try
        {
            source = transport(offer);
            var progress = new Reporter(this);
            log.Information("Portrait pack: downloading {Size} from {Uri} (the player confirmed)", PortraitPackOffer.SizeText(offer.Size), offer.DownloadUri);
            result = await PortraitPackDownload.RunAsync(source, offer, download, progress, cancellation).ConfigureAwait(false);
            if (result == PortraitPackFailure.None)
            {
                phase = (int)PortraitPackPhase.Installing;
                result = store.Install(download, offer, cancellation, out var pack, out var detail);
                if (result == PortraitPackFailure.None && pack is not null)
                {
                    installed = pack;
                    damaged = false;
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
            InstalledChanged?.Invoke(true);
        }
    }

    private void Finish(PortraitPackFailure result, bool removal)
    {
        LastResult = result;
        LastWasRemoval = removal;
        LastFinishedUtc = DateTime.UtcNow;
        phase = (int)PortraitPackPhase.Idle;
    }

    /// <summary>Cancels a running download and waits a moment for its worker, so nothing of it runs on after unload.</summary>
    public void Dispose()
    {
        disposed = true;
        Cancel();
        try
        {
            work?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Logged by the worker.
        }
    }

    /// <summary>Writes progress where the Settings row reads it (no synchronisation context: the worker reports straight in).</summary>
    private sealed class Reporter(PortraitPackService owner) : IProgress<PortraitPackProgress>
    {
        public void Report(PortraitPackProgress value) => Interlocked.Exchange(ref owner.received, value.Received);
    }
}
