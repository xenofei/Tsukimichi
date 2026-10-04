using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Unlocks;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The wider spoiler shield (plan v7, 1.20.0 N6) over the installed game: Kugane, the Sirensong Sea, Hancock and a
/// Stormblood story reward hide for a character before Stormblood and show once the story reaches them; Dawntrail's
/// city likewise; search and find by unlock never name what the shield hides.
/// </summary>
public sealed class SpoilerNamesGameDataTests(UnlockIndexFixture fixture, ITestOutputHelper output) : IClassFixture<UnlockIndexFixture>
{
    private const uint NotWithoutIncident = 68005;
    private const byte Heavensward = 1;
    private const byte Stormblood = 2;
    private const byte Shadowbringers = 3;
    private const byte Endwalker = 4;
    private const byte Dawntrail = 5;

    private QuestCatalog Catalog => fixture.Catalog;

    private QuestUnlocks Index => fixture.Unlocks;

    private IReadOnlyList<QuestRecord> Story => MsqGraph.For(Catalog).Story;

    /// <summary>The first main scenario quest of <paramref name="expansion"/> in story order.</summary>
    private int FirstOf(byte expansion)
    {
        for (var i = 0; i < Story.Count; i++)
        {
            if (Story[i].Expansion == expansion)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"no main scenario quest of expansion {expansion}");
    }

    private int IndexOf(uint rowId)
    {
        for (var i = 0; i < Story.Count; i++)
        {
            if (Story[i].RowId == rowId)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"{rowId} is no main scenario quest");
    }

    /// <summary>A character whose next main scenario quest is the one at <paramref name="next"/>: everything before it done, nothing else.</summary>
    private SpoilerMask At(int next)
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

