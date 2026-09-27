using System.Globalization;
using System.Text;

namespace Tsukimichi.Core.Storage;

/// <summary>Whole-file text I/O where a write either fully lands or leaves the previous file untouched.</summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Writes <paramref name="contents"/> to <c>&lt;path&gt;.tmp</c>, then moves it over <paramref name="path"/>. Parent directories are created.</summary>
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

        var tmp = full + ".tmp";
        File.WriteAllText(tmp, contents, Utf8NoBom);
        File.Move(tmp, full, overwrite: true);
    }

    /// <summary>Reads the whole file, or returns null when it does not exist. I/O failures propagate.</summary>
    public static string? Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return File.Exists(path) ? File.ReadAllText(path) : null;
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
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return null;
        }
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
}
