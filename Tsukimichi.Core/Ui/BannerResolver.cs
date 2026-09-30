using System.Collections.Frozen;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui;

/// <summary>Where a quest's hero banner came from (feature plan V4, "every quest has art"), in fallback order.</summary>
public enum BannerSource : byte
{
    /// <summary>Nothing resolved yet (a default <see cref="BannerChoice"/>).</summary>
    None = 0,

    /// <summary>The quest's own journal banner (<c>Quest.Icon</c>).</summary>
    Own,

    /// <summary>The journal banner of the nearest quest in the same chain or journal genre.</summary>
    Sibling,

    /// <summary>The Duty Finder banner (<c>ContentFinderCondition.Image</c>) of a duty the quest unlocks.</summary>
    Duty,

    /// <summary>The loading-screen image of the territory where the quest starts.</summary>
    Zone,

    /// <summary>The bundled Moon Road category art (<see cref="BannerArt"/>): original, shipped with the plugin.</summary>
    Category,
}

/// <summary>
/// The bundled category banners (<c>Tsukimichi/assets/ui/banners/&lt;slug&gt;@2x.png</c>, 752 × 240, sources in
/// <c>docs/design/moon-road/banners/</c>): each expansion's main scenario, the sidequest region families, and the other
/// journal categories. <see cref="Other"/> is the generic moon road.
/// </summary>
public enum BannerArt : byte
{
    Other = 0,
    MsqArr,
    MsqHw,
    MsqSb,
    MsqShb,
    MsqEw,
    MsqDt,
    SideLaNoscea,
    SideBlackShroud,
    SideThanalan,
    SideCoerthas,
    SideDravania,
    SideGyrAbaniaOthard,
    SideNorvrandt,
    SideIlsabard,
    SideTural,
    AlliedSocieties,
    ClassJob,
    GrandCompany,
    Seasonal,
    Chronicles,
    Hildibrand,
    Relic,
}

/// <summary>
/// A quest's resolved hero banner. <see cref="Art"/> is always set, so the UI can fall back to the bundled art when a
/// game texture fails to load.
/// </summary>
/// <param name="Source">Which step of the chain supplied the banner.</param>
/// <param name="IconId">The game icon to draw (<c>GetFromGameIcon</c>) for <see cref="BannerSource.Own"/>, <see cref="BannerSource.Sibling"/> and <see cref="BannerSource.Duty"/>; 0 otherwise.</param>
/// <param name="GamePath">The game texture path (<c>GetFromGame</c>) for <see cref="BannerSource.Zone"/>; null otherwise.</param>
/// <param name="Art">The bundled category art for the quest.</param>
/// <param name="SourceId">What lent the banner: the donor quest's row id (Sibling), the ContentFinderCondition row id (Duty), the TerritoryType row id (Zone); 0 otherwise.</param>
public readonly record struct BannerChoice(BannerSource Source, uint IconId, string? GamePath, BannerArt Art, uint SourceId)
{
    /// <summary>The bundled art alone.</summary>
    public static BannerChoice ForArt(BannerArt art) => new(BannerSource.Category, 0, null, art, 0);
}

/// <summary>The sheet lookups the banner chain needs beyond the catalog (Lumina-backed in <c>Tsukimichi.GameData.BannerSources</c>).</summary>
public interface IBannerLookups
{
    /// <summary>
    /// The Duty Finder banner of a duty the quest unlocks: the first of its duties (curated unlocks first, then its
    /// reward data) whose <c>ContentFinderCondition.Image</c> is set. False when none.
    /// </summary>
    bool TryGetDutyBanner(QuestRecord quest, out uint iconId, out uint contentFinderConditionId);

    /// <summary>The territory's loading-screen texture path (<c>ui/loadingimage/&lt;file&gt;_hr1.tex</c>), or null when it has none.</summary>
    string? ZoneBannerPath(uint territoryId);
}

/// <summary>
/// Every quest's hero banner, resolved once per catalog (feature plan V4). The chain, first hit wins:
/// <list type="number">
/// <item>the quest's own journal banner (<see cref="QuestRecord.Icon"/>);</item>
/// <item>the banner of the nearest quest in the same chain (its previous quests, walked back within the genre) or, failing that, the nearest in journal order within the same genre;</item>
/// <item>the Duty Finder banner of a duty the quest unlocks;</item>
/// <item>the loading-screen image of the territory where its issuer stands;</item>
/// <item>the bundled category art (<see cref="BannerArts.For"/>).</item>
/// </list>
/// Steps 1–4 are the player's own game art read at runtime; nothing official ships. Immutable; lookups allocate nothing.
/// </summary>
public sealed class BannerIndex
{
    /// <summary>How far back a chain is walked for a banner before the genre's journal order takes over.</summary>
    public const int ChainDepth = 8;

