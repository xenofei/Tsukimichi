using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// Produces the requirement list for a quest in display and "next step" priority order (spec section 5).
/// Only gates the quest actually has are listed; entitlement caps appear only when exceeded.
/// </summary>
public static class RequirementEvaluator
{
    /// <summary>Evaluates on the character's current job.</summary>
    public static IReadOnlyList<RequirementResult> Evaluate(QuestRecord q, CharacterSnapshot s, QuestCatalog catalog, EvalContext ctx) =>
        EvaluateForJob(q, s, catalog, ctx, s.CurrentJob);

    /// <summary>Evaluates as if <paramref name="job"/> were active; class/job and level checks use that job.</summary>
    public static IReadOnlyList<RequirementResult> EvaluateForJob(QuestRecord q, CharacterSnapshot s, QuestCatalog catalog, EvalContext ctx, byte job)
    {
        ArgumentNullException.ThrowIfNull(q);
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(ctx);

        var results = new List<RequirementResult>(4);

        if (q.QuestLocks.Length > 0 && !IsSwitchableGrandCompanyQuest(q, s))
        {
            var completedLocks = q.QuestLocks.Where(id => s.IsCompleted(QuestRecord.ToQuestId(id))).ToArray();
            var detail = completedLocks.Length == 0
                ? "no conflicting quest completed"
                : "locked out by " + string.Join(", ", completedLocks.Select(id => NameOf(catalog, id)));
            results.Add(new(new ForeclosureRequirement(q.QuestLocks, completedLocks), completedLocks.Length == 0, detail));
        }

        if (s.MaxExpansion > 0 && q.Expansion > s.MaxExpansion)
        {
            results.Add(new(new ExpansionCapRequirement(q.Expansion, s.MaxExpansion), false, $"requires {ctx.ExpansionName(q.Expansion)}"));
        }

        if (s.LevelCap > 0 && q.Level > s.LevelCap)
        {
            results.Add(new(new LevelCapRequirement(q.Level, s.LevelCap), false, $"level {q.Level} is above your cap of {s.LevelCap}"));
        }

        if (q.ClassJobRequired != 0 || q.ClassJobCategory != 0)
        {
            var admits = AdmitsJob(q, ctx, job);
            var which = job == s.CurrentJob ? "the current job" : "this job";
            results.Add(new(new ClassJobRequirement(q.ClassJobCategory, q.ClassJobRequired, job), admits, admits ? $"available on {which}" : $"not available on {which}"));
        }

        if (q.Level > 0)
        {
            var actual = LevelOf(s, job);
            var met = actual >= q.Level;
            results.Add(new(new LevelRequirement(q.Level, actual), met, met ? $"level {q.Level}" : $"needs level {q.Level}, you are {actual}"));
        }

        if (!q.PreviousQuests.IsEmpty)
        {
            var ids = q.PreviousQuests.QuestIds;
            var join = q.PreviousQuests.Join;
            var doneIds = ids.Where(id => s.IsCompleted(QuestRecord.ToQuestId(id))).ToArray();
            var done = doneIds.Length;
            var met = join == JoinKind.Any ? done >= 1 : done == ids.Length;
            string detail;
            if (ids.Length == 1)
            {
                var name = NameOf(catalog, ids[0]);
                detail = met ? $"{name} done" : $"needs {name}";
            }
            else
            {
                detail = $"{done} of {ids.Length} prerequisites done";
                if (join == JoinKind.Any)
                {
                    detail += ", one needed";
                }
            }

            results.Add(new(new PreviousQuestsRequirement(ids, join, done, doneIds), met, detail));
        }

        if (q.GrandCompany != 0)
        {
            var met = s.GrandCompany == q.GrandCompany;
            var name = ctx.GrandCompanyName(q.GrandCompany);
            results.Add(new(new GrandCompanyRequirement(q.GrandCompany, s.GrandCompany), met, met ? name : $"requires {name}"));
        }

        if (q.GrandCompanyRank != 0)
        {
            var gc = q.GrandCompany != 0 ? q.GrandCompany : s.GrandCompany;
            var actual = gc < s.GcRanks.Length ? s.GcRanks[gc] : (byte)0;
            var met = actual >= q.GrandCompanyRank;
            var name = ctx.GrandCompanyName(gc);
            results.Add(new(
                new GrandCompanyRankRequirement(gc, q.GrandCompanyRank, actual),
                met,
                met ? $"{name} rank {actual}" : $"needs {name} rank {q.GrandCompanyRank}, you are rank {actual}"));
        }

        if (q.BeastTribe != 0)
        {
            var standing = s.Tribes.GetValueOrDefault(q.BeastTribe);

            if (q.BeastRank > 0)
            {
                var met = standing.Rank >= q.BeastRank;
                results.Add(new(
                    new TribeRankRequirement(q.BeastTribe, q.BeastRank, standing.Rank),
                    met,
                    met ? ctx.TribeRankName(standing.Rank) : $"needs {ctx.TribeRankName(q.BeastRank)}, you are {ctx.TribeRankName(standing.Rank)}"));
            }

            if (q.BeastValue > 0)
            {
                var met = standing.Value >= q.BeastValue;
                results.Add(new(
                    new TribeReputationRequirement(q.BeastTribe, q.BeastValue, standing.Value),
                    met,
                    met ? $"{standing.Value} reputation" : $"needs {q.BeastValue} reputation, you have {standing.Value}"));
            }

            if (q.IsRepeatable)
            {
                var hasAllowance = s.TribeAllowance > 0;
                results.Add(new(
                    new TribeAllowanceRequirement(s.TribeAllowance),
                    hasAllowance,
                    hasAllowance ? $"{s.TribeAllowance} allowances left" : "no allowances left today"));

                // An unknown offer (null) is not a blocker: nothing is listed, so the daily resolves on its other gates.
                if (ctx.TodaysDailyOffer is { } todaysOffer)
                {
                    var offered = todaysOffer.Contains(q.QuestId);
                    results.Add(new(new TribeDailyOfferRequirement(q.QuestId, offered), offered, offered ? "offered today" : "not offered today"));
                }
            }
        }

        if (q.InstanceContentRequired.Length > 0)
        {
            var ids = q.InstanceContentRequired;
            var done = ids.Count(s.UnlockedInstances.Contains);
            var met = q.InstanceJoin == JoinKind.Any ? done >= 1 : done == ids.Length;
            var detail = $"{done} of {ids.Length} duties completed";
            if (q.InstanceJoin == JoinKind.Any)
            {
                detail += ", one needed";
            }

            results.Add(new(new DutyCompletionRequirement(ids, q.InstanceJoin, done), met, detail));
        }

        if (q.Festival != 0)
        {
            var active = s.ActiveFestivals.Contains(q.Festival);
            results.Add(new(new SeasonalRequirement(q.Festival, active), active, active ? "seasonal event active" : "seasonal event not active"));
        }

        if (q.AcceptConditions.Length > 0)
        {
            var n = q.AcceptConditions.Length;
            results.Add(new(new AcceptConditionRequirement(q.AcceptConditions), true, n == 1 ? "1 accept condition not checked" : $"{n} accept conditions not checked"));
        }

        if (q.MountRequired)
        {
            results.Add(new(new MountRequirement(ctx.HasMount), ctx.HasMount != false, ctx.HasMount switch
            {
                true => "mount available",
                false => "requires a mount",
                null => "requires a mount, not checked",
            }));
        }

        if (q.HouseRequired)
        {
            results.Add(new(new HouseRequirement(ctx.HasHouse), ctx.HasHouse != false, ctx.HasHouse switch
            {
                true => "house available",
                false => "requires a house",
                null => "requires a house, not checked",
            }));
        }

        if (ctx.IsAchievementGated(q.RowId))
        {
            results.Add(new(
                new AchievementRequirement(q.RowId, s.AchievementsLoaded),
                s.AchievementsLoaded,
                s.AchievementsLoaded ? "achievement requirement, see journal" : "achievements not loaded"));
        }

        return results;
    }

