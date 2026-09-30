using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// <see cref="BlockerText"/> on synthetic catalogs: one phrase per requirement kind, the decisive order, the step
/// text, and the rule that the state word opens every Status string. The fixture-backed cases (a Dragoon quest gated
/// by the main scenario, an allied society rank) are in <see cref="BlockerTextFixtureTests"/>.
/// </summary>
public class BlockerTextTests
{
    private const byte Pelupelu = 3;
    private const uint DisciplesOfTheHand = 33;
    private const uint TheVault = 7;
    private const uint UnnamedDuty = 8;

    private static readonly BlockerNames Names = new()
    {
        Tribe = id => id == Pelupelu ? "Pelupelu" : string.Empty,
        JobAbbreviation = id => id switch { Gladiator => "GLA", Conjurer => "CNJ", Paladin => "PLD", _ => string.Empty },
        ClassJobCategory = id => id == DisciplesOfTheHand ? "Disciple of the Hand" : string.Empty,
        Duty = id => id == TheVault ? "The Vault" : string.Empty,
    };

    /// <summary>A side quest (journal section 3): the shared factory files quests under section 1, which is Dawntrail's main scenario.</summary>
    private static QuestRecord Quest(uint rowId, string? name = null) =>
        Fixture.Quest(rowId, name) with { Journal = new JournalRef(3, "Side Quests", 1, "Category", 1, "Genre", (int)rowId) };

    private static QuestRecord Msq(uint rowId, string name) =>
        Fixture.Quest(rowId, name) with { Journal = new JournalRef(0, "Main Scenario", 1, "Seventh Umbral Era", 1, "Seventh Umbral Era", (int)rowId) };

    private static (QuestEvaluation Evaluation, BlockerNames Names) Resolve(QuestRecord q, CharacterSnapshot s, QuestCatalog? c = null, EvalContext? ctx = null)
    {
        var catalog = c ?? Catalog(q);
        return (StateResolver.Resolve(q, s, catalog, ctx ?? EvalContext.Default), Names with { Catalog = catalog });
    }

    private static string For(QuestRecord q, CharacterSnapshot s, QuestCatalog? c = null, EvalContext? ctx = null)
    {
        var (evaluation, names) = Resolve(q, s, c, ctx);
        return BlockerText.For(evaluation, q, names);
    }

    private static string Status(QuestRecord q, CharacterSnapshot s, QuestCatalog? c = null, EvalContext? ctx = null)
    {
        var (evaluation, names) = Resolve(q, s, c, ctx);
        return BlockerText.StatusText(evaluation, q, names);
    }

    [Fact]
    public void Ready_and_done_quests_have_no_blocker_and_show_the_state_name_alone()
    {
        var quest = Quest(Target);

        Assert.Equal(string.Empty, For(quest, Snapshot()));
        Assert.Equal("Ready", Status(quest, Snapshot()));
        Assert.Equal(string.Empty, For(quest, Snapshot(Target)));
        Assert.Equal("Completed", Status(quest, Snapshot(Target)));

        var daily = quest with { IsRepeatable = true, RepeatInterval = StateNames.DailyInterval };
        var done = Snapshot() with { DailyDone = new Dictionary<ushort, byte> { [daily.QuestId] = 1 } };
        Assert.Equal("Done today", Status(daily, done));
    }

    [Fact]
    public void Level_reads_as_Lv_and_names_the_pinned_job()
    {
        Assert.Equal("Lv 80", For(Quest(Target) with { Level = 80 }, Snapshot()));
        Assert.Equal("Blocked · Lv 80", Status(Quest(Target) with { Level = 80 }, Snapshot()));

        // Pinned to Paladin while a Gladiator is active: the whole gate is reaching 80 on that job.
        var pinned = Quest(Target) with { Level = 80, ClassJobRequired = Paladin };
        Assert.Equal("Lv 80 on PLD", For(pinned, Snapshot()));
        Assert.Equal("Lv 80 on PLD", For(pinned, Snapshot() with { JobLevels = Levels((Gladiator, 90), (Paladin, 50)) }));
    }

