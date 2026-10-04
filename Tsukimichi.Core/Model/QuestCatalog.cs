using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Tsukimichi.Core.Model;

/// <summary>
/// Immutable index over every <see cref="QuestRecord"/>. Built once per load; all lists preserve journal sort order.
/// <para>
/// Two id spaces meet here. The Quest sheet <b>row id</b> (65536 + n) is what <see cref="QuestRecord.RowId"/>,
/// previous quests, quest locks, pins, overrides and unique-reward entries carry. The runtime <b>quest id</b> is its
/// low 16 bits (<see cref="QuestRecord.QuestId"/>), what the completion bitmask, the journal and daily flags use.
/// Every lookup names the space it takes; the untyped <c>Get</c> overloads are kept only for older callers.
/// </para>
/// </summary>
public sealed class QuestCatalog
{
    public static readonly QuestCatalog Empty = new([], null, null);

    private QuestCatalog(IReadOnlyList<QuestRecord> all, IReadOnlyDictionary<uint, uint[]>? extras, IReadOnlyDictionary<uint, QuestGate>? gates)
    {
        All = all;
        ByRowId = all.ToFrozenDictionary(q => q.RowId);

        var byQuestId = new Dictionary<ushort, QuestRecord>(all.Count);
        foreach (var quest in all)
        {
            byQuestId.TryAdd(quest.QuestId, quest);
        }

        ByQuestId = byQuestId.ToFrozenDictionary();

        BySection = Group(all, q => q.Journal.SectionId);
        ByCategory = Group(all, q => q.Journal.CategoryId);
        ByGenre = Group(all, q => q.Journal.GenreId);

        var removed = new List<QuestRecord>();
        foreach (var quest in all)
        {
            if (quest.IsRemoved)
            {
                removed.Add(quest);
            }
        }

        Removed = removed.ToArray();
        PhasedFestivals = FindPhasedFestivals(all);

        // A game gate's "after" quests are prerequisites like the curated extras: the gate cannot be passed before them.
        var merged = new Dictionary<uint, List<uint>>();
        foreach (var (rowId, ids) in extras ?? FrozenDictionary<uint, uint[]>.Empty)
        {
            merged[rowId] = [.. ids];
        }

        var knownGates = new Dictionary<uint, QuestGate>();
        foreach (var (rowId, gate) in gates ?? FrozenDictionary<uint, QuestGate>.Empty)
        {
            if (!ByRowId.ContainsKey(rowId))
            {
                continue;
            }

            knownGates[rowId] = gate;
            if (!merged.TryGetValue(rowId, out var list))
            {
                merged[rowId] = list = [];
            }

            list.AddRange(gate.After);
        }

        gameGates = knownGates.ToFrozenDictionary();
        GateItemWatch = knownGates.Values
            .Where(g => g.Items is not null)
            .SelectMany(g => g.Items!.Groups.SelectMany(group => group))
            .Distinct()
            .Order()
            .ToArray();
        GateItemFingerprint = GateItemCapture.Fingerprint(GateItemWatch);
        MountWatch = knownGates.Values
            .Where(g => g.Mounts is not null)
            .SelectMany(g => g.Mounts!)
            .Concat(all.Where(q => q.MountRequired != 0).Select(q => q.MountRequired))
            .Distinct()
            .Order()
            .ToArray();
        GateUnlockLinkWatch = knownGates.Values
            .Where(g => g.UnlockLinks is not null)
            .SelectMany(g => g.UnlockLinks!)
            .Distinct()
            .Order()
            .ToArray();

        // Curated ids that name no quest of this catalog (a test catalog, a row the game dropped) are left out here.
        var known = new Dictionary<uint, uint[]>();
        foreach (var (rowId, ids) in merged)
        {
            var kept = ids.Where(id => id != rowId && ByRowId.ContainsKey(id)).Distinct().ToArray();
            if (kept.Length > 0 && ByRowId.ContainsKey(rowId))
            {
                known[rowId] = kept;
            }
        }

        extraPrerequisites = known.ToFrozenDictionary();

        var extended = new Dictionary<uint, Prereq>();
        foreach (var quest in all)
        {
            if ((quest.AcceptConditions.Length > 0 || extraPrerequisites.ContainsKey(quest.RowId)) && Extend(quest) is { } prereq)
            {
                extended[quest.RowId] = prereq;
            }
        }

        extendedPrerequisites = extended.ToFrozenDictionary();
    }

    /// <summary><see cref="PrerequisitesOf"/> for the held records whose accept conditions or curated extras add a quest.</summary>
    private readonly FrozenDictionary<uint, Prereq> extendedPrerequisites;

