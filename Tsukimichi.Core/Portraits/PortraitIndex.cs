using System.Collections.Concurrent;
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
/// <param name="Curated">Named by the curated file (<see cref="PortraitCuration.Faces"/>,
/// <see cref="PortraitCuration.SharedFaces"/>): its era, when it gives one, wins over a sheet face's for the same icon.</param>
/// <param name="Crop">The crop this person wears of a shared icon (<see cref="CuratedSharedFace"/>); null wears the icon's.</param>
public sealed record PortraitFace(uint Icon, PortraitSource Source, string Name, byte? Era = null, uint NpcId = 0, bool Curated = false, PortraitCrop? Crop = null);

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
/// <param name="Curated">Chosen by hand: a face the curated file names (<see cref="PortraitCuration.Faces"/>,
/// <see cref="PortraitCuration.SharedFaces"/>) or a pin. Curated game art ranks above the portrait pack
/// (<see cref="PortraitIndex.PackWins"/>).</param>
/// <param name="Crop">The giver's own crop of a shared icon (two people on one card); null wears the icon's crop.</param>
public readonly record struct PortraitVariant(uint Icon, PortraitSource Source, byte Era, string MatchedName, bool Curated = false, PortraitCrop? Crop = null);

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
/// <param name="SourceBox">For a portrait pack photo, the side in source pixels of the head box it was cut from (the hover
/// never shows it larger, spec-1.20 F4); 0 for game art and when the pack does not say.</param>
public readonly record struct PortraitRef(uint GiverId, uint Icon, PortraitSource Source, PortraitCrop Crop, byte Era, PortraitFallback Fallback, PortraitMask? Mask = null, int SourceBox = 0)
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
/// <b>Picking.</b> The era first, then the family (the 1.22.1 portrait audit, C4): the face of the latest expansion
/// not after the quest's, across every family, is the look the character had then; among faces of that one era the
/// family order of <see cref="PortraitSources.Priority"/> decides (Trust bust, Triple Triad card, battle-talk face,
/// delivery portrait), then the lowest icon. A Trust strip is the last resort: it is worn only when no other family has
/// a face of the quest's era or earlier. A face from a later expansion than the
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

    /// <summary>Matches a name that gives no quest (a cast member who is never a giver) to its faces; shared by every copy.</summary>
    private readonly NameMatcher names;

    private PortraitIndex(FrozenDictionary<uint, Giver> givers, FrozenDictionary<uint, PortraitQuest> quests, FrozenDictionary<byte, uint> tribeIcons, PortraitCrops crops, IReadOnlyDictionary<uint, PortraitMask> masks, NameMatcher names, PortraitPack? pack = null)
    {
        this.givers = givers;
        this.quests = quests;
        this.tribeIcons = tribeIcons;
        this.masks = masks;
        this.names = names;
        this.pack = pack;
        Crops = crops;
        GiversWithArt = givers.Values.Count(g => g.Variants.Length > 0);
    }

    /// <summary>The portrait pack this index fills gaps from (<see cref="WithPack"/>); null for game art alone.</summary>
    public PortraitPack? Pack => pack;

    /// <summary>
    /// This index with the optional portrait pack (feature plan v7 F4) in it, or without one for null: a giver the pack
    /// has a photo of wears it unless the game art picked for the quest is curated (<see cref="PackWins"/>), and every
    /// other giver gets the game art exactly as before. Shares everything with this index; nothing is rebuilt.
    /// </summary>
    public PortraitIndex WithPack(PortraitPack? portraitPack) =>
        ReferenceEquals(portraitPack, pack) ? this : new PortraitIndex(givers, quests, tribeIcons, Crops, masks, names, portraitPack);

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

        // A pack photo shows its own ENpcResident row as that row looks, so it belongs to the row's era (the first
        // expansion it gives a quest in). A quest of an earlier expansion never wears it: a cast plate asks for every row
        // of a character, and a later row's photo would show a later look (Emet-Selch's Elpis photo on his
        // Shadowbringers quests). When worn it carries the quest's era, so the spoiler shield weighs it as before; a
        // seasonal event's quest carries the row's.
        var rowEra = giver?.RowEra ?? (era == SeasonalEra ? (byte)0 : era);
        if (pack is not null && pack.Has(giverId) && (era == SeasonalEra || rowEra <= era) && PackWins(variant, Crops))
        {
            var packEra = era == SeasonalEra ? rowEra : era;
            return new PortraitRef(giverId, giverId, PortraitSource.Pack, PortraitCrop.Full, packEra, fallback, SourceBox: pack.BoxOf(giverId));
        }

        if (variant.Source == PortraitSource.None)
        {
            return new PortraitRef(giverId, 0, PortraitSource.None, PortraitCrop.Full, era, fallback);
        }

        return new PortraitRef(giverId, variant.Icon, variant.Source, CropOf(variant), variant.Era, fallback, masks.GetValueOrDefault(variant.Icon));
    }

    /// <summary>
    /// Whether a pack photo is worn instead of the game art's pick <paramref name="game"/> (the 1.22.1 portrait audit,
    /// C1): when the game has none for the quest, or when the pick is uncurated: no crop of its own in
    /// <paramref name="crops"/> (<see cref="PortraitCrops.HasIconCrop"/>), and neither named by the curated file nor
    /// pinned (<see cref="PortraitVariant.Curated"/>). A face measured or chosen by hand is a known good portrait; a pack
    /// photo is cut by a machine and can hold a staff or a lance instead of a head.
    /// </summary>
    public static bool PackWins(PortraitVariant game, PortraitCrops crops)
    {
        ArgumentNullException.ThrowIfNull(crops);
        return game.Source == PortraitSource.None
            || (!game.Curated && game.Crop is null && !crops.HasIconCrop(game.Icon) && PortraitSources.Rank(PortraitSource.Pack) < PortraitSources.Rank(game.Source));
    }

    /// <summary>Every portrait the giver can wear, latest era first (Trust strips last), best family first within one; empty when none.</summary>
    public IReadOnlyList<PortraitVariant> Variants(uint giverId) =>
        givers.TryGetValue(giverId, out var giver) ? giver.Variants : [];

    /// <summary>The giver's name as matched (English); empty when unknown.</summary>
    public string NameOf(uint giverId) => givers.TryGetValue(giverId, out var giver) ? giver.Name : string.Empty;

    /// <summary>
    /// The era of the giver row itself: the first expansion it gives a story quest in (a seasonal one when it gives no
    /// other). A row is one look of a character, so this is when that look starts. 0 when the index does not know the row.
    /// </summary>
    public byte RowEraOf(uint giverId) => givers.TryGetValue(giverId, out var giver) ? giver.RowEra : (byte)0;

    /// <summary>
    /// The face among <paramref name="variants"/> for a quest of <paramref name="era"/> (the 1.22.1 portrait audit, C4):
    /// of every face from that era or earlier, the latest era wins, then the better family
    /// (<see cref="PortraitSources.Rank"/>), then the lowest icon; a Trust strip only when no other family has one. When
    /// every face is from a later expansion there is none (<c>default</c>, <see cref="PortraitSource.None"/>): a later
    /// face could show a look, or a person, the story has not reached, so the giver's fallback shows instead. Any order of
    /// <paramref name="variants"/>; allocates nothing.
    /// </summary>
    public static PortraitVariant Pick(IReadOnlyList<PortraitVariant> variants, byte era)
    {
        ArgumentNullException.ThrowIfNull(variants);
        PortraitVariant best = default, strip = default;
        for (var i = 0; i < variants.Count; i++)
        {
            var face = variants[i];
            if (face.Era > era || face.Source == PortraitSource.None)
            {
                // Never the nearest later face, which for a giver met early (G'raha Tia in the Crystal Tower) would be
                // a later expansion's look, or reveal who they become.
                continue;
            }

            if (face.Source == PortraitSource.TrustStrip)
            {
                strip = Better(face, strip) ? face : strip;
            }
            else
            {
                best = Better(face, best) ? face : best;
            }
        }

        return best.Source != PortraitSource.None ? best : strip;

        static bool Better(PortraitVariant face, PortraitVariant than) =>
            than.Source == PortraitSource.None
            || face.Era > than.Era
            || (face.Era == than.Era && (PortraitSources.Rank(face.Source) < PortraitSources.Rank(than.Source)
                || (face.Source == than.Source && face.Icon < than.Icon)));
    }

    /// <summary>
    /// A cast member's plate for a quest of <paramref name="era"/> (the 1.22.1 portrait audit, C2; feature plan v7 N10):
    /// the first of the character's ENpcResident rows the index knows that has a portrait, tried in this order: the rows
    /// the quest's own script names (<paramref name="questRows"/>: the look the quest shows), then the rows of the quest's
    /// era, then earlier rows latest first, then later rows earliest first (a later row still offers its earlier game
    /// art, by the era rule; never its pack photo, <see cref="For(uint, byte, byte)"/>). A character none of whose rows
    /// gives a quest is looked up by name (<see cref="ForName"/>). No portrait: <see cref="PortraitRef.HasArt"/> false.
    /// Allocates nothing once the name is cached.
    /// </summary>
    /// <param name="name">The character's English name (<c>CastMember.Key</c>).</param>
    /// <param name="npcIds">Every ENpcResident row of the character.</param>
    /// <param name="questRows">The rows of the character the quest's script names; empty when not known.</param>
    public PortraitRef ForCast(string name, IReadOnlyList<uint> npcIds, IReadOnlyList<uint> questRows, byte era)
    {
        ArgumentNullException.ThrowIfNull(npcIds);
        ArgumentNullException.ThrowIfNull(questRows);
        for (var i = 0; i < questRows.Count; i++)
        {
            if (givers.ContainsKey(questRows[i]) && For(questRows[i], era, 0) is { HasArt: true } inQuest)
            {
                return inQuest;
            }
        }

        var known = false;
        byte latest = 0;
        for (var i = 0; i < npcIds.Count; i++)
        {
            if (givers.TryGetValue(npcIds[i], out var row))
            {
                known = true;
                latest = Math.Max(latest, row.RowEra);
            }
        }

        if (!known)
        {
            return ForName(name, era);
        }

        // The quest's era, then down to the first; a seasonal event's quest starts from the latest row.
        var start = era == SeasonalEra ? latest : era;
        for (var target = (int)start; target >= 0; target--)
        {
            if (TryRowsOfEra(npcIds, (byte)target, era, out var portrait))
            {
                return portrait;
            }
        }

        for (var target = start + 1; target <= latest; target++)
        {
            if (TryRowsOfEra(npcIds, (byte)target, era, out var portrait))
            {
                return portrait;
            }
        }

        return new PortraitRef(0, 0, PortraitSource.None, PortraitCrop.Full, era, InitialsOf(name));
    }

    /// <summary>
    /// The game art for a name that gives no quest (a cast member the index has no giver row of; the 1.22.1 portrait
    /// audit, C2): matched as a named giver of that name would be (names, aliases, the first-word rule, name blocks; no
    /// pins or id blocks, which need a row), picked by <paramref name="era"/>. A face whose source gives no era is left
    /// out: with no quest of the name's to date it by, it could be a later look. Never a pack photo (the pack is by row).
    /// </summary>
    public PortraitRef ForName(string name, byte era)
    {
        var fallback = InitialsOf(name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return new PortraitRef(0, 0, PortraitSource.None, PortraitCrop.Full, era, fallback);
        }

        // A generic name ("troubled adventurer") matches nothing: its cached entry has no faces.
        var variant = Pick(names.Of(name).Variants, era);
        return variant.Source == PortraitSource.None
            ? new PortraitRef(0, 0, PortraitSource.None, PortraitCrop.Full, era, fallback)
            : new PortraitRef(0, variant.Icon, variant.Source, CropOf(variant), variant.Era, fallback, masks.GetValueOrDefault(variant.Icon));
    }

    private bool TryRowsOfEra(IReadOnlyList<uint> npcIds, byte rowEra, byte era, out PortraitRef portrait)
    {
        for (var i = 0; i < npcIds.Count; i++)
        {
            // A row without art: the next one, never initials while another row has a face (1.22.1 audit, C2).
            if (givers.TryGetValue(npcIds[i], out var row) && row.RowEra == rowEra && For(npcIds[i], era, 0) is { HasArt: true } found)
            {
                portrait = found;
                return true;
            }
        }

        portrait = default;
        return false;
    }

    /// <summary>A name's initials medallion, its string worked out once per name (<see cref="NameMatcher"/>).</summary>
    private PortraitFallback InitialsOf(string name) =>
        new(PortraitFallbackKind.Initials, 0, 0, 0, string.IsNullOrEmpty(name) ? string.Empty : names.Of(name).Initials);

    /// <summary>The crop a variant is drawn through: the giver's own crop of a shared icon, else the icon's, else its family's.</summary>
    private PortraitCrop CropOf(PortraitVariant variant) => variant.Crop ?? Crops.For(variant.Source, variant.Icon);

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

        var faces = new List<PortraitFace>(inputs.Faces.Count + curation.Faces.Count + curation.SharedFaces.Count);
        faces.AddRange(inputs.Faces.Where(f => Offered(f.Icon, f.Source)));
        faces.AddRange(curation.Faces
            .Where(kv => CuratedOffered(kv.Key, kv.Value.Source))
            .Select(kv => new PortraitFace(kv.Key, kv.Value.Source, kv.Value.Name, kv.Value.Era, Curated: true)));

        // One person of a shared icon, with that person's crop (two people on one card: the 1.22.1 audit, C6).
        faces.AddRange(curation.SharedFaces
            .Where(f => CuratedOffered(f.Icon, f.Source))
            .Select(f => new PortraitFace(f.Icon, f.Source, f.Name, f.Era, Curated: true, Crop: f.Crop)));

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

        // A giver's first expansion, over its story quests; over its seasonal ones only when it has no other. The same per
        // row: the expansion one row (one look of the character) first gives a quest in.
        var questMap = new Dictionary<uint, PortraitQuest>();
        var firstEraByName = new Dictionary<string, byte>(StringComparer.Ordinal);
        var firstSeasonalEraByName = new Dictionary<string, byte>(StringComparer.Ordinal);
        var rowEras = new Dictionary<uint, byte>();
        var rowSeasonalEras = new Dictionary<uint, byte>();
        var nameById = inputs.Givers.GroupBy(g => g.NpcId).ToDictionary(g => g.Key, g => g.First().Name);
        foreach (var quest in inputs.Quests)
        {
            questMap.TryAdd(quest.QuestId, quest);
            var rows = quest.Seasonal ? rowSeasonalEras : rowEras;
            rows[quest.GiverId] = rows.TryGetValue(quest.GiverId, out var rowSeen) ? Math.Min(rowSeen, quest.Expansion) : quest.Expansion;
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

        foreach (var (id, era) in rowSeasonalEras)
        {
            rowEras.TryAdd(id, era);
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
                rowEras.TryGetValue(giver.NpcId, out var rowEra) ? rowEra : firstEra,
                giver.Race,
                giver.Gender,
                generic ? string.Empty : PortraitNames.Initials(giver.Name),
                generic);
        }

        // A name that gives no quest is matched as a named giver of that name would be, on first ask. Its era-less faces
        // are dropped: no quest of the name's dates them, so any of them could be a later look.
        const byte Undated = byte.MaxValue;
        var nameMatcher = new NameMatcher(name =>
        {
            var normalized = PortraitNames.Normalize(name);
            if (normalized.Length == 0 || PortraitNames.IsGeneric(name))
            {
                return [];
            }

            var firstEra = firstEraByName.TryGetValue(normalized, out var known) ? known : Undated;
            return Match(new PortraitGiver(0, name, 0, 0), normalized, Aliased(normalized), firstEra, byName, byNpc, byFirstWord, curation, CuratedOffered)
                .Where(v => v.Era != Undated)
                .ToArray();
        });

        var tribes = inputs.TribeIcons.Where(kv => kv.Key != 0 && kv.Value != 0).ToFrozenDictionary(kv => kv.Key, kv => kv.Value);
        return new PortraitIndex(built.ToFrozenDictionary(), questMap.ToFrozenDictionary(), tribes, curation.CropTable(), curation.Masks, nameMatcher);
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
            return [new PortraitVariant(pin.Icon, pin.Source, pin.Era ?? firstEra, giver.Name, Curated: true)];
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
            // guess for the same art), then any curated face (a shared icon's person carries their own crop), then the
            // better family, then the earliest era.
            .Select(g => g.OrderBy(f => f.Curated && f.Era is not null ? 0 : 1).ThenBy(f => f.Curated ? 0 : 1).ThenBy(f => PortraitSources.Rank(f.Source)).ThenBy(f => f.Era ?? firstEra).First())
            .Select(f => new PortraitVariant(f.Icon, f.Source, f.Era ?? firstEra, f.Name, f.Curated, f.Crop))
            // The order Pick prefers: Trust strips last, then the latest era, the better family, the lowest icon.
            .OrderBy(v => v.Source == PortraitSource.TrustStrip ? 1 : 0)
            .ThenByDescending(v => v.Era)
            .ThenBy(v => PortraitSources.Rank(v.Source))
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

    /// <param name="FirstEra">The first expansion any giver of this name gives a quest in (era-less faces read as it).</param>
    /// <param name="RowEra">The first expansion this row gives a quest in: when this look of the character starts.</param>
    private sealed record Giver(string Name, PortraitVariant[] Variants, byte FirstEra, byte RowEra, byte Race, byte Gender, string Initials, bool Generic);

    /// <summary>A name's faces and initials, worked out on first ask and kept (the cast's names are a few hundred at most). Thread-safe.</summary>
    private sealed class NameMatcher(Func<string, PortraitVariant[]> match)
    {
        private readonly ConcurrentDictionary<string, NameEntry> cache = new(StringComparer.Ordinal);
        private readonly Func<string, NameEntry> entry = name => new NameEntry(match(name), PortraitNames.Initials(name));

        public NameEntry Of(string name) => cache.GetOrAdd(name, entry);
    }

    private sealed record NameEntry(PortraitVariant[] Variants, string Initials);
}
