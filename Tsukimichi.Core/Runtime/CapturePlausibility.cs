using System.Globalization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>What <see cref="CapturePlausibility.Check"/> made of a capture.</summary>
public enum PlausibilityVerdict
{
    /// <summary>Nothing suspicious: commit and save it.</summary>
    Plausible,

    /// <summary>The capture reads as an empty character (no completion bit, empty journal) while the last one had data.</summary>
    EmptyCapture,

    /// <summary>Many quests the character had completed read as not completed at once.</summary>
    LostCompletions,

    /// <summary>A journal of several quests emptied at once, none of them completed.</summary>
    EmptiedJournal,

    /// <summary>
    /// Completed quests read as not completed, every one of them a quest New Game+ replays (1.11.0, C4a): a chapter
    /// being replayed rather than a real loss, so it is never taken in, however long it reads the same.
    /// </summary>
    NewGamePlusReplay,
}

/// <summary>
/// A <see cref="CapturePlausibility.Check"/> result: the verdict and the counts it rests on, for the log.
/// </summary>
/// <param name="Lost">Completed quests (seasonal and repeatable ones aside) the capture no longer reads as completed.</param>
/// <param name="Completed">Completed quests (seasonal and repeatable ones aside) in the last committed capture; counted only when some bit cleared, else 0.</param>
/// <param name="JournalBefore">Quests in the last capture's journal (seasonal, repeatable and unnamed ones aside).</param>
/// <param name="JournalLeft">Of those, how many left the journal without being completed.</param>
public readonly record struct PlausibilityResult(PlausibilityVerdict Verdict, int Lost, int Completed, int JournalBefore, int JournalLeft)
{
    public bool Plausible => Verdict == PlausibilityVerdict.Plausible;

    /// <summary>One log line for an implausible capture; null for a plausible one. Counts only, nothing about the character.</summary>
    public string? LogNote => Verdict switch
    {
        PlausibilityVerdict.EmptyCapture => "the game reported no completed quest and an empty journal",
        PlausibilityVerdict.LostCompletions => string.Create(CultureInfo.InvariantCulture, $"the game reported {Lost} of {Completed} completed quests as not completed"),
        PlausibilityVerdict.EmptiedJournal => string.Create(CultureInfo.InvariantCulture, $"the journal of {JournalBefore} quests emptied with {JournalLeft} of them not completed"),
        PlausibilityVerdict.NewGamePlusReplay => string.Create(CultureInfo.InvariantCulture, $"the game reported {Lost} of {Completed} completed quests as not completed, all of them quests New Game+ replays"),
        _ => null,
    };
}

/// <summary>
/// The plausibility guard (feature plan v5, 1.5.0 "Trust"; R4 proposal 8). Mid-session the client can hand the
/// poller a capture that is not the character's real state: a half-loaded or zeroed quest array during a zone change
/// or a client hiccup. Committing it would overwrite the saved snapshot and its sidecars with far less progress than
/// the character has. A capture of the same character that suddenly loses many completion bits, or empties a
/// journal of several quests none of which was completed, is not committed or saved; the poller backs off and
/// captures again. The first capture of a session is judged the same way against the stored snapshot, and the backup
/// refresh (<see cref="Storage.SnapshotBackup"/>) judges the saved file against the backup it would replace. A loss
/// that keeps reading the same for a few minutes is real and is then taken in (<see cref="HeldBackCaptures"/>).
/// <para>
/// Completion bits can clear legitimately: seasonal (festival) quests are reset every year and repeatable quests on
/// their schedule. Those, and ids the catalog does not know (unnamed quests whose kind is unknown), are left out of
/// the counts, the journal's too (the game takes a festival's quests out of the journal when it ends). What is left
/// only ever grows in play, so even a modest loss is suspicious; the thresholds still allow a handful, and a small
/// character is judged by the share rather than the absolute count.
/// </para>
/// <para>
/// New Game+ (1.11.0, C4a): replaying a chapter clears the completion bits of its quests while the replay lasts. A
/// loss made only of quests some New Game+ chapter lists, however small, is a replay and never the character's real
/// progress (<see cref="PlausibilityVerdict.NewGamePlusReplay"/>): it is never saved over the progress on file, and
/// <see cref="HeldBackCaptures"/> never takes it in.
/// </para>
/// Pure; the framework thread calls it once per changed capture, a worker once per first pass.
/// </summary>
public static class CapturePlausibility
{
    /// <summary>Losing more than this many completed quests at once is implausible, whatever the character's size.</summary>
    public const int MaxLost = 50;

    /// <summary>Losing more than this share of the completed quests at once is implausible…</summary>
    public const double MaxLostShare = 0.10;

    /// <summary>…once at least this many are lost, so a character with 20 completed quests may lose a couple.</summary>
    public const int MinLostForShare = 10;

    /// <summary>A journal of at least this many quests emptying at once, with none completed, is implausible.</summary>
    public const int MinJournalEmptied = 3;

