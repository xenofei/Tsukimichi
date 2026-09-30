using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

public class FlightIndexTests
{
    private static FlightZone Zone(uint territory, string name, byte expansion, int quests = 5, int field = 10)
    {
        var questCurrents = new FlightCurrent[quests];
        for (var i = 0; i < quests; i++)
        {
            questCurrents[i] = new FlightCurrent(territory * 100 + (uint)i, territory * 1000 + (uint)i);
        }

        var fieldIds = new uint[field];
        for (var i = 0; i < field; i++)
        {
            fieldIds[i] = territory * 100 + 50 + (uint)i;
        }

        return new FlightZone(territory, name, expansion, questCurrents, fieldIds);
    }

    [Fact]
    public void From_orders_by_expansion_then_name_and_answers_zone_for()
    {
        var index = FlightIndex.From(
        [
            Zone(1188, "Urqopacha", 5),
            Zone(397, "Coerthas Western Highlands", 1, field: 4),
            Zone(1187, "Kozama'uka", 5),
            Zone(398, "The Dravanian Forelands", 1, field: 4),
        ]);

        Assert.Equal(["Coerthas Western Highlands", "The Dravanian Forelands", "Kozama'uka", "Urqopacha"], index.Zones.Select(z => z.Name));
        Assert.Equal(1187u, index.ZoneFor(1187)!.TerritoryId);
        Assert.Null(index.ZoneFor(132));
        Assert.Equal(9, index.Zones[0].TotalCurrents);
        Assert.Equal(4, index.Zones[0].FieldCurrentCount);
        Assert.Equal(15, index.Zones[3].TotalCurrents);
    }

    [Fact]
    public void From_keeps_the_first_zone_per_territory_and_nothing_is_empty()
    {
        var index = FlightIndex.From([Zone(397, "First", 1), Zone(397, "Second", 1)], aetherCompassIcon: 405);
        Assert.Single(index.Zones);
        Assert.Equal("First", index.ZoneFor(397)!.Name);
        Assert.Equal(405u, index.AetherCompassIcon);

        Assert.Same(FlightIndex.Empty, FlightIndex.From([]));
        Assert.Empty(FlightIndex.Empty.Zones);
        Assert.Null(FlightIndex.Empty.ZoneFor(397));
        Assert.Equal(0u, FlightIndex.Empty.AetherCompassIcon);
    }

    [Fact]
    public void A_zone_shared_by_several_territories_answers_each_of_them()
    {
        var arr = new FlightZone(156, "A Realm Reborn (all zones)", 0, [new FlightCurrent(2818308, 70058)], [], [134, 135, 180]);
        var index = FlightIndex.From([arr, Zone(397, "Coerthas Western Highlands", 1, field: 4)]);

        Assert.Equal(2, index.Zones.Count);
        Assert.True(arr.CoversManyTerritories);
        Assert.False(index.ZoneFor(397)!.CoversManyTerritories);
        Assert.All(new uint[] { 156, 134, 135, 180 }, t => Assert.Same(arr, index.ZoneFor(t)));
        Assert.Null(index.ZoneFor(132));
    }
}

/// <summary>The awarding-quest rule on plain ids (<see cref="AetherCurrentQuests.Resolve(uint, uint, Func{uint, bool}, Func{uint, IEnumerable{uint}})"/>).</summary>
public class AetherCurrentQuestsTests
{
    // Quests 10, 20, 21 and 30 carry the Aether Current reward; 11 is the follow-up of 10, 12 follows 20 and 21.
    private static readonly HashSet<uint> Awarding = [10, 20, 21, 30];

    private static readonly Dictionary<uint, uint[]> Previous = new()
    {
        [11] = [5, 10, 0],
        [12] = [20, 21, 0],
        [13] = [4, 0, 0],
        [40] = [3, 0, 0],
    };

    private static AetherCurrentQuest Resolve(uint current, uint listed) =>
        AetherCurrentQuests.Resolve(current, listed, Awarding.Contains, id => Previous.GetValueOrDefault(id) ?? []);

