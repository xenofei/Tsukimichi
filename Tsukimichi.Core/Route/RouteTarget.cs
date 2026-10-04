using System.Globalization;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;

namespace Tsukimichi.Core.Route;

/// <summary>What an unlock route leads to; the route itself is always to a quest (<see cref="RouteTarget.QuestRowIds"/>).</summary>
public enum RouteTargetKind : byte
{
    /// <summary>A quest picked directly (the detail pane's "Route to this").</summary>
    Quest,

    /// <summary>A job, through its unlock quest (<see cref="LadderJob.UnlockQuestRowId"/>).</summary>
    Job,

    /// <summary>A duty: the quest whose reward opens it (an Instance or DutyUnlock reward, sheet or curated).</summary>
    Duty,

    /// <summary>A system (retainers, the Gold Saucer, flying), from curated <c>system_unlocks.json</c>.</summary>
    System,

    /// <summary>A Moonlit reward: the quest that grants it.</summary>
    Reward,

    /// <summary>"Everything for a job" (1.6.0): its unlock quest, its job quests and its role's quests, up to the level cap.</summary>
    JobQuests,

    /// <summary>"All my pins" (1.6.0): every pinned quest still to do.</summary>
    Pins,

    /// <summary>"This expansion's blues" (1.6.0): the unlock quests of one expansion left in My blues.</summary>
    Blues,

    /// <summary>
    /// Anything a quest opens (plan v7, 1.19.0 K3, <see cref="Unlocks.UnlockFind"/>): an area, an aetheryte, a duty, a
    /// feature, a job, a mount or an emote through any one of its quests, or flying in a zone through every quest current.
    /// </summary>
    Unlock,
}

/// <summary>
/// The thing a player wants and the quest or quests that give it (feature plan v3 P6). Several quests stand for one
/// target where the game has variants (the three Grand Company enrollments, the starting-city envoys that each hand
/// out the Wind-up Airship, two row ids of one reworked quest); <see cref="UnlockRoute.Build"/> takes the variant
/// with the fewest quests left and lists the others. Empty when nothing is known to unlock the target.
/// </summary>
/// <param name="Kind">What the target is.</param>
/// <param name="Label">The target's name as the route card titles it ("Blue Mage", "Retainers"); for a quest target the caller passes the name through the spoiler shield.</param>
/// <param name="QuestRowIds">Quest sheet row ids any one of which unlocks the target, distinct, in the order given.</param>
public sealed record RouteTarget(RouteTargetKind Kind, string Label, IReadOnlyList<uint> QuestRowIds)
{
    /// <summary>
    /// A route to several things at once (1.6.0, "Everything for a job", "All my pins", "This expansion's blues"): each
    /// part is a target of its own, the route runs through the union of their closures in one order
    /// (<see cref="UnlockRoute.Build"/>), and every part is a milestone. Empty for a single target, whose variants
    /// <see cref="QuestRowIds"/> lists. Parts sharing a <see cref="Label"/> make one milestone, reached with the last
    /// of them; a part without a label is a milestone of its own, its quest marked as a target.
    /// </summary>
    public IReadOnlyList<RouteTarget> Parts { get; init; } = [];

    /// <summary>
    /// The target's own game icon for the Route window's header (UI-5e, I19): a duty's tile, a reward's icon, a job's,
    /// an expansion's ring; 0 when the caller did not know it (a quest target then wears its map marker,
    /// <see cref="Ui.ActionIcons.RouteHeader"/>). Only drawn, never compared by the route itself.
    /// </summary>
    public uint Icon { get; init; }

    /// <summary>
    /// For a route to flying in a zone (<see cref="ForUnlock"/>): the zone's territory, whose field currents the Route
    /// window adds after the quests (plan v7, 1.19.0 K3); 0 for any other target.
    /// </summary>
    public uint FlyingTerritory { get; init; }

    /// <summary>True for a route to several targets (<see cref="Parts"/>).</summary>
    public bool IsUnion => Parts.Count > 0;

