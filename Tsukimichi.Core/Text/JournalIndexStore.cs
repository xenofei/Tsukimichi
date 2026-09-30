using System.Security.Cryptography;
using System.Text;

namespace Tsukimichi.Core.Text;

/// <summary>
/// Where the journal search index (<see cref="JournalTextIndex"/>) lives on disk: one file per game version and
/// language under the plugin's config directory, <c>cache/journal-index.&lt;version&gt;.&lt;lang&gt;.bin</c>. A patch
/// changes the game version, so the next enable finds no file for it, rebuilds, and removes the files of other
/// versions. Written through a temporary file and a rename, so a crash never leaves half an index behind.
/// </summary>
public static class JournalIndexStore
{
    public const string CacheFolder = "cache";
    public const string FilePrefix = "journal-index.";
    public const string FileSuffix = ".bin";

    /// <summary>The end of a temporary file's name: <c>journal-index.&lt;version&gt;.&lt;lang&gt;.bin.&lt;random&gt;.tmp</c>.</summary>
    public const string TempSuffix = ".tmp";

    /// <summary>The folder the index files live in.</summary>
    public static string Folder(string configDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDir);
        return Path.Combine(configDir, CacheFolder);
    }

    /// <summary>
    /// The file for a game version and language. The version is spelled out when it is a plain version string
    /// (2026.09.15.0000.0000); anything else (a combined key) is hashed so the name stays short and legal.
    /// </summary>
    public static string PathFor(string configDir, string gameVersion, string language)
    {
        ArgumentNullException.ThrowIfNull(gameVersion);
        ArgumentNullException.ThrowIfNull(language);
        return Path.Combine(Folder(configDir), FilePrefix + Slug(gameVersion) + "." + Slug(language) + FileSuffix);
    }

    /// <summary>The saved index for this game version and language, or null when there is none or it does not read.</summary>
    public static JournalTextIndex? Load(string configDir, string gameVersion, string language)
    {
        var path = PathFor(configDir, gameVersion, language);
        if (!File.Exists(path))
        {
            return null;
        }

        // Every share mode open: another game client sharing the config folder may rename a fresh build over this file
        // while it is read (D11); the read keeps the version it opened.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
        return JournalTextIndex.Read(stream, gameVersion, language);
    }

    /// <summary>
    /// Writes the index for its own game version and language; returns the file's size in bytes. The temporary file
    /// has a name of its own per call, so two builds never write the same one.
    /// </summary>
    public static long Save(string configDir, JournalTextIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        var path = PathFor(configDir, index.GameVersion, index.Language);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + TempSuffix;
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16))
            {
                index.Write(stream);
                stream.Flush(flushToDisk: true);
            }

            // Retried while another game client is reading the file it replaces (D11).
            Storage.AtomicFile.MoveOver(temp, path);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }

        return new FileInfo(path).Length;
    }

    /// <summary>
    /// Deletes every index file except <paramref name="keepPath"/> (null deletes them all); returns how many went.
    /// Temporary files are left alone: one may belong to a build still writing it (<see cref="DeleteTemp"/>).
    /// </summary>
    public static int DeleteOthers(string configDir, string? keepPath) =>
        Delete(configDir, file =>
            !file.EndsWith(TempSuffix, StringComparison.OrdinalIgnoreCase)
            && (keepPath is null || !string.Equals(Path.GetFullPath(file), Path.GetFullPath(keepPath), StringComparison.OrdinalIgnoreCase)));

    /// <summary>How long the index of another game version is kept: another install at another patch level may use it.</summary>
    public static readonly TimeSpan OtherVersionKeep = TimeSpan.FromDays(30);

    /// <summary>
    /// Deletes the index files of other game versions older than <paramref name="keepFor"/> (default
    /// <see cref="OtherVersionKeep"/>); the files of every language of <paramref name="gameVersion"/> stay, since two
    /// game clients sharing the config folder (D11) may run in different languages, and so do recent files of other
    /// versions, since two game installs at different patch levels may share it too: either would otherwise delete the
    /// other's index after every build. Temporary files are left alone. Returns how many went.
    /// </summary>
    public static int DeleteOtherVersions(string configDir, string gameVersion, TimeSpan? keepFor = null, DateTime? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(gameVersion);
        var keep = FilePrefix + Slug(gameVersion) + ".";
        var cutoff = (nowUtc ?? DateTime.UtcNow) - (keepFor ?? OtherVersionKeep);
        return Delete(configDir, file =>
            !file.EndsWith(TempSuffix, StringComparison.OrdinalIgnoreCase)
            && !Path.GetFileName(file).StartsWith(keep, StringComparison.OrdinalIgnoreCase)
            && WrittenBefore(file, cutoff));
    }

    /// <summary>
    /// Deletes the temporary files a save left behind (a crash or an unload mid-write); returns how many went. Called
    /// at startup, and with <see cref="DeleteOthers"/> for "Delete all data". With <paramref name="olderThan"/>, only
    /// files older than that go, so a build another game client has in flight in the same folder keeps its file.
    /// </summary>
    public static int DeleteTemp(string configDir, TimeSpan? olderThan = null)
    {
        var cutoff = olderThan is { } age ? DateTime.UtcNow - age : DateTime.MaxValue;
        return Delete(configDir, file => file.EndsWith(TempSuffix, StringComparison.OrdinalIgnoreCase) && WrittenBefore(file, cutoff));
    }

    private static bool WrittenBefore(string file, DateTime cutoffUtc)
    {
        if (cutoffUtc == DateTime.MaxValue)
        {
            return true;
        }

        try
        {
            return File.GetLastWriteTimeUtc(file) < cutoffUtc;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static int Delete(string configDir, Func<string, bool> which)
    {
        var folder = Folder(configDir);
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        var deleted = 0;
        foreach (var file in Directory.EnumerateFiles(folder, FilePrefix + "*"))
        {
            if (!which(file))
            {
                continue;
            }

            if (TryDelete(file))
            {
                deleted++;
            }
        }

        return deleted;
    }

    private static bool TryDelete(string file)
    {
        try
        {
            File.Delete(file);
            return true;
        }
        catch (IOException)
        {
            // In use or already gone; the next rebuild tries again.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string Slug(string value)
    {
        var plain = value.Length is > 0 and <= 40 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
        if (plain)
        {
            return value;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash.AsSpan(0, 8));
    }
}
