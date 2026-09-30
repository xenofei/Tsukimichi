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
/// hands a quest to Questionable's priority list when the player clicks the opt-in button. Tsukimichi never starts,
/// stops or steers Questionable: the one call that changes anything is <see cref="AddToPriority"/>, behind a button.
/// <para>
/// The gates, as Questionable's own provider registers them (<c>Questionable/External/QuestionableIpc.cs</c> at
/// github.com/PunishXIV/Questionable, commit 0bd61efe8a6806a7a8010741c0c46b7dea153709, 2026-09-30; the WigglyMuffin
/// fork at 4f2909b7bc9e6ec65e63c4b71f4fb7f523688b46 has the first and the last, not the reason gate):
/// <c>Questionable.IsQuestLocked(string questId) -> bool</c>,
/// <c>Questionable.IsQuestLockedReason(string questId) -> (bool, string)</c> (reasons joined by ','), and
/// <c>Questionable.AddQuestPriority(string questId) -> bool</c>. The quest id is the Quest row id's low 16 bits in
/// decimal (<see cref="QuestionableCrossCheck.QuestionableId"/>). Both lock gates answer locked for a quest Questionable
/// has no path for, the reason gate with an empty reason; <c>AddQuestPriority</c> answers true even for a quest it
/// does not know, so the button is offered only when the reason gate names the quest as one it has a path for.
/// </para>
/// <para>
/// Questionable is asked on demand, never per frame: an answer is cached per quest until the session's
/// <see cref="SessionState.Version"/> moves or Dalamud's plugin list changes (<see cref="Generation"/>). Every call is
/// wrapped: a gate that is not registered or throws reads as no answer, and the first failure is logged once.
/// </para>
/// </summary>
public sealed class QuestionableIpc : IDisposable
{
    public const string PluginInternalName = "Questionable";
    public const string IsQuestLockedGate = "Questionable.IsQuestLocked";
    public const string IsQuestLockedReasonGate = "Questionable.IsQuestLockedReason";
    public const string AddQuestPriorityGate = "Questionable.AddQuestPriority";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<string, bool>? isQuestLocked;
    private readonly ICallGateSubscriber<string, (bool, string)>? isQuestLockedReason;
    private readonly ICallGateSubscriber<string, bool>? addQuestPriority;

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

    /// <summary>Moves whenever Questionable loads or unloads, so a cache of cross-checks knows to ask again.</summary>
    public int Generation { get; private set; }

    /// <summary>Questionable is loaded and registers <c>Questionable.AddQuestPriority</c>.</summary>
    public bool SupportsPriority => Available && HasFunction(addQuestPriority);

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
    /// asked about it. A disagreement is logged once per quest per load.
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
            return;
        }

        pluginListDirty = false;
        var now = isQuestLocked is not null && IsLoaded();
        if (now != loaded)
        {
            loaded = now;
            Generation++;
            answers.Clear();
            answersVersion = int.MinValue;
            reasonGateBroken = false;
        }
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

    private bool IsLoaded()
    {
        try
        {
            foreach (var plugin in pluginInterface.InstalledPlugins)
            {
                if (plugin.IsLoaded && string.Equals(plugin.InternalName, PluginInternalName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Installed plugin list unavailable");
        }

        return false;
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
