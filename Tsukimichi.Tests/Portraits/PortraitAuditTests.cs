using Tsukimichi.Core.Portraits;
using Tsukimichi.GameData;

namespace Tsukimichi.Tests.Portraits;

/// <summary>
/// The 1.22.1 portrait audit's code changes without the game (docs/research/portrait-audit/reconciled/changes.json):
/// curated game art ranks above the pack (C1), cast plates pick a row by the quest (C2), the era comes before the
/// family (C4), late Trust busts take their own family box (C5), a shared card gives each person their own crop (C6),
/// and the Trust outfit alternates are dated by their look (C8).
/// </summary>
public sealed class PortraitAuditTests
{
    private const uint Yshtola = 1026000;
    private const uint Hnaanza = 1026001;
    private const uint EmetShb = 1029000;
    private const uint EmetEw = 1037000;

    // ------------------------------------------------------------------ C1: curated game art before the pack

    [Fact]
    public void Curated_game_art_ranks_above_the_pack_and_uncurated_art_below_it()
    {
        var inputs = new PortraitInputs(
            [
                new PortraitFace(72626, PortraitSource.TrustBust, "Y'shtola", 3),
                new PortraitFace(73025, PortraitSource.BattleTalk, "H'naanza", 3),
            ],
            [new PortraitGiver(Yshtola, "Y'shtola", 4, 1), new PortraitGiver(Hnaanza, "H'naanza", 4, 1)],
            [new PortraitQuest(69001, Yshtola, 3, 0), new PortraitQuest(69002, Hnaanza, 3, 0)],
            new Dictionary<byte, uint>());
        var pack = Pack(Yshtola, Hnaanza);

        // Y'shtola's bust has a measured crop: it wins over her pack photo (which shows her staff).
        var measured = new PortraitCuration { IconCrops = new Dictionary<uint, PortraitCrop> { [72626] = PortraitCrop.FromBox(PortraitSource.TrustBust, 1, 58, 143) } };
        var index = PortraitIndex.Build(inputs, measured).WithPack(pack);
        var yshtola = index.For(Yshtola, 69001);
        Assert.Equal(PortraitSource.TrustBust, yshtola.Source);
        Assert.Equal(72626u, yshtola.Icon);
        Assert.Equal(PortraitCrop.FromBox(PortraitSource.TrustBust, 1, 58, 143), yshtola.Crop);

        // H'naanza's battle-talk face has no crop of its own and no curated name: the pack's photo of her goes first.
        Assert.Equal(PortraitSource.Pack, index.For(Hnaanza, 69002).Source);

        // A face named by the curated file, or a pin, is chosen by hand: it wins too, crop or not.
        var named = new PortraitCuration { Faces = new Dictionary<uint, CuratedFace> { [73025] = new("H'naanza", PortraitSource.BattleTalk, 3, "named") } };
        Assert.Equal(73025u, PortraitIndex.Build(inputs, named).WithPack(pack).For(Hnaanza, 69002).Icon);
        var pinned = new PortraitCuration { Pins = new Dictionary<uint, CuratedPortraitPin> { [Hnaanza] = new(73025, PortraitSource.BattleTalk, "pinned", 3) } };
        Assert.Equal(73025u, PortraitIndex.Build(inputs, pinned).WithPack(pack).For(Hnaanza, 69002).Icon);

        // Without curation the pack goes first for both, as in 1.20.
        var plain = PortraitIndex.Build(inputs).WithPack(pack);
        Assert.Equal(PortraitSource.Pack, plain.For(Yshtola, 69001).Source);
    }

    // ------------------------------------------------------------------ C4: the era before the family

