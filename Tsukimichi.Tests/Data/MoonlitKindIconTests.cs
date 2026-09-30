using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Data;

/// <summary>The Moonlit kinds list's MainCommand rows name the menus they stand for, and each has an icon.</summary>
public class MoonlitKindIconTests(GameDataFixture fixture) : IClassFixture<GameDataFixture>
{
    [GameDataFact]
    public void Every_main_command_row_is_the_menu_it_names()
    {
        var sheet = fixture.Game.Excel.GetSheet<MainCommand>(Language.English);
        var expected = new Dictionary<uint, string>
        {
            [MoonlitKindIcons.MainCommandActionsAndTraits] = "Actions & Traits",
            [MoonlitKindIcons.MainCommandAchievements] = "Achievements",
            [MoonlitKindIcons.MainCommandInventory] = "Inventory",
            [MoonlitKindIcons.MainCommandEmotes] = "Emotes",
            [MoonlitKindIcons.MainCommandArmouryChest] = "Armoury Chest",
            [MoonlitKindIcons.MainCommandDutyFinder] = "Duty Finder",
            [MoonlitKindIcons.MainCommandCompanion] = "Companion",
            [MoonlitKindIcons.MainCommandMountGuide] = "Mount Guide",
            [MoonlitKindIcons.MainCommandMinionGuide] = "Minion Guide",
            [MoonlitKindIcons.MainCommandGoldSaucer] = "Gold Saucer",
            [MoonlitKindIcons.MainCommandAetherCurrents] = "Aether Currents",
            [MoonlitKindIcons.MainCommandOrchestrionList] = "Orchestrion List",
            [MoonlitKindIcons.MainCommandBlueMagicSpellbook] = "Blue Magic Spellbook",
            [MoonlitKindIcons.MainCommandFashionAccessories] = "Fashion Accessories",
        };

        foreach (var (rowId, name) in expected)
        {
            var row = sheet.GetRowOrDefault(rowId);
            Assert.NotNull(row);
            Assert.Equal(name, row.Value.Name.ExtractText());
            Assert.True(row.Value.Icon > 0, $"{name} has no icon");
        }
    }
}
