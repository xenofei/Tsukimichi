using Tsukimichi.Core.Travel;

namespace Tsukimichi.Tests.Travel;

public sealed class TravelPreflightTests
{
    private static PreflightResult Of(IReadOnlyList<PreflightResult> results, PreflightItem item) => results.Single(r => r.Item == item);

    [Fact]
    public void Everything_fine_reads_ok_in_order_with_no_fixes()
    {
        var results = TravelPreflight.Evaluate(new TravelPreflightReading(TravelPreflight.StandardMoveMode, false, true, []));

        Assert.Equal([PreflightItem.MovementType, PreflightItem.Camera, PreflightItem.VnavmeshMovement, PreflightItem.Conflicts], results.Select(r => r.Item));
        Assert.All(results, r => Assert.Equal(PreflightState.Ok, r.State));
        Assert.All(results, r => Assert.Equal(PreflightFix.None, r.Fix));
        Assert.Equal(0, TravelPreflight.Warnings(results));
        Assert.Empty(TravelPreflight.WalkWarnings(results));
    }

    [Fact]
    public void Legacy_movement_warns_and_offers_standard()
    {
        var results = TravelPreflight.Evaluate(new TravelPreflightReading(TravelPreflight.LegacyMoveMode, false, true, []));

        var movement = Of(results, PreflightItem.MovementType);
        Assert.Equal(PreflightState.Warn, movement.State);
        Assert.Equal(PreflightFix.StandardMovement, movement.Fix);
        Assert.Equal([PreflightItem.MovementType], TravelPreflight.WalkWarnings(results));
    }

    [Fact]
    public void First_person_warns_without_a_fix()
    {
        var camera = Of(TravelPreflight.Evaluate(new TravelPreflightReading(0, true, true, [])), PreflightItem.Camera);

        Assert.Equal(PreflightState.Warn, camera.State);
        Assert.Equal(PreflightFix.None, camera.Fix);
    }

    [Fact]
    public void Vnavmesh_movement_paused_by_another_plugin_warns_and_offers_to_allow_it()
    {
        var vnav = Of(TravelPreflight.Evaluate(new TravelPreflightReading(0, false, false, [])), PreflightItem.VnavmeshMovement);

        Assert.Equal(PreflightState.Warn, vnav.State);
        Assert.Equal(PreflightFix.AllowVnavmeshMovement, vnav.Fix);
    }

    [Fact]
    public void What_cannot_be_read_is_unread_and_never_warns_or_offers_a_fix()
    {
        var results = TravelPreflight.Evaluate(TravelPreflightReading.Unread);

        Assert.Equal(PreflightState.Unread, Of(results, PreflightItem.MovementType).State);
        Assert.Equal(PreflightState.Unread, Of(results, PreflightItem.Camera).State);
        Assert.Equal(PreflightState.Unread, Of(results, PreflightItem.VnavmeshMovement).State);
        Assert.Equal(PreflightState.Ok, Of(results, PreflightItem.Conflicts).State);
        Assert.All(results, r => Assert.Equal(PreflightFix.None, r.Fix));
        Assert.Empty(TravelPreflight.WalkWarnings(results));
    }

    [Fact]
    public void A_loaded_conflict_warns_in_setup_but_not_at_walk_start()
    {
        var conflicts = TravelPreflight.ConflictsAmong(["Lifestream", "wrongwarpfinder", "vnavmesh"]);
        var results = TravelPreflight.Evaluate(new TravelPreflightReading(0, false, true, conflicts));

        var row = Of(results, PreflightItem.Conflicts);
        Assert.Equal(PreflightState.Warn, row.State);
        Assert.Equal("WrongWarpFinder", Assert.Single(row.Conflicts).InternalName);
        Assert.Equal(1, TravelPreflight.Warnings(results));
        Assert.Empty(TravelPreflight.WalkWarnings(results));
    }

    [Fact]
    public void Only_known_conflicts_count()
    {
        Assert.Empty(TravelPreflight.ConflictsAmong(["Questionable", "TextAdvance", "WrongWarp"]));
        Assert.All(TravelPreflight.KnownConflicts, c => Assert.False(string.IsNullOrWhiteSpace(c.Key)));
    }

    [Fact]
    public void Undo_puts_the_old_value_back_only_while_the_setting_still_reads_what_the_fix_set()
    {
        var change = new PreflightChange(PreflightItem.MovementType, TravelPreflight.LegacyMoveMode, TravelPreflight.StandardMoveMode);

        Assert.True(change.CanUndo(TravelPreflight.StandardMoveMode));

        // The player changed it again in the game's own window, or it cannot be read: hands off.
        Assert.False(change.CanUndo(TravelPreflight.LegacyMoveMode));
        Assert.False(change.CanUndo(null));
        Assert.False(new PreflightChange(PreflightItem.MovementType, 0, 0).CanUndo(0));
    }
}
