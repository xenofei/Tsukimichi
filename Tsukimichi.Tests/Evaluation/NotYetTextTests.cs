using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// The detail pane's "Not yet" callout, gap meters and jump targets (feature plan v4 L8): the callout names every
/// unmet requirement in one line, only for a quest the character cannot take on the current job; a numeric requirement
/// reads "52 → 56"; a jump selects the quest that clears a requirement, never the quest itself.
/// </summary>
public class NotYetTextTests
{
    private static BlockerNames Names(QuestCatalog catalog) => new()
    {
        Catalog = catalog,
        JobAbbreviation = static id => id switch
        {
            Gladiator => "GLA",
            Conjurer => "CNJ",
            Paladin => "PLD",
            _ => string.Empty,
        },
    };

    private static QuestEvaluation Resolve(QuestRecord quest, CharacterSnapshot snapshot, QuestCatalog catalog, EvalContext? context = null) =>
        StateResolver.Resolve(quest, snapshot, catalog, context ?? EvalContext.Default);

    [Fact]
    public void A_blocked_quest_names_every_unmet_requirement_in_one_line()
    {
        var quest = Quest(Target) with { Level = 56, PreviousQuests = new Prereq([A], JoinKind.All) };
        var catalog = Catalog(quest, Quest(A));
        var evaluation = Resolve(quest, Snapshot() with { JobLevels = Levels((Gladiator, 52)) }, catalog);

        Assert.Equal(QuestState.Blocked, evaluation.State);
        var callout = NotYetText.Callout(evaluation, quest, Names(catalog));
        Assert.NotNull(callout);
        Assert.False(callout.LockedOut);
        Assert.Equal("Not yet · level 56 (you're 52) and 1 previous quest", callout.Text);
    }

    [Fact]
    public void Past_three_requirements_the_rest_are_counted()
    {
        var quest = Quest(Target) with
        {
            Level = 56,
            PreviousQuests = new Prereq([A, B], JoinKind.All),
            GrandCompany = 1,
            MountRequired = true,
            HouseRequired = true,
        };
        var catalog = Catalog(quest, Quest(A), Quest(B));
        var context = new EvalContext { HasMount = false, HasHouse = false };
        var evaluation = Resolve(quest, Snapshot() with { JobLevels = Levels((Gladiator, 52)) }, catalog, context);

        Assert.Equal(
            "Not yet · level 56 (you're 52), 2 previous quests, joining the Maelstrom and 2 more",
            NotYetText.Callout(evaluation, quest, Names(catalog))!.Text);
    }

