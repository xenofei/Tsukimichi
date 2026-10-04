using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The unlock index (feature plan v6 K1) over the installed game: the sheet links' floors, the wiki's own MSQ rows
/// (unlocks spec §2.3), the first-visit rule's coverage and the curated aetherytes it cannot reach.
/// </summary>
public sealed class UnlockLinkReaderTests(UnlockIndexFixture fixture, ITestOutputHelper output) : IClassFixture<UnlockIndexFixture>
{
    private const uint NotWithoutIncident = 68005;
    private const uint OnceMoreToTheRubySea = 68012;
    private const uint ConfederateConsternation = 68016;
    private const uint Kugane = 628;
    private const uint SirensongSea = 238;
    private const uint RubyPrice = 120;
    private const uint Onokoro = 106;
    private const uint EasternBow = 154;

    private QuestUnlocks Index => fixture.Unlocks;

    private UnlockLinks Links => fixture.Links;

    [GameDataFact]
    public void The_sheet_links_hold_their_floors()
    {
        // 45 quests gate 102 warps; 27 of them lead to an area rather than a room of a story building.
        Assert.True(Links.Warps.Select(w => w.QuestRowId).Distinct().Count() >= 25, $"warp-gated quests: {Links.Warps.Select(w => w.QuestRowId).Distinct().Count()}");
        Assert.True(Links.MapRegions.Select(r => r.QuestRowId).Distinct().Count() >= 14, $"map-condition quests: {Links.MapRegions.Select(r => r.QuestRowId).Distinct().Count()}");
        // 37 Map and PlaceName rows, 28 distinct regions once a map and its own place name count once.
        Assert.True(Links.MapRegions.Count >= 25, $"map-condition links: {Links.MapRegions.Count}");
        Assert.Equal(13, Links.GatedAethernet.Count);
        Assert.All(Links.GatedAethernet, gate => Assert.False(Links.Aetherytes.Single(a => a.AetheryteId == gate.AetheryteId).IsAetheryte));
        Assert.True(Links.Touches.Count > 10_000, $"objective points: {Links.Touches.Count}");
        Assert.True(Links.AreaIcon > 0, "the Map menu's icon was not read");
        Assert.Contains(Links.Duties, d => d.ContentFinderConditionId == SirensongSea && d.Level == 61 && d.Icon == 61801);
    }

    [GameDataFact]
    public void Not_without_Incident_opens_Kugane_and_the_Sirensong_Sea()
    {
        var entries = Index.For(NotWithoutIncident);
        Dump(NotWithoutIncident);
        var kugane = Assert.Single(entries, e => e.Target == UnlockTarget.Zone && e.TargetId == Kugane);
        Assert.Equal("Kugane", kugane.Name);
        Assert.Equal(UnlockSource.Sheet, kugane.Source);
        var duty = Assert.Single(entries, e => e.Target == UnlockTarget.Dungeon && e.TargetId == SirensongSea);
        Assert.Equal("The Sirensong Sea", duty.Name);
        Assert.Contains(entries, e => e.Target == UnlockTarget.NextQuest && e.Name == "The Man from Ul'dah");
        Assert.Equal("Kugane", Index.Headline(NotWithoutIncident)!.Name);
        Assert.Contains(NotWithoutIncident, Index.UnlockedBy(UnlockTarget.Zone, Kugane));
    }

    [GameDataFact]
    public void Once_More_to_the_Ruby_Sea_opens_the_Ruby_Sea()
    {
        var entries = Index.For(OnceMoreToTheRubySea);
        Dump(OnceMoreToTheRubySea);
        var zone = Assert.Single(entries, e => e.Target == UnlockTarget.Zone && e.Name == "The Ruby Sea");
        Assert.Equal(UnlockSource.Derived, zone.Source);
        Assert.Contains(entries, e => e.Target == UnlockTarget.AethernetShard && e.TargetId == RubyPrice && e.Source == UnlockSource.Sheet);
    }

    [GameDataFact]
    public void Confederate_Consternation_opens_the_Eastern_Bow_and_Onokoro()
    {
        var entries = Index.For(ConfederateConsternation);
        Dump(ConfederateConsternation);

        // The Eastern Bow is one of its Rewards (Quest.EmoteReward): a Rewards tile, never an unlock row.
        var emote = Assert.Single(Index.IncludingRewards(ConfederateConsternation), e => e.Target == UnlockTarget.Emote && e.TargetId == EasternBow);
        Assert.Equal("Eastern Bow", emote.Name, ignoreCase: true);
        Assert.True(emote.InRewards);
        Assert.DoesNotContain(entries, e => e.Target == UnlockTarget.Emote);
        var onokoro = Assert.Single(entries, e => e.Target == UnlockTarget.Aetheryte && e.TargetId == Onokoro);
        Assert.Equal("Onokoro", onokoro.Name);
        Assert.True(onokoro.IsLikely);
        Assert.Equal(3, entries.Count(e => e.Target == UnlockTarget.NextQuest));
    }

