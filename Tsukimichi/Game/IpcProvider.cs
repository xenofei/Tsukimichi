using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Game;

/// <summary>
/// Tsukimichi's own IPC gates (feature plan V2-16, docs/ipc.md), so overlays and other quest plugins can build on the
/// evaluator: <c>Tsukimichi.ApiVersion</c>, <c>IsReady</c>, <c>IsQuestAvailable</c>, <c>GetState</c>,
/// <c>GetStateName</c>, <c>GetBlockers</c>, <c>GetMsqPosition</c>, <c>OpenQuest</c> and the
/// <c>StatesChanged</c> message (<see cref="IpcChannels"/>). Registered in the constructor, unregistered on dispose.
/// <para>
/// <b>Threads.</b> A gate runs on whatever thread its caller is on. Every answer comes from an <see cref="IpcView"/>:
/// an immutable capture of the catalog, the logged-in character's evaluations and name lookups through that
/// character's spoiler mask, taken on the framework thread (the session is read nowhere else). A session change marks
/// the capture stale; the next framework tick captures again, and a gate called on the framework thread in between
/// captures first, so a caller there never reads a frame-old answer. A caller elsewhere reads the last capture
/// through a volatile reference. <c>OpenQuest</c> only resolves the id off the framework thread and hands the window
/// work to <see cref="IFramework.RunOnFrameworkThread(Action)"/>.
/// </para>
/// <para>
/// <b>Changes.</b> After a tick that captured, <c>StatesChanged</c> is sent on the framework thread when the logged-in
/// character's states differ from those last announced (<see cref="IpcView.StatesDiffer"/>: a poll that moved a
/// quest, a first evaluation after login, a logout, a catalog rebuild) or another character logged in.
/// </para>
/// <para>
/// <b>Failures.</b> No gate throws into its caller: before the catalog or a character is ready it answers false, an
/// empty string or array, or 0, and an unexpected exception is logged once and answered the same way. Registration
/// that fails leaves the plugin running without the gates.
/// </para>
/// </summary>
public sealed class IpcProvider : IDisposable
{
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly SessionState session;
    private readonly Action<QuestRecord> openQuest;

    private readonly ICallGateProvider<int>? apiVersion;
    private readonly ICallGateProvider<bool>? isReady;
    private readonly ICallGateProvider<uint, bool>? isQuestAvailable;
    private readonly ICallGateProvider<uint, string>? getState;
    private readonly ICallGateProvider<uint, string>? getStateName;
    private readonly ICallGateProvider<uint, string[]>? getBlockers;
    private readonly ICallGateProvider<uint>? getMsqPosition;
    private readonly ICallGateProvider<uint, bool>? openQuestGate;
    private readonly ICallGateProvider<object>? statesChanged;

    // Written on the framework thread only; read from any thread.
    private volatile IpcView view = IpcView.Empty;

    // Set by SessionState.Changed (framework thread); cleared by the capture.
    private volatile bool stale = true;
    private volatile bool disposed;
    private int warned;

    // Framework thread only: what StatesChanged last announced.
    private IReadOnlyDictionary<uint, QuestEvaluation>? announcedStates;
    private ulong? announcedContentId;
    private ulong? capturedContentId;

    /// <param name="openQuest">Opens the main window on a quest; called on the framework thread.</param>
    public IpcProvider(IDalamudPluginInterface pluginInterface, IFramework framework, IPluginLog log, SessionState session, Action<QuestRecord> openQuest)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.openQuest = openQuest ?? throw new ArgumentNullException(nameof(openQuest));

        try
        {
            apiVersion = pluginInterface.GetIpcProvider<int>(IpcChannels.ApiVersionGate);
            isReady = pluginInterface.GetIpcProvider<bool>(IpcChannels.IsReadyGate);
            isQuestAvailable = pluginInterface.GetIpcProvider<uint, bool>(IpcChannels.IsQuestAvailableGate);
            getState = pluginInterface.GetIpcProvider<uint, string>(IpcChannels.GetStateGate);
            getStateName = pluginInterface.GetIpcProvider<uint, string>(IpcChannels.GetStateNameGate);
            getBlockers = pluginInterface.GetIpcProvider<uint, string[]>(IpcChannels.GetBlockersGate);
            getMsqPosition = pluginInterface.GetIpcProvider<uint>(IpcChannels.GetMsqPositionGate);
            openQuestGate = pluginInterface.GetIpcProvider<uint, bool>(IpcChannels.OpenQuestGate);
            statesChanged = pluginInterface.GetIpcProvider<object>(IpcChannels.StatesChangedGate);

            apiVersion.RegisterFunc(static () => IpcChannels.ApiVersion);
            isReady.RegisterFunc(() => Answer(IpcChannels.IsReadyGate, false, static v => v.IsReady));
            isQuestAvailable.RegisterFunc(id => Answer(IpcChannels.IsQuestAvailableGate, false, v => v.IsQuestAvailable(id)));
            getState.RegisterFunc(id => Answer(IpcChannels.GetStateGate, string.Empty, v => v.State(id)));
            getStateName.RegisterFunc(id => Answer(IpcChannels.GetStateNameGate, string.Empty, v => v.StateName(id)));
            getBlockers.RegisterFunc(id => Answer(IpcChannels.GetBlockersGate, [], v => v.Blockers(id)));
            getMsqPosition.RegisterFunc(() => Answer(IpcChannels.GetMsqPositionGate, 0u, static v => v.MsqNext()));
            openQuestGate.RegisterFunc(OpenQuest);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Tsukimichi IPC gates could not be registered; other plugins will not see them");
            Unregister();
        }

