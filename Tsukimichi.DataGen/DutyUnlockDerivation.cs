using Tsukimichi.Core.Model;
using Tsukimichi.GameData;

namespace Tsukimichi.DataGen;

/// <summary>
/// The duty unlocks DataGen derives beyond the sheet links (<c>Quest.InstanceContentUnlock</c>,
/// <c>ContentFinderCondition.UnlockCriteria</c>), and the sheet links it drops (docs/data/v4/tagging-audit.md finding 4):
/// <list type="bullet">
/// <item>the quest-script rule (<see cref="QuestScriptDuty.UnlockedDuties"/>: the script's primary
/// <c>INSTANCEDUNGEON</c>/<c>CONTENT_START</c> duty when it runs <c>UNLOCK_ADD_NEW_CONTENT_TO_CF</c> or names an
/// <c>UNLOCK_IMAGE</c>), over live quests only: the removed legacy rows it would also credit (Rock the Castrum 66672,
/// Levin an Impression 66988) have current rows in <c>curated/duty_unlocks.json</c>;</item>
/// <item>the Heavensward Diadem (<see cref="RetiredDiademContentType"/>): its seven rows still name Heavensward (67205)
/// in <c>UnlockCriteria</c>, but the content left the game when the Firmament's Diadem (CFC 722, Towards the Firmament)
/// replaced it, so they credited the quest with duties nobody can enter instead of its real ones.</item>
/// </list>
/// Everything else the wiki knows and the data does not is seeded in <c>curated/duty_unlocks.json</c>.
/// </summary>
internal static class DutyUnlockDerivation
{
    /// <summary>Source text of a script-derived entry.</summary>
    public const string ScriptSource = "QuestScript";

    /// <summary>
    /// <c>ContentFinderCondition.ContentType</c> of the Heavensward Diadem (CFC 131, 132, 133, 202, 203, 225, 234), a
    /// content type without a name: the old Diadem was retired and the current one (CFC 722) is Disciples of the Land.
    /// </summary>
    public const uint RetiredDiademContentType = 23;

    /// <summary>
    /// Adds a static <see cref="RewardKind.DutyUnlock"/> entry for every script-rule pair the generator does not hold yet;
    /// quests the catalog marks removed or retired are skipped. Run before the curated overlay, which wins on a clash.
    /// Returns how many entries were added.
    /// </summary>
    public static int AddScriptUnlocks(GameSheets sheets, QuestCatalog catalog, UniqueRewardGenerator generator, TextWriter log)
    {
        var scripts = QuestScriptDuties.Build(sheets.Data.Excel);
        var existing = generator.Entries
            .Where(e => e.Kind == RewardKind.DutyUnlock)
            .Select(e => (e.QuestRowId, e.RewardId))
            .ToHashSet();
        var added = 0;
        foreach (var script in scripts.Values.OrderBy(s => s.QuestRowId))
        {
            if (script.UnlockedDuties.Count == 0)
                continue;

            if (catalog.GetByRowId(script.QuestRowId) is not { IsRemoved: false, IsRetired: false })
            {
                log.WriteLine($"script:  {script.QuestRowId} names duty {string.Join(", ", script.UnlockedDuties)} but is removed from the game; skipped.");
                continue;
            }

            foreach (var cfc in script.UnlockedDuties)
            {
                var name = sheets.ContentFinderConditions.GetRowOrDefault(cfc) is { } row ? UniqueRewardGenerator.Text(row.Name) : string.Empty;
                if (name.Length == 0 || !existing.Add((script.QuestRowId, cfc)))
                    continue;

                var how = script.AddsToDutyFinder ? QuestScriptDuties.AddNewContentToDutyFinder : QuestScriptDuties.UnlockImagePrefix;
                // AddCurated only stores the entry; the entry itself stays Static (derived from the game files).
                generator.AddCurated(new UniqueRewardEntry(script.QuestRowId, RewardKind.DutyUnlock, cfc, 0, name, Confidence.Static, $"{ScriptSource};primary duty;{how}"));
                added++;
            }
        }

        log.WriteLine($"script:  {added} duty unlocks from the quest-script rule.");
        return added;
    }

    /// <summary>The entries without duty unlocks of the retired Heavensward Diadem.</summary>
    public static List<UniqueRewardEntry> WithoutRetiredDuties(IEnumerable<UniqueRewardEntry> entries, GameSheets sheets, TextWriter log)
    {
        var kept = new List<UniqueRewardEntry>();
        var dropped = 0;
        foreach (var entry in entries)
        {
            if (entry.Kind == RewardKind.DutyUnlock
                && sheets.ContentFinderConditions.GetRowOrDefault(entry.RewardId) is { } row
                && row.ContentType.RowId == RetiredDiademContentType)
            {
                dropped++;
                continue;
            }

            kept.Add(entry);
        }

        log.WriteLine($"script:  {dropped} duty unlocks of the retired Heavensward Diadem dropped.");
        return kept;
    }
}
