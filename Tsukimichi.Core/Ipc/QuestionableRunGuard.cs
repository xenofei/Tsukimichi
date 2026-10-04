using System.Globalization;

namespace Tsukimichi.Core.Ipc;

/// <summary>Who started a Questionable run, as far as Tsukimichi knows.</summary>
public enum QuestionableRunOrigin
{
    /// <summary>Started some other way: Questionable's own window, its commands, another plugin.</summary>
    Elsewhere,

    /// <summary>Tsukimichi's Start pill through <c>StartSingleQuest</c>: one quest, then Questionable stops.</summary>
    SingleQuest,

    /// <summary>Tsukimichi through <c>StartQuest</c> ("Start here and keep going", Add and start): it carries on until stopped.</summary>
    KeepGoing,
}

/// <summary>A stop condition the player set on a running Questionable (feature plan v7 A4).</summary>
public enum QuestionableStopRule
{
    /// <summary>No condition: the run goes on until it ends or is stopped.</summary>
    None,

    /// <summary>Stop once this many more quests are done.</summary>
    AfterQuests,

    /// <summary>Stop at a set time.</summary>
    AtTime,

    /// <summary>Stop once the quest Questionable works on now is done.</summary>
    AfterCurrent,
}

/// <summary>Why a Questionable run ended, as its receipt says.</summary>
public enum QuestionableRunEnd
{
    /// <summary>A single-quest run whose quest is done: Questionable stopped by itself, as asked.</summary>
    SingleQuestDone,

    /// <summary>A single-quest run that ended before its quest was done.</summary>
    SingleQuestNotDone,

    /// <summary>Stopped by Tsukimichi after the number of quests the player set.</summary>
    AfterQuests,

    /// <summary>Stopped by Tsukimichi at the time the player set.</summary>
    AtTime,

    /// <summary>Stopped by Tsukimichi once the quest under way when the player asked was done.</summary>
    AfterCurrent,

    /// <summary>Stopped from Tsukimichi: a Stop button or <c>/tsuki stop</c>.</summary>
    StoppedFromTsukimichi,

    /// <summary>Ended some other way: Questionable stopped itself, or was stopped from its own window.</summary>
    Ended,
}

/// <summary>A stop condition and what it needs: a count, a time (UTC) or the quest under way (0 when unknown: the next quest done).</summary>
public readonly record struct QuestionableStopCondition(QuestionableStopRule Rule, int Quests, DateTime AtUtc, uint QuestRowId)
{
    /// <summary>No condition.</summary>
    public static readonly QuestionableStopCondition None = new(QuestionableStopRule.None, 0, default, 0);

    /// <summary>Stop once <paramref name="quests"/> more quests are done (clamped to 1–<see cref="QuestionableRunGuard.MaxQuests"/>).</summary>
    public static QuestionableStopCondition AfterQuests(int quests) =>
        new(QuestionableStopRule.AfterQuests, Math.Clamp(quests, 1, QuestionableRunGuard.MaxQuests), default, 0);

    /// <summary>Stop at <paramref name="atUtc"/>.</summary>
    public static QuestionableStopCondition AtTime(DateTime atUtc) => new(QuestionableStopRule.AtTime, 0, atUtc, 0);

    /// <summary>Stop once quest <paramref name="rowId"/> is done; 0 stops after the next quest done, whichever it is.</summary>
    public static QuestionableStopCondition AfterCurrent(uint? rowId) => new(QuestionableStopRule.AfterCurrent, 0, default, rowId ?? 0);
}

/// <summary>
/// What one Questionable run did (feature plan v7 A4, the run receipt): when it ran, the quests the character completed
/// meanwhile, who started it and why it ended. A plain class with setters, so the last one can be kept in the plugin's
/// configuration.
/// </summary>
public sealed class QuestionableRunReceipt
{
    public DateTime StartedUtc { get; set; }

    public DateTime EndedUtc { get; set; }

    public QuestionableRunOrigin Origin { get; set; }

    public QuestionableRunEnd End { get; set; }

    /// <summary>The Quest row ids completed during the run, in order.</summary>
    public List<uint> Completed { get; set; } = [];

