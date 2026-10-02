using System.Globalization;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Multibox;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Storage;

/// <summary>
/// Quest completion dates in their own file, <c>&lt;id&gt;.dates.json</c> (1.5.0): a 1.4 build or a downgrade that
/// rewrites the snapshot cannot drop them, a snapshot without dates never replaces them, a 1.5 preview's inline dates
/// move over, and the first pass learns whether the file was missing or only unreadable.
/// </summary>
public sealed class CompletionDateFileTests : IDisposable
{
    private const ulong Id = 1;
    private static readonly DateTime Install = new(2026, 9, 12, 19, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = new(2026, 9, 20, 21, 0, 0, DateTimeKind.Utc);

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private string Characters => tmp.File("characters");

    private string SnapshotPath => Path.Combine(Characters, "1.json");

    private string DatesPath => CompletionDateFile.PathFor(Characters, Id);

    private static ushort Q(uint rowId) => QuestRecord.ToQuestId(rowId);

    /// <summary>Recording started at <see cref="Install"/> with A complete; B seen completing at <see cref="Later"/>.</summary>
    private static CharacterSnapshot Dated() =>
        CompletionDates.Carry(
            CompletionDates.Begin(null, Fixture.Snapshot(Fixture.A) with { TakenUtc = Install }),
            Fixture.Snapshot(Fixture.A, Fixture.B) with { TakenUtc = Later });

    [Fact]
    public void Dates_sit_in_their_own_file_and_load_back_into_the_snapshot()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        store.Save(Dated());

        Assert.Equal(Path.Combine(Characters, "1.dates.json"), DatesPath);
        Assert.True(File.Exists(DatesPath));
        Assert.DoesNotContain("completedUtc", File.ReadAllText(SnapshotPath), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("completionDatesSinceUtc", File.ReadAllText(SnapshotPath), StringComparison.OrdinalIgnoreCase);

        var loaded = store.Load(Id)!;
        Assert.Equal(Install, loaded.CompletionDatesSinceUtc);
        Assert.Equal(new QuestCompletionDate(CompletionDateKind.Seen, Later), CompletionDates.For(loaded, Q(Fixture.B)));
        Assert.Equal(CompletionDateKind.Before, CompletionDates.For(loaded, Q(Fixture.A))!.Value.Kind);
        Assert.True(store.LoadShared(Id).Value!.CompletedUtc.ContainsKey(Q(Fixture.B)));

        // Not a character of its own, and deleted with the character.
        Assert.Single(store.List());
        Assert.Contains(DatesPath, CharacterSidecars.PathsFor(Characters, Id));
        Assert.Contains(DatesPath, CharacterSidecars.FindAll(Characters));
        var scan = FolderScan.Run(Characters, new Dictionary<ulong, FileStamp>(), store, new HashSet<ulong>());
        Assert.Equal(new[] { Id }, scan.Stamps.Keys);
    }

    [Fact]
    public void An_older_build_rewriting_the_snapshot_keeps_the_dates()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        store.Save(Dated());

        // A 1.4 build saves the character again: its snapshot knows nothing of dates.
        var json = JsonNode.Parse(File.ReadAllText(SnapshotPath))!.AsObject();
        json["name"] = "Rewritten by 1.4";
        File.WriteAllText(SnapshotPath, json.ToJsonString());

        var loaded = store.Load(Id)!;
        Assert.Equal("Rewritten by 1.4", loaded.Name);
        Assert.Equal(Later, loaded.CompletedUtc[Q(Fixture.B)]);

        // And a save of a snapshot without dates (this build never makes one, but a downgrade path might) leaves the file alone.
        var before = File.ReadAllText(DatesPath);
        store.Save(Fixture.Snapshot(Fixture.A, Fixture.B));
        Assert.Equal(before, File.ReadAllText(DatesPath));
        Assert.Equal(Later, store.Load(Id)!.CompletedUtc[Q(Fixture.B)]);
    }

    [Fact]
    public void The_dates_file_keeps_its_own_basis_for_quests_completed_under_an_older_build()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        store.Save(Dated());

        // C completed while a 1.4 build played: the snapshot has it, the dates file's basis does not.
        var json = JsonNode.Parse(File.ReadAllText(SnapshotPath))!.AsObject();
        json["completedBits"] = Convert.ToBase64String(Fixture.Bits(Fixture.A, Fixture.B, Fixture.C));
        File.WriteAllText(SnapshotPath, json.ToJsonString());

        // The stored character does not claim C was complete before recording started…
        Assert.Null(CompletionDates.For(store.Load(Id), Q(Fixture.C)));

