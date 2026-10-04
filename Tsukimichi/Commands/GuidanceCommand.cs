using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Text;
using Tsukimichi.Core.Travel;
using Tsukimichi.Game;
using Tsukimichi.Ui;

namespace Tsukimichi.Commands;

/// <summary>
/// <c>/tsuki msq</c>, <c>/tsuki next</c> and <c>/tsuki go [quest name]</c> (plan v7, 1.21.0 P8 and P2): plain sentences
/// for text-to-speech (<see cref="GuidanceText"/>), printed to the plugin's own echo channel only, about the logged-in
/// character (the viewed one while nobody is logged in), every name through its spoiler shield.
/// <list type="bullet">
/// <item><c>msq</c>: where the character stands in the main scenario and what is left (<see cref="MsqLeft"/>).</item>
/// <item><c>next</c>: the one quest to do next (<see cref="GuidancePick"/>, Up next's order) with, for a quest in the
/// journal, its current step in the game's words and where it is (<see cref="GameLinks.CurrentStep"/>).</item>
/// <item><c>go</c>: travels to the current step of the named or selected quest in the journal, else to its giver (with no
/// name and no selection, to <c>next</c>'s quest), at the player's automation level and never above it: Go to (teleport
/// then walk), else Teleport, else Walk, else the map flag; one line says what started. <c>/tsuki stop</c> stops it.</item>
/// </list>
/// "Say what's next in chat" prints <see cref="NextLine"/> and <see cref="StepDoneLine"/> (ChatNotifier.WhatsNext.cs).
/// </summary>
public sealed class GuidanceCommand(SessionState session, UiState ui, GameLinks links)
{
    /// <summary>The followed route's next quest (Up next rule 1); null when no route is followed.</summary>
    public Func<QuestRecord?>? RouteNext { get; set; }

    /// <summary>The pinned quests in pin order.</summary>
    public Func<IReadOnlyList<uint>>? Pins { get; set; }

    /// <summary>Next stops' quests, closest stop first.</summary>
    public Func<IEnumerable<uint>>? Closest { get; set; }

    private IReadOnlyDictionary<uint, QuestEvaluation> States => session.LiveContentId is not null ? session.LiveStates : session.States;

    private SpoilerMask Spoilers => session.LiveContentId is not null ? session.LiveSpoilers : session.Spoilers;

    private bool Live => session.LiveContentId is not null;

    private ulong ContentId => session.LiveContentId ?? session.ViewedContentId ?? 0;

    /// <summary><c>/tsuki msq</c>.</summary>
    public void Msq()
    {
        if (session.Bundle is not { } bundle)
        {
            links.PrintText(Strings.CatalogNotReady);
            return;
        }

        var line = MsqLeft.For(bundle.Catalog, States) is { } left
            ? GuidanceText.Msq(left.Part, Spoilers.DisplayName(left.Next), left.Next.RowId, left.LeftInPart, left.LeftToLatest, left.MinLevel, left.MaxLevel)
            : GuidanceText.MsqCaughtUp();
        links.PrintGuidance(line);
    }

    /// <summary><c>/tsuki next</c>.</summary>
    public void Next()
    {
        if (NextLine() is { } line)
        {
            links.PrintGuidance(line);
        }
        else
        {
            links.PrintText(Strings.CatalogNotReady);
        }
    }

    /// <summary>The <c>/tsuki next</c> line; null while the catalog is not ready.</summary>
    public GuidanceLine? NextLine()
    {
        if (session.Bundle is not { } bundle)
        {
            return null;
        }

        var states = States;
        return Pick(bundle.Catalog, states) is { } quest ? LineFor(quest, states) : GuidanceText.NextNothing(GuidancePick.ReadyOnOtherJob(states));
    }

    /// <summary>
    /// "Step done. Next: step 4. …" for the quest whose step just finished; null when it is not in the journal any more
    /// or the catalog does not know it.
    /// </summary>
    public GuidanceLine? StepDoneLine(ushort questId)
    {
        if (session.Bundle?.Catalog.GetByQuestId(questId) is not { } quest || !States.TryGetValue(quest.RowId, out var evaluation)
            || links.CurrentStep(quest, evaluation, Live, null, ContentId) is not { } view)
        {
            return null;
        }

        return GuidanceText.StepDone(view.Step.Step, view.Objective, StepPlace(view), PlayerZone());
    }

