using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Data;
using Tsukimichi.Tests.Storage;
using Xunit.Abstractions;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>How feature ("blue") quests are derived from curated data and rewards.</summary>
public sealed class FeaturePresetsTests : IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private CuratedData Curated(string? featureQuests = null, string? systemUnlocks = null, string? dutyUnlocks = null)
    {
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        if (featureQuests is not null)
        {
            File.WriteAllText(Path.Combine(dir, CuratedData.FeatureQuestsFileName), featureQuests);
        }

        if (systemUnlocks is not null)
        {
            File.WriteAllText(Path.Combine(dir, CuratedData.SystemUnlocksFileName), systemUnlocks);
        }

        if (dutyUnlocks is not null)
        {
            File.WriteAllText(Path.Combine(dir, CuratedData.DutyUnlocksFileName), dutyUnlocks);
        }

        return CuratedData.Load(dir);
    }

    private static QuestRecord Side(uint rowId, params RewardRef[] rewards) => Quest(rowId, $"Quest {rowId}", section: 2, rewards: rewards);

    [Theory]
    [InlineData(RewardKind.Instance)]
    [InlineData(RewardKind.DutyUnlock)]
    [InlineData(RewardKind.ClassJob)]
    [InlineData(RewardKind.SystemUnlock)]
    [InlineData(RewardKind.Action)]
    [InlineData(RewardKind.GeneralAction)]
    [InlineData(RewardKind.Trait)]
    [InlineData(RewardKind.AetherCurrent)]
    [InlineData(RewardKind.BlueMageSpell)]
    public void A_reward_that_unlocks_something_marks_a_feature_quest(RewardKind kind)
    {
        Assert.True(FeaturePresets.IsFeatureQuest(Side(1, Reward(kind, "x")), CuratedData.Empty));
    }

    [Theory]
    [InlineData(RewardKind.Item)]
    [InlineData(RewardKind.OptionalItem)]
    [InlineData(RewardKind.Emote)]
    [InlineData(RewardKind.Mount)]
    [InlineData(RewardKind.Minion)]
    [InlineData(RewardKind.Orchestrion)]
    [InlineData(RewardKind.TripleTriadCard)]
    [InlineData(RewardKind.Ornament)]
    [InlineData(RewardKind.ArtifactGear)]
    [InlineData(RewardKind.Title)]
    [InlineData(RewardKind.Achievement)]
    public void Collectible_and_cosmetic_rewards_do_not(RewardKind kind)
    {
        Assert.False(FeaturePresets.IsFeatureQuest(Side(1, Reward(kind, "x")), CuratedData.Empty));
    }

    [Fact]
    public void Other_counts_only_as_a_named_OtherReward_not_a_currency_or_an_unresolved_slot()
    {
        var otherReward = new RewardRef(RewardKind.Other, 12, 0, 1, "Chocobo Companion", 0);
        var currency = new RewardRef(RewardKind.Other, 28, 28, 100, "Allagan Tomestone of Poetics", 0);
        var unresolved = new RewardRef(RewardKind.Other, 5, 0, 1, string.Empty, 0);

        Assert.True(FeaturePresets.IsFeatureQuest(Side(1, otherReward), CuratedData.Empty));
        Assert.False(FeaturePresets.IsFeatureQuest(Side(2, currency), CuratedData.Empty));
        Assert.False(FeaturePresets.IsFeatureQuest(Side(3, unresolved), CuratedData.Empty));
    }

    [Fact]
    public void Curated_feature_list_and_unlock_files_mark_quests_without_rewards()
    {
        var curated = Curated(
            featureQuests: "[ 66001 ]",
            systemUnlocks: """{ "66002": { "label": "Glamour Dresser" } }""",
            dutyUnlocks: """{ "66003": [ 4 ] }""");

        Assert.True(FeaturePresets.IsFeatureQuest(Side(66001), curated));
        Assert.True(FeaturePresets.IsFeatureQuest(Side(66002), curated));
        Assert.True(FeaturePresets.IsFeatureQuest(Side(66003), curated));
        Assert.False(FeaturePresets.IsFeatureQuest(Side(66004), curated));
    }

    [Fact]
    public void Main_scenario_sections_zero_and_one_are_never_feature_quests()
    {
        var curated = Curated(featureQuests: "[ 66001, 66002 ]");
        var arr = Quest(66001, "MSQ", section: 0, rewards: Reward(RewardKind.Instance, "Sastasha"));
        var dawntrail = Quest(66002, "MSQ", section: 1, rewards: Reward(RewardKind.ClassJob, "Viper"));
        var side = Quest(66003, "Side", section: 2, rewards: Reward(RewardKind.Instance, "Haukke Manor"));

        Assert.True(FeaturePresets.IsMainScenario(arr));
        Assert.True(FeaturePresets.IsMainScenario(dawntrail));
        Assert.False(FeaturePresets.IsMainScenario(side));
        Assert.False(FeaturePresets.IsFeatureQuest(arr, curated));
        Assert.False(FeaturePresets.IsFeatureQuest(dawntrail, curated));
        Assert.True(FeaturePresets.IsFeatureQuest(side, curated));
    }

    [Fact]
    public void An_unlisted_quest_in_section_zero_is_not_main_scenario()
    {
        var unlisted = Quest(66005, "Removed", section: 0, category: 0, genre: 0, rewards: Reward(RewardKind.Action, "Sprint"));
        Assert.True(unlisted.IsUnlisted);
        Assert.False(FeaturePresets.IsMainScenario(unlisted));
        Assert.True(FeaturePresets.IsFeatureQuest(unlisted, CuratedData.Empty));
    }

    [Fact]
    public void Repeatables_are_never_feature_quests()
    {
        var curated = Curated(featureQuests: "[ 66001 ]");
        var daily = Quest(66001, "Daily", section: 2, repeatable: true, rewards: Reward(RewardKind.Action, "x"));
        Assert.False(FeaturePresets.IsFeatureQuest(daily, curated));
    }

    [Fact]
    public void The_blue_journal_icon_marks_a_feature_quest_unless_main_scenario_or_repeatable()
    {
        var blue = Side(1) with { EventIconType = FeaturePresets.FeatureEventIconType };
        var ordinary = Side(2) with { EventIconType = 3 };
        var msq = Quest(3, "MSQ", section: 0) with { EventIconType = FeaturePresets.FeatureEventIconType };
        var daily = Quest(4, "Daily", section: 2, repeatable: true) with { EventIconType = FeaturePresets.FeatureEventIconType };

        Assert.True(FeaturePresets.IsFeatureQuest(blue, CuratedData.Empty));
        Assert.False(FeaturePresets.IsFeatureQuest(ordinary, CuratedData.Empty));
        Assert.False(FeaturePresets.IsFeatureQuest(msq, CuratedData.Empty));
        Assert.False(FeaturePresets.IsFeatureQuest(daily, CuratedData.Empty));
    }

    [Fact]
    public void Derive_collects_every_feature_quest_row_id_once()
    {
        var curated = Curated(featureQuests: "[ 3 ]");
        var catalog = QuestCatalog.Build(
        [
            Side(1, Reward(RewardKind.Instance, "a")),
            Side(2, Reward(RewardKind.Item, "b")),
            Side(3),
            Quest(4, "MSQ", section: 0, rewards: Reward(RewardKind.Instance, "c")),
            Side(5, Reward(RewardKind.Trait, "d"), Reward(RewardKind.Action, "e")),
        ]);

        var ids = FeaturePresets.Derive(catalog, curated);

        Assert.Equal(new HashSet<uint> { 1, 3, 5 }, ids);
    }

    [Fact]
    public void Unique_reward_unlock_entries_mark_their_quests_but_collectibles_do_not()
    {
        var catalog = QuestCatalog.Build([Side(1), Side(2), Side(3), Quest(4, "MSQ", section: 0), Side(5)]);
        UniqueRewardEntry[] unique =
        [
            new(1, RewardKind.DutyUnlock, 4, 0, "Sastasha", Confidence.Static, "test"),
            new(2, RewardKind.Mount, 1, 0, "Company Chocobo", Confidence.Static, "test"),
            new(3, RewardKind.AetherCurrent, 9, 0, "Aether current", Confidence.Static, "test"),
            new(4, RewardKind.SystemUnlock, 0, 0, "Retainers", Confidence.Curated, "test"),
        ];

        var unlockQuests = FeaturePresets.UnlockQuests(unique);
        Assert.Equal(new HashSet<uint> { 1, 3, 4 }, unlockQuests);

        // The main scenario quest stays out even though it unlocks a system.
        Assert.Equal(new HashSet<uint> { 1, 3 }, FeaturePresets.Derive(catalog, CuratedData.Empty, unique));
        Assert.Empty(FeaturePresets.Derive(catalog, CuratedData.Empty));
    }
}

