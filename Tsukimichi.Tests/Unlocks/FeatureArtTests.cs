using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.Tests.Data;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Unlocks;

/// <summary>
/// Feature unlocks wear a game icon (owner point 9, UI-5a): a label finds its Duty Finder tile, menu or item
/// (<see cref="FeatureArt"/>), every curated label finds one, and the unlock index puts the icon on the row, without
/// game files.
/// </summary>
public class FeatureArtTests
{
    private const uint Quest1 = 66_640;
    private const uint PvpIcon = 61806;
    private const uint HuntIcon = 61819;
    private const uint SightseeingIcon = 61;

    private static FeatureIcons Icons() => new(new Dictionary<FeatureArtRef, uint>
    {
        [FeatureArtRef.Content(FeatureArt.ContentPvp)] = PvpIcon,
        [FeatureArtRef.Content(FeatureArt.ContentTheHunt)] = HuntIcon,
        [FeatureArtRef.Menu(FeatureArt.MenuSightseeingLog)] = SightseeingIcon,
        [FeatureArtRef.Content(FeatureArt.ContentEureka)] = 61833,
        [FeatureArtRef.Menu(FeatureArt.MenuTrust)] = 0,
    });

    private static UniqueRewardEntry Feature(string label) =>
        new(Quest1, RewardKind.SystemUnlock, 0, 0, label, Confidence.Curated, "curated/system_unlocks.json");

    private static UniqueRewardCatalog Rewards(params UniqueRewardEntry[] entries) =>
        UniqueRewardCatalog.Build(new UniqueRewardsData("test", default, entries), new Dictionary<uint, UniqueOverride>(), CuratedData.Empty);

    [Fact]
    public void A_label_finds_its_tile_menu_or_item_the_more_specific_word_first()
    {
        Assert.Equal(FeatureArtRef.Content(FeatureArt.ContentPvp), FeatureArt.Of("PvP"));
        Assert.Equal(FeatureArtRef.Content(FeatureArt.ContentPvp), FeatureArt.Of("Crystalline Conflict"));
        Assert.Equal(FeatureArtRef.Content(FeatureArt.ContentTheHunt), FeatureArt.Of("Hunts (Endwalker elite)"));
        Assert.Equal(FeatureArtRef.Content(FeatureArt.ContentTreasureHunt), FeatureArt.Of("Treasure hunt"));
        Assert.Equal(FeatureArtRef.Content(FeatureArt.ContentOceanFishing), FeatureArt.Of("Ocean fishing"));
        Assert.Equal(FeatureArtRef.Menu(FeatureArt.MenuFishingLog), FeatureArt.Of("Spearfishing"));
        Assert.Equal(FeatureArtRef.Content(FeatureArt.ContentDeepDungeons), FeatureArt.Of("Eureka Orthos"));
        Assert.Equal(FeatureArtRef.Content(FeatureArt.ContentEureka), FeatureArt.Of("Eureka (Forbidden Land)"));
        Assert.Equal(FeatureArtRef.Content(FeatureArt.ContentSaveTheQueen), FeatureArt.Of("Bozjan Southern Front"));
        Assert.Equal(FeatureArtRef.Content(FeatureArt.ContentGoldSaucer), FeatureArt.Of("Chocobo racing"));
        Assert.Equal(FeatureArtRef.Menu(FeatureArt.MenuChocoboSaddlebag), FeatureArt.Of("Chocobo saddlebag"));
        Assert.Equal(FeatureArtRef.Menu(FeatureArt.MenuCompanion), FeatureArt.Of("Chocobo companion"));
        Assert.Equal(FeatureArtRef.Menu(FeatureArt.MenuCraftingLog), FeatureArt.Of("Master recipes: glamours"));
        Assert.Equal(FeatureArtRef.Menu(FeatureArt.MenuArmouryChest), FeatureArt.Of("Glamour (cast/dispel)"));
        Assert.Equal(FeatureArtRef.Menu(FeatureArt.MenuSightseeingLog), FeatureArt.Of("Sightseeing Log (Dawntrail) · entries 29-45"));
        Assert.Equal(FeatureArtRef.ItemRow(FeatureArt.ItemStrikingDummy), FeatureArt.Of("The Spire of Trial"));
        Assert.Equal(FeatureArtRef.Content(FeatureArt.ContentRetainerVentures), FeatureArt.Of("retainers"));
        Assert.Equal(FeatureArtRef.None, FeatureArt.Of("Something new"));
        Assert.Equal(FeatureArtRef.None, FeatureArt.Of(" "));
        Assert.Equal(FeatureArtRef.None, FeatureArt.Of(null));
        Assert.True(FeatureArt.Of(null).IsNone);
    }

