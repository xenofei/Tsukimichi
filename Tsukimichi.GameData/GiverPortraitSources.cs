using System.Globalization;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Portraits;

namespace Tsukimichi.GameData;

/// <summary>
/// The sheet side of giver portraits (feature plan v7 F1): reads, once and off the frame, every NPC face the install
/// holds and every quest giver, for <see cref="PortraitIndex.Build"/>. The families:
/// <list type="bullet">
/// <item>Duty Support and Trust members (<c>DawnQuestMember</c>): <c>BigImageOld</c> the tall bust,
/// <c>BigImageNew</c> the wide strip, named by the member's ENpcResident or, for a blank one, by
/// <c>DawnMemberUIParam.Name</c> (Wuk Lamat, Koana, Krile); the era is the earliest expansion of the duties the row
/// joins (<c>DawnContentParticipable</c> → <c>DawnContent</c> → the duty's territory's ExVersion), else of another row
/// with the same art; a row whose era cannot be told (it joins no duty yet) is left out rather than guessed; and the
/// outfits a member can be switched into (<c>DawnGrowMember</c>), dated by their look (<see cref="OutfitLookEras"/>);</item>
/// <item>Triple Triad cards: icon 087000 + the card row, named by the card; the era is the expansion whose cards the
/// card's number falls among (<see cref="CardEra"/>);</item>
/// <item>battle-talk faces (073001–073999), named three ways: a quest battle's <c>FACE_GRAPHIC_&lt;NAME&gt;</c> script
/// variable (era: the battle's quest), a Scion note (<c>AkatsukiNote</c> list and title names, era: the quest that
/// unlocks the note), and a Doman Mahjong costume row, whose faces all show one character (a face shares the names
/// of its row's named faces; era: the expansion its attire is named for, else the costume's unlock quest);</item>
/// <item>custom delivery clients (<c>SatisfactionNpc.Icon</c>, era: the quest that opens the client).</item>
/// </list>
/// Names are read in English whatever the client's language: they are only matched (the script variables are English
/// whatever the client), never shown. Each part is read on its own; a part that fails is logged and left out.
/// Standalone (takes an <see cref="ExcelModule"/>) so tests run it without Dalamud.
/// </summary>
public static class GiverPortraitSources
{
    /// <summary>A Triple Triad card's 208 × 256 art is this plus the card's row id.</summary>
    public const uint CardArtBase = 87000;

    /// <summary>The battle-talk face set: <c>073001</c> and up, below <see cref="BattleTalkEnd"/>.</summary>
    public const uint BattleTalkFirst = 73001;

    public const uint BattleTalkEnd = 74000;

    /// <summary>The quest-battle script variable prefix that names a battle-talk face.</summary>
    public const string FaceVariablePrefix = "FACE_GRAPHIC_";

    /// <summary>Builds the index: <see cref="Read"/> and <see cref="PortraitIndex.Build"/> with <paramref name="curation"/>.</summary>
    /// <param name="iconExists">Whether the game has an icon's texture; a face whose texture is missing is left out. Null trusts every icon.</param>
    public static PortraitIndex Build(ExcelModule excel, PortraitCuration? curation = null, Func<uint, bool>? iconExists = null, System.Action<string>? log = null) =>
        PortraitIndex.Build(Read(excel, iconExists, log), curation);

