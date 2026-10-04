using System.Collections.Frozen;

namespace Tsukimichi.Core.Companions;

/// <summary>
/// The best item level per job the reader last read (<see cref="Model.CharacterSnapshot.JobItemLevels"/>), and whose
/// they are (1.19.0 review). The read can be skipped (the hooks paused, the equipped gear not loaded yet), and a skipped
/// read must not hand the last character's gearsets to the next one: the table is kept only for the character it was
/// read for, and any other character gets <see cref="Unknown"/>, which <see cref="ItemLevelWall"/> reads as "not
/// judged". Framework thread only.
/// </summary>
public sealed class JobItemLevelMemory
{
    /// <summary>No job's item level known: what a character whose gear has not been read yet captures.</summary>
    public static readonly IReadOnlyDictionary<byte, ushort> Unknown = FrozenDictionary<byte, ushort>.Empty;

    private ulong owner;
    private IReadOnlyDictionary<byte, ushort> levels = Unknown;

    /// <summary>
    /// The table to capture for <paramref name="contentId"/> when this capture's read was skipped: the last one read,
    /// if it was this character's; <see cref="Unknown"/> otherwise.
    /// </summary>
    public IReadOnlyDictionary<byte, ushort> Skipped(ulong contentId) =>
        contentId != 0 && contentId == owner ? levels : Unknown;

    /// <summary>
    /// Takes a fresh read for <paramref name="contentId"/> and returns the table to capture: the previous instance when
    /// it is this character's and holds the same levels, so the diff sees it unchanged by reference.
    /// </summary>
    public IReadOnlyDictionary<byte, ushort> Read(ulong contentId, IReadOnlyDictionary<byte, ushort> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (contentId != owner || !Same(levels, read))
        {
            owner = contentId;
            levels = read.Count == 0 ? Unknown : read;
        }

        return levels;
    }

    private static bool Same(IReadOnlyDictionary<byte, ushort> a, IReadOnlyDictionary<byte, ushort> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (job, level) in b)
        {
            if (!a.TryGetValue(job, out var other) || other != level)
            {
                return false;
            }
        }

        return true;
    }
}
