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

    /// <summary><c>() -> uint</c>: the Quest row id of the next main scenario quest; 0 when the story is done or there is no answer.</summary>
    public const string GetMsqPositionGate = "Tsukimichi.GetMsqPosition";

    /// <summary><c>(uint questId) -> bool</c>: opens the main window on the quest; false for a quest the catalog does not hold.</summary>
    public const string OpenQuestGate = "Tsukimichi.OpenQuest";

    /// <summary>Message with no arguments, sent on the framework thread after a poll changed the logged-in character's states.</summary>
    public const string StatesChangedGate = "Tsukimichi.StatesChanged";
}
