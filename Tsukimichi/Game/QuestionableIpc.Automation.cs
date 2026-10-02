using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;

namespace Tsukimichi.Game;

/// <summary>
/// Questionable's step data as Tsukimichi reads it from <c>Questionable.GetCurrentStepData</c>. Dalamud hands an object
/// of another plugin's type over by converting it through JSON, so this class only has to carry the same property
/// names: upstream sends <c>TerritoryId</c> as <c>uint</c>, the WigglyMuffin fork as <c>ushort</c>, and both read into
/// a <c>uint</c>. The step's position is not read.
/// </summary>
public sealed class QuestionableStepData
{
    public string? QuestId { get; set; }

    public byte Sequence { get; set; }

    public int Step { get; set; }

    public string? InteractionType { get; set; }

    public uint TerritoryId { get; set; }
}

/// <summary>
/// The hand-offs to Questionable (feature plan v5, 1.6.0, decision 1): send a list of quests to its priority list and
/// read it back, start it and stop it, its live status, whether it has a path for a quest, and its unobtainable and
/// active-event answers for the wider cross-check. Every gate is looked up per call (<c>HasFunction</c>), so the
/// WigglyMuffin fork, which registers the list gates, <c>StartQuest</c> and the status gates but not
/// <c>IsQuestLockedReason</c>, <c>IsQuestUnobtainable</c> or <c>Stop</c>, gets what it offers and a missing gate reads as
/// "not offered".
/// <para>
/// The gates, from Questionable's provider class <c>Questionable/External/QuestionableIpc.cs</c> at
/// github.com/PunishXIV/Questionable commit 0bd61efe8a6806a7a8010741c0c46b7dea153709 (2026-09-30) and the WigglyMuffin
/// fork at 4f2909b7bc9e6ec65e63c4b71f4fb7f523688b46 (2026-09-24):
/// <c>IsRunning() -> bool</c>, <c>GetCurrentQuestId() -> string?</c>, <c>GetCurrentStepData() -> StepData?</c>,
/// <c>GetCurrentlyActiveEventQuests() -> List&lt;string&gt;</c>, <c>StartQuest(string) -> bool</c> (sets the quest as
/// the next one and starts its automatic mode, which then works through the priority list),
/// <c>IsQuestUnobtainable(string) -> bool</c> (upstream; throws for a quest it has no data for),
/// <c>ImportQuestPriority(string) -> bool</c>, <c>ClearQuestPriority() -> bool</c>, <c>ExportQuestPriority() -> string</c>
/// and <c>Stop(string label) -> bool</c> (upstream). Questionable sends no message when its list or its running state
/// changes, so those are read again when asked: the list after a send, when a pane opens and at most every 30 seconds
/// (<see cref="QuestionableBadges"/>), the live status at most once a second and only while a Tsukimichi window or the
/// Todo overlay draws it.
/// </para>
/// </summary>
public sealed partial class QuestionableIpc
{
    public const string IsRunningGate = "Questionable.IsRunning";
    public const string GetCurrentQuestIdGate = "Questionable.GetCurrentQuestId";
    public const string GetCurrentStepDataGate = "Questionable.GetCurrentStepData";
    public const string GetCurrentlyActiveEventQuestsGate = "Questionable.GetCurrentlyActiveEventQuests";
    public const string StartQuestGate = "Questionable.StartQuest";
    public const string IsQuestUnobtainableGate = "Questionable.IsQuestUnobtainable";
    public const string ImportQuestPriorityGate = "Questionable.ImportQuestPriority";
    public const string ClearQuestPriorityGate = "Questionable.ClearQuestPriority";
    public const string ExportQuestPriorityGate = "Questionable.ExportQuestPriority";
    public const string StopGate = "Questionable.Stop";

    /// <summary>The label <c>Questionable.Stop</c> logs as the reason.</summary>
    public const string StopLabel = "Tsukimichi";

    /// <summary>
    /// The plugins Questionable needs to run, by internal name: its manifest's "Required Plugins: vnavmesh, TextAdvance,
    /// Lifestream" and the <c>RequiredPlugins</c> list of <c>Questionable/Windows/ConfigComponents/PluginConfigComponent.cs</c>
    /// (the same in both versions at the commits above).
    /// </summary>
    public static readonly IReadOnlyList<string> RequiredPlugins = ["vnavmesh", "TextAdvance", "Lifestream"];

