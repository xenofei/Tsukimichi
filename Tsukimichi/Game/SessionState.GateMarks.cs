using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Tsukimichi.Game;

/// <summary>
/// "I've done this" (feature plan v7 C3): the game gates each character's player marked passed, which a gate Tsukimichi
/// cannot check honours (<see cref="Core.Evaluation.EvalContext.GateMarkedDone"/>). The marks live in
/// <c>user/characters.json</c>; the session holds an immutable copy the resolvers read from any thread, swapped on the
/// framework thread when the book changes. A new copy re-resolves the stored character on view here, and the live one
/// through the poller (<see cref="GateMarksVersion"/>).
/// </summary>
public sealed partial class SessionState
{
    private volatile FrozenDictionary<ulong, FrozenSet<uint>> gateMarks = FrozenDictionary<ulong, FrozenSet<uint>>.Empty;

    /// <summary>Moves on every change of the marks; the poller resolves the live character again when it does.</summary>
    public int GateMarksVersion { get; private set; }

    /// <summary>Whether the player marked the game gate of <paramref name="questRowId"/> passed on the character. Any thread.</summary>
    public bool IsGateMarkedDone(ulong contentId, uint questRowId) =>
        gateMarks.TryGetValue(contentId, out var rows) && rows.Contains(questRowId);

    /// <summary>
    /// Takes in every character's marks (framework thread). Unchanged marks do nothing; changed ones move
    /// <see cref="GateMarksVersion"/> and resolve the stored character on view again.
    /// </summary>
    internal void SetGateMarks(IReadOnlyDictionary<ulong, IReadOnlyList<uint>> marks)
    {
        var next = marks.Where(kv => kv.Value.Count > 0).ToFrozenDictionary(kv => kv.Key, kv => kv.Value.ToFrozenSet());
        var current = gateMarks;
        if (next.Count == current.Count && next.All(kv => current.TryGetValue(kv.Key, out var rows) && rows.SetEquals(kv.Value)))
        {
            return;
        }

        gateMarks = next;
        GateMarksVersion++;
        if (!IsLive && ViewedSnapshot is { } viewed && Bundle is { } bundle && pendingView is not { Refresh: false })
        {
            StartView(viewed.ContentId, bundle, viewed, refresh: true);
        }

        Bump();
    }
}