    /// <summary>The single-quest run's quest, or the quest a "stop after this quest" waited for; 0 when none.</summary>
    public uint QuestRowId { get; set; }

    /// <summary>The count a "stop after N quests" was set to; 0 otherwise.</summary>
    public int LimitQuests { get; set; }

    /// <summary>The time a "stop at" was set to (UTC); default otherwise.</summary>
    public DateTime LimitAtUtc { get; set; }

    /// <summary>How long the run took; never negative.</summary>
    public TimeSpan Duration => EndedUtc > StartedUtc ? EndedUtc - StartedUtc : TimeSpan.Zero;
}

/// <summary>What <see cref="QuestionableRunGuard.Observe"/> asks of its caller.</summary>
public enum QuestionableGuardAction
{
    /// <summary>Nothing to do.</summary>
    None,

    /// <summary>A stop condition is met: ask Questionable to stop (and call <see cref="QuestionableRunGuard.StopFailed"/> if it refuses).</summary>
    Stop,

    /// <summary>The run ended: its receipt is out.</summary>
    Ended,
}

/// <summary>
/// The rules behind "stop after N quests", "stop at a time", "stop after this quest" and the run receipt (feature plan
/// v7 A4, A6). It is fed Questionable's live status (running, current quest), the character's completions and
/// Tsukimichi's own starts and stops, and says when to stop Questionable and when a run ended. Pure: the clock is
/// passed in.
/// <para>
/// A run is tracked from the first status that reads running. A start Tsukimichi asked for less than
/// <see cref="StartWindowSeconds"/> earlier names its origin and start time; anything else counts as started
/// elsewhere. A run ends once Questionable has read not running for <see cref="EndGraceSeconds"/> (an IPC blip or a
/// late completion does not cut it short); a start Tsukimichi asks for meanwhile ends it at once. A condition holds for the run it was set on and is gone when that run ends;
/// it fires once, and a stop Questionable refuses clears it (<see cref="StopFailed"/>) so it is not asked again every
/// second.
/// </para>
/// </summary>
public sealed class QuestionableRunGuard
{
    /// <summary>How long Questionable must read not running before its run counts as ended.</summary>
    public const double EndGraceSeconds = 4.0;

    /// <summary>How long a start Tsukimichi asked for waits to be seen running.</summary>
    public const double StartWindowSeconds = 15.0;

    /// <summary>The largest "stop after N quests".</summary>
    public const int MaxQuests = 99;

    private readonly List<uint> completed = [];

    private DateTime? pendingAt;
    private QuestionableRunOrigin pendingOrigin;
    private uint pendingRowId;

    private DateTime? idleSince;
    private QuestionableRunEnd? stopReason;
    private int completedAtArm;

    /// <summary>Whether a run is being followed.</summary>
    public bool Tracking { get; private set; }

    /// <summary>Who started the run followed.</summary>
    public QuestionableRunOrigin Origin { get; private set; }

    /// <summary>The single-quest run's quest; 0 for any other run.</summary>
    public uint SingleRowId { get; private set; }

    /// <summary>When the run followed started (UTC).</summary>
    public DateTime StartedUtc { get; private set; }

    /// <summary>The quest Questionable last said it works on during this run; 0 when none was named.</summary>
    public uint CurrentRowId { get; private set; }

    /// <summary>The condition set on the run followed; <see cref="QuestionableStopCondition.None"/> when none.</summary>
    public QuestionableStopCondition Condition { get; private set; } = QuestionableStopCondition.None;

    /// <summary>The quests completed during the run, in order.</summary>
    public IReadOnlyList<uint> Completed => completed;

    /// <summary>Whether the status must be read even while no window shows it: a run is followed, or a start awaits.</summary>
    public bool NeedsPolling => Tracking || pendingAt is not null;

    /// <summary>Whether a stop the guard asked for (or Tsukimichi's Stop) is under way.</summary>
    public bool Stopping => stopReason is not null;

    /// <summary>How many more quests a "stop after N quests" waits for; 0 for any other condition.</summary>
    public int QuestsLeft => Condition.Rule == QuestionableStopRule.AfterQuests
        ? Math.Max(0, Condition.Quests - (completed.Count - completedAtArm))
        : 0;

