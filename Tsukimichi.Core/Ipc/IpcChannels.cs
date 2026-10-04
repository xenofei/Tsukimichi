namespace Tsukimichi.Core.Ipc;

/// <summary>
/// The names of Tsukimichi's IPC gates (docs/ipc.md) and the API version they answer with. A name never changes once
/// shipped: adding a gate keeps <see cref="ApiVersion"/>, and changing what an existing gate takes or returns bumps it.
/// </summary>
public static class IpcChannels
{
    /// <summary>What <see cref="ApiVersionGate"/> returns. Bumped only by a breaking change to a shipped gate.</summary>
    public const int ApiVersion = 1;

    /// <summary><c>() -> int</c>: <see cref="ApiVersion"/>.</summary>
    public const string ApiVersionGate = "Tsukimichi.ApiVersion";

    /// <summary><c>() -> bool</c>: the catalog is built and the logged-in character has been evaluated.</summary>
    public const string IsReadyGate = "Tsukimichi.IsReady";

    /// <summary><c>(uint questId) -> bool</c>: Ready or Ready on another job for the logged-in character.</summary>
    public const string IsQuestAvailableGate = "Tsukimichi.IsQuestAvailable";

    /// <summary><c>(uint questId) -> string</c>: the state's enum name ("Ready", "Blocked", …); empty when there is no answer.</summary>
    public const string GetStateGate = "Tsukimichi.GetState";

    /// <summary><c>(uint questId) -> string</c>: the state's display name ("Ready", "In journal", "Done today", …); empty when there is no answer.</summary>
    public const string GetStateNameGate = "Tsukimichi.GetStateName";

    /// <summary><c>(uint questId) -> string[]</c>: the status line, then one line per requirement; empty when there is no answer.</summary>
    public const string GetBlockersGate = "Tsukimichi.GetBlockers";

    /// <summary>
    /// <c>() -> uint</c>: the Quest row id of the next main scenario quest (inside a branch region, the first route's);
    /// 0 when the story is done or there is no answer.
    /// </summary>
    public const string GetMsqPositionGate = "Tsukimichi.GetMsqPosition";

    /// <summary>
    /// <c>() -> uint[]</c>: the Quest row ids of every main scenario position: the next quest alone on a linear
    /// stretch, each open route's next quest (in route order) inside a branch region; empty when the story is done or
    /// there is no answer. Since 1.0.0.
    /// </summary>
    public const string GetMsqPositionsGate = "Tsukimichi.GetMsqPositions";

    /// <summary><c>(uint questId) -> bool</c>: opens the main window on the quest; false for a quest the catalog does not hold.</summary>
    public const string OpenQuestGate = "Tsukimichi.OpenQuest";

    /// <summary>Message with no arguments, sent on the framework thread after a poll changed the logged-in character's states.</summary>
    public const string StatesChangedGate = "Tsukimichi.StatesChanged";

    // ---- Since 1.8.0 (additive: ApiVersion stays 1) ----

    /// <summary><c>() -> string[]</c>: every gate and message name this build registers (feature detection). Since 1.8.0.</summary>
    public const string GetGatesGate = "Tsukimichi.GetGates";

    /// <summary>Message with no arguments, sent once as Tsukimichi unloads, before its gates are unregistered. Since 1.8.0.</summary>
    public const string DisposingGate = "Tsukimichi.Disposing";

    /// <summary><c>(uint[] questIds) -> string[]</c>: <see cref="GetStateGate"/> for each id, in order. Since 1.8.0.</summary>
    public const string GetStatesGate = "Tsukimichi.GetStates";

    /// <summary><c>(string state) -> uint[]</c>: the row ids of every quest in that state, in journal order. Since 1.8.0.</summary>
    public const string GetQuestsInStateGate = "Tsukimichi.GetQuestsInState";

    /// <summary><c>(uint territoryId, bool readyOnly) -> uint[]</c>: quests whose giver stands in the zone. Since 1.8.0.</summary>
    public const string GetQuestsInZoneGate = "Tsukimichi.GetQuestsInZone";

    /// <summary><c>(uint questId) -> (string kind, uint refId, int need, int have)</c>: the first blocker, in <see cref="IpcBlockerKinds"/>' words. Since 1.8.0.</summary>
    public const string GetFirstBlockerGate = "Tsukimichi.GetFirstBlocker";

    /// <summary><c>(uint targetRowId) -> uint[]</c>: the unlock route's steps to the quest, in order. Since 1.8.0.</summary>
    public const string GetRouteGate = "Tsukimichi.GetRoute";

    /// <summary><c>(uint itemId) -> uint[]</c>: quests that reward the item. Since 1.8.0.</summary>
    public const string GetQuestsForItemGate = "Tsukimichi.GetQuestsForItem";

