using System.Collections.Frozen;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.GamePanels;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.GamePanels;

public class QuestVerdictTests
{
    private static readonly PlanUnlock[] NoUnlocks = [];
    private static readonly MoonlitReward[] NoMoonlit = [];

    [Fact]
    public void An_unlock_comes_first()
    {
        var quest = Quest(A);
        PlanUnlock[] unlocks = [new(UnlockKind.Trial, "Aglaia")];
        MoonlitReward[] moonlit = [new("Wind-up Sun", false)];

        Assert.Equal("Unlocks Aglaia", QuestVerdict.Line(quest, false, unlocks, moonlit, null, string.Empty));
    }

    [Fact]
    public void A_Moonlit_reward_the_character_lacks_comes_before_one_it_owns()
    {
        var quest = Quest(A);
        MoonlitReward[] moonlit = [new("Wind-up Moon", true), new("Wind-up Sun", false)];

        Assert.Equal("Moonlit: Wind-up Sun", QuestVerdict.Line(quest, false, NoUnlocks, moonlit, null, string.Empty));
    }

    [Fact]
    public void Several_lacking_rewards_are_counted_and_unknown_counts_as_lacking()
    {
        var quest = Quest(A);
        MoonlitReward[] moonlit = [new("Wind-up Sun", null), new("Wind-up Moon", false), new("Gold Moon", true)];

        Assert.Equal("Moonlit: Wind-up Sun and 1 more", QuestVerdict.Line(quest, false, NoUnlocks, moonlit, null, string.Empty));
    }

    [Fact]
    public void An_owned_reward_is_said_to_be_owned()
    {
        MoonlitReward[] moonlit = [new("Wind-up Sun", true)];

        Assert.Equal("Moonlit: Wind-up Sun (you have it)", QuestVerdict.Line(Quest(A), false, NoUnlocks, moonlit, null, string.Empty));
    }

    [Fact]
    public void An_unlock_of_kind_Other_waits_behind_Moonlit_rewards()
    {
        PlanUnlock[] other = [new(UnlockKind.Other, "Faded Copy")];
        MoonlitReward[] moonlit = [new("Wind-up Sun", false)];

        Assert.Equal("Moonlit: Wind-up Sun", QuestVerdict.Line(Quest(A), false, other, moonlit, null, string.Empty));
        Assert.Equal("Unlocks Faded Copy", QuestVerdict.Line(Quest(A), false, other, NoMoonlit, null, string.Empty));
    }

    [Fact]
    public void A_chain_step_then_seasonal_then_repeatable_then_nothing()
    {
        var step = new ChainStep(3, 7, B);

        Assert.Equal("Part of Hildibrand · 4 more after this", QuestVerdict.Line(Quest(A), false, NoUnlocks, NoMoonlit, step, "Hildibrand"));
        Assert.Equal("Seasonal event quest", QuestVerdict.Line(Quest(A) with { Festival = 4, IsRepeatable = true }, false, NoUnlocks, NoMoonlit, null, string.Empty));
        Assert.Equal("Repeatable quest", QuestVerdict.Line(Quest(A) with { IsRepeatable = true }, false, NoUnlocks, NoMoonlit, null, string.Empty));
        Assert.Equal("No unlock or unique reward", QuestVerdict.Line(Quest(A), false, NoUnlocks, NoMoonlit, null, string.Empty));
    }

    [Fact]
    public void A_masked_quest_says_nothing()
    {
        PlanUnlock[] unlocks = [new(UnlockKind.Trial, "Aglaia")];

        Assert.Equal(QuestVerdict.Masked, QuestVerdict.Line(Quest(A), true, unlocks, NoMoonlit, null, string.Empty));
    }

    [Fact]
    public void A_chain_place_says_what_is_left_after_the_quest_never_step_N_of_M()
    {
        Assert.Equal("Part of Hildibrand · 1 more after this", QuestVerdict.ChainPlace(new ChainStep(6, 7, B), "Hildibrand"));
        Assert.Equal("The last quest of Hildibrand", QuestVerdict.ChainPlace(new ChainStep(7, 7, null), "Hildibrand"));
        Assert.Equal("Part of a chain · 4 more after this", QuestVerdict.ChainPlace(new ChainStep(3, 7, B), string.Empty));
        Assert.Equal("The last quest of a chain", QuestVerdict.ChainPlace(new ChainStep(7, 7, null), string.Empty));
    }

    [Fact]
    public void StepOf_counts_only_the_counted_steps_and_names_the_next()
    {
        var chain = new Chain("Hildibrand", [A, B, C, D]) { Uncounted = new HashSet<uint> { B }.ToFrozenSet() };

        Assert.Equal(new ChainStep(2, 3, D), QuestVerdict.StepOf(chain, C));
        Assert.Equal(new ChainStep(3, 3, null), QuestVerdict.StepOf(chain, D));
        Assert.Null(QuestVerdict.StepOf(chain, B));
        Assert.Null(QuestVerdict.StepOf(chain, E));
    }

    [Fact]
    public void Unlocks_take_the_plan_tags_and_drop_inherited_ones()
    {
        PlanUnlock[] tags = [new(UnlockKind.Trial, "Aglaia"), new(UnlockKind.NormalRaid, string.Empty, Inherited: true), new(UnlockKind.Trial, "Aglaia")];

        var unlocks = QuestVerdict.Unlocks(Quest(A), tags);

        Assert.Equal([new PlanUnlock(UnlockKind.Trial, "Aglaia")], unlocks);
    }

    [Fact]
    public void Without_plan_tags_the_quest_s_own_unlock_rewards_are_read()
    {
        var quest = Quest(A) with
        {
            Rewards =
            [
                new RewardRef(RewardKind.Item, 1, 1, 3, "Potion", 0),
                new RewardRef(RewardKind.Instance, 2, 0, 1, "the Vault", 0),
                new RewardRef(RewardKind.ClassJob, 19, 0, 1, "paladin", 0),
                new RewardRef(RewardKind.GeneralAction, 5, 0, 1, "Desynthesis", 0),
            ],
        };

        var labels = QuestVerdict.Unlocks(quest, NoUnlocks).Select(u => u.Name).ToList();

        Assert.Equal(["The Vault", "Paladin", "Desynthesis"], labels);
    }
}
