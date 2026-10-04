namespace Tsukimichi.Core.Route;

/// <summary>A field aether current on a route to flying, with the aetheryte nearest where it stands (0 and empty when unplaced).</summary>
/// <param name="AetherCurrentId">AetherCurrent sheet row id.</param>
/// <param name="AetheryteId">The nearest aetheryte's row id; 0 when the game's layout does not place the current.</param>
/// <param name="AetheryteName">The nearest aetheryte's name; empty when unplaced.</param>
public sealed record FieldCurrentStop(uint AetherCurrentId, uint AetheryteId, string AetheryteName);

/// <summary>
/// The field part of a route to flying in a zone (plan v7, 1.19.0 K3; spec-1.19 "Route to unlock"): after the quests,
/// each field current the character has not attuned, as "Aether current · &lt;nearest aetheryte&gt;". Pure.
/// </summary>
public static class FieldCurrentStops
{
    /// <summary>
    /// The currents of <paramref name="all"/> still to attune, grouped by their nearest aetheryte (the groups in the
    /// order their first current comes, the zone's sheet order inside each), unplaced ones last. A current
    /// <paramref name="attuned"/> answers true for is left out; one it cannot tell (null: a stored alt) stays, since the
    /// route never claims what it does not know.
    /// </summary>
    public static IReadOnlyList<FieldCurrentStop> Left(IEnumerable<FieldCurrentStop> all, Func<uint, bool?> attuned)
    {
        ArgumentNullException.ThrowIfNull(all);
        ArgumentNullException.ThrowIfNull(attuned);
        var groups = new List<(uint Aetheryte, List<FieldCurrentStop> Stops)>();
        var unplaced = new List<FieldCurrentStop>();
        foreach (var stop in all)
        {
            if (stop is null || attuned(stop.AetherCurrentId) == true)
            {
                continue;
            }

            if (stop.AetheryteId == 0)
            {
                unplaced.Add(stop);
                continue;
            }

            var at = groups.FindIndex(g => g.Aetheryte == stop.AetheryteId);
            if (at < 0)
            {
                groups.Add((stop.AetheryteId, [stop]));
            }
            else
            {
                groups[at].Stops.Add(stop);
            }
        }

        var left = new List<FieldCurrentStop>();
        foreach (var (_, stops) in groups)
        {
            left.AddRange(stops);
        }

        left.AddRange(unplaced);
        return left;
    }

    /// <summary>
    /// The currents of a zone still to attune while where they stand is still being read (off the draw thread): every
    /// one of <paramref name="fieldCurrentIds"/> <paramref name="attuned"/> does not answer true for, in the zone's sheet
    /// order, none placed. As many as <see cref="Left"/> gives once the places land, so the route reserves their lines
    /// and nothing below moves when the places fill them in.
    /// </summary>
    public static IReadOnlyList<FieldCurrentStop> Pending(IEnumerable<uint> fieldCurrentIds, Func<uint, bool?> attuned)
    {
        ArgumentNullException.ThrowIfNull(fieldCurrentIds);
        ArgumentNullException.ThrowIfNull(attuned);
        return Left(fieldCurrentIds.Select(static id => new FieldCurrentStop(id, 0, string.Empty)), attuned);
    }

    /// <summary>The header's "N stops": the quests left on the route and the field currents left.</summary>
    public static int Stops(int questSteps, int fieldLeft) => Math.Max(0, questSteps) + Math.Max(0, fieldLeft);
}
