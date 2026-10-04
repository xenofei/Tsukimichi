using System.Collections.Frozen;
using System.Collections.Generic;

namespace Tsukimichi.Game;

/// <summary>
/// The quests the player set aside per character (feature plan v7 P4, "Set aside for later" and "Not for me", kept in
/// <c>user/characters.json</c> and merged across game clients like the other per-character choices). They leave My
/// blues' counts, Nearby and the server info bar, the overlay and the chat notices. Immutable copies, swapped on the
/// framework thread; a change tells the panes (<see cref="Version"/>) without resolving anything again, since a quest's
/// state does not depend on it.
/// </summary>
public sealed partial class SessionState
{
    private static readonly FrozenSet<uint> NoneSetAside = FrozenSet<uint>.Empty;

    private volatile FrozenDictionary<ulong, FrozenSet<uint>> setAside = FrozenDictionary<ulong, FrozenSet<uint>>.Empty;

    /// <summary>Moves whenever any character's set-aside quests change.</summary>
    public int SetAsideRevision { get; private set; }

    /// <summary>Whether the player set <paramref name="questRowId"/> aside on the character. Any thread.</summary>
    public bool IsSetAside(ulong contentId, uint questRowId) =>
        setAside.TryGetValue(contentId, out var rows) && rows.Contains(questRowId);

    /// <summary>The character's set-aside quests; empty for none.</summary>
    public IReadOnlySet<uint> SetAsideOf(ulong? contentId) =>
        contentId is { } id && setAside.TryGetValue(id, out var rows) ? rows : NoneSetAside;

    /// <summary>The set-aside quests of the character on view.</summary>
    public IReadOnlySet<uint> ViewedSetAside => SetAsideOf(ViewedContentId);

    /// <summary>
    /// Takes in every character's set-aside quests (framework thread). Unchanged lists do nothing; a change moves
    /// <see cref="SetAsideRevision"/> and <see cref="Version"/> so every surface that leaves them out rebuilds.
    /// </summary>
    internal void SetSetAside(IReadOnlyDictionary<ulong, IReadOnlyList<uint>> rows)
    {
        var next = Frozen(rows);
        if (SameRows(next, setAside))
        {
            return;
        }

        setAside = next;
        SetAsideRevision++;
        Bump();
    }
}
