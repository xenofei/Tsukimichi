using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Storage;

/// <summary>
/// Builds before 1.4.2 saved the client's raw allied society rank byte, so a file written on a rank-up day holds
/// 128 + rank. The store masks it on load; the "ranked up today" flag is additive at schema v1 and written only when set.
/// </summary>
public sealed class TribeRankRepairTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshot-v1.json");

    /// <summary>The frozen v1 fixture with tribe 7 (rank 4, Trusted) saved as the raw rank-up-day byte 0x84.</summary>
    private static string RankUpDayFile()
    {
        var root = JsonNode.Parse(File.ReadAllText(FixturePath))!.AsObject();
        Assert.Equal(4, (int)root["tribes"]!["7"]!["rank"]!);
        root["tribes"]!["7"]!["rank"] = 0x84;
        return root.ToJsonString();
    }

    private static string WriteCharacter(TempDir tmp, string json)
    {
        var root = tmp.File("store");
        Directory.CreateDirectory(Path.Combine(root, "characters"));
        File.WriteAllText(Path.Combine(root, "characters", "1.json"), json);
        return root;
    }

    [Fact]
    public void A_rank_saved_with_the_rank_up_bit_loads_masked()
    {
        using var tmp = new TempDir();
        var store = new JsonSnapshotStore(WriteCharacter(tmp, RankUpDayFile()));

        var loaded = store.Load(1);

        Assert.NotNull(loaded);
        Assert.Empty(store.Warnings);
        Assert.Equal(new TribeStanding(4, 0, true), loaded.Tribes[7]);
        Assert.Equal(new TribeStanding(3, 510), loaded.Tribes[10]);
        Assert.All(loaded.Tribes.Values, t => Assert.True(t.Rank < 0x80));
    }

    [Fact]
    public void LoadShared_masks_too()
    {
        using var tmp = new TempDir();
        var read = new JsonSnapshotStore(WriteCharacter(tmp, RankUpDayFile())).LoadShared(1);

        Assert.Equal(SharedLoad.Loaded, read.Status);
        Assert.Equal((byte)4, read.Value!.Tribes[7].Rank);
    }

    [Fact]
    public void A_repaired_snapshot_saves_the_real_rank_and_the_flag()
    {
        using var tmp = new TempDir();
        var root = WriteCharacter(tmp, RankUpDayFile());
        var store = new JsonSnapshotStore(root);

        store.Save(store.Load(1)!);

        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "characters", "1.json")))!.AsObject();
        Assert.Equal(4, (int)json["tribes"]!["7"]!["rank"]!);
        Assert.True((bool)json["tribes"]!["7"]!["rankedUpToday"]!);
        Assert.Null(json["tribes"]!["10"]!["rankedUpToday"]);
    }

    [Fact]
    public void The_flag_is_not_written_while_false_and_round_trips_when_set()
    {
        using var tmp = new TempDir();
        var root = tmp.File("store");
        var snapshot = Fixture.Snapshot(Fixture.A) with
        {
            Tribes = new Dictionary<byte, TribeStanding> { [3] = TribeStanding.FromClient(0x84, 0), [9] = new(8, 0) },
        };

        new JsonSnapshotStore(root).Save(snapshot);
        var text = File.ReadAllText(Path.Combine(root, "characters", "1.json"));
        var json = JsonNode.Parse(text)!.AsObject();
        var loaded = new JsonSnapshotStore(root).Load(1);

        Assert.Null(json["tribes"]!["9"]!["rankedUpToday"]);
        Assert.True((bool)json["tribes"]!["3"]!["rankedUpToday"]!);
        Assert.NotNull(loaded);
        Assert.Equal(new TribeStanding(4, 0, true), loaded.Tribes[3]);
        Assert.Equal(new TribeStanding(8, 0), loaded.Tribes[9]);
    }
}
