using System.Globalization;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// The detail pane's "Not yet" callout (feature plan v4 L8): one line naming everything the character is missing.
/// <paramref name="Text"/> is the whole line ("Not yet · level 56 (you're 52) and 1 previous quest", "Locked out ·
/// Another city's start (Ul'dah)", "Not on this job · level 30 (you're 12) · ready on PLD");
/// <paramref name="LockedOut"/> says it is a quest the character can never take (the eclipse tone).
/// </summary>
public sealed record NotYetCallout(string Text, bool LockedOut);

/// <summary>
/// A numeric requirement as a gap meter: where the character stands (<paramref name="Current"/>) against what the
/// quest asks (<paramref name="Required"/>).
/// </summary>
public readonly record struct RequirementGap(int Current, int Required)
{
    /// <summary>How far along the character is, 0 to 1; a requirement asking for nothing reads as full.</summary>
    public float Fraction => Required <= 0 ? 1f : Math.Clamp((float)Current / Required, 0f, 1f);
}

/// <summary>
/// What the detail pane says about requirements the character does not meet (feature plan v4 L8): the "Not yet"
/// callout (<see cref="Callout"/>), the gap meter of a numeric requirement (<see cref="Gap"/>, <see cref="GapLabel"/>)
/// and the quest a jump button selects to clear one (<see cref="JumpTarget"/>). Pure, so the words and the choices are
/// unit-tested; the phrases follow the UI language through <see cref="CoreText"/> (keys <c>Core.NotYet.*</c> and
/// <c>Core.Gap.*</c>).
/// </summary>
public static class NotYetText
{
    /// <summary>At most this many requirements are named in the callout; the rest read "and 2 more".</summary>
    public const int MaxClauses = 3;

    /// <summary>The callout's lead for a quest the character cannot take yet: "Not yet".</summary>
    public static string Lead => CoreText.T("Core.NotYet.Lead", "Not yet");

    /// <summary>The callout's lead for a quest only another job can take now: "Not on this job".</summary>
    public static string OtherJobLead => CoreText.T("Core.NotYet.OtherJobLead", "Not on this job");

    private static string T(string key, string english) => CoreText.T(key, english);

    private static string F(string key, string english, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, CoreText.T(key, english), args);

