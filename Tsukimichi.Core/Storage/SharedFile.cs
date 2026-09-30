using System.Diagnostics;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// A lock across game clients for the files every character shares (<c>user/pins.json</c>, <c>user/overrides.json</c>):
/// while one client re-reads, merges and writes such a file, another waits, so neither saves over an update it never
/// read (D11). The lock is a <c>&lt;file&gt;.lock</c> file opened for exclusive use and deleted when closed; Windows
/// releases it when the process ends, so a crashed client never leaves the file locked.
/// </summary>
public static class SharedFile
{
    public const string LockSuffix = ".lock";

    /// <summary>How long <see cref="Lock"/> waits for another client before giving up.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Takes the lock of <paramref name="path"/>, waiting up to <paramref name="timeout"/> (default
    /// <see cref="DefaultTimeout"/>) while another client or thread holds it; dispose to release. Throws
    /// <see cref="IOException"/> when the wait runs out.
    /// </summary>
    public static IDisposable Lock(string path, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var lockPath = full + LockSuffix;
        var limit = timeout ?? DefaultTimeout;
        var waited = Stopwatch.StartNew();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Held by another writer, or being deleted as it is released (access denied while the delete is pending).
                if (waited.Elapsed >= limit)
                {
                    throw new IOException($"{Path.GetFileName(full)} is being saved by another game client; gave up after {limit.TotalSeconds:0.#} s.", ex);
                }

                Thread.Sleep(Math.Min(attempt, 15));
            }
        }
    }
}

/// <summary>The merge rule for a shared keyed file (D11): the copy on disk, with only the keys this client changed taken from its own copy.</summary>
public static class KeyedMerge
{
    /// <summary>
    /// <paramref name="disk"/> with every key in <paramref name="touched"/> set to its value in <paramref name="local"/>,
    /// or removed when <paramref name="local"/> no longer has it. Keys this client did not touch keep what another
    /// client saved, so two clients editing different keys never lose each other's change; on the same key the later
    /// save wins.
    /// </summary>
    public static Dictionary<TKey, TValue> Apply<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> disk,
        IReadOnlyDictionary<TKey, TValue> local,
        IEnumerable<TKey> touched)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(disk);
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(touched);

        var result = new Dictionary<TKey, TValue>(disk.Count);
        foreach (var (key, value) in disk)
        {
            result[key] = value;
        }

        foreach (var key in touched)
        {
            if (local.TryGetValue(key, out var value))
            {
                result[key] = value;
            }
            else
            {
                result.Remove(key);
            }
        }

        return result;
    }
}
