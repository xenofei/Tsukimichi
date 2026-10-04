using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Text;
using Tsukimichi.Core.Travel;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The current step against the installed game (plan v7, 1.21.0 P2): which sheets and fields give it, on the quests
/// spec-1.21 draws. <c>Quest.TodoParams[i]</c> is objective <c>i</c>: its <c>ToDoCompleteSeq</c> is the step, its
/// <c>ToDoLocation</c> Level rows the places (<c>Level.Territory</c>, <c>Map</c>, <c>X</c>/<c>Z</c>, <c>Type</c> 8 a
/// person whose <c>Object</c> is the ENpcResident), and the quest text sheet's <c>TEXT_&lt;ID&gt;_TODO_i</c> its words.
/// Assertions are about structure, places and coordinates, never the game's text.
/// </summary>
public sealed class QuestStepGameDataTests(UnlockIndexFixture fixture, ITestOutputHelper output) : IClassFixture<UnlockIndexFixture>
{
    private const uint TheLongRoadToXakTural = 70448;
    private const uint GiftsForTheOutcasts = 67148;
    private const uint IntoTheAery = 67170;
    private const uint MorbidMotivation = 66677;

    private const uint MorDhona = 156;
    private const uint TheDravanianForelands = 398;
    private const uint TheChurningMists = 400;
    private const uint TheAery = 1065;
    private const uint Tuliyollal = 1185;
    private const uint Shaaloani = 1190;

    private QuestCatalog Catalog => fixture.Catalog;

    private QuestSteps Steps(uint rowId) => QuestStepReader.Read(fixture.Game.Excel, rowId, Language.English);

    private CurrentStep Resolve(uint rowId, byte sequence)
    {
        var quest = Catalog.GetByRowId(rowId);
        Assert.NotNull(quest);
        var step = CurrentStepResolver.Resolve(Steps(rowId), sequence, quest.StepCount);
        Assert.NotNull(step);
        return step;
    }

    /// <summary>The map coordinates of a place, as the game's map prints them.</summary>
    private (float X, float Y) MapXY(StepPlace place)
    {
        var map = fixture.Game.Excel.GetSheet<Map>(Language.English).GetRow(place.MapId);
        return (ItemSourceIndex.MapCoordinate(place.X, map.OffsetX, map.SizeFactor), ItemSourceIndex.MapCoordinate(place.Z, map.OffsetY, map.SizeFactor));
    }

    private void AssertAt(StepPlace? place, uint territory, float x, float y)
    {
        Assert.NotNull(place);
        Assert.Equal(territory, place.TerritoryId);
        var (mx, my) = MapXY(place);
        output.WriteLine($"level {place.LevelId}: territory {place.TerritoryId}, X {mx:0.0}, Y {my:0.0}, area {place.IsArea}, npc '{place.NpcName}'");
        Assert.InRange(mx, x - 0.06f, x + 0.06f);
        Assert.InRange(my, y - 0.06f, y + 0.06f);
    }

    [GameDataFact]
    public void The_long_road_step_three_is_erenville_in_shaaloani()
    {
        var step = Resolve(TheLongRoadToXakTural, 3);
        Assert.Equal(3, step.Step);
        Assert.Equal(StepPlaceKind.OnePlace, step.Kind);
        Assert.Equal("Erenville", step.Place?.NpcName);
        AssertAt(step.Place, Shaaloani, 27.0f, 34.8f);

        // Objective 2 is step 3's, and its text is the quest text sheet's TODO_02 for sequence 3.
        Assert.Equal(2, step.Objective?.Index);
        var quest = Catalog.GetByRowId(TheLongRoadToXakTural)!;
        var sequences = QuestTextReader.ObjectiveSequences(fixture.Game.Excel, quest.RowId, Language.English);
        var text = QuestTextReader.Read(QuestTextFiles.From(fixture.Game), quest.InternalId, Language.English, sequences);
        Assert.NotNull(text);
        var line = Assert.Single(text.Objectives, l => l.Sequence == 3);
        Assert.Equal(2, line.Index);
        Assert.False(line.Text.IsEmpty);

        // The giver is elsewhere: in Tuliyollal.
        Assert.Equal(Tuliyollal, quest.Issuer?.TerritoryId);
    }

    [GameDataFact]
    public void The_long_road_never_names_a_later_step()
    {
        var all = Steps(TheLongRoadToXakTural);
        foreach (var sequence in all.Objectives.Select(o => o.Sequence).Distinct())
        {
            var step = Resolve(TheLongRoadToXakTural, sequence);
            Assert.All(step.Objectives, o => Assert.Equal(sequence, o.Sequence));
            if (step.Place is { } place)
            {
                Assert.Contains(all.Objectives.Where(o => o.Sequence == sequence).SelectMany(o => o.Places), p => p.LevelId == place.LevelId);
            }
        }
    }

