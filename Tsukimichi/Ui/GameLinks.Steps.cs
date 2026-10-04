using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Travel;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The current step of a quest in the journal as the panes and chat lines show it (plan v7, 1.21.0 P2): the step,
/// the game's own objective text for it, the place's zone and coordinates and the person there, each name through the
/// wider spoiler shield, and <see cref="Travel"/>, the quest aimed at the step's place: the same quest with its
/// <see cref="QuestRecord.Issuer"/> moved to the step's place, so Flag, Teleport (Lifestream), Walk and Go to
/// (vnavmesh) aim there through the existing travel checks. Null <see cref="Travel"/> when the step has no place in
/// the open world.
/// </summary>
public sealed record StepView(
    CurrentStep Step,
    string Objective,
    string? Target,
    string Zone,
    bool ZoneHidden,
    Vector2? Coordinates,
    string DutyName,
    QuestRecord? Travel);

/// <summary>
/// Where to go for a quest in the journal (P2): its current step (<see cref="CurrentStepResolver"/> over
/// <see cref="QuestStepReader"/>'s objectives, read once per quest and kept), the player's choice to aim at the giver
/// instead (kept per quest until it leaves the journal) and the quest record travel aims at. Framework thread only.
/// </summary>
public sealed partial class GameLinks
{
    private const int StepViewCacheSize = 16;

    private readonly Dictionary<uint, QuestSteps> questSteps = [];
    private readonly HashSet<uint> giverAims = [];
    private readonly List<(StepViewKey Key, StepView View)> stepViews = [];

    private readonly record struct StepViewKey(uint RowId, byte Sequence, uint LevelId, bool Live, ulong ContentId, int Spoilers, int Language);

    /// <summary>The journal text reader, for the step's objective in the game's words; null shows no objective text.</summary>
    public QuestTextService? QuestText { get; set; }

    /// <summary>A quest's objectives and their places, read from the sheets once per quest.</summary>
    public QuestSteps StepsOf(uint rowId)
    {
        if (questSteps.TryGetValue(rowId, out var known))
        {
            return known;
        }

        QuestSteps steps;
        try
        {
            steps = QuestStepReader.Read(data.Excel, rowId, data.Language.ToLumina());
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Steps of quest {RowId} could not be read", rowId);
            steps = QuestSteps.Empty;
        }

        questSteps[rowId] = steps;
        return steps;
    }

    /// <summary>Whether the player chose Giver for this quest's Where to go (it aims at the current step otherwise).</summary>
    public bool AimsAtGiver(uint rowId) => giverAims.Contains(rowId);

    /// <summary>Aims a quest's travel at its giver (<paramref name="giver"/>) or back at its current step.</summary>
    public void AimAtGiver(uint rowId, bool giver)
    {
        if (giver)
        {
            giverAims.Add(rowId);
        }
        else
        {
            giverAims.Remove(rowId);
        }
    }

    /// <summary>
    /// The current step of <paramref name="quest"/> for the character whose <paramref name="evaluation"/> it is, or
    /// null when the quest is not in that character's journal. Several places resolve to the one nearest the
    /// logged-in character (<paramref name="live"/>). The text is rendered for the live character, else neutrally with
    /// <paramref name="storedName"/>.
    /// </summary>
    public StepView? CurrentStep(QuestRecord quest, QuestEvaluation? evaluation, bool live, string? storedName, ulong contentId)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (evaluation is not { State: QuestState.Accepted, Sequence: { } sequence } || sequence == 0)
        {
            // Out of the logged-in character's journal: the giver choice ends with it (spec-1.21 P2).
            if (live)
            {
                giverAims.Remove(quest.RowId);
            }

            return null;
        }

        uint territory = 0;
        float x = 0f, z = 0f;
        if (live && Travel is { Position: { } at } travel)
        {
            territory = travel.Territory;
            (x, z) = at;
        }

        if (CurrentStepResolver.Resolve(StepsOf(quest.RowId), sequence, quest.StepCount, territory, x, z) is not { } step)
        {
            return null;
        }

        var spoilers = Spoilers?.Invoke();
        var key = new StepViewKey(quest.RowId, sequence, step.Place?.LevelId ?? 0, live, contentId, spoilers?.Fingerprint ?? 0, Localization.Loc.Version);
        for (var i = stepViews.Count - 1; i >= 0; i--)
        {
            if (stepViews[i].Key == key)
            {
                return stepViews[i].View;
            }
        }

