using System.Globalization;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// The requirement's clause as the detail pane shows it ("needs level 50, you are 42"), in the UI language. The
/// evaluator writes <see cref="RequirementResult.Detail"/> in English once, at evaluation time, and the diagnostic
/// block keeps that; a language switch must not wait for the next evaluation, so this renders the clause from the
/// requirement record at display time. In English it is <see cref="RequirementResult.Detail"/> itself, unchanged;
/// in another language each phrase comes from <see cref="CoreText"/> (keys <c>Core.Req.*</c>), and a record that
/// carries too little to say more falls back to the English detail.
/// </summary>
public static class RequirementDetail
{
    /// <summary>The clause for <paramref name="result"/> in the UI language.</summary>
    /// <param name="currentJob">The job the character is on now; says "the current job" rather than "this job".</param>
    public static string Text(RequirementResult result, BlockerNames names, byte? currentJob = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(names);
        // A level measured on another job (NotYetText.OnAdmittedJob) names that job, which the English detail cannot.
        if (CoreText.IsEnglish && result.Req is not LevelRequirement { MeasuredOn: not 0 } and not LevelRequirement { NoJob: true })
        {
            return result.Detail;
        }

        return Render(result, names, currentJob) ?? result.Detail;
    }

    /// <summary>The clause rendered from the record through <see cref="CoreText"/>; null when the record cannot say it.</summary>
    public static string? Render(RequirementResult result, BlockerNames names, byte? currentJob = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(names);
        var met = result.Met;
        return result.Req switch
        {
            RetiredRequirement => T("Core.Req.Retired", "removed from the game"),
            OtherPathRequirement o => PathText.Detail(o, names.GrandCompany),
            ForeclosureRequirement f => f.CompletedLockIds.Length == 0
                ? T("Core.Req.NoConflict", "no conflicting quest completed")
                : F("Core.Req.LockedOutBy", "locked out by {0}", string.Join(", ", f.CompletedLockIds.Select(id => QuestName(names, id)))),
            ExpansionCapRequirement e => F("Core.Req.RequiresExpansion", "requires {0}", names.Expansion(e.Expansion)),
            LevelCapRequirement l => F("Core.Req.LevelCap", "level {0} is above your cap of {1}", l.Level, l.LevelCap),
            ClassJobRequirement c => (met, currentJob is { } now && now == c.Job) switch
            {
                (true, true) => T("Core.Req.AvailableCurrentJob", "available on the current job"),
                (true, false) => T("Core.Req.AvailableThisJob", "available on this job"),
                (false, true) => T("Core.Req.NotAvailableCurrentJob", "not available on the current job"),
                _ => T("Core.Req.NotAvailableThisJob", "not available on this job"),
            },
            LevelRequirement l when l.NoJob || (l.MeasuredOn != 0 && names.JobAbbreviation(l.MeasuredOn).Length > 0) => MeasuredLevel(l, met, names),
            LevelRequirement l => met
                ? F("Core.Req.Level", "level {0}", l.Level)
                : F("Core.Req.NeedsLevel", "needs level {0}, you are {1}", l.Level, l.ActualLevel),
            PreviousQuestsRequirement p => Previous(p, met, names),
            GrandCompanyRequirement g => met
                ? names.GrandCompany(g.GrandCompany)
                : F("Core.Req.RequiresGrandCompany", "requires {0}", names.GrandCompany(g.GrandCompany)),
            GrandCompanyRankRequirement g => met
                ? F("Core.Req.GrandCompanyRank", "{0} rank {1}", names.GrandCompany(g.GrandCompany), g.ActualRank)
                : F("Core.Req.NeedsGrandCompanyRank", "needs {0} rank {1}, you are rank {2}", names.GrandCompany(g.GrandCompany), g.RequiredRank, g.ActualRank),
            TribeRankRequirement t => met
                ? names.TribeRank(t.ActualRank)
                : F("Core.Req.NeedsTribeRank", "needs {0}, you are {1}", names.TribeRank(t.RequiredRank), names.TribeRank(t.ActualRank)),
            TribeReputationRequirement { MaxedRank: not 0, NotChecked: true } t =>
                F("Core.Req.NeedsMaxedReputationNotChecked", "needs {0} reputation maxed, not checked", names.TribeRank(t.MaxedRank)),
            TribeReputationRequirement { MaxedRank: not 0 } t => met
                ? F("Core.Req.MaxedReputation", "{0} reputation maxed", names.TribeRank(t.MaxedRank))
                : F("Core.Req.RankReputation", "{0} {1:N0}/{2:N0} reputation", names.TribeRank(t.MaxedRank), t.ActualValue, t.RequiredValue),
            TribeReputationRequirement t => met
                ? F("Core.Req.Reputation", "{0} reputation", t.ActualValue)
                : F("Core.Req.NeedsReputation", "needs {0} reputation, you have {1}", t.RequiredValue, t.ActualValue),
            TribeAllowanceRequirement a => a.Allowance > 0
                ? F("Core.Req.AllowancesLeft", "{0} allowances left", a.Allowance)
                : T("Core.Req.NoAllowances", "no allowances left today"),
            TribeDailyOfferRequirement d => d.Offered
                ? T("Core.Req.OfferedToday", "offered today")
                : T("Core.Req.NotOfferedToday", "not offered today"),
            CustomDeliveryRankRequirement c => CustomDelivery(c, met, names),
            CarrierLevelRequirement c => c.ActualLevel switch
            {
                null => F("Core.Req.NeedsCarrierNotChecked", "needs carrier level {0}, not checked", c.RequiredLevel),
                { } actual when met => F("Core.Req.CarrierLevel", "carrier level {0}", actual),
                { } actual => F("Core.Req.NeedsCarrierLevel", "needs carrier level {0}, you are level {1}", c.RequiredLevel, actual),
            },
            DutyCompletionRequirement d => Duties(d),
            SeasonalRequirement s => Seasonal(s),
            AcceptConditionRequirement a => a.ConditionIds.Length == 1
                ? T("Core.Req.AcceptConditionOne", "1 accept condition not checked")
                : F("Core.Req.AcceptConditions", "{0} accept conditions not checked", a.ConditionIds.Length),
            // Names the mounts (MountCheck), which no CoreText phrase carries yet: the English detail stands.
            MountRequirement { Mounts.Length: > 0 } => null,
            MountRequirement m => m.HasMount switch
            {
                true => T("Core.Req.MountAvailable", "mount available"),
                false => T("Core.Req.RequiresMount", "requires a mount"),
                null => T("Core.Req.RequiresMountNotChecked", "requires a mount, not checked"),
            },
            HouseRequirement h => h.HasHouse switch
            {
                true => T("Core.Req.HouseAvailable", "house available"),
                false => T("Core.Req.RequiresHouse", "requires a house"),
                null => T("Core.Req.RequiresHouseNotChecked", "requires a house, not checked"),
            },
            AchievementRequirement a => a.Loaded
                ? T("Core.Req.Achievement", "achievement requirement, see journal")
                : T("Core.Req.AchievementsNotLoaded", "achievements not loaded"),
            GameGateRequirement { Checked: null } g => string.Format(CultureInfo.CurrentCulture, T("Core.Req.GameGateNotChecked", "needs {0}, not checked"), g.Gate),
            _ => null,
        };
    }

