using System.Globalization;
using System.Text;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// Whole-file text I/O where a write either fully lands or leaves the previous file untouched, safe with several game
/// clients sharing one config folder (D11): every write goes through a temporary file of its own and a rename, and
/// reads never lock another client's rename out.
/// </summary>
public static class AtomicFile
{
    /// <summary>The end of every temporary file <see cref="Write"/> creates.</summary>
    public const string TempSuffix = ".tmp";

    /// <summary>
    /// How many times a rename over the target is tried before its error propagates: about a second in all
    /// (<see cref="Pause"/>), far longer than any reader here holds a file.
    /// </summary>
    private const int Attempts = 50;

    /// <summary>
    /// How many times a read refused by a sharing violation is tried: about a tenth of a second. Nothing here holds a
    /// file against readers (a save writes its own temporary file), so a longer refusal is another program's, and the
    /// caller reports it rather than waiting on it.
    /// </summary>
    private const int ReadAttempts = 10;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes <paramref name="contents"/> to a temporary file of its own beside <paramref name="path"/>
    /// (<c>&lt;path&gt;.&lt;process&gt;-&lt;random&gt;.tmp</c>), then renames it over <paramref name="path"/>. Parent directories
    /// are created. Two game clients saving the same file at once each write their own temporary file, so neither can
    /// move the other's half-written one into place: the file always holds one whole save, the last one renamed. A
    /// rename refused because a reader in another process has the file open is retried for a moment
    /// (<see cref="MoveOver"/>). A failed write deletes its temporary file before the exception propagates.
    /// </summary>
    public static void Write(string path, string contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);

        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tmp = TempPathFor(full);
        try
        {
            File.WriteAllText(tmp, contents, Utf8NoBom);
            MoveOver(tmp, full);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    /// <summary>A temporary file name no other writer, in this process or another, uses at the same time.</summary>
    public static string TempPathFor(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return path + "." + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..12] + TempSuffix;
    }

    /// <summary>Reads the whole file, or returns null when it does not exist. I/O failures propagate.</summary>
    public static string? Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ReadShared(path);
    }

    /// <summary>
    /// Reads the whole file. Returns null when the file is missing (<paramref name="error"/> null) or when it exists but
    /// cannot be read because it is locked, inaccessible or the disk failed (<paramref name="error"/> set). Such a file
    /// is not corrupt and must be left where it is.
    /// </summary>
    public static string? Read(string path, out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        error = null;
        try
        {
            return ReadShared(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Deletes the temporary files <see cref="Write"/> left in <paramref name="directory"/> (a crash mid-write) that are
    /// older than <paramref name="olderThan"/>, so a save another client has in flight is never touched. Best effort;
    /// returns how many went.
    /// </summary>
    public static int DeleteStaleTemps(string directory, TimeSpan olderThan, DateTime? nowUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var cutoff = (nowUtc ?? DateTime.UtcNow) - olderThan;
        var deleted = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*" + TempSuffix))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                    {
                        File.Delete(file);
                        deleted++;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // In use, or gone already: the next sweep tries again.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The folder went away while it was listed.
        }

        return deleted;
    }

    /// <summary>
    /// Moves a file that could not be parsed to <c>&lt;path&gt;.corrupt-&lt;yyyyMMddHHmmss&gt;</c> so nothing is lost and the
    /// next write starts fresh. Returns the new path. An existing quarantine file with the same stamp is never overwritten.
    /// </summary>
    public static string Quarantine(string path, DateTime? nowUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var stamp = (nowUtc ?? DateTime.UtcNow).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var baseName = path + ".corrupt-" + stamp;
        var target = baseName;
        for (var n = 1; File.Exists(target); n++)
        {
            target = baseName + "-" + n.ToString(CultureInfo.InvariantCulture);
        }

        File.Move(path, target);
        return target;
    }

    /// <summary><see cref="Quarantine"/> that reports an I/O or access failure instead of throwing; the file then stays in place.</summary>
    public static bool TryQuarantine(string path, out string? movedTo, out string? error, DateTime? nowUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            movedTo = Quarantine(path, nowUtc);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            movedTo = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Reads the file with every share mode open, so a writer in another client is never refused the file for longer
    /// than the read takes (<see cref="MoveOver"/> retries meanwhile). A sharing violation is retried for a moment; a
    /// missing file is null.
    /// </summary>
    private static string? ReadShared(string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return reader.ReadToEnd();
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return null;
            }
            catch (IOException) when (attempt < ReadAttempts)
            {
                Pause(attempt);
            }
        }
    }

    /// <summary>
    /// Renames <paramref name="source"/> over <paramref name="target"/>. Windows refuses to replace a file while anyone
    /// has it open, even a reader that shares delete access, so a refusal is retried for a few hundred milliseconds:
    /// every reader here holds a file only for the moment it takes to read it.
    /// </summary>
    public static void MoveOver(string source, string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(source, target, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && ex is not FileNotFoundException && attempt < Attempts)
            {
                Pause(attempt);
            }
        }
    }

    private static void Pause(int attempt) => Thread.Sleep(Math.Min(attempt * 2, 25));

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: DeleteStaleTemps sweeps a leftover later.
        }
    }
}
