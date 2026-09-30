using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Return;

/// <summary>
/// What "Since you were away" (P7) remembers about one character, kept in a sidecar beside the snapshot
/// (<c>characters/&lt;ContentId&gt;.return.json</c>) the way <see cref="Runtime.AcceptedSince"/> keeps accepted times,
/// so the snapshot schema stays untouched. Derived and optional: a file that cannot be read starts over empty.
/// </summary>
public sealed record WelcomeBackState
{
    /// <summary>The <see cref="LastPlayedPatch"/> answer "I'm new": nothing is "new since" for this character.</summary>
    public const string NewPlayer = "new";

    public static readonly WelcomeBackState Empty = new();

    /// <summary>
    /// The answer to "When did you last play?": a patch series ("7.2", read as 7.2x), <see cref="NewPlayer"/>, or empty
    /// when never asked or never answered. Asked only when the character has no capture to measure from.
    /// </summary>
    public string LastPlayedPatch { get; init; } = string.Empty;

    /// <summary>
    /// The newest patch the catalog knew (<see cref="Query.PatchIndex.Newest"/>) the last time this character was
    /// captured live: the patch its stored snapshot was taken on. Empty for a snapshot written before 1.1, whose patch
    /// is inferred from what it had done (<see cref="WelcomeBack.InferPatch"/>).
    /// </summary>
    public string SeenPatch { get; init; } = string.Empty;

    /// <summary>
    /// <see cref="CharacterSnapshot.TakenUtc"/> of the capture the card was last shown for: one return shows the card
    /// once, however often the plugin reloads before the new capture replaces the old one.
    /// </summary>
    public DateTime? ShownForUtc { get; init; }

    /// <summary>"Don't show again": never open the card on its own for this character (the dashboard button still does).</summary>
    public bool Quiet { get; init; }

    /// <summary>Whether the player answered "When did you last play?" (a patch or "I'm new").</summary>
    [JsonIgnore]
    public bool Answered => LastPlayedPatch.Length > 0;

    /// <summary>Whether the answer was "I'm new".</summary>
    [JsonIgnore]
    public bool IsNewPlayer => string.Equals(LastPlayedPatch, NewPlayer, StringComparison.Ordinal);

    /// <summary>
    /// The state a summary is computed from: this one (the current answer and choices) with the patch recorded as it
    /// was at login (<paramref name="atLogin"/>). A character's first live evaluation records this session's patch as
    /// <see cref="SeenPatch"/>, but the capture kept from before the login was taken on the older one, so "new since"
    /// must be measured from that. Null <paramref name="atLogin"/> (no evaluation this session) keeps this state.
    /// </summary>
    public WelcomeBackState MeasuredFrom(WelcomeBackState? atLogin) =>
        atLogin is null ? this : this with { SeenPatch = atLogin.SeenPatch };
}

/// <summary>Reads and writes <see cref="WelcomeBackState"/> sidecars.</summary>
public static class WelcomeBackStateFile
{
    public const string FileSuffix = ".return.json";

    /// <summary>The sidecar for one character, beside <c>&lt;ContentId&gt;.json</c> in the characters directory.</summary>
    public static string PathFor(string charactersDir, ulong contentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(charactersDir);
        return Path.Combine(charactersDir, contentId.ToString(CultureInfo.InvariantCulture) + FileSuffix);
    }

    /// <summary>
    /// Reads a sidecar. A missing file yields <see cref="WelcomeBackState.Empty"/>; an unreadable or unparseable one
    /// does too, with one line in <paramref name="warnings"/>, and is left in place to be overwritten. A patch that is
    /// not a patch number (other than "new") reads as unanswered; <see cref="WelcomeBackState.ShownForUtc"/> is
    /// normalized to UTC.
    /// </summary>
    public static WelcomeBackState Load(string path, IList<string>? warnings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fileName = Path.GetFileName(path);
        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            if (ioError is not null)
            {
                warnings?.Add($"{fileName} could not be read; \"Since you were away\" starts over: {ioError}");
            }

            return WelcomeBackState.Empty;
        }

        WelcomeBackState? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<WelcomeBackState>(text, StorageJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            warnings?.Add($"{fileName} could not be parsed; \"Since you were away\" starts over: {ex.Message}");
            return WelcomeBackState.Empty;
        }

        if (parsed is null)
        {
            return WelcomeBackState.Empty;
        }

        var answer = parsed.LastPlayedPatch?.Trim() ?? string.Empty;
        if (!string.Equals(answer, WelcomeBackState.NewPlayer, StringComparison.Ordinal))
        {
            answer = PatchVersion.IsPatch(PatchVersion.Normalize(answer)) ? PatchVersion.Series(PatchVersion.Normalize(answer)) : string.Empty;
        }

        var seen = PatchVersion.Normalize(parsed.SeenPatch);
        return parsed with
        {
            LastPlayedPatch = answer,
            SeenPatch = PatchVersion.IsPatch(seen) ? seen : string.Empty,
            ShownForUtc = parsed.ShownForUtc is { } shown ? AsUtc(shown) : null,
        };
    }

    /// <param name="attempts">Renames tried over the file (<see cref="AtomicFile.QuickAttempts"/> on the framework thread).</param>
    public static void Save(string path, WelcomeBackState state, int attempts = AtomicFile.DefaultAttempts)
    {
        ArgumentNullException.ThrowIfNull(state);
        AtomicFile.Write(path, JsonSerializer.Serialize(state, StorageJson.Options), attempts);
    }

    private static DateTime AsUtc(DateTime time) => time.Kind switch
    {
        DateTimeKind.Utc => time,
        DateTimeKind.Local => time.ToUniversalTime(),
        _ => DateTime.SpecifyKind(time, DateTimeKind.Utc),
    };
}
