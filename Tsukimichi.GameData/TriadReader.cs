using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Triad;

namespace Tsukimichi.GameData;

/// <summary>
/// The Triple Triad opponents (feature plan v7, 1.21.0 P6), read once from the sheets:
/// <list type="bullet">
/// <item><c>TripleTriad</c>: one row per opponent (rows 0x230000 up, the event id the NPC's talk opens):
/// <c>PreviousQuest[0..2]</c> with <c>PreviousQuestJoin</c> (2 any, else all), the quests the game wants done before
/// the NPC plays you, and <c>ItemPossibleReward[0..3]</c>, the card items it can hand out.</item>
/// <item><c>ENpcBase.ENpcData</c>: the NPC whose event data names the row; its name is <c>ENpcResident.Singular</c>.
/// A row no NPC opens (a tournament's, a test row) is left out.</item>
/// <item>Where the NPC stands: its <c>Level</c> row (type 8), else the zone's event layout (<c>planevent.lgb</c>),
/// as the item sources place a vendor (<see cref="ItemSourceIndex"/>).</item>
/// <item>A card item's card: <c>Item.ItemAction</c> of action type 3357 carries the <c>TripleTriadCard</c> row in
/// <c>Data[0]</c>.</item>
/// </list>
/// <c>TripleTriadResident</c> shares the row ids; the game's beaten flag (<c>UIState.IsTripleTriadNpcBeaten</c>) is
/// read by them.
/// </summary>
public static class TriadReader
{
    /// <summary><c>ItemAction.Action</c> of an item that adds a Triple Triad card.</summary>
    private const uint ActionTripleTriad = 3357;

    /// <summary>The first <c>TripleTriad</c> row id (the event handler id <c>0x23</c> in the high 16 bits).</summary>
    public const uint FirstRowId = 0x230000;

    /// <summary>
    /// Reads every opponent; with <paramref name="readLayout"/>, NPCs no <c>Level</c> row places are looked for in the
    /// zones' event layouts.
    /// </summary>
    public static TriadOpponents Build(ExcelModule excel, Language? language = null, Func<string, LgbFile?>? readLayout = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var triads = excel.GetSheet<TripleTriad>(language);
        var rows = new HashSet<uint>();
        foreach (var row in triads)
        {
            rows.Add(row.RowId);
        }

        // The NPC whose event data opens each row: the lowest NPC row id, so the pick is stable across reads.
        var npcOf = new Dictionary<uint, uint>();
        foreach (var npc in excel.GetSheet<ENpcBase>(language))
        {
            foreach (var data in npc.ENpcData)
            {
                if (data.RowId != 0 && rows.Contains(data.RowId) && (!npcOf.TryGetValue(data.RowId, out var known) || npc.RowId < known))
                {
                    npcOf[data.RowId] = npc.RowId;
                }
            }
        }

        var places = new ItemSourceIndex.Places(excel, language);
        var spots = ItemSourceIndex.NpcSpots(excel, language, npcOf.Values.ToHashSet(), places, readLayout);
        var residents = excel.GetSheet<ENpcResident>(language);
        var items = excel.GetSheet<Item>(language);
        var actions = excel.GetSheet<ItemAction>(language);
        var opponents = new List<TriadOpponent>(npcOf.Count);
        foreach (var row in triads)
        {
            if (!npcOf.TryGetValue(row.RowId, out var npcId) || residents.GetRowOrDefault(npcId) is not { } resident)
            {
                continue;
            }

            var name = resident.Singular.ExtractText().Trim();
            if (name.Length == 0)
            {
                continue;
            }

            var gate = new List<uint>(row.PreviousQuest.Count);
            foreach (var quest in row.PreviousQuest)
            {
                if (quest.RowId != 0 && !gate.Contains(quest.RowId))
                {
                    gate.Add(quest.RowId);
                }
            }

            var cards = new List<uint>(row.ItemPossibleReward.Count);
            foreach (var reward in row.ItemPossibleReward)
            {
                if (reward.RowId != 0 && CardOf(items, actions, reward.RowId) is var card && card != 0 && !cards.Contains(card))
                {
                    cards.Add(card);
                }
            }

            var prereq = gate.Count == 0 ? Prereq.None : new Prereq([.. gate], CatalogMapper.ToJoin(row.PreviousQuestJoin));
            opponents.Add(new TriadOpponent(row.RowId, npcId, name, spots.GetValueOrDefault(npcId), prereq, cards));
        }

        return new TriadOpponents(opponents);
    }

    /// <summary>The <c>TripleTriadCard</c> row a card item adds; 0 for an item that adds none.</summary>
    private static uint CardOf(ExcelSheet<Item> items, ExcelSheet<ItemAction> actions, uint itemId)
    {
        if (items.GetRowOrDefault(itemId) is not { } item || item.ItemAction.RowId == 0 || actions.GetRowOrDefault(item.ItemAction.RowId) is not { } action)
        {
            return 0;
        }

        return action.Action.RowId == ActionTripleTriad && action.Data.Count > 0 ? action.Data[0] : 0u;
    }
}
