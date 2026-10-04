# IPC for other plugins

Tsukimichi answers other Dalamud plugins over IPC: whether a quest can be picked up now, why not, where the main scenario stands, and "show me this quest". An overlay, a route planner or another quest plugin can build on its requirement evaluator instead of writing its own. The gates exist from Tsukimichi 0.9.0 and answer with API version **1**.

Everything here is read-only except `Tsukimichi.OpenQuest`, which opens Tsukimichi's own window, and `Tsukimichi.PinQuest` (1.8.0), which changes Tsukimichi's own pin list. Nothing moves the character, accepts a quest or touches the game on a caller's behalf.

## Quick reference

| Gate | Signature | Returns | Since |
|---|---|---|---|
| `Tsukimichi.ApiVersion` | `() -> int` | `1` | 0.9.0 |
| `Tsukimichi.IsReady` | `() -> bool` | true once the catalog is built and the logged-in character is evaluated | 0.9.0 |
| `Tsukimichi.GetGates` | `() -> string[]` | the name of every gate and message this build registers (feature detection) | 1.8.0 |
| `Tsukimichi.IsQuestAvailable` | `(uint questId) -> bool` | true for Ready or Ready on another job | 0.9.0 |
| `Tsukimichi.GetState` | `(uint questId) -> string` | the state's enum name (`"Ready"`, `"Blocked"`, …) | 0.9.0 |
| `Tsukimichi.GetStates` | `(uint[] questIds) -> string[]` | `GetState` for each id, in order | 1.8.0 |
| `Tsukimichi.GetStateName` | `(uint questId) -> string` | the state as the window shows it (`"In journal"`, `"Done today"`, …) | 0.9.0 |
| `Tsukimichi.GetBlockers` | `(uint questId) -> string[]` | the status line, then one line per requirement | 0.9.0 |
| `Tsukimichi.GetFirstBlocker` | `(uint questId) -> (string kind, uint refId, int need, int have)` | the one blocker to act on first, in a stable vocabulary | 1.8.0 |
| `Tsukimichi.GetQuestsInState` | `(string state) -> uint[]` | row ids of every quest in that state, in journal order | 1.8.0 |
| `Tsukimichi.GetQuestsInZone` | `(uint territoryId, bool readyOnly) -> uint[]` | quests whose giver stands in the zone; with `readyOnly`, the ones that can be picked up | 1.8.0 |
| `Tsukimichi.GetMsqPosition` | `() -> uint` | Quest row id of the next main scenario quest (inside a branch region, the first route's); 0 when the story is done | 0.9.0 |
| `Tsukimichi.GetMsqPositions` | `() -> uint[]` | Quest row ids of every main scenario position: the next quest, or one per open route inside a branch region; empty when the story is done | 1.0.0 |
| `Tsukimichi.GetRoute` | `(uint targetRowId) -> uint[]` | the unlock route's steps to the quest, in order, the target last | 1.8.0 |
| `Tsukimichi.GetNextJobQuest` | `(uint classJobId) -> uint` | the next quest of a class's or job's quest line; 0 when done | 1.8.0 |
| `Tsukimichi.GetQuestsForItem` | `(uint itemId) -> uint[]` | quests that reward the item | 1.8.0 |
| `Tsukimichi.GetMoonlitStatus` | `(uint itemId) -> (bool unique, bool owned, string confidence)` | whether the item is a quest-only reward, whether the character has it, and how that is known | 1.8.0 |
| `Tsukimichi.GetUnlockQuests` | `(uint contentFinderConditionId) -> uint[]` | quests that unlock the Duty Finder entry | 1.8.0 |
| `Tsukimichi.GetAbandoned` | `() -> (uint rowId, byte step, long abandonedUnixSeconds)[]` | quests abandoned mid-way and not taken up again, newest first | 1.8.0 |
| `Tsukimichi.GetPins` | `() -> uint[]` | the quests pinned in Tsukimichi, in the order they were pinned | 1.8.0 |
| `Tsukimichi.PinQuest` | `(uint questId, bool pinned) -> bool` | pins or unpins a quest in Tsukimichi's own list | 1.8.0 |
| `Tsukimichi.OpenQuest` | `(uint questId) -> bool` | true when the quest exists and the window was asked to show it | 0.9.0 |
| `Tsukimichi.StatesChanged` | message, no arguments | sent after a poll changed the logged-in character's states | 0.9.0 |
| `Tsukimichi.QuestStateChanged` | message `(uint rowId, string from, string to)` | one per quest whose state a live poll moved | 1.8.0 |
| `Tsukimichi.Disposing` | message, no arguments | sent once as Tsukimichi unloads, before the gates go away | 1.8.0 |

The plugin's internal name is `Tsukimichi`. Every argument and answer is a primitive, an array of primitives or a value tuple of them, so no shared type is needed: copy [`TsukimichiIpc.cs`](TsukimichiIpc.cs), a drop-in client that wraps every gate, or use the examples below.

## Whose data, which ids

**The logged-in character.** Every answer is about the character logged in now, whichever character the player is looking at in Tsukimichi's window. Logged out, there is no character and the gates give their not-ready answers.

**Either id.** Every `questId` argument takes either id space:

- a **Quest sheet row id**, 65536 and up (`66236`), the id xivapi, Garland Tools, FFXIV Collect and Lumina's `Quest` sheet use;
- a **runtime quest id**, 1 to 65535 (`700`), the low 16 bits of the row id, which `QuestManager`, the journal and the completion flags use.

`0`, an id past the sheet and a quest Tsukimichi's catalog does not hold all read as unknown. Ids that come back (`GetMsqPosition`, `GetMsqPositions`, the ids inside requirement lines, and every quest id the 1.8.0 gates return) are always row ids.

**Other ids.** `territoryId` is a TerritoryType row id (`ClientState.TerritoryType`), `itemId` an Item row id (HQ ids, 1,000,000 and up, and collectable ids are folded to the base item), `contentFinderConditionId` a ContentFinderCondition row id (the Duty Finder entry), `classJobId` a ClassJob row id.

**The spoiler shield.** Quest names inside `GetBlockers` go through the logged-in character's spoiler settings (Settings › Spoilers), exactly as Tsukimichi's own chat lines do: a main scenario quest the player has not reached prints as `Main scenario quest (Lv 83)`. Show the lines as they come and the player's choice is kept. The 1.8.0 gates return ids, never names; if you print a name for one of them, a masked quest is one `GetBlockers` would mask, and the polite thing is to ask Tsukimichi (`GetBlockers`' status line names it as the player wants).

## Not ready yet

The gates never throw on Tsukimichi's side. Until they can answer they return:

| Situation | `IsReady` | `IsQuestAvailable` | `GetState`, `GetStateName` | `GetBlockers` | `GetMsqPosition` | `OpenQuest` |
|---|---|---|---|---|---|---|
| Catalog still building, or it failed | false | false | `""` | `[]` | 0 | false |
| Catalog built, nobody logged in or the first evaluation after login still running | false | false | `""` | `[]` | 0 | true for a known quest |
| Unknown id | (unchanged) | false | `""` | `[]` | (unchanged) | false |

`GetMsqPosition` returns 0 both when the story is complete and when there is no answer; `IsReady` tells the two apart. `GetMsqPositions` returns an empty array in every case where `GetMsqPosition` returns 0. The first evaluation after login runs on a worker and lands a moment after the character loads; `StatesChanged` is sent when it does.

The 1.8.0 gates follow the same rule:

| Gate | Catalog building or failed | Nobody logged in, or first evaluation running | Unknown id or argument |
|---|---|---|---|
| `GetGates` | the full list (it never depends on data) | the full list | — |
| `GetStates` | one `""` per id | one `""` per id | `""` in its place; `[]` for a null array |
| `GetQuestsInState`, `GetQuestsInZone`, `GetRoute`, `GetAbandoned` | `[]` | `[]` | `[]` (a state name `GetState` never returns, territory 0) |
| `GetFirstBlocker` | `("", 0, 0, 0)` | `("", 0, 0, 0)` | `("", 0, 0, 0)` |
| `GetNextJobQuest` | 0 | 0 | 0 |
| `GetQuestsForItem`, `GetUnlockQuests` | `[]` | **answered** (data only, no character needed) | `[]` |
| `GetMoonlitStatus` | `(false, false, "")` | `unique` answered, `owned` false, confidence `"unknown"` | `(false, false, "")` |
| `GetPins` | `[]` | `[]` | — |
| `PinQuest` | false | false | false |

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
| `Foreclosed` | can never be done by this character (another city's start, another starting class, another Grand Company's quest, another choice of a set only one of which can be done, removed from the game, a seasonal event that ended) | Locked out |
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

