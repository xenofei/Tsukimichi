using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// Watches Questionable runs (feature plan v7 A4, A6): the stop conditions the player set ("Stop later"), the chat line
/// when a run Tsukimichi started ends ("Questionable finished Close to Home and stopped.") and the run receipts. The
/// rules are <see cref="QuestionableRunGuard"/>; this feeds it each frame with the live character's completions and,
/// while a run is followed or a start awaits, with Questionable's status (read at most once a second inside
/// <see cref="QuestionableIpc.PollStatus"/>). While nothing is followed it asks Questionable nothing: a run starts being
/// followed once Tsukimichi starts one or a window's status line reads it running, and is then followed until it ends,
/// window or not.
/// <para>
/// Receipts are kept in memory (the last <see cref="MaxReceipts"/>, newest first) and the last one in the
/// configuration, so Settings can show it after a restart. A receipt is printed in chat for a run Tsukimichi started
/// or one with a stop condition; a run started elsewhere and left alone ends without a chat line.
/// </para>
/// </summary>
public sealed class QuestionableRunWatch : IDisposable
{
    /// <summary>How many receipts are kept in memory.</summary>
    public const int MaxReceipts = 10;

    private readonly IFramework framework;
    private readonly QuestionableIpc ipc;
    private readonly SessionState session;
    private readonly Configuration settings;
    private readonly Action save;
    private readonly Action<string> print;
    private readonly Func<uint, string> questName;
    private readonly IPluginLog log;
    private readonly QuestionableRunGuard guard = new();
    private readonly CompletionCues cues = new();
    private readonly List<uint> justCompleted = new(8);
    private readonly List<QuestionableRunReceipt> receipts = [];
    private readonly Func<uint, bool> isCompleted;
    private bool warned;

    /// <param name="framework">Each frame notes completions and, while a run is followed, reads the status.</param>
    /// <param name="ipc">Questionable's status and Stop.</param>
    /// <param name="session">The live character's completions and states.</param>
    /// <param name="settings">Keeps the last receipt.</param>
    /// <param name="save">Saves the settings.</param>
    /// <param name="print">Prints a chat line.</param>
    /// <param name="questName">A quest's name as the spoiler shield shows it.</param>
    /// <param name="log">The plugin log.</param>
    public QuestionableRunWatch(IFramework framework, QuestionableIpc ipc, SessionState session, Configuration settings, Action save, Action<string> print, Func<uint, string> questName, IPluginLog log)
    {
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.ipc = ipc ?? throw new ArgumentNullException(nameof(ipc));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        this.print = print ?? throw new ArgumentNullException(nameof(print));
        this.questName = questName ?? throw new ArgumentNullException(nameof(questName));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        isCompleted = IsCompleted;
        if (settings.QuestionableLastReceipt is { } last)
        {
            receipts.Add(last);
        }

        framework.Update += OnUpdate;
    }

    public void Dispose() => framework.Update -= OnUpdate;

    /// <summary>Moves whenever a receipt is added, so a view of them knows to compose its text again.</summary>
    public int Version { get; private set; }

    /// <summary>The receipts, newest first: this session's runs, and the last one of an earlier session.</summary>
    public IReadOnlyList<QuestionableRunReceipt> Receipts => receipts;

    /// <summary>The condition set on the run followed; <see cref="QuestionableStopCondition.None"/> when none.</summary>
    public QuestionableStopCondition Condition => guard.Condition;

    /// <summary>How many more quests a "stop after N quests" waits for.</summary>
    public int QuestsLeft => guard.QuestsLeft;

    /// <summary>Who started the run followed.</summary>
    public QuestionableRunOrigin Origin => guard.Origin;

    /// <summary>The quest Questionable last named during the run followed; 0 when none.</summary>
    public uint CurrentRowId => guard.CurrentRowId;

    /// <summary>Tsukimichi started Questionable: the run seen next is this one.</summary>
    public void NoteStarted(QuestionableRunOrigin origin, uint rowId) => guard.NoteStarted(origin, rowId, DateTime.UtcNow);

    /// <summary>Tsukimichi's Stop was taken by Questionable: the receipt says it was stopped from Tsukimichi.</summary>
    public void NoteStopAsked() => guard.NoteStopAsked();

    /// <summary>The duty guard's stop (plan v7 A3) was taken by Questionable: the receipt says it stopped before a duty with other players.</summary>
    public void NoteDutyGuardStop() => guard.NoteStopAsked(QuestionableRunEnd.BeforeDutyWithPlayers);

    /// <summary>
    /// Sets a stop condition on the running Questionable (the status a window last read says it runs); false when no
    /// run is under way or Questionable cannot be stopped from here.
    /// </summary>
    public bool Arm(QuestionableStopCondition condition)
    {
        if (!ipc.CanStop)
        {
            return false;
        }

        if (!guard.Tracking && ipc.LastStatus.Running)
        {
            // A run a window has shown but the watch has not followed yet: follow it from now.
            Step(ipc.LastStatus);
        }

        var armed = guard.Arm(condition);
        if (armed)
        {
            log.Information("Questionable stop condition set: {Rule} {Quests} {At} {Quest}", condition.Rule, condition.Quests, condition.AtUtc, condition.QuestRowId);
        }

        return armed;
    }