    /// <summary>The curated extra prerequisites by quest row id, every id a quest of this catalog (<see cref="ExtraPrerequisitesOf"/>).</summary>
    private readonly FrozenDictionary<uint, uint[]> extraPrerequisites;

    /// <summary>The curated game gates by quest row id, every key a quest of this catalog (<see cref="GameGateOf"/>).</summary>
    private readonly FrozenDictionary<uint, QuestGate> gameGates;

    /// <summary>Builds a catalog. Records are ordered by <see cref="JournalRef.SortKey"/> then row id; duplicate row ids throw.</summary>
    public static QuestCatalog Build(IEnumerable<QuestRecord> quests) => Build(quests, null, null);

    /// <summary>
    /// Builds a catalog with curated extra prerequisites (<c>curated/extra_prerequisites.json</c>): quest row id to the
    /// quest row ids the game wants completed first that neither the sheet's previous quests nor its accept conditions
    /// record. <see cref="PrerequisitesOf"/> adds them; an id that is no quest of this catalog is ignored.
    /// </summary>
    public static QuestCatalog Build(IEnumerable<QuestRecord> quests, IReadOnlyDictionary<uint, uint[]>? extraPrerequisites) =>
        Build(quests, extraPrerequisites, null);

    /// <summary>
    /// Builds a catalog with curated extra prerequisites and curated game gates (<c>curated/game_gates.json</c>): quest
    /// row id to a gate the game checks that Tsukimichi cannot read (<see cref="GameGateOf"/>) and the quests before
    /// which it cannot be passed, which <see cref="PrerequisitesOf"/> adds as it adds the extras. A gate on a row that is
    /// no quest of this catalog is ignored.
    /// </summary>
    public static QuestCatalog Build(IEnumerable<QuestRecord> quests, IReadOnlyDictionary<uint, uint[]>? extraPrerequisites, IReadOnlyDictionary<uint, QuestGate>? gameGates)
    {
        ArgumentNullException.ThrowIfNull(quests);
        var ordered = quests
            .OrderBy(q => q.Journal.SortKey)
            .ThenBy(q => q.RowId)
            .ToArray();
        return new QuestCatalog(ordered, extraPrerequisites, gameGates);
    }

    public int Count => All.Count;

    /// <summary>Every quest in journal order.</summary>
    public IReadOnlyList<QuestRecord> All { get; }

    /// <summary>Keyed by Quest sheet row id.</summary>
    public IReadOnlyDictionary<uint, QuestRecord> ByRowId { get; }

    /// <summary>Keyed by runtime quest id (low 16 bits of the row id). First record wins on a collision.</summary>
    public IReadOnlyDictionary<ushort, QuestRecord> ByQuestId { get; }

    /// <summary>
    /// Keyed by JournalSection id. Removed quests (<see cref="QuestRecord.IsRemoved"/>) sit under whatever section id
    /// the sheet gave them (255 for genre 0, their own for a retired listed row); the tree and query layers never show
    /// them there.
    /// </summary>
    public IReadOnlyDictionary<uint, IReadOnlyList<QuestRecord>> BySection { get; }

    /// <summary>Keyed by JournalCategory id; see <see cref="BySection"/> for how removed quests are treated.</summary>
    public IReadOnlyDictionary<uint, IReadOnlyList<QuestRecord>> ByCategory { get; }

    /// <summary>Keyed by JournalGenre id; key 0 holds every unlisted quest. A retired listed row stays under its genre.</summary>
    public IReadOnlyDictionary<uint, IReadOnlyList<QuestRecord>> ByGenre { get; }

    /// <summary>Every quest of the "Removed from the game" bucket (<see cref="QuestRecord.IsRemoved"/>), in journal order.</summary>
    public IReadOnlyList<QuestRecord> Removed { get; }

    /// <summary>
    /// Festivals whose quests carry at least two distinct (FestivalBegin, FestivalEnd) windows: the events that open
    /// later chapters on later phases (Hatching-tide 2014 and a handful of others). Only these have their phase window
    /// judged; every other festival's quests share one window, which cannot separate chapters, so they keep the
    /// id-only seasonal check until the meaning of the reported phase is verified in game.
    /// </summary>
    public IReadOnlySet<ushort> PhasedFestivals { get; }

    /// <summary>Lookup by Quest sheet row id (65536 + n): the id prerequisites, locks, pins and unique-reward entries carry.</summary>
    public QuestRecord? GetByRowId(uint rowId) => ByRowId.GetValueOrDefault(rowId);