    public static readonly BannerIndex Empty = new(FrozenDictionary<uint, BannerChoice>.Empty);

    private readonly FrozenDictionary<uint, BannerChoice> byRowId;

    private BannerIndex(FrozenDictionary<uint, BannerChoice> byRowId)
    {
        this.byRowId = byRowId;
        var counts = new int[(int)BannerSource.Category + 1];
        foreach (var choice in byRowId.Values)
        {
            counts[(int)choice.Source]++;
        }

        Counts = counts;
    }

    /// <summary>How many quests each source supplied, indexed by <see cref="BannerSource"/>.</summary>
    public IReadOnlyList<int> Counts { get; }

    /// <summary>The number of quests resolved.</summary>
    public int Count => byRowId.Count;

    /// <summary>The quest's banner; a quest the index does not know gets its category art.</summary>
    public BannerChoice For(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return byRowId.TryGetValue(quest.RowId, out var choice) ? choice : BannerChoice.ForArt(BannerArts.For(quest));
    }

    /// <summary>The banner by Quest sheet row id.</summary>
    public bool TryGet(uint rowId, out BannerChoice choice) => byRowId.TryGetValue(rowId, out choice);

    /// <summary>Resolves every quest of the catalog. <paramref name="lookups"/> null skips the duty and zone steps.</summary>
    public static BannerIndex Build(QuestCatalog catalog, IBannerLookups? lookups)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var siblings = SiblingDonors(catalog);
        var result = new Dictionary<uint, BannerChoice>(catalog.Count);
        foreach (var quest in catalog.All)
        {
            result[quest.RowId] = Resolve(quest, siblings.GetValueOrDefault(quest.RowId), lookups);
        }

        return new BannerIndex(result.ToFrozenDictionary());
    }

    /// <summary>One quest through the chain, given its sibling donor (null when none).</summary>
    public static BannerChoice Resolve(QuestRecord quest, QuestRecord? siblingDonor, IBannerLookups? lookups)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var art = BannerArts.For(quest);
        if (quest.Icon != 0)
        {
            return new BannerChoice(BannerSource.Own, quest.Icon, null, art, 0);
        }

        if (siblingDonor is { Icon: not 0 })
        {
            return new BannerChoice(BannerSource.Sibling, siblingDonor.Icon, null, art, siblingDonor.RowId);
        }

        if (lookups is not null)
        {
            if (lookups.TryGetDutyBanner(quest, out var dutyIcon, out var condition) && dutyIcon != 0)
            {
                return new BannerChoice(BannerSource.Duty, dutyIcon, null, art, condition);
            }

            if (quest.Issuer is { TerritoryId: not 0 } issuer && lookups.ZoneBannerPath(issuer.TerritoryId) is { Length: > 0 } path)
            {
                return new BannerChoice(BannerSource.Zone, 0, path, art, issuer.TerritoryId);
            }
        }

        return BannerChoice.ForArt(art);
    }

    /// <summary>
    /// For every quest without a banner of its own, the quest that lends it one: first its chain (previous quests, walked
    /// breadth-first up to <see cref="ChainDepth"/> steps, staying in the genre), then the genre's nearest quest with a
    /// banner in journal order (the earlier one on a tie). Genre 0 (unlisted quests) lends nothing: it is no chain.
    /// Precomputed once per catalog, linear in the genre sizes.
    /// </summary>
    public static IReadOnlyDictionary<uint, QuestRecord> SiblingDonors(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var donors = new Dictionary<uint, QuestRecord>();
        foreach (var (genreId, quests) in catalog.ByGenre)
        {
            if (genreId == 0 || quests.Count < 2)
            {
                continue;
            }

            // Nearest banner before and after each position, in journal order.
            var before = new int[quests.Count];
            var after = new int[quests.Count];
            var last = -1;
            for (var i = 0; i < quests.Count; i++)
            {
                before[i] = last;
                if (quests[i].Icon != 0)
                {
                    last = i;
                }
            }

            last = -1;
            for (var i = quests.Count - 1; i >= 0; i--)
            {
                after[i] = last;
                if (quests[i].Icon != 0)
                {
                    last = i;
                }
            }

            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest.Icon != 0)
                {
                    continue;
                }

                if (ChainDonor(catalog, quest, genreId) is { } chained)
                {
                    donors[quest.RowId] = chained;
                    continue;
                }

                var b = before[i];
                var a = after[i];
                var pick = b < 0 ? a : a < 0 ? b : (i - b) <= (a - i) ? b : a;
                if (pick >= 0)
                {
                    donors[quest.RowId] = quests[pick];
                }
            }
        }

        return donors;
    }

    private static QuestRecord? ChainDonor(QuestCatalog catalog, QuestRecord quest, uint genreId)
    {
        var frontier = new List<uint>(quest.PreviousQuests.QuestIds);
        var seen = new HashSet<uint> { quest.RowId };
        for (var depth = 0; depth < ChainDepth && frontier.Count > 0; depth++)
        {
            var next = new List<uint>();
            foreach (var id in frontier)
            {
                if (!seen.Add(id) || catalog.GetByRowId(id) is not { } previous || previous.Journal.GenreId != genreId)
                {
                    continue;
                }

                if (previous.Icon != 0)
                {
                    return previous;
                }

                next.AddRange(previous.PreviousQuests.QuestIds);
            }

            frontier = next;
        }

        return null;
    }
}

