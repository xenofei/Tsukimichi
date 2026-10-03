using Tsukimichi.Core.Model;
using Tsukimichi.Core.Portraits;

namespace Tsukimichi.Tests.Portraits;

/// <summary>
/// The giver portrait index's rules without the game (feature plan v7 F1, F3): name matching, the era pick, the
/// fallbacks, the curated overlay and the framing rule.
/// </summary>
public sealed class PortraitIndexTests
{
    private const uint Alphinaud = 1001000;
    private const uint Tataru = 1001001;
    private const uint Adventurer = 1001002;
    private const uint Moogle = 1001003;
    private const uint Munavanu = 1001004;
    private const uint Clive = 1001005;
    private const uint Gerolt = 1001006;

    [Fact]
    public void Names_compare_by_letters_and_digits()
    {
        Assert.Equal("yshtola", PortraitNames.Normalize("Y'shtola"));
        Assert.Equal("grahatia", PortraitNames.Normalize("G'raha Tia"));
        Assert.Equal("yshtola", PortraitNames.Normalize("YSHTOLA"));
        Assert.NotEqual(PortraitNames.Normalize("N-4486"), PortraitNames.Normalize("N-7000"));
        Assert.Equal("eleonore", PortraitNames.Normalize("Éléonore"));
    }

    [Theory]
    [InlineData("troubled adventurer", true)]
    [InlineData("Resistance fighter", true)]
    [InlineData("House Fortemps knight", true)]
    [InlineData("", true)]
    [InlineData("Y'shtola", false)]
    [InlineData("Nero tol Scaeva", false)]
    [InlineData("Raya-O-Senna", false)]
    public void Generic_givers_are_lower_case_names_and_roles(string name, bool generic) =>
        Assert.Equal(generic, PortraitNames.IsGeneric(name));

    [Theory]
    [InlineData("Tataru Taru", "TT")]
    [InlineData("Munavanu", "M")]
    [InlineData("G'raha Tia", "GT")]
    [InlineData("Nero tol Scaeva", "NS")]
    [InlineData("", "")]
    public void Initials_are_one_or_two_capitals(string name, string initials) =>
        Assert.Equal(initials, PortraitNames.Initials(name));

    [Fact]
    public void A_giver_matches_by_name_by_id_and_by_a_unique_first_word()
    {
        var index = PortraitIndex.Build(Inputs(
            [
                new PortraitFace(72621, PortraitSource.TrustBust, "Alphinaud", 3),
                new PortraitFace(87019, PortraitSource.TripleTriadCard, "Tataru Taru"),
                new PortraitFace(73034, PortraitSource.BattleTalk, "ALPHINAUD", 1),
                new PortraitFace(61661, PortraitSource.Delivery, "Somebody Else", 2, Munavanu),
            ]));

        Assert.Equal([72621u, 73034u], index.Variants(Alphinaud).Select(v => v.Icon));
        Assert.Equal(87019u, Assert.Single(index.Variants(Tataru)).Icon);

        // A delivery portrait without its keep mask is never offered, even matched by id.
        Assert.Empty(index.Variants(Munavanu));
    }

    [Fact]
    public void A_first_word_shared_by_two_names_matches_neither_and_generic_givers_never_match()
    {
        var index = PortraitIndex.Build(Inputs(
            [
                new PortraitFace(87019, PortraitSource.TripleTriadCard, "Tataru Taru"),
                new PortraitFace(87241, PortraitSource.TripleTriadCard, "Tataru the Second"),
                new PortraitFace(73001, PortraitSource.BattleTalk, "troubled adventurer"),
            ]));

        Assert.Empty(index.Variants(Tataru));
        Assert.Empty(index.Variants(Adventurer));
    }