    /// <summary>The live status is read at most this often.</summary>
    public const double StatusIntervalSeconds = 1.0;

    /// <summary>The active-events list is read at most this often.</summary>
    private const double EventsIntervalSeconds = 60.0;

    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly QuestionableBadges badges = new();

    private ICallGateSubscriber<bool>? isRunning;
    private ICallGateSubscriber<string?>? getCurrentQuestId;
    private ICallGateSubscriber<QuestionableStepData?>? getCurrentStepData;
    private ICallGateSubscriber<List<string>>? getActiveEventQuests;
    private ICallGateSubscriber<string, bool>? startQuest;
    private ICallGateSubscriber<string, bool>? isQuestUnobtainable;
    private ICallGateSubscriber<string, bool>? importQuestPriority;
    private ICallGateSubscriber<bool>? clearQuestPriority;
    private ICallGateSubscriber<string>? exportQuestPriority;
    private ICallGateSubscriber<string, bool>? stop;

    private IReadOnlyList<string> missingRequired = [];

    // Live status: the last answer and when it was read; a gate of another shape turns the status off until the
    // plugin list changes.
    private QuestionableStatus status = QuestionableStatus.Idle;
    private double statusAt = double.NegativeInfinity;
    private bool statusBroken;
    private bool stepDataBroken;

    // Unobtainable answers per session version; the active events, refreshed at most every minute.
    private readonly Dictionary<uint, bool?> unobtainable = [];
    private int unobtainableVersion = int.MinValue;
    private IReadOnlySet<uint>? activeEvents;
    private double activeEventsAt = double.NegativeInfinity;
    private int activeEventsGeneration = int.MinValue;

    private double Now => clock.Elapsed.TotalSeconds;

    /// <summary>The plugins Questionable needs to run that are not loaded, by internal name; empty when all are (or Questionable is absent).</summary>
    public IReadOnlyList<string> MissingRequiredPlugins
    {
        get
        {
            Refresh();
            return loaded ? missingRequired : [];
        }
    }

    /// <summary>Questionable takes a list of quests (<c>ImportQuestPriority</c>): both versions.</summary>
    public bool CanSend => Available && HasFunction(importQuestPriority);

    /// <summary>
    /// Questionable can empty its list first (<c>ClearQuestPriority</c>), and its list can be read beforehand
    /// (<c>ExportQuestPriority</c>) so a failed replace puts it back: both versions.
    /// </summary>
    public bool CanReplace => CanSend && HasFunction(clearQuestPriority) && HasFunction(exportQuestPriority);

    /// <summary>Questionable's list can be read back (<c>ExportQuestPriority</c>): both versions.</summary>
    public bool CanReadList => Available && HasFunction(exportQuestPriority);

    /// <summary>Questionable can be started on a quest (<c>StartQuest</c>): both versions.</summary>
    public bool CanStart => Available && HasFunction(startQuest);

    /// <summary>Questionable can be stopped by another plugin (<c>Stop</c>): upstream only.</summary>
    public bool CanStop => Available && HasFunction(stop);

    /// <summary>Questionable answers whether it has a path for a quest (through <c>IsQuestLockedReason</c>): upstream only.</summary>
    public bool CanTellPaths => Available && !reasonGateBroken && HasFunction(isQuestLockedReason);

