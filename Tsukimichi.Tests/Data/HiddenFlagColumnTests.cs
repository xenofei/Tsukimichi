using Lumina.Data.Structs.Excel;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Pins the sheet column the mapper reads as <c>QuestRecord.IsHidden</c>. Lumina names it <c>Unknown12</c> today and
/// renumbers unknown columns whenever EXDSchema names one, so the column index is asserted against the raw sheet
/// header, and the row counts against docs/data/unlisted-report.md section 1 (98 unlisted + 2 listed).
/// </summary>
public class HiddenFlagColumnTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    /// <summary>
    /// The bool column between <c>HideOfferIcon</c> and <c>HideInScenarioGuide</c> in the Quest sheet header: column
    /// 1651 of 1653, a packed bool (bit 5 of the byte at offset 2792) on game 2026.09.15.0000.0000.
    /// </summary>
    public const int HiddenColumn = 1651;

    [GameDataFact]
    public void Hidden_flag_is_the_pinned_bool_column_and_marks_the_reported_rows()
    {
        var excel = fixture.Game.Excel;
        var typed = excel.GetSheet<Quest>();
        var raw = excel.GetSheet<RawRow>(null, "Quest");
        Assert.Equal(typed.Count, raw.Count);

        // Which bool columns (plain or bit-packed) agree with Unknown12 on every named row: exactly one, the pinned one.
        var candidates = new List<int>();
        for (var column = 0; column < raw.Columns.Count; column++)
        {
            if (raw.Columns[column].Type is not (ExcelColumnDataType.Bool
                or ExcelColumnDataType.PackedBool0 or ExcelColumnDataType.PackedBool1 or ExcelColumnDataType.PackedBool2 or ExcelColumnDataType.PackedBool3
                or ExcelColumnDataType.PackedBool4 or ExcelColumnDataType.PackedBool5 or ExcelColumnDataType.PackedBool6 or ExcelColumnDataType.PackedBool7))
            {
                continue;
            }

            var agrees = true;
            foreach (var row in raw)
            {
                var quest = typed.GetRowOrDefault(row.RowId);
                if (quest is null || quest.Value.Name.IsEmpty)
                {
                    continue;
                }

                if (row.ReadColumn(column) is not bool value || value != quest.Value.Unknown12)
                {
                    agrees = false;
                    break;
                }
            }

            if (agrees)
            {
                candidates.Add(column);
            }
        }

        output.WriteLine($"bool columns equal to Unknown12 on every named row: [{string.Join(", ", candidates.Select(c => $"{c} ({raw.Columns[c].Type} @{raw.Columns[c].Offset})"))}] of {raw.Columns.Count} columns");
        Assert.Equal([HiddenColumn], candidates);

        var named = typed.Where(q => !q.Name.IsEmpty).ToList();
        var hidden = named.Where(q => q.Unknown12).ToList();
        Assert.Equal(ExpectedCounts.HiddenRows, hidden.Count);
        Assert.Equal(ExpectedCounts.HiddenUnlistedRows, hidden.Count(q => q.JournalGenre.RowId == 0));
        Assert.Equal([66033u, 66034u], hidden.Where(q => q.JournalGenre.RowId != 0).Select(q => q.RowId).OrderBy(id => id));

        // Every hidden row also sits on the placeholder issuer except The Favors of the House's inverse: the flag is a
        // strict subset of the placeholder rows, and the mapper carries it as IsHidden.
        Assert.All(hidden, q => Assert.Equal(GameData.JournalRefiler.PlaceholderIssuer, q.IssuerStart.RowId));
        var legacy = GameData.CatalogMapper.Map(excel, Lumina.Data.Language.English, filing: Core.Model.JournalFiling.Legacy).Catalog;
        Assert.Equal(hidden.Select(q => q.RowId).OrderBy(id => id), legacy.All.Where(q => q.IsHidden).Select(q => q.RowId).OrderBy(id => id));
        Assert.Equal(ExpectedCounts.RetiredByRule1, legacy.All.Count(q => q.IsUnlisted && GameData.JournalRefiler.IsRetiredRow(q)));
    }
}
