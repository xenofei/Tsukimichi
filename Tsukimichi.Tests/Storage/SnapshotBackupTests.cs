using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Storage;

/// <summary>The once-a-day <c>&lt;id&gt;.prev.json</c> backup kept beside each saved character (1.5.0 "Trust").</summary>
public sealed class SnapshotBackupTests : IDisposable
{
    private const ulong Id = 0x0040_0000_0123_4567UL;
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private string Characters => tmp.File("characters");

    private string SnapshotPath => Path.Combine(Characters, Id + ".json");

    private string BackupPath => SnapshotBackup.PathFor(Characters, Id);

    private static CharacterSnapshot Snapshot(string name) => new() { ContentId = Id, Name = name, CompletedBits = [0xFF] };

    [Fact]
    public void The_backup_sits_beside_the_snapshot_and_is_not_listed_as_a_character()
    {
        var store = new JsonSnapshotStore(tmp.Path) { Clock = () => T0 };
        store.Save(Snapshot("first"));
        store.Save(Snapshot("second"));

        Assert.Equal(Path.Combine(Characters, Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".prev.json"), BackupPath);
        Assert.True(File.Exists(BackupPath));
        Assert.Single(store.List());
        Assert.Contains(BackupPath, CharacterSidecars.PathsFor(Characters, Id));
        Assert.Contains(BackupPath, CharacterSidecars.FindAll(Characters));
    }

    [Fact]
    public void The_first_save_has_nothing_to_back_up()
    {
        var store = new JsonSnapshotStore(tmp.Path) { Clock = () => T0 };
        store.Save(Snapshot("first"));

        Assert.False(File.Exists(BackupPath));
    }

    [Fact]
    public void The_backup_holds_the_file_as_it_was_before_the_save()
    {
        var store = new JsonSnapshotStore(tmp.Path) { Clock = () => T0 };
        store.Save(Snapshot("first"));
        store.Save(Snapshot("second"));

        Assert.Contains("\"first\"", File.ReadAllText(BackupPath));
        Assert.Equal("second", store.Load(Id)!.Name);
    }

    [Fact]
    public void The_backup_is_refreshed_at_most_once_a_day()
    {
        var now = T0;
        var store = new JsonSnapshotStore(tmp.Path) { Clock = () => now };
        store.Save(Snapshot("first"));
        store.Save(Snapshot("second"));
        File.SetLastWriteTimeUtc(BackupPath, T0);

        // Within the day, later saves leave the backup alone.
        now = T0.AddHours(23);
        store.Save(Snapshot("third"));
        Assert.Contains("\"first\"", File.ReadAllText(BackupPath));

        // A day on, the next save refreshes it with the file it overwrites.
        now = T0.AddDays(1);
        store.Save(Snapshot("fourth"));
        Assert.Contains("\"third\"", File.ReadAllText(BackupPath));
    }

    [Fact]
    public void A_snapshot_file_that_does_not_parse_never_replaces_the_backup()
    {
        Directory.CreateDirectory(Characters);
        File.WriteAllText(BackupPath, "{\"Name\":\"good\"}");
        File.SetLastWriteTimeUtc(BackupPath, T0.AddDays(-3));
        File.WriteAllText(SnapshotPath, "{ not json");

        Assert.False(SnapshotBackup.RotateIfDue(SnapshotPath, BackupPath, T0));
        Assert.Equal("{\"Name\":\"good\"}", File.ReadAllText(BackupPath));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(-25.0, true)]
    [InlineData(-24.0, true)]
    [InlineData(-23.9, false)]
    [InlineData(0.0, false)]
    [InlineData(48.0, true)] // stamped two days ahead: the clock moved back
    public void Due_once_a_day(double? hoursAgo, bool due)
    {
        DateTime? written = hoursAgo is { } h ? T0.AddHours(h) : null;

        Assert.Equal(due, SnapshotBackup.IsDue(written, T0));
    }

    private string OlderPath => SnapshotBackup.OlderPathFor(Characters, Id);

    /// <summary>600 one-off quests the catalog names, so the lost-progress check counts them.</summary>
    private static readonly uint[] OneOff = Enumerable.Range(0, 600).Select(i => 65600u + (uint)i).ToArray();

