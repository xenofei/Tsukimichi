using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Triad;

/// <summary>Which group of the Triple Triad card an opponent falls in.</summary>
public enum TriadGroup : byte
{
    /// <summary>The opponent's gate is open (or it has none) and it still has something for you: not beaten, or cards you lack.</summary>
    PlaysYou,

    /// <summary>A quest the character has not done keeps the opponent from playing, or it stands past the story point.</summary>
    Locked,
}

/// <summary>One opponent on the Triple Triad card for one character.</summary>
/// <param name="Opponent">The opponent.</param>
/// <param name="Group">Where the card lists it.</param>
/// <param name="Beaten">Beaten once (the game's record); null when the capture holds no records.</param>
/// <param name="CardsLeft">Its cards the character does not own; -1 when the capture holds no records.</param>
/// <param name="Quest">
/// For a locked opponent, the quest the card names after "after": of an any join the alternative closest to done (Ready,
/// then in the journal, then the rest, lowest level first), of an all join the first not done in the same order. Null
/// for an opponent without a quest gate, or one whose gate is open.
/// </param>
/// <param name="Masked">
/// The opponent lies past the story point (the spoiler shield): its gate's quests are masked (every alternative of an
/// any join, one of an all join), or the zone it stands in is. Its name, place and quest are never shown.
/// </param>
public sealed record TriadRow(TriadOpponent Opponent, TriadGroup Group, bool? Beaten, int CardsLeft, QuestRecord? Quest, bool Masked)
{
    /// <summary>Known to have every card owned and to be beaten: the opponent leaves the list.</summary>
    public bool IsDone => Beaten == true && CardsLeft == 0;
}

/// <summary>The Triple Triad card for one character (1.21.0 P6).</summary>
/// <param name="PlaysYou">Open opponents with something left, not beaten first, then most cards left, then by name.</param>
/// <param name="Locked">
/// Opponents behind a quest: those that can be shown first (a Ready quest, then one in the journal, then the rest, lowest
/// level first), then the masked ones (past the story point) by the level of their quest.
/// </param>
/// <param name="Done">Opponents beaten with every card owned: only the caption's hover counts them.</param>
/// <param name="HasRecords">The capture holds the beaten and card records (a login reads them).</param>
public sealed record TriadBoardModel(IReadOnlyList<TriadRow> PlaysYou, IReadOnlyList<TriadRow> Locked, int Done, bool HasRecords)
{
    public static readonly TriadBoardModel Empty = new([], [], 0, false);

    /// <summary>"11 opponents to unlock": every locked opponent, masked ones included.</summary>
    public int ToUnlock => Locked.Count;

    /// <summary>Opponents on the card with cards the character does not own ("Cards left 9").</summary>
    public int WithCardsLeft => PlaysYou.Count(static r => r.CardsLeft > 0) + Locked.Count(static r => r.CardsLeft > 0);

    /// <summary>The locked opponents the card names (not masked), in order.</summary>
    public IEnumerable<TriadRow> Named => Locked.Where(static r => !r.Masked);
}

/// <summary>Which rows the card's chips keep: a row stays when any chip it matches is on.</summary>
/// <param name="PlaysYou">"Plays you": the open opponents.</param>
/// <param name="Locked">"Locked": the opponents behind a quest.</param>
/// <param name="CardsLeft">"Cards left": opponents with cards the character does not own, in either group.</param>
public readonly record struct TriadChips(bool PlaysYou = true, bool Locked = true, bool CardsLeft = true)
{
    public static readonly TriadChips All = new(true, true, true);

    public bool Keeps(TriadRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return (PlaysYou && row.Group == TriadGroup.PlaysYou) || (Locked && row.Group == TriadGroup.Locked) || (CardsLeft && row.CardsLeft > 0);
    }
}

