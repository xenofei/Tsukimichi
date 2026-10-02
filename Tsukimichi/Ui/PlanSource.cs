using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The "Clear my blues" plan (P3) for the viewed character, shared by the Plan tab and the todo overlay's pinned
/// block. <see cref="UnlockTags"/> are built once per catalog (the shipped unique-reward data merged with the curated
/// unlocks, without the user's verdicts, and the ContentFinderCondition kinds read on first use through the factory
/// the plugin hands in); the <see cref="UnlockPlan"/> is rebuilt when <see cref="SessionState.Version"/> changes, which
/// covers new states and a moved spoiler mask. Framework thread only.
/// </summary>
public sealed class PlanSource
{
    private readonly SessionState session;
    private readonly Func<PlanDuties> buildDuties;
    private readonly IPluginLog log;

    private PlanDuties? duties;
    private UnlockTags tags = UnlockTags.Empty;
    private object? tagsBundle;
    private IReadOnlySet<uint>? tagsFeatures;
    private UnlockPlan plan = UnlockPlan.Empty;
    private int planVersion = -1;
    private UnlockTags? planTags;

    public PlanSource(SessionState session, Func<PlanDuties> buildDuties, IPluginLog log)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.buildDuties = buildDuties ?? throw new ArgumentNullException(nameof(buildDuties));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// A zone's region by Map row id (the map's PlaceNameRegion: "La Noscea", "Thanalan"), so the plan walks a level
    /// band region by region (1.6.0); set by the plugin before the plan is first read. Null orders zones by level alone.
    /// </summary>
    public Func<uint, string>? RegionOfMap { get; set; }

    /// <summary>Bumped whenever <see cref="Plan"/> is rebuilt, so callers can memoize what they derive from it.</summary>
    public int Revision { get; private set; }

    /// <summary>The plan quests and their unlocks for the loaded catalog; empty while it loads.</summary>
    public UnlockTags Tags
    {
        get
        {
            Refresh();
            return tags;
        }
    }

    /// <summary>The viewed character's plan, unfiltered; empty while the catalog loads.</summary>
    public UnlockPlan Plan
    {
        get
        {
            Refresh();
            return plan;
        }
    }

    /// <summary>The expansion the character's main scenario has reached (Sprout mode's limit); 255 once it is complete.</summary>
    public byte Reach => session.Spoilers.ReachExpansion;

    private void Refresh()
    {
        var bundle = session.Bundle;
        if (bundle is null)
        {
            if (!plan.IsEmpty || tags.Count > 0)
            {
                tags = UnlockTags.Empty;
                plan = UnlockPlan.Empty;
                tagsBundle = null;
                Revision++;
            }

            return;
        }

        if (!ReferenceEquals(tagsBundle, bundle) || !ReferenceEquals(tagsFeatures, session.FeatureQuestIds))
        {
            tagsBundle = bundle;
            tagsFeatures = session.FeatureQuestIds;
            tags = BuildTags(bundle);
        }

        if (planVersion != session.Version || !ReferenceEquals(planTags, tags))
        {
            planVersion = session.Version;
            planTags = tags;
            plan = UnlockPlan.Build(tags, session.States, session.Names, RegionOfMap);
            Revision++;
        }
    }

    private UnlockTags BuildTags(CatalogBundle bundle)
    {
        try
        {
            if (duties is null)
            {
                try
                {
                    duties = buildDuties();
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "Duty kinds could not be read; the plan tags every duty Other");
                    duties = PlanDuties.Empty;
                }
            }

            var rewards = UniqueRewardCatalog.Build(session.UniqueRewards, new Dictionary<uint, UniqueOverride>(), session.Curated);
            return UnlockTags.Build(bundle.Catalog, session.FeatureQuestIds, rewards, duties, bundle.BlockerNames().Tribe);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "The unlock plan could not be built; the Plan tab is empty");
            return UnlockTags.Empty;
        }
    }
}
