using System.Numerics;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unique;
using Tsukimichi.Core.Unlocks;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Unlocks;

/// <summary>
/// The duty icon chain and nameless duties (owner point 9, UI-5b), without game files: each step of
/// <see cref="DutyArt"/> in order, the name from the duty, its territory or PvP, the <see cref="DutyIcons"/> lookup, a
/// duty row that never falls to the stand-in while the game has something, and a non-square emblem drawn whole.
/// </summary>
public class DutyArtTests
{
    private const uint Emblem = 80121;
    private const uint TrialsTile = 61804;
    private const uint TrialsMark = 60834;
    private const uint GenreIcon = 61804;
    private const uint PvpTile = 61806;
    private const uint DutyFinderMenu = 46;

    [Fact]
    public void The_chain_takes_the_first_icon_the_game_has()
    {
        Assert.Equal((Emblem, DutyArtStep.Duty), DutyArt.Resolve(new DutyArtSources(Emblem, TrialsTile, TrialsMark, GenreIcon, false), PvpTile, DutyFinderMenu));
        Assert.Equal((TrialsTile, DutyArtStep.ContentType), DutyArt.Resolve(new DutyArtSources(0, TrialsTile, TrialsMark, GenreIcon, true), PvpTile, DutyFinderMenu));
        Assert.Equal((TrialsMark, DutyArtStep.ContentTypeDutyFinder), DutyArt.Resolve(new DutyArtSources(0, 0, TrialsMark, GenreIcon, true), PvpTile, DutyFinderMenu));
        Assert.Equal((GenreIcon, DutyArtStep.Genre), DutyArt.Resolve(new DutyArtSources(0, 0, 0, GenreIcon, true), PvpTile, DutyFinderMenu));
        Assert.Equal((PvpTile, DutyArtStep.Pvp), DutyArt.Resolve(new DutyArtSources(0, 0, 0, 0, true), PvpTile, DutyFinderMenu));
        Assert.Equal((DutyFinderMenu, DutyArtStep.DutyFinder), DutyArt.Resolve(new DutyArtSources(0, 0, 0, 0, false), PvpTile, DutyFinderMenu));

        // A PvP instance whose PvP tile was not read goes on to the menu; with nothing read at all, the stand-in.
        Assert.Equal((DutyFinderMenu, DutyArtStep.DutyFinder), DutyArt.Resolve(new DutyArtSources(0, 0, 0, 0, true), 0, DutyFinderMenu));
        Assert.Equal((0u, DutyArtStep.None), DutyArt.Resolve(default, 0, 0));
        Assert.Equal(TrialsTile, DutyArt.Icon(new DutyArtSources(0, TrialsTile, 0, 0, false), PvpTile, DutyFinderMenu));
    }

    [Fact]
    public void A_nameless_duty_is_named_for_its_territory_else_for_PvP()
    {
        Assert.Equal("the Great Hunt", DutyArt.Name("the Great Hunt", "The Great Hunt", false, "PvP"));
        Assert.Equal("The Palaistra", DutyArt.Name(string.Empty, " The Palaistra ", true, "PvP"));
        Assert.Equal("PvP", DutyArt.Name(null, null, true, "PvP"));
        Assert.Equal("PvP", DutyArt.Name("  ", string.Empty, true, "PvP"));

        // Neither a name, a territory nor PvP: no name, and the row stays out.
        Assert.Equal(string.Empty, DutyArt.Name(null, null, false, "PvP"));
        Assert.Equal(string.Empty, DutyArt.Name(null, null, true, null));
    }

    [Fact]
    public void The_lookup_answers_by_entry_and_by_instance_and_falls_back_to_the_Duty_Finder()
    {
        var icons = new DutyIcons(
            new Dictionary<uint, uint> { [474] = Emblem, [238] = 61801, [0] = 1, [9] = 0 },
            new Dictionary<uint, uint> { [62] = 61801 },
            DutyFinderMenu);

        Assert.Equal(2, icons.Count);
        Assert.Equal(Emblem, icons.For(474));
        Assert.Equal(61801u, icons.ForInstance(62));
        Assert.Equal(DutyFinderMenu, icons.For(9));
        Assert.Equal(DutyFinderMenu, icons.For(12_345));
        Assert.Equal(DutyFinderMenu, icons.ForInstance(40_001));
        Assert.Equal(0u, DutyIcons.Empty.For(474));
    }

    [Fact]
    public void A_duty_row_with_nothing_of_its_own_wears_the_Duty_Finder_icon()
    {
        const uint Opener = 68_005;
        const uint Unknown = 9_999;
        var catalog = Catalog(Quest(Opener, "Opener") with { Expansion = 2 });
        var rewards = UniqueRewardCatalog.Build(
            new UniqueRewardsData("test", default,
            [
                new UniqueRewardEntry(Opener, RewardKind.DutyUnlock, Unknown, 0, "the Unknown Depths", Confidence.Curated, "curated/duty_unlocks.json"),
                new UniqueRewardEntry(Opener, RewardKind.DutyUnlock, 238, 0, "the Sirensong Sea", Confidence.Curated, "curated/duty_unlocks.json"),
            ]),
            new Dictionary<uint, UniqueOverride>(),
            CuratedData.Empty);
        var duties = PlanDuties.From([new PlanDuty(238, 62, UnlockKind.Dungeon, "the Sirensong Sea")]);
        var links = new UnlockLinks { Duties = [new UnlockDuty(238, 61801, 61, 2)], DutyFinderIcon = DutyFinderMenu };

        var entries = QuestUnlocks.Build(catalog, rewards, duties, links).For(Opener);
        Assert.Equal(DutyFinderMenu, Assert.Single(entries, e => e.Name == "The Unknown Depths").Icon);
        Assert.Equal(61801u, Assert.Single(entries, e => e.Name == "The Sirensong Sea").Icon);

        // Without the menu icon (the sheet unread) the row keeps the stand-in.
        var bare = QuestUnlocks.Build(catalog, rewards, duties, links with { DutyFinderIcon = 0 }).For(Opener);
        Assert.Equal(0u, Assert.Single(bare, e => e.Name == "The Unknown Depths").Icon);
    }

    [Fact]
    public void A_square_icon_fills_its_box_and_an_emblem_is_drawn_whole_and_centred()
    {
        var min = new Vector2(10f, 20f);
        var max = new Vector2(42f, 52f);
        Assert.Equal((min, max), IconFit.Contain(min, max, 40f, 40f));
        Assert.Equal((min, max), IconFit.Contain(min, max, 0f, 168f));

        // The 136 x 168 emblem in a 32 px box: 32 high, 25.9 wide, centred across.
        var (fitMin, fitMax) = IconFit.Contain(min, max, 136f, 168f);
        Assert.Equal(20f, fitMin.Y, 3);
        Assert.Equal(52f, fitMax.Y, 3);
        Assert.Equal(32f * 136f / 168f, fitMax.X - fitMin.X, 3);
        Assert.Equal(26f, (fitMin.X + fitMax.X) * 0.5f, 3);

        // A wide texture in a square box: full width, centred down.
        var (wideMin, wideMax) = IconFit.Contain(min, max, 64f, 32f);
        Assert.Equal(new Vector2(10f, 28f), wideMin);
        Assert.Equal(new Vector2(42f, 44f), wideMax);
    }
}
