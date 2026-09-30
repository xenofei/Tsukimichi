using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The curated duty unlocks (<c>curated/duty_unlocks.json</c>) against the quest scripts: every entry is held to what
/// its quest's script names (<see cref="QuestScriptDuties"/>), and every live quest whose script announces a curated
/// duty must be one of that duty's curated quests. This is the second source that 66476 Blood for Blood (the Stone
/// Vigil, really 66488) and Operation Archon (Castrum Meridianum, really Rock the Castrum) lacked.
/// </summary>
public sealed class DutyUnlockScriptTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    [Fact]
    public void A_script_reads_its_first_dungeon_and_content_start_as_primary_and_maps_them_to_duties()
    {
        var byInstance = new Dictionary<uint, IReadOnlyList<uint>>
        {
            [86] = [16],
            [20082] = [830],
            [30001] = [92],
        };

        var duty = QuestScriptDuties.Read(
            70058,
            [("INSTANCEDUNGEON0", 86), ("INSTANCEDUNGEON1", 20082), ("CONTENT_START", 86), ("UNLOCK_IMAGE_DUNGEON", 99), ("UNLOCK_ADD_NEW_CONTENT_TO_CF", 3702)],
            byInstance)!;

        Assert.Equal([16u], duty.PrimaryDuties);
        Assert.Equal([16u, 830u], duty.NamedDuties);
        Assert.True(duty.AddsToDutyFinder);
        Assert.True(duty.ShowsUnlockImage);
        Assert.True(duty.IsUnlockStep);

        // A quest battle's instance links no named CFC row: kept aside, not a duty.
        var battle = QuestScriptDuties.Read(66572, [("INSTANCEDUNGEON0", 20007), ("UNLOCK_ADD_NEW_CONTENT_TO_CF", 3702)], byInstance)!;
        Assert.Empty(battle.NamedDuties);
        Assert.Equal([20007u], battle.UnmappedInstances);

        // A script naming nothing duty-related yields nothing.
        Assert.Null(QuestScriptDuties.Read(65557, [("ACTOR0", 1000100), ("LOC_ACTOR0", 12)], byInstance));
    }

    [GameDataFact]
    public void Every_curated_duty_unlock_agrees_with_the_quest_scripts()
    {
        var excel = fixture.Game.Excel;
        var scripts = QuestScriptDuties.Build(excel);
        var catalog = fixture.Bundle.Catalog;
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var dutyNames = excel.GetSheet<ContentFinderCondition>(Language.English)
            .ToDictionary(row => row.RowId, row => row.Name.ExtractText());
        string Duty(uint cfc) => dutyNames.TryGetValue(cfc, out var name) && name.Length > 0 ? $"CFC {cfc} {name}" : $"CFC {cfc}";
        string Quest(uint id) => catalog.GetByRowId(id) is { } q ? $"{id} {q.Name}" : id.ToString();

        Assert.True(scripts.Count > 200, $"only {scripts.Count} quest scripts name a duty");
        Assert.NotEmpty(curated.DutyUnlocks);

        var contradictions = new List<string>();
        var withoutEvidence = new List<string>();
        var confirmed = 0;

        // 1. The entry's own quest: when its script names duties, one of them must be the entry's.
        foreach (var (questId, unlock) in curated.DutyUnlocks.OrderBy(kv => kv.Key))
        {
            if (!scripts.TryGetValue(questId, out var script) || script.NamedDuties.Count == 0)
            {
                var unmapped = script?.UnmappedInstances.Count > 0 ? $" (it names InstanceContent {string.Join(", ", script.UnmappedInstances)}, which no named duty links)" : string.Empty;
                withoutEvidence.Add($"{Quest(questId)} -> {string.Join(", ", unlock.ContentFinderConditionIds.Select(Duty))}: the script names no duty{unmapped}");
                continue;
            }

            if (!unlock.ContentFinderConditionIds.Any(script.NamedDuties.Contains))
            {
                contradictions.Add($"{Quest(questId)} is curated for {string.Join(", ", unlock.ContentFinderConditionIds.Select(Duty))}, but its script names {string.Join(", ", script.NamedDuties.Select(Duty))}");
                continue;
            }

            confirmed++;
            foreach (var cfc in unlock.ContentFinderConditionIds.Where(c => !script.NamedDuties.Contains(c)))
            {
                withoutEvidence.Add($"{Quest(questId)} -> {Duty(cfc)}: the script names the entry's other duties but not this one");
            }
        }

        // 2. Other quests: a live quest whose script announces a duty (Duty Finder message or unlock banner) as its
        // primary duty must be curated for it when that duty is curated at all.
        var curatedByDuty = curated.DutyUnlocks
            .SelectMany(kv => kv.Value.ContentFinderConditionIds.Select(cfc => (Cfc: cfc, Quest: kv.Key)))
            .GroupBy(x => x.Cfc)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Quest).ToHashSet());
        foreach (var script in scripts.Values.Where(s => s.IsUnlockStep).OrderBy(s => s.QuestRowId))
        {
            if (catalog.GetByRowId(script.QuestRowId) is not { IsRemoved: false, IsRetired: false })
            {
                continue;
            }

            foreach (var cfc in script.PrimaryDuties)
            {
                if (curatedByDuty.TryGetValue(cfc, out var quests) && !quests.Contains(script.QuestRowId))
                {
                    contradictions.Add($"{Duty(cfc)} is curated on {string.Join(", ", quests.Order().Select(Quest))}, but {Quest(script.QuestRowId)}'s script announces it (INSTANCEDUNGEON/CONTENT_START with an unlock step)");
                }
            }
        }

        output.WriteLine($"{curated.DutyUnlocks.Count} curated duty unlocks: {confirmed} confirmed by their quest's script, {contradictions.Count} contradicted.");
        foreach (var note in withoutEvidence)
        {
            output.WriteLine("no script evidence: " + note);
        }

        Assert.True(contradictions.Count == 0, "curated duty unlocks the quest scripts contradict:\n" + string.Join("\n", contradictions));
    }
}