        var view = BuildStep(quest, step, live, storedName, contentId);
        stepViews.Add((key, view));
        if (stepViews.Count > StepViewCacheSize)
        {
            stepViews.RemoveAt(0);
        }

        return view;
    }

    /// <summary>
    /// The quest travel aims at: the step-aimed record while the quest is in the journal, its step has a place and the
    /// player has not chosen Giver; the quest itself (its giver) otherwise.
    /// </summary>
    public QuestRecord TravelTarget(QuestRecord quest, StepView? step) =>
        step?.Travel is { } aimed && !AimsAtGiver(quest.RowId) ? aimed : quest;

    private StepView BuildStep(QuestRecord quest, CurrentStep step, bool live, string? storedName, ulong contentId)
    {
        var objective = string.Empty;
        if (QuestText is { } text && step.Objective is { } aimed)
        {
            var lines = text.StepObjectives(quest, step.Sequence, live, storedName, contentId);
            foreach (var line in lines)
            {
                if (line.Index == aimed.Index)
                {
                    objective = line.Text;
                    break;
                }
            }

            if (objective.Length == 0 && lines.Count > 0)
            {
                objective = lines[0].Text;
            }
        }

        var spoilers = Spoilers?.Invoke();
        var dutyName = step.DutyId != 0 ? spoilers?.Name(SpoilerKind.Duty, DutyName(step.DutyId)) ?? DutyName(step.DutyId) : string.Empty;
        if (step.Place is not { } place)
        {
            return new StepView(step, objective, null, string.Empty, false, null, dutyName, null);
        }

        var map = Map(place.MapId);
        var zoneName = map?.PlaceName ?? string.Empty;
        var hidden = zoneName.Length > 0 && PlaceHidden(zoneName);
        Vector2? coordinates = map is { } m
            ? new Vector2(ToMapCoordinate(place.X, m.OffsetX, m.SizeFactor), ToMapCoordinate(place.Z, m.OffsetY, m.SizeFactor))
            : null;
        string? target = place.NpcName.Length > 0 ? spoilers?.Name(SpoilerKind.Npc, place.NpcName) ?? place.NpcName : null;

        // The issuer moved to the step's place: every travel check, the map flag and the coordinates aim there.
        var issuer = new Issuer(place.NpcId, place.NpcName.Length > 0 ? place.NpcName : Strings.StepAreaTarget, place.TerritoryId, place.MapId, place.X, place.Y, place.Z);
        return new StepView(step, objective, target, PlaceName(zoneName), hidden, coordinates, dutyName, quest with { Issuer = issuer });
    }

    /// <summary>A territory's zone name through the shield ("you're in Tuliyollal"); empty when unknown.</summary>
    public string TerritoryNameOf(uint territoryId) => territoryId == 0 ? string.Empty : TerritoryName(territoryId);

    /// <summary>
    /// Prints a <c>/tsuki msq</c>, <c>next</c> or <c>go</c> line (P8) to the plugin's own echo channel, after the gold
    /// "[Tsukimichi]" prefix: plain text, with the quest's name as a quest link whose text is the plain name.
    /// </summary>
    public void PrintGuidance(Core.Text.GuidanceLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        try
        {
            var builder = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder().AddText(line.Before);
            if (line.QuestRowId != 0 && line.QuestName.Length > 0)
            {
                builder.Add(new Dalamud.Game.Text.SeStringHandling.Payloads.QuestPayload(line.QuestRowId))
                       .AddText(line.QuestName)
                       .Add(Dalamud.Game.Text.SeStringHandling.Payloads.RawPayload.LinkTerminator);
            }
            else
            {
                builder.AddText(line.QuestName);
            }

            builder.AddText(line.After);
            chat.Print(builder.Build(), Strings.ChatTag);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Guidance line failed");
        }
    }

    /// <summary>A duty's name as the Duty Finder lists it, its first letter raised ("The Aery"); empty when unknown.</summary>
    private string DutyName(uint contentFinderConditionId)
    {
        try
        {
            var name = data.GetExcelSheet<ContentFinderCondition>().GetRowOrDefault(contentFinderConditionId)?.Name.ExtractText() ?? string.Empty;
            return name.Length > 0 ? char.ToUpperInvariant(name[0]) + name[1..] : name;
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Duty name unavailable");
            return string.Empty;
        }
    }
}
