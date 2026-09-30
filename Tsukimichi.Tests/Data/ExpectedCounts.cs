namespace Tsukimichi.Tests.Data;

/// <summary>
/// The patch-sensitive numbers the data tests pin, in one place, stamped with the game version they were measured on
/// (docs/data/unlisted-report.md appendix A, docs/data/verification-report-2.md section 5, the DataGen sanity checks).
/// A game update that adds quests moves the totals; a refiler or mapper change moves the rule counts and the feature
/// set. Change a number here only with the report or the golden file that explains it.
/// </summary>
internal static class ExpectedCounts
{
    public const string GameVersion = "2026.09.15.0000.0000";

    /// <summary>Named rows of the Quest sheet.</summary>
    public const int NamedQuests = 5373;

    /// <summary>Genre-0 rows of the sheet: the 0.6.0 Unlisted bucket, and the Legacy filing's bucket today.</summary>
    public const int LegacyUnlisted = 180;

    /// <summary>Listed rows of the sheet (<see cref="NamedQuests"/> − <see cref="LegacyUnlisted"/>): the 0.6.0 All quests total.</summary>
    public const int LegacyListed = 5193;

    /// <summary>0.6.0 per-section totals (verification-report-2 section 5, "Catalog rows").</summary>
    public static readonly IReadOnlyDictionary<uint, int> LegacySectionTotals = new Dictionary<uint, int>
    {
        [0] = 907,
        [1] = 143,
        [2] = 196,
        [3] = 2027,
        [4] = 612,
        [5] = 104,
        [6] = 875,
        [7] = 329,
    };

    // ---- Refiling (unlisted-report appendix A; the rules alone, before curated overrides) ----
    public const int RetiredByRule1 = 99;
    public const int RefiledByRule2 = 23;

    /// <summary>
    /// Of the rule-2 rows, the class intros (<c>Cls…001</c>/<c>999</c>, "So You Want to Be a Gladiator") stay out of
    /// the totals (<c>QuestRecord.CountsInTotals</c> false: the class a character started as never gets its intro);
    /// the job intros (<c>Job…299</c>: A Dark Spectacle, So You Want to Be a Machinist, What's Your Sign) count.
    /// </summary>
    public const int UncountedClassIntros = 20;
    public const int CountedJobIntros = 3;
    public const int RefiledByRule3 = 3;
    public const int RefiledByRule4 = 24;
    public const int RefiledByRule5 = 3;
    public const int RefiledByRule6 = 27;
    public const int LeftByRule7 = 1;
    public const int RefiledByRules = RefiledByRule2 + RefiledByRule3 + RefiledByRule4 + RefiledByRule5 + RefiledByRule6;

    /// <summary>Curated overrides applied to genre-0 rows: the three Eureka entry quests, Seeing the Cieldalaes, The New Frontier.</summary>
    public const int OverriddenUnlisted = 5;

    /// <summary>
    /// Listed rows retired_quests.json names: the 3.05 trio the sheet does not mark (<see cref="CuratedOnlyRetiredListed"/>)
    /// and the five rows on the placeholder issuer, which rule 1 retires by itself (<see cref="SheetRetiredListed"/>;
    /// the entry only carries the patch note).
    /// </summary>
    public const int CuratedRetiredListed = 8;
    public const int SheetRetiredListed = 5;
    public const int CuratedOnlyRetiredListed = 3;

    /// <summary>Every retired row: <see cref="RetiredByRule1"/> + <see cref="CuratedRetiredListed"/>.</summary>
    public const int Retired = RetiredByRule1 + CuratedRetiredListed;

    /// <summary>Retired rows with a listed same-name replacement whose internal id starts with "Xx" (5.3, 5.5 and 6.x rewrites).</summary>
    public const int RetiredTwins = 44;

    /// <summary>Rows carrying the sheet's hidden flag: 98 unlisted + 2 listed (66033, 66034).</summary>
    public const int HiddenRows = 100;
    public const int HiddenUnlistedRows = 98;

    /// <summary>Quasi-quests (EventIconType 10) that are neither retired nor listed by the sheet: the 58 the plan expects to join Unlock quests.</summary>
    public const int RefiledQuasiQuests = 58;

    /// <summary>Listed quasi-quests (Earning Your Wings, The Crystal (Line's) Call).</summary>
    public const int ListedQuasiQuests = 2;

    /// <summary>
    /// <c>FeaturePresets.Derive</c> over the refiled catalog with the shipped curated and unique-reward data: the 0.6.0
    /// set of 1,699 plus the 43 quasi-quests the icon rule newly admits (the other 15 of the 58, the class intros
    /// among them, were in through their unlock rewards already), minus the 8 retired rows the rule now drops (1,734),
    /// minus the four Heavensward quests the AetherCurrent sheet named instead of the quest that awards the current
    /// (67328, 67334, 67365, 67437; docs/data/v4/flight-currents.md). The awarding quests were in already through their
    /// "Aether Current" quest reward, and Thavnair's listed 70030 stays in on its own merits.
    /// </summary>
    public const int FeatureQuests = 1730;

    /// <summary>Retired rows that carry an unlock reward or the blue icon and so derived as feature quests in 0.6.0.</summary>
    public const int RetiredFeatureRows = 8;

    // ---- Lodestone (verification-report-2 section 5) ----

    /// <summary>The Lodestone Eorzea Database count per journal section, 2026-09-28.</summary>
    public static readonly IReadOnlyDictionary<uint, int> LodestoneSectionTotals = new Dictionary<uint, int>
    {
        [0] = 907,
        [1] = 140,
        [2] = 192,
        [3] = 2021,
        [4] = 612,
        [5] = 104,
        [6] = 866,
        [7] = 307,
    };

    /// <summary>
    /// Listed rows the Lodestone lags on or omits, by section (the report's explanation column): the newest main
    /// scenario rows, the promotional quests, the Beastmaster job quests and Keyward Bound. Each must be a listed
    /// sheet row of its section. The seasonal rows of section 7 are a range, <see cref="SeasonalLagFromRowId"/>. The
    /// report's other differences (the retired Crystal Tower, Return to Ivalice, Mor Dhonan and 3.05 rows) are retired
    /// now and no longer counted.
    /// </summary>
    public static readonly IReadOnlyDictionary<uint, uint[]> LodestoneLag = new Dictionary<uint, uint[]>
    {
        [1] = [71012, 71013, 71014],
        [3] = [68543, 70121],
        [6] = [71027, 71028, 71029, 71030, 71031, 71032, 71033, 71034, 71035],
        [7] = [71002],
    };

    /// <summary>
    /// The seasonal rows the Lodestone has not indexed (the 2025–26 events, Heavensturn through Little Ladies' Day):
    /// the report gives their count per section and category, not their ids, so they are pinned as counts.
    /// </summary>
    public static readonly IReadOnlyDictionary<uint, int> LodestoneLagSectionCounts = new Dictionary<uint, int> { [7] = 21 };
    public static readonly IReadOnlyDictionary<uint, int> LodestoneLagCategoryCounts = new Dictionary<uint, int> { [97] = 21 };

    /// <summary>The Lodestone count per journal category the report lists as differing, 2026-09-28.</summary>
    public static readonly IReadOnlyDictionary<uint, int> LodestoneCategoryTotals = new Dictionary<uint, int>
    {
        [15] = 15,
        [18] = 8,
        [23] = 10,
        [59] = 126,
        [60] = 117,
        [61] = 137,
        [63] = 43,
        [93] = 199,
        [97] = 273,
        [98] = 19,
    };
}