    [Fact]
    public void Every_curated_feature_label_finds_an_icon_row()
    {
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var labels = curated.SystemUnlocks.Values.Select(s => s.Label).Distinct().ToList();
        Assert.NotEmpty(labels);
        var blank = labels.Where(l => FeatureArt.Of(l).IsNone).ToList();
        Assert.True(blank.Count == 0, "curated features without an icon row: " + string.Join(", ", blank));

        var rows = FeatureArt.All().ToList();
        Assert.Equal(rows.Count, rows.Distinct().Count());
        Assert.DoesNotContain(rows, r => r.IsNone);
    }

    [Fact]
    public void The_icons_read_leave_out_the_rows_without_one()
    {
        var icons = Icons();

        Assert.Equal(4, icons.Count);
        Assert.Equal(PvpIcon, icons.For("PvP"));
        Assert.Equal(0u, icons.For("Trust"));
        Assert.Equal(0u, icons.For("Something new"));
        Assert.Equal(0u, FeatureIcons.Empty.For("PvP"));
    }

    [Fact]
    public void A_feature_row_wears_its_icon_and_a_rewards_own_icon_wins()
    {
        // One curated feature per quest, as system_unlocks.json has them.
        string[] labels = ["PvP", "Hunts (ARR)", "Eureka (Forbidden Land)", "Retainers", "Flying (Heavensward entry)"];
        var quests = labels.Select((_, i) => Quest(Quest1 + (uint)i, "Quest " + i) with
        {
            Rewards = i == 0 ? [new RewardRef(RewardKind.Other, 7, 0, 1, "Wondrous Tails", 25987)] : [],
        }).ToArray();
        var links = new UnlockLinks { FeatureIcons = Icons() };
        var rewards = Rewards([.. labels.Select((label, i) => Feature(label) with { QuestRowId = Quest1 + (uint)i })]);
        var index = QuestUnlocks.Build(Catalog(quests), rewards, PlanDuties.Empty, links);
        var rows = quests.SelectMany(q => index.For(q.RowId)).ToList();

        Assert.Equal(PvpIcon, Assert.Single(rows, e => e.Name == "PvP").Icon);
        Assert.Equal(HuntIcon, Assert.Single(rows, e => e.Name == "Hunts (ARR)").Icon);
        var eureka = Assert.Single(rows, e => e.Name == "Eureka (Forbidden Land)");
        Assert.Equal(UnlockTarget.FieldOperation, eureka.Target);
        Assert.Equal(61833u, eureka.Icon);
        Assert.Equal(QuestUnlocks.AetherCurrentIcon, Assert.Single(rows, e => e.Target == UnlockTarget.Flying).Icon);
        Assert.Equal(25987u, Assert.Single(rows, e => e.Name == "Wondrous Tails").Icon);

        // An icon the sheets did not give leaves the stand-in.
        Assert.Equal(0u, Assert.Single(rows, e => e.Name == "Retainers").Icon);
        var unread = QuestUnlocks.Build(Catalog(quests), rewards, PlanDuties.Empty, UnlockLinks.Empty);
        Assert.All(quests.SelectMany(q => unread.For(q.RowId)).Where(e => e.Target == UnlockTarget.System && e.Name != "Wondrous Tails"), e => Assert.Equal(0u, e.Icon));
    }

    [Fact]
    public void A_nameless_pvp_instance_named_PvP_and_the_curated_PvP_feature_are_one_row()
    {
        // CatalogMapper names A Pup No Longer's solo instance for the PvP content type and gives it PvP's tile.
        var quest = Quest(Quest1, "A Pup No Longer (Maelstrom)") with
        {
            Rewards = [new RewardRef(RewardKind.Instance, 40001, 0, 1, "PvP", PvpIcon)],
        };
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var links = new UnlockLinks { FeatureIcons = Icons() };
        var rows = QuestUnlocks.Build(Catalog(quest), Rewards(Feature("PvP")), PlanDuties.Empty, links, curated).For(Quest1);

        var pvp = Assert.Single(rows);
        Assert.Equal("PvP", pvp.Name);
        Assert.Equal(UnlockGroup.Duty, pvp.Group);
        Assert.Equal(PvpIcon, pvp.Icon);
        Assert.Equal(UnlockSource.Curated, pvp.Source);
        Assert.False(pvp.InRewards);
    }
}
