using System.Globalization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// Judges a curated game gate (<see cref="QuestCatalog.GameGateOf"/>). A gate without weapons, or a character whose
/// capture cannot answer it (no <see cref="CharacterSnapshot.GateItems"/>, or one made against another weapon list),
/// stays "not checked": never met, never a block, so the quest reads Not checked where it would read Ready. A gear gate
/// with a capture is met when one group of its weapons is all equipped (or held), and unmet otherwise, naming what the
/// character has instead.
/// </summary>
public static class GameGateCheck
{
    /// <summary>The requirement result for <paramref name="gate"/> on <paramref name="s"/>, its detail in English.</summary>
    public static RequirementResult Evaluate(QuestGate gate, CharacterSnapshot s, QuestCatalog catalog, Func<uint, string> itemName)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(itemName);

        if (gate.Items is not { } items || s.GateItems is not { } capture || capture.Watch == 0 || capture.Watch != catalog.GateItemFingerprint)
        {
            return new(new GameGateRequirement(gate.Gate), false, $"needs {gate.Gate}, not checked");
        }

        var equipped = capture.Equipped.ToArray();
        var pool = items.Hold == GateHold.Equipped ? capture.Equipped : capture.Held;
        if (items.GroupIn(pool) is { } met)
        {
            var where = items.Hold == GateHold.Equipped ? "equipped" : "in your possession";
            return new(
                new GameGateRequirement(gate.Gate) { Checked = items.Hold, Equipped = equipped, Matching = met },
                true,
                $"{Names(met, itemName)} {where}");
        }

        if (items.Hold == GateHold.Held)
        {
            return new(new GameGateRequirement(gate.Gate) { Checked = items.Hold, Equipped = equipped }, false, $"needs {gate.Gate}, you have none");
        }

        // Worn elsewhere or carried: the weapon is there, it only needs equipping.
        if (items.GroupIn(capture.Held) is { } carried)
        {
            return new(
                new GameGateRequirement(gate.Gate) { Checked = items.Hold, Equipped = equipped, Matching = carried },
                false,
                $"needs {gate.Gate}, equip {Names(carried, itemName)}");
        }

        var detail = equipped.Length > 0
            ? $"needs {gate.Gate}, you have {Names(equipped, itemName)} equipped"
            : $"needs {gate.Gate}, none equipped";
        return new(new GameGateRequirement(gate.Gate) { Checked = items.Hold, Equipped = equipped }, false, detail);
    }

    /// <summary>"Curtana Nexus and Holy Shield Nexus"; an item without a name reads "item 8649".</summary>
    public static string Names(IReadOnlyList<uint> ids, Func<uint, string> itemName)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(itemName);
        return string.Join(" and ", ids.Select(id => itemName(id) is { Length: > 0 } name ? name : "item " + id.ToString(CultureInfo.InvariantCulture)));
    }
}
