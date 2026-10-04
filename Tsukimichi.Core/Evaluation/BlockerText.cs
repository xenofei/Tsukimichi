using System.Globalization;
using Tsukimichi.Core.Localization;
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
/// <item>Removed from the game, then on a path the character did not take ("Another city's start (Ul'dah)"), then
/// locked out by a completed lock (nothing can be done)</item>
/// <item>Expansion the account does not own; level above the account's cap</item>
/// <item>Prerequisite quests: the nearest unmet one, "after MSQ:" when it is a main scenario quest; through an
/// Any join, the prerequisite with the fewest quests left on its path</item>
/// <item>A relic weapon the character carries but has not equipped, which a gear gate wants ("equip Curtana Zenith"):
/// equipping it is the job change</item>
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
/// <para>
/// The phrases are English here and follow the UI language through <see cref="CoreText"/> (keys
/// <c>Core.Blocker.*</c>); each is one format string, so a language can move the name it carries. The separator stays
/// " · " in every language: the table's status column splits on it.
/// </para>
/// </summary>
public static class BlockerText
{
    /// <summary>Between the state name and the reason in <see cref="StatusText"/>.</summary>
    public const string Separator = " · ";

    /// <summary>The journal sequence of a quest on its last step (<see cref="QuestRecord.StepCount"/>).</summary>
    public const byte FinalSequence = 255;

    /// <summary>The reason a retired quest is locked out; the "Removed from the game" node and the detail pane use the same words.</summary>
    public static string RemovedFromGame => CoreText.T("Core.Blocker.RemovedFromGame", "removed from the game");

    private static string AnotherChoice => CoreText.T("Core.Blocker.AnotherChoice", "another choice");
    private static string AnotherJob => CoreText.T("Core.Blocker.AnotherJob", "another job");

    private static string F(string key, string english, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, CoreText.T(key, english), args);

    private static readonly RequirementKind[] Priority =
    [
        RequirementKind.Retired,
        RequirementKind.OtherPath,
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
        RequirementKind.GameGate,
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

        var decisive = Decide(evaluation);
        return decisive is null ? string.Empty : Phrase(decisive, evaluation.State, quest, names, states);
    }

    /// <summary>The requirement <see cref="For"/> names (the decisive blocker); null for a state that needs no reason.</summary>
    public static Requirement? DecisiveRequirement(QuestEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        return Decide(evaluation)?.Req;
    }

    /// <summary>The requirement <see cref="For"/> names; null for a state that needs no reason.</summary>
    private static RequirementResult? Decide(QuestEvaluation evaluation) => evaluation.State switch
    {
        QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Accepted or QuestState.DoneThisCycle or QuestState.Completed => null,
        QuestState.Foreclosed or QuestState.Unknown => evaluation.NextStep ?? Decisive(evaluation.Requirements),
        _ => Decisive(evaluation.Requirements) ?? evaluation.NextStep,
    };

    /// <summary>
    /// What the plugin could not check for a requirement ("achievements", "mount"), or null when it did check it; the
    /// phrase is "Not checked: achievements", and the status line writes the item alone after the state name.
    /// </summary>
    private static string? NotCheckedItem(Requirement requirement) => requirement switch
    {
        CustomDeliveryRankRequirement { ActualRank: null } => CoreText.T("Core.Blocker.NotChecked.CustomDeliveryRank", "custom delivery rank"),
        CarrierLevelRequirement { ActualLevel: null } => CoreText.T("Core.Blocker.NotChecked.CarrierLevel", "carrier level"),
        MountRequirement { HasMount: null } => CoreText.T("Core.Blocker.NotChecked.Mount", "mount"),
        HouseRequirement { HasHouse: null } => CoreText.T("Core.Blocker.NotChecked.House", "house"),
        AchievementRequirement => CoreText.T("Core.Blocker.NotChecked.Achievements", "achievements"),
        GameGateRequirement { IsNotChecked: true } g => g.Gate,
        AcceptConditionRequirement => CoreText.T("Core.Blocker.NotChecked.AcceptCondition", "accept condition"),
        _ => null,
    };