/// <summary>
/// The Triple Triad card (feature plan v7, 1.21.0 P6; spec-1.21 P6): which opponents play the character and still have
/// something for it, and which wait on a quest the game never names, with that quest. Pure.
/// <para>
/// <b>Open.</b> An opponent with no quest gate is open; one with an any join is open once any of its quests is done, an
/// all join once every one is. "Done" is Completed (a repeatable done this cycle counts).
/// </para>
/// <para>
/// <b>Left out.</b> An opponent the data does not place (the eight tournament rows), one whose every gate quest lies on
/// a path the character did not take (another Grand Company's, locked out for good), and one known beaten with every
/// card owned (counted in <see cref="TriadBoardModel.Done"/>).
/// </para>
/// <para>
/// <b>Past the story point.</b> An opponent is masked when its gate is (every alternative of an any join masked, one
/// quest of an all join) or its zone is: such an opponent is listed with the locked ones, whatever its gate says,
/// since the character cannot reach it yet, and the card names neither it, its place nor its quest.
/// </para>
/// </summary>
public static class TriadBoard
{
    /// <summary>
    /// The card for the character whose states are <paramref name="states"/>. <paramref name="records"/> is read only
    /// when it was made against <paramref name="index"/>'s list (<see cref="TriadOpponents.Fingerprint"/>); otherwise,
    /// or when null, beaten and cards are unknown and every open opponent is listed.
    /// </summary>
    /// <param name="questMasked">Whether the spoiler shield hides a quest (by row id); null hides none.</param>
    /// <param name="zoneMasked">Whether the spoiler shield hides a zone (by name); null hides none.</param>
    public static TriadBoardModel Build(
        TriadOpponents index,
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        TriadRecordCapture? records,
        Func<uint, bool>? questMasked = null,
        Func<string, bool>? zoneMasked = null)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        if (index.Count == 0)
        {
            return TriadBoardModel.Empty;
        }

        var read = records is not null && records.Watch == index.Fingerprint ? records : null;
        var beaten = read is null ? null : new HashSet<uint>(read.Beaten ?? []);
        var owned = read is null ? null : new HashSet<uint>(read.Cards ?? []);
        questMasked ??= static _ => false;
        zoneMasked ??= static _ => false;

        var plays = new List<TriadRow>();
        var locked = new List<TriadRow>();
        var done = 0;
        foreach (var opponent in index.All)
        {
            if (opponent.Spot is null || OnOtherPath(opponent.Gate, states))
            {
                continue;
            }

            bool? isBeaten = beaten is null ? null : beaten.Contains(opponent.ResidentId);
            var cardsLeft = owned is null ? -1 : opponent.Cards.Count(c => !owned.Contains(c));
            var masked = IsMasked(opponent.Gate, questMasked) || (opponent.Zone.Length > 0 && zoneMasked(opponent.Zone));
            var open = IsOpen(opponent.Gate, states);
            if (open && !masked)
            {
                var row = new TriadRow(opponent, TriadGroup.PlaysYou, isBeaten, cardsLeft, null, false);
                if (row.IsDone)
                {
                    done++;
                }
                else
                {
                    plays.Add(row);
                }

                continue;
            }

            locked.Add(new TriadRow(opponent, TriadGroup.Locked, isBeaten, cardsLeft, open ? null : ShownQuest(opponent.Gate, catalog, states), masked));
        }