    /// <summary>
    /// Sends <paramref name="plan"/>'s quests to Questionable's priority list in one <c>ImportQuestPriority</c> call,
    /// then reads the list back to say how many landed. With <paramref name="replace"/> the list is read first and
    /// emptied (<see cref="QuestionableListReplace"/>): <paramref name="replaced"/> says how that went, and a failed import
    /// puts the old list back. Null when Questionable is absent, the gate is missing or the call failed; nothing is sent
    /// for an empty plan.
    /// </summary>
    public QuestionableSendResult? Send(QuestionableSendPlan plan, bool replace, out QuestionableReplaceOutcome? replaced)
    {
        ArgumentNullException.ThrowIfNull(plan);
        replaced = null;
        if (plan.Count == 0 || !(replace ? CanReplace : CanSend) || importQuestPriority is null)
        {
            return null;
        }

        var encoded = QuestionableList.Encode(plan.Ids);
        IReadOnlyList<string>? before = null;
        if (replace)
        {
            var import = importQuestPriority;
            var outcome = QuestionableListReplace.Run(
                encoded,
                () => exportQuestPriority!.InvokeFunc(),
                () => clearQuestPriority!.InvokeFunc(),
                text => import.InvokeFunc(text),
                ex =>
                {
                    if (ex is not IpcNotReadyError)
                    {
                        WarnOnce(ex, "Questionable list replace failed");
                    }
                });
            replaced = outcome;
            if (outcome != QuestionableReplaceOutcome.Replaced)
            {
                badges.MarkListStale();
                log.Information("Questionable list replace did not go through: {Outcome}", outcome);
                return null;
            }
        }
        else
        {
            before = ReadList();
            try
            {
                importQuestPriority.InvokeFunc(encoded);
            }
            catch (IpcNotReadyError)
            {
                return null;
            }
            catch (Exception ex)
            {
                WarnOnce(ex, "Questionable.ImportQuestPriority failed");
                return null;
            }
        }

        var after = ReadList();
        var result = QuestionableList.Verify(plan, before, after);
        if (after is null)
        {
            badges.MarkListStale();
        }
        else
        {
            badges.StorePositions(result.Positions, Generation, Now);
        }

        log.Information(
            "Sent {Count} quests to Questionable ({Mode}): {OnList} on its list, {NoPath} without a path{Unverified}",
            plan.Count,
            replace ? "replace" : "append",
            result.OnList,
            result.NoPath,
            result.Verified ? string.Empty : " (not read back)");
        return result;
    }

    /// <summary>
    /// Starts Questionable on a quest through <c>StartQuest</c>: it takes the quest as the next one and turns on its
    /// automatic mode, which then works through its priority list in order. False when the gate is missing, Questionable
    /// has no path for the quest, or the call failed.
    /// </summary>
    public bool Start(uint rowId)
    {
        if (!CanStart || startQuest is null || QuestionableCrossCheck.QuestionableId(rowId) is not { } id)
        {
            return false;
        }

        try
        {
            var started = startQuest.InvokeFunc(id);
            log.Information("Questionable start on quest {RowId}: {Started}", rowId, started);
            return started;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Questionable.StartQuest failed");
            return false;
        }
        finally
        {
            statusAt = double.NegativeInfinity;
        }
    }

    /// <summary>Stops Questionable through <c>Stop("Tsukimichi")</c> (upstream only). False when the gate is missing or failed.</summary>
    public bool Stop()
    {
        if (!CanStop || stop is null)
        {
            return false;
        }

        try
        {
            var stopped = stop.InvokeFunc(StopLabel);
            log.Information("Questionable stop: {Stopped}", stopped);
            return stopped;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Questionable.Stop failed");
            return false;
        }
        finally
        {
            statusAt = double.NegativeInfinity;
        }
    }

    /// <summary>
    /// Questionable's live status, read at most once a second (<see cref="StatusIntervalSeconds"/>); call it only from
    /// code that draws it, so nothing is asked while no Tsukimichi window is visible. Idle when Questionable is absent
    /// or its status gates are missing; a gate of another shape turns the status off (logged once) until Dalamud's plugin
    /// list changes.
    /// </summary>
    public QuestionableStatus PollStatus()
    {
        if (!Available || statusBroken || isRunning is null)
        {
            return QuestionableStatus.Idle;
        }

        var now = Now;
        if (now - statusAt < StatusIntervalSeconds)
        {
            return status;
        }

        statusAt = now;
        string? questId;
        try
        {
            if (!HasFunction(isRunning) || !isRunning.InvokeFunc())
            {
                status = QuestionableStatus.Idle;
                return status;
            }

            questId = getCurrentQuestId is not null && HasFunction(getCurrentQuestId) ? getCurrentQuestId.InvokeFunc() : null;
            status = QuestionableStatus.From(true, questId, null, null, null, 0);
        }
        catch (IpcNotReadyError)
        {
            status = QuestionableStatus.Idle;
            return status;
        }
        catch (Exception ex)
        {
            statusBroken = true;
            status = QuestionableStatus.Idle;
            log.Warning(ex, "Questionable's running status could not be read; the live status is off until Questionable reloads");
            return status;
        }

        if (stepDataBroken || getCurrentStepData is null || !HasFunction(getCurrentStepData))
        {
            return status;
        }

        try
        {
            if (getCurrentStepData.InvokeFunc() is { } step)
            {
                status = QuestionableStatus.From(true, questId, step.QuestId, step.Sequence, step.Step, step.TerritoryId);
            }
        }
        catch (IpcNotReadyError)
        {
            // Unregistered between the check and the call; the quest alone stands.
        }
        catch (Exception ex)
        {
            // A step data of another shape: the quest is still shown, without its step.
            stepDataBroken = true;
            log.Warning(ex, "Questionable.GetCurrentStepData could not be read; the live status shows the quest without its step");
        }

        return status;
    }