A quest on a path the character did not take (since 1.2) reads `Foreclosed`, and its first requirement line is `OtherPath`: each choice the quest belongs to with its options and the character's own, then the quests that decided it. Its name is never masked (it tells nothing of the character's own story).

```
[
  "Locked out · Another starting class (Archer)",
  "OtherPath: unmet (StartCity Gridania, chosen Gridania; StartClass Archer, chosen Lancer; by 65621 Close to Home, 65559 Way of the Lancer)",
  …
]
```

An option of a choice the character has not made yet (one company's version of a quest, one of two stelae) keeps its own state and gains `Choose one of 3` at the end of its status line.

### Tsukimichi.GetMsqPosition

`() -> uint`. The Quest row id of the logged-in character's next main scenario quest: the first one in journal order (A Realm Reborn through Dawntrail) that is neither completed nor on a branch the character did not take (since 1.2 that includes the other start cities' and starting classes' lines, so it never stops at another city's "Close to Home"). Its state may be Ready, In journal or Blocked (a level gate between patches); ask `GetState`. 0 when every main scenario quest is done, and 0 when there is no answer.

Inside a branch region of the main scenario (from Evercold, 8.0, on: routes that run in parallel from a shared quest and meet again later), this is the **first route's** next quest: the first route in journal order that is not done yet. The quest where the routes meet is never returned while a route it needs is still open; once the game opens it (or no route is left to play), it is the answer again. To see every route, call `GetMsqPositions`.

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

## Gates added in 1.8.0

All additive: the API version stays `1`. On an older Tsukimichi each throws `IpcNotReadyError`; call `GetGates` once (absent before 1.8.0) to see what the installed build offers.

### Tsukimichi.GetGates

`() -> string[]`. The name of every gate and message this build registers, in this page's order (the Quick reference above). A fresh array per call. Use it for feature detection instead of calling a gate to see whether it throws.

### Tsukimichi.Disposing

A message with no arguments, sent once on the framework thread when Tsukimichi unloads (disabled, updated, reloaded, or the game closing), **before** its gates are unregistered. Drop what you cached; the gates throw `IpcNotReadyError` from the next frame on. When Tsukimichi loads again it sends `StatesChanged` after its first evaluation.

### Tsukimichi.GetStates

`(uint[] questIds) -> string[]`. `GetState` for every id in one call, in the same order: one dictionary lookup each, so asking for a few hundred is fine. `""` where there is no answer (an unknown id, or not ready). A null or empty array returns an empty array.

```
GetStates([66236, 700, 1]) -> ["Completed", "Ready", ""]
```

### Tsukimichi.GetQuestsInState

`(string state) -> uint[]`. The row ids of every quest whose state is `state`, spelled exactly as `GetState` returns it (`"Ready"`, `"ReadyOnOtherJob"`, `"Accepted"`, `"Blocked"`, `"DoneThisCycle"`, `"Completed"`, `"Foreclosed"`, `"Unknown"`; case matters), in journal order. Removed quests are included when the character's state for them is that state (a completed removed quest is `Completed`). Empty for any other string and when there is no answer.

```
GetQuestsInState("Accepted") -> [66045, 67112, 70210]   // the journal, in journal order
```

### Tsukimichi.GetQuestsInZone

`(uint territoryId, bool readyOnly) -> uint[]`. The quests whose giver stands in the zone (the quest's issuer as the Quest sheet places it), in journal order; quests the game removed are left out. With `readyOnly` only those `IsQuestAvailable` answers true for (Ready, or Ready on another job); allied society dailies not offered today read Blocked and are left out. Without it, every quest of the zone whatever its state. Empty when there is no answer.

### Tsukimichi.GetFirstBlocker

`(uint questId) -> (string kind, uint refId, int need, int have)`. The one thing to act on first, as data rather than text: the evaluator's first unmet requirement (the one `GetBlockers`' status line names), in a vocabulary of its own that does not follow Tsukimichi's internal names. `have` is -1 when Tsukimichi could not read the value.

| `kind` | Means | `refId` | `need` / `have` |
|---|---|---|---|
| `""` | no answer (not ready, unknown id) | 0 | 0 / 0 |
| `none` | nothing blocks: Ready, in the journal, done, or done this cycle | 0 | 0 / 0 |
| `level` | the job's level is too low | the ClassJob the level was read on (0: the current job) | levels (have -1: no job can take the quest) |
| `quest` | a previous quest is not done | the quest to do next (row id) | quests needed / done (an "any of" list needs 1) |
| `job` | needs another class or job (and for Ready on another job: the job it is open on) | ClassJob row id | 0 / 0 |
| `jobCategory` | needs one of a category's jobs | ClassJobCategory row id | 0 / 0 |
| `grandCompany` | needs a Grand Company | GrandCompany row id | that id / the character's (0: none) |
| `grandCompanyRank` | needs a Grand Company rank | GrandCompany row id | GrandCompanyRank row ids |
| `alliedSocietyRank` | needs an allied society rank | BeastTribe row id | ranks |
| `alliedSocietyReputation` | needs allied society reputation | BeastTribe row id | reputation points |
| `alliedSocietyAllowance` | no allied society allowances left today | 0 | 1 / allowances left |
| `notOfferedToday` | an allied society daily its giver does not offer today | the quest's row id | 1 / 0 |
| `duty` | a duty must be cleared | the first InstanceContent row id the quest lists | duties needed / cleared |
| `seasonal` | its seasonal event is not running (or its chapter is not open) | Festival row id | 1 / 0 |
| `expansion` | the account does not own the expansion | ExVersion row id | that id / the newest owned |
| `levelCap` | above the account's level cap | 0 | the quest's level / the cap |
| `otherPath` | on a path the character did not take (another city, class, Grand Company or choice): locked out for good | a quest that decided it, or 0 | 0 / 0 |
| `lockedOut` | a quest that forecloses this one is done: locked out for good | that quest's row id | 0 / 0 |
| `removed` | the game removed the quest | 0 | 0 / 0 |
| `achievement` | needs an achievement | Achievement row id | 1 / 0 (have -1: the list is not loaded yet) |
| `mount` | needs a mount, or every mount of a collection (the seven Lanners before the Firebird) | the Mount row id still missing, else the first one needed | 1 / 0 or 1 (-1: not read) |
| `house` | needs a house | 0 | 1 / 0 or 1 (-1: not read) |
| `customDeliveryRank` | needs a custom delivery satisfaction rank | SatisfactionNpc row id | ranks (have -1: not read) |
| `carrierLevel` | needs a Delivery Moogle carrier level | 0 | levels (have -1: not read) |
| `unchecked` | a condition the game does not expose (the quest reads Not checked): an accept condition, or a game gate Tsukimichi cannot read (a relic weapon gate before the gear was captured) | the first condition id; 0 for a game gate | 0 / 0 |
| `gameGate` | a relic weapon at the right stage is not equipped (or not held), judged from the gear Tsukimichi read (1.10) | the first weapon (Item row id) of the passing group the character carries but has not equipped, else of the gate's first group | 1 / 0 |
| `other` | something this vocabulary has no word for yet | 0 | 0 / 0 |

The words are frozen: none is renamed or removed within API version 1. A later release may add one; treat a word you do not know as `other`.

```
GetFirstBlocker(66753) -> ("quest", 66752, 1, 0)
GetFirstBlocker(70011) -> ("level", 0, 100, 96)
GetFirstBlocker(66236) -> ("none", 0, 0, 0)
```

### Tsukimichi.GetRoute

