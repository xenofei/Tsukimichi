using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Companions;

namespace Tsukimichi.GameData;

/// <summary>
/// Builds the <see cref="DutyRunIndex"/> "Run with AutoDuty" reads: every named ContentFinderCondition row with its
/// TerritoryType (the id AutoDuty's gates take), the InstanceContent row it links and whether Duty Support or Trust
/// lists it. Duty Support follows AutoDuty's own rule (erdelf/AutoDuty <c>Helpers/ContentHelper.cs</c>): a DawnContent
/// row names the duty and its DawnContentParticipable row has more than one party choice. Trust needs a DawnContent row
/// and a duty from Shadowbringers (ExVersion 3) on; AutoDuty reads an unnamed DawnContent column for it, which this
/// leaves alone so a renamed column cannot stop the plugin loading. Each duty wears its icon through
/// <see cref="Core.Unlocks.DutyArt"/>'s chain (<see cref="DutyArtReader"/>). Each also carries the facts "how you'll clear
/// it" reads (feature plan v7 C7): its level and item level, whether the Duty Finder matches it, the players it seats,
/// and the roulettes that draw from it; the index carries the Duty Roulettes themselves for the Duties board (N4).
/// Standalone (takes an <see cref="ExcelModule"/>) so tests run it against game data without Dalamud.
/// </summary>
public static class DutyRunSheets
{
    /// <summary>
    /// The <c>ContentRoulette</c> row behind each roulette column of <c>ContentFinderCondition</c>. The sheets do not
    /// link the two, so the map is by row id; <c>DutyRunSheetsTests</c> checks each row's name against the game.
    /// </summary>
    public static readonly IReadOnlyDictionary<uint, DutyRoulettes> RouletteRows = new Dictionary<uint, DutyRoulettes>
    {
        [1] = DutyRoulettes.Leveling,
        [2] = DutyRoulettes.HighLevel,
        [3] = DutyRoulettes.MainScenario,
        [4] = DutyRoulettes.Guildhests,
        [5] = DutyRoulettes.Expert,
        [6] = DutyRoulettes.Trials,
        [8] = DutyRoulettes.LevelCap,
        [9] = DutyRoulettes.Mentor,
        [15] = DutyRoulettes.AllianceRaids,
        [17] = DutyRoulettes.NormalRaids,
    };

    /// <summary><c>ContentFinderCondition.ContentLinkType</c> value whose <c>Content</c> is an InstanceContent row.</summary>
    private const byte InstanceContentLink = 1;

    /// <summary>Shadowbringers' ExVersion row: Trust's first expansion.</summary>
    public const uint TrustFirstExVersion = 3;

