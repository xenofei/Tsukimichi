using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Multibox;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Game;

/// <summary>
/// Characters in other XIVLauncher roaming folders (plan v7, 1.21.0 P3), for the All characters roster: every
/// <see cref="PollInterval"/>, on a worker, each folder in <see cref="Configuration.LinkedCharacterFolders"/> is looked at
/// as this client's own multibox scan looks at its own (<see cref="FolderScan.Run"/>): the <c>characters\*.live.json</c>
/// heartbeats are read every pass, and a <c>characters\&lt;id&gt;.json</c> snapshot only when its size or write time moved
/// since the last pass. Read only: nothing in a linked folder is ever written, deleted, moved or quarantined
/// (<see cref="JsonSnapshotStore.LoadShared"/>). A character this folder also holds is left to this folder's own copy.
/// The framework thread only swaps in the finished list (<see cref="Characters"/>, <see cref="Revision"/>).
/// </summary>
public sealed class LinkedFolderService : IDisposable
{
    /// <summary>How often the linked folders are looked at: as often as a live row should move ("every few seconds").</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan DisposeWait = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WarningInterval = TimeSpan.FromMinutes(10);

    private readonly Configuration settings;
    private readonly SessionState session;
    private readonly IPluginLog log;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task loop;
    private readonly LinkedFolders folders = new();
    private readonly Dictionary<string, JsonSnapshotStore> stores = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> warnedMs = new(StringComparer.Ordinal);

    // Handed from the framework thread to the loop: the folders to read and this folder's own character ids.
    private volatile IReadOnlyList<string> wanted = [];
    private volatile IReadOnlySet<ulong> ownIds = new HashSet<ulong>();
    private int wake;

    // Handed from the loop to the framework thread.
    private volatile IReadOnlyList<LinkedCharacter>? landed;
    private IReadOnlyList<LinkedCharacter> shown = [];
    private int ownVersion = -1;

