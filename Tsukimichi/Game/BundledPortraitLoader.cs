using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Portraits;

namespace Tsukimichi.Game;

/// <summary>
/// The quest-giver photos that ship with the plugin (<see cref="BundledPortraits"/>): read once at start on a worker, so
/// the framework thread never waits, after the copy 1.20–1.22 downloaded into the config folder is deleted (once: the
/// photos now come with the plugin). Nothing here goes online. <see cref="Pack"/> is null until the read lands, or when
/// the folder is missing or incomplete (logged; givers then show game art and fallbacks).
/// </summary>
public sealed class BundledPortraitLoader : IDisposable
{
    private readonly string folder;
    private readonly string downloadedDir;
    private readonly IPluginLog log;
    private readonly CancellationTokenSource lifetime = new();
    private volatile PortraitPack? pack;
    private Task? loading;

    /// <param name="folder">The plugin's <c>assets/portraits</c> folder.</param>
    /// <param name="downloadedDir">The config folder's <c>portraits/</c>, where 1.20–1.22 installed the downloaded pack.</param>
    public BundledPortraitLoader(string folder, string downloadedDir, IPluginLog log)
    {
        this.folder = folder ?? throw new ArgumentNullException(nameof(folder));
        this.downloadedDir = downloadedDir ?? throw new ArgumentNullException(nameof(downloadedDir));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>The bundled photos; null while they load, or when they could not be read.</summary>
    public PortraitPack? Pack => pack;

    /// <summary>Deletes the old downloaded pack and reads the bundled one, on a worker.</summary>
    public Task LoadAsync()
    {
        var cancellation = lifetime.Token;
        loading = Task.Run(
            () =>
            {
                try
                {
                    var removed = BundledPortraits.RemoveDownloaded(downloadedDir);
                    if (removed > 0)
                    {
                        log.Information("Portrait photos now ship with Tsukimichi: removed the portrait pack downloaded before ({Count} entries in {Folder})", removed, downloadedDir);
                    }

                    var read = BundledPortraits.Load(folder, out var problem, cancellation);
                    if (read is null)
                    {
                        log.Warning("Portrait photos could not be read ({Problem}); givers show game art and fallbacks", problem ?? "unknown");
                        return;
                    }

                    pack = read;
                    log.Information("Portrait photos: {Givers} givers, {Faces} photos, built for game {Game}", read.Givers, read.Faces, read.GameVersion);
                }
                catch (OperationCanceledException)
                {
                    // The plugin is unloading.
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "Portrait photos could not be read; givers show game art and fallbacks");
                }
            },
            cancellation);
        return loading;
    }

    /// <summary>Stops the read and waits a moment (two seconds at most) for it, so none of it runs on after unload.</summary>
    public void Dispose()
    {
        lifetime.Cancel();
        try
        {
            loading?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Logged by the worker, or cancelled before it started.
        }

        lifetime.Dispose();
    }
}