    [Fact]
    public void The_latest_era_wins_across_families_and_ties_go_to_the_better_family()
    {
        // Tataru: her Stormblood card and her Endwalker battle-talk face. An Endwalker quest wears the battle-talk face.
        PortraitVariant[] tataru =
        [
            new(87019, PortraitSource.TripleTriadCard, 0, "Tataru Taru"),
            new(87241, PortraitSource.TripleTriadCard, 2, "Tataru"),
            new(73072, PortraitSource.BattleTalk, 4, "TATARU"),
        ];
        Assert.Equal(73072u, PortraitIndex.Pick(tataru, 4).Icon);
        Assert.Equal(73072u, PortraitIndex.Pick(tataru, 5).Icon);
        Assert.Equal(87241u, PortraitIndex.Pick(tataru, 3).Icon);
        Assert.Equal(87019u, PortraitIndex.Pick(tataru, 0).Icon);

        // Zero: an Endwalker bust, a Dawntrail battle-talk face; Yugiri: a Heavensward card, a Stormblood face.
        PortraitVariant[] zero = [new(72651, PortraitSource.TrustBust, 4, "Zero"), new(73274, PortraitSource.BattleTalk, 5, "ZERO")];
        Assert.Equal(73274u, PortraitIndex.Pick(zero, 5).Icon);
        Assert.Equal(72651u, PortraitIndex.Pick(zero, 4).Icon);
        PortraitVariant[] yugiri = [new(87084, PortraitSource.TripleTriadCard, 1, "Yugiri"), new(73081, PortraitSource.BattleTalk, 2, "YUGIRI")];
        Assert.Equal(73081u, PortraitIndex.Pick(yugiri, 2).Icon);

        // One era: the better family, then the lowest icon.
        PortraitVariant[] tie = [new(73100, PortraitSource.BattleTalk, 2, "x"), new(87199, PortraitSource.TripleTriadCard, 2, "x"), new(87198, PortraitSource.TripleTriadCard, 2, "x")];
        Assert.Equal(87198u, PortraitIndex.Pick(tie, 2).Icon);

        // Any order of the list gives the same pick.
        Assert.Equal(PortraitIndex.Pick(tataru, 4), PortraitIndex.Pick(tataru.Reverse().ToArray(), 4));
        Assert.Equal(PortraitIndex.Pick(tie, 2), PortraitIndex.Pick(tie.Reverse().ToArray(), 2));
    }

    [Fact]
    public void A_trust_strip_is_worn_only_when_no_other_family_has_a_face_that_early()
    {
        PortraitVariant[] variants = [new(73034, PortraitSource.BattleTalk, 1, "ALPHINAUD"), new(72681, PortraitSource.TrustStrip, 3, "Alphinaud")];
        Assert.Equal(73034u, PortraitIndex.Pick(variants, 3).Icon);
        Assert.Equal(72681u, PortraitIndex.Pick([new PortraitVariant(72681, PortraitSource.TrustStrip, 3, "Alphinaud")], 3).Icon);
        Assert.Equal(default, PortraitIndex.Pick(variants, 0));
    }

    [Fact]
    public void Raubahns_helmeted_stormblood_face_is_kept_off_his_quests()
    {
        const uint Raubahn = 1020000;
        var inputs = new PortraitInputs(
            [
                new PortraitFace(87067, PortraitSource.TripleTriadCard, "Raubahn", 0),
                new PortraitFace(73130, PortraitSource.BattleTalk, "RAUBAHN", 1),
                new PortraitFace(73031, PortraitSource.BattleTalk, "RAUBAHN", 2),
            ],
            [new PortraitGiver(Raubahn, "Raubahn", 5, 0)],
            [new PortraitQuest(67000, Raubahn, 2, 0)],
            new Dictionary<byte, uint>());

        // Era first would put the faceless 73031 on his Stormblood quests: the curated guard keeps it off.
        Assert.Equal(73031u, PortraitIndex.Build(inputs).For(Raubahn, 67000).Icon);
        var guard = new PortraitCuration { Blocks = [new CuratedPortraitBlock(0, "raubahn", [73031], "helmeted")] };
        var index = PortraitIndex.Build(inputs, guard);
        Assert.Equal(73130u, index.For(Raubahn, 67000).Icon);
        Assert.DoesNotContain(index.Variants(Raubahn), v => v.Icon == 73031);
    }