    [Fact]
    public void Category_reads_as_Job_any_with_the_current_job()
    {
        var quest = Quest(Target) with { ClassJobCategory = DisciplesOfTheHand };
        var ctx = new EvalContext { ClassJobs = new Jobs((DisciplesOfTheHand, [Conjurer])) };

        Assert.Equal("Job: any Disciple of the Hand, you are GLA", For(quest, Snapshot(), ctx: ctx));

        // A category the sheet already calls "Any …" is not doubled.
        var names = Names with { ClassJobCategory = _ => "Any Disciple of War or Magic" };
        var evaluation = StateResolver.Resolve(quest, Snapshot(), Catalog(quest), ctx);
        Assert.Equal("Job: Any Disciple of War or Magic, you are GLA", BlockerText.For(evaluation, quest, names));
    }

    [Fact]
    public void Prerequisite_reads_as_after_and_after_MSQ_for_a_main_scenario_quest()
    {
        var side = Quest(A, "Peace for Thanalan");
        var msq = Msq(B, "Shadowbringers");

        var afterSide = Quest(Target) with { PreviousQuests = new Prereq([A], JoinKind.All) };
        Assert.Equal("after: Peace for Thanalan", For(afterSide, Snapshot(), Catalog(side, afterSide)));

        var afterMsq = Quest(Target) with { PreviousQuests = new Prereq([B], JoinKind.All) };
        Assert.Equal("after MSQ: Shadowbringers", For(afterMsq, Snapshot(), Catalog(msq, afterMsq)));
        Assert.Equal("Blocked · after MSQ: Shadowbringers", Status(afterMsq, Snapshot(), Catalog(msq, afterMsq)));

        // A prerequisite the catalog does not know still reads as a prerequisite.
        Assert.Equal("after: quest 65601", For(afterMsq, Snapshot()));
    }

    [Fact]
    public void Prerequisites_with_states_skip_the_completed_ones_and_take_the_nearest_Any_branch()
    {
        // A needs C needs D (three quests left); B stands alone (one left).
        var d = Quest(D, "Deep");
        var c = Quest(C, "Chain") with { PreviousQuests = new Prereq([D], JoinKind.All) };
        var a = Quest(A, "Long road") with { PreviousQuests = new Prereq([C], JoinKind.All) };
        var b = Quest(B, "Short road");
        var any = Quest(Target) with { PreviousQuests = new Prereq([A, B], JoinKind.Any) };
        var all = Quest(E) with { PreviousQuests = new Prereq([A, B], JoinKind.All) };
        var catalog = Catalog(d, c, a, b, any, all);
        var names = Names with { Catalog = catalog };

        var states = StateResolver.ResolveAll(catalog, Snapshot(), EvalContext.Default);
        Assert.Equal("after: Short road", BlockerText.For(states[Target], any, names, states));
        // Without states the first listed prerequisite is named.
        Assert.Equal("after: Long road", BlockerText.For(states[Target], any, names));

        // Through an All join the first prerequisite still to do is named.
        Assert.Equal("after: Long road", BlockerText.For(states[E], all, names, states));
        var aDone = StateResolver.ResolveAll(catalog, Snapshot(A), EvalContext.Default);
        Assert.Equal("after: Short road", BlockerText.For(aDone[E], all, names, aDone));
    }

    [Fact]
    public void Grand_Company_membership_and_rank_read_as_Grand_Company()
    {
        Assert.Equal("Grand Company: Maelstrom", For(Quest(Target) with { GrandCompany = 1 }, Snapshot() with { GrandCompany = 2 }));

        var rank = Quest(Target) with { GrandCompany = 1, GrandCompanyRank = 5 };
        var member = Snapshot() with { GrandCompany = 1, GcRanks = [0, 2, 0, 0] };
        Assert.Equal("Grand Company: Sergeant Third Class", For(rank, member));
        Assert.Equal("Blocked · Grand Company: Sergeant Third Class", Status(rank, member));
    }

