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

    [Fact]
    public void Tokenizer_stores_a_french_elision_with_and_without_its_article()
    {
        var words = JournalTokenizer.Words("Aux portes d'Ishgard, jusqu'à l'Alliance qu'Ul'dah soutient.");

        Assert.Contains("d'ishgard", words);
        Assert.Contains("ishgard", words);
        Assert.Contains("l'alliance", words);
        Assert.Contains("alliance", words);
        Assert.Contains("qu'ul'dah", words);
        Assert.Contains("ul'dah", words);
        Assert.Contains("uldah", words);
        Assert.DoesNotContain("jusqu", words);

        // A longer word before the apostrophe is no elision: its end stays part of it.
        Assert.Equal(["tataru's", "tatarus"], JournalTokenizer.Words("Tataru's").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Tokenizer_spells_out_ligatures_and_the_sharp_s()
    {
        Assert.Equal("coeur", Assert.Single(JournalTokenizer.Words("Cœur")));
        Assert.Equal("aether", Assert.Single(JournalTokenizer.Words("Æther")));
        Assert.Equal("strasse", Assert.Single(JournalTokenizer.Words("Straße")));
        Assert.Equal("strasse", Assert.Single(JournalTokenizer.Words("STRAẞE")));
    }

    [Theory]
    [InlineData("Ishgard", 65580u)]
    [InlineData("d'Ishgard", 65580u)]
    [InlineData("d’ishga", 65580u)]
    [InlineData("alliance", 65580u)]
    [InlineData("coeur", 65580u)]
    [InlineData("cœur", 65580u)]
    [InlineData("strasse", 65581u)]
    [InlineData("straße", 65581u)]
    [InlineData("uldah", 65581u)]
    [InlineData("Ul'dah", 65581u)]
    [InlineData("yshtola", 65581u)]
    [InlineData("Y'shtola", 65581u)]
    public void Elisions_ligatures_and_apostrophes_match_either_way(string query, uint expected)
    {
        var builder = new JournalTextIndex.Builder();
        builder.Add(65580, JournalTokenizer.Words("Le cœur de l'Alliance bat aux portes d'Ishgard."));
        builder.Add(65581, JournalTokenizer.Words("Y'shtola attend dans la Straße d'Ul'dah."));
        var index = builder.Build("2026.09.15.0000.0000", "fr");

        Assert.Equal([expected], index.Match(query)!.Order());
    }

    [Fact]
    public void A_single_character_of_a_script_without_spaces_matches_inside_a_longer_run()
    {
        var builder = new JournalTextIndex.Builder();
        builder.Add(65590, JournalTokenizer.Words("竜騎士の槍"));
        builder.Add(65591, JournalTokenizer.Words("光の戦士"));
        builder.Add(65592, JournalTokenizer.Words("竜"));
        var index = builder.Build("2026.09.15.0000.0000", "ja");

        // Beginning, middle and end of a run, and a run of one.
        Assert.Equal([65590u, 65592u], index.Match("竜")!.Order());
        Assert.Equal([65590u], index.Match("騎")!.Order());
        Assert.Equal([65590u], index.Match("槍")!.Order());
        Assert.Equal([65590u, 65591u], index.Match("の")!.Order());
        Assert.Equal([65590u, 65591u], index.Match("士")!.Order());
        Assert.Empty(index.Match("剣")!);

        // A pair still matches only itself.
        Assert.Equal([65590u], index.Match("竜騎士")!.Order());
    }

    [Fact]
    public void A_name_written_with_a_middle_dot_matches_with_or_without_it()
    {
        Assert.Equal(["シュ", "トラ", "ヤシ", "ュト"], JournalTokenizer.Words("ヤ・シュトラ").Order(StringComparer.Ordinal));

        var builder = new JournalTextIndex.Builder();
        builder.Add(65600, JournalTokenizer.Words("ヤ・シュトラは石の家にいる。"));
        builder.Add(65601, JournalTokenizer.Words("ヤシの木"));
        var index = builder.Build("2026.09.15.0000.0000", "ja");

        Assert.Equal([65600u], index.Match("ヤシュトラ")!.Order());
        Assert.Equal([65600u], index.Match("ヤ・シュトラ")!.Order());
        Assert.Equal([65600u], index.Match("シュトラ")!.Order());
        Assert.Equal([65600u, 65601u], index.Match("ヤ")!.Order());

        // Outside a run the dot only separates.
        Assert.Equal(["abc", "def"], JournalTokenizer.Words("abc・def").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Saves_use_their_own_temporary_file_and_only_a_startup_sweep_removes_leftovers()
    {
        using var dir = new TempDir();
        var index = Sample();
        JournalIndexStore.Save(dir.Path, index);
        var path = JournalIndexStore.PathFor(dir.Path, index.GameVersion, index.Language);

        // A temporary file another build is writing: the sweep of other versions leaves it alone.
        var leftover = path + ".abcdefgh" + JournalIndexStore.TempSuffix;
        File.WriteAllBytes(leftover, [1, 2, 3]);
        Assert.Equal(0, JournalIndexStore.DeleteOthers(dir.Path, path));
        Assert.True(File.Exists(leftover));

        // Saving again does not touch it either, and leaves no temporary file of its own.
        JournalIndexStore.Save(dir.Path, index);
        Assert.Equal([leftover], Directory.GetFiles(JournalIndexStore.Folder(dir.Path), "*" + JournalIndexStore.TempSuffix));

        Assert.Equal(1, JournalIndexStore.DeleteTemp(dir.Path));
        Assert.Equal(1, JournalIndexStore.DeleteOthers(dir.Path, null));
        Assert.Empty(Directory.GetFiles(JournalIndexStore.Folder(dir.Path)));
    }

    [Fact]
    public void Two_clients_in_different_languages_keep_each_others_index_and_build_in_flight()
    {
        using var dir = new TempDir();
        var english = Sample();
        JournalIndexStore.Save(dir.Path, english);
        var japanese = new JournalTextIndex.Builder().Build(english.GameVersion, "ja");
        JournalIndexStore.Save(dir.Path, japanese);
        var older = new JournalTextIndex.Builder().Build("2026.08.01.0000.0000", "en");
        JournalIndexStore.Save(dir.Path, older);
        File.SetLastWriteTimeUtc(JournalIndexStore.PathFor(dir.Path, older.GameVersion, older.Language), DateTime.UtcNow.AddDays(-31));
        // Another game install at another patch level, built recently: its index stays too.
        var otherInstall = new JournalTextIndex.Builder().Build("2026.07.01.0000.0000", "en");
        JournalIndexStore.Save(dir.Path, otherInstall);

        // After a build, only other game versions go: the other client's language stays.
        Assert.Equal(1, JournalIndexStore.DeleteOtherVersions(dir.Path, english.GameVersion));
        Assert.NotNull(JournalIndexStore.Load(dir.Path, english.GameVersion, english.Language));
        Assert.NotNull(JournalIndexStore.Load(dir.Path, japanese.GameVersion, japanese.Language));
        Assert.NotNull(JournalIndexStore.Load(dir.Path, otherInstall.GameVersion, otherInstall.Language));
        Assert.Null(JournalIndexStore.Load(dir.Path, older.GameVersion, older.Language));

        // A startup sweep with an age leaves a fresh temporary file (another client's build) and takes an old one.
        var path = JournalIndexStore.PathFor(dir.Path, english.GameVersion, english.Language);
        var fresh = path + ".fresh" + JournalIndexStore.TempSuffix;
        var stale = path + ".stale" + JournalIndexStore.TempSuffix;
        File.WriteAllBytes(fresh, [1]);
        File.WriteAllBytes(stale, [2]);
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-1));
        Assert.Equal(1, JournalIndexStore.DeleteTemp(dir.Path, TimeSpan.FromMinutes(10)));
        Assert.True(File.Exists(fresh));
        Assert.False(File.Exists(stale));
    }

    [Fact]
    public void A_build_waits_out_another_client_reading_the_index()
    {
        using var dir = new TempDir();
        var index = Sample();
        JournalIndexStore.Save(dir.Path, index);
        var path = JournalIndexStore.PathFor(dir.Path, index.GameVersion, index.Language);

        // The reader lets go after 50 ms, on its own thread so a busy thread pool cannot hold it past the retries.
        using (HeldFile.ReleasedAfter(path, TimeSpan.FromMilliseconds(50)))
        {
            JournalIndexStore.Save(dir.Path, index);
        }

        Assert.NotNull(JournalIndexStore.Load(dir.Path, index.GameVersion, index.Language));
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
