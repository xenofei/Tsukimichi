using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// <see cref="BlockerText"/> over the frozen catalog with the bundle's own names: the cases from the player research
/// (docs/research/player-gripes-2026.md P1), a Dragoon job quest gated by the main scenario and an allied society
/// quest gated by rank.
/// </summary>
public class BlockerTextFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const byte Dragoon = 22;
    private const byte Amaljaa = 1;

    // Dragoon quest line, Shadowbringers tier.
    private const uint GoneButNotForgiven = 68749;
    private const uint DragonSound = 68450;
    private const uint CourageBornOfFear = 68814;
    private const uint Shadowbringers = 69190;

    // Amalj'aa society story.
    private const uint PeaceForThanalan = 66753;
    private const uint BrotherhoodOfAsh = 66754;
    private const uint RangerRescue = 66755;
    private const uint AnEyeOnTheInside = 66756;

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private EvalContext Context => new() { ClassJobs = fixture.Bundle.Jobs };

    private static CharacterSnapshot Dragoon80(params uint[] completedRowIds) => Fixture.Snapshot(completedRowIds) with
    {
        CurrentJob = Dragoon,
        JobLevels = Fixture.Levels((Dragoon, 80)),
    };

    [Fact]
    public void A_level_80_Dragoon_quest_gated_by_the_main_scenario_says_after_MSQ()
    {
        var quest = Catalog.GetByRowId(GoneButNotForgiven)!;
        Assert.Equal("Gone but Not Forgiven", quest.Name);
        Assert.Equal(80, quest.Level);
        Assert.Equal(Dragoon, quest.ClassJobRequired);

        var names = fixture.Bundle.BlockerNames();
        var snapshot = Dragoon80(DragonSound, CourageBornOfFear);
        var evaluation = StateResolver.Resolve(quest, snapshot, Catalog, Context);

        Assert.Equal(QuestState.Blocked, evaluation.State);
        Assert.Equal("after MSQ: Shadowbringers", BlockerText.For(evaluation, quest, names));
        Assert.Equal("Blocked · after MSQ: Shadowbringers", BlockerText.StatusText(evaluation, quest, names));

        // With every quest resolved the completed job quests are skipped and the main scenario gate is still the one named.
        var states = StateResolver.ResolveAll(Catalog, snapshot, Context);
        Assert.Equal("after MSQ: Shadowbringers", BlockerText.For(states[GoneButNotForgiven], quest, names, states));

        // The main scenario done: the quest is Ready on the Dragoon and nothing is said.
        var ready = StateResolver.Resolve(quest, Dragoon80(DragonSound, CourageBornOfFear, Shadowbringers), Catalog, Context);
        Assert.Equal(QuestState.Ready, ready.State);
        Assert.Equal("Ready", BlockerText.StatusText(ready, quest, names));

        // On a level 50 Dragoon the level comes before the main scenario gate? No: the prerequisite is what the
        // player acts on first, and the pinned job's level is named only once the prerequisites are done.
        var low = Dragoon80(DragonSound, CourageBornOfFear) with { JobLevels = Fixture.Levels((Dragoon, 50)) };
        Assert.Equal("after MSQ: Shadowbringers", BlockerText.For(StateResolver.Resolve(quest, low, Catalog, Context), quest, names));
        var lowButUnlocked = Dragoon80(DragonSound, CourageBornOfFear, Shadowbringers) with { JobLevels = Fixture.Levels((Dragoon, 50)) };
        Assert.Equal("Lv 80 on DRG", BlockerText.For(StateResolver.Resolve(quest, lowButUnlocked, Catalog, Context), quest, names));
    }

    [Fact]
    public void An_allied_society_quest_gated_by_rank_says_Rank_with_the_society()
    {
        var quest = Catalog.GetByRowId(AnEyeOnTheInside)!;
        Assert.Equal("An Eye on the Inside", quest.Name);
        Assert.Equal(Amaljaa, quest.BeastTribe);
        Assert.Equal(2, quest.BeastRank);
        Assert.Equal(0, quest.BeastValue);

        var names = fixture.Bundle.BlockerNames();
        var neutral = Fixture.Snapshot(RangerRescue) with
        {
            JobLevels = Fixture.Levels((Fixture.Gladiator, 60)),
            Tribes = new Dictionary<byte, TribeStanding> { [Amaljaa] = new(1, 0) },
        };
        var evaluation = StateResolver.Resolve(quest, neutral, Catalog, Context);

        Assert.Equal(QuestState.Blocked, evaluation.State);
        Assert.Equal("Rank: Recognized with the Amalj'aa", BlockerText.For(evaluation, quest, names));
        Assert.Equal("Blocked · Rank: Recognized with the Amalj'aa", BlockerText.StatusText(evaluation, quest, names));

        var recognized = neutral with { Tribes = new Dictionary<byte, TribeStanding> { [Amaljaa] = new(2, 0) } };
        Assert.Equal(QuestState.Ready, StateResolver.Resolve(quest, recognized, Catalog, Context).State);
    }

    [Fact]
    public void Brotherhood_of_Ash_is_gated_by_its_society_story_prerequisite_not_by_reputation()
    {
        var quest = Catalog.GetByRowId(BrotherhoodOfAsh)!;
        Assert.Equal("Brotherhood of Ash", quest.Name);
        Assert.Equal(Amaljaa, quest.BeastTribe);
        // The sheet writes 65535 for the society story quests; the mapper reads that as no reputation gate.
        Assert.Equal(0, quest.BeastValue);

        var names = fixture.Bundle.BlockerNames();
        var fresh = Fixture.Snapshot() with { JobLevels = Fixture.Levels((Fixture.Gladiator, 60)) };
        var blocked = StateResolver.Resolve(quest, fresh, Catalog, Context);
        Assert.Equal("Blocked · after: Peace for Thanalan", BlockerText.StatusText(blocked, quest, names));

        var ready = StateResolver.Resolve(quest, fresh with { CompletedBits = Fixture.Bits(PeaceForThanalan) }, Catalog, Context);
        Assert.Equal(QuestState.Ready, ready.State);
    }

    [Fact]
    public void The_bundle_names_duties_categories_and_steps_for_the_blocker_line()
    {
        var names = fixture.Bundle.Names;
        Assert.Equal("Sastasha", names.Duty(4));
        Assert.Equal("The Thousand Maws of Toto-Rak", names.Duty(1));
        Assert.Equal("Disciple of the Hand", names.ClassJobCategory(33));

        Assert.Equal(2, Catalog.GetByRowId(65621)!.StepCount);
        Assert.Equal(12, Catalog.GetByRowId(Shadowbringers)!.StepCount);
        Assert.Equal(5, Catalog.GetByRowId(GoneButNotForgiven)!.StepCount);
        Assert.All(Catalog.All, q => Assert.True(q.StepCount <= 24, q.Name));
    }
}
