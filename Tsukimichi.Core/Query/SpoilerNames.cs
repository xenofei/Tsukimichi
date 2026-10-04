using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.Core.Query;

/// <summary>The kinds of name the wider spoiler shield (plan v7, 1.20.0 N6) places in the story.</summary>
public enum SpoilerKind : byte
{
    /// <summary>A zone or city, a world-map region, a zone's region, flying in a zone.</summary>
    Area,

    /// <summary>A dungeon, trial, raid, field operation or any other duty.</summary>
    Duty,

    /// <summary>A quest reward (an item, a mount, an emote, a title) or any other thing a quest opens (a job, a feature, an action).</summary>
    Reward,

    /// <summary>A person: a quest's giver.</summary>
    Npc,

    /// <summary>An aetheryte or aethernet shard: hidden while its area is (spec-1.20 N6).</summary>
    Aetheryte,
}

/// <summary>
/// Where in the main scenario each zone, aetheryte, duty, reward and NPC name stops being a spoiler (plan v7, 1.20.0
/// N6), and what each prints while it is one: the data the wider spoiler shield reads, built once per catalog and
/// unlock index, so new zones, duties and people of a later patch are placed by the same rule after a data refresh.
/// <para>
/// <b>Story anchor of a quest.</b> A main scenario quest is its own anchor. Any other quest is anchored at the latest
/// main scenario quest it needs: through its previous quests (<see cref="QuestCatalog.PrerequisitesOf"/>, walked to the
/// end; an Any join takes its earliest alternative), and through the zone its giver stands in (the first main scenario
/// quest that opens that zone). A quest nothing places in the story has no anchor.
/// </para>
/// <para>
/// <b>Introducing quests of a name.</b> An area: the quests the unlock index says open it (a zone, a world map, flying
/// in it), and the region of a zone with it; a zone no quest opens is introduced by the quests given in it. An
/// aetheryte: its area (it follows the area's rule). A duty: the quests that open it. A reward: the quests that give
/// it, and those that open it (a job, a feature, an action). A person: the quests they give. Names are matched
/// whatever their case. Every town and field zone is placed, with its expansion, even when no quest introduces it.
/// </para>
/// <para>
/// <b>The rule</b> (<see cref="SpoilerMask.IsNameMasked"/>): a name is shown once any quest that introduces it is
/// anchored at a quest the shield shows (done, in the journal, or within "names ahead"). Otherwise an area, aetheryte
/// or duty of an expansion past the one the story has reached is masked; and any name whose introducing quests are all
/// anchored (every anchor then masked) is masked. A name introduced by an unanchored quest, or one the data does not
/// place, is shown: the shield never guesses.
/// </para>
/// <para>
/// <b>The placeholders</b> (spec-1.20, "What it hides"): a kind word with a safe locator, never a glyph. An area reads
/// "Dawntrail area 6", where 6 is its place among the expansion's zones in the game's own <c>TerritoryType</c> order
/// (the same number everywhere); an aetheryte "Dawntrail aetheryte · area 6"; a duty its content type and level,
/// "Dungeon (Lv 97)"; a reward its kind, "A mount"; a person "Dawntrail character". The locators keep a non-breaking
/// space. Expansion names come from the game's own sheet (<c>ExVersion</c>), so a new expansion names itself.
/// </para>
/// Immutable but for the placeholders' text, formatted on first use per language; lookups allocate nothing.
/// </summary>
public sealed class SpoilerNames
{
    private const int KindCount = 5;

    /// <summary>Places nothing: no catalog or unlock index yet.</summary>
    public static readonly SpoilerNames Empty = new(
        [.. Enumerable.Range(0, KindCount).Select(static _ => FrozenDictionary<string, Placed>.Empty)],
        FrozenDictionary<uint, uint>.Empty,
        FrozenDictionary<uint, byte>.Empty,
        [],
        null);

    /// <summary>Compares a (kind, name) pair the way names are placed: the kind exactly, the name ignoring case.</summary>
    public static readonly IEqualityComparer<(SpoilerKind Kind, string Name)> NameComparer = new KindNameComparer();

