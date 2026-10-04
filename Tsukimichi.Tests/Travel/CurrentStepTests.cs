using Tsukimichi.Core.Travel;

namespace Tsukimichi.Tests.Travel;

/// <summary>
/// The current step of a quest in the journal (plan v7, 1.21.0 P2): only the current sequence's objectives are read,
/// so a later step is never named or aimed at; places inside a duty are not travelled to; several places aim at the
/// nearest in the player's zone; a step with no place says so.
/// </summary>
public sealed class CurrentStepTests
{
    private const uint Tuliyollal = 1185;
    private const uint Shaaloani = 1190;
    private const uint TheAery = 1065;

    private static StepPlace Npc(uint level, uint territory, float x, float z, string name) =>
        new(level, territory, 1, x, 0f, z, 1f, false, 1000 + level, name);

    private static StepPlace Area(uint level, uint territory, float x, float z, uint duty = 0) =>
        new(level, territory, 1, x, 0f, z, 50f, true, 0, string.Empty, duty);

    /// <summary>The Long Road to Xak Tural's shape: steps 1 to 4, then the last (255).</summary>
    private static readonly QuestSteps LongRoad = new(70448,
    [
        new StepObjective(0, 1, 1, [Npc(1, Tuliyollal, 10f, 10f, "Landsguard gate sentry")]),
        new StepObjective(1, 2, 1, [Area(2, Shaaloani, 50f, 50f)]),
        new StepObjective(2, 3, 1, [Npc(3, Shaaloani, 100f, 100f, "Erenville")]),
        new StepObjective(3, 4, 1, [Npc(4, Shaaloani, 90f, 60f, "Erenville")]),
        new StepObjective(4, 255, 1, [Npc(5, Shaaloani, 120f, 40f, "Erenville")]),
    ]);

    [Fact]
    public void The_current_step_names_its_own_objective_and_place_only()
    {
        var step = CurrentStepResolver.Resolve(LongRoad, 3, 5);
        Assert.NotNull(step);
        Assert.Equal(3, step.Step);
        Assert.Equal(StepPlaceKind.OnePlace, step.Kind);
        Assert.Equal(3u, step.Place?.LevelId);
        Assert.Equal("Erenville", step.Place?.NpcName);
        var objective = Assert.Single(step.Objectives);
        Assert.Equal(2, objective.Index);
        Assert.Equal(0, step.MoreObjectives);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(255)]
    public void A_later_step_is_never_read(byte sequence)
    {
        var step = CurrentStepResolver.Resolve(LongRoad, sequence, 5);
        Assert.NotNull(step);
        Assert.All(step.Objectives, o => Assert.Equal(sequence, o.Sequence));
        var levels = LongRoad.Objectives.Where(o => o.Sequence == sequence).SelectMany(o => o.Places).Select(p => p.LevelId).ToHashSet();
        Assert.Contains(step.Place!.LevelId, levels);
    }

