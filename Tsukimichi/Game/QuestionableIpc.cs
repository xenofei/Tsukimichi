using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Game;

/// <summary>
/// Reads Questionable's lock answer for a quest, to cross-check Tsukimichi's evaluator (feature plan v3 §3 V2-17), and
/// hands a quest to Questionable's priority list when the player clicks the opt-in button. The calls that change
/// anything in Questionable, <see cref="AddToPriority"/> here and the send, start and stop of
/// <c>QuestionableIpc.Automation.cs</c> (feature plan v5, 1.6.0, decision 1), each run only from a button the player
/// presses.
/// <para>
/// The gates, as Questionable's own provider registers them (<c>Questionable/External/QuestionableIpc.cs</c> at
/// github.com/PunishXIV/Questionable, commit 0bd61efe8a6806a7a8010741c0c46b7dea153709, 2026-09-30; the WigglyMuffin
/// fork at 4f2909b7bc9e6ec65e63c4b71f4fb7f523688b46 has the first and the last, not the reason gate):
/// <c>Questionable.IsQuestLocked(string questId) -> bool</c>,
/// <c>Questionable.IsQuestLockedReason(string questId) -> (bool, string)</c> (reasons joined by ','), and
/// <c>Questionable.AddQuestPriority(string questId) -> bool</c>, and the message <c>Questionable.ReloadData</c> (no
/// arguments), which <c>Questionable/Controller/QuestRegistry.cs</c> sends at the end of every <c>Reload</c>: at load,
/// after its path bundle is downloaded at run time, and on its "Reload Data" button. The quest id is the Quest row id's low 16 bits in
/// decimal (<see cref="QuestionableCrossCheck.QuestionableId"/>). Both lock gates answer locked for a quest Questionable
/// has no path for, the reason gate with an empty reason; <c>AddQuestPriority</c> answers true even for a quest it
/// does not know, so the button is offered only when the reason gate names the quest as one it has a path for.
/// </para>
/// <para>
/// Questionable is asked on demand, never per frame: an answer is cached per quest until the session's
/// <see cref="SessionState.Version"/> moves, Dalamud's plugin list changes or Questionable reloads its paths
/// (<see cref="Generation"/>): an answer given before its paths arrived says "no path" for every quest. Every call is
/// wrapped: a gate that is not registered or throws reads as no answer, and the first failure is logged once.
/// </para>
/// </summary>
public sealed partial class QuestionableIpc : IDisposable
{
    public const string PluginInternalName = "Questionable";
    public const string IsQuestLockedGate = "Questionable.IsQuestLocked";
    public const string IsQuestLockedReasonGate = "Questionable.IsQuestLockedReason";
    public const string AddQuestPriorityGate = "Questionable.AddQuestPriority";
    public const string ReloadDataMessage = "Questionable.ReloadData";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<string, bool>? isQuestLocked;
    private readonly ICallGateSubscriber<string, (bool, string)>? isQuestLockedReason;
    private readonly ICallGateSubscriber<string, bool>? addQuestPriority;
    private readonly ICallGateSubscriber<object>? reloadData;

    // Answers by row id for one session version; a null value is "asked, no answer".
    private readonly Dictionary<uint, QuestionableAnswer?> answers = [];
    private readonly HashSet<uint> loggedDisagreements = [];
    private int answersVersion = int.MinValue;

    private bool loaded;
    private bool reasonGateBroken;
    private bool warned;
    private bool disposed;

    // Raised by ActivePluginsChanged, which may arrive off the framework thread; consumed on the next read.
    private volatile bool pluginListDirty = true;

    // Raised by Questionable.ReloadData, which Questionable sends from whatever thread reloaded its paths (the bundle
    // download finishes on a worker); consumed on the next read.
    private volatile bool pathsReloaded;

    public QuestionableIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        try
        {
            isQuestLocked = pluginInterface.GetIpcSubscriber<string, bool>(IsQuestLockedGate);
            isQuestLockedReason = pluginInterface.GetIpcSubscriber<string, (bool, string)>(IsQuestLockedReasonGate);
            addQuestPriority = pluginInterface.GetIpcSubscriber<string, bool>(AddQuestPriorityGate);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Questionable IPC subscribers unavailable");
            isQuestLocked = null;
            isQuestLockedReason = null;
            addQuestPriority = null;
        }

