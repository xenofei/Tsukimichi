using System;
using System.Collections.Generic;
using System.Linq;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Unique;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The C7 clear badges (spec-1.19 C7, "Badges": "the same badges appear wherever duties appear") for the surfaces
/// outside the detail pane: My blues, the Route window and the Duty Finder hint. Which duty a quest involves follows
/// the detail pane's "How you'll clear it" (<see cref="QuestDuties"/>: a duty it asks to have cleared, else one it
/// unlocks); which badges a surface wears follows <see cref="DutyBadgeRules.For(DutyRunInfo, bool?, DutyBadgeSurface)"/>.
/// The looks are built once per duty and surface and kept until the duty index, the catalog, the curated or reward data,
/// the story's duty source or the language changes (<see cref="Revision"/> then moves, so a caller that keeps the looks
/// rebuilds). Framework thread only.
/// </summary>
public sealed class ClearBadgeSource
{
    private static readonly DutyBadges.Look[] NoBadges = [];

    private readonly SessionState session;
    private readonly Func<DutyRunIndex?> runs;
    private readonly Func<UniqueRewardCatalog> rewards;
    private readonly Func<CatchUpDutySource?> story;
    private readonly Dictionary<(uint Condition, DutyBadgeSurface Surface), DutyBadges.Look[]> looks = [];
    private readonly Dictionary<uint, DutyRunInfo?> dutyOf = [];

    private (DutyRunIndex? Runs, QuestCatalog? Catalog, UniqueRewardCatalog? Rewards, CatchUpDutySource? Story, int Language) builtKey;
    private StoryRequirements? storyRequirements;
    private int revision;

    /// <param name="runs">The duty index (null while it is read).</param>
    /// <param name="rewards">The merged reward catalog, for the duties a quest unlocks.</param>
    /// <param name="story">The catch-up's duty source, for which duties the story needs; null leaves Story-required out.</param>
    public ClearBadgeSource(SessionState session, Func<DutyRunIndex?> runs, Func<UniqueRewardCatalog> rewards, Func<CatchUpDutySource?> story)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.runs = runs ?? throw new ArgumentNullException(nameof(runs));
        this.rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        this.story = story ?? throw new ArgumentNullException(nameof(story));
    }

    /// <summary>Moves whenever the badges may read differently; a caller keeping looks rebuilds when it does.</summary>
    public int Revision
    {
        get
        {
            Refresh();
            return revision;
        }
    }

    /// <summary>The duty index as of now; null while it is read.</summary>
    public DutyRunIndex? Index
    {
        get
        {
            Refresh();
            return builtKey.Runs;
        }
    }

    /// <summary>The first duty <paramref name="quest"/> involves, as "How you'll clear it" lists them; null for none.</summary>
    public DutyRunInfo? DutyOf(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        Refresh();
        if (builtKey.Runs is not { } index)
        {
            return null;
        }

        if (!dutyOf.TryGetValue(quest.RowId, out var duty))
        {
            var duties = QuestDuties.For(quest, index, session.Curated, builtKey.Rewards?.ForQuest(quest.RowId));
            duty = duties.Count > 0 ? duties[0].Duty : null;
            dutyOf[quest.RowId] = duty;
        }

        return duty;
    }

    /// <summary>The badges <paramref name="duty"/> wears on <paramref name="surface"/>, each with its label, icon and hover.</summary>
    public DutyBadges.Look[] For(DutyRunInfo duty, DutyBadgeSurface surface)
    {
        ArgumentNullException.ThrowIfNull(duty);
        Refresh();
        var key = (duty.ContentFinderConditionId, surface);
        if (!looks.TryGetValue(key, out var built))
        {
            bool? storyRequired = storyRequirements?.IsStoryDuty(duty.ContentFinderConditionId, duty.InstanceContentId);
            var roulettes = builtKey.Runs?.Roulettes ?? [];
            built = DutyBadgeRules.For(duty, storyRequired, surface).Select(b => DutyBadges.Describe(b, duty, roulettes)).ToArray();
            looks[key] = built;
        }

        return built;
    }

    /// <summary>The badges of the first duty <paramref name="quest"/> involves (<see cref="DutyOf"/>); empty for none.</summary>
    public DutyBadges.Look[] ForQuest(QuestRecord quest, DutyBadgeSurface surface) =>
        DutyOf(quest) is { } duty ? For(duty, surface) : NoBadges;

    private void Refresh()
    {
        (DutyRunIndex? Runs, QuestCatalog? Catalog, UniqueRewardCatalog? Rewards, CatchUpDutySource? Story, int Language) key =
            (runs(), session.Bundle?.Catalog, rewards(), story(), Localization.Loc.Version);
        if (ReferenceEquals(key.Runs, builtKey.Runs) && ReferenceEquals(key.Catalog, builtKey.Catalog) && ReferenceEquals(key.Rewards, builtKey.Rewards)
            && ReferenceEquals(key.Story, builtKey.Story) && key.Language == builtKey.Language)
        {
            return;
        }

        builtKey = key;
        looks.Clear();
        dutyOf.Clear();
        storyRequirements = key.Catalog is { } catalog && key.Story is { } source ? StoryRequirements.For(catalog, source) : null;
        revision++;
    }
}
