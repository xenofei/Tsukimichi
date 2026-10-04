// TsukimichiIpc.cs — a drop-in client for Tsukimichi's IPC (docs/ipc.md), API version 1, gates up to Tsukimichi 1.22.0
// (the summary, version 1).
//
// Copy this file into your Dalamud plugin, change the namespace, and create one instance with your plugin interface:
//
//     var tsukimichi = new TsukimichiIpc(pluginInterface);
//     if (tsukimichi.Ready) { var state = tsukimichi.State(66236); }
//     ...
//     tsukimichi.Dispose();
//
// Every call degrades to an "absent" answer (false, "", an empty array, 0) when Tsukimichi is not installed, not
// loaded, reloading, or older than the gate: nothing here throws into your code. The events are raised on the
// framework thread. Use it freely: the file is public domain (CC0); no attribution needed.

using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;

namespace YourPlugin.Integrations;

/// <summary>Reads Tsukimichi's quest states, routes and Moonlit rewards for the logged-in character.</summary>
public sealed class TsukimichiIpc : IDisposable
{
    /// <summary>The API version this file was written for.</summary>
    public const int SupportedApiVersion = 1;

    /// <summary>The summary version this file was written for (Tsukimichi.GetSummaryVersion, since 1.22.0).</summary>
    public const int SupportedSummaryVersion = 1;

    private readonly ICallGateSubscriber<int> apiVersion;
    private readonly ICallGateSubscriber<bool> isReady;
    private readonly ICallGateSubscriber<string[]> getGates;
    private readonly ICallGateSubscriber<uint, bool> isQuestAvailable;
    private readonly ICallGateSubscriber<uint, string> getState;
    private readonly ICallGateSubscriber<uint[], string[]> getStates;
    private readonly ICallGateSubscriber<uint, string> getStateName;
    private readonly ICallGateSubscriber<uint, string[]> getBlockers;
    private readonly ICallGateSubscriber<uint, (string, uint, int, int)> getFirstBlocker;
    private readonly ICallGateSubscriber<string, uint[]> getQuestsInState;
    private readonly ICallGateSubscriber<uint, bool, uint[]> getQuestsInZone;
    private readonly ICallGateSubscriber<uint> getMsqPosition;
    private readonly ICallGateSubscriber<uint[]> getMsqPositions;
    private readonly ICallGateSubscriber<uint, uint[]> getRoute;
    private readonly ICallGateSubscriber<uint, uint> getNextJobQuest;
    private readonly ICallGateSubscriber<uint, uint[]> getQuestsForItem;
    private readonly ICallGateSubscriber<uint, (bool, bool, string)> getMoonlitStatus;
    private readonly ICallGateSubscriber<uint, uint[]> getUnlockQuests;
    private readonly ICallGateSubscriber<(uint, byte, long)[]> getAbandoned;
    private readonly ICallGateSubscriber<uint[]> getPins;
    private readonly ICallGateSubscriber<uint, bool, bool> pinQuest;
    private readonly ICallGateSubscriber<uint, bool> openQuest;
    private readonly ICallGateSubscriber<object> statesChanged;
    private readonly ICallGateSubscriber<uint, string, string, object> questStateChanged;
    private readonly ICallGateSubscriber<object> disposing;
    private readonly ICallGateSubscriber<int> getSummaryVersion;
    private readonly ICallGateSubscriber<(string, string, int)> getCharacter;
    private readonly ICallGateSubscriber<(uint, string, string)> getUpNext;
    private readonly ICallGateSubscriber<(int, int, string)> getReadyCount;
    private readonly ICallGateSubscriber<(int, int)> getJournalRoom;
    private readonly ICallGateSubscriber<(string, int)[]> getEndingSoon;
    private readonly ICallGateSubscriber<(string, int, bool)> getStoryMeter;
    private readonly ICallGateSubscriber<string> getTheme;
    private readonly ICallGateSubscriber<string, bool> openAt;
    private readonly ICallGateSubscriber<string, string, int> addonHello;
    private readonly ICallGateSubscriber<int, (uint, string, string)[]> getReadyTonight;
    private readonly ICallGateSubscriber<object> summaryChanged;