/// <summary>Counts over the real sheets, so the derived set can be eyeballed against the wiki's feature quest list.</summary>
public class FeaturePresetsDataTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    [GameDataFact]
    public void Derived_feature_set_covers_the_unlock_quests()
    {
        var catalog = fixture.Bundle.Catalog;
        var dataDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Tsukimichi", "Data"));
        var curated = CuratedData.Load(Path.Combine(dataDir, "curated"));
        var unique = UniqueRewardsFile.Load(Path.Combine(dataDir, "unique_quests.json"));
        Assert.NotEmpty(unique.Entries);

        IReadOnlySet<uint> ids = FeaturePresets.Derive(catalog, curated, unique.Entries);

        var byKind = new Dictionary<RewardKind, int>();
        var msq = 0;
        foreach (var quest in catalog.All)
        {
            if (FeaturePresets.IsMainScenario(quest))
            {
                msq++;
            }

            if (!ids.Contains(quest.RowId))
            {
                continue;
            }

            foreach (var reward in quest.Rewards)
            {
                byKind[reward.Kind] = byKind.GetValueOrDefault(reward.Kind) + 1;
            }
        }

        output.WriteLine($"Feature quests: {ids.Count} of {catalog.Count} (main scenario: {msq}; curated feature {curated.FeatureQuests.Count}, system {curated.SystemUnlocks.Count}, duty {curated.DutyUnlocks.Count})");
        foreach (var section in new uint[] { 0, 1 })
        {
            var quests = catalog.BySection.GetValueOrDefault(section) ?? [];
            output.WriteLine($"Section {section}: {quests.Count} quests, \"{(quests.Count > 0 ? quests[0].Journal.SectionName : string.Empty)}\"");
        }

        foreach (var (kind, count) in byKind.OrderByDescending(kv => kv.Value))
        {
            output.WriteLine($"  {kind}: {count} rewards");
        }

        // Cross-check against the sheet's EventIconType (the journal icon family) so the derivation can be judged:
        // type 8 is the blue "+" feature icon, which the derivation now takes directly (it also covers job quests
        // and the raid chronicles the reward-based rules cannot see), so the "not derived" sample below stays empty.
        var sheet = fixture.Game.Excel.GetSheet<Lumina.Excel.Sheets.Quest>();
        var byIcon = new SortedDictionary<uint, (int Total, int Msq, int Derived, int Repeatable)>();
        var missing = new List<string>();
        foreach (var quest in catalog.All)
        {
            var row = sheet.GetRowOrDefault(quest.RowId);
            if (row is null)
            {
                continue;
            }

            var icon = row.Value.EventIconType.RowId;
            var entry = byIcon.GetValueOrDefault(icon);
            entry.Total++;
            if (FeaturePresets.IsMainScenario(quest))
            {
                entry.Msq++;
            }

            if (quest.IsRepeatable)
            {
                entry.Repeatable++;
            }

            if (ids.Contains(quest.RowId))
            {
                entry.Derived++;
            }
            else if (icon == 8 && !quest.IsUnlisted && !FeaturePresets.IsMainScenario(quest) && !quest.IsRepeatable && missing.Count < 10)
            {
                missing.Add($"{quest.RowId} {quest.Name} [{quest.Journal.SectionName} › {quest.Journal.CategoryName}] rewards: {string.Join(", ", quest.Rewards.Select(r => r.Kind + ":" + r.Name))}");
            }

            byIcon[icon] = entry;
        }

        foreach (var (icon, entry) in byIcon)
        {
            output.WriteLine($"EventIconType {icon}: {entry.Total} quests, {entry.Msq} MSQ, {entry.Repeatable} repeatable, {entry.Derived} in derived set");
        }

        foreach (var line in missing)
        {
            output.WriteLine("  not derived: " + line);
        }

        Assert.Equal(5373, catalog.Count);
        Assert.Equal(1699, ids.Count);
        Assert.True(ids.Count >= 1600, $"expected at least 1600 feature quests, got {ids.Count}");
        Assert.Empty(missing);
        Assert.All(ids, id => Assert.False(FeaturePresets.IsMainScenario(catalog.ByRowId[id])));
        Assert.All(ids, id => Assert.False(catalog.ByRowId[id].IsRepeatable));

        // Every listed, non-repeatable blue-icon quest outside the main scenario is in.
        foreach (var quest in catalog.All)
        {
            if (quest.EventIconType == FeaturePresets.FeatureEventIconType && !quest.IsRepeatable && !FeaturePresets.IsMainScenario(quest))
            {
                Assert.Contains(quest.RowId, ids);
            }
        }

        // Landmarks: the MSQ dungeon unlock and "Close to Home" (65621, an ordinary icon-3 side quest) stay out;
        // "Hallo Halatali" (66233, Instance reward), "Ifrit Bleeds, We Can Kill It" (66584, hard-mode trial unlock)
        // and the Crystal Tower opener "Legacy of Allag" (Chronicles of a New Era, blue icon, no unlock reward) are in.
        Assert.DoesNotContain(catalog.All.First(q => q.Name == "It's Probably Pirates" && !q.IsUnlisted).RowId, ids);
        Assert.DoesNotContain(65621u, ids);
        Assert.Contains(66233u, ids);
        Assert.Contains(66584u, ids);
        var chronicles = catalog.All.FirstOrDefault(q => q.Name == "Legacy of Allag" && !q.IsUnlisted)
                         ?? catalog.All.First(q => q.Journal.GenreId == 18 && !q.IsRepeatable);
        Assert.Contains(chronicles.RowId, ids);
    }
}
