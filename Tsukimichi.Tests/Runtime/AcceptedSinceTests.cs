using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Runtime;

public sealed class AcceptedSinceTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private static ushort Id(uint rowId) => QuestRecord.ToQuestId(rowId);

    private static CharacterSnapshot Journal(params (uint RowId, byte Sequence)[] accepted) =>
        Fixture.Snapshot() with { Accepted = accepted.Select(a => Fixture.Accepted(a.RowId, a.Sequence)).ToArray() };

    [Fact]
    public void Sidecar_sits_beside_the_snapshot_file()
    {
        const ulong contentId = 0x0040_0000_0123_4567UL;
        var path = AcceptedSince.PathFor(tmp.File("characters"), contentId);
        Assert.Equal(Path.Combine(tmp.File("characters"), contentId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".accepted.json"), path);
        Assert.EndsWith(AcceptedSince.FileSuffix, path);
    }

    [Fact]
    public void Missing_file_loads_empty_without_a_warning()
    {
        var warnings = new List<string>();
        var since = AcceptedSince.Load(tmp.File("none.accepted.json"), warnings);
        Assert.Empty(since);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Save_then_Load_round_trips_utc_times()
    {
        var path = AcceptedSince.PathFor(tmp.File("characters"), 7);
        var since = new Dictionary<ushort, DateTime> { [Id(Fixture.A)] = T0, [Id(Fixture.B)] = T1 };

        AcceptedSince.Save(path, since);
        var back = AcceptedSince.Load(path);

        Assert.Equal(since, back);
        Assert.All(back.Values, t => Assert.Equal(DateTimeKind.Utc, t.Kind));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Corrupt_file_loads_empty_with_one_warning_and_stays_in_place()
    {
        var path = tmp.File("bad.accepted.json");
        File.WriteAllText(path, "{ not json");
        var warnings = new List<string>();

        var since = AcceptedSince.Load(path, warnings);

        Assert.Empty(since);
        Assert.Single(warnings);
        Assert.True(File.Exists(path));
        Assert.Empty(Directory.GetFiles(tmp.Path, "*.corrupt-*"));
    }

    [Fact]
    public void Wrong_shape_loads_empty_with_a_warning()
    {
        var path = tmp.File("shape.accepted.json");
        File.WriteAllText(path, "[1, 2, 3]");
        var warnings = new List<string>();

        Assert.Empty(AcceptedSince.Load(path, warnings));
        Assert.Single(warnings);
    }

    [Fact]
    public void Timestamps_without_a_zone_read_as_utc()
    {
        var path = tmp.File("kind.accepted.json");
        File.WriteAllText(path, """{ "108": "2026-09-20T08:00:00" }""");

        var since = AcceptedSince.Load(path);

        var time = Assert.Single(since).Value;
        Assert.Equal(T0, time);
        Assert.Equal(DateTimeKind.Utc, time.Kind);
    }

    [Fact]
    public void Reconcile_seeds_new_entries_drops_stale_ones_and_keeps_known_times()
    {
        var since = new Dictionary<ushort, DateTime> { [Id(Fixture.A)] = T0, [Id(Fixture.C)] = T0 };
        var snapshot = Journal((Fixture.A, 1), (Fixture.B, 2));

        Assert.True(AcceptedSince.Reconcile(since, snapshot, T1));

        Assert.Equal(new Dictionary<ushort, DateTime> { [Id(Fixture.A)] = T0, [Id(Fixture.B)] = T1 }, since);
        Assert.False(AcceptedSince.Reconcile(since, snapshot, T1.AddHours(1)));
    }

    [Fact]
    public void Reconcile_of_an_empty_journal_clears_everything()
    {
        var since = new Dictionary<ushort, DateTime> { [Id(Fixture.A)] = T0 };
        Assert.True(AcceptedSince.Reconcile(since, Journal(), T1));
        Assert.Empty(since);
        Assert.False(AcceptedSince.Reconcile(new Dictionary<ushort, DateTime>(), Journal(), T1));
    }

    [Fact]
    public void Apply_stamps_new_journal_entries_and_drops_departed_ones()
    {
        var old = Journal((Fixture.A, 1));
        var @new = Journal((Fixture.B, 1));
        var since = new Dictionary<ushort, DateTime> { [Id(Fixture.A)] = T0 };

        Assert.True(AcceptedSince.Apply(since, old, @new, SnapshotDiff.Compute(old, @new), T1));

        Assert.Equal(new Dictionary<ushort, DateTime> { [Id(Fixture.B)] = T1 }, since);
    }

    [Fact]
    public void Apply_refreshes_the_time_when_the_step_moves_and_keeps_it_otherwise()
    {
        var old = Journal((Fixture.A, 1), (Fixture.B, 3));
        var @new = Journal((Fixture.A, 2), (Fixture.B, 3)) with { CompletedBits = Fixture.Bits(Fixture.B) };
        var since = new Dictionary<ushort, DateTime> { [Id(Fixture.A)] = T0, [Id(Fixture.B)] = T0 };

        // A advanced a step; B only gained a completion bit while staying in the journal at the same step.
        var diff = SnapshotDiff.Compute(old, @new);
        Assert.Contains(Id(Fixture.A), diff.ChangedQuestIds);
        Assert.Contains(Id(Fixture.B), diff.ChangedQuestIds);

        Assert.True(AcceptedSince.Apply(since, old, @new, diff, T1));

        Assert.Equal(T1, since[Id(Fixture.A)]);
        Assert.Equal(T0, since[Id(Fixture.B)]);
    }

    [Fact]
    public void Apply_stamps_an_unchanged_entry_the_map_never_saw()
    {
        // The journal did not move for A, but a completion elsewhere put its id in the diff and the map lacks it.
        var old = Journal((Fixture.A, 1));
        var @new = Journal((Fixture.A, 1)) with { CompletedBits = Fixture.Bits(Fixture.A) };
        var since = new Dictionary<ushort, DateTime>();

        Assert.True(AcceptedSince.Apply(since, old, @new, SnapshotDiff.Compute(old, @new), T1));
        Assert.Equal(T1, since[Id(Fixture.A)]);
    }

    [Fact]
    public void Apply_with_an_empty_diff_changes_nothing()
    {
        var snapshot = Journal((Fixture.A, 1));
        var since = new Dictionary<ushort, DateTime> { [Id(Fixture.A)] = T0 };

        Assert.False(AcceptedSince.Apply(since, snapshot, snapshot, SnapshotDiff.Empty, T1));
        Assert.Equal(T0, since[Id(Fixture.A)]);
    }

    [Fact]
    public void Apply_ignores_changed_ids_that_were_never_in_the_journal()
    {
        var old = Fixture.Snapshot();
        var @new = Fixture.Snapshot(Fixture.D);
        var since = new Dictionary<ushort, DateTime>();

        Assert.False(AcceptedSince.Apply(since, old, @new, SnapshotDiff.Compute(old, @new), T1));
        Assert.Empty(since);
    }
}
