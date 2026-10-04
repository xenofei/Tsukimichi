using System;
using System.IO;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Ui.Themes;
using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Game;

/// <summary>
/// Umbra, read-only (plan v8 M1 and M3; spec-1.22 M3): whether it is installed and loaded (Dalamud's
/// <see cref="IDalamudPluginInterface.InstalledPlugins"/>), where its toolbar is and its colour profile (its saved
/// settings, <see cref="UmbraSettings"/>), and whether Tsukimichi for Umbra is there (its IPC hello, or Umbra's add-on
/// list naming it). Umbra has no API for any of this; its files are read, never written.
/// <para>
/// <b>When.</b> The settings file is read on a worker when Umbra loads, when the logged-in character changes (each
/// character may use its own Umbra profile) and when the file's write time moves, looked at every
/// <see cref="StatInterval"/>: so the clearance follows the bar when it appears, changes side or changes size, never
/// per frame. <see cref="UmbraLayout.Clearance"/> is kept current on the framework thread.
/// </para>
/// </summary>
public sealed class UmbraProbe : IDisposable, Core.Ui.IUmbraLayout
{
    /// <summary>How often the settings file's write time is looked at, in seconds.</summary>
    public const double StatInterval = 5.0;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IFramework framework;
    private readonly Configuration settings;
    private readonly Func<ulong?> contentId;
    private readonly Func<float> uiScale;
    private readonly IPluginLog log;
    private readonly PluginPresence umbra;
    private readonly string folder;

    private int seenGeneration = -1;
    private ulong? seenCharacter;
    private string? readPath;
    private DateTime readStamp;
    private double nextStat;
    private double? retryAt;
    private readonly UmbraReads reads = new();
    private volatile Landed? landed;
    private UmbraColorProfile? paletteFor;
    private bool disposed;

    /// <summary>A read from the worker: the file it read, its write time, and what it found.</summary>
    private sealed record Landed(string Path, DateTime Stamp, UmbraRead Read);