    public TsukimichiIpc(IDalamudPluginInterface pluginInterface)
    {
        apiVersion = pluginInterface.GetIpcSubscriber<int>("Tsukimichi.ApiVersion");
        isReady = pluginInterface.GetIpcSubscriber<bool>("Tsukimichi.IsReady");
        getGates = pluginInterface.GetIpcSubscriber<string[]>("Tsukimichi.GetGates");
        isQuestAvailable = pluginInterface.GetIpcSubscriber<uint, bool>("Tsukimichi.IsQuestAvailable");
        getState = pluginInterface.GetIpcSubscriber<uint, string>("Tsukimichi.GetState");
        getStates = pluginInterface.GetIpcSubscriber<uint[], string[]>("Tsukimichi.GetStates");
        getStateName = pluginInterface.GetIpcSubscriber<uint, string>("Tsukimichi.GetStateName");
        getBlockers = pluginInterface.GetIpcSubscriber<uint, string[]>("Tsukimichi.GetBlockers");
        getFirstBlocker = pluginInterface.GetIpcSubscriber<uint, (string, uint, int, int)>("Tsukimichi.GetFirstBlocker");
        getQuestsInState = pluginInterface.GetIpcSubscriber<string, uint[]>("Tsukimichi.GetQuestsInState");
        getQuestsInZone = pluginInterface.GetIpcSubscriber<uint, bool, uint[]>("Tsukimichi.GetQuestsInZone");
        getMsqPosition = pluginInterface.GetIpcSubscriber<uint>("Tsukimichi.GetMsqPosition");
        getMsqPositions = pluginInterface.GetIpcSubscriber<uint[]>("Tsukimichi.GetMsqPositions");
        getRoute = pluginInterface.GetIpcSubscriber<uint, uint[]>("Tsukimichi.GetRoute");
        getNextJobQuest = pluginInterface.GetIpcSubscriber<uint, uint>("Tsukimichi.GetNextJobQuest");
        getQuestsForItem = pluginInterface.GetIpcSubscriber<uint, uint[]>("Tsukimichi.GetQuestsForItem");
        getMoonlitStatus = pluginInterface.GetIpcSubscriber<uint, (bool, bool, string)>("Tsukimichi.GetMoonlitStatus");
        getUnlockQuests = pluginInterface.GetIpcSubscriber<uint, uint[]>("Tsukimichi.GetUnlockQuests");
        getAbandoned = pluginInterface.GetIpcSubscriber<(uint, byte, long)[]>("Tsukimichi.GetAbandoned");
        getPins = pluginInterface.GetIpcSubscriber<uint[]>("Tsukimichi.GetPins");
        pinQuest = pluginInterface.GetIpcSubscriber<uint, bool, bool>("Tsukimichi.PinQuest");
        openQuest = pluginInterface.GetIpcSubscriber<uint, bool>("Tsukimichi.OpenQuest");
        statesChanged = pluginInterface.GetIpcSubscriber<object>("Tsukimichi.StatesChanged");
        questStateChanged = pluginInterface.GetIpcSubscriber<uint, string, string, object>("Tsukimichi.QuestStateChanged");
        disposing = pluginInterface.GetIpcSubscriber<object>("Tsukimichi.Disposing");
        getSummaryVersion = pluginInterface.GetIpcSubscriber<int>("Tsukimichi.GetSummaryVersion");
        getCharacter = pluginInterface.GetIpcSubscriber<(string, string, int)>("Tsukimichi.GetCharacter");
        getUpNext = pluginInterface.GetIpcSubscriber<(uint, string, string)>("Tsukimichi.GetUpNext");
        getReadyCount = pluginInterface.GetIpcSubscriber<(int, int, string)>("Tsukimichi.GetReadyCount");
        getJournalRoom = pluginInterface.GetIpcSubscriber<(int, int)>("Tsukimichi.GetJournalRoom");
        getEndingSoon = pluginInterface.GetIpcSubscriber<(string, int)[]>("Tsukimichi.GetEndingSoon");
        getStoryMeter = pluginInterface.GetIpcSubscriber<(string, int, bool)>("Tsukimichi.GetStoryMeter");
        getTheme = pluginInterface.GetIpcSubscriber<string>("Tsukimichi.GetTheme");
        openAt = pluginInterface.GetIpcSubscriber<string, bool>("Tsukimichi.OpenAt");
        addonHello = pluginInterface.GetIpcSubscriber<string, string, int>("Tsukimichi.AddonHello");
        getReadyTonight = pluginInterface.GetIpcSubscriber<int, (uint, string, string)[]>("Tsukimichi.GetReadyTonight");
        summaryChanged = pluginInterface.GetIpcSubscriber<object>("Tsukimichi.SummaryChanged");

        // Subscribing works whether or not Tsukimichi is loaded yet; the messages start arriving once it is.
        statesChanged.Subscribe(OnStatesChanged);
        questStateChanged.Subscribe(OnQuestStateChanged);
        disposing.Subscribe(OnDisposing);
        summaryChanged.Subscribe(OnSummaryChanged);
    }

    /// <summary>The logged-in character's quest states changed: read again what you show.</summary>
    public event Action? Changed;

    /// <summary>One quest's state changed in a live poll (row id, old state, new state; "" when it had none). Since 1.8.0.</summary>
    public event Action<uint, string, string>? QuestChanged;

