using Tsukimichi.Core.Model;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The Far Shore's places over the installed game (the owner's decision: Moonfall follows Tsukimichi's spoiler shield):
/// every area a Far Shore stage or a shipped scene is set in is one the shield places, in the expansion it is tagged
/// with; it is hidden for a character whose story stops short of that expansion and shown once the story has passed it;
/// "Reveal this name" opens it; and the placeholder Moonfall prints never holds the area's name.
/// </summary>
public sealed class MoonfallShieldGameDataTests(UnlockIndexFixture fixture, ITestOutputHelper output) : IClassFixture<UnlockIndexFixture>
{
    private QuestCatalog Catalog => fixture.Catalog;

    private IReadOnlyList<QuestRecord> Story => MsqGraph.For(Catalog).Story;

    /// <summary>Every area Moonfall tags, with its era.</summary>
    private static IEnumerable<(string Zone, byte Era, string Where)> Places()
    {
        foreach (var stage in MoonfallStages.Of(MoonfallCampaignKind.Expansion))
        {
            if (MoonfallPlaces.OfStage(stage) is { Zone: { } zone } place)
            {
                yield return (zone, place.Era, "Far Shore stage " + stage.Number);
            }
        }

        foreach (var (name, place) in MoonfallPlaces.Scenes)
        {
            if (place.Zone is { } zone)
            {
                yield return (zone, place.Era, "scene " + name);
            }
        }
    }

    /// <summary>The first main scenario quest of <paramref name="expansion"/> in story order; the story's length past the last.</summary>
    private int FirstOf(byte expansion)
    {
        for (var i = 0; i < Story.Count; i++)
        {
            if (Story[i].Expansion >= expansion)
            {
                return i;
            }
        }

        return Story.Count;
    }

    /// <summary>A character whose next main scenario quest is the one at <paramref name="next"/>: everything before it done.</summary>
    private SpoilerMask At(int next, SpoilerOptions? options = null, IEnumerable<(SpoilerKind Kind, string Name)>? revealedNames = null)
    {
        var states = new Dictionary<uint, QuestState>();
        foreach (var quest in Catalog.All)
        {
            states[quest.RowId] = QuestState.Blocked;
        }

        for (var i = 0; i < Story.Count; i++)
        {
            states[Story[i].RowId] = i < next ? QuestState.Completed : i == next ? QuestState.Ready : QuestState.Blocked;
        }

        return SpoilerMask.Build(Catalog, states, options ?? SpoilerOptions.Default, null, names: fixture.Unlocks.Names, revealedNames: revealedNames);
    }

    [GameDataFact]
    public void Every_tagged_place_is_an_area_the_shield_places_in_its_tagged_expansion()
    {
        var problems = new List<string>();
        foreach (var (zone, era, where) in Places())
        {
            if (!fixture.Unlocks.Names.TryGet(SpoilerKind.Area, zone, out var placed))
            {
                problems.Add($"{where}: the shield does not place \"{zone}\"");
            }
            else if (placed.Expansion != era)
            {
                problems.Add($"{where}: \"{zone}\" is expansion {placed.Expansion}, tagged {era}");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [GameDataFact]
    public void Each_place_is_hidden_before_its_era_and_shown_after_it()
    {
        var problems = new List<string>();
        var strict = SpoilerOptions.Default with { Ahead = 0 };
        foreach (var (zone, era, where) in Places())
        {
            // Just before the era's story: the last quest of the one before is next, and nothing ahead is shown.
            if (era > MoonfallPlaces.ARealmReborn && !At(FirstOf(era) - 1, strict).IsNameMasked(SpoilerKind.Area, zone))
            {
                problems.Add($"{where}: \"{zone}\" shows before {era}'s story");
            }

            // Once the story has passed the era: shown.
            if (At(FirstOf((byte)(era + 1))).IsNameMasked(SpoilerKind.Area, zone))
            {
                problems.Add($"{where}: \"{zone}\" is still hidden after {era}'s story");
            }
        }

        output.WriteLine($"{Places().Count()} places checked");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [GameDataFact]
    public void The_far_shore_through_the_real_shield_veils_endwalkers_stages_until_their_story_or_a_reveal()
    {
        var far = MoonfallStages.Of(MoonfallCampaignKind.Expansion);
        var beforeEndwalker = At(FirstOf(MoonfallPlaces.Endwalker) - 1, SpoilerOptions.Default with { Ahead = 0 });
        var modes = new MoonfallModes(MoonfallCampaigns.LoadBuiltIn(), new MoonfallProgress(), MoonfallStory.Everyone, [])
        {
            Shield = MoonfallShield.FromMask(beforeEndwalker),
        };

        foreach (var number in (int[])[9, 10, 12])
        {
            var stage = far[number - 1];
            Assert.True(modes.StageVeiled(stage), stage.Name);
            var shown = modes.StageName(stage);
            output.WriteLine($"{stage.Name} -> {shown}");
            Assert.NotEqual(stage.Name, shown);
            Assert.DoesNotContain(MoonfallPlaces.OfStage(stage).Zone!, shown, StringComparison.OrdinalIgnoreCase);
            Assert.True(SpoilerMask.IsPlaceholder(shown) || SpoilerMask.HoldsPlaceholder(shown), shown);
        }

        // "Reveal this name" on Old Sharlayan opens the Domes of Sharlayan and nothing else.
        var revealed = At(FirstOf(MoonfallPlaces.Endwalker) - 1, SpoilerOptions.Default with { Ahead = 0 }, [(SpoilerKind.Area, "Old Sharlayan")]);
        var after = new MoonfallModes(MoonfallCampaigns.LoadBuiltIn(), new MoonfallProgress(), MoonfallStory.Everyone, [])
        {
            Shield = MoonfallShield.FromMask(revealed),
        };
        Assert.False(after.StageVeiled(far[8]));
        Assert.True(after.StageVeiled(far[9]));

        // Past Endwalker's story, none of them is veiled.
        var done = new MoonfallModes(MoonfallCampaigns.LoadBuiltIn(), new MoonfallProgress(), MoonfallStory.Everyone, [])
        {
            Shield = MoonfallShield.FromMask(At(FirstOf(MoonfallPlaces.Dawntrail))),
        };
        Assert.All(far, stage => Assert.False(done.StageVeiled(stage), stage.Name));
    }
}
