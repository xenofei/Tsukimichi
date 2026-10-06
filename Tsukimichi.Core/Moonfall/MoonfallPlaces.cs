namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// The story era and place a Far Shore stage or a level scene depicts, on the spoiler shield's own scale: the era is
/// the game's expansion number (its <c>ExVersion</c>: 0 A Realm Reborn, 1 Heavensward, 2 Stormblood, 3 Shadowbringers,
/// 4 Endwalker, 5 Dawntrail), and the place is the English name of the area the shield places in the story
/// (<see cref="Query.SpoilerKind.Area"/>), so the shield's own rule decides whether the player's story has reached it
/// (<see cref="MoonfallShield"/>).
/// </summary>
/// <param name="Era">The expansion it belongs to.</param>
/// <param name="Zone">The area it depicts, as the shield names it; null for a picture of no place in the story (our own paintings, the open sea, Eorzea's map), which no shield hides.</param>
public readonly record struct MoonfallPlace(byte Era, string? Zone)
{
    /// <summary>Nowhere in the story: never hidden.</summary>
    public static readonly MoonfallPlace Nowhere = new(MoonfallPlaces.ARealmReborn, null);
}

/// <summary>
/// Where each Far Shore stage and each shipped level scene is set (owner's decision, plan v9: the Far Shore follows
/// Tsukimichi's spoiler shield). A stage set past the player's story is veiled: its name prints as the shield's
/// placeholder, its levels show no scene and no names, and it stays closed until the story reaches it or the player
/// reveals the place. A scene set past the story is drawn as the plain night sky wherever it would show. The Moon
/// Road's stages are set nowhere in particular (they are Moonfall's own road), so only their scenes are tagged.
/// </summary>
public static class MoonfallPlaces
{
    public const byte ARealmReborn = 0;
    public const byte Heavensward = 1;
    public const byte Stormblood = 2;
    public const byte Shadowbringers = 3;
    public const byte Endwalker = 4;
    public const byte Dawntrail = 5;

    /// <summary>
    /// The Far Shore's stages, stage 1 first, each themed to its companion on the sea voyage that ends on the moon
    /// (<see cref="MoonfallStages"/>).
    /// </summary>
    private static readonly MoonfallPlace[] FarShore =
    [
        // 1 The Lantern Quay (Minfilia): Limsa Lominsa's lantern-lit docks, where the voyage sets out.
        new(ARealmReborn, "Limsa Lominsa Lower Decks"),

        // 2 The Twin Lights (the twins): Vesper Bay's lights over the Thanalan coast, where the twins are first met.
        new(ARealmReborn, "Western Thanalan"),

        // 3 The Skyward Deck (Cid): an airship's deck over the Sea of Clouds.
        new(Heavensward, "The Sea of Clouds"),

        // 4 The Sunlit Isles (Raubahn): the Ruby Sea's islands.
        new(Stormblood, "The Ruby Sea"),

        // 5 The Admiral's Sea (Merlwyb): La Noscea's open water.
        new(ARealmReborn, "Western La Noscea"),

        // 6 The Ferry in the Stars (Urianger): our own painting of a constellation over the open sea.
        MoonfallPlace.Nowhere,

        // 7 The Floating Grove (Kan-E-Senna): the faerie lands of Il Mheg.
        new(Shadowbringers, "Il Mheg"),

        // 8 The Floating Market (Tataru): Kugane's harbour market.
        new(Stormblood, "Kugane"),

        // 9 The Domes of Sharlayan (Y'shtola): Old Sharlayan's harbour (pilot exp-p1, the Old Sharlayan painting).
        new(Endwalker, "Old Sharlayan"),

        // 10 The Archon's Crossing (Louisoix): the archons' Labyrinthos beneath Sharlayan.
        new(Endwalker, "Labyrinthos"),

        // 11 The Courier's Wake (the moogle): our own painting of a courier's wake over open sky, set nowhere (the owner's
        // answer, 6 October 2026), so Storm Post and the moogle, met in the first hours of any start, open at every era.
        MoonfallPlace.Nowhere,

        // 12 The Sea of Sorrows (your pick): Mare Lamentorum on Endwalker's moon (pilot exp-p3, the Mare Lamentorum painting).
        new(Endwalker, "Mare Lamentorum"),
    ];