        // …and the next login dates it between the dates' last capture and now.
        var login = new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc);
        var warnings = new List<string>();
        var dated = CompletionDates.BeginFrom(store.LoadDates(Id), Fixture.Snapshot(Fixture.A, Fixture.B, Fixture.C) with { TakenUtc = login }, warnings);
        Assert.Equal(new QuestCompletionDate(CompletionDateKind.By, login, Later), CompletionDates.For(dated, Q(Fixture.C)));
        Assert.Empty(warnings);
    }

    [Fact]
    public void Inline_dates_from_a_preview_build_are_read_and_moved_by_the_next_save()
    {
        Directory.CreateDirectory(Characters);
        var json = JsonNode.Parse(File.ReadAllText(SaveAndRead(Fixture.Snapshot(Fixture.A, Fixture.B) with { TakenUtc = Later })))!.AsObject();
        json["completionDatesSinceUtc"] = Install.ToString("O", CultureInfo.InvariantCulture);
        json["completedUtc"] = new JsonObject { [Q(Fixture.B).ToString(CultureInfo.InvariantCulture)] = Later.ToString("O", CultureInfo.InvariantCulture) };
        File.WriteAllText(SnapshotPath, json.ToJsonString());
        File.Delete(DatesPath);

        var store = new JsonSnapshotStore(tmp.Path);
        var loaded = store.Load(Id)!;
        Assert.Equal(Install, loaded.CompletionDatesSinceUtc);
        Assert.Equal(Later, loaded.CompletedUtc[Q(Fixture.B)]);

        var forFirstPass = store.LoadDates(Id);
        Assert.Equal(SharedLoad.Loaded, forFirstPass.Status);
        Assert.Equal(Later, forFirstPass.Value!.CompletedUtc[Q(Fixture.B)]);

        // The first pass continues from them, and the save moves them to the dates file.
        var dated = CompletionDates.BeginFrom(forFirstPass, Fixture.Snapshot(Fixture.A, Fixture.B) with { TakenUtc = Later.AddDays(1) }, []);
        store.Save(dated);
        Assert.DoesNotContain("completedUtc", File.ReadAllText(SnapshotPath), StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(DatesPath));
        var again = store.Load(Id)!;
        Assert.Equal(Install, again.CompletionDatesSinceUtc);
        Assert.Equal(new QuestCompletionDate(CompletionDateKind.Seen, Later), CompletionDates.For(again, Q(Fixture.B)));
        Assert.Equal(CompletionDateKind.Before, CompletionDates.For(again, Q(Fixture.A))!.Value.Kind);
    }

    [Fact]
    public void The_first_pass_read_tells_missing_from_unreadable_corrupt_and_newer()
    {
        var store = new JsonSnapshotStore(tmp.Path);
        Assert.Equal(SharedLoad.Missing, store.LoadDates(Id).Status);

        // A snapshot from before 1.5, no dates file: nothing recorded yet.
        store.Save(Fixture.Snapshot(Fixture.A));
        Assert.Equal(SharedLoad.Missing, store.LoadDates(Id).Status);

        store.Save(Dated());
        Assert.Equal(SharedLoad.Loaded, store.LoadDates(Id).Status);

        // Locked by another process: unreadable, left in place.
        using (new FileStream(DatesPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var locked = store.LoadDates(Id);
            Assert.Equal(SharedLoad.Unreadable, locked.Status);
            Assert.NotNull(locked.Problem);
        }

        Assert.True(File.Exists(DatesPath));

        // Written by a newer build: left alone.
        var newer = JsonNode.Parse(File.ReadAllText(DatesPath))!.AsObject();
        newer["version"] = CompletionDateFile.CurrentVersion + 1;
        File.WriteAllText(DatesPath, newer.ToJsonString());
        Assert.Equal(SharedLoad.Newer, store.LoadDates(Id).Status);
        Assert.True(File.Exists(DatesPath));

        // Corrupt: quarantined, so the next save starts a fresh file beside the kept copy.
        File.WriteAllText(DatesPath, "{ not json");
        var corrupt = store.LoadDates(Id);
        Assert.Equal(SharedLoad.Invalid, corrupt.Status);
        Assert.False(File.Exists(DatesPath));
        Assert.Single(Directory.GetFiles(Characters, "1.dates.json.corrupt-*"));
        Assert.Equal(SharedLoad.Missing, store.LoadDates(Id).Status);
    }

    private string SaveAndRead(CharacterSnapshot snapshot)
    {
        new JsonSnapshotStore(tmp.Path).Save(snapshot);
        return SnapshotPath;
    }
}
