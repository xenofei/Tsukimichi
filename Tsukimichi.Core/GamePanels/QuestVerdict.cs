using System.Globalization;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;

namespace Tsukimichi.Core.GamePanels;

/// <summary>Where a quest stands in its chain's play order.</summary>
/// <param name="Position">1-based place among the chain's counted steps.</param>
/// <param name="Total">How many counted steps the chain has.</param>
/// <param name="NextRowId">The counted step after this one in play order; null for the last.</param>
public readonly record struct ChainStep(int Position, int Total, uint? NextRowId);

/// <summary>A Moonlit reward of the quest a panel shows, and whether the character has it (null: cannot tell).</summary>
public readonly record struct MoonlitReward(string Name, bool? Owned);

/// <summary>
/// The one-line answer the "Worth it?" and Journal panels open with (1.7.0), and the pieces it is made from. What a
/// player weighs when a quest is offered, in this order: what it opens (a duty, a job, a system: "Unlocks Aglaia"),
/// a Moonlit reward they lack ("Moonlit: Wind-up Sun"), the same reward when they already have it, its place in a
/// chain ("Part of Hildibrand · 4 more after this"), then whether it is a repeatable or a seasonal quest; and when none applies,
/// that it carries nothing unique. A quest the spoiler shield masks says nothing beyond its placeholder. Pure; the
/// phrases come through <see cref="CoreText"/>.
/// </summary>
public static class QuestVerdict
{
    /// <summary>The verdict for a quest masked by the spoiler shield: nothing past the placeholder.</summary>
    public const string Masked = "";

    /// <summary>
    /// The quest's place among <paramref name="chain"/>'s counted steps (the chain's repeatables,
    /// <see cref="Chain.Uncounted"/>, are skipped); null when the chain does not list it as a counted step.
    /// </summary>
    public static ChainStep? StepOf(Chain chain, uint rowId)
    {
        ArgumentNullException.ThrowIfNull(chain);
        var position = 0;
        var total = 0;
        uint? next = null;
        foreach (var id in chain.RowIds)
        {
            if (chain.Uncounted.Contains(id))
            {
                continue;
            }

            total++;
            if (id == rowId)
            {
                position = total;
            }
            else if (position > 0 && next is null)
            {
                next = id;
            }
        }

        return position == 0 ? null : new ChainStep(position, total, next);
    }

    /// <summary>
    /// What the quest opens, one label each ("Dungeon: The Tam-Tara Deepcroft", "Job: Paladin"): the plan's tags for a
    /// feature quest (<see cref="UnlockTags.For"/>), else the quest's own unlock rewards (a duty, a class or job, a
    /// system, a general action), which the plan does not tag outside the feature quests. Inherited tags (a raid
    /// series step that leads to a raid rather than opening it) are left out; duplicates once.
    /// </summary>
    public static IReadOnlyList<PlanUnlock> Unlocks(QuestRecord quest, IReadOnlyList<PlanUnlock> tags)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(tags);
        var unlocks = new List<PlanUnlock>();
        foreach (var tag in tags)
        {
            if (!tag.Inherited && !unlocks.Contains(tag))
            {
                unlocks.Add(tag);
            }
        }

        if (unlocks.Count > 0)
        {
            return unlocks;
        }

        foreach (var reward in quest.Rewards)
        {
            if (reward.Name.Length == 0)
            {
                continue;
            }

            PlanUnlock? unlock = reward.Kind switch
            {
                RewardKind.Instance or RewardKind.DutyUnlock => new PlanUnlock(UnlockKind.Other, Capitalize(reward.Name)),
                RewardKind.ClassJob => new PlanUnlock(UnlockKind.Job, NameCase.Title(reward.Name)),
                RewardKind.SystemUnlock or RewardKind.GeneralAction => new PlanUnlock(UnlockKind.System, reward.Name),
                _ => null,
            };

            if (unlock is not null && !unlocks.Contains(unlock))
            {
                unlocks.Add(unlock);
            }
        }

