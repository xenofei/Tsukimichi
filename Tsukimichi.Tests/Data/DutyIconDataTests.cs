using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The duty icon chain over the installed game (owner point 9, UI-5b): every duty a quest opens, every Duty Finder
/// entry, Moonlit's duty rewards and AutoDuty's duty list resolve to an icon the game draws; a duty with an emblem of
/// its own wears it; a nameless instance is named for its territory, and A Pup No Longer's stays PvP.
/// </summary>
public sealed class DutyIconDataTests(UnlockIndexFixture fixture, ITestOutputHelper output) : IClassFixture<UnlockIndexFixture>
{
    private const uint GreatHunt = 474;

    private Lumina.GameData Game => fixture.Game;

    private bool Drawable(uint icon) => icon != 0 && Game.FileExists(RewardArtIndex.IconPath(icon));

    private static bool IsDuty(UnlockEntry entry) => entry.Group == UnlockGroup.Duty;

    [GameDataFact]
    public void Every_duty_a_quest_opens_resolves_to_an_icon_the_game_draws()
    {
        var conditions = Game.Excel.GetSheet<ContentFinderCondition>(Language.English);
        var shared = DutyArtReader.Shared.Read(Game.Excel, Language.English);
        var rows = 0;
        var emblems = 0;
        var steps = new Dictionary<DutyArtStep, int>();
        var bad = new List<string>();
        foreach (var quest in fixture.Catalog.All)
        {
            foreach (var entry in fixture.Unlocks.IncludingRewards(quest.RowId))
            {
                if (!IsDuty(entry))
                {
                    continue;
                }

                rows++;
                if (!Drawable(entry.Icon))
                {
                    bad.Add($"{quest.RowId} {quest.Name}: {entry.Name} icon {entry.Icon}");
                }

                if (entry.TargetId != 0 && conditions.GetRowOrDefault(entry.TargetId) is { } row)
                {
                    var step = DutyArt.Resolve(DutyArtReader.Sources(in row), shared.PvpIcon, shared.DutyFinderIcon).Step;
                    steps[step] = steps.GetValueOrDefault(step) + 1;

                    // Before UI-5b the row read the category tile alone; a duty with an emblem wears that now.
                    if (step == DutyArtStep.Duty)
                    {
                        emblems++;
                        Assert.Equal(row.Icon, entry.Icon);
                    }
                }
            }
        }

        output.WriteLine($"duty rows: {rows}, without a drawable icon: {bad.Count}, wearing their own emblem: {emblems}; by step: {string.Join(", ", steps.OrderBy(s => s.Key).Select(s => $"{s.Key} {s.Value}"))}");
        Assert.True(rows > 300, $"only {rows} duty rows");
        Assert.True(bad.Count == 0, "duty rows without a drawable icon:\n" + string.Join('\n', bad));
    }

    [GameDataFact]
    public void Every_Duty_Finder_entry_and_instance_resolves_and_emblems_win()
    {
        var excel = Game.Excel;
        var icons = DutyArtReader.Read(excel, Language.English, message => output.WriteLine(message));
        var shared = DutyArtReader.Shared.Read(excel, Language.English);
        Assert.True(Drawable(shared.DutyFinderIcon), "the Duty Finder menu icon is not in the game");
        Assert.True(Drawable(shared.PvpIcon), "PvP's tile is not in the game");
        Assert.Equal(shared.DutyFinderIcon, icons.Fallback);

        var named = 0;
        var emblems = 0;
        var bad = new List<string>();
        foreach (var row in excel.GetSheet<ContentFinderCondition>(Language.English))
        {
            if (row.Name.IsEmpty)
            {
                continue;
            }

            named++;
            var icon = icons.For(row.RowId);
            if (!Drawable(icon))
            {
                bad.Add($"{row.RowId} {row.Name.ExtractText()}: {icon}");
            }

            if (row.Icon != 0)
            {
                emblems++;
                Assert.Equal(row.Icon, icon);
            }

            if (row.ContentLinkType == DutyArtReader.InstanceContentLink && row.Content.RowId != 0)
            {
                Assert.True(Drawable(icons.ForInstance(row.Content.RowId)), $"instance {row.Content.RowId} has no drawable icon");
            }
        }

        output.WriteLine($"named Duty Finder entries: {named}, with an emblem of their own: {emblems}, without a drawable icon: {bad.Count}");
        Assert.True(named > 800, $"only {named} named entries");
        Assert.True(emblems > 0, "no duty has an emblem of its own any more; the chain's first step is never taken");
        Assert.True(bad.Count == 0, "entries without a drawable icon:\n" + string.Join('\n', bad));

        // The Great Hunt wears its own crest, a 136 x 168 card drawn whole (IconFit), not the Trials tile.
        Assert.Equal(excel.GetSheet<ContentFinderCondition>(Language.English).GetRow(GreatHunt).Icon, icons.For(GreatHunt));
        Assert.NotEqual(excel.GetSheet<ContentType>(Language.English).GetRow(4).Icon, icons.For(GreatHunt));
    }

