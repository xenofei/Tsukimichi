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
    /// (<see cref="Pause"/>), far longer than any reader here holds a file. Meant for a background writer: the
    /// framework thread passes <see cref="QuickAttempts"/>.
    /// </summary>
    public const int DefaultAttempts = 50;

    /// <summary>
    /// The budget of a save on the framework thread: a few milliseconds of retries, never a frame. A save it cannot
    /// finish fails, and the caller's own retry schedule (a save interval, the next change) tries again later.
    /// </summary>
    public const int QuickAttempts = 3;

    /// <summary>
    /// How many times a rename refused with "access denied" is tried. Windows reports a file whose delete is pending
    /// that way for a moment, but a read-only or ACL-protected target says the same forever, so it is not waited out.
    /// </summary>
    public const int AccessDeniedAttempts = 3;

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
    public static void Write(string path, string contents) => Write(path, contents, DefaultAttempts);

    /// <summary>
    /// <see cref="Write(string, string)"/> with at most <paramref name="attempts"/> renames over the target
    /// (<see cref="QuickAttempts"/> on the framework thread).
    /// </summary>
    public static void Write(string path, string contents, int attempts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);

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
            MoveOver(tmp, full, attempts);
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
    /// than the read takes (<see cref="MoveOver"/> retries meanwhile). A sharing violation is retried for a moment, and so
    /// is "access denied": Windows answers that while another writer's rename is replacing the file (the old file is
    /// pending delete until its last handle closes). A missing file is null.
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
            catch (Exception ex) when (ShouldRetryRead(ex, attempt))
            {
                Pause(attempt);
            }
        }
    }

    /// <summary>
    /// Whether a failed read (<see cref="ReadShared"/>) is tried again: a sharing violation or "access denied" (a file
    /// another writer's rename is replacing) is, up to <see cref="ReadAttempts"/> tries; anything else, a missing file
    /// included, is not.
    /// </summary>
    public static bool ShouldRetryRead(Exception error, int attempt)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error is not (FileNotFoundException or DirectoryNotFoundException)
            && error is (IOException or UnauthorizedAccessException)
            && attempt < ReadAttempts;
    }

    /// <summary>
    /// Renames <paramref name="source"/> over <paramref name="target"/>. Windows refuses to replace a file while anyone
    /// has it open, even a reader that shares delete access, so a refusal is retried for a few hundred milliseconds:
    /// every reader here holds a file only for the moment it takes to read it.
    /// </summary>
    public static void MoveOver(string source, string target) => MoveOver(source, target, DefaultAttempts);

    /// <summary>
    /// <see cref="MoveOver(string, string)"/> with at most <paramref name="attempts"/> tries. A read-only target fails
    /// at once, and "access denied" is tried at most <see cref="AccessDeniedAttempts"/> times (<see cref="ShouldRetry"/>).
    /// </summary>
    public static void MoveOver(string source, string target, int attempts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);
        var denied = 0;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(source, target, overwrite: true);
                return;
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt, attempts, Classify(ex, target, ref denied), denied))
            {
                if (attempts <= QuickAttempts)
                {
                    // The framework thread's budget: a yield, not a sleep, which Windows rounds up to a whole 15.6 ms
                    // timer tick; the caller's own schedule retries a save that cannot land now.
                    Thread.Sleep(0);
                }
                else
                {
                    Pause(attempt);
                }
            }
        }
    }

    /// <summary>Why a rename over a target was refused, as far as the target tells.</summary>
    public enum Refusal
    {
        /// <summary>A sharing violation, or "access denied" while another handle has the target open.</summary>
        InUse,

        /// <summary>"Access denied" on a target nobody holds: permissions, or a delete still pending.</summary>
        Denied,

        /// <summary>The target has the read-only attribute.</summary>
        ReadOnly,
    }

    /// <summary>
    /// Whether a failed rename is tried again. Windows answers "access denied" both when a reader has the target open
    /// (the usual case here: another client reading it for a moment) and when the target is read-only or its ACL
    /// refuses the replace, which no wait can change. So a refusal while the target is in use is retried until
    /// <paramref name="attempts"/> run out; a read-only target never; any other "access denied" until it has been met
    /// <see cref="AccessDeniedAttempts"/> times (<paramref name="deniedSoFar"/>, this one included); a missing source or
    /// folder never.
    /// </summary>
    public static bool ShouldRetry(Exception error, int attempt, int attempts, Refusal refusal, int deniedSoFar)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error is FileNotFoundException or DirectoryNotFoundException || error is not (IOException or UnauthorizedAccessException))
        {
            return false;
        }

        return refusal switch
        {
            Refusal.ReadOnly => false,
            Refusal.Denied => deniedSoFar < AccessDeniedAttempts && attempt < attempts,
            _ => attempt < attempts,
        };
    }

    /// <summary>Classifies a refused rename by probing the target; counts the refusals that are not about sharing.</summary>
    private static Refusal Classify(Exception error, string target, ref int denied)
    {
        if (error is not UnauthorizedAccessException)
        {
            return Refusal.InUse;
        }

        if (IsReadOnly(target))
        {
            return Refusal.ReadOnly;
        }

        if (!IsWriteDenied(target))
        {
            // Writable: the refusal was another handle (a reader, another client's rename in flight), and it passes.
            return Refusal.InUse;
        }

        denied++;
        return Refusal.Denied;
    }

    /// <summary>True when <paramref name="path"/> exists with the read-only attribute; false when that cannot be told.</summary>
    private static bool IsReadOnly(string path)
    {
        try
        {
            return File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// True when <paramref name="path"/> refuses to be opened for writing with "access denied": its ACL, or a delete
    /// still pending on it. A file that opens (sharing everything, so no reader or writer is disturbed), is missing, or
    /// is merely in use (a sharing violation) is not denied: the refused rename was about another handle.
    /// </summary>
    private static bool IsWriteDenied(string path)
    {
        try
        {
            using var probe = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.None);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return false;
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
