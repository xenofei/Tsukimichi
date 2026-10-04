using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Travel;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Where to go (plan v7, 1.21.0 P2; spec-1.21 P2): for a quest in the journal, a section under the hero that aims
/// travel at the step the character is on, with the giver one click away.
/// <list type="number">
/// <item><b>Aim at</b>: Current step · Giver (<see cref="Chrome.Segmented"/>), Current step by default; the choice is
/// kept for the quest until it leaves the journal (<see cref="GameLinks.AimAtGiver"/>). A label that does not fit its
/// cell shortens to its first word.</item>
/// <item><b>The step</b>: "Step 3 · Speak with Erenville.", the game's own objective for the step the quest is on
/// (<c>TEXT_&lt;ID&gt;_TODO_nn</c>); "· 2 more" before it when the step has more objectives. A later step is never
/// named.</item>
/// <item><b>The place</b>: "Shaaloani · X 27.0, Y 34.8 · you're in Tuliyollal"; a marked area says so; several places
/// say "Nearest of 3 places". A step inside a duty, or with no place in the game data, says so in the section (never
/// only in a tooltip) and aims at the giver, with Current step disabled; a duty step offers the Duty Finder.</item>
/// <item><b>The pills</b>: Flag, Teleport (Lifestream) and Walk (vnavmesh), each shown by the automation level (1.18:
/// hidden above it, never greyed), aimed at the chosen place.</item>
/// <item><b>A note</b> in Secondary: travel follows the step until it is done, and where the giver is.</item>
/// </list>
/// The action bar's pills follow the same aim (<see cref="PrepareStep"/>). Every name goes through the wider spoiler
/// shield. Lines are composed when the step, the aim, the zone or the language changes, not per frame. Sections draw
/// at every Decoration level through <see cref="BeginSection"/>.
/// </summary>
public sealed partial class DetailPane
{
    private static readonly string StepIcon = FontAwesomeIcon.MapSigns.ToIconString();

    // This frame's current step of the quest shown (null when it is not in the journal) and whether travel aims at it.
    private StepView? stepView;
    private bool travelAimsAtStep;

    // The section's lines as last composed, and what they were composed for.
    private (StepView? View, bool Giver, uint Territory, int Language) stepLinesKey;
    private readonly List<string> stepHead = [];
    private readonly List<string> stepPlace = [];
    private string stepNote = string.Empty;

    // The part of each line that prints a placeholder (spec-1.20 N6), and the hidden name its right-click reveals; -1 for none.
    private DotShield stepHeadShield = DotShield.None;
    private DotShield stepPlaceShield = DotShield.None;

    /// <summary>A placeholder among a dot line's parts: the part's index, the name's kind and the hidden name.</summary>
    private readonly record struct DotShield(int Index, SpoilerKind Kind, string Name)
    {
        public static readonly DotShield None = new(-1, SpoilerKind.Area, string.Empty);
    }

    /// <summary>
    /// Reads the current step of the quest shown, once per frame before the travel checks, and returns the quest travel
    /// aims at: the step's place, or the giver (not in the journal, Giver chosen, or no place for the step).
    /// </summary>
    private QuestRecord PrepareStep(SessionState session, QuestRecord quest)
    {
        var live = session.IsLive;
        stepView = links.CurrentStep(quest, model.Evaluation, live, live ? null : session.ViewedSnapshot?.Name, session.ViewedContentId ?? 0);
        var target = links.TravelTarget(quest, stepView);
        travelAimsAtStep = !ReferenceEquals(target, quest);
        return target;
    }

    /// <summary>The Where to go section; nothing for a quest that is not in the journal.</summary>
    private void DrawWhereToGo(SessionState session, QuestRecord quest)
    {
        if (stepView is not { } view)
        {
            return;
        }

        Gap();
        BeginSection("##whereToGo", Strings.StepWhereToGo, StepIcon);
        var right = cardRight - UiMetrics.Px(Theme.Spacing.CardPad.X);
        var hasPlace = view.Step.HasPlace;
        var giver = !hasPlace || links.AimsAtGiver(quest.RowId);
        ComposeStepLines(session, quest, view, giver);

        // Aim at: Current step · Giver.
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(Strings.StepAimAt);
        }

