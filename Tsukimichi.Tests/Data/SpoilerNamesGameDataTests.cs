using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Unlocks;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The wider spoiler shield (plan v7, 1.20.0 N6) over the installed game: Kugane, the Sirensong Sea, Hancock and a
/// Stormblood story reward hide for a character before Stormblood and show once the story reaches them, under the
/// spec's placeholders ("Stormblood area 2", "Dungeon (Lv 61)", "Stormblood character"); Dawntrail's areas are numbered
/// in the game's own TerritoryType order and named from its ExVersion sheet; search and find by unlock never name what
/// the shield hides.
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

    private static readonly SpoilerKind[] Kinds = [SpoilerKind.Area, SpoilerKind.Aetheryte, SpoilerKind.Duty, SpoilerKind.Reward, SpoilerKind.Npc];

    /// <summary>A character whose next main scenario quest is the one at <paramref name="next"/>: everything before it done, nothing else.</summary>
    private SpoilerMask At(int next, SpoilerOptions? options = null, IReadOnlySet<uint>? revealed = null, IEnumerable<(SpoilerKind Kind, string Name)>? revealedNames = null)
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

        return SpoilerMask.Build(Catalog, states, options ?? SpoilerOptions.Default, revealed, names: Index.Names, revealedNames: revealedNames);
    }

    [GameDataFact]
    public void No_wotsit_item_names_what_the_shield_hides_at_the_start_of_stormblood()
    {
        // A duty unlock is placed as a duty and an aether current as flying in its zone, not as a reward: Wotsit once
        // registered Doma Castle, Ala Mhigo and "Aether Current (Yanxia)" for a character before Stormblood.
        var before = At(FirstOf(Stormblood));
        var items = WotsitOrder.Items(Catalog, fixture.Rewards, k => k.ToString(), "English", before);
        var leaks = new List<string>();
        foreach (var item in items)
        {
            if (item.Reward is null)
            {
                // A quest's own name is the quest shield's (a side quest may share a duty's name).
                continue;
            }

            var name = item.Name;
            var open = name.IndexOf('(', StringComparison.Ordinal);
            var close = name.LastIndexOf(')');
            var inner = open >= 0 && close > open ? name[(open + 1)..close].Trim() : null;
            foreach (var kind in Kinds)
            {
                if (before.IsNameMasked(kind, name) || (item.Reward.Kind == Core.Model.RewardKind.AetherCurrent && before.IsNameMasked(kind, inner)))
                {
                    leaks.Add($"{kind}: {name} [{item.Reward.Kind}]");
                }
            }
        }

        output.WriteLine($"{items.Count} items; {leaks.Count} leak: {string.Join(" | ", leaks.Take(20))}");
        Assert.Empty(leaks);
        Assert.DoesNotContain(items, i => i.Name is "Doma Castle" or "Ala Mhigo" or "Bardam's Mettle" or "Aether Current (Yanxia)");
        Assert.Contains(WotsitOrder.Items(Catalog, fixture.Rewards, k => k.ToString(), "English", At(FirstOf(Shadowbringers))), i => i.Name == "Doma Castle");
    }

    [GameDataFact]
    public void Revealing_a_story_quests_names_reveals_its_own_names_and_nothing_else()
    {
        var at = FirstOf(Stormblood);
        var before = At(at);
        var quest = Story.Skip(at).First(q => before.IsMasked(q) && q.Name == "Stormblood");
        var names = SpoilerNames.NamesIn(quest, Index);
        var revealed = At(at, revealed: new HashSet<uint> { quest.RowId }, revealedNames: names);
        var titleOnly = At(at, revealed: new HashSet<uint> { quest.RowId });

        Assert.False(revealed.IsMasked(quest));
        foreach (var (kind, name) in names)
        {
            Assert.False(revealed.IsNameMasked(kind, name), $"{kind}: {name}");
        }

        // A reveal is no story progress: what the quest's place in the story would introduce stays hidden.
        var ownSet = names.ToHashSet(SpoilerNames.NameComparer);
        var widened = new List<string>();
        foreach (var kind in Kinds)
        {
            foreach (var (name, placed) in Index.Names.All(kind))
            {
                // An aetheryte follows its area: one in an area the quest names is the quest's own.
                var own = ownSet.Contains((kind, name)) || (placed.Zone is { } zone && ownSet.Contains((SpoilerKind.Area, zone)));
                if (before.IsNameMasked(kind, name) && !own && (!revealed.IsNameMasked(kind, name) || !titleOnly.IsNameMasked(kind, name)))
                {
                    widened.Add($"{kind}: {name}");
                }
            }
        }

        output.WriteLine($"'{quest.Name}' reveals {names.Count} names; {widened.Count} others: {string.Join(" | ", widened.Take(20))}");
        Assert.Empty(widened);

        // The names-ahead slider still moves the story: with ten quests ahead, the next ones' names show.
        Assert.True(At(at, SpoilerOptions.Default with { Ahead = SpoilerOptions.MaxAhead }).MaskedNameCount < before.MaskedNameCount);
    }

    [GameDataFact]
    public void A_later_expansions_place_or_duty_hides_however_early_the_quest_that_opens_it()
    {
        // The Boards of the Unbroken and Shinryu's Domain (Unreal) are Dawntrail duties opened by an earlier quest.
        foreach (var at in new[] { FirstOf(Stormblood), FirstOf(Endwalker) })
        {
            var mask = At(at);
            var shown = new List<string>();
            foreach (var kind in new[] { SpoilerKind.Area, SpoilerKind.Aetheryte, SpoilerKind.Duty })
            {
                foreach (var (name, placed) in Index.Names.All(kind))
                {
                    if (placed.Expansion != byte.MaxValue && placed.Expansion > mask.ReachExpansion && !mask.IsNameMasked(kind, name))
                    {
                        shown.Add($"{kind}: {name} (ex {placed.Expansion})");
                    }
                }
            }

            output.WriteLine($"at {Story[at].Name}: {string.Join(" | ", shown)}");
            Assert.Empty(shown);
        }

        Assert.True(At(FirstOf(Endwalker)).IsNameMasked(SpoilerKind.Duty, "First Board of the Unbroken"));

        // "Names ahead" across an expansion's end still shows the next one's places: the last Endwalker quest with
        // ten ahead shows the zone Dawntrail's first quests open.
        var last = FirstOf(Dawntrail) - 1;
        var opened = Story.Skip(FirstOf(Dawntrail)).Take(3).SelectMany(q => Index.For(q.RowId)).First(e => e.Target == UnlockTarget.Zone && e.Expansion == Dawntrail);
        Assert.True(At(last, SpoilerOptions.Default with { Ahead = 0 }).IsNameMasked(SpoilerKind.Area, opened.Name));
        Assert.False(At(last, SpoilerOptions.Default with { Ahead = SpoilerOptions.MaxAhead }).IsNameMasked(SpoilerKind.Area, opened.Name), opened.Name);
    }

    [GameDataFact]
    public void The_plans_other_unlocks_are_shielded_as_duties_when_they_are_duties()
    {
        // "The Final Verse (Quantum)" is a duty the plan files as Other: it once printed in full before Stormblood.
        var before = At(FirstOf(Stormblood));
        var featureIds = Catalog.All.Select(q => q.RowId).ToHashSet();
        var tags = UnlockTags.Build(Catalog, featureIds, fixture.Rewards, fixture.Duties);
        var plan = UnlockPlan.Build(tags, new Dictionary<uint, QuestEvaluation>(), BlockerNames.Default with { Catalog = Catalog }, spoilers: before);
        var leaks = new List<string>();
        foreach (var entry in plan.Entries)
        {
            foreach (var unlock in entry.Unlocks)
            {
                foreach (var kind in Kinds)
                {
                    if (unlock.Name.Length > 0 && before.IsNameMasked(kind, unlock.Name))
                    {
                        leaks.Add($"{unlock.Kind} {kind}: {unlock.Name}");
                    }
                }
            }
        }

        output.WriteLine(string.Join(" | ", leaks.Take(20)));
        Assert.Empty(leaks);
    }

    [GameDataFact]
    public void The_names_are_placed_from_the_data()
    {
        var names = Index.Names;
        output.WriteLine($"{names.Count} names placed in the story");
        Assert.True(names.Count > 2_000, $"names placed: {names.Count}");
        Assert.Equal(NotWithoutIncident, names.AnchorOf(NotWithoutIncident));
        Assert.True(names.TryGet(SpoilerKind.Area, "Kugane", out var kugane));
        output.WriteLine($"Kugane: anchored at {string.Join(", ", kugane.Anchors)}, expansion {kugane.Expansion}, prints {names.Placeholder(kugane)}");
        Assert.Equal(Stormblood, kugane.Expansion);
    }

    [GameDataFact]
    public void Kugane_hides_before_stormblood_and_shows_once_the_story_opens_it()
    {
        var before = At(FirstOf(Stormblood));
        Assert.True(before.IsMasked(NotWithoutIncident), "Not without Incident lies past the three names ahead");
        Assert.True(before.IsNameMasked(SpoilerKind.Area, "Kugane"));
        Assert.StartsWith("Stormblood area\u00A0", before.Name(SpoilerKind.Area, "Kugane"), StringComparison.Ordinal);
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
        Assert.Equal("Dungeon (Lv\u00A061)", before.Name(SpoilerKind.Duty, "The Sirensong Sea"));
        Assert.False(after.IsNameMasked(SpoilerKind.Duty, "The Sirensong Sea"));

        Assert.True(before.IsNameMasked(SpoilerKind.Npc, "Hancock"));
        Assert.Equal("Stormblood character", before.Name(SpoilerKind.Npc, "Hancock"));
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

                output.WriteLine($"{quest.RowId} {quest.Name}: {reward.Name} prints {before.Name(SpoilerKind.Reward, reward.Name)}");
                Assert.True(before.IsNameMasked(SpoilerKind.Reward, reward.Name), reward.Name);
                // A reward prints its kind, never a level or its own name.
                Assert.StartsWith("A", before.Name(SpoilerKind.Reward, reward.Name), StringComparison.Ordinal);
                Assert.DoesNotContain(reward.Name, before.Name(SpoilerKind.Reward, reward.Name), StringComparison.Ordinal);
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

        // Its number is its place among Dawntrail's zones in the game's own TerritoryType order, the expansion named
        // from the ExVersion sheet; the same number everywhere. The numbers the game data gives today, pinned.
        string[] dawntrail = ["Tuliyollal", "Solution Nine", "Urqopacha", "Kozama'uka", "Yak T'el", "Shaaloani", "Heritage Found", "Living Memory", "Phantom Village"];
        for (var i = 0; i < dawntrail.Length; i++)
        {
            Assert.Equal($"Dawntrail area\u00A0{i + 1}", before.Name(SpoilerKind.Area, dawntrail[i]));
        }

        Assert.Contains(city.Name, dawntrail, StringComparer.OrdinalIgnoreCase);

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