    /// <summary>The shipped level scenes (<c>Moonfall/Levels/scenes</c>), by recipe name.</summary>
    private static readonly Dictionary<string, MoonfallPlace> SceneTable = new(StringComparer.Ordinal)
    {
        // Eorzea's world map painting as a chart: no place in the story.
        ["airship-road"] = MoonfallPlace.Nowhere,

        // The Coerthas loading painting (Ishgard's walls over the cloud sea, as A Realm Reborn shows them).
        ["holy-see"] = new(ARealmReborn, "Coerthas Central Highlands"),

        // Kugane's painting.
        ["lantern-night"] = new(Stormblood, "Kugane"),

        // Our own painting.
        ["moon-road-night"] = MoonfallPlace.Nowhere,

        // The Moon Road's stages 1 and 2 (the level pipeline, docs/design/v9/levels). Western Thanalan's own area map.
        ["thanalan-road-chart"] = new(ARealmReborn, "Western Thanalan"),

        // The Thanalan loading painting's great dome.
        ["uldah-gilded-dome"] = new(ARealmReborn, "Ul'dah - Steps of Nald"),

        // The La Noscea loading painting's west: Limsa Lominsa's tower over the water.
        ["limsa-across-water"] = new(ARealmReborn, "Limsa Lominsa Lower Decks"),

        // The world painting "The Three Great Continents" read as a chart: no place in the story, as airship-road.
        ["rhotano-wonders"] = MoonfallPlace.Nowhere,

        // Our own paintings (Horizon, the Sagolii, the Crystal, Vesper Bay, the ferry): set nowhere, as moon-road-night.
        ["horizon-by-night"] = MoonfallPlace.Nowhere,
        ["sagolii-cactuar"] = MoonfallPlace.Nowhere,
        ["crystal-call"] = MoonfallPlace.Nowhere,
        ["vesper-moonpath"] = MoonfallPlace.Nowhere,
        ["ferry-under-sail"] = MoonfallPlace.Nowhere,
        ["vesper-twin-lanterns"] = MoonfallPlace.Nowhere,
    };

    /// <summary>
    /// Where each menu backdrop is set (the backdrop policy, <c>moonfall-modes.md</c>): the menus follow the same rule
    /// as the scenes. The title's painting is Sohm Al, Heavensward's peak in Dravania; while the shield hides it the title
    /// is Ul'dah's painting (A Realm Reborn); the chart (Eorzea's world map, no story place) stands in only when neither
    /// can be shown or read.
    /// </summary>
    public static MoonfallPlace OfBackdrop(Art.MoonfallBackdrop backdrop) => backdrop switch
    {
        Art.MoonfallBackdrop.Title => new(Heavensward, "The Dravanian Forelands"),

        // Ul'dah's painting: the title for a story still in A Realm Reborn.
        Art.MoonfallBackdrop.TitleEarly => new(ARealmReborn, "Ul'dah - Steps of Nald"),
        _ => MoonfallPlace.Nowhere,
    };

    /// <summary>The scenes tagged here, by name (every shipped recipe must be one; a test holds it).</summary>
    public static IReadOnlyDictionary<string, MoonfallPlace> Scenes => SceneTable;

    /// <summary>Where <paramref name="stage"/> is set: a Far Shore stage's place, <see cref="MoonfallPlace.Nowhere"/> for The Moon Road's.</summary>
    public static MoonfallPlace OfStage(MoonfallStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        return stage.Campaign == MoonfallCampaignKind.Expansion && stage.Number >= 1 && stage.Number <= FarShore.Length
            ? FarShore[stage.Number - 1]
            : MoonfallPlace.Nowhere;
    }

    /// <summary>
    /// Where the scene <paramref name="name"/> is set; null for a scene not tagged here (a test keeps every shipped one
    /// tagged, and an untagged one is treated as set nowhere).
    /// </summary>
    public static MoonfallPlace? OfScene(string? name) =>
        name is not null && SceneTable.TryGetValue(name, out var place) ? place : null;
}