    [GameDataFact]
    public void Moonlit_duty_rewards_and_AutoDutys_duties_wear_a_drawable_icon()
    {
        var entries = RewardArtIndexTests.Entries();
        var index = RewardArtIndex.Build(Game.Excel, Language.English, entries, icon => Game.FileExists(RewardArtIndex.IconPath(icon)), message => output.WriteLine(message));
        var duties = entries.Where(e => e.Kind is RewardKind.DutyUnlock or RewardKind.Instance && e.RewardId != 0).ToList();
        var blank = duties.Where(e => !Drawable(index.Icon(e.Kind, e.RewardId))).Select(e => $"{e.QuestRowId} {e.Kind} {e.RewardId} {e.RewardName}").ToList();
        output.WriteLine($"Moonlit duty rewards: {duties.Count}, without a drawable icon: {blank.Count}");
        Assert.True(duties.Count > 300, $"only {duties.Count} duty rewards");
        Assert.True(blank.Count == 0, "duty rewards without a drawable icon:\n" + string.Join('\n', blank));

        var runs = DutyRunSheets.Build(Game.Excel, Language.English);
        var all = Game.Excel.GetSheet<ContentFinderCondition>(Language.English).Select(r => runs.ByCondition(r.RowId)).OfType<Core.Companions.DutyRunInfo>().ToList();
        var noIcon = all.Where(d => !Drawable(d.Icon)).Select(d => $"{d.ContentFinderConditionId} {d.Name}").ToList();
        output.WriteLine($"AutoDuty duties: {all.Count}, without a drawable icon: {noIcon.Count}");
        Assert.True(all.Count > 500, $"only {all.Count} AutoDuty duties");
        Assert.True(noIcon.Count == 0, "AutoDuty duties without a drawable icon:\n" + string.Join('\n', noIcon));
    }

    [GameDataFact]
    public void Nameless_instances_are_named_for_their_territory_and_the_Pup_stays_PvP()
    {
        var excel = Game.Excel;
        var names = fixture.GameFixture.Bundle.Names.Duties;

        // Every duty a quest requires has a name for its blocker line.
        var unnamed = fixture.Catalog.All.SelectMany(q => q.InstanceContentRequired.Where(id => !names.ContainsKey(id)).Select(id => $"{q.RowId} {q.Name}: {id}")).ToList();
        Assert.True(unnamed.Count == 0, "required duties without a name:\n" + string.Join('\n', unnamed));

        // An instance only nameless entries link takes its territory's name, else PvP's for a PvP instance.
        var shared = DutyArtReader.Shared.Read(excel, Language.English);
        var namedInstances = excel.GetSheet<ContentFinderCondition>(Language.English)
            .Where(r => r.ContentLinkType == DutyArtReader.InstanceContentLink && !r.Name.IsEmpty)
            .Select(r => r.Content.RowId)
            .ToHashSet();
        var territoryNamed = 0;
        foreach (var row in excel.GetSheet<ContentFinderCondition>(Language.English))
        {
            if (row.ContentLinkType != DutyArtReader.InstanceContentLink || row.Content.RowId == 0 || !row.Name.IsEmpty || namedInstances.Contains(row.Content.RowId))
            {
                continue;
            }

            var name = DutyArtReader.Name(in row, in shared);
            if (name.Length == 0)
            {
                continue;
            }

            territoryNamed++;
            Assert.True(names.TryGetValue(row.Content.RowId, out var given), $"instance {row.Content.RowId} has no name");
            Assert.Equal(char.ToUpperInvariant(name[0]) + name[1..], given);
        }

        // None in the 2026.10 client: every nameless entry's instance has a named entry too, or neither territory nor
        // PvP type. The step stands for the next patch's nameless duty.
        output.WriteLine($"instances named for their territory or PvP: {territoryNamed}");

        // A Pup No Longer's solo instance: no name, no territory, so PvP, with PvP's tile (1.14.0's fallback kept).
        var pup = fixture.Catalog.GetByRowId(66640)!;
        var instance = Assert.Single(pup.Rewards, r => r.Kind == RewardKind.Instance);
        Assert.Equal(shared.PvpName, instance.Name);
        Assert.Equal(shared.PvpIcon, instance.Icon);
    }
}
