namespace Tsukimichi.Core.Travel;

/// <summary>A giver's zone that a plain teleport does not reach.</summary>
public enum TravelSpecial
{
    None,

    /// <summary>The Firmament: teleport to the Foundation, then Lifestream's aethernet hop to the Firmament.</summary>
    Firmament,

    /// <summary>Island Sanctuary: reached by talking to the ferry skipper at Moraby Drydocks.</summary>
    IslandSanctuary,

    /// <summary>The Occult Crescent (South Horn): entered by talking to the guide in the Phantom Village.</summary>
    OccultCrescent,

    /// <summary>A Cosmic Exploration zone: teleport to Bestways Burrow, then talk to its aetheryte.</summary>
    CosmicExploration,
}

/// <summary>
/// The zones with no ordinary way in by teleport (feature plan v5, 1.6.0; research C2 E) and how Tsukimichi gets
/// there through Lifestream. The Firmament is a plain aethernet hop and joins the Go to giver chain. Island Sanctuary
/// and the Occult Crescent are entered through a conversation that Lifestream holds for the player
/// (<c>/li island</c>, <c>/li occult</c>), so they run only from an explicit Teleport click whose tooltip says so, never
/// inside the chain. Cosmic Exploration's <c>/li cosmic</c> picks the newest planet and may change worlds, so
/// Tsukimichi only teleports to Bestways Burrow and says where to go from there.
/// </summary>
public static class TravelSpecials
{
    /// <summary>TerritoryType of the Firmament.</summary>
    public const uint FirmamentTerritory = 886;

    /// <summary>Aetheryte sheet row of the Foundation's aetheryte, the only place the Firmament hop leaves from.</summary>
    public const uint FoundationAetheryte = 70;

    /// <summary>TerritoryType of Island Sanctuary.</summary>
    public const uint IslandSanctuaryTerritory = 1055;

    /// <summary>TerritoryType of the Occult Crescent's South Horn.</summary>
    public const uint OccultCrescentTerritory = 1252;

    /// <summary>TerritoryTypes of the Cosmic Exploration zones (Sinus Ardorum, Phaenna, Oizys).</summary>
    public static readonly IReadOnlySet<uint> CosmicTerritories = new HashSet<uint> { 1237, 1291, 1310 };

    public static TravelSpecial Classify(uint territoryId) => territoryId switch
    {
        FirmamentTerritory => TravelSpecial.Firmament,
        IslandSanctuaryTerritory => TravelSpecial.IslandSanctuary,
        OccultCrescentTerritory => TravelSpecial.OccultCrescent,
        _ when CosmicTerritories.Contains(territoryId) => TravelSpecial.CosmicExploration,
        _ => TravelSpecial.None,
    };

    /// <summary>True for the zones entered through a conversation Lifestream holds (explicit Teleport only, never the chain).</summary>
    public static bool NeedsConversation(TravelSpecial special) => special is TravelSpecial.IslandSanctuary or TravelSpecial.OccultCrescent;

    /// <summary>The <c>/li</c> arguments for a conversation zone; null for every other zone.</summary>
    public static string? LifestreamCommand(TravelSpecial special) => special switch
    {
        TravelSpecial.IslandSanctuary => "island",
        TravelSpecial.OccultCrescent => "occult",
        _ => null,
    };
}