    /// <summary>Tsukimichi is unloading: drop what you cached. Since 1.8.0.</summary>
    public event Action? Unloading;

    /// <summary>Anything the summary answers changed: read it again. Since 1.22.0.</summary>
    public event Action? SummaryChanged;

    /// <summary>Tsukimichi is loaded, speaks a version this file understands, and has evaluated the character.</summary>
    public bool Ready => Try(() => apiVersion.InvokeFunc() == SupportedApiVersion && isReady.InvokeFunc(), false);

    /// <summary>Every gate and message the installed Tsukimichi registers; empty before 1.8.0 or when absent.</summary>
    public string[] Gates => Try(() => getGates.InvokeFunc(), Array.Empty<string>());

    /// <summary>Whether the installed Tsukimichi has a gate (feature detection).</summary>
    public bool Has(string gate) => Array.IndexOf(Gates, gate) >= 0;

    /// <summary>Row id (65536+) or quest id; true for Ready and Ready on another job.</summary>
    public bool IsAvailable(uint questId) => Try(() => isQuestAvailable.InvokeFunc(questId), false);

    /// <summary>"Ready", "ReadyOnOtherJob", "Accepted", "Blocked", "DoneThisCycle", "Completed", "Foreclosed", "Unknown"; "" for no answer.</summary>
    public string State(uint questId) => Try(() => getState.InvokeFunc(questId), string.Empty);

    /// <summary><see cref="State"/> for many quests in one call, in order. Since 1.8.0.</summary>
    public string[] States(uint[] questIds) => Try(() => getStates.InvokeFunc(questIds), Array.Empty<string>());

    /// <summary>The state as Tsukimichi's window words it ("In journal", "Done today"); display only.</summary>
    public string StateName(uint questId) => Try(() => getStateName.InvokeFunc(questId), string.Empty);

    /// <summary>Status line first, then one line per requirement; display text only.</summary>
    public string[] Blockers(uint questId) => Try(() => getBlockers.InvokeFunc(questId), Array.Empty<string>());

    /// <summary>The first blocker in a stable vocabulary ("level", "quest", "job", …; "none" when nothing blocks). Since 1.8.0.</summary>
    public (string Kind, uint RefId, int Need, int Have) FirstBlocker(uint questId) =>
        Try(() => getFirstBlocker.InvokeFunc(questId), (string.Empty, 0u, 0, 0));

    /// <summary>Row ids of every quest in a state ("Ready", "Accepted", …), in journal order. Since 1.8.0.</summary>
    public uint[] QuestsInState(string state) => Try(() => getQuestsInState.InvokeFunc(state), Array.Empty<uint>());

    /// <summary>Quests whose giver stands in a zone (TerritoryType id); readyOnly keeps the ones that can be picked up. Since 1.8.0.</summary>
    public uint[] QuestsInZone(uint territoryId, bool readyOnly) => Try(() => getQuestsInZone.InvokeFunc(territoryId, readyOnly), Array.Empty<uint>());

    /// <summary>Row id of the next main scenario quest; 0 when complete or not ready (check <see cref="Ready"/>).</summary>
    public uint NextMsq => Try(() => getMsqPosition.InvokeFunc(), 0u);

    /// <summary>Every main scenario position (one per open route inside a branch region). Since 1.0.0.</summary>
    public uint[] MsqPositions => Try(() => getMsqPositions.InvokeFunc(), Array.Empty<uint>());

    /// <summary>The unlock route to a quest: its steps' row ids in order, the target last; empty when done. Since 1.8.0.</summary>
    public uint[] Route(uint targetRowId) => Try(() => getRoute.InvokeFunc(targetRowId), Array.Empty<uint>());

    /// <summary>The next quest of a class's or job's line (ClassJob id); 0 when done. Since 1.8.0.</summary>
    public uint NextJobQuest(uint classJobId) => Try(() => getNextJobQuest.InvokeFunc(classJobId), 0u);

    /// <summary>Quests that reward an item (HQ ids accepted). Since 1.8.0.</summary>
    public uint[] QuestsForItem(uint itemId) => Try(() => getQuestsForItem.InvokeFunc(itemId), Array.Empty<uint>());

    /// <summary>Whether an item is a quest-only reward, owned, and how that is known ("live", "saved", "quest", "unknown"). Since 1.8.0.</summary>
    public (bool Unique, bool Owned, string Confidence) MoonlitStatus(uint itemId) =>
        Try(() => getMoonlitStatus.InvokeFunc(itemId), (false, false, string.Empty));

    /// <summary>Quests that unlock a Duty Finder entry (ContentFinderCondition id). Since 1.8.0.</summary>
    public uint[] UnlockQuests(uint contentFinderConditionId) => Try(() => getUnlockQuests.InvokeFunc(contentFinderConditionId), Array.Empty<uint>());

