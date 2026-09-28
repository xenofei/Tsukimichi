using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Invariants over the shipped curated files (<c>Tsukimichi/Data/curated</c>) against the frozen catalog fixture and
/// the shipped <c>unique_quests.json</c>. They run everywhere (no game files needed) and carry the Curated trait so CI
/// runs them as their own step.
/// </summary>
[Trait("Category", "Curated")]
public sealed class CuratedInvariantsTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private static string CuratedDir => Path.Combine(FixtureCatalog.ShippedDataDir(), "curated");

    private static CuratedData Curated() => CuratedData.Load(CuratedDir);

    private static UniqueRewardsData Unique() => UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));

    [Fact]
    public void Feature_quests_json_equals_the_set_DataGen_derives()
    {
        // feature_quests.json is written by DataGen from FeaturePresets.Derive over the catalog, the other curated
        // files and the unique-reward entries; a hand edit, or a regen against a different game version than the
        // fixture, shows up here as a set difference.
        var curated = Curated();
        var unique = Unique();
        Assert.NotEmpty(unique.Entries);
        Assert.Equal(fixture.GameVersion, unique.GameVersion);

        var derived = FeaturePresets.Derive(fixture.Bundle.Catalog, curated.WithoutFeatureQuests(), unique.Entries);
        var shipped = curated.FeatureQuests;

        var missing = derived.Where(id => !shipped.Contains(id)).OrderBy(id => id).ToList();
        var extra = shipped.Where(id => !derived.Contains(id)).OrderBy(id => id).ToList();
        Assert.True(missing.Count == 0 && extra.Count == 0,
            $"feature_quests.json differs from the derived set: missing {missing.Count} [{string.Join(", ", missing.Take(10))}], extra {extra.Count} [{string.Join(", ", extra.Take(10))}]; regenerate with tools/regen.ps1");

        // The file itself: an object with a note and questRowIds sorted ascending without duplicates.
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(CuratedDir, CuratedData.FeatureQuestsFileName)), documentOptions: CuratedData.StrictOptions)!.AsObject();
        var ids = root["questRowIds"]!.AsArray().Select(n => n!.GetValue<uint>()).ToList();
        Assert.Equal(ids.OrderBy(id => id).Distinct(), ids);
        Assert.Equal(derived.Count, ids.Count);
        Assert.Contains("DataGen", (string?)root["note"]);
    }

    [Fact]
    public void Asphodelos_first_circle_is_unlocked_by_Where_Familiars_Dare_not_The_Crystal_from_Beyond()
    {
        // docs/data/verification-report-2.md section 2 row 5: the wiki duty page names Where Familiars Dare as the
        // unlock quest and Garland lists instance 30111 on quest 70012; 70011 only starts the Pandaemonium chain.
        var curated = Curated();

        Assert.True(curated.DutyUnlocks.TryGetValue(70012, out var unlock), "70012 Where Familiars Dare has no duty unlock entry");
        Assert.Contains(808u, unlock!.ContentFinderConditionIds);
        Assert.False(curated.DutyUnlocks.ContainsKey(70011), "70011 The Crystal from Beyond must not carry the Asphodelos unlock");
        Assert.DoesNotContain(70011u, curated.SystemUnlocks.Keys);
    }

    [Fact]
    public void The_Crystal_from_Beyond_stays_an_unlock_quest_under_the_EventIconType_rule()
    {
        // 70011 lost its curated duty unlock, but the sheet draws it with the blue "+" journal icon (EventIconType 8),
        // so it remains a feature quest through FeaturePresets alone: the curated files are not needed for that.
        var quest = fixture.Bundle.Catalog.ByRowId[70011];
        Assert.Equal("The Crystal from Beyond", quest.Name);
        Assert.Equal(FeaturePresets.FeatureEventIconType, quest.EventIconType);
        Assert.True(FeaturePresets.IsFeatureQuest(quest, CuratedData.Empty), "70011 should be a feature quest by its journal icon alone");

        var successor = fixture.Bundle.Catalog.ByRowId[70012];
        Assert.Equal("Where Familiars Dare", successor.Name);
        Assert.Equal(FeaturePresets.FeatureEventIconType, successor.EventIconType);
    }
}
