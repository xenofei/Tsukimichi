using Lumina.Data.Files;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Giver portraits against the game (feature plan v7 F1, F3): the index built from the install with the shipped curated
/// overlay covers at least what the research measured (118 givers, 69 % of main scenario quests), every face it offers
/// exists, the curated blocks hold, and the shipped delivery keep masks are the ones the keying makes of today's art.
/// </summary>
public class GiverPortraitDataTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    private static PortraitCuration Curation() => CuratedData.Load(FixtureCatalog.CuratedDir()).GiverPortraits;

    private PortraitIndex Build() =>
        GiverPortraitSources.Build(fixture.Game.Excel, Curation(), icon => fixture.Game.FileExists(RewardArtIndex.IconPath(icon)), message => output.WriteLine(message));

    [GameDataFact]
    public void Coverage_holds_the_research_floor()
    {
        var index = Build();
        var named = fixture.Bundle.Catalog.All
            .Where(q => q.Issuer is { NpcId: > 0 } issuer && !q.IsRetired && index.NameOf(issuer.NpcId) is { Length: > 0 } name && !PortraitNames.IsGeneric(name))
            .ToList();
        var covered = named.Where(q => index.For(q).HasArt).ToList();
        var msq = named.Where(FeaturePresets.IsMainScenario).ToList();
        var msqCovered = msq.Count(q => index.For(q).HasArt);
        var givers = covered.Select(q => PortraitNames.Normalize(index.NameOf(q.Issuer!.NpcId))).Distinct().Count();
        output.WriteLine($"named-giver quests {named.Count}, with a portrait {covered.Count} ({100.0 * covered.Count / named.Count:F1} %); main scenario {msq.Count}, with a portrait {msqCovered} ({100.0 * msqCovered / msq.Count:F1} %); givers (by name) {givers}; giver ids with art {index.GiversWithArt} of {index.GiverCount}");
        foreach (var source in PortraitSources.Priority)
        {
            var bySource = covered.Where(q => index.For(q).Source == source).ToList();
            output.WriteLine($"  {source}: {bySource.Count} quests, {bySource.Count(FeaturePresets.IsMainScenario)} main scenario, {bySource.Select(q => index.NameOf(q.Issuer!.NpcId)).Distinct().Count()} givers");
        }

        // The research's own universe: every Quest row with a name whose giver is a named ENpcResident, generic or not.
        var sheet = GiverPortraitSources.Read(fixture.Game.Excel);
        var names = sheet.Givers.ToDictionary(g => g.NpcId, g => g.Name);
        var universe = sheet.Quests.Where(q => names.GetValueOrDefault(q.GiverId) is { Length: > 0 }).ToList();
        var universeCovered = universe.Count(q => index.For(q.GiverId, q.QuestId).HasArt);
        var universeMsq = universe.Where(q => fixture.Bundle.Catalog.GetByRowId(q.QuestId) is { } quest && FeaturePresets.IsMainScenario(quest)).ToList();
        output.WriteLine($"research universe: {universeCovered} of {universe.Count} named-giver quests ({100.0 * universeCovered / universe.Count:F1} %), {universeMsq.Count(q => index.For(q.GiverId, q.QuestId).HasArt)} of {universeMsq.Count} main scenario");

        Assert.True(givers >= 110, $"only {givers} givers have a portrait");
        Assert.True(msqCovered >= msq.Count * 65 / 100, $"only {msqCovered} of {msq.Count} main scenario quests have a portrait");
        Assert.True(covered.Count >= 900, $"only {covered.Count} quests have a portrait");
    }

    [GameDataFact]
    public void The_story_leads_wear_portraits_of_the_right_family()
    {
        var index = Build();
        var byName = fixture.Bundle.Catalog.All
            .Where(q => q.Issuer is { NpcId: > 0 })
            .GroupBy(q => index.NameOf(q.Issuer!.NpcId))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var lead in new[] { "Alphinaud", "Alisaie", "Y'shtola", "Thancred", "Urianger", "Tataru", "Krile", "G'raha Tia", "Wuk Lamat", "Erenville", "Cid", "Raubahn" })
        {
            Assert.True(byName.TryGetValue(lead, out var quests), $"{lead} gives no quest");
            Assert.True(quests.All(q => index.For(q).HasArt), $"{lead} has a quest without a portrait");
        }

        // A Heavensward quest of Thancred's wears his Heavensward card, a Shadowbringers one his Trust bust.
        var thancred = byName["Thancred"];
        Assert.Contains(thancred.Where(q => q.Expansion == 1), q => index.For(q).Icon == 87165);
        Assert.Contains(thancred.Where(q => q.Expansion == 3), q => index.For(q).Source == PortraitSource.TrustBust);
    }

    [GameDataFact]
    public void Every_face_offered_exists_and_crops_inside_its_texture()
    {
        var index = Build();
        var inputs = GiverPortraitSources.Read(fixture.Game.Excel);
        var icons = inputs.Givers.SelectMany(g => index.Variants(g.NpcId)).Select(v => (v.Icon, v.Source)).Distinct().ToList();
        Assert.True(icons.Count > 150, $"only {icons.Count} faces");
        foreach (var (icon, source) in icons)
        {
            Assert.True(fixture.Game.FileExists(RewardArtIndex.IconPath(icon)), $"icon {icon} is not in the game");
            Assert.Equal(source, PortraitSources.FamilyOfIcon(icon));
            Assert.True(index.Crops.For(source, icon).IsValid, $"icon {icon} has no valid crop");
            var hr = fixture.Game.GetFile<TexFile>(RewardArtIndex.IconPath(icon).Replace(".tex", "_hr1.tex", StringComparison.Ordinal));
            if (hr is not null)
            {
                Assert.Equal(PortraitSources.TextureSize(source), (hr.Header.Width, (int)hr.Header.Height));
            }
        }
    }

    [GameDataFact]
    public void Blocked_matches_never_show()
    {
        var curation = Curation();
        Assert.NotEmpty(curation.Blocks);
        var index = Build();
        var inputs = GiverPortraitSources.Read(fixture.Game.Excel);
        var blockedSomewhere = 0;
        foreach (var giver in inputs.Givers)
        {
            var normalized = PortraitNames.Normalize(giver.Name);
            foreach (var block in curation.Blocks.Where(b => b.GiverId == giver.NpcId || b.NormalizedName == normalized))
            {
                blockedSomewhere++;
                Assert.DoesNotContain(index.Variants(giver.NpcId), v => block.Blocks(v.Icon));
            }
        }

        Assert.True(blockedSomewhere > 0, "no giver is named by a block; the blocks are stale");

        // And the matches they replace still exist without them, so each block is doing something.
        var unblocked = GiverPortraitSources.Build(fixture.Game.Excel, curation with { Blocks = [] }, icon => fixture.Game.FileExists(RewardArtIndex.IconPath(icon)));
        foreach (var block in curation.Blocks)
        {
            var givers = inputs.Givers.Where(g => block.GiverId == g.NpcId || block.NormalizedName == PortraitNames.Normalize(g.Name)).ToList();
            Assert.Contains(givers, g => unblocked.Variants(g.NpcId).Any(v => block.Blocks(v.Icon)));
        }
    }

    [GameDataFact]
    public void The_shipped_masks_are_todays_keying_of_todays_art()
    {
        var curation = Curation();
        Assert.Equal(curation.DeliveryKeys.Keys.Order(), curation.Masks.Keys.Order());
        foreach (var (icon, mask) in curation.Masks)
        {
            var raw = fixture.Game.GetFile(mask.TexturePath);
            var texture = fixture.Game.GetFile<TexFile>(mask.TexturePath);
            Assert.NotNull(raw);
            Assert.NotNull(texture);
            Assert.True(mask.Matches(raw!.Data), $"{icon:D6}: the game's art changed; run tools/regen.ps1 to key it again");

            var key = curation.DeliveryKeys[icon];
            var keyed = DeliveryKey.Run(texture!.ImageData, texture.Header.Width, texture.Header.Height, key.SeedX, key.SeedY);
            Assert.Equal(0, keyed.RemovedInsideFigure);
            Assert.True(PortraitMaskFile.TryRead(mask.File, out var width, out var height, out var shipped));
            Assert.Equal((mask.Width, mask.Height), (width, height));
            Assert.Equal(keyed.Keep, shipped);
            output.WriteLine($"{icon:D6}: removed {keyed.Removed}");
        }

        // The port matches the design's reference keying (key_delivery.py) on its two samples.
        Assert.Equal(7069, Removed(61661));
        Assert.Equal(4498, Removed(61662));

        int Removed(uint icon)
        {
            var texture = fixture.Game.GetFile<TexFile>(curation.Masks[icon].TexturePath)!;
            var key = curation.DeliveryKeys[icon];
            return DeliveryKey.Run(texture.ImageData, texture.Header.Width, texture.Header.Height, key.SeedX, key.SeedY).Removed;
        }
    }
}