    /// <summary>Quests abandoned mid-way: row id, the step left at, when (Unix seconds). Since 1.8.0.</summary>
    public (uint RowId, byte Step, long AbandonedUnixSeconds)[] Abandoned =>
        Try(() => getAbandoned.InvokeFunc(), Array.Empty<(uint, byte, long)>());

    /// <summary>The quests pinned in Tsukimichi, in the order they were pinned. Since 1.8.0.</summary>
    public uint[] Pins => Try(() => getPins.InvokeFunc(), Array.Empty<uint>());

    /// <summary>Pins or unpins a quest in Tsukimichi's own list; call it from a click. Since 1.8.0.</summary>
    public bool Pin(uint questId, bool pinned) => Try(() => pinQuest.InvokeFunc(questId, pinned), false);

    /// <summary>Opens Tsukimichi's window on the quest; call it from a click.</summary>
    public bool Open(uint questId) => Try(() => openQuest.InvokeFunc(questId), false);

    // ---- The summary (since 1.22.0): tonight for the logged-in character, names already through the spoiler shield.

    /// <summary>The summary is there and speaks a version this file understands.</summary>
    public bool SummaryReady => Try(() => getSummaryVersion.InvokeFunc() == SupportedSummaryVersion, false);

    /// <summary>The character's name, job ("WHM") and level; empty before one is evaluated.</summary>
    public (string Name, string Job, int Level) Character => Try(() => getCharacter.InvokeFunc(), (string.Empty, string.Empty, 0));

    /// <summary>Up next: row id (0 for none), its name and its step or place line.</summary>
    public (uint RowId, string Name, string Step) UpNext => Try(() => getUpNext.InvokeFunc(), (0u, string.Empty, string.Empty));

    /// <summary>Quests Ready on the current job, quests that can start in this zone, and the job.</summary>
    public (int Ready, int Here, string Job) ReadyCount => Try(() => getReadyCount.InvokeFunc(), (0, 0, string.Empty));

    /// <summary>Journal slots used and in all; (-1, 0) when not read.</summary>
    public (int Used, int Cap) JournalRoom => Try(() => getJournalRoom.InvokeFunc(), (-1, 0));

    /// <summary>Seasonal events ending soon, soonest first, with whole days left (0: today).</summary>
    public (string Name, int DaysLeft)[] EndingSoon => Try(() => getEndingSoon.InvokeFunc(), Array.Empty<(string, int)>());

    /// <summary>The main scenario part, quests left to the latest story, and whether the story is caught up.</summary>
    public (string Part, int LeftToLatest, bool CaughtUp) StoryMeter => Try(() => getStoryMeter.InvokeFunc(), (string.Empty, 0, false));

    /// <summary>The theme in use ("medallion", "classic", "ishgard-glass", "aether-crystal", "astrologian-orrery", "sumi-to-kinpaku").</summary>
    public string Theme => Try(() => getTheme.InvokeFunc(), string.Empty);

    /// <summary>Opens Tsukimichi at "main", "tonight", "upnext", "route", "settings" or "makeroom"; never travels. Call it from a click.</summary>
    public bool OpenAt(string place) => Try(() => openAt.InvokeFunc(place), false);

    /// <summary>Says your add-on is there (Tsukimichi's Settings › About shows its version); returns the summary version, 0 when absent.</summary>
    public int Hello(string addon, string version) => Try(() => addonHello.InvokeFunc(addon, version), 0);

    /// <summary>The first <paramref name="max"/> Ready quests in Tonight's order (at most 20): row id, shielded name, the giver's zone.</summary>
    public (uint RowId, string Name, string Place)[] ReadyTonight(int max) => Try(() => getReadyTonight.InvokeFunc(max), Array.Empty<(uint, string, string)>());

    public void Dispose()
    {
        statesChanged.Unsubscribe(OnStatesChanged);
        questStateChanged.Unsubscribe(OnQuestStateChanged);
        disposing.Unsubscribe(OnDisposing);
        summaryChanged.Unsubscribe(OnSummaryChanged);
    }

    private void OnStatesChanged() => Changed?.Invoke();

    private void OnQuestStateChanged(uint rowId, string from, string to) => QuestChanged?.Invoke(rowId, from, to);

    private void OnDisposing() => Unloading?.Invoke();

    private void OnSummaryChanged() => SummaryChanged?.Invoke();

    private static T Try<T>(Func<T> call, T absent)
    {
        try
        {
            return call();
        }
        catch (IpcNotReadyError)
        {
            // Tsukimichi is not installed, not loaded, reloading, or older than the gate.
            return absent;
        }
        catch (IpcTypeMismatchError)
        {
            // A Tsukimichi with another signature for this gate (never within API version 1).
            return absent;
        }
    }
}
