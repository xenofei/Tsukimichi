using System;
using System.Collections.Generic;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// "Next stops" (1.6.0, R6 B) for the viewed character, shared by the Tonight card and the todo overlay: its Ready
/// quests batched by the aetheryte nearest their givers (<see cref="StopPlanner"/>), from the followed route's next
/// stop, the pins, the Ready blues of the expansion pinned from My blues and the other Ready quests within
/// <see cref="StopPlanner.DefaultLevelRange"/> levels. Rebuilt when the session, the pins, the followed route, the
/// plan, the pinned expansion or the zone changes; each giver's aetheryte is looked up once per catalog. Framework
/// thread only.
/// </summary>
public sealed class NextStopsSource
{
    private readonly SessionState session;
    private readonly GameLinks links;
    private readonly QueryRunner runner;
    private readonly PlanSource plan;
    private readonly ActiveRouteService routes;
    private readonly Configuration settings;
    private readonly Func<uint> territory;

    private readonly Dictionary<uint, StopPlace?> places = [];
    private CatalogBundle? placesBundle;

    private (int Version, int Pins, int Route, int Plan, uint Territory, int Expansion) builtKey = (-1, -1, -1, -1, 0, -2);
    private IReadOnlyList<Stop> stops = [];

    /// <param name="territory">The zone the logged-in character stands in.</param>
    public NextStopsSource(SessionState session, GameLinks links, QueryRunner runner, PlanSource plan, ActiveRouteService routes, Configuration settings, Func<uint> territory)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
        this.routes = routes ?? throw new ArgumentNullException(nameof(routes));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.territory = territory ?? throw new ArgumentNullException(nameof(territory));
    }

    /// <summary>Bumps whenever <see cref="Stops"/> is rebuilt.</summary>
    public int Revision { get; private set; }

    /// <summary>The viewed character's stops, best first; empty without a character or a catalog.</summary>
    public IReadOnlyList<Stop> Stops
    {
        get
        {
            Refresh();
            return stops;
        }
    }

    private void Refresh()
    {
        runner.SyncPins();
        // Read before the key: both rebuild on first use after a change, and bump their revision when they do.
        var route = routes.ViewedRoute;
        var blues = plan.Plan;
        var zone = session.IsLive ? territory() : 0u;
        var key = (session.Version, runner.PinsVersion, routes.Revision, plan.Revision, zone, settings.TodoPlanExpansion);
        if (key == builtKey)
        {
            return;
        }

        builtKey = key;
        Revision++;
        if (session.Bundle is not { } bundle || session.ViewedSnapshot is not { } snapshot)
        {
            stops = [];
            return;
        }

        if (!ReferenceEquals(placesBundle, bundle))
        {
            placesBundle = bundle;
            places.Clear();
        }

        var ready = new List<uint>();
        if (settings.TodoPlanExpansion is >= 0 and <= byte.MaxValue && blues.Expansion((byte)settings.TodoPlanExpansion) is { } block)
        {
            foreach (var entry in block.Entries)
            {
                if (entry.IsReady)
                {
                    ready.Add(entry.Quest.RowId);
                }
            }
        }

        stops = StopPlanner.Plan(new StopInputs(
            bundle.Catalog,
            session.States,
            PlaceOf,
            runner.PinnedInOrder,
            route is null ? null : ActiveRoute.NextStop(route)?.RowId,
            ready,
            session.FeatureQuestIds,
            snapshot.JobLevels.GetValueOrDefault(snapshot.CurrentJob),
            StopPlanner.DefaultLevelRange,
            zone));
    }

    /// <summary>The aetheryte nearest a quest's giver, looked up once per quest and catalog.</summary>
    private StopPlace? PlaceOf(QuestRecord quest)
    {
        if (places.TryGetValue(quest.RowId, out var known))
        {
            return known;
        }

        var place = links.NearestAetheryte(quest) is { } aetheryte ? new StopPlace(aetheryte.Id, aetheryte.Name) : null;
        places[quest.RowId] = place;
        return place;
    }
}