    public LinkedFolderService(Configuration settings, SessionState session, string ownConfigDir, IPluginLog log)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        ArgumentException.ThrowIfNullOrWhiteSpace(ownConfigDir);
        OwnConfigDir = ownConfigDir;
        loop = Task.Run(() => RunAsync(lifetime.Token));
    }

    /// <summary>Bumps whenever <see cref="Characters"/> reads differently (framework thread; takes the loop's latest list).</summary>
    public int Revision
    {
        get
        {
            Update();
            return revision;
        }
    }

    private int revision;

    /// <summary>The characters of the linked folders, as of the last pass (framework thread).</summary>
    public IReadOnlyList<LinkedCharacter> Characters
    {
        get
        {
            Update();
            return shown;
        }
    }

    /// <summary>This client's own Tsukimichi config folder (never linked to itself).</summary>
    public string OwnConfigDir { get; }

    /// <summary>
    /// Links a folder the player typed or picked (<see cref="LinkedFolders.Resolve"/>: the Tsukimichi folder, its
    /// <c>pluginConfigs</c> or the roaming folder above); false when it holds no characters folder, is this client's own
    /// or is linked already. The caller saves the settings.
    /// </summary>
    public bool Link(string path)
    {
        if (LinkedFolders.Resolve(path) is not { } folder
            || string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(OwnConfigDir)), folder, StringComparison.OrdinalIgnoreCase)
            || settings.LinkedCharacterFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        settings.LinkedCharacterFolders.Add(folder);
        Refresh();
        return true;
    }

    /// <summary>Unlinks a folder; its characters leave the roster on the next pass. The caller saves the settings.</summary>
    public void Unlink(string folder)
    {
        settings.LinkedCharacterFolders.RemoveAll(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase));
        Refresh();
    }

    /// <summary>Whether any folder is linked.</summary>
    public bool Any => settings.LinkedCharacterFolders.Count > 0;

    /// <summary>Reads the folders again on the next tick (a folder linked or unlinked in Settings).</summary>
    public void Refresh() => Interlocked.Exchange(ref wake, 1);

    /// <summary>
    /// Auto-detect: Tsukimichi folders of the roaming folders beside this client's own and under <c>%AppData%</c>
    /// (where <c>XIVLauncher</c>, <c>XIVLauncher2</c> and the like sit), other than this one's.
    /// </summary>
    public static List<string> Detect(string ownConfigDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownConfigDir);
        var roots = new List<string>();
        var roaming = Directory.GetParent(ownConfigDir)?.Parent;
        if (roaming?.Parent is { } beside)
        {
            roots.Add(beside.FullName);
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (appData.Length > 0)
        {
            roots.Add(appData);
        }

        return LinkedFolders.Candidates(ownConfigDir, roots.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Takes the loop's latest list on the framework thread and hands the loop what to read next.</summary>
    private void Update()
    {
        var folderList = settings.LinkedCharacterFolders;
        if (!SameFolders(folderList, wanted))
        {
            wanted = [.. folderList];
            Refresh();
        }

        if (ownVersion != session.RosterVersion)
        {
            ownVersion = session.RosterVersion;
            ownIds = session.Characters.Select(static c => c.ContentId).Append(session.LiveContentId ?? 0).ToHashSet();
        }

        if (Interlocked.Exchange(ref landed, null) is not { } next)
        {
            return;
        }

        if (next.Count == shown.Count && next.Zip(shown).All(static p => Same(p.First, p.Second)))
        {
            return;
        }

        shown = next;
        revision++;
    }

    /// <summary>The same folders in the same order (no allocation: it runs every frame the roster is read).</summary>
    private static bool SameFolders(List<string> a, IReadOnlyList<string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Same(LinkedCharacter a, LinkedCharacter b) =>
        ReferenceEquals(a.Snapshot, b.Snapshot) && a.Live == b.Live && string.Equals(a.Folder, b.Folder, StringComparison.OrdinalIgnoreCase);

    private async Task RunAsync(CancellationToken token)
    {
        var nextMs = 0L;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var now = Environment.TickCount64;
                if (now >= nextMs || Interlocked.Exchange(ref wake, 0) == 1)
                {
                    nextMs = now + (long)PollInterval.TotalMilliseconds;
                    Pass();
                }

                await Task.Delay(250, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Warn("loop", ex, "Linked character folders could not be read this pass");
            }
        }
    }

    /// <summary>One look at every linked folder (worker).</summary>
    private void Pass()
    {
        var list = wanted;

        // Each folder on its own (LinkedFolders.ScanAll): one that throws keeps its last snapshots and loses its
        // heartbeats (no stale "Live"), one that is gone loses its characters, and the others are read all the same.
        folders.ScanAll(list, folder =>
        {
            var dir = Path.Combine(folder, LinkedFolders.CharactersFolder);
            if (!Directory.Exists(dir))
            {
                stores.Remove(folder);
                return null;
            }

            if (!stores.TryGetValue(folder, out var store))
            {
                stores[folder] = store = new JsonSnapshotStore(folder);
            }

            var result = FolderScan.Run(dir, folders.KnownStamps(folder), store, new HashSet<ulong>());
            foreach (var warning in result.Warnings)
            {
                Warn(warning, null, "Linked folder " + folder + ": " + warning);
            }

            return result;
        }, (folder, ex) => Warn("folder " + folder, ex, "Linked folder " + folder + " could not be read this pass"));

        landed = folders.Characters(list, ownIds, session.Me, DateTime.UtcNow);
    }

    private void Warn(string key, Exception? ex, string message)
    {
        var now = Environment.TickCount64;
        if (warnedMs.TryGetValue(key, out var last) && now - last < (long)WarningInterval.TotalMilliseconds)
        {
            return;
        }

        warnedMs[key] = now;
        log.Warning(ex, "{Message}", message);
    }

    public void Dispose()
    {
        lifetime.Cancel();
        try
        {
            loop.Wait(DisposeWait);
        }
        catch (AggregateException)
        {
            // Cancelled or failed: either way it is done.
        }

        lifetime.Dispose();
    }
}