    [Fact]
    public void A_quest_ready_on_another_job_says_which_and_the_level_still_to_reach_here()
    {
        var pinned = Quest(Target) with { Level = 30, ClassJobRequired = Conjurer };
        var catalog = Catalog(pinned);
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 50), (Conjurer, 35)) };
        var evaluation = Resolve(pinned, snapshot, catalog);

        Assert.Equal(QuestState.ReadyOnOtherJob, evaluation.State);
        Assert.Equal("Not on this job · ready on CNJ", NotYetText.Callout(evaluation, pinned, Names(catalog))!.Text);

        var anyJob = Quest(Target) with { Level = 60 };
        var levelled = Resolve(anyJob, snapshot with { JobLevels = Levels((Gladiator, 50), (Conjurer, 60)) }, Catalog(anyJob));
        Assert.Equal(QuestState.ReadyOnOtherJob, levelled.State);
        Assert.Equal("Not on this job · level 60 (you're 50) · ready on CNJ", NotYetText.Callout(levelled, anyJob, Names(Catalog(anyJob)))!.Text);
    }

    [Fact]
    public void A_locked_out_quest_says_why_in_the_status_column_s_words()
    {
        var quest = Quest(Target) with { IsRetired = true };
        var catalog = Catalog(quest);
        var evaluation = Resolve(quest, Snapshot(), catalog);

        Assert.Equal(QuestState.Foreclosed, evaluation.State);
        var callout = NotYetText.Callout(evaluation, quest, Names(catalog));
        Assert.NotNull(callout);
        Assert.True(callout.LockedOut);
        Assert.Equal("Locked out · removed from the game", callout.Text);
        Assert.Equal(BlockerText.StatusText(evaluation, quest, Names(catalog)), callout.Text);
    }

    [Fact]
    public void An_available_or_unchecked_quest_has_no_callout()
    {
        var quest = Quest(Target) with { Level = 10 };
        var catalog = Catalog(quest);
        var names = Names(catalog);

        Assert.Null(NotYetText.Callout(null, quest, names));
        Assert.Null(NotYetText.Callout(Resolve(quest, Snapshot(), catalog), quest, names));
        Assert.Null(NotYetText.Callout(Resolve(quest, Snapshot(Target), catalog), quest, names));
        Assert.Null(NotYetText.Callout(Resolve(quest, Snapshot() with { Accepted = [Accepted(Target)] }, catalog), quest, names));

        var gated = new EvalContext { IsAchievementGated = static id => id == Target };
        var unknown = Resolve(quest, Snapshot() with { AchievementsLoaded = false }, catalog, gated);
        Assert.Equal(QuestState.Unknown, unknown.State);
        Assert.Null(NotYetText.Callout(unknown, quest, names));
    }

    [Fact]
    public void Numeric_requirements_have_a_gap_meter()
    {
        var names = Names(QuestCatalog.Empty);
        var level = new LevelRequirement(56, 52);
        Assert.Equal(new RequirementGap(52, 56), NotYetText.Gap(level));
        Assert.Equal(52f / 56f, NotYetText.Gap(level)!.Value.Fraction, 3);
        Assert.Equal("52 → 56", NotYetText.GapLabel(level, names));

        Assert.Equal("1200 → 3000", NotYetText.GapLabel(new TribeReputationRequirement(1, 3000, 1200), names));
        Assert.Equal("Recognized → Trusted", NotYetText.GapLabel(new TribeRankRequirement(1, 4, 2), names));
        Assert.Equal("3 → 5", NotYetText.GapLabel(new CarrierLevelRequirement(5, 3), names));

        // Not read, or not a number: no meter.
        Assert.Null(NotYetText.Gap(new CarrierLevelRequirement(5, null)));
        Assert.Null(NotYetText.GapLabel(new CustomDeliveryRankRequirement(1, 3, null), names));
        Assert.Null(NotYetText.Gap(new PreviousQuestsRequirement([A], JoinKind.All, 0)));
        Assert.Equal(1f, new RequirementGap(0, 0).Fraction);
    }

    [Fact]
    public void A_previous_quest_jumps_to_the_nearest_one_still_to_do()
    {
        var quest = Quest(Target) with { PreviousQuests = new Prereq([A, B], JoinKind.All) };
        var catalog = Catalog(quest, Quest(A), Quest(B));
        var snapshot = Snapshot(A);
        var states = StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default);
        var previous = Only(states[Target].Requirements, RequirementKind.PreviousQuests);

        Assert.False(previous.Met);
        Assert.Equal(B, NotYetText.JumpTarget(previous, quest, catalog, states));

        // Met, or pointing outside the catalog: no jump.
        var met = Only(StateResolver.ResolveAll(catalog, Snapshot(A, B), EvalContext.Default)[Target].Requirements, RequirementKind.PreviousQuests);
        Assert.Null(NotYetText.JumpTarget(met, quest, catalog, states));
        var outside = new RequirementResult(new PreviousQuestsRequirement([C], JoinKind.All, 0), false, "needs quest");
        Assert.Null(NotYetText.JumpTarget(outside, quest, catalog, states));
    }

    [Fact]
    public void A_pinned_job_jumps_to_its_unlock_quest_until_it_is_done()
    {
        var unlock = Quest(A, "Way of the Conjurer");
        var quest = Quest(Target) with { Level = 30, ClassJobRequired = Conjurer };
        var catalog = Catalog(quest, unlock);
        uint UnlockOf(uint job) => job == Conjurer ? A : 0u;

        var states = StateResolver.ResolveAll(catalog, Snapshot(), EvalContext.Default);
        var job = Only(states[Target].Requirements, RequirementKind.ClassJob);
        Assert.False(job.Met);
        Assert.Equal(A, NotYetText.JumpTarget(job, quest, catalog, states, UnlockOf));

        var unlocked = StateResolver.ResolveAll(catalog, Snapshot(A), EvalContext.Default);
        Assert.Null(NotYetText.JumpTarget(Only(unlocked[Target].Requirements, RequirementKind.ClassJob), quest, catalog, unlocked, UnlockOf));

        // Without the sheet's unlock quests, nothing to jump to.
        Assert.Null(NotYetText.JumpTarget(job, quest, catalog, states));
    }

    // ---------------------------------------------------------------- the level of a job that can take the quest

    [Fact]
    public void A_pinned_quest_ready_on_its_job_says_nothing_of_the_current_job_s_lower_level()
    {
        // The owner on Lancer 70 with Paladin 90: "Worth Fighting For" (level 80, Paladin) read "level 80 on PLD
        // (you're 70) · ready on PLD", comparing Lancer's level with a Paladin quest.
        var quest = Quest(Target) with { Level = 80, ClassJobRequired = Paladin };
        var catalog = Catalog(quest);
        var snapshot = Snapshot() with { CurrentJob = Conjurer, JobLevels = Levels((Conjurer, 70), (Paladin, 90)) };
        var raw = Resolve(quest, snapshot, catalog);
        Assert.Equal(QuestState.ReadyOnOtherJob, raw.State);

        var shown = NotYetText.OnAdmittedJob(raw, quest, snapshot, EvalContext.Default);
        Assert.Equal("Not on this job · ready on PLD", NotYetText.Callout(shown, quest, Names(catalog))!.Text);

        // An evaluation not passed through OnAdmittedJob leaves the unrelated level out too.
        Assert.Equal("Not on this job · ready on PLD", NotYetText.Callout(raw, quest, Names(catalog))!.Text);

        // The level line is measured on Paladin, and met there.
        var level = Only(shown.Requirements, RequirementKind.Level);
        Assert.True(level.Met);
        Assert.Equal("level 80 on PLD", RequirementDetail.Text(level, Names(catalog), Conjurer));
        Assert.Equal(new RequirementGap(90, 80), NotYetText.Gap(level.Req));
    }

    [Fact]
    public void A_pinned_quest_blocked_elsewhere_compares_its_own_job_s_level()
    {
        // The owner on Dragoon 69 with Culinarian 35: "The Way to a Father's Heart" (level 70, Culinarian) read
        // "CUL (you're DRG), level 70 on CUL (you're 69)".
        var quest = Quest(Target) with { Level = 70, ClassJobRequired = Conjurer, PreviousQuests = new Prereq([A], JoinKind.All) };
        var catalog = Catalog(quest, Quest(A));
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 69), (Conjurer, 35)) };
        var shown = NotYetText.OnAdmittedJob(Resolve(quest, snapshot, catalog), quest, snapshot, EvalContext.Default);

        Assert.Equal(QuestState.Blocked, shown.State);
        Assert.Equal("Not yet · CNJ (you're GLA), level 70 (CNJ is 35) and 1 previous quest", NotYetText.Callout(shown, quest, Names(catalog))!.Text);
        var level = Only(shown.Requirements, RequirementKind.Level);
        Assert.False(level.Met);
        Assert.Equal("needs level 70, CNJ is 35", RequirementDetail.Text(level, Names(catalog), Gladiator));
        Assert.Equal(new RequirementGap(35, 70), NotYetText.Gap(level.Req));
        Assert.Equal("35 → 70", NotYetText.GapLabel(level.Req, Names(catalog)));
    }

    [Fact]
    public void A_pinned_job_not_unlocked_names_the_level_without_comparing()
    {
        var quest = Quest(Target) with { Level = 70, ClassJobRequired = Conjurer, PreviousQuests = new Prereq([A], JoinKind.All) };
        var catalog = Catalog(quest, Quest(A));
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 69)) };
        var shown = NotYetText.OnAdmittedJob(Resolve(quest, snapshot, catalog), quest, snapshot, EvalContext.Default);

        Assert.Equal("Not yet · CNJ (you're GLA), level 70 and 1 previous quest", NotYetText.Callout(shown, quest, Names(catalog))!.Text);
        var level = Only(shown.Requirements, RequirementKind.Level);
        Assert.False(level.Met);
        Assert.Equal("needs level 70 on CNJ, not unlocked", RequirementDetail.Text(level, Names(catalog), Gladiator));
        Assert.Null(NotYetText.Gap(level.Req));
    }

    [Fact]
    public void A_category_the_current_job_is_outside_of_measures_the_best_job_it_admits()
    {
        // Fisher 1 on a Disciple of War or Magic quest read "another job (you're FSH), level 50 (you're 1)".
        var quest = Quest(Target) with { Level = 60, ClassJobCategory = 5, PreviousQuests = new Prereq([A], JoinKind.All) };
        var catalog = Catalog(quest, Quest(A));
        var context = new EvalContext { ClassJobs = new Jobs((5, [Conjurer, Paladin])) };
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 1), (Conjurer, 30), (Paladin, 40)) };
        var shown = NotYetText.OnAdmittedJob(Resolve(quest, snapshot, catalog, context), quest, snapshot, context);
        var names = Names(catalog) with { ClassJobCategory = static id => id == 5 ? "Disciple of Magic" : string.Empty };

        Assert.Equal("Not yet · Disciple of Magic (you're GLA), level 60 (PLD is 40) and 1 previous quest", NotYetText.Callout(shown, quest, names)!.Text);

        // No job of the category levelled: the class or job needed, and the level without a comparison.
        var none = snapshot with { JobLevels = Levels((Gladiator, 1)) };
        var bare = NotYetText.OnAdmittedJob(Resolve(quest, none, catalog, context), quest, none, context);
        Assert.Equal("Not yet · Disciple of Magic (you're GLA), level 60 and 1 previous quest", NotYetText.Callout(bare, quest, names)!.Text);
        Assert.Null(NotYetText.Gap(Only(bare.Requirements, RequirementKind.Level).Req));

        // A category name that lists every job reads "another job".
        var long_ = Names(catalog) with { ClassJobCategory = static _ => "PLD MNK WAR DRG BRD WHM BLM SMN SCH NIN" };
        Assert.StartsWith("Not yet · another job (you're GLA)", NotYetText.Callout(bare, quest, long_)!.Text);
    }

    [Fact]
    public void A_current_job_the_quest_admits_keeps_its_own_level()
    {
        var pinned = Quest(Target) with { Level = 30, ClassJobRequired = Paladin };
        var catalog = Catalog(pinned);
        var snapshot = Snapshot() with { CurrentJob = Paladin, JobLevels = Levels((Paladin, 12)) };
        var raw = Resolve(pinned, snapshot, catalog);

        Assert.Same(raw, NotYetText.OnAdmittedJob(raw, pinned, snapshot, EvalContext.Default));
        Assert.Equal("Not yet · level 30 on PLD (you're 12)", NotYetText.Callout(raw, pinned, Names(catalog))!.Text);
    }

    // ---------------------------------------------------------------- wording

    [Fact]
    public void An_option_of_a_choice_says_choose_one()
    {
        // The Vital Title and the stelae lost "Choose one of 2" from the detail pane: the callout hides the status line.
        var quest = Quest(Target) with { PreviousQuests = new Prereq([A], JoinKind.All) };
        var catalog = Catalog(quest, Quest(A));
        var evaluation = Resolve(quest, Snapshot(), catalog) with { ChoiceOf = 2 };

        Assert.Equal("Not yet · 1 previous quest · Choose one of 2", NotYetText.Callout(evaluation, quest, Names(catalog))!.Text);
    }

    [Fact]
    public void A_single_requirement_past_three_is_named_not_counted()
    {
        var quest = Quest(Target) with
        {
            Level = 56,
            PreviousQuests = new Prereq([A, B], JoinKind.All),
            GrandCompany = 1,
            MountRequired = true,
        };
        var catalog = Catalog(quest, Quest(A), Quest(B));
        var context = new EvalContext { HasMount = false };
        var evaluation = Resolve(quest, Snapshot() with { JobLevels = Levels((Gladiator, 52)) }, catalog, context);

        Assert.Equal(
            "Not yet · level 56 (you're 52), 2 previous quests, joining the Maelstrom and a mount",
            NotYetText.Callout(evaluation, quest, Names(catalog))!.Text);
    }

    [Fact]
    public void An_allied_society_not_started_says_so()
    {
        var quest = Quest(Target) with { BeastTribe = 3, BeastRank = 4 };
        var catalog = Catalog(quest);
        var evaluation = Resolve(quest, Snapshot(), catalog);
        var names = Names(catalog) with { Tribe = static id => id == 3 ? "Qitari" : string.Empty };

        Assert.Equal("Not yet · Trusted with the Qitari (not started)", NotYetText.Callout(evaluation, quest, names)!.Text);
        Assert.Equal("Not yet · Trusted (not started)", NotYetText.Callout(evaluation, quest, Names(catalog))!.Text);
    }

    // ---------------------------------------------------------------- jumps only to quests the character can take

    [Fact]
    public void A_locked_out_quest_has_no_jump()
    {
        // Close to Home (another city's) jumped to Coming to Limsa Lominsa, itself locked out.
        var quest = Quest(Target) with { PreviousQuests = new Prereq([A], JoinKind.All), IsRetired = true };
        var catalog = Catalog(quest, Quest(A));
        var states = StateResolver.ResolveAll(catalog, Snapshot(), EvalContext.Default);

        Assert.Equal(QuestState.Foreclosed, states[Target].State);
        Assert.Null(NotYetText.JumpTarget(Only(states[Target].Requirements, RequirementKind.PreviousQuests), quest, catalog, states));
    }

    [Fact]
    public void A_jump_passes_over_a_removed_or_locked_out_prerequisite()
    {
        // What's It to U jumped to Can't Do It without U (removed from the game) with its other prerequisite open.
        var removed = Quest(A) with { IsRetired = true };
        var quest = Quest(Target) with { PreviousQuests = new Prereq([A, B], JoinKind.Any) };
        var catalog = Catalog(quest, removed, Quest(B));
        var states = StateResolver.ResolveAll(catalog, Snapshot(), EvalContext.Default);
        var previous = Only(states[Target].Requirements, RequirementKind.PreviousQuests);
        Assert.Equal(B, NotYetText.JumpTarget(previous, quest, catalog, states));

        // Only a removed one left: no jump.
        var alone = Quest(Target) with { PreviousQuests = new Prereq([A], JoinKind.All) };
        var lone = Catalog(alone, removed);
        var loneStates = StateResolver.ResolveAll(lone, Snapshot(), EvalContext.Default);
        Assert.Null(NotYetText.JumpTarget(Only(loneStates[Target].Requirements, RequirementKind.PreviousQuests), alone, lone, loneStates));

        // A locked out prerequisite (a completed lock) is passed over the same way.
        var locked = Quest(A) with { QuestLocks = [C] };
        var both = Catalog(quest, locked, Quest(B), Quest(C));
        var lockedStates = StateResolver.ResolveAll(both, Snapshot(C), EvalContext.Default);
        Assert.Equal(QuestState.Foreclosed, lockedStates[A].State);
        Assert.Equal(B, NotYetText.JumpTarget(Only(lockedStates[Target].Requirements, RequirementKind.PreviousQuests), quest, both, lockedStates));
    }

    [Fact]
    public void A_duty_jumps_to_the_quest_that_unlocks_it()
    {
        const uint Duty = 42;
        var opener = Quest(A) with { Rewards = [new RewardRef(RewardKind.Instance, Duty, 0, 1, "The Vault", 0)] };
        var quest = Quest(Target) with { InstanceContentRequired = [Duty] };
        var catalog = Catalog(quest, opener);
        var states = StateResolver.ResolveAll(catalog, Snapshot(), EvalContext.Default);
        var duty = Only(states[Target].Requirements, RequirementKind.DutyCompletion);

        Assert.False(duty.Met);
        Assert.Equal(A, NotYetText.JumpTarget(duty, quest, catalog, states));
        Assert.Equal("Not yet · 1 duty", NotYetText.Callout(states[Target], quest, Names(catalog))!.Text);
    }
}