    [Fact]
    public void Allied_society_rank_reputation_allowance_and_offer_each_have_a_phrase()
    {
        Assert.Equal("Rank: Trusted with the Pelupelu", For(Quest(Target) with { BeastTribe = Pelupelu, BeastRank = 4 }, Snapshot()));

        var reputation = Quest(Target) with { BeastTribe = Pelupelu, BeastRank = 1, BeastValue = 120 };
        var standing = Snapshot() with { Tribes = new Dictionary<byte, TribeStanding> { [Pelupelu] = new(1, 40) } };
        Assert.Equal("Reputation: 80 more with the Pelupelu", For(reputation, standing));

        var daily = Quest(Target) with { BeastTribe = Pelupelu, BeastRank = 1, IsRepeatable = true, RepeatInterval = StateNames.DailyInterval };
        Assert.Equal("Allowance: none left today", For(daily, standing));

        var offered = standing with { TribeAllowance = 3 };
        var ctx = new EvalContext { TodaysDailyOffer = new HashSet<ushort>() };
        Assert.Equal("Not offered today", For(daily, offered, ctx: ctx));

        // Without a society name the clause is dropped rather than left dangling.
        var evaluation = StateResolver.Resolve(reputation, standing, Catalog(reputation), EvalContext.Default);
        Assert.Equal("Reputation: 80 more", BlockerText.For(evaluation, reputation, BlockerNames.Default));
    }

    [Fact]
    public void Duty_names_the_duty_or_counts_what_is_left()
    {
        Assert.Equal("Duty: The Vault", For(Quest(Target) with { InstanceContentRequired = [TheVault] }, Snapshot()));
        Assert.Equal("Duty: 1 to clear", For(Quest(Target) with { InstanceContentRequired = [UnnamedDuty] }, Snapshot()));
        Assert.Equal("Duty: 2 to clear", For(Quest(Target) with { InstanceContentRequired = [UnnamedDuty, 9] }, Snapshot()));
        Assert.Equal("Duty: 1 to clear", For(Quest(Target) with { InstanceContentRequired = [UnnamedDuty, 9], InstanceJoin = JoinKind.Any }, Snapshot()));
    }

    [Fact]
    public void Seasonal_reads_as_not_running_or_ended_once_the_character_missed_it()
    {
        var seasonal = Quest(Target) with { Festival = 9 };

        Assert.Equal("Seasonal: not running", For(seasonal, Snapshot()));
        Assert.Equal("Blocked · Seasonal: not running", Status(seasonal, Snapshot()));

        var past = new EvalContext { FestivalIsPast = _ => true };
        Assert.Equal("Seasonal: ended", For(seasonal, Snapshot(), ctx: past));
        Assert.Equal("Locked out · Seasonal: ended", Status(seasonal, Snapshot(), ctx: past));
    }

    [Fact]
    public void Mount_house_expansion_and_level_cap_have_a_phrase()
    {
        Assert.Equal("Mount", For(Quest(Target) with { MountRequired = true }, Snapshot(), ctx: new EvalContext { HasMount = false }));
        Assert.Equal("House", For(Quest(Target) with { HouseRequired = true }, Snapshot(), ctx: new EvalContext { HasHouse = false }));
        Assert.Equal("Expansion: Stormblood", For(Quest(Target) with { Expansion = 2 }, Snapshot() with { MaxExpansion = 1 }));
        Assert.Equal("Lv 60, above your cap", For(Quest(Target) with { Level = 60 }, Snapshot() with { LevelCap = 50 }));
    }

    [Fact]
    public void Foreclosure_names_the_completed_lock()
    {
        var other = Quest(B, "The Ul'dahn Envoy");
        var quest = Quest(Target) with { QuestLocks = [B] };

        Assert.Equal("closed by: The Ul'dahn Envoy", For(quest, Snapshot(B), Catalog(other, quest)));
        Assert.Equal("Locked out · closed by: The Ul'dahn Envoy", Status(quest, Snapshot(B), Catalog(other, quest)));
    }

    [Fact]
    public void Not_checked_kinds_say_so_and_the_status_line_does_not_double_the_words()
    {
        var gated = new EvalContext { IsAchievementGated = _ => true };
        var unloaded = Snapshot() with { AchievementsLoaded = false };
        Assert.Equal("Not checked: achievements", For(Quest(Target), unloaded, ctx: gated));
        Assert.Equal("Not checked · achievements", Status(Quest(Target), unloaded, ctx: gated));

        // Kinds the resolver does not judge yet (feature plan T3), as a blocked evaluation would carry them.
        var condition = new RequirementResult(new AcceptConditionRequirement([1]), false, "1 accept condition not checked");
        var evaluation = new QuestEvaluation(QuestState.Blocked, [condition], condition, null, null);
        Assert.Equal("Not checked: accept condition", BlockerText.For(evaluation, Quest(Target), Names));
        Assert.Equal("Blocked · Not checked: accept condition", BlockerText.StatusText(evaluation, Quest(Target), Names));

        var mount = new RequirementResult(new MountRequirement(null), false, "requires a mount, not checked");
        Assert.Equal("Not checked: mount", BlockerText.For(new QuestEvaluation(QuestState.Blocked, [mount], mount, null, null), Quest(Target), Names));
    }

