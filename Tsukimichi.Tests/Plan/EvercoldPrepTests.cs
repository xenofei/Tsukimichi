using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Plan;

/// <summary>
/// The Before Evercold card (feature plan v7, 1.20.0, N7; spec-1.20 "N7. Before Evercold"): which of the five lines a
/// character gets and when the game says each is done (the story, the journal at 10 free slots, job and role quests,
/// the newest expansion's roulette duties and flying), an earlier-expansion character's story line and no newest-expansion
/// lines, ticks per character, done lines folding only when the card is built, the all-done card, hiding with Undo, and
/// the card retiring on 8.0 data or on the curated early access day.
/// </summary>
public sealed class EvercoldPrepTests
{
    // Story: an Endwalker quest, then three Dawntrail main scenario quests in a row; the last ends the data's story.
    private const uint Msq0 = 69990;
    private const uint Msq1 = 70000;
    private const uint Msq2 = 70001;
    private const uint Msq3 = 70002;

    // Jobs and their quests (ClassJob row ids as in the sheet).
    private const byte Gladiator = 1;
    private const byte Paladin = 19;
    private const uint GladiatorOnly = 2;
    private const uint PaladinOnly = 20;
    private const uint Tanks = 156;
    private const uint Pledge = 65601;
    private const uint Paladin100 = 65603;
    private const uint TankRole = 65605;

    // Flying: two Dawntrail areas, each with one quest current; and Dawntrail duties.
    private const uint ZoneA = 1187;
    private const uint ZoneB = 1190;
    private const uint CurrentQuestA = 70100;
    private const uint CurrentQuestB = 70101;

    private static readonly DateTime Now = new(2026, 12, 5, 12, 0, 0, DateTimeKind.Utc);

    private static readonly LadderJob[] Jobs =
    [
        new(Gladiator, "gladiator", Gladiator, 0, 1, false, false),
        new(Paladin, "paladin", Gladiator, Pledge, 1, false, false),
    ];

    private static readonly ClassJobCategoryLookup Categories = ClassJobCategoryLookup.FromMembership(
    [
        new(GladiatorOnly, [Gladiator]),
        new(PaladinOnly, [Paladin]),
        new(Tanks, [Paladin]),
    ]);

    private static QuestRecord Msq(uint rowId, int order, uint? after, byte expansion = 5) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = $"Test_{rowId}",
        Name = $"Story {rowId}",
        Journal = expansion == 5
            ? new JournalRef(1, "Main Scenario (Dawntrail)", 30, "Dawntrail Main Scenario Quests", 300, "Dawntrail", 1000 + order)
            : new JournalRef(0, "Main Scenario (ARR/Heavensward/Stormblood/Shadowbringers/Endwalker)", 29, "Endwalker Main Scenario Quests", 290, "Endwalker", 1000 + order),
        Level = 100,
        Expansion = expansion,
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

