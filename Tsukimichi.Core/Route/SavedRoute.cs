namespace Tsukimichi.Core.Route;

/// <summary>
/// The route a character follows, as the plugin's configuration stores it (1.6.0, R6 A): the target, not its steps
/// and not pins, so the steps are worked out again from the character's states every time and completed ones drop
/// off. Plain settable properties for the configuration serializer; <see cref="ToTarget"/> and <see cref="From"/>
/// convert.
/// </summary>
public sealed class SavedRoute
{
    /// <summary>The character the route was followed for (content id); the route shows for that character only.</summary>
    public ulong OwnerContentId { get; set; }

    public RouteTargetKind Kind { get; set; }

    public string Label { get; set; } = string.Empty;

    public List<uint> QuestRowIds { get; set; } = [];

    /// <summary>The target's own game icon (<see cref="RouteTarget.Icon"/>); 0 when it has none, and in a file written before 1.15.</summary>
    public uint Icon { get; set; }

    /// <summary>A route to flying in a zone: the zone's territory (<see cref="RouteTarget.FlyingTerritory"/>); 0 otherwise, and in a file written before 1.19.</summary>
    public uint FlyingTerritory { get; set; }

    /// <summary>The parts of a route to several targets; empty for a single target.</summary>
    public List<SavedRoutePart> Parts { get; set; } = [];

    /// <summary>A route to Triple Triad opponents: the opponents after the quests (<see cref="RouteTarget.TriadStops"/>); empty otherwise, and in a file written before 1.21.</summary>
    public List<SavedTriadStop> TriadStops { get; set; } = [];

    /// <summary>
    /// The kind of the name the wider spoiler shield places the target by (<see cref="RouteTarget.Placed"/>, "The Ruby
    /// Sea" for "Flying in The Ruby Sea"); null when the label is read by the target's kind, and in a file written before 1.21.1.
    /// </summary>
    public Query.SpoilerKind? PlacedKind { get; set; }

    /// <summary>The name of <see cref="PlacedKind"/>; null with it.</summary>
    public string? PlacedName { get; set; }

    /// <summary>The target to build the route from; a part or list the file left null reads as empty.</summary>
    public RouteTarget ToTarget()
    {
        var quests = QuestRowIds ?? [];
        var stops = (TriadStops ?? []).Where(static s => s is not null).Select(static s => new TriadStop(s.ResidentId, s.Name ?? string.Empty, s.Zone ?? string.Empty, s.TerritoryId, s.X, s.Z)).ToArray();
        (Query.SpoilerKind Kind, string Name)? placed = PlacedKind is { } kind && PlacedName is { Length: > 0 } name ? (kind, name) : null;
        var target = new RouteTarget(Kind, Label ?? string.Empty, quests.ToArray()) { Icon = Icon, FlyingTerritory = FlyingTerritory, TriadStops = stops, Placed = placed };
        if (Parts is not { Count: > 0 } parts)
        {
            return target;
        }

        var list = new List<RouteTarget>(parts.Count);
        foreach (var part in parts)
        {
            if (part is not null)
            {
                list.Add(new RouteTarget(part.Kind, part.Label ?? string.Empty, (part.QuestRowIds ?? []).ToArray()) { Icon = part.Icon });
            }
        }

        return RouteTarget.Union(Kind, Label ?? string.Empty, list) with { Icon = Icon, FlyingTerritory = FlyingTerritory, TriadStops = stops, Placed = placed };
    }

    /// <summary>What to store for <paramref name="target"/> followed by <paramref name="ownerContentId"/>.</summary>
    public static SavedRoute From(RouteTarget target, ulong ownerContentId)
    {
        ArgumentNullException.ThrowIfNull(target);
        var saved = new SavedRoute
        {
            OwnerContentId = ownerContentId,
            Kind = target.Kind,
            Label = target.Label,
            QuestRowIds = [.. target.QuestRowIds],
            Icon = target.Icon,
            FlyingTerritory = target.FlyingTerritory,
            PlacedKind = target.Placed?.Kind,
            PlacedName = target.Placed?.Name,
        };
        foreach (var part in target.Parts)
        {
            saved.Parts.Add(new SavedRoutePart { Kind = part.Kind, Label = part.Label, QuestRowIds = [.. part.QuestRowIds], Icon = part.Icon });
        }

        foreach (var stop in target.TriadStops)
        {
            saved.TriadStops.Add(new SavedTriadStop { ResidentId = stop.ResidentId, Name = stop.Name, Zone = stop.Zone, TerritoryId = stop.TerritoryId, X = stop.X, Z = stop.Z });
        }

        return saved;
    }

    /// <summary>Whether this is the route to <paramref name="target"/> (same kind, label, quests and parts).</summary>
    public bool Matches(RouteTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Kind != target.Kind || !string.Equals(Label, target.Label, StringComparison.Ordinal) || !(QuestRowIds ?? []).SequenceEqual(target.QuestRowIds))
        {
            return false;
        }

        var parts = Parts ?? [];
        if (parts.Count != target.Parts.Count)
        {
            return false;
        }

        for (var i = 0; i < parts.Count; i++)
        {
            var a = parts[i];
            var b = target.Parts[i];
            if (a is null || a.Kind != b.Kind || !string.Equals(a.Label, b.Label, StringComparison.Ordinal) || !(a.QuestRowIds ?? []).SequenceEqual(b.QuestRowIds))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>One part of a stored route to several targets.</summary>
public sealed class SavedRoutePart
{
    public RouteTargetKind Kind { get; set; }

    public string Label { get; set; } = string.Empty;

    public List<uint> QuestRowIds { get; set; } = [];

    /// <summary>The part's own game icon (<see cref="RouteTarget.Icon"/>); 0 when it has none.</summary>
    public uint Icon { get; set; }
}

/// <summary>One Triple Triad opponent of a stored route to opponents (<see cref="TriadStop"/>).</summary>
public sealed class SavedTriadStop
{
    public uint ResidentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Zone { get; set; } = string.Empty;

    public uint TerritoryId { get; set; }

    public float X { get; set; }

    public float Z { get; set; }
}