    /// <summary>Lookup by runtime quest id (low 16 bits of the row id): the id the completion bitmask, journal and daily flags use.</summary>
    public QuestRecord? GetByQuestId(ushort questId) => ByQuestId.GetValueOrDefault(questId);

    /// <summary>Lookup by Quest sheet row id; see <see cref="GetByRowId"/>.</summary>
    public bool TryGetByRowId(uint rowId, [NotNullWhen(true)] out QuestRecord? quest) => ByRowId.TryGetValue(rowId, out quest);

    /// <summary>Lookup by runtime quest id; see <see cref="GetByQuestId"/>.</summary>
    public bool TryGetByQuestId(ushort questId, [NotNullWhen(true)] out QuestRecord? quest) => ByQuestId.TryGetValue(questId, out quest);

    /// <summary>
    /// The quests the game wants completed before it offers <paramref name="quest"/>: its
    /// <see cref="QuestRecord.PreviousQuests"/>, then every <see cref="QuestRecord.AcceptConditions"/> value that is a
    /// quest of this catalog and not already listed, then the curated extras (<see cref="ExtraPrerequisitesOf"/>) not
    /// already listed, under the previous quests' join. The sheet has three previous-quest slots and
    /// QuestAcceptAdditionCondition carries the rest: with 7.3 data 47 of its 57 rows hold quest ids only (The Killing
    /// Art also needs Over the Wall; the role, Studium and allied society finales list their fourth and fifth lines and
    /// the expansion's last main scenario quest there, and the wiki names all of them as required), so the values are
    /// an "all" list like the slots they extend. The curated extras are the gates neither records, most of them a main
    /// scenario milestone the quest's own text names; they are always an "all" list. The one Any-join quest, Royal
    /// Rumblings, repeats its three envoy quests, one per city and only one ever done: read under the quest's own
    /// join it stays "any of the three", where an "all" reading would block it for good. A condition or extra outside
    /// an Any join's previous quests is no further alternative: it is listed in <see cref="Prereq.Required"/>, needed
    /// beside one of them (no such quest with 7.3 data). A value that is no quest (rows such as 17, 226 or 509 of other
    /// sheets) is no prerequisite; see <see cref="UncheckedAcceptConditions"/>.
    /// The record's own <see cref="QuestRecord.PreviousQuests"/> when neither adds a quest.
    /// </summary>
    public Prereq PrerequisitesOf(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (quest.AcceptConditions.Length == 0 && !extraPrerequisites.ContainsKey(quest.RowId))
        {
            return quest.PreviousQuests;
        }

        // The cached answer belongs to the record the catalog holds; an edited copy is worked out afresh.
        if (ByRowId.TryGetValue(quest.RowId, out var held) && ReferenceEquals(held, quest))
        {
            return extendedPrerequisites.GetValueOrDefault(quest.RowId) ?? quest.PreviousQuests;
        }

        return Extend(quest) ?? quest.PreviousQuests;
    }

    /// <summary>
    /// The curated extra prerequisites of <paramref name="rowId"/> (<c>curated/extra_prerequisites.json</c>, and the
    /// "after" quests of its game gate, <see cref="GameGateOf"/>) that are quests of this catalog; empty for most
    /// quests. <see cref="PrerequisitesOf"/> already includes them.
    /// </summary>
    public uint[] ExtraPrerequisitesOf(uint rowId) => extraPrerequisites.TryGetValue(rowId, out var ids) ? ids : [];

    /// <summary>
    /// The gate the game checks before it offers <paramref name="rowId"/> that no quest of the sheets records
    /// (<c>curated/game_gates.json</c>); null for nearly every quest. The evaluator judges it from the capture where it
    /// can (<c>Evaluation.GameGateCheck</c>) and lists it as not checked otherwise, so the quest reads Not checked where
    /// it would otherwise read Ready.
    /// </summary>
    public QuestGate? GameGateOf(uint rowId) => gameGates.GetValueOrDefault(rowId);

    /// <summary>
    /// Every weapon a gear gate of this catalog names (<see cref="QuestGate.Items"/>), ascending and distinct: what a
    /// capture looks for in the character's gear (<see cref="CharacterSnapshot.GateItems"/>). Empty when no gate names
    /// one.
    /// </summary>
    public uint[] GateItemWatch { get; }

    /// <summary>
    /// <see cref="GateItemCapture.Fingerprint"/> of <see cref="GateItemWatch"/>: a capture made against another list
    /// (an older build's data) is not judged, since it never looked for the weapons the list has gained.
    /// </summary>
    public uint GateItemFingerprint { get; }

