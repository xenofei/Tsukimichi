using Tsukimichi.Core.Ipc;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// "Replace Questionable's list…" (<see cref="QuestionableListReplace"/>): the list is read before it is emptied, is not
/// touched when it cannot be read, and comes back when the import fails.
/// </summary>
public class QuestionableListReplaceTests
{
    private const string Old = "qst:priority:old";
    private const string New = "qst:priority:new";

    /// <summary>Questionable's list as a string, with switches for each gate.</summary>
    private sealed class FakeList
    {
        public string List { get; set; } = Old;

        public bool ExportFails { get; init; }

        public bool ClearFails { get; init; }

        public int ImportFailures { get; set; }

        public bool ImportAnswersFalse { get; init; }

        public List<string> Calls { get; } = [];

        public List<Exception> Failures { get; } = [];

        public QuestionableReplaceOutcome Replace() => QuestionableListReplace.Run(
            New,
            () =>
            {
                Calls.Add("export");
                return ExportFails ? throw new InvalidOperationException("export") : List;
            },
            () =>
            {
                Calls.Add("clear");
                if (ClearFails)
                {
                    throw new InvalidOperationException("clear");
                }

                List = string.Empty;
                return true;
            },
            text =>
            {
                Calls.Add("import " + text);
                if (ImportFailures > 0)
                {
                    ImportFailures--;
                    throw new InvalidOperationException("import");
                }

                if (ImportAnswersFalse && text == New)
                {
                    return false;
                }

                List = text;
                return true;
            },
            Failures.Add);
    }

    [Fact]
    public void A_replace_reads_the_list_before_emptying_it()
    {
        var fake = new FakeList();

        Assert.Equal(QuestionableReplaceOutcome.Replaced, fake.Replace());
        Assert.Equal(["export", "clear", "import " + New], fake.Calls);
        Assert.Equal(New, fake.List);
    }

    [Fact]
    public void A_list_that_cannot_be_read_is_left_alone()
    {
        var fake = new FakeList { ExportFails = true };

        Assert.Equal(QuestionableReplaceOutcome.NotRead, fake.Replace());
        Assert.Equal(["export"], fake.Calls);
        Assert.Equal(Old, fake.List);
        Assert.Single(fake.Failures);
    }

    [Fact]
    public void A_failed_import_puts_the_old_list_back()
    {
        var fake = new FakeList { ImportFailures = 1 };

        Assert.Equal(QuestionableReplaceOutcome.FailedRestored, fake.Replace());
        Assert.Equal(["export", "clear", "import " + New, "clear", "import " + Old], fake.Calls);
        Assert.Equal(Old, fake.List);
    }

    [Fact]
    public void An_import_that_answers_false_puts_the_old_list_back()
    {
        var fake = new FakeList { ImportAnswersFalse = true };

        Assert.Equal(QuestionableReplaceOutcome.FailedRestored, fake.Replace());
        Assert.Equal(Old, fake.List);
    }

    [Fact]
    public void A_restore_that_fails_too_says_so()
    {
        var fake = new FakeList { ImportFailures = 2 };

        Assert.Equal(QuestionableReplaceOutcome.FailedNotRestored, fake.Replace());
        Assert.Equal(2, fake.Failures.Count);
    }

    [Fact]
    public void A_failed_clear_sends_nothing_and_keeps_the_list()
    {
        var fake = new FakeList { ClearFails = true };

        Assert.Equal(QuestionableReplaceOutcome.FailedRestored, fake.Replace());
        Assert.DoesNotContain("import " + New, fake.Calls);
        Assert.Equal(Old, fake.List);
        Assert.Equal(2, fake.Failures.Count);
    }

    [Fact]
    public void An_empty_old_list_needs_nothing_imported_back()
    {
        var fake = new FakeList { List = string.Empty, ImportFailures = 1 };

        Assert.Equal(QuestionableReplaceOutcome.FailedRestored, fake.Replace());
        Assert.Equal(["export", "clear", "import " + New, "clear"], fake.Calls);
    }
}
