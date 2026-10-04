using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Tsukimichi.Game;

/// <summary>
/// "I've done this" (feature plan v7 C3): the game gates each character's player marked passed, which a gate Tsukimichi
/// cannot check honours (<see cref="Core.Evaluation.EvalContext.GateMarkedDone"/>). The marks live in
/// <c>user/characters.json</c>; the session holds an immutable copy the resolvers read from any thread, swapped on the
/// framework thread when the book changes. A new copy re-resolves the stored character on view here, and the live one
/// through the poller (<see cref="AnswersVersion"/>). The game's own answers (C1, SessionState.GameAnswers.cs) follow
/// the same path.
/// </summary>
public sealed partial class SessionState
{
    private volatile FrozenDictionary<ulong, FrozenSet<uint>> gateMarks = FrozenDictionary<ulong, FrozenSet<uint>>.Empty;

    /// <summary>
    /// Moves on every change of what the player or the game answered for a quest's state: the "I've done this" marks,
    /// the "Go with the game" choices and the quests the game offered. The poller resolves the live character again when
    /// it does.
    /// </summary>
    public int AnswersVersion { get; private set; }

    /// <summary>Whether the player marked the game gate of <paramref name="questRowId"/> passed on the character. Any thread.</summary>
    public bool IsGateMarkedDone(ulong contentId, uint questRowId) =>
        gateMarks.TryGetValue(contentId, out var rows) && rows.Contains(questRowId);

    /// <summary>
    /// Takes in every character's marks (framework thread). Unchanged marks do nothing; changed ones move
    /// <see cref="AnswersVersion"/> and resolve the stored character on view again.
    /// </summary>
    internal void SetGateMarks(IReadOnlyDictionary<ulong, IReadOnlyList<uint>> marks)
    {
        var next = Frozen(marks);
        if (SameRows(next, gateMarks))
        {
            return;
        }

        gateMarks = next;
        AnswersChanged(viewedChanged: true);
    }

    /// <summary>
    /// After a change of the answers: moves <see cref="AnswersVersion"/>, resolves the stored character on view again
    /// when <paramref name="viewedChanged"/> says its answers moved, and tells the panes.
    /// </summary>
    private void AnswersChanged(bool viewedChanged)
    {
        AnswersVersion++;
        if (viewedChanged && !IsLive && ViewedSnapshot is { } viewed && Bundle is { } bundle && pendingView is not { Refresh: false })
        {
            StartView(viewed.ContentId, bundle, viewed, refresh: true);
        }

        Bump();
    }

    private static FrozenDictionary<ulong, FrozenSet<uint>> Frozen(IReadOnlyDictionary<ulong, IReadOnlyList<uint>> rows) =>
        rows.Where(kv => kv.Value.Count > 0).ToFrozenDictionary(kv => kv.Key, kv => kv.Value.ToFrozenSet());

    private static bool SameRows(FrozenDictionary<ulong, FrozenSet<uint>> a, FrozenDictionary<ulong, FrozenSet<uint>> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var rows) && rows.SetEquals(kv.Value));
}