        return SpoilerMask.Build(Catalog, states, SpoilerOptions.Default, names: Index.Names);
    }

    [GameDataFact]
    public void The_names_are_placed_from_the_data()
    {
        var names = Index.Names;
        output.WriteLine($"{names.Count} names placed in the story");
        Assert.True(names.Count > 2_000, $"names placed: {names.Count}");
        Assert.Equal(NotWithoutIncident, names.AnchorOf(NotWithoutIncident));
        Assert.True(names.TryGet(SpoilerKind.Area, "Kugane", out var kugane));
        output.WriteLine($"Kugane: anchored at {string.Join(", ", kugane.Anchors)}, Lv {kugane.Level}");
    }

    [GameDataFact]
    public void Kugane_hides_before_stormblood_and_shows_once_the_story_opens_it()
    {
        var before = At(FirstOf(Stormblood));
        Assert.True(before.IsMasked(NotWithoutIncident), "Not without Incident lies past the three names ahead");
        Assert.True(before.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.StartsWith("Area ahead (Lv ", before.Name(SpoilerKind.Area, "Kugane"), StringComparison.Ordinal);
        // A city the character has already been through never hides.
        Assert.False(before.IsNameMasked(SpoilerKind.Area, "Limsa Lominsa Lower Decks"));
        Assert.False(before.IsNameMasked(SpoilerKind.Area, "Ishgard - Foundation"));

        var after = At(IndexOf(NotWithoutIncident) + 1);
        Assert.False(after.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.Equal("Kugane", after.Name(SpoilerKind.Area, "Kugane"));
    }

    [GameDataFact]
    public void A_stormblood_duty_and_person_hide_until_the_story_reaches_them()
    {
        var before = At(FirstOf(Stormblood));
        var after = At(FirstOf(Shadowbringers));

        Assert.True(before.IsNameMasked(SpoilerKind.Duty, "The Sirensong Sea"));
        Assert.StartsWith("Duty ahead (Lv ", before.Name(SpoilerKind.Duty, "The Sirensong Sea"), StringComparison.Ordinal);
        Assert.False(after.IsNameMasked(SpoilerKind.Duty, "The Sirensong Sea"));

        Assert.True(before.IsNameMasked(SpoilerKind.Npc, "Hancock"));
        Assert.False(after.IsNameMasked(SpoilerKind.Npc, "Hancock"));
        // Someone met in A Realm Reborn is never hidden.
        Assert.False(before.IsNameMasked(SpoilerKind.Npc, "Alphinaud"));
    }

    [GameDataFact]
    public void A_reward_only_a_masked_story_quest_gives_is_masked_until_it_is_done()
    {
        var before = At(FirstOf(Stormblood));
        var givers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var quest in Catalog.All)
        {
            foreach (var reward in quest.Rewards)
            {
                if (reward.Name.Length > 0)
                {
                    givers[reward.Name] = givers.GetValueOrDefault(reward.Name) + 1;
                }
            }
        }

        var checkedOne = false;
        for (var i = FirstOf(Stormblood); i < FirstOf(Shadowbringers) && !checkedOne; i++)
        {
            var quest = Story[i];
            if (!before.IsMasked(quest))
            {
                continue;
            }

            foreach (var reward in quest.Rewards)
            {
                if (reward.Name.Length == 0 || givers[reward.Name] != 1)
                {
                    continue;
                }

                output.WriteLine($"{quest.RowId} {quest.Name}: {reward.Name}");
                Assert.True(before.IsNameMasked(SpoilerKind.Reward, reward.Name), reward.Name);
                Assert.False(At(i + 1).IsNameMasked(SpoilerKind.Reward, reward.Name), reward.Name);
                checkedOne = true;
                break;
            }
        }

        Assert.True(checkedOne, "a Stormblood story quest gives a reward no other quest gives");
    }

    [GameDataFact]
    public void Search_and_find_by_unlock_never_name_a_masked_place()
    {
        var before = At(FirstOf(Stormblood));
        var after = At(IndexOf(NotWithoutIncident) + 1);

        Assert.DoesNotContain(Index.Find("kugane", id => !before.IsMasked(id), spoilers: before), m => m.Find.Target == UnlockTarget.Zone && m.Find.Name == "Kugane");
        Assert.Contains(Index.Find("kugane", id => !after.IsMasked(id), spoilers: after), m => m.Find.Target == UnlockTarget.Zone && m.Find.Name == "Kugane");

        // No quest the shield shows is found by "kugane" through what it opens or rewards, unless its own name says it.
        var search = SearchIndex.For(Catalog);
        foreach (var quest in Catalog.All)
        {
            if (quest.IsRemoved || before.IsMasked(quest) || quest.Name.Contains("kugane", StringComparison.OrdinalIgnoreCase) || quest.InternalId.Contains("kugane", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Assert.False(search.Matches(quest.RowId, "kugane", before, Index), $"{quest.RowId} {quest.Name} is found by a masked name");
        }
    }

    [GameDataFact]
    public void Dawntrails_city_hides_before_dawntrail_by_the_same_rule()
    {
        // Nothing names Dawntrail in the code: the city is placed by the quests that open it, as Evercold's will be.
        var city = Index.For(Story[FirstOf(Dawntrail)].RowId).Concat(Story.Skip(FirstOf(Dawntrail)).Take(20).SelectMany(q => Index.For(q.RowId)))
            .FirstOrDefault(e => e.Target == UnlockTarget.Zone && e.Expansion == Dawntrail);
        Assert.NotNull(city);
        output.WriteLine($"Dawntrail's first zone: {city.Name}");

        var before = At(FirstOf(Endwalker) + 10);
        Assert.True(before.IsNameMasked(SpoilerKind.Area, city.Name));
        Assert.False(At(Story.Count).IsNameMasked(SpoilerKind.Area, city.Name));

        // Every Dawntrail zone the story opens hides from a Heavensward character.
        var heavensward = At(FirstOf(Heavensward));
        foreach (var quest in Story.Skip(FirstOf(Dawntrail)))
        {
            foreach (var entry in Index.For(quest.RowId))
            {
                if (entry.Target == UnlockTarget.Zone && entry.Expansion == Dawntrail)
                {
                    Assert.True(heavensward.IsNameMasked(SpoilerKind.Area, entry.Name), entry.Name);
                }
            }
        }
    }
}
