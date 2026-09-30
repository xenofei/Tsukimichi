using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Runtime;

/// <summary>
/// The reader-side rule for custom delivery ranks (<see cref="SatisfactionRankSlots.ToRanks"/>): every slot is kept,
/// rank 0 included, unless every slot reads 0, which is stored as "not checked" rather than twelve zeros.
/// </summary>
public class SatisfactionRankSlotsTests
{
    [Fact]
    public void Twelve_zero_slots_store_no_ranks()
    {
        var ranks = SatisfactionRankSlots.ToRanks(new byte[12]);

        Assert.Empty(ranks);
        var snapshot = Snapshot() with { SatisfactionRanks = ranks };
        Assert.Null(snapshot.SatisfactionRank(2));

        // So a rank quest is listed, not judged, until the client has the ranks.
        var quest = Quest(Target) with { SatisfactionNpc = 2, SatisfactionLevel = 4 };
        Assert.Equal(QuestState.Ready, StateResolver.Resolve(quest, snapshot, Catalog(quest), EvalContext.Default).State);
    }

    [Fact]
    public void Any_nonzero_slot_stores_every_slot_rank_0_included()
    {
        var slots = new byte[12];
        slots[1] = 5;

        var ranks = SatisfactionRankSlots.ToRanks(slots);

        Assert.Equal(12, ranks.Count);
        Assert.Equal(Enumerable.Range(1, 12).Select(i => (byte)i), ranks.Keys.Order());
        Assert.Equal((byte)5, ranks[2]);
        Assert.Equal((byte)0, ranks[1]);
        Assert.Equal((byte)0, ranks[12]);
    }

    [Fact]
    public void No_slots_store_no_ranks()
    {
        Assert.Empty(SatisfactionRankSlots.ToRanks([]));
    }
}
