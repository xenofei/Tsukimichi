using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests;

public class SmokeTests
{
    private static QuestRecord Quest(uint rowId, string name, uint genre, int sortKey) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = $"Test_{rowId}",
        Name = name,
        Journal = new JournalRef(1, "Main Scenario", 2, "Seventh Umbral Era", genre, "Gridania", sortKey),
    };

    [Fact]
    public void Catalog_indexes_records_in_journal_order()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(65576, "Close to Home", 3, 20),
            Quest(65575, "Coming to Gridania", 3, 10),
            Quest(70000, "Hidden", 0, 5),
        ]);

        Assert.Equal(3, catalog.Count);
        Assert.Equal("Coming to Gridania", catalog.ByGenre[3][0].Name);
        Assert.Equal("Close to Home", catalog.ByGenre[3][1].Name);
        Assert.Equal(65576u, catalog.ByQuestId[40].RowId);
        Assert.Equal("close to home", catalog.LowercaseNames[65576]);
        Assert.True(catalog.Get(70000)!.IsUnlisted);
        Assert.False(catalog.Get(65575)!.IsUnlisted);
    }

    [Fact]
    public void Snapshot_reads_completion_bits()
    {
        var snapshot = new CharacterSnapshot
        {
            ContentId = 1,
            Name = "Michiru Tsukikage",
            CompletedBits = [0b0000_0100],
        };

        Assert.Equal(CharacterSnapshot.CurrentSchemaVersion, snapshot.SchemaVersion);
        Assert.True(snapshot.IsCompleted(2));
        Assert.False(snapshot.IsCompleted(3));
        Assert.False(snapshot.IsCompleted(4000));
    }
}