    /// <summary>
    /// Every mount a quest of this catalog needs owned, ascending and distinct: the sheet's
    /// <see cref="QuestRecord.MountRequired"/> and the mounts of the mount-collection gates (<see cref="QuestGate.Mounts"/>).
    /// A capture reads whether the character owns each, with the collectibles (<see cref="CharacterSnapshot.Collectibles"/>).
    /// </summary>
    public uint[] MountWatch { get; }

    /// <summary>
    /// Every unlock link a gate of this catalog needs set (<see cref="QuestGate.UnlockLinks"/>), ascending and distinct:
    /// what a capture reads (<see cref="CharacterSnapshot.GateUnlockLinks"/>). Empty when no gate names one.
    /// </summary>
    public uint[] GateUnlockLinkWatch { get; }

    /// <summary>
    /// The accept conditions <see cref="PrerequisitesOf"/> cannot use, values that are no quest of this catalog and that
    /// the quest's game gate does not stand for (<see cref="QuestGate.UnlockLinks"/>, <see cref="QuestGate.AcceptConditions"/>):
    /// the evaluator lists them as not checked. Empty for most quests.
    /// </summary>
    public uint[] UncheckedAcceptConditions(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (quest.AcceptConditions.Length == 0)
        {
            return quest.AcceptConditions;
        }

        var gate = GameGateOf(quest.RowId);
        return quest.AcceptConditions
            .Where(id => !ByRowId.ContainsKey(id) && !(gate is not null && (gate.AcceptConditions.Contains(id) || (gate.UnlockLinks?.Contains(id) ?? false))))
            .ToArray();
    }

    /// <summary>Lookup by Quest sheet row id. Kept for older callers; the name does not say which id it takes.</summary>
    [Obsolete("Use GetByRowId: this overload takes a Quest sheet row id (65536 + n), not a runtime quest id.")]
    public QuestRecord? Get(uint rowId) => GetByRowId(rowId);

    /// <summary>Lookup by runtime quest id. Kept for older callers; the name does not say which id it takes.</summary>
    [Obsolete("Use GetByQuestId: this overload takes a runtime quest id (low 16 bits), not a row id.")]
    public QuestRecord? Get(ushort questId) => GetByQuestId(questId);

    /// <summary>Lookup by runtime quest id. Kept for older callers; see <see cref="TryGetByQuestId"/>.</summary>
    [Obsolete("Use TryGetByQuestId: this overload takes a runtime quest id (low 16 bits), not a row id.")]
    public bool TryGet(ushort questId, out QuestRecord quest)
    {
        if (TryGetByQuestId(questId, out var found))
        {
            quest = found;
            return true;
        }

        quest = null!;
        return false;
    }

    /// <summary>
    /// The previous quests extended by the accept conditions that name a catalog quest and by the curated extras; null
    /// when none adds one.
    /// </summary>
    private Prereq? Extend(QuestRecord quest)
    {
        var previous = quest.PreviousQuests.QuestIds;
        List<uint>? extra = null;
        foreach (var id in quest.AcceptConditions.Concat(ExtraPrerequisitesOf(quest.RowId)))
        {
            if (ByRowId.ContainsKey(id) && Array.IndexOf(previous, id) < 0 && (extra is null || !extra.Contains(id)))
            {
                (extra ??= []).Add(id);
            }
        }

        if (extra is null)
        {
            return null;
        }

        // Accept conditions and curated extras are "all" lists: under an Any join they are needed beside one of the
        // previous quests.
        var join = quest.PreviousQuests.Join;
        return new Prereq([.. previous, .. extra], join) { Required = join == JoinKind.Any ? [.. extra] : [] };
    }

    private static FrozenSet<ushort> FindPhasedFestivals(IReadOnlyList<QuestRecord> all)
    {
        var windows = new Dictionary<ushort, HashSet<(byte Begin, byte End)>>();
        foreach (var quest in all)
        {
            if (quest.Festival == 0)
            {
                continue;
            }

            if (!windows.TryGetValue(quest.Festival, out var set))
            {
                windows[quest.Festival] = set = [];
            }

            set.Add((quest.FestivalBegin, quest.FestivalEnd));
        }

        return windows.Where(pair => pair.Value.Count >= 2).Select(pair => pair.Key).ToFrozenSet();
    }

