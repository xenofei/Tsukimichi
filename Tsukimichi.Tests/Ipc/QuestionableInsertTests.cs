using Tsukimichi.Core.Ipc;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// <see cref="QuestionableInsert.Read"/> ("Do this next", feature plan v7 A6): the list read back after
/// <c>InsertQuestPriority(0, id)</c> says where the quest stands, since the gate answers true for a quest Questionable
/// does not know and leaves a quest it holds where it is.
/// </summary>
public class QuestionableInsertTests
{
    private const uint Row428 = 0x10000 + 428;

    [Fact]
    public void First_on_the_list()
    {
        var outcome = QuestionableInsert.Read(Row428, ["12", "A3"], ["428", "12", "A3"]);

        Assert.Equal(new QuestionableInsertOutcome(QuestionableInsertKind.First, 1), outcome);
    }

    [Fact]
    public void Already_on_the_list_keeps_its_place()
    {
        var outcome = QuestionableInsert.Read(Row428, ["12", "A3", "428"], ["12", "A3", "428"]);

        Assert.Equal(new QuestionableInsertOutcome(QuestionableInsertKind.AlreadyThere, 3), outcome);
    }

    [Fact]
    public void Not_first_and_new_reads_lower()
    {
        var outcome = QuestionableInsert.Read(Row428, ["12"], ["12", "428"]);

        Assert.Equal(new QuestionableInsertOutcome(QuestionableInsertKind.Lower, 2), outcome);
    }

    [Fact]
    public void Without_the_list_before_a_lower_place_is_not_called_already_there()
    {
        var outcome = QuestionableInsert.Read(Row428, null, ["12", "428"]);

        Assert.Equal(QuestionableInsertKind.Lower, outcome.Kind);
    }

    [Fact]
    public void Missing_when_Questionable_did_not_take_it()
    {
        var outcome = QuestionableInsert.Read(Row428, ["12"], ["12"]);

        Assert.Equal(new QuestionableInsertOutcome(QuestionableInsertKind.Missing, 0), outcome);
    }

    [Fact]
    public void Unverified_when_the_list_cannot_be_read_back()
    {
        var outcome = QuestionableInsert.Read(Row428, ["12"], null);

        Assert.Equal(new QuestionableInsertOutcome(QuestionableInsertKind.Unverified, 0), outcome);
    }
}
