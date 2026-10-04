using Tsukimichi.Core.Travel;

namespace Tsukimichi.Tests.Travel;

public sealed class TravelPreflightTests
{
    /// <summary>A character's content id.</summary>
    private const ulong Alt = 0x0040_0000_1234_5678;

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
    public void First_person_is_information_without_a_fix_and_is_named_when_a_walk_starts()
    {
        var results = TravelPreflight.Evaluate(new TravelPreflightReading(0, true, true, []));
        var camera = Of(results, PreflightItem.Camera);

        Assert.Equal(PreflightState.Info, camera.State);
        Assert.Equal(PreflightFix.None, camera.Fix);
        Assert.Equal(0, TravelPreflight.Warnings(results));
        Assert.Equal([PreflightItem.Camera], TravelPreflight.WalkWarnings(results));
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
        Assert.Equal(PreflightFix.OpenPluginInstaller, row.Fix);
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
        var change = new PreflightChange(PreflightItem.MovementType, TravelPreflight.LegacyMoveMode, TravelPreflight.StandardMoveMode, Alt);

        Assert.True(change.CanUndo(TravelPreflight.StandardMoveMode, Alt));

        // The player changed it again in the game's own window, or it cannot be read: hands off.
        Assert.False(change.CanUndo(TravelPreflight.LegacyMoveMode, Alt));
        Assert.False(change.CanUndo(null, Alt));
        Assert.False(new PreflightChange(PreflightItem.MovementType, 0, 0, Alt).CanUndo(0, Alt));

        // vnavmesh's switch as 0 (paused) and 1 (allowed): Undo pauses it again only while it is still allowed.
        var vnav = new PreflightChange(PreflightItem.VnavmeshMovement, 0, 1, Alt);
        Assert.True(vnav.CanUndo(1, Alt));
        Assert.False(vnav.CanUndo(0, Alt));
    }

    [Fact]
    public void Undo_is_only_for_the_character_the_fix_was_made_on()
    {
        // "Restore Legacy" made on one character: another one logged in (whose own setting reads Standard too) gets no
        // Undo, nor does a logged-out reading, nor a change with no character.
        var change = new PreflightChange(PreflightItem.MovementType, TravelPreflight.LegacyMoveMode, TravelPreflight.StandardMoveMode, Alt);
        Assert.False(change.CanUndo(TravelPreflight.StandardMoveMode, Alt + 1));
        Assert.False(change.CanUndo(TravelPreflight.StandardMoveMode, null));
        Assert.False((change with { ContentId = 0 }).CanUndo(TravelPreflight.StandardMoveMode, 0));
        Assert.True(change.CanUndo(TravelPreflight.StandardMoveMode, Alt));
    }
}
