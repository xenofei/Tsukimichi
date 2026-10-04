using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Rewards;
using Tsukimichi.Core.Ui;
using Tsukimichi.GameData;
using Xunit.Abstractions;
using LuminaGameData = Lumina.GameData;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The icons of UI-5e over the installed game: every icon a button, the Route window's header or a requirement line can
/// wear exists in the game, and each fixed id is the one the sheet row it stands for carries (Sprint's tile is
/// GeneralAction 4's, the Duty Finder's MainCommand 33's, gil Item 1's), so a patch that renumbers one fails here.
/// </summary>
public sealed class ActionIconDataTests(ITestOutputHelper output) : IDisposable
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
    public void Every_action_icon_exists_in_the_game()
    {
        foreach (var icon in ActionIcons.FixedIcons)
        {
            AssertDrawn(icon, "fixed");
        }

        foreach (byte family in new byte[] { 0, 1, 3, 8, 10 })
        {
            AssertDrawn(ActionIcons.QuestMarker(family), $"marker of EventIconType {family}");
        }
    }

    [GameDataFact]
    public void The_fixed_ids_are_the_sheet_rows_they_stand_for()
    {
        var excel = game.Value.Excel;
        var general = excel.GetSheet<GeneralAction>(Language.English);
        Assert.Equal("Sprint", general.GetRow(4).Name.ExtractText());
        Assert.Equal(ActionIcons.Walk, (uint)general.GetRow(4).Icon);
        Assert.Equal("Return", general.GetRow(8).Name.ExtractText());
        Assert.Equal(ActionIcons.Return, (uint)general.GetRow(8).Icon);
        Assert.Equal("Mount Roulette", general.GetRow(9).Name.ExtractText());
        Assert.Equal(ActionIcons.Mount, (uint)general.GetRow(9).Icon);
        Assert.Equal("Flying Mount Roulette", general.GetRow(24).Name.ExtractText());
        Assert.Equal(ActionIcons.Fly, (uint)general.GetRow(24).Icon);

        var commands = excel.GetSheet<MainCommand>(Language.English);
        Assert.Equal(ActionIcons.Map, (uint)commands.GetRow(16).Icon);
        Assert.Equal(ActionIcons.DutyFinder, (uint)commands.GetRow(33).Icon);
        Assert.Contains(commands, command => (uint)command.Icon == ActionIcons.CraftingLog && command.Name.ExtractText() == "Crafting Log");

        var symbols = excel.GetSheet<MapSymbol>();
        Assert.Equal(ActionIcons.Teleport, (uint)symbols.GetRow(1).Icon);
        Assert.Equal(ActionIcons.Aethernet, (uint)symbols.GetRow(2).Icon);
        Assert.Equal(ActionIcons.Ferry, (uint)symbols.GetRow(15).Icon);

        Assert.Equal(RewardIcons.Gil, (uint)excel.GetSheet<Item>().GetRow(RewardIcons.GilItem).Icon);
    }

    [GameDataFact]
    public void Every_job_and_mount_a_requirement_names_has_its_icon()
    {
        var excel = game.Value.Excel;
        var jobs = 0;
        foreach (var job in excel.GetSheet<ClassJob>())
        {
            if (job.RowId == 0 || job.Abbreviation.ExtractText().Length == 0)
            {
                continue;
            }

            AssertDrawn(ActionIcons.Job(job.RowId), $"job {job.Abbreviation.ExtractText()}");
            jobs++;
        }

        Assert.True(jobs >= 40, $"only {jobs} jobs");
        Assert.Equal(62136u, ActionIcons.Job(36));

        var sheets = PaneIconSheets.Build(excel, message => Assert.Fail(message));
        var mounts = excel.GetSheet<Mount>().Where(m => m.Icon != 0).Take(50).ToList();
        Assert.NotEmpty(mounts);
        foreach (var mount in mounts)
        {
            Assert.Equal((uint)mount.Icon, sheets.MountIcon(mount.RowId));
            AssertDrawn(sheets.MountIcon(mount.RowId), $"mount {mount.RowId}");
        }
    }
}
