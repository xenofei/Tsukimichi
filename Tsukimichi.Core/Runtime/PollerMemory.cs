using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// What the poller keeps between polls for the live character: the last committed capture, its evaluations, the
/// catalog they were resolved against, the accepted-time map and the two dirty flags that drive the snapshot file
/// and its <c>.accepted.json</c> sidecar. Keeping it together makes the rules that tie memory and disk explicit:
/// deleting all data forgets the character so the next poll starts over and writes both files afresh; forgetting the
/// live character marks both files dirty so the next flush rewrites both, never an orphan sidecar; a catalog swap
/// makes the next poll a first pass rather than an empty diff against evaluations of another catalog.
/// Not thread-safe: the framework thread owns it.
/// </summary>
public sealed class PollerMemory
{
    public PollerMemory(TimeSpan saveInterval)
    {
        Saves = new SaveDebouncer(saveInterval);
    }

    /// <summary>When the snapshot file is due; <see cref="SaveDebouncer.Pending"/> is the snapshot's dirty flag.</summary>
    public SaveDebouncer Saves { get; }

    /// <summary>The last committed capture; null before the first pass and after <see cref="Reset"/>.</summary>
    public CharacterSnapshot? Last { get; private set; }

    /// <summary>Evaluations of <see cref="Last"/>, keyed by row id; null whenever <see cref="Last"/> is.</summary>
    public IReadOnlyDictionary<uint, QuestEvaluation>? States { get; private set; }

    /// <summary>The catalog instance <see cref="States"/> were resolved against; null whenever <see cref="Last"/> is.</summary>
    public object? Catalog { get; private set; }

    /// <summary>When each accepted quest entered the journal (runtime quest id to UTC); the sidecar's contents.</summary>
    public Dictionary<ushort, DateTime> AcceptedSince { get; private set; } = [];

    /// <summary>The sidecar differs from <see cref="AcceptedSince"/> and is rewritten on the next flush.</summary>
    public bool AcceptedSinceDirty { get; set; }

    /// <summary>True while nothing is committed: the next poll is a first pass.</summary>
    public bool IsEmpty => Last is null;

    /// <summary>
    /// The committed evaluations belong to another catalog instance (a retry replaced the bundle). Diffing the next
    /// capture against them would be an empty no-op, so the caller flushes and resets first.
    /// </summary>
    public bool IsStaleFor(object catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Last is not null && !ReferenceEquals(Catalog, catalog);
    }

    /// <summary>Records a committed capture, its evaluations and the catalog they came from, and marks the snapshot dirty.</summary>
    public void Commit(CharacterSnapshot snapshot, IReadOnlyDictionary<uint, QuestEvaluation> states, object catalog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(catalog);

        Last = snapshot;
        States = states;
        Catalog = catalog;
        Saves.MarkDirty();
    }

    /// <summary>Replaces the accepted-time map (loaded from the sidecar on a first pass).</summary>
    public void SetAcceptedSince(Dictionary<ushort, DateTime> since, bool dirty)
    {
        ArgumentNullException.ThrowIfNull(since);
        AcceptedSince = since;
        AcceptedSinceDirty = dirty;
    }

    /// <summary>Forgets everything; the next poll is a first pass. The save debouncer keeps its cadence.</summary>
    public void Reset()
    {
        Last = null;
        States = null;
        Catalog = null;
        AcceptedSince = [];
        AcceptedSinceDirty = false;
    }

    /// <summary>
    /// Every stored file was deleted (Settings › Delete all data). The memory is dropped with them: the next poll
    /// captures afresh, writes the snapshot and seeds a new sidecar, so disk and memory agree again.
    /// </summary>
    public void OnDataDeleted() => Reset();

    /// <summary>
    /// A character's snapshot and sidecar were deleted. When it is the live one, both files are marked dirty so the
    /// next flush rewrites the pair; before, only the sidecar was, which left an <c>.accepted.json</c> beside no snapshot.
    /// </summary>
    public void OnCharacterForgotten(ulong contentId)
    {
        if (Last is { } last && last.ContentId == contentId)
        {
            Saves.MarkDirty();
            AcceptedSinceDirty = true;
        }
    }
}
