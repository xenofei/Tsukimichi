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
/// plan, the pinned expansion, the zone or the known ways into interiors (<see cref="GameLinks.EntranceRevision"/>)
/// change; each giver's aetheryte (the nearest one, attuned or not) is looked up once per catalog and entrance revision.
/// Framework thread only.
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

    private (int Version, int Pins, int Route, int Plan, uint Territory, int Expansion, int Entrances) builtKey = (-1, -1, -1, -1, 0, -2, -1);
    private int placesEntrances = -1;
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
        // Read before the key: both rebuild on first use after a change, and bump their revision when they do. The plan
        // (the duty index and every expansion's blues) is built only while an expansion is pinned from My blues.
        var route = routes.ViewedRoute;
        var expansion = settings.TodoPlanExpansion;
        var blues = expansion is >= 0 and <= byte.MaxValue ? plan.Plan : null;
        var zone = session.IsLive ? territory() : 0u;
        var entrances = links.EntranceRevision;
        var key = (session.Version, runner.PinsVersion, routes.Revision, blues is null ? -1 : plan.Revision, zone, expansion, entrances);
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

        if (!ReferenceEquals(placesBundle, bundle) || placesEntrances != entrances)
        {
            // A new catalog, or the ways into interiors became known: givers inside them grouped under their zone's
            // aetheryte meanwhile.
            placesBundle = bundle;
            placesEntrances = entrances;
            places.Clear();
        }

        var ready = new List<uint>();
        if (blues is not null && blues.Expansion((byte)expansion) is { } block)
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

    /// <summary>
    /// The aetheryte nearest a quest's giver, attuned or not (<see cref="GameLinks.GiverAetheryte"/>), looked up once
    /// per quest and catalog. Attunement plays no part in the grouping, so the cache never goes stale as aetherytes are
    /// attuned; each stop's Teleport checks attunement when it is drawn.
    /// </summary>
    private StopPlace? PlaceOf(QuestRecord quest)
    {
        if (places.TryGetValue(quest.RowId, out var known))
        {
            return known;
        }

        var place = links.GiverAetheryte(quest) is { } aetheryte ? new StopPlace(aetheryte.Id, aetheryte.Name) : null;
        places[quest.RowId] = place;
        return place;
    }
}
