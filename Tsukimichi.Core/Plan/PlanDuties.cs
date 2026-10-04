using System.Collections.Frozen;

namespace Tsukimichi.Core.Plan;

/// <summary>One duty as the plan tags it: a ContentFinderCondition row with its kind and name.</summary>
/// <param name="ContentFinderConditionId">ContentFinderCondition row id, the id <c>RewardKind.DutyUnlock</c> entries carry.</param>
/// <param name="InstanceContentId">The InstanceContent row the duty links, the id <c>RewardKind.Instance</c> carries; 0 when none.</param>
/// <param name="Kind">
/// What the duty is, from its content type (the plugin reads it from the ContentFinderCondition sheet; see
/// <c>Tsukimichi.GameData.DutyIndex</c>): dungeon, trial, raid, alliance raid, field operation, society, a system's
/// content (deep dungeons, PvP, the Gold Saucer), or <see cref="UnlockKind.Other"/>.
/// </param>
/// <param name="Name">The duty's name as the sheet spells it ("the Tam-Tara Deepcroft").</param>
/// <param name="HighEnd">
/// High-end content: Extreme, Savage, Unreal, Ultimate and Chaotic (the sheet's <c>HighEndDuty</c>, the Ultimate and
/// Chaotic content types and the Duty Finder's high-end categories), which the P4 tiers keep apart.
/// </param>
public sealed record PlanDuty(uint ContentFinderConditionId, uint InstanceContentId, UnlockKind Kind, string Name, bool HighEnd = false);

/// <summary>
/// The duties the plan can name, by ContentFinderCondition id, by InstanceContent id and by name. Core cannot read the
/// sheets, so the plugin (and the tests) hand them in; <see cref="Empty"/> leaves every duty unlock tagged
/// <see cref="UnlockKind.Other"/> under the name the reward data gives. Immutable.
/// </summary>
public sealed class PlanDuties
{
    public static readonly PlanDuties Empty = new(
        FrozenDictionary<uint, PlanDuty>.Empty,
        FrozenDictionary<uint, PlanDuty>.Empty,
        FrozenDictionary<string, PlanDuty>.Empty);

    private readonly FrozenDictionary<uint, PlanDuty> byCondition;
    private readonly FrozenDictionary<uint, PlanDuty> byInstance;
    private readonly FrozenDictionary<string, PlanDuty> byName;

    private PlanDuties(FrozenDictionary<uint, PlanDuty> byCondition, FrozenDictionary<uint, PlanDuty> byInstance, FrozenDictionary<string, PlanDuty> byName)
    {
        this.byCondition = byCondition;
        this.byInstance = byInstance;
        this.byName = byName;
    }

    /// <summary>How many duties are known.</summary>
    public int Count => byCondition.Count;

    /// <summary>
    /// Builds the lookup; the first duty wins per id and per name. Only duty content (dungeon, trial, raid, alliance
    /// raid, field operation) is indexed by name, so a quest named like a Gold Saucer attraction is never matched.
    /// </summary>
    public static PlanDuties From(IEnumerable<PlanDuty> duties)
    {
        ArgumentNullException.ThrowIfNull(duties);
        var byCondition = new Dictionary<uint, PlanDuty>();
        var byInstance = new Dictionary<uint, PlanDuty>();
        var byName = new Dictionary<string, PlanDuty>(StringComparer.Ordinal);
        foreach (var duty in duties)
        {
            if (duty is null || duty.ContentFinderConditionId == 0)
            {
                continue;
            }

            byCondition.TryAdd(duty.ContentFinderConditionId, duty);
            if (duty.InstanceContentId != 0)
            {
                byInstance.TryAdd(duty.InstanceContentId, duty);
            }

            if (duty.Kind <= UnlockKind.FieldOperation && NameKey(duty.Name) is { Length: > 0 } key)
            {
                byName.TryAdd(key, duty);
            }
        }

        return byCondition.Count == 0
            ? Empty
            : new PlanDuties(byCondition.ToFrozenDictionary(), byInstance.ToFrozenDictionary(), byName.ToFrozenDictionary(StringComparer.Ordinal));
    }

    /// <summary>The duty behind a ContentFinderCondition row id.</summary>
    public bool TryGetCondition(uint contentFinderConditionId, out PlanDuty duty) => byCondition.TryGetValue(contentFinderConditionId, out duty!);

    /// <summary>The duty behind an InstanceContent row id.</summary>
    public bool TryGetInstance(uint instanceContentId, out PlanDuty duty) => byInstance.TryGetValue(instanceContentId, out duty!);

    /// <summary>The duty content (dungeon to field operation) named exactly <paramref name="name"/>, ignoring case and a leading "the".</summary>
    public bool TryGetByName(string name, out PlanDuty duty)
    {
        var key = NameKey(name);
        if (key.Length == 0)
        {
            duty = null!;
            return false;
        }

        return byName.TryGetValue(key, out duty!);
    }

    /// <summary>"the Labyrinth of the Ancients" and "Labyrinth of the Ancients" share the key "labyrinth of the ancients".</summary>
    internal static string NameKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var trimmed = name.Trim();
        if (trimmed.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[4..].TrimStart();
        }

        return trimmed.ToLowerInvariant();
    }
}