    [Fact]
    public void Decisive_order_is_prerequisites_then_level_then_rank_then_seasonal()
    {
        var prerequisite = Quest(A, "Peace for Thanalan");
        var quest = Quest(Target) with
        {
            Level = 80,
            PreviousQuests = new Prereq([A], JoinKind.All),
            BeastTribe = Pelupelu,
            BeastRank = 4,
            Festival = 9,
        };
        var catalog = Catalog(prerequisite, quest);

        Assert.Equal("after: Peace for Thanalan", For(quest, Snapshot(), catalog));
        Assert.Equal("Lv 80", For(quest, Snapshot(A), catalog));

        var leveled = Snapshot(A) with { JobLevels = Levels((Gladiator, 80)) };
        Assert.Equal("Rank: Trusted with the Pelupelu", For(quest, leveled, catalog));

        var trusted = leveled with { Tribes = new Dictionary<byte, TribeStanding> { [Pelupelu] = new(4, 0) } };
        Assert.Equal("Seasonal: not running", For(quest, trusted, catalog));

        // Job before level: the pinned job's level covers both.
        var pinned = quest with { ClassJobRequired = Paladin, Festival = 0, BeastTribe = 0, BeastRank = 0 };
        Assert.Equal("Lv 80 on PLD", For(pinned, Snapshot(A), Catalog(prerequisite, pinned)));
    }

    [Fact]
    public void Step_text_counts_from_the_journal_sequence_and_the_step_count()
    {
        Assert.Equal(string.Empty, BlockerText.StepText(null, 7));
        Assert.Equal("step 3 of 7", BlockerText.StepText(3, 7));
        Assert.Equal("step 1 of 7", BlockerText.StepText(1, 7));
        Assert.Equal("step 7 of 7", BlockerText.StepText(BlockerText.FinalSequence, 7));
        Assert.Equal("step 3", BlockerText.StepText(3, 0));

        var quest = Quest(Target) with { StepCount = 7 };
        var accepted = Snapshot() with { Accepted = [Accepted(Target, 3)] };
        Assert.Equal("In journal · step 3 of 7", Status(quest, accepted));
        Assert.Equal(string.Empty, For(StateResolver.Resolve(quest, accepted, Catalog(quest), EvalContext.Default), quest));
    }

    private static string For(QuestEvaluation evaluation, QuestRecord quest) => BlockerText.For(evaluation, quest, Names);

    [Theory]
    [InlineData(QuestState.Ready)]
    [InlineData(QuestState.ReadyOnOtherJob)]
    [InlineData(QuestState.Accepted)]
    [InlineData(QuestState.Blocked)]
    [InlineData(QuestState.DoneThisCycle)]
    [InlineData(QuestState.Completed)]
    [InlineData(QuestState.Foreclosed)]
    [InlineData(QuestState.Unknown)]
    public void The_state_word_is_the_first_token_of_every_status_string(QuestState state)
    {
        var quest = Quest(Target) with { Level = 80, StepCount = 4 };
        var level = new RequirementResult(new LevelRequirement(80, 50), false, "needs level 80, you are 50");
        var evaluation = new QuestEvaluation(state, [level], level, state == QuestState.ReadyOnOtherJob ? Paladin : null, state == QuestState.Accepted ? (byte)2 : null);

        var status = BlockerText.StatusText(evaluation, quest, Names);

        Assert.StartsWith(StateNames.Name(state, quest), status, StringComparison.Ordinal);
        Assert.True(status == StateNames.Name(state, quest) || status.Contains(BlockerText.Separator, StringComparison.Ordinal), status);
    }

    [Fact]
    public void A_missing_evaluation_reads_as_Not_checked()
    {
        Assert.Equal("Not checked", BlockerText.StatusText(null, Quest(Target), Names));
    }
}
