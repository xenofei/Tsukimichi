using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Rewards;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The missing icons of UI-5a over the installed game: every feature row wears a game icon that exists, A Pup No
/// Longer's PvP instance is named and drawn, and the EXP and gil icons are the game's own.
/// </summary>
public sealed class FeatureIconDataTests(UnlockIndexFixture fixture, ITestOutputHelper output) : IClassFixture<UnlockIndexFixture>
{
    private const uint PupMaelstrom = 66640;
    private const uint PupTwinAdder = 66641;
    private const uint PupImmortalFlames = 66642;

    private QuestUnlocks Index => fixture.Unlocks;

    [GameDataFact]
    public void Every_feature_art_row_has_an_icon_the_game_draws()
    {
        var icons = fixture.Links.FeatureIcons;
        foreach (var art in FeatureArt.All())
        {
            var icon = icons.Icon(art);
            output.WriteLine($"{art.Sheet} {art.Row}: icon {icon}");
            Assert.True(icon != 0, $"{art.Sheet} {art.Row} has no icon");
            Assert.True(fixture.Game.FileExists(RewardArtIndex.IconPath(icon)), $"{art.Sheet} {art.Row} icon {icon} is not in the game");
        }

        // The rows read as named: PvP's Duty Finder tile, the Sightseeing Log menu, a striking dummy.
        var excel = fixture.Game.Excel;
        Assert.Equal("PvP", excel.GetSheet<ContentType>(Language.English).GetRow(FeatureArt.ContentPvp).Name.ExtractText());
        Assert.Equal("The Hunt", excel.GetSheet<ContentType>(Language.English).GetRow(FeatureArt.ContentTheHunt).Name.ExtractText());
        Assert.Equal("Sightseeing Log", excel.GetSheet<MainCommand>(Language.English).GetRow(FeatureArt.MenuSightseeingLog).Name.ExtractText());
        Assert.Contains("Striking Dummy", excel.GetSheet<Item>(Language.English).GetRow(FeatureArt.ItemStrikingDummy).Name.ExtractText(), StringComparison.Ordinal);
        Assert.Equal("Faux Leaf", excel.GetSheet<Item>(Language.English).GetRow(FeatureArt.ItemFauxLeaf).Name.ExtractText());
    }

    [GameDataFact]
    public void No_feature_row_draws_the_stand_in_any_more()
    {
        var blank = new List<string>();
        var features = 0;
        foreach (var quest in fixture.Catalog.All)
        {
            foreach (var entry in Index.For(quest.RowId))
            {
                if (entry.Target is not (UnlockTarget.System or UnlockTarget.FieldOperation))
                {
                    continue;
                }

                features++;
                if (entry.Icon == 0)
                {
                    blank.Add($"{quest.RowId} {quest.Name}: {entry.Name}");
                }
            }
        }

        output.WriteLine($"feature rows: {features}, without an icon: {blank.Count}");
        Assert.True(features > 100, $"only {features} feature rows");
        Assert.True(blank.Count == 0, "feature rows without an icon:\n" + string.Join('\n', blank));
    }

    [GameDataFact]
    public void A_Pup_No_Longer_opens_PvP_with_its_tile()
    {
        var pvpIcon = fixture.Game.Excel.GetSheet<ContentType>(Language.English).GetRow(FeatureArt.ContentPvp).Icon;
        foreach (var rowId in new[] { PupMaelstrom, PupTwinAdder, PupImmortalFlames })
        {
            var quest = fixture.Catalog.GetByRowId(rowId)!;
            var instance = Assert.Single(quest.Rewards, r => r.Kind == RewardKind.Instance);
            Assert.Equal("PvP", instance.Name);
            Assert.Equal(pvpIcon, instance.Icon);

            // The instance and the curated PvP feature are one row, as sure as the curated file, with its note.
            var pvp = Assert.Single(Index.For(rowId), e => e.Name == "PvP");
            Assert.Equal(UnlockGroup.Duty, pvp.Group);
            Assert.Equal(pvpIcon, pvp.Icon);
            Assert.Equal(UnlockSource.Curated, pvp.Source);
            Assert.NotNull(pvp.Note);
        }
    }

    [GameDataFact]
    public void The_EXP_and_gil_icons_are_the_games_own()
    {
        Assert.Equal(RewardIcons.Gil, (uint)fixture.Game.Excel.GetSheet<Item>(Language.English).GetRow(RewardIcons.GilItem).Icon);
        Assert.True(fixture.Game.FileExists(RewardArtIndex.IconPath(RewardIcons.Exp)));
        Assert.True(fixture.Game.FileExists(RewardArtIndex.IconPath(RewardIcons.Gil)));
    }
}
