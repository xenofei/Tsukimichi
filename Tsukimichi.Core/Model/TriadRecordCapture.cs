namespace Tsukimichi.Core.Model;

/// <summary>
/// The Triple Triad records a capture read for the Triple Triad card (1.21.0 P6, <see cref="CharacterSnapshot.TriadRecords"/>):
/// which opponents the character has beaten once (<c>UIState.IsTripleTriadNpcBeaten</c>, by <c>TripleTriadResident</c>
/// row) and which of the opponents' cards it owns (<c>UIState.IsTripleTriadCardUnlocked</c>), each list ascending and
/// distinct.
/// </summary>
/// <param name="Watch"><see cref="GateItemCapture.Fingerprint"/> of the opponent and card list the capture read (<see cref="Triad.TriadOpponents.Fingerprint"/>); a board reads the capture only when it matches.</param>
/// <param name="Beaten">The watched opponents (resident row ids) the character has beaten at least once.</param>
/// <param name="Cards">The watched cards (<c>TripleTriadCard</c> row ids) the character owns.</param>
public sealed record TriadRecordCapture(uint Watch, IReadOnlyList<uint> Beaten, IReadOnlyList<uint> Cards)
{
    /// <summary>Same watch list and the same records; the lists compare in order (captures write them sorted).</summary>
    public static bool Same(TriadRecordCapture? a, TriadRecordCapture? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null || a.Watch != b.Watch)
        {
            return false;
        }

        return a.Beaten.SequenceEqual(b.Beaten) && a.Cards.SequenceEqual(b.Cards);
    }
}
