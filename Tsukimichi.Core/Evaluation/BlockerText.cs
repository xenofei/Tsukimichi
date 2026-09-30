using System.Globalization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// The one "why" line every surface prints (feature plan v3 P1): the single decisive blocker of a quest as a short
/// phrase in the display vocabulary ("after MSQ: The Vault", "Lv 80 on PLD", "Rank: Trusted with the Pelupelu"),
/// and <see cref="StatusText"/>, the state name followed by that phrase, which the Journal's Status column, the
/// dashboard, Compare, the item hint, the detail header and the chat commands all show. The state word is always the
/// first token of a status string.
/// <para>
/// <b>Decisive order.</b> The evaluator lists every gate; this picks the one the player must act on first, in play
/// order rather than in the sheet's order, so a prerequisite the character has yet to do outranks a level the
/// character has yet to reach, which outranks a rank, and a seasonal event that is not running comes last (the
/// player can do everything else meanwhile). The order is:
/// <list type="number">
/// <item>Removed from the game, then locked out by a completed lock (nothing can be done)</item>
/// <item>Expansion the account does not own; level above the account's cap</item>
/// <item>Prerequisite quests: the nearest unmet one, "after MSQ:" when it is a main scenario quest; through an
/// Any join, the prerequisite with the fewest quests left on its path</item>
/// <item>Job: the pinned job's level ("Lv 30 on PLD") or the category the quest is limited to</item>
/// <item>Level on the current job</item>
/// <item>Grand Company membership, then rank</item>
/// <item>Allied society rank, then reputation, then daily allowances and today's offer</item>
/// <item>Custom delivery satisfaction rank, then Delivery Moogle carrier level</item>
/// <item>Duties to clear</item>
/// <item>Mount, house</item>
/// <item>Seasonal event not running, or its chapter not open yet or over</item>
/// <item>Not checked: achievements not loaded, accept conditions the plugin cannot judge (docs: Help › Known quirks)</item>
/// </list>
/// A quest whose state is Not checked or Locked out keeps the evaluator's own reason (the veil or the lock), since
/// nothing else about it is decided.
/// </para>
/// </summary>
public static class BlockerText
{
    /// <summary>Between the state name and the reason in <see cref="StatusText"/>.</summary>
    public const string Separator = " · ";

    /// <summary>The journal sequence of a quest on its last step (<see cref="QuestRecord.StepCount"/>).</summary>
    public const byte FinalSequence = 255;

    private const string AfterMsqPrefix = "after MSQ: ";
    private const string AfterPrefix = "after: ";
    private const string ClosedByPrefix = "closed by: ";

    /// <summary>The reason a retired quest is locked out; the "Removed from the game" node and the detail pane use the same words.</summary>
    public const string RemovedFromGame = "removed from the game";
    private const string AnotherChoice = "another choice";
    private const string AnotherJob = "another job";
    private const string NotCheckedPrefix = "Not checked: ";

    private static readonly RequirementKind[] Priority =
    [
        RequirementKind.Retired,
        RequirementKind.Foreclosure,
        RequirementKind.ExpansionCap,
        RequirementKind.LevelCap,
        RequirementKind.PreviousQuests,
        RequirementKind.ClassJob,
        RequirementKind.Level,
        RequirementKind.GrandCompany,
        RequirementKind.GrandCompanyRank,
        RequirementKind.TribeRank,
        RequirementKind.TribeReputation,
        RequirementKind.TribeAllowance,
        RequirementKind.TribeDailyOffer,
        RequirementKind.CustomDeliveryRank,
        RequirementKind.CarrierLevel,
        RequirementKind.DutyCompletion,
        RequirementKind.Mount,
        RequirementKind.House,
        RequirementKind.Seasonal,
        RequirementKind.Achievement,
        RequirementKind.AcceptCondition,
    ];

