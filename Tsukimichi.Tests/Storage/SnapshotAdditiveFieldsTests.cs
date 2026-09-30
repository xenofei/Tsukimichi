using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Storage;

/// <summary>
/// The fields 0.6.2 added at schema v1 (festival phases, custom delivery ranks, carrier level) are written only when
/// they hold something, survive a save and load, and read as "not captured" from a file that lacks them.
/// </summary>
public sealed class SnapshotAdditiveFieldsTests
{
    [Fact]
    public void Save_then_Load_round_trips_phases_ranks_and_carrier_level()
    {
        using var tmp = new TempDir();
        var root = tmp.File("store");
        var snapshot = Fixture.Snapshot(Fixture.A) with
        {
            ActiveFestivals = [10, 39],
            ActiveFestivalPhases = [2, 0],
            SatisfactionRanks = new Dictionary<byte, byte> { [1] = 5, [2] = 3, [12] = 0 },
            CarrierLevel = 7,
        };

        new JsonSnapshotStore(root).Save(snapshot);
        var loaded = new JsonSnapshotStore(root).Load(1);

        Assert.NotNull(loaded);
        Assert.Equal([10, 39], loaded.ActiveFestivals);
        Assert.Equal([2, 0], loaded.ActiveFestivalPhases);
        Assert.Equal((ushort)2, loaded.FestivalPhase(10));
        Assert.Equal((ushort)0, loaded.FestivalPhase(39));
        Assert.Null(loaded.FestivalPhase(84));
        Assert.Equal(3, loaded.SatisfactionRanks.Count);
        Assert.Equal((byte)3, loaded.SatisfactionRank(2));
        Assert.Equal((byte)0, loaded.SatisfactionRank(12));
        Assert.Null(loaded.SatisfactionRank(13));
        Assert.Equal((byte?)7, loaded.CarrierLevel);
        Assert.Equal(1, loaded.SchemaVersion);

        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "characters", "1.json")))!.AsObject();
        Assert.Equal("[2,0]", json["activeFestivalPhases"]!.ToJsonString());
        Assert.Equal(7, (int)json["carrierLevel"]!);
        Assert.Equal(3, (int)json["satisfactionRanks"]!["2"]!);
    }

    [Fact]
    public void Empty_additive_fields_are_not_written_and_read_back_as_not_captured()
    {
        using var tmp = new TempDir();
        var root = tmp.File("store");
        new JsonSnapshotStore(root).Save(Fixture.Snapshot(Fixture.A) with { ActiveFestivals = [10] });

        var text = File.ReadAllText(Path.Combine(root, "characters", "1.json"));
        Assert.DoesNotContain("activeFestivalPhases", text);
        Assert.DoesNotContain("satisfactionRanks", text);
        Assert.DoesNotContain("carrierLevel", text);
        Assert.Contains("\"activeFestivals\"", text);

        var loaded = new JsonSnapshotStore(root).Load(1);
        Assert.NotNull(loaded);
        Assert.Null(loaded.FestivalPhase(10));
        Assert.Empty(loaded.SatisfactionRanks);
        Assert.Null(loaded.CarrierLevel);
    }

    [Fact]
    public void A_captured_carrier_level_of_0_is_written_and_read_back_as_a_value()
    {
        using var tmp = new TempDir();
        var root = tmp.File("store");
        new JsonSnapshotStore(root).Save(Fixture.Snapshot(Fixture.A) with { CarrierLevel = 0 });

        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "characters", "1.json")))!.AsObject();
        Assert.Equal(0, (int)json["carrierLevel"]!);

        var loaded = new JsonSnapshotStore(root).Load(1);
        Assert.NotNull(loaded);
        Assert.Equal((byte?)0, loaded.CarrierLevel);
    }
}
