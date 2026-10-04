using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Triad;

namespace Tsukimichi.Game;

/// <summary>The Triple Triad records of a capture (feature plan v7, 1.21.0 P6).</summary>
public sealed partial class GameStateReader
{
    // The last Triple Triad read: whose, under which opponent list, how many captures ago. Reused until one moves.
    private TriadRecordCapture? triadRecords;
    private ulong triadRecordsContentId;
    private int triadRecordsAge;
    private bool triadRecordsWarned;

    /// <summary>
    /// The Triple Triad opponents (built once from the sheets) whose beaten flags and cards a capture reads
    /// (<see cref="CharacterSnapshot.TriadRecords"/>); null, or an index not built yet, reads none.
    /// </summary>
    public Func<TriadOpponents?>? TriadIndex { get; set; }

    /// <summary>
    /// The Triple Triad card's records (1.21.0 P6): of the opponents the index lists, those the character has beaten
    /// (<c>UIState.IsTripleTriadNpcBeaten</c>, by <c>TripleTriadResident</c> row), and of their cards those it owns
    /// (<c>UIState.IsTripleTriadCardUnlocked</c>). About 390 flag reads, so, like the duty records, they are read only
    /// when something may have changed them: another character, a quest just completed, another list, or
    /// <see cref="CollectibleRefreshCaptures"/> captures since the last read. Null, so the card says "log in to read",
    /// while the <see cref="Gate"/> holds the hooks or the index has not landed.
    /// </summary>
    private unsafe TriadRecordCapture? ReadTriadRecords(UIState* ui, ulong contentId, bool completedChanged)
    {
        if (Gate is not { HooksAllowed: true } || ui == null || TriadIndex?.Invoke() is not { Count: > 0 } index)
        {
            return triadRecords = null;
        }

        triadRecordsAge++;
        if (triadRecords is { } last
            && contentId == triadRecordsContentId
            && last.Watch == index.Fingerprint
            && !completedChanged
            && triadRecordsAge < CollectibleRefreshCaptures)
        {
            return last;
        }

        try
        {
            var beaten = new List<uint>();
            foreach (var id in index.Watched)
            {
                if (ui->IsTripleTriadNpcBeaten(id))
                {
                    beaten.Add(id);
                }
            }

            var cards = new List<uint>();
            foreach (var id in index.WatchedCards)
            {
                if (id <= ushort.MaxValue && ui->IsTripleTriadCardUnlocked((ushort)id))
                {
                    cards.Add(id);
                }
            }

            triadRecordsAge = 0;
            var read = new TriadRecordCapture(index.Fingerprint, beaten, cards);
            if (contentId != triadRecordsContentId || !TriadRecordCapture.Same(triadRecords, read))
            {
                triadRecords = read;
            }

            triadRecordsContentId = contentId;
            return triadRecords;
        }
        catch (Exception ex)
        {
            if (!triadRecordsWarned)
            {
                triadRecordsWarned = true;
                log.Warning(ex, "The Triple Triad records could not be read; the Triple Triad card waits for the next read");
            }

            return triadRecords = null;
        }
    }
}
