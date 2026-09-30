using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Runtime;

public sealed class AbandonedLedgerTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = new(2026, 9, 22, 8, 0, 0, DateTimeKind.Utc);

    private static readonly EvalContext Context = EvalContext.Default;

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private static ushort Id(uint rowId) => QuestRecord.ToQuestId(rowId);

    private static QuestCatalog Catalog() => Fixture.Catalog(
        Fixture.Quest(Fixture.A) with { StepCount = 5 },
        Fixture.Quest(Fixture.B) with { StepCount = 2 },
        Fixture.Quest(Fixture.C));

    private static CharacterSnapshot Journal(uint[] completed, params (uint RowId, byte Sequence)[] accepted) =>
        Fixture.Snapshot(completed) with { Accepted = accepted.Select(a => Fixture.Accepted(a.RowId, a.Sequence)).ToArray() };

    /// <summary>One poll the way the poller runs it: diff, resolve, derive events, apply them to the ledger.</summary>
    private static bool Poll(Dictionary<ushort, AbandonedEntry> ledger, QuestCatalog catalog, CharacterSnapshot old, CharacterSnapshot @new, DateTime now)
    {
        var diff = SnapshotDiff.Compute(old, @new);
        var oldStates = StateResolver.ResolveAll(catalog, old, Context);
        var newStates = StateResolver.ResolveAll(catalog, @new, Context);
        var events = QuestEvents.Derive(diff, old, @new, catalog, oldStates, newStates, now);
        return AbandonedLedger.Apply(ledger, events, old, catalog);
    }

    [Fact]
    public void An_abandoned_quest_is_recorded_with_the_step_it_reached_and_its_step_count()
    {
        var catalog = Catalog();
        var ledger = new Dictionary<ushort, AbandonedEntry>();
        var old = Journal([], (Fixture.A, 3), (Fixture.B, 1));
        var @new = Journal([], (Fixture.B, 1));

        Assert.True(Poll(ledger, catalog, old, @new, T0));

        var entry = Assert.Single(ledger.Values);
        Assert.Equal(new AbandonedEntry(Id(Fixture.A), T0, 3, 5), entry);
        Assert.Equal(Fixture.A, entry.RowId);
        Assert.Equal("step 3 of 5", entry.StepText);
        Assert.Equal("step 3 of 5 · 2 days ago", AbandonedLedger.Describe(entry, T0.AddDays(2).AddHours(3)));
    }

    [Fact]
    public void The_last_step_reads_as_the_step_count_and_an_unknown_count_as_the_bare_step()
    {
        var catalog = Catalog();
        var ledger = new Dictionary<ushort, AbandonedEntry>();
        var old = Journal([], (Fixture.B, BlockerText.FinalSequence), (Fixture.C, 4));

        Assert.True(Poll(ledger, catalog, old, Journal([]), T0));

        Assert.Equal("step 2 of 2", ledger[Id(Fixture.B)].StepText);
        Assert.Equal("step 4", ledger[Id(Fixture.C)].StepText);
        Assert.Equal("just now", AbandonedLedger.Describe(new AbandonedEntry(Id(Fixture.C), T0, 0, 0), T0));
    }

    [Fact]
    public void A_completed_quest_leaving_the_journal_is_not_recorded()
    {
        var catalog = Catalog();
        var ledger = new Dictionary<ushort, AbandonedEntry>();
        var old = Journal([], (Fixture.A, BlockerText.FinalSequence));
        var @new = Journal([Fixture.A]);

        Assert.False(Poll(ledger, catalog, old, @new, T0));
        Assert.Empty(ledger);
    }

    [Fact]
    public void Accepting_the_quest_again_clears_its_entry()
    {
        var catalog = Catalog();
        var ledger = new Dictionary<ushort, AbandonedEntry>();
        var withA = Journal([], (Fixture.A, 3));
        var without = Journal([]);
        Poll(ledger, catalog, withA, without, T0);
        Assert.Single(ledger);

        Assert.True(Poll(ledger, catalog, without, Journal([], (Fixture.A, 1)), T1));

        Assert.Empty(ledger);
    }

    [Fact]
    public void Completing_the_quest_clears_its_entry()
    {
        var catalog = Catalog();
        var ledger = new Dictionary<ushort, AbandonedEntry>();
        var withA = Journal([], (Fixture.A, 3));
        var without = Journal([]);
        Poll(ledger, catalog, withA, without, T0);

        // Picked up and turned in between two polls: the completion bit alone clears it.
        Assert.True(Poll(ledger, catalog, without, Journal([Fixture.A]), T1));

        Assert.Empty(ledger);
    }

    [Fact]
    public void Abandoning_again_replaces_the_entry_with_the_new_time_and_step()
    {
        var catalog = Catalog();
        var ledger = new Dictionary<ushort, AbandonedEntry>();
        Poll(ledger, catalog, Journal([], (Fixture.A, 2)), Journal([]), T0);
        var entry = new AbandonedEntry(Id(Fixture.A), T1, 4, 5);

        AbandonedLedger.Apply(ledger, [new QuestEvent(QuestEventKind.Abandoned, Fixture.A, T1)], Journal([], (Fixture.A, 4)), catalog);

        Assert.Equal(entry, Assert.Single(ledger.Values));
    }

    [Fact]
    public void Reconcile_drops_quests_taken_up_again_or_completed_while_unwatched()
    {
        var ledger = new Dictionary<ushort, AbandonedEntry>
        {
            [Id(Fixture.A)] = new(Id(Fixture.A), T0, 3, 5),
            [Id(Fixture.B)] = new(Id(Fixture.B), T0, 1, 2),
            [Id(Fixture.C)] = new(Id(Fixture.C), T0, 1, 0),
        };

        Assert.True(AbandonedLedger.Reconcile(ledger, Journal([Fixture.B], (Fixture.A, 1))));

        Assert.Equal([Id(Fixture.C)], ledger.Keys);
        Assert.False(AbandonedLedger.Reconcile(ledger, Journal([])));
    }

    [Fact]
    public void Save_then_Load_round_trips_the_entries_newest_first()
    {
        var path = AbandonedLedger.PathFor(tmp.File("characters"), 7);
        var ledger = new Dictionary<ushort, AbandonedEntry>
        {
            [Id(Fixture.A)] = new(Id(Fixture.A), T0, 3, 5),
            [Id(Fixture.B)] = new(Id(Fixture.B), T1, BlockerText.FinalSequence, 2),
        };

        AbandonedLedger.Save(path, ledger);
        var back = AbandonedLedger.Load(path);

        Assert.Equal(ledger, back);
        Assert.All(back.Values, e => Assert.Equal(DateTimeKind.Utc, e.AbandonedUtc.Kind));
        Assert.Equal([Id(Fixture.B), Id(Fixture.A)], AbandonedLedger.Newest(back).Select(e => e.QuestId));
        Assert.False(File.Exists(path + ".tmp"));

        var text = File.ReadAllText(path);
        Assert.Contains("\"questId\"", text);
        Assert.Contains("\"abandonedUtc\"", text);
        Assert.Contains("\"stepCount\"", text);
        // Derived properties stay out of the file.
        Assert.DoesNotContain("\"rowId\"", text);
        Assert.DoesNotContain("\"stepText\"", text);
    }

    [Fact]
    public void A_ledger_written_with_the_derived_properties_still_loads()
    {
        // Files written before RowId and StepText were ignored carry both; they are read past, not trusted.
        var path = tmp.File("old.abandoned.json");
        File.WriteAllText(path, """
            {
              "version": 1,
              "entries": [
                { "questId": 1, "abandonedUtc": "2026-09-20T08:00:00Z", "sequence": 3, "stepCount": 5, "rowId": 99, "stepText": "step 9 of 9" }
              ]
            }
            """);

        var warnings = new List<string>();
        var back = AbandonedLedger.Load(path, warnings);

        Assert.Empty(warnings);
        var entry = Assert.Single(back.Values);
        Assert.Equal(new AbandonedEntry(1, T0, 3, 5), entry);
        Assert.Equal(0x10001u, entry.RowId);
        Assert.Equal(BlockerText.StepText(3, 5), entry.StepText);
    }

    [Fact]
    public void Missing_file_loads_empty_and_a_corrupt_one_loads_empty_with_a_warning()
    {
        var warnings = new List<string>();
        Assert.Empty(AbandonedLedger.Load(tmp.File("none.abandoned.json"), warnings));
        Assert.Empty(warnings);

        var bad = tmp.File("bad.abandoned.json");
        File.WriteAllText(bad, "{ not json");
        Assert.Empty(AbandonedLedger.Load(bad, warnings));
        Assert.Single(warnings);
        Assert.True(File.Exists(bad));
    }

    [Fact]
    public void Sidecar_sits_beside_the_snapshot_and_is_deleted_with_its_character()
    {
        var dir = tmp.File("characters");
        Directory.CreateDirectory(dir);
        const ulong forgotten = 0x0040_0000_0123_4567UL;
        const ulong kept = 42;
        var entry = new Dictionary<ushort, AbandonedEntry> { [Id(Fixture.A)] = new(Id(Fixture.A), T0, 3, 5) };

        var path = AbandonedLedger.PathFor(dir, forgotten);
        Assert.Equal(Path.Combine(dir, forgotten.ToString(CultureInfo.InvariantCulture) + ".abandoned.json"), path);

        AbandonedLedger.Save(path, entry);
        AbandonedLedger.Save(AbandonedLedger.PathFor(dir, kept), entry);
        AcceptedSince.Save(AcceptedSince.PathFor(dir, forgotten), new Dictionary<ushort, DateTime> { [Id(Fixture.A)] = T0 });
        File.WriteAllText(Path.Combine(dir, forgotten.ToString(CultureInfo.InvariantCulture) + ".json"), "{}");

        // Forget one character: every sidecar path it has.
        Assert.Contains(path, CharacterSidecars.PathsFor(dir, forgotten));
        foreach (var sidecar in CharacterSidecars.PathsFor(dir, forgotten))
        {
            File.Delete(sidecar);
        }

        Assert.False(File.Exists(path));
        Assert.Empty(AbandonedLedger.Load(path));
        Assert.True(File.Exists(AbandonedLedger.PathFor(dir, kept)));

        // Delete all data: every sidecar of every character, never the snapshot files themselves.
        AbandonedLedger.Save(path, entry);
        var all = CharacterSidecars.FindAll(dir);
        Assert.Equal(2, all.Count);
        Assert.All(all, f => Assert.EndsWith(AbandonedLedger.FileSuffix, f));
        Assert.DoesNotContain(all, f => f.EndsWith(forgotten.ToString(CultureInfo.InvariantCulture) + ".json", StringComparison.Ordinal));
        Assert.Empty(CharacterSidecars.FindAll(tmp.File("missing")));
    }

    [Fact]
    public void Ages_read_in_minutes_hours_and_days()
    {
        Assert.Equal("just now", AbandonedLedger.AgeText(TimeSpan.FromSeconds(-5)));
        Assert.Equal("5 min ago", AbandonedLedger.AgeText(TimeSpan.FromMinutes(5.5)));
        Assert.Equal("3 h ago", AbandonedLedger.AgeText(TimeSpan.FromHours(3.2)));
        Assert.Equal("1 day ago", AbandonedLedger.AgeText(TimeSpan.FromHours(30)));
        Assert.Equal("12 days ago", AbandonedLedger.AgeText(TimeSpan.FromDays(12.9)));
    }

    [Fact]
    public void Poller_memory_resets_the_ledger_and_marks_it_dirty_when_the_live_character_is_forgotten()
    {
        var memory = new PollerMemory(TimeSpan.FromSeconds(10));
        var snapshot = Fixture.Snapshot();
        memory.Commit(snapshot, new Dictionary<uint, QuestEvaluation>(), new object());
        memory.SetAbandoned(new Dictionary<ushort, AbandonedEntry> { [Id(Fixture.A)] = new(Id(Fixture.A), T0, 3, 5) }, dirty: false);

        memory.OnCharacterForgotten(snapshot.ContentId);
        Assert.True(memory.AbandonedDirty);

        memory.OnDataDeleted();
        Assert.Empty(memory.Abandoned);
        Assert.False(memory.AbandonedDirty);
    }
}
