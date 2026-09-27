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

    private static QuestCatalog Catalog() => QuestCatalog.Build(
    [
        Quest(65576, "Close to Home", 3, 20),
        Quest(65575, "Coming to Gridania", 3, 10),
        Quest(70000, "Hidden", 0, 5),
    ]);

    [Fact]
    public void Catalog_indexes_records_in_journal_order()
    {
        var catalog = Catalog();

        Assert.Equal(3, catalog.Count);
        Assert.Equal("Coming to Gridania", catalog.ByGenre[3][0].Name);
        Assert.Equal("Close to Home", catalog.ByGenre[3][1].Name);
        Assert.Equal(65576u, catalog.ByQuestId[40].RowId);
        Assert.True(catalog.GetByRowId(70000)!.IsUnlisted);
        Assert.False(catalog.GetByRowId(65575)!.IsUnlisted);
    }

    [Fact]
    public void Catalog_lookups_distinguish_row_ids_from_quest_ids()
    {
        var catalog = Catalog();

        // 65576 is the row id; 40 is its quest id (low 16 bits). Each lookup answers only its own kind.
        Assert.Equal("Close to Home", catalog.GetByRowId(65576)!.Name);
        Assert.Equal("Close to Home", catalog.GetByQuestId(40)!.Name);
        Assert.Null(catalog.GetByRowId(40));
        Assert.Null(catalog.GetByQuestId(65535));

        Assert.True(catalog.TryGetByRowId(65575, out var byRow));
        Assert.Equal("Coming to Gridania", byRow.Name);
        Assert.False(catalog.TryGetByRowId(1, out _));

        Assert.True(catalog.TryGetByQuestId(4464, out var byQuest));
        Assert.Equal("Hidden", byQuest.Name);
        Assert.False(catalog.TryGetByQuestId(1, out _));
    }

    [Fact]
    public void Obsolete_lookups_forward_to_the_named_ones()
    {
        var catalog = Catalog();
#pragma warning disable CS0618 // the forwards exist for callers that have not moved yet
        Assert.Same(catalog.GetByRowId(65576), catalog.Get(65576u));
        Assert.Same(catalog.GetByQuestId(40), catalog.Get((ushort)40));
        Assert.True(catalog.TryGet(40, out var quest));
        Assert.Same(catalog.GetByQuestId(40), quest);
        Assert.False(catalog.TryGet(1, out _));
#pragma warning restore CS0618
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

    [Fact]
    public void Snapshot_reads_the_last_bit_of_the_array_and_nothing_past_it()
    {
        var snapshot = new CharacterSnapshot { CompletedBits = [0x00, 0x00, 0b1000_0000] };

        Assert.True(snapshot.IsCompleted(23));
        Assert.False(snapshot.IsCompleted(22));
        Assert.False(snapshot.IsCompleted(24));
        Assert.False(snapshot.IsCompleted(ushort.MaxValue));
        Assert.False(new CharacterSnapshot().IsCompleted(0));
    }
}
