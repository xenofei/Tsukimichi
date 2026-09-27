using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Storage;

/// <summary>One stored character, enough to populate a picker without loading the full snapshot.</summary>
/// <param name="CompletedCount">Number of set bits in the completion bitmask.</param>
public sealed record SnapshotSummary(ulong ContentId, string Name, uint World, DateTime TakenUtc, int CompletedCount);

/// <summary>Persistence for <see cref="CharacterSnapshot"/>, keyed by content id.</summary>
public interface ISnapshotStore
{
    IReadOnlyList<SnapshotSummary> List();

    /// <summary>Loads one character, or null when there is no readable snapshot for it.</summary>
    CharacterSnapshot? Load(ulong contentId);

    void Save(CharacterSnapshot snapshot);

    void Delete(ulong contentId);
}