        session.Changed += OnSessionChanged;
        framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        framework.Update -= OnUpdate;
        session.Changed -= OnSessionChanged;
        Unregister();
        view = IpcView.Empty;
    }

    /// <summary>The capture the gates answer from; a framework-thread caller refreshes a stale one first.</summary>
    internal IpcView Current
    {
        get
        {
            if (stale && !disposed && framework.IsInFrameworkUpdateThread)
            {
                Capture();
            }

            return view;
        }
    }

    private void OnSessionChanged() => stale = true;

    private void OnUpdate(IFramework _)
    {
        if (disposed)
        {
            return;
        }

        try
        {
            if (stale)
            {
                Capture();
            }

            AnnounceChanges();
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Tsukimichi IPC capture failed");
        }
    }

    /// <summary>Framework thread: a new view over the session as it is now.</summary>
    private void Capture()
    {
        stale = false;
        capturedContentId = session.LiveContentId;
        if (session.Bundle is not { } bundle)
        {
            view = IpcView.Empty;
            return;
        }

        var states = session.LiveContentId is null ? null : session.LiveStates;
        // The mask is immutable; binding its DisplayName (not session.LiveNames, whose lookup reads the session on
        // each call) keeps the view safe to read from any thread.
        var mask = session.LiveSpoilers;
        var names = bundle.BlockerNames() with { QuestName = mask.DisplayName };
        view = new IpcView(bundle.Catalog, states, names);
    }

    /// <summary>Framework thread: sends StatesChanged when the captured states or character differ from the last announced.</summary>
    private void AnnounceChanges()
    {
        var current = view.States;
        if (capturedContentId == announcedContentId && !IpcView.StatesDiffer(announcedStates, current))
        {
            // Keep the newest instance so the next comparison starts from it (usually the same reference).
            announcedStates = current;
            return;
        }

        announcedStates = current;
        announcedContentId = capturedContentId;
        if (statesChanged is null)
        {
            return;
        }

        try
        {
            statesChanged.SendMessage();
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Tsukimichi.StatesChanged subscriber failed");
        }
    }

    private T Answer<T>(string gate, T fallback, Func<IpcView, T> answer)
    {
        if (disposed)
        {
            return fallback;
        }

        try
        {
            return answer(Current);
        }
        catch (Exception ex)
        {
            WarnOnce(ex, gate + " failed");
            return fallback;
        }
    }

    /// <summary>
    /// Resolves the id against the current capture on the caller's thread; the window is opened on the framework
    /// thread (at once when the caller is already there). True when the quest exists and the request was queued.
    /// </summary>
    private bool OpenQuest(uint id)
    {
        var quest = Answer(IpcChannels.OpenQuestGate, null, v => v.Find(id));
        if (quest is null || disposed)
        {
            return false;
        }

        try
        {
            framework.RunOnFrameworkThread(() =>
            {
                if (!disposed)
                {
                    openQuest(quest);
                }
            }).ContinueWith(t => WarnOnce(t.Exception?.GetBaseException(), "Tsukimichi.OpenQuest failed"), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return true;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Tsukimichi.OpenQuest failed");
            return false;
        }
    }

    private void Unregister()
    {
        Unregister(apiVersion);
        Unregister(isReady);
        Unregister(isQuestAvailable);
        Unregister(getState);
        Unregister(getStateName);
        Unregister(getBlockers);
        Unregister(getMsqPosition);
        Unregister(openQuestGate);
    }

    private void Unregister(ICallGateProvider? gate)
    {
        try
        {
            gate?.UnregisterFunc();
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Unregistering an IPC gate failed");
        }
    }

    private void WarnOnce(Exception? ex, string message)
    {
        if (Interlocked.Exchange(ref warned, 1) == 1)
        {
            log.Debug(ex, message);
            return;
        }

        log.Warning(ex, message);
    }
}
