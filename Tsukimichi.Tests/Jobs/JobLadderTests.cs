using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.GameData;

namespace Tsukimichi.Tests.JobLadders;

public sealed class JobLadderTests
{
    // Jobs (ClassJob row ids as in the sheet).
    private const byte Gladiator = 1;
    private const byte Archer = 5;
    private const byte Conjurer = 6;
    private const byte Thaumaturge = 7;
    private const byte Carpenter = 8;
    private const byte Paladin = 19;
    private const byte Monk = 20;
    private const byte Bard = 23;
    private const byte WhiteMage = 24;
    private const byte BlackMage = 25;
    private const byte DarkKnight = 32;

    // Categories.
    private const uint AllJobs = 1;
    private const uint GladiatorOnly = 2;
    private const uint PaladinOnly = 20;
    private const uint DisciplesOfMagic = JobLadder.DisciplesOfMagicCategory;
    private const uint DarkKnightOnly = 98;
    private const uint AllCombat = 142;
    private const uint Tanks = 156;
    private const uint PhysicalDps = 158;
    private const uint Casters = 159;

    // Quests.
    private const uint GladiatorOne = 65600;
    private const uint GladiatorTwo = 65601;
    private const uint GladiatorOtherStart = 65602;
    private const uint Pledge = 65603;
    private const uint PaladinOne = 65604;
    private const uint OurEnd = 65605;
    private const uint DarkKnightOne = 65606;
    private const uint TankRole = 65607;
    private const uint Capstone = 65608;
    private const uint PhysicalRole = 65609;
    private const uint CasterRole = 65610;
    private const uint Sidequest = 65611;

    private static readonly LadderJob[] Jobs =
    [
        new(Gladiator, "gladiator", Gladiator, 0, 1, false, false),
        new(Archer, "archer", Archer, 0, 3, false, false),
        new(Conjurer, "conjurer", Conjurer, 0, 4, false, false),
        new(Thaumaturge, "thaumaturge", Thaumaturge, 0, 3, false, false),
        new(Carpenter, "carpenter", Carpenter, 0, 0, true, false),
        new(Paladin, "paladin", Gladiator, Pledge, 1, false, false),
        new(Monk, "monk", 2, 0, 2, false, false),
        new(Bard, "bard", Archer, 0, 3, false, false),
        new(WhiteMage, "white mage", Conjurer, 0, 4, false, false),
        new(BlackMage, "black mage", Thaumaturge, 0, 3, false, false),
        new(DarkKnight, "dark knight", DarkKnight, OurEnd, 1, false, false),
        new(44, string.Empty, 44, 0, 0, false, false),
    ];

    private static readonly ClassJobCategoryLookup Categories = ClassJobCategoryLookup.FromMembership(
    [
        new(AllJobs, [Gladiator, Archer, Conjurer, Thaumaturge, Carpenter, Paladin, Monk, Bard, WhiteMage, BlackMage, DarkKnight]),
        new(GladiatorOnly, [Gladiator]),
        new(PaladinOnly, [Paladin]),
        new(DisciplesOfMagic, [Conjurer, Thaumaturge, WhiteMage, BlackMage]),
        new(DarkKnightOnly, [DarkKnight]),
        new(AllCombat, [Gladiator, Archer, Conjurer, Thaumaturge, Paladin, Monk, Bard, WhiteMage, BlackMage, DarkKnight]),
        new(Tanks, [Paladin, DarkKnight]),
        new(PhysicalDps, [Monk, Bard]),
        new(Casters, [BlackMage]),
    ]);