    /// <summary>
    /// Judges <paramref name="capture"/> against the last committed (or stored) capture of the same character. A
    /// different character, or no previous capture, is always plausible (a first capture with nothing to compare is
    /// left to the login guard, <see cref="LoginReadiness"/>).
    /// </summary>
    /// <param name="replayable">
    /// Quest row ids some New Game+ chapter lists (<c>CatalogBundle.NewGamePlus</c>); null or empty when not read, which
    /// leaves a replay to the other rules.
    /// </param>
    public static PlausibilityResult Check(CharacterSnapshot? last, CharacterSnapshot capture, QuestCatalog catalog, IReadOnlySet<uint>? replayable = null)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(catalog);
        if (last is null || last.ContentId != capture.ContentId)
        {
            return default;
        }

        var (lost, completed, lostReplayable) = CountLost(last.CompletedBits, capture.CompletedBits, catalog, replayable);
        var journalBefore = CountJournal(last, catalog);
        var journalLeft = capture.Accepted.Count == 0 ? CountLeftUncompleted(last, capture, catalog) : 0;

        PlausibilityVerdict verdict;
        if (LoginReadiness.LooksEmpty(capture) && !LoginReadiness.LooksEmpty(last))
        {
            verdict = PlausibilityVerdict.EmptyCapture;
        }
        else if (lost > 0 && lostReplayable == lost)
        {
            verdict = PlausibilityVerdict.NewGamePlusReplay;
        }
        else if (lost > MaxLost || (lost >= MinLostForShare && lost > completed * MaxLostShare))
        {
            verdict = PlausibilityVerdict.LostCompletions;
        }
        else if (journalBefore >= MinJournalEmptied && journalLeft >= MinJournalEmptied)
        {
            verdict = PlausibilityVerdict.EmptiedJournal;
        }
        else
        {
            verdict = PlausibilityVerdict.Plausible;
        }

        return new PlausibilityResult(verdict, lost, completed, journalBefore, journalLeft);
    }

    /// <summary>
    /// Whether a completion bit may clear in play: a seasonal (festival) or repeatable quest, or an id the catalog
    /// does not name. Such bits are left out of the counts.
    /// </summary>
    public static bool MayClear(ushort questId, QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return !catalog.TryGetByQuestId(questId, out var quest) || quest.IsRepeatable || quest.Festival != 0;
    }

    /// <summary>
    /// The comparable completion bits set before and cleared now, those set before, and how many of the cleared ones
    /// are quests New Game+ replays. The usual capture clears no bit at all and returns (0, 0, 0) after one pass over
    /// the bytes, without a catalog lookup.
    /// </summary>
    private static (int Lost, int Completed, int LostReplayable) CountLost(byte[] before, byte[] now, QuestCatalog catalog, IReadOnlySet<uint>? replayable)
    {
        var anyCleared = false;
        for (var i = 0; i < before.Length && !anyCleared; i++)
        {
            anyCleared = (before[i] & ~(i < now.Length ? now[i] : 0)) != 0;
        }

        if (!anyCleared)
        {
            return (0, 0, 0);
        }

        var lost = 0;
        var completed = 0;
        var lostReplayable = 0;
        for (var i = 0; i < before.Length; i++)
        {
            int was = before[i];
            if (was == 0)
            {
                continue;
            }

            var still = i < now.Length ? now[i] : 0;
            for (var bit = 0; bit < 8; bit++)
            {
                if ((was & (1 << bit)) == 0)
                {
                    continue;
                }

                var questId = (ushort)((i << 3) | bit);
                if (MayClear(questId, catalog))
                {
                    continue;
                }

                completed++;
                if ((still & (1 << bit)) == 0)
                {
                    lost++;
                    if (replayable is { Count: > 0 } && catalog.TryGetByQuestId(questId, out var quest) && replayable.Contains(quest.RowId))
                    {
                        lostReplayable++;
                    }
                }
            }
        }

        return (lost, completed, lostReplayable);
    }

    /// <summary>Quests in the journal that only the player removes (seasonal, repeatable and unnamed ones aside).</summary>
    private static int CountJournal(CharacterSnapshot snapshot, QuestCatalog catalog)
    {
        var count = 0;
        foreach (var accepted in snapshot.Accepted)
        {
            if (!MayClear(accepted.QuestId, catalog))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Quests in the last journal that are neither in the new journal nor completed now: abandoned, or lost by the read.
    /// Seasonal quests (the game takes them out of the journal when their festival ends), repeatable and unnamed ones
    /// are not counted.
    /// </summary>
    private static int CountLeftUncompleted(CharacterSnapshot last, CharacterSnapshot capture, QuestCatalog catalog)
    {
        var left = 0;
        foreach (var accepted in last.Accepted)
        {
            if (!capture.IsCompleted(accepted.QuestId) && !MayClear(accepted.QuestId, catalog))
            {
                left++;
            }
        }

        return left;
    }
}
