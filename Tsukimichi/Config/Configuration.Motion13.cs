using Tsukimichi.Core.Todo;

namespace Tsukimichi.Config;

/// <summary>
/// 1.13.0 motion settings (feature plan v6 M3, decision 12): the Todo overlay's hiding options. Each is off by default,
/// so the overlay stays where it is unless the player asks it to step aside. Drawn under Settings › Todo overlay
/// (<c>Ui/ConfigWindow.Motion13.cs</c>).
/// </summary>
public sealed partial class Configuration
{
    /// <summary>The Todo overlay fades away while the player is in combat and comes back after. Off by default.</summary>
    public bool TodoHideInCombat { get; set; }

    /// <summary>The Todo overlay fades away while the player talks to an NPC (or another event runs). Off by default.</summary>
    public bool TodoHideTalking { get; set; }

    /// <summary>The Todo overlay fades away in group pose, so it stays out of screenshots. Off by default.</summary>
    public bool TodoHideGroupPose { get; set; }

    /// <summary>The three hiding options as the overlay reads them (a method, so the settings file does not store it twice).</summary>
    public TodoHideRules TodoHiding() => new(TodoHideInCombat, TodoHideTalking, TodoHideGroupPose);
}
