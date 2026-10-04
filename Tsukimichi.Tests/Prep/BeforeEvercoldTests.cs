using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Prep;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Prep;

/// <summary>
/// The Before Evercold card (feature plan v7, 1.20.0, N7): which lines a character gets and when each is checked off
/// (caught up or behind on the story, a full journal, jobs at the cap or not, events ending before 8.0), the story's end
/// read from the data, the card retiring on 8.0 data or on the date, and hiding it per character.
/// </summary>
public sealed class BeforeEvercoldTests
{
    // Story: three Dawntrail main scenario quests in a row, the last one the end of the data's story.
    private const uint Msq1 = 70000;
    private const uint Msq2 = 70001;
    private const uint Msq3 = 70002;

    // Jobs and their quests (ClassJob row ids as in the sheet).
    private const byte Gladiator = 1;
    private const byte Paladin = 19;
    private const byte Conjurer = 6;
    private const uint GladiatorOnly = 2;
    private const uint PaladinOnly = 20;
    private const uint ConjurerOnly = 23;
    private const uint Tanks = 156;
    private const uint GladiatorQuest = 65600;
    private const uint Pledge = 65601;
    private const uint Paladin90 = 65602;
    private const uint Paladin100 = 65603;
    private const uint Paladin110 = 65604;
    private const uint TankRole = 65605;
    private const uint ConjurerQuest = 65606;

    // An event.
    private const ushort Festival = 84;
    private const uint EventQuest = 65700;
    private const uint EventQuest2 = 65701;
    private const string Lodestone = "https://na.finalfantasyxiv.com/lodestone/topics/detail/abc";

    private static readonly DateTime Now = new(2026, 12, 5, 12, 0, 0, DateTimeKind.Utc);

    private static readonly LadderJob[] Jobs =
    [
        new(Gladiator, "gladiator", Gladiator, 0, 1, false, false),
        new(Conjurer, "conjurer", Conjurer, 0, 4, false, false),
        new(Paladin, "paladin", Gladiator, Pledge, 1, false, false),
    ];

    private static readonly ClassJobCategoryLookup Categories = ClassJobCategoryLookup.FromMembership(
    [
        new(GladiatorOnly, [Gladiator]),
        new(PaladinOnly, [Paladin]),
        new(ConjurerOnly, [Conjurer]),
        new(Tanks, [Paladin]),
        new(JobLadder.DisciplesOfMagicCategory, [Conjurer]),
    ]);

