using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Game;

/// <summary>
/// The game's answers (feature plan v7 C1; spec-1.19 "C1. The game confirms it"): the quests the game itself showed each
/// character as available (<see cref="OfferObserver"/>'s sightings), which turn a Not checked quest Ready
/// (<see cref="Core.Evaluation.EvalContext.GameOffered"/>), and the player's per-character "Go with the game" choices
/// (<c>user/characters.json</c>), which turn a quest Tsukimichi reads Blocked Ready until taken back
/// (<see cref="Core.Evaluation.EvalContext.GoWithGame"/>). Immutable copies the resolvers read from any thread, swapped
/// on the framework thread like the gate marks; a change of which quests they name re-resolves the character (stored
/// here, live through the poller, <see cref="AnswersVersion"/>).
/// </summary>
public sealed partial class SessionState
{
    private volatile FrozenDictionary<ulong, FrozenSet<uint>> goWithGame = FrozenDictionary<ulong, FrozenSet<uint>>.Empty;
    private volatile FrozenDictionary<ulong, FrozenDictionary<uint, OfferSighting>> gameOffers = FrozenDictionary<ulong, FrozenDictionary<uint, OfferSighting>>.Empty;

    /// <summary>Moves whenever a sighting the session holds changes (a new quest, a new source, a later last-seen time).</summary>
    public int GameOffersRevision { get; private set; }

    /// <summary>Whether the player chose "Go with the game" for <paramref name="questRowId"/> on the character. Any thread.</summary>
    public bool IsGoWithGame(ulong contentId, uint questRowId) =>
        goWithGame.TryGetValue(contentId, out var rows) && rows.Contains(questRowId);

    /// <summary>Whether the game showed <paramref name="questRowId"/> to the character as available. Any thread.</summary>
    public bool IsGameOffered(ulong contentId, uint questRowId) =>
        gameOffers.TryGetValue(contentId, out var rows) && rows.ContainsKey(questRowId);

    /// <summary>The game's sighting of <paramref name="questRowId"/> for the character; null when it never showed it (this session).</summary>
    public OfferSighting? GameOffer(ulong contentId, uint questRowId) =>
        gameOffers.TryGetValue(contentId, out var rows) && rows.TryGetValue(questRowId, out var sighting) ? sighting : null;

    /// <summary>
    /// Takes in every character's "Go with the game" choices (framework thread). Unchanged choices do nothing; changed
    /// ones move <see cref="AnswersVersion"/> and resolve the stored character on view again.
    /// </summary>
    internal void SetGoWithGame(IReadOnlyDictionary<ulong, IReadOnlyList<uint>> choices)
    {
        var next = Frozen(choices);
        if (SameRows(next, goWithGame))
        {
            return;
        }

        goWithGame = next;
        AnswersChanged(viewedChanged: true);
    }

    /// <summary>
    /// The observer's sightings for the logged-in character (framework thread). Kept for the session after the character
    /// logs out, so its stored view reads as it did. A change of which quests the game showed moves
    /// <see cref="AnswersVersion"/>; a later time only <see cref="GameOffersRevision"/>.
    /// </summary>
    internal void SetGameOffers(ulong contentId, IReadOnlyDictionary<ushort, OfferSighting> book)
    {
        if (contentId == 0)
        {
            return;
        }

        var current = gameOffers;
        current.TryGetValue(contentId, out var held);
        if (book.Count == 0 && held is null)
        {
            return;
        }

        var rows = book.Values.ToFrozenDictionary(static s => s.RowId);
        var sameQuests = held is not null && held.Count == rows.Count && rows.Keys.All(held.ContainsKey);
        if (sameQuests && rows.All(kv => held![kv.Key] == kv.Value))
        {
            return;
        }

        var next = current.ToDictionary(static kv => kv.Key, static kv => kv.Value);
        if (rows.Count == 0)
        {
            next.Remove(contentId);
        }
        else
        {
            next[contentId] = rows;
        }

        gameOffers = next.ToFrozenDictionary();
        GameOffersRevision++;
        if (sameQuests)
        {
            // Only a time moved: the surfaces that show it rebuild on the revision; nothing resolves again.
            return;
        }

        AnswersChanged(viewedChanged: ViewedContentId == contentId);
    }

    /// <summary>Drops a character's sightings (forgotten, or every character's data deleted when <paramref name="contentId"/> is null).</summary>
    internal void ClearGameOffers(ulong? contentId)
    {
        var current = gameOffers;
        if (contentId is { } id ? !current.ContainsKey(id) : current.Count == 0)
        {
            return;
        }

        gameOffers = contentId is { } drop
            ? current.Where(kv => kv.Key != drop).ToFrozenDictionary(static kv => kv.Key, static kv => kv.Value)
            : FrozenDictionary<ulong, FrozenDictionary<uint, OfferSighting>>.Empty;
        GameOffersRevision++;
        AnswersChanged(viewedChanged: contentId is null || ViewedContentId == contentId);
    }
}
