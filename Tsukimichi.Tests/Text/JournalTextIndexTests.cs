using Tsukimichi.Core.Model;
using Tsukimichi.Core.Text;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Text;

/// <summary>The journal search index (P9): tokenizer rules, prefix matching, the file round trip and the spoiler rules of the reader.</summary>
public sealed class JournalTextIndexTests
{
    [Fact]
    public void Tokenizer_lowercases_folds_accents_keeps_inner_apostrophes_and_drops_short_words()
    {
        var words = JournalTokenizer.Words("Y'shtola and Tataru of the Scions reach Ul’dah. Éorzéa, a 1st-class city!");

        Assert.Contains("y'shtola", words);
        Assert.Contains("ul'dah", words);
        Assert.Contains("eorzea", words);
        Assert.Contains("scions", words);
        Assert.Contains("1st", words);
        Assert.Contains("class", words);
        Assert.DoesNotContain("of", words);
        Assert.DoesNotContain("a", words);
        Assert.All(words, w => Assert.True(w.Length >= JournalTokenizer.MinWordLength));
    }

    [Fact]
    public void Tokenizer_cuts_scripts_without_spaces_into_pairs()
    {
        var words = JournalTokenizer.Words("光の戦士");

        Assert.Equal(["の戦", "光の", "戦士"], words.Order(StringComparer.Ordinal));
        Assert.True(JournalTokenizer.IsCjkWord("光の"));
    }

    [Fact]
    public void A_query_matches_word_prefixes_and_needs_every_term()
    {
        var index = Sample();

        Assert.Equal([65575u, 65576u], index.Match("ishga")!.Order());
        Assert.Equal([65576u], index.Match("ishgard dragon")!.Order());
        Assert.Empty(index.Match("ishgard eorzea")!);

        // Nothing long enough to search the journal for: the caller falls back to the name search alone.
        Assert.Null(index.Match("of a"));
        Assert.Null(index.Match("   "));
    }

    [Fact]
    public void Match_keeps_only_the_quests_the_filter_accepts()
    {
        var index = Sample();

        var completed = new HashSet<uint> { 65576 };
        Assert.Equal([65576u], index.Match("ishgard", completed.Contains)!.Order());
    }

    [Fact]
    public void A_pair_of_a_script_without_spaces_matches_exactly()
    {
        var index = Sample();

        Assert.Equal([65577u], index.Match("光の戦士")!.Order());
        Assert.Empty(index.Match("光の剣")!);
    }

    [Fact]
    public void The_file_round_trips_and_refuses_another_game_version_or_language()
    {
        var index = Sample();
        using var stream = new MemoryStream();
        index.Write(stream);

        stream.Position = 0;
        var read = JournalTextIndex.Read(stream, "2026.09.15.0000.0000", "en");
        Assert.NotNull(read);
        Assert.Equal(index.WordCount, read.WordCount);
        Assert.Equal(index.PostingCount, read.PostingCount);
        Assert.Equal(index.QuestCount, read.QuestCount);
        Assert.Equal(index.Match("ishgard")!.Order(), read.Match("ishgard")!.Order());

        stream.Position = 0;
        Assert.Null(JournalTextIndex.Read(stream, "2026.10.01.0000.0000", "en"));
        stream.Position = 0;
        Assert.Null(JournalTextIndex.Read(stream, "2026.09.15.0000.0000", "de"));
        Assert.Null(JournalTextIndex.Read(new MemoryStream([1, 2, 3]), "2026.09.15.0000.0000", "en"));
        Assert.Null(JournalTextIndex.Read(new MemoryStream(stream.ToArray()[..20]), "2026.09.15.0000.0000", "en"));
    }