    private static QuestRecord SideQuest(uint rowId) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = $"Test_{rowId}",
        Name = $"Side {rowId}",
        Journal = new JournalRef(3, "Sidequests", 40, "Dawntrail Sidequests", 400, "Dawntrail Sidequests", (int)rowId),
        Level = 95,
        Expansion = 5,
    };

    private static QuestCatalog Catalog(byte extraExpansion = 0)
    {
        var quests = new List<QuestRecord>
        {
            Msq(Msq0, 0, null, expansion: 4),
            Msq(Msq1, 1, Msq0),
            Msq(Msq2, 2, Msq1),
            Msq(Msq3, 3, Msq2),
            JobQuest(Pledge, 30, 176, Gladiator, GladiatorOnly),
            JobQuest(Paladin100, 100, 176, Paladin, PaladinOnly),
            JobQuest(TankRole, 100, 217, 0, Tanks, "Role Quests"),
            SideQuest(CurrentQuestA),
            SideQuest(CurrentQuestB),
        };
        if (extraExpansion != 0)
        {
            quests.Add(Msq(70010, 4, Msq3, extraExpansion) with { AddedIn = "8.0" });
        }

        return QuestCatalog.Build(quests);
    }

    private static readonly QuestCatalog Data = Catalog();

    private static readonly DutyRunIndex Duties = DutyRunIndex.From(
    [
        Duty(1, 101, DutyRunInfo.Dungeons, 5, "Vanguard"),
        Duty(2, 102, DutyRunInfo.Trials, 5, "Worqor Lar Dor"),
        Duty(3, 103, DutyRunInfo.Raids, 5, "Jeuno"),
        Duty(4, 104, DutyRunInfo.Trials, 5, "Worqor Lar Dor (Extreme)") with { HighEnd = true },
        Duty(5, 105, DutyRunInfo.Dungeons, 4, "The Aitiascope"),
    ]);

    private static DutyRunInfo Duty(uint condition, uint instance, uint type, byte expansion, string name) =>
        new(condition, instance, 2000 + condition, type, name, false, false)
        {
            Expansion = expansion,
            LevelRequired = 97,
            Roulettes = DutyRoulettes.Leveling,
        };

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

    private static CharacterSnapshot Character(byte journal = 5, IReadOnlyList<uint>? unlockedDuties = null) => new()
    {
        ContentId = 1,
        Name = "Michiru Tsukikage",
        LevelCap = 100,
        MaxExpansion = 5,
        JobLevels = new Dictionary<byte, short> { [Gladiator] = 100, [Paladin] = 100 },
        JournalSlotsUsed = journal,
        DutyRecords = new DutyRecordCapture(0, unlockedDuties ?? [101, 102, 103], []),
    };

    private static readonly PrepZone[] Zones =
    [
        new(ZoneA, "Shaaloani", [CurrentQuestA], Flying: null),
        new(ZoneB, "Heritage Found", [CurrentQuestB], Flying: null),
    ];

    private static IReadOnlyList<PrepLine> Lines(CharacterSnapshot snapshot, Dictionary<uint, QuestEvaluation> states, IReadOnlyList<PrepZone>? zones = null, DutyRunIndex? duties = null) =>
        EvercoldPrep.Lines(new PrepInputs(Data, snapshot, states, Ladder(), duties ?? Duties, zones ?? Zones));

    private static PrepLine Line(IReadOnlyList<PrepLine> lines, PrepLineKind kind) => Assert.Single(lines, l => l.Kind == kind);

    private static readonly uint[] AllStory = [Msq0, Msq1, Msq2, Msq3];
    private static readonly uint[] AllDone = [.. AllStory, Pledge, Paladin100, TankRole, CurrentQuestA, CurrentQuestB];

    private static readonly IReadOnlySet<PrepLineKind> NoTicks = new HashSet<PrepLineKind>();

    [Fact]
    public void A_caught_up_character_gets_all_five_lines_in_the_table_s_order_each_done()
    {
        var lines = Lines(Character(), States(AllDone));

        Assert.Equal([PrepLineKind.Story, PrepLineKind.Journal, PrepLineKind.Jobs, PrepLineKind.Duties, PrepLineKind.Flying], lines.Select(l => l.Kind));
        Assert.All(lines, l => Assert.True(l.Done, $"{l.Kind} is not done"));
    }

    [Fact]
    public void The_story_line_counts_what_is_left_to_the_data_s_last_main_scenario_quest()
    {
        Assert.Equal(Msq3, EvercoldPrep.StoryEnd(Data)?.RowId);

        var story = Line(Lines(Character(), States([Msq0, Msq1], (Msq2, QuestState.Ready))), PrepLineKind.Story);
        Assert.False(story.Done);
        Assert.False(story.Earlier);
        Assert.Equal(2, story.Left);
        Assert.Equal(Msq2, story.Next?.RowId);
        Assert.Equal(Msq3, story.StoryEnd?.RowId);
        Assert.Equal("7.5", PatchVersion.Series(story.StoryEnd?.AddedIn));

        // Done when the last quest is complete.
        Assert.True(Line(Lines(Character(), States(AllStory)), PrepLineKind.Story).Done);
    }

    [Fact]
    public void A_character_in_an_earlier_expansion_gets_the_story_line_and_no_newest_expansion_lines()
    {
        var lines = Lines(Character(), States([], (Msq0, QuestState.Ready)));

        var story = Line(lines, PrepLineKind.Story);
        Assert.True(story.Earlier);
        Assert.Equal((byte)4, story.InExpansion);
        Assert.Equal(4, story.Left);
        Assert.Equal([PrepLineKind.Story, PrepLineKind.Journal, PrepLineKind.Jobs], lines.Select(l => l.Kind));
    }

    [Fact]
    public void The_journal_line_is_done_at_ten_free_slots()
    {
        Assert.Equal(10, EvercoldPrep.JournalFreeSlots);
        var at20 = Line(Lines(Character(journal: 20), States(AllDone)), PrepLineKind.Journal);
        Assert.True(at20.Done);
        Assert.Equal(new JournalSlots(20, JournalSlots.GameCap), at20.Slots);

        // Nine free slots: C9 calls that room, but decision 9 asks for ten.
        Assert.False(Line(Lines(Character(journal: 21), States(AllDone)), PrepLineKind.Journal).Done);
        Assert.False(Line(Lines(Character(journal: 30), States(AllDone)), PrepLineKind.Journal).Done);
    }

    [Fact]
    public void The_jobs_line_lists_jobs_and_roles_with_a_quest_ready_on_any_job()
    {
        var jobs = Line(Lines(Character(), States([.. AllStory, Pledge], (Paladin100, QuestState.ReadyOnOtherJob), (TankRole, QuestState.Ready))), PrepLineKind.Jobs);

        Assert.False(jobs.Done);
        Assert.Equal([(uint)Paladin], jobs.JobIds);
        Assert.Equal([JobRole.Tank], jobs.Roles);
        Assert.Equal([Paladin100, TankRole], jobs.Quests.Select(q => q.RowId));

        // Blocked (not offered) job quests do not count: done when none is Ready.
        Assert.True(Line(Lines(Character(), States([.. AllStory, Pledge])), PrepLineKind.Jobs).Done);
    }

    [Fact]
    public void The_duties_line_counts_the_newest_expansion_s_roulette_duties_not_unlocked()
    {
        var duties = Line(Lines(Character(unlockedDuties: [101]), States(AllDone)), PrepLineKind.Duties);

        Assert.False(duties.Done);
        Assert.Equal((byte)5, duties.Expansion);

        // The Extreme trial and the Endwalker dungeon are not counted; a cleared duty is unlocked.
        Assert.Equal(["Worqor Lar Dor", "Jeuno"], duties.Duties.Select(d => d.Name));
        var cleared = Character() with { DutyRecords = new DutyRecordCapture(0, [101], [102, 103]) };
        Assert.True(Line(Lines(cleared, States(AllDone)), PrepLineKind.Duties).Done);

        // No records, or no duty index yet: no line.
        Assert.DoesNotContain(Lines(Character() with { DutyRecords = null }, States(AllDone)), l => l.Kind == PrepLineKind.Duties);
        Assert.DoesNotContain(Lines(Character(), States(AllDone), duties: DutyRunIndex.Empty), l => l.Kind == PrepLineKind.Duties);
    }

    [Fact]
    public void The_flying_line_reads_the_live_flags_else_the_current_quests()
    {
        // A stored character (flags unreadable): an area is left while a quest whose current it needs is not done.
        var stored = Line(Lines(Character(), States([.. AllStory, CurrentQuestA])), PrepLineKind.Flying);
        Assert.False(stored.Done);
        Assert.Equal([ZoneB], stored.Zones.Select(z => z.TerritoryId));

        // Live: the flags win, so a field current still missing keeps the area open even with its quests done.
        PrepZone[] live = [Zones[0] with { Flying = true }, Zones[1] with { Flying = false }];
        var flying = Line(Lines(Character(), States(AllDone), zones: live), PrepLineKind.Flying);
        Assert.Equal(["Heritage Found"], flying.Zones.Select(z => z.Name));
        Assert.True(Line(Lines(Character(), States([.. AllStory]), zones: [Zones[0] with { Flying = true }]), PrepLineKind.Flying).Done);

        // No flight index yet: no line.
        Assert.DoesNotContain(EvercoldPrep.Lines(new PrepInputs(Data, Character(), States(AllDone), Ladder(), Duties, null)), l => l.Kind == PrepLineKind.Flying);
    }

    [Fact]
    public void Done_lines_fold_only_when_the_card_is_built()
    {
        uint[] storyAndFlying = [.. AllStory, CurrentQuestA, CurrentQuestB];
        var lines = Lines(Character(journal: 25), States(storyAndFlying, (Paladin100, QuestState.Ready)));

        // Built: the story, duties and flying lines are done and fold, in the table's order; journal and jobs stay open.
        var card = PrepCard.Build(lines, NoTicks);
        Assert.Equal([PrepLineKind.Journal, PrepLineKind.Jobs], card.Open.Select(l => l.Line.Kind));
        Assert.All(card.Open, l => Assert.Equal(PrepCheck.Open, l.Check));
        Assert.Equal([PrepLineKind.Story, PrepLineKind.Duties, PrepLineKind.Flying], card.Done);

        // The player ticks the journal line: it stays in its place with "you said so", nothing folds.
        var ticked = new HashSet<PrepLineKind> { PrepLineKind.Journal };
        var afterTick = card.Update(lines, ticked);
        Assert.Equal([PrepLineKind.Journal, PrepLineKind.Jobs], afterTick.Open.Select(l => l.Line.Kind));
        Assert.Equal(PrepCheck.You, afterTick.Open[0].Check);
        Assert.Same(card.Done, afterTick.Done);

        // A new snapshot where the game says the jobs line is done: checked without "you said so", still in place.
        var later = Lines(Character(journal: 25), States([.. storyAndFlying, Paladin100]));
        var afterGame = afterTick.Update(later, ticked);
        Assert.Equal(PrepCheck.Game, afterGame.Open[1].Check);
        Assert.Equal(2, afterGame.Open.Count);

        // Unticking brings the line back; an update with nothing changed is the same card.
        var unticked = afterGame.Update(later, NoTicks);
        Assert.Equal(PrepCheck.Open, unticked.Open[0].Check);
        Assert.Same(unticked, unticked.Update(later, NoTicks));

        // Built again (a new selection, session or snapshot): the ticked and game-done lines fold.
        var rebuilt = PrepCard.Build(later, ticked);
        Assert.Empty(rebuilt.Open);
        Assert.Equal([PrepLineKind.Story, PrepLineKind.Journal, PrepLineKind.Jobs, PrepLineKind.Duties, PrepLineKind.Flying], rebuilt.Done);
    }

    [Fact]
    public void Everything_done_makes_the_all_done_card()
    {
        var card = PrepCard.Build(Lines(Character(), States(AllDone)), NoTicks);
        Assert.True(card.AllDone);
        Assert.Empty(card.Open);

        var one = PrepCard.Build(Lines(Character(journal: 25), States(AllDone)), NoTicks);
        Assert.False(one.AllDone);
        Assert.True(PrepCard.Build(Lines(Character(journal: 25), States(AllDone)), new HashSet<PrepLineKind> { PrepLineKind.Journal }).AllDone);
    }

    [Fact]
    public void Ticks_are_per_character_saved_and_taken_back()
    {
        using var tmp = new TempDir();
        var path = tmp.File("characters.json");
        var book = new CharacterSettingsBook(path);
        book.Edit(CharacterSettingChange.EvercoldTick(1, EvercoldPrep.TickId(PrepLineKind.Duties), true));
        book.Edit(CharacterSettingChange.EvercoldTick(1, EvercoldPrep.TickId(PrepLineKind.Flying), true));

        Assert.Equal([PrepLineKind.Duties, PrepLineKind.Flying], EvercoldPrep.Ticked(book.EvercoldTicks(1)).OrderBy(k => k));
        Assert.Empty(book.EvercoldTicks(2));
        Assert.Equal(["duties", "flying"], CharacterSettingsFile.Load(path)[1].EvercoldTicks);

        // Another client's book reads them.
        var other = new CharacterSettingsBook(path);
        other.ReloadFromDisk();
        Assert.Equal(["duties", "flying"], other.EvercoldTicks(1));

        // Unticking (and an unknown id) leaves nothing: a character with nothing set drops out of the file.
        book.Edit([CharacterSettingChange.EvercoldTick(1, "duties", false), CharacterSettingChange.EvercoldTick(1, "flying", false)]);
        Assert.Empty(book.EvercoldTicks(1));
        Assert.False(CharacterSettingsFile.Load(path).ContainsKey(1));
        Assert.Empty(EvercoldPrep.Ticked(["no such line"]));
    }

    [Fact]
    public void Hiding_is_per_character_and_undo_shows_it_again()
    {
        using var tmp = new TempDir();
        var path = tmp.File("characters.json");
        var book = new CharacterSettingsBook(path);

        // ×: hidden for this character only.
        book.Edit(CharacterSettingChange.Dismiss(1, EvercoldPrep.CardId, true));
        Assert.True(book.IsCardDismissed(1, EvercoldPrep.CardId));
        Assert.False(book.IsCardDismissed(2, EvercoldPrep.CardId));
        Assert.Equal([EvercoldPrep.CardId], CharacterSettingsFile.Load(path)[1].CardsDismissed);

        // Undo (or Show again on the dashboard) is the opposite edit; nothing is left in the file.
        book.Edit(CharacterSettingChange.Dismiss(1, EvercoldPrep.CardId, false));
        Assert.False(book.IsCardDismissed(1, EvercoldPrep.CardId));
        Assert.False(CharacterSettingsFile.Load(path).ContainsKey(1));

        // One click, Undo follows.
        Assert.Equal(Core.Ui.SafetyTier.None, Core.Ui.SafetyRules.TierOf(Core.Ui.GuardedAction.HideEvercoldCard));
        Assert.True(Core.Ui.SafetyRules.OffersUndo(Core.Ui.GuardedAction.HideEvercoldCard));
    }

    [Fact]
    public void The_card_retires_on_evercold_data_or_on_the_curated_early_access_day()
    {
        var launch = EvercoldPrep.FallbackLaunch;
        Assert.False(EvercoldPrep.IsRetired(Data, launch, Now));

        // The game data holds Evercold's expansion: 8.0 data.
        var evercold = Catalog(extraExpansion: EvercoldPrep.EvercoldExpansion);
        Assert.Equal(EvercoldPrep.EvercoldExpansion, EvercoldPrep.LatestExpansion(evercold));
        Assert.True(EvercoldPrep.IsRetired(evercold, launch, Now));

        // The early access day has come.
        Assert.False(EvercoldPrep.IsRetired(Data, launch, launch.EarlyAccessUtc.AddTicks(-1)));
        Assert.True(EvercoldPrep.IsRetired(Data, launch, launch.EarlyAccessUtc));

        // The day is curated data: a moved date moves the retirement.
        var moved = launch with { EarlyAccessUtc = new DateTime(2027, 2, 5, 0, 0, 0, DateTimeKind.Utc) };
        Assert.False(EvercoldPrep.IsRetired(Data, moved, launch.EarlyAccessUtc.AddDays(3)));
        Assert.True(EvercoldPrep.IsRetired(Data, moved, moved.EarlyAccessUtc));
    }

    [Fact]
    public void The_early_access_day_comes_from_the_shipped_curated_file()
    {
        var curated = CuratedData.Load(Tsukimichi.Tests.Data.FixtureCatalog.CuratedDir());
        var launch = EvercoldPrep.Launch(curated);

        Assert.Equal(EvercoldPrep.EvercoldExpansion, launch.Expansion);
        Assert.Equal("Evercold", launch.Name);
        Assert.Equal(new DateTime(2027, 1, 22, 0, 0, 0, DateTimeKind.Utc), launch.EarlyAccessUtc);
        Assert.Equal(DateTimeKind.Utc, launch.EarlyAccessUtc.Kind);
        Assert.StartsWith("https://", launch.Evidence, StringComparison.Ordinal);
        Assert.NotSame(EvercoldPrep.FallbackLaunch, launch);

        // Without an entry the card falls back on the same estimate.
        Assert.Same(EvercoldPrep.FallbackLaunch, EvercoldPrep.Launch(null));
    }

    [Fact]
    public void A_bad_launch_entry_is_skipped_with_a_warning()
    {
        using var tmp = new TempDir();
        File.WriteAllText(tmp.File(CuratedData.ExpansionLaunchesFileName),
            """{ "entries": { "6": { "name": "Evercold", "earlyAccess": "soon", "expected": true, "evidence": "https://example.com", "note": "x" }, "7": { "name": "Next", "earlyAccess": "2029-02-01", "expected": false, "evidence": "https://example.com", "note": "x" } } }""");
        var curated = CuratedData.Load(tmp.Path);

        Assert.False(curated.ExpansionLaunches.ContainsKey(6));
        Assert.Contains(curated.Warnings, w => w.Contains("earlyAccess", StringComparison.Ordinal));
        Assert.False(curated.ExpansionLaunches[7].Expected);
        Assert.Same(EvercoldPrep.FallbackLaunch, EvercoldPrep.Launch(curated));
    }
}