    private static QuestRecord Msq(uint rowId, int order, uint? after) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = $"Test_{rowId}",
        Name = $"Story {rowId}",
        Journal = new JournalRef(1, "Main Scenario (Dawntrail)", 30, "Dawntrail Main Scenario Quests", 300, "Dawntrail", 1000 + order),
        Level = 100,
        Expansion = 5,
        AddedIn = "7.56",
        PreviousQuests = after is { } previous ? new Prereq([previous], JoinKind.All) : Prereq.None,
    };

    private static QuestRecord JobQuest(uint rowId, byte level, uint genre, uint required, uint category, string categoryName = "Job Quests") => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = $"Test_{rowId}",
        Name = $"Job {rowId}",
        Journal = new JournalRef(JobLadder.ClassJobSectionId, "Class & Job Quests", genre / 10, categoryName, genre, $"Genre {genre}", (int)(genre << 16) | (int)(rowId - 65600)),
        Level = level,
        ClassJobRequired = required,
        ClassJobCategory = category,
    };

    private static QuestRecord EventQuestRecord(uint rowId) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = $"Test_{rowId}",
        Name = $"Event {rowId}",
        Journal = new JournalRef(3, "Sidequests", 40, "Seasonal Events", 400, "Seasonal Events", (int)rowId),
        Level = 15,
        Festival = Festival,
    };

    private static QuestCatalog Catalog(byte extraExpansion = 0)
    {
        var quests = new List<QuestRecord>
        {
            Msq(Msq1, 0, null),
            Msq(Msq2, 1, Msq1),
            Msq(Msq3, 2, Msq2),
            JobQuest(GladiatorQuest, 1, 156, Gladiator, GladiatorOnly),
            JobQuest(Pledge, 30, 176, Gladiator, GladiatorOnly),
            JobQuest(Paladin90, 90, 176, Paladin, PaladinOnly),
            JobQuest(Paladin100, 100, 176, Paladin, PaladinOnly),
            JobQuest(Paladin110, 110, 176, Paladin, PaladinOnly),
            JobQuest(TankRole, 100, 217, 0, Tanks, "Role Quests"),
            JobQuest(ConjurerQuest, 1, 166, Conjurer, ConjurerOnly),
            EventQuestRecord(EventQuest),
            EventQuestRecord(EventQuest2),
        };
        if (extraExpansion != 0)
        {
            quests.Add(Msq(70010, 3, Msq3) with { Expansion = extraExpansion, AddedIn = "8.0" });
        }

        return QuestCatalog.Build(quests);
    }

    private static readonly QuestCatalog Data = Catalog();

    private static JobLadder Ladder() => JobLadder.Build(Data, Jobs, Categories);

    private static Dictionary<uint, QuestEvaluation> States(IEnumerable<uint> completed, params (uint RowId, QuestState State)[] others)
    {
        var states = Data.All.ToDictionary(q => q.RowId, _ => new QuestEvaluation(QuestState.Blocked, [], null, null, null));
        foreach (var rowId in completed)
        {
            states[rowId] = new QuestEvaluation(QuestState.Completed, [], null, null, null);
        }

        foreach (var (rowId, state) in others)
        {
            states[rowId] = new QuestEvaluation(state, [], null, null, null);
        }

        return states;
    }

    private static CharacterSnapshot Character(short paladinLevel = 100, short conjurerLevel = 30, int journal = 5) => new()
    {
        ContentId = 1,
        Name = "Michiru Tsukikage",
        LevelCap = 100,
        MaxExpansion = 5,
        JobLevels = new Dictionary<byte, short> { [Gladiator] = paladinLevel, [Paladin] = paladinLevel, [Conjurer] = conjurerLevel },
        Accepted = Enumerable.Range(0, journal).Select(i => new AcceptedQuest((ushort)(30000 + i), 1)).ToList(),
    };

    private static IReadOnlyList<PrepLine> Lines(CharacterSnapshot snapshot, Dictionary<uint, QuestEvaluation> states, IReadOnlyList<RunningFestival>? running = null) =>
        BeforeEvercold.Lines(new PrepInputs(Data, snapshot, states, Ladder(), running ?? []));

    private static PrepLine Line(IReadOnlyList<PrepLine> lines, PrepKind kind) => Assert.Single(lines, l => l.Kind == kind);

    private static readonly uint[] AllStory = [Msq1, Msq2, Msq3];
    private static readonly uint[] PaladinDone = [GladiatorQuest, Pledge, Paladin90, Paladin100, TankRole];

    [Fact]
    public void The_story_s_end_is_the_last_main_scenario_quest_nothing_follows()
    {
        Assert.Equal(Msq3, BeforeEvercold.StoryEnd(Data)?.RowId);
        Assert.Null(BeforeEvercold.StoryEnd(QuestCatalog.Build([EventQuestRecord(EventQuest)])));
    }

    [Fact]
    public void A_caught_up_character_gets_every_line_checked_off()
    {
        var lines = Lines(Character(), States([.. AllStory, .. PaladinDone]));

        Assert.Equal([PrepKind.MainScenario, PrepKind.JobQuests, PrepKind.JournalRoom], lines.Select(l => l.Kind));
        Assert.All(lines, l => Assert.True(l.Done, $"{l.Kind} is not done"));
        Assert.Equal(Msq3, Line(lines, PrepKind.MainScenario).StoryEnd?.RowId);
        Assert.Null(Line(lines, PrepKind.MainScenario).Target);
    }

    [Fact]
    public void A_character_behind_on_the_story_is_pointed_at_the_next_quest()
    {
        var story = Line(Lines(Character(), States([Msq1, .. PaladinDone], (Msq2, QuestState.Ready))), PrepKind.MainScenario);

        Assert.False(story.Done);
        Assert.Equal(2, story.Left);
        Assert.Equal(Msq2, story.Target?.RowId);
    }

    [Fact]
    public void A_full_journal_asks_for_room_and_points_at_a_quest_to_drop()
    {
        var full = Character(journal: JournalSlots.GameCap);
        var journal = Line(Lines(full, States(AllStory)), PrepKind.JournalRoom);
        Assert.False(journal.Done);
        Assert.Equal(0, journal.Left);
        Assert.Equal(JournalRoom.Full, journal.Slots.Room);

        // Nearly full still asks; more than three slots free is done.
        Assert.False(Line(Lines(Character(journal: JournalSlots.GameCap - JournalSlots.NearLeft), States(AllStory)), PrepKind.JournalRoom).Done);
        Assert.True(Line(Lines(Character(journal: JournalSlots.GameCap - JournalSlots.NearLeft - 1), States(AllStory)), PrepKind.JournalRoom).Done);

        // The target is the first journal quest to hand in or safe to drop: here one at its last step.
        var withEvent = full with { Accepted = [new AcceptedQuest(QuestRecord.ToQuestId(EventQuest), 255), .. full.Accepted.Skip(1)] };
        Assert.Equal(EventQuest, Line(Lines(withEvent, States(AllStory)), PrepKind.JournalRoom).Target?.RowId);
    }

    [Fact]
    public void Job_quests_count_only_jobs_at_the_cap_and_quests_up_to_it()
    {
        // Paladin at 100 with its level-100 quest and the tank role quest left; the level-110 quest is above the cap.
        var jobs = Line(Lines(Character(), States([.. AllStory, GladiatorQuest, Pledge, Paladin90])), PrepKind.JobQuests);
        Assert.False(jobs.Done);
        Assert.Equal(100, jobs.LevelCap);
        Assert.Equal([((uint)Paladin, (JobRole?)null, 1), (0u, (JobRole?)JobRole.Tank, 1)], jobs.Jobs.Select(j => (j.JobId, j.Role, j.Left)));
        Assert.Equal(Paladin100, jobs.Target?.RowId);

        // Conjurer at 30 is not at the cap, so its quest left does not count; nobody at the cap means no line at all.
        Assert.DoesNotContain(jobs.Jobs, j => j.JobId == Conjurer);
        Assert.DoesNotContain(Lines(Character(paladinLevel: 99), States(AllStory)), l => l.Kind == PrepKind.JobQuests);
    }

    [Fact]
    public void A_class_at_the_cap_speaks_for_itself_until_its_job_is_unlocked()
    {
        // Pledge (the job's unlock quest) not done: the gladiator ladder counts, which holds only the class quests.
        var asClass = Line(Lines(Character(), States(AllStory)), PrepKind.JobQuests);
        Assert.Contains(asClass.Jobs, j => j.JobId == Gladiator);
        Assert.DoesNotContain(asClass.Jobs, j => j.JobId == Paladin);

        // Unlocked: the paladin ladder speaks, the class no longer does.
        var asJob = Line(Lines(Character(), States([.. AllStory, GladiatorQuest, Pledge])), PrepKind.JobQuests);
        Assert.Contains(asJob.Jobs, j => j.JobId == Paladin);
        Assert.DoesNotContain(asJob.Jobs, j => j.JobId == Gladiator);
    }

    [Fact]
    public void A_character_without_a_known_cap_gets_no_job_line()
    {
        Assert.DoesNotContain(Lines(Character() with { LevelCap = 0 }, States(AllStory)), l => l.Kind == PrepKind.JobQuests);
    }

    [Fact]
    public void Events_ending_before_evercold_with_quests_left_are_listed_and_checked_off()
    {
        var info = new FestivalInfo("Starlight Celebration", null, new DateTime(2026, 12, 31, 14, 59, 0, DateTimeKind.Utc), false, Lodestone);
        IReadOnlyList<RunningFestival> Running(FestivalInfo festival, Dictionary<uint, QuestEvaluation> states) =>
            SeasonalNow.Running(Data, Character() with { ActiveFestivals = [Festival] }, states, new Dictionary<ushort, FestivalInfo> { [Festival] = festival }, Now);

        var left = States(AllStory, (EventQuest, QuestState.Ready), (EventQuest2, QuestState.Accepted));
        var line = Line(Lines(Character(), left, Running(info, left)), PrepKind.Event);
        Assert.False(line.Done);
        Assert.Equal(2, line.Left);
        Assert.Equal(EventQuest2, line.Target?.RowId);
        Assert.Equal("Starlight Celebration", line.Festival?.Name);

        // Every quest done: the line stays, checked.
        var done = States([.. AllStory, EventQuest, EventQuest2]);
        Assert.True(Line(Lines(Character(), done, Running(info, done)), PrepKind.Event).Done);

        // Ending after Evercold, or with no known end, it is not listed.
        var later = info with { End = BeforeEvercold.ExpectedUtc.AddDays(3) };
        Assert.DoesNotContain(Lines(Character(), left, Running(later, left)), l => l.Kind == PrepKind.Event);
        var undated = info with { End = null };
        Assert.DoesNotContain(Lines(Character(), left, Running(undated, left)), l => l.Kind == PrepKind.Event);
    }

    [Fact]
    public void The_card_retires_on_evercold_data_or_once_the_date_passes()
    {
        Assert.False(BeforeEvercold.IsRetired(Data, Now));
        Assert.True(BeforeEvercold.Shows(Data, Now, dismissed: false));

        // The game data holds Evercold's expansion: 8.0 data.
        var evercold = Catalog(extraExpansion: BeforeEvercold.EvercoldExpansion);
        Assert.Equal(BeforeEvercold.EvercoldExpansion, BeforeEvercold.LatestExpansion(evercold));
        Assert.True(BeforeEvercold.IsRetired(evercold, Now));

        // The expected early-access day has come.
        Assert.False(BeforeEvercold.IsRetired(Data, BeforeEvercold.ExpectedUtc.AddTicks(-1)));
        Assert.True(BeforeEvercold.IsRetired(Data, BeforeEvercold.ExpectedUtc));
        Assert.False(BeforeEvercold.Shows(Data, BeforeEvercold.ExpectedUtc.AddDays(1), dismissed: false));
    }

    [Fact]
    public void Hiding_the_card_is_per_character_and_undoable()
    {
        Assert.False(BeforeEvercold.Shows(Data, Now, dismissed: true));

        using var tmp = new TempDir();
        var book = new CharacterSettingsBook(tmp.File("characters.json"));
        book.Edit(CharacterSettingChange.Dismiss(1, BeforeEvercold.CardId, true));
        Assert.True(book.IsCardDismissed(1, BeforeEvercold.CardId));
        Assert.False(book.IsCardDismissed(2, BeforeEvercold.CardId));
        Assert.Equal([BeforeEvercold.CardId], CharacterSettingsFile.Load(tmp.File("characters.json"))[1].CardsDismissed);

        // Undo shows it again; a character left with nothing set drops out of the file.
        book.Edit(CharacterSettingChange.Dismiss(1, BeforeEvercold.CardId, false));
        Assert.False(book.IsCardDismissed(1, BeforeEvercold.CardId));
        Assert.False(CharacterSettingsFile.Load(tmp.File("characters.json")).ContainsKey(1));
    }
}
