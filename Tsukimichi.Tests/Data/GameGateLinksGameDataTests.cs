using Lumina.Excel;
using Tsukimichi.Core.Storage;
using Sheets = Lumina.Excel.Sheets;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The unlock links and accept conditions of <c>curated/game_gates.json</c> (feature plan v7 C3) against the installed
/// game: each link list is what its <c>sources</c> give when derived again, and every accept condition of a live quest
/// that is no quest is stood for by a gate, so none reads as a silent "accept condition not checked" any more.
/// </summary>
public sealed class GameGateLinksGameDataTests(GameDataFixture game) : IClassFixture<GameDataFixture>
{
    [GameDataFact]
    public void Every_unlock_link_gate_lists_exactly_the_links_its_sources_give()
    {
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var problems = new List<string>();
        var checkedGates = 0;
        foreach (var (rowId, gate) in curated.GameGates)
        {
            if (gate.UnlockLinks is not { } set)
            {
                continue;
            }

            checkedGates++;
            var derived = new SortedSet<uint>();
            foreach (var source in set.Sources)
            {
                var hash = source.IndexOf('#', StringComparison.Ordinal);
                var (sheet, row) = (source[..hash], uint.Parse(source[(hash + 1)..], System.Globalization.CultureInfo.InvariantCulture));
                switch (sheet)
                {
                    case "QuestAcceptAdditionCondition" when row == rowId:
                        derived.UnionWith(AcceptConditionsThatAreNoQuest(row));
                        break;
                    case "Quest" when row == rowId:
                        derived.Add(game.Game.GetExcelSheet<Sheets.Quest>()!.GetRow(row).Header);
                        break;
                    case "Action":
                        var action = game.Game.GetExcelSheet<Sheets.Action>()!.GetRow(row);
                        derived.Add(action.UnlockLink.RowId);
                        break;
                    default:
                        problems.Add($"{rowId}: source {source} is no accept row or Quest row of the quest itself nor an action");
                        break;
                }
            }

            if (!derived.SequenceEqual(set.All))
            {
                problems.Add($"{rowId}: lists [{string.Join(", ", set.All)}], sources give [{string.Join(", ", derived)}]");
            }
        }

        Assert.True(checkedGates >= 20, $"only {checkedGates} unlock-link gates");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [GameDataFact]
    public void Every_accept_condition_that_is_no_quest_has_its_gate()
    {
        var catalog = game.Bundle.Catalog;
        var left = catalog.All
            .Where(q => !q.IsRemoved && catalog.UncheckedAcceptConditions(q).Length > 0)
            .Select(q => $"{q.RowId} {q.Name}: [{string.Join(", ", catalog.UncheckedAcceptConditions(q))}]")
            .ToList();
        Assert.True(left.Count == 0, "accept conditions no gate stands for (add one to curated/game_gates.json): " + string.Join("; ", left));
    }

    [GameDataFact]
    public void The_blue_magic_links_are_the_spells_of_the_blue_mage()
    {
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var actions = game.Game.GetExcelSheet<Sheets.Action>()!;
        foreach (var (rowId, gate) in curated.GameGates.Where(kv => kv.Value.UnlockLinks?.Sources.Any(s => s.StartsWith("Action#", StringComparison.Ordinal)) == true))
        {
            var action = actions.GetRow(uint.Parse(gate.UnlockLinks!.Sources[0]["Action#".Length..], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(36u, action.ClassJob.RowId);
            Assert.Contains(action.Name.ExtractText(), gate.Gate, StringComparison.Ordinal);
            Assert.True(rowId > 0);
        }
    }

    /// <summary>
    /// What the <c>Quest#row</c> source reads: a quest's <c>Header</c> is the unlock link the quest waits for. Every blue
    /// magic quest names its spell's <c>UnlockLink</c> there, and the Palace of the Dead quests name the link the floor
    /// clears set, as the scripts that check them call it: <c>REWARD_DD1_50F_COMP</c> (320) in the scripts of Knocking
    /// on Heaven's Door, Delve into Myth and Pilgrimage of Light, <c>DEEP_DUNGEON1_REWARD_100F</c> (327) in the
    /// dungeon's reward talk.
    /// </summary>
    [GameDataFact]
    public void A_quests_header_is_the_unlock_link_it_waits_for()
    {
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var quests = game.Game.GetExcelSheet<Sheets.Quest>()!;
        var actions = game.Game.GetExcelSheet<Sheets.Action>()!;
        var blue = curated.GameGates.Where(kv => kv.Value.UnlockLinks?.Sources.Any(s => s.StartsWith("Action#", StringComparison.Ordinal)) == true).ToList();
        Assert.True(blue.Count >= 15, $"only {blue.Count} blue magic gates");
        foreach (var (rowId, gate) in blue)
        {
            var action = actions.GetRow(uint.Parse(gate.UnlockLinks!.Sources[0]["Action#".Length..], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(action.UnlockLink.RowId, (uint)quests.GetRow(rowId).Header);
        }

        uint Param(uint questRowId, string name) => quests.GetRow(questRowId).QuestParams.Single(p => p.ScriptInstruction.ExtractText() == name).ScriptArg;
        foreach (var floor50 in new uint[] { 68667, 70199, 70941 })
        {
            Assert.Equal(320u, Param(floor50, "REWARD_DD1_50F_COMP"));
        }

        var rewardTalk = game.Game.GetExcelSheet<Sheets.CustomTalk>()!.Single(t => t.Name.ExtractText().StartsWith("CtsDdd1Reward", StringComparison.Ordinal));
        Assert.Contains(rewardTalk.Script, s => s.ScriptInstruction.ExtractText() == "DEEP_DUNGEON1_REWARD_100F" && s.ScriptArg == 327);
        Assert.Equal(320, (int)quests.GetRow(67093).Header);
        Assert.Equal(327, (int)quests.GetRow(67924).Header);
    }

    /// <summary>The values of the quest's <c>QuestAcceptAdditionCondition</c> row below 65536 (the unlock links), read raw.</summary>
    private IEnumerable<uint> AcceptConditionsThatAreNoQuest(uint questRowId)
    {
        var sheet = game.Game.Excel.GetSheet<RawRow>(Lumina.Data.Language.None, "QuestAcceptAdditionCondition");
        if (!sheet.HasRow(questRowId))
        {
            yield break;
        }

        var row = sheet.GetRow(questRowId);
        for (var i = 0; i < sheet.Columns.Count; i++)
        {
            if (sheet.Columns[i].Type == Lumina.Data.Structs.Excel.ExcelColumnDataType.UInt32 && row.ReadColumn(i) is uint value && value is > 0 and < 65536)
            {
                yield return value;
            }
        }
    }
}
