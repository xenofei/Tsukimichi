using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>
/// <c>Fixtures/snapshot-v1.json</c> is a real schema-v1 character file written by 0.5.0 (content id, name and world
/// anonymised). It is frozen before any model change so a later build must still read it and write it back unchanged.
/// </summary>
public sealed class SnapshotFixtureTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshot-v1.json");

    [Fact]
    public void SnapshotV1Fixture_RoundTripsUnchanged()
    {
        var original = File.ReadAllText(FixturePath);
        using var tmp = new TempDir();

        var inRoot = tmp.File("in");
        Directory.CreateDirectory(Path.Combine(inRoot, "characters"));
        File.WriteAllText(Path.Combine(inRoot, "characters", "1.json"), original);

        var store = new JsonSnapshotStore(inRoot);
        var loaded = store.Load(1);

        Assert.NotNull(loaded);
        Assert.Empty(store.Warnings);
        Assert.Single(Directory.GetFiles(Path.Combine(inRoot, "characters")));
        Assert.Equal(1, CharacterSnapshot.CurrentSchemaVersion);
        Assert.Equal(1, loaded.SchemaVersion);
        Assert.Equal(1ul, loaded.ContentId);
        Assert.Equal("Fixture Character", loaded.Name);
        Assert.Equal(0u, loaded.World);
        Assert.Equal(new DateTime(2026, 9, 28, 16, 56, 54, DateTimeKind.Utc).AddTicks(9800056), loaded.TakenUtc);

        // The character is a real one: a populated journal, levels on twenty jobs, tribe standings, unlocks.
        Assert.True(loaded.CompletedBits.Length > 1000, "completion bitmask is present");
        Assert.True(loaded.IsCompleted(QuestRecord.ToQuestId(65621u)), "Close to Home is done");
        Assert.Equal(9, loaded.Accepted.Count);
        Assert.Equal(20, loaded.JobLevels.Count);
        Assert.Equal(9, loaded.Tribes.Count);
        Assert.Equal(13, loaded.UnlockedInstances.Count);
        Assert.Equal(4, loaded.ActiveFestivals.Count);
        Assert.Equal(2, loaded.GrandCompany);
        Assert.Equal(32, loaded.CurrentJob);
        Assert.Empty(loaded.DailyDone);
        Assert.False(loaded.AchievementsLoaded);

        // Written before the caps were read from the client: both 0, which the evaluator treats as "not checked".
        Assert.Equal(0, loaded.MaxExpansion);
        Assert.Equal(0, loaded.LevelCap);

        // Written before 0.6.2 read festival phases, custom delivery ranks and the carrier level: every one defaults
        // to "not captured", so a phase is unknown (never blocks) and the delivery gates are listed, not judged.
        Assert.Empty(loaded.ActiveFestivalPhases);
        Assert.Null(loaded.FestivalPhase(39));
        Assert.Empty(loaded.SatisfactionRanks);
        Assert.Null(loaded.SatisfactionRank(2));
        Assert.Null(loaded.CarrierLevel);

        var outRoot = tmp.File("out");
        new JsonSnapshotStore(outRoot).Save(loaded);
        var written = File.ReadAllText(Path.Combine(outRoot, "characters", "1.json"));

        Assert.Equal(Normalize(original), Normalize(written));
    }

    /// <summary>Line endings follow the platform (and git's autocrlf); the trailing newline is not part of the document.</summary>
    private static string Normalize(string json) => json.Replace("\r\n", "\n").TrimEnd('\n');
}
