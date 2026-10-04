namespace Tsukimichi.Core.Umbra;

/// <summary>What to do with a read of Umbra's settings that landed (<see cref="UmbraReads.Land"/>).</summary>
/// <param name="Take">The read becomes the state; false keeps the last good read.</param>
/// <param name="StartAgain">Start another read now: one was asked for while this one ran (a character change, a newer file).</param>
/// <param name="RetrySoon">The read failed on the file the last good read came from: read it again soon (Umbra may have been mid-write).</param>
public readonly record struct UmbraLanding(bool Take, bool StartAgain, bool RetrySoon);

/// <summary>
/// The bookkeeping of the reads of Umbra's settings (the plugin's <c>Game.UmbraProbe</c>; spec-1.22 M3), pure and
/// framework-thread only: one read at a time; a read asked for while one is under way is kept and starts when that one
/// lands, so a character change during a read is never dropped; a failed re-read of the same file keeps the last good
/// read once and asks for a retry. Also whether Tsukimichi for Umbra counts as installed.
/// </summary>
public sealed class UmbraReads
{
    /// <summary>How soon a failed re-read of the same file is tried again, in seconds.</summary>
    public const double RetrySeconds = 1.0;

    private bool wanted;
    private bool retried;

    /// <summary>A read is under way.</summary>
    public bool Reading { get; private set; }

    /// <summary>
    /// A read has landed since Umbra loaded: until then nothing keeps clear (the assumed top bar is for settings that
    /// could not be read, not for settings not read yet), so a bottom or floating bar never sees a surface jump at load.
    /// </summary>
    public bool Settled { get; private set; }

    /// <summary>Umbra unloaded: the next load waits for its own first read. A read under way still lands.</summary>
    public void Unloaded()
    {
        Settled = false;
        wanted = false;
        retried = false;
    }

    /// <summary>Asks for a read: true when one should start now; otherwise the one under way is followed by another.</summary>
    public bool Request()
    {
        if (Reading)
        {
            wanted = true;
            return false;
        }

        Reading = true;
        return true;
    }

    /// <summary>
    /// The read under way landed (<paramref name="landed"/>, from <paramref name="landedPath"/>), with the state it would
    /// replace (<paramref name="current"/>, from <paramref name="currentPath"/>). When <see cref="UmbraLanding.StartAgain"/>
    /// is true a new read counts as started.
    /// </summary>
    public UmbraLanding Land(UmbraRead? current, string? currentPath, UmbraRead landed, string landedPath)
    {
        ArgumentNullException.ThrowIfNull(landed);
        Settled = true;
        var keep = !Readable(landed) && current is not null && Readable(current) && !retried
            && string.Equals(currentPath, landedPath, StringComparison.OrdinalIgnoreCase);
        retried = keep;
        var again = wanted;
        wanted = false;
        Reading = again;
        return new UmbraLanding(!keep, again, keep);
    }

    /// <summary>Whether a read found Umbra's settings at all (its toolbar or its colours), not only a problem.</summary>
    public static bool Readable(UmbraRead read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return read.Toolbar is not null || read.Colors is not null;
    }

    /// <summary>
    /// Whether Tsukimichi for Umbra is installed (Settings › About, the server info bar default): it said hello since
    /// Umbra loaded (<paramref name="hello"/>), or Umbra's settings list it with Umbra's custom plugins switched on (a
    /// listed add-on with the switch off never runs).
    /// </summary>
    public static bool AddonPresent(string? hello, UmbraRead? read) =>
        hello is not null || read is { AddonListed: true, CustomPluginsOn: true };

    /// <summary>
    /// Whether an add-on's hello still stands: not once Umbra unloads (the add-on goes with it), nor once a read of
    /// Umbra's settings shows it unlisted or the custom plugins switched off. A read that found nothing changes nothing.
    /// </summary>
    public static bool HelloStands(bool umbraLoaded, UmbraRead? read) =>
        umbraLoaded && !(read is { } r && Readable(r) && !(r.AddonListed && r.CustomPluginsOn));
}
