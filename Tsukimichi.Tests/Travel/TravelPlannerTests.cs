using Tsukimichi.Core.Travel;

namespace Tsukimichi.Tests.Travel;

public sealed class TravelPlannerTests
{
    private const uint Field = 148;
    private const uint NewGridania = 132;
    private const uint OldGridania = 133;

    private static readonly TravelNode Bentbranch = new(3, Field, 0f, 0f);
    private static readonly TravelNode Hawthorne = new(4, Field, 300f, -200f);
    private static readonly TravelNode GridaniaPlaza = new(2, NewGridania, 35f, 28f, Group: 2);

    private static readonly TravelNode ArchersGuild = new(25, NewGridania, 166f, 88f, Group: 2);
    private static readonly TravelNode ConjurersGuild = new(28, OldGridania, -145f, -12f, Group: 2);
    private static readonly TravelNode LancersGuild = new(27, OldGridania, 117f, -231f, Group: 2);
    private static readonly TravelNode OtherCity = new(41, OldGridania, -140f, -10f, Group: 1);

    private static readonly TravelNode[] GridaniaShards = [ArchersGuild, ConjurersGuild, LancersGuild, OtherCity];

    private static bool All(uint id) => true;

    // ------------------------------------------------------------------ nearest attuned

    [Fact]
    public void Nearest_attuned_picks_the_closest_attuned_aetheryte()
    {
        TravelNode[] field = [Bentbranch, Hawthorne];

        Assert.Equal(Hawthorne, TravelPlanner.NearestAttuned(field, null, 250f, -150f, All));
        Assert.Equal(Bentbranch, TravelPlanner.NearestAttuned(field, null, 10f, 10f, All));
    }

    [Fact]
    public void Nearest_attuned_skips_an_aetheryte_the_player_has_not_attuned()
    {
        TravelNode[] field = [Bentbranch, Hawthorne];

        // Hawthorne is nearer the giver but not attuned: Bentbranch it is, however far.
        Assert.Equal(Bentbranch, TravelPlanner.NearestAttuned(field, null, 290f, -190f, id => id != Hawthorne.RowId));
        Assert.Null(TravelPlanner.NearestAttuned(field, null, 290f, -190f, _ => false));
    }

    [Fact]
    public void Nearest_attuned_falls_back_to_the_zones_own_aetheryte_only_when_attuned()
    {
        // Old Gridania holds no aetheryte; its TerritoryType row names New Gridania's.
        Assert.Equal(GridaniaPlaza, TravelPlanner.NearestAttuned([], GridaniaPlaza, -140f, -10f, All));
        Assert.Null(TravelPlanner.NearestAttuned([], GridaniaPlaza, -140f, -10f, _ => false));
        Assert.Null(TravelPlanner.NearestAttuned([], null, 0f, 0f, All));
    }

    [Fact]
    public void Nearest_attuned_prefers_an_aetheryte_in_the_zone_over_the_fallback()
    {
        Assert.Equal(Bentbranch, TravelPlanner.NearestAttuned([Bentbranch], GridaniaPlaza, 0f, 0f, All));
        Assert.Equal(GridaniaPlaza, TravelPlanner.NearestAttuned([Bentbranch], GridaniaPlaza, 0f, 0f, id => id == GridaniaPlaza.RowId));
    }

    // ------------------------------------------------------------------ already here

    [Fact]
    public void Already_here_needs_the_giver_zone()
    {
        Assert.False(TravelPlanner.IsAlreadyHere(NewGridania, 0f, 0f, Field, 1f, 1f, Bentbranch));
        Assert.False(TravelPlanner.IsAlreadyHere(0, 0f, 0f, 0, 1f, 1f, null));
    }

    [Fact]
    public void Already_here_compares_the_player_with_the_aetheryte()
    {
        // The giver stands at (100, 0); Bentbranch at (0, 0) is 100 away.
        Assert.True(TravelPlanner.IsAlreadyHere(Field, 90f, 0f, Field, 100f, 0f, Bentbranch));
        Assert.False(TravelPlanner.IsAlreadyHere(Field, -50f, 0f, Field, 100f, 0f, Bentbranch));

        // Exactly as far: the teleport is no better, but not worse either; Teleport keeps its emphasis.
        Assert.False(TravelPlanner.IsAlreadyHere(Field, 200f, 0f, Field, 100f, 0f, Bentbranch));
    }

    [Fact]
    public void Already_here_when_no_teleport_lands_in_the_zone()
    {
        // In Old Gridania with the giver: the only teleport lands in New Gridania.
        Assert.True(TravelPlanner.IsAlreadyHere(OldGridania, 500f, 500f, OldGridania, 0f, 0f, GridaniaPlaza));
        Assert.True(TravelPlanner.IsAlreadyHere(OldGridania, 500f, 500f, OldGridania, 0f, 0f, null));
    }

    // ------------------------------------------------------------------ shard choice

    [Fact]
    public void Shard_choice_needs_a_city_network()
    {
        Assert.Null(TravelPlanner.ChooseShard(Bentbranch, GridaniaShards, Field, 0f, 0f, All));
    }