    // ------------------------------------------------------------------ C5: the Trust bust family box by range

    [Fact]
    public void Trust_busts_after_shadowbringers_take_their_own_family_box()
    {
        var crops = PortraitCrops.Default;
        Assert.Equal(PortraitCrop.FromBox(PortraitSource.TrustBust, 6, 86, 140), crops.For(PortraitSource.TrustBust, 72621));
        Assert.Equal(PortraitCrop.FromBox(PortraitSource.TrustBust, 6, 86, 140), crops.For(PortraitSource.TrustBust, 72637));
        Assert.Equal(PortraitCrop.FromBox(PortraitSource.TrustBust, 24, 58, 135), crops.For(PortraitSource.TrustBust, PortraitCrops.FirstLateTrustBust));
        Assert.Equal(PortraitCrops.LateTrustBustDefault, crops.For(PortraitSource.TrustBust, 72661));
        Assert.True(PortraitCrops.LateTrustBustDefault.IsValid);

        // Other families keep one box; an icon's own crop still comes first.
        Assert.Equal(crops.For(PortraitSource.BattleTalk), crops.For(PortraitSource.BattleTalk, 73999));
        var own = PortraitCrop.FromBox(PortraitSource.TrustBust, 0, 5, 188);
        var withOwn = new PortraitCrops(null, new Dictionary<uint, PortraitCrop> { [72650] = own });
        Assert.Equal(own, withOwn.For(PortraitSource.TrustBust, 72650));
        Assert.True(withOwn.HasIconCrop(72650));
        Assert.False(withOwn.HasIconCrop(72661));

        // A curated family box replaces the default for every bust, early or late.
        var family = PortraitCrop.FromBox(PortraitSource.TrustBust, 10, 70, 150);
        var replaced = new PortraitCrops(new Dictionary<PortraitSource, PortraitCrop> { [PortraitSource.TrustBust] = family }, null);
        Assert.Equal(family, replaced.For(PortraitSource.TrustBust, 72621));
        Assert.Equal(family, replaced.For(PortraitSource.TrustBust, 72661));
    }

    // ------------------------------------------------------------------ C2: cast plates

    [Fact]
    public void A_cast_plate_never_wears_a_later_rows_photo()
    {
        // Emet-Selch: a Shadowbringers row and an Endwalker (Elpis) row, both photographed; his only game face is the
        // Endwalker Trust bust.
        var index = EmetIndex().WithPack(Pack(EmetShb, EmetEw));
        uint[] rows = [EmetShb, EmetEw];

        // A Shadowbringers quest: the Shadowbringers row's photo, at the quest's era; never the Elpis photo or bust,
        // even when the quest's script names the Endwalker row.
        foreach (var questRows in new[] { Array.Empty<uint>(), [EmetEw], [EmetShb] })
        {
            var plate = index.ForCast("Emet-Selch", rows, questRows, 3);
            Assert.Equal(PortraitSource.Pack, plate.Source);
            Assert.Equal(EmetShb, plate.Icon);
            Assert.Equal(3, plate.Era);
        }

        // Without a photo of the Shadowbringers row there is nothing of his look then: initials, not the later look.
        var later = EmetIndex().WithPack(Pack(EmetEw)).ForCast("Emet-Selch", rows, [], 3);
        Assert.False(later.HasArt);
        Assert.Equal(PortraitFallbackKind.Initials, later.Fallback.Kind);
        Assert.Equal("E", later.Fallback.Initials);

        // An Endwalker quest: the row the script names.
        Assert.Equal(EmetEw, index.ForCast("Emet-Selch", rows, [EmetEw], 4).Icon);
        Assert.Equal(EmetShb, index.ForCast("Emet-Selch", rows, [EmetShb], 4).Icon);

        // A giver's own quest is unchanged: the photo of the giver at the quest's era.
        Assert.Equal(EmetEw, index.For(EmetEw, 70001).Icon);
        Assert.Equal(3, index.RowEraOf(EmetShb));
        Assert.Equal(4, index.RowEraOf(EmetEw));
    }

