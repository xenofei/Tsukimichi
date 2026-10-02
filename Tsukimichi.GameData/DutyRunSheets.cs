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
/// leaves alone so a renamed column cannot stop the plugin loading. Standalone (takes an <see cref="ExcelModule"/>) so
/// tests run it against game data without Dalamud.
/// </summary>
public static class DutyRunSheets
{
    /// <summary><c>ContentFinderCondition.ContentLinkType</c> value whose <c>Content</c> is an InstanceContent row.</summary>
    private const byte InstanceContentLink = 1;

    /// <summary>Shadowbringers' ExVersion row: Trust's first expansion.</summary>
    public const uint TrustFirstExVersion = 3;

    public static DutyRunIndex Build(ExcelModule excel, Language language = Language.None)
    {
        ArgumentNullException.ThrowIfNull(excel);

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
            duties.Add(new DutyRunInfo(row.RowId, instance, territory, row.ContentType.RowId, name, support, trust));
        }

        return DutyRunIndex.From(duties);
    }
}