    /// <summary>
    /// The callout for a quest the character cannot take on the current job: Blocked ("Not yet · …", every unmet
    /// requirement in the order the pane lists them, at most <see cref="MaxClauses"/>), Ready on another job ("Not on
    /// this job · … · ready on PLD") and Locked out ("Locked out · " and the reason the Status column gives). Null for
    /// a quest that is available (Ready, In journal, done or Completed) and for one not checked.
    /// </summary>
    /// <param name="states">Every quest's evaluation, when at hand: picks the prerequisite a Locked out reason names.</param>
    public static NotYetCallout? Callout(QuestEvaluation? evaluation, QuestRecord quest, BlockerNames names, IReadOnlyDictionary<uint, QuestEvaluation>? states = null)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(names);
        switch (evaluation?.State)
        {
            case QuestState.Foreclosed:
            {
                var reason = BlockerText.For(evaluation, quest, names, states);
                var text = reason.Length == 0 ? StateNames.Foreclosed : StateNames.Foreclosed + BlockerText.Separator + reason;
                return new NotYetCallout(text, LockedOut: true);
            }

            case QuestState.Blocked:
            {
                var clauses = Clauses(evaluation, quest, names);
                if (clauses.Length == 0)
                {
                    // Should not occur (a Blocked quest has an unmet requirement); the Status column's reason stands in.
                    clauses = BlockerText.For(evaluation, quest, names, states);
                }

                return new NotYetCallout(WithChoice(clauses.Length == 0 ? Lead : Lead + BlockerText.Separator + clauses, evaluation), LockedOut: false);
            }

            case QuestState.ReadyOnOtherJob:
            {
                // "ready on PLD" names the job already; the level still to reach on this one is worth saying when this
                // job can take the quest at all (a job it does not admit has no level here to reach).
                var clauses = Clauses(evaluation, quest, names, skipJob: true);
                var job = evaluation.ReadyOnJob is { } id ? names.JobAbbreviation(id) : string.Empty;
                var text = OtherJobLead;
                if (clauses.Length > 0)
                {
                    text += BlockerText.Separator + clauses;
                }

                if (job.Length > 0)
                {
                    text += BlockerText.Separator + F("Core.NotYet.ReadyOn", "ready on {0}", job);
                }

                return new NotYetCallout(WithChoice(text, evaluation), LockedOut: false);
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// "… · Choose one of 2" for an option of a choice the character has not made yet
    /// (<see cref="QuestEvaluation.ChoiceOf"/>), as the status line has it: the callout stands in for that line.
    /// </summary>
    private static string WithChoice(string text, QuestEvaluation evaluation) =>
        evaluation.ChoiceOf > 1 ? text + BlockerText.Separator + PathText.ChooseOne(evaluation.ChoiceOf) : text;

    /// <summary>
    /// The evaluation as the detail pane reads it when the current job cannot take the quest: its level requirement
    /// measured on the job that matters instead of the current one (the evaluator measures every quest on the current
    /// job). That job is the one the quest is ready on, else its pinned job, else the character's best job its
    /// category admits; with none (the pinned job not unlocked, no job of the category levelled) the requirement says
    /// so (<see cref="LevelRequirement.NoJob"/>) and nothing compares a level. So a quest pinned to Culinarian read on
    /// Dragoon says "level 70 (CUL is 35)", not Dragoon's level, and one ready on Paladin says nothing of the current
    /// job's level. The same evaluation when the current job is admitted, or when the quest has no level.
    /// </summary>
    public static QuestEvaluation OnAdmittedJob(QuestEvaluation evaluation, QuestRecord quest, CharacterSnapshot snapshot, EvalContext context)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        var requirements = evaluation.Requirements;
        if (!JobNotAdmitted(evaluation))
        {
            return evaluation;
        }

        for (var i = 0; i < requirements.Count; i++)
        {
            if (requirements[i] is not { Req: LevelRequirement { MeasuredOn: 0, NoJob: false } level } old)
            {
                continue;
            }

            var job = evaluation.ReadyOnJob ?? quest.ClassJobRequired switch
            {
                0 => StateResolver.BestAdmittedJob(quest, snapshot, context),
                <= byte.MaxValue => (byte)quest.ClassJobRequired,
                _ => null,
            };
            var actual = job is { } j ? RequirementEvaluator.LevelOf(snapshot, j) : (byte)0;
            var noJob = actual == 0;
            var rebased = new LevelRequirement(level.Level, actual) { MeasuredOn = job ?? 0, NoJob = noJob };
            var met = !noJob && actual >= level.Level;
            var copy = new RequirementResult[requirements.Count];
            for (var k = 0; k < copy.Length; k++)
            {
                copy[k] = requirements[k];
            }

            // The detail in English as the evaluator writes it; RequirementDetail renders a measured level from the
            // record in every language, since it names a job.
            copy[i] = new RequirementResult(rebased, met, EnglishLevelDetail(rebased, met));
            return evaluation with
            {
                Requirements = copy,
                NextStep = ReferenceEquals(evaluation.NextStep, old) ? copy[i] : evaluation.NextStep,
            };
        }

        return evaluation;
    }

    private static string EnglishLevelDetail(LevelRequirement l, bool met) => l switch
    {
        { NoJob: true } => $"needs level {l.Level}, no job of yours can take it",
        _ when met => $"level {l.Level}",
        _ => $"needs level {l.Level}, that job is {l.ActualLevel}",
    };

    /// <summary>The current job is not one the quest admits (its class or job requirement is unmet).</summary>
    private static bool JobNotAdmitted(QuestEvaluation evaluation)
    {
        foreach (var result in evaluation.Requirements)
        {
            if (result.Req.Kind == RequirementKind.ClassJob)
            {
                return !result.Met;
            }
        }

        return false;
    }

    /// <summary>
    /// The unmet requirements as a phrase list in the order the evaluator lists them (job, level, previous quests,
    /// Grand Company, allied society, …): "level 56 (you're 52) and 1 previous quest"; past
    /// <see cref="MaxClauses"/> the rest read "and 2 more" (a single one left over is named instead). Empty when every
    /// requirement is met. A level measured on a job the quest does not admit (an evaluation not passed through
    /// <see cref="OnAdmittedJob"/>) is left out: it would compare an unrelated job's level.
    /// </summary>
    /// <param name="skipJob">Leaves out the class or job admission (the callout of a quest ready on another job names that job).</param>
    public static string Clauses(QuestEvaluation evaluation, QuestRecord quest, BlockerNames names, bool skipJob = false)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(names);
        var phrases = new List<string>(MaxClauses + 2);
        var notAdmitted = JobNotAdmitted(evaluation);
        foreach (var result in evaluation.Requirements)
        {
            if (result.Met
                || (skipJob && result.Req.Kind == RequirementKind.ClassJob)
                || (notAdmitted && result.Req is LevelRequirement { MeasuredOn: 0, NoJob: false })
                || Clause(result, quest, names) is not { Length: > 0 } phrase)
            {
                continue;
            }

            phrases.Add(phrase);
        }

        if (phrases.Count > MaxClauses + 1)
        {
            var more = phrases.Count - MaxClauses;
            phrases.RemoveRange(MaxClauses, more);
            phrases.Add(F("Core.NotYet.More", "{0} more", more));
        }

        return Join(phrases);
    }

