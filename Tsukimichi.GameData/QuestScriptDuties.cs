using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Companions;

namespace Tsukimichi.GameData;

/// <summary>
/// What one quest's script says about duties, read from its <c>Quest.QuestParams</c> (the script's named constants).
/// <c>INSTANCEDUNGEONn</c> and <c>CONTENT_START</c> carry an InstanceContent row id, mapped here to the
/// ContentFinderCondition rows that link it (<c>ContentLinkType</c> 1). <c>UNLOCK_ADD_NEW_CONTENT_TO_CF</c> is the step
/// that shows "added to the Duty Finder"; an <c>UNLOCK_IMAGE_*</c> constant is the unlock banner. Either marks the
/// script as an unlock step.
/// </summary>
/// <param name="QuestRowId">The Quest sheet row.</param>
/// <param name="PrimaryDuties">CFC ids of the first <c>INSTANCEDUNGEON</c> constant and of <c>CONTENT_START</c>: the duty the
/// quest is about. Later <c>INSTANCEDUNGEON</c> constants are often duties of other steps (the relic quests name six).</param>
/// <param name="NamedDuties">CFC ids of every <c>INSTANCEDUNGEON</c> constant and <c>CONTENT_START</c>, in script order.</param>
/// <param name="UnmappedInstances">InstanceContent ids those constants name that no named CFC row links (quest battles such
/// as Cape Westwind's 20007).</param>
/// <param name="AddsToDutyFinder">The script runs <c>UNLOCK_ADD_NEW_CONTENT_TO_CF</c>.</param>
/// <param name="ShowsUnlockImage">The script names an <c>UNLOCK_IMAGE_*</c> constant.</param>
public sealed record QuestScriptDuty(
    uint QuestRowId,
    IReadOnlyList<uint> PrimaryDuties,
    IReadOnlyList<uint> NamedDuties,
    IReadOnlyList<uint> UnmappedInstances,
    bool AddsToDutyFinder,
    bool ShowsUnlockImage)
{
    /// <summary>The script announces a new duty (Duty Finder message or unlock banner).</summary>
    public bool IsUnlockStep => AddsToDutyFinder || ShowsUnlockImage;

    /// <summary>
    /// The duties the script rule says the quest unlocks: its <see cref="PrimaryDuties"/> when it
    /// <see cref="IsUnlockStep">announces a duty</see>, else none. Against the wiki's quest pages the rule's only false
    /// pairs came from removed legacy rows (Rock the Castrum 66672, Levin an Impression 66988, whose current rows are
    /// curated), so callers leave out quests the catalog marks <c>IsRemoved</c> or <c>IsRetired</c>; over the live
    /// quests every pair it yields is on the wiki (docs/data/v4/tagging-audit.md finding 4).
    /// </summary>
    public IReadOnlyList<uint> UnlockedDuties => IsUnlockStep ? PrimaryDuties : [];
}

/// <summary>
/// Reads the duty evidence of every quest script (<see cref="QuestScriptDuty"/>). Standalone (takes an
/// <see cref="ExcelModule"/>) so tests and tools can run it against game data without Dalamud. Quests whose script names
/// no duty and announces nothing are left out.
/// </summary>
public static class QuestScriptDuties
{
    public const string InstanceDungeonPrefix = "INSTANCEDUNGEON";
    public const string ContentStart = "CONTENT_START";
    public const string AddNewContentToDutyFinder = "UNLOCK_ADD_NEW_CONTENT_TO_CF";
    public const string UnlockImagePrefix = "UNLOCK_IMAGE";

    /// <summary>Quest row id to its script's duty evidence.</summary>
    public static IReadOnlyDictionary<uint, QuestScriptDuty> Build(ExcelModule excel, Language? language = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var dutiesByInstance = DutiesByInstance(excel, language);
        var result = new Dictionary<uint, QuestScriptDuty>();
        foreach (var quest in excel.GetSheet<Quest>(language))
        {
            var constants = new List<(string Name, uint Arg)>();
            foreach (var param in quest.QuestParams)
            {
                var name = param.ScriptInstruction.ExtractText();
                if (name.Length > 0)
                {
                    constants.Add((name, param.ScriptArg));
                }
            }

            var duty = Read(quest.RowId, constants, dutiesByInstance);
            if (duty is not null)
            {
                result[quest.RowId] = duty;
            }
        }

        return result;
    }

