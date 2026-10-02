using System.Globalization;
using System.Text;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ipc;

/// <summary>
/// What "Send to Questionable" hands over (feature plan v5, 1.6.0): the quests of a route, chain, ladder, expansion or
/// the pins that are worth putting on Questionable's priority list, in the order given, as Questionable ids
/// (<see cref="QuestionableCrossCheck.QuestionableId"/>), and how many were left out and why.
/// </summary>
/// <param name="RowIds">The quests sent, as Quest row ids, in the order given, each once.</param>
/// <param name="Ids">The same quests as Questionable ids ("428"), in the same order.</param>
/// <param name="Done">Left out: completed, or done for this cycle.</param>
/// <param name="InJournal">Left out: already in the journal.</param>
/// <param name="LockedOut">Left out: locked out for good (another choice was made, or the game removed it).</param>
/// <param name="NoId">Left out: no Questionable id (not a Quest row, or one Tsukimichi never asks Questionable about).</param>
public sealed record QuestionableSendPlan(IReadOnlyList<uint> RowIds, IReadOnlyList<string> Ids, int Done, int InJournal, int LockedOut, int NoId)
{
    public static readonly QuestionableSendPlan Empty = new([], [], 0, 0, 0, 0);

    /// <summary>Quests sent.</summary>
    public int Count => Ids.Count;

    /// <summary>Quests left out for any reason.</summary>
    public int Skipped => Done + InJournal + LockedOut + NoId;
}

/// <summary>
/// How a send went, read back from Questionable's own list (<c>ExportQuestPriority</c>) after the import.
/// </summary>
/// <param name="Sent">Quests handed over (<see cref="QuestionableSendPlan.Count"/>).</param>
/// <param name="OnList">Of those, the ones on Questionable's list afterwards (newly added or already there).</param>
/// <param name="AlreadyThere">Of <paramref name="OnList"/>, the ones that were on the list before the send.</param>
/// <param name="Verified">False when the list could not be read back; the counts then assume every quest landed.</param>
/// <param name="Positions">Questionable's list afterwards: Quest row id to its 1-based place.</param>
public sealed record QuestionableSendResult(int Sent, int OnList, int AlreadyThere, bool Verified, IReadOnlyDictionary<uint, int> Positions)
{
    /// <summary>Quests Questionable dropped: it has no path for them.</summary>
    public int NoPath => Sent - OnList;
}

/// <summary>
/// Builds and reads Questionable's priority list format (feature plan v5, 1.6.0 "Send to Questionable"). Pure; the
/// plugin's <c>Game.QuestionableIpc</c> makes the calls.
/// <para>
/// The format, as Questionable reads and writes it (<c>Questionable/Windows/PriorityWindow.cs</c>,
/// <c>EncodeQuestPriority</c> and <c>DecodeQuestPriority</c>, github.com/PunishXIV/Questionable commit
/// 0bd61efe8a6806a7a8010741c0c46b7dea153709, the same in the WigglyMuffin fork at 4f2909b7): <c>"qst:priority:"</c>
/// followed by the Base64 of the UTF-8 text of the element ids joined by ';' ("428;1021;A12"). A quest's element id is
/// its Quest row id's low 16 bits in decimal; other kinds carry a letter prefix (A allied society daily, S
/// satisfaction supply, U unlock link, N aethernet, C collection), which Tsukimichi never sends and ignores when
/// reading. <c>ImportQuestPriority</c> appends the quests it has a path for that are not already on the list
/// (<c>QuestPriorityManager.Import</c>), drops the rest without saying so, and always answers true, so the result is
/// checked by reading the list back.
/// </para>
/// </summary>
public static class QuestionableList
{
    /// <summary>The prefix of the current format.</summary>
    public const string Prefix = "qst:priority:";

    /// <summary>The prefix of the format before it, which Questionable still reads.</summary>
    public const string LegacyPrefix = "qst:v1:";

    /// <summary>Between element ids in the decoded text.</summary>
    public const char Separator = ';';

