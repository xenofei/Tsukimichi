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

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
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

            File.Move(temp, path, overwrite: true);
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

    /// <summary>
    /// Deletes the temporary files a save left behind (a crash or an unload mid-write); returns how many went. Called
    /// when no build runs: at startup, and with <see cref="DeleteOthers"/> for "Delete all data".
    /// </summary>
    public static int DeleteTemp(string configDir) =>
        Delete(configDir, file => file.EndsWith(TempSuffix, StringComparison.OrdinalIgnoreCase));

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
