namespace Tsukimichi.Core.Model;

/// <summary>The NPC that starts a quest and where it stands.</summary>
public sealed record Issuer(
    uint NpcId,
    string Name,
    uint TerritoryId,
    uint MapId,
    float X,
    float Y,
    float Z);
