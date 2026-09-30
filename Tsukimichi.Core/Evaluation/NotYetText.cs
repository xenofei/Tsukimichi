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

                return new NotYetCallout(clauses.Length == 0 ? Lead : Lead + BlockerText.Separator + clauses, LockedOut: false);
            }

            case QuestState.ReadyOnOtherJob:
            {
                // "ready on PLD" names the job already; the level still to reach on this one is worth saying.
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

                return new NotYetCallout(text, LockedOut: false);
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// The unmet requirements as a phrase list in the order the evaluator lists them (job, level, previous quests,
    /// Grand Company, allied society, …): "level 56 (you're 52) and 1 previous quest"; past
    /// <see cref="MaxClauses"/> the rest read "and 2 more". Empty when every requirement is met.
    /// </summary>
    /// <param name="skipJob">Leaves out the class or job admission (the callout of a quest ready on another job names that job).</param>
    public static string Clauses(QuestEvaluation evaluation, QuestRecord quest, BlockerNames names, bool skipJob = false)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(names);
        var phrases = new List<string>(MaxClauses + 1);
        var unmet = 0;
        foreach (var result in evaluation.Requirements)
        {
            if (result.Met || (skipJob && result.Req.Kind == RequirementKind.ClassJob) || Clause(result, quest, names) is not { Length: > 0 } phrase)
            {
                continue;
            }

            unmet++;
            if (phrases.Count < MaxClauses)
            {
                phrases.Add(phrase);
            }
        }

        if (unmet > phrases.Count)
        {
            phrases.Add(F("Core.NotYet.More", "{0} more", unmet - phrases.Count));
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
            TribeRankRequirement t => names.Tribe(t.Tribe) is { Length: > 0 } tribe
                ? F("Core.NotYet.TribeRankWith", "{0} with the {1} (you're {2})", names.TribeRank(t.RequiredRank), tribe, names.TribeRank(t.ActualRank))
                : F("Core.NotYet.TribeRank", "{0} (you're {1})", names.TribeRank(t.RequiredRank), names.TribeRank(t.ActualRank)),
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

    /// <summary>"level 56 (you're 52)", or "level 30 on PLD (you're 12)" when the quest pins a job.</summary>
    private static string Level(LevelRequirement l, QuestRecord quest, BlockerNames names)
    {
        var job = quest.ClassJobRequired == 0 ? string.Empty : names.JobAbbreviation(quest.ClassJobRequired);
        return job.Length > 0
            ? F("Core.NotYet.LevelOn", "level {0} on {1} (you're {2})", l.Level, job, l.ActualLevel)
            : F("Core.NotYet.Level", "level {0} (you're {1})", l.Level, l.ActualLevel);
    }

    /// <summary>"PLD (you're WHM)" for a pinned job; "another job (you're WHM)" for a category.</summary>
    private static string Job(ClassJobRequirement c, QuestRecord quest, BlockerNames names)
    {
        var pinned = c.RequiredJob != 0 ? c.RequiredJob : quest.ClassJobRequired;
        var required = pinned == 0 ? string.Empty : names.JobAbbreviation(pinned);
        var you = names.JobAbbreviation(c.Job);
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
        LevelRequirement l => new RequirementGap(l.ActualLevel, l.Level),
        GrandCompanyRankRequirement g => new RequirementGap(g.ActualRank, g.RequiredRank),
        TribeRankRequirement t => new RequirementGap(t.ActualRank, t.RequiredRank),
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
            _ => Gap(requirement) is { } gap
                ? Arrow(gap.Current.ToString(CultureInfo.CurrentCulture), gap.Required.ToString(CultureInfo.CurrentCulture))
                : null,
        };
    }

    private static string Arrow(string from, string to) => F("Core.Gap.Arrow", "{0} → {1}", from, to);

    /// <summary>
    /// The quest that clears an unmet requirement, for the jump button beside it: the nearest previous quest still to
    /// do (<see cref="BlockerText.NearestPrerequisite"/>); for a pinned job the character has not unlocked, the job's
    /// unlock quest; for a duty to clear, the quest that unlocks it. Null for a met requirement, a requirement no quest
    /// clears (a level, a rank, the seasonal event) and a quest the catalog does not hold; never the quest itself.
    /// </summary>
    /// <param name="jobUnlockQuest">A job's unlock quest row id (the ClassJob sheet's), 0 when none; null when unknown.</param>
    public static uint? JumpTarget(RequirementResult result, QuestRecord quest, QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation>? states, Func<uint, uint>? jobUnlockQuest = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(catalog);
        if (result.Met)
        {
            return null;
        }

        var target = result.Req switch
        {
            PreviousQuestsRequirement p => BlockerText.NearestPrerequisite(p, catalog, states),
            ClassJobRequirement or LevelRequirement => JobUnlock(quest, catalog, states, jobUnlockQuest),
            DutyCompletionRequirement d => DutyUnlock(d, catalog, states),
            _ => null,
        };
        return target is { } id && id != quest.RowId && catalog.ByRowId.ContainsKey(id) ? id : null;
    }

    /// <summary>The pinned job's unlock quest while the character has not completed it.</summary>
    private static uint? JobUnlock(QuestRecord quest, QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation>? states, Func<uint, uint>? jobUnlockQuest)
    {
        if (quest.ClassJobRequired == 0 || jobUnlockQuest?.Invoke(quest.ClassJobRequired) is not { } unlock || unlock == 0 || !catalog.ByRowId.ContainsKey(unlock))
        {
            return null;
        }

        return IsCompleted(unlock, states) ? null : unlock;
    }

    /// <summary>The first quest still to do that unlocks one of the duties (a <see cref="RewardKind.Instance"/> reward).</summary>
    private static uint? DutyUnlock(DutyCompletionRequirement d, QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation>? states)
    {
        foreach (var instance in d.InstanceIds)
        {
            foreach (var candidate in catalog.All)
            {
                foreach (var reward in candidate.Rewards)
                {
                    if (reward.Kind == RewardKind.Instance && reward.Id == instance && !IsCompleted(candidate.RowId, states))
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