    /// <summary>
    /// A Grand Company quest for a company the character is not in. Its locks (the other companies' quests) do not
    /// foreclose it because the character can switch companies; the Grand Company requirement carries the message.
    /// </summary>
    internal static bool IsSwitchableGrandCompanyQuest(QuestRecord q, CharacterSnapshot s) =>
        q.GrandCompany != 0 && s.GrandCompany != q.GrandCompany;

    /// <summary>Unsynced level of a job; zero when the snapshot has none for it.</summary>
    internal static byte LevelOf(CharacterSnapshot s, byte job) =>
        s.JobLevels.TryGetValue(job, out var level) ? (byte)Math.Clamp(level, (short)0, (short)byte.MaxValue) : (byte)0;

    /// <summary>
    /// Whether the quest can be taken on <paramref name="job"/>: the pinned job when it has one, else the category
    /// through the context lookup (no lookup admits everything), else any job.
    /// </summary>
    internal static bool AdmitsJob(QuestRecord q, EvalContext ctx, byte job)
    {
        if (q.ClassJobRequired != 0)
        {
            return job == q.ClassJobRequired;
        }

        return q.ClassJobCategory == 0 || (ctx.ClassJobs?.Admits(q.ClassJobCategory, job) ?? true);
    }

    private static string NameOf(QuestCatalog catalog, uint rowId) =>
        catalog.GetByRowId(rowId)?.Name is { Length: > 0 } name ? name : $"quest {rowId}";
}
