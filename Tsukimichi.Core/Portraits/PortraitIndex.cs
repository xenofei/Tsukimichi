using System.Collections.Frozen;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Portraits;

/// <summary>
/// A face the game data offers for an NPC name: one icon of one family, with the name the source gives it.
/// </summary>
/// <param name="Icon">The icon id (load it with its <c>_hr1</c> twin as any game icon).</param>
/// <param name="Name">The source's name for the NPC: an ENpcResident name, a card name, a script variable stem.</param>
/// <param name="Era">The expansion the face belongs to (ExVersion row); null when the source does not say, which then
/// reads as the expansion of the giver's first quest.</param>
/// <param name="NpcId">The ENpcResident the source points at directly (a Trust member, a delivery client); 0 when it only names.</param>
/// <param name="Curated">Named by the curated file (<see cref="PortraitCuration.Faces"/>): its era, when it gives one,
/// wins over a sheet face's for the same icon.</param>
public sealed record PortraitFace(uint Icon, PortraitSource Source, string Name, byte? Era = null, uint NpcId = 0, bool Curated = false);

/// <summary>A quest giver as the index sees it: the ENpcResident row, its name and its ENpcBase race and gender.</summary>
/// <param name="Name">The English name, as the sources spell it (names are matched, never shown).</param>
/// <param name="Race">ENpcBase race row (1 Hyur … 8 Viera); 0 for a giver that is not one of the playable races (moogles, beast tribes).</param>
/// <param name="Gender">ENpcBase gender: 0 male, 1 female.</param>
public sealed record PortraitGiver(uint NpcId, string Name, byte Race, byte Gender);

/// <summary>A quest's giver, expansion and allied society, which pick the portrait and the fallback.</summary>
/// <param name="Seasonal">A seasonal event's quest (<c>Quest.Festival</c> set): the sheet files those under A Realm
/// Reborn whatever the year, so they do not count toward the giver's first expansion.</param>
public readonly record struct PortraitQuest(uint QuestId, uint GiverId, byte Expansion, byte BeastTribe, bool Seasonal = false);

/// <summary>Everything read from the game for <see cref="PortraitIndex.Build"/>.</summary>
/// <param name="TribeIcons">BeastTribe row to its emblem icon (<c>BeastTribe.Icon</c>).</param>
public sealed record PortraitInputs(
    IReadOnlyList<PortraitFace> Faces,
    IReadOnlyList<PortraitGiver> Givers,
    IReadOnlyList<PortraitQuest> Quests,
    IReadOnlyDictionary<byte, uint> TribeIcons)
{
    public static readonly PortraitInputs Empty = new([], [], [], new Dictionary<byte, uint>());
}

/// <summary>One portrait a giver can wear: the icon, its family and its era, and the source name that matched.</summary>
public readonly record struct PortraitVariant(uint Icon, PortraitSource Source, byte Era, string MatchedName);

/// <summary>What the drawing shows when there is no portrait, or when the spoiler shield hides it.</summary>
public enum PortraitFallbackKind : byte
{
    /// <summary>A neutral moon disc: a giver that is not humanoid and has no name of its own, or an unknown giver.</summary>
    Moon = 0,

    /// <summary>The allied society's emblem (<see cref="PortraitFallback.SocietyIcon"/>): the quest is a society's.</summary>
    SocietyEmblem = 1,

    /// <summary>A race silhouette (<see cref="PortraitFallback.Race"/>, <see cref="PortraitFallback.Gender"/>): a generic humanoid giver.</summary>
    Silhouette = 2,

    /// <summary>A moon medallion with the giver's initials (<see cref="PortraitFallback.Initials"/>): a named giver without art.</summary>
    Initials = 3,
}

