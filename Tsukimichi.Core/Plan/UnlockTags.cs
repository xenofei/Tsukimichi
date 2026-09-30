using System.Collections.Frozen;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Core.Plan;

/// <summary>One thing a plan quest unlocks: its kind and, when known, its name ("Dungeon: The Tam-Tara Deepcroft").</summary>
/// <param name="Name">What is unlocked ("The Tam-Tara Deepcroft", "Retainers", "Paladin"); empty when only the kind is known.</param>
/// <param name="Inherited">
/// The quest's own data names no unlock and the kind was taken from its journal genre, every unlock of which is of this
/// one kind (the steps of a raid series or a job's quest line): the quest leads to such content rather than opening it.
/// </param>
public sealed record PlanUnlock(UnlockKind Kind, string Name, bool Inherited = false)
{
    /// <summary>"Dungeon: The Tam-Tara Deepcroft", or the kind alone when no name is known or the kind is inherited.</summary>
    public string Label => Inherited || Name.Length == 0 ? UnlockKinds.Name(Kind) : UnlockKinds.Name(Kind) + ": " + Name;
}

/// <summary>
/// The quests the "Clear my blues" plan (P3) lists and what each one unlocks, derived once per catalog.
/// <para>
/// A plan quest is a feature ("blue") quest (<c>FeaturePresets.IsFeatureQuest</c>, handed in as the set of row ids)
/// that is not removed from the game and belongs to no seasonal event (an event quest cannot be cleared on demand; the
/// seasonal views carry those). Its kinds come from, in this order of trust:
/// </para>
/// <list type="number">
/// <item>the unique-reward data and the quest's own rewards: a duty unlock (<see cref="RewardKind.DutyUnlock"/>, a
/// ContentFinderCondition, including the curated <c>duty_unlocks.json</c>) or an instance
/// (<see cref="RewardKind.Instance"/>) takes the duty's kind from <see cref="PlanDuties"/> (dungeon, trial, raid,
/// alliance raid, field operation, society or a system's content, Other when the duty is unknown); a class or job
/// (<see cref="RewardKind.ClassJob"/>), an action, a trait or a blue magic spell is Job; an aether current is Flying;
/// a curated system unlock (<c>system_unlocks.json</c>) is System, except the flying and field-operation entries named
/// in <see cref="FlyingLabels"/> and <see cref="FieldOperationLabels"/>; a general action (Desynthesis, Dye, Decipher)
/// is System; a named <c>Quest.OtherReward</c> is Other;</item>
/// <item>an allied society quest (<see cref="QuestRecord.BeastTribe"/> set) is Society;</item>
/// <item>a quest with no duty yet whose name is a duty's name ("Labyrinth of the Ancients") unlocks that duty;</item>
/// <item>a quest with nothing but Other so far takes its journal genre's kind when every direct unlock in the genre is
/// of that one kind and the kind is inheritable (<see cref="IsInheritable"/>: duty content, Job, Society): the Primal
/// Quests are all trials, a job's quests all Job; marked <see cref="PlanUnlock.Inherited"/>;</item>
/// <item>otherwise it is Other.</item>
/// </list>
/// Other is dropped from a quest that has any other kind. A quest's unlocks are ordered by kind precedence
/// (<see cref="UnlockKind"/>), duplicates removed, so the first is its primary kind. Immutable.
/// </summary>
public sealed class UnlockTags
{
    /// <summary>Curated system-unlock labels that open flying rather than a system.</summary>
    public static readonly FrozenSet<string> FlyingLabels = new[] { "Flying (Heavensward entry)" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Curated system-unlock labels that open a field operation (Eureka, Bozja, the Occult Crescent).</summary>
    public static readonly FrozenSet<string> FieldOperationLabels =
        new[] { "Eureka (Forbidden Land)", "Bozjan Southern Front", "Occult Crescent" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static readonly UnlockTags Empty = new([], FrozenDictionary<uint, IReadOnlyList<PlanUnlock>>.Empty);

    private static readonly PlanUnlock[] NoUnlocks = [];
    private static readonly PlanUnlock[] Unknown = [new PlanUnlock(UnlockKind.Other, string.Empty)];

    private readonly FrozenDictionary<uint, IReadOnlyList<PlanUnlock>> byQuest;

    private UnlockTags(QuestRecord[] quests, FrozenDictionary<uint, IReadOnlyList<PlanUnlock>> byQuest)
    {
        Quests = quests;
        this.byQuest = byQuest;
    }

    /// <summary>Every plan quest, in catalog order, whatever a character's state.</summary>
    public IReadOnlyList<QuestRecord> Quests { get; }

    /// <summary>How many quests the plan can list.</summary>
    public int Count => Quests.Count;

    /// <summary>Whether the quest is a plan quest.</summary>
    public bool Contains(uint rowId) => byQuest.ContainsKey(rowId);

    /// <summary>What the quest unlocks, primary kind first; empty for a quest the plan does not list.</summary>
    public IReadOnlyList<PlanUnlock> For(uint rowId) => byQuest.GetValueOrDefault(rowId) ?? NoUnlocks;

    /// <summary>Whether a feature quest belongs in the plan: not removed and of no seasonal event.</summary>
    public static bool IsPlanQuest(QuestRecord quest, IReadOnlySet<uint> featureQuestIds)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(featureQuestIds);
        return featureQuestIds.Contains(quest.RowId) && !quest.IsRemoved && quest.Festival == 0 && !quest.IsRepeatable;
    }

    /// <param name="catalog">The quest catalog.</param>
    /// <param name="featureQuestIds">Row ids of the feature quests (<c>FeaturePresets.Derive</c>).</param>
    /// <param name="rewards">
    /// The unique-reward catalog with the curated unlocks merged in (<see cref="UniqueRewardCatalog.Build"/>); build it
    /// without the user's overrides, since whether a reward is unique does not change what the quest unlocks.
    /// </param>
    /// <param name="duties">Duty kinds and names; <see cref="PlanDuties.Empty"/> tags every duty Other.</param>
    /// <param name="tribeName">Allied society name by BeastTribe row id; null leaves the society unnamed.</param>
    public static UnlockTags Build(QuestCatalog catalog, IReadOnlySet<uint> featureQuestIds, UniqueRewardCatalog rewards, PlanDuties duties, Func<byte, string>? tribeName = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(featureQuestIds);
        ArgumentNullException.ThrowIfNull(rewards);
        ArgumentNullException.ThrowIfNull(duties);

        var quests = new List<QuestRecord>();
        var direct = new Dictionary<uint, List<PlanUnlock>>();
        var genreKinds = new Dictionary<uint, HashSet<UnlockKind>>();
        foreach (var quest in catalog.All)
        {
            if (!IsPlanQuest(quest, featureQuestIds))
            {
                continue;
            }

            quests.Add(quest);
            var unlocks = Direct(quest, rewards, duties, tribeName);
            direct[quest.RowId] = unlocks;
            foreach (var unlock in unlocks)
            {
                if (unlock.Kind == UnlockKind.Other)
                {
                    continue;
                }

                if (!genreKinds.TryGetValue(quest.Journal.GenreId, out var kinds))
                {
                    kinds = [];
                    genreKinds[quest.Journal.GenreId] = kinds;
                }

                kinds.Add(unlock.Kind);
            }
        }

        var byQuest = new Dictionary<uint, IReadOnlyList<PlanUnlock>>(quests.Count);
        foreach (var quest in quests)
        {
            var unlocks = direct[quest.RowId];
            var specific = unlocks.Exists(static u => u.Kind != UnlockKind.Other);
            if (!specific
                && genreKinds.TryGetValue(quest.Journal.GenreId, out var kinds)
                && kinds.Count == 1
                && IsInheritable(kinds.First()))
            {
                unlocks.Insert(0, new PlanUnlock(kinds.First(), string.Empty, Inherited: true));
                specific = true;
            }

            if (specific)
            {
                unlocks.RemoveAll(static u => u.Kind == UnlockKind.Other);
            }

            byQuest[quest.RowId] = unlocks.Count == 0 ? Unknown : Ordered(unlocks);
        }

        return quests.Count == 0 ? Empty : new UnlockTags(quests.ToArray(), byQuest.ToFrozenDictionary());
    }

    /// <summary>The quest's own unlocks (rules 1 to 3 of the class summary), unordered, duplicates removed.</summary>
    private static List<PlanUnlock> Direct(QuestRecord quest, UniqueRewardCatalog rewards, PlanDuties duties, Func<byte, string>? tribeName)
    {
        var unlocks = new List<PlanUnlock>();
        foreach (var entry in rewards.ForQuest(quest.RowId))
        {
            if (string.Equals(entry.Source, UniqueRewardCatalog.UserSource, StringComparison.Ordinal))
            {
                continue;
            }

            AddReward(unlocks, entry.Kind, entry.RewardId, entry.RewardName, duties);
        }

        foreach (var reward in quest.Rewards)
        {
            // Currency rewards (an item id) and unresolved slots (no name) share Other with Quest.OtherReward.
            if (reward.Kind == RewardKind.Other && (reward.ItemId != 0 || reward.Name.Length == 0))
            {
                continue;
            }

            AddReward(unlocks, reward.Kind, reward.Id, reward.Name, duties);
        }

        if (quest.BeastTribe != 0)
        {
            Add(unlocks, new PlanUnlock(UnlockKind.Society, tribeName?.Invoke(quest.BeastTribe) ?? string.Empty));
        }

        if (!unlocks.Exists(static u => u.Kind <= UnlockKind.FieldOperation) && duties.TryGetByName(quest.Name, out var named))
        {
            Add(unlocks, DutyUnlock(named));
        }

        return unlocks;
    }

    private static void AddReward(List<PlanUnlock> unlocks, RewardKind kind, uint id, string name, PlanDuties duties)
    {
        switch (kind)
        {
            case RewardKind.DutyUnlock:
                Add(unlocks, duties.TryGetCondition(id, out var duty) ? DutyUnlock(duty) : new PlanUnlock(UnlockKind.Other, Capitalize(name)));
                break;

            case RewardKind.Instance:
                if (duties.TryGetInstance(id, out var instance) || duties.TryGetByName(name, out instance))
                {
                    Add(unlocks, DutyUnlock(instance));
                }
                else if (name.Length > 0)
                {
                    Add(unlocks, new PlanUnlock(UnlockKind.Other, Capitalize(name)));
                }

                break;

            case RewardKind.ClassJob:
                // The job itself is named before the actions its quest line teaches.
                Add(unlocks, new PlanUnlock(UnlockKind.Job, NameCase.Title(name)), first: true);
                break;

            case RewardKind.Action:
            case RewardKind.Trait:
            case RewardKind.BlueMageSpell:
                Add(unlocks, new PlanUnlock(UnlockKind.Job, name));
                break;

            case RewardKind.AetherCurrent:
                Add(unlocks, new PlanUnlock(UnlockKind.Flying, CurrentZone(name)));
                break;

            case RewardKind.SystemUnlock:
                var systemKind = FlyingLabels.Contains(name) ? UnlockKind.Flying
                    : FieldOperationLabels.Contains(name) ? UnlockKind.FieldOperation
                    : UnlockKind.System;
                Add(unlocks, new PlanUnlock(systemKind, name));
                break;

            case RewardKind.GeneralAction:
                Add(unlocks, new PlanUnlock(UnlockKind.System, name));
                break;

            case RewardKind.Other when name.Length > 0:
                Add(unlocks, new PlanUnlock(UnlockKind.Other, name));
                break;
        }
    }

    private static PlanUnlock DutyUnlock(PlanDuty duty) => new(duty.Kind, Capitalize(duty.Name));

    private static void Add(List<PlanUnlock> unlocks, PlanUnlock unlock, bool first = false)
    {
        foreach (var existing in unlocks)
        {
            if (existing.Kind == unlock.Kind && string.Equals(existing.Name, unlock.Name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        if (first)
        {
            unlocks.Insert(0, unlock);
        }
        else
        {
            unlocks.Add(unlock);
        }
    }

    /// <summary>
    /// Kinds a genre passes on to its quests without an unlock of their own: duty content, Job and Society, whose genres
    /// are one series or quest line. Flying and System are not: a zone's sidequest genre holding its aether currents
    /// or one system unlock says nothing about the zone's other blue quests.
    /// </summary>
    public static bool IsInheritable(UnlockKind kind) => kind is <= UnlockKind.FieldOperation or UnlockKind.Job or UnlockKind.Society;

    /// <summary>By kind precedence, keeping the data's order within a kind; a named unlock before the kind alone.</summary>
    private static PlanUnlock[] Ordered(List<PlanUnlock> unlocks)
    {
        var result = new PlanUnlock[unlocks.Count];
        var n = 0;
        foreach (var kind in UnlockKinds.All)
        {
            foreach (var unlock in unlocks)
            {
                if (unlock.Kind == kind && unlock.Name.Length > 0)
                {
                    result[n++] = unlock;
                }
            }

            foreach (var unlock in unlocks)
            {
                if (unlock.Kind == kind && unlock.Name.Length == 0 && !HasNamed(unlocks, kind))
                {
                    result[n++] = unlock;
                }
            }
        }

        return n == result.Length ? result : result[..n];
    }

    private static bool HasNamed(List<PlanUnlock> unlocks, UnlockKind kind) => unlocks.Exists(u => u.Kind == kind && u.Name.Length > 0);

    /// <summary>"the Tam-Tara Deepcroft" reads "The Tam-Tara Deepcroft" at the head of a label.</summary>
    internal static string Capitalize(string name) =>
        name.Length > 0 && char.IsLower(name[0]) ? char.ToUpperInvariant(name[0]) + name[1..] : name;

    /// <summary>"Aether Current (Coerthas Western Highlands)" names the zone, which is what flying is unlocked in.</summary>
    private static string CurrentZone(string name)
    {
        var open = name.IndexOf('(', StringComparison.Ordinal);
        var close = name.LastIndexOf(')');
        return open >= 0 && close > open + 1 ? name[(open + 1)..close].Trim() : name;
    }
}