    /// <summary>Reads every face, every quest giver, the quests' eras and societies, and the society emblems.</summary>
    public static PortraitInputs Read(ExcelModule excel, Func<uint, bool>? iconExists = null, System.Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var faces = new List<PortraitFace>();
        var givers = new List<PortraitGiver>();
        var quests = new List<PortraitQuest>();
        var tribes = new Dictionary<byte, uint>();

        void Part(string what, System.Action read)
        {
            try
            {
                read();
            }
            catch (Exception ex)
            {
                log?.Invoke($"Giver portraits: {what} could not be read; those faces are left out ({ex.GetBaseException().Message})");
            }
        }

        bool Exists(uint icon) => icon != 0 && (iconExists is null || iconExists(icon));

        void Face(uint icon, PortraitSource source, string? name, byte? era, uint npcId = 0)
        {
            if (Exists(icon) && !string.IsNullOrWhiteSpace(name))
            {
                faces.Add(new PortraitFace(icon, source, name.Trim(), era, npcId));
            }
        }

        var questSheet = excel.GetSheet<Quest>();
        byte? EraOfQuest(uint questId) =>
            questId != 0 && questSheet.GetRowOrDefault(questId) is { } quest ? (byte)quest.Expansion.RowId : null;

        var residents = English<ENpcResident>(excel);
        string NameOf(uint npcId) => npcId != 0 ? residents.GetRowOrDefault(npcId)?.Singular.ExtractText() ?? string.Empty : string.Empty;

        Part("quest givers", () =>
        {
            var bases = excel.GetSheet<ENpcBase>();
            var seen = new HashSet<uint>();
            foreach (var quest in questSheet)
            {
                var issuer = quest.IssuerStart;
                if (issuer.RowId == 0 || issuer.RowType != typeof(ENpcResident) || quest.Name.IsEmpty)
                {
                    continue;
                }

                quests.Add(new PortraitQuest(quest.RowId, issuer.RowId, (byte)quest.Expansion.RowId, (byte)quest.BeastTribe.RowId, Seasonal: quest.Festival.RowId != 0));
                if (seen.Add(issuer.RowId))
                {
                    var npc = bases.GetRowOrDefault(issuer.RowId);
                    givers.Add(new PortraitGiver(issuer.RowId, NameOf(issuer.RowId), (byte)(npc?.Race.RowId ?? 0), npc?.Gender ?? 0));
                }
            }
        });

        Part("allied society emblems", () =>
        {
            foreach (var tribe in excel.GetSheet<BeastTribe>())
            {
                if (tribe.RowId is > 0 and <= byte.MaxValue && tribe.Icon != 0)
                {
                    tribes[(byte)tribe.RowId] = tribe.Icon;
                }
            }
        });

        Part("Duty Support and Trust members", () =>
        {
            // A member row's era: the earliest expansion of the duties it joins.
            var eraByMember = new Dictionary<uint, byte>();
            var duties = excel.GetSheet<DawnContent>();
            foreach (var participants in excel.GetSubrowSheet<DawnContentParticipable>())
            {
                if (duties.GetRowOrDefault(participants.RowId)?.Content.ValueNullable?.TerritoryType.ValueNullable is not { } territory)
                {
                    continue;
                }

                var era = (byte)territory.ExVersion.RowId;
                foreach (var slot in participants)
                {
                    var member = slot.DawnQuestMember.RowId;
                    if (member != 0)
                    {
                        eraByMember[member] = eraByMember.TryGetValue(member, out var seen) ? Math.Min(seen, era) : era;
                    }
                }
            }

            // A row that joins no duty takes the era of another row with the same art; one with neither is left out,
            // since a face whose era cannot be told could be a later expansion's.
            var members = excel.GetSheet<DawnQuestMember>();
            var eraByArt = new Dictionary<uint, byte>();
            foreach (var row in members)
            {
                if (eraByMember.TryGetValue(row.RowId, out var era))
                {
                    foreach (var art in (ReadOnlySpan<uint>)[row.BigImageOld, row.BigImageNew])
                    {
                        if (art != 0)
                        {
                            eraByArt[art] = eraByArt.TryGetValue(art, out var seen) ? Math.Min(seen, era) : era;
                        }
                    }
                }
            }

            var labels = English<DawnMemberUIParam>(excel);
            foreach (var row in members)
            {
                var name = NameOf(row.Member.RowId);
                if (name.Length == 0)
                {
                    name = labels.GetRowOrDefault(row.Class.RowId)?.Name.ExtractText() ?? string.Empty;
                }

                byte? era = eraByMember.TryGetValue(row.RowId, out var known) ? known
                    : eraByArt.TryGetValue(row.BigImageOld, out var old) ? old
                    : eraByArt.TryGetValue(row.BigImageNew, out var strip) ? strip
                    : null;
                if (era is null)
                {
                    continue;
                }

                Face(row.BigImageOld, PortraitSource.TrustBust, name, era, row.Member.RowId);
                Face(row.BigImageNew, PortraitSource.TrustStrip, name, era, row.Member.RowId);
            }
        });

        Part("Duty Support and Trust outfits", () =>
        {
            // DawnGrowMember: the outfits a Trust member can be switched into, four slots of bust and strip each. The ones
            // DawnQuestMember shows are read above with their duties' era; the others are dated by the look they are
            // (OutfitLookEras), and one with no story look (the summer outfits, the Endwalker Trust outfits) is left out.
            var read = new HashSet<uint>(faces.Where(f => f.Source is PortraitSource.TrustBust or PortraitSource.TrustStrip).Select(f => f.Icon));
            var labels = English<DawnMemberUIParam>(excel);
            foreach (var row in excel.GetSheet<DawnGrowMember>())
            {
                var name = labels.GetRowOrDefault(row.Class.RowId)?.Name.ExtractText() ?? string.Empty;
                foreach (var (art, source) in row.SelectImage.Select(i => (i, PortraitSource.TrustBust)).Concat(row.PortraitImage.Select(i => (i, PortraitSource.TrustStrip))))
                {
                    if (art != 0 && !read.Contains(art) && OutfitLookEras.TryGetValue(art, out var era) && read.Add(art))
                    {
                        Face(art, source, name, era);
                    }
                }
            }
        });

        Part("Triple Triad cards", () =>
        {
            var residents = excel.GetSheet<TripleTriadCardResident>();
            foreach (var card in English<TripleTriadCard>(excel))
            {
                if (card.RowId != 0)
                {
                    var era = residents.GetRowOrDefault(card.RowId) is { } resident ? CardEra(resident.Order, resident.UIPriority) : null;
                    Face(CardArtBase + card.RowId, PortraitSource.TripleTriadCard, card.Name.ExtractText(), era);
                }
            }
        });

        Part("battle-talk faces named by quest battles", () =>
        {
            var named = new Dictionary<(uint Icon, string Name), byte?>();
            foreach (var battle in excel.GetSheet<QuestBattle>())
            {
                var era = EraOfQuest(battle.Quest.RowId);
                foreach (var param in battle.QuestBattleParams)
                {
                    var icon = param.ScriptValue;
                    if (icon is < BattleTalkFirst or >= BattleTalkEnd)
                    {
                        continue;
                    }

                    var variable = param.ScriptInstruction.ExtractText();
                    if (!variable.StartsWith(FaceVariablePrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var stem = FaceStem(variable);
                    if (stem.Length == 0)
                    {
                        continue;
                    }

                    // One face, several battles: the earliest era it appears in.
                    named[(icon, stem)] = named.TryGetValue((icon, stem), out var seen) ? Min(seen, era) : era;
                }
            }

            foreach (var ((icon, stem), era) in named)
            {
                Face(icon, PortraitSource.BattleTalk, stem, era);
            }
        });

        Part("battle-talk faces named by Scion notes", () =>
        {
            foreach (var note in EnglishSubrows<AkatsukiNote>(excel))
            {
                foreach (var row in note)
                {
                    var icon = (uint)Math.Max(0, row.Icon);
                    if (icon is < BattleTalkFirst or >= BattleTalkEnd)
                    {
                        continue;
                    }

                    var era = EraOfQuest(row.UnlockOnQuest.RowId);
                    Face(icon, PortraitSource.BattleTalk, row.ListName.ValueNullable?.Text.ExtractText(), era);
                    Face(icon, PortraitSource.BattleTalk, row.Title.ValueNullable?.Text.ExtractText(), era);
                }
            }
        });

        Part("battle-talk faces named by mahjong costumes", () =>
        {
            var expansions = English<ExVersion>(excel)
                .Select(x => (Era: (byte)x.RowId, Name: x.Name.ExtractText()))
                .Where(x => x.Name.Length > 0)
                .ToList();
            var namesByIcon = faces
                .Where(f => f.Source == PortraitSource.BattleTalk)
                .GroupBy(f => f.Icon)
                .ToDictionary(g => g.Key, g => g.Select(f => f.Name).Distinct(StringComparer.Ordinal).ToList());
            foreach (var costume in EnglishSubrows<EmjCostume>(excel))
            {
                var rows = costume
                    .Where(r => r.Image is >= BattleTalkFirst and < BattleTalkEnd)
                    .Select(r => (Icon: r.Image, Era: AttireEra(r.Data.ValueNullable?.Name.ExtractText(), expansions) ?? EraOfQuest(r.UnlockQuest.RowId)))
                    .ToList();
                var names = rows.SelectMany(r => namesByIcon.GetValueOrDefault(r.Icon) ?? []).Distinct(StringComparer.Ordinal).ToList();
                foreach (var (icon, era) in rows)
                {
                    if (namesByIcon.ContainsKey(icon))
                    {
                        continue;
                    }

                    foreach (var name in names)
                    {
                        Face(icon, PortraitSource.BattleTalk, name, era);
                    }
                }
            }
        });

        Part("custom delivery clients", () =>
        {
            foreach (var client in excel.GetSheet<SatisfactionNpc>())
            {
                if (client.Icon > 0)
                {
                    Face((uint)client.Icon, PortraitSource.Delivery, NameOf(client.Npc.RowId), EraOfQuest(client.QuestRequired.RowId), client.Npc.RowId);
                }
            }
        });

        return new PortraitInputs(faces, givers, quests, tribes);
    }

    /// <summary>
    /// The name part of a <c>FACE_GRAPHIC_</c> variable: the prefix and a <c>_VER&lt;n&gt;</c> suffix dropped
    /// (<c>FACE_GRAPHIC_THANCRED_VER20</c> → <c>THANCRED</c>); empty for a battle NPC or object placeholder
    /// (<c>BNPC_…</c>, <c>EOBJ_…</c>).
    /// </summary>
    public static string FaceStem(string variable)
    {
        if (string.IsNullOrEmpty(variable) || !variable.StartsWith(FaceVariablePrefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var stem = variable[FaceVariablePrefix.Length..];
        if (stem.StartsWith("BNPC", StringComparison.Ordinal) || stem.StartsWith("EOBJ", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var version = stem.LastIndexOf("_VER", StringComparison.Ordinal);
        if (version > 0 && int.TryParse(stem.AsSpan(version + 4), NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            stem = stem[..version];
        }

        return stem.Trim('_');
    }

    /// <summary>
    /// The Trust outfit alternates (<c>DawnGrowMember</c>, the 1.22.1 portrait audit C8) by the expansion of the look each
    /// one shows, bust and strip alike. Those not listed are never read: the summer outfits (072652–072658, 072660 and
    /// their strips) were never a story look, and no quest giver wears the Endwalker Trust outfits (072640–072643,
    /// 072646 and their strips): Thancred's, Y'shtola's and Urianger's giver rows keep their Shadowbringers bodies through
    /// Endwalker and Dawntrail, and Estinien's Endwalker rows wear 072644's travel outfit, so with the era picked first
    /// those outfits would replace the right look on every Endwalker quest. Estinien's Heavensward armour (072645) is not
    /// read either: the owner kept his Heavensward card for those quests (portrait review Q1), and a bust of the same era
    /// would outrank it.
    /// </summary>
    public static readonly IReadOnlyDictionary<uint, byte> OutfitLookEras = new Dictionary<uint, byte>
    {
        [72632] = 1, [72692] = 1, // Thancred: the Heavensward look of 3.1
        [72633] = 0, [72693] = 0, // Urianger: the hood and goggles of A Realm Reborn
        [72634] = 1, [72694] = 1, // Y'shtola: her Heavensward look
        [72635] = 3, [72695] = 3, // Ryne as "Minfilia" (Shadowbringers)
    };

    /// <summary>
    /// The card number (<c>TripleTriadCardResident.Order</c>) each expansion's cards start at: No. 1 A Realm Reborn,
    /// No. 68 Heavensward (Gaelicat), No. 170 Stormblood (Namazu), No. 239 Shadowbringers (Amaro), No. 313 Endwalker
    /// (Troll), No. 391 Dawntrail (Pelupelu). The game numbers cards in the order they were added, so a card's number
    /// tells the expansion of its art. A new expansion's first card number goes here when it ships; until then its
    /// cards read as the last expansion listed.
    /// </summary>
    public static readonly IReadOnlyList<ushort> CardEraStarts = [1, 68, 170, 239, 313, 391];

    /// <summary>
    /// A card's era from its number (<see cref="CardEraStarts"/>); null for a card outside the numbered list (the
    /// crossover cards, <c>UIPriority</c> set, are numbered on their own), whose era then reads as its giver's first
    /// expansion.
    /// </summary>
    public static byte? CardEra(ushort order, byte uiPriority)
    {
        if (uiPriority != 0 || order == 0)
        {
            return null;
        }

        byte era = 0;
        for (var i = 1; i < CardEraStarts.Count; i++)
        {
            if (order >= CardEraStarts[i])
            {
                era = (byte)i;
            }
        }

        return era;
    }

    /// <summary>"Heavensward Attire" → 1: the expansion a costume's attire is named for; null when it names none ("Growing Light Attire").</summary>
    internal static byte? AttireEra(string? attire, IReadOnlyList<(byte Era, string Name)> expansions)
    {
        if (string.IsNullOrWhiteSpace(attire))
        {
            return null;
        }

        // Longest name first, so no expansion's name is read inside another's.
        foreach (var (era, name) in expansions.OrderByDescending(x => x.Name.Length))
        {
            if (attire.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                return era;
            }
        }

        return null;
    }

    private static byte? Min(byte? a, byte? b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);

    /// <summary>The sheet in English, or in the client's language when the install has no English sheet.</summary>
    private static ExcelSheet<T> English<T>(ExcelModule excel)
        where T : struct, IExcelRow<T>
    {
        try
        {
            return excel.GetSheet<T>(Language.English);
        }
        catch (Exception)
        {
            return excel.GetSheet<T>();
        }
    }

    private static SubrowExcelSheet<T> EnglishSubrows<T>(ExcelModule excel)
        where T : struct, IExcelSubrow<T>
    {
        try
        {
            return excel.GetSubrowSheet<T>(Language.English);
        }
        catch (Exception)
        {
            return excel.GetSubrowSheet<T>();
        }
    }
}