`(uint targetRowId) -> uint[]`. The unlock route to the quest, as Tsukimichi's route window plans it for the logged-in character: the row ids of every quest still to do, in an order that never puts a quest before what it requires (and, among quests that can come in either order, the lower level first), the target last. Done quests are left out; a quest already done returns an empty array, as does a quest no route reaches. Where an "any of" join leaves a choice, the route takes the shortest way; only the route window shows the alternatives. Planned once per quest per session change (a poll that moved a state, a level-up, another character), so asking again is cheap; a fresh array per call.

### Tsukimichi.GetNextJobQuest

`(uint classJobId) -> uint`. The next quest of a class's or job's quest line, as the Characters tab shows it: the first quest that is neither done nor locked out, in level then journal order (a job's line starts with its class's quests, then its unlock quest). 0 when the line is done, for a ClassJob with no quests, and when there is no answer. Its state may be anything; ask `GetState` or `GetFirstBlocker`.

### Tsukimichi.GetQuestsForItem

`(uint itemId) -> uint[]`. The quests that reward the item, as a sheet reward (fixed or optional) or as a Moonlit reward's item (the item that teaches a mount, a minion, an orchestrion roll…), in journal order; removed quests are left out. HQ and collectable ids are folded to the base item. Data only: it answers whenever the catalog is built, logged in or not.

### Tsukimichi.GetMoonlitStatus

`(uint itemId) -> (bool unique, bool owned, string confidence)`. Whether the item is a Moonlit reward (a reward only a quest gives, as the Moonlit tab lists it), whether the logged-in character has it, and how that is known:

| `confidence` | Means |
|---|---|
| `live` | read from the game now (the character is logged in and on view in Tsukimichi, and the call came on the framework thread) |
| `saved` | from the character's last capture (owned collectibles are saved with each capture since 1.5) |
| `quest` | the reward comes with its quest (an action, a trait, a job, a system unlock), so the quest's completion answers |
| `unknown` | nothing can tell (an item reward, or a capture from before 1.5); `owned` reads false |
| `""` | the item is no Moonlit reward (`unique` false), or no answer |

### Tsukimichi.GetUnlockQuests

`(uint contentFinderConditionId) -> uint[]`. The quests that unlock the Duty Finder entry, the same list as Tsukimichi's hint beside the Duty Finder, in journal order; removed quests are left out. Data only: answers whenever the catalog is built.

### Tsukimichi.GetAbandoned

`() -> (uint rowId, byte step, long abandonedUnixSeconds)[]`. The quests the logged-in character abandoned mid-way and has not taken up again (Tsukimichi notices the journal losing a quest that was not completed), newest first: the row id, the step it was left at (0 when unknown) and when, as Unix seconds in UTC.

### Tsukimichi.GetPins

