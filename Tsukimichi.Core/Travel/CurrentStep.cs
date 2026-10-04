using Tsukimichi.Core.Text;

namespace Tsukimichi.Core.Travel;

/// <summary>
/// One place a quest objective reaches: a <c>Level</c> row named by <c>Quest.TodoParams[i].ToDoLocation</c>, in raw
/// world units (<paramref name="Y"/> the height). <paramref name="IsArea"/> is a marked area (the Level row is no
/// person: the game draws a circle of <paramref name="Radius"/> on the map) rather than someone to talk to;
/// <paramref name="NpcName"/> is the person's name when the row is one (ENpcResident), else empty.
/// <paramref name="DutyId"/> is the duty (ContentFinderCondition row) whose territory the place lies in, 0 for the open
/// world: such a step happens inside the duty.
/// </summary>
public sealed record StepPlace(
    uint LevelId,
    uint TerritoryId,
    uint MapId,
    float X,
    float Y,
    float Z,
    float Radius,
    bool IsArea,
    uint NpcId,
    string NpcName,
    uint DutyId = 0);

/// <summary>
/// One objective of a quest (<c>TodoParams[Index]</c>): the journal sequence it is completed in
/// (<c>ToDoCompleteSeq</c>, 255 for the last step), how many of it the step wants (<c>ToDoQty</c>) and its places.
/// </summary>
public sealed record StepObjective(int Index, byte Sequence, byte Quantity, IReadOnlyList<StepPlace> Places);

/// <summary>Every objective of one quest as the game data holds it, in <c>TodoParams</c> order.</summary>
public sealed record QuestSteps(uint QuestRowId, IReadOnlyList<StepObjective> Objectives)
{
    public static readonly QuestSteps Empty = new(0, []);
}

/// <summary>What the current step's place is, so the detail pane and the chat lines say it the same way.</summary>
public enum StepPlaceKind : byte
{
    /// <summary>One person or spot.</summary>
    OnePlace,

    /// <summary>One marked area (gather, defeat, look around), aimed at its centre.</summary>
    Area,

    /// <summary>Several separate places; travel aims at the nearest.</summary>
    SeveralPlaces,

    /// <summary>The step happens inside a duty, which the game data gives no outside entrance for.</summary>
    Duty,

    /// <summary>The game data gives the step no place (a roulette, a gathering log): travel aims at the giver.</summary>
    NoPlace,
}

/// <summary>
/// The step a quest in the journal is on (plan v7, 1.21.0 P2): its number, its objectives (never a later step's),
/// where travel aims and how many places the step has. <see cref="Place"/> is null for <see cref="StepPlaceKind.Duty"/>
/// and <see cref="StepPlaceKind.NoPlace"/>.
/// </summary>
public sealed record CurrentStep(int Step, byte Sequence, IReadOnlyList<StepObjective> Objectives, StepPlaceKind Kind, StepPlace? Place, int PlaceCount, uint DutyId)
{
    /// <summary>The objective the aimed place belongs to, else the step's first; null when the data has none for the step.</summary>
    public StepObjective? Objective
    {
        get
        {
            if (Place is { } place)
            {
                foreach (var objective in Objectives)
                {
                    foreach (var p in objective.Places)
                    {
                        if (p.LevelId == place.LevelId)
                        {
                            return objective;
                        }
                    }
                }
            }

            return Objectives.Count > 0 ? Objectives[0] : null;
        }
    }

    /// <summary>The step's other objectives besides <see cref="Objective"/> ("· 2 more"): three people to talk to is one objective and two more.</summary>
    public int MoreObjectives => Math.Max(0, Objectives.Count - 1);

    /// <summary>Whether travel can aim at the step: it has a place in the open world.</summary>
    public bool HasPlace => Place is not null;
}

/// <summary>
/// What one sequence of a quest can resolve to (<see cref="CurrentStepResolver.Choices"/>): its open-world places and
/// the <see cref="CurrentStep"/> each would aim at, built once per quest and sequence. <see cref="Pick"/> chooses by the
/// player's position and allocates nothing, so a caller can keep the choices and ask every frame.
/// </summary>
public sealed class StepChoices
{
    private readonly CurrentStep[] steps;

    internal StepChoices(IReadOnlyList<StepPlace> places, CurrentStep[] steps)
    {
        Places = places;
        this.steps = steps;
    }

    /// <summary>The sequence's open-world places, each once, in data order; empty for a duty step or one with no place.</summary>
    public IReadOnlyList<StepPlace> Places { get; }