    /// <summary>
    /// What follows the state name: the blocker from <see cref="For"/>, or "step 3 of 7" for a quest in the journal.
    /// An option of a choice the character has not made yet adds "Choose one of 3" (<see cref="QuestEvaluation.ChoiceOf"/>).
    /// Empty when there is nothing to add.
    /// </summary>
    public static string Reason(QuestEvaluation evaluation, QuestRecord quest, BlockerNames names, IReadOnlyDictionary<uint, QuestEvaluation>? states = null)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(quest);
        var reason = evaluation.State == QuestState.Accepted ? StepText(evaluation.Sequence, quest.StepCount) : For(evaluation, quest, names, states);
        if (evaluation.ChoiceOf > 1 && evaluation.State is QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Blocked or QuestState.Unknown)
        {
            var choose = PathText.ChooseOne(evaluation.ChoiceOf);
            reason = reason.Length == 0 ? choose : reason + Separator + choose;
        }

        return reason;
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
        if (evaluation.State == QuestState.Unknown && Decide(evaluation) is { } decisive && NotCheckedItem(decisive.Req) is { } item)
        {
            // "Not checked · achievements", not "Not checked · Not checked: achievements".
            reason = evaluation.ChoiceOf > 1 ? item + Separator + PathText.ChooseOne(evaluation.ChoiceOf) : item;

            // A game gate Tsukimichi can't check reads "Can't check · <gate>" (1.19 C3): the moon stays Not checked.
            if (decisive.Req is GameGateRequirement { IsNotChecked: true })
            {
                name = CoreText.T("Core.Blocker.CantCheck", "Can't check");
            }
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
            return F("Core.Blocker.Step", "step {0}", seq);
        }

