using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

/// <summary>
/// <see cref="ChatLinkRegistry"/>: the command ids behind the "[Open] [Pin] [Route]" and "[Show]" chat links carry the
/// action and the quest (or batch) themselves, and the registry keeps the most recently printed ones, handing back the
/// oldest for the plugin to unregister once the cap is reached.
/// </summary>
public sealed class ChatLinkRegistryTests
{
    [Theory]
    [InlineData(ChatLinkAction.Open, 65536u)]
    [InlineData(ChatLinkAction.Pin, 70123u)]
    [InlineData(ChatLinkAction.Route, 1u)]
    [InlineData(ChatLinkAction.ShowOpened, ChatLinkRegistry.MaxValue)]
    public void An_id_carries_its_action_and_value_back(ChatLinkAction action, uint value)
    {
        var id = ChatLinkRegistry.Encode(action, value);

        Assert.True(ChatLinkRegistry.TryDecode(id, out var decodedAction, out var decodedValue));
        Assert.Equal(action, decodedAction);
        Assert.Equal(value, decodedValue);
    }

    [Fact]
    public void Each_action_on_one_quest_gets_its_own_id()
    {
        var ids = new[] { ChatLinkAction.Open, ChatLinkAction.Pin, ChatLinkAction.Route, ChatLinkAction.ShowOpened }
            .Select(a => ChatLinkRegistry.Encode(a, 66000))
            .ToHashSet();

        Assert.Equal(4, ids.Count);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x0001_0000u)]
    [InlineData(0x0500_0001u)]
    [InlineData(0xFF00_0001u)]
    public void An_id_of_no_known_action_does_not_decode(uint id)
    {
        Assert.False(ChatLinkRegistry.TryDecode(id, out var action, out var value));
        Assert.Equal(default, action);
        Assert.Equal(0u, value);
    }

    [Fact]
    public void A_value_wider_than_24_bits_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChatLinkRegistry.Encode(ChatLinkAction.Open, ChatLinkRegistry.MaxValue + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChatLinkRegistry.Encode((ChatLinkAction)9, 1));
    }

    [Fact]
    public void An_id_is_registered_once_and_reused_after()
    {
        var registry = new ChatLinkRegistry(capacity: 3);
        var id = ChatLinkRegistry.Encode(ChatLinkAction.Open, 66000);

        Assert.True(registry.Touch(id, out var first));
        Assert.Null(first);
        Assert.False(registry.Touch(id, out var second));
        Assert.Null(second);
        Assert.Equal(1, registry.Count);
        Assert.True(registry.Contains(id));
    }

    [Fact]
    public void The_cap_pushes_out_the_least_recently_printed_id()
    {
        var registry = new ChatLinkRegistry(capacity: 3);
        registry.Touch(1, out _);
        registry.Touch(2, out _);
        registry.Touch(3, out _);

        // Printing 1 again makes 2 the oldest.
        Assert.False(registry.Touch(1, out var none));
        Assert.Null(none);

        Assert.True(registry.Touch(4, out var evicted));
        Assert.Equal(2u, evicted);
        Assert.Equal(3, registry.Count);
        Assert.Equal(new uint[] { 3, 1, 4 }, registry.Registered.ToArray());
        Assert.False(registry.Contains(2));
    }

    [Fact]
    public void An_evicted_id_comes_back_as_new_when_printed_again()
    {
        var registry = new ChatLinkRegistry(capacity: 1);
        registry.Touch(1, out _);
        registry.Touch(2, out var evicted);
        Assert.Equal(1u, evicted);

        Assert.True(registry.Touch(1, out evicted));
        Assert.Equal(2u, evicted);
    }

    [Fact]
    public void Clear_hands_back_every_id_oldest_first_and_empties_the_registry()
    {
        var registry = new ChatLinkRegistry();
        registry.Touch(5, out _);
        registry.Touch(6, out _);
        registry.Touch(5, out _);

        Assert.Equal(new uint[] { 6, 5 }, registry.Clear());
        Assert.Equal(0, registry.Count);
        Assert.Empty(registry.Registered);
    }

    [Fact]
    public void A_busy_session_never_holds_more_than_the_cap()
    {
        var registry = new ChatLinkRegistry();
        var evictions = 0;
        for (uint rowId = 65536; rowId < 65536 + 1000; rowId++)
        {
            foreach (var action in new[] { ChatLinkAction.Open, ChatLinkAction.Pin, ChatLinkAction.Route })
            {
                if (registry.Touch(ChatLinkRegistry.Encode(action, rowId), out var evicted) && evicted is not null)
                {
                    evictions++;
                }
            }
        }

        Assert.Equal(ChatLinkRegistry.DefaultCapacity, registry.Count);
        Assert.Equal(3000 - ChatLinkRegistry.DefaultCapacity, evictions);
    }

    [Fact]
    public void A_capacity_below_one_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChatLinkRegistry(0));
    }
}