    /// <summary>Tsukimichi asked Questionable to start: the run seen next is this one.</summary>
    public void NoteStarted(QuestionableRunOrigin origin, uint rowId, DateTime nowUtc)
    {
        // A run that still reads running cannot have been started again; one that went idle can (its grace runs).
        if (Tracking && idleSince is null)
        {
            return;
        }

        pendingAt = nowUtc;
        pendingOrigin = origin;
        pendingRowId = origin == QuestionableRunOrigin.SingleQuest ? rowId : 0;
    }

    /// <summary>Tsukimichi's Stop (a button, <c>/tsuki stop</c>) was taken by Questionable.</summary>
    public void NoteStopAsked()
    {
        if (Tracking)
        {
            stopReason ??= QuestionableRunEnd.StoppedFromTsukimichi;
        }
    }

    /// <summary>The character completed a quest; counted while a run is followed.</summary>
    public void NoteCompleted(uint rowId)
    {
        if (Tracking && !completed.Contains(rowId))
        {
            completed.Add(rowId);
        }
    }

    /// <summary>Sets a stop condition on the run followed, replacing any other; false when no run is followed or one is stopping.</summary>
    public bool Arm(QuestionableStopCondition condition)
    {
        if (!Tracking || stopReason is not null || condition.Rule == QuestionableStopRule.None)
        {
            return false;
        }

        Condition = condition;
        completedAtArm = completed.Count;
        return true;
    }

    /// <summary>Clears the stop condition.</summary>
    public void Disarm() => Condition = QuestionableStopCondition.None;

    /// <summary>The stop asked after <see cref="QuestionableGuardAction.Stop"/> was refused: the condition is dropped and the run goes on.</summary>
    public void StopFailed()
    {
        stopReason = null;
        Condition = QuestionableStopCondition.None;
    }

    /// <summary>
    /// One reading of Questionable's status at <paramref name="nowUtc"/>. <paramref name="isCompleted"/> says whether the
    /// character has a quest completed now (null: only the completions noted count), for a single-quest run whose last
    /// completion was not noted before it ended. <paramref name="receipt"/> is set with <see cref="QuestionableGuardAction.Ended"/>.
    /// </summary>
    public QuestionableGuardAction Observe(bool running, uint? currentRowId, DateTime nowUtc, Func<uint, bool>? isCompleted, out QuestionableRunReceipt? receipt)
    {
        receipt = null;
        if (running)
        {
            if (Tracking && idleSince is { } idle && pendingAt is { } at && at >= idle)
            {
                // Started again from Tsukimichi while the last run's grace ran: that run ended when it went idle.
                receipt = Finish(idle, isCompleted);
                Begin(nowUtc);
                return QuestionableGuardAction.Ended;
            }

            idleSince = null;
            if (!Tracking)
            {
                Begin(nowUtc);
            }

            if (currentRowId is { } rowId and not 0)
            {
                CurrentRowId = rowId;
            }

            if (stopReason is null && Reached(nowUtc) is { } reason)
            {
                stopReason = reason;
                return QuestionableGuardAction.Stop;
            }

            return QuestionableGuardAction.None;
        }

        if (!Tracking)
        {
            if (pendingAt is { } at && (nowUtc - at).TotalSeconds > StartWindowSeconds)
            {
                pendingAt = null;
            }

            return QuestionableGuardAction.None;
        }

        idleSince ??= nowUtc;
        if ((nowUtc - idleSince.Value).TotalSeconds < EndGraceSeconds)
        {
            return QuestionableGuardAction.None;
        }

        receipt = Finish(idleSince.Value, isCompleted);
        return QuestionableGuardAction.Ended;
    }

    /// <summary>
    /// The next moment the clock reads <paramref name="hour"/>:<paramref name="minute"/> after
    /// <paramref name="nowLocal"/>: today when that is still ahead, else tomorrow.
    /// </summary>
    public static DateTime NextAt(DateTime nowLocal, int hour, int minute)
    {
        var at = nowLocal.Date.AddHours(Math.Clamp(hour, 0, 23)).AddMinutes(Math.Clamp(minute, 0, 59));
        return at > nowLocal ? at : at.AddDays(1);
    }