    /// <summary>
    /// A level measured on the job that can take the quest (<see cref="NotYetText.OnAdmittedJob"/>): "level 80 on PLD",
    /// "needs level 70, CUL is 35"; with no such job, "needs level 70 on CUL, not unlocked" for a pinned job and "needs
    /// level 50, no job of yours can take it" for a category.
    /// </summary>
    private static string MeasuredLevel(LevelRequirement l, bool met, BlockerNames names)
    {
        var job = l.MeasuredOn == 0 ? string.Empty : names.JobAbbreviation(l.MeasuredOn);
        if (l.NoJob)
        {
            return job.Length > 0
                ? F("Core.Req.NeedsLevelOnLocked", "needs level {0} on {1}, not unlocked", l.Level, job)
                : F("Core.Req.NeedsLevelNoJob", "needs level {0}, no job of yours can take it", l.Level);
        }

        return met
            ? F("Core.Req.LevelOn", "level {0} on {1}", l.Level, job)
            : F("Core.Req.NeedsLevelJobIs", "needs level {0}, {1} is {2}", l.Level, job, l.ActualLevel);
    }

    private static string Previous(PreviousQuestsRequirement p, bool met, BlockerNames names)
    {
        if (p.QuestIds.Length == 1)
        {
            var name = QuestName(names, p.QuestIds[0]);
            return met ? F("Core.Req.QuestDone", "{0} done", name) : F("Core.Req.NeedsQuest", "needs {0}", name);
        }

        // What is left, not a tally (feature plan v6 U5); the same words as RequirementEvaluator.LeftDetail.
        var left = p.QuestIds.Length - p.DoneCount;
        if (p.Join == JoinKind.Any)
        {
            return p.DoneCount > 0
                ? F("Core.Req.PrerequisitesOneDone", "one of {0} prerequisites done", p.QuestIds.Length)
                : F("Core.Req.PrerequisitesOneNeeded", "needs one of {0} prerequisites", p.QuestIds.Length);
        }

        return left <= 0 ? F("Core.Req.PrerequisitesAllDone", "all {0} prerequisites done", p.QuestIds.Length)
            : left == 1 ? T("Core.Req.PrerequisiteLeft", "1 prerequisite left")
            : F("Core.Req.PrerequisitesLeft", "{0} prerequisites left", left);
    }