    [Fact]
    public void The_listed_quest_stands_when_it_carries_the_reward()
    {
        var resolved = Resolve(1, 10);
        Assert.Equal(10u, resolved.QuestRowId);
        Assert.Equal(10u, resolved.ListedQuestRowId);
        Assert.Equal(AetherCurrentQuestSource.Listed, resolved.Source);
    }

    [Fact]
    public void Otherwise_the_single_prerequisite_that_carries_it()
    {
        var resolved = Resolve(1, 11);
        Assert.Equal(10u, resolved.QuestRowId);
        Assert.Equal(11u, resolved.ListedQuestRowId);
        Assert.Equal(AetherCurrentQuestSource.PreviousQuest, resolved.Source);

        // Two prerequisites carry it: neither is taken, and without an override the listed quest stays.
        var ambiguous = Resolve(1, 12);
        Assert.Equal(12u, ambiguous.QuestRowId);
        Assert.Equal(AetherCurrentQuestSource.ListedUnflagged, ambiguous.Source);
    }

    [Fact]
    public void Otherwise_the_curated_override_then_the_listed_quest()
    {
        var thavnair = Resolve(2818328, 13);
        Assert.Equal(69793u, thavnair.QuestRowId);
        Assert.Equal(13u, thavnair.ListedQuestRowId);
        Assert.Equal(AetherCurrentQuestSource.Override, thavnair.Source);

        // The Ultimate Weapon: nothing carries the reward, so the listed quest is kept.
        var ultimateWeapon = Resolve(2818308, 40);
        Assert.Equal(40u, ultimateWeapon.QuestRowId);
        Assert.Equal(AetherCurrentQuestSource.ListedUnflagged, ultimateWeapon.Source);
    }
}

/// <summary>
/// The shipped unique-reward data credits the awarding quests (DataGen resolves them as the Flight index does), so the
/// aether-current presets classify the five corrected quests and no longer the five the sheet lists.
/// </summary>
public class AetherCurrentFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private static readonly uint[] Awarding = [67364, 67326, 67333, 67410, 69793];
    private static readonly uint[] Listed = [67365, 67328, 67334, 67437, 70030];

    [Fact]
    public void Shipped_data_credits_the_awarding_quests()
    {
        var unique = Tsukimichi.Core.Storage.UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var currentEntries = unique.Entries.Where(e => e.Kind == RewardKind.AetherCurrent).ToList();
        Assert.Equal(151, currentEntries.Count);
        Assert.Equal(151, currentEntries.Select(e => e.RewardId).Distinct().Count());

        var currents = Tsukimichi.Core.Query.FeaturePresets.AetherCurrentQuests(fixture.Bundle.Catalog, unique.Entries);
        var others = Tsukimichi.Core.Query.FeaturePresets.NonCurrentUnlockQuests(unique.Entries);
        foreach (var id in Awarding)
        {
            Assert.Contains(id, currents);
            Assert.True(Tsukimichi.Core.Query.FeaturePresets.UnlocksOnlyAetherCurrents(fixture.Bundle.Catalog.ByRowId[id], fixture.Curated, currents, others), $"{id}");
        }

        foreach (var id in Listed)
        {
            Assert.DoesNotContain(id, currents);
            Assert.DoesNotContain(currentEntries, e => e.QuestRowId == id);
        }
    }
}

/// <summary>The Flight view's counting rule (<see cref="FlightProgress.QuestCurrentDone"/>).</summary>
public class FlightProgressTests
{
    [Fact]
    public void The_attunement_flag_decides_when_it_can_be_read()
    {
        // Flag set: done, whatever the quest says.
        Assert.True(FlightProgress.QuestCurrentDone(true, QuestState.Completed));
        Assert.True(FlightProgress.QuestCurrentDone(true, QuestState.Ready));
        Assert.True(FlightProgress.QuestCurrentDone(true, null));

        // Flag unset: not done, even with the quest complete.
        Assert.False(FlightProgress.QuestCurrentDone(false, QuestState.Completed));
        Assert.False(FlightProgress.QuestCurrentDone(false, QuestState.Ready));
    }

