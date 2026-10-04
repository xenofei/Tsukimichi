using System.Diagnostics;
using System.Text;
using Lumina.Data;
using Lumina.Text.ReadOnly;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Text;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Storage;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Tests that measure the process heap (<see cref="GC.GetTotalMemory(bool)"/>) run alone, after the parallel ones:
/// other tests allocating at the same time would be counted as theirs.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HeapMeasureCollection
{
    public const string Name = "Heap measurements";
}

/// <summary>
/// The journal text reader (P9) against the game's quest text sheets. Every assertion is about structure (how many
/// entries, which step they belong to, that macros resolve, which quests a word finds), never the text itself: the
/// game's text is never written into the repository. The whole-index test measures the heap, so the class runs in
/// <see cref="HeapMeasureCollection"/>.
/// </summary>
[Collection(HeapMeasureCollection.Name)]
public sealed class QuestTextTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    private const uint ComingToGridania = 65575;
    private const uint ChasingShadows = 65981;
    private const uint IshgardianJustice = 67590;
    private const uint SoYouWantToBeAGladiator = 65713;
    private const uint CloseToHome = 65621;

    [Theory]
    [InlineData("ClsGla001_00177", "quest/001/ClsGla001_00177")]
    [InlineData("JobDrk301_02054", "quest/020/JobDrk301_02054")]
    [InlineData("ManFst001_00039", "quest/000/ManFst001_00039")]
    [InlineData("", null)]
    [InlineData("NoNumber", null)]
    [InlineData("Short_12", null)]
    [InlineData("Bad/Path_00123", null)]
    public void The_sheet_name_is_the_script_id_under_its_number_folder(string internalId, string? expected)
    {
        Assert.Equal(expected, QuestTextReader.SheetName(internalId));
    }

    [Theory]
    [InlineData("TEXT_MANFST001_00039_SEQ_00", true, false, 0)]
    [InlineData("TEXT_MANFST001_00039_SEQ_12", true, false, 12)]
    [InlineData("TEXT_MANFST001_00039_TODO_03", true, true, 3)]
    [InlineData("TEXT_JOBREL521_02404_TODO_23_SYSTEM_000_000", false, false, 0)]
    [InlineData("TEXT_MANFST001_00039_MIOUNNE_000_20", false, false, 0)]
    [InlineData("TEXT_MANFST001_00039_SEQ_", false, false, 0)]
    public void Only_journal_and_objective_keys_are_read(string key, bool expected, bool objective, int index)
    {
        Assert.Equal(expected, QuestTextReader.TryParseKey(key, out var isObjective, out var number));
        if (expected)
        {
            Assert.Equal(objective, isObjective);
            Assert.Equal(index, number);
        }
    }

    [GameDataTheory]
    [InlineData(ComingToGridania, 1, 3, 1)]
    [InlineData(ChasingShadows, 4, 6, 4)]
    [InlineData(IshgardianJustice, 6, 8, 8)]
    public void A_known_quest_has_one_entry_per_step_plus_the_offer_and_the_turn_in(uint rowId, int steps, int entries, int objectives)
    {
        var quest = fixture.Bundle.Catalog.GetByRowId(rowId)!;
        var text = Read(quest);

        Assert.Equal(steps, quest.StepCount);
        Assert.Equal(entries, text.Journal.Count);
        Assert.Equal(quest.StepCount + 2, text.Journal.Count);
        Assert.Equal(Enumerable.Range(0, entries), text.Journal.Select(l => l.Index));
        Assert.Equal(objectives, text.Objectives.Count);

        // Every objective belongs to a step the quest has, the last one to the final sequence.
        Assert.All(text.Objectives, o => Assert.InRange(JournalVisibility.CurrentStep(o.Sequence, quest.StepCount), 1, quest.StepCount));
        Assert.Equal(255, text.Objectives[^1].Sequence);

        // In the journal at step 2 (or the last, for a one-step quest), the reader shows entries 0 to that step only.
        var sequence = quest.StepCount == 1 ? (byte)255 : (byte)2;
        var through = JournalVisibility.VisibleThrough(QuestState.Accepted, sequence, quest.StepCount);
        Assert.Equal(Math.Min(2, (int)quest.StepCount) + 1, text.Journal.Count(l => l.Index <= through));
        Assert.True(text.Journal.Count(l => l.Index <= through) < text.Journal.Count, "an accepted quest must not show its turn-in entry");
    }

    [GameDataFact]
    public void Objectives_that_share_a_step_carry_the_same_sequence()
    {
        var quest = fixture.Bundle.Catalog.GetByRowId(CloseToHome)!;
        var text = Read(quest);

        Assert.Equal(2, quest.StepCount);
        Assert.Equal([1, 1, 1, 255], text.Objectives.Select(o => (int)o.Sequence));
        Assert.Equal(3, text.Objectives.Count(o => JournalVisibility.ObjectiveVisible(QuestState.Accepted, 1, quest.StepCount, o.Sequence)));
    }

    [GameDataFact]
    public void A_quest_whose_sheet_holds_no_journal_reads_as_empty_and_a_missing_sheet_as_null()
    {
        var gladiator = fixture.Bundle.Catalog.GetByRowId(SoYouWantToBeAGladiator)!;
        var text = QuestTextReader.Read(Files, gladiator.InternalId, Language.English);
        Assert.NotNull(text);
        Assert.True(text.IsEmpty);

        Assert.Null(QuestTextReader.Read(Files, "NoSuch000_99999", Language.English));
        Assert.Null(QuestTextReader.Read(Files, string.Empty, Language.English));
        Assert.Null(QuestTextReader.Read(Files, gladiator.InternalId, Language.Korean));
    }

    [GameDataFact]
    public void Removed_quests_read_without_error()
    {
        var removed = fixture.Bundle.Catalog.All.Where(q => q.IsRemoved).ToList();
        Assert.NotEmpty(removed);
        var withText = 0;
        foreach (var quest in removed)
        {
            var text = QuestTextReader.Read(Files, quest.InternalId, Language.English);
            withText += text is { IsEmpty: false } ? 1 : 0;
        }

        output.WriteLine($"{removed.Count} removed quests, {withText} with journal text");
    }

    [GameDataFact]
    public void Every_macro_of_every_journal_entry_resolves_to_plain_text()
    {
        var excel = fixture.Game.Excel;
        var lines = 0;
        var withMacros = 0;
        var filled = 0;
        foreach (var quest in fixture.Bundle.Catalog.All)
        {
            if (QuestTextReader.Read(Files, quest.InternalId, Language.English) is not { } text)
            {
                continue;
            }

            foreach (var line in text.Journal.Concat(text.Objectives))
            {
                lines++;
                var neutral = QuestTextNeutral.Render(line.Text, "Forename Surname", excel, Language.English);

                // No macro byte (STX) survives, and nothing is left doubled up by a dropped macro.
                Assert.DoesNotContain('\u0002', neutral);
                Assert.DoesNotContain("  ", neutral);
                if (HasFork(line.Text))
                {
                    withMacros++;
                    filled += neutral.Length > line.Text.ExtractText().Trim().Length ? 1 : 0;
                }
            }
        }

        output.WriteLine($"{lines} lines, {withMacros} with a fork or reference, {filled} filled in");
        Assert.True(lines > 20_000, $"only {lines} lines");
        Assert.True(withMacros > 100, $"only {withMacros} lines with forks");

        // The forks and references the plain extraction leaves blank are filled in for nearly all of them.
        Assert.True(filled > withMacros * 0.9, $"{filled} of {withMacros} forks filled");
    }

    [Fact]
    public void The_player_name_is_the_stored_name_or_its_first_part()
    {
        // A made-up line with the macros the journal uses for the name: the full name and the forename.
        var line = ReadOnlySeString.FromMacroString("<string(gstr1)> and <split(<string(gstr1)>, ,1)>");
        Assert.Equal("Forename Surname and Forename", QuestTextNeutral.Render(line, "Forename Surname"));
        Assert.Equal($"{QuestTextNeutral.NameFallback} and {QuestTextNeutral.NameFallback}", QuestTextNeutral.Render(line));

        var fork = ReadOnlySeString.FromMacroString("a <if(gnum4,woman,man)> of action");
        Assert.Equal("a woman/man of action", QuestTextNeutral.Render(fork));

        var words = new StringBuilder();
        QuestTextNeutral.Words(line, words);
        Assert.Equal("and", words.ToString().Trim());
    }

    [GameDataFact]
    public void The_whole_journal_index_builds_off_the_excel_cache_and_round_trips()
    {
        var quests = fixture.Bundle.Catalog.All.Select(q => (q.RowId, q.InternalId)).ToList();
        GC.Collect();
        var before = GC.GetTotalMemory(forceFullCollection: true);
        var stopwatch = Stopwatch.StartNew();
        var index = QuestTextIndexer.Build(Files, quests, Language.English, "test-version");
        var elapsed = stopwatch.Elapsed;
        var after = GC.GetTotalMemory(forceFullCollection: true);

        using var dir = new TempDir();
        var bytes = JournalIndexStore.Save(dir.Path, index);
        var loaded = JournalIndexStore.Load(dir.Path, "test-version", "en");

        output.WriteLine($"{index.QuestCount} quests, {index.WordCount} words, {index.PostingCount} links, {bytes / 1024} KiB on disk, built in {elapsed.TotalSeconds:0.00} s, heap {(after - before) / (1024 * 1024)} MiB more");
        Assert.True(index.QuestCount > 5000, $"only {index.QuestCount} quests indexed");
        Assert.True(index.WordCount > 10_000, $"only {index.WordCount} words");
        Assert.True(bytes < 8 * 1024 * 1024, $"index file is {bytes} bytes");

        // The index alone stays resident: the sheets read to build it are not kept.
        Assert.True(after - before < 64L * 1024 * 1024, $"heap grew {(after - before) / (1024 * 1024)} MiB");

        Assert.NotNull(loaded);
        Assert.Equal(index.WordCount, loaded.WordCount);
        Assert.Equal(index.PostingCount, loaded.PostingCount);

        // A city named in the first Gridania quest's journal finds that quest; restricted to "completed" quests, only those.
        var hits = loaded.Match("gridania");
        Assert.NotNull(hits);
        Assert.Contains(ComingToGridania, hits);
        var completed = new HashSet<uint> { ComingToGridania, SoYouWantToBeAGladiator };
        Assert.Equal([ComingToGridania], loaded.Match("gridania", completed.Contains)!);
        Assert.DoesNotContain(SoYouWantToBeAGladiator, hits);
    }

    private QuestTextFiles Files => QuestTextFiles.From(fixture.Game);

    private QuestText Read(QuestRecord quest)
    {
        var sequences = QuestTextReader.ObjectiveSequences(fixture.Game.Excel, quest.RowId, Language.English);
        var text = QuestTextReader.Read(Files, quest.InternalId, Language.English, sequences);
        Assert.NotNull(text);
        return text;
    }

    private static bool HasFork(ReadOnlySeString text)
    {
        foreach (var payload in text)
        {
            if (payload.Type == ReadOnlySePayloadType.Macro
                && payload.MacroCode is Lumina.Text.Payloads.MacroCode.If or Lumina.Text.Payloads.MacroCode.Switch
                    or Lumina.Text.Payloads.MacroCode.EnNoun
                    or Lumina.Text.Payloads.MacroCode.String or Lumina.Text.Payloads.MacroCode.Split)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary><see cref="GameDataFactAttribute"/> for theories: runs only when TSUKIMICHI_GAME_PATH is set.</summary>
public sealed class GameDataTheoryAttribute : TheoryAttribute
{
    public GameDataTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(GameDataFactAttribute.EnvVar)))
        {
            Skip = $"{GameDataFactAttribute.EnvVar} is not set";
        }
    }
}