    /// <summary>What is left of a duty requirement; the same words as RequirementEvaluator.LeftDetail.</summary>
    private static string Duties(DutyCompletionRequirement d)
    {
        var left = d.InstanceIds.Length - d.DoneCount;
        if (d.Join == JoinKind.Any)
        {
            return d.DoneCount > 0
                ? F("Core.Req.DutiesOneDone", "one of {0} duties completed", d.InstanceIds.Length)
                : F("Core.Req.DutiesOneNeeded", "needs one of {0} duties", d.InstanceIds.Length);
        }

        return left <= 0 ? F("Core.Req.DutiesAllDone", "all {0} duties completed", d.InstanceIds.Length)
            : left == 1 ? T("Core.Req.DutyLeft", "1 duty left")
            : F("Core.Req.DutiesLeft", "{0} duties left", left);
    }

    private static string CustomDelivery(CustomDeliveryRankRequirement c, bool met, BlockerNames names)
    {
        var npc = names.SatisfactionNpc(c.Npc);
        return (c.ActualRank, npc.Length > 0) switch
        {
            (null, true) => F("Core.Req.NeedsSatisfactionWithNotChecked", "needs satisfaction rank {0} with {1}, not checked", c.RequiredRank, npc),
            (null, false) => F("Core.Req.NeedsSatisfactionNotChecked", "needs satisfaction rank {0}, not checked", c.RequiredRank),
            ({ } actual, true) when met => F("Core.Req.SatisfactionWith", "satisfaction rank {0} with {1}", actual, npc),
            ({ } actual, false) when met => F("Core.Req.Satisfaction", "satisfaction rank {0}", actual),
            ({ } actual, true) => F("Core.Req.NeedsSatisfactionWith", "needs satisfaction rank {0} with {1}, you are rank {2}", c.RequiredRank, npc, actual),
            ({ } actual, false) => F("Core.Req.NeedsSatisfaction", "needs satisfaction rank {0}, you are rank {1}", c.RequiredRank, actual),
        };
    }

    private static string Seasonal(SeasonalRequirement s)
    {
        if (!s.Active)
        {
            return T("Core.Req.SeasonalNotActive", "seasonal event not active");
        }

        if (s.ChapterNotOpen)
        {
            return F("Core.Req.ChapterNotOpen", "chapter opens at phase {0}, the event is at phase {1}", s.Begin, s.Phase);
        }

        if (s.ChapterOver)
        {
            return F("Core.Req.ChapterOver", "chapter ended after phase {0}, the event is at phase {1}", s.End, s.Phase);
        }

        return s.HasWindow && s.Phase is { } phase
            ? F("Core.Req.SeasonalActivePhase", "seasonal event active, phase {0}", phase)
            : T("Core.Req.SeasonalActive", "seasonal event active");
    }

    private static string QuestName(BlockerNames names, uint rowId) =>
        names.Catalog.GetByRowId(rowId)?.Name is { Length: > 0 } name ? name : F("Core.Req.QuestId", "quest {0}", rowId);

    private static string T(string key, string english) => CoreText.T(key, english);

    private static string F(string key, string english, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, CoreText.T(key, english), args);
}
