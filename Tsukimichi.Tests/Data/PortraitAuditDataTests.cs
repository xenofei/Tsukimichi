using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Portraits;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The 1.22.1 portrait audit as shipped in the curated file (docs/research/portrait-audit): the owner's reviewed boxes
/// and the reconciled crops the face-centring supervisor passed are what <c>giver_portraits.json</c> holds, and the
/// owner's examples land their eyes on the plate's eye line.
/// </summary>
public sealed class PortraitAuditCurationTests
{
    private static PortraitCuration Curation() => CuratedData.Load(FixtureCatalog.CuratedDir()).GiverPortraits;

    private static JsonObject Audit(string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(FixtureCatalog.ShippedDataDir()))!, "docs", "research", "portrait-audit", file)))!.AsObject();

    [Fact]
    public void Every_portrait_the_owner_reviewed_ships_the_owners_box()
    {
        var curation = Curation();
        var game = Audit("desk/owner-answers.json")["corrections"]!.AsArray().Where(c => (string?)c!["kind"] == "game").ToList();
        Assert.Equal(126, game.Count);
        foreach (var correction in game)
        {
            var icon = (uint)correction!["id"]!;
            var source = PortraitSources.FamilyOfIcon(icon);
            var (width, height) = PortraitSources.TextureSize(source);
            Assert.Equal([width, height], correction["source"]!.AsArray().Select(n => (int)n!));

            // Team C's conversion: side = s·W, x = cx·W − side/2, y = cy·H − side/2, rounded (none needed clamping).
            var box = correction["box"]!;
            var side = (double)box["s"]! * width;
            var x = Math.Round(((double)box["cx"]! * width) - (side / 2), MidpointRounding.ToEven);
            var y = Math.Round(((double)box["cy"]! * height) - (side / 2), MidpointRounding.ToEven);
            side = Math.Round(side, MidpointRounding.ToEven);
            var bounds = PortraitSources.ArtBounds(source);
            Assert.True(x >= Math.Floor(bounds.U0 * width) && y >= Math.Floor(bounds.V0 * height) && x + side <= Math.Ceiling(bounds.U1 * width) && y + side <= Math.Ceiling(bounds.V1 * height), $"{icon} box ({x}, {y}, {side}) leaves the art");
            Assert.True(curation.IconCrops.TryGetValue(icon, out var shipped), $"{icon} has no iconCrop");
            Assert.Equal(PortraitCrop.FromBox(source, (float)x, (float)y, (float)side), shipped);
        }
    }

    [Fact]
    public void Every_unreviewed_change_the_supervisor_passed_ships_the_reconciled_crop()
    {
        var curation = Curation();
        var reviewed = Audit("desk/owner-answers.json")["corrections"]!.AsArray().Select(c => (string)c!["key"]!).ToHashSet(StringComparer.Ordinal);
        var verdicts = Audit("reconciled/supervisor/verdicts.json")["verdicts"]!.AsObject();
        var applied = 0;
        foreach (var row in Audit("reconciled/reconciled.json")["rows"]!.AsArray())
        {
            var key = (string)row!["key"]!;
            if ((string?)row["kind"] != "game" || reviewed.Contains(key) || (string?)row["final"]!["action"] != "change")
            {
                continue;
            }

            // Every unreviewed change was passed by the supervisor; none is left out.
            Assert.Equal("pass", (string?)verdicts[key]?["verdict"]);
            var icon = (uint)row["id"]!;
            var final = row["final"]!["curated"]!;
            var eyes = final["eyes"]!.AsArray();
            var expected = PortraitFraming.CropFor(PortraitSources.FamilyOfIcon(icon), new PortraitLandmarks((float)(double)eyes[0]!, (float)(double)eyes[1]!, (float)(double)final["chin"]!));
            Assert.Equal(expected, curation.IconCrops[icon]);
            applied++;
        }

        Assert.Equal(141, applied);
    }

    [Theory]
    [InlineData(72650u, 0.457f, 0.198f)] // Varshahn, the owner's box (0, 5, 188)
    [InlineData(72661u, 0.421f, 0.194f)] // Wuk Lamat
    [InlineData(72659u, 0.416f, 0.489f)] // Krile, Dawntrail
    [InlineData(72626u, 0.387f, 0.252f)] // Y'shtola, Shadowbringers on
    public void The_owners_examples_land_their_eyes_on_the_eye_line(uint icon, float eyeU, float eyeV)
    {
        // The audit's measured eyes (reconciled.json final.landmarks); 0.44 ± 0.04 down, the centre ± 0.10 across.
        var (x, y) = OnPlate(Curation().CropTable().For(PortraitSource.TrustBust, icon), eyeU, eyeV);
        Assert.InRange(y, 0.40f, 0.48f);
        Assert.InRange(x, 0.40f, 0.60f);
    }

    [Fact]
    public void Estiniens_bust_ships_the_owners_framing_with_his_whole_face_on_the_plate()
    {
        // The owner's box (0, 20, 161). His face sits at the bust's left edge (eyes 0.288, 0.211; chin 0.309), so a box
        // this size clamps at x 0 and puts his eyes at 0.34 across and 0.50 down: the owner framed his head rather than
        // his eyes (a box of side 145 or less would centre them within 0.10). Before the audit the family box put his eyes
        // at 0.11, the rim. The check here is Team C's "cut" rule: eye line at least 0.30, chin at most 0.97.
        var crop = Curation().CropTable().For(PortraitSource.TrustBust, 72644);
        Assert.Equal(PortraitCrop.FromBox(PortraitSource.TrustBust, 0, 20, 161), crop);
        var (x, y) = OnPlate(crop, 0.288f, 0.211f);
        var (_, chin) = OnPlate(crop, 0.288f, 0.309f);
        Assert.InRange(y, 0.30f, 0.55f);
        Assert.InRange(x, 0.30f, 0.70f);
        Assert.True(chin <= 0.97f, $"chin at {chin}");
        Assert.True(OnPlate(PortraitCrop.FromBox(PortraitSource.TrustBust, 6, 86, 140), 0.288f, 0.211f).Y < 0.30f, "the old family box did not cut him");
    }

    internal static (float X, float Y) OnPlate(PortraitCrop crop, float u, float v) => ((u - crop.U0) / (crop.U1 - crop.U0), (v - crop.V0) / (crop.V1 - crop.V0));
}

