using System.Collections.Frozen;
using Lumina.Data;
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

    /// <summary>
    /// The chapters as parts (feature plan v7, 1.19.0, C4): each <c>QuestRedo</c> row with its chapter, the row that
    /// follows it (the sheet's link column, unnamed in Lumina 7.5.0 as <c>Unknown1</c>; 0 at a chapter's end) and its
    /// quests in order with their variant masks, and each <c>QuestRedoChapterUI</c> name in
    /// <paramref name="language"/>. <see cref="Core.Query.NewGamePlusChapters"/> interprets them.
    /// </summary>
    public static Core.Query.NewGamePlusChapters ReadChapters(ExcelModule excel, Language language)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var parts = new List<Core.Query.NewGamePlusPart>();
        foreach (var row in excel.GetSheet<QuestRedo>())
        {
            if (row.Chapter.RowId == 0)
            {
                continue;
            }

            var quests = new List<Core.Query.NewGamePlusStep>();
            foreach (var param in row.QuestRedoParam)
            {
                if (param.Quest.RowId != 0)
                {
                    quests.Add(new Core.Query.NewGamePlusStep(param.Quest.RowId, param.UnknownParam));
                }
            }

            if (quests.Count > 0)
            {
                parts.Add(new Core.Query.NewGamePlusPart(row.RowId, row.Chapter.RowId, row.Unknown1, quests));
            }
        }

        var names = new Dictionary<uint, string>();
        foreach (var chapter in excel.GetSheet<QuestRedoChapterUI>(language))
        {
            var name = chapter.ChapterName.ExtractText();
            if (name.Length > 0)
            {
                names[chapter.RowId] = name;
            }
        }

        return new Core.Query.NewGamePlusChapters(parts, names);
    }
}