    /// <summary><c>(uint itemId) -> (bool unique, bool owned, string confidence)</c>: the item's Moonlit standing. Since 1.8.0.</summary>
    public const string GetMoonlitStatusGate = "Tsukimichi.GetMoonlitStatus";

    /// <summary><c>(uint contentFinderConditionId) -> uint[]</c>: quests that unlock the duty. Since 1.8.0.</summary>
    public const string GetUnlockQuestsGate = "Tsukimichi.GetUnlockQuests";

    /// <summary><c>() -> uint[]</c>: the logged-in character's pinned quests, in the order they were pinned. Since 1.8.0.</summary>
    public const string GetPinsGate = "Tsukimichi.GetPins";

    /// <summary><c>(uint questId, bool pinned) -> bool</c>: pins or unpins a quest in Tsukimichi's own list. Since 1.8.0.</summary>
    public const string PinQuestGate = "Tsukimichi.PinQuest";

    /// <summary><c>() -> (uint rowId, byte step, long abandonedUnixSeconds)[]</c>: quests abandoned mid-way, newest first. Since 1.8.0.</summary>
    public const string GetAbandonedGate = "Tsukimichi.GetAbandoned";

    /// <summary><c>(uint classJobId) -> uint</c>: the next quest of a class's or job's quest line; 0 when done or unknown. Since 1.8.0.</summary>
    public const string GetNextJobQuestGate = "Tsukimichi.GetNextJobQuest";

    /// <summary>Message <c>(uint rowId, string from, string to)</c>, one per quest whose state a live poll changed. Since 1.8.0.</summary>
    public const string QuestStateChangedGate = "Tsukimichi.QuestStateChanged";

    // ---- Since 1.22.0: the summary for Tsukimichi for Umbra (additive: ApiVersion stays 1; the summary has its own version) ----

    /// <summary>
    /// What <see cref="GetSummaryVersionGate"/> returns: the summary gates' own contract version (plan v8 M2). Bumped only
    /// by a breaking change to a shipped summary gate; adding one keeps it.
    /// </summary>
    public const int SummaryVersion = 1;

    /// <summary><c>() -> int</c>: <see cref="SummaryVersion"/>. Since 1.22.0.</summary>
    public const string GetSummaryVersionGate = "Tsukimichi.GetSummaryVersion";

    /// <summary><c>() -> (string name, string job, int level)</c>: the logged-in character. Since 1.22.0.</summary>
    public const string GetCharacterGate = "Tsukimichi.GetCharacter";

    /// <summary><c>() -> (uint rowId, string name, string step)</c>: Up next, its name shielded. Since 1.22.0.</summary>
    public const string GetUpNextGate = "Tsukimichi.GetUpNext";

    /// <summary><c>() -> (int ready, int here, string job)</c>: quests Ready on the current job, and how many can start in this zone. Since 1.22.0.</summary>
    public const string GetReadyCountGate = "Tsukimichi.GetReadyCount";

    /// <summary><c>() -> (int used, int cap)</c>: the journal's slots. Since 1.22.0.</summary>
    public const string GetJournalRoomGate = "Tsukimichi.GetJournalRoom";

    /// <summary><c>() -> (string name, int daysLeft)[]</c>: the seasonal events ending soon, soonest first. Since 1.22.0.</summary>
    public const string GetEndingSoonGate = "Tsukimichi.GetEndingSoon";

    /// <summary><c>() -> (string part, int leftToLatest, bool caughtUp)</c>: the story meter. Since 1.22.0.</summary>
    public const string GetStoryMeterGate = "Tsukimichi.GetStoryMeter";

    /// <summary><c>() -> string</c>: the theme in use, by its key ("medallion", "classic", …). Since 1.22.0.</summary>
    public const string GetThemeGate = "Tsukimichi.GetTheme";

    /// <summary><c>(string place) -> bool</c>: opens Tsukimichi at a place (<see cref="IpcPlaces"/>); never starts travel or a run. Since 1.22.0.</summary>
    public const string OpenAtGate = "Tsukimichi.OpenAt";

    /// <summary><c>(string addon, string version) -> int</c>: an add-on says it is there; answers <see cref="SummaryVersion"/>. Since 1.22.0.</summary>
    public const string AddonHelloGate = "Tsukimichi.AddonHello";

    /// <summary>
    /// <c>(int max) -> (uint rowId, string name, string place)[]</c>: the first <c>max</c> Ready quests in Tonight's order,
    /// names and places shielded for the logged-in character; empty when logged out. Since 1.22.0.
    /// </summary>
    public const string GetReadyTonightGate = "Tsukimichi.GetReadyTonight";