    public UmbraProbe(IDalamudPluginInterface pluginInterface, IFramework framework, Configuration settings, Func<ulong?> contentId, Func<float> uiScale, IPluginLog log)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.contentId = contentId ?? throw new ArgumentNullException(nameof(contentId));
        this.uiScale = uiScale ?? throw new ArgumentNullException(nameof(uiScale));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        umbra = new PluginPresence(pluginInterface, log, UmbraSettings.InternalName);
        folder = Path.Combine(pluginInterface.ConfigDirectory.Parent?.FullName ?? pluginInterface.ConfigDirectory.FullName, UmbraSettings.InternalName);
        framework.Update += OnUpdate;
    }

    /// <summary>Umbra is installed and loaded.</summary>
    public bool Loaded => umbra.Loaded;

    /// <summary>The last read of Umbra's settings; null before one, or while Umbra is not loaded.</summary>
    public UmbraRead? Read { get; private set; }

    /// <summary>
    /// The moon icon's view of the bar (H1, <see cref="Core.Ui.IUmbraLayout"/>): the edge it holds and its height in
    /// screen px without the gap (the icon adds its own 8 px); none while nothing keeps clear.
    /// </summary>
    public Core.Ui.ToolbarClearance Toolbar => UmbraLayout.Clearance switch
    {
        { Top: > 0f } c => new Core.Ui.ToolbarClearance(Core.Ui.ToolbarEdge.Top, c.Bar),
        { Bottom: > 0f } c => new Core.Ui.ToolbarClearance(Core.Ui.ToolbarEdge.Bottom, c.Bar),
        _ => Core.Ui.ToolbarClearance.None,
    };

    /// <summary>Tsukimichi for Umbra said hello over IPC this session, with its version; null until it does.</summary>
    public string? AddonVersion { get; private set; }

    /// <summary>
    /// Tsukimichi for Umbra is installed: it said hello, or Umbra's add-on list names it with Umbra's custom plugins on
    /// (<see cref="UmbraReads.AddonPresent"/>).
    /// </summary>
    public bool AddonPresent => UmbraReads.AddonPresent(AddonVersion, Read);

    /// <summary>The Follow Umbra palette for the profile last read: Night when it can't be read (<see cref="PaletteFellBack"/>).</summary>
    public UiPalette Palette { get; private set; } = UiPalettes.Night;

    /// <summary>Whether <see cref="Palette"/> is Night in place of Umbra's colours.</summary>
    public bool PaletteFellBack { get; private set; } = true;

    /// <summary>Whether Umbra's text colour had to be pushed to read (the contrast clamp).</summary>
    public bool PaletteClamped { get; private set; }

    /// <summary>Moves whenever anything above changes (Settings › About, the server info bar default).</summary>
    public int Revision { get; private set; }

    /// <summary>Raised on the framework thread after <see cref="Revision"/> moved.</summary>
    public event Action? Changed;

    /// <summary>Tsukimichi for Umbra's hello (the IPC gate): remembered for Settings › About and the entry's default.</summary>
    public void AddonHello(string version)
    {
        var shown = string.IsNullOrWhiteSpace(version) ? "?" : version.Trim();
        if (shown.Length > 32)
        {
            shown = shown[..32];
        }

        if (!string.Equals(AddonVersion, shown, StringComparison.Ordinal))
        {
            AddonVersion = shown;
            Bump();
        }
    }

    /// <summary>Recomputes the clearance after a setting changed (the assumed bar height).</summary>
    public void SettingsChanged() => Recompute();

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        framework.Update -= OnUpdate;
        umbra.Dispose();
        UmbraLayout.Clearance = UmbraClearance.None;
        UmbraLayout.Revision++;
    }

    private void OnUpdate(IFramework fw)
    {
        if (disposed)
        {
            return;
        }

        try
        {
            var loaded = umbra.Loaded;
            var character = contentId();
            var now = fw.LastUpdateUTC.Subtract(DateTime.UnixEpoch).TotalSeconds;
            if (landed is { } read)
            {
                landed = null;
                var landing = reads.Land(Read, readPath, read.Read, read.Path);
                if (landing.Take)
                {
                    readPath = read.Path;
                    readStamp = read.Stamp;
                    Land(read.Read);
                }

                // A failed re-read of the same file keeps the last good read and tries once more soon.
                retryAt = landing.RetrySoon ? now + UmbraReads.RetrySeconds : null;
                if (landing.StartAgain)
                {
                    // Asked for while that read ran (the character changed, or Umbra's file moved): read for the character now.
                    Run(character);
                }
            }

            var moved = umbra.Generation != seenGeneration || character != seenCharacter;
            if (moved)
            {
                seenGeneration = umbra.Generation;
                seenCharacter = character;
                if (!loaded)
                {
                    Unloaded();
                }
                else
                {
                    StartRead(character);
                }
            }
            else if (loaded && retryAt is { } retry && now >= retry)
            {
                retryAt = null;
                StartRead(character);
            }
            else if (loaded && now >= nextStat)
            {
                nextStat = now + StatInterval;
                if (readPath is { } path && Stamp(path) != readStamp)
                {
                    StartRead(character);
                }
            }

            Recompute();
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Umbra probe failed");
        }
    }

    /// <summary>
    /// Umbra unloaded: its read, the first-read wait and the add-on's hello go (the add-on unloads with Umbra), so the
    /// clearance and Settings › About follow at once.
    /// </summary>
    private void Unloaded()
    {
        readPath = null;
        retryAt = null;
        reads.Unloaded();
        var changed = Read is not null || AddonVersion is not null;
        Read = null;
        AddonVersion = null;
        if (changed)
        {
            Bump();
        }
    }

    /// <summary>
    /// Reads the character's profile file on a worker; the result lands on a later framework tick. Asked for while a read
    /// runs, it starts when that one lands (<see cref="UmbraReads"/>), so a character change mid-read is never dropped.
    /// </summary>
    private void StartRead(ulong? character)
    {
        if (reads.Request())
        {
            Run(character);
        }
    }

    /// <summary>The read itself, on a worker; every path ends by publishing what it found.</summary>
    private void Run(ulong? character)
    {
        var dir = folder;
        _ = Task.Run(() =>
        {
            try
            {
                string? profiles = null;
                var map = Path.Combine(dir, UmbraSettings.ProfilesFile);
                if (File.Exists(map))
                {
                    profiles = ReadShared(map);
                }

                var path = Path.Combine(dir, UmbraSettings.ProfileFileName(profiles, character));
                var stamp = Stamp(path);
                landed = new Landed(path, stamp, File.Exists(path) ? UmbraSettings.Parse(ReadShared(path)) : UmbraRead.Failed("no settings file"));
            }
            catch (Exception ex)
            {
                landed = new Landed(Path.Combine(dir, UmbraSettings.DefaultProfile + ".profile.json"), DateTime.MinValue, UmbraRead.Failed(ex.GetType().Name));
            }
        });
    }

    /// <summary>Framework thread: the read becomes the state, and the palette is rebuilt when the profile changed.</summary>
    private void Land(UmbraRead read)
    {
        if (!umbra.Loaded)
        {
            return;
        }

        if (read.Problem is { } problem)
        {
            log.Debug("Umbra's settings read with a gap: {Problem}", problem);
        }

        Read = read;
        if (AddonVersion is not null && !UmbraReads.HelloStands(umbraLoaded: true, read))
        {
            // Umbra's settings now show the add-on unlisted or its custom plugins off: it no longer runs.
            AddonVersion = null;
        }

        if (!SameColors(read.Colors, paletteFor))
        {
            paletteFor = read.Colors;
            Palette = UmbraPalette.From(read.Colors, out var clamped, out var fellBack);
            PaletteClamped = clamped;
            PaletteFellBack = fellBack;
        }

        Bump();
        Recompute();
    }

    /// <summary>The clearance from what is known now; <see cref="UmbraLayout"/> moves only when it changes.</summary>
    private void Recompute()
    {
        // Nothing moves until the first read since Umbra loaded lands: assuming a top bar before it would move a surface
        // down and back for a player whose bar is at the bottom or floating.
        var next = UmbraClearance.For(umbra.Loaded && reads.Settled, Read?.Toolbar, settings.UmbraAssumedBarHeight, uiScale());
        if (next != UmbraLayout.Clearance)
        {
            UmbraLayout.Clearance = next;
            UmbraLayout.Revision++;
        }
    }

    /// <summary>Whether two profiles hold the same name and colours (a re-read of an unchanged file keeps the palette).</summary>
    private static bool SameColors(UmbraColorProfile? a, UmbraColorProfile? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal) || a.Colors.Count != b.Colors.Count)
        {
            return false;
        }

        foreach (var (role, color) in a.Colors)
        {
            if (!b.Colors.TryGetValue(role, out var other) || other != color)
            {
                return false;
            }
        }

        return true;
    }

    private void Bump()
    {
        Revision++;
        Changed?.Invoke();
    }

    /// <summary>Reads a file Umbra may be writing: shared, read-only, capped.</summary>
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > UmbraSettings.MaxFileBytes)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static DateTime Stamp(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }
        catch (Exception)
        {
            return DateTime.MinValue;
        }
    }
}