    /// <summary>
    /// The decisive blocker as a short phrase; empty for a quest that is Ready, Ready on another job, In journal, done
    /// or Completed, and for a Blocked quest with no unmet requirement (should not occur).
    /// </summary>
    /// <param name="states">Every quest's evaluation, when at hand: tells which of several prerequisites are still to do and
    /// how far each Any-join branch is. Without it the first listed prerequisite is named.</param>
    public static string For(QuestEvaluation evaluation, QuestRecord quest, BlockerNames names, IReadOnlyDictionary<uint, QuestEvaluation>? states = null)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(names);

        RequirementResult? decisive;
        switch (evaluation.State)
        {
            case QuestState.Ready:
            case QuestState.ReadyOnOtherJob:
            case QuestState.Accepted:
            case QuestState.DoneThisCycle:
            case QuestState.Completed:
                return string.Empty;

            case QuestState.Foreclosed:
            case QuestState.Unknown:
                decisive = evaluation.NextStep ?? Decisive(evaluation.Requirements);
                break;

            default:
                decisive = Decisive(evaluation.Requirements) ?? evaluation.NextStep;
                break;
        }

        return decisive is null ? string.Empty : Phrase(decisive, evaluation.State, quest, names, states);
    }

    /// <summary>
    /// What follows the state name: the blocker from <see cref="For"/>, or "step 3 of 7" for a quest in the journal.
    /// Empty when there is nothing to add.
    /// </summary>
    public static string Reason(QuestEvaluation evaluation, QuestRecord quest, BlockerNames names, IReadOnlyDictionary<uint, QuestEvaluation>? states = null)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(quest);
        return evaluation.State == QuestState.Accepted ? StepText(evaluation.Sequence, quest.StepCount) : For(evaluation, quest, names, states);
    }

    /// <summary>
    /// "&lt;state name&gt; · &lt;blocker&gt;" for Blocked, Locked out and Not checked, "In journal · step 3 of 7" for a
    /// quest in the journal, and the state name alone otherwise; a null evaluation reads as Not checked. The state word
    /// is always the first token.
    /// </summary>
    public static string StatusText(QuestEvaluation? evaluation, QuestRecord quest, BlockerNames names, IReadOnlyDictionary<uint, QuestEvaluation>? states = null)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(names);

        var name = StateNames.Name(evaluation?.State ?? QuestState.Unknown, quest);
        if (evaluation is null)
        {
            return name;
        }

        var reason = Reason(evaluation, quest, names, states);
        if (evaluation.State == QuestState.Unknown && reason.StartsWith(NotCheckedPrefix, StringComparison.Ordinal))
        {
            // "Not checked · achievements", not "Not checked · Not checked: achievements".
            reason = reason[NotCheckedPrefix.Length..];
        }

        return reason.Length == 0 ? name : name + Separator + reason;
    }

    /// <summary>
    /// "step 3 of 7" for a journal sequence and the quest's step count; the final sequence (255) is the last step.
    /// Without a step count only "step 3" can be said; without a sequence, nothing.
    /// </summary>
    public static string StepText(byte? sequence, byte stepCount)
    {
        if (sequence is not { } seq)
        {
            return string.Empty;
        }

        if (stepCount == 0)
        {
            return "step " + seq.ToString(CultureInfo.InvariantCulture);
        }

        var step = seq == FinalSequence ? stepCount : Math.Clamp(seq, (byte)1, stepCount);
        return string.Create(CultureInfo.InvariantCulture, $"step {step} of {stepCount}");
    }

    /// <summary>The first unmet requirement in <see cref="Priority"/> order.</summary>
    private static RequirementResult? Decisive(IReadOnlyList<RequirementResult> requirements)
    {
        foreach (var kind in Priority)
        {
            foreach (var result in requirements)
            {
                if (!result.Met && result.Req.Kind == kind)
                {
                    return result;
                }
            }
        }

        return null;
    }

    private static string Phrase(RequirementResult result, QuestState state, QuestRecord quest, BlockerNames names, IReadOnlyDictionary<uint, QuestEvaluation>? states) =>
        result.Req switch
        {
            RetiredRequirement => RemovedFromGame,
            ForeclosureRequirement f => ClosedByPrefix + (f.CompletedLockIds.Length > 0 ? QuestName(names, f.CompletedLockIds[0]) : AnotherChoice),
            ExpansionCapRequirement e => "Expansion: " + names.Expansion(e.Expansion),
            LevelCapRequirement l => string.Create(CultureInfo.InvariantCulture, $"Lv {l.Level}, above your cap"),
            PreviousQuestsRequirement p => Prerequisite(p, names, states),
            ClassJobRequirement c => Job(c, quest, names),
            LevelRequirement l => Level(l.Level, quest, names),
            GrandCompanyRequirement g => "Grand Company: " + names.GrandCompany(g.GrandCompany),
            GrandCompanyRankRequirement g => "Grand Company: " + names.GrandCompanyRank(g.RequiredRank),
            TribeRankRequirement t => "Rank: " + names.TribeRank(t.RequiredRank) + WithTribe(t.Tribe, names),
            TribeReputationRequirement t => string.Create(CultureInfo.InvariantCulture, $"Reputation: {Math.Max(0, t.RequiredValue - t.ActualValue)} more") + WithTribe(t.Tribe, names),
            TribeAllowanceRequirement => "Allowance: none left today",
            TribeDailyOfferRequirement => "Not offered today",
            CustomDeliveryRankRequirement c => c.ActualRank is null ? NotCheckedPrefix + "custom delivery rank" : CustomDelivery(c, names),
            CarrierLevelRequirement c => c.ActualLevel is null ? NotCheckedPrefix + "carrier level" : string.Create(CultureInfo.InvariantCulture, $"Delivery Moogle: carrier level {c.RequiredLevel}"),
            DutyCompletionRequirement d => Duty(d, names),
            SeasonalRequirement s => Seasonal(s, state),
            MountRequirement m => m.HasMount is null ? NotCheckedPrefix + "mount" : "Mount",
            HouseRequirement h => h.HasHouse is null ? NotCheckedPrefix + "house" : "House",
            AchievementRequirement => NotCheckedPrefix + "achievements",
            AcceptConditionRequirement => NotCheckedPrefix + "accept condition",
            _ => result.Detail,
        };

    /// <summary>"Lv 80", or "Lv 80 on PLD" when the quest pins a job.</summary>
    private static string Level(byte level, QuestRecord quest, BlockerNames names)
    {
        var text = "Lv " + level.ToString(CultureInfo.InvariantCulture);
        if (quest.ClassJobRequired == 0)
        {
            return text;
        }

        var job = names.JobAbbreviation(quest.ClassJobRequired);
        return text + " on " + (job.Length > 0 ? job : AnotherJob);
    }

    /// <summary>
    /// A pinned job reads as that job's level ("Lv 30 on PLD": reaching it there is the whole gate); a category reads
    /// as the category and the job the check was made for ("Job: Disciples of the Hand, you are WHM").
    /// </summary>
    private static string Job(ClassJobRequirement c, QuestRecord quest, BlockerNames names)
    {
        if (quest.ClassJobRequired != 0)
        {
            if (quest.Level > 0)
            {
                return Level(quest.Level, quest, names);
            }

            var required = names.JobAbbreviation(quest.ClassJobRequired);
            return "Job: " + (required.Length > 0 ? required : AnotherJob) + YouAre(c.Job, names);
        }

        // "Job: any Disciple of the Hand, you are WHM"; the sheet already opens some category names with "Any".
        var category = names.ClassJobCategory(c.CategoryId);
        if (category.Length == 0)
        {
            return "Job: " + AnotherJob + YouAre(c.Job, names);
        }

        var any = category.StartsWith("any ", StringComparison.OrdinalIgnoreCase) ? string.Empty : "any ";
        return "Job: " + any + category + YouAre(c.Job, names);
    }

    private static string YouAre(byte job, BlockerNames names)
    {
        var abbreviation = names.JobAbbreviation(job);
        return abbreviation.Length > 0 ? ", you are " + abbreviation : string.Empty;
    }

    private static string WithTribe(byte tribe, BlockerNames names)
    {
        var name = names.Tribe(tribe);
        return name.Length > 0 ? " with the " + name : string.Empty;
    }

    /// <summary>
    /// The nearest unmet prerequisite: the first listed one still to do (completed ones are known from the requirement
    /// itself, or from <paramref name="states"/>); through an Any join, with states at hand, the one whose path has
    /// the fewest quests left (ties to the lowest row id). Main scenario quests get the "after MSQ:" prefix.
    /// </summary>
    private static string Prerequisite(PreviousQuestsRequirement p, BlockerNames names, IReadOnlyDictionary<uint, QuestEvaluation>? states)
    {
        var ids = p.QuestIds;
        if (ids.Length == 0)
        {
            return AfterPrefix + AnotherChoice;
        }

        var chosen = ids[0];
        var found = false;
        var bestRemaining = int.MaxValue;
        foreach (var id in ids)
        {
            if (IsDone(id, p, states))
            {
                continue;
            }

            if (p.Join != JoinKind.Any || states is null)
            {
                chosen = id;
                break;
            }

            var remaining = names.Catalog.ByRowId.ContainsKey(id) ? PathFinder.RemainingCount(id, names.Catalog, states) : int.MaxValue;
            if (!found || remaining < bestRemaining || (remaining == bestRemaining && id < chosen))
            {
                chosen = id;
                bestRemaining = remaining;
                found = true;
            }
        }

        var quest = names.Catalog.GetByRowId(chosen);
        var prefix = quest is not null && FeaturePresets.IsMainScenario(quest) ? AfterMsqPrefix : AfterPrefix;
        return prefix + QuestName(names, chosen);
    }

    private static bool IsDone(uint rowId, PreviousQuestsRequirement p, IReadOnlyDictionary<uint, QuestEvaluation>? states)
    {
        if (states is not null)
        {
            return states.TryGetValue(rowId, out var evaluation) && evaluation.State == QuestState.Completed;
        }

        return p.DoneIds is not null && Array.IndexOf(p.DoneIds, rowId) >= 0;
    }

    /// <summary>"Custom delivery: rank 4 with M'naago"; without a client name, "Custom delivery: rank 4".</summary>
    private static string CustomDelivery(CustomDeliveryRankRequirement c, BlockerNames names)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"Custom delivery: rank {c.RequiredRank}");
        var npc = names.SatisfactionNpc(c.Npc);
        return npc.Length > 0 ? text + " with " + npc : text;
    }

    /// <summary>
    /// "Seasonal: ended" for a run the character missed, "Seasonal: chapter not open yet" or "Seasonal: chapter over"
    /// while the event runs but its phase lies outside the quest's window, and "Seasonal: not running" otherwise.
    /// </summary>
    private static string Seasonal(SeasonalRequirement s, QuestState state)
    {
        if (state == QuestState.Foreclosed)
        {
            return "Seasonal: ended";
        }

        if (s.ChapterNotOpen)
        {
            return "Seasonal: chapter not open yet";
        }

        return s.ChapterOver ? "Seasonal: chapter over" : "Seasonal: not running";
    }

    /// <summary>"Duty: The Vault" for the first duty with a name; otherwise how many are left to clear.</summary>
    private static string Duty(DutyCompletionRequirement d, BlockerNames names)
    {
        foreach (var id in d.InstanceIds)
        {
            var name = names.Duty(id);
            if (name.Length > 0)
            {
                return "Duty: " + name;
            }
        }

        var remaining = d.Join == JoinKind.Any ? 1 : Math.Max(1, d.InstanceIds.Length - d.DoneCount);
        return string.Create(CultureInfo.InvariantCulture, $"Duty: {remaining} to clear");
    }

    private static string QuestName(BlockerNames names, uint rowId) =>
        names.Catalog.GetByRowId(rowId) is { Name.Length: > 0 } quest ? names.QuestName(quest) : "quest " + rowId.ToString(CultureInfo.InvariantCulture);
}