    /// <summary>Reads a clock time: "21:30", "9:05", "21.30" or "2130"; false for anything else.</summary>
    public static bool TryParseClock(string? text, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        string h, m;
        var separator = trimmed.IndexOfAny([':', '.']);
        if (separator >= 0)
        {
            h = trimmed[..separator];
            m = trimmed[(separator + 1)..];
        }
        else if (trimmed.Length is 3 or 4)
        {
            h = trimmed[..^2];
            m = trimmed[^2..];
        }
        else
        {
            return false;
        }

        return m.Length == 2
            && h.Length is 1 or 2
            && int.TryParse(h, NumberStyles.None, CultureInfo.InvariantCulture, out hour)
            && int.TryParse(m, NumberStyles.None, CultureInfo.InvariantCulture, out minute)
            && hour is >= 0 and <= 23
            && minute is >= 0 and <= 59;
    }

    private void Begin(DateTime nowUtc)
    {
        Tracking = true;
        completed.Clear();
        CurrentRowId = 0;
        stopReason = null;
        Condition = QuestionableStopCondition.None;
        completedAtArm = 0;
        if (pendingAt is { } at && (nowUtc - at).TotalSeconds <= StartWindowSeconds)
        {
            Origin = pendingOrigin;
            SingleRowId = pendingRowId;
            StartedUtc = at;
        }
        else
        {
            Origin = QuestionableRunOrigin.Elsewhere;
            SingleRowId = 0;
            StartedUtc = nowUtc;
        }

        pendingAt = null;
    }

    private QuestionableRunEnd? Reached(DateTime nowUtc)
    {
        var condition = Condition;
        var since = completed.Count - completedAtArm;
        return condition.Rule switch
        {
            QuestionableStopRule.AfterQuests when since >= condition.Quests => QuestionableRunEnd.AfterQuests,
            QuestionableStopRule.AtTime when nowUtc >= condition.AtUtc => QuestionableRunEnd.AtTime,
            QuestionableStopRule.AfterCurrent when (condition.QuestRowId == 0 ? since > 0 : CompletedSinceArm(condition.QuestRowId)) => QuestionableRunEnd.AfterCurrent,
            _ => null,
        };
    }

    private bool CompletedSinceArm(uint rowId)
    {
        for (var i = completedAtArm; i < completed.Count; i++)
        {
            if (completed[i] == rowId)
            {
                return true;
            }
        }

        return false;
    }

    private QuestionableRunReceipt Finish(DateTime endedUtc, Func<uint, bool>? isCompleted)
    {
        if (SingleRowId != 0 && !completed.Contains(SingleRowId) && isCompleted?.Invoke(SingleRowId) == true)
        {
            completed.Add(SingleRowId);
        }

        var end = stopReason
            ?? (Origin == QuestionableRunOrigin.SingleQuest
                ? completed.Contains(SingleRowId) ? QuestionableRunEnd.SingleQuestDone : QuestionableRunEnd.SingleQuestNotDone
                : QuestionableRunEnd.Ended);
        var condition = Condition;
        var receipt = new QuestionableRunReceipt
        {
            StartedUtc = StartedUtc,
            EndedUtc = endedUtc,
            Origin = Origin,
            End = end,
            Completed = [.. completed],
            QuestRowId = end == QuestionableRunEnd.AfterCurrent ? condition.QuestRowId : SingleRowId,
            LimitQuests = end == QuestionableRunEnd.AfterQuests ? condition.Quests : 0,
            LimitAtUtc = end == QuestionableRunEnd.AtTime ? condition.AtUtc : default,
        };

        Tracking = false;
        Origin = QuestionableRunOrigin.Elsewhere;
        SingleRowId = 0;
        CurrentRowId = 0;
        Condition = QuestionableStopCondition.None;
        stopReason = null;
        idleSince = null;
        completedAtArm = 0;
        completed.Clear();
        return receipt;
    }
}