/// <summary>
/// The fallback for one giver and quest, chosen by <see cref="Kind"/>; the other inputs ride along so the drawing can
/// pick differently (a silhouette for a named giver under the spoiler shield, say).
/// </summary>
/// <param name="SocietyIcon">The allied society's emblem icon when the quest is a society's; 0 otherwise.</param>
/// <param name="Race">ENpcBase race (1 Hyur, 2 Elezen, 3 Lalafell, 4 Miqo'te, 5 Roegadyn, 6 Au Ra, 7 Hrothgar, 8 Viera); 0 when not humanoid or unknown.</param>
/// <param name="Gender">0 male, 1 female.</param>
/// <param name="Initials">One or two capitals for a named giver; empty for a generic one.</param>
public readonly record struct PortraitFallback(PortraitFallbackKind Kind, uint SocietyIcon, byte Race, byte Gender, string Initials)
{
    /// <summary>Whether the giver is one of the playable races (a silhouette can be drawn).</summary>
    public bool Humanoid => Race != 0;
}

/// <summary>
/// A giver's portrait for one quest: the icon to load, its family (for the "Portrait: …" source line), the face window
/// to draw, the era the face belongs to (the spoiler shield hides a face from a later expansion than the character has
/// reached), the keep mask a delivery portrait is drawn through, and the fallback. <see cref="HasArt"/> is false when
/// the giver has no portrait; the fallback is always set. A struct over objects held by the index, so asking for one
/// every frame allocates nothing.
/// <para>
/// Drawing it: load <see cref="Icon"/> (its hr texture), grade it by <see cref="Source"/>'s family (spec A3), and when
/// <see cref="Mask"/> is set multiply the texture's alpha by the mask in that same pass, after checking
/// <see cref="PortraitMask.Matches"/> on the texture file; draw <see cref="Crop"/> of the graded copy. Until the copy
/// lands, or when the mask does not match the art, or the quest is masked by the spoiler shield, draw the fallback.
/// </para>
/// </summary>
/// <param name="Mask">The keep mask for a delivery portrait (always set when <see cref="Source"/> is
/// <see cref="PortraitSource.Delivery"/>: the index never offers one without it); null for every other family.</param>
public readonly record struct PortraitRef(uint GiverId, uint Icon, PortraitSource Source, PortraitCrop Crop, byte Era, PortraitFallback Fallback, PortraitMask? Mask = null)
{
    /// <summary>Whether there is a portrait to draw (else draw <see cref="Fallback"/>).</summary>
    public bool HasArt => Source != PortraitSource.None && Icon != 0;
}

/// <summary>
/// Quest givers' portraits from the game's own art (feature plan v7 F1), with the curated overlay (F3) applied. Built
/// once from <see cref="PortraitInputs"/> (read by <c>GiverPortraitSources</c> in Tsukimichi.GameData) and immutable
/// after, so safe to read from any thread.
/// <para>
/// <b>Matching.</b> A giver matches a face when the source points at the giver's ENpcResident id, or when the names
/// agree in normal form (<see cref="PortraitNames.Normalize"/>) after the curated aliases. A one-word giver name also
/// matches a several-word source name whose first word it is ("Tataru" → the "Tataru Taru" card) when exactly one name
/// of that family starts so. Generic givers (lower-case names) never match. Curated blocks then drop wrong matches,
/// and a curated pin replaces them all.
/// </para>
/// <para>
/// <b>Picking.</b> Families are tried in <see cref="PortraitSources.Priority"/> order (Trust bust, Triple Triad card,
/// battle-talk face, delivery portrait, Trust strip). The first family with a face from the quest's expansion or
/// earlier wins, with its latest such face: the look the character had then. A face from a later expansion than the
/// quest is never shown, even when it is the giver's only face: the giver gets the fallback instead (G'raha Tia's
/// A Realm Reborn Crystal Tower quests must not wear his Shadowbringers Trust bust). A seasonal event's quest has no
/// expansion of its own (the sheet files it under A Realm Reborn whatever the year it runs): it reads as the newest,
/// the event's present day, so it wears the giver's latest face; the spoiler shield still hides a face from an
/// expansion the character has not reached (<c>PortraitPlate.FaceAllowed</c>).
/// </para>
/// <para>
/// <b>Eras.</b> A face's era is what its source says (a Trust member's duties, a quest battle's quest, a card's place
/// in the card list); a face whose source says nothing reads as the giver's first expansion, counted over the giver's
/// story quests (a seasonal event's quests are filed under A Realm Reborn whatever the year, so they count only for a
/// giver with no other quest). When a curated face and a sheet face share an icon, the curated era wins.
/// </para>
/// </summary>
public sealed class PortraitIndex
{
    private readonly FrozenDictionary<uint, Giver> givers;
    private readonly FrozenDictionary<uint, PortraitQuest> quests;
    private readonly FrozenDictionary<byte, uint> tribeIcons;
    private readonly IReadOnlyDictionary<uint, PortraitMask> masks;