    [Fact]
    public void The_era_pick_keeps_the_look_of_the_quests_time()
    {
        var index = PortraitIndex.Build(Inputs(
            [
                new PortraitFace(72621, PortraitSource.TrustBust, "Alphinaud", 3),
                new PortraitFace(72638, PortraitSource.TrustBust, "Alphinaud", 4),
                new PortraitFace(73034, PortraitSource.BattleTalk, "ALPHINAUD", 1),
                new PortraitFace(73128, PortraitSource.BattleTalk, "ALPHINAUD", 2),
            ]));

        // The best family with a face not after the quest's era, its latest such face.
        Assert.Equal(72621u, index.For(Alphinaud, 3, 0).Icon);
        Assert.Equal(72638u, index.For(Alphinaud, 5, 0).Icon);

        // No bust that early: the battle-talk face of that time, never a later bust.
        Assert.Equal(73128u, index.For(Alphinaud, 2, 0).Icon);
        Assert.Equal(PortraitSource.BattleTalk, index.For(Alphinaud, 2, 0).Source);

        // Every face is later than the quest: the earliest face of all.
        Assert.Equal(73034u, index.For(Alphinaud, 0, 0).Icon);
        Assert.Equal(1, index.For(Alphinaud, 0, 0).Era);
    }

    [Fact]
    public void A_quest_id_picks_its_era_and_society_and_an_unknown_one_reads_as_the_first_quest()
    {
        var index = PortraitIndex.Build(Inputs(
            [
                new PortraitFace(72621, PortraitSource.TrustBust, "Alphinaud", 3),
                new PortraitFace(73034, PortraitSource.BattleTalk, "ALPHINAUD", 0),
            ]));

        Assert.Equal(72621u, index.For(Alphinaud, 70001u).Icon);
        Assert.Equal(73034u, index.For(Alphinaud, 65001u).Icon);
        Assert.Equal(73034u, index.For(Alphinaud, 99999u).Icon);

        var quest = new QuestRecord { RowId = 70001, Expansion = 3, Issuer = new Issuer(Alphinaud, "Alphinaud", 0, 0, 0, 0, 0) };
        Assert.Equal(72621u, index.For(quest).Icon);
    }

    [Fact]
    public void Every_portrait_carries_its_fallback()
    {
        var index = PortraitIndex.Build(Inputs([]));

        var society = index.For(Munavanu, 70100u);
        Assert.False(society.HasArt);
        Assert.Equal(PortraitFallbackKind.SocietyEmblem, society.Fallback.Kind);
        Assert.Equal(65036u, society.Fallback.SocietyIcon);

        var adventurer = index.For(Adventurer, 1, 0);
        Assert.Equal(PortraitFallbackKind.Silhouette, adventurer.Fallback.Kind);
        Assert.Equal((byte)3, adventurer.Fallback.Race);
        Assert.Equal((byte)1, adventurer.Fallback.Gender);
        Assert.Empty(adventurer.Fallback.Initials);

        Assert.Equal(PortraitFallbackKind.Moon, index.For(Moogle, 1, 0).Fallback.Kind);
        Assert.Equal(PortraitFallbackKind.Initials, index.For(Tataru, 1, 0).Fallback.Kind);
        Assert.Equal("T", index.For(Tataru, 1, 0).Fallback.Initials);
        Assert.Equal(PortraitFallbackKind.Moon, index.For(42, 1, 0).Fallback.Kind);
    }

    [Fact]
    public void Curated_names_aliases_blocks_and_pins_apply()
    {
        var curation = new PortraitCuration
        {
            Faces = new Dictionary<uint, CuratedFace> { [73270] = new("Tataru", PortraitSource.BattleTalk, 5, "named") },
            Aliases = new Dictionary<string, CuratedPortraitAlias> { ["paparimo"] = new("Alphinaud", "alias") },
            Blocks =
            [
                new CuratedPortraitBlock(0, "clive", [87405], "the other Clive"),
                new CuratedPortraitBlock(Gerolt, string.Empty, [], "every portrait"),
            ],
            Pins = new Dictionary<uint, CuratedPortraitPin> { [Munavanu] = new(73001, PortraitSource.BattleTalk, "pinned") },
        };
        var index = PortraitIndex.Build(
            Inputs(
            [
                new PortraitFace(73164, PortraitSource.BattleTalk, "PAPARIMO", 0),
                new PortraitFace(87405, PortraitSource.TripleTriadCard, "Clive Rosfield"),
                new PortraitFace(87034, PortraitSource.TripleTriadCard, "Gerolt"),
            ]),
            curation);

        Assert.Equal(73270u, Assert.Single(index.Variants(Tataru)).Icon);
        Assert.Equal(5, index.For(Tataru, 5, 0).Era);
        Assert.Equal(73164u, Assert.Single(index.Variants(Alphinaud)).Icon);
        Assert.Empty(index.Variants(Clive));
        Assert.Empty(index.Variants(Gerolt));
        Assert.Equal(73001u, index.For(Munavanu, 1, 0).Icon);
    }

