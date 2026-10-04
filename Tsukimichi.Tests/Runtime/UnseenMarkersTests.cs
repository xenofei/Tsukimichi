using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Runtime;

/// <summary>The reverse check of C1: Ready quests the game lists no marker for while the character stands in their giver's zone.</summary>
public class UnseenMarkersTests
{
    private const uint Zone = 132;
    private const uint OtherZone = 148;

    private static Issuer Giver(uint territory) => new(1000, "Giver", territory, 2, 0f, 0f, 0f);

    private static QuestCatalog ZoneCatalog() => Catalog(
        Quest(A) with { Issuer = Giver(Zone) },
        Quest(B) with { Issuer = Giver(Zone) },
        Quest(C) with { Issuer = Giver(Zone), IsRepeatable = true },
        Quest(D) with { Issuer = Giver(Zone), Festival = 3 },
        Quest(E) with { Issuer = Giver(OtherZone) });

    private static QuestEvaluation State(QuestState state) => new(state, [], null, null, null);

    private static Dictionary<uint, QuestEvaluation> States(QuestState a = QuestState.Ready, QuestState b = QuestState.Ready) => new()
    {
        [A] = State(a),
        [B] = State(b),
        [E] = State(QuestState.Ready),
    };

    [Fact]
    public void Candidates_are_the_zone_s_one_off_quests()
    {
        Assert.Equal([A, B], UnseenMarkers.Candidates(ZoneCatalog(), Zone).Select(q => q.RowId));
        Assert.Equal([E], UnseenMarkers.Candidates(ZoneCatalog(), OtherZone).Select(q => q.RowId));
        Assert.Empty(UnseenMarkers.Candidates(ZoneCatalog(), 0));
    }

    [Fact]
    public void A_ready_quest_without_its_marker_counts_up_and_a_marker_starts_it_over()
    {
        var candidates = UnseenMarkers.Candidates(ZoneCatalog(), Zone);
        var unseen = new UnseenMarkers();
        var markers = new HashSet<uint> { A };
        var zones = new HashSet<uint> { Zone };

        for (var i = 0; i < 3; i++)
        {
            Assert.True(unseen.Observe(Zone, candidates, States(), markers, zones));
        }

        Assert.Equal(3, unseen.Misses[QuestRecord.ToQuestId(B)]);
        Assert.False(unseen.Misses.ContainsKey(QuestRecord.ToQuestId(A)));

        markers.Add(B);
        Assert.True(unseen.Observe(Zone, candidates, States(), markers, zones));
        Assert.Empty(unseen.Misses);
    }

    [Fact]
    public void Nothing_is_counted_while_the_game_lists_no_marker_in_the_zone_and_a_quest_no_longer_ready_is_dropped()
    {
        var candidates = UnseenMarkers.Candidates(ZoneCatalog(), Zone);
        var unseen = new UnseenMarkers();
        var noMarkers = new HashSet<uint>();

        // The game has not built the zone's list: no evidence either way.
        Assert.False(unseen.Observe(Zone, candidates, States(), noMarkers, new HashSet<uint>()));
        Assert.Empty(unseen.Misses);

        Assert.True(unseen.Observe(Zone, candidates, States(), noMarkers, new HashSet<uint> { Zone }));
        Assert.Equal(2, unseen.Misses.Count);

        // B was accepted meanwhile: its count goes even though the character left the zone.
        Assert.True(unseen.Observe(OtherZone, [], States(b: QuestState.Accepted), noMarkers, new HashSet<uint>()));
        Assert.Equal([QuestRecord.ToQuestId(A)], unseen.Misses.Keys);

        unseen.Clear();
        Assert.Empty(unseen.Misses);
    }
}