    [Fact]
    public void Shard_choice_takes_the_attuned_shard_nearest_the_giver_in_the_giver_zone()
    {
        // A giver by the Conjurers' Guild in Old Gridania: the aetheryte is in New Gridania, so any shard there wins.
        Assert.Equal(ConjurersGuild, TravelPlanner.ChooseShard(GridaniaPlaza, GridaniaShards, OldGridania, -150f, -15f, All));

        // Not attuned to it: the next one in Old Gridania.
        Assert.Equal(LancersGuild, TravelPlanner.ChooseShard(GridaniaPlaza, GridaniaShards, OldGridania, -150f, -15f, id => id != ConjurersGuild.RowId));

        // None attuned there: no hop.
        Assert.Null(TravelPlanner.ChooseShard(GridaniaPlaza, GridaniaShards, OldGridania, -150f, -15f, id => id == ArchersGuild.RowId));
    }

    [Fact]
    public void Shard_choice_ignores_other_networks_and_the_arrival_itself()
    {
        // OtherCity (group 1) is the nearest node to the giver, but it is not in Gridania's network.
        var chosen = TravelPlanner.ChooseShard(GridaniaPlaza, GridaniaShards, OldGridania, -140f, -10f, All);
        Assert.Equal(ConjurersGuild, chosen);

        // Standing on a shard: that shard is never "the hop".
        Assert.Null(TravelPlanner.ChooseShard(ConjurersGuild, [ConjurersGuild], OldGridania, -140f, -10f, All));
    }

    [Fact]
    public void Shard_choice_in_the_aetheryte_zone_needs_to_beat_it_by_the_margin()
    {
        // A giver at the Archers' Guild (166, 88): the plaza (35, 28) is ~144 away, the shard 0; the hop pays.
        Assert.Equal(ArchersGuild, TravelPlanner.ChooseShard(GridaniaPlaza, GridaniaShards, NewGridania, 166f, 88f, All));

        // A giver halfway: the shard is nearer, but by less than the margin; no hop.
        Assert.Null(TravelPlanner.ChooseShard(GridaniaPlaza, GridaniaShards, NewGridania, 100f, 58f, All));

        // A giver by the plaza: no hop.
        Assert.Null(TravelPlanner.ChooseShard(GridaniaPlaza, GridaniaShards, NewGridania, 40f, 30f, All));
    }

    [Fact]
    public void Distance_is_on_the_ground_plane()
    {
        Assert.Equal(5f, TravelPlanner.Distance(0f, 0f, 3f, 4f));
        Assert.Equal(0f, TravelPlanner.Distance(7f, -2f, 7f, -2f));
    }

    // ------------------------------------------------------------------ special zones

    [Theory]
    [InlineData(886u, TravelSpecial.Firmament)]
    [InlineData(1055u, TravelSpecial.IslandSanctuary)]
    [InlineData(1252u, TravelSpecial.OccultCrescent)]
    [InlineData(1237u, TravelSpecial.CosmicExploration)]
    [InlineData(1291u, TravelSpecial.CosmicExploration)]
    [InlineData(1310u, TravelSpecial.CosmicExploration)]
    [InlineData(132u, TravelSpecial.None)]
    [InlineData(0u, TravelSpecial.None)]
    public void Special_zones_are_classified(uint territory, TravelSpecial expected)
    {
        Assert.Equal(expected, TravelSpecials.Classify(territory));
    }

    [Fact]
    public void Only_conversation_zones_run_a_lifestream_command()
    {
        Assert.Equal("island", TravelSpecials.LifestreamCommand(TravelSpecial.IslandSanctuary));
        Assert.Equal("occult", TravelSpecials.LifestreamCommand(TravelSpecial.OccultCrescent));
        Assert.Null(TravelSpecials.LifestreamCommand(TravelSpecial.Firmament));
        Assert.Null(TravelSpecials.LifestreamCommand(TravelSpecial.CosmicExploration));
        Assert.Null(TravelSpecials.LifestreamCommand(TravelSpecial.None));

        Assert.True(TravelSpecials.NeedsConversation(TravelSpecial.IslandSanctuary));
        Assert.True(TravelSpecials.NeedsConversation(TravelSpecial.OccultCrescent));
        Assert.False(TravelSpecials.NeedsConversation(TravelSpecial.Firmament));
        Assert.False(TravelSpecials.NeedsConversation(TravelSpecial.CosmicExploration));
    }

    // ------------------------------------------------------------------ arrival choice and goals (travel review, 1.10)

    private const uint Thavnair = 957;
    private const uint ZerosDomain = 1077;
    private static readonly TravelNode Yedlihmad = new(169, Thavnair, 196f, 628f);
    private static readonly TravelNode GreatWork = new(170, Thavnair, -526f, 36f);
    private static readonly TravelNode PalakasStand = new(171, Thavnair, 404f, -247f);
    private static readonly TravelNode[] ThavnairAetherytes = [Yedlihmad, GreatWork, PalakasStand];

