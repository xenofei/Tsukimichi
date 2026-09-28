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