    [Fact]
    public void A_delivery_portrait_is_offered_with_its_mask()
    {
        var mask = new PortraitMask(61661, "061661.png", "ui/icon/061000/061661_hr1.tex", new string('a', 64), 400, 480);
        var curation = new PortraitCuration { Masks = new Dictionary<uint, PortraitMask> { [61661] = mask } };
        var index = PortraitIndex.Build(Inputs([new PortraitFace(61661, PortraitSource.Delivery, "Somebody Else", 2, Munavanu)]), curation);

        var portrait = index.For(Munavanu, 2, 0);
        Assert.Equal(PortraitSource.Delivery, portrait.Source);
        Assert.Same(mask, portrait.Mask);
        Assert.Null(index.For(Tataru, 1, 0).Mask);
    }

    [Fact]
    public void Crops_follow_the_framing_rule_and_the_specs_boxes()
    {
        // The eyes land on the 44 % line and the chin on the 81 % line of a square plate.
        var crop = PortraitFraming.CropFor(0.5f, 0.4f, 0.5f, 640, 512);
        var side = (crop.V1 - crop.V0) * 512;
        Assert.Equal((crop.U1 - crop.U0) * 640, side, 3);
        Assert.Equal(PortraitFraming.EyeLine, ((0.4f * 512) - (crop.V0 * 512)) / side, 3);
        Assert.Equal(PortraitFraming.Chin, ((0.5f * 512) - (crop.V0 * 512)) / side, 3);

        // A face near the edge slides inside the art; a card's gold frame stays out.
        var card = PortraitFraming.CropFor(PortraitSource.TripleTriadCard, new PortraitLandmarks(0.95f, 0.3f, 0.45f));
        Assert.True(card.IsValid);
        Assert.True(card.U1 <= 0.933f + 1e-5f);

        // The spec's family box for Duty Support portraits (6, 86, 140 on 188 × 480).
        var bust = PortraitCrops.Default.For(PortraitSource.TrustBust);
        Assert.Equal(6f / 188, bust.U0, 4);
        Assert.Equal(86f / 480, bust.V0, 4);
        Assert.Equal((6f, 86f, 140f), bust.ToBox(PortraitSource.TrustBust), new BoxComparer());
        Assert.All(PortraitSources.Priority, s => Assert.True(PortraitCrops.Default.For(s).IsValid, $"{s} has no valid default"));
    }