    private readonly PortraitPack? pack;

    private PortraitIndex(FrozenDictionary<uint, Giver> givers, FrozenDictionary<uint, PortraitQuest> quests, FrozenDictionary<byte, uint> tribeIcons, PortraitCrops crops, IReadOnlyDictionary<uint, PortraitMask> masks, PortraitPack? pack = null)
    {
        this.givers = givers;
        this.quests = quests;
        this.tribeIcons = tribeIcons;
        this.masks = masks;
        this.pack = pack;
        Crops = crops;
        GiversWithArt = givers.Values.Count(g => g.Variants.Length > 0);
    }

    /// <summary>The portrait pack this index fills gaps from (<see cref="WithPack"/>); null for game art alone.</summary>
    public PortraitPack? Pack => pack;

    /// <summary>
    /// This index with the optional portrait pack (feature plan v7 F4) behind it, or without one for null: the game art
    /// is picked exactly as before, and a giver the pack has a photo of wears it wherever the game art has nothing for the
    /// quest (<see cref="PortraitSources.Rank"/>: the pack ranks after every game family), instead of a fallback. Shares
    /// everything with this index; nothing is rebuilt.
    /// </summary>
    public PortraitIndex WithPack(PortraitPack? portraitPack) =>
        ReferenceEquals(portraitPack, pack) ? this : new PortraitIndex(givers, quests, tribeIcons, Crops, masks, portraitPack);

    /// <summary>No game data: every giver gets a fallback.</summary>
    public static PortraitIndex Empty { get; } = Build(PortraitInputs.Empty, PortraitCuration.Empty);

    /// <summary>The crop table in use (the defaults with the curated replacements).</summary>
    public PortraitCrops Crops { get; }

    /// <summary>How many giver ids wear at least one portrait.</summary>
    public int GiversWithArt { get; }

    /// <summary>How many giver ids the index knows (with art or not).</summary>
    public int GiverCount => givers.Count;

    /// <summary>The era a seasonal event's quest picks a face for: the newest (see Picking above).</summary>
    public const byte SeasonalEra = byte.MaxValue;

    /// <summary>The portrait for a catalog quest: its giver, expansion (the newest for a seasonal event's quest) and allied society.</summary>
    public PortraitRef For(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return For(quest.Issuer?.NpcId ?? 0, quest.Festival != 0 ? SeasonalEra : quest.Expansion, quest.BeastTribe);
    }

    /// <summary>
    /// The portrait of <paramref name="giverId"/> (an ENpcResident row) for quest <paramref name="questId"/> (a Quest
    /// row): the quest's expansion (the newest for a seasonal event's quest) picks the era and its allied society the
    /// emblem. A quest the index does not know reads as the giver's first quest's expansion and no society.
    /// </summary>
    public PortraitRef For(uint giverId, uint questId)
    {
        if (quests.TryGetValue(questId, out var quest))
        {
            return For(giverId, quest.Seasonal ? SeasonalEra : quest.Expansion, quest.BeastTribe);
        }

        return For(giverId, givers.TryGetValue(giverId, out var giver) ? giver.FirstEra : (byte)0, 0);
    }

