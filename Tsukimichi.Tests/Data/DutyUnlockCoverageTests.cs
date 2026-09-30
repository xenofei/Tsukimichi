using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The duty unlock index the Duty Finder hint (P13) and the unlock routes read, over the live sheets
/// (docs/data/v4/tagging-audit.md finding 4): every dungeon, trial, raid and alliance raid the Duty Finder lists names
/// a quest that unlocks it, unless <see cref="NoUnlockQuest"/> says why none does; the quest-script rule's pairs are all
/// in it; and the retired Heavensward Diadem no longer stands in for Heavensward's own duties.
/// </summary>
public sealed class DutyUnlockCoverageTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    /// <summary>
    /// Duty Finder duties no quest unlocks, by reason (each checked on the duty's wiki page, 2026-09-30). The test fails
    /// when one of them gains an unlock quest, so the list never outlives the gap.
    /// </summary>
    public static readonly IReadOnlyList<(string Reason, uint[] Duties)> NoUnlockQuest =
    [
        ("Omega Savage: interact with the Magitek Terminal after The Anomaly, Test World of Ruin or To Kweh under Distant Skies",
            [256, 257, 258, 259, 292, 293, 294, 295, 591, 592, 593, 594]),
        ("Eden Savage: talk to Lewrey after The Next Piece of the Puzzle, Life Finds a Way or Where I Belong",
            [654, 683, 685, 690, 716, 720, 727, 729, 748, 750, 752, 759]),
        ("Pandæmonium Savage: talk to Nemjiji after Who Wards the Warders?, Truth Imperfect or Guided by the Past",
            [801, 807, 809, 811, 873, 877, 881, 884, 937, 939, 941, 943]),
        ("Arcadion Savage: talk to Gabbro after The Neoteric Witch, The Lone Wolf or Glory Incarnate",
            [986, 988, 990, 992, 1020, 1022, 1024, 1026, 1069, 1071, 1073, 1075]),
        ("Memoria Misera (Extreme): talk to the Wandering Dramaturge in Gangos after The Bozja Incident", [725]),
        ("Special Event I and II: trial rows with no Duty Finder category (ContentUICategory 0), never listed", [70, 71]),
    ];

    private static readonly UnlockKind[] DutyFinderKinds = [UnlockKind.Dungeon, UnlockKind.Trial, UnlockKind.NormalRaid, UnlockKind.AllianceRaid];

    private static DutyUnlockIndex ShippedIndex()
    {
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        return DutyUnlockIndex.Build(curated, UniqueRewardCatalog.Build(unique, new Dictionary<uint, UniqueOverride>(), curated));
    }

    [GameDataFact]
    public void Every_dungeon_trial_and_raid_in_the_Duty_Finder_has_an_unlock_quest()
    {
        var excel = fixture.Game.Excel;
        var catalog = fixture.Bundle.Catalog;
        var index = ShippedIndex();
        var duties = DutyIndex.Build(excel, Language.English);
        var allowed = NoUnlockQuest.SelectMany(g => g.Duties).ToHashSet();
        Assert.Equal(NoUnlockQuest.Sum(g => g.Duties.Length), allowed.Count);

        var checkedCount = 0;
        var missing = new List<string>();
        var stale = new List<string>();
        foreach (var row in excel.GetSheet<ContentFinderCondition>(Language.English))
        {
            if (!duties.TryGetCondition(row.RowId, out var duty) || !DutyFinderKinds.Contains(duty.Kind))
            {
                continue;
            }

            checkedCount++;
            var quests = index.Resolve(row.RowId, catalog);
            if (quests.Count == 0 && !allowed.Contains(row.RowId))
            {
                missing.Add($"CFC {row.RowId} {duty.Name} ({duty.Kind})");
            }
            else if (quests.Count > 0 && allowed.Contains(row.RowId))
            {
                stale.Add($"CFC {row.RowId} {duty.Name} is unlocked by {string.Join(", ", quests.Select(q => $"{q.RowId} {q.Name}"))}; take it off NoUnlockQuest");
            }
        }

        output.WriteLine($"{checkedCount} Duty Finder dungeons, trials and raids; {allowed.Count} allowlisted without an unlock quest.");
        Assert.True(checkedCount > 350, $"only {checkedCount} duties checked");
        Assert.True(missing.Count == 0, "duties without an unlock quest (seed curated/duty_unlocks.json from the wiki, or allowlist with a reason):\n" + string.Join("\n", missing));
        Assert.True(stale.Count == 0, string.Join("\n", stale));

        // Every allowlisted id is a real Duty Finder duty.
        Assert.All(allowed, cfc => Assert.True(duties.TryGetCondition(cfc, out var d) && DutyFinderKinds.Contains(d.Kind), $"CFC {cfc} is not a dungeon, trial or raid"));
    }

    [GameDataFact]
    public void Every_quest_script_rule_pair_is_in_the_index()
    {
        var scripts = QuestScriptDuties.Build(fixture.Game.Excel);
        var catalog = fixture.Bundle.Catalog;
        var index = ShippedIndex();

        var pairs = 0;
        var absent = new List<string>();
        foreach (var script in scripts.Values.Where(s => s.UnlockedDuties.Count > 0).OrderBy(s => s.QuestRowId))
        {
            if (catalog.GetByRowId(script.QuestRowId) is not { IsRemoved: false, IsRetired: false } quest)
            {
                continue;
            }

            foreach (var cfc in script.UnlockedDuties)
            {
                pairs++;
                if (!index.QuestsFor(cfc).Contains(script.QuestRowId))
                {
                    absent.Add($"{quest.RowId} {quest.Name} -> CFC {cfc}");
                }
            }
        }

        output.WriteLine($"{pairs} script-rule pairs over live quests.");
        Assert.True(pairs > 90, $"only {pairs} script-rule pairs");
        Assert.True(absent.Count == 0, "script-rule pairs missing from the duty unlock index (regenerate with tools/regen.ps1):\n" + string.Join("\n", absent));
    }

    [GameDataFact]
    public void Heavensward_unlocks_its_own_duties_and_no_entry_names_the_retired_Diadem()
    {
        var conditions = fixture.Game.Excel.GetSheet<ContentFinderCondition>(Language.English);
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var retired = unique.Entries
            .Where(e => e.Kind == RewardKind.DutyUnlock && conditions.GetRowOrDefault(e.RewardId)?.ContentType.RowId == 23)
            .Select(e => $"{e.QuestRowId} -> CFC {e.RewardId} {e.RewardName}")
            .ToList();
        Assert.True(retired.Count == 0, "duty unlocks of the Heavensward Diadem (ContentType 23):\n" + string.Join("\n", retired));

        // 67205 Heavensward: the Aetherochemical Research Facility and the Singularity Reactor.
        var index = ShippedIndex();
        Assert.Contains(67205u, index.QuestsFor(38));
        Assert.Contains(67205u, index.QuestsFor(90));
    }
}
