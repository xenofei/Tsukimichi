using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Plan;

/// <summary>
/// My blues' tiers (feature plan v7 P4, spec-1.21 "Tiers") over the frozen catalog and the shipped data: the story's
/// side quests first, then content, systems, high-end, and other jobs and societies, for the job the character plays.
/// </summary>
public class UnlockTierTests(PlanFixture fixture) : IClassFixture<PlanFixture>
{
    private const uint LegacyOfAllag = 67245;
    private const uint HalloHalatali = 66233;
    private const uint PrimalAwakening = 66695;
    private const uint PaladinUnlock = 66591;
    private const uint BrotherhoodOfAsh = 66754;
    private const uint ScratchItRich = 66024;
    private const uint IfritBleeds = 66584;
    private const byte Marauder = 3;

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private IReadOnlySet<uint> StoryRequired => StoryRequirements.For(Catalog, null).SideQuests;

    private UnlockTierContext Playing(byte job, params (byte Tribe, byte Rank)[] tribes) =>
        UnlockTierContext.For(job, fixture.Bundle.JobParents(), fixture.Bundle.Jobs, tribes.ToDictionary(static t => t.Tribe, static t => new TribeStanding(t.Rank, 0)), StoryRequired);

    private UnlockTier Tier(uint rowId, UnlockTierContext context, UnlockTags? tags = null) =>
        UnlockTiers.Classify(Catalog.ByRowId[rowId], (tags ?? fixture.Tags).For(rowId), context);

    [Fact]
    public void The_Crystal_Tower_is_what_the_story_needs()
    {
        Assert.Contains(LegacyOfAllag, StoryRequired);
        Assert.Equal(UnlockTier.StoryNeedsIt, Tier(LegacyOfAllag, Playing(Fixture.Gladiator)));

        // The story's need wins over every other rule, the browse view's included.
        Assert.Equal(UnlockTier.StoryNeedsIt, Tier(LegacyOfAllag, UnlockTierContext.Empty with { StoryRequired = StoryRequired }));
    }

    [Fact]
    public void Dungeons_and_raids_open_content()
    {
        var gladiator = Playing(Fixture.Gladiator);
        Assert.Equal(UnlockTier.OpensContent, Tier(HalloHalatali, gladiator));
        Assert.Equal(UnlockTier.OpensContent, Tier(PrimalAwakening, gladiator));
    }

    [Fact]
    public void A_job_quest_is_content_for_its_own_line_and_another_job_for_the_rest()
    {
        // The Paladin unlock is the Gladiator's own line, and another job's quest for a Marauder.
        Assert.Equal(UnlockTier.OpensContent, Tier(PaladinUnlock, Playing(Fixture.Gladiator)));
        Assert.Equal(UnlockTier.OpensContent, Tier(PaladinUnlock, Playing(Fixture.Paladin)));
        Assert.Equal(UnlockTier.AnotherJob, Tier(PaladinUnlock, Playing(Marauder)));

        // Browse mode plays no job.
        Assert.Equal(UnlockTier.AnotherJob, Tier(PaladinUnlock, UnlockTierContext.Empty));
        Assert.Equal(UnlockTier.OpensContent, Tier(HalloHalatali, UnlockTierContext.Empty));
    }

    [Fact]
    public void An_allied_society_counts_as_played_once_the_character_has_a_rank_with_it()
    {
        var tribe = Catalog.ByRowId[BrotherhoodOfAsh].BeastTribe;
        Assert.NotEqual(0, tribe);
        Assert.Equal(UnlockTier.AnotherJob, Tier(BrotherhoodOfAsh, Playing(Fixture.Gladiator)));
        Assert.Equal(UnlockTier.Systems, Tier(BrotherhoodOfAsh, Playing(Fixture.Gladiator, (tribe, 1))));
    }

    [Fact]
    public void A_game_feature_is_a_system()
    {
        Assert.Equal(UnlockTier.Systems, Tier(ScratchItRich, Playing(Fixture.Gladiator)));
    }

    [Fact]
    public void A_quest_that_opens_only_high_end_duties_is_high_end()
    {
        // A duty table with the Bowl of Embers (Hard) typed high-end, as the live sheet types an Extreme.
        var duties = PlanDuties.From([new PlanDuty(59, 0, UnlockKind.Trial, "the Bowl of Embers (Hard)", HighEnd: true)]);
        var unique = UniqueRewardsFile.Load(Path.Combine(Data.FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var rewards = UniqueRewardCatalog.Build(unique, new Dictionary<uint, UniqueOverride>(), fixture.Catalog.Curated);
        var tags = UnlockTags.Build(Catalog, fixture.Features, rewards, duties, fixture.Names.Tribe);

        var unlock = Assert.Single(tags.For(IfritBleeds));
        Assert.True(unlock.HighEnd);
        Assert.False(Assert.Single(fixture.Tags.For(IfritBleeds)).HighEnd);
        Assert.Equal(UnlockTier.HighEnd, Tier(IfritBleeds, Playing(Fixture.Gladiator) with { StoryRequired = new HashSet<uint>() }, tags));
        Assert.Equal(UnlockTier.OpensContent, Tier(IfritBleeds, Playing(Fixture.Gladiator) with { StoryRequired = new HashSet<uint>() }));
    }

    [Fact]
    public void The_plan_carries_each_tier_and_groups_them_in_tier_order()
    {
        var context = Playing(Fixture.Gladiator);
        var states = fixture.States(PlanFixture.Fresh());
        var plan = UnlockPlan.Build(fixture.Tags, states, fixture.Names, tierOf: (quest, unlocks) => UnlockTiers.Classify(quest, unlocks, context));

        Assert.All(plan.Entries, e => Assert.Equal(UnlockTiers.Classify(e.Quest, fixture.Tags.For(e.Quest.RowId), context), e.Tier));
        var order = plan.TierGroups.Select(static g => g.Tier).ToList();
        Assert.Equal(order.Order().ToList(), order);
        Assert.Equal(UnlockTier.StoryNeedsIt, order[0]);
        Assert.Equal(plan.Count, plan.TierGroups.Sum(static g => g.Count));
        Assert.Contains(plan.TierGroups.Single(static g => g.Tier == UnlockTier.StoryNeedsIt).Entries, e => e.Quest.RowId == LegacyOfAllag);
    }
}