    /// <summary>Clears the stop condition.</summary>
    public void Disarm() => guard.Disarm();

    /// <summary>The receipt in one line, for chat and the receipt list: "Questionable ran 42 min: 3 quests done. Stopped from Tsukimichi."</summary>
    public string Describe(QuestionableRunReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var duration = DurationText(receipt.Duration);
        switch (receipt.End)
        {
            case QuestionableRunEnd.SingleQuestDone:
                return string.Format(CultureInfo.CurrentCulture, Strings.QuestionableRunFinishedFormat, questName(receipt.QuestRowId));
            case QuestionableRunEnd.SingleQuestNotDone:
                return string.Format(CultureInfo.CurrentCulture, Strings.QuestionableRunNotFinishedFormat, questName(receipt.QuestRowId), duration);
        }

        var count = receipt.Completed.Count;
        var quests = count switch
        {
            0 => Strings.QuestionableRunNoQuests,
            1 => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableRunOneQuestFormat, questName(receipt.Completed[0])),
            <= 3 => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableRunQuestsNamedFormat, count, string.Join(Strings.CommandListSeparator, receipt.Completed.ConvertAll(id => questName(id)))),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableRunQuestsFormat, count),
        };
        var why = receipt.End switch
        {
            QuestionableRunEnd.AfterQuests when receipt.LimitQuests == 1 => Strings.QuestionableRunWhyAfterOne,
            QuestionableRunEnd.AfterQuests => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableRunWhyAfterQuestsFormat, receipt.LimitQuests),
            QuestionableRunEnd.AtTime => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableRunWhyAtTimeFormat, QuestionableActions.ClockText(receipt.LimitAtUtc)),
            QuestionableRunEnd.AfterCurrent when receipt.QuestRowId != 0 => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableRunWhyAfterCurrentFormat, questName(receipt.QuestRowId)),
            QuestionableRunEnd.AfterCurrent => Strings.QuestionableRunWhyAfterNext,
            QuestionableRunEnd.StoppedFromTsukimichi => Strings.QuestionableRunWhyTsukimichi,
            QuestionableRunEnd.BeforeDutyWithPlayers => Strings.QuestionableRunWhyDutyGuard,
            _ => Strings.QuestionableRunWhyEnded,
        };
        return string.Format(CultureInfo.CurrentCulture, Strings.QuestionableRunReceiptFormat, duration, quests, why);
    }

    /// <summary>"under a minute", "42 min", "1 h 12 min".</summary>
    public static string DurationText(TimeSpan duration)
    {
        var minutes = (int)duration.TotalMinutes;
        if (minutes < 1)
        {
            return Strings.QuestionableRunUnderMinute;
        }

        return minutes < 60
            ? string.Format(CultureInfo.CurrentCulture, Strings.QuestionableRunMinutesFormat, minutes)
            : string.Format(CultureInfo.CurrentCulture, Strings.QuestionableRunHoursFormat, minutes / 60, minutes % 60);
    }

    private void OnUpdate(IFramework _)
    {
        try
        {
            justCompleted.Clear();
            cues.Take(session.LiveContentId, session.RecentEvents, shown: true, justCompleted);
            foreach (var rowId in justCompleted)
            {
                guard.NoteCompleted(rowId);
            }

            // Idle: nothing followed, nothing awaited, and no window has seen Questionable running. Nothing is asked.
            if (!guard.NeedsPolling && !ipc.LastStatus.Running)
            {
                return;
            }

            Step(ipc.PollStatus());
        }
        catch (Exception ex)
        {
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "Questionable run watch failed; further failures are logged at debug level");
            }
            else
            {
                log.Debug(ex, "Questionable run watch failed again");
            }
        }
    }

    private void Step(QuestionableStatus status)
    {
        switch (guard.Observe(status.Running, status.RowId, DateTime.UtcNow, isCompleted, out var receipt))
        {
            case QuestionableGuardAction.Stop:
                log.Information("Questionable stop condition met: {Rule}", guard.Condition.Rule);
                if (!ipc.Stop())
                {
                    guard.StopFailed();
                    print(Strings.QuestionableRunStopFailed);
                }

                break;
            case QuestionableGuardAction.Ended when receipt is not null:
                Keep(receipt);
                break;
        }
    }

    private void Keep(QuestionableRunReceipt receipt)
    {
        receipts.Insert(0, receipt);
        if (receipts.Count > MaxReceipts)
        {
            receipts.RemoveAt(receipts.Count - 1);
        }

        Version++;
        settings.QuestionableLastReceipt = receipt;
        save();
        log.Information("Questionable run ended: {End}, {Origin}, {Count} quests in {Duration}", receipt.End, receipt.Origin, receipt.Completed.Count, receipt.Duration);
        var conditionEnd = receipt.End is QuestionableRunEnd.AfterQuests or QuestionableRunEnd.AtTime or QuestionableRunEnd.AfterCurrent;
        if (receipt.Origin != QuestionableRunOrigin.Elsewhere || conditionEnd)
        {
            print(Describe(receipt));
        }
    }

    private bool IsCompleted(uint rowId) =>
        session.LiveStates.TryGetValue(rowId, out var evaluation) && evaluation.State is QuestState.Completed or QuestState.DoneThisCycle;
}
