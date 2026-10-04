using System.Runtime.CompilerServices;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Companions;

/// <summary>Which item level an <see cref="ItemLevelWall"/> compares with the duty's.</summary>
public enum ItemLevelBasis : byte
{
    /// <summary>The job the character is on: the game reads its equipped gear (Patch 7.x and before).</summary>
    CurrentJob,

    /// <summary>The character's best job: Evercold (Patch 8.0) applies the highest item level to every job.</summary>
    BestJob,
}

/// <summary>
/// Which item level the game holds a duty's requirement against. Up to Patch 7.x it is the equipped gear of the job the
/// player queues on; from Patch 8.0 "Evercold" a character's highest item level applies to all its jobs (the Armoury
/// change announced at Fan Fest 2026; <c>docs/research/plan-v7/feature-ideas.md</c>). Not hard-coded to a date: the
/// rule follows the game data the plugin reads, switching once the quest catalog holds Evercold's expansion
/// (<see cref="SharedFromExpansion"/>), so the same build is right on both sides of the patch.
/// </summary>
/// <param name="SharedAcrossJobs">Whether the best job's item level applies to every job.</param>
public sealed record ItemLevelRule(bool SharedAcrossJobs)
{
    /// <summary>The ExVersion row from which the item level is shared across jobs: 6, Evercold (8.0). Dawntrail is 5.</summary>
    public const byte SharedFromExpansion = 6;

    public static readonly ItemLevelRule PerJob = new(false);
    public static readonly ItemLevelRule Shared = new(true);

    private static readonly ConditionalWeakTable<QuestCatalog, ItemLevelRule> Cache = [];

    /// <summary>The rule for a game whose newest expansion is <paramref name="latestExpansion"/> (an ExVersion row id).</summary>
    public static ItemLevelRule ForExpansion(byte latestExpansion) => latestExpansion >= SharedFromExpansion ? Shared : PerJob;

    /// <summary>The rule for the game <paramref name="catalog"/> was read from: its newest quest's expansion decides. Cached per catalog.</summary>
    public static ItemLevelRule For(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Cache.GetValue(catalog, static c =>
        {
            byte latest = 0;
            foreach (var quest in c.All)
            {
                if (!quest.IsRemoved && quest.Expansion > latest)
                {
                    latest = quest.Expansion;
                }
            }

            return ForExpansion(latest);
        });
    }

    /// <summary>The basis this rule compares by.</summary>
    public ItemLevelBasis Basis => SharedAcrossJobs ? ItemLevelBasis.BestJob : ItemLevelBasis.CurrentJob;
}

/// <summary>
/// The item-level wall of one duty for one character (feature plan v7 C7): "needs i690 (you: i677)". Compares the
/// duty's <see cref="DutyRunInfo.ItemLevelRequired"/> with the character's item level by <see cref="ItemLevelRule"/>,
/// and names the best saved gearset when it would pass while the current job does not ("WAR gearset i692").
/// </summary>
/// <param name="Required">The duty's required average item level.</param>
/// <param name="Have">The item level the game holds it against (<paramref name="Basis"/>).</param>
/// <param name="Basis">Which item level <paramref name="Have"/> is.</param>
/// <param name="CurrentJob">The job the character was on at capture.</param>
/// <param name="CurrentItemLevel">That job's equipped average item level.</param>
/// <param name="BestJob">The job with the highest known item level; <paramref name="CurrentJob"/> when none is higher.</param>
/// <param name="BestItemLevel">That job's item level.</param>
public sealed record ItemLevelWall(
    ushort Required,
    ushort Have,
    ItemLevelBasis Basis,
    byte CurrentJob,
    ushort CurrentItemLevel,
    byte BestJob,
    ushort BestItemLevel)
{
    /// <summary>Whether the character clears the wall.</summary>
    public bool Met => Have >= Required;

    /// <summary>
    /// The wall stands on the current job, but another job's gearset passes it: before Patch 8.0, switching gearsets
    /// is the answer.
    /// </summary>
    public bool OtherGearsetPasses => !Met && Basis == ItemLevelBasis.CurrentJob && BestJob != CurrentJob && BestItemLevel >= Required;

    /// <summary>How many item levels are missing; 0 when met.</summary>
    public int Short => Met ? 0 : Required - Have;

    /// <summary>
    /// The other jobs whose item level clears the wall, highest first (the job id breaks a tie): the first is
    /// <paramref name="BestJob"/> when it qualifies, the rest "also qualify" (spec-1.19 C7: "Your SGE (i705) also
    /// qualifies"). Empty when none does.
    /// </summary>
    public IReadOnlyList<(byte Job, ushort ItemLevel)> Qualifying { get; init; } = [];

    /// <summary>
    /// The wall for a duty asking <paramref name="required"/>; null when the duty asks none or the capture holds no item
    /// level (a stored character from an older build, the hooks paused).
    /// </summary>
    public static ItemLevelWall? For(ushort required, CharacterSnapshot? snapshot, ItemLevelRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (required == 0 || snapshot is null)
        {
            return null;
        }

        var current = snapshot.ItemLevel;
        if (current == 0 && snapshot.JobItemLevels.Count == 0)
        {
            return null;
        }

        // The current job's own gear, else its best saved gearset.
        if (current == 0)
        {
            current = snapshot.JobItemLevels.GetValueOrDefault(snapshot.CurrentJob);
        }

        var bestJob = snapshot.CurrentJob;
        var best = current;
        // Strictly higher only, in job order: the current job keeps a tie, and two other jobs tie to the lower id.
        foreach (var (job, level) in snapshot.JobItemLevels.OrderBy(static kv => kv.Key))
        {
            if (level > best)
            {
                bestJob = job;
                best = level;
            }
        }

        var qualifying = snapshot.JobItemLevels
            .Where(kv => kv.Key != snapshot.CurrentJob && kv.Value >= required)
            .OrderByDescending(static kv => kv.Value)
            .ThenBy(static kv => kv.Key)
            .Select(static kv => (kv.Key, kv.Value))
            .ToArray();
        if (rule.SharedAcrossJobs)
        {
            return new ItemLevelWall(required, best, ItemLevelBasis.BestJob, snapshot.CurrentJob, current, bestJob, best) { Qualifying = qualifying };
        }

        return current == 0 ? null : new ItemLevelWall(required, current, ItemLevelBasis.CurrentJob, snapshot.CurrentJob, current, bestJob, best) { Qualifying = qualifying };
    }

    /// <inheritdoc cref="For(ushort, CharacterSnapshot?, ItemLevelRule)"/>
    public static ItemLevelWall? For(DutyRunInfo duty, CharacterSnapshot? snapshot, ItemLevelRule rule)
    {
        ArgumentNullException.ThrowIfNull(duty);
        return For(duty.ItemLevelRequired, snapshot, rule);
    }
}