    [Fact]
    public void Arrival_reports_the_nearest_and_says_when_it_is_not_attuned()
    {
        // The way into the Void stands at (-270, 606): Yedlihmad (about 467 away) beats the Great Work (about 625).
        var all = TravelPlanner.ChooseArrival(ThavnairAetherytes, Yedlihmad, -270f, 606f, All);
        Assert.Equal(Yedlihmad, all.Nearest);
        Assert.Equal(Yedlihmad, all.Target);
        Assert.False(all.Substituted);

        var withoutYedlihmad = TravelPlanner.ChooseArrival(ThavnairAetherytes, Yedlihmad, -270f, 606f, id => id != Yedlihmad.RowId);
        Assert.Equal(Yedlihmad, withoutYedlihmad.Nearest);
        Assert.Equal(GreatWork, withoutYedlihmad.Target);
        Assert.True(withoutYedlihmad.Substituted);

        var none = TravelPlanner.ChooseArrival(ThavnairAetherytes, Yedlihmad, -270f, 606f, _ => false);
        Assert.Equal(Yedlihmad, none.Nearest);
        Assert.Null(none.Target);
        Assert.False(none.Substituted);
    }

    [Fact]
    public void The_zones_own_aetheryte_wins_a_tie()
    {
        TravelNode a = new(1, Field, 0f, 0f);
        TravelNode b = new(2, Field, 8f, 0f);

        // (4.5, 0): a is 4.5 away, b 3.5; within the tie margin the preferred one wins, either way round.
        Assert.Equal(a, TravelPlanner.ChooseArrival([a, b], a, 4.5f, 0f, All).Target);
        Assert.Equal(a, TravelPlanner.ChooseArrival([b, a], a, 4.5f, 0f, All).Target);
        Assert.Equal(b, TravelPlanner.ChooseArrival([a, b], b, 3.5f, 0f, All).Target);

        // Beyond the margin the nearer one wins whatever the zone names.
        Assert.Equal(b, TravelPlanner.ChooseArrival([a, b], a, 8f + TravelPlanner.TieMargin, 0f, All).Target);
        Assert.Equal(b, TravelPlanner.ChooseArrival([a, b], null, 4.5f, 0f, All).Target);
    }

    [Fact]
    public void Without_a_position_the_zones_own_aetheryte_is_the_reference()
    {
        // A door the data does not place: the aetheryte the zone names, and its neighbour when it is not attuned.
        var choice = TravelPlanner.ChooseArrival(ThavnairAetherytes, Yedlihmad, null, null, All);
        Assert.Equal(Yedlihmad, choice.Target);

        var substitute = TravelPlanner.ChooseArrival(ThavnairAetherytes, Yedlihmad, null, null, id => id != Yedlihmad.RowId);
        Assert.Equal(PalakasStand, substitute.Target);
        Assert.True(substitute.Substituted);

        // The zone's own aetheryte stands elsewhere and the territory has none: it is the fallback (a city sub-zone).
        Assert.Equal(GridaniaPlaza, TravelPlanner.ChooseArrival([], GridaniaPlaza, null, null, All).Target);
        Assert.Null(TravelPlanner.ChooseArrival([], null, null, null, All).Nearest);
    }

    [Fact]
    public void The_goal_is_the_way_in_while_outside_and_the_giver_inside()
    {
        var giver = new TravelPlace(ZerosDomain, -27f, 0f, 50f);
        var door = new InteriorEntrance(ZerosDomain, Thavnair, -270f, 0f, 606f, true);

        var outside = TravelPlanner.Goal(giver, door, playerTerritory: 132);
        Assert.True(outside.AtEntrance);
        Assert.True(outside.Placed);
        Assert.Equal(new TravelPlace(Thavnair, -270f, 0f, 606f), outside.Place);

        // In Thavnair itself the goal is still the door; inside the Void it is the giver.
        Assert.True(TravelPlanner.Goal(giver, door, Thavnair).AtEntrance);
        var inside = TravelPlanner.Goal(giver, door, ZerosDomain);
        Assert.False(inside.AtEntrance);
        Assert.Equal(giver, inside.Place);

        // An unplaced door keeps its outside territory but no spot; another interior's door does not apply.
        var unplaced = TravelPlanner.Goal(giver, door with { Placed = false }, 0);
        Assert.False(unplaced.Placed);
        Assert.Equal(Thavnair, unplaced.Place.TerritoryId);
        Assert.False(TravelPlanner.Goal(giver, door with { Interior = 212 }, 0).AtEntrance);
        Assert.False(TravelPlanner.Goal(giver, null, 0).AtEntrance);
    }

    [Fact]
    public void Already_here_measures_against_the_goal_not_the_giver_inside()
    {
        // Standing in Thavnair 50 from the door, Yedlihmad 467 from it: walking is quicker than teleporting.
        Assert.True(TravelPlanner.IsAlreadyHere(Thavnair, -250f, 580f, Thavnair, -270f, 606f, Yedlihmad));
        Assert.False(TravelPlanner.IsAlreadyHere(Thavnair, 400f, -200f, Thavnair, -270f, 606f, Yedlihmad));
    }
}