    /// <summary>"a", "a and b", "a, b and c".</summary>
    private static string Join(List<string> phrases)
    {
        switch (phrases.Count)
        {
            case 0:
                return string.Empty;
            case 1:
                return phrases[0];
        }

        var head = string.Join(T("Core.NotYet.ListSeparator", ", "), phrases.GetRange(0, phrases.Count - 1));
        return F("Core.NotYet.And", "{0} and {1}", head, phrases[^1]);
    }

    /// <summary>
    /// One unmet requirement as the callout names it: "level 56 (you're 52)", "1 previous quest", "PLD (you're WHM)",
    /// "Trusted with the Amalj'aa (you're Recognized)". Null for a kind the callout leaves to its reason (a quest
    /// removed from the game, another path, a completed lock).
    /// </summary>
    public static string? Clause(RequirementResult result, QuestRecord quest, BlockerNames names)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(names);
        return result.Req switch
        {
            ExpansionCapRequirement e => F("Core.NotYet.Expansion", "{0} (not owned)", names.Expansion(e.Expansion)),
            LevelCapRequirement l => F("Core.NotYet.LevelCap", "level cap {0} (yours is {1})", l.Level, l.LevelCap),
            ClassJobRequirement c => Job(c, quest, names),
            LevelRequirement l => Level(l, quest, names),
            PreviousQuestsRequirement p => Previous(p),
            GrandCompanyRequirement g => F("Core.NotYet.GrandCompany", "joining the {0}", names.GrandCompany(g.GrandCompany)),
            GrandCompanyRankRequirement g => F("Core.NotYet.GrandCompanyRank", "{0} (you're {1})", names.GrandCompanyRank(g.RequiredRank), names.GrandCompanyRank(g.ActualRank)),
            TribeRankRequirement t => TribeRank(t, names),
            TribeReputationRequirement { MaxedRank: not 0 } t => F("Core.NotYet.MaxedReputation", "{0} reputation maxed ({1:N0}/{2:N0})", names.TribeRank(t.MaxedRank), t.ActualValue, t.RequiredValue),
            TribeReputationRequirement t => F("Core.NotYet.Reputation", "{0} more reputation", Math.Max(0, t.RequiredValue - t.ActualValue)),
            TribeAllowanceRequirement => T("Core.NotYet.Allowance", "an allowance (none left today)"),
            TribeDailyOfferRequirement => T("Core.NotYet.NotOffered", "today's offer"),
            CustomDeliveryRankRequirement { ActualRank: { } actual } c => F("Core.NotYet.Satisfaction", "satisfaction rank {0} (you're {1})", c.RequiredRank, actual),
            CustomDeliveryRankRequirement c => F("Core.NotYet.SatisfactionNotChecked", "satisfaction rank {0}", c.RequiredRank),
            CarrierLevelRequirement { ActualLevel: { } actual } c => F("Core.NotYet.CarrierLevel", "carrier level {0} (you're {1})", c.RequiredLevel, actual),
            CarrierLevelRequirement c => F("Core.NotYet.CarrierLevelNotChecked", "carrier level {0}", c.RequiredLevel),
            DutyCompletionRequirement d => Duties(d),
            SeasonalRequirement s => s.ChapterNotOpen ? T("Core.NotYet.ChapterNotOpen", "its chapter to open")
                : s.ChapterOver ? T("Core.NotYet.ChapterOver", "a chapter that is over")
                : T("Core.NotYet.Seasonal", "its seasonal event"),
            MountRequirement => T("Core.NotYet.Mount", "a mount"),
            HouseRequirement => T("Core.NotYet.House", "a house"),
            AchievementRequirement => T("Core.NotYet.Achievement", "an achievement"),
            AcceptConditionRequirement => T("Core.NotYet.AcceptCondition", "an accept condition"),
            _ => null,
        };
    }

    /// <summary>"Trusted with the Qitari (you're Recognized)"; "(not started)" before the first rank.</summary>
    private static string TribeRank(TribeRankRequirement t, BlockerNames names)
    {
        var required = names.TribeRank(t.RequiredRank);
        var tribe = names.Tribe(t.Tribe);
        if (t.ActualRank == 0)
        {
            return tribe.Length > 0
                ? F("Core.NotYet.TribeRankWithNotStarted", "{0} with the {1} (not started)", required, tribe)
                : F("Core.NotYet.TribeRankNotStarted", "{0} (not started)", required);
        }

        return tribe.Length > 0
            ? F("Core.NotYet.TribeRankWith", "{0} with the {1} (you're {2})", required, tribe, names.TribeRank(t.ActualRank))
            : F("Core.NotYet.TribeRank", "{0} (you're {1})", required, names.TribeRank(t.ActualRank));
    }

    /// <summary>
    /// "level 56 (you're 52)", or "level 30 on PLD (you're 12)" when the quest pins the current job; measured on
    /// another job (<see cref="OnAdmittedJob"/>), "level 70 (CUL is 35)"; with no job that can take it, "level 70".
    /// </summary>
    private static string Level(LevelRequirement l, QuestRecord quest, BlockerNames names)
    {
        if (l.NoJob)
        {
            return F("Core.NotYet.LevelOnly", "level {0}", l.Level);
        }

        if (l.MeasuredOn != 0 && names.JobAbbreviation(l.MeasuredOn) is { Length: > 0 } measured)
        {
            return F("Core.NotYet.LevelJobIs", "level {0} ({1} is {2})", l.Level, measured, l.ActualLevel);
        }

        var job = quest.ClassJobRequired == 0 ? string.Empty : names.JobAbbreviation(quest.ClassJobRequired);
        return job.Length > 0
            ? F("Core.NotYet.LevelOn", "level {0} on {1} (you're {2})", l.Level, job, l.ActualLevel)
            : F("Core.NotYet.Level", "level {0} (you're {1})", l.Level, l.ActualLevel);
    }

    /// <summary>The longest category name the callout prints; the sheet names many by listing every job in them.</summary>
    private const int MaxCategoryName = 30;

    /// <summary>
    /// "PLD (you're WHM)" for a pinned job; "Disciple of the Land (you're WHM)" or "PLD WAR DRK GNB (you're WHM)" for a
    /// category the sheet names briefly; "another job (you're WHM)" otherwise.
    /// </summary>
    private static string Job(ClassJobRequirement c, QuestRecord quest, BlockerNames names)
    {
        var pinned = c.RequiredJob != 0 ? c.RequiredJob : quest.ClassJobRequired;
        var required = pinned == 0 ? string.Empty : names.JobAbbreviation(pinned);
        var you = names.JobAbbreviation(c.Job);
        if (pinned == 0 && c.CategoryId != 0 && names.ClassJobCategory(c.CategoryId) is { Length: > 0 and <= MaxCategoryName } category)
        {
            required = category;
        }

        if (required.Length > 0)
        {
            return you.Length > 0
                ? F("Core.NotYet.JobYouAre", "{0} (you're {1})", required, you)
                : required;
        }

        return you.Length > 0
            ? F("Core.NotYet.AnotherJobYouAre", "another job (you're {0})", you)
            : T("Core.NotYet.AnotherJob", "another job");
    }

    /// <summary>"1 previous quest", "3 previous quests": how many are still to do (one, through an Any join).</summary>
    private static string Previous(PreviousQuestsRequirement p)
    {
        var left = p.Join == JoinKind.Any ? 1 : Math.Max(1, p.QuestIds.Length - p.DoneCount);
        return left == 1
            ? T("Core.NotYet.PreviousOne", "1 previous quest")
            : F("Core.NotYet.PreviousMany", "{0} previous quests", left);
    }

    /// <summary>"1 duty", "2 duties": how many are still to clear (one, through an Any join).</summary>
    private static string Duties(DutyCompletionRequirement d)
    {
        var left = d.Join == JoinKind.Any ? 1 : Math.Max(1, d.InstanceIds.Length - d.DoneCount);
        return left == 1
            ? T("Core.NotYet.DutyOne", "1 duty")
            : F("Core.NotYet.DutyMany", "{0} duties", left);
    }

    /// <summary>
    /// The gap meter of a numeric requirement: level, Grand Company rank, allied society rank and reputation, custom
    /// delivery rank, carrier level. Null for any other kind, and for a rank the plugin did not read.
    /// </summary>
    public static RequirementGap? Gap(Requirement requirement) => requirement switch
    {
        LevelRequirement { NoJob: true } => null,
        LevelRequirement l => new RequirementGap(l.ActualLevel, l.Level),
        GrandCompanyRankRequirement g => new RequirementGap(g.ActualRank, g.RequiredRank),
        TribeRankRequirement t => new RequirementGap(t.ActualRank, t.RequiredRank),
        TribeReputationRequirement { NotChecked: true } => null,
        TribeReputationRequirement t => new RequirementGap(t.ActualValue, t.RequiredValue),
        CustomDeliveryRankRequirement { ActualRank: { } actual } c => new RequirementGap(actual, c.RequiredRank),
        CarrierLevelRequirement { ActualLevel: { } actual } c => new RequirementGap(actual, c.RequiredLevel),
        _ => null,
    };

    /// <summary>
    /// The gap meter's label, where the character stands and what the quest asks: "52 → 56"; ranks by name
    /// ("Recognized → Trusted"). Null where <see cref="Gap"/> is.
    /// </summary>
    public static string? GapLabel(Requirement requirement, BlockerNames names)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(names);
        return requirement switch
        {
            TribeRankRequirement t => Arrow(names.TribeRank(t.ActualRank), names.TribeRank(t.RequiredRank)),
            GrandCompanyRankRequirement g => Arrow(names.GrandCompanyRank(g.ActualRank), names.GrandCompanyRank(g.RequiredRank)),
            TribeReputationRequirement { MaxedRank: not 0, NotChecked: false } t =>
                F("Core.Gap.RankReputation", "{0} {1:N0}/{2:N0} reputation", names.TribeRank(t.MaxedRank), t.ActualValue, t.RequiredValue),
            _ => Gap(requirement) is { } gap
                ? Arrow(gap.Current.ToString(CultureInfo.CurrentCulture), gap.Required.ToString(CultureInfo.CurrentCulture))
                : null,
        };
    }

    private static string Arrow(string from, string to) => F("Core.Gap.Arrow", "{0} → {1}", from, to);

    /// <summary>
    /// The quest that clears an unmet requirement, for the jump button beside it: the nearest previous quest still to
    /// do (<see cref="BlockerText.NearestPrerequisite"/>); for a pinned job the character has not unlocked, the job's
    /// unlock quest; for a duty to clear, the quest that unlocks it. Only a quest the character can still take: one
    /// removed from the game, on a path the character did not take or otherwise Locked out is passed over for the next
    /// nearest. Null for a met requirement, a requirement no quest clears (a level, a rank, the seasonal event), a
    /// quest the catalog does not hold, and on a quest that is itself Locked out (nothing clears it); never the quest
    /// itself.
    /// </summary>
    /// <param name="jobUnlockQuest">A job's unlock quest row id (the ClassJob sheet's), 0 when none; null when unknown.</param>
    public static uint? JumpTarget(RequirementResult result, QuestRecord quest, QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation>? states, Func<uint, uint>? jobUnlockQuest = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(catalog);
        if (result.Met || IsLockedOut(quest.RowId, states))
        {
            return null;
        }

        bool Usable(uint id) =>
            id != quest.RowId && catalog.ByRowId.TryGetValue(id, out var candidate) && !candidate.IsRemoved && !IsLockedOut(id, states);

        var target = result.Req switch
        {
            PreviousQuestsRequirement p => BlockerText.NearestPrerequisite(p, catalog, states, Usable),
            ClassJobRequirement or LevelRequirement => JobUnlock(quest, states, jobUnlockQuest),
            DutyCompletionRequirement d => DutyUnlock(d, catalog, states, Usable),
            _ => null,
        };
        return target is { } id && Usable(id) ? id : null;
    }

    private static bool IsLockedOut(uint rowId, IReadOnlyDictionary<uint, QuestEvaluation>? states) =>
        states is not null && states.TryGetValue(rowId, out var evaluation) && evaluation.State == QuestState.Foreclosed;

    /// <summary>The pinned job's unlock quest while the character has not completed it.</summary>
    private static uint? JobUnlock(QuestRecord quest, IReadOnlyDictionary<uint, QuestEvaluation>? states, Func<uint, uint>? jobUnlockQuest)
    {
        if (quest.ClassJobRequired == 0 || jobUnlockQuest?.Invoke(quest.ClassJobRequired) is not { } unlock || unlock == 0)
        {
            return null;
        }

        return IsCompleted(unlock, states) ? null : unlock;
    }

    /// <summary>The first quest still to do that unlocks one of the duties (a <see cref="RewardKind.Instance"/> reward).</summary>
    private static uint? DutyUnlock(DutyCompletionRequirement d, QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation>? states, Func<uint, bool> usable)
    {
        foreach (var instance in d.InstanceIds)
        {
            foreach (var candidate in catalog.All)
            {
                foreach (var reward in candidate.Rewards)
                {
                    if (reward.Kind == RewardKind.Instance && reward.Id == instance && !IsCompleted(candidate.RowId, states) && usable(candidate.RowId))
                    {
                        return candidate.RowId;
                    }
                }
            }
        }

        return null;
    }

    private static bool IsCompleted(uint rowId, IReadOnlyDictionary<uint, QuestEvaluation>? states) =>
        states is not null && states.TryGetValue(rowId, out var evaluation) && evaluation.State == QuestState.Completed;
}
