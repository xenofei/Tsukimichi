using Tsukimichi.Core.Multibox;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Multibox;

public sealed class HeartbeatFileTests : IDisposable
{
    private static readonly ClientIdentity Me = new(1000, "bbbb");
    private static readonly ClientIdentity Other = new(2000, "cccc");

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private string Dir => tmp.File("characters");

    private static Heartbeat Beat(ClientIdentity client, ulong contentId, DateTime written) =>
        new(contentId, "Michiru Tsukikage", 74, client.ProcessId, client.ClientId, written.AddMinutes(-5), written);

    [Fact]
    public void Write_then_read_round_trips_and_names_the_file_after_the_character()
    {
        var written = new DateTime(2026, 9, 30, 20, 0, 0, DateTimeKind.Utc);
        var beat = Beat(Other, 0x0040_0000_0123_4567UL, written);

        HeartbeatFile.Write(Dir, beat);

        Assert.True(File.Exists(Path.Combine(Dir, "18014398528570727.live.json")));
        var read = HeartbeatFile.TryRead(Dir, beat.ContentId);
        Assert.Equal(beat, read);
        Assert.Equal(DateTimeKind.Utc, read!.WrittenUtc.Kind);
    }

    [Fact]
    public void The_snapshot_store_does_not_list_a_heartbeat_as_a_character()
    {
        HeartbeatFile.Write(Dir, Beat(Other, 42, DateTime.UtcNow));

        Assert.Empty(new JsonSnapshotStore(tmp.Path).List());
    }

    [Theory]
    [InlineData("42.live.json", 42UL)]
    [InlineData("42.json", null)]
    [InlineData("42.accepted.json", null)]
    [InlineData("abc.live.json", null)]
    [InlineData("42.live.json.1234-abcdef.tmp", null)]
    public void Content_id_of_reads_heartbeat_names_only(string name, ulong? expected)
    {
        Assert.Equal(expected, HeartbeatFile.ContentIdOf(name));
    }

    [Fact]
    public void A_corrupt_or_mismatched_heartbeat_reads_as_absent_and_stays_in_place()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "42.live.json"), "{ not json");
        HeartbeatFile.Write(Dir, Beat(Other, 43, DateTime.UtcNow));
        File.Move(Path.Combine(Dir, "43.live.json"), Path.Combine(Dir, "44.live.json"));

        Assert.Null(HeartbeatFile.TryRead(Dir, 42));
        Assert.Null(HeartbeatFile.TryRead(Dir, 44));
        Assert.True(File.Exists(Path.Combine(Dir, "42.live.json")));
        Assert.Empty(HeartbeatFile.ReadAll(Dir));
    }

    [Fact]
    public void Delete_if_allowed_removes_own_and_stale_heartbeats_and_keeps_another_clients_fresh_one()
    {
        var now = DateTime.UtcNow;
        HeartbeatFile.Write(Dir, Beat(Me, 1, now));
        HeartbeatFile.Write(Dir, Beat(Other, 2, now.AddMinutes(-2)));
        HeartbeatFile.Write(Dir, Beat(Other, 3, now));

        Assert.True(HeartbeatFile.DeleteIfAllowed(Dir, 1, Me, now));
        Assert.True(HeartbeatFile.DeleteIfAllowed(Dir, 2, Me, now));
        Assert.False(HeartbeatFile.DeleteIfAllowed(Dir, 3, Me, now));
        Assert.False(HeartbeatFile.DeleteIfAllowed(Dir, 4, Me, now));

        Assert.Equal([3UL], HeartbeatFile.ReadAll(Dir).Select(static b => b.ContentId));
    }
}