    [GameDataFact]
    public void The_first_visit_rule_reaches_nearly_every_zone_and_aetheryte()
    {
        var zonesWithAetheryte = Links.Zones.Where(z => z.AetheryteId != 0 && fixture.Game.Excel.GetSheet<TerritoryType>(Language.English).GetRow(z.TerritoryId).TerritoryIntendedUse.RowId is 0 or 1).ToList();
        var reachedZones = zonesWithAetheryte.Where(z => Index.UnlockedBy(UnlockTarget.Zone, z.TerritoryId).Count > 0).ToList();
        var missedZones = zonesWithAetheryte.Except(reachedZones).Select(z => $"{z.TerritoryId} {z.Name}").ToList();
        output.WriteLine($"zones: {reachedZones.Select(z => z.Name).Distinct().Count()} of {zonesWithAetheryte.Select(z => z.Name).Distinct().Count()} names; missed {string.Join(", ", missedZones)}");

        var aetherytes = Links.Aetherytes.Where(a => a.IsAetheryte).ToList();
        var reached = aetherytes.Where(a => Index.UnlockedBy(UnlockTarget.Aetheryte, a.AetheryteId).Count > 0).ToList();
        var missed = aetherytes.Except(reached).ToList();
        output.WriteLine($"aetherytes: {reached.Count} of {aetherytes.Count}; missed {string.Join(", ", missed.Select(a => $"{a.AetheryteId} {a.Name}"))}");
        Assert.True(reachedZones.Select(z => z.Name).Distinct().Count() >= 65, "first-visit coverage of the zones fell");
        var byRule = UnlockAreas.Derive(fixture.Catalog, Links).Aetherytes.Values.SelectMany(a => a).Distinct().Count();
        output.WriteLine($"aetherytes by the rule alone: {byRule}");
        Assert.True(byRule >= 87, "the first-visit rule's aetheryte coverage fell");
        // Every teleportable aetheryte has its quest since 1.19 (K5): the rule's and the curated file's together.
        Assert.True(missed.Count == 0, "aetherytes no quest opens: " + string.Join(", ", missed.Select(a => $"{a.AetheryteId} {a.Name}")));
    }

    [GameDataFact]
    public void Every_aetheryte_the_rule_cannot_reach_is_curated()
    {
        // A new patch's aetheryte fails here until aetheryte_unlocks.json names its quest.
        var curated = fixture.Curated.AetheryteUnlocks;
        var derived = UnlockAreas.Derive(fixture.Catalog, Links).Aetherytes.Values.SelectMany(a => a).ToHashSet();
        var missing = Links.Aetherytes
            .Where(a => a.IsAetheryte && !derived.Contains(a.AetheryteId) && !curated.ContainsKey(a.AetheryteId) && !UnlockIndexFixture.NotYetCurated.ContainsKey(a.AetheryteId))
            .Select(a => $"{a.AetheryteId} {a.Name}")
            .ToList();
        Assert.True(missing.Count == 0, "aetherytes no quest opens, neither by the rule nor curated: " + string.Join(", ", missing));

        // The allowlist never outlives its gap: an aetheryte the rule or the file now places comes off it.
        var stale = UnlockIndexFixture.NotYetCurated.Keys.Where(id => derived.Contains(id) || curated.ContainsKey(id)).ToList();
        Assert.True(stale.Count == 0, "take these off NotYetCurated: " + string.Join(", ", stale));
        Assert.All(UnlockIndexFixture.NotYetCurated.Keys, id => Assert.Contains(Links.Aetherytes, a => a.AetheryteId == id && a.IsAetheryte));

        // Curated rows are not also inferred for another quest.
        foreach (var id in curated.Keys)
        {
            Assert.All(Index.UnlockedBy(UnlockTarget.Aetheryte, id), q => Assert.Contains(q, curated[id].Quests));
        }

        // Each curated entry names a real aetheryte and quests of the catalog.
        foreach (var (id, unlock) in curated)
        {
            var aetheryte = Assert.Single(Links.Aetherytes, a => a.AetheryteId == id);
            Assert.Equal(aetheryte.Name, unlock.Name);
            Assert.All(unlock.Quests, q => Assert.NotNull(fixture.Catalog.GetByRowId(q)));
            Assert.All(unlock.Quests, q => Assert.Contains(Index.For(q), e => e.Target == UnlockTarget.Aetheryte && e.TargetId == id && e.Source == UnlockSource.Curated));
        }
    }

