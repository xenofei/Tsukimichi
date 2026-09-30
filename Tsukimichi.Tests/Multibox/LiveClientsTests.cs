using Tsukimichi.Core.Multibox;

namespace Tsukimichi.Tests.Multibox;

/// <summary>The pure rules of multibox sharing (D11): staleness, who is live elsewhere, who owns a character's files, which heartbeats may go.</summary>
public sealed class LiveClientsTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 20, 0, 0, DateTimeKind.Utc);
    private static readonly ClientIdentity Me = new(1000, "bbbb");
    private static readonly ClientIdentity Other = new(2000, "cccc");

    private static Heartbeat Beat(ClientIdentity client, ulong contentId = 42, double ageSeconds = 0, DateTime? since = null) =>
        new(contentId, "Michiru Tsukikage", 74, client.ProcessId, client.ClientId, since ?? Now.AddMinutes(-5), Now.AddSeconds(-ageSeconds));

    [Theory]
    [InlineData(0, false)]
    [InlineData(10, false)]
    [InlineData(29.9, false)]
    [InlineData(30, false)]
    [InlineData(30.1, true)]
    [InlineData(600, true)]
    [InlineData(-10, false)]
    [InlineData(-31, true)]
    public void A_heartbeat_older_than_30_seconds_is_stale(double ageSeconds, bool stale)
    {
        Assert.Equal(stale, LiveClients.IsStale(Beat(Other, ageSeconds: ageSeconds), Now));
    }

    [Fact]
    public void The_refresh_interval_leaves_room_before_staleness()
    {
        Assert.True(LiveClients.RefreshInterval * 2 < LiveClients.StaleAfter);
    }

    [Fact]
    public void Another_process_is_another_client_and_this_process_never_is()
    {
        Assert.True(LiveClients.IsOtherClient(Beat(Other), Me));
        Assert.False(LiveClients.IsOtherClient(Beat(Me), Me));
        // An earlier load of the plugin in this same game client (a reload) left it: still this client.
        Assert.False(LiveClients.IsOtherClient(Beat(new ClientIdentity(Me.ProcessId, "aaaa")), Me));
    }

    [Fact]
    public void Live_elsewhere_lists_fresh_heartbeats_of_other_processes_only()
    {
        var beats = new[]
        {
            Beat(Other, contentId: 1),
            Beat(Other, contentId: 2, ageSeconds: 45),
            Beat(Me, contentId: 3),
            Beat(new ClientIdentity(3000, "dddd"), contentId: 4, ageSeconds: 5),
        };

        var live = LiveClients.LiveElsewhere(beats, Me, Now);

        Assert.Equal([1UL, 4UL], live.Keys.Order());
    }

    [Fact]
    public void The_character_live_here_is_never_live_elsewhere()
    {
        var live = LiveClients.LiveElsewhere([Beat(Other, contentId: 7)], Me, Now, ownLive: 7);

        Assert.Empty(live);
    }

    [Fact]
    public void No_heartbeat_a_stale_one_or_our_own_leaves_the_character_ours()
    {
        var since = Now.AddMinutes(-1);
        Assert.Equal(Ownership.Mine, LiveClients.Decide(null, Me, since, Now));
        Assert.Equal(Ownership.Mine, LiveClients.Decide(Beat(Other, ageSeconds: 31, since: Now), Me, since, Now));
        Assert.Equal(Ownership.Mine, LiveClients.Decide(Beat(Me, since: Now), Me, since, Now));
    }

    [Fact]
    public void Two_clients_on_one_character_the_newer_login_wins()
    {
        var older = Now.AddMinutes(-10);
        var newer = Now.AddMinutes(-2);

        // The other client logged in later: it keeps writing, this one stops.
        Assert.Equal(Ownership.Theirs, LiveClients.Decide(Beat(Other, since: newer), Me, older, Now));
        // This client logged in later: it takes over, and the other one reads Theirs from this client's heartbeat.
        Assert.Equal(Ownership.Mine, LiveClients.Decide(Beat(Other, since: older), Me, newer, Now));
        Assert.Equal(Ownership.Theirs, LiveClients.Decide(Beat(Me, since: newer), Other, older, Now));
    }

    [Fact]
    public void Two_claims_at_the_same_instant_settle_on_exactly_one_client()
    {
        var since = Now.AddMinutes(-3);
        var mine = LiveClients.Decide(Beat(Other, since: since), Me, since, Now);
        var theirs = LiveClients.Decide(Beat(Me, since: since), Other, since, Now);

        Assert.NotEqual(mine, theirs);
    }

    [Fact]
    public void Only_the_owner_of_the_live_character_writes_its_snapshot()
    {
        Assert.True(LiveClients.MayWriteSnapshot(42, 42, Ownership.Mine));
        Assert.False(LiveClients.MayWriteSnapshot(42, 42, Ownership.Theirs));
        // A stored character, live in another client or not, is never written here.
        Assert.False(LiveClients.MayWriteSnapshot(42, 7, Ownership.Mine));
        Assert.False(LiveClients.MayWriteSnapshot(42, null, Ownership.Mine));
    }

    [Fact]
    public void A_client_deletes_its_own_or_stale_heartbeats_but_never_a_fresh_one_of_another_client()
    {
        Assert.True(LiveClients.MayDelete(null, Me, Now));
        Assert.True(LiveClients.MayDelete(Beat(Me), Me, Now));
        Assert.True(LiveClients.MayDelete(Beat(Other, ageSeconds: 31), Me, Now));
        Assert.False(LiveClients.MayDelete(Beat(Other, ageSeconds: 5), Me, Now));
    }

    [Fact]
    public void Same_characters_ignores_the_refresh_time()
    {
        var a = new Dictionary<ulong, Heartbeat> { [1] = Beat(Other, contentId: 1) };
        var b = new Dictionary<ulong, Heartbeat> { [1] = Beat(Other, contentId: 1, ageSeconds: 10) };
        var c = new Dictionary<ulong, Heartbeat> { [2] = Beat(Other, contentId: 2) };

        Assert.True(LiveClients.SameCharacters(a, b));
        Assert.False(LiveClients.SameCharacters(a, c));
        Assert.False(LiveClients.SameCharacters(a, new Dictionary<ulong, Heartbeat>()));
    }
}