    [Fact]
    public void The_last_sequence_is_the_last_step()
    {
        var step = CurrentStepResolver.Resolve(LongRoad, 255, 5);
        Assert.Equal(5, step?.Step);
        Assert.Equal(5u, step?.Place?.LevelId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((byte)0)]
    public void No_sequence_resolves_nothing(byte? sequence)
    {
        Assert.Null(CurrentStepResolver.Resolve(LongRoad, sequence, 5));
    }

    [Fact]
    public void A_marked_area_is_an_area()
    {
        var step = CurrentStepResolver.Resolve(LongRoad, 2, 5);
        Assert.Equal(StepPlaceKind.Area, step?.Kind);
        Assert.True(step?.HasPlace);
    }

    [Fact]
    public void Several_places_aim_at_the_nearest_in_the_players_zone()
    {
        var steps = new QuestSteps(1,
        [
            new StepObjective(0, 2, 1, [Npc(10, Shaaloani, 0f, 0f, "A")]),
            new StepObjective(1, 2, 1, [Npc(11, Shaaloani, 200f, 0f, "B")]),
            new StepObjective(2, 2, 1, [Npc(12, Tuliyollal, 5f, 5f, "C")]),
        ]);

        var near = CurrentStepResolver.Resolve(steps, 2, 3, Shaaloani, 190f, 10f);
        Assert.Equal(StepPlaceKind.SeveralPlaces, near?.Kind);
        Assert.Equal(3, near?.PlaceCount);
        Assert.Equal(11u, near?.Place?.LevelId);
        Assert.Equal(1, near?.Objective?.Index);
        Assert.Equal(2, near?.MoreObjectives);

        // Elsewhere, or with no position, the first in the data's order.
        Assert.Equal(10u, CurrentStepResolver.Resolve(steps, 2, 3)?.Place?.LevelId);
        Assert.Equal(12u, CurrentStepResolver.Resolve(steps, 2, 3, Tuliyollal, 900f, 900f)?.Place?.LevelId);
    }

    [Fact]
    public void Choices_are_built_once_and_the_nearest_is_picked_without_allocating()
    {
        // 1.21.0 review: the detail pane and Up next re-aim at the nearest place every frame; the lists, the set and the
        // step records are built once per quest and sequence, and the per-frame pick allocates nothing.
        var steps = new QuestSteps(1,
        [
            new StepObjective(0, 2, 1, [Npc(10, Shaaloani, 0f, 0f, "A")]),
            new StepObjective(1, 2, 1, [Npc(11, Shaaloani, 200f, 0f, "B")]),
            new StepObjective(2, 2, 1, [Npc(12, Tuliyollal, 5f, 5f, "C")]),
        ]);

        var choices = CurrentStepResolver.Choices(steps, 2, 3);
        Assert.NotNull(choices);
        Assert.Equal(3, choices.Places.Count);
        Assert.Equal(CurrentStepResolver.Resolve(steps, 2, 3, Shaaloani, 190f, 10f)?.Place, choices.Pick(Shaaloani, 190f, 10f).Place);
        Assert.Equal(11u, choices.Pick(Shaaloani, 190f, 10f).Place?.LevelId);
        Assert.Same(choices.Pick(Shaaloani, 190f, 10f), choices.Pick(Shaaloani, 180f, 0f));
        Assert.Equal(10u, choices.Pick().Place?.LevelId);

        choices.Pick(Shaaloani, 1f, 1f);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            choices.Pick(Shaaloani, i * 2f, 0f);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Null(CurrentStepResolver.Choices(steps, 0, 3));
    }

    [Fact]
    public void A_step_inside_a_duty_is_a_duty_with_no_place()
    {
        var steps = new QuestSteps(67170,
        [
            new StepObjective(0, 1, 1, [Npc(20, Tuliyollal, 0f, 0f, "Cid")]),
            new StepObjective(1, 2, 1, [Area(21, TheAery, 0f, 0f, duty: 39)]),
        ]);

        var step = CurrentStepResolver.Resolve(steps, 2, 3);
        Assert.Equal(StepPlaceKind.Duty, step?.Kind);
        Assert.Equal(39u, step?.DutyId);
        Assert.Null(step?.Place);
        Assert.False(step?.HasPlace);
    }

    [Fact]
    public void A_step_with_no_place_says_so()
    {
        var steps = new QuestSteps(66677,
        [
            new StepObjective(0, 1, 1, []),
            new StepObjective(1, 255, 1, [Npc(30, 156, 0f, 0f, "Brangwine")]),
        ]);

        var step = CurrentStepResolver.Resolve(steps, 1, 2);
        Assert.Equal(StepPlaceKind.NoPlace, step?.Kind);
        Assert.Null(step?.Place);
        Assert.Single(step!.Objectives);
    }

    [Fact]
    public void A_sequence_the_data_does_not_hold_has_no_objective_and_no_place()
    {
        var step = CurrentStepResolver.Resolve(LongRoad, 9, 5);
        Assert.NotNull(step);
        Assert.Empty(step.Objectives);
        Assert.Null(step.Objective);
        Assert.Equal(StepPlaceKind.NoPlace, step.Kind);
    }
}