    /// <summary>
    /// The quests of <paramref name="rowIds"/> worth sending, in the given order, each once: done, in-journal and
    /// locked-out quests are left out (by the viewed character's <paramref name="states"/>), and so are rows without a
    /// Questionable id. A quest with no evaluation (none checked yet) is sent.
    /// </summary>
    public static QuestionableSendPlan Plan(IEnumerable<uint> rowIds, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(rowIds);
        ArgumentNullException.ThrowIfNull(states);
        var seen = new HashSet<uint>();
        var rows = new List<uint>();
        var ids = new List<string>();
        int done = 0, inJournal = 0, lockedOut = 0, noId = 0;
        foreach (var rowId in rowIds)
        {
            if (!seen.Add(rowId))
            {
                continue;
            }

            if (QuestionableCrossCheck.QuestionableId(rowId) is not { } id)
            {
                noId++;
                continue;
            }

            switch (states.TryGetValue(rowId, out var evaluation) ? evaluation.State : QuestState.Unknown)
            {
                case QuestState.Completed or QuestState.DoneThisCycle:
                    done++;
                    continue;
                case QuestState.Accepted:
                    inJournal++;
                    continue;
                case QuestState.Foreclosed:
                    lockedOut++;
                    continue;
            }

            rows.Add(rowId);
            ids.Add(id);
        }

        return new QuestionableSendPlan(rows, ids, done, inJournal, lockedOut, noId);
    }

    /// <summary><c>"qst:priority:"</c> + Base64(UTF-8(ids joined by ';')): the text <c>ImportQuestPriority</c> takes.</summary>
    public static string Encode(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(Separator, ids)));
    }

    /// <summary>
    /// The element ids of an exported list, in order (quests and the other kinds alike, as Questionable wrote them).
    /// Empty for null, empty or malformed text, as Questionable itself reads it.
    /// </summary>
    public static IReadOnlyList<string> Decode(string? exported)
    {
        var text = exported?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        string body;
        if (text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            body = text[Prefix.Length..];
        }
        else if (text.StartsWith(LegacyPrefix, StringComparison.Ordinal))
        {
            body = text[LegacyPrefix.Length..];
        }
        else
        {
            return [];
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(body));
            return decoded.Length == 0 ? [] : decoded.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch (FormatException)
        {
            return [];
        }
    }

    /// <summary>
    /// The Quest row id of a Questionable element id: a bare decimal number from 1 to 65535 (a <c>QuestId</c>). Null
    /// for the prefixed kinds ("A12", "U5"), for null or empty text and for anything else.
    /// </summary>
    public static uint? RowIdOf(string? id)
    {
        if (string.IsNullOrEmpty(id) || !char.IsAsciiDigit(id[0]))
        {
            return null;
        }

        return ushort.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? QuestionableCrossCheck.FirstRowId + value
            : null;
    }

    /// <summary>Each quest on the list by Quest row id, to its 1-based place among all the list's entries; other kinds are skipped but counted.</summary>
    public static IReadOnlyDictionary<uint, int> Positions(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var positions = new Dictionary<uint, int>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            if (RowIdOf(ids[i]) is { } rowId)
            {
                positions.TryAdd(rowId, i + 1);
            }
        }

        return positions;
    }

    /// <summary>
    /// The result of a send from the list before it (null when it was not read, or was cleared first) and after it
    /// (null when it could not be read back, which counts every quest as sent).
    /// </summary>
    public static QuestionableSendResult Verify(QuestionableSendPlan plan, IReadOnlyList<string>? before, IReadOnlyList<string>? after)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (after is null)
        {
            return new QuestionableSendResult(plan.Count, plan.Count, 0, false, new Dictionary<uint, int>());
        }

        var positions = Positions(after);
        var earlier = before is null ? null : Positions(before);
        int onList = 0, already = 0;
        foreach (var rowId in plan.RowIds)
        {
            if (!positions.ContainsKey(rowId))
            {
                continue;
            }

            onList++;
            if (earlier?.ContainsKey(rowId) == true)
            {
                already++;
            }
        }

        return new QuestionableSendResult(plan.Count, onList, already, true, positions);
    }
}