    /// <summary>Message with no arguments, sent on the framework thread when anything the summary gates answer changed. Since 1.22.0.</summary>
    public const string SummaryChangedGate = "Tsukimichi.SummaryChanged";

    /// <summary>
    /// Every gate and message with its signature and the release that added it, in the order docs/ipc.md lists them:
    /// what <see cref="GetGatesGate"/> answers (the names) and the <c>/tsuki ipc</c> window lists.
    /// </summary>
    public static IReadOnlyList<IpcGateInfo> All { get; } =
    [
        new(ApiVersionGate, "() -> int", "0.9.0"),
        new(IsReadyGate, "() -> bool", "0.9.0"),
        new(GetGatesGate, "() -> string[]", "1.8.0"),
        new(IsQuestAvailableGate, "(uint questId) -> bool", "0.9.0"),
        new(GetStateGate, "(uint questId) -> string", "0.9.0"),
        new(GetStatesGate, "(uint[] questIds) -> string[]", "1.8.0"),
        new(GetStateNameGate, "(uint questId) -> string", "0.9.0"),
        new(GetBlockersGate, "(uint questId) -> string[]", "0.9.0"),
        new(GetFirstBlockerGate, "(uint questId) -> (string kind, uint refId, int need, int have)", "1.8.0"),
        new(GetQuestsInStateGate, "(string state) -> uint[]", "1.8.0"),
        new(GetQuestsInZoneGate, "(uint territoryId, bool readyOnly) -> uint[]", "1.8.0"),
        new(GetMsqPositionGate, "() -> uint", "0.9.0"),
        new(GetMsqPositionsGate, "() -> uint[]", "1.0.0"),
        new(GetRouteGate, "(uint targetRowId) -> uint[]", "1.8.0"),
        new(GetNextJobQuestGate, "(uint classJobId) -> uint", "1.8.0"),
        new(GetQuestsForItemGate, "(uint itemId) -> uint[]", "1.8.0"),
        new(GetMoonlitStatusGate, "(uint itemId) -> (bool unique, bool owned, string confidence)", "1.8.0"),
        new(GetUnlockQuestsGate, "(uint contentFinderConditionId) -> uint[]", "1.8.0"),
        new(GetAbandonedGate, "() -> (uint rowId, byte step, long abandonedUnixSeconds)[]", "1.8.0"),
        new(GetPinsGate, "() -> uint[]", "1.8.0"),
        new(PinQuestGate, "(uint questId, bool pinned) -> bool", "1.8.0"),
        new(OpenQuestGate, "(uint questId) -> bool", "0.9.0"),
        new(GetSummaryVersionGate, "() -> int", "1.22.0"),
        new(GetCharacterGate, "() -> (string name, string job, int level)", "1.22.0"),
        new(GetUpNextGate, "() -> (uint rowId, string name, string step)", "1.22.0"),
        new(GetReadyCountGate, "() -> (int ready, int here, string job)", "1.22.0"),
        new(GetJournalRoomGate, "() -> (int used, int cap)", "1.22.0"),
        new(GetEndingSoonGate, "() -> (string name, int daysLeft)[]", "1.22.0"),
        new(GetStoryMeterGate, "() -> (string part, int leftToLatest, bool caughtUp)", "1.22.0"),
        new(GetThemeGate, "() -> string", "1.22.0"),
        new(OpenAtGate, "(string place) -> bool", "1.22.0"),
        new(AddonHelloGate, "(string addon, string version) -> int", "1.22.0"),
        new(GetReadyTonightGate, "(int max) -> (uint rowId, string name, string place)[]", "1.22.0"),
        new(StatesChangedGate, "message ()", "0.9.0", IsMessage: true),
        new(QuestStateChangedGate, "message (uint rowId, string from, string to)", "1.8.0", IsMessage: true),
        new(DisposingGate, "message ()", "1.8.0", IsMessage: true),
        new(SummaryChangedGate, "message ()", "1.22.0", IsMessage: true),
    ];

    /// <summary>The names of <see cref="All"/>, a fresh array per call: what <see cref="GetGatesGate"/> returns.</summary>
    public static string[] Names()
    {
        var names = new string[All.Count];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = All[i].Name;
        }

        return names;
    }
}

/// <summary>One of Tsukimichi's IPC gates or messages, for <see cref="IpcChannels.All"/>.</summary>
/// <param name="Name">The gate's name ("Tsukimichi.GetState").</param>
/// <param name="Signature">Its arguments and answer as docs/ipc.md writes them.</param>
/// <param name="Since">The release that added it.</param>
/// <param name="IsMessage">A message Tsukimichi sends (subscribe to it) rather than a function to call.</param>
public sealed record IpcGateInfo(string Name, string Signature, string Since, bool IsMessage = false);
