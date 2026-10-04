using System.Collections.Frozen;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Core.Unlocks;

/// <summary>The sheet a feature's icon is read from (<see cref="FeatureArt"/>).</summary>
public enum FeatureArtSheet : byte
{
    /// <summary>No icon: the row keeps its stand-in.</summary>
    None,

    /// <summary><c>MainCommand.Icon</c>: the game's own menu icon (Sightseeing Log, Housing, Trust).</summary>
    MainCommand,

    /// <summary><c>ContentType.Icon</c>: the Duty Finder and Timers category tile (PvP, Treasure Hunt, Island Sanctuary).</summary>
    ContentType,

    /// <summary><c>Item.Icon</c>: the item that stands for a feature with neither (a striking dummy, a faux leaf).</summary>
    Item,
}

/// <summary>A sheet row whose icon stands for a feature: the sheet and the row id, never the icon itself.</summary>
public readonly record struct FeatureArtRef(FeatureArtSheet Sheet, uint Row)
{
    /// <summary>No icon.</summary>
    public static readonly FeatureArtRef None = default;

    /// <summary>Whether there is no row to read.</summary>
    public bool IsNone => Sheet == FeatureArtSheet.None || Row == 0;

    public static FeatureArtRef Menu(uint mainCommandRow) => new(FeatureArtSheet.MainCommand, mainCommandRow);

    public static FeatureArtRef Content(uint contentTypeRow) => new(FeatureArtSheet.ContentType, contentTypeRow);

    public static FeatureArtRef ItemRow(uint itemRow) => new(FeatureArtSheet.Item, itemRow);
}

/// <summary>
/// The game icon a feature unlock wears (owner point 9, UI-5a): the curated system unlocks (<c>system_unlocks.json</c>,
/// whose English labels every client language shares) and the field operations, which used to draw the veiled moon in
/// the detail pane's Unlocks rows, Moonlit and the table's Opens column. The table maps a word of the label to a sheet
/// row, first match wins, ignoring case, a more specific word before a wider one ("Ocean fishing" before "fishing",
/// "Eureka Orthos" before "Eureka"). The Duty Finder category tile comes first where the game has one (PvP, The Hunt,
/// Island Sanctuary, the Gold Saucer), then the menu the feature lives in (Trust, Housing, the Sightseeing Log), then an
/// item that stands for it (a striking dummy for the training grounds). The icon itself is read from the sheet at run
/// time (<see cref="FeatureIcons"/>), so a patch that redraws one needs no change here; the game-data tests check every
/// row has an icon and every curated label finds one.
/// </summary>
public static class FeatureArt
{
    // MainCommand rows (English names in the 2026.10 client).
    public const uint MenuActionsAndTraits = MoonlitKindIcons.MainCommandActionsAndTraits;
    public const uint MenuJournal = 4;
    public const uint MenuCraftingLog = 9;
    public const uint MenuMap = 16;
    public const uint MenuArmouryChest = MoonlitKindIcons.MainCommandArmouryChest;
    public const uint MenuFishingLog = 29;
    public const uint MenuCompanion = MoonlitKindIcons.MainCommandCompanion;
    public const uint MenuHousing = 44;
    public const uint MenuChallengeLog = 60;
    public const uint MenuSightseeingLog = 64;
    public const uint MenuCurrency = 66;
    public const uint MenuAetherCurrents = MoonlitKindIcons.MainCommandAetherCurrents;
    public const uint MenuDutyRecorder = 76;
    public const uint MenuChocoboSaddlebag = 77;
    public const uint MenuBlueMagicSpellbook = MoonlitKindIcons.MainCommandBlueMagicSpellbook;
    public const uint MenuTrust = 82;
    public const uint MenuNewGamePlus = 88;
    public const uint MenuDutySupport = 91;

