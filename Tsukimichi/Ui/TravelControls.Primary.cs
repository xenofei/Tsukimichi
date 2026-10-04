using Dalamud.Interface;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>Which travel the primary pill starts (<see cref="TravelControls.Primary"/>).</summary>
internal enum PrimaryTravel : byte
{
    None,
    GoTo,
    Teleport,
    Walk,
    Flag,
    Stop,
}

/// <summary>The one travel pill a surface shows for a quest: what it does, its icon and its label.</summary>
internal readonly record struct PrimaryPill(PrimaryTravel Kind, PillIcon Icon, string Label)
{
    public static readonly PrimaryPill None = new(PrimaryTravel.None, default, string.Empty);
}

/// <summary>
/// The primary travel pill (plan v7, 1.21.0 P1): exactly the pill the detail pane's action row gives the accent at the
/// player's automation level (<c>DetailPane.PrepareActions</c>): the first of Go to, Teleport and Walk that can start
/// now, else Flag on map; Stop while a trip is under way; none for a giver in a place the story has not reached.
/// <paramref name="target"/> is the quest travel aims at (<c>GameLinks.TravelTarget</c>: the current step while in the
/// journal), and <paramref name="atStep"/> says so in the labels ("Go to step", "Walk to step").
/// </summary>
internal static partial class TravelControls
{
    private static readonly string StopGlyph = FontAwesomeIcon.Stop.ToIconString();

    public static PrimaryPill Primary(GameLinks links, QuestRecord target, bool atStep)
    {
        if (links.IsTraveling)
        {
            return new PrimaryPill(PrimaryTravel.Stop, StopGlyph, Strings.TravelStop);
        }

        if (links.GiverPlaceHidden(target))
        {
            return PrimaryPill.None;
        }

        if (links.GoToShown && links.CheckGoTo(target).Ready)
        {
            return new PrimaryPill(PrimaryTravel.GoTo, ActionIcons.GoTo(target), atStep ? Strings.StepGoTo : Strings.TravelGoTo);
        }

        if (links.TeleportShown && links.CheckTeleport(target) is { Ready: true, AlreadyHere: false })
        {
            return new PrimaryPill(PrimaryTravel.Teleport, ActionIcons.TeleportIcon, Strings.ActionTeleport);
        }

        if (links.WalkShown && links.CheckWalk(target).Ready)
        {
            return new PrimaryPill(PrimaryTravel.Walk, ActionIcons.WalkIcon, atStep ? Strings.StepWalkTo : Strings.TravelWalk);
        }

        return links.CanFlagMap(target) ? new PrimaryPill(PrimaryTravel.Flag, ActionIcons.FlagIcon, Strings.FlagOnMap) : PrimaryPill.None;
    }

    /// <summary>Starts what <paramref name="pill"/> says for <paramref name="target"/>.</summary>
    public static void Run(GameLinks links, QuestRecord target, in PrimaryPill pill)
    {
        switch (pill.Kind)
        {
            case PrimaryTravel.Stop:
                links.StopTravel();
                break;
            case PrimaryTravel.GoTo:
                links.GoToGiver(target);
                break;
            case PrimaryTravel.Teleport:
                links.TeleportToGiver(target);
                break;
            case PrimaryTravel.Walk:
                links.WalkToGiver(target);
                break;
            case PrimaryTravel.Flag:
                links.FlagMap(target);
                break;
        }
    }
}
