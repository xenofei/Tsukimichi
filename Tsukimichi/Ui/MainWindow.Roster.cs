using System;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>1.21.0 (plan v7 P1, P3): Up next in the Tonight card and the detail hero's "your other characters" line read the roster.</summary>
public sealed partial class MainWindow
{
    /// <summary>Hands the roster (goals, every character's states) and the followed route to Tonight's Up next and the detail pane.</summary>
    public void AttachRoster(RosterSource roster, ActiveRouteService routes)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(routes);
        tonightCard.Roster = roster;
        tonightCard.Routes = routes;
        detailPane.Roster = roster;
    }
}
