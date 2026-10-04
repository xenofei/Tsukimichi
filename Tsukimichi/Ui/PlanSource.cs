using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The "Clear my blues" plan (P3) for the viewed character, shared by the Plan tab and the todo overlay's pinned
/// block. <see cref="UnlockTags"/> are built once per catalog on a worker (the shipped unique-reward data merged with
/// the curated unlocks, without the user's verdicts, and the ContentFinderCondition kinds warmed at load,
/// <see cref="IndexWarmer"/>), so opening the Plan tab never builds them on the frame; until they land the previous
/// catalog's tags serve, or none (<see cref="IsReady"/> says which). The <see cref="UnlockPlan"/> is rebuilt when
/// <see cref="SessionState.Version"/> changes, which covers new states, a moved spoiler mask and the character's
/// set-aside quests (P4, <see cref="SessionState.ViewedSetAside"/>), which it keeps out of the entries and counts. Each
/// quest carries its P4 tier (<see cref="TierOf"/>, <see cref="UnlockTiers.Classify"/>) for the viewed character.
/// Framework thread only.
/// </summary>
public sealed class PlanSource
{
    private readonly SessionState session;
    private readonly Func<PlanDuties?> readDuties;
    private readonly IPluginLog log;

    private UnlockTags tags = UnlockTags.Empty;
    private object? tagsBundle;
    private IReadOnlySet<uint>? tagsFeatures;
    private Task<UnlockTags>? pending;
    private CatalogBundle? pendingBundle;
    private IReadOnlySet<uint>? pendingFeatures;
    private UnlockPlan plan = UnlockPlan.Empty;
    private int planVersion = -1;
    private UnlockTags? planTags;
    private IReadOnlySet<uint>? planStory;

    /// <param name="readDuties">The duty kinds once warmed; null while they build (an empty index when they could not be read).</param>
    public PlanSource(SessionState session, Func<PlanDuties?> readDuties, IPluginLog log)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.readDuties = readDuties ?? throw new ArgumentNullException(nameof(readDuties));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// A zone's region by Map row id (the map's PlaceNameRegion: "La Noscea", "Thanalan"), so the plan walks a level
    /// band region by region (1.6.0); set by the plugin before the plan is first read. Null orders zones by level alone.
    /// </summary>
    public Func<uint, string>? RegionOfMap { get; set; }

    /// <summary>
    /// The duty data for the side quests the story needs (<see cref="StoryRequirements"/>), which make the P4 tier
    /// "Story needs it"; set by the plugin. Null knows only the main scenario's previous quests.
    /// </summary>
    public Func<CatchUpDutySource?>? StoryDuties { get; set; }

    /// <summary>The side quests the main scenario needs for the loaded catalog (shared with Your story); empty without a catalog.</summary>
    public IReadOnlySet<uint> StoryRequired =>
        session.Bundle is { } bundle ? StoryRequirements.For(bundle.Catalog, StoryDuties?.Invoke()).SideQuests : EmptyRows;

    private static readonly IReadOnlySet<uint> EmptyRows = new HashSet<uint>();

    private StoryRequirements? neededByFor;
    private Dictionary<uint, QuestRecord> neededBy = [];

    /// <summary>The first main scenario quest that needs <paramref name="rowId"/> (Do first's "Good Intentions needs it"); null when none does.</summary>
    public QuestRecord? StoryQuestNeeding(uint rowId)
    {
        if (session.Bundle is not { } bundle)
        {
            return null;
        }

        var requirements = StoryRequirements.For(bundle.Catalog, StoryDuties?.Invoke());
        if (!ReferenceEquals(requirements, neededByFor))
        {
            neededByFor = requirements;
            neededBy = [];
            foreach (var requirement in requirements.All)
            {
                foreach (var option in requirement.Options)
                {
                    foreach (var id in option)
                    {
                        neededBy.TryAdd(id, requirement.StoryQuest);
                    }
                }
            }
        }

        return neededBy.GetValueOrDefault(rowId);
    }

    /// <summary>
    /// The P4 tier of a plan quest for the viewed character (the tier word Nearby and the overlay add after the level);
    /// null for a quest the plan does not list.
    /// </summary>
    public UnlockTier? TierOf(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var current = Tags;
        return current.Contains(quest.RowId) ? UnlockTiers.Classify(quest, current.For(quest.RowId), TierContext()) : null;
    }