    [Fact]
    public void The_store_keeps_one_file_per_game_version_and_removes_the_others()
    {
        using var dir = new TempDir();
        var index = Sample();

        var bytes = JournalIndexStore.Save(dir.Path, index);
        Assert.True(bytes > 0);
        var path = JournalIndexStore.PathFor(dir.Path, index.GameVersion, index.Language);
        Assert.True(File.Exists(path));
        Assert.EndsWith("journal-index.2026.09.15.0000.0000.en.bin", path);
        Assert.NotNull(JournalIndexStore.Load(dir.Path, index.GameVersion, index.Language));
        Assert.Null(JournalIndexStore.Load(dir.Path, "2026.10.01.0000.0000", index.Language));

        var older = new JournalTextIndex.Builder().Build("2026.08.01.0000.0000", "en");
        JournalIndexStore.Save(dir.Path, older);
        Assert.Equal(1, JournalIndexStore.DeleteOthers(dir.Path, path));
        Assert.True(File.Exists(path));
        Assert.Single(Directory.GetFiles(JournalIndexStore.Folder(dir.Path)));

        // A combined version key is hashed into a short, legal file name.
        var hashed = JournalIndexStore.PathFor(dir.Path, "ffxiv=2026.09.15|ex1=2026.09.10/ex5", "en");
        Assert.DoesNotContain("|", Path.GetFileName(hashed));
    }

    [Theory]
    [InlineData(QuestState.Completed, null, 5, JournalVisibility.All)]
    [InlineData(QuestState.DoneThisCycle, null, 5, JournalVisibility.All)]
    [InlineData(QuestState.Accepted, (byte)1, 5, 1)]
    [InlineData(QuestState.Accepted, (byte)3, 5, 3)]
    [InlineData(QuestState.Accepted, (byte)255, 5, 5)]
    [InlineData(QuestState.Accepted, (byte)9, 5, 5)]
    [InlineData(QuestState.Accepted, null, 5, 0)]
    [InlineData(QuestState.Accepted, (byte)255, 0, 0)]
    [InlineData(QuestState.Ready, null, 5, JournalVisibility.None)]
    [InlineData(QuestState.Blocked, null, 5, JournalVisibility.None)]
    [InlineData(QuestState.Unknown, null, 5, JournalVisibility.None)]
    public void The_reader_shows_a_journal_up_to_the_current_step_and_never_beyond(QuestState state, byte? sequence, byte stepCount, int expected)
    {
        Assert.Equal(expected, JournalVisibility.VisibleThrough(state, sequence, stepCount));
    }

    [Fact]
    public void Objectives_of_later_steps_stay_hidden_while_the_quest_is_in_the_journal()
    {
        // Step 2 of 4: objectives completed in sequences 1 and 2 show; 3 and the last (255) do not.
        Assert.True(JournalVisibility.ObjectiveVisible(QuestState.Accepted, 2, 4, 1));
        Assert.True(JournalVisibility.ObjectiveVisible(QuestState.Accepted, 2, 4, 2));
        Assert.False(JournalVisibility.ObjectiveVisible(QuestState.Accepted, 2, 4, 3));
        Assert.False(JournalVisibility.ObjectiveVisible(QuestState.Accepted, 2, 4, 255));
        Assert.True(JournalVisibility.ObjectiveVisible(QuestState.Accepted, 255, 4, 255));
        Assert.False(JournalVisibility.ObjectiveVisible(QuestState.Accepted, 2, 4, 0));
        Assert.True(JournalVisibility.ObjectiveVisible(QuestState.Completed, null, 4, 0));
        Assert.False(JournalVisibility.ObjectiveVisible(QuestState.Ready, null, 4, 1));
    }

    private static JournalTextIndex Sample()
    {
        // Made-up sentences: the tests never carry the game's own text.
        var builder = new JournalTextIndex.Builder();
        builder.Add(65575, JournalTokenizer.Words("A knight of Ishgard waits by the gate."));
        builder.Add(65576, JournalTokenizer.Words("The Ishgardian dragon hunt begins."));
        builder.Add(65577, JournalTokenizer.Words("光の戦士 and Eorzea."));
        builder.Add(65578, []);
        return builder.Build("2026.09.15.0000.0000", "en");
    }
}
