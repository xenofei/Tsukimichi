using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>
/// Tsukimichi's own IPC gates (feature plan V2-16, docs/ipc.md), so overlays and other quest plugins can build on the
/// evaluator: <c>Tsukimichi.ApiVersion</c>, <c>IsReady</c>, <c>IsQuestAvailable</c>, <c>GetState</c>,
/// <c>GetStateName</c>, <c>GetBlockers</c>, <c>GetMsqPosition</c>, <c>GetMsqPositions</c>, <c>OpenQuest</c> and the
/// <c>StatesChanged</c> message, and since 1.8.0 <c>GetGates</c>, <c>GetStates</c>, <c>GetQuestsInState</c>,
/// <c>GetQuestsInZone</c>, <c>GetFirstBlocker</c>, <c>GetRoute</c>, <c>GetQuestsForItem</c>, <c>GetMoonlitStatus</c>,
/// <c>GetUnlockQuests</c>, <c>GetPins</c>, <c>PinQuest</c>, <c>GetAbandoned</c>, <c>GetNextJobQuest</c> and the
/// <c>QuestStateChanged</c> and <c>Disposing</c> messages (<see cref="IpcChannels"/>). Registered in the constructor,
/// unregistered on dispose, after <c>Disposing</c> is sent.
/// <para>
/// <b>Threads.</b> A gate runs on whatever thread its caller is on. Every answer comes from an <see cref="IpcView"/>:
/// an immutable capture of the catalog, the logged-in character's evaluations and name lookups through that
/// character's spoiler mask, plus <see cref="IpcExtras"/> (the reward and duty lookups, the job ladders, the abandoned
/// quests, the saved collectibles), taken on the framework thread (the session is read nowhere else). A session change
/// marks the capture stale; the next framework tick captures again, and a gate called on the framework thread in
/// between captures first, so a caller there never reads a frame-old answer. A caller elsewhere reads the last capture
/// through a volatile reference. <c>OpenQuest</c> and <c>PinQuest</c> only resolve the id off the framework thread and
/// hand the work to <see cref="IFramework.RunOnFrameworkThread(Action)"/>. The live spoiler mask (quest names in
/// <c>GetBlockers</c>) is only built into a capture once a gate was called or a message has a subscriber, so a session
/// change costs nothing extra while no plugin uses the gates.
/// </para>
/// <para>
/// <b>Changes.</b> After a tick that captured, <c>StatesChanged</c> is sent on the framework thread when the logged-in
/// character's states differ from those last announced (<see cref="IpcView.StatesDiffer"/>: a poll that moved a
/// quest, a first evaluation after login, a logout, a catalog rebuild) or another character logged in. Before it,
/// <c>QuestStateChanged</c> is sent once per quest whose state moved, but only for a live poll of the same character
/// on the same catalog with states already announced, and at most <see cref="IpcView.MaxStateChangesPerTick"/> per tick
/// (more sends none): a login, a first evaluation and a catalog rebuild never flood a subscriber.
/// </para>
/// <para>
/// <b>Failures.</b> No gate throws into its caller: before the catalog or a character is ready it answers false, an
/// empty string or array, or 0, and an unexpected exception is logged once and answered the same way. Registration
/// that fails leaves the plugin running without the gates.
/// </para>
/// </summary>
public sealed class IpcProvider : IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
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
    private readonly ICallGateProvider<uint[]>? getMsqPositions;
    private readonly ICallGateProvider<uint, bool>? openQuestGate;
    private readonly ICallGateProvider<object>? statesChanged;

    // Since 1.8.0.
    private readonly ICallGateProvider<string[]>? getGates;
    private readonly ICallGateProvider<object>? disposing;
    private readonly ICallGateProvider<uint[], string[]>? getStates;
    private readonly ICallGateProvider<string, uint[]>? getQuestsInState;
    private readonly ICallGateProvider<uint, bool, uint[]>? getQuestsInZone;
    private readonly ICallGateProvider<uint, (string, uint, int, int)>? getFirstBlocker;
    private readonly ICallGateProvider<uint, uint[]>? getRoute;
    private readonly ICallGateProvider<uint, uint[]>? getQuestsForItem;
    private readonly ICallGateProvider<uint, (bool, bool, string)>? getMoonlitStatus;
    private readonly ICallGateProvider<uint, uint[]>? getUnlockQuests;
    private readonly ICallGateProvider<uint[]>? getPins;
    private readonly ICallGateProvider<uint, bool, bool>? pinQuest;
    private readonly ICallGateProvider<(uint, byte, long)[]>? getAbandoned;
    private readonly ICallGateProvider<uint, uint>? getNextJobQuest;
    private readonly ICallGateProvider<uint, string, string, object>? questStateChanged;

    // Written on the framework thread only; read from any thread. `masked` says whether `view`'s quest names go
    // through the live spoiler mask; a capture skips the mask until something reads the gates (see MaskWanted).
    private volatile IpcView view = IpcView.Empty;
    private volatile bool masked = true;

    // Set by SessionState.Changed (framework thread); cleared by the capture.
    private volatile bool stale = true;
    private volatile bool disposed;

    // Set by the first gate call from any thread and never cleared: from then on every capture builds the mask.
    private volatile bool consumed;
    private int warned;

    // Framework thread only: what StatesChanged last announced.
    private IReadOnlyDictionary<uint, QuestEvaluation>? announcedStates;
    private ulong? announcedContentId;
    private QuestCatalog? announcedCatalog;
    private ulong? capturedContentId;
    private int capturedVersion = -1;

    // Framework thread only: the per-bundle and per-snapshot pieces of IpcExtras, kept between captures.
    private CatalogBundle? ladderBundle;
    private JobLadder? ladder;
    private CharacterSnapshot? collectiblesFor;
    private CollectibleLookup? collectibles;

    // The logged-in character's pins, refreshed on the framework thread when they or the character change.
    private volatile uint[] pins = [];
    private int pinsVersion = -1;
    private ulong? pinsOwner;

    /// <param name="openQuest">Opens the main window on a quest; called on the framework thread.</param>
    public IpcProvider(IDalamudPluginInterface pluginInterface, IFramework framework, IPluginLog log, SessionState session, Action<QuestRecord> openQuest)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
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
            getMsqPositions = pluginInterface.GetIpcProvider<uint[]>(IpcChannels.GetMsqPositionsGate);
            openQuestGate = pluginInterface.GetIpcProvider<uint, bool>(IpcChannels.OpenQuestGate);
            statesChanged = pluginInterface.GetIpcProvider<object>(IpcChannels.StatesChangedGate);

            getGates = pluginInterface.GetIpcProvider<string[]>(IpcChannels.GetGatesGate);
            disposing = pluginInterface.GetIpcProvider<object>(IpcChannels.DisposingGate);
            getStates = pluginInterface.GetIpcProvider<uint[], string[]>(IpcChannels.GetStatesGate);
            getQuestsInState = pluginInterface.GetIpcProvider<string, uint[]>(IpcChannels.GetQuestsInStateGate);
            getQuestsInZone = pluginInterface.GetIpcProvider<uint, bool, uint[]>(IpcChannels.GetQuestsInZoneGate);
            getFirstBlocker = pluginInterface.GetIpcProvider<uint, (string, uint, int, int)>(IpcChannels.GetFirstBlockerGate);
            getRoute = pluginInterface.GetIpcProvider<uint, uint[]>(IpcChannels.GetRouteGate);
            getQuestsForItem = pluginInterface.GetIpcProvider<uint, uint[]>(IpcChannels.GetQuestsForItemGate);
            getMoonlitStatus = pluginInterface.GetIpcProvider<uint, (bool, bool, string)>(IpcChannels.GetMoonlitStatusGate);
            getUnlockQuests = pluginInterface.GetIpcProvider<uint, uint[]>(IpcChannels.GetUnlockQuestsGate);
            getPins = pluginInterface.GetIpcProvider<uint[]>(IpcChannels.GetPinsGate);
            pinQuest = pluginInterface.GetIpcProvider<uint, bool, bool>(IpcChannels.PinQuestGate);
            getAbandoned = pluginInterface.GetIpcProvider<(uint, byte, long)[]>(IpcChannels.GetAbandonedGate);
            getNextJobQuest = pluginInterface.GetIpcProvider<uint, uint>(IpcChannels.GetNextJobQuestGate);
            questStateChanged = pluginInterface.GetIpcProvider<uint, string, string, object>(IpcChannels.QuestStateChangedGate);

            apiVersion.RegisterFunc(static () => IpcChannels.ApiVersion);
            isReady.RegisterFunc(() => Answer(IpcChannels.IsReadyGate, false, static v => v.IsReady));
            isQuestAvailable.RegisterFunc(id => Answer(IpcChannels.IsQuestAvailableGate, false, v => v.IsQuestAvailable(id)));
            getState.RegisterFunc(id => Answer(IpcChannels.GetStateGate, string.Empty, v => v.State(id)));
            getStateName.RegisterFunc(id => Answer(IpcChannels.GetStateNameGate, string.Empty, v => v.StateName(id)));
            getBlockers.RegisterFunc(id => Answer(IpcChannels.GetBlockersGate, [], v => v.Blockers(id), needsNames: true));
            getMsqPosition.RegisterFunc(() => Answer(IpcChannels.GetMsqPositionGate, 0u, static v => v.MsqNext()));
            getMsqPositions.RegisterFunc(() => Answer(IpcChannels.GetMsqPositionsGate, [], static v => v.MsqPositions()));
            openQuestGate.RegisterFunc(OpenQuest);

            getGates.RegisterFunc(static () => IpcChannels.Names());
            getStates.RegisterFunc(ids => Answer(IpcChannels.GetStatesGate, [], v => v.StatesOf(ids)));
            getQuestsInState.RegisterFunc(state => Answer(IpcChannels.GetQuestsInStateGate, [], v => v.QuestsInState(state)));
            getQuestsInZone.RegisterFunc((territory, readyOnly) => Answer(IpcChannels.GetQuestsInZoneGate, [], v => v.QuestsInZone(territory, readyOnly)));
            getFirstBlocker.RegisterFunc(id => Answer(IpcChannels.GetFirstBlockerGate, IpcBlocker.NoAnswer, v => v.FirstBlocker(id)).ToTuple());
            getRoute.RegisterFunc(id => Answer(IpcChannels.GetRouteGate, [], v => v.Route(id)));
            getQuestsForItem.RegisterFunc(item => Answer(IpcChannels.GetQuestsForItemGate, [], v => v.QuestsForItem(item)));
            getMoonlitStatus.RegisterFunc(item => Answer(IpcChannels.GetMoonlitStatusGate, (false, false, string.Empty), v => v.MoonlitStatus(item, LiveOwnedHere())));
            getUnlockQuests.RegisterFunc(duty => Answer(IpcChannels.GetUnlockQuestsGate, [], v => v.UnlockQuests(duty)));
            getPins.RegisterFunc(GetPins);
            pinQuest.RegisterFunc(PinQuest);
            getAbandoned.RegisterFunc(() => Answer(IpcChannels.GetAbandonedGate, [], static v => v.Abandoned()));
            getNextJobQuest.RegisterFunc(job => Answer(IpcChannels.GetNextJobQuestGate, 0u, v => v.NextJobQuest(job)));
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Tsukimichi IPC gates could not be registered; other plugins will not see them");
            Unregister();
        }

        session.Changed += OnSessionChanged;
        framework.Update += OnUpdate;
    }

    /// <summary>The Moonlit rewards by item, followed per capture (the merged catalog changes with the player's overrides); framework thread.</summary>
    public Func<RewardLookup>? Rewards { get; set; }

    /// <summary>The quests behind each Duty Finder entry, followed per capture; framework thread.</summary>
    public Func<DutyUnlockIndex>? DutyUnlocks { get; set; }

    /// <summary>The game's own owned answer for a Moonlit entry while the logged-in character is on view, on the framework thread; null when it cannot read.</summary>
    public Func<UniqueRewardEntry, bool?>? LiveOwned { get; set; }

    /// <summary>One character's pins in order (<c>QueryRunner.PinsOf</c>); framework thread.</summary>
    public Func<ulong, uint[]>? PinsOf { get; set; }

    /// <summary>Pins or unpins a quest for one character (<c>QueryRunner.SetPin</c>); framework thread.</summary>
    public Func<ulong, uint, bool, bool>? SetPin { get; set; }

    /// <summary>
    /// Moves whenever any character's pins change (<c>QueryRunner.AllPinsVersion</c>), not only the viewed one's: the
    /// logged-in character's pins change while an alt is on view too. Framework thread.
    /// </summary>
    public Func<int>? PinsVersion { get; set; }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        // Tell subscribers first, while the gates still answer, so they can drop what they hold.
        try
        {
            disposing?.SendMessage();
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Tsukimichi.Disposing subscriber failed");
        }

        disposed = true;
        framework.Update -= OnUpdate;
        session.Changed -= OnSessionChanged;
        Unregister();
        view = IpcView.Empty;
    }

    /// <summary>
    /// The capture the gates answer from; a framework-thread caller refreshes a stale one first, or one taken
    /// without the spoiler mask. Marks the provider as consumed, so every later capture builds the mask.
    /// </summary>
    internal IpcView Current
    {
        get
        {
            if (!consumed)
            {
                consumed = true;
                if (!masked)
                {
                    // Off the framework thread the next tick captures with the mask; on it, the capture below does.
                    stale = true;
                }
            }

            if ((stale || !masked) && !disposed && framework.IsInFrameworkUpdateThread)
            {
                Capture();
            }

            return view;
        }
    }

    /// <summary>
    /// For the <c>/tsuki ipc</c> window: how many subscribers a gate's provider counts (meaningful for the messages);
    /// -1 for a name this build does not register or when the count cannot be read.
    /// </summary>
    public int SubscriptionCount(string gate)
    {
        ICallGateProvider? provider = gate switch
        {
            IpcChannels.ApiVersionGate => apiVersion,
            IpcChannels.IsReadyGate => isReady,
            IpcChannels.IsQuestAvailableGate => isQuestAvailable,
            IpcChannels.GetStateGate => getState,
            IpcChannels.GetStateNameGate => getStateName,
            IpcChannels.GetBlockersGate => getBlockers,
            IpcChannels.GetMsqPositionGate => getMsqPosition,
            IpcChannels.GetMsqPositionsGate => getMsqPositions,
            IpcChannels.OpenQuestGate => openQuestGate,
            IpcChannels.StatesChangedGate => statesChanged,
            IpcChannels.GetGatesGate => getGates,
            IpcChannels.DisposingGate => disposing,
            IpcChannels.GetStatesGate => getStates,
            IpcChannels.GetQuestsInStateGate => getQuestsInState,
            IpcChannels.GetQuestsInZoneGate => getQuestsInZone,
            IpcChannels.GetFirstBlockerGate => getFirstBlocker,
            IpcChannels.GetRouteGate => getRoute,
            IpcChannels.GetQuestsForItemGate => getQuestsForItem,
            IpcChannels.GetMoonlitStatusGate => getMoonlitStatus,
            IpcChannels.GetUnlockQuestsGate => getUnlockQuests,
            IpcChannels.GetPinsGate => getPins,
            IpcChannels.PinQuestGate => pinQuest,
            IpcChannels.GetAbandonedGate => getAbandoned,
            IpcChannels.GetNextJobQuestGate => getNextJobQuest,
            IpcChannels.QuestStateChangedGate => questStateChanged,
            _ => null,
        };

        try
        {
            return provider?.SubscriptionCount ?? -1;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    /// <summary>
    /// For the <c>/tsuki ipc</c> window: calls a gate through Dalamud's own subscriber, exactly as another plugin would,
    /// with the arguments typed (<see cref="IpcConsole"/>), and prints the answer or the failure. Framework thread.
    /// </summary>
    public string TestCall(string gate, string arguments)
    {
        var words = IpcConsole.Words(arguments);
        uint First() => words.Length > 0 && IpcConsole.TryUInt(words[0], out var id) ? id : throw new FormatException("Type a number first.");
        bool Second() => words.Length > 1 && IpcConsole.TryBool(words[1], out var flag) ? flag : throw new FormatException("Type true or false after the number.");
        try
        {
            object? answer = gate switch
            {
                IpcChannels.ApiVersionGate => pluginInterface.GetIpcSubscriber<int>(gate).InvokeFunc(),
                IpcChannels.IsReadyGate => pluginInterface.GetIpcSubscriber<bool>(gate).InvokeFunc(),
                IpcChannels.GetGatesGate => pluginInterface.GetIpcSubscriber<string[]>(gate).InvokeFunc(),
                IpcChannels.IsQuestAvailableGate => pluginInterface.GetIpcSubscriber<uint, bool>(gate).InvokeFunc(First()),
                IpcChannels.GetStateGate or IpcChannels.GetStateNameGate => pluginInterface.GetIpcSubscriber<uint, string>(gate).InvokeFunc(First()),
                IpcChannels.GetStatesGate => pluginInterface.GetIpcSubscriber<uint[], string[]>(gate).InvokeFunc(IpcConsole.TryUInts(arguments, out var ids) ? ids : throw new FormatException("Type numbers separated by spaces.")),
                IpcChannels.GetBlockersGate => pluginInterface.GetIpcSubscriber<uint, string[]>(gate).InvokeFunc(First()),
                IpcChannels.GetFirstBlockerGate => pluginInterface.GetIpcSubscriber<uint, (string, uint, int, int)>(gate).InvokeFunc(First()),
                IpcChannels.GetQuestsInStateGate => pluginInterface.GetIpcSubscriber<string, uint[]>(gate).InvokeFunc(arguments.Trim()),
                IpcChannels.GetQuestsInZoneGate => pluginInterface.GetIpcSubscriber<uint, bool, uint[]>(gate).InvokeFunc(First(), Second()),
                IpcChannels.GetMsqPositionGate => pluginInterface.GetIpcSubscriber<uint>(gate).InvokeFunc(),
                IpcChannels.GetMsqPositionsGate or IpcChannels.GetPinsGate => pluginInterface.GetIpcSubscriber<uint[]>(gate).InvokeFunc(),
                IpcChannels.GetRouteGate or IpcChannels.GetQuestsForItemGate or IpcChannels.GetUnlockQuestsGate => pluginInterface.GetIpcSubscriber<uint, uint[]>(gate).InvokeFunc(First()),
                IpcChannels.GetNextJobQuestGate => pluginInterface.GetIpcSubscriber<uint, uint>(gate).InvokeFunc(First()),
                IpcChannels.GetMoonlitStatusGate => pluginInterface.GetIpcSubscriber<uint, (bool, bool, string)>(gate).InvokeFunc(First()),
                IpcChannels.GetAbandonedGate => pluginInterface.GetIpcSubscriber<(uint, byte, long)[]>(gate).InvokeFunc(),
                IpcChannels.PinQuestGate => pluginInterface.GetIpcSubscriber<uint, bool, bool>(gate).InvokeFunc(First(), Second()),
                IpcChannels.OpenQuestGate => pluginInterface.GetIpcSubscriber<uint, bool>(gate).InvokeFunc(First()),
                _ => throw new FormatException("A message cannot be called; subscribe to it instead."),
            };
            return IpcConsole.Format(answer);
        }
        catch (FormatException ex)
        {
            return ex.Message;
        }
        catch (IpcNotReadyError)
        {
            return "IpcNotReadyError: the gate is not registered.";
        }
        catch (Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    /// <summary>
    /// Whether a capture needs the live spoiler mask (built per session <see cref="SessionState.Version"/>, so costly
    /// to take on every change for nobody): once any gate was called, or while a message has a subscriber.
    /// </summary>
    private bool MaskWanted
    {
        get
        {
            if (consumed)
            {
                return true;
            }

            try
            {
                return statesChanged is { SubscriptionCount: > 0 } || questStateChanged is { SubscriptionCount: > 0 };
            }
            catch (Exception)
            {
                return true;
            }
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
            // The view (and the routes it caches) is keyed on the session version, which every live capture bumps; the
            // reward and duty lookups follow the Moonlit catalog (an override rebuilds it) without a session change.
            if (!stale && (session.Version != capturedVersion || (consumed && ExtrasMoved())))
            {
                stale = true;
            }

            if (stale)
            {
                Capture();
            }

            AnnounceChanges();
            if (consumed)
            {
                RefreshPins();
            }
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
        capturedVersion = session.Version;
        capturedContentId = session.LiveContentId;
        if (session.Bundle is not { } bundle)
        {
            // The empty view names nothing, so it needs no mask.
            view = IpcView.Empty;
            masked = true;
            return;
        }

        var live = session.LiveContentId is not null;
        var states = live ? session.LiveStates : null;
        var extras = BuildExtras(bundle, live);
        if (!MaskWanted)
        {
            // Nobody reads the names yet: skip rebuilding the mask. The first gate call recaptures (Current), and
            // GetBlockers, the one answer that prints quest names, never reads this view (Answer).
            masked = false;
            view = new IpcView(bundle.Catalog, states, bundle.BlockerNames(), extras);
            return;
        }

        // The mask is immutable; binding its DisplayName (not session.LiveNames, whose lookup reads the session on
        // each call) keeps the view safe to read from any thread.
        var mask = session.LiveSpoilers;
        var names = bundle.BlockerNames().Through(mask);
        view = new IpcView(bundle.Catalog, states, names, extras);
        masked = true;
    }

    /// <summary>Framework thread: what the 1.8.0 gates read, each piece immutable or copied.</summary>
    private IpcExtras BuildExtras(CatalogBundle bundle, bool live)
    {
        if (!ReferenceEquals(bundle, ladderBundle))
        {
            ladderBundle = bundle;
            ladder = bundle.BuildJobLadder();
        }

        var snapshot = live ? session.LiveSnapshot : null;
        if (!ReferenceEquals(snapshot, collectiblesFor))
        {
            collectiblesFor = snapshot;
            collectibles = CollectibleLookup.For(snapshot);
        }

        return new IpcExtras
        {
            Rewards = Rewards?.Invoke() ?? RewardLookup.Empty,
            DutyUnlocks = DutyUnlocks?.Invoke() ?? DutyUnlockIndex.Empty,
            Ladder = ladder,
            Abandoned = live ? AbandonedLedger.Newest(session.LiveAbandoned) : [],
            SavedCollectibles = collectibles,
            LevelOf = snapshot is null ? null : RouteLevels.For(snapshot, session.Context),
        };
    }

    /// <summary>Whether the reward or duty lookup the view holds is no longer the current one (two reference compares).</summary>
    private bool ExtrasMoved()
    {
        var extras = view.Extras;
        return (Rewards is { } rewards && !ReferenceEquals(rewards(), extras.Rewards))
            || (DutyUnlocks is { } duties && !ReferenceEquals(duties(), extras.DutyUnlocks));
    }

    /// <summary>
    /// Framework thread: sends QuestStateChanged for each quest a live poll moved, then StatesChanged, when the
    /// captured states or character differ from the last announced.
    /// </summary>
    private void AnnounceChanges()
    {
        var current = view.States;
        if (capturedContentId == announcedContentId && !IpcView.StatesDiffer(announcedStates, current))
        {
            // Keep the newest instance so the next comparison starts from it (usually the same reference).
            announcedStates = current;
            return;
        }

        var previous = announcedStates;
        var livePoll = capturedContentId is not null
            && capturedContentId == announcedContentId
            && ReferenceEquals(view.Catalog, announcedCatalog)
            && previous is { Count: > 0 }
            && current.Count > 0;
        announcedStates = current;
        announcedContentId = capturedContentId;
        announcedCatalog = view.Catalog;

        if (livePoll)
        {
            SendQuestChanges(previous, current);
        }

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

    private void SendQuestChanges(IReadOnlyDictionary<uint, QuestEvaluation>? previous, IReadOnlyDictionary<uint, QuestEvaluation> current)
    {
        if (questStateChanged is null)
        {
            return;
        }

        try
        {
            if (questStateChanged.SubscriptionCount == 0)
            {
                return;
            }
        }
        catch (Exception)
        {
            // Read as subscribed.
        }

        if (IpcView.StateChanges(previous, current) is not { } changes)
        {
            log.Debug("Tsukimichi.QuestStateChanged skipped: more than {Max} quests changed in one poll", IpcView.MaxStateChangesPerTick);
            return;
        }

        foreach (var (rowId, from, to) in changes)
        {
            try
            {
                questStateChanged.SendMessage(rowId, from, to);
            }
            catch (Exception ex)
            {
                WarnOnce(ex, "Tsukimichi.QuestStateChanged subscriber failed");
            }
        }
    }

    /// <summary>Framework thread: re-reads the logged-in character's pins when they or the character changed.</summary>
    private void RefreshPins()
    {
        var owner = session.LiveContentId;
        var version = PinsVersion?.Invoke() ?? 0;
        if (owner == pinsOwner && version == pinsVersion)
        {
            return;
        }

        pinsOwner = owner;
        pinsVersion = version;
        pins = owner is { } id && PinsOf is { } read ? read(id) : [];
    }

    /// <summary>GetPins: refreshed first on the framework thread; elsewhere the last copy (at most a frame old).</summary>
    private uint[] GetPins()
    {
        if (disposed)
        {
            return [];
        }

        try
        {
            consumed = true;
            if (framework.IsInFrameworkUpdateThread)
            {
                RefreshPins();
            }

            var current = pins;
            return current.Length == 0 ? [] : (uint[])current.Clone();
        }
        catch (Exception ex)
        {
            WarnOnce(ex, IpcChannels.GetPinsGate + " failed");
            return [];
        }
    }

    /// <summary>
    /// PinQuest: pins or unpins a quest for the logged-in character in Tsukimichi's own list (nothing in the game
    /// changes). On the framework thread the answer is the result; elsewhere the change is queued for the next frame
    /// and true means it was accepted. False for an unknown id, nobody logged in, or before the catalog is built.
    /// </summary>
    private bool PinQuest(uint id, bool pin)
    {
        var quest = Answer(IpcChannels.PinQuestGate, null, v => v.Find(id));
        var owner = capturedContentId;
        if (quest is null || owner is not { } contentId || SetPin is not { } set || disposed)
        {
            return false;
        }

        try
        {
            if (framework.IsInFrameworkUpdateThread)
            {
                return set(contentId, quest.RowId, pin);
            }

            framework.RunOnFrameworkThread(() =>
            {
                if (!disposed && session.LiveContentId == contentId)
                {
                    set(contentId, quest.RowId, pin);
                }
            }).ContinueWith(t => WarnOnce(t.Exception?.GetBaseException(), "Tsukimichi.PinQuest failed"), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return true;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Tsukimichi.PinQuest failed");
            return false;
        }
    }

    /// <summary>The game's owned answer, offered to GetMoonlitStatus only on the framework thread.</summary>
    private Func<UniqueRewardEntry, bool?>? LiveOwnedHere() =>
        !disposed && framework.IsInFrameworkUpdateThread && session.IsLive ? LiveOwned : null;

    /// <param name="needsNames">The answer prints quest names: never taken from a view captured without the spoiler
    /// mask (only possible off the framework thread, until the tick after the first gate call), which answers
    /// <paramref name="fallback"/> instead, as before the catalog is ready.</param>
    private T Answer<T>(string gate, T fallback, Func<IpcView, T> answer, bool needsNames = false)
    {
        if (disposed)
        {
            return fallback;
        }

        try
        {
            var current = Current;
            // Read after the view: Capture clears `masked` before publishing a maskless view and sets it after a masked
            // one, so a view read here is never taken for masked when it is not.
            if (needsNames && !masked)
            {
                return fallback;
            }

            return answer(current);
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
        Unregister(getMsqPositions);
        Unregister(openQuestGate);
        Unregister(getGates);
        Unregister(getStates);
        Unregister(getQuestsInState);
        Unregister(getQuestsInZone);
        Unregister(getFirstBlocker);
        Unregister(getRoute);
        Unregister(getQuestsForItem);
        Unregister(getMoonlitStatus);
        Unregister(getUnlockQuests);
        Unregister(getPins);
        Unregister(pinQuest);
        Unregister(getAbandoned);
        Unregister(getNextJobQuest);
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
