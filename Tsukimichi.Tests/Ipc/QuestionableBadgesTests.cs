using Tsukimichi.Core.Ipc;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// <see cref="QuestionableBadges"/>, the cache behind "On Questionable's list (#n)" and "Questionable has a path"
/// (feature plan v5, 1.6.0): what a lock answer says about a path, paths held until Questionable's generation moves,
/// a few questions per frame, and when the list is read again.
/// </summary>
public class QuestionableBadgesTests
{
    private const uint Row428 = 0x10000 + 428;
    private const uint Row1021 = 0x10000 + 1021;

    [Fact]
    public void A_locked_answer_with_no_reason_is_no_path()
    {
        Assert.False(QuestionableBadges.HasPath(new QuestionableAnswer(true, string.Empty)));
    }

    [Fact]
    public void A_reason_or_an_open_answer_is_a_path()
    {
        Assert.True(QuestionableBadges.HasPath(new QuestionableAnswer(true, "Prev quest (1)")));
        Assert.True(QuestionableBadges.HasPath(new QuestionableAnswer(false, string.Empty)));
    }

    [Fact]
    public void No_answer_or_the_forks_reasonless_gate_is_unknown()
    {
        Assert.Null(QuestionableBadges.HasPath(null));
        Assert.Null(QuestionableBadges.HasPath(new QuestionableAnswer(true, null)));
        Assert.Null(QuestionableBadges.HasPath(new QuestionableAnswer(false, null)));
    }

    [Fact]
    public void A_path_answer_holds_until_the_generation_moves()
    {
        var badges = new QuestionableBadges();
        Assert.False(badges.TryGetPath(Row428, 1, out _));

        badges.StorePath(Row428, 1, false);
        Assert.True(badges.TryGetPath(Row428, 1, out var known));
        Assert.False(known);

        // A plugin list change or Questionable.ReloadData moves the generation: ask again.
        Assert.False(badges.TryGetPath(Row428, 2, out _));
    }

    [Fact]
    public void An_unanswered_question_is_cached_too()
    {
        var badges = new QuestionableBadges();
        badges.StorePath(Row428, 1, null);

        Assert.True(badges.TryGetPath(Row428, 1, out var known));
        Assert.Null(known);
    }

    [Fact]
    public void A_frame_asks_at_most_its_budget_and_the_next_frame_asks_again()
    {
        var badges = new QuestionableBadges();
        for (var i = 0; i < QuestionableBadges.MaxAsksPerFrame; i++)
        {
            Assert.True(badges.TryTakeAsk(10));
        }

        Assert.False(badges.TryTakeAsk(10));
        Assert.True(badges.TryTakeAsk(11));
    }

    [Fact]
    public void The_list_is_read_when_stale_or_old_or_after_a_new_generation()
    {
        var badges = new QuestionableBadges();
        Assert.True(badges.ListNeedsRead(1, 0.0));

        badges.StoreList(["A12", "428"], 1, 100.0);
        Assert.False(badges.ListNeedsRead(1, 100.0 + QuestionableBadges.ListMaxAgeSeconds - 1.0));
        Assert.True(badges.ListNeedsRead(1, 100.0 + QuestionableBadges.ListMaxAgeSeconds));
        Assert.True(badges.ListNeedsRead(2, 101.0));

        badges.MarkListStale();
        Assert.True(badges.ListNeedsRead(1, 101.0));
    }

    [Fact]
    public void Positions_come_from_the_last_read()
    {
        var badges = new QuestionableBadges();
        badges.StoreList(["A12", "428"], 1, 0.0);

        Assert.Equal(2, badges.Position(Row428));
        Assert.Null(badges.Position(Row1021));
        Assert.Equal(1, badges.QuestCount);

        badges.StorePositions(new Dictionary<uint, int> { [Row1021] = 5 }, 1, 1.0);
        Assert.Null(badges.Position(Row428));
        Assert.Equal(5, badges.Position(Row1021));
        Assert.False(badges.ListNeedsRead(1, 1.0));
    }

    [Fact]
    public void A_failed_read_shows_no_badge()
    {
        var badges = new QuestionableBadges();
        badges.StoreList(["428"], 1, 0.0);
        badges.StoreList(null, 1, 1.0);

        Assert.Null(badges.Position(Row428));
        Assert.Equal(0, badges.QuestCount);
    }
}