    /// <summary>The portrait of <paramref name="giverId"/> for a quest of <paramref name="era"/> (ExVersion row) and allied society <paramref name="beastTribe"/> (0 for none).</summary>
    public PortraitRef For(uint giverId, byte era, byte beastTribe)
    {
        givers.TryGetValue(giverId, out var giver);
        var fallback = FallbackFor(giver, beastTribe);
        var variant = giver is null || giver.Variants.Length == 0 ? default : Pick(giver.Variants, era);
        if (pack is not null && pack.Has(giverId) && PackWins(variant.Source))
        {
            // The pack's photo is this exact NPC as the quest shows them, so it belongs to the quest's own era (a
            // seasonal event's quest: the giver's first, as nothing newer is known about them).
            var packEra = era == SeasonalEra ? giver?.FirstEra ?? 0 : era;
            return new PortraitRef(giverId, giverId, PortraitSource.Pack, PortraitCrop.Full, packEra, fallback);
        }

        if (variant.Source == PortraitSource.None)
        {
            return new PortraitRef(giverId, 0, PortraitSource.None, PortraitCrop.Full, era, fallback);
        }

        return new PortraitRef(giverId, variant.Icon, variant.Source, Crops.For(variant.Source, variant.Icon), variant.Era, fallback, masks.GetValueOrDefault(variant.Icon));
    }

    /// <summary>
    /// Whether a pack photo is worn instead of the game art's pick <paramref name="gameSource"/>: when the game has none
    /// for the quest, or when its family ranks after the pack (none does today; <see cref="PortraitSources.Rank"/>).
    /// </summary>
    public static bool PackWins(PortraitSource gameSource) =>
        gameSource == PortraitSource.None || PortraitSources.Rank(PortraitSource.Pack) < PortraitSources.Rank(gameSource);

    /// <summary>Every portrait the giver can wear, best family first and oldest first within one; empty when none.</summary>
    public IReadOnlyList<PortraitVariant> Variants(uint giverId) =>
        givers.TryGetValue(giverId, out var giver) ? giver.Variants : [];

    /// <summary>The giver's name as matched (English); empty when unknown.</summary>
    public string NameOf(uint giverId) => givers.TryGetValue(giverId, out var giver) ? giver.Name : string.Empty;

    /// <summary>
    /// The face among <paramref name="variants"/> (sorted by family rank, then era, then icon) for a quest of
    /// <paramref name="era"/>: the first family with a face from that era or earlier, its latest such face. When every
    /// face is from a later expansion there is none (<c>default</c>, <see cref="PortraitSource.None"/>): a later face
    /// could show a look, or a person, the story has not reached, so the giver's fallback shows instead. Allocates nothing.
    /// </summary>
    public static PortraitVariant Pick(IReadOnlyList<PortraitVariant> variants, byte era)
    {
        ArgumentNullException.ThrowIfNull(variants);
        if (variants.Count == 0)
        {
            return default;
        }

        var i = 0;
        while (i < variants.Count)
        {
            var source = variants[i].Source;
            var end = i;
            PortraitVariant? best = null;
            while (end < variants.Count && variants[end].Source == source)
            {
                // Sorted by era, then icon: the last face not after the quest's era, the lowest icon of that era.
                if (variants[end].Era <= era && (best is null || variants[end].Era > best.Value.Era))
                {
                    best = variants[end];
                }

                end++;
            }

            if (best is { } found)
            {
                return found;
            }

            i = end;
        }

        // Every face is from a later expansion than the quest: none. Never the nearest later face, which for a giver
        // met early (G'raha Tia in the Crystal Tower) would be a later expansion's look, or reveal who they become.
        return default;
    }

