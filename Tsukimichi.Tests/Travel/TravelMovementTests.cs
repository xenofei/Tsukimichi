using Tsukimichi.Core.Travel;

namespace Tsukimichi.Tests.Travel;

public sealed class TravelMovementTests
{
    private static readonly TravelMoveContext Field = new(Mounted: false, MountAllowed: true, FlightUnlocked: true, NoMountZone: false, SprintReady: true);
    private static readonly TravelMoveContext Town = new(Mounted: false, MountAllowed: false, FlightUnlocked: false, NoMountZone: true, SprintReady: true);

    [Fact]
    public void A_walk_longer_than_the_mount_distance_mounts_and_flies_where_unlocked()
    {
        Assert.Equal(new TravelMove(Mount: true, Fly: true, Sprint: false), TravelMovement.Decide(200f, TravelOptions.Default, Field));

        // Flying not unlocked in this zone: ride on the ground.
        Assert.Equal(new TravelMove(true, false, false), TravelMovement.Decide(200f, TravelOptions.Default, Field with { FlightUnlocked = false }));

        // The fly setting off: ride on the ground.
        Assert.Equal(new TravelMove(true, false, false), TravelMovement.Decide(200f, TravelOptions.Default with { Fly = false }, Field));
    }

    [Fact]
    public void A_short_walk_never_mounts_and_the_distance_is_the_setting()
    {
        Assert.Equal(TravelMove.Walk, TravelMovement.Decide(40f, TravelOptions.Default, Field));
        Assert.Equal(TravelMove.Walk, TravelMovement.Decide(10f, TravelOptions.Default, Field));
        Assert.True(TravelMovement.Decide(41f, TravelOptions.Default, Field).Mount);

        var far = TravelOptions.Default with { MountDistance = 120f };
        Assert.False(TravelMovement.Decide(100f, far, Field).Mount);
        Assert.True(TravelMovement.Decide(121f, far, Field).Mount);
    }

    [Fact]
    public void No_mount_where_the_game_would_not_take_it()
    {
        // In combat, a duty, water or a zone without mounts: MountAllowed is false, and a flight needs the mount.
        var blocked = Field with { MountAllowed = false };
        Assert.Equal(TravelMove.Walk, TravelMovement.Decide(200f, TravelOptions.Default, blocked));
    }

    [Fact]
    public void Mounting_off_never_mounts_but_an_existing_mount_still_flies_far()
    {
        var off = TravelOptions.Default with { MountDistance = 0f };
        Assert.False(TravelMovement.Decide(500f, off, Field).Mount);

        var mounted = Field with { Mounted = true };
        Assert.Equal(new TravelMove(false, true, false), TravelMovement.Decide(500f, off, mounted));
        Assert.Equal(new TravelMove(false, false, false), TravelMovement.Decide(TravelOptions.DefaultMountDistance, off, mounted));

        // Already mounted with the default settings: no second summons.
        Assert.Equal(new TravelMove(false, true, false), TravelMovement.Decide(200f, TravelOptions.Default, mounted));
    }

    [Fact]
    public void Towns_sprint_on_foot_when_the_setting_is_on()
    {
        Assert.Equal(new TravelMove(false, false, true), TravelMovement.Decide(60f, TravelOptions.Default, Town));
        Assert.False(TravelMovement.Decide(TravelMovement.MinSprintDistance - 1f, TravelOptions.Default, Town).Sprint);
        Assert.False(TravelMovement.Decide(60f, TravelOptions.Default with { SprintInTowns = false }, Town).Sprint);
        Assert.False(TravelMovement.Decide(60f, TravelOptions.Default, Town with { SprintReady = false }).Sprint);

        // Outside towns Sprint is left alone (the mount is the speed-up there).
        Assert.False(TravelMovement.Decide(30f, TravelOptions.Default, Field).Sprint);
    }

    [Fact]
    public void On_foot_options_and_unknown_distances_just_walk()
    {
        Assert.Equal(TravelMove.Walk, TravelMovement.Decide(500f, TravelOptions.OnFoot, Field));
        Assert.Equal(TravelMove.Walk, TravelMovement.Decide(float.NaN, TravelOptions.Default, Field));
        Assert.Equal(TravelMove.Walk, TravelMovement.Decide(-1f, TravelOptions.Default, Field));
    }
}
