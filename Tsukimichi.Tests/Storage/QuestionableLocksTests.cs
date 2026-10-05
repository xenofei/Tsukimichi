using System.Text.Json.Nodes;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>
/// <see cref="QuestionableLocks"/> (1.22.0): the extraction reads every arm of Questionable's <c>questPrereqs</c>
/// switch whole or fails, so a refresh never drops or misreads a lock silently; the file round-trips and a malformed one
/// is refused.
/// </summary>
public sealed class QuestionableLocksTests : IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    /// <summary>A made-up <c>IsQuestLocked</c> in the shape of Questionable's, with <paramref name="arms"/> as its switch arms.</summary>
    private static string Code(string arms) =>
        $$"""
        private bool IsQuestLocked(QuestId questId)
        {
            bool questPrereqs = questId.Value switch
            {
        {{arms}}
                _ => true
            };
            return !questPrereqs;
        }
        """;

    [Fact]
    public void Every_arm_form_is_read_whole()
    {
        var locks = QuestionableLocks.Extract(Code(
            """
                    // EX mounts
                    432 => AllMountsUnlocked(new ushort[] { 28, 29, 30 }),
                    /* gold saucer */ 576 => RaceChocoboRank40(),
                    1234 or 1235 => IsAchievementComplete(10),
                    3195 => IsUnlockLinkUnlocked(new(113)),
                    5 => _f.IsUnlockLinkUnlocked(5),
                    7 => !IsAchievementComplete(7),
                    8 => this.AllMountsUnlocked([1, 2]),
                    9 => IsAchievementComplete(new AchievementId(9)),
            """));

        Assert.Equal(
            [
                new QuestionableLock(65541, QuestionableLocks.UnlockLink, [5]),
                new QuestionableLock(65543, QuestionableLocks.Achievement, [7], Negated: true),
                new QuestionableLock(65544, QuestionableLocks.Mounts, [1, 2]),
                new QuestionableLock(65545, QuestionableLocks.Achievement, [9]),
                new QuestionableLock(65968, QuestionableLocks.Mounts, [28, 29, 30]),
                new QuestionableLock(66112, QuestionableLocks.RaceChocoboRank, [40]),
                new QuestionableLock(66770, QuestionableLocks.Achievement, [10]),
                new QuestionableLock(66771, QuestionableLocks.Achievement, [10]),
                new QuestionableLock(68731, QuestionableLocks.UnlockLink, [113]),
            ],
            locks,
            LockComparer.Instance);
    }

    /// <summary>The arms the first parser dropped or misread, and every other arm it cannot read whole: each fails, naming the arm.</summary>
    [Theory]
    [InlineData("1 => IsA(1) && IsB(2),", "quest 1 is not one check")]
    [InlineData("1 => IsAchievementComplete(1) || IsAchievementComplete(2),", "quest 1 is not one check")]
    [InlineData("1 => Achievement.Instance()->IsComplete(1),", "quest 1 is not one check")]
    [InlineData("1 => IsAchievementComplete(_someId),", "quest 1 is not one check")]
    [InlineData("1 => IsAchievementComplete(1)", "quest 1 is not one check")]
    [InlineData("1 when x > 2 => IsAchievementComplete(1),", "no list of quest ids")]
    [InlineData("QuestIds.Foo => IsAchievementComplete(1),", "no list of quest ids")]
    [InlineData("1 => IsAchievementComplete(1),\n        > 4000 => IsAchievementComplete(2),", "quest 1 is not one check")]
    [InlineData("1 => IsSomethingNew(4),", "no known kind of lock")]
    [InlineData("1 => IsAchievementComplete(1, 2),", "cannot take")]
    [InlineData("1 => IsUnlockLinkUnlocked(),", "cannot take")]
    [InlineData("1 => RaceChocoboRank40(5),", "cannot take")]
    [InlineData("1 => IsAchievementComplete(1),\n        1 => IsAchievementComplete(2),", "repeats quest 1")]
    [InlineData("1 or 2 => IsAchievementComplete(1),\n        2 => IsAchievementComplete(2),", "repeats quest 2")]
    public void An_arm_that_is_not_read_whole_fails_the_extraction(string arms, string message)
    {
        var ex = Assert.Throws<InvalidDataException>(() => QuestionableLocks.Extract(Code(arms)));
        Assert.Contains(message, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_with_no_switch_or_no_default_arm_fails()
    {
        Assert.Throws<InvalidDataException>(() => QuestionableLocks.Extract("bool x = true;"));
        Assert.Throws<InvalidDataException>(() => QuestionableLocks.Extract("bool questPrereqs = questId.Value switch { 1 => IsAchievementComplete(1), };"));
    }

    [Fact]
    public void Locks_round_trip_through_the_file_and_bad_files_are_refused()
    {
        var path = tmp.File(QuestionableLocks.FileName);
        QuestionableLock[] locks =
        [
            new(65543, QuestionableLocks.Achievement, [7], Negated: true),
            new(65968, QuestionableLocks.Mounts, [28, 29]),
            new(66112, QuestionableLocks.RaceChocoboRank, [40]),
        ];
        new QuestionableLocks("https://github.com/PunishXIV/Questionable", "new-main", new string('a', 40), "Questionable/Functions/QuestFunctions.cs", "2026-10-04", locks).Write(path);

        var read = QuestionableLocks.Load(path);
        Assert.Equal(locks, read.Locks, LockComparer.Instance);
        Assert.True(read.Of(65543)!.Negated);
        Assert.False(read.Of(65968)!.Negated);
        Assert.Null(read.Of(1));
        var text = File.ReadAllText(path);
        Assert.Contains("[65543, \"achievement\", [7], \"not\"]", text, StringComparison.Ordinal);
        Assert.Contains("[65968, \"mounts\", [28, 29]]", text, StringComparison.Ordinal);

        void Refused(JsonNode lockEntry, string message)
        {
            var root = JsonNode.Parse(text)!.AsObject();
            root["locks"] = new JsonArray(lockEntry);
            File.WriteAllText(path, root.ToJsonString());
            Assert.Contains(message, Assert.Throws<InvalidDataException>(() => QuestionableLocks.Load(path)).Message, StringComparison.Ordinal);
        }

        Refused(new JsonArray(65543, "achievement", new JsonArray(7), "maybe"), "locks[0]");
        Refused(new JsonArray(65543, "achievement", new JsonArray(7), true), "locks[0]");
        Refused(new JsonArray(65543, "achievement", new JsonArray(7), "not", "not"), "locks[0]");
        Refused(new JsonArray(65543, "title", new JsonArray(7)), "locks[0]");
        Refused(new JsonArray(65543, "achievement", new JsonArray()), "locks[0]");
        Refused(new JsonArray(43, "achievement", new JsonArray(7)), "locks[0]");
        Refused(new JsonArray(65543, "achievement", new JsonArray(0)), "no positive integer");

        var unsorted = JsonNode.Parse(text)!.AsObject();
        unsorted["locks"] = new JsonArray(new JsonArray(65968, "mounts", new JsonArray(28)), new JsonArray(65543, "achievement", new JsonArray(7)));
        File.WriteAllText(path, unsorted.ToJsonString());
        Assert.Contains("sorted", Assert.Throws<InvalidDataException>(() => QuestionableLocks.Load(path)).Message, StringComparison.Ordinal);

        File.WriteAllText(path, text.Replace(new string('a', 40), "db49ec1", StringComparison.Ordinal));
        Assert.Contains("commit", Assert.Throws<InvalidDataException>(() => QuestionableLocks.Load(path)).Message, StringComparison.Ordinal);
    }

    /// <summary>Locks compared by value, their ids included.</summary>
    private sealed class LockComparer : IEqualityComparer<QuestionableLock>
    {
        public static readonly LockComparer Instance = new();

        public bool Equals(QuestionableLock? x, QuestionableLock? y)
            => x is not null && y is not null && x.QuestRowId == y.QuestRowId && x.Kind == y.Kind && x.Negated == y.Negated && x.Ids.SequenceEqual(y.Ids);

        public int GetHashCode(QuestionableLock obj) => HashCode.Combine(obj.QuestRowId, obj.Kind, obj.Negated);
    }
}