    /// <summary>
    /// Builds the index: matches every giver in <paramref name="inputs"/> to the faces, with
    /// <paramref name="curation"/>'s names, aliases, blocks and pins applied.
    /// </summary>
    public static PortraitIndex Build(PortraitInputs inputs, PortraitCuration? curation = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        curation ??= PortraitCuration.Empty;

        string Aliased(string normalized) =>
            curation.Aliases.TryGetValue(normalized, out var alias) ? PortraitNames.Normalize(alias.Name) : normalized;

        // A delivery portrait without its keep mask would show the emblem script: it is not offered at all.
        bool Offered(uint icon, PortraitSource source) =>
            icon != 0 && source != PortraitSource.None && (source != PortraitSource.Delivery || curation.Masks.ContainsKey(icon));

        // A curated face or pin whose icon is not of the family it names is a typo (its crop would be measured on the
        // wrong texture): it is not offered either. The curated file's reader warns about both.
        bool CuratedOffered(uint icon, PortraitSource source) => Offered(icon, source) && PortraitSources.FamilyOfIcon(icon) == source;

        var faces = new List<PortraitFace>(inputs.Faces.Count + curation.Faces.Count);
        faces.AddRange(inputs.Faces.Where(f => Offered(f.Icon, f.Source)));
        faces.AddRange(curation.Faces
            .Where(kv => CuratedOffered(kv.Key, kv.Value.Source))
            .Select(kv => new PortraitFace(kv.Key, kv.Value.Source, kv.Value.Name, kv.Value.Era, Curated: true)));

        var byName = new Dictionary<string, List<PortraitFace>>(StringComparer.Ordinal);
        var byNpc = new Dictionary<uint, List<PortraitFace>>();

        // Per family: a several-word name's first word to the distinct full names starting with it.
        var byFirstWord = new Dictionary<(PortraitSource, string), HashSet<string>>();
        foreach (var face in faces)
        {
            var key = Aliased(PortraitNames.Normalize(face.Name));
            if (key.Length > 0 && !PortraitNames.IsGeneric(face.Name))
            {
                Add(byName, key, face);
                var first = PortraitNames.FirstWordOfSeveral(face.Name);
                if (first.Length > 0)
                {
                    if (!byFirstWord.TryGetValue((face.Source, first), out var names))
                    {
                        names = new HashSet<string>(StringComparer.Ordinal);
                        byFirstWord[(face.Source, first)] = names;
                    }

                    names.Add(key);
                }
            }

            if (face.NpcId != 0)
            {
                Add(byNpc, face.NpcId, face);
            }
        }

        // A giver's first expansion, over its story quests; over its seasonal ones only when it has no other.
        var questMap = new Dictionary<uint, PortraitQuest>();
        var firstEraByName = new Dictionary<string, byte>(StringComparer.Ordinal);
        var firstSeasonalEraByName = new Dictionary<string, byte>(StringComparer.Ordinal);
        var nameById = inputs.Givers.GroupBy(g => g.NpcId).ToDictionary(g => g.Key, g => g.First().Name);
        foreach (var quest in inputs.Quests)
        {
            questMap.TryAdd(quest.QuestId, quest);
            if (nameById.TryGetValue(quest.GiverId, out var name))
            {
                var key = PortraitNames.Normalize(name);
                var map = quest.Seasonal ? firstSeasonalEraByName : firstEraByName;
                map[key] = map.TryGetValue(key, out var seen) ? Math.Min(seen, quest.Expansion) : quest.Expansion;
            }
        }

        foreach (var (key, era) in firstSeasonalEraByName)
        {
            firstEraByName.TryAdd(key, era);
        }

        var built = new Dictionary<uint, Giver>();
        foreach (var giver in inputs.Givers)
        {
            if (built.ContainsKey(giver.NpcId))
            {
                continue;
            }

            var generic = PortraitNames.IsGeneric(giver.Name);
            var normalized = PortraitNames.Normalize(giver.Name);
            var firstEra = firstEraByName.GetValueOrDefault(normalized);
            var variants = generic
                ? []
                : Match(giver, normalized, Aliased(normalized), firstEra, byName, byNpc, byFirstWord, curation, CuratedOffered);
            built[giver.NpcId] = new Giver(
                giver.Name,
                variants,
                firstEra,
                giver.Race,
                giver.Gender,
                generic ? string.Empty : PortraitNames.Initials(giver.Name),
                generic);
        }

        var tribes = inputs.TribeIcons.Where(kv => kv.Key != 0 && kv.Value != 0).ToFrozenDictionary(kv => kv.Key, kv => kv.Value);
        return new PortraitIndex(built.ToFrozenDictionary(), questMap.ToFrozenDictionary(), tribes, curation.CropTable(), curation.Masks);
    }