    /// <summary>
    /// A target made of <paramref name="parts"/> (see <see cref="Parts"/>); parts with no quest are dropped, and
    /// <see cref="QuestRowIds"/> lists every part's quests once, in order.
    /// </summary>
    public static RouteTarget Union(RouteTargetKind kind, string label, IEnumerable<RouteTarget> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var kept = new List<RouteTarget>();
        var quests = new List<uint>();
        var seen = new HashSet<uint>();
        foreach (var part in parts)
        {
            if (part is null || part.QuestRowIds.Count == 0)
            {
                continue;
            }

            kept.Add(part);
            foreach (var id in part.QuestRowIds)
            {
                if (seen.Add(id))
                {
                    quests.Add(id);
                }
            }
        }

        return new RouteTarget(kind, label ?? string.Empty, quests) { Parts = kept };
    }

    /// <summary>
    /// "Everything for a job": the job's unlock quest (milestone "Dragoon unlocked"), every quest of its ladder
    /// (<see cref="JobLadder.ForJob"/>, its base class's included; milestone "Dragoon quests") and every quest of its
    /// role's ladder (<see cref="JobLadder.RoleLadder"/>; milestone "Role quests"), each up to
    /// <paramref name="levelCap"/> (0 for no cap). A job with no ladder and no unlock quest gives an empty target.
    /// </summary>
    /// <param name="jobName">The job's name as the route is titled ("Dragoon").</param>
    /// <param name="unlockQuestRowId">The job's unlock quest (<see cref="LadderJob.UnlockQuestRowId"/>); 0 when it has none.</param>
    public static RouteTarget ForJobQuests(JobLadder ladder, QuestCatalog catalog, uint jobId, string jobName, uint unlockQuestRowId, byte levelCap = 0)
    {
        ArgumentNullException.ThrowIfNull(ladder);
        ArgumentNullException.ThrowIfNull(catalog);
        var name = jobName ?? string.Empty;
        var parts = new List<RouteTarget>();
        if (unlockQuestRowId != 0)
        {
            parts.Add(new RouteTarget(RouteTargetKind.Job, F("Core.Route.JobUnlocked", "{0} unlocked", name), [unlockQuestRowId]));
        }

        bool Within(uint rowId) =>
            rowId != unlockQuestRowId && catalog.TryGetByRowId(rowId, out var quest) && (levelCap == 0 || quest.Level <= levelCap);

        if (ladder.ForJob(jobId) is { } entry)
        {
            var label = F("Core.Route.JobQuestsPart", "{0} quests", name);
            foreach (var rowId in entry.QuestRowIds)
            {
                if (Within(rowId))
                {
                    parts.Add(new RouteTarget(RouteTargetKind.Quest, label, [rowId]));
                }
            }
        }

        if (ladder.RoleOf(jobId) is { } role)
        {
            var label = CoreText.T("Core.Route.RoleQuestsPart", "Role quests");
            foreach (var rowId in ladder.RoleLadder(role))
            {
                if (Within(rowId))
                {
                    parts.Add(new RouteTarget(RouteTargetKind.Quest, label, [rowId]));
                }
            }
        }

        return Union(RouteTargetKind.JobQuests, F("Core.Route.JobQuestsLabel", "everything for {0}", name), parts) with { Icon = Ui.ActionIcons.Job(jobId) };
    }

    /// <summary>"All my pins": every pinned quest, in the order pinned, each its own milestone.</summary>
    public static RouteTarget ForPins(IEnumerable<uint> pinned)
    {
        ArgumentNullException.ThrowIfNull(pinned);
        var parts = new List<RouteTarget>();
        foreach (var rowId in pinned)
        {
            parts.Add(new RouteTarget(RouteTargetKind.Quest, string.Empty, [rowId]));
        }

        return Union(RouteTargetKind.Pins, CoreText.T("Core.Route.PinsLabel", "all your pins"), parts);
    }

