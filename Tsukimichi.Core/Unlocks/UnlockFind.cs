using System.Globalization;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Unlocks;

/// <summary>
/// The kinds the Unlocks filter and the search's kind chips name (plan v7, 1.19.0 K3; spec-1.19 "K3"), in the chips'
/// order. Each stands for one or more <see cref="UnlockTarget"/>s (<see cref="UnlockFindKinds.Of"/>); the rest (an action, a
/// minion, a title, a next quest) belong to no kind.
/// </summary>
public enum UnlockFindKind : byte
{
    Mount,
    Flying,

    /// <summary>A dungeon, trial, raid, field operation or any other duty.</summary>
    Duty,

    /// <summary>A game system or feature, or a general action (the Aether Compass, Sprint).</summary>
    Feature,
    Job,

    /// <summary>A zone, a world-map region, an aetheryte or an aethernet shard.</summary>
    Area,
    Emote,
    Orchestrion,
}

/// <summary>The <see cref="UnlockFindKind"/> mapping, names and bits.</summary>
public static class UnlockFindKinds
{
    /// <summary>Every kind, in the chips' order.</summary>
    public static readonly UnlockFindKind[] All =
    [
        UnlockFindKind.Mount, UnlockFindKind.Flying, UnlockFindKind.Duty, UnlockFindKind.Feature,
        UnlockFindKind.Job, UnlockFindKind.Area, UnlockFindKind.Emote, UnlockFindKind.Orchestrion,
    ];

    /// <summary>The kind a row's target belongs to; null for a target no chip names.</summary>
    public static UnlockFindKind? Of(UnlockTarget target) => target switch
    {
        UnlockTarget.Zone or UnlockTarget.WorldMap or UnlockTarget.Aetheryte or UnlockTarget.AethernetShard => UnlockFindKind.Area,
        >= UnlockTarget.Dungeon and <= UnlockTarget.OtherDuty => UnlockFindKind.Duty,
        UnlockTarget.Job => UnlockFindKind.Job,
        UnlockTarget.Flying => UnlockFindKind.Flying,
        UnlockTarget.System or UnlockTarget.GeneralAction => UnlockFindKind.Feature,
        UnlockTarget.Emote => UnlockFindKind.Emote,
        UnlockTarget.Mount => UnlockFindKind.Mount,
        UnlockTarget.Orchestrion => UnlockFindKind.Orchestrion,
        _ => null,
    };

    /// <summary>A kind's bit in <see cref="QuestUnlocks.KindMask"/>.</summary>
    public static ushort Bit(UnlockFindKind kind) => (ushort)(1 << (int)kind);

    /// <summary>The mask of a set of kinds; 0 for none.</summary>
    public static ushort Mask(IEnumerable<UnlockFindKind>? kinds)
    {
        ushort mask = 0;
        if (kinds is null)
        {
            return mask;
        }

        foreach (var kind in kinds)
        {
            mask |= Bit(kind);
        }

        return mask;
    }

    /// <summary>The chip's word: "Mount", "Flying", "Duty", "Feature", "Job", "Area", "Emote", "Orchestrion".</summary>
    public static string Name(UnlockFindKind kind) => kind switch
    {
        UnlockFindKind.Mount => CoreText.T("Core.UnlockFindKind.Mount", "Mount"),
        UnlockFindKind.Flying => CoreText.T("Core.UnlockFindKind.Flying", "Flying"),
        UnlockFindKind.Duty => CoreText.T("Core.UnlockFindKind.Duty", "Duty"),
        UnlockFindKind.Feature => CoreText.T("Core.UnlockFindKind.Feature", "Feature"),
        UnlockFindKind.Job => CoreText.T("Core.UnlockFindKind.Job", "Job"),
        UnlockFindKind.Area => CoreText.T("Core.UnlockFindKind.Area", "Area"),
        UnlockFindKind.Emote => CoreText.T("Core.UnlockFindKind.Emote", "Emote"),
        _ => CoreText.T("Core.UnlockFindKind.Orchestrion", "Orchestrion"),
    };

    /// <summary>
    /// What a find prints as its name: flying as "Flying in Thavnair" (its row holds the zone alone), any other row its
    /// own name.
    /// </summary>
    public static string Label(UnlockTarget target, string name) =>
        target == UnlockTarget.Flying && !name.StartsWith(FlyingWord, StringComparison.OrdinalIgnoreCase)
            ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.UnlockFind.FlyingIn", "Flying in {0}"), name)
            : name;

    /// <summary>"10 aether currents, 4 from quests": a flying find's summary, from the zone's own counts.</summary>
    public static string FlyingSummary(int total, int fromQuests) => string.Format(
        CultureInfo.CurrentCulture,
        total == 1 ? CoreText.T("Core.UnlockFind.CurrentsOne", "1 aether current, {1} from quests") : CoreText.T("Core.UnlockFind.Currents", "{0} aether currents, {1} from quests"),
        total,
        fromQuests);

    private const string FlyingWord = "Flying";
}

/// <summary>
/// One thing quests open, across the whole catalog (plan v7, 1.19.0 K3): what "find by unlock" lists and Route to
/// unlock routes to. Flying in a zone is one find whatever number of quest currents it has, and needs every one of
/// them (<see cref="NeedsAll"/>); any other find is opened by any one of its quests (a city two branches reach).
/// Immutable.
/// </summary>
/// <param name="Target">What it is (the first row's target; a zone and the world map of its name are one find).</param>
/// <param name="TargetId">The row id in the target's own sheet (<see cref="UnlockEntry.TargetId"/>); 0 for flying and a curated feature.</param>
/// <param name="Name">The row's own name ("Thavnair" for flying in it).</param>
/// <param name="Icon">The game icon of the first row that has one; 0 when none.</param>
/// <param name="Expansion">The expansion the target belongs to, which Sprout mode compares with the character's reach.</param>
/// <param name="Quests">The quests that open it, in catalog order.</param>
public sealed record UnlockFind(UnlockTarget Target, uint TargetId, string Name, uint Icon, byte Expansion, IReadOnlyList<uint> Quests)
{
    /// <summary>The kind its chip names; null for a target no chip names (never in <see cref="QuestUnlocks.Finds"/>).</summary>
    public UnlockFindKind? Kind => UnlockFindKinds.Of(Target);

    /// <summary>The name as printed: "Flying in Thavnair", "Kugane", "The Sirensong Sea".</summary>
    public string Label => UnlockFindKinds.Label(Target, Name);

    /// <summary>Flying needs every quest current of its zone; anything else needs any one of its quests.</summary>
    public bool NeedsAll => Target == UnlockTarget.Flying;

    /// <summary>The lowercased label, what a search term must be part of.</summary>
    public string SearchText { get; } = UnlockFindKinds.Label(Target, Name).ToLowerInvariant();
}

/// <summary>A find the search shows, with the quest its "via" line names (the first one the shield shows).</summary>
/// <param name="Find">The find.</param>
/// <param name="Via">The first quest of <see cref="UnlockFind.Quests"/> the spoiler shield shows.</param>
public sealed record UnlockMatch(UnlockFind Find, uint Via)
{
    /// <summary>
    /// The label as printed: the find's own, or for a name the wider spoiler shield hides (plan v7, 1.20.0 N6) its
    /// placeholder ("Flying in Dawntrail area 6").
    /// </summary>
    public string Label { get; init; } = Find.Label;

    /// <summary>The wider shield hides the find's name: <see cref="Label"/> holds its placeholder and the find's own art is not shown.</summary>
    public bool Hidden { get; init; }
}