`() -> uint[]`. The quests pinned in Tsukimichi for the logged-in character (the todo overlay's Pinned section), in the order they were pinned. A fresh array per call. Pins change without `StatesChanged`; ask again when you draw.

### Tsukimichi.PinQuest

`(uint questId, bool pinned) -> bool`. Pins (`true`) or unpins (`false`) a quest for the logged-in character in Tsukimichi's own list, saved as a pin made in the window is. Only Tsukimichi's data changes: nothing in the game, no other plugin's list. Called on the framework thread it answers whether the quest now is as asked (true also when it already was); from another thread the change is queued for the next frame and true means it was accepted. False for an unknown id, nobody logged in, or before the catalog is built. Call it from a click.

### Tsukimichi.QuestStateChanged

A message `(uint rowId, string from, string to)`, sent on the framework thread once per quest whose state a live poll moved (accepted, completed, abandoned, opened by a level-up, reset at the daily reset), just before `StatesChanged`. `from` and `to` are `GetState` names; `""` for a quest that had no state (or has none any more). It is **not** sent for a login, the first evaluation after one, another character logging in, a logout or a catalog rebuild: those send `StatesChanged` alone, so re-read everything then. When more than 64 quests change in one poll (a daily reset, a level-up opening dozens), none are sent and `StatesChanged` alone says to re-read. It is only computed while it has a subscriber.

```
QuestStateChanged(66236, "Accepted", "Completed")
QuestStateChanged(66237, "Blocked", "Ready")
StatesChanged()
```

## The /tsuki ipc window

`/tsuki ipc` (not listed in `/tsuki help`) opens a developer window: every gate and message with its signature, the release that added it and the subscriber count Dalamud reports for it, and a test-call box that calls a gate through Dalamud's IPC exactly as another plugin would (`66236`, `132 true`, `1 2 3`). `PinQuest` and `OpenQuest` act for real there.

## Threads

- **Any thread may call.** Tsukimichi answers from a snapshot of its evaluation taken on the framework thread, made of immutable data, so a gate never reads game memory, never draws and never blocks waiting for a frame. The snapshot is refreshed on the first framework tick after a change; a call made on the framework thread (a `Framework.Update` handler, a `UiBuilder.Draw` handler) refreshes it first when it is stale, and a call from elsewhere sees at most a frame-old answer.
- **Calls are cheap** (a dictionary lookup, plus a few string builds for `GetBlockers`). Still, cache what you draw per frame and refresh it on `StatesChanged` rather than calling every gate for every row every frame.
- **`OpenQuest` returns at once**; the window opens on the framework thread, in the same frame when you call from it. **`PinQuest`** acts at once on the framework thread and is queued for the next frame from anywhere else.
- **`StatesChanged`, `QuestStateChanged` and `Disposing` arrive on the framework thread**, from Tsukimichi's `Framework.Update` handler (and its unload). Calling the gates from inside them is fine. Keep the handlers short.
- **`GetMoonlitStatus` reads the game's own flags only on the framework thread** (confidence `live`); from elsewhere it answers from the last capture (`saved`).

## Consumer example

The whole client, every gate included, is [`TsukimichiIpc.cs`](TsukimichiIpc.cs): copy it into your plugin and change its namespace. The shorter version below shows the pattern.

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

### With ECommons' EzIPC

A plugin on [ECommons](https://github.com/NightmareXIV/ECommons) can import the gates by name. EzIPC prefixes each member with the plugin name it is given, so the members are named after the gates; `[EzIPC]` fields are filled when `EzIPC.Init` runs and throw `IpcNotReadyError` like any subscriber while Tsukimichi is absent, and `[EzIPCEvent]` methods are subscribed to the messages.

```csharp
using System;
using ECommons.EzIpcManager;

public sealed class TsukimichiEz
{
    [EzIPC] public readonly Func<int> ApiVersion = null!;
    [EzIPC] public readonly Func<bool> IsReady = null!;
    [EzIPC] public readonly Func<string[]> GetGates = null!;
    [EzIPC] public readonly Func<uint[], string[]> GetStates = null!;
    [EzIPC] public readonly Func<uint, (string Kind, uint RefId, int Need, int Have)> GetFirstBlocker = null!;
    [EzIPC] public readonly Func<uint, bool, uint[]> GetQuestsInZone = null!;
    [EzIPC] public readonly Func<uint, uint[]> GetRoute = null!;
    [EzIPC] public readonly Func<uint, (bool Unique, bool Owned, string Confidence)> GetMoonlitStatus = null!;
    [EzIPC] public readonly Func<uint, bool> OpenQuest = null!;

    public TsukimichiEz() => EzIPC.Init(this, "Tsukimichi");

    [EzIPCEvent]
    private void QuestStateChanged(uint rowId, string from, string to)
    {
        // A live poll moved one quest; StatesChanged follows.
    }

    [EzIPCEvent]
    private void Disposing()
    {
        // Tsukimichi is unloading: drop what you cached.
    }
}
```

Wrap the calls as the example above does: `try { … } catch (IpcNotReadyError) { … }`.

## Consumed IPC

Tsukimichi also calls other plugins' gates when they are loaded. Each is optional: a plugin that is absent, not loaded, or whose gate is missing or throws reads as unavailable, and the first failure is logged once. A passive feature that needed it (a line, a badge) does not show; a button that needs it stays visible, disabled, and names the plugin (feature plan v5, decision 1). Detection goes through Dalamud's installed plugin list (`IDalamudPluginInterface.InstalledPlugins`, loaded state, re-read when the list changes), never through a per-frame IPC call.

### Companion plugin registry (since 1.6.0)

`Game/CompanionPlugins.cs` reads the installed list once per `ActivePluginsChanged` and gives each companion plugin a state: **Loaded**, **Installed but turned off**, **Outdated** (below the minimum version Tsukimichi needs, or flagged by Dalamud as built for an older API) or **Not installed**. The plugins, their internal names, minimum versions and repositories are in `Tsukimichi.Core/Companions/CompanionCatalog.cs`; Settings › Integrations › Companion plugins lists them.

Minimum versions are written the way Dalamud reports the plugin's version (its assembly version), which is not always the version a plugin's changelog uses. The last column is a released build as Dalamud reports it (checked 2026-10-02); `CompanionResolverTests.Every_minimum_is_in_the_numbering_of_a_released_build` holds each minimum to that numbering.

| Plugin | Internal name(s) | Minimum version | Repository | Numbering (a released build) |
|---|---|---|---|---|
| Lifestream | `Lifestream` | — | `https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json` | 2.5.4.23 (release tag) |
| vnavmesh | `vnavmesh` | — | `https://puni.sh/api/repository/veyn` | 1.2.3.14 (csproj) |
| Questionable / WigglyQuest | `Questionable`, `WigglyQuest` (a `Questionable` at 99.0.0.0 or above is the fork's do-nothing placeholder and reads as not installed) | — | `https://love.puni.sh/ment.json` / `https://github.com/WigglyMuffin/DalamudPlugins/raw/main/pluginmaster.json` | 15.756.3.26 (release tag) / 7.5.27.0 |
| TextAdvance (needed by Questionable) | `TextAdvance` | — | `https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json` | 3.3.0.1 (csproj) |
| AutoDuty | `AutoDuty` | 0.0.0.336 | `https://puni.sh/api/repository/erdelf` | 0.0.0.375 (release tag; the csproj says 0.0.0.0) |
| Boss Mod / Boss Mod Reborn (needed by AutoDuty and Questionable) | `BossMod`, `BossModReborn` | — | `https://puni.sh/api/repository/veyn` / Combat Reborn | 7.5.6.9 / 7.5.6.27 (tags) |
| Wrath Combo / Rotation Solver Reborn (needed by AutoDuty and Questionable) | `WrathCombo`, `RotationSolver` | — | `https://love.puni.sh/ment.json` / Combat Reborn | 1.0.4.26 / 7.5.6.13 |
| Artisan | `Artisan` | — | `https://love.puni.sh/ment.json` | 4.0.5.21 (csproj) |
| GatherBuddy / GatherBuddy Reborn | `GatherBuddy`, `GatherBuddyReborn` | — | official / `https://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json` | 3.8.11.1 / 7.5.6.1 |
| Allagan Tools | `InventoryTools` | 1.15.0.12 | official | 1.15.0.13 (manifest; its changelog says 15.0.13) |
| Quest Map | `QuestMap` | — | official | 15.755.2.0 (`<API>.<patch>.N.0` since API 14) |
| Chat 2 | `ChatTwo` | — | official | 1.40.9.0 |

- **Allagan Tools 1.15.0.12** (2026-08-31) added `ItemCountOwnedByCategory` and `GetItemCountsByCharacter`. Its changelog calls that build 15.0.12, and the official repository publishes it as 1.15.0.12 (goatcorp/DalamudPluginsD17 `b0e378b`, InventoryTools `84af185`). Up to 1.9.0 the minimum was written 15.0.12, so every Allagan Tools build read as outdated.
- **Quest Map** added its gates in 1.7.2.2 (`b2ce55e`, 2025-06-05). Since API 14 it numbers builds 14.x and 15.x, so every build for the current API has them and no minimum is needed.

Every button that hands work to one of them stays visible when the plugin is not loaded: it is disabled and its tooltip is `CompanionPlugins.DisabledReason(plugin)` ("Needs Lifestream — see Settings › Integrations", "… is installed but turned off — turn it on in /xlplugins", "Needs AutoDuty 0.0.0.336 or newer — update it in /xlplugins"). A loaded plugin whose hand-off a setting blocks gets a reason too ("Questionable needs TextAdvance's quest accept on — see Settings › Integrations"; see Companion setup below). Each IPC wrapper keeps its own gates; the registry only answers "is it there", "is it set up" and "why not".

### Companion setup

Settings › Integrations › Companion plugins shows one line at the top ("Ready for full automation", "2 plugins need setup") and, under each loaded plugin, a **Setup** list of the settings that matter to Tsukimichi's hand-offs. Each setting reads ✓ (as recommended), ✕ (set otherwise) or ? (Tsukimichi cannot read it). Help › Companion plugins and the "Set up your road" card show the same line. The list is `Tsukimichi.Core/Companions/CompanionSetupCatalog.cs`; the checks are `CompanionSetupEvaluator` (pure, tested); `Game/CompanionSetupService.cs` reads them at most every 5 seconds while something asks.

How a setting is read:
- From the plugin's own configuration file in Dalamud's `pluginConfigs` folder, **read only**: `Questionable.json`, `Lifestream/DefaultConfig.json`, `Artisan.json`, `GatherBuddy.json` / `GatherBuddyReborn.json`. The file is opened with sharing for reading and writing, and parsed again only when its write time changes. A setting the file lacks takes the plugin's own default.
- Or through the plugin's own getter gate: `AutoDuty.GetConfig(string key) -> string` (the active profile), `TextAdvance.GetEnableQuestAccept`, `GetEnableQuestComplete`, `GetEnableTalkSkip`, `IsEnabled`, `IsPaused` (each `() -> bool`), `vnavmesh.Nav.IsAutoLoad() -> bool`.

How a setting is set: never by writing another plugin's file. **Apply recommended settings** uses the plugin's own setter, only for the settings marked ✕ that it lists, after a confirmation that names each change:
- `AutoDuty.SetConfig(string key, object value)`, which saves the active profile. Bools are sent as "True"/"False". It is refused while AutoDuty runs: the run holds Tsukimichi's temporary overrides, and AutoDuty saves nothing while it does. Each change is read back with `GetConfig`.
- `vnavmesh.Nav.SetAutoLoad(bool)`.

Questionable, TextAdvance, Lifestream, Artisan and GatherBuddy have no setter for these settings, so their Setup lists say what to change and where.

Blocking settings (the hand-off button is disabled and names them): Questionable's "Prevent quest completion" on; TextAdvance's quest accept or quest complete off, unless Questionable's "Automatically configure TextAdvance" is on, because Questionable then turns them on itself while it runs; vnavmesh's automatic navmesh loading off. Any other setting set otherwise adds a note under the enabled button's tooltip ("AutoDuty: 2 recommended settings are set otherwise — see Settings › Integrations").

### Lifestream (internal name `Lifestream`)

| Gate | Signature | Used for |
|---|---|---|
| `Lifestream.Teleport` | `(uint aetheryteId, byte subIndex) -> bool` | every Teleport button and menu item, and Go to giver's first step; false (combat, casting, not attuned) prints one chat line with the reason |
| `Lifestream.IsBusy` | `() -> bool` | greying Teleport while Lifestream is busy, and Go to giver's arrival wait (cached for 250 ms) |
| `Lifestream.AethernetTeleportById` | `(uint aetheryteSheetRow) -> bool` | "Aethernet to <shard>" and Go to giver's hop; false only while busy |
| `Lifestream.AethernetTeleportToFirmament` | `() -> bool` | the hop from the Foundation to the Firmament |
| `Lifestream.GetActiveAetheryte` | `() -> uint` | whether the player stands at the city's aetheryte or a shard (cached for 250 ms) |
| `Lifestream.ExecuteCommand` | `(string arguments)` (action) | Teleport for an Island Sanctuary (`/li island`) or Occult Crescent (`/li occult`) giver, on an explicit click only: Lifestream talks to the NPC |
| `Lifestream.Abort` | `()` (action) | Stop during Go to giver's teleport or hop |

Source: `Game/LifestreamIpc.cs`. Read from [github.com/NightmareXIV/Lifestream](https://github.com/NightmareXIV/Lifestream) `Lifestream/IPC/IPCProvider.cs` at commit `ef759e9d3c3cd989b4af569c3f770ee9ba060922` (2026-09-23); EzIPC names each gate "Lifestream." plus the method name. Teleport is Lifestream only: there is no fallback to another plugin or to the game's own teleport (feature plan v5, decision 2). Without Lifestream the Teleport buttons stay, greyed, and name it.

Special zones: the Firmament is a teleport to the Foundation and the Firmament hop (part of Go to giver). Island Sanctuary and the Occult Crescent are entered through a conversation Lifestream holds for the player, so they run only from the Teleport button, whose tooltip says so, never inside Go to giver. Cosmic Exploration's `/li cosmic` picks the newest planet and may change worlds, so Tsukimichi only teleports to Bestways Burrow and says where to go from there.

### vnavmesh (internal name `vnavmesh`)

| Gate | Signature | Used for |
|---|---|---|
| `vnavmesh.Nav.IsReady` | `() -> bool` | Walk waits for the zone's navmesh ("Preparing path…") |
| `vnavmesh.Nav.BuildProgress` | `() -> float` | the "Preparing path… 40%" label (negative when no build runs) |
| `vnavmesh.SimpleMove.PathfindAndMoveCloseTo` | `(Vector3 destination, bool fly, float range) -> bool` | Walk to giver and Go to giver's last step, to 3 yalms of the giver (or of the way into the interior the giver stands in): on foot, on a mount, or flying (`fly` true) where the zone's flying is unlocked |
| `vnavmesh.SimpleMove.PathfindInProgress` | `() -> bool` | the Walk button reads Stop while a path is found |
| `vnavmesh.Path.IsRunning` | `() -> bool` | the Walk button reads Stop while the character moves |
| `vnavmesh.Path.Stop` | `()` (action) | Stop; also on leaving the zone, logging out and unloading Tsukimichi |
| `vnavmesh.Nav.Reload` | `() -> bool` | travel recovery (1.18): once per Walk or Go to giver, after a walk got stuck, ended short of the giver on its own, never started or waited too long for a navmesh; the walk is then tried once more |
| `vnavmesh.Query.Mesh.PointOnFloor` | `(Vector3 p, bool allowUnlandable, float halfExtentXZ) -> Vector3?` | the landing spot a flight aims for: the floor under the giver (probed 2 yalms above it, 3 around, `allowUnlandable` false) |
| `vnavmesh.Query.Mesh.NearestPointReachable` | `(Vector3 p, float halfExtentXZ, float halfExtentY) -> Vector3?` | the landing spot when there is no floor under the giver, and the aetheryte's place on the mesh when its object is not loaded |
| `vnavmesh.Path.GetMovementAllowed` | `() -> bool` | the travel preflight's "vnavmesh movement" line (another plugin paused vnavmesh's movement) |
| `vnavmesh.Path.SetMovementAllowed` | `(bool)` (action) | the preflight's "Let vnavmesh move" button only, with `true` |

Source: `Game/VnavmeshIpc.cs` (state reads cached for 250 ms) and `Game/TravelService.cs` (the Go to giver chain, `Core/Travel/GoToGiver.cs`). Read from [github.com/awgil/ffxiv_navmesh](https://github.com/awgil/ffxiv_navmesh) `vnavmesh/IPCProvider.cs` at commit `6fc80725eb8290472eee433fc4be7ee06ec79357` (2026-08-31). The character moves only after an explicit click on Walk or Go to giver (feature plan v5, decision 1); Settings › Integrations hides either button.

Mounting and flying (1.10, Settings › Integrations › Travel). How vnavmesh flies, read from the same commit (`vnavmesh/AsyncMoveRequest.cs`, `vnavmesh/Movement/FollowPath.cs`): `PathfindAndMoveCloseTo(dest, fly: true, range)` queues a flying path from the player to `dest`; while its next waypoint is above the player and the character is not yet in the air, it jumps (GeneralAction 2) to take off when mounted and stands still on foot. The path ends at the destination, within `range`, which leaves the mount hovering beside the giver. So Tsukimichi:

- mounts first (Mount Roulette, GeneralAction 9, or the chosen mount the character owns, through FFXIVClientStructs' `ActionManager.UseAction`; `GetActionStatus` must answer 0, which covers combat, duties, water and zones without mounts), only for a walk longer than the setting (40 yalms by default) and only in a zone whose TerritoryType allows mounts; it waits up to 8 seconds for the Mounted condition (asking once more after 3) and otherwise walks on foot;
- flies only on a mount and only where every aether current of the zone is attuned (Dalamud's `IUnlockState.IsAetherCurrentCompFlgSetUnlocked`, which reads `PlayerState.IsAetherCurrentZoneComplete`; read from [github.com/goatcorp/Dalamud](https://github.com/goatcorp/Dalamud) `Dalamud/Game/UnlockState/UnlockState.cs` at commit `b666d821a47306fb447c60155b5d99377f91a5ee`, 2026-09-29);
- lands when the path ends in the air: Dismount (GeneralAction 23) brings a flying mount down; it is asked again every second for up to 15 seconds. The character is never dismounted on the ground;
- sprints (GeneralAction 4) at the start of a walk on foot where mounts are not allowed, when "Sprint in towns" is on;
- gives a walk that comes no 2 yalms closer in 15 seconds one new path, and stops with "the walk stopped making progress" when that one sticks too. vnavmesh's own stuck retry (its `RetryOnStuck` setting) works underneath and is left alone.

Runs you can trust (1.18, feature plan v7 A8). Each recovery happens at most once per run, so a run never loops, and Stop or `/tsuki stop` ends it at any step:

- a walk that got stuck on its new path, ended more than 8 yalms from the giver with its path run out, never started, or waited two minutes for a navmesh has vnavmesh reload the zone's navmesh (`Nav.Reload`, the fix vnavmesh's developers prescribe), waits for `Nav.IsReady`, and walks again from where the character stands; a chat line says so. A walk stopped by hand (its path still had waypoints) is not retried;
- before an aethernet hop, when Lifestream's `GetActiveAetheryte` answers 0, the chain walks to the network's nearest attuned aetheryte or shard first (the aetheryte object when the object table holds one, else its map marker on the mesh), then hops; a hop that never started is asked for once more, after that walk or after a second's pause (Lifestream checks it can teleport once, too soon after zoning or dismounting). Go to giver offers this from anywhere in the city when the walk and the hop beat walking straight to the giver;
- a flight aims for the landing spot above, and a landing more than 4.5 yalms from the giver walks the last yalms on the landed mount.

Every game call runs on the framework thread, only during a Walk or Go to giver the player clicked, and only while the shared hook gate allows game calls (without it the walk stays on foot).

TextAdvance offers `TextAdvance.EnqueueMoveAndInteract(MoveData)` with `Mount` and `Fly` flags (read from [github.com/NightmareXIV/TextAdvance](https://github.com/NightmareXIV/TextAdvance) `TextAdvance/Navmesh/MoveManager.cs` and `MoveData.cs` at commit `9dee62760472b08a7c36c596c64e4dfbfdc5cf8b`, 2026-05-03). Tsukimichi does not use it: `MoveData` is a class of TextAdvance's own that crosses the IPC boundary only by serialisation, its queue answers no "where is it" a status line or a Stop could read, it mounts by its own 20-yalm rule and flies by its own check, and it is one more plugin to require. vnavmesh plus Tsukimichi's own mount, land and stuck steps keep every step visible, stoppable and under the settings.

### Wotsit (internal name `Dalamud.FindAnything`)

| Gate | Signature | Used for |
|---|---|---|
| `FA.RegisterWithSearch` | `(string plugin, string display, string search, uint iconId) -> string guid` | one search entry per quest and Moonlit reward |
| `FA.UnregisterOne` | `(string plugin, string guid) -> bool` | replacing, in order, the entries whose text (spoiler shield) or group (quest state) changed |
| `FA.UnregisterAll` | `(string plugin) -> bool` | turning the integration off, or a new catalog |
| `FA.Invoke` | message `(string guid)` | an entry was picked: reveal it in the Journal |
| `FA.Available` | message, no arguments | Wotsit (re)loaded: register again |
| `FA.IsAvailable` | `() -> bool` | whether Wotsit is ready when the plugin list lags |

Source: `Game/WotsitIpc.cs`. Settings › Integrations › "Register quests and rewards with Wotsit" (on by default).

Registration order matters. Read from [github.com/goaaats/Dalamud.FindAnything](https://github.com/goaaats/Dalamud.FindAnything) at commit `bee02b974d7d2189b3428dc5c1f6c7b3b56555d9` (2026-09-21):
- Wotsit's default match mode is fuzzy (`Configuration.cs`: `MatchMode = MatchMode.Fuzzy`).
- `FA.RegisterWithSearch` matches the query against the search text only. The display text is never matched (`IpcSystem.Register`).
- Wotsit keeps the first 26 matching entries per plugin, in the order they were registered, and stops there (`Modules/PluginSettingsModule.cs`, the `++pluginResults > 25` break). It sorts by score only afterwards (`Finder.GetResults`).
- `FA.UnregisterOne` removes an entry, and a new registration is appended at the end.

So each entry's search text is its name alone: the quest's shown name, or the reward's name. Entries are registered by group (`Core/Runtime/WotsitOrder.cs`):
1. Quests in the journal, Ready, or Ready on another job.
2. Moonlit rewards.
3. Other open quests (Blocked, done for today, not checked).
4. Completed and Locked out quests.

Within a group, entries keep the journal order. The groups follow the logged-in character's states, or the viewed character's while nobody is logged in.

When an entry changes group, Tsukimichi re-registers the entries that must move. It checks at most once every 10 seconds, and only when the states changed. It keeps the longest prefix Wotsit already holds in the right order and replaces everything after it, one entry at a time.

### Questionable (internal name `Questionable`, since 1.0.0; also `WigglyQuest`, since 1.11.0)

Since 2026-09-11 the WigglyMuffin fork installs under the internal name `WigglyQuest` and leaves "Questionable (moved to WigglyQuest)" at 99.0.0.0 under the old name, a placeholder that does no questing. Either name counts as Questionable loaded; the placeholder does not. The fork's public mirror still registers its gates as `Questionable.*`, and its settings are read from `pluginConfigs/WigglyQuest.json`.

Read from Questionable's provider class, `Questionable/External/QuestionableIpc.cs`, at [github.com/PunishXIV/Questionable](https://github.com/PunishXIV/Questionable) commit `0bd61efe8a6806a7a8010741c0c46b7dea153709` (branch `new-main`, 2026-09-30). The WigglyMuffin fork ([github.com/WigglyMuffin/Questionable](https://github.com/WigglyMuffin/Questionable), commit `4f2909b7bc9e6ec65e63c4b71f4fb7f523688b46`, 2026-09-24) registers fewer gates; the last column says which.

| Gate | Signature | Used for | Fork |
|---|---|---|---|
| `Questionable.IsQuestLockedReason` | `(string questId) -> (bool locked, string reasons)` | the cross-check: the detail pane's "Questionable agrees" / "Questionable says: …" line and the "questionable:" line of Report this quest; the "has a path / no path" badges (1.6.0) | no |
| `Questionable.IsQuestLocked` | `(string questId) -> bool` | the same, without a reason, when the reason gate is absent or fails | yes |
| `Questionable.AddQuestPriority` | `(string questId) -> bool` | "Add to Questionable priority" in the detail pane's "…" menu, only with Settings › Integrations › "Show Questionable hand-off" ticked (off by default) | yes |
| `Questionable.ImportQuestPriority` | `(string encoded) -> bool` | Send to Questionable (1.6.0): one call per send | yes |
| `Questionable.ClearQuestPriority` | `() -> bool` | "Replace Questionable's list…", after a confirmation (1.6.0) | yes |
| `Questionable.ExportQuestPriority` | `() -> string` | reading the list back after a send ("Sent 14 of 17 (3 have no Questionable path)") and after "Do this next", and the "On Questionable's list (#n)" badges (1.6.0) | yes |
| `Questionable.StartQuest` | `(string questId) -> bool` | "Add and start Questionable" (1.6.0) and "Start here and keep going" (1.18.0); the Start pill only on a Questionable without `StartSingleQuest` | yes |
| `Questionable.StartSingleQuest` | `(string questId) -> bool` | the detail pane's Start Questionable pill (1.18.0): this quest, then stop; false for a quest Questionable has no path for | yes |
| `Questionable.InsertQuestPriority` | `(int index, string questId) -> bool` | "Do this next" in the detail pane's "…" menu (1.18.0), always at index 0 | yes |
| `Questionable.Stop` | `(string label) -> bool` | Stop, and a "Stop later" condition when it is met, called with the label `"Tsukimichi"` (1.6.0; 1.18.0) | no |
| `Questionable.IsRunning` | `() -> bool` | the live status (1.6.0) | yes |
| `Questionable.GetCurrentQuestId` | `() -> string?` | the live status: the quest it works on (1.6.0) | yes |
| `Questionable.GetCurrentStepData` | `() -> StepData?` | the live status: the step (1.6.0) | yes (`TerritoryId` is `ushort` there, `uint` upstream) |
| `Questionable.IsQuestUnobtainable` | `(string questId) -> bool` | the wider cross-check against Locked out (1.6.0) | no |
| `Questionable.GetCurrentlyActiveEventQuests` | `() -> List<string>` | the wider cross-check against the seasonal events the game reports running (1.6.0) | yes |
| `Questionable.ReloadData` | message, no arguments | Questionable reloaded its paths (at load, after downloading its path bundle, or its "Reload Data" button): forget the cached answers, path badges and list, and ask again (since 1.4.2) | yes |

Source: `Game/QuestionableIpc.cs` and `Game/QuestionableIpc.Automation.cs`; the pure parts are in `Tsukimichi.Core/Ipc/` (`QuestionableCrossCheck.cs`, `QuestionableList.cs`, `QuestionableBadges.cs`, `QuestionableStatus.cs`, `QuestionableWiderCheck.cs`). Every gate is looked up when it is used (`HasFunction`), so whichever version is installed gets what it offers; a button whose gate is missing stays visible, disabled, and says why.

**Ids and the cross-check**

- The quest id is the Quest row id's low 16 bits in decimal (row 65964 is `"428"`), the form Questionable's `ElementId.FromString` reads as a quest. Questionable's other element kinds carry a letter (`A` allied society daily, `S` satisfaction supply, `U` unlock link, `N` aethernet, `C` collection); Tsukimichi never sends them and ignores them when it reads Questionable's list or status.
- Questionable is asked when a quest is selected or the character's state changes, one call per quest per session version, never per frame, and only about the character logged in: a stored character is not compared. The answers are asked again after `Questionable.ReloadData`, since an answer given before Questionable's paths arrived reads "no path" for every quest.
- Both lock gates answer locked for a quest Questionable has no path for; the reason gate then gives an empty reason. Tsukimichi reads a locked answer without a reason as "no answer", not a disagreement.
- Questionable's lock does not check the level of an ordinary quest, the job, the account caps, or whether the quest is done or in the journal, so only Ready, Available on another job and Blocked are compared, and a quest Blocked only by level or job is compared as open. Its reasons ("Prev quest (2)", "Aetheryte locked: …", "Low level (GLA)") follow Questionable's own language: it translates them into Japanese, Simplified and Traditional Chinese (`Questionable/Resources/I18N.xml`), so "Questionable says: …" can show "レベル不足 (GLA)". Tsukimichi recognises the level reason in each of those languages (since 1.4.2; before, only the English one, so a quest blocked only by level showed a false disagreement under Questionable in Japanese or Chinese).
- Tsukimichi never asks about two quests whose lock check makes Questionable open the Achievements window (it checks an achievement, and shows the window to load the list; `Questionable/Functions/QuestFunctions.cs`): 4081, row 69617 "The Adventurer with All the Cards" (Gold Saucer), and 2387, row 67923 "What Lies Beneath", the Palace of the Dead quest that opens floors 51 to 100 (the journal files it with the Gridanian sidequests). It never sends them either.
- `AddQuestPriority` answers true even for a quest Questionable does not know, so the "…" menu item is enabled only when the reason gate named a path for the quest, and the "…" button is not shown at all when the reason gate is absent (the WigglyMuffin fork) or fails.

**Send to Questionable (1.6.0)**

- Offered on the route window (the route's steps, in route order), each My blues expansion card (its quests as the card lists them), the right-click menu of a Characters job, role or chain row (its quests in order), and the Todo overlay's title menu (the pins, in the order they were pinned). Done, in-journal and locked-out quests are left out, and each quest is sent once.
- The list goes over in one `ImportQuestPriority` call, in Questionable's own clipboard format: `"qst:priority:"` followed by the Base64 of the UTF-8 text of the ids joined by `;` (`PriorityWindow.EncodeQuestPriority` / `DecodeQuestPriority`). Questionable appends the quests it has a path for that are not already on its list, drops the others without saying so, and always answers true (`QuestPriorityManager.Import`). So Tsukimichi reads the list before and after with `ExportQuestPriority` and reports the difference in chat: "Questionable: sent 14 of 17 (3 have no Questionable path)."
- Adding to the end is the default. "Replace Questionable's list…" calls `ClearQuestPriority` first, after a confirmation that says how many quests the list holds now.
- Questionable keeps its list in memory only and sends no message when it changes, so the "On Questionable's list (#n)" badge reads it again after a send, when a quest is opened in the detail pane or a route window opens, after `ReloadData`, and otherwise at most every 30 seconds while a badge is drawn.
- There is no "has a path" gate. The badge reads it from the reason gate: `(true, "")` means no path, any other answer means a path. It does not depend on the character, so an answer holds until Questionable's paths reload or Dalamud's plugin list changes. A route asks for the steps it draws, at most six new questions a frame. On the fork, whose `IsQuestLocked` cannot tell "no path" from "locked", the badge is hidden.

**Start and Stop (1.6.0; one quest at a time since 1.18.0)**

- "Add and start Questionable" sends the quests, then calls `StartQuest` on the first quest Questionable took, preferring one that is Ready. `StartQuest` sets the quest as the next one and turns on Questionable's automatic mode (`QuestController.Start`), which then works through its priority list in order and, when the list is done, carries on with its own choices (the main scenario) until it is stopped. Sends from a route, a chain, the pins or My blues keep this "continue" behaviour.
- The detail pane's **Start Questionable** pill (1.18.0, feature plan v7 A6, decided by a player panel: `docs/research/plan-v7/a6-panel.md`) calls `StartSingleQuest`: "Questionable does this quest, then stops." Both versions register it and route it to the same method as `StartQuest` with `single: true` (upstream `QuestionableIpc.StartQuest(questId, single)` at `0bd61ef`; the fork at `4f2909b`): it sets the quest as the next one and runs `QuestController.StartSingleQuest`, which picks the quest up (SingleQuestA), works it (SingleQuestB) and, when the current quest changes, logs "Single quest is finished" and goes back to manual, asking for no new main scenario quest (`allowNewMsq: false`). Nothing is added to the priority list. It answers false for a quest Questionable has no path for. On a Questionable without the gate the pill falls back to the old path (add the quest to the list, then `StartQuest`), and its tooltip says so.
- **Start here and keep going** (1.18.0), in the "…" menu, is the pill's old behaviour: add the quest to the list when it is not in the journal yet, then `StartQuest`. It shows only where the pill does one quest.
- **Do this next** (1.18.0), in the "…" menu under Settings › Automation › "Add to priority list" (the same setting as "Add to Questionable priority"), calls `InsertQuestPriority(0, id)`. Both versions register it (upstream `QuestionableIpc.InsertQuestPriority` → `QuestPriorityManager.Insert`; the fork → `QuestController.InsertQuestPriority`). It answers true even for a quest Questionable does not know, and leaves a quest already on the list where it is, so it is enabled only when the reason gate names a path for the quest, the list is read with `ExportQuestPriority` before and after, and chat says where the quest stands: "Close to Home is #1 on Questionable's list.", "… was already on Questionable's list and keeps its place, #3.", or that Questionable did not take it (`Core/Ipc/QuestionableInsert.cs`). Nothing starts. It is disabled while Questionable runs ("Questionable is on a quest; add it after it stops."): Questionable#45 reports a priority quest started mid-run resetting the current quest's progress, and until that is tested in game the panel's conservative branch holds.
- It is offered when Settings › Integrations › "Allow Tsukimichi to start Questionable" is ticked (on by default) and asks first until the player ticks "Don't ask again" ("Ask before starting Questionable" turns the question back on). It is disabled, naming what is missing, when Questionable is already running, or when a plugin Questionable needs to run is not loaded: vnavmesh, TextAdvance and Lifestream, from its manifest ("Required Plugins: vnavmesh, TextAdvance, Lifestream") and the `RequiredPlugins` list of `Questionable/Windows/ConfigComponents/PluginConfigComponent.cs` (the same in both versions).
- Stop calls `Stop("Tsukimichi")`. It shows beside the live status and in every Send to Questionable menu while Questionable runs. The fork has no `Stop` gate, so there the button is disabled and says to stop it from Questionable's own window.

**Stop later and run receipts (1.18.0, feature plan v7 A4)**

- **Stop later**, while Questionable runs, whoever started it: in every Send to Questionable menu, the detail pane's "…" menu and on a right-click of the status line's Stop. "After this quest" (the quest `GetCurrentQuestId` names), "After this many quests" (1 to 99) or "At a time" (hh:mm, today or tomorrow by the computer's clock). Questionable's own "Stop after current quest" toggle (Questionable#118) cannot be reached over IPC, so Tsukimichi counts the character's own completions (its quest events) and calls `Stop("Tsukimichi")` when the condition is met, once; a refused stop drops the condition with a chat line. The status line says what is set ("… · stops after 2 more quests", "… · stops at 21:30"; a single-quest run reads "… · then stops"). The stop runs Questionable's "command after stop" like any IPC stop, and the menu's tooltip says so. Without the `Stop` gate (the fork) the items are disabled.
- **The run receipt.** When a run ends (Questionable has read not running for 4 seconds), Tsukimichi notes how long it ran, the quests completed meanwhile and why it ended: done (a single-quest run), stopped before its quest was done, the condition set, Stop from Tsukimichi, or on its own or from Questionable's window. Questionable's IPC gives no stop reason, so "on its own" is all Tsukimichi can say there. A run Tsukimichi started, or one with a condition, gets a chat line ("Questionable finished Close to Home and stopped.", "Questionable ran 1 h 12 min: 14 quests done. Stopped after 14 quests, as you set."). The last ten are kept for the session and the last one in the configuration; Settings › Automation › Questionable › Recent runs lists them.
- The rules are `Core/Ipc/QuestionableRunGuard.cs` (pure, tested); `Game/QuestionableRunWatch.cs` feeds it each frame.

**Live status (1.6.0)**

- `IsRunning`, then `GetCurrentQuestId` and `GetCurrentStepData`, at most once a second, while the main window or the Todo overlay draws it, while a run is followed for Stop later and its receipt (1.18.0): from a start Tsukimichi asked for, or a run a window showed, until it ends, and (1.18.0) while Questionable is loaded and the duty guard or any "Needs you" alert is on (below). With all of these off and no Tsukimichi window visible, nothing is asked. Questionable sends no event for this, so it is polled.
- The main window's status bar shows "Questionable: running · <quest> · step 3 of 7" with a Stop button, the Todo overlay a line under its title, and that quest's row is washed gold in the Journal and its name gold in the overlay.
- `GetCurrentStepData` returns Questionable's own `StepData` class, which Dalamud converts into Tsukimichi's `QuestionableStepData` through JSON by property name (`QuestId`, `Sequence`, `Step`, `InteractionType`, `TerritoryId` as `uint`, which also reads the fork's `ushort`). If a gate answers with another shape, the status (or just the step) is turned off with one log line until Dalamud's plugin list changes.

**Duty guard and "Needs you" (1.18.0, plan v7 A3 and A5)**

- The duty guard reads the step's `InteractionType`. On `Duty` (Questionable's `EInteractionType.Duty`, which queues a Duty Finder entry through AutoDuty or opens the Duty Finder on it, `Questionable/Controller/Steps/Interactions/Duty.cs`), it looks up the duty: the step data does not name it, so it comes from the quest's script (`INSTANCEDUNGEON` and `CONTENT_START` constants, mapped to ContentFinderCondition rows), narrowed to the duties the character has not cleared. `SinglePlayerDuty` (a solo quest battle) is never guarded.
- If that duty has neither Duty Support nor Trust, Settings › Automation › Questionable › "Before a duty with other players" decides: Stop (the default) calls `Stop("Tsukimichi")`, which also stops the AutoDuty run Questionable started, Warn says so and lets it go on, and Do nothing says nothing. One chat line says why, with the "Needs you" toast and sound. It acts once per step: starting Questionable again on the same step lets it go ahead. A followed run's receipt gives "Stopped before a duty with other players." as its reason. A quest whose script names no known duty gets a "can't tell" line and is never stopped. On the fork, which has no `Stop` gate, the line says to stop it in Questionable's window. Three seconds after a stop, a character still in the Duty Finder queue is told so; Tsukimichi never withdraws for the player. Questionable's "command after stop" runs on this stop as on any other.
- "Needs you" alerts (Settings › Alerts › While automation runs) are raised only while Questionable runs or a Go to giver, Lifestream task, AutoDuty run or Artisan craft Tsukimichi started is under way: on death, on 30 seconds without moving 3 yalms while vnavmesh says it moves (its `IsRunning` or pathfind state), on a duty pop (Dalamud's `IClientState.CfPop`) and on an incoming tell (`IChatGui.ChatMessage`, read only: never answered or hidden). Each kind is raised at most once a minute; the sound (chat sound effect 6) plays at most every 10 seconds. The toast and sound wait for the hook gate. Source: `Game/RunWatch.cs`; the rules are `Tsukimichi.Core/Companions/DutyGuard.cs` and `NeedsYou.cs`.

**Wider cross-check (1.6.0)**

- `IsQuestUnobtainable` (upstream only) against Tsukimichi's Locked out: compared for Ready, Available on another job, Blocked and Locked out quests, never for a stored character. Unobtainable on a quest Tsukimichi holds back only for an expansion the account does not own agrees. The gate throws for a quest Questionable has no data for, which reads as no answer.
- `GetCurrentlyActiveEventQuests` (read at most once a minute) against the festivals the game's flags report running: a quest Questionable lists while its festival is not running is a disagreement. Questionable leaves a whole event out once one of its quests is done, so a running event quest it does not list is only noted.
- A disagreement adds a line under the detail pane's Questionable line ("Questionable says it can no longer be done"), and Report this quest adds `questionable more: path yes; list #3; unobtainable no, agrees; event listed, agrees`.

### AutoDuty (internal name `AutoDuty`, since 1.6.0)

Read from AutoDuty's provider class, `AutoDuty/IPC/IPCProvider.cs`, at [github.com/erdelf/AutoDuty](https://github.com/erdelf/AutoDuty) commit `39b9a877a466871e8a25b3af6e12dae33abd2d6c` (release 0.0.0.375, 2026-10-01). AutoDuty registers its gates through ECommons' EzIPC, so each is named `AutoDuty.<method>`.

| Gate | Signature | Used for |
|---|---|---|
| `AutoDuty.ContentHasPath` | `(uint territoryType) -> bool` | "AutoDuty has a path" in the detail pane's Duties section (read only; cached per territory for a minute or until the plugin list changes) |
| `AutoDuty.PushConfigOverrides` | `(object Dictionary<string, string>) -> bool` | before a run: `{"Meta.AutoDutyModeEnum": "Looping", "Meta.DutyModeEnum": "Support" \| "Trust" \| "Regular" \| "Trial" \| "Raid", "Meta.LoopTimes": "1"}`, temporary overrides AutoDuty restores itself when it stops (`StopAndResetAll` → `ConfigOverrideHelper.Pop`), so the player's own run mode, queue and loop count come back |
| `AutoDuty.Run` | `(uint territoryType, int loops, bool bareMode)` | "Run with AutoDuty": `loops` 0 (Run would otherwise write the loop count for good; the override holds it at 1), `bareMode` true (no pre-loop, between-loop or termination actions) |
| `AutoDuty.IsStopped` | `() -> bool` | Stop and the "AutoDuty is running" line (cached for 250 ms); right after Run, a still-stopped AutoDuty means the run did not start |
| `AutoDuty.PopConfigOverrides` | `() -> bool` | only when Run left AutoDuty stopped, to undo the override |
| `AutoDuty.Stop` | `()` | the Stop button |

Source: `Game/AutoDutyIpc.cs`; the mode choice and the disabled reasons are `Tsukimichi.Core/Companions/AutoDutyPlan.cs`, the duty data `Tsukimichi.GameData/DutyRunSheets.cs`.

- The territory is the duty's `ContentFinderCondition.TerritoryType`. A quest's duties are the ones it requires (`Quest.InstanceContent`, mapped to the Duty Finder entry that links the instance) and the ones it unlocks (curated `duty_unlocks.json` and the reward data's duty unlocks).
- The mode is Duty Support when a DawnContent row names the duty with more than one party choice (AutoDuty's own rule), else Trust (a DawnContent row and Shadowbringers on), else the regular Duty Finder only with Settings › Integrations › "Allow AutoDuty to queue in the regular Duty Finder" (off by default). `Meta.DutyModeEnum` and `PushConfigOverrides` are both in AutoDuty from 0.0.0.336 on, the minimum the registry asks for.
- Run is offered only to the logged-in character, for a duty it has unlocked (`UIState.IsInstanceContentUnlocked`), with a path, and with AutoDuty's own requirements loaded: vnavmesh and Boss Mod or Boss Mod Reborn. A missing rotation plugin (Wrath Combo or Rotation Solver Reborn) is a note, since Boss Mod's autorotation also serves.
- Companion setup reads `AutoDuty.GetConfig(string) -> string` and, only on Settings › Integrations › Apply recommended settings after the player confirms, calls `AutoDuty.SetConfig(string, object)` for the listed settings (see Companion setup above).
- Tsukimichi never calls `Start`, `SetLevelingMode` or any other gate.

### Quest Map (internal name `QuestMap`, since 1.6.0)

Read from `QuestMap/Ipc.cs` at [github.com/GemPlugins/QuestMap](https://github.com/GemPlugins/QuestMap) commit `5926c83ee9aec9036a8bbffc686b8e3ba133720f` (the gates were added in `b2ce55e3e9d972f126961a66342f58113dd75b86`, Quest Map 1.7.2.2).

| Gate | Signature | Used for |
|---|---|---|
| `QuestMap.ShowGraphByQuestId` | `(uint questId) -> bool` | "Open in Quest Map" in the detail pane's Path section; false ("Quest Map does not chart this quest") when Quest Map has no node for it |

The id is the Quest sheet row id (65536 and up): Quest Map keys its nodes by `Quest.RowId`. `QuestMap.ShowInfoByQuestId` (same signature, Quest Map's info window) is wrapped in `Game/QuestMapIpc.cs` but no button calls it yet. `/tsuki why` ends with a line pointing at Open in Quest Map, or naming Quest Map as missing.

## Versioning

- `Tsukimichi.ApiVersion` returns the API version, `1` today.
- **Additive changes keep the version**: a new gate, a new message, a new `QuestState` name, a new requirement kind in `GetBlockers`, or new wording in `GetStateName` and the blocker lines. A new gate is listed here with the release that added it; calling it on an older Tsukimichi throws `IpcNotReadyError`, which the example above already treats as absent.
- **Breaking changes bump the version**: renaming or removing a gate, changing a gate's argument or return types, changing what an id argument accepts, or changing the meaning of a return value (what `IsQuestAvailable` counts as available, what an enum name in `GetState` means, 0 in `GetMsqPosition`). A bump is announced in the changelog, and a gate whose meaning changes gets a new name so a caller written for the old version never gets the new answer by accident.
- Gate names never change once released, and nothing is removed within a version.

Questions and requests for new gates: open an issue with the **IPC request** template.