    [Fact]
    public void A_cast_plate_picks_a_row_of_the_quests_era_and_goes_on_past_a_row_without_art()
    {
        const uint Old = 1001000, Arr = 1001001, Hw = 1001002;
        var inputs = new PortraitInputs(
            [new PortraitFace(87019, PortraitSource.TripleTriadCard, "Tataru", 0), new PortraitFace(73125, PortraitSource.BattleTalk, "Tataru", 1)],
            [new PortraitGiver(Old, "Tataru", 3, 1), new PortraitGiver(Arr, "Tataru", 3, 1), new PortraitGiver(Hw, "Tataru", 3, 1)],
            [new PortraitQuest(65000, Old, 0, 0), new PortraitQuest(65001, Arr, 0, 0), new PortraitQuest(67000, Hw, 1, 0)],
            new Dictionary<byte, uint>());

        // The lowest row is blocked from every portrait (a stand-in or a costume): the next row's art, not initials.
        var curation = new PortraitCuration { Blocks = [new CuratedPortraitBlock(Old, string.Empty, [], "a stand-in")] };
        var index = PortraitIndex.Build(inputs, curation);
        Assert.False(index.For(Old, 0, 0).HasArt);
        Assert.Equal(87019u, index.ForCast("Tataru", [Old, Arr, Hw], [], 0).Icon);

        // The row of the quest's era is tried before the others: its pack photo shows the look then.
        var withPack = index.WithPack(Pack(Arr, Hw));
        Assert.Equal(Hw, withPack.ForCast("Tataru", [Old, Arr, Hw], [], 1).Icon);
        Assert.Equal(Arr, withPack.ForCast("Tataru", [Old, Arr, Hw], [], 0).Icon);

        // A seasonal event's quest starts from the latest row; the shield weighs the photo by that row's era.
        var seasonal = withPack.ForCast("Tataru", [Old, Arr, Hw], [], PortraitIndex.SeasonalEra);
        Assert.Equal(Hw, seasonal.Icon);
        Assert.Equal(1, seasonal.Era);
    }

    [Fact]
    public void A_cast_member_who_gives_no_quest_is_looked_up_by_name()
    {
        const uint Meteion = 1040000;
        var inputs = new PortraitInputs(
            [
                new PortraitFace(87337, PortraitSource.TripleTriadCard, "Meteion", 4),
                new PortraitFace(87093, PortraitSource.TripleTriadCard, "Count Edmont"),
            ],
            [],
            [],
            new Dictionary<byte, uint>());
        var index = PortraitIndex.Build(inputs);

        var plate = index.ForCast("Meteion", [Meteion], [], 4);
        Assert.Equal(87337u, plate.Icon);
        Assert.Equal(4, plate.Era);
        Assert.Equal(0u, plate.GiverId);

        // Never a later face, and never a face whose era nothing tells (no quest of the name's dates it).
        Assert.False(index.ForCast("Meteion", [Meteion], [], 3).HasArt);
        Assert.False(index.ForName("Count Edmont", 5).HasArt);
        Assert.Equal("CE", index.ForName("Count Edmont", 5).Fallback.Initials);

        // Generic names and name blocks hold as for givers.
        Assert.False(index.ForName("troubled adventurer", 5).HasArt);
        var blocked = PortraitIndex.Build(inputs, new PortraitCuration { Blocks = [new CuratedPortraitBlock(0, "meteion", [87337], "x")] });
        Assert.False(blocked.ForName("Meteion", 5).HasArt);
    }