    [Fact]
    public void The_curated_file_reads_every_section_and_skips_malformed_entries()
    {
        var dir = Directory.CreateTempSubdirectory("tsuki-portraits-");
        try
        {
            var path = Path.Combine(dir.FullName, "giver_portraits.json");
            File.WriteAllText(path, """
                {
                  "schema": 1,
                  "crops": { "BattleTalk": { "box": [ 164, 154, 172 ] }, "Nothing": { "box": [ 1, 2, 3 ] } },
                  "iconCrops": {
                    "87019": { "box": [ 70, 62, 75 ], "note": "spec" },
                    "72659": { "eyes": [ 0.5, 0.564 ], "chin": 0.68, "note": "measured" },
                    "73001": { "box": [ 70, 62, 75 ] },
                    "99999": { "box": [ 1, 1, 10 ], "note": "no family" }
                  },
                  "faces": { "73270": { "name": "Sphene", "era": 5, "note": "named" }, "73271": { "name": "Otis" } },
                  "aliases": { "PAPARIMO": { "name": "Papalymo", "note": "spelling" }, "IDA": { "name": "Ida", "note": "itself" } },
                  "blocks": { "Clive": { "icons": [ 87405 ], "note": "other Clive" }, "1001003": { "note": "all" }, "0": { "note": "bad" } },
                  "pins": { "1001004": { "icon": 73001, "source": "BattleTalk", "note": "pinned" }, "1001005": { "icon": 73001, "source": "None", "note": "bad" } },
                  "deliveryKeys": { "61661": { "seed": [ 190, 225 ], "note": "seed" }, "61662": { "seed": [ 900, 225 ], "note": "outside" }, "73001": { "seed": [ 1, 1 ], "note": "not delivery" } }
                }
                """);
            var warnings = new List<string>();
            var curation = PortraitCuration.Load(path, warnings);

            Assert.Equal(PortraitCrop.FromBox(PortraitSource.BattleTalk, 164, 154, 172), curation.Crops[PortraitSource.BattleTalk]);
            Assert.Equal([72659u, 87019u], curation.IconCrops.Keys.Order());
            Assert.Equal(PortraitCrop.FromBox(PortraitSource.TripleTriadCard, 70, 62, 75), curation.IconCrops[87019]);
            Assert.Equal(73270u, Assert.Single(curation.Faces.Keys));
            Assert.Equal((byte)5, curation.Faces[73270].Era);
            Assert.Equal("Papalymo", Assert.Single(curation.Aliases).Value.Name);
            Assert.Equal(2, curation.Blocks.Count);
            Assert.Equal(1001004u, Assert.Single(curation.Pins.Keys));
            Assert.Equal((190, 225), (curation.DeliveryKeys[61661].SeedX, curation.DeliveryKeys[61661].SeedY));
            Assert.Single(curation.DeliveryKeys);
            Assert.Empty(curation.Masks);

            // One warning per skipped entry: the unknown family, the note-less and family-less crops, the note-less face,
            // the self alias, the zero block, the None pin, the seed outside the texture and the non-delivery seed.
            Assert.Equal(9, warnings.Count);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_mask_round_trips_through_its_png_and_hash()
    {
        const int Width = 13, Height = 7;
        var keep = Enumerable.Range(0, Width * Height).Select(i => (i * 7 % 5) != 0).ToArray();
        var bytes = PortraitMaskFile.Encode(keep, Width, Height);
        Assert.True(PortraitMaskFile.TryDecode(bytes, out var width, out var height, out var decoded));
        Assert.Equal((Width, Height), (width, height));
        Assert.Equal(keep, decoded);
        Assert.False(PortraitMaskFile.TryDecode(bytes.AsSpan(0, 20), out _, out _, out _));

        var mask = new PortraitMask(61661, "x.png", "t", PortraitMask.HashOf([1, 2, 3]), Width, Height);
        Assert.Equal(64, mask.Sha256.Length);
        Assert.True(mask.Matches([1, 2, 3]));
        Assert.False(mask.Matches([1, 2, 4]));
    }

    private static PortraitInputs Inputs(IReadOnlyList<PortraitFace> faces) => new(
        faces,
        [
            new PortraitGiver(Alphinaud, "Alphinaud", 2, 0),
            new PortraitGiver(Tataru, "Tataru", 3, 1),
            new PortraitGiver(Adventurer, "troubled adventurer", 3, 1),
            new PortraitGiver(Moogle, "moogle", 0, 0),
            new PortraitGiver(Gerolt, "Gerolt", 5, 0),
            new PortraitGiver(Munavanu, "Munavanu", 0, 0),
            new PortraitGiver(Clive, "Clive", 1, 0),
        ],
        [
            new PortraitQuest(65001, Alphinaud, 0, 0),
            new PortraitQuest(70001, Alphinaud, 3, 0),
            new PortraitQuest(70100, Munavanu, 1, 6),
            new PortraitQuest(65002, Moogle, 1, 0),
        ],
        new Dictionary<byte, uint> { [6] = 65036 });

    private sealed class BoxComparer : IEqualityComparer<(float, float, float)>
    {
        public bool Equals((float, float, float) x, (float, float, float) y) =>
            Math.Abs(x.Item1 - y.Item1) < 0.01f && Math.Abs(x.Item2 - y.Item2) < 0.01f && Math.Abs(x.Item3 - y.Item3) < 0.01f;

        public int GetHashCode((float, float, float) obj) => 0;
    }
}