/// <summary>
/// The 1.22.1 portrait audit against the game (docs/research/portrait-audit): the owner's examples wear the portraits
/// the review settled on, curated game art beats the pack, the era comes before the family, and no cast plate shows a
/// later look than its quest.
/// </summary>
public class PortraitAuditDataTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    private static PortraitCuration Curation() => CuratedData.Load(FixtureCatalog.CuratedDir()).GiverPortraits;

    private PortraitIndex Build() =>
        GiverPortraitSources.Build(fixture.Game.Excel, Curation(), icon => fixture.Game.FileExists(RewardArtIndex.IconPath(icon)), message => output.WriteLine(message));

    private List<QuestRecord> QuestsOf(PortraitIndex index, string name) =>
        fixture.Bundle.Catalog.All.Where(q => q.Issuer is { NpcId: > 0 } issuer && index.NameOf(issuer.NpcId) == name && q.Festival == 0).ToList();

    /// <summary>A pack with a photo of every giver: what a full pack would offer each of them.</summary>
    private PortraitPack FullPack() =>
        new(Path.Combine(Path.GetTempPath(), "tsukimichi-audit-full-pack"), PortraitPackTests.Manifest(GiverPortraitSources.Read(fixture.Game.Excel).Givers.Select(g => g.NpcId).Distinct().Select(id => ($"{id}.png", new[] { id })).ToArray()), new string('a', 64), "portraits-1");

    [GameDataFact]
    public void The_owners_examples_wear_their_reviewed_portraits()
    {
        var index = Build();
        Assert.All(QuestsOf(index, "Varshahn"), q => Assert.Equal(72650u, index.For(q).Icon));
        Assert.All(QuestsOf(index, "Wuk Lamat").Where(q => q.Expansion == 5), q => Assert.Equal(72661u, index.For(q).Icon));
        Assert.All(QuestsOf(index, "Krile").Where(q => q.Expansion == 5), q => Assert.Equal(72659u, index.For(q).Icon));
        Assert.All(QuestsOf(index, "Estinien").Where(q => q.Expansion >= 4), q => Assert.Equal(72644u, index.For(q).Icon));

        // Estinien's Heavensward quests keep his helmeted card (the owner's Q1), through the owner's box.
        Assert.All(QuestsOf(index, "Estinien").Where(q => q.Expansion == 1), q => Assert.Equal(87088u, index.For(q).Icon));
        Assert.NotEmpty(QuestsOf(index, "Varshahn"));
    }

    [GameDataFact]
    public void Yshtolas_shadowbringers_and_endwalker_quests_wear_her_bust_not_her_pack_photo()
    {
        var index = Build().WithPack(FullPack());
        var quests = QuestsOf(index, "Y'shtola").Where(q => q.Expansion is 3 or 4).ToList();
        Assert.NotEmpty(quests);
        Assert.All(quests, q => Assert.Equal((72626u, PortraitSource.TrustBust), (index.For(q).Icon, index.For(q).Source)));
    }

    [GameDataFact]
    public void Curated_game_art_is_never_replaced_by_a_pack_photo()
    {
        var plain = Build();
        var withPack = plain.WithPack(FullPack());
        var kept = 0;
        foreach (var quest in GiverPortraitSources.Read(fixture.Game.Excel).Quests)
        {
            var game = plain.For(quest.GiverId, quest.QuestId);
            if (game.HasArt && plain.Crops.HasIconCrop(game.Icon))
            {
                Assert.Equal(game, withPack.For(quest.GiverId, quest.QuestId));
                kept++;
            }
        }

        output.WriteLine($"{kept} quests keep their curated game art over the pack");
        Assert.True(kept > 800, $"only {kept} quests wear curated game art");
    }

    [GameDataFact]
    public void Alphinauds_a_realm_reborn_quests_wear_the_twins_card_through_the_owners_box()
    {
        var index = Build();
        var quests = QuestsOf(index, "Alphinaud").Where(q => q.Expansion == 0).ToList();
        Assert.NotEmpty(quests);
        foreach (var quest in quests)
        {
            var portrait = index.For(quest);
            Assert.Equal(87059u, portrait.Icon);
            Assert.Equal(PortraitCrop.FromBox(PortraitSource.TripleTriadCard, 26, 27, 154), portrait.Crop);
        }

        // Only Alphinaud: the card is not offered to anyone else by this.
        Assert.DoesNotContain(QuestsOf(index, "Alisaie"), q => index.For(q).Icon == 87059);
    }

    [GameDataFact]
    public void Sphenes_7_0_quests_show_the_veiled_queen()
    {
        var index = Build();
        uint[] queen = [1047403, 1047406, 1048062, 1048067];
        var quests = fixture.Bundle.Catalog.All.Where(q => q.Issuer is { } issuer && queen.Contains(issuer.NpcId)).ToList();
        Assert.Equal(4, quests.Count);
        Assert.All(quests, q => Assert.Equal(87425u, index.For(q).Icon));
    }

    [GameDataFact]
    public void The_era_comes_before_the_family()
    {
        var index = Build();
        Assert.All(QuestsOf(index, "Tataru").Where(q => q.Expansion >= 4), q => Assert.Equal(73072u, index.For(q).Icon));
        Assert.All(QuestsOf(index, "Zero").Where(q => q.Expansion == 5), q => Assert.Contains(index.For(q).Icon, new uint[] { 73274, 87467 }));
        Assert.All(QuestsOf(index, "Yugiri").Where(q => q.Expansion == 2), q => Assert.Equal(73081u, index.For(q).Icon));
        Assert.False(index.For(1007763, 66738u).HasArt);
        Assert.False(index.For(1007763, 66739u).HasArt);

        // Raubahn's helmeted Stormblood battle-talk face never shows: his Heavensward face or his card instead.
        var raubahn = QuestsOf(index, "Raubahn");
        Assert.Contains(raubahn, q => q.Expansion == 2);
        Assert.All(raubahn, q => Assert.Contains(index.For(q).Icon, new uint[] { 73130, 87067 }));
    }

    [GameDataFact]
    public void The_trust_outfits_are_read_with_their_looks_era()
    {
        var faces = GiverPortraitSources.Read(fixture.Game.Excel).Faces;
        Assert.Contains(faces, f => f is { Icon: 72632, Source: PortraitSource.TrustBust, Name: "Thancred", Era: 1 });
        Assert.Contains(faces, f => f is { Icon: 72633, Name: "Urianger", Era: 0 });
        Assert.Contains(faces, f => f is { Icon: 72634, Name: "Y'shtola", Era: 1 });
        Assert.DoesNotContain(faces, f => f.Icon is (>= 72640 and <= 72643) or 72645 or 72646 or (>= 72652 and <= 72658) or 72660);
    }

    [GameDataFact]
    public void No_cast_plate_shows_a_later_look_than_its_quest()
    {
        var index = Build().WithPack(FullPack());
        var cast = fixture.Bundle.Cast;
        var plates = 0;
        foreach (var quest in fixture.Bundle.Catalog.All.Where(q => q.Festival == 0 && cast.HasCast(q.RowId)))
        {
            foreach (var member in cast.Of(quest.RowId))
            {
                var plate = index.ForCast(member.Key, member.NpcIds, cast.RowsIn(quest.RowId, member), quest.Expansion);
                if (!plate.HasArt)
                {
                    continue;
                }

                plates++;
                Assert.True(plate.Era <= quest.Expansion, $"{member.Key} on quest {quest.RowId} (expansion {quest.Expansion}) wears {plate.Icon} of era {plate.Era}");
                Assert.True(plate.Source != PortraitSource.Pack || index.RowEraOf(plate.Icon) <= quest.Expansion, $"{member.Key} on quest {quest.RowId} wears the photo of row {plate.Icon}, era {index.RowEraOf(plate.Icon)}");
            }
        }

        output.WriteLine($"{plates} cast plates with a face");
        Assert.True(plates > 1000, $"only {plates} cast plates have a face");

        // Emet-Selch's Shadowbringers quests never show his Endwalker (Elpis) look, photo or bust.
        var emet = cast.Find("Emet-Selch");
        Assert.NotNull(emet);
        var shb = emet.StoryQuests.Select(fixture.Bundle.Catalog.GetByRowId).Where(q => q is { Expansion: 3 }).ToList();
        Assert.NotEmpty(shb);
        foreach (var quest in shb)
        {
            var plate = index.ForCast(emet.Key, emet.NpcIds, cast.RowsIn(quest!.RowId, emet), 3);
            Assert.NotEqual(72648u, plate.Icon);
            Assert.True(!plate.HasArt || plate.Source != PortraitSource.Pack || index.RowEraOf(plate.Icon) <= 3);
        }
    }
}
