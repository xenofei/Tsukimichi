namespace Tsukimichi.Core.Updates;

/// <summary>Where the update note stands (plan v8 U1; spec-1.22 U1).</summary>
public enum UpdateStatus : byte
{
    /// <summary>No newer version is known: the check is off, has not answered yet, or found nothing newer.</summary>
    None,

    /// <summary>A newer version is waiting in Dalamud: the status-bar note and the moon icon's dot show.</summary>
    Ready,

    /// <summary>A newer version is waiting, but the player chose Later for it: the note and the dot hide until a newer one.</summary>
    Dismissed,
}

/// <summary>
/// What Tsukimichi knows about an update (plan v8 U1): the version Dalamud has, its plain notes, and whether the note
/// shows. The moon icon reads <see cref="Status"/> for its dot; Settings › About shows <see cref="Available"/> even when
/// dismissed, with Update. Immutable.
/// </summary>
/// <param name="Status">Whether the note and the dot show.</param>
/// <param name="Available">The newer version Dalamud has ("1.23.0"); null when none is known.</param>
/// <param name="Notes">That version's plain notes as Dalamud has them; empty when it has none.</param>
/// <param name="CheckedUtc">When Dalamud was last asked; null before the first answer.</param>
public sealed record UpdateState(UpdateStatus Status, string? Available, string Notes, DateTime? CheckedUtc)
{
    /// <summary>Nothing known yet.</summary>
    public static readonly UpdateState Unknown = new(UpdateStatus.None, null, string.Empty, null);

    /// <summary>Whether the status-bar note and the dot show.</summary>
    public bool ShowsNote => Status == UpdateStatus.Ready;

    /// <summary>Whether a newer version is waiting, shown or not.</summary>
    public bool HasUpdate => Status != UpdateStatus.None && Available is not null;
}

/// <summary>
/// The update check's rules (plan v8 U1, spec-1.22 U1 "The check" and "Later"), pure so they are tested without
/// Dalamud: how often to ask, whether an answer is newer than the running build, and the state after an answer or a
/// Later. Tsukimichi asks Dalamud (<c>CheckForUpdateAsync</c>), which reads the repository data it already refreshes;
/// nothing here goes online.
/// </summary>
public static class UpdateRules
{
    /// <summary>How often the check runs while it is on (spec-1.22 U1: "at login and every 3 hours").</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(3);

    /// <summary>
    /// Whether the check is due: never while off, at once when it has not run since the login
    /// (<paramref name="lastCheckUtc"/> null), then every <see cref="Interval"/>.
    /// </summary>
    public static bool Due(bool enabled, DateTime? lastCheckUtc, DateTime nowUtc) =>
        enabled && (lastCheckUtc is not { } last || nowUtc - last >= Interval || nowUtc < last);

    /// <summary>
    /// Whether <paramref name="candidate"/> is newer than <paramref name="running"/>, comparing the parsed versions
    /// (major.minor.build, a missing part read as 0); false when either does not parse.
    /// </summary>
    public static bool IsNewer(string? candidate, string? running) =>
        TryParse(candidate, out var a) && TryParse(running, out var b) && a > b;

    /// <summary>
    /// The state after Dalamud answered: <paramref name="found"/> is the version it has (null: none), compared with the
    /// running build. A version the player dismissed with Later stays dismissed; any newer one shows again.
    /// </summary>
    /// <param name="running">The running build's version.</param>
    /// <param name="found">The version Dalamud offers; null or not newer means no update.</param>
    /// <param name="notes">Its plain notes (null: none).</param>
    /// <param name="dismissed">The version the player chose Later for; empty for none.</param>
    /// <param name="nowUtc">When the answer came.</param>
    public static UpdateState Answer(string running, string? found, string? notes, string? dismissed, DateTime nowUtc)
    {
        if (!IsNewer(found, running))
        {
            return new UpdateState(UpdateStatus.None, null, string.Empty, nowUtc);
        }

        var version = Normalise(found!);
        var hidden = TryParse(dismissed, out var later) && TryParse(version, out var offered) && offered <= later;
        return new UpdateState(hidden ? UpdateStatus.Dismissed : UpdateStatus.Ready, version, (notes ?? string.Empty).Trim(), nowUtc);
    }

    /// <summary>Later (×): the note and the dot hide until a version newer than this one appears. Nothing is lost, so no Undo.</summary>
    public static UpdateState Dismiss(UpdateState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Status == UpdateStatus.Ready ? state with { Status = UpdateStatus.Dismissed } : state;
    }

    /// <summary>The state with the check turned off: nothing shows, and nothing is remembered but the dismissed version.</summary>
    public static UpdateState Off(UpdateState state) => UpdateState.Unknown with { CheckedUtc = state?.CheckedUtc };

    /// <summary>"1.23.0" for "1.23.0.0" and "1.23": three parts, as the plugin's version is written everywhere else.</summary>
    public static string Normalise(string version) =>
        TryParse(version, out var parsed) ? $"{parsed.Major}.{Math.Max(0, parsed.Minor)}.{Math.Max(0, parsed.Build)}" : version.Trim();

    private static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(text) || !Version.TryParse(text.Trim(), out var parsed))
        {
            return false;
        }

        // Compare on three parts: 1.23 and 1.23.0.0 are the same release.
        version = new Version(parsed.Major, Math.Max(0, parsed.Minor), Math.Max(0, parsed.Build));
        return true;
    }
}