    private static QuestRecord Quest(uint rowId, byte level, uint genre, string genreName, uint required = 0, uint category = 0, uint section = JobLadder.ClassJobSectionId, string categoryName = "Job Quests") => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = $"Test_{rowId}",
        Name = $"Quest {rowId}",
        Journal = new JournalRef(section, "Section", genre / 10, categoryName, genre, genreName, (int)(genre << 16) | (int)(rowId - 65600)),
        Level = level,
        ClassJobRequired = required,
        ClassJobCategory = category,
    };

    private static QuestCatalog Catalog() => QuestCatalog.Build(
    [
        Quest(GladiatorOne, 1, 156, "Gladiator Quests", required: Gladiator, category: GladiatorOnly),
        Quest(GladiatorOtherStart, 1, 156, "Gladiator Quests", category: AllJobs),
        Quest(GladiatorTwo, 15, 156, "Gladiator Quests", category: GladiatorOnly),
        Quest(Pledge, 30, 176, "Paladin Quests", required: Gladiator, category: GladiatorOnly),
        Quest(PaladinOne, 35, 176, "Paladin Quests", required: Paladin, category: PaladinOnly),
        // The unlock quest asks for a level-50 character of any combat job, as the sheet does; it still opens the ladder.
        Quest(OurEnd, 50, 186, "Dark Knight Quests", category: AllCombat),
        Quest(DarkKnightOne, 35, 186, "Dark Knight Quests", required: DarkKnight, category: DarkKnightOnly),
        Quest(TankRole, 70, 217, "Tank Role Quests (Shadowbringers)", category: Tanks, categoryName: "Role Quests"),
        Quest(PhysicalRole, 70, 219, "Physical DPS Role Quests (Shadowbringers)", category: PhysicalDps, categoryName: "Role Quests"),
        Quest(CasterRole, 70, 220, "Magical Ranged DPS Role Quests (Shadowbringers)", category: Casters, categoryName: "Role Quests"),
        Quest(Capstone, 80, 221, "Role Quests (Shadowbringers)", category: AllCombat, categoryName: "Role Quests"),
        Quest(Sidequest, 5, 30, "Sidequests", required: Gladiator, category: GladiatorOnly, section: 3),
    ]);

    private static JobLadder Ladder() => JobLadder.Build(Catalog(), Jobs, Categories);

    private static Dictionary<uint, QuestEvaluation> States(params (uint RowId, QuestState State)[] states) =>
        states.ToDictionary(s => s.RowId, s => new QuestEvaluation(s.State, [], null, null, null));

    [Fact]
    public void Job_ladder_starts_with_its_class_quests_then_its_own_by_level()
    {
        var ladder = Ladder();

        var paladin = ladder.ForJob(Paladin);
        Assert.NotNull(paladin);
        Assert.Equal([GladiatorOne, GladiatorTwo, Pledge, PaladinOne], paladin.QuestRowIds);

        var gladiator = ladder.ForJob(Gladiator);
        Assert.NotNull(gladiator);
        Assert.Equal([GladiatorOne, GladiatorTwo, Pledge], gladiator.QuestRowIds);
    }

    [Fact]
    public void Single_job_categories_count_and_any_job_or_other_section_quests_do_not()
    {
        var ladder = Ladder();
        var gladiator = ladder.ForJob(Gladiator)!;

        // GladiatorTwo names no required job but its category admits only Gladiator.
        Assert.Contains(GladiatorTwo, gladiator.QuestRowIds);
        // The other start of the class is open to every job: not on the ladder.
        Assert.DoesNotContain(GladiatorOtherStart, gladiator.QuestRowIds);
        // A sidequest that requires Gladiator is not a job quest.
        Assert.DoesNotContain(Sidequest, gladiator.QuestRowIds);
    }

    [Fact]
    public void Job_without_a_class_opens_with_its_unlock_quest_and_has_no_class_quests()
    {
        var ladder = Ladder();

        var darkKnight = ladder.ForJob(DarkKnight);
        Assert.NotNull(darkKnight);
        Assert.Equal([OurEnd, DarkKnightOne], darkKnight.QuestRowIds);
    }

    [Fact]
    public void Jobs_without_quests_and_nameless_rows_have_no_ladder()
    {
        var ladder = Ladder();

        Assert.Null(ladder.ForJob(Carpenter));
        Assert.Null(ladder.ForJob(44));
        Assert.Equal([Gladiator, Paladin, DarkKnight], ladder.Jobs.Select(e => e.Job.RowId).ToArray());
    }

    [Fact]
    public void Role_quests_group_by_every_role_their_category_admits()
    {
        var ladder = Ladder();

        Assert.Equal([TankRole, Capstone], ladder.RoleLadder(JobRole.Tank));
        Assert.Equal([PhysicalRole, Capstone], ladder.RoleLadder(JobRole.Melee));
        Assert.Equal([PhysicalRole, Capstone], ladder.RoleLadder(JobRole.PhysicalRanged));
        Assert.Equal([CasterRole, Capstone], ladder.RoleLadder(JobRole.MagicalRanged));
        Assert.Equal([Capstone], ladder.RoleLadder(JobRole.Healer));

        // Role quests never land on a job's own ladder.
        Assert.All(ladder.Jobs, e => Assert.DoesNotContain(TankRole, e.QuestRowIds));
    }

    [Fact]
    public void Roles_come_from_the_sheet_role_and_disciples_of_magic_membership()
    {
        var ladder = Ladder();

        Assert.Equal(JobRole.Tank, ladder.RoleOf(Paladin));
        Assert.Equal(JobRole.Healer, ladder.RoleOf(WhiteMage));
        Assert.Equal(JobRole.Melee, ladder.RoleOf(Monk));
        Assert.Equal(JobRole.PhysicalRanged, ladder.RoleOf(Bard));
        Assert.Equal(JobRole.MagicalRanged, ladder.RoleOf(BlackMage));
        Assert.Null(ladder.RoleOf(Carpenter));
        Assert.Null(ladder.RoleOf(200));
    }

    [Fact]
    public void Progress_counts_completed_and_names_the_first_open_quest()
    {
        var ladder = Ladder();
        var paladin = ladder.ForJob(Paladin)!;

        var ready = ladder.Progress(paladin, States((GladiatorOne, QuestState.Completed), (GladiatorTwo, QuestState.Ready)), 15);
        Assert.Equal(new LadderProgress(1, 4, GladiatorTwo, 15, IsReadyNow: true, LevelReached: true), ready);
        Assert.False(ready.IsComplete);
        Assert.Equal(0.25f, ready.Fraction);

        var accepted = ladder.Progress(paladin, States((GladiatorOne, QuestState.Completed), (GladiatorTwo, QuestState.Accepted)), 15);
        Assert.True(accepted.IsReadyNow);

        var otherJob = ladder.Progress(paladin, States((GladiatorOne, QuestState.Completed), (GladiatorTwo, QuestState.ReadyOnOtherJob)), 15);
        Assert.True(otherJob.IsReadyNow);

        var blocked = ladder.Progress(paladin, States((GladiatorOne, QuestState.Completed), (GladiatorTwo, QuestState.Blocked)), 10);
        Assert.Equal(new LadderProgress(1, 4, GladiatorTwo, 15, IsReadyNow: false, LevelReached: false), blocked);

        // No evaluation at all reads as not done and not ready.
        var unknown = ladder.Progress(paladin, States(), 90);
        Assert.Equal(new LadderProgress(0, 4, GladiatorOne, 1, IsReadyNow: false, LevelReached: true), unknown);
    }

    [Fact]
    public void Progress_is_complete_when_every_quest_is_done_and_skips_foreclosed_ones()
    {
        var ladder = Ladder();
        var paladin = ladder.ForJob(Paladin)!;

        var complete = ladder.Progress(paladin, States(
            (GladiatorOne, QuestState.Completed), (GladiatorTwo, QuestState.Completed), (Pledge, QuestState.Completed), (PaladinOne, QuestState.Completed)), 90);
        Assert.Equal(new LadderProgress(4, 4, null, 0, IsReadyNow: false, LevelReached: true), complete);
        Assert.True(complete.IsComplete);

        // The start quest taken on another class is foreclosed: neither next nor counted.
        var foreclosed = ladder.Progress(paladin, States(
            (GladiatorOne, QuestState.Foreclosed), (GladiatorTwo, QuestState.Completed), (Pledge, QuestState.Ready)), 30);
        Assert.Equal(new LadderProgress(1, 3, Pledge, 30, IsReadyNow: true, LevelReached: true), foreclosed);
    }

    [Fact]
    public void Level_up_nudges_the_next_job_quest_once_when_it_is_ready()
    {
        var ladder = Ladder();
        var states = States((GladiatorOne, QuestState.Completed), (GladiatorTwo, QuestState.Completed), (Pledge, QuestState.Ready));
        var before = new Dictionary<byte, short> { [Gladiator] = 29, [Paladin] = 29 };
        var after = new Dictionary<byte, short> { [Gladiator] = 30, [Paladin] = 30 };

        // The class and its (not yet unlocked) job share the level slot and the next quest; one line, named for the class.
        var nudges = ladder.LevelUpNudges(before, after, states);
        Assert.Equal([new JobNudge(Jobs[0], 30, Pledge)], nudges);
    }

    [Fact]
    public void Level_up_names_the_job_once_it_is_unlocked_and_covers_role_quests()
    {
        var ladder = Ladder();
        var states = States(
            (GladiatorOne, QuestState.Completed), (GladiatorTwo, QuestState.Completed), (Pledge, QuestState.Completed),
            (PaladinOne, QuestState.Ready), (TankRole, QuestState.Ready));
        var before = new Dictionary<byte, short> { [Gladiator] = 34, [Paladin] = 34 };
        var after = new Dictionary<byte, short> { [Gladiator] = 35, [Paladin] = 35 };

        // The role quest is level 70: not this level-up's doing, so only the job quest is named.
        var nudges = ladder.LevelUpNudges(before, after, states);
        Assert.Equal([new JobNudge(Jobs[5], 35, PaladinOne)], nudges);

        // Reaching 70 opens the role ladder's next quest.
        var roleStates = States(
            (GladiatorOne, QuestState.Completed), (GladiatorTwo, QuestState.Completed), (Pledge, QuestState.Completed),
            (PaladinOne, QuestState.Completed), (TankRole, QuestState.Ready));
        var roleNudges = ladder.LevelUpNudges(
            new Dictionary<byte, short> { [Gladiator] = 69, [Paladin] = 69 },
            new Dictionary<byte, short> { [Gladiator] = 70, [Paladin] = 70 },
            roleStates);
        Assert.Equal([new JobNudge(Jobs[5], 70, TankRole)], roleNudges);
    }

    [Fact]
    public void Level_up_nudges_only_the_quest_the_new_level_unlocked()
    {
        var ladder = Ladder();
        var ready = States((GladiatorOne, QuestState.Completed), (GladiatorTwo, QuestState.Ready));

        // Level 15 is exactly the quest's level: nudged, also across a jump that passes it.
        Assert.Equal([new JobNudge(Jobs[0], 15, GladiatorTwo)], ladder.LevelUpNudges(new Dictionary<byte, short> { [Gladiator] = 14 }, new Dictionary<byte, short> { [Gladiator] = 15 }, ready));
        Assert.Equal([new JobNudge(Jobs[0], 16, GladiatorTwo)], ladder.LevelUpNudges(new Dictionary<byte, short> { [Gladiator] = 13 }, new Dictionary<byte, short> { [Gladiator] = 16 }, ready));
        // Ready on another job counts as open, and the nudge carries the state.
        Assert.Equal(
            [new JobNudge(Jobs[0], 15, GladiatorTwo, QuestState.ReadyOnOtherJob)],
            ladder.LevelUpNudges(new Dictionary<byte, short> { [Gladiator] = 14 }, new Dictionary<byte, short> { [Gladiator] = 15 }, States((GladiatorOne, QuestState.Completed), (GladiatorTwo, QuestState.ReadyOnOtherJob))));
        // Still blocked by something other than the level (a main scenario gate, say): announced as Blocked so the
        // notice can carry the blocker; the level-up is still what made the level gate pass.
        Assert.Equal(
            [new JobNudge(Jobs[0], 15, GladiatorTwo, QuestState.Blocked)],
            ladder.LevelUpNudges(new Dictionary<byte, short> { [Gladiator] = 14 }, new Dictionary<byte, short> { [Gladiator] = 15 }, States((GladiatorOne, QuestState.Completed), (GladiatorTwo, QuestState.Blocked))));
        Assert.Empty(ladder.LevelUpNudges(new Dictionary<byte, short> { [Gladiator] = 13 }, new Dictionary<byte, short> { [Gladiator] = 14 }, States((GladiatorOne, QuestState.Completed), (GladiatorTwo, QuestState.Blocked))));

        // Already available before the level-up (level 15 quest, 20 to 21): nothing new to announce.
        Assert.Empty(ladder.LevelUpNudges(new Dictionary<byte, short> { [Gladiator] = 20 }, new Dictionary<byte, short> { [Gladiator] = 21 }, ready));
        // Still one level short.
        Assert.Empty(ladder.LevelUpNudges(new Dictionary<byte, short> { [Gladiator] = 13 }, new Dictionary<byte, short> { [Gladiator] = 14 }, ready));
        // Already accepted: it is in the journal, so no nudge even at the unlocking level.
        Assert.Empty(ladder.LevelUpNudges(
            new Dictionary<byte, short> { [Gladiator] = 14 },
            new Dictionary<byte, short> { [Gladiator] = 15 },
            States((GladiatorOne, QuestState.Completed), (GladiatorTwo, QuestState.Accepted))));
    }

    [Fact]
    public void No_nudge_without_a_level_rise_a_ready_quest_or_a_previous_level()
    {
        var ladder = Ladder();
        var ready = States((GladiatorOne, QuestState.Ready));

        // Same level.
        Assert.Empty(ladder.LevelUpNudges(new Dictionary<byte, short> { [Gladiator] = 5 }, new Dictionary<byte, short> { [Gladiator] = 5 }, ready));
        // Level fell (a sync or a data hiccup).
        Assert.Empty(ladder.LevelUpNudges(new Dictionary<byte, short> { [Gladiator] = 6 }, new Dictionary<byte, short> { [Gladiator] = 5 }, ready));
        // First capture of the job.
        Assert.Empty(ladder.LevelUpNudges(new Dictionary<byte, short>(), new Dictionary<byte, short> { [Gladiator] = 5 }, ready));
        // Level rose but the next quest is still blocked.
        Assert.Empty(ladder.LevelUpNudges(new Dictionary<byte, short> { [Gladiator] = 4 }, new Dictionary<byte, short> { [Gladiator] = 5 }, States((GladiatorOne, QuestState.Blocked))));
        // A job without a ladder.
        Assert.Empty(ladder.LevelUpNudges(new Dictionary<byte, short> { [Carpenter] = 4 }, new Dictionary<byte, short> { [Carpenter] = 5 }, ready));
    }

    [Fact]
    public void Empty_ladder_answers_nothing()
    {
        Assert.Empty(JobLadder.Empty.Jobs);
        Assert.Null(JobLadder.Empty.ForJob(Paladin));
        Assert.Empty(JobLadder.Empty.RoleLadder(JobRole.Tank));
        Assert.True(JobLadder.Empty.Progress([], States(), 1).IsComplete);
    }
}