        var step = seq == FinalSequence ? stepCount : Math.Clamp(seq, (byte)1, stepCount);
        return F("Core.Blocker.StepOf", "step {0} of {1}", step, stepCount);
    }

    /// <summary>The first unmet requirement in <see cref="Priority"/> order.</summary>
    private static RequirementResult? Decisive(IReadOnlyList<RequirementResult> requirements)
    {
        foreach (var kind in Priority)
        {
            foreach (var result in requirements)
            {
                // A relic weapon carried but not equipped comes before the job and level: equipping it is the job change.
                if (kind == RequirementKind.ClassJob && StateResolver.IsCarriedGate(result))
                {
                    return result;
                }

                if (!result.Met && result.Req.Kind == kind)
                {
                    return result;
                }
            }
        }

        return null;
    }

    private static string Phrase(RequirementResult result, QuestState state, QuestRecord quest, BlockerNames names, IReadOnlyDictionary<uint, QuestEvaluation>? states)
    {
        if (NotCheckedItem(result.Req) is { } item)
        {
            return F("Core.Blocker.NotChecked", "Not checked: {0}", item);
        }

        return result.Req switch
        {
            RetiredRequirement => RemovedFromGame,
            OtherPathRequirement o => PathText.Reason(o, names.GrandCompany),
            ForeclosureRequirement f => F("Core.Blocker.ClosedBy", "closed by: {0}", f.CompletedLockIds.Length > 0 ? QuestName(names, f.CompletedLockIds[0]) : AnotherChoice),
            ExpansionCapRequirement e => F("Core.Blocker.Expansion", "Expansion: {0}", names.Expansion(e.Expansion)),
            LevelCapRequirement l => F("Core.Blocker.LevelCap", "Lv {0}, above your cap", l.Level),
            PreviousQuestsRequirement p => Prerequisite(p, names, states),
            ClassJobRequirement c => Job(c, quest, names),
            LevelRequirement l => Level(l.Level, quest, names),
            GrandCompanyRequirement g => F("Core.Blocker.GrandCompany", "Grand Company: {0}", names.GrandCompany(g.GrandCompany)),
            GrandCompanyRankRequirement g => F("Core.Blocker.GrandCompany", "Grand Company: {0}", names.GrandCompanyRank(g.RequiredRank)),
            TribeRankRequirement t => names.Tribe(t.Tribe) is { Length: > 0 } tribe
                ? F("Core.Blocker.TribeRankWith", "Rank: {0} with the {1}", names.TribeRank(t.RequiredRank), tribe)
                : F("Core.Blocker.TribeRank", "Rank: {0}", names.TribeRank(t.RequiredRank)),
            TribeReputationRequirement { MaxedRank: not 0 } t => names.Tribe(t.Tribe) is { Length: > 0 } tribe
                ? F("Core.Blocker.MaxedReputationWith", "Reputation: {0} {1:N0}/{2:N0} with the {3}", names.TribeRank(t.MaxedRank), t.ActualValue, t.RequiredValue, tribe)
                : F("Core.Blocker.MaxedReputation", "Reputation: {0} {1:N0}/{2:N0}", names.TribeRank(t.MaxedRank), t.ActualValue, t.RequiredValue),
            TribeReputationRequirement t => names.Tribe(t.Tribe) is { Length: > 0 } tribe
                ? F("Core.Blocker.ReputationWith", "Reputation: {0} more with the {1}", Math.Max(0, t.RequiredValue - t.ActualValue), tribe)
                : F("Core.Blocker.Reputation", "Reputation: {0} more", Math.Max(0, t.RequiredValue - t.ActualValue)),
            TribeAllowanceRequirement => CoreText.T("Core.Blocker.Allowance", "Allowance: none left today"),
            TribeDailyOfferRequirement => CoreText.T("Core.Blocker.NotOffered", "Not offered today"),
            CustomDeliveryRankRequirement c => CustomDelivery(c, names),
            CarrierLevelRequirement c => F("Core.Blocker.CarrierLevel", "Delivery Moogle: carrier level {0}", c.RequiredLevel),
            DutyCompletionRequirement d => Duty(d, names),
            SeasonalRequirement s => Seasonal(s, state),
            MountRequirement => CoreText.T("Core.Blocker.Mount", "Mount"),
            HouseRequirement => CoreText.T("Core.Blocker.House", "House"),
            _ => result.Detail,
        };
    }

    /// <summary>"Lv 80", or "Lv 80 on PLD" when the quest pins a job.</summary>
    private static string Level(byte level, QuestRecord quest, BlockerNames names)
    {
        if (quest.ClassJobRequired == 0)
        {
            return F("Core.Blocker.Level", "Lv {0}", level);
        }

        var job = names.JobAbbreviation(quest.ClassJobRequired);
        return F("Core.Blocker.LevelOn", "Lv {0} on {1}", level, job.Length > 0 ? job : AnotherJob);
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
            return JobPhrase(required.Length > 0 ? required : AnotherJob, c.Job, names);
        }

        // "Job: any Disciple of the Hand, you are WHM"; the sheet already opens some category names with "Any".
        var category = names.ClassJobCategory(c.CategoryId);
        if (category.Length == 0)
        {
            return JobPhrase(AnotherJob, c.Job, names);
        }

        if (category.StartsWith("any ", StringComparison.OrdinalIgnoreCase))
        {
            return JobPhrase(category, c.Job, names);
        }

        var you = names.JobAbbreviation(c.Job);
        return you.Length > 0
            ? F("Core.Blocker.JobAnyYouAre", "Job: any {0}, you are {1}", category, you)
            : F("Core.Blocker.JobAny", "Job: any {0}", category);
    }

    /// <summary>"Job: PLD, you are WHM", or "Job: PLD" when the current job has no abbreviation.</summary>
    private static string JobPhrase(string required, byte job, BlockerNames names)
    {
        var you = names.JobAbbreviation(job);
        return you.Length > 0
            ? F("Core.Blocker.JobYouAre", "Job: {0}, you are {1}", required, you)
            : F("Core.Blocker.Job", "Job: {0}", required);
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
            return F("Core.Blocker.After", "after: {0}", AnotherChoice);
        }

        var chosen = NearestPrerequisite(p, names.Catalog, states) ?? ids[0];
        var quest = names.Catalog.GetByRowId(chosen);
        return quest is not null && FeaturePresets.IsMainScenario(quest)
            ? F("Core.Blocker.AfterMsq", "after MSQ: {0}", QuestName(names, chosen))
            : F("Core.Blocker.After", "after: {0}", QuestName(names, chosen));
    }

    /// <summary>
    /// The prerequisite the blocker line names, and the one the detail pane's jump button selects: the first listed
    /// one still to do; through an Any join, with <paramref name="states"/> at hand, the one whose path has the fewest
    /// quests left (ties to the lowest row id), another path's line last. Null when every prerequisite is done or
    /// there is none.
    /// </summary>
    /// <param name="usable">Leaves out a prerequisite it answers false for (the jump button's: one the character can
    /// never take); the next nearest is chosen instead, and null when none is left.</param>
    public static uint? NearestPrerequisite(PreviousQuestsRequirement p, QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation>? states, Func<uint, bool>? usable = null)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(catalog);
        uint? chosen = null;
        var bestRemaining = int.MaxValue;
        foreach (var id in p.QuestIds)
        {
            if (IsDone(id, p, states) || (usable is not null && !usable(id)))
            {
                continue;
            }

            if (p.Join != JoinKind.Any || states is null)
            {
                return id;
            }

            // Another city's or class's line is never the one to name while the character's own is open.
            var remaining = states.TryGetValue(id, out var evaluation) && evaluation.IsOtherPath ? int.MaxValue - 1
                : catalog.ByRowId.ContainsKey(id) ? PathFinder.RemainingCount(id, catalog, states)
                : int.MaxValue;
            if (chosen is not { } best || remaining < bestRemaining || (remaining == bestRemaining && id < best))
            {
                chosen = id;
                bestRemaining = remaining;
            }
        }

        return chosen;
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
        var npc = names.SatisfactionNpc(c.Npc);
        return npc.Length > 0
            ? F("Core.Blocker.CustomDeliveryWith", "Custom delivery: rank {0} with {1}", c.RequiredRank, npc)
            : F("Core.Blocker.CustomDelivery", "Custom delivery: rank {0}", c.RequiredRank);
    }

    /// <summary>
    /// "Seasonal: ended" for a run the character missed, "Seasonal: chapter not open yet" or "Seasonal: chapter over"
    /// while the event runs but its phase lies outside the quest's window, and "Seasonal: not running" otherwise.
    /// </summary>
    private static string Seasonal(SeasonalRequirement s, QuestState state)
    {
        if (state == QuestState.Foreclosed)
        {
            return CoreText.T("Core.Blocker.SeasonalEnded", "Seasonal: ended");
        }

        if (s.ChapterNotOpen)
        {
            return CoreText.T("Core.Blocker.SeasonalChapterNotOpen", "Seasonal: chapter not open yet");
        }

        return s.ChapterOver
            ? CoreText.T("Core.Blocker.SeasonalChapterOver", "Seasonal: chapter over")
            : CoreText.T("Core.Blocker.SeasonalNotRunning", "Seasonal: not running");
    }

    /// <summary>"Duty: The Vault" for the first duty with a name; otherwise how many are left to clear.</summary>
    private static string Duty(DutyCompletionRequirement d, BlockerNames names)
    {
        foreach (var id in d.InstanceIds)
        {
            var name = names.Duty(id);
            if (name.Length > 0)
            {
                return F("Core.Blocker.Duty", "Duty: {0}", name);
            }
        }

        var remaining = d.Join == JoinKind.Any ? 1 : Math.Max(1, d.InstanceIds.Length - d.DoneCount);
        return F("Core.Blocker.DutiesToClear", "Duty: {0} to clear", remaining);
    }

    private static string QuestName(BlockerNames names, uint rowId) =>
        names.Catalog.GetByRowId(rowId) is { Name.Length: > 0 } quest ? names.QuestName(quest) : F("Core.Blocker.QuestId", "quest {0}", rowId);
}