    [GameDataFact]
    public void Gifts_for_the_outcasts_step_two_is_an_area_in_the_forelands()
    {
        var step = Resolve(GiftsForTheOutcasts, 2);
        Assert.Equal(StepPlaceKind.Area, step.Kind);
        Assert.Equal((byte)3, step.Objective?.Quantity);
        Assert.True(step.Place?.Radius > 50f);
        AssertAt(step.Place, TheDravanianForelands, 28.8f, 22.3f);
    }

    [GameDataFact]
    public void Into_the_aery_step_two_happens_inside_the_duty_and_step_three_at_its_door()
    {
        var duty = Resolve(IntoTheAery, 2);
        Assert.Equal(StepPlaceKind.Duty, duty.Kind);
        Assert.Null(duty.Place);
        var cfc = fixture.Game.Excel.GetSheet<ContentFinderCondition>(Language.English).GetRow(duty.DutyId);
        Assert.Equal(TheAery, cfc.TerritoryType.RowId);

        var door = Resolve(IntoTheAery, 3);
        Assert.Equal(StepPlaceKind.Area, door.Kind);
        AssertAt(door.Place, TheChurningMists, 33.7f, 15.5f);
    }

    [GameDataFact]
    public void Morbid_motivation_step_one_has_no_place_and_aims_at_the_giver()
    {
        var step = Resolve(MorbidMotivation, 1);
        Assert.Equal(StepPlaceKind.NoPlace, step.Kind);
        Assert.False(step.HasPlace);
        Assert.Single(step.Objectives);
        var quest = Catalog.GetByRowId(MorbidMotivation)!;
        Assert.Equal("Brangwine", quest.Issuer?.Name);
        Assert.Equal(MorDhona, quest.Issuer?.TerritoryId);
    }

    [GameDataFact]
    public void Every_quests_steps_resolve_to_their_own_objectives()
    {
        var kinds = new Dictionary<StepPlaceKind, int>();
        var steps = 0;
        foreach (var quest in Catalog.All)
        {
            var all = Steps(quest.RowId);
            foreach (var sequence in all.Objectives.Select(o => o.Sequence).Distinct())
            {
                var step = CurrentStepResolver.Resolve(all, sequence, quest.StepCount);
                Assert.NotNull(step);
                Assert.All(step.Objectives, o => Assert.Equal(sequence, o.Sequence));
                if (quest.StepCount > 0)
                {
                    Assert.InRange(step.Step, 1, (int)quest.StepCount);
                }
                if (step.Place is { } place)
                {
                    Assert.Equal(0u, place.DutyId);
                    Assert.NotEqual(0u, place.TerritoryId);
                }

                kinds[step.Kind] = kinds.GetValueOrDefault(step.Kind) + 1;
                steps++;
            }
        }

        output.WriteLine($"{steps} steps: " + string.Join(", ", kinds.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}")));
        Assert.True(steps > 10_000, $"steps: {steps}");
        Assert.True(kinds.GetValueOrDefault(StepPlaceKind.OnePlace) > kinds.GetValueOrDefault(StepPlaceKind.NoPlace));
    }

    [GameDataFact]
    public void The_step_zone_is_masked_before_the_story_reaches_it()
    {
        var story = MsqGraph.For(Catalog).Story;
        var endwalker = story.ToList().FindIndex(q => q.Expansion == 4);
        var longRoad = story.ToList().FindIndex(q => q.RowId == TheLongRoadToXakTural);
        Assert.True(endwalker >= 0 && longRoad > endwalker);

        var shaaloani = fixture.Game.Excel.GetSheet<TerritoryType>(Language.English).GetRow(Shaaloani).PlaceName.Value.Name.ExtractText();
        Assert.True(At(endwalker).IsNameMasked(SpoilerKind.Area, shaaloani));
        Assert.False(At(longRoad + 1).IsNameMasked(SpoilerKind.Area, shaaloani));

        // A chat line given a hidden zone (null) speaks of it in words, never by name.
        var line = GuidanceText.NextInJournal(SpoilerMask.Placeholder(Catalog.GetByRowId(TheLongRoadToXakTural)!), 0, 3, null, new GuidancePlace(null, 27f, 34.8f), null);
        Assert.DoesNotContain(shaaloani, line.Text, StringComparison.Ordinal);
        Assert.Contains("a zone ahead of your story", line.Text, StringComparison.Ordinal);
    }

    /// <summary>A character whose next main scenario quest is the one at <paramref name="next"/>.</summary>
    private SpoilerMask At(int next)
    {
        var story = MsqGraph.For(Catalog).Story;
        var states = new Dictionary<uint, QuestState>();
        foreach (var quest in Catalog.All)
        {
            states[quest.RowId] = QuestState.Blocked;
        }

        for (var i = 0; i < story.Count; i++)
        {
            states[story[i].RowId] = i < next ? QuestState.Completed : i == next ? QuestState.Ready : QuestState.Blocked;
        }

        return SpoilerMask.Build(Catalog, states, SpoilerOptions.Default, names: fixture.Unlocks.Names);
    }
}