    private static PortraitVariant[] Match(
        PortraitGiver giver,
        string normalized,
        string aliased,
        byte firstEra,
        Dictionary<string, List<PortraitFace>> byName,
        Dictionary<uint, List<PortraitFace>> byNpc,
        Dictionary<(PortraitSource, string), HashSet<string>> byFirstWord,
        PortraitCuration curation,
        Func<uint, PortraitSource, bool> offered)
    {
        if (curation.Pins.TryGetValue(giver.NpcId, out var pin) && offered(pin.Icon, pin.Source))
        {
            return [new PortraitVariant(pin.Icon, pin.Source, pin.Era ?? firstEra, giver.Name)];
        }

        var found = new List<PortraitFace>();
        if (byNpc.TryGetValue(giver.NpcId, out var direct))
        {
            found.AddRange(direct);
        }

        if (byName.TryGetValue(aliased, out var named))
        {
            found.AddRange(named);
        }

        if (PortraitNames.IsOneWord(giver.Name))
        {
            foreach (var source in PortraitSources.Priority)
            {
                if (!byFirstWord.TryGetValue((source, aliased), out var fullNames)
                    || fullNames.Count != 1)
                {
                    continue;
                }

                found.AddRange(byName[fullNames.First()].Where(f => f.Source == source));
            }
        }

        var blocks = curation.Blocks
            .Where(b => (b.GiverId != 0 && b.GiverId == giver.NpcId) || (b.NormalizedName.Length > 0 && (b.NormalizedName == normalized || b.NormalizedName == aliased)))
            .ToList();

        return found
            .Where(f => !blocks.Any(b => b.Blocks(f.Icon)))
            .GroupBy(f => f.Icon)
            // One face per icon: a curated face that gives an era first (the hand-checked era wins over a sheet's
            // guess for the same art), then the better family, then the earliest era.
            .Select(g => g.OrderBy(f => f.Curated && f.Era is not null ? 0 : 1).ThenBy(f => PortraitSources.Rank(f.Source)).ThenBy(f => f.Era ?? firstEra).First())
            .Select(f => new PortraitVariant(f.Icon, f.Source, f.Era ?? firstEra, f.Name))
            .OrderBy(v => PortraitSources.Rank(v.Source))
            .ThenBy(v => v.Era)
            .ThenBy(v => v.Icon)
            .ToArray();
    }

    private PortraitFallback FallbackFor(Giver? giver, byte beastTribe)
    {
        var race = giver?.Race ?? 0;
        var gender = giver?.Gender ?? 0;
        var initials = giver?.Initials ?? string.Empty;
        if (beastTribe != 0 && tribeIcons.TryGetValue(beastTribe, out var emblem))
        {
            return new PortraitFallback(PortraitFallbackKind.SocietyEmblem, emblem, race, gender, initials);
        }

        if (giver is null)
        {
            return new PortraitFallback(PortraitFallbackKind.Moon, 0, 0, 0, string.Empty);
        }

        if (giver.Generic || initials.Length == 0)
        {
            return new PortraitFallback(race != 0 ? PortraitFallbackKind.Silhouette : PortraitFallbackKind.Moon, 0, race, gender, initials);
        }

        return new PortraitFallback(PortraitFallbackKind.Initials, 0, race, gender, initials);
    }

    private static void Add<TKey>(Dictionary<TKey, List<PortraitFace>> map, TKey key, PortraitFace face)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        list.Add(face);
    }

    private sealed record Giver(string Name, PortraitVariant[] Variants, byte FirstEra, byte Race, byte Gender, string Initials, bool Generic);
}