    [Fact]
    public void Without_a_flag_the_quest_completion_stands_in()
    {
        Assert.True(FlightProgress.QuestCurrentDone(null, QuestState.Completed));
        Assert.False(FlightProgress.QuestCurrentDone(null, QuestState.Ready));
        Assert.False(FlightProgress.QuestCurrentDone(null, QuestState.Unknown));
        Assert.False(FlightProgress.QuestCurrentDone(null, null));
    }
}

public class FlightIndexGameDataTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    private const byte ARealmReborn = 0;
    private const byte Heavensward = 1;
    private const byte Endwalker = 4;
    private const byte Dawntrail = 5;

    [GameDataFact]
    public void Builds_every_flying_zone_from_the_sheets()
    {
        var index = FlightIndex.Build(fixture.Game.Excel, Language.English);
        foreach (var zone in index.Zones)
        {
            output.WriteLine($"{zone.Expansion} {zone.TerritoryId,5} {zone.Name,-32} quests {zone.QuestCurrents.Count} field {zone.FieldCurrentCount}");
        }

        output.WriteLine($"{index.Zones.Count} zones; Aether Compass icon {index.AetherCompassIcon}");

        // 31 rows: Mor Dhona (the ARR flight current), six zones for each of Heavensward, Stormblood, Shadowbringers
        // and Endwalker, six for Dawntrail.
        Assert.Equal(31, index.Zones.Count);
        Assert.Equal(6, index.Zones.Count(z => z.Expansion == Heavensward));
        Assert.Equal(6, index.Zones.Count(z => z.Expansion == Endwalker));

        // A Realm Reborn: flight is opened by The Ultimate Weapon, one quest current in a set whose own territory is
        // Mor Dhona (156) and which all seventeen field zones name: one entry for all of them.
        var arr = Assert.Single(index.Zones, z => z.Expansion == ARealmReborn);
        Assert.Equal(156u, arr.TerritoryId);
        Assert.Equal("A Realm Reborn (all zones)", arr.Name);
        Assert.True(arr.CoversManyTerritories);
        Assert.Equal(0, arr.FieldCurrentCount);
        var ultimateWeapon = Assert.Single(arr.QuestCurrents);
        Assert.Equal(70058u, ultimateWeapon.QuestRowId);
        Assert.False(ultimateWeapon.Corrected);
        Assert.Equal("The Ultimate Weapon", fixture.Bundle.Catalog.GetByRowId(ultimateWeapon.QuestRowId)?.Name);

        // Heavensward to Endwalker: five quest currents and, since the 6.0 reduction, four field currents per zone.
        var reduced = index.Zones.Where(z => z.Expansion is >= Heavensward and <= Endwalker).ToList();
        Assert.Equal(24, reduced.Count);
        Assert.All(reduced, z => Assert.Equal(5, z.QuestCurrents.Count));
        Assert.All(reduced.Where(z => z.TerritoryId != 402), z => Assert.Equal(4, z.FieldCurrentCount));
        // Azys Lla (402) is quest-only.
        Assert.Equal(0, index.ZoneFor(402)!.FieldCurrentCount);
        Assert.Contains(reduced, z => z.Name == "Coerthas Western Highlands" && z.TerritoryId == 397);
        Assert.Contains(reduced, z => z.Name == "The Sea of Clouds" && z.TerritoryId == 401);

        // Dawntrail: six zones, each with five quest currents and ten field currents (15 in all).
        var dawntrail = index.Zones.Where(z => z.Expansion == Dawntrail).ToList();
        Assert.Equal(6, dawntrail.Count);
        Assert.All(dawntrail, z => Assert.Equal(5, z.QuestCurrents.Count));
        Assert.All(dawntrail, z => Assert.Equal(10, z.FieldCurrentCount));
        Assert.All(dawntrail, z => Assert.Equal(15, z.TotalCurrents));
        Assert.Contains(dawntrail, z => z.Name == "Urqopacha" && z.TerritoryId == 1187);
        Assert.Contains(dawntrail, z => z.Name == "Living Memory" && z.TerritoryId == 1192);

        // Ordered by expansion then name; every quest current names a catalog quest; no current is listed twice.
        for (var i = 1; i < index.Zones.Count; i++)
        {
            var a = index.Zones[i - 1];
            var b = index.Zones[i];
            Assert.True(a.Expansion < b.Expansion || (a.Expansion == b.Expansion && string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase) <= 0), $"{a.Name} before {b.Name}");
        }

        Assert.All(index.Zones.SelectMany(z => z.QuestCurrents), c => Assert.NotNull(fixture.Bundle.Catalog.GetByRowId(c.QuestRowId)));
        var allIds = index.Zones.SelectMany(z => z.QuestCurrents.Select(c => c.AetherCurrentId).Concat(z.FieldCurrentIds)).ToList();
        Assert.Equal(allIds.Count, allIds.Distinct().Count());
        Assert.Equal(303, allIds.Count);

        // Territories without flying are absent: the cities New Gridania (132) and Ul'dah - Steps of Nald (130), the
        // duty Sastasha (1036).
        Assert.Null(index.ZoneFor(132));
        Assert.Null(index.ZoneFor(130));
        Assert.Null(index.ZoneFor(1036));
    }

    /// <summary>The seventeen A Realm Reborn field territories, all naming AetherCurrentCompFlgSet 19.</summary>
    private static readonly uint[] ArrFieldZones = [134, 135, 137, 138, 139, 140, 141, 145, 146, 147, 148, 152, 153, 154, 155, 156, 180];

    [GameDataFact]
    public void Every_a_realm_reborn_field_zone_resolves_to_the_one_entry()
    {
        var index = FlightIndex.Build(fixture.Game.Excel, Language.English);
        var arr = Assert.Single(index.Zones, z => z.Expansion == ARealmReborn);
        Assert.Equal(ArrFieldZones.Length - 1, arr.OtherTerritoryIds!.Count);
        Assert.Equal(ArrFieldZones, arr.OtherTerritoryIds.Append(arr.TerritoryId).Order());
        Assert.All(ArrFieldZones, t => Assert.Same(arr, index.ZoneFor(t)));

        // Middle La Noscea (134) has A Realm Reborn flying; later zones keep their own entry.
        Assert.Equal("A Realm Reborn (all zones)", index.ZoneFor(134)!.Name);
        Assert.Equal("Coerthas Western Highlands", index.ZoneFor(397)!.Name);
        Assert.All(index.Zones.Where(z => z.Expansion != ARealmReborn), z => Assert.False(z.CoversManyTerritories, z.Name));

        // The label follows the format the plugin hands in.
        Assert.Equal("A Realm Reborn · every zone", FlightIndex.Build(fixture.Game.Excel, Language.English, "{0} · every zone").ZoneFor(140)!.Name);
    }

    /// <summary>Current -> (listed quest, awarding quest) for the five currents <c>AetherCurrent.Quest</c> names wrongly.</summary>
    private static readonly Dictionary<uint, (uint Listed, uint Awarding)> Corrected = new()
    {
        [2818096] = (67365, 67364), // The Churning Mists: The Unceasing Gardener -> Hide Your Moogles
        [2818065] = (67328, 67326), // The Dravanian Forelands: Natural Repellent -> Stolen Munitions
        [2818066] = (67334, 67333), // The Dravanian Forelands: Chocobo's Last Stand -> The Hunter Becomes the Kweh
        [2818110] = (67437, 67410), // The Sea of Clouds: Search and Rescue -> Honoring the Past
        [2818328] = (70030, 69793), // Thavnair: Curing What Ails -> In Agama's Footsteps
    };

    [GameDataFact]
    public void Every_counted_quest_awards_its_aether_current_one_to_one()
    {
        var index = FlightIndex.Build(fixture.Game.Excel, Language.English);
        var quests = fixture.Game.Excel.GetSheet<Quest>(Language.English);
        var awarding = quests
            .Where(q => q.OtherReward.RowId == AetherCurrentQuests.OtherRewardAetherCurrent)
            .Select(q => q.RowId)
            .ToHashSet();
        Assert.Equal(150, awarding.Count);

        // Every quest current outside A Realm Reborn: its quest carries Quest.OtherReward = Aether Current, and the 150
        // counted quests are exactly the 150 flagged ones, each once.
        var counted = index.Zones
            .Where(z => z.Expansion != ARealmReborn)
            .SelectMany(z => z.QuestCurrents)
            .ToList();
        Assert.Equal(150, counted.Count);
        Assert.All(counted, c => Assert.True(awarding.Contains(c.QuestRowId), $"current {c.AetherCurrentId}: quest {c.QuestRowId} does not award an aether current"));
        Assert.Equal(150, counted.Select(c => c.QuestRowId).Distinct().Count());
        Assert.True(awarding.SetEquals(counted.Select(c => c.QuestRowId)));

        // The five corrected ids, with the listed quest kept for diagnostics; no other current was changed.
        var byCurrent = index.Zones.SelectMany(z => z.QuestCurrents).ToDictionary(c => c.AetherCurrentId);
        foreach (var (current, (listed, quest)) in Corrected)
        {
            var row = byCurrent[current];
            output.WriteLine($"{current}: {listed} -> {row.QuestRowId} {fixture.Bundle.Catalog.GetByRowId(row.QuestRowId)?.Name}");
            Assert.Equal(quest, row.QuestRowId);
            Assert.Equal(listed, row.ListedQuestRowId);
            Assert.True(row.Corrected);
        }

        Assert.Equal(Corrected.Keys.Order(), byCurrent.Values.Where(c => c.Corrected).Select(c => c.AetherCurrentId).Order());

        // The zones the five belong to.
        Assert.Contains(index.ZoneFor(400)!.QuestCurrents, c => c.QuestRowId == 67364);
        Assert.Contains(index.ZoneFor(398)!.QuestCurrents, c => c.QuestRowId == 67326);
        Assert.Contains(index.ZoneFor(398)!.QuestCurrents, c => c.QuestRowId == 67333);
        Assert.Contains(index.ZoneFor(401)!.QuestCurrents, c => c.QuestRowId == 67410);
        Assert.Contains(index.ZoneFor(957)!.QuestCurrents, c => c.QuestRowId == 69793);

        // DataGen's resolver is the same code: every listed current resolves as the index does.
        var resolved = AetherCurrentQuests.ResolveAll(fixture.Game.Excel.GetSheet<AetherCurrent>(Language.English), quests);
        Assert.All(byCurrent.Values, c => Assert.Equal(c.QuestRowId, resolved[c.AetherCurrentId].QuestRowId));
    }

    [GameDataFact]
    public void The_aether_compass_is_the_action_row_the_index_names()
    {
        var actions = fixture.Game.Excel.GetSheet<Lumina.Excel.Sheets.Action>(Language.English);
        var compass = actions.GetRow(FlightIndex.AetherCompassAction);
        output.WriteLine($"Action {compass.RowId}: '{compass.Name.ExtractText()}' icon {compass.Icon}");
        Assert.Equal("the Aether Compass", compass.Name.ExtractText());
        Assert.True(compass.Icon > 0);

        // Nothing in GeneralAction is the compass; the Action sheet holds it.
        Assert.DoesNotContain(fixture.Game.Excel.GetSheet<GeneralAction>(Language.English), a => a.Name.ExtractText().Contains("Compass", StringComparison.OrdinalIgnoreCase));

        var index = FlightIndex.Build(fixture.Game.Excel, Language.English);
        Assert.Equal((uint)compass.Icon, index.AetherCompassIcon);
    }
}
