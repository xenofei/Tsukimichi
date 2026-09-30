using System.Globalization;
using System.Text.Json;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Multibox;

/// <summary>
/// "This character is logged in on a game client right now" (D11): the small file a client keeps fresh for its
/// logged-in character, <c>characters/&lt;contentId&gt;.live.json</c>, beside the snapshot. Every client runs its own copy
/// of the plugin and they share the config folder, so the file is how one client learns another has a character live.
/// </summary>
/// <param name="ContentId">The character.</param>
/// <param name="Name">Its name, for a log line or a tooltip when no snapshot is readable yet.</param>
/// <param name="World">Its home world id.</param>
/// <param name="ProcessId">The game client's process id.</param>
/// <param name="ClientId">A random id per plugin load, the tie-break when two claims start at the same instant.</param>
/// <param name="SinceUtc">When that client began holding the character (its login): the newer claim wins a clash.</param>
/// <param name="WrittenUtc">When the file was last refreshed; older than <see cref="LiveClients.StaleAfter"/> means that client is gone.</param>
public sealed record Heartbeat(
    ulong ContentId,
    string Name,
    uint World,
    int ProcessId,
    string ClientId,
    DateTime SinceUtc,
    DateTime WrittenUtc);

/// <summary>Who this game client is: its process, and this load of the plugin in it.</summary>
public readonly record struct ClientIdentity(int ProcessId, string ClientId)
{
    /// <summary>This process with a fresh random id.</summary>
    public static ClientIdentity ForThisProcess() => new(Environment.ProcessId, Guid.NewGuid().ToString("N"));
}

/// <summary>Reads and writes <see cref="Heartbeat"/> files. A heartbeat is disposable: an unreadable or corrupt one reads as absent and is never quarantined.</summary>
public static class HeartbeatFile
{
    /// <summary>The end of a heartbeat file's name.</summary>
    public const string Suffix = ".live.json";

    public static string PathFor(string charactersDir, ulong contentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(charactersDir);
        return Path.Combine(charactersDir, contentId.ToString(CultureInfo.InvariantCulture) + Suffix);
    }

    /// <summary>The content id a heartbeat file is named for, or null when <paramref name="fileName"/> is not one.</summary>
    public static ulong? ContentIdOf(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var name = Path.GetFileName(fileName);
        if (!name.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var stem = name[..^Suffix.Length];
        return ulong.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    public static void Write(string charactersDir, Heartbeat heartbeat)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        AtomicFile.Write(PathFor(charactersDir, heartbeat.ContentId), JsonSerializer.Serialize(heartbeat, StorageJson.Options));
    }

    /// <summary>The heartbeat of a character, or null when there is none or it cannot be read.</summary>
    public static Heartbeat? TryRead(string charactersDir, ulong contentId) => TryReadPath(PathFor(charactersDir, contentId));

    /// <summary>The heartbeat at <paramref name="path"/>, or null when there is none or it cannot be read.</summary>
    public static Heartbeat? TryReadPath(string path)
    {
        var text = AtomicFile.Read(path, out _);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            var heartbeat = JsonSerializer.Deserialize<Heartbeat>(text, StorageJson.Options);
            return heartbeat is { ContentId: not 0 } && heartbeat.ContentId == ContentIdOf(path) ? heartbeat : null;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Every readable heartbeat in <paramref name="charactersDir"/>; empty when the folder is missing.</summary>
    public static List<Heartbeat> ReadAll(string charactersDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(charactersDir);
        var result = new List<Heartbeat>();
        if (!Directory.Exists(charactersDir))
        {
            return result;
        }

        try
        {
            foreach (var path in Directory.EnumerateFiles(charactersDir, "*" + Suffix))
            {
                if (TryReadPath(path) is { } heartbeat)
                {
                    result.Add(heartbeat);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The folder went away while it was listed: nobody is live.
        }

        return result;
    }

    /// <summary>
    /// Deletes a character's heartbeat when <see cref="LiveClients.MayDelete"/> allows it (it is this client's own, or
    /// stale); a fresh heartbeat of another client stays. Returns true when a file was deleted.
    /// </summary>
    public static bool DeleteIfAllowed(string charactersDir, ulong contentId, ClientIdentity me, DateTime nowUtc)
    {
        var path = PathFor(charactersDir, contentId);
        if (!File.Exists(path))
        {
            return false;
        }

        if (!LiveClients.MayDelete(TryReadPath(path), me, nowUtc))
        {
            return false;
        }

        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