    /// <summary>"This expansion's blues": every unlock quest of the My blues block, each its own milestone.</summary>
    public static RouteTarget ForBlues(PlanExpansion block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var parts = new List<RouteTarget>();
        foreach (var entry in block.Entries)
        {
            parts.Add(new RouteTarget(RouteTargetKind.Quest, string.Empty, [entry.Quest.RowId]));
        }

        return Union(RouteTargetKind.Blues, F("Core.Route.BluesLabel", "{0} blues", block.Name), parts) with { Icon = Ui.PaneIcons.Expansion(block.Expansion) };
    }

    /// <summary>
    /// "Route to this" for an unlock quest (My blues): the duty it opens when its rewards or <paramref name="entries"/>
    /// name one (<see cref="ForDuty"/>, so every quest opening that duty is a variant), else the curated system it
    /// opens (<see cref="ForSystem"/>), else the quest itself. A duty or system target that leaves the quest out
    /// falls back to the quest.
    /// </summary>
    /// <param name="label">The route's title: the duty or system's name when known, else the quest's.</param>
    public static RouteTarget ForUnlockQuest(QuestRecord quest, QuestCatalog catalog, IEnumerable<UniqueRewardEntry>? entries, string label)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(catalog);
        IReadOnlyList<UniqueRewardEntry> list = entries as IReadOnlyList<UniqueRewardEntry> ?? entries?.ToList() ?? [];
        foreach (var reward in quest.Rewards)
        {
            if (reward.Kind is RewardKind.DutyUnlock or RewardKind.Instance && reward.Id != 0
                && ForDuty(catalog, reward.Kind, reward.Id, label, list) is { } duty && duty.QuestRowIds.Contains(quest.RowId))
            {
                return duty;
            }
        }

        foreach (var entry in list)
        {
            if (entry.QuestRowId != quest.RowId)
            {
                continue;
            }

            if (entry.Kind is RewardKind.DutyUnlock or RewardKind.Instance && entry.RewardId != 0
                && ForDuty(catalog, entry.Kind, entry.RewardId, label, list) is { } duty && duty.QuestRowIds.Contains(quest.RowId))
            {
                return duty;
            }

            if (entry.Kind == RewardKind.SystemUnlock && entry.RewardName.Length > 0
                && ForSystem(list, entry.RewardName) is { } system && system.QuestRowIds.Contains(quest.RowId))
            {
                return system with { Label = label ?? string.Empty };
            }
        }