    private static FrozenDictionary<uint, IReadOnlyList<QuestRecord>> Group(
        IReadOnlyList<QuestRecord> ordered,
        Func<QuestRecord, uint> key)
    {
        // Input is already in journal order, so each group keeps that order.
        var groups = new Dictionary<uint, List<QuestRecord>>();
        foreach (var quest in ordered)
        {
            var k = key(quest);
            if (!groups.TryGetValue(k, out var list))
            {
                list = [];
                groups[k] = list;
            }

            list.Add(quest);
        }

        return groups.ToFrozenDictionary(kv => kv.Key, kv => (IReadOnlyList<QuestRecord>)kv.Value.ToArray());
    }
}

/// <summary>
/// A gate the game checks before it offers a quest (<see cref="QuestCatalog.GameGateOf"/>). Without
/// <paramref name="Items"/> Tsukimichi cannot read it; with them it is judged from the character's captured gear
/// (<see cref="CharacterSnapshot.GateItems"/>) when the capture is there.
/// </summary>
/// <param name="Gate">What the game wants, in English, as a phrase after "needs" ("a relic weapon nexus equipped").</param>
/// <param name="After">Quests before which the gate cannot be passed (the step that makes the weapon possible);
/// <see cref="QuestCatalog.PrerequisitesOf"/> adds them. May be empty.</param>
/// <param name="Items">The weapons that pass the gate, when the gate is one of gear; null for any other gate.</param>
/// <param name="Mounts">
/// The mounts that must all be owned, when the gate is a mount collection (the seven Lanners before the Firebird);
/// judged from the owned mounts a capture reads (<see cref="QuestCatalog.MountWatch"/>). Null for any other gate.
/// </param>
public sealed record QuestGate(string Gate, uint[] After, GateItems? Items = null, uint[]? Mounts = null)
{
    /// <summary>
    /// The unlock links that must all be set, when the game keeps the gate as unlock links (Occult Record entries, a blue
    /// magic spell learned); judged from the links a capture reads (<see cref="QuestCatalog.GateUnlockLinkWatch"/>).
    /// Null for any other gate.
    /// </summary>
    public uint[]? UnlockLinks { get; init; }

    /// <summary>Quests the game gives only once the gate is passed: one of them completed meets it. Empty for most gates.</summary>
    public uint[] MetBy { get; init; } = [];

    /// <summary>The sheet's accept conditions that are no quest which this gate stands for (<see cref="QuestCatalog.UncheckedAcceptConditions"/> leaves them out).</summary>
    public uint[] AcceptConditions { get; init; } = [];

    /// <summary>
    /// Where the gate was confirmed, in a fixed order: <see cref="GameTextSource"/> (the quest's own text, an after
    /// quest's, or a required quest's states it), <see cref="SheetSource"/> (sheet rows give its weapons, mounts, unlock
    /// links or accept conditions), <see cref="WikiSource"/> (the Console Games Wiki page in its evidence),
    /// <see cref="LodestoneSource"/> (the quest's Lodestone page states a requirement), <see cref="QuestionableSource"/>
    /// (Questionable holds the quest back on the same check), <see cref="PlayerSource"/> (a gate stated by the wiki alone
    /// that is never judged: the player confirms it with "I've done this"). Every curated gate has two.
    /// </summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    public const string GameTextSource = "gameText";
    public const string SheetSource = "sheet";
    public const string WikiSource = "wiki";
    public const string LodestoneSource = "lodestone";
    public const string QuestionableSource = "questionable";
    public const string PlayerSource = "player";
}

/// <summary>Where a gate's weapons must be (<see cref="GateItems.Hold"/>).</summary>
public enum GateHold
{
    /// <summary>Worn: in the main hand (and, for a paladin, the off hand).</summary>
    Equipped,

    /// <summary>Equipped, in the Armoury Chest or in the inventory ("in your possession").</summary>
    Held,
}

/// <summary>
/// The weapons that pass a gear gate: any one group, every item of it (a paladin's sword and shield form one group,
/// every other job's weapon a group of its own). Groups and the items in each are sorted ascending.
/// </summary>
public sealed record GateItems(GateHold Hold, uint[][] Groups)
{
    /// <summary>The first group every item of which <paramref name="have"/> holds; null when none does.</summary>
    public uint[]? GroupIn(IReadOnlyList<uint> have)
    {
        ArgumentNullException.ThrowIfNull(have);
        foreach (var group in Groups)
        {
            var all = group.Length > 0;
            foreach (var id in group)
            {
                if (!Contains(have, id))
                {
                    all = false;
                    break;
                }
            }

            if (all)
            {
                return group;
            }
        }

        return null;
    }

    private static bool Contains(IReadOnlyList<uint> list, uint id)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == id)
            {
                return true;
            }
        }

        return false;
    }
}