    // The snapshot, catalog and story requirements the context was built for, compared by reference.
    private CharacterSnapshot? tierSnapshot;
    private CatalogBundle? tierBundle;
    private IReadOnlySet<uint>? tierStory;
    private UnlockTierContext tierContext = UnlockTierContext.Empty;

    /// <summary>What the tiers know of the viewed character: the story's side quests, the job it plays, its societies.</summary>
    private UnlockTierContext TierContext()
    {
        var bundle = session.Bundle;
        var snapshot = session.ViewedSnapshot;
        var story = StoryRequired;
        if (ReferenceEquals(snapshot, tierSnapshot) && ReferenceEquals(bundle, tierBundle) && ReferenceEquals(story, tierStory) && tierStory is not null)
        {
            return tierContext;
        }

        tierSnapshot = snapshot;
        tierBundle = bundle;
        tierStory = story;
        if (bundle is null || snapshot is null)
        {
            tierContext = UnlockTierContext.Empty with { StoryRequired = story };
            return tierContext;
        }

        tierContext = UnlockTierContext.For(snapshot.CurrentJob, bundle.JobParents(), bundle.Jobs, snapshot.Tribes, story);
        return tierContext;
    }

    /// <summary>Bumped whenever <see cref="Plan"/> is rebuilt, so callers can memoize what they derive from it.</summary>
    public int Revision { get; private set; }

    /// <summary>Whether the tags of the loaded catalog are in hand; false while the catalog or its tags still load.</summary>
    public bool IsReady
    {
        get
        {
            Refresh();
            return session.Bundle is { } bundle && ReferenceEquals(tagsBundle, bundle);
        }
    }

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

    /// <summary>
    /// Starts (or collects) the tags of a newly loaded catalog without building the plan: polled from the framework
    /// tick, so the tags are ready by the time the Plan tab or the overlay first asks.
    /// </summary>
    public void Warm()
    {
        if (session.Bundle is not { } bundle)
        {
            return;
        }

        CollectTags();
        if (pending is null && (!ReferenceEquals(tagsBundle, bundle) || !ReferenceEquals(tagsFeatures, session.FeatureQuestIds)))
        {
            StartTags(bundle);
        }
    }

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

        Warm();
        var story = StoryRequired;
        if (planVersion != session.Version || !ReferenceEquals(planTags, tags) || !ReferenceEquals(planStory, story))
        {
            planVersion = session.Version;
            planTags = tags;
            planStory = story;
            var context = TierContext();
            plan = UnlockPlan.Build(tags, session.States, session.Names, RegionOfMap, session.Spoilers, (quest, unlocks) => UnlockTiers.Classify(quest, unlocks, context), session.ViewedSetAside);
            Revision++;
        }
    }

    /// <summary>Takes finished tags; ones built for a catalog that has since been replaced are dropped.</summary>
    private void CollectTags()
    {
        if (pending is not { IsCompleted: true } done)
        {
            return;
        }

        pending = null;
        if (!ReferenceEquals(pendingBundle, session.Bundle))
        {
            return;
        }

        tags = done.IsCompletedSuccessfully ? done.Result : UnlockTags.Empty;
        tagsBundle = pendingBundle;
        tagsFeatures = pendingFeatures;
    }

    /// <summary>Starts the tags of <paramref name="bundle"/> on a worker, once the duty kinds have landed.</summary>
    private void StartTags(CatalogBundle bundle)
    {
        if (readDuties() is not { } duties)
        {
            return;
        }

        var features = session.FeatureQuestIds;
        var unique = session.UniqueRewards;
        var curated = session.Curated;
        pendingBundle = bundle;
        pendingFeatures = features;
        pending = Task.Run(() => BuildTags(bundle, features, unique, curated, duties));
    }

    /// <summary>Runs on a worker: reads only what it is given.</summary>
    private UnlockTags BuildTags(CatalogBundle bundle, IReadOnlySet<uint> features, UniqueRewardsData unique, CuratedData curated, PlanDuties duties)
    {
        try
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var rewards = UniqueRewardCatalog.Build(unique, new Dictionary<uint, UniqueOverride>(), curated);
            var built = UnlockTags.Build(bundle.Catalog, features, rewards, duties, bundle.BlockerNames().Tribe);
            log.Debug("Plan tags: {Count} quests in {Ms:F0} ms on a worker", built.Count, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return built;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "The unlock plan could not be built; the Plan tab is empty");
            return UnlockTags.Empty;
        }
    }
}