        return ForQuest(quest.RowId, label ?? string.Empty);
    }

    /// <summary>
    /// Route to unlock (plan v7, 1.19.0 K3): to <paramref name="find"/>, titled with its label ("Flying in Thavnair",
    /// "Kugane") and wearing its icon. Flying needs every quest current of the zone, so each is a part of its own (all on
    /// one route); anything else is reached through whichever of its quests has the fewest left, the others listed.
    /// </summary>
    /// <param name="flyingTerritory">For flying, the zone's territory (its field currents follow the quests); 0 otherwise.</param>
    public static RouteTarget ForUnlock(Unlocks.UnlockFind find, uint flyingTerritory = 0)
    {
        ArgumentNullException.ThrowIfNull(find);
        if (!find.NeedsAll)
        {
            return new RouteTarget(RouteTargetKind.Unlock, find.Label, find.Quests) { Icon = find.Icon };
        }

        var parts = new List<RouteTarget>(find.Quests.Count);
        foreach (var rowId in find.Quests)
        {
            parts.Add(new RouteTarget(RouteTargetKind.Quest, string.Empty, [rowId]));
        }

        return Union(RouteTargetKind.Unlock, find.Label, parts) with { Icon = find.Icon, FlyingTerritory = flyingTerritory };
    }

    private static string F(string key, string english, string value) =>
        string.Format(CultureInfo.CurrentCulture, CoreText.T(key, english), value);

    /// <summary>A quest picked directly.</summary>
    public static RouteTarget ForQuest(uint rowId, string label) => new(RouteTargetKind.Quest, label ?? string.Empty, [rowId]);

    /// <summary>A job through its unlock quest; a base class (no unlock quest) has nothing to route to.</summary>
    public static RouteTarget ForJob(LadderJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return ForJob(job.Name, job.UnlockQuestRowId);
    }

    /// <summary>A job by name and unlock quest row id (0 when the job has none, which gives an empty target).</summary>
    public static RouteTarget ForJob(string name, uint unlockQuestRowId) =>
        new(RouteTargetKind.Job, name ?? string.Empty, unlockQuestRowId == 0 ? [] : [unlockQuestRowId]);

    /// <summary>
    /// A Moonlit entry: the quest granting it, plus every other entry of <paramref name="all"/> handing out the same
    /// reward (same kind, reward id and item; for a system unlock, which has no id, the same curated label). A reward
    /// without an id of any other kind (a quest you marked unique yourself) stands alone. A duty or system unlock entry
    /// makes a <see cref="RouteTargetKind.Duty"/> or <see cref="RouteTargetKind.System"/> target.
    /// </summary>
    /// <param name="label">The name the route is titled with; null uses <see cref="UniqueRewardEntry.RewardName"/>.</param>
    public static RouteTarget ForReward(UniqueRewardEntry entry, IEnumerable<UniqueRewardEntry>? all = null, string? label = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var quests = new List<uint> { entry.QuestRowId };
        if (all is not null)
        {
            foreach (var other in all)
            {
                if (other.Kind == entry.Kind && SameReward(entry, other) && !quests.Contains(other.QuestRowId))
                {
                    quests.Add(other.QuestRowId);
                }
            }
        }

        var kind = entry.Kind switch
        {
            RewardKind.DutyUnlock or RewardKind.Instance => RouteTargetKind.Duty,
            RewardKind.SystemUnlock => RouteTargetKind.System,
            _ => RouteTargetKind.Reward,
        };
        return new RouteTarget(kind, label ?? entry.RewardName, quests);
    }

    /// <summary>
    /// A duty by the id its reward carries: the quests whose sheet rewards list it (<paramref name="kind"/>
    /// <see cref="RewardKind.Instance"/> with an InstanceContent id, or <see cref="RewardKind.DutyUnlock"/> with a
    /// ContentFinderCondition id), then the entries of <paramref name="entries"/> (the curated duty unlocks) naming it.
    /// </summary>
    public static RouteTarget ForDuty(QuestCatalog catalog, RewardKind kind, uint dutyId, string label, IEnumerable<UniqueRewardEntry>? entries = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var quests = new List<uint>();
        foreach (var quest in catalog.All)
        {
            foreach (var reward in quest.Rewards)
            {
                if (reward.Kind == kind && reward.Id == dutyId && !quests.Contains(quest.RowId))
                {
                    quests.Add(quest.RowId);
                }
            }
        }

        if (entries is not null)
        {
            foreach (var entry in entries)
            {
                if (entry.Kind == kind && entry.RewardId == dutyId && !quests.Contains(entry.QuestRowId))
                {
                    quests.Add(entry.QuestRowId);
                }
            }
        }

        return new RouteTarget(RouteTargetKind.Duty, label ?? string.Empty, quests);
    }

    /// <summary>A system by its curated label ("Retainers"): every system-unlock entry of <paramref name="entries"/> with that label.</summary>
    public static RouteTarget ForSystem(IEnumerable<UniqueRewardEntry> entries, string label)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var quests = new List<uint>();
        foreach (var entry in entries)
        {
            if (entry.Kind == RewardKind.SystemUnlock && string.Equals(entry.RewardName, label, StringComparison.Ordinal) && !quests.Contains(entry.QuestRowId))
            {
                quests.Add(entry.QuestRowId);
            }
        }

        return new RouteTarget(RouteTargetKind.System, label ?? string.Empty, quests);
    }

    private static bool SameReward(UniqueRewardEntry a, UniqueRewardEntry b)
    {
        if (a.RewardId != 0 || b.RewardId != 0)
        {
            return a.RewardId == b.RewardId && a.ItemId == b.ItemId;
        }

        return a.Kind == RewardKind.SystemUnlock && string.Equals(a.RewardName, b.RewardName, StringComparison.Ordinal);
    }
}
