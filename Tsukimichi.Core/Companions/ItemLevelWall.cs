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
    /// level (a stored character from an older build, the hooks paused). Only jobs that enter duties count
    /// (<paramref name="queues"/>, <see cref="DutyJobs.ByRowId"/> when null): a crafter's, gatherer's or limited job's
    /// gearset never passes the wall, names a gearset or stands in for the shared item level. On such a job the wall
    /// is judged as if on the best geared combat job, which is what the player would switch to.
    /// </summary>
    public static ItemLevelWall? For(ushort required, CharacterSnapshot? snapshot, ItemLevelRule rule, Func<byte, bool>? queues = null)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (required == 0 || snapshot is null)
        {
            return null;
        }

        queues ??= DutyJobs.ByRowId;
        var combat = snapshot.JobItemLevels.Where(kv => queues(kv.Key)).OrderBy(static kv => kv.Key).ToArray();
        var currentJob = snapshot.CurrentJob;
        ushort current;
        if (queues(currentJob))
        {
            // The current job's own gear, else its best saved gearset.
            current = snapshot.ItemLevel != 0 ? snapshot.ItemLevel : snapshot.JobItemLevels.GetValueOrDefault(currentJob);
        }
        else
        {
            // A crafter or gatherer (or a limited job) never queues: the best geared combat job stands in for it.
            current = 0;
            foreach (var (job, level) in combat)
            {
                if (level > current)
                {
                    currentJob = job;
                    current = level;
                }
            }
        }

        if (current == 0 && combat.Length == 0)
        {
            return null;
        }

        var bestJob = currentJob;
        var best = current;
        // Strictly higher only, in job order: the current job keeps a tie, and two other jobs tie to the lower id.
        foreach (var (job, level) in combat)
        {
            if (level > best)
            {
                bestJob = job;
                best = level;
            }
        }

        var qualifying = combat
            .Where(kv => kv.Key != currentJob && kv.Value >= required)
            .OrderByDescending(static kv => kv.Value)
            .ThenBy(static kv => kv.Key)
            .Select(static kv => (kv.Key, kv.Value))
            .ToArray();
        if (rule.SharedAcrossJobs)
        {
            return new ItemLevelWall(required, best, ItemLevelBasis.BestJob, currentJob, current, bestJob, best) { Qualifying = qualifying };
        }

        return current == 0 ? null : new ItemLevelWall(required, current, ItemLevelBasis.CurrentJob, currentJob, current, bestJob, best) { Qualifying = qualifying };
    }

    /// <inheritdoc cref="For(ushort, CharacterSnapshot?, ItemLevelRule, Func{byte, bool}?)"/>
    public static ItemLevelWall? For(DutyRunInfo duty, CharacterSnapshot? snapshot, ItemLevelRule rule, Func<byte, bool>? queues = null)
    {
        ArgumentNullException.ThrowIfNull(duty);
        return For(duty.ItemLevelRequired, snapshot, rule, queues);
    }
}