        plays.Sort(static (a, b) =>
        {
            var byBeaten = (a.Beaten == true).CompareTo(b.Beaten == true);
            if (byBeaten != 0)
            {
                return byBeaten;
            }

            var byCards = b.CardsLeft.CompareTo(a.CardsLeft);
            return byCards != 0 ? byCards : string.Compare(a.Opponent.Name, b.Opponent.Name, StringComparison.OrdinalIgnoreCase);
        });
        locked.Sort((a, b) =>
        {
            var byMask = a.Masked.CompareTo(b.Masked);
            if (byMask != 0)
            {
                return byMask;
            }

            var byState = Rank(StateOf(a.Quest, states)).CompareTo(Rank(StateOf(b.Quest, states)));
            if (!a.Masked && byState != 0)
            {
                return byState;
            }

            var byLevel = (a.Quest?.DisplayLevel ?? 0).CompareTo(b.Quest?.DisplayLevel ?? 0);
            return byLevel != 0 ? byLevel : a.Opponent.ResidentId.CompareTo(b.Opponent.ResidentId);
        });
        return new TriadBoardModel(plays, locked, done, read is not null);
    }

    /// <summary>Whether the gate lets the opponent play: no quests, any one done (any join) or every one done (all join).</summary>
    public static bool IsOpen(Prereq gate, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(states);
        if (gate.IsEmpty)
        {
            return true;
        }

        return gate.Join == JoinKind.Any ? gate.QuestIds.Any(q => Done(q, states)) : gate.QuestIds.All(q => Done(q, states));
    }

    /// <summary>
    /// Whether the gate lies past the story point: every alternative of an any join masked, any quest of an all join
    /// (the opponent needs it). A gate with no quests never is.
    /// </summary>
    public static bool IsMasked(Prereq gate, Func<uint, bool> questMasked)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(questMasked);
        if (gate.IsEmpty)
        {
            return false;
        }

        return gate.Join == JoinKind.Any ? gate.QuestIds.All(questMasked) : gate.QuestIds.Any(questMasked);
    }

    /// <summary>
    /// The quest the card names for a gate not open: of an any join the alternative closest to done, of an all join
    /// the first not done in the same order (Ready, in the journal, Ready on another job, Blocked, the rest; then the
    /// lowest level, then the order the sheet gives). Alternatives on a path the character did not take come last.
    /// Null when the catalog knows none of them.
    /// </summary>
    public static QuestRecord? ShownQuest(Prereq gate, QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        QuestRecord? best = null;
        var bestKey = (Path: int.MaxValue, Rank: int.MaxValue, Level: int.MaxValue, Order: int.MaxValue);
        for (var i = 0; i < gate.QuestIds.Length; i++)
        {
            var id = gate.QuestIds[i];
            if (Done(id, states) || catalog.GetByRowId(id) is not { } quest)
            {
                continue;
            }

            var evaluation = states.GetValueOrDefault(id);
            var key = (evaluation is { LeavesTotals: true } ? 1 : 0, Rank(evaluation?.State ?? QuestState.Unknown), (int)quest.DisplayLevel, i);
            if (key.CompareTo(bestKey) < 0)
            {
                best = quest;
                bestKey = key;
            }
        }

        return best;
    }

    /// <summary>Every quest of the gate lies on a path the character did not take, and none is done: the opponent can never play it.</summary>
    private static bool OnOtherPath(Prereq gate, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        if (gate.IsEmpty)
        {
            return false;
        }

        var lockedOut = 0;
        foreach (var id in gate.QuestIds)
        {
            if (states.TryGetValue(id, out var evaluation) && evaluation.State == QuestState.Foreclosed)
            {
                lockedOut++;
            }
        }

        return gate.Join == JoinKind.Any ? lockedOut == gate.QuestIds.Length : lockedOut > 0;
    }

    private static bool Done(uint rowId, IReadOnlyDictionary<uint, QuestEvaluation> states) =>
        states.TryGetValue(rowId, out var evaluation) && evaluation.State is QuestState.Completed or QuestState.DoneThisCycle;

    private static QuestState StateOf(QuestRecord? quest, IReadOnlyDictionary<uint, QuestEvaluation> states) =>
        quest is not null && states.TryGetValue(quest.RowId, out var evaluation) ? evaluation.State : QuestState.Unknown;

    /// <summary>Closest to done first: Ready, in the journal, Ready on another job, Blocked, Not checked, Locked out.</summary>
    public static int Rank(QuestState state) => state switch
    {
        QuestState.Ready => 0,
        QuestState.Accepted => 1,
        QuestState.ReadyOnOtherJob => 2,
        QuestState.Blocked => 3,
        QuestState.Foreclosed => 5,
        _ => 4,
    };
}
