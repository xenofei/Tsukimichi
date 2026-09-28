using System.Globalization;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// Keeps a configuration file that could not be read. The plugin's settings live in a file Dalamud owns; when it fails
/// to deserialise, defaults are used and the next save overwrites it, so the unreadable original is copied to
/// <c>&lt;name&gt;.corrupt-&lt;yyyyMMddHHmmss&gt;&lt;ext&gt;</c> beside it first. A copy, not a move: the original stays
/// where Dalamud expects it, and a file that only failed transiently (locked, half-written by another process) is
/// left for the next load.
/// </summary>
public static class ConfigRecovery
{
    /// <summary>The copy's path for a file at <paramref name="path"/> stamped with <paramref name="nowUtc"/>, e.g. <c>Tsukimichi.corrupt-20260928120000.json</c>.</summary>
    public static string CorruptPathFor(string path, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full) ?? string.Empty;
        var stamp = nowUtc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(full) + ".corrupt-" + stamp + Path.GetExtension(full));
    }

    /// <summary>
    /// Copies the file to <see cref="CorruptPathFor"/> (with a <c>-n</c> suffix when that name is taken) and reports
    /// where it went. Returns false with <paramref name="error"/> null when there is nothing to copy (no file), and
    /// false with an error when the copy failed; the original is never touched either way.
    /// </summary>
    public static bool TryCopyAside(string path, out string? copiedTo, out string? error, DateTime? nowUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        copiedTo = null;
        error = null;

        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var baseName = CorruptPathFor(path, nowUtc ?? DateTime.UtcNow);
            var extension = Path.GetExtension(baseName);
            var stem = baseName[..^extension.Length];
            var target = baseName;
            for (var n = 1; File.Exists(target); n++)
            {
                target = stem + "-" + n.ToString(CultureInfo.InvariantCulture) + extension;
            }

            File.Copy(path, target, overwrite: false);
            copiedTo = target;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }
}