    /// <summary>Asks for a fresh read of Questionable's list on the next badge that needs it (a pane opened, the player asked).</summary>
    public void MarkListStale() => badges.MarkListStale();

    /// <summary>
    /// The quest's 1-based place on Questionable's priority list; null when it is not on it, the list cannot be read or
    /// Questionable is absent. Reads the list only when it was marked stale, Questionable reloaded, or the last read is
    /// over 30 seconds old.
    /// </summary>
    public int? ListPosition(uint rowId)
    {
        if (!CanReadList)
        {
            return null;
        }

        var now = Now;
        if (badges.ListNeedsRead(Generation, now))
        {
            badges.StoreList(ReadList(), Generation, now);
        }

        return badges.Position(rowId);
    }

    /// <summary>The list was read and holds this many quests; null when it cannot be read.</summary>
    public int? ListQuestCount
    {
        get
        {
            if (!CanReadList)
            {
                return null;
            }

            var now = Now;
            if (badges.ListNeedsRead(Generation, now))
            {
                badges.StoreList(ReadList(), Generation, now);
            }

            return badges.QuestCount;
        }
    }

    /// <summary>
    /// Whether Questionable has a path for the quest (upstream only): true or false once asked, null when unknown (the
    /// fork, Questionable absent, or not asked yet because this frame's questions are used up; ask again next frame).
    /// Answers hold until Questionable's <see cref="Generation"/> moves. <paramref name="frame"/> is the ImGui frame
    /// count, which spreads the questions of a long list over several frames; null asks at once (one quest, on a click).
    /// </summary>
    public bool? HasPath(uint rowId, long? frame)
    {
        if (!CanTellPaths || QuestionableCrossCheck.QuestionableId(rowId) is not { } id)
        {
            return null;
        }

        if (badges.TryGetPath(rowId, Generation, out var known))
        {
            return known;
        }

        if (frame is { } now && !badges.TryTakeAsk(now))
        {
            return null;
        }

        var answer = QuestionableBadges.HasPath(Query(id));
        badges.StorePath(rowId, Generation, answer);
        return answer;
    }

    /// <summary>
    /// Questionable's <c>IsQuestUnobtainable</c> for a quest (upstream only), asked once per session version; null when
    /// the gate is absent or threw (it throws for a quest it has no data for).
    /// </summary>
    public bool? IsUnobtainable(uint rowId, int sessionVersion)
    {
        if (!Available || isQuestUnobtainable is null || !HasFunction(isQuestUnobtainable) || QuestionableCrossCheck.QuestionableId(rowId) is not { } id)
        {
            return null;
        }

        if (unobtainableVersion != sessionVersion)
        {
            unobtainableVersion = sessionVersion;
            unobtainable.Clear();
        }

        if (unobtainable.TryGetValue(rowId, out var cached))
        {
            return cached;
        }

        bool? answer;
        try
        {
            answer = isQuestUnobtainable.InvokeFunc(id);
        }
        catch (IpcNotReadyError)
        {
            answer = null;
        }
        catch (Exception ex)
        {
            // Expected for a quest Questionable has no data for; not worth a warning.
            log.Debug(ex, "Questionable.IsQuestUnobtainable failed for quest {RowId}", rowId);
            answer = null;
        }

        unobtainable[rowId] = answer;
        return answer;
    }

    /// <summary>
    /// The quests Questionable lists as active event quests (<c>GetCurrentlyActiveEventQuests</c>), as Quest row ids,
    /// read at most once a minute; null when the gate is absent or failed.
    /// </summary>
    public IReadOnlySet<uint>? ActiveEventQuests()
    {
        if (!Available || getActiveEventQuests is null || !HasFunction(getActiveEventQuests))
        {
            return null;
        }

        var now = Now;
        if (activeEventsGeneration == Generation && now - activeEventsAt < EventsIntervalSeconds)
        {
            return activeEvents;
        }

        activeEventsGeneration = Generation;
        activeEventsAt = now;
        try
        {
            activeEvents = QuestionableWiderCheck.EventRowIds(getActiveEventQuests.InvokeFunc());
        }
        catch (IpcNotReadyError)
        {
            activeEvents = null;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Questionable.GetCurrentlyActiveEventQuests failed");
            activeEvents = null;
        }

        return activeEvents;
    }

