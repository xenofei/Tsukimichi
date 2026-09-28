using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

public sealed class BatchCursorTests
{
    [Fact]
    public void Advances_through_every_item_then_is_done()
    {
        var cursor = new BatchCursor(3);

        Assert.False(cursor.IsDone);
        cursor.Advance();
        cursor.Advance();
        Assert.Equal(2, cursor.Index);
        Assert.False(cursor.IsFinished);
        cursor.Advance();

        Assert.True(cursor.IsDone);
        Assert.True(cursor.IsFinished);
        Assert.False(cursor.GaveUp);
    }

    [Fact]
    public void Failure_keeps_the_index_so_the_next_tick_resumes_there()
    {
        var cursor = new BatchCursor(10, maxAttempts: 3);
        cursor.Advance();
        cursor.Advance();

        Assert.False(cursor.Fail());

        Assert.Equal(2, cursor.Index);
        Assert.Equal(1, cursor.Attempts);
        Assert.False(cursor.IsFinished);

        // The retry succeeds: the attempt count starts over for the next item.
        cursor.Advance();
        Assert.Equal(3, cursor.Index);
        Assert.Equal(0, cursor.Attempts);
    }

    [Fact]
    public void Gives_up_after_the_bounded_attempts_on_one_item()
    {
        var cursor = new BatchCursor(10, maxAttempts: 3);
        cursor.Advance();

        Assert.False(cursor.Fail());
        Assert.False(cursor.Fail());
        Assert.True(cursor.Fail());

        Assert.True(cursor.GaveUp);
        Assert.True(cursor.IsFinished);
        Assert.False(cursor.IsDone);
        Assert.Equal(1, cursor.Index);
        Assert.Throws<InvalidOperationException>(cursor.Advance);
        Assert.Throws<InvalidOperationException>(() => cursor.Fail());
    }

    [Fact]
    public void Intermittent_failures_never_add_up_across_items()
    {
        var cursor = new BatchCursor(4, maxAttempts: 2);
        for (var i = 0; i < 4; i++)
        {
            Assert.False(cursor.Fail());
            cursor.Advance();
        }

        Assert.True(cursor.IsDone);
        Assert.False(cursor.GaveUp);
    }

    [Fact]
    public void Empty_batch_is_done_at_once()
    {
        Assert.True(new BatchCursor(0).IsDone);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BatchCursor(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BatchCursor(1, maxAttempts: 0));
    }
}
