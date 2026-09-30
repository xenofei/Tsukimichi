using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Unique;

/// <summary>
/// The Duty Finder unlock hint's lookup (P13): ContentFinderCondition id to the quests that unlock it, curated first,
/// then the reward data. The fixture half runs over the shipped curated files, the shipped <c>unique_quests.json</c> and
/// the frozen catalog, so it needs no game install.
/// </summary>
public sealed class DutyUnlockIndexTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const uint Asphodelos = 808;
    private const uint DuskVigil = 36;
    private const uint LabyrinthOfTheAncients = 92;
    private const uint PharosSirius = 17;
    private const uint Sastasha = 4;
    private const uint Praetorium = 16;

    private static readonly Dictionary<uint, UniqueOverride> NoOverrides = [];

    private DutyUnlockIndex Shipped()
    {
        var data = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        return DutyUnlockIndex.Build(fixture.Curated, UniqueRewardCatalog.Build(data, NoOverrides, fixture.Curated));
    }

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    [Fact]
    public void Asphodelos_the_first_circle_is_unlocked_by_Where_Familiars_Dare()
    {
        var index = Shipped();

        Assert.Equal([70012u], index.QuestsFor(Asphodelos));
        var quest = Assert.Single(index.Resolve(Asphodelos, Catalog));
        Assert.Equal("Where Familiars Dare", quest.Name);
    }

    [Fact]
    public void A_Heavensward_dungeon_resolves_to_its_unlock_quest()
    {
        var index = Shipped();

        var quest = Assert.Single(index.Resolve(DuskVigil, Catalog));
        Assert.Equal(67647u, quest.RowId);
        Assert.Equal("For All the Nights to Come", quest.Name);
        Assert.Equal(1, quest.Expansion);
    }

    [Fact]
    public void An_alliance_raid_resolves_to_its_unlock_quest()
    {
        var index = Shipped();

        var quest = Assert.Single(index.Resolve(LabyrinthOfTheAncients, Catalog));
        Assert.Equal(66738u, quest.RowId);
        Assert.Equal("Labyrinth of the Ancients", quest.Name);
    }

    [Fact]
    public void A_duty_only_the_reward_data_knows_resolves_through_it()
    {
        // Pharos Sirius has no curated entry; Sirius Business carries it as Quest.InstanceContentUnlock.
        var index = Shipped();

        Assert.DoesNotContain(fixture.Curated.DutyUnlocks.Values, u => u.ContentFinderConditionIds.Contains(PharosSirius));
        var quest = Assert.Single(index.Resolve(PharosSirius, Catalog));
        Assert.Equal(66744u, quest.RowId);
    }

    [Fact]
    public void Every_curated_start_of_a_duty_is_listed_in_file_order()
    {
        // It's Probably Pirates has one row per starting city; both unlock Sastasha.
        var index = Shipped();

        Assert.Equal([65781u, 66211u], index.QuestsFor(Sastasha));
        Assert.All(index.Resolve(Sastasha, Catalog), q => Assert.Equal("It's Probably Pirates", q.Name));
    }

    [Fact]
    public void Removed_rows_are_left_out_when_resolving()
    {
        // The pre-6.1 The Ultimate Weapon row stays in the data but the journal no longer lists it; its rework does.
        var index = Shipped();

        Assert.Contains(66060u, index.QuestsFor(Praetorium));
        var resolved = index.Resolve(Praetorium, Catalog);
        Assert.DoesNotContain(resolved, q => q.IsRemoved);
        Assert.Contains(resolved, q => q.RowId == 70058);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(999_999u)]
    public void A_duty_with_no_known_unlock_quest_resolves_to_none(uint condition)
    {
        var index = Shipped();

        Assert.Empty(index.QuestsFor(condition));
        Assert.Empty(index.Resolve(condition, Catalog));
    }

    [Fact]
    public void Resolve_without_a_catalog_is_empty()
    {
        Assert.NotEmpty(Shipped().QuestsFor(Asphodelos));
        Assert.Empty(Shipped().Resolve(Asphodelos, null));
    }

    // ---- synthetic data ----

    private static UniqueRewardEntry Duty(uint quest, uint condition, Confidence confidence = Confidence.Static) =>
        new(quest, RewardKind.DutyUnlock, condition, 0, "Duty " + condition, confidence, "test");

    private static CuratedData CuratedWith(string dutyUnlocksJson)
    {
        var dir = Path.Combine(Path.GetTempPath(), "tsuki-dutyindex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, CuratedData.DutyUnlocksFileName), dutyUnlocksJson);
            return CuratedData.Load(dir);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Curated_quests_come_before_reward_data_and_duplicates_collapse()
    {
        var curated = CuratedWith("""{ "70002": { "contentFinderConditionIds": [ 5 ], "note": "curated" } }""");
        var data = new UniqueRewardsData("1.0", default, [Duty(70001, 5), Duty(70002, 5), Duty(70003, 6)]);
        var index = DutyUnlockIndex.Build(curated, UniqueRewardCatalog.Build(data, NoOverrides, curated));

        Assert.Equal([70002u, 70001u], index.QuestsFor(5));
        Assert.Equal([70003u], index.QuestsFor(6));
        Assert.Equal(2, index.Count);
    }

    [Fact]
    public void A_quest_marked_not_unique_still_unlocks_its_duty()
    {
        var data = new UniqueRewardsData("1.0", default, [Duty(70001, 7)]);
        var overrides = new Dictionary<uint, UniqueOverride> { [70001] = new(false, null) };
        var index = DutyUnlockIndex.Build(CuratedData.Empty, UniqueRewardCatalog.Build(data, overrides, CuratedData.Empty));

        Assert.Equal([70001u], index.QuestsFor(7));
    }

    [Fact]
    public void Entries_of_other_kinds_and_zero_ids_are_ignored()
    {
        var data = new UniqueRewardsData("1.0", default, [
            new UniqueRewardEntry(70001, RewardKind.Instance, 9, 0, "Instance", Confidence.Static, "test"),
            Duty(70002, 0),
        ]);
        var index = DutyUnlockIndex.Build(CuratedData.Empty, UniqueRewardCatalog.Build(data, NoOverrides, CuratedData.Empty));

        Assert.Equal(0, index.Count);
        Assert.Empty(index.QuestsFor(9));
    }

    [Fact]
    public void Source_rebuilds_only_when_an_input_reference_changes()
    {
        var rewards = UniqueRewardCatalog.Build(new UniqueRewardsData("1.0", default, [Duty(70001, 5)]), NoOverrides, CuratedData.Empty);
        var source = new DutyUnlockIndexSource(() => CuratedData.Empty, () => rewards);

        var first = source.Current;
        Assert.Same(first, source.Current);
        Assert.Equal([70001u], first.QuestsFor(5));

        rewards = UniqueRewardCatalog.Build(new UniqueRewardsData("1.0", default, [Duty(70009, 5)]), NoOverrides, CuratedData.Empty);
        Assert.NotSame(first, source.Current);
        Assert.Equal([70009u], source.Current.QuestsFor(5));
    }
}