    /// <summary><c>/tsuki go [quest name]</c>.</summary>
    public void Go(string name)
    {
        if (session.Bundle is not { } bundle)
        {
            links.PrintText(Strings.CatalogNotReady);
            return;
        }

        var text = (name ?? string.Empty).Trim();
        QuestRecord? quest;
        if (text.Length > 0)
        {
            quest = ReportCommand.FindByName(bundle.Catalog, text, Spoilers);
            if (quest is null)
            {
                links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.ReportNoMatchFormat, text));
                return;
            }
        }
        else
        {
            quest = (ui.SelectedRowId is { } rowId ? bundle.Catalog.GetByRowId(rowId) : null) ?? Pick(bundle.Catalog, States);
        }

        if (quest is null)
        {
            links.PrintText(Strings.GuidanceNothingToGo);
            return;
        }

        States.TryGetValue(quest.RowId, out var evaluation);
        var view = links.CurrentStep(quest, evaluation, Live, null, ContentId);
        var target = links.TravelTarget(quest, view);
        var atStep = !ReferenceEquals(target, quest);
        if (Start(target) is not { } action)
        {
            links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.GuidanceNoPlaceFormat, GuidanceText.Speakable(Spoilers.DisplayName(quest))));
            return;
        }

        var line = atStep && view is not null
            ? GuidanceText.Go(Spoilers.DisplayName(quest), quest.RowId, view.Step.Step, StepTargetName(view), StepPlace(view), action)
            : GuidanceText.Go(Spoilers.DisplayName(quest), quest.RowId, 0, GiverName(quest), GiverPlace(quest), action);
        links.PrintGuidance(line);
    }

    /// <summary>Starts travel to <paramref name="target"/>'s place at the automation level; what started, or null when nothing could.</summary>
    private GuidanceText.GoAction? Start(QuestRecord target)
    {
        if (links.GoToShown && links.CheckGoTo(target) is { Ready: true, Plan: { } plan } && links.GoToGiver(target))
        {
            return plan.Teleport is not null ? GuidanceText.GoAction.TeleportFirst : GuidanceText.GoAction.Walk;
        }

        if (links.TeleportShown && links.CheckTeleport(target) is { Ready: true, AlreadyHere: false } && links.TeleportToGiver(target))
        {
            return GuidanceText.GoAction.Teleport;
        }

        if (links.WalkShown && links.CheckWalk(target).Ready && links.WalkToGiver(target))
        {
            return GuidanceText.GoAction.Walk;
        }

        if (links.CanFlagMap(target))
        {
            links.FlagMap(target);
            return GuidanceText.GoAction.Flag;
        }

        return null;
    }

    private QuestRecord? Pick(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        var msq = MsqProgress.Compute(catalog, states)?.Next?.RowId;
        var pick = GuidancePick.Pick(states, RouteNext?.Invoke()?.RowId, msq, Pins?.Invoke() ?? [], Closest?.Invoke() ?? []);
        return pick is { } chosen ? catalog.GetByRowId(chosen.RowId) : null;
    }

    private GuidanceLine LineFor(QuestRecord quest, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        if (Spoilers.IsMasked(quest))
        {
            var zone = quest.Issuer is { } issuer ? links.Map(issuer.MapId)?.PlaceName : null;
            return GuidanceText.NextMasked(MsqProgress.MainScenarioSections.Contains(quest.Journal.SectionId), quest.DisplayLevel, Spoilers.IsNameMasked(SpoilerKind.Area, zone));
        }

        states.TryGetValue(quest.RowId, out var evaluation);
        if (links.CurrentStep(quest, evaluation, Live, null, ContentId) is { } view)
        {
            return GuidanceText.NextInJournal(Spoilers.DisplayName(quest), quest.RowId, view.Step.Step, view.Objective, StepPlace(view), PlayerZone());
        }

        return GuidanceText.NextReady(Spoilers.DisplayName(quest), quest.RowId, GiverName(quest), GiverPlace(quest), PlayerZone());
    }

    /// <summary>The person at the step's place, or the marked area's words; null when the shield hides the person.</summary>
    private string? StepTargetName(StepView view) => view.Step.Place switch
    {
        { NpcName.Length: > 0 } place => Spoilers.IsNameMasked(SpoilerKind.Npc, place.NpcName) ? null : place.NpcName,
        { } => Strings.StepAreaTarget,
        _ => null,
    };

    private string? GiverName(QuestRecord quest) =>
        quest.Issuer is { Name.Length: > 0 } issuer && !Spoilers.IsNameMasked(SpoilerKind.Npc, issuer.Name) ? issuer.Name : null;

    private GuidancePlace? StepPlace(StepView view) =>
        view.Step.Place is { } place ? Place(place.TerritoryId, place.MapId, place.X, place.Z, view.Coordinates) : null;

    private GuidancePlace? GiverPlace(QuestRecord quest) =>
        quest.Issuer is { TerritoryId: > 0 } issuer ? Place(issuer.TerritoryId, issuer.MapId, issuer.X, issuer.Z, links.MapCoordinates(quest)) : null;

    /// <summary>A place for a chat line: its zone (null when the shield hides it), coordinates, and the distance and direction in the player's zone.</summary>
    private GuidancePlace Place(uint territory, uint mapId, float x, float z, System.Numerics.Vector2? coordinates)
    {
        var zone = links.Map(mapId)?.PlaceName;
        var shown = zone is { Length: > 0 } && !Spoilers.IsNameMasked(SpoilerKind.Area, zone) ? zone : null;
        var sameZone = Live && links.Travel is { } travel && travel.Territory == territory;
        float? yalms = null;
        CompassPoint? direction = null;
        if (sameZone && links.Travel?.Position is { } at)
        {
            yalms = TravelPlanner.Distance(at.X, at.Z, x, z);
            direction = GuidanceText.Compass(at.X, at.Z, x, z);
        }

        return new GuidancePlace(shown, coordinates?.X, coordinates?.Y, sameZone, yalms, direction);
    }

    private string? PlayerZone() =>
        Live && links.Travel is { Territory: > 0 } travel ? links.TerritoryNameOf(travel.Territory) : null;
}