    /// <summary>
    /// The wider cross-check for a quest (F6): path, list place, unobtainable and active events. Null when Questionable
    /// is not loaded. A stored character gets the path and the list (they do not depend on the character) but no
    /// comparison. <paramref name="festivalRunning"/> says whether the game's flags have the quest's festival running.
    /// </summary>
    public QuestionableWider? Wider(Core.Model.QuestRecord quest, QuestEvaluation? evaluation, bool live, int sessionVersion, bool festivalRunning)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (!Available)
        {
            return null;
        }

        var hasPath = HasPath(quest.RowId, null);
        var listKnown = CanReadList;
        var position = listKnown ? ListPosition(quest.RowId) : null;
        bool? unobtainableAnswer = live ? IsUnobtainable(quest.RowId, sessionVersion) : null;
        var events = live ? ActiveEventQuests() : null;
        bool? listed = events is null ? null : events.Contains(quest.RowId);
        return new QuestionableWider(
            hasPath,
            position,
            listKnown,
            unobtainableAnswer,
            QuestionableWiderCheck.CompareUnobtainable(evaluation, unobtainableAnswer, live),
            live ? QuestionableWiderCheck.CompareEvent(quest, festivalRunning, listed) : EventOutcome.NotCompared);
    }

    /// <summary>Questionable's list as element ids, in order; null when the gate is missing or failed.</summary>
    private IReadOnlyList<string>? ReadList()
    {
        if (exportQuestPriority is null || !HasFunction(exportQuestPriority))
        {
            return null;
        }

        try
        {
            return QuestionableList.Decode(exportQuestPriority.InvokeFunc());
        }
        catch (IpcNotReadyError)
        {
            return null;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Questionable.ExportQuestPriority failed");
            return null;
        }
    }

    private void SubscribeAutomation()
    {
        try
        {
            isRunning = pluginInterface.GetIpcSubscriber<bool>(IsRunningGate);
            getCurrentQuestId = pluginInterface.GetIpcSubscriber<string?>(GetCurrentQuestIdGate);
            getCurrentStepData = pluginInterface.GetIpcSubscriber<QuestionableStepData?>(GetCurrentStepDataGate);
            getActiveEventQuests = pluginInterface.GetIpcSubscriber<List<string>>(GetCurrentlyActiveEventQuestsGate);
            startQuest = pluginInterface.GetIpcSubscriber<string, bool>(StartQuestGate);
            isQuestUnobtainable = pluginInterface.GetIpcSubscriber<string, bool>(IsQuestUnobtainableGate);
            importQuestPriority = pluginInterface.GetIpcSubscriber<string, bool>(ImportQuestPriorityGate);
            clearQuestPriority = pluginInterface.GetIpcSubscriber<bool>(ClearQuestPriorityGate);
            exportQuestPriority = pluginInterface.GetIpcSubscriber<string>(ExportQuestPriorityGate);
            stop = pluginInterface.GetIpcSubscriber<string, bool>(StopGate);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Questionable automation IPC subscribers unavailable");
            isRunning = null;
            getCurrentQuestId = null;
            getCurrentStepData = null;
            getActiveEventQuests = null;
            startQuest = null;
            isQuestUnobtainable = null;
            importQuestPriority = null;
            clearQuestPriority = null;
            exportQuestPriority = null;
            stop = null;
        }
    }

    /// <summary>A new Questionable (plugin list change) or new paths (<c>ReloadData</c>): forget what it said.</summary>
    private void ResetAutomation(bool pluginsChanged)
    {
        badges.MarkListStale();
        unobtainable.Clear();
        unobtainableVersion = int.MinValue;
        activeEventsGeneration = int.MinValue;
        statusAt = double.NegativeInfinity;
        if (pluginsChanged)
        {
            statusBroken = false;
            stepDataBroken = false;
            status = QuestionableStatus.Idle;
        }
    }
}