    // ContentType rows.
    public const uint ContentGuildhests = 3;
    public const uint ContentPvp = 6;
    public const uint ContentTreasureHunt = 9;
    public const uint ContentLevequests = 10;
    public const uint ContentGrandCompany = NodeIcons.GrandCompanyContent;
    public const uint ContentDiscipleOfTheHand = NodeIcons.HandContent;
    public const uint ContentRetainerVentures = 18;
    public const uint ContentGoldSaucer = 19;
    public const uint ContentDeepDungeons = NodeIcons.DeepDungeonContent;
    public const uint ContentWondrousTails = 24;
    public const uint ContentCustomDeliveries = 25;
    public const uint ContentEureka = 26;
    public const uint ContentSaveTheQueen = 29;
    public const uint ContentVariantDungeons = 30;
    public const uint ContentOceanFishing = 31;
    public const uint ContentTripleTriad = 32;
    public const uint ContentTheHunt = 33;
    public const uint ContentIslandSanctuary = 36;
    public const uint ContentOccultCrescent = 38;

    // Item rows.
    public const uint ItemStrikingDummy = 7118;
    public const uint ItemModernAesthetics = 10084;
    public const uint ItemFauxLeaf = 30341;

    /// <summary>A label's words and the row whose icon stands for it, first match wins.</summary>
    private static readonly (string Word, FeatureArtRef Art)[] Words =
    [
        ("Sightseeing Log", FeatureArtRef.Menu(MenuSightseeingLog)),
        ("Variant dungeon", FeatureArtRef.Content(ContentVariantDungeons)),
        ("Duty Recorder", FeatureArtRef.Menu(MenuDutyRecorder)),
        ("Duty Support", FeatureArtRef.Menu(MenuDutySupport)),
        ("Trust", FeatureArtRef.Menu(MenuTrust)),
        ("New Game+", FeatureArtRef.Menu(MenuNewGamePlus)),
        ("Triple Triad", FeatureArtRef.Content(ContentTripleTriad)),
        ("Gold Saucer", FeatureArtRef.Content(ContentGoldSaucer)),
        ("Cactpot", FeatureArtRef.Content(ContentGoldSaucer)),
        ("Chocobo racing", FeatureArtRef.Content(ContentGoldSaucer)),
        ("Fashion Report", FeatureArtRef.Content(ContentGoldSaucer)),
        ("Verminion", FeatureArtRef.Content(ContentGoldSaucer)),
        ("Chocobo saddlebag", FeatureArtRef.Menu(MenuChocoboSaddlebag)),
        ("Chocobo", FeatureArtRef.Menu(MenuCompanion)),
        ("Crystalline Conflict", FeatureArtRef.Content(ContentPvp)),
        ("PvP", FeatureArtRef.Content(ContentPvp)),
        ("Treasure hunt", FeatureArtRef.Content(ContentTreasureHunt)),
        ("Hunts", FeatureArtRef.Content(ContentTheHunt)),
        ("Master recipes", FeatureArtRef.Menu(MenuCraftingLog)),
        ("Ocean fishing", FeatureArtRef.Content(ContentOceanFishing)),
        ("fishing", FeatureArtRef.Menu(MenuFishingLog)),
        ("Housing", FeatureArtRef.Menu(MenuHousing)),
        ("Flying", FeatureArtRef.Menu(MenuAetherCurrents)),
        ("Scrip exchange", FeatureArtRef.Menu(MenuCurrency)),
        ("Levequests", FeatureArtRef.Content(ContentLevequests)),
        ("Guildleves", FeatureArtRef.Content(ContentLevequests)),
        ("Egi glamours", FeatureArtRef.Menu(MenuActionsAndTraits)),
        ("Desynthesis", FeatureArtRef.Menu(MenuActionsAndTraits)),
        ("Materia", FeatureArtRef.Menu(MenuActionsAndTraits)),
        ("Performance", FeatureArtRef.Menu(MenuActionsAndTraits)),
        ("Augmented artifact", FeatureArtRef.Menu(MenuArmouryChest)),
        ("Glamour", FeatureArtRef.Menu(MenuArmouryChest)),
        ("Relic", FeatureArtRef.Menu(MenuArmouryChest)),
        ("Guildhests", FeatureArtRef.Content(ContentGuildhests)),
        ("Eureka Orthos", FeatureArtRef.Content(ContentDeepDungeons)),
        ("Palace of the Dead", FeatureArtRef.Content(ContentDeepDungeons)),
        ("Heaven-on-High", FeatureArtRef.Content(ContentDeepDungeons)),
        ("Eureka", FeatureArtRef.Content(ContentEureka)),
        ("Delubrum", FeatureArtRef.Content(ContentSaveTheQueen)),
        ("Bozja", FeatureArtRef.Content(ContentSaveTheQueen)),
        ("Occult Crescent", FeatureArtRef.Content(ContentOccultCrescent)),
        ("Occult Record", FeatureArtRef.Content(ContentOccultCrescent)),
        ("Phantom jobs", FeatureArtRef.Content(ContentOccultCrescent)),
        ("Island Sanctuary", FeatureArtRef.Content(ContentIslandSanctuary)),
        ("Retainers", FeatureArtRef.Content(ContentRetainerVentures)),
        ("Wondrous Tails", FeatureArtRef.Content(ContentWondrousTails)),
        ("Faux Hollows", FeatureArtRef.ItemRow(ItemFauxLeaf)),
        ("Custom deliveries", FeatureArtRef.Content(ContentCustomDeliveries)),
        ("Grand Company", FeatureArtRef.Content(ContentGrandCompany)),
        ("Adventurer Squadron", FeatureArtRef.Content(ContentGrandCompany)),
        ("Ishgardian Restoration", FeatureArtRef.Content(ContentDiscipleOfTheHand)),
        ("Cosmic Exploration", FeatureArtRef.Content(ContentDiscipleOfTheHand)),
        ("Challenge Log", FeatureArtRef.Menu(MenuChallengeLog)),
        ("Blue Mage", FeatureArtRef.Menu(MenuBlueMagicSpellbook)),
        ("Aesthetician", FeatureArtRef.ItemRow(ItemModernAesthetics)),

        // The training grounds' striking dummies.
        ("Stone, Sky, Sea", FeatureArtRef.ItemRow(ItemStrikingDummy)),
        ("Circles of Answering", FeatureArtRef.ItemRow(ItemStrikingDummy)),
        ("The Lawns", FeatureArtRef.ItemRow(ItemStrikingDummy)),
        ("Burning Field", FeatureArtRef.ItemRow(ItemStrikingDummy)),
        ("Spire of Trial", FeatureArtRef.ItemRow(ItemStrikingDummy)),

        // Passage somewhere: the Map menu, as the zone rows wear.
        ("Airship", FeatureArtRef.Menu(MenuMap)),
        ("White Wolf Gate", FeatureArtRef.Menu(MenuMap)),

        // The Doman Enclave's donations pay out gil.
        ("Doman Enclave", FeatureArtRef.Menu(MenuCurrency)),
    ];

