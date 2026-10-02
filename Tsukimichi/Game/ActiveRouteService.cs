using System;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.GameData;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// The followed route (1.6.0, R6 A, C3 C): "Follow this route" in the route window stores the target and the
/// character in <see cref="Configuration.ActiveRoute"/> (never pins, never steps); this service builds the route
/// again from that character's states whenever the session changes, so completed steps drop off on their own. The
/// Todo overlay's route section, the Tonight card's Next stops and the route window read <see cref="ViewedRoute"/>.
/// <para>
/// Progress is watched on the character's own states (the live ones while it is logged in, else while it is viewed)
/// through a <see cref="RouteFollower"/>: when a step is turned in and the next stop moves, the map flag follows
/// (<see cref="Configuration.RouteFlagAdvance"/>, the flag only, logged-in character only); when the route runs out,
/// one chat line says so and the route is no longer followed. Framework thread only.
/// </para>
/// </summary>
public sealed class ActiveRouteService : IDisposable
{
    private readonly Configuration settings;
    private readonly SessionState session;
    private readonly GameLinks links;
    private readonly MapFlag flag;
    private readonly Action save;
    private readonly IPluginLog log;

    private RouteFollower follower = new();
    private SavedRoute? targetFor;
    private RouteTarget? target;

    // The routes built for the session version, the stored route and the catalog they were built with.
    private int builtVersion = -1;
    private SavedRoute? builtSaved;
    private CatalogBundle? builtBundle;
    private UnlockRoute? viewedRoute;
    private UnlockRoute? liveRoute;
    private bool disposed;

    public ActiveRouteService(Configuration settings, SessionState session, GameLinks links, MapFlag flag, Action save, IPluginLog log)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.flag = flag ?? throw new ArgumentNullException(nameof(flag));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        session.Changed += Evaluate;
        session.CharacterForgotten += OnCharacterForgotten;
        session.DataDeleted += OnDataDeleted;
    }

    /// <summary>Bumps when a route is followed or dropped and whenever the routes are rebuilt, for callers that memoize.</summary>
    public int Revision { get; private set; }

    /// <summary>The followed route as stored; null when none is.</summary>
    public SavedRoute? Saved => settings.ActiveRoute;

    /// <summary>The followed route's target ("everything for Dragoon"); empty when none is followed.</summary>
    public string Label => settings.ActiveRoute?.Label ?? string.Empty;

    /// <summary>Whether <paramref name="routeTarget"/> is the route followed for <paramref name="owner"/>.</summary>
    public bool Follows(RouteTarget routeTarget, ulong? owner) =>
        routeTarget is not null && owner is { } id && settings.ActiveRoute is { } saved && saved.OwnerContentId == id && saved.Matches(routeTarget);

    /// <summary>The followed route built for the viewed character; null when none is followed or another character is viewed.</summary>
    public UnlockRoute? ViewedRoute
    {
        get
        {
            Refresh();
            return viewedRoute;
        }
    }

    /// <summary>Follows <paramref name="routeTarget"/> for <paramref name="owner"/>, in place of any route followed before.</summary>
    public void Follow(RouteTarget routeTarget, ulong owner)
    {
        ArgumentNullException.ThrowIfNull(routeTarget);
        settings.ActiveRoute = SavedRoute.From(routeTarget, owner);
        save();
        follower = new RouteFollower();
        builtVersion = -1;
        Revision++;
        Evaluate();
    }

    /// <summary>Stops following the route; nothing else changes (pins stay as they are).</summary>
    public void Stop()
    {
        if (settings.ActiveRoute is null)
        {
            return;
        }

        settings.ActiveRoute = null;
        save();
        follower = new RouteFollower();
        builtVersion = -1;
        viewedRoute = null;
        liveRoute = null;
        Revision++;
    }

    /// <summary>
    /// "Flag next stop": opens the map with the flag on the giver of <paramref name="route"/>'s next stop
    /// (<see cref="ActiveRoute.NextStop"/>); false when the route is empty or that giver cannot be mapped.
    /// </summary>
    public bool FlagNextStop(UnlockRoute? route)
    {
        if (route is null || ActiveRoute.NextStop(route) is not { } step || session.Bundle?.Catalog.GetByRowId(step.RowId) is not { } quest || !links.CanFlagMap(quest))
        {
            return false;
        }

        links.FlagMap(quest);
        return true;
    }

    /// <summary>The quest of <paramref name="route"/>'s next stop, for a Flag button's enabled state; null when none.</summary>
    public QuestRecord? NextStopQuest(UnlockRoute? route) =>
        route is not null && ActiveRoute.NextStop(route) is { } step ? session.Bundle?.Catalog.GetByRowId(step.RowId) : null;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        session.Changed -= Evaluate;
        session.CharacterForgotten -= OnCharacterForgotten;
        session.DataDeleted -= OnDataDeleted;
    }

    /// <summary>On every session change: rebuilds the routes and acts on what the owner's progress says.</summary>
    private void Evaluate()
    {
        try
        {
            Refresh();
            if (settings.ActiveRoute is not { } saved)
            {
                return;
            }

            var isLive = session.LiveContentId == saved.OwnerContentId;
            var route = isLive ? liveRoute : viewedRoute;
            var states = isLive ? session.LiveStates : session.States;
            if (route is null || states.Count == 0)
            {
                return;
            }

            var progress = follower.Update(route);
            switch (progress.Kind)
            {
                case RouteProgressKind.Finished:
                    links.PrintText(ActiveRoute.CompletedLine(saved.Label));
                    Stop();
                    break;
                case RouteProgressKind.Lost:
                    log.Information("The followed route to {Label} has no quest any more; it is no longer followed", saved.Label);
                    Stop();
                    break;
                case RouteProgressKind.Advanced when settings.RouteFlagAdvance && isLive && progress.NextStop is { } next:
                    if (session.Bundle?.Catalog.GetByRowId(next) is { } quest)
                    {
                        flag.Set(quest);
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "The followed route could not be updated");
        }
    }

    /// <summary>Builds the route for the viewed character and for the logged-in one, each only when it is the owner.</summary>
    private void Refresh()
    {
        var saved = settings.ActiveRoute;
        var bundle = session.Bundle;
        if (builtVersion == session.Version && ReferenceEquals(builtSaved, saved) && ReferenceEquals(builtBundle, bundle))
        {
            return;
        }

        builtVersion = session.Version;
        builtSaved = saved;
        builtBundle = bundle;
        viewedRoute = null;
        liveRoute = null;
        Revision++;
        if (saved is null || bundle is null)
        {
            return;
        }

        if (!ReferenceEquals(targetFor, saved))
        {
            targetFor = saved;
            target = saved.ToTarget();
        }

        var routeTarget = target!;
        if (session.ViewedContentId == saved.OwnerContentId && session.ViewedSnapshot is { } viewed)
        {
            viewedRoute = UnlockRoute.Build(routeTarget, bundle.Catalog, session.States, session.Names, RouteLevels.For(viewed, session.Context));
        }

        if (session.LiveContentId == saved.OwnerContentId && session.LiveSnapshot is { } live)
        {
            liveRoute = session.IsLive && viewedRoute is not null
                ? viewedRoute
                : UnlockRoute.Build(routeTarget, bundle.Catalog, session.LiveStates, session.LiveNames, RouteLevels.For(live, session.Context));
        }
    }

    private void OnCharacterForgotten(ulong contentId)
    {
        if (settings.ActiveRoute?.OwnerContentId == contentId)
        {
            Stop();
        }
    }

    private void OnDataDeleted() => Stop();
}