    /// <summary>The step aimed at the place nearest the player in the player's zone, else at the first (see <see cref="CurrentStepResolver.Nearest"/>).</summary>
    public CurrentStep Pick(uint playerTerritory = 0, float playerX = 0f, float playerZ = 0f) =>
        steps.Length == 1 ? steps[0] : steps[CurrentStepResolver.NearestIndex(Places, playerTerritory, playerX, playerZ)];
}

/// <summary>
/// Resolves the current step of a quest in the journal from its sequence (P2). Only the objectives completed in the
/// current sequence are read, so a later step is never named and its place is never aimed at. Places inside a duty's
/// territory are not travelled to (the duty is joined through the Duty Finder); several open-world places aim at the
/// one nearest the player in the player's zone, else at the first in the data's order. Pure.
/// </summary>
public static class CurrentStepResolver
{
    /// <param name="steps">The quest's objectives (<c>QuestStepReader</c>).</param>
    /// <param name="sequence">The quest's journal sequence (255 the last step); null or 0 resolves nothing.</param>
    /// <param name="stepCount">The quest's step count (<c>QuestRecord.StepCount</c>), for the step's number.</param>
    /// <param name="playerTerritory">The zone the player stands in; 0 when unknown (no nearest is picked).</param>
    /// <param name="playerX">The player's raw world x.</param>
    /// <param name="playerZ">The player's raw world z.</param>
    public static CurrentStep? Resolve(QuestSteps steps, byte? sequence, byte stepCount, uint playerTerritory = 0, float playerX = 0f, float playerZ = 0f) =>
        Choices(steps, sequence, stepCount)?.Pick(playerTerritory, playerX, playerZ);

    /// <summary>
    /// Everything <see cref="Resolve"/> can answer for one sequence, built once (lists, records); then
    /// <see cref="StepChoices.Pick"/> per position. Null for no sequence.
    /// </summary>
    public static StepChoices? Choices(QuestSteps steps, byte? sequence, byte stepCount)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (sequence is not { } seq || seq == 0)
        {
            return null;
        }

        var objectives = new List<StepObjective>();
        var places = new List<StepPlace>();
        var seen = new HashSet<uint>();
        uint dutyId = 0;
        foreach (var objective in steps.Objectives)
        {
            if (objective.Sequence != seq)
            {
                continue;
            }

            objectives.Add(objective);
            foreach (var place in objective.Places)
            {
                if (place.DutyId != 0)
                {
                    if (dutyId == 0)
                    {
                        dutyId = place.DutyId;
                    }

                    continue;
                }

                if (place.TerritoryId != 0 && seen.Add(place.LevelId))
                {
                    places.Add(place);
                }
            }
        }

        var step = JournalVisibility.CurrentStep(seq, stepCount);
        if (places.Count == 0)
        {
            return new StepChoices(places, [new CurrentStep(step, seq, objectives, dutyId != 0 ? StepPlaceKind.Duty : StepPlaceKind.NoPlace, null, 0, dutyId)]);
        }

        if (places.Count == 1)
        {
            var only = places[0];
            return new StepChoices(places, [new CurrentStep(step, seq, objectives, only.IsArea ? StepPlaceKind.Area : StepPlaceKind.OnePlace, only, 1, dutyId)]);
        }

        var each = new CurrentStep[places.Count];
        for (var i = 0; i < each.Length; i++)
        {
            each[i] = new CurrentStep(step, seq, objectives, StepPlaceKind.SeveralPlaces, places[i], places.Count, dutyId);
        }

        return new StepChoices(places, each);
    }

    /// <summary>The place nearest the player in the player's zone; the first in data order when none is in it.</summary>
    public static StepPlace Nearest(IReadOnlyList<StepPlace> places, uint playerTerritory, float playerX, float playerZ)
    {
        ArgumentNullException.ThrowIfNull(places);
        return places[NearestIndex(places, playerTerritory, playerX, playerZ)];
    }

    /// <summary>The index of <see cref="Nearest"/>'s place. Allocates nothing.</summary>
    internal static int NearestIndex(IReadOnlyList<StepPlace> places, uint playerTerritory, float playerX, float playerZ)
    {
        var best = 0;
        var bestDistance = float.MaxValue;
        var found = false;
        if (playerTerritory != 0)
        {
            for (var i = 0; i < places.Count; i++)
            {
                var place = places[i];
                if (place.TerritoryId != playerTerritory)
                {
                    continue;
                }

                var distance = TravelPlanner.Distance(place.X, place.Z, playerX, playerZ);
                if (!found || distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                    found = true;
                }
            }
        }

        return best;
    }
}