    private readonly FrozenDictionary<string, Placed>[] byKind;
    private readonly FrozenDictionary<uint, uint> anchors;
    private readonly FrozenDictionary<uint, byte> storyExpansion;
    private readonly Shape[] shapes;
    private readonly Func<byte, string>? expansionName;
    private Texts? texts;

    private SpoilerNames(FrozenDictionary<string, Placed>[] byKind, FrozenDictionary<uint, uint> anchors, FrozenDictionary<uint, byte> storyExpansion, Shape[] shapes, Func<byte, string>? expansionName)
    {
        this.byKind = byKind;
        this.anchors = anchors;
        this.storyExpansion = storyExpansion;
        this.shapes = shapes;
        this.expansionName = expansionName;
        foreach (var names in byKind)
        {
            Count += names.Count;
        }
    }

    /// <summary>
    /// Where one name sits in the story and what it prints while hidden.
    /// </summary>
    /// <param name="Anchors">The story anchors of its introducing quests (distinct, earliest first); empty when none is anchored.</param>
    /// <param name="Unanchored">A quest that introduces it has no story anchor: the anchors alone never hide it.</param>
    /// <param name="Expansion">The thing's expansion (an area's or duty's ExVersion; a reward's or person's first anchor's); <see cref="byte.MaxValue"/> when unknown.</param>
    /// <param name="Zone">An aetheryte's area, whose rule it follows; null otherwise.</param>
    /// <param name="Shape">Index of its placeholder in the names' table.</param>
    public readonly record struct Placed(uint[] Anchors, bool Unanchored, byte Expansion, string? Zone, int Shape);

    /// <summary>How many names are placed, every kind together.</summary>
    public int Count { get; }

    /// <summary>Where a name sits; false for a name nothing places (it is always shown).</summary>
    public bool TryGet(SpoilerKind kind, string? name, out Placed placed)
    {
        placed = default;
        return !string.IsNullOrEmpty(name) && (uint)kind < (uint)byKind.Length && byKind[(int)kind].TryGetValue(name, out placed);
    }

    /// <summary>Every placed name of <paramref name="kind"/> (for counting what a mask hides).</summary>
    public IEnumerable<KeyValuePair<string, Placed>> All(SpoilerKind kind) => (uint)kind < (uint)byKind.Length ? byKind[(int)kind] : [];

    /// <summary>The quest's story anchor (a main scenario quest's row id); 0 when nothing places it.</summary>
    public uint AnchorOf(uint rowId) => anchors.GetValueOrDefault(rowId);

    /// <summary>
    /// The expansion of a story anchor (a main scenario quest's row id, as <see cref="Placed.Anchors"/> hold them);
    /// <see cref="byte.MaxValue"/> for a row the story does not hold. Allocates nothing.
    /// </summary>
    public byte AnchorExpansion(uint rowId) => storyExpansion.TryGetValue(rowId, out var expansion) ? expansion : byte.MaxValue;

    /// <summary>
    /// What a placed name prints while hidden ("Dawntrail area 6", "Dungeon (Lv 97)", "A mount"), in the UI language;
    /// formatted once per name shape and language. Allocates nothing after the first call.
    /// </summary>
    public string Placeholder(in Placed placed) => TextsNow().Display(placed.Shape, this);

    /// <summary>
    /// The short form a slot that holds a name and a place keeps when room runs out (spec-1.20: the place is cut to
    /// its locator first): "area 6" for an area; the whole placeholder for anything else.
    /// </summary>
    public string Locator(in Placed placed) => TextsNow().Locator(placed.Shape, this);

    /// <summary>The lowercased placeholder, with plain spaces, that search matches in place of the hidden name.</summary>
    public string SearchText(in Placed placed) => TextsNow().Search(placed.Shape, this);

    private Texts TextsNow()
    {
        var current = Volatile.Read(ref texts);
        if (current is null || current.Version != CoreText.Version)
        {
            current = new Texts(CoreText.Version, shapes.Length);
            Volatile.Write(ref texts, current);
        }

        return current;
    }