    /// <summary>The row whose icon stands for a feature's label; <see cref="FeatureArtRef.None"/> when nothing fits.</summary>
    public static FeatureArtRef Of(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return FeatureArtRef.None;
        }

        foreach (var (word, art) in Words)
        {
            if (label.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return art;
            }
        }

        return FeatureArtRef.None;
    }

    /// <summary>Every row <see cref="Of"/> can answer, once each: what <see cref="FeatureIcons"/> reads.</summary>
    public static IEnumerable<FeatureArtRef> All() => Words.Select(static w => w.Art).Distinct();
}

/// <summary>
/// The icons of <see cref="FeatureArt"/>'s rows, read from the client's sheets (<c>Tsukimichi.GameData.FeatureIconReader</c>);
/// tests build it by hand. Immutable.
/// </summary>
public sealed class FeatureIcons
{
    public static readonly FeatureIcons Empty = new(FrozenDictionary<FeatureArtRef, uint>.Empty);

    private readonly FrozenDictionary<FeatureArtRef, uint> icons;

    public FeatureIcons(IReadOnlyDictionary<FeatureArtRef, uint> icons)
    {
        ArgumentNullException.ThrowIfNull(icons);
        this.icons = icons.Where(static kv => kv.Value != 0).ToFrozenDictionary();
    }

    /// <summary>How many rows have an icon.</summary>
    public int Count => icons.Count;

    /// <summary>The icon of a row; 0 when unknown.</summary>
    public uint Icon(FeatureArtRef art) => icons.GetValueOrDefault(art);

    /// <summary>The icon a feature's label wears (<see cref="FeatureArt.Of"/>); 0 when no row fits or it was not read.</summary>
    public uint For(string? label) => Icon(FeatureArt.Of(label));
}