/// <summary>
/// The bundled category art for a quest, chosen from its journal section, category and expansion by sheet ids (so it
/// holds in every client language), and each art's resource name.
/// </summary>
public static class BannerArts
{
    /// <summary>JournalSection ids.</summary>
    public const uint MainScenarioSection = 0;
    public const uint MainScenarioDawntrailSection = 1;
    public const uint ChroniclesSection = 2;
    public const uint SidequestSection = 3;
    public const uint AlliedSection = 4;
    public const uint AlliedDawntrailSection = 5;
    public const uint ClassJobSection = 6;
    public const uint OtherQuestsSection = 7;

    /// <summary>JournalCategory ids with their own art.</summary>
    public const uint ChroniclesOfLightCategory = 54;
    public const uint HildibrandCategory = 55;
    public const uint WeaponEnhancementCategory = 56;
    public const uint UnusualEndeavorsCategory = 57;
    public const uint SideStoryCategory = 58;
    public const uint GrandCompanyCategory = 96;
    public const uint SeasonalEventsCategory = 97;

    /// <summary>Every art, for tests and the preview.</summary>
    public static IReadOnlyList<BannerArt> All { get; } = Enum.GetValues<BannerArt>();

    /// <summary>The quest's category art.</summary>
    public static BannerArt For(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return For(quest.Journal.SectionId, quest.Journal.CategoryId, quest.Journal.GenreId, quest.Expansion);
    }

    /// <summary>
    /// The art for a journal position: main scenario by expansion; Chronicles of a New Era; sidequests by region family
    /// (the regional categories 59–85, and any later one by its expansion's family), with Chronicles of Light,
    /// Hildibrand, relic and endeavour categories their own; allied societies; class and job; Grand Company and seasonal
    /// events; everything else (and genre 0, the unlisted quests) the generic moon road.
    /// </summary>
    public static BannerArt For(uint sectionId, uint categoryId, uint genreId, byte expansion)
    {
        if (genreId == 0)
        {
            return BannerArt.Other;
        }

        return sectionId switch
        {
            MainScenarioSection or MainScenarioDawntrailSection => Msq(expansion),
            ChroniclesSection => BannerArt.Chronicles,
            SidequestSection => Sidequest(categoryId, expansion),
            AlliedSection or AlliedDawntrailSection => BannerArt.AlliedSocieties,
            ClassJobSection => BannerArt.ClassJob,
            OtherQuestsSection => categoryId switch
            {
                GrandCompanyCategory => BannerArt.GrandCompany,
                SeasonalEventsCategory => BannerArt.Seasonal,
                _ => BannerArt.Other,
            },
            _ => BannerArt.Other,
        };
    }