    private static readonly QuestCatalog Catalog = Fixture.Catalog([.. OneOff.Select(id => Fixture.Quest(id))]);

    private static CharacterSnapshot Progress(string name, int completed) =>
        Fixture.Snapshot([.. OneOff.Take(completed)]) with { ContentId = Id, Name = name };

    [Fact]
    public void The_refresh_keeps_the_backup_it_replaces_as_the_older_generation()
    {
        var now = T0;
        var store = new JsonSnapshotStore(tmp.Path) { Clock = () => now };
        store.Save(Snapshot("first"));
        store.Save(Snapshot("second"));
        File.SetLastWriteTimeUtc(BackupPath, T0);
        Assert.False(File.Exists(OlderPath));

        now = T0.AddDays(1);
        store.Save(Snapshot("third"));

        Assert.Contains("\"second\"", File.ReadAllText(BackupPath));
        Assert.Contains("\"first\"", File.ReadAllText(OlderPath));
        Assert.Contains(OlderPath, CharacterSidecars.PathsFor(Characters, Id));
        Assert.Contains(OlderPath, CharacterSidecars.FindAll(Characters));
        Assert.Single(store.List());
    }

    [Fact]
    public void A_saved_file_that_lost_many_completed_quests_never_becomes_the_backup()
    {
        var now = T0;
        var store = new JsonSnapshotStore(tmp.Path) { Clock = () => now, Catalog = () => Catalog };
        store.Save(Progress("good", 500));
        store.Save(Progress("good", 501));
        Assert.Contains("\"good\"", File.ReadAllText(BackupPath));
        File.SetLastWriteTimeUtc(BackupPath, T0);

        // A bad capture saved at 23:59 …
        now = T0.AddHours(23).AddMinutes(59);
        store.Save(Progress("bad", 100));

        // … is still on disk when the backup comes due a minute later: the backup keeps the good file.
        now = T0.AddDays(1);
        store.Save(Progress("bad", 101));
        Assert.Contains("\"good\"", File.ReadAllText(BackupPath));
        Assert.False(File.Exists(OlderPath));

        // A handful lost against the backup is ordinary play (a few quests the game resets): the refresh goes ahead.
        store.Save(Progress("fine", 495));
        now = T0.AddDays(3);
        File.SetLastWriteTimeUtc(BackupPath, T0);
        store.Save(Progress("fine", 496));
        Assert.Contains("\"fine\"", File.ReadAllText(BackupPath));
        Assert.Contains("\"good\"", File.ReadAllText(OlderPath));
    }

    [Fact]
    public void A_backup_now_copies_the_save_within_the_day_unless_it_lost_progress()
    {
        var now = T0;
        var store = new JsonSnapshotStore(tmp.Path) { Clock = () => now, Catalog = () => Catalog };
        store.Save(Progress("old", 400));
        store.Save(Progress("before the loss", 500));
        Assert.Contains("\"old\"", File.ReadAllText(BackupPath));

        // A held-back capture is accepted: the save it overwrites becomes the backup at once.
        now = T0.AddHours(1);
        Assert.True(store.BackupNow(Id));
        Assert.Contains("\"before the loss\"", File.ReadAllText(BackupPath));
        Assert.Contains("\"old\"", File.ReadAllText(OlderPath));
        File.SetLastWriteTimeUtc(BackupPath, now);

        // Once the loss is saved, another backup now keeps the better copy.
        store.Save(Progress("after the loss", 100));
        Assert.False(store.BackupNow(Id));
        Assert.Contains("\"before the loss\"", File.ReadAllText(BackupPath));
    }

    [Fact]
    public void A_backup_that_fails_is_reported_and_the_save_goes_ahead()
    {
        string? failed = null;
        var store = new JsonSnapshotStore(tmp.Path) { Clock = () => T0, BackupFailed = (path, _) => failed = path };
        store.Save(Snapshot("first"));

        // A directory where the backup file should be makes its write fail.
        Directory.CreateDirectory(BackupPath);
        store.Save(Snapshot("second"));

        Assert.Equal(SnapshotPath, failed);
        Assert.Equal("second", store.Load(Id)!.Name);
    }
}
