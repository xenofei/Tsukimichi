using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// One region constellation (docs/design/v7/ui/spec.md §3.5): our own motif, not an in-game star chart, drawn in the
/// Astrologian card idiom: round stars at the joints, hairline links and a brighter lead star (the first point).
/// <see cref="Points"/> sit in a unit box (0..1 each way); <see cref="Edges"/> join two of them by index.
/// </summary>
/// <param name="Name">The motif's name, for the design notes and tests (never shown).</param>
/// <param name="Points">5–8 stars in the unit box, the lead star first.</param>
/// <param name="Edges">The links between them.</param>
public sealed record Constellation(string Name, Vector2[] Points, (int From, int To)[] Edges);

/// <summary>
/// The six constellations, one per expansion region (spec §3.5): A Realm Reborn's Chocobo (a bird in profile, not a
/// crystal), Heavensward's Wyrm, Stormblood's Lotus, Shadowbringers' Tower (a tall spire, not a plus sign),
/// Endwalker's Crescent and Dawntrail's Plume. The sky shows one at a time, for the selected quest's expansion.
/// </summary>
public static class Constellations
{
    /// <summary>The lead star's index in every constellation.</summary>
    public const int Lead = 0;

    /// <summary>The box a constellation needs, clear of every item, in logical px (spec §3.5).</summary>
    public static readonly Vector2 ClearBoxLogical = new(120f, 100f);

    /// <summary>The size it is drawn at, in logical px: between 70 and 90 (spec §3.5).</summary>
    public const float SizeLogical = 80f;

    /// <summary>A Realm Reborn: beak, head, neck, back, tail, body and two legs.</summary>
    public static readonly Constellation Chocobo = new(
        "The Chocobo",
        [new(0.20f, 0.12f), new(0f, 0.22f), new(0.30f, 0.46f), new(0.66f, 0.38f), new(1f, 0.18f), new(0.60f, 0.68f), new(0.46f, 1f), new(0.74f, 1f)],
        [(1, 0), (0, 2), (2, 3), (3, 4), (2, 5), (5, 3), (5, 6), (5, 7)]);

    /// <summary>Heavensward: a wyrm's long neck rising to its head, one wing.</summary>
    public static readonly Constellation Wyrm = new(
        "The Wyrm",
        [new(1f, 0.06f), new(0.82f, 0.36f), new(0.60f, 0.30f), new(0.42f, 0.56f), new(0.20f, 0.50f), new(0f, 0.78f), new(0.66f, 0.66f)],
        [(5, 4), (4, 3), (3, 2), (2, 1), (1, 0), (2, 6)]);

    /// <summary>Stormblood: five petals round a heart.</summary>
    public static readonly Constellation Lotus = new(
        "The Lotus",
        [new(0.50f, 0.55f), new(0.50f, 0.08f), new(0.12f, 0.38f), new(0.88f, 0.38f), new(0.28f, 0.90f), new(0.72f, 0.90f)],
        [(0, 1), (0, 2), (0, 3), (0, 4), (0, 5)]);

    /// <summary>Shadowbringers: a tall, narrow spire, two parallel stems converging to a crown star.</summary>
    public static readonly Constellation Tower = new(
        "The Tower",
        [new(0.50f, 0f), new(0.40f, 0.30f), new(0.60f, 0.30f), new(0.40f, 0.66f), new(0.60f, 0.66f), new(0.36f, 1f), new(0.64f, 1f)],
        [(0, 1), (0, 2), (1, 3), (2, 4), (3, 5), (4, 6)]);

    /// <summary>Endwalker: a waxing crescent's arc.</summary>
    public static readonly Constellation Crescent = new(
        "The Crescent",
        [new(0.72f, 0.02f), new(0.36f, 0.14f), new(0.12f, 0.42f), new(0.14f, 0.70f), new(0.40f, 0.94f), new(0.74f, 0.98f)],
        [(0, 1), (1, 2), (2, 3), (3, 4), (4, 5)]);

    /// <summary>Dawntrail: a feather's quill with its vanes.</summary>
    public static readonly Constellation Plume = new(
        "The Plume",
        [new(0.86f, 0.04f), new(0.58f, 0.38f), new(0.34f, 0.70f), new(0.10f, 1f), new(0.62f, 0.78f), new(0.86f, 0.50f), new(0.30f, 0.34f)],
        [(3, 2), (2, 1), (1, 0), (2, 4), (1, 5), (1, 6)]);

    /// <summary>Every constellation, in expansion order (A Realm Reborn first).</summary>
    public static IReadOnlyList<Constellation> All { get; } = [Chocobo, Wyrm, Lotus, Tower, Crescent, Plume];

    /// <summary>The constellation of <paramref name="expansion"/> (0 = A Realm Reborn … 5 = Dawntrail); null for any other.</summary>
    public static Constellation? For(byte expansion) => expansion < All.Count ? All[expansion] : null;
}