    /// <summary>
    /// The Duty Finder entries one quest's script names (its <c>INSTANCEDUNGEON</c> and <c>CONTENT_START</c> constants),
    /// in script order, as <paramref name="duties"/> knows them: what the duty guard (plan v7 A3) reads when Questionable
    /// reaches a duty step of the quest, since Questionable's step data does not say which duty. Reads one Quest row;
    /// empty for an unknown row or a script that names no duty <paramref name="duties"/> has (a quest battle).
    /// </summary>
    public static IReadOnlyList<DutyRunInfo> NamedRuns(ExcelModule excel, Language language, uint questRowId, DutyRunIndex duties)
    {
        ArgumentNullException.ThrowIfNull(excel);
        ArgumentNullException.ThrowIfNull(duties);
        if (excel.GetSheet<Quest>(language).GetRowOrDefault(questRowId) is not { } quest)
        {
            return [];
        }

        List<DutyRunInfo>? result = null;
        foreach (var param in quest.QuestParams)
        {
            if (param.ScriptArg == 0)
            {
                continue;
            }

            var name = param.ScriptInstruction.ExtractText();
            if (!name.StartsWith(InstanceDungeonPrefix, StringComparison.Ordinal) && !name.StartsWith(ContentStart, StringComparison.Ordinal))
            {
                continue;
            }

            if (duties.ByInstance(param.ScriptArg) is { } duty && !(result?.Contains(duty) ?? false))
            {
                (result ??= []).Add(duty);
            }
        }

        return result ?? (IReadOnlyList<DutyRunInfo>)[];
    }

    /// <summary>
    /// InstanceContent row id to the named ContentFinderCondition rows that link it, in sheet order.
    /// </summary>
    public static IReadOnlyDictionary<uint, IReadOnlyList<uint>> DutiesByInstance(ExcelModule excel, Language? language = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var map = new Dictionary<uint, List<uint>>();
        foreach (var row in excel.GetSheet<ContentFinderCondition>(language))
        {
            if (row.ContentLinkType != DutyIndex.InstanceContentLink || row.Content.RowId == 0 || row.Name.ExtractText().Trim().Length == 0)
            {
                continue;
            }

            if (!map.TryGetValue(row.Content.RowId, out var list))
            {
                map[row.Content.RowId] = list = [];
            }

            list.Add(row.RowId);
        }

        return map.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<uint>)kv.Value);
    }

    /// <summary>
    /// One script's evidence from its (constant name, argument) pairs in script order; null when it names no duty
    /// instance and announces nothing.
    /// </summary>
    public static QuestScriptDuty? Read(uint questRowId, IEnumerable<(string Name, uint Arg)> constants, IReadOnlyDictionary<uint, IReadOnlyList<uint>> dutiesByInstance)
    {
        ArgumentNullException.ThrowIfNull(constants);
        ArgumentNullException.ThrowIfNull(dutiesByInstance);
        var primary = new List<uint>();
        var named = new List<uint>();
        var unmapped = new List<uint>();
        var addsToDutyFinder = false;
        var showsImage = false;
        var sawDungeon = false;
        var any = false;
        foreach (var (name, arg) in constants)
        {
            if (name.StartsWith(AddNewContentToDutyFinder, StringComparison.Ordinal))
            {
                addsToDutyFinder = true;
                continue;
            }

            if (name.StartsWith(UnlockImagePrefix, StringComparison.Ordinal))
            {
                showsImage = true;
                continue;
            }

            var isDungeon = name.StartsWith(InstanceDungeonPrefix, StringComparison.Ordinal);
            if (!isDungeon && !name.StartsWith(ContentStart, StringComparison.Ordinal))
            {
                continue;
            }

            any = true;
            var isPrimary = !isDungeon || !sawDungeon;
            sawDungeon |= isDungeon;
            if (arg == 0)
            {
                continue;
            }

            if (!dutiesByInstance.TryGetValue(arg, out var duties) || duties.Count == 0)
            {
                AddOnce(unmapped, arg);
                continue;
            }

            foreach (var cfc in duties)
            {
                AddOnce(named, cfc);
                if (isPrimary)
                {
                    AddOnce(primary, cfc);
                }
            }
        }

        return any || addsToDutyFinder || showsImage
            ? new QuestScriptDuty(questRowId, primary, named, unmapped, addsToDutyFinder, showsImage)
            : null;
    }

    private static void AddOnce(List<uint> list, uint value)
    {
        if (!list.Contains(value))
        {
            list.Add(value);
        }
    }
}