        ImGui.SameLine();
        var room = MathF.Max(1f, right - ImGui.GetCursorScreenPos().X);
        ReadOnlySpan<string> full = [Strings.StepAimCurrent, Strings.StepAimGiver];
        ReadOnlySpan<string> shortLabels = [Strings.StepAimCurrentShort, Strings.StepAimGiver];
        var labels = Chrome.SegmentedWidth(full) <= room ? full : shortLabels;
        ReadOnlySpan<string> tooltips = [hasPlace ? Strings.StepAimCurrentTooltip : stepPlace.Count > 0 ? stepPlace[0] : string.Empty, Strings.StepAimGiverTooltip];
        var selected = giver ? 1 : 0;
        using (ImRaii.Disabled(!hasPlace))
        {
            if (Chrome.Segmented("##aimAt", ref selected, labels, Chrome.SegmentedWidth(labels, room), tooltips) && hasPlace)
            {
                links.AimAtGiver(quest.RowId, selected == 1);
                giver = selected == 1;
                ComposeStepLines(session, quest, view, giver);
            }
        }

        DrawDotLine(stepHead, right, Theme.Surface.TextSecondary, Theme.Surface.Text, stepHeadShield);
        DrawDotLine(stepPlace, right, Theme.Surface.TextSecondary, Theme.Surface.TextSecondary, stepPlaceShield);
        DrawStepPills(view, links.TravelTarget(quest, view));
        if (stepNote.Length > 0)
        {
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                TextFlow.Wrapped(stepNote, RoomTo(right));
            }
        }

        EndSection();
    }

    /// <summary>Flag, Teleport and Walk aimed at the chosen place, and the Duty Finder for a step inside a duty.</summary>
    private void DrawStepPills(StepView view, QuestRecord target)
    {
        var canFlag = links.CanFlagMap(target);
        if (TravelControls.FlagButton(Strings.StepFlag, canFlag, "##stepFlag"))
        {
            links.FlagMap(target);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(canFlag ? Strings.FlagOnMap : Strings.ActionFlagUnavailable);
        }

        using (ImRaii.PushId("##stepTravel"))
        {
            TravelControls.Buttons(links, target, Strings.ActionTeleport);
        }

        if (view.Step.Kind == StepPlaceKind.Duty && links.CanOpenDutyFinder(view.Step.DutyId))
        {
            ImGui.SameLine();
            if (TravelControls.RowButton("##stepDutyFinder", GameIconRef.Tile(ActionIcons.DutyFinder), Strings.StepDutyFinder))
            {
                links.OpenDutyFinder(view.Step.DutyId);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.StepDutyFinderTooltip);
            }
        }
    }

    /// <summary>Composes the step, place and note lines when the step, the aim, the player's zone or the language changed.</summary>
    private void ComposeStepLines(SessionState session, QuestRecord quest, StepView view, bool giver)
    {
        var territory = session.IsLive ? links.Travel?.Territory ?? 0 : 0;
        var key = (view, giver, territory, Localization.Loc.Version);
        if (stepLinesKey.View is not null && key == stepLinesKey)
        {
            return;
        }

        stepLinesKey = key;
        stepHead.Clear();
        stepPlace.Clear();
        stepHeadShield = DotShield.None;
        stepPlaceShield = DotShield.None;
        var (giverName, giverZone, giverCoordinates) = GiverPlace(session, quest);
        var giverZoneHidden = HiddenArea(session, quest.Issuer?.MapId ?? 0);
        var step = view.Step;
        if (giver && step.HasPlace)
        {
            stepHead.Add(Strings.StepGiverLabel);
            if (quest.Issuer is { Name.Length: > 0 } issuer && session.Spoilers.IsNameMasked(SpoilerKind.Npc, issuer.Name))
            {
                stepHeadShield = new DotShield(stepHead.Count, SpoilerKind.Npc, issuer.Name);
            }

            stepHead.Add(giverName);
            AddPlace(giverZone, giverZoneHidden, giverCoordinates, quest.Issuer?.TerritoryId ?? 0, territory);
            stepNote = Strings.StepGiverNote;
            return;
        }

        stepHead.Add(string.Format(CultureInfo.CurrentCulture, Strings.StepNumberFormat, step.Step));
        if (step.MoreObjectives > 0)
        {
            stepHead.Add(string.Format(CultureInfo.CurrentCulture, Strings.StepMoreFormat, step.MoreObjectives));
        }

        stepHead.Add(view.Objective.Length > 0 ? view.Objective : Strings.StepNoObjective);
        switch (step.Kind)
        {
            case StepPlaceKind.Duty:
                ShieldGiverZone(giverZoneHidden);
                stepPlace.Add(string.Format(CultureInfo.CurrentCulture, Strings.StepDutyFormat, view.DutyName, giverName, giverZone));
                stepNote = string.Empty;
                return;
            case StepPlaceKind.NoPlace:
                ShieldGiverZone(giverZoneHidden);
                stepPlace.Add(string.Format(CultureInfo.CurrentCulture, Strings.StepNoPlaceFormat, giverName, giverZone));
                stepNote = string.Empty;
                return;
            case StepPlaceKind.Area:
                stepPlace.Add(Strings.StepAreaPlace);
                break;
            case StepPlaceKind.SeveralPlaces:
                stepPlace.Add(string.Format(CultureInfo.CurrentCulture, Strings.StepNearestFormat, step.PlaceCount));
                break;
        }

        AddPlace(view.Zone, view.ZoneHidden ? HiddenArea(session, step.Place?.MapId ?? 0) : null, view.Coordinates, step.Place?.TerritoryId ?? 0, territory);
        stepNote = string.Format(CultureInfo.CurrentCulture, Strings.StepNoteFormat, giverName, giverZone);
    }

    /// <summary>The next place line's part names the giver's zone, which the story has not reached: its placeholder answers.</summary>
    private void ShieldGiverZone(string? hidden)
    {
        if (hidden is not null)
        {
            stepPlaceShield = new DotShield(stepPlace.Count, SpoilerKind.Area, hidden);
        }
    }

    /// <summary>A map's zone name when the shield hides it (what the placeholder's reveal reveals); null otherwise.</summary>
    private string? HiddenArea(SessionState session, uint mapId) =>
        mapId != 0 && links.Map(mapId)?.PlaceName is { Length: > 0 } name && session.Spoilers.IsNameMasked(SpoilerKind.Area, name) ? name : null;

    /// <summary>"Shaaloani · X 27.0, Y 34.8 · you're in Tuliyollal" (the last part only while the player is elsewhere).</summary>
    private void AddPlace(string zone, string? hiddenZone, Vector2? coordinates, uint placeTerritory, uint playerTerritory)
    {
        if (zone.Length > 0)
        {
            if (hiddenZone is not null)
            {
                stepPlaceShield = new DotShield(stepPlace.Count, SpoilerKind.Area, hiddenZone);
            }

            stepPlace.Add(zone);
        }

        if (coordinates is { } c)
        {
            stepPlace.Add(string.Format(CultureInfo.CurrentCulture, Strings.StepCoordinatesFormat, c.X, c.Y));
        }

        if (playerTerritory != 0 && placeTerritory != 0 && playerTerritory != placeTerritory && links.TerritoryNameOf(playerTerritory) is { Length: > 0 } here)
        {
            stepPlace.Add(string.Format(CultureInfo.CurrentCulture, Strings.StepYoureInFormat, here));
        }
    }

    /// <summary>The giver's name, zone and coordinates through the shield ("the giver" when the sheet names nobody).</summary>
    private (string Name, string Zone, Vector2? Coordinates) GiverPlace(SessionState session, QuestRecord quest)
    {
        if (quest.Issuer is not { } issuer)
        {
            return (Strings.TravelTheGiver, string.Empty, null);
        }

        var name = issuer.Name.Length > 0 ? session.Spoilers.Name(SpoilerKind.Npc, issuer.Name) : Strings.TravelTheGiver;
        var zone = links.Map(issuer.MapId)?.PlaceName is { } place ? session.Spoilers.Name(SpoilerKind.Area, place) : string.Empty;
        return (name, zone, links.MapCoordinates(quest));
    }

    /// <summary>
    /// Parts joined by " · " that wrap only between parts (spec-1.21 P2: a coordinate pair or "2 more" never breaks
    /// inside, and a wrapped line never starts with the separator); a part wider than the room wraps between words. The
    /// first part in <paramref name="first"/>, the rest in <paramref name="rest"/>.
    /// </summary>
    private void DrawDotLine(List<string> parts, float right, Vector4 first, Vector4 rest, DotShield shield)
    {
        if (parts.Count == 0)
        {
            return;
        }

        var separator = Strings.AutoDutyCaptionSeparator;
        var separatorWidth = ImGui.CalcTextSize(separator).X;
        var left = ImGui.GetCursorScreenPos().X;
        var x = left;
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            var width = ImGui.CalcTextSize(part).X;
            var color = i == 0 ? first : rest;
            if (i > 0 && x + separatorWidth + width <= right)
            {
                ImGui.SameLine(0f, 0f);
                using (Theme.PushText(Theme.Surface.TextSecondary))
                {
                    ImGui.TextUnformatted(separator);
                }

                ImGui.SameLine(0f, 0f);
                x += separatorWidth;
            }
            else if (i > 0)
            {
                x = left;
            }

            if (x + width <= right)
            {
                using (Theme.PushText(color))
                {
                    ImGui.TextUnformatted(part);
                }

                x += width;
            }
            else
            {
                TextFlow.Wrapped(part, MathF.Max(1f, right - left), Theme.U32(color));
                x = right;
            }

            if (i == shield.Index)
            {
                // A placeholder ("a zone ahead"): the shield's hover and right-click, in the quest's own context.
                ShieldItem(shield.Kind, shield.Name, part);
            }
        }
    }
}
