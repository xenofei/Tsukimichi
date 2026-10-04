using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Plan;

namespace Tsukimichi.GameData;

/// <summary>
/// Every named duty of the ContentFinderCondition sheet as the "Clear my blues" plan tags it (<see cref="PlanDuties"/>):
/// its kind from <c>ContentType</c> and, for raids, <c>ContentMemberType</c>. Standalone (takes an
/// <see cref="ExcelModule"/>) so tests can build it against game data without Dalamud.
/// </summary>
public static class DutyIndex
{
    /// <summary><c>ContentFinderCondition.ContentLinkType</c> value whose <c>Content</c> is an InstanceContent row.</summary>
    public const byte InstanceContentLink = 1;

    // ContentType rows (the Duty Finder's categories).
    public const uint Dungeons = 2;
    public const uint Guildhests = 3;
    public const uint Trials = 4;
    public const uint Raids = 5;
    public const uint PvP = 6;
    public const uint TreasureHunt = 9;
    public const uint SocietyQuests = 13;
    public const uint GoldSaucer = 19;
    public const uint DeepDungeons = 21;
    public const uint WondrousTails = 24;
    public const uint Eureka = 26;
    public const uint MaskedCarnivale = 27;
    public const uint UltimateRaids = 28;
    public const uint SaveTheQueen = 29;
    public const uint VariantCriterion = 30;
    public const uint OceanFishing = 31;
    public const uint TripleTriad = 32;
    public const uint IslandSanctuary = 36;
    public const uint ChaoticAllianceRaid = 37;
    public const uint OccultCrescent = 38;

    /// <summary>
    /// The plan's kind for a duty: Dungeons are dungeons, Trials trials; Raids are alliance raids when their member
    /// type seats three parties or more (24 players) and normal raids otherwise, Ultimate raids normal raids and the
    /// Chaotic alliance raid an alliance raid; Eureka, Save the Queen (Bozja, Zadnor) and the Occult Crescent are field
    /// operations; society duties Society; PvP, treasure hunt, the Gold Saucer, deep dungeons, Wondrous Tails, the
    /// Masked Carnivale, Variant and Criterion dungeons, ocean fishing, Triple Triad and Island Sanctuary are systems;
    /// anything else (guildhests, quest battles) is Other.
    /// </summary>
    public static UnlockKind KindOf(uint contentType, int partyCount) => contentType switch
    {
        Dungeons => UnlockKind.Dungeon,
        Trials => UnlockKind.Trial,
        Raids => partyCount >= 3 ? UnlockKind.AllianceRaid : UnlockKind.NormalRaid,
        UltimateRaids => UnlockKind.NormalRaid,
        ChaoticAllianceRaid => UnlockKind.AllianceRaid,
        Eureka or SaveTheQueen or OccultCrescent => UnlockKind.FieldOperation,
        SocietyQuests => UnlockKind.Society,
        PvP or TreasureHunt or GoldSaucer or DeepDungeons or WondrousTails or MaskedCarnivale or VariantCriterion
            or OceanFishing or TripleTriad or IslandSanctuary => UnlockKind.System,
        _ => UnlockKind.Other,
    };

    /// <summary>Reads the ContentFinderCondition sheet once; rows without a name are skipped. High-end duties are marked (<see cref="PlanDuty.HighEnd"/>).</summary>
    public static PlanDuties Build(ExcelModule excel, Language language = Language.None)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var duties = new List<PlanDuty>();
        var highEndCategories = DutyRunSheets.HighEndCategories(excel);
        foreach (var row in excel.GetSheet<ContentFinderCondition>(language))
        {
            var name = row.Name.ExtractText().Trim();
            if (name.Length == 0)
            {
                continue;
            }

            var partyCount = row.ContentMemberType.ValueNullable?.PartyCount ?? 0;
            var instance = row.ContentLinkType == InstanceContentLink ? row.Content.RowId : 0u;
            // High-end as C7's badge reads it (DutyRunSheets): the sheet's flag for the current tier, the Ultimate and
            // Chaotic content types, and the Duty Finder's "High-end Trials" and "Savage Raids" categories.
            var highEnd = row.HighEndDuty
                || row.ContentType.RowId is UltimateRaids or ChaoticAllianceRaid
                || highEndCategories.Contains(row.ContentUICategory.RowId);
            duties.Add(new PlanDuty(row.RowId, instance, KindOf(row.ContentType.RowId, partyCount), name, highEnd));
        }

        return PlanDuties.From(duties);
    }
}
