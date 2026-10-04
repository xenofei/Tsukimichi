using System.Globalization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Runtime;

/// <summary>"The game says available" (feature plan v7, C1): the per-character book of the game's own offers.</summary>
public sealed class OfferSightingsTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 10, 3, 8, 0, 0, DateTimeKind.Utc);

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private static ushort Id(uint rowId) => QuestRecord.ToQuestId(rowId);

    private static QuestCatalog Catalog() => Fixture.Catalog(
        Fixture.Quest(Fixture.A),
        Fixture.Quest(Fixture.B),
        Fixture.Quest(Fixture.C) with { IsRepeatable = true },
        Fixture.Quest(Fixture.D));

    [Fact]
    public void A_quest_the_game_shows_is_recorded_once_and_its_last_sighting_moves_only_after_the_refresh()
    {
        var catalog = Catalog();
        var snapshot = Fixture.Snapshot();
        var book = new Dictionary<ushort, OfferSighting>();

        Assert.True(OfferSightings.Record(book, [Fixture.A], OfferSource.Marker, T0, catalog, snapshot));
        Assert.Equal(new OfferSighting(Id(Fixture.A), T0, T0, OfferSource.Marker), book[Id(Fixture.A)]);
        Assert.Equal(Fixture.A, book[Id(Fixture.A)].RowId);

        // Seen again on the next poll: nothing to write.
        Assert.False(OfferSightings.Record(book, [Fixture.A], OfferSource.Marker, T0.AddSeconds(2), catalog, snapshot));

        // Seen in the offer window too: a new source is a change.
        Assert.True(OfferSightings.Record(book, [Fixture.A], OfferSource.Offer, T0.AddMinutes(1), catalog, snapshot));
        Assert.Equal(OfferSource.Marker | OfferSource.Offer, book[Id(Fixture.A)].Sources);
        Assert.Equal(T0, book[Id(Fixture.A)].LastSeenUtc);

        // Once the refresh has passed the last sighting moves; the first stays.
        var later = T0 + OfferSightings.Refresh;
        Assert.True(OfferSightings.Record(book, [Fixture.A], OfferSource.Marker, later, catalog, snapshot));
        Assert.Equal((T0, later), (book[Id(Fixture.A)].FirstSeenUtc, book[Id(Fixture.A)].LastSeenUtc));
    }

    [Fact]
    public void Repeatable_unknown_completed_and_accepted_quests_are_not_recorded()
    {
        var catalog = Catalog();
        var snapshot = Fixture.Snapshot(Fixture.B) with { Accepted = [Fixture.Accepted(Fixture.D)] };
        var book = new Dictionary<ushort, OfferSighting>();

        Assert.False(OfferSightings.Record(book, [Fixture.B, Fixture.C, Fixture.D, Fixture.Target, 0], OfferSource.Marker, T0, catalog, snapshot));
        Assert.Empty(book);
        Assert.False(OfferSightings.Record(book, [Fixture.A], OfferSource.None, T0, catalog, snapshot));
    }

    [Fact]
    public void A_sighting_lasts_until_the_quest_is_accepted_or_completed()
    {
        var catalog = Catalog();
        var book = new Dictionary<ushort, OfferSighting>();
        OfferSightings.Record(book, [Fixture.A, Fixture.B, Fixture.D], OfferSource.Marker, T0, catalog, Fixture.Snapshot());

        Assert.False(OfferSightings.Reconcile(book, Fixture.Snapshot()));
        var moved = Fixture.Snapshot(Fixture.A) with { Accepted = [Fixture.Accepted(Fixture.B)] };
        Assert.True(OfferSightings.Reconcile(book, moved));
        Assert.Equal([Id(Fixture.D)], book.Keys);
    }

    [Theory]
    [InlineData(0u, 0u)]
    [InlineData(1u, 65537u)]
    [InlineData(5000u, 70536u)]
    [InlineData(70536u, 70536u)]
    public void A_marker_objective_reads_as_a_quest_row_whether_it_holds_the_row_or_the_runtime_id(uint objective, uint rowId)
    {
        Assert.Equal(rowId, OfferSightings.MarkerRowId(objective));
    }

    [Fact]
    public void Save_then_Load_round_trips_latest_first_and_a_bad_file_starts_over()
    {
        var path = OfferSightings.PathFor(tmp.File("characters"), 7);
        var book = new Dictionary<ushort, OfferSighting>
        {
            [Id(Fixture.A)] = new(Id(Fixture.A), T0, T0.AddHours(1), OfferSource.Marker),
            [Id(Fixture.B)] = new(Id(Fixture.B), T0, T0.AddHours(2), OfferSource.Offer | OfferSource.Marker),
        };

        OfferSightings.Save(path, book);
        var back = OfferSightings.Load(path);
        Assert.Equal(book, back);
        Assert.Equal([Id(Fixture.B), Id(Fixture.A)], OfferSightings.Newest(back).Select(s => s.QuestId));
        Assert.DoesNotContain("\"rowId\"", File.ReadAllText(path));

        var warnings = new List<string>();
        Assert.Empty(OfferSightings.Load(tmp.File("none.offers.json"), warnings));
        Assert.Empty(warnings);
        var bad = tmp.File("bad.offers.json");
        File.WriteAllText(bad, "{ not json");
        Assert.Empty(OfferSightings.Load(bad, warnings));
        Assert.Single(warnings);
    }

    [Fact]
    public void Duplicate_and_inverted_entries_load_merged_and_a_sourceless_one_is_skipped()
    {
        var path = tmp.File("dupes.offers.json");
        File.WriteAllText(path, """
            {
              "version": 1,
              "entries": [
                { "questId": 1, "firstSeenUtc": "2026-10-03T09:00:00Z", "lastSeenUtc": "2026-10-03T08:00:00Z", "sources": 1 },
                { "questId": 1, "firstSeenUtc": "2026-10-03T07:00:00Z", "lastSeenUtc": "2026-10-03T07:30:00Z", "sources": 2 },
                { "questId": 2, "firstSeenUtc": "2026-10-03T07:00:00Z", "lastSeenUtc": "2026-10-03T07:30:00Z", "sources": 0 }
              ]
            }
            """);

        var entry = Assert.Single(OfferSightings.Load(path).Values);
        Assert.Equal(new OfferSighting(1, T0.AddHours(-1), T0.AddHours(1), OfferSource.Marker | OfferSource.Offer), entry);
    }

    [Fact]
    public void The_sidecar_sits_beside_the_snapshot_and_is_deleted_with_its_character()
    {
        var dir = tmp.File("characters");
        const ulong id = 0x0040_0000_0123_4567UL;
        var path = OfferSightings.PathFor(dir, id);
        Assert.Equal(Path.Combine(dir, id.ToString(CultureInfo.InvariantCulture) + ".offers.json"), path);
        Assert.Contains(path, CharacterSidecars.PathsFor(dir, id));
        Assert.Contains(OfferSightings.FileSuffix, CharacterSidecars.Suffixes);
    }
}
