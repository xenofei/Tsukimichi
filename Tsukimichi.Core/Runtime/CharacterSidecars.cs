namespace Tsukimichi.Core.Runtime;

/// <summary>
/// The per-character files kept beside a snapshot (<c>characters/&lt;ContentId&gt;.json</c>): the accepted-time map
/// (<see cref="AcceptedSince"/>) and the abandoned ledger (<see cref="AbandonedLedger"/>). Forgetting a character and
/// Settings › Delete all data remove them through these lists, so a sidecar added later is deleted with the rest.
/// </summary>
public static class CharacterSidecars
{
    /// <summary>File suffixes of every sidecar kind.</summary>
    public static readonly IReadOnlyList<string> Suffixes = [AcceptedSince.FileSuffix, AbandonedLedger.FileSuffix];

    /// <summary>Every sidecar path of one character, whether or not the file exists.</summary>
    public static IReadOnlyList<string> PathsFor(string charactersDir, ulong contentId) =>
        [AcceptedSince.PathFor(charactersDir, contentId), AbandonedLedger.PathFor(charactersDir, contentId)];

    /// <summary>Every sidecar file present in <paramref name="charactersDir"/>, of every character; empty when the directory is missing.</summary>
    public static List<string> FindAll(string charactersDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(charactersDir);
        var result = new List<string>();
        if (!Directory.Exists(charactersDir))
        {
            return result;
        }

        foreach (var suffix in Suffixes)
        {
            result.AddRange(Directory.GetFiles(charactersDir, "*" + suffix));
        }

        return result;
    }
}
