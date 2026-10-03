using System.Security.Cryptography;

namespace Tsukimichi.Core.Diagnostics;

/// <summary>
/// "Trust you can check" (feature plan v7 N2): the SHA-256 of a file as lowercase hex, the form <c>sha256sum</c> prints
/// and the release workflow writes to each release's <c>SHA256SUMS.txt</c>. Settings › Advanced › Privacy &amp; trust
/// shows the loaded <c>Tsukimichi.dll</c>'s, so a player can compare it with the release page.
/// </summary>
public static class FileFingerprint
{
    /// <summary>The SHA-256 of everything left in <paramref name="stream"/>, as 64 lowercase hex digits.</summary>
    public static string Sha256(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>
    /// The SHA-256 of the file at <paramref name="path"/>, read shared (the game keeps the loaded plugin file open), as
    /// 64 lowercase hex digits. Throws an <see cref="IOException"/> (or the like) when the file cannot be read.
    /// </summary>
    public static string Sha256(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Sha256(stream);
    }
}