    /// <summary>
    /// The names "Reveal names in this quest" reveals (spec-1.20 N6): the quest's giver and place (with its region), the
    /// duties it needs, its rewards and what it opens (an aetheryte with its area). Duplicates are left to the caller's set.
    /// </summary>
    /// <param name="quest">The quest.</param>
    /// <param name="unlocks">What quests open; <see cref="QuestUnlocks.Empty"/> leaves the unlocks out.</param>
    /// <param name="place">The zone the giver stands in (the map's place name); null when unknown.</param>
    /// <param name="region">That zone's region; null when unknown.</param>
    /// <param name="duties">The duties the quest needs, by name ("How you'll clear it"); null for none.</param>
    public static List<(SpoilerKind Kind, string Name)> NamesIn(QuestRecord quest, QuestUnlocks unlocks, string? place = null, string? region = null, IEnumerable<string>? duties = null)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(unlocks);
        var names = new List<(SpoilerKind Kind, string Name)>();
        void Add(SpoilerKind kind, string? name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                names.Add((kind, name));
            }
        }

        Add(SpoilerKind.Npc, quest.Issuer?.Name);
        Add(SpoilerKind.Area, place);
        Add(SpoilerKind.Area, region);
        foreach (var duty in duties ?? [])
        {
            Add(SpoilerKind.Duty, duty);
        }

        foreach (var reward in quest.Rewards)
        {
            Add(SpoilerKind.Reward, reward.Name);
        }

        foreach (var entry in unlocks.ExtraRewards(quest.RowId))
        {
            Add(SpoilerKind.Reward, entry.Name);
        }

        foreach (var entry in unlocks.IncludingRewards(quest.RowId))
        {
            if (KindOf(entry.Target) is { } kind)
            {
                Add(kind, entry.Name);
                if (kind == SpoilerKind.Aetheryte)
                {
                    Add(SpoilerKind.Area, entry.Detail);
                }
            }
        }

        return names;
    }

    /// <summary>The kind an unlock row's name is placed under; null for a next quest (the quest shield names it).</summary>
    public static SpoilerKind? KindOf(UnlockTarget target) => UnlockTargets.GroupOf(target) switch
    {
        UnlockGroup.Area => SpoilerKind.Area,
        UnlockGroup.Aetheryte => SpoilerKind.Aetheryte,
        UnlockGroup.Duty => SpoilerKind.Duty,
        UnlockGroup.NextQuest => null,
        _ => target == UnlockTarget.Flying ? SpoilerKind.Area : SpoilerKind.Reward,
    };

    /// <summary>
    /// Places every name the catalog and its unlock index introduce.
    /// </summary>
    /// <param name="catalog">The quest catalog.</param>
    /// <param name="unlocks">What every quest opens; <see cref="QuestUnlocks.Empty"/> places only rewards and people.</param>
    /// <param name="zones">Every town and field zone (<see cref="UnlockLinks.Zones"/>), for the zone a giver stands in, a zone's region and the area numbers.</param>
    /// <param name="expansionName">The game's name of an expansion (its <c>ExVersion</c> row); null or an empty answer falls back to <see cref="Expansions.Name"/>.</param>
    public static SpoilerNames Build(QuestCatalog catalog, QuestUnlocks unlocks, IReadOnlyList<UnlockZone>? zones = null, Func<byte, string>? expansionName = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(unlocks);
        if (catalog.Count == 0)
        {
            return Empty;
        }

        var graph = MsqGraph.For(catalog);
        var storyIndex = new Dictionary<uint, int>(graph.Story.Count);
        for (var i = 0; i < graph.Story.Count; i++)
        {
            storyIndex.TryAdd(graph.Story[i].RowId, i);
        }

        var zoneById = new Dictionary<uint, UnlockZone>();
        foreach (var zone in zones ?? [])
        {
            zoneById.TryAdd(zone.TerritoryId, zone);
        }

        // Each area's number: its place among its expansion's zones in the sheet's order, one number per name.
        var areas = AreaNumbers(zoneById.Values);

        // The first main scenario quest that opens each zone: where a quest given there sits at the earliest.
        var zoneAnchor = new Dictionary<uint, int>();
        foreach (var quest in graph.Story)
        {
            foreach (var entry in unlocks.IncludingRewards(quest.RowId))
            {
                if (entry.Target == UnlockTarget.Zone && entry.TargetId != 0 && !zoneAnchor.ContainsKey(entry.TargetId))
                {
                    zoneAnchor[entry.TargetId] = storyIndex[quest.RowId];
                }
            }
        }

        var anchorIndex = new Anchors(catalog, storyIndex, zoneAnchor);
        var anchorRows = new Dictionary<uint, uint>();
        foreach (var quest in catalog.All)
        {
            var at = anchorIndex.Of(quest);
            if (at >= 0)
            {
                anchorRows[quest.RowId] = graph.Story[at].RowId;
            }
        }

        var builders = new Dictionary<string, Introduced>[KindCount];
        for (var i = 0; i < builders.Length; i++)
        {
            builders[i] = new Dictionary<string, Introduced>(StringComparer.OrdinalIgnoreCase);
        }

        Introduced Of(SpoilerKind kind, string name, Shape shape)
        {
            var names = builders[(int)kind];
            if (!names.TryGetValue(name, out var introduced))
            {
                names[name] = introduced = new Introduced(shape);
            }

            return introduced;
        }

        void Add(SpoilerKind kind, string name, QuestRecord? quest, Shape shape)
        {
            if (name.Length == 0)
            {
                return;
            }

            var introduced = Of(kind, name, shape);
            if (quest is not null)
            {
                introduced.Add(anchorIndex.Of(quest));
            }
        }

        Shape AreaShape(string name, byte fallbackExpansion) =>
            areas.TryGetValue(name, out var area) ? new Shape(Word.Area, area.Expansion, area.Number, 0) : new Shape(Word.Area, fallbackExpansion, 0, 0);

        // Every town and field zone, and every zone's region, with its expansion: the expansion rule needs no quest.
        foreach (var zone in zoneById.Values)
        {
            Add(SpoilerKind.Area, zone.Name, null, AreaShape(zone.Name, zone.Expansion));
            Add(SpoilerKind.Area, zone.Region, null, new Shape(Word.Region, zone.Expansion, 0, 0));
        }

        var opened = new HashSet<uint>();
        foreach (var quest in catalog.All)
        {
            foreach (var entry in unlocks.IncludingRewards(quest.RowId))
            {
                if (KindOf(entry.Target) is not { } kind)
                {
                    continue;
                }

                switch (kind)
                {
                    case SpoilerKind.Area:
                        Add(kind, entry.Name, quest, entry.Target == UnlockTarget.WorldMap ? new Shape(Word.Region, entry.Expansion, 0, 0) : AreaShape(entry.Name, entry.Expansion));
                        break;
                    case SpoilerKind.Aetheryte:
                        // An aetheryte follows its area (the caption names it); one with no area is placed on its own.
                        Add(kind, entry.Name, quest, new Shape(Word.Aetheryte, entry.Expansion, areas.TryGetValue(entry.Detail, out var area) ? area.Number : (ushort)0, 0));
                        if (entry.Name.Length > 0 && entry.Detail.Length > 0)
                        {
                            Of(kind, entry.Name, default).Zone ??= entry.Detail;
                            Add(SpoilerKind.Area, entry.Detail, quest, AreaShape(entry.Detail, entry.Expansion));
                        }

                        break;
                    case SpoilerKind.Duty:
                        Add(kind, entry.Name, quest, new Shape(DutyWord(entry.Target), entry.Expansion, 0, (byte)Math.Min(entry.SortKey, byte.MaxValue)));
                        break;
                    default:
                        Add(kind, entry.Name, quest, new Shape(RewardWord(entry.Target), byte.MaxValue, 0, 0));
                        break;
                }

                if (entry.Target == UnlockTarget.Zone)
                {
                    opened.Add(entry.TargetId);
                    if (zoneById.TryGetValue(entry.TargetId, out var zone))
                    {
                        Add(SpoilerKind.Area, zone.Region, quest, new Shape(Word.Region, zone.Expansion, 0, 0));
                    }
                }
            }

            foreach (var entry in unlocks.ExtraRewards(quest.RowId))
            {
                Add(SpoilerKind.Reward, entry.Name, quest, new Shape(RewardWord(entry.Target), byte.MaxValue, 0, 0));
            }

            foreach (var reward in quest.Rewards)
            {
                Add(SpoilerKind.Reward, reward.Name, quest, new Shape(RewardWord(reward.Kind), byte.MaxValue, 0, 0));
            }

            if (quest.Issuer is { } issuer)
            {
                Add(SpoilerKind.Npc, issuer.Name, quest, new Shape(Word.Person, byte.MaxValue, 0, 0));
            }
        }

        // A zone no quest opens (a small town, an instanced area) is introduced by the quests given in it.
        foreach (var quest in catalog.All)
        {
            if (quest.Issuer is { } issuer && !opened.Contains(issuer.TerritoryId) && zoneById.TryGetValue(issuer.TerritoryId, out var zone))
            {
                Add(SpoilerKind.Area, zone.Name, quest, AreaShape(zone.Name, zone.Expansion));
            }
        }

        var shapes = new List<Shape>();
        var shapeIndex = new Dictionary<Shape, int>();
        var byKind = new FrozenDictionary<string, Placed>[builders.Length];
        for (var i = 0; i < builders.Length; i++)
        {
            var placed = new Dictionary<string, Placed>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, introduced) in builders[i])
            {
                introduced.Indexes.Sort();
                var rows = new uint[introduced.Unanchored ? 0 : introduced.Indexes.Count];
                for (var j = 0; j < rows.Length; j++)
                {
                    rows[j] = graph.Story[introduced.Indexes[j]].RowId;
                }

                // A reward or person belongs to the expansion of the story quest that first introduces it.
                var shape = introduced.Shape;
                if (shape.Expansion == byte.MaxValue && introduced.Indexes.Count > 0)
                {
                    shape = shape with { Expansion = graph.Story[introduced.Indexes[0]].Expansion };
                }

                if (!shapeIndex.TryGetValue(shape, out var index))
                {
                    index = shapes.Count;
                    shapes.Add(shape);
                    shapeIndex[shape] = index;
                }

                var expansion = (SpoilerKind)i is SpoilerKind.Area or SpoilerKind.Aetheryte or SpoilerKind.Duty ? introduced.Shape.Expansion : shape.Expansion;
                placed[name] = new Placed(rows, introduced.Unanchored, expansion, introduced.Zone, index);
            }

            byKind[i] = placed.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        var storyExpansion = new Dictionary<uint, byte>(graph.Story.Count);
        foreach (var quest in graph.Story)
        {
            storyExpansion.TryAdd(quest.RowId, quest.Expansion);
        }

        return new SpoilerNames(byKind, anchorRows.ToFrozenDictionary(), storyExpansion.ToFrozenDictionary(), [.. shapes], expansionName);
    }

    /// <summary>
    /// Each town and field zone's number within its expansion: the zones of one expansion in the sheet's own order
    /// (<see cref="UnlockZone.SortKey"/>, then the row id), one number per name (a zone with several rows keeps its first).
    /// </summary>
    internal static Dictionary<string, (byte Expansion, ushort Number)> AreaNumbers(IEnumerable<UnlockZone> zones)
    {
        var numbers = new Dictionary<string, (byte Expansion, ushort Number)>(StringComparer.OrdinalIgnoreCase);
        var next = new Dictionary<byte, ushort>();
        foreach (var zone in zones.OrderBy(static z => z.Expansion).ThenBy(static z => z.SortKey).ThenBy(static z => z.TerritoryId))
        {
            if (zone.Name.Length == 0 || numbers.ContainsKey(zone.Name))
            {
                continue;
            }

            var number = (ushort)(next.GetValueOrDefault(zone.Expansion) + 1);
            next[zone.Expansion] = number;
            numbers[zone.Name] = (zone.Expansion, number);
        }

        return numbers;
    }

    private static Word DutyWord(UnlockTarget target) => target switch
    {
        UnlockTarget.Dungeon => Word.Dungeon,
        UnlockTarget.Trial => Word.Trial,
        UnlockTarget.NormalRaid => Word.Raid,
        UnlockTarget.AllianceRaid => Word.AllianceRaid,
        UnlockTarget.FieldOperation => Word.FieldOperation,
        _ => Word.Duty,
    };

    private static Word RewardWord(UnlockTarget target) => target switch
    {
        UnlockTarget.Job => Word.Job,
        UnlockTarget.Flying => Word.AetherCurrent,
        UnlockTarget.System => Word.Feature,
        UnlockTarget.Action or UnlockTarget.GeneralAction => Word.Action,
        UnlockTarget.Trait => Word.Trait,
        UnlockTarget.Emote => Word.Emote,
        UnlockTarget.BlueMageSpell => Word.BlueMagic,
        UnlockTarget.Mount => Word.Mount,
        UnlockTarget.Minion => Word.Minion,
        UnlockTarget.Orchestrion => Word.Orchestrion,
        UnlockTarget.Card => Word.Card,
        UnlockTarget.Hairstyle => Word.Hairstyle,
        UnlockTarget.Barding => Word.Barding,
        UnlockTarget.Ornament => Word.Ornament,
        UnlockTarget.Title => Word.Title,
        _ => Word.Reward,
    };

    private static Word RewardWord(RewardKind kind) => kind switch
    {
        RewardKind.Item or RewardKind.OptionalItem or RewardKind.ArtifactGear => Word.Item,
        RewardKind.Emote => Word.Emote,
        RewardKind.Action or RewardKind.GeneralAction => Word.Action,
        RewardKind.Trait => Word.Trait,
        RewardKind.BlueMageSpell => Word.BlueMagic,
        RewardKind.Instance or RewardKind.DutyUnlock => Word.DutyReward,
        RewardKind.ClassJob => Word.Job,
        RewardKind.Mount => Word.Mount,
        RewardKind.Minion => Word.Minion,
        RewardKind.Orchestrion => Word.Orchestrion,
        RewardKind.TripleTriadCard => Word.Card,
        RewardKind.Ornament => Word.Ornament,
        RewardKind.Barding => Word.Barding,
        RewardKind.Hairstyle => Word.Hairstyle,
        RewardKind.AetherCurrent => Word.AetherCurrent,
        RewardKind.Achievement => Word.Achievement,
        RewardKind.Title => Word.Title,
        RewardKind.SystemUnlock => Word.Feature,
        _ => Word.Reward,
    };

    private string ExpansionName(byte expansion) =>
        expansionName?.Invoke(expansion) is { Length: > 0 } named ? named : Expansions.Name(expansion);

    /// <summary>The placeholder of one shape, in the UI language.</summary>
    private string Format(Shape shape)
    {
        var culture = CultureInfo.CurrentCulture;
        return shape.Word switch
        {
            Word.Area => shape.Number > 0
                ? string.Format(culture, CoreText.T("Core.Spoiler.Area", "{0} area {1}"), ExpansionName(shape.Expansion), shape.Number)
                : string.Format(culture, CoreText.T("Core.Spoiler.AreaUnnumbered", "{0} area"), ExpansionName(shape.Expansion)),
            Word.Region => string.Format(culture, CoreText.T("Core.Spoiler.Region", "{0} region"), ExpansionName(shape.Expansion)),
            Word.Aetheryte => shape.Number > 0
                ? string.Format(culture, CoreText.T("Core.Spoiler.Aetheryte", "{0} aetheryte · area {1}"), ExpansionName(shape.Expansion), shape.Number)
                : string.Format(culture, CoreText.T("Core.Spoiler.AetheryteUnnumbered", "{0} aetheryte"), ExpansionName(shape.Expansion)),
            Word.Person => string.Format(culture, CoreText.T("Core.Spoiler.Person", "{0} character"), ExpansionName(shape.Expansion)),
            Word.Dungeon or Word.Trial or Word.Raid or Word.AllianceRaid or Word.FieldOperation or Word.Duty => FormatDuty(shape.Word, shape.Level),
            Word.Item => CoreText.T("Core.Spoiler.Item", "An item"),
            Word.Mount => CoreText.T("Core.Spoiler.Mount", "A mount"),
            Word.Minion => CoreText.T("Core.Spoiler.Minion", "A minion"),
            Word.Emote => CoreText.T("Core.Spoiler.Emote", "An emote"),
            Word.Orchestrion => CoreText.T("Core.Spoiler.Orchestrion", "An orchestrion roll"),
            Word.Title => CoreText.T("Core.Spoiler.Title", "A title"),
            Word.Hairstyle => CoreText.T("Core.Spoiler.Hairstyle", "A hairstyle"),
            Word.Card => CoreText.T("Core.Spoiler.Card", "A Triple Triad card"),
            Word.Barding => CoreText.T("Core.Spoiler.Barding", "A barding"),
            Word.Ornament => CoreText.T("Core.Spoiler.Ornament", "A fashion accessory"),
            Word.Action => CoreText.T("Core.Spoiler.Action", "An action"),
            Word.Trait => CoreText.T("Core.Spoiler.Trait", "A trait"),
            Word.BlueMagic => CoreText.T("Core.Spoiler.BlueMagic", "A blue magic spell"),
            Word.Job => CoreText.T("Core.Spoiler.Job", "A job"),
            Word.Feature => CoreText.T("Core.Spoiler.Feature", "A feature"),
            Word.AetherCurrent => CoreText.T("Core.Spoiler.AetherCurrent", "An aether current"),
            Word.Achievement => CoreText.T("Core.Spoiler.Achievement", "An achievement"),
            Word.DutyReward => CoreText.T("Core.Spoiler.DutyReward", "A duty"),
            _ => CoreText.T("Core.Spoiler.Reward", "A reward"),
        };
    }

    /// <summary>"Dungeon (Lv 97)": a duty's placeholder from its kind word and level (0 for none), in the UI language.</summary>
    private static string FormatDuty(Word word, byte level) => level > 0
        ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Spoiler.Duty", "{0} (Lv {1})"), DutyName(word), level)
        : DutyName(word);

    /// <summary>
    /// "Dungeon (Lv 90)": the placeholder of a duty from its kind (<paramref name="target"/>, a duty target) and level
    /// alone, for a duty the names do not place; registered as a placeholder (<see cref="SpoilerMask.IsPlaceholder"/>).
    /// Formatted once per kind, level and language; allocates nothing after that.
    /// </summary>
    public static string DutyPlaceholder(UnlockTarget target, byte level) =>
        DutyTexts.Value.GetOrAdd((DutyWord(target), level), static key => SpoilerMask.Register(FormatDuty(key.Word, key.Level)));

    private static readonly TextCache<ConcurrentDictionary<(Word Word, byte Level), string>> DutyTexts = new(static () => new());

    /// <summary>
    /// "A duty", "A mount": the placeholder of a reward from its kind alone, for a reward the names do not place (a
    /// curated unlock); registered as a placeholder. Formatted once per kind and language; allocates nothing after that.
    /// </summary>
    public static string RewardPlaceholder(RewardKind kind) =>
        RewardTexts.Value.GetOrAdd(RewardWord(kind), static word => SpoilerMask.Register(Empty.Format(new Shape(word, byte.MaxValue, 0, 0))));

    private static readonly TextCache<ConcurrentDictionary<Word, string>> RewardTexts = new(static () => new());

    /// <summary>"area 6": an area's locator alone; null for a shape that has none (its placeholder is the short form).</summary>
    private static string? FormatLocator(Shape shape) => shape.Word is Word.Area or Word.Aetheryte && shape.Number > 0
        ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Spoiler.AreaLocator", "area {0}"), shape.Number)
        : null;

    private static string DutyName(Word word) => word switch
    {
        Word.Dungeon => UnlockTargets.Name(UnlockTarget.Dungeon),
        Word.Trial => UnlockTargets.Name(UnlockTarget.Trial),
        Word.Raid => UnlockTargets.Name(UnlockTarget.NormalRaid),
        Word.AllianceRaid => UnlockTargets.Name(UnlockTarget.AllianceRaid),
        Word.FieldOperation => UnlockTargets.Name(UnlockTarget.FieldOperation),
        _ => UnlockTargets.Name(UnlockTarget.OtherDuty),
    };

    /// <summary>The kind word of a placeholder.</summary>
    private enum Word : byte
    {
        Area,
        Region,
        Aetheryte,
        Person,
        Dungeon,
        Trial,
        Raid,
        AllianceRaid,
        FieldOperation,
        Duty,
        Item,
        Mount,
        Minion,
        Emote,
        Orchestrion,
        Title,
        Hairstyle,
        Card,
        Barding,
        Ornament,
        Action,
        Trait,
        BlueMagic,
        Job,
        Feature,
        AetherCurrent,
        Achievement,
        DutyReward,
        Reward,
    }

    /// <summary>What a placeholder says: its kind word, the expansion, the area number and the duty level (0 for none).</summary>
    private readonly record struct Shape(Word Word, byte Expansion, ushort Number, byte Level);

    /// <summary>The placeholders' text in one language, formatted on first use; written racily at worst with equal values.</summary>
    private sealed class Texts(int version, int count)
    {
        private readonly string?[] display = new string?[count];
        private readonly string?[] locator = new string?[count];
        private readonly string?[] search = new string?[count];

        public int Version { get; } = version;

        public string Display(int shape, SpoilerNames names)
        {
            if ((uint)shape >= (uint)display.Length)
            {
                return string.Empty;
            }

            return display[shape] ??= SpoilerMask.Register(names.Format(names.shapes[shape]));
        }

        public string Locator(int shape, SpoilerNames names)
        {
            if ((uint)shape >= (uint)locator.Length)
            {
                return string.Empty;
            }

            return locator[shape] ??= FormatLocator(names.shapes[shape]) is { } shortForm ? SpoilerMask.Register(shortForm) : Display(shape, names);
        }

        public string Search(int shape, SpoilerNames names)
        {
            if ((uint)shape >= (uint)search.Length)
            {
                return string.Empty;
            }

            return search[shape] ??= SpoilerMask.SearchForm(Display(shape, names));
        }
    }

    /// <summary>The story anchors of one name's introducing quests, as story indexes, and the shape it prints.</summary>
    private sealed class Introduced(Shape shape)
    {
        public List<int> Indexes { get; } = new(1);

        public bool Unanchored { get; private set; }

        public Shape Shape { get; } = shape;

        public string? Zone { get; set; }

        public void Add(int index)
        {
            if (index < 0)
            {
                Unanchored = true;
            }
            else if (!Indexes.Contains(index))
            {
                Indexes.Add(index);
            }
        }
    }

    private sealed class KindNameComparer : IEqualityComparer<(SpoilerKind Kind, string Name)>
    {
        public bool Equals((SpoilerKind Kind, string Name) x, (SpoilerKind Kind, string Name) y) =>
            x.Kind == y.Kind && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((SpoilerKind Kind, string Name) obj) =>
            HashCode.Combine(obj.Kind, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name ?? string.Empty));
    }

    /// <summary>Each quest's story anchor as an index into <see cref="MsqGraph.Story"/>, memoized; -1 for none.</summary>
    private sealed class Anchors(QuestCatalog catalog, Dictionary<uint, int> storyIndex, Dictionary<uint, int> zoneAnchor)
    {
        private const int Visiting = int.MinValue;
        private readonly Dictionary<uint, int> memo = [];

        public int Of(QuestRecord quest)
        {
            if (storyIndex.TryGetValue(quest.RowId, out var own))
            {
                return own;
            }

            if (memo.TryGetValue(quest.RowId, out var known))
            {
                // A cycle in the prerequisites places nothing more.
                return known == Visiting ? -1 : known;
            }

            memo[quest.RowId] = Visiting;
            var at = quest.Issuer is { } issuer && zoneAnchor.TryGetValue(issuer.TerritoryId, out var zone) ? zone : -1;
            var prerequisites = catalog.PrerequisitesOf(quest);
            if (prerequisites.Join == JoinKind.Any)
            {
                // Any one alternative will do: the earliest; the quests needed beside it count in full.
                var earliest = int.MaxValue;
                foreach (var id in prerequisites.QuestIds)
                {
                    var of = OfRow(id, quest.RowId);
                    if (prerequisites.IsRequired(id))
                    {
                        at = Math.Max(at, of);
                    }
                    else
                    {
                        earliest = Math.Min(earliest, of);
                    }
                }

                if (earliest != int.MaxValue)
                {
                    at = Math.Max(at, earliest);
                }
            }
            else
            {
                foreach (var id in prerequisites.QuestIds)
                {
                    at = Math.Max(at, OfRow(id, quest.RowId));
                }
            }

            memo[quest.RowId] = at;
            return at;
        }

        private int OfRow(uint rowId, uint self) =>
            rowId != self && catalog.GetByRowId(rowId) is { } quest ? Of(quest) : -1;
    }
}