        // Apart from the gates: a message that cannot be subscribed costs fresh answers after a reload, not the cross-check.
        try
        {
            reloadData = pluginInterface.GetIpcSubscriber<object>(ReloadDataMessage);
            reloadData.Subscribe(OnReloadData);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Questionable.ReloadData subscription unavailable");
            reloadData = null;
        }

        SubscribeAutomation();
        pluginInterface.ActivePluginsChanged += OnActivePluginsChanged;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;
        try
        {
            reloadData?.Unsubscribe(OnReloadData);
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Questionable.ReloadData unsubscribe failed");
        }
    }

    /// <summary>True while Questionable is installed and loaded. Cached; re-read after Dalamud's plugin list changes.</summary>
    public bool Available
    {
        get
        {
            Refresh();
            return loaded;
        }
    }

    /// <summary>
    /// Moves whenever Dalamud's plugin list changes (Questionable loaded, unloaded, updated or swapped for a fork) or
    /// Questionable sends <c>Questionable.ReloadData</c>, so a cache of cross-checks knows to ask again.
    /// </summary>
    public int Generation { get; private set; }

    /// <summary>
    /// Questionable is loaded and registers <c>Questionable.AddQuestPriority</c> and a working
    /// <c>Questionable.IsQuestLockedReason</c>. Without the reason gate (the WigglyMuffin fork, or a reason gate of
    /// another shape) there is no telling a quest Questionable has a path for from one it does not, and
    /// <c>AddQuestPriority</c> answers true for both, so the hand-off is not offered at all.
    /// </summary>
    public bool SupportsPriority => Available && HasFunction(addQuestPriority) && !reasonGateBroken && HasFunction(isQuestLockedReason);

    /// <summary>
    /// Questionable's answer for a Quest row, asked once per <paramref name="sessionVersion"/>. Null when Questionable
    /// is not loaded, the row has no Questionable id, or neither lock gate answered.
    /// </summary>
    public QuestionableAnswer? Ask(uint rowId, int sessionVersion)
    {
        if (!Available || QuestionableCrossCheck.QuestionableId(rowId) is not { } id)
        {
            return null;
        }

        if (answersVersion != sessionVersion)
        {
            answersVersion = sessionVersion;
            answers.Clear();
        }

        if (answers.TryGetValue(rowId, out var cached))
        {
            return cached;
        }

        var answer = Query(id);
        answers[rowId] = answer;
        return answer;
    }

    /// <summary>
    /// The cross-check for a quest against the viewed character's evaluation; null when Questionable is not loaded.
    /// A stored character is not compared (Questionable answers for the character logged in) and Questionable is not
    /// asked about it. A disagreement is logged once per quest until the plugin list changes.
    /// </summary>
    public CrossCheckResult? Check(QuestRecord quest, SessionState session)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(session);
        if (!Available)
        {
            return null;
        }

        session.States.TryGetValue(quest.RowId, out var evaluation);
        if (!session.IsLive)
        {
            return QuestionableCrossCheck.Compare(evaluation, null, live: false);
        }

        if (QuestionableCrossCheck.QuestionableId(quest.RowId) is null)
        {
            return new CrossCheckResult(CrossCheckOutcome.NotCompared, null, evaluation?.State);
        }

        var result = QuestionableCrossCheck.Compare(evaluation, Ask(quest.RowId, session.Version));
        if (result.Disagrees && loggedDisagreements.Add(quest.RowId))
        {
            log.Information("Questionable disagrees on quest {RowId}: {Text}", quest.RowId, QuestionableCrossCheck.DiagnosticText(result));
        }

        return result;
    }

    /// <summary>
    /// Whether the priority hand-off makes sense for a quest: Questionable registers the gate and its reason gate
    /// answered with a path for the quest (a locked answer with no reason means it has none). Asks at most once per
    /// session version.
    /// </summary>
    public bool CanAddToPriority(uint rowId, int sessionVersion) =>
        SupportsPriority && Ask(rowId, sessionVersion) is { Reason: not null, IsIndeterminate: false };

    /// <summary>
    /// Hands one quest to Questionable's priority list through its own gate. Nothing else happens: Questionable does
    /// not start, and Tsukimichi does not move the character. False when Questionable is absent, refused or threw.
    /// </summary>
    public bool AddToPriority(uint rowId)
    {
        if (!SupportsPriority || addQuestPriority is null || QuestionableCrossCheck.QuestionableId(rowId) is not { } id)
        {
            return false;
        }

        try
        {
            var accepted = addQuestPriority.InvokeFunc(id);
            if (!accepted)
            {
                log.Debug("Questionable declined priority for quest {RowId}", rowId);
            }

            return accepted;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Questionable.AddQuestPriority failed");
            return false;
        }
    }

    /// <summary>The reason gate first, the flag gate when the reason gate is missing or failed; null when neither answers.</summary>
    private QuestionableAnswer? Query(string id)
    {
        if (!reasonGateBroken && isQuestLockedReason is not null && HasFunction(isQuestLockedReason))
        {
            try
            {
                var (locked, reason) = isQuestLockedReason.InvokeFunc(id);
                return new QuestionableAnswer(locked, reason ?? string.Empty);
            }
            catch (IpcNotReadyError)
            {
                // Unregistered between the check and the call (Questionable unloading); the flag gate may still answer.
            }
            catch (Exception ex)
            {
                // A reason gate of another shape (a fork): use the flag gate from now on.
                reasonGateBroken = true;
                WarnOnce(ex, "Questionable.IsQuestLockedReason failed; using Questionable.IsQuestLocked");
            }
        }

        if (isQuestLocked is null)
        {
            return null;
        }

        try
        {
            return new QuestionableAnswer(isQuestLocked.InvokeFunc(id), null);
        }
        catch (IpcNotReadyError)
        {
            return null;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Questionable.IsQuestLocked failed");
            return null;
        }
    }

    private void Refresh()
    {
        if (!pluginListDirty)
        {
            if (pathsReloaded)
            {
                // The same Questionable with new paths: its answers may change (a quest that had no path has one now),
                // its gates and what is loaded do not.
                pathsReloaded = false;
                Generation++;
                answers.Clear();
                answersVersion = int.MinValue;
                loggedDisagreements.Clear();
                ResetAutomation(pluginsChanged: false);
            }

            return;
        }

        // Every change of the plugin list starts over, whether or not the loaded flag flips: an unload and a load
        // handled in one pass (an update, or a swap to the fork under the same internal name) is a new Questionable
        // whose answers and reason gate may differ.
        pluginListDirty = false;
        pathsReloaded = false;
        loaded = isQuestLocked is not null && ScanPlugins();
        Generation++;
        answers.Clear();
        answersVersion = int.MinValue;
        reasonGateBroken = false;
        loggedDisagreements.Clear();
        ResetAutomation(pluginsChanged: true);
    }

    private bool HasFunction(ICallGateSubscriber? gate)
    {
        if (gate is null)
        {
            return false;
        }

        try
        {
            return gate.HasFunction;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Questionable IPC gate check failed");
            return false;
        }
    }

    private void OnActivePluginsChanged(IActivePluginsChangedEventArgs args) => pluginListDirty = true;

    private void OnReloadData() => pathsReloaded = true;

    /// <summary>
    /// Whether Questionable is loaded, read from Dalamud's installed plugin list in the same pass as the plugins
    /// Questionable itself needs to run (<see cref="MissingRequiredPlugins"/>).
    /// </summary>
    private bool ScanPlugins()
    {
        var found = false;
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var plugin in pluginInterface.InstalledPlugins)
            {
                if (!plugin.IsLoaded)
                {
                    continue;
                }

                present.Add(plugin.InternalName);
                if (string.Equals(plugin.InternalName, PluginInternalName, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                }
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Installed plugin list unavailable");
            missingRequired = [];
            return false;
        }

        var missing = new List<string>(RequiredPlugins.Count);
        foreach (var required in RequiredPlugins)
        {
            if (!present.Contains(required))
            {
                missing.Add(required);
            }
        }

        missingRequired = missing;
        return found;
    }

    private void WarnOnce(Exception ex, string message)
    {
        if (warned)
        {
            log.Debug(ex, message);
            return;
        }

        warned = true;
        log.Warning(ex, message);
    }
}
