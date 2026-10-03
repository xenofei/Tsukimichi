using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Ui;
using Tsukimichi.GameData;
using Xunit.Abstractions;
using LuminaGameData = Lumina.GameData;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The icons of UI-5d over the installed game: every icon <see cref="PaneIcons"/> can answer exists in the game, and the
/// sheet rows it names are the ones it means (the Dungeons tile, the Disciples of the Hand, the Maelstrom's insignia).
/// </summary>
public sealed class PaneIconDataTests(ITestOutputHelper output) : IDisposable
{
    private readonly Lazy<LuminaGameData> game = new(static () => new LuminaGameData(
        Environment.GetEnvironmentVariable(GameDataFactAttribute.EnvVar)!,
        new LuminaOptions { DefaultExcelLanguage = Language.English, PanicOnSheetChecksumMismatch = false }));

    public void Dispose()
    {
        if (game.IsValueCreated)
        {
            game.Value.Dispose();
        }
    }

    private void AssertDrawn(uint icon, string what)
    {
        output.WriteLine($"{what}: {icon:D6}");
        Assert.True(icon != 0, $"{what} has no icon");
        Assert.True(game.Value.FileExists(RewardArtIndex.IconPath(icon)), $"{what}: icon {icon} is not in the game");
    }

    [GameDataFact]
    public void Every_mapped_icon_exists_in_the_game()
    {
        var sheets = PaneIconSheets.Build(game.Value.Excel, message => Assert.Fail(message));
        foreach (var icon in PaneIcons.FixedIcons)
        {
            AssertDrawn(icon, "fixed");
        }

        foreach (var row in PaneIcons.ContentTypeRows)
        {
            AssertDrawn(sheets.ContentTypeIcon(row), $"ContentType {row}");
        }

        foreach (var family in Enum.GetValues<JobFamily>().Where(f => f != JobFamily.None))
        {
            AssertDrawn(PaneIcons.Family(family, sheets), $"family {family}");
        }

        foreach (var role in Enum.GetValues<JobRole>())
        {
            AssertDrawn(PaneIcons.Role(role), $"role {role}");
        }

        foreach (var kind in UnlockKinds.All.Where(k => k != UnlockKind.Other))
        {
            AssertDrawn(PaneIcons.UnlockKind(kind, sheets), $"unlock kind {kind}");
        }

        for (var expansion = 0; expansion < Core.Evaluation.Expansions.Count; expansion++)
        {
            AssertDrawn(PaneIcons.Expansion((byte)expansion), $"expansion {expansion}");
        }

        // Every allied society and every rank of every Grand Company.
        var tribes = sheets.Tribes.ToList();
        Assert.True(tribes.Count >= 20, $"only {tribes.Count} societies");
        foreach (var tribe in tribes)
        {
            AssertDrawn(PaneIcons.Tribe(tribe, sheets), $"tribe {tribe}");
        }

        var ranks = sheets.Ranks.ToList();
        Assert.True(ranks.Count >= 3 * 19, $"only {ranks.Count} ranks");
        for (byte company = 1; company <= 3; company++)
        {
            for (byte rank = 0; rank <= 20; rank++)
            {
                AssertDrawn(PaneIcons.GrandCompanyRank(company, rank, sheets), $"Grand Company {company} rank {rank}");
            }
        }
    }

    [GameDataFact]
    public void The_sheet_rows_are_the_ones_named()
    {
        var excel = game.Value.Excel;
        var content = excel.GetSheet<ContentType>(Language.English);
        Assert.Equal("Dungeons", content.GetRow(PaneIcons.DungeonsContent).Name.ExtractText());
        Assert.Equal("Trials", content.GetRow(PaneIcons.TrialsContent).Name.ExtractText());
        Assert.Equal("Raids", content.GetRow(PaneIcons.RaidsContent).Name.ExtractText());
        Assert.Equal("Eureka", content.GetRow(PaneIcons.EurekaContent).Name.ExtractText());
        Assert.Equal("Grand Company", content.GetRow(NodeIcons.GrandCompanyContent).Name.ExtractText());
        Assert.Equal("Society Quests", content.GetRow(NodeIcons.SocietyContent).Name.ExtractText());
        Assert.Equal("Disciples of the Land", content.GetRow(NodeIcons.LandContent).Name.ExtractText());
        Assert.Equal("Disciples of the Hand", content.GetRow(NodeIcons.HandContent).Name.ExtractText());

        var companies = excel.GetSheet<GrandCompany>(Language.English);
        Assert.Equal("Maelstrom", companies.GetRow(1).Name.ExtractText());
        Assert.Equal("Order of the Twin Adder", companies.GetRow(2).Name.ExtractText());
        Assert.Equal("Immortal Flames", companies.GetRow(3).Name.ExtractText());

        // The insignia follow the company: the Maelstrom's, the Adders' and the Flames' Second Lieutenant.
        var sheets = PaneIconSheets.Build(excel);
        var lieutenant = excel.GetSheet<GrandCompanyRank>().GetRow(11);
        Assert.Equal((uint)lieutenant.IconMaelstrom, sheets.GrandCompanyRankIcon(1, 11));
        Assert.Equal((uint)lieutenant.IconSerpents, sheets.GrandCompanyRankIcon(2, 11));
        Assert.Equal((uint)lieutenant.IconFlames, sheets.GrandCompanyRankIcon(3, 11));

        // The Amalj'aa's coloured emblem, and an achievement's own icon.
        Assert.Equal(excel.GetSheet<BeastTribe>().GetRow(1).Icon, sheets.TribeIcon(1));
        var achievement = excel.GetSheet<Achievement>().First(a => a.Icon != 0);
        Assert.Equal((uint)achievement.Icon, sheets.AchievementIcon(achievement.RowId));
        AssertDrawn(sheets.AchievementIcon(achievement.RowId), $"achievement {achievement.RowId}");
    }
}