    [GameDataFact]
    public void A_return_warp_is_no_unlock()
    {
        // Where the Heart Is (The Lavender Beds) opens the district; the warp back to Old Gridania is a way home.
        var entries = Index.For(66748);
        Dump(66748);
        Assert.Contains(entries, e => e.Target == UnlockTarget.Zone && e.Name == "The Lavender Beds");
        Assert.DoesNotContain(entries, e => e.Target == UnlockTarget.Zone && e.Name == "Old Gridania");
    }

    [GameDataFact]
    public void The_spot_check_rows_read_as_the_spec_says()
    {
        foreach (var rowId in new uint[] { 65621, 66196, 70058, 65970, 66968, 67116, 69208, 68815, 69893, 70396 })
        {
            Dump(rowId);
        }

        Assert.Contains(Index.For(66196), e => e.Group == UnlockGroup.Duty && e.Name == "Copperbell Mines");
        Assert.Contains(Index.For(65970), e => e.Target == UnlockTarget.Zone && e.Name == "The Gold Saucer");
        Assert.Contains(Index.For(66968), e => e.Target == UnlockTarget.System && e.Name.Contains("Retainer", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(Index.For(68815), e => e.Target == UnlockTarget.WorldMap && e.Name == "Norvrandt");
        Assert.Contains(Index.For(69893), e => e.Target == UnlockTarget.Zone && e.Name == "Old Sharlayan");
        Assert.Contains(Index.For(70396), e => e.Target == UnlockTarget.Zone && e.Name == "Tuliyollal");
        Assert.Contains(Index.For(65621), e => e.Target == UnlockTarget.Zone && e.Name == "New Gridania");
    }

    private void Dump(uint rowId)
    {
        output.WriteLine($"## {rowId} {fixture.Catalog.GetByRowId(rowId)?.Name}");
        foreach (var entry in Index.IncludingRewards(rowId))
        {
            output.WriteLine($"   {entry.Group,-11} {entry.Target,-14} {entry.TargetId,7} {entry.Name} [{entry.Source}{(entry.InRewards ? ", reward" : string.Empty)}] icon {entry.Icon} · {entry.Caption}");
        }
    }
}

/// <summary>The unlock index over the installed game, built once per test class (lazy, so skipped runs never read the disk).</summary>
public sealed class UnlockIndexFixture : IDisposable
{
    /// <summary>
    /// Teleportable aetherytes neither the first-visit rule nor <c>aetheryte_unlocks.json</c> places yet, each with why:
    /// no main scenario objective stands near enough to tell which quest first takes a character there. Their rows are
    /// simply absent. Empty since 1.19 (feature plan v7 K5 curated the last eleven). A new patch's aetheryte is not in
    /// this list, so it fails <see cref="UnlockLinkReaderTests"/> until it is curated or listed here with a reason.
    /// </summary>
    public static readonly IReadOnlyDictionary<uint, string> NotYetCurated = new Dictionary<uint, string>();

    private readonly GameDataFixture game = new();
    private readonly Lazy<(UnlockLinks Links, QuestUnlocks Unlocks, CuratedData Curated)> built;

    public UnlockIndexFixture()
    {
        built = new(Build, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public GameDataFixture GameFixture => game;

    public Lumina.GameData Game => game.Game;

    public QuestCatalog Catalog => game.Bundle.Catalog;

    public UnlockLinks Links => built.Value.Links;

    public QuestUnlocks Unlocks => built.Value.Unlocks;

    public CuratedData Curated => built.Value.Curated;

    public void Dispose() => game.Dispose();

    private (UnlockLinks, QuestUnlocks, CuratedData) Build()
    {
        var excel = game.Game.Excel;
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var rewards = UniqueRewardCatalog.Build(unique, new Dictionary<uint, UniqueOverride>(), curated);
        var links = UnlockLinkReader.Read(excel, Language.English);
        var duties = DutyIndex.Build(excel, Language.English);
        // The spoiler shield's placeholders name expansions from the client's ExVersion sheet, as the plugin does.
        var names = game.Bundle.Names;
        return (links, QuestUnlocks.Build(Catalog, rewards, duties, links, curated, id => names.Expansion(id)), curated);
    }
}
