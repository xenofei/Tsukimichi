using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Tsukimichi.GameData;

/// <summary>
/// A quest script's named constants (<c>Quest.QuestParams</c>: <c>ScriptInstruction</c> and <c>ScriptArg</c>), the values
/// its script works with: the actors it places, the items it hands over, and every other quest it checks
/// (<c>QST_CHECK_…</c>, <c>COMP_…</c>, <c>QUEST0</c>, …). A quest the script must check, it names here.
/// The verifier's prerequisite rule (1.22.0) reads it: a quest the wiki names as a prerequisite that neither the sheet
/// (previous quests, accept conditions) nor these constants name is one the game cannot be checking, so the wiki is
/// wrong (<c>sourceWrong</c>).
/// </summary>
public static class QuestScriptConstants
{
    /// <summary>The runtime quest id is the Quest sheet row id less this.</summary>
    private const uint RowIdOffset = 65536;

    /// <summary>The named constants of <paramref name="questRowId"/>'s script, in sheet order; empty for a missing row.</summary>
    public static IReadOnlyList<(string Name, uint Value)> Read(ExcelModule excel, uint questRowId, Language language = Language.English)
    {
        ArgumentNullException.ThrowIfNull(excel);
        if (excel.GetSheet<Quest>(language).GetRowOrDefault(questRowId) is not { } quest)
        {
            return [];
        }

        var list = new List<(string, uint)>();
        foreach (var param in quest.QuestParams)
        {
            var name = param.ScriptInstruction.ExtractText();
            if (name.Length > 0)
            {
                list.Add((name, param.ScriptArg));
            }
        }

        return list;
    }

    /// <summary>
    /// Whether any constant's value is <paramref name="questRowId"/>, as a Quest sheet row id or as the runtime quest id
    /// (the row id less 65536). Counting a runtime id that only happens to equal another constant's value errs toward
    /// "named", which keeps a finding open rather than closing it.
    /// </summary>
    public static bool NamesQuest(IEnumerable<(string Name, uint Value)> constants, uint questRowId)
    {
        ArgumentNullException.ThrowIfNull(constants);
        var runtime = questRowId >= RowIdOffset ? questRowId - RowIdOffset : uint.MaxValue;
        return constants.Any(c => c.Value == questRowId || c.Value == runtime);
    }
}
