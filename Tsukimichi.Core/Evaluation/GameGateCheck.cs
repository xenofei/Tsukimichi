using System.Globalization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// Judges a curated game gate (<see cref="QuestCatalog.GameGateOf"/>). A gate one of whose <see cref="QuestGate.MetBy"/>
/// quests is completed is met: the game gives that quest only once the gate is passed. An unlock-link gate is judged
/// from the links the capture read (<see cref="CharacterSnapshot.GateUnlockLinks"/>): met when every one is set, unmet
/// when one is read as not set, not checked when one was not read. A gear gate is met when one group of its weapons is
/// all equipped (or held), and unmet otherwise, naming what the character has instead; one whose capture cannot answer
/// it (no <see cref="CharacterSnapshot.GateItems"/>, or one made against another weapon list) is not checked. Any other
/// gate stays "not checked": never met, never a block, so the quest reads Not checked where it would read Ready.
/// </summary>
public static class GameGateCheck
{
    /// <summary>
    /// The requirement result for <paramref name="gate"/> on <paramref name="s"/>, its detail in English. With
    /// <paramref name="markedDone"/> (the player said "I've done this", feature plan v7 C3) a gate Tsukimichi cannot check
    /// reads met, "you said so"; a gate it judged keeps its own answer.
    /// </summary>
    public static RequirementResult Evaluate(QuestGate gate, CharacterSnapshot s, QuestCatalog catalog, Func<uint, string> itemName, bool markedDone = false)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(itemName);

        var result = Judge(gate, s, catalog, itemName);
        return markedDone && result.Req is GameGateRequirement { IsNotChecked: true }
            ? new(new GameGateRequirement(gate.Gate) { Judged = true, MarkedByYou = true, Sources = gate.Sources }, true, "you said so")
            : result with { Req = ((GameGateRequirement)result.Req) with { Sources = gate.Sources } };
    }

    private static RequirementResult Judge(QuestGate gate, CharacterSnapshot s, QuestCatalog catalog, Func<uint, string> itemName)
    {
        if (Proof(gate, s, catalog) is { } proof)
        {
            return proof;
        }

        if (gate.UnlockLinks is { } links)
        {
            return EvaluateLinks(gate.Gate, links, s);
        }

        if (gate.Items is not { } items || s.GateItems is not { } capture || capture.Watch == 0 || capture.Watch != catalog.GateItemFingerprint)
        {
            return NotChecked(gate.Gate);
        }

        var equipped = capture.Equipped.ToArray();
        var first = items.Groups.Length > 0 ? items.Groups[0] : [];
        var pool = items.Hold == GateHold.Equipped ? capture.Equipped : capture.Held;
        if (items.GroupIn(pool) is { } met)
        {
            var where = items.Hold == GateHold.Equipped ? "equipped" : "in your possession";
            return new(
                new GameGateRequirement(gate.Gate) { Checked = items.Hold, FirstGroup = first, Equipped = equipped, Matching = met },
                true,
                $"{Names(met, itemName)} {where}");
        }

        if (items.Hold == GateHold.Held)
        {
            return new(new GameGateRequirement(gate.Gate) { Checked = items.Hold, FirstGroup = first, Equipped = equipped }, false, $"needs {gate.Gate}, you have none");
        }

        // Worn elsewhere or carried: the weapon is there, it only needs equipping.
        if (items.GroupIn(capture.Held) is { } carried)
        {
            return new(
                new GameGateRequirement(gate.Gate) { Checked = items.Hold, FirstGroup = first, Equipped = equipped, Matching = carried },
                false,
                $"needs {gate.Gate}, equip {Names(carried, itemName)}");
        }

        var detail = equipped.Length > 0
            ? $"needs {gate.Gate}, you have {Names(equipped, itemName)} equipped"
            : $"needs {gate.Gate}, none equipped";
        return new(new GameGateRequirement(gate.Gate) { Checked = items.Hold, FirstGroup = first, Equipped = equipped }, false, detail);
    }

    /// <summary>"Curtana Nexus and Holy Shield Nexus"; an item without a name reads "item 8649".</summary>
    public static string Names(IReadOnlyList<uint> ids, Func<uint, string> itemName)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(itemName);
        return string.Join(" and ", ids.Select(id => itemName(id) is { Length: > 0 } name ? name : "item " + id.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>The gate met by its first completed <see cref="QuestGate.MetBy"/> quest; null when none is completed.</summary>
    private static RequirementResult? Proof(QuestGate gate, CharacterSnapshot s, QuestCatalog catalog)
    {
        foreach (var rowId in gate.MetBy)
        {
            if (s.IsCompleted(QuestRecord.ToQuestId(rowId)))
            {
                var name = catalog.GetByRowId(rowId)?.Name is { Length: > 0 } n ? n : "quest " + rowId.ToString(CultureInfo.InvariantCulture);
                return new(new GameGateRequirement(gate.Gate) { Judged = true }, true, $"{gate.Gate} ({name} done)");
            }
        }

        return null;
    }

    /// <summary>
    /// An unlock-link gate on the captured links: met when every one is set; unmet, with what is left, when one is read
    /// as not set; not checked when one was not read (a capture from before 1.19, or a link the watch list gained since).
    /// </summary>
    private static RequirementResult EvaluateLinks(string gate, uint[] links, CharacterSnapshot s)
    {
        if (s.GateUnlockLinks is not { } read)
        {
            return NotChecked(gate);
        }

        var missing = new List<uint>();
        foreach (var link in links)
        {
            if (Contains(read.Owned, link))
            {
                continue;
            }

            if (!Contains(read.Missing, link))
            {
                return NotChecked(gate);
            }

            missing.Add(link);
        }

        if (missing.Count == 0)
        {
            return new(new GameGateRequirement(gate) { Judged = true }, true, gate);
        }

        // What is left, never a tally (feature plan v6 U5): "needs …" alone when one link stands for the whole gate.
        var detail = links.Length == 1 ? $"needs {gate}" : $"needs {gate}, {missing.Count} left";
        return new(new GameGateRequirement(gate) { Judged = true, MissingLinks = [.. missing] }, false, detail);
    }

    private static RequirementResult NotChecked(string gate) => new(new GameGateRequirement(gate), false, $"needs {gate}, not checked");

    private static bool Contains(IReadOnlyList<uint> ids, uint id)
    {
        foreach (var x in ids)
        {
            if (x == id)
            {
                return true;
            }
        }

        return false;
    }
}