    /// <summary>The main scenario art of an expansion (0 ARR … 5 DT); a later expansion gets the generic art until it has its own.</summary>
    public static BannerArt Msq(byte expansion) => expansion switch
    {
        0 => BannerArt.MsqArr,
        1 => BannerArt.MsqHw,
        2 => BannerArt.MsqSb,
        3 => BannerArt.MsqShb,
        4 => BannerArt.MsqEw,
        5 => BannerArt.MsqDt,
        _ => BannerArt.Other,
    };

    /// <summary>
    /// A sidequest category's art. The regional categories by id: La Noscea 59; the Black Shroud 60; Thanalan 61;
    /// Coerthas, Mor Dhona and Ishgard 62–64; Abalathia, Dravania and Azys Lla 65–67; Gyr Abania, Othard and Hingashi
    /// 68–70; Norvrandt 71–76; Ilsabard, Sharlayan, Thavnair and the lands beyond 77–82; Tural 83–85.
    /// </summary>
    public static BannerArt Sidequest(uint categoryId, byte expansion) => categoryId switch
    {
        ChroniclesOfLightCategory => BannerArt.Chronicles,
        HildibrandCategory => BannerArt.Hildibrand,
        WeaponEnhancementCategory or UnusualEndeavorsCategory => BannerArt.Relic,
        SideStoryCategory => BannerArt.Other,
        59 => BannerArt.SideLaNoscea,
        60 => BannerArt.SideBlackShroud,
        61 => BannerArt.SideThanalan,
        >= 62 and <= 64 => BannerArt.SideCoerthas,
        >= 65 and <= 67 => BannerArt.SideDravania,
        >= 68 and <= 70 => BannerArt.SideGyrAbaniaOthard,
        >= 71 and <= 76 => BannerArt.SideNorvrandt,
        >= 77 and <= 82 => BannerArt.SideIlsabard,
        >= 83 and <= 85 => BannerArt.SideTural,
        _ => RegionOf(expansion),
    };

    /// <summary>Whether a sidequest category has an explicit region family (not the by-expansion guess).</summary>
    public static bool IsMappedRegion(uint categoryId) => categoryId is >= 59 and <= 85;

    /// <summary>A region family for a sidequest category this table does not know yet, by the quest's expansion.</summary>
    public static BannerArt RegionOf(byte expansion) => expansion switch
    {
        1 => BannerArt.SideDravania,
        2 => BannerArt.SideGyrAbaniaOthard,
        3 => BannerArt.SideNorvrandt,
        4 => BannerArt.SideIlsabard,
        5 => BannerArt.SideTural,
        _ => BannerArt.Other,
    };

    /// <summary>The art's file stem ("msq-arr", "side-black-shroud").</summary>
    public static string Slug(BannerArt art) => art switch
    {
        BannerArt.MsqArr => "msq-arr",
        BannerArt.MsqHw => "msq-hw",
        BannerArt.MsqSb => "msq-sb",
        BannerArt.MsqShb => "msq-shb",
        BannerArt.MsqEw => "msq-ew",
        BannerArt.MsqDt => "msq-dt",
        BannerArt.SideLaNoscea => "side-la-noscea",
        BannerArt.SideBlackShroud => "side-black-shroud",
        BannerArt.SideThanalan => "side-thanalan",
        BannerArt.SideCoerthas => "side-coerthas",
        BannerArt.SideDravania => "side-dravania",
        BannerArt.SideGyrAbaniaOthard => "side-gyr-abania-othard",
        BannerArt.SideNorvrandt => "side-norvrandt",
        BannerArt.SideIlsabard => "side-ilsabard",
        BannerArt.SideTural => "side-tural",
        BannerArt.AlliedSocieties => "allied-societies",
        BannerArt.ClassJob => "class-job",
        BannerArt.GrandCompany => "grand-company",
        BannerArt.Seasonal => "seasonal",
        BannerArt.Chronicles => "chronicles",
        BannerArt.Hildibrand => "hildibrand",
        BannerArt.Relic => "relic",
        _ => "other",
    };

    /// <summary>Pixel size of every bundled banner (2x of the hero's 376 × 120 logical).</summary>
    public const int PixelWidth = 752;
    public const int PixelHeight = 240;

    /// <summary>The embedded resource name ("Tsukimichi.assets.ui.banners.msq-arr@2x.png").</summary>
    public static string ResourceName(BannerArt art) => "Tsukimichi.assets.ui.banners." + Slug(art) + "@2x.png";
}
