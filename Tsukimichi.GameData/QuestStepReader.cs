using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Travel;

namespace Tsukimichi.GameData;

/// <summary>
/// A quest's objectives and where each leads (plan v7, 1.21.0 P2), from the Quest sheet: every
/// <c>TodoParams[i]</c> with a <c>ToDoCompleteSeq</c> (the journal sequence it is completed in, 255 the last step) is
/// objective <c>i</c>, whose text is the quest text sheet's <c>TEXT_&lt;ID&gt;_TODO_i</c> (<see cref="QuestTextReader"/>);
/// its <c>ToDoQty</c> is the quantity and each non-zero <c>ToDoLocation</c> is a <c>Level</c> row: the territory and map
/// (<c>Level.Territory</c>, <c>Level.Map</c>), the raw world position (<c>Level.X</c>, <c>Y</c>, <c>Z</c>) and the
/// circle's <c>Radius</c>. A Level row of type 8 is a person (<c>Level.Object</c> an ENpcResident, whose
/// <c>Singular</c> is the name); any other is a marked area or object. A place whose territory is a duty's
/// (<c>TerritoryType.ContentFinderCondition</c>) lies inside that duty. Standalone (an <see cref="ExcelModule"/>) so
/// tests read real quests without Dalamud.
/// </summary>
public static class QuestStepReader
{
    /// <summary>Level.Type of an event NPC standing at the spot.</summary>
    public const byte NpcLevelType = 8;

    /// <summary>The quest's objectives; <see cref="QuestSteps.Empty"/> when the row does not exist.</summary>
    public static QuestSteps Read(ExcelModule excel, uint questRowId, Language? language = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        if (excel.GetSheet<Quest>(language).GetRowOrDefault(questRowId) is not { } quest)
        {
            return QuestSteps.Empty;
        }

        var levels = excel.GetSheet<Level>(language);
        var npcs = excel.GetSheet<ENpcResident>(language);
        var objectives = new List<StepObjective>();
        var todos = quest.TodoParams;
        for (var i = 0; i < todos.Count; i++)
        {
            var todo = todos[i];
            if (todo.ToDoCompleteSeq == 0)
            {
                continue;
            }

            var places = new List<StepPlace>();
            foreach (var location in todo.ToDoLocation)
            {
                if (location.RowId == 0 || levels.GetRowOrDefault(location.RowId) is not { } level || level.Territory.RowId == 0)
                {
                    continue;
                }

                var npc = level.Type == NpcLevelType && npcs.GetRowOrDefault(level.Object.RowId) is { } resident ? resident.Singular.ExtractText() : string.Empty;
                var duty = level.Territory.ValueNullable?.ContentFinderCondition.RowId ?? 0;
                places.Add(new StepPlace(
                    level.RowId,
                    level.Territory.RowId,
                    level.Map.RowId,
                    level.X,
                    level.Y,
                    level.Z,
                    level.Radius,
                    npc.Length == 0,
                    npc.Length == 0 ? 0 : level.Object.RowId,
                    npc,
                    duty));
            }

            objectives.Add(new StepObjective(i, todo.ToDoCompleteSeq, todo.ToDoQty, places));
        }

        return new QuestSteps(questRowId, objectives);
    }
}