        return unlocks;
    }

    /// <summary>
    /// The verdict line, or <see cref="Masked"/> when <paramref name="masked"/>. <paramref name="chainName"/> is the
    /// chain's name through the spoiler shield; ignored without <paramref name="step"/>.
    /// </summary>
    public static string Line(
        QuestRecord quest,
        bool masked,
        IReadOnlyList<PlanUnlock> unlocks,
        IReadOnlyList<MoonlitReward> moonlit,
        ChainStep? step,
        string chainName)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(unlocks);
        ArgumentNullException.ThrowIfNull(moonlit);
        if (masked)
        {
            return Masked;
        }

        // Something named that the quest opens; a duty, a job or a system before anything of kind Other.
        PlanUnlock? opens = null;
        foreach (var unlock in unlocks)
        {
            if (unlock.Name.Length > 0 && (opens is null || (opens.Kind == UnlockKind.Other && unlock.Kind != UnlockKind.Other)))
            {
                opens = unlock;
            }
        }

        if (opens is not null && opens.Kind != UnlockKind.Other)
        {
            return Format(CoreText.T("Core.Verdict.Unlocks", "Unlocks {0}"), opens.Name);
        }

        // A Moonlit reward the character lacks (or cannot be told to have) before one it owns.
        var lacking = 0;
        string? firstLacking = null;
        foreach (var reward in moonlit)
        {
            if (reward.Owned != true)
            {
                lacking++;
                firstLacking ??= reward.Name;
            }
        }

        if (firstLacking is not null)
        {
            return lacking > 1
                ? Format(CoreText.T("Core.Verdict.MoonlitMore", "Moonlit: {0} and {1} more"), firstLacking, lacking - 1)
                : Format(CoreText.T("Core.Verdict.Moonlit", "Moonlit: {0}"), firstLacking);
        }

        if (moonlit.Count > 0)
        {
            return Format(CoreText.T("Core.Verdict.MoonlitOwned", "Moonlit: {0} (you have it)"), moonlit[0].Name);
        }

        if (opens is not null)
        {
            return Format(CoreText.T("Core.Verdict.Unlocks", "Unlocks {0}"), opens.Name);
        }

        if (step is { } s && s.Total > 1)
        {
            return ChainPlace(s, chainName);
        }

        if (quest.Festival != 0)
        {
            return CoreText.T("Core.Verdict.Seasonal", "Seasonal event quest");
        }

        if (quest.IsRepeatable)
        {
            return CoreText.T("Core.Verdict.Repeatable", "Repeatable quest");
        }

        return CoreText.T("Core.Verdict.Nothing", "No unlock or unique reward");
    }

    /// <summary>
    /// Where a quest sits in its chain, as what is left after it (feature plan v6 U5, never "Step 3 of 7"): "Part of
    /// Hildibrand · 4 more after this", or "The last quest of Hildibrand". <paramref name="chainName"/> is the chain's
    /// name through the spoiler shield; empty says "a chain".
    /// </summary>
    public static string ChainPlace(ChainStep step, string chainName)
    {
        ArgumentNullException.ThrowIfNull(chainName);
        var after = Math.Max(0, step.Total - step.Position);
        if (chainName.Length == 0)
        {
            return after == 0
                ? CoreText.T("Core.Verdict.ChainLastUnnamed", "The last quest of a chain")
                : Format(CoreText.T("Core.Verdict.ChainMoreUnnamed", "Part of a chain · {0} more after this"), after);
        }

        return after == 0
            ? Format(CoreText.T("Core.Verdict.ChainLast", "The last quest of {0}"), chainName)
            : Format(CoreText.T("Core.Verdict.ChainMore", "Part of {0} · {1} more after this"), chainName, after);
    }

    private static string Format(string format, params object[] args) => string.Format(CultureInfo.CurrentCulture, format, args);

    /// <summary>"the Vault" opens a line as "The Vault".</summary>
    private static string Capitalize(string name) =>
        name.Length > 0 && char.IsLower(name[0]) ? char.ToUpperInvariant(name[0]) + name[1..] : name;
}
