using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// The seasonal events running on the server, with their phases (parallel lists, as in
/// <see cref="CharacterSnapshot.ActiveFestivals"/> and <see cref="CharacterSnapshot.ActiveFestivalPhases"/>).
/// Festivals run server-wide, so while someone is logged in the live character's flags are the truth for every
/// character, a stored alt included; a stored snapshot's own flags only say what ran when it was saved.
/// <see cref="For"/> makes that choice; <see cref="EvalContext.ServerFestivals"/> and
/// <see cref="Seasonal.SeasonalNow.Running(QuestCatalog, ServerFestivals, IReadOnlyDictionary{uint, QuestEvaluation}, IReadOnlyDictionary{ushort, FestivalInfo}, DateTime)"/> take the result.
/// </summary>
public sealed record ServerFestivals(IReadOnlyList<ushort> Ids, IReadOnlyList<ushort> Phases)
{
    public static readonly ServerFestivals None = new([], []);

    /// <summary>The flags <paramref name="snapshot"/> was captured with, as they are.</summary>
    public static ServerFestivals Of(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(snapshot.ActiveFestivals, snapshot.ActiveFestivalPhases);
    }

    /// <summary>
    /// The events running for whichever character is viewed: the live character's flags when someone is logged in
    /// (<paramref name="live"/>); otherwise the viewed snapshot's, less any flag whose curated end has passed
    /// (before <paramref name="nowUtc"/>) while the snapshot was taken before that end, since such a flag cannot be a
    /// later run. A flag taken after its end (the game still running the event late, or a rerun of the id), or one
    /// without a curated end, is kept. No snapshot at all is <see cref="None"/>.
    /// </summary>
    public static ServerFestivals For(
        CharacterSnapshot? viewed,
        CharacterSnapshot? live,
        IReadOnlyDictionary<ushort, FestivalInfo> curated,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(curated);
        if (live is not null)
        {
            return Of(live);
        }

        if (viewed is null)
        {
            return None;
        }

        var active = viewed.ActiveFestivals;
        if (!active.Any(Stale))
        {
            return Of(viewed);
        }

        // Phases are a prefix-parallel list (an id past its end has an unknown phase), so a kept id's phase is added
        // only while its index has one, which keeps the result parallel too.
        var known = viewed.ActiveFestivalPhases;
        var ids = new List<ushort>(active.Count);
        var phases = new List<ushort>(known.Count);
        for (var i = 0; i < active.Count; i++)
        {
            if (Stale(active[i]))
            {
                continue;
            }

            ids.Add(active[i]);
            if (i < known.Count)
            {
                phases.Add(known[i]);
            }
        }

        return new ServerFestivals(ids, phases);

        bool Stale(ushort id) =>
            curated.TryGetValue(id, out var info) && info.End is { } end && end < nowUtc && viewed.TakenUtc <= end;
    }

    /// <summary>Whether <paramref name="festival"/> is running.</summary>
    public bool Contains(ushort festival)
    {
        for (var i = 0; i < Ids.Count; i++)
        {
            if (Ids[i] == festival)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The phase of a running festival, or null when it is not running or its phase was not captured.</summary>
    public ushort? Phase(ushort festival)
    {
        for (var i = 0; i < Ids.Count; i++)
        {
            if (Ids[i] == festival)
            {
                return i < Phases.Count ? Phases[i] : null;
            }
        }

        return null;
    }

    /// <summary>Same ids and phases in the same order; an evaluation made with one holds for the other.</summary>
    public bool SameAs(ServerFestivals? other) =>
        other is not null && Ids.SequenceEqual(other.Ids) && Phases.SequenceEqual(other.Phases);
}
