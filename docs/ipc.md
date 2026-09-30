# IPC for other plugins

Tsukimichi answers other Dalamud plugins over IPC: whether a quest can be picked up now, why not, where the main scenario stands, and "show me this quest". An overlay, a route planner or another quest plugin can build on its requirement evaluator instead of writing its own. The gates exist from Tsukimichi 0.9.0 and answer with API version **1**.

Everything here is read-only except `Tsukimichi.OpenQuest`, which opens Tsukimichi's own window. Nothing moves the character, accepts a quest or touches the game on a caller's behalf.

## Quick reference

| Gate | Signature | Returns | Since |
|---|---|---|---|
| `Tsukimichi.ApiVersion` | `() -> int` | `1` | 0.9.0 |
| `Tsukimichi.IsReady` | `() -> bool` | true once the catalog is built and the logged-in character is evaluated | 0.9.0 |
| `Tsukimichi.IsQuestAvailable` | `(uint questId) -> bool` | true for Ready or Ready on another job | 0.9.0 |
| `Tsukimichi.GetState` | `(uint questId) -> string` | the state's enum name (`"Ready"`, `"Blocked"`, …) | 0.9.0 |
| `Tsukimichi.GetStateName` | `(uint questId) -> string` | the state as the window shows it (`"In journal"`, `"Done today"`, …) | 0.9.0 |
| `Tsukimichi.GetBlockers` | `(uint questId) -> string[]` | the status line, then one line per requirement | 0.9.0 |
| `Tsukimichi.GetMsqPosition` | `() -> uint` | Quest row id of the next main scenario quest (inside a branch region, the first route's); 0 when the story is done | 0.9.0 |
| `Tsukimichi.GetMsqPositions` | `() -> uint[]` | Quest row ids of every main scenario position: the next quest, or one per open route inside a branch region; empty when the story is done | 1.0.0 |
| `Tsukimichi.OpenQuest` | `(uint questId) -> bool` | true when the quest exists and the window was asked to show it | 0.9.0 |
| `Tsukimichi.StatesChanged` | message, no arguments | sent after a poll changed the logged-in character's states | 0.9.0 |

The plugin's internal name is `Tsukimichi`.

## Whose data, which ids

**The logged-in character.** Every answer is about the character logged in now, whichever character the player is looking at in Tsukimichi's window. Logged out, there is no character and the gates give their not-ready answers.

**Either id.** Every `questId` argument takes either id space:

- a **Quest sheet row id**, 65536 and up (`66236`), the id xivapi, Garland Tools, FFXIV Collect and Lumina's `Quest` sheet use;
- a **runtime quest id**, 1 to 65535 (`700`), the low 16 bits of the row id, which `QuestManager`, the journal and the completion flags use.

`0`, an id past the sheet and a quest Tsukimichi's catalog does not hold all read as unknown. Ids that come back (`GetMsqPosition`, `GetMsqPositions`, the ids inside requirement lines) are always row ids.

**The spoiler shield.** Quest names inside `GetBlockers` go through the logged-in character's spoiler settings (Settings › Spoilers), exactly as Tsukimichi's own chat lines do: a main scenario quest the player has not reached prints as `Main scenario quest (Lv 83)`. Show the lines as they come and the player's choice is kept.

## Not ready yet

The gates never throw on Tsukimichi's side. Until they can answer they return:

| Situation | `IsReady` | `IsQuestAvailable` | `GetState`, `GetStateName` | `GetBlockers` | `GetMsqPosition` | `OpenQuest` |
|---|---|---|---|---|---|---|
| Catalog still building, or it failed | false | false | `""` | `[]` | 0 | false |
| Catalog built, nobody logged in or the first evaluation after login still running | false | false | `""` | `[]` | 0 | true for a known quest |
| Unknown id | (unchanged) | false | `""` | `[]` | (unchanged) | false |

`GetMsqPosition` returns 0 both when the story is complete and when there is no answer; `IsReady` tells the two apart. `GetMsqPositions` returns an empty array in every case where `GetMsqPosition` returns 0. The first evaluation after login runs on a worker and lands a moment after the character loads; `StatesChanged` is sent when it does.

When Tsukimichi is not installed or not loaded (or is reloading), Dalamud itself throws `IpcNotReadyError` from `InvokeFunc`. Catch it and treat the plugin as absent.

## The gates

### Tsukimichi.ApiVersion

`() -> int`. The API version, `1`. Check it once when Tsukimichi appears and refuse to use the gates when it is not a version you were written for.

### Tsukimichi.IsReady

`() -> bool`. True when the catalog is built and the logged-in character has been evaluated, so every other gate can answer. False while loading, while logged out and during the first evaluation after a login.

### Tsukimichi.IsQuestAvailable

`(uint questId) -> bool`. True when the quest is **Ready** (the character can pick it up now) or **Ready on another job** (every gate is met on a job other than the current one). False for every other state, for an unknown id and when not ready.

### Tsukimichi.GetState

`(uint questId) -> string`. The state as the name of Tsukimichi's `QuestState` enum. These names are stable; branch on them.

| Value | Meaning | `GetStateName` shows |
|---|---|---|
| `Ready` | can be picked up now on the current job | Ready |
| `ReadyOnOtherJob` | can be picked up on another job | Ready on another job |
| `Accepted` | in the journal | In journal |
| `Blocked` | something is still missing; `GetBlockers` says what | Blocked |
| `DoneThisCycle` | a repeatable done until its reset | Done today, Done this week, Done this cycle |
| `Completed` | done for good | Completed |
| `Foreclosed` | can never be done by this character (another Grand Company's quest, a path not taken, removed from the game, a seasonal event that ended) | Locked out |
| `Unknown` | Tsukimichi cannot judge it (achievements not loaded, a condition the game does not expose) | Not checked |

Empty string when there is no answer. A future version may add a state; treat a name you do not know as `Unknown`.

### Tsukimichi.GetStateName

`(uint questId) -> string`. The same state in the words every Tsukimichi surface uses (third column above). Repeatables say `Done today` or `Done this week`. For display; the wording may change in any release. Empty string when there is no answer.

### Tsukimichi.GetBlockers

`(uint questId) -> string[]`. The quest's "why", in the format of Tsukimichi's diagnostic block and `/tsuki why`:

1. **The status line**: the display state, then ` · ` and the one blocker the player must act on first, for a Blocked, Locked out or Not checked quest; `In journal · step 3 of 7` for a quest in the journal; for Ready on another job the job follows in parentheses.
2. **One line per requirement**, in the evaluator's order: `<Kind>: <met | unmet | not checked> (<values compared>)`. `Kind` is the requirement kind's enum name (`Level`, `PreviousQuests`, `ClassJob`, `GrandCompanyRank`, `TribeRank`, `DutyCompletion`, `Seasonal`, …). Quests inside a line are written as row id and name.

```
[
  "Blocked · after: Peace for Thanalan",
  "Level: met (24 ≤ 31)",
  "PreviousQuests: unmet (66753 Peace for Thanalan: not done)",
  "TribeRank: met (Amalj'aa Recognized ≥ Recognized)"
]
```

A Ready quest still lists its (met) requirements after `"Ready"`; a quest with no requirements returns the status line alone. Empty array when there is no answer. The lines are for display; parse `GetState`, not these.

### Tsukimichi.GetMsqPosition

`() -> uint`. The Quest row id of the logged-in character's next main scenario quest: the first one in journal order (A Realm Reborn through Dawntrail) that is neither completed nor on a branch the character did not take. Its state may be Ready, In journal or Blocked (a level gate between patches); ask `GetState`. 0 when every main scenario quest is done, and 0 when there is no answer.

Inside a branch region of the main scenario (from Evercold, 8.0, on: routes that run in parallel from a shared quest and meet again later), this is the **first route's** next quest: the first route in journal order that is not done yet. The quest where the routes meet is never returned while a route it needs is still open. To see every route, call `GetMsqPositions`.

### Tsukimichi.GetMsqPositions

`() -> uint[]`. Every main scenario position of the logged-in character, as Quest row ids. On a linear stretch of the story (all of A Realm Reborn through Dawntrail) it holds one id, the same one `GetMsqPosition` returns. Inside a branch region it holds each open route's next quest, in route order (routes are ordered by where their first quest sits in the journal); a finished route has no entry, and the first entry is always `GetMsqPosition`'s answer. Empty when the story is done and when there is no answer. Each call returns a new array. Added in 1.0.0; on an older Tsukimichi the call throws `IpcNotReadyError`, so fall back to `GetMsqPosition`.

```
[70011, 70021, 70031]   // three routes open, none started
[70023]                 // two routes done, the third at its third quest
[70040]                 // every route done: the quest where they meet
```

### Tsukimichi.OpenQuest

`(uint questId) -> bool`. Opens Tsukimichi's main window on the quest (the Journal tab, scoped to the quest's genre, the quest selected and scrolled into view; narrowing filters that would hide it are cleared). Returns true when the catalog holds the quest and the request was queued, false for an unknown id or while the catalog is still building. It works while logged out: the window can show any quest. Call it from a click, not on your own schedule; opening a window the player did not ask for is not welcome.

### Tsukimichi.StatesChanged

A message with no arguments. Sent after the logged-in character's states changed: a poll that moved any quest's state, its ready-on job or its journal step (a quest accepted, advanced, completed or abandoned, a level-up that opened something, a daily reset), the first evaluation after login, a logout, another character logging in, and a catalog rebuild. Re-ask whatever you show when it arrives. Requirement detail alone (the current level printed inside a `Level` line) can change without it.

## Threads

- **Any thread may call.** Tsukimichi answers from a snapshot of its evaluation taken on the framework thread, made of immutable data, so a gate never reads game memory, never draws and never blocks waiting for a frame. The snapshot is refreshed on the first framework tick after a change; a call made on the framework thread (a `Framework.Update` handler, a `UiBuilder.Draw` handler) refreshes it first when it is stale, and a call from elsewhere sees at most a frame-old answer.
- **Calls are cheap** (a dictionary lookup, plus a few string builds for `GetBlockers`). Still, cache what you draw per frame and refresh it on `StatesChanged` rather than calling every gate for every row every frame.
- **`OpenQuest` returns at once**; the window opens on the framework thread, in the same frame when you call from it.
- **`StatesChanged` arrives on the framework thread**, from Tsukimichi's `Framework.Update` handler. Calling the gates from inside it is fine. Keep the handler short.

## Consumer example

```csharp
using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;

/// <summary>Reads Tsukimichi's quest states; every call degrades to "absent" when Tsukimichi is not loaded.</summary>
public sealed class TsukimichiIpc : IDisposable
{
    private const int SupportedApiVersion = 1;

    private readonly ICallGateSubscriber<int> apiVersion;
    private readonly ICallGateSubscriber<bool> isReady;
    private readonly ICallGateSubscriber<uint, bool> isQuestAvailable;
    private readonly ICallGateSubscriber<uint, string> getState;
    private readonly ICallGateSubscriber<uint, string[]> getBlockers;
    private readonly ICallGateSubscriber<uint> getMsqPosition;
    private readonly ICallGateSubscriber<uint[]> getMsqPositions;
    private readonly ICallGateSubscriber<uint, bool> openQuest;
    private readonly ICallGateSubscriber<object> statesChanged;

    public TsukimichiIpc(IDalamudPluginInterface pluginInterface)
    {
        apiVersion = pluginInterface.GetIpcSubscriber<int>("Tsukimichi.ApiVersion");
        isReady = pluginInterface.GetIpcSubscriber<bool>("Tsukimichi.IsReady");
        isQuestAvailable = pluginInterface.GetIpcSubscriber<uint, bool>("Tsukimichi.IsQuestAvailable");
        getState = pluginInterface.GetIpcSubscriber<uint, string>("Tsukimichi.GetState");
        getBlockers = pluginInterface.GetIpcSubscriber<uint, string[]>("Tsukimichi.GetBlockers");
        getMsqPosition = pluginInterface.GetIpcSubscriber<uint>("Tsukimichi.GetMsqPosition");
        getMsqPositions = pluginInterface.GetIpcSubscriber<uint[]>("Tsukimichi.GetMsqPositions");
        openQuest = pluginInterface.GetIpcSubscriber<uint, bool>("Tsukimichi.OpenQuest");
        statesChanged = pluginInterface.GetIpcSubscriber<object>("Tsukimichi.StatesChanged");

        // Subscribing works whether or not Tsukimichi is loaded yet; the message starts arriving once it is.
        statesChanged.Subscribe(OnStatesChanged);
    }

    /// <summary>Raised on the framework thread whenever the logged-in character's quest states changed.</summary>
    public event Action? Changed;

    /// <summary>Tsukimichi is loaded, speaks a version this code understands, and has evaluated the character.</summary>
    public bool Ready => Try(() => apiVersion.InvokeFunc() == SupportedApiVersion && isReady.InvokeFunc(), false);

    /// <summary>Row id (65536+) or quest id; true for Ready and Ready on another job.</summary>
    public bool IsAvailable(uint questId) => Try(() => isQuestAvailable.InvokeFunc(questId), false);

    /// <summary>"Ready", "ReadyOnOtherJob", "Accepted", "Blocked", "DoneThisCycle", "Completed", "Foreclosed", "Unknown"; "" for no answer.</summary>
    public string State(uint questId) => Try(() => getState.InvokeFunc(questId), string.Empty);

    /// <summary>Status line first, then one line per requirement; display text only.</summary>
    public string[] Blockers(uint questId) => Try(() => getBlockers.InvokeFunc(questId), Array.Empty<string>());

    /// <summary>Row id of the next main scenario quest; 0 when complete or not ready (check <see cref="Ready"/>).</summary>
    public uint NextMsq => Try(() => getMsqPosition.InvokeFunc(), 0u);

    /// <summary>Every main scenario position (one per open route inside a branch region); empty when complete, not ready, or on Tsukimichi before 1.0.0.</summary>
    public uint[] MsqPositions => Try(() => getMsqPositions.InvokeFunc(), Array.Empty<uint>());

    /// <summary>Opens Tsukimichi's window on the quest; call it from a click.</summary>
    public bool Open(uint questId) => Try(() => openQuest.InvokeFunc(questId), false);

    public void Dispose() => statesChanged.Unsubscribe(OnStatesChanged);

    private void OnStatesChanged() => Changed?.Invoke();

    private static T Try<T>(Func<T> call, T absent)
    {
        try
        {
            return call();
        }
        catch (IpcNotReadyError)
        {
            // Tsukimichi is not installed, not loaded, or reloading.
            return absent;
        }
    }
}
```

## Versioning

- `Tsukimichi.ApiVersion` returns the API version, `1` today.
- **Additive changes keep the version**: a new gate, a new message, a new `QuestState` name, a new requirement kind in `GetBlockers`, or new wording in `GetStateName` and the blocker lines. A new gate is listed here with the release that added it; calling it on an older Tsukimichi throws `IpcNotReadyError`, which the example above already treats as absent.
- **Breaking changes bump the version**: renaming or removing a gate, changing a gate's argument or return types, changing what an id argument accepts, or changing the meaning of a return value (what `IsQuestAvailable` counts as available, what an enum name in `GetState` means, 0 in `GetMsqPosition`). A bump is announced in the changelog, and a gate whose meaning changes gets a new name so a caller written for the old version never gets the new answer by accident.
- Gate names never change once released, and nothing is removed within a version.

Questions and requests for new gates: open an issue with the **IPC request** template.
