using System.Collections.Frozen;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Tsukimichi.GameData;

/// <summary>
/// The quests the game's New Game+ can replay (feature plan v5, collector extras; R9 F7): every quest some
/// <c>QuestRedo</c> row lists. Each row is one part of a New Game+ chapter (<c>QuestRedo.Chapter</c> names its
/// <c>QuestRedoChapterUI</c> row: "A Realm Reborn - Part 1", "Hildibrand, Agent of Enquiry", "Carpenter - Part 2"…) and
/// lists up to 32 quests in <c>QuestRedoParam</c>; a chapter split by start city or class has one row per variant. With
/// the 7.x sheets 2,188 quests: the main scenario (all but the eight "Close to Home" quests of the starting classes),
/// the Chronicles of a New Era, the side stories (Hildibrand, the Scholasticate, Tales of the Dragonsong War, the
/// Chronicles of Light), the class, job and role quests, and the crafter and gatherer quests.
/// <see cref="Core.Query.NewGamePlus"/> interprets the set.
/// </summary>
public static class NewGamePlusQuests
{
    /// <summary>Every Quest sheet row id a New Game+ chapter lists.</summary>
    public static FrozenSet<uint> Read(ExcelModule excel)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var ids = new HashSet<uint>();
        foreach (var row in excel.GetSheet<QuestRedo>())
        {
            foreach (var param in row.QuestRedoParam)
            {
                if (param.Quest.RowId != 0)
                {
                    ids.Add(param.Quest.RowId);
                }
            }
        }

        return ids.ToFrozenSet();
    }
}
