using System.Text.Json;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>Progress version 2 (plan v9 G7): levels, best scores and Aces, challenges and duels, merged across clients, and version 1 read as it is.</summary>
public sealed class MoonfallProgressV2Tests
{
    [Fact]
    public void A_version_1_file_reads_as_it_is_and_the_next_save_writes_version_2()
    {
        using var dir = new TempDir();
        var path = dir.File("user/moonfall.json");
        Directory.CreateDirectory(dir.File("user"));
        File.WriteAllText(path, """{ "version": 1, "baseCleared": 3, "expansionCleared": 0 }""");
        var read = MoonfallProgress.Load(path);
        Assert.Equal(3, read.BaseCleared);
        Assert.True(read.IsCleared("base-01"));
        Assert.True(read.IsCleared("base-03"));
        Assert.False(read.IsCleared("base-04"));
        Assert.Equal(0, read.Best("base-01"));
        Assert.Empty(read.Levels);

        read.RecordLevel("base-04", won: true, 120_000);
        var saved = MoonfallProgress.Record(path, read);
        Assert.Equal(4, saved.BaseCleared);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(2, json.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(4, json.RootElement.GetProperty("baseCleared").GetInt32());
        Assert.Equal(120_000, json.RootElement.GetProperty("levels").GetProperty("base-04").GetProperty("best").GetInt64());
        Assert.False(json.RootElement.TryGetProperty("challenges", out _));

        var again = MoonfallProgress.Load(path);
        Assert.True(again.IsCleared("base-04"));
        Assert.Equal(120_000, again.Best("base-04"));
    }

    [Fact]
    public void A_win_in_order_moves_the_count_and_one_out_of_order_waits_for_the_gap()
    {
        var progress = new MoonfallProgress();
        Assert.True(progress.RecordLevel("base-02", won: true, 50_000));
        Assert.Equal(0, progress.BaseCleared);
        progress.RecordLevel("base-01", won: true, 40_000);
        Assert.Equal(2, progress.BaseCleared);
        progress.RecordLevel("base-03", won: false, 90_000);
        Assert.Equal(2, progress.BaseCleared);
        Assert.Equal(90_000, progress.Best("base-03"));
        Assert.False(progress.IsCleared("base-03"));
        Assert.False(progress.RecordLevel("base-03", won: false, 10_000));
    }

    [Fact]
    public void Two_clients_merge_to_the_further_of_each()
    {
        using var dir = new TempDir();
        var path = dir.File("moonfall.json");
        var other = new MoonfallProgress();
        other.RecordLevel("base-01", won: true, 200_000, ace: 150_000);
        other.RecordChallenge("ch-01", done: false, 90_000);
        other.RecordDuel(MoonfallCompanion.Louisoix, MoonfallAiDifficulty.Adept, MoonfallDuelOutcome.Won);
        other.RecordDuel(MoonfallCompanion.Louisoix, MoonfallAiDifficulty.Adept, MoonfallDuelOutcome.Won);
        MoonfallProgress.Record(path, other);

        var mine = new MoonfallProgress();
        mine.RecordLevel("base-01", won: true, 120_000, ace: 150_000);
        mine.RecordLevel("base-02", won: true, 80_000);
        mine.RecordChallenge("ch-01", done: true, 160_000);
        mine.RecordDuel(MoonfallCompanion.Louisoix, MoonfallAiDifficulty.Adept, MoonfallDuelOutcome.Lost);
        mine.RecordDuel(MoonfallCompanion.Raubahn, MoonfallAiDifficulty.Master, MoonfallDuelOutcome.Drawn);
        var merged = MoonfallProgress.Record(path, mine);

        Assert.Equal(200_000, merged.Best("base-01"));
        Assert.True(merged.IsAced("base-01"));
        Assert.True(merged.IsCleared("base-02"));
        Assert.Equal(2, merged.BaseCleared);
        Assert.True(merged.IsChallengeDone("ch-01"));
        Assert.Equal(160_000, merged.Challenges["ch-01"].Best);
        var louisoix = merged.DuelRecord(MoonfallCompanion.Louisoix, MoonfallAiDifficulty.Adept);
        Assert.Equal((2, 1, 0), (louisoix.Wins, louisoix.Losses, louisoix.Draws));
        Assert.Equal(1, merged.DuelRecord(MoonfallCompanion.Raubahn, MoonfallAiDifficulty.Master).Draws);
        Assert.Equal("louisoix/adept", MoonfallProgress.DuelKey(MoonfallCompanion.Louisoix, MoonfallAiDifficulty.Adept));

        // This client takes in what the other won.
        Assert.True(mine.Absorb(merged));
        Assert.Equal(200_000, mine.Best("base-01"));
        Assert.False(mine.Absorb(merged));
    }

    [Fact]
    public void A_copy_is_deep()
    {
        var progress = new MoonfallProgress();
        progress.RecordLevel("base-01", won: true, 10);
        progress.RecordChallenge("ch-02", done: true, 5);
        progress.RecordDuel(MoonfallCompanion.Cid, MoonfallAiDifficulty.Novice, MoonfallDuelOutcome.Won);
        var copy = progress.Copy();
        progress.RecordLevel("base-01", won: true, 99);
        progress.RecordDuel(MoonfallCompanion.Cid, MoonfallAiDifficulty.Novice, MoonfallDuelOutcome.Won);
        Assert.Equal(10, copy.Best("base-01"));
        Assert.Equal(1, copy.BaseCleared);
        Assert.True(copy.IsChallengeDone("ch-02"));
        Assert.Equal(1, copy.DuelRecord(MoonfallCompanion.Cid, MoonfallAiDifficulty.Novice).Wins);
    }

    [Fact]
    public void Damaged_entries_read_as_nothing_and_a_corrupt_file_still_never_costs_progress()
    {
        using var dir = new TempDir();
        var path = dir.File("moonfall.json");
        File.WriteAllText(path, """
            { "version": 2, "baseCleared": 1,
              "levels": { "base-01": { "cleared": true, "best": -5 }, "base-02": null, "": { "best": 9 } },
              "challenges": { "ch-01": { "done": true, "best": -1 } },
              "duels": { "cid/adept": { "wins": -3, "losses": 2 } } }
            """);
        var read = MoonfallProgress.Load(path);
        Assert.True(read.IsCleared("base-01"));
        Assert.Equal(0, read.Best("base-01"));
        Assert.Single(read.Levels);
        Assert.True(read.IsChallengeDone("ch-01"));
        Assert.Equal(0, read.Challenges["ch-01"].Best);
        Assert.Equal((0, 2), (read.Duels["cid/adept"].Wins, read.Duels["cid/adept"].Losses));

        File.WriteAllText(path, "{ not json");
        var warnings = new List<string>();
        var mine = new MoonfallProgress();
        mine.RecordLevel("base-01", won: true, 7_000);
        var merged = MoonfallProgress.Record(path, mine, warnings);
        Assert.Single(warnings);
        Assert.Equal(7_000, merged.Best("base-01"));
        Assert.Equal(7_000, MoonfallProgress.Load(path).Best("base-01"));
    }

    [Fact]
    public void A_file_with_nulls_for_its_lists_reads()
    {
        using var dir = new TempDir();
        var path = dir.File("moonfall.json");
        File.WriteAllText(path, """{ "version": 2, "baseCleared": 2, "levels": null, "challenges": null, "duels": null }""");
        var read = MoonfallProgress.Load(path);
        Assert.Equal(2, read.BaseCleared);
        Assert.Empty(read.Levels);
        Assert.True(read.RecordLevel("base-03", won: true, 1));
        Assert.Equal(3, read.BaseCleared);
    }
}