    /// <summary>
    /// Reads the index in <paramref name="language"/>, the client's. ContentFinderCondition exists only per language, so
    /// there is no default: 1.6 to 1.10 built it with <see cref="Language.None"/>, Lumina refused, and the Duties
    /// section stayed hidden in game. <see cref="Language.None"/> is refused here, before any sheet is read.
    /// </summary>
    public static DutyRunIndex Build(ExcelModule excel, Language language)
    {
        ArgumentNullException.ThrowIfNull(excel);
        if (language == Language.None)
        {
            throw new ArgumentException("The duty index is read per language: pass the client's.", nameof(language));
        }

        // DawnContentParticipable: one page per DawnContent row, one subrow per party choice.
        var choices = new Dictionary<uint, int>();
        foreach (var page in excel.GetSubrowSheet<DawnContentParticipable>(language))
        {
            choices[page.RowId] = page.Count;
        }

        var dawnByCondition = new Dictionary<uint, uint>();
        foreach (var dawn in excel.GetSheet<DawnContent>(language))
        {
            var condition = dawn.Content.RowId;
            if (condition != 0)
            {
                dawnByCondition.TryAdd(condition, dawn.RowId);
            }
        }

        var duties = new List<DutyRunInfo>();
        var art = DutyArtReader.Shared.Read(excel, language);
        foreach (var row in excel.GetSheet<ContentFinderCondition>(language))
        {
            var name = row.Name.ExtractText().Trim();
            var territory = row.TerritoryType.RowId;
            if (name.Length == 0 || territory == 0)
            {
                continue;
            }

            var support = false;
            var trust = false;
            if (dawnByCondition.TryGetValue(row.RowId, out var dawnRow))
            {
                support = choices.GetValueOrDefault(dawnRow) > 1;
                trust = (row.TerritoryType.ValueNullable?.ExVersion.RowId ?? 0) >= TrustFirstExVersion;
            }

            var instance = row.ContentLinkType == InstanceContentLink ? row.Content.RowId : 0u;
            var members = row.ContentMemberType.ValueNullable;
            duties.Add(new DutyRunInfo(row.RowId, instance, territory, row.ContentType.RowId, name, support, trust, DutyArtReader.Icon(in row, in art))
            {
                LevelRequired = row.ClassJobLevelRequired,
                ItemLevelRequired = row.ItemLevelRequired,
                InDutyFinder = row.IsInDutyFinder,
                Players = members is { } m ? m.MembersPerParty * Math.Max(1, (int)m.PartyCount) : 0,
                Roulettes = RoulettesOf(in row),
                SortKey = row.SortKey,
            });
        }

        return DutyRunIndex.From(duties, Roulettes(excel, language));
    }

    /// <summary>The roulette columns a duty is ticked in.</summary>
    public static DutyRoulettes RoulettesOf(in ContentFinderCondition row)
    {
        var flags = DutyRoulettes.None;
        if (row.LevelingRoulette)
        {
            flags |= DutyRoulettes.Leveling;
        }

        if (row.HighLevelRoulette)
        {
            flags |= DutyRoulettes.HighLevel;
        }

        if (row.MSQRoulette)
        {
            flags |= DutyRoulettes.MainScenario;
        }

        if (row.GuildHestRoulette)
        {
            flags |= DutyRoulettes.Guildhests;
        }

        if (row.ExpertRoulette)
        {
            flags |= DutyRoulettes.Expert;
        }

        if (row.TrialRoulette)
        {
            flags |= DutyRoulettes.Trials;
        }

        if (row.LevelCapRoulette)
        {
            flags |= DutyRoulettes.LevelCap;
        }

        if (row.MentorRoulette)
        {
            flags |= DutyRoulettes.Mentor;
        }

        if (row.AllianceRoulette)
        {
            flags |= DutyRoulettes.AllianceRaids;
        }

        if (row.NormalRaidRoulette)
        {
            flags |= DutyRoulettes.NormalRaids;
        }

        return flags;
    }

    /// <summary>
    /// The Duty Roulettes of <see cref="RouletteRows"/> the Duty Finder lists, with what opens them
    /// (<c>ContentRouletteOpenRule.HasDutyRequirements</c>: every duty), their level and expansion.
    /// </summary>
    private static List<RouletteInfo> Roulettes(ExcelModule excel, Language language)
    {
        var list = new List<RouletteInfo>(RouletteRows.Count);
        var sheet = excel.GetSheet<ContentRoulette>(language);
        foreach (var (rowId, flag) in RouletteRows)
        {
            if (sheet.GetRowOrDefault(rowId) is not { } row || !row.IsInDutyFinder || row.IsPvP)
            {
                continue;
            }

            var name = row.Name.ExtractText().Trim();
            if (name.Length == 0)
            {
                continue;
            }

            list.Add(new RouletteInfo(
                rowId,
                name,
                flag,
                row.OpenRule.ValueNullable?.HasDutyRequirements == true,
                row.RequiredLevel,
                (byte)Math.Min(row.RequiredExVersion.RowId, byte.MaxValue),
                row.ItemLevelRequired,
                row.SortKey)
            {
                ShortName = row.Category.ExtractText().Trim(),
            });
        }

        return list;
    }
}