    [Fact]
    public void A_cast_plate_allocates_nothing_once_warm()
    {
        var index = EmetIndex().WithPack(Pack(EmetShb, EmetEw));
        uint[] rows = [EmetShb, EmetEw];
        uint[] none = [];
        for (var i = 0; i < 2; i++)
        {
            index.ForCast("Emet-Selch", rows, none, 3);
            index.ForCast("Emet-Selch", rows, rows, 4);
            index.ForName("Emet-Selch", 4);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            index.ForCast("Emet-Selch", rows, none, 3);
            index.ForCast("Emet-Selch", rows, rows, 4);
            index.ForName("Emet-Selch", 4);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    // ------------------------------------------------------------------ C6: one card, a crop per person

    [Fact]
    public void A_shared_card_gives_each_person_on_it_their_own_crop()
    {
        const uint Hildibrand = 1008716, Nashu = 1008722;
        var hildibrandCrop = PortraitFraming.CropFor(PortraitSource.TripleTriadCard, new PortraitLandmarks(0.649f, 0.294f, 0.4f));
        var nashuCrop = PortraitFraming.CropFor(PortraitSource.TripleTriadCard, new PortraitLandmarks(0.3f, 0.45f, 0.56f));
        var curation = new PortraitCuration
        {
            SharedFaces =
            [
                new CuratedSharedFace(87062, "Nashu Mhakaracca", PortraitSource.TripleTriadCard, 0, nashuCrop, "left"),
                new CuratedSharedFace(87062, "Hildibrand", PortraitSource.TripleTriadCard, 0, hildibrandCrop, "right"),
            ],
        };
        var inputs = new PortraitInputs(
            [new PortraitFace(87062, PortraitSource.TripleTriadCard, "Hildibrand & Nashu", 0)],
            [new PortraitGiver(Hildibrand, "Hildibrand", 1, 0), new PortraitGiver(Nashu, "Nashu Mhakaracca", 3, 1)],
            [new PortraitQuest(66000, Hildibrand, 0, 0), new PortraitQuest(66001, Nashu, 0, 0)],
            new Dictionary<byte, uint>());
        var index = PortraitIndex.Build(inputs, curation);

        var hildibrand = index.For(Hildibrand, 66000);
        var nashu = index.For(Nashu, 66001);
        Assert.Equal(87062u, hildibrand.Icon);
        Assert.Equal(87062u, nashu.Icon);
        Assert.Equal(hildibrandCrop, hildibrand.Crop);
        Assert.Equal(nashuCrop, nashu.Crop);
        Assert.NotEqual(hildibrand.Crop, nashu.Crop);

        // A shared face without a crop of its own wears the card's (the twins' card for Alphinaud: Q8).
        var cardCrop = PortraitCrop.FromBox(PortraitSource.TripleTriadCard, 26, 27, 154);
        var twins = PortraitIndex.Build(
            new PortraitInputs(
                [new PortraitFace(87059, PortraitSource.TripleTriadCard, "Alphinaud & Alisaie", 0)],
                [new PortraitGiver(1006368, "Alphinaud", 1, 0)],
                [new PortraitQuest(65000, 1006368, 0, 0)],
                new Dictionary<byte, uint>()),
            new PortraitCuration
            {
                IconCrops = new Dictionary<uint, PortraitCrop> { [87059] = cardCrop },
                SharedFaces = [new CuratedSharedFace(87059, "Alphinaud", PortraitSource.TripleTriadCard, 0, null, "Q8")],
            });
        var alphinaud = twins.For(1006368, 65000);
        Assert.Equal(87059u, alphinaud.Icon);
        Assert.Equal(cardCrop, alphinaud.Crop);
        Assert.True(Assert.Single(twins.Variants(1006368)).Curated);
    }

    [Fact]
    public void The_curated_file_reads_shared_faces_and_skips_malformed_ones()
    {
        var dir = Directory.CreateTempSubdirectory("tsuki-shared-");
        try
        {
            var path = Path.Combine(dir.FullName, "giver_portraits.json");
            File.WriteAllText(path, """
                {
                  "schema": 1,
                  "sharedFaces": {
                    "87062/Nashu Mhakaracca": { "era": 0, "eyes": [ 0.3, 0.45 ], "chin": 0.56, "note": "left of the card" },
                    "87059/Alphinaud": { "source": "TripleTriadCard", "era": 0, "note": "the card's own crop" },
                    "87062/NASHU MHAKARACCA": { "note": "the same person again" },
                    "87062": { "note": "no name" },
                    "87062/Hildibrand": { "box": [ 1, 2 ], "note": "a box of two numbers" },
                    "73001/Someone": { "source": "TripleTriadCard", "note": "a battle-talk icon named as a card" },
                    "87210/Pipin": { "era": 2 }
                  }
                }
                """);
            var warnings = new List<string>();
            var curation = PortraitCuration.Load(path, warnings);

            Assert.Equal(2, curation.SharedFaces.Count);
            var nashu = curation.SharedFaces[0];
            Assert.Equal((87062u, "Nashu Mhakaracca", PortraitSource.TripleTriadCard, (byte?)0), (nashu.Icon, nashu.Name, nashu.Source, nashu.Era));
            Assert.Equal(PortraitFraming.CropFor(PortraitSource.TripleTriadCard, new PortraitLandmarks(0.3f, 0.45f, 0.56f)), nashu.Crop);
            Assert.Null(curation.SharedFaces[1].Crop);

            // The repeat, the key without a name, the bad box, the wrong family and the missing note.
            Assert.Equal(5, warnings.Count);
            Assert.All(warnings, w => Assert.Contains("sharedFaces", w, StringComparison.Ordinal));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    // ------------------------------------------------------------------ C8: the Trust outfit alternates

    [Fact]
    public void The_trust_outfits_read_are_story_looks_dated_by_their_look()
    {
        var eras = GiverPortraitSources.OutfitLookEras;
        Assert.Equal((byte)1, eras[72632]); // Thancred, Heavensward
        Assert.Equal((byte)0, eras[72633]); // Urianger, A Realm Reborn
        Assert.Equal((byte)1, eras[72634]); // Y'shtola, Heavensward
        Assert.Equal((byte)3, eras[72635]); // Ryne as "Minfilia"
        Assert.All(eras, kv => Assert.Equal(kv.Key <= 72680 ? PortraitSource.TrustBust : PortraitSource.TrustStrip, PortraitSources.FamilyOfIcon(kv.Key)));

        // Never the summer outfits, the Endwalker Trust outfits no giver wears, or Estinien's armour (the owner kept his
        // Heavensward card, Q1).
        uint[] never = [72640, 72641, 72642, 72643, 72645, 72646, 72652, 72653, 72654, 72655, 72656, 72657, 72658, 72660, 72700, 72701, 72702, 72703, 72706, 72712, 72713, 72714, 72715, 72716, 72717, 72718, 72720];
        Assert.All(never, icon => Assert.False(eras.ContainsKey(icon), $"{icon} is read"));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Emet-Selch's two rows: one giving a Shadowbringers quest, one an Endwalker quest; his Endwalker bust.</summary>
    private static PortraitIndex EmetIndex() => PortraitIndex.Build(new PortraitInputs(
        [new PortraitFace(72648, PortraitSource.TrustBust, "Emet-Selch", 4)],
        [new PortraitGiver(EmetShb, "Emet-Selch", 1, 0), new PortraitGiver(EmetEw, "Emet-Selch", 1, 0)],
        [new PortraitQuest(69001, EmetShb, 3, 0), new PortraitQuest(70001, EmetEw, 4, 0)],
        new Dictionary<byte, uint>()));

    private static PortraitPack Pack(params uint[] ids) =>
        new(Path.Combine(Path.GetTempPath(), "tsukimichi-audit-pack"), PortraitPackTests.Manifest(ids.Select(id => ($"{id}.png", new[] { id })).ToArray()), new string('a', 64), "portraits-1");
}
