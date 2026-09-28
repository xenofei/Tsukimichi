using Tsukimichi.Core.Discovery;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests.Discovery;

public class QuestDiscoveryTests
{
    private const uint Gridania = 132;
    private const uint Bentbranch = 148;
    private const uint Mother = 1000100;
    private const uint Bertennant = 1000200;

    private static QuestRecord Quest(uint rowId, string name, byte level, uint npcId, uint territory) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = $"Test_{rowId}",
        Name = name,
        Level = level,
        Journal = new JournalRef(1, "Side", 2, "Gridania", 3, "Central Shroud", (int)rowId),
        Issuer = new Issuer(npcId, "NPC", territory, 0, 0f, 0f, 0f),
    };

    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(65601, "Zephyr", 15, Mother, Gridania),
        Quest(65602, "Aurora", 15, Mother, Gridania),
        Quest(65603, "Bramble", 5, Bertennant, Gridania),
        Quest(65604, "Corundum", 10, Bertennant, Gridania),
        Quest(65605, "Distant", 1, Bertennant, Bentbranch),
        new QuestRecord { RowId = 65606, QuestId = QuestRecord.ToQuestId(65606), Name = "No issuer", Journal = new JournalRef(1, "Side", 2, "Gridania", 3, "Central Shroud", 65606) },
    ]);

    private static QuestEvaluation Eval(QuestState state) => new(state, [], null, null, null);

    private static readonly Dictionary<uint, QuestEvaluation> States = new()
    {
        [65601] = Eval(QuestState.Ready),
        [65602] = Eval(QuestState.ReadyOnOtherJob),
        [65603] = Eval(QuestState.Completed),
        [65604] = Eval(QuestState.Ready),
        [65605] = Eval(QuestState.Ready),
    };

    [Fact]
    public void StartableInZone_keeps_ready_states_in_the_territory_sorted_by_level_then_name()
    {
        var matches = QuestDiscovery.StartableInZone(Catalog, States, Gridania);

        Assert.Equal(["Corundum", "Aurora", "Zephyr"], matches.Select(q => q.Name));
    }

    [Fact]
    public void StartableInZone_sorts_by_the_displayed_level()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(65601, "Shown as twelve", 10, Mother, Gridania) with { LevelOffset = 2 },
            Quest(65602, "Eleven", 11, Mother, Gridania),
        ]);

        Assert.Equal(["Eleven", "Shown as twelve"], QuestDiscovery.StartableInZone(catalog, States, Gridania).Select(q => q.Name));
    }

    [Fact]
    public void StartableInZone_can_leave_out_quests_ready_on_another_job()
    {
        Assert.Equal(["Corundum", "Zephyr"], QuestDiscovery.StartableInZone(Catalog, States, Gridania, includeOtherJob: false).Select(q => q.Name));
        Assert.Equal(["Corundum", "Aurora", "Zephyr"], QuestDiscovery.StartableInZone(Catalog, States, Gridania, includeOtherJob: true).Select(q => q.Name));
    }

    [Fact]
    public void AcceptedInZone_lists_accepted_quests_whose_giver_stands_in_the_territory_sorted_by_level_then_name()
    {
        var states = new Dictionary<uint, QuestEvaluation>(States)
        {
            [65601] = Eval(QuestState.Accepted),
            [65603] = Eval(QuestState.Accepted),
            [65605] = Eval(QuestState.Accepted),
        };

        Assert.Equal(["Bramble", "Zephyr"], QuestDiscovery.AcceptedInZone(Catalog, states, Gridania).Select(q => q.Name));
        Assert.Equal(["Distant"], QuestDiscovery.AcceptedInZone(Catalog, states, Bentbranch).Select(q => q.Name));
        Assert.Equal(["Corundum", "Aurora"], QuestDiscovery.StartableInZone(Catalog, states, Gridania).Select(q => q.Name));
    }

    [Fact]
    public void AcceptedInZone_is_empty_without_territory_states_or_matches()
    {
        Assert.Empty(QuestDiscovery.AcceptedInZone(Catalog, States, Gridania));
        Assert.Empty(QuestDiscovery.AcceptedInZone(Catalog, States, 0));
        Assert.Empty(QuestDiscovery.AcceptedInZone(Catalog, new Dictionary<uint, QuestEvaluation>(), Gridania));
        Assert.Empty(QuestDiscovery.AcceptedInZone(Catalog, States, 999));
    }

    [Fact]
    public void StartableInZone_is_empty_without_territory_states_or_matches()
    {
        Assert.Empty(QuestDiscovery.StartableInZone(Catalog, States, 0));
        Assert.Empty(QuestDiscovery.StartableInZone(Catalog, new Dictionary<uint, QuestEvaluation>(), Gridania));
        Assert.Empty(QuestDiscovery.StartableInZone(Catalog, States, 999));
        Assert.Equal(["Distant"], QuestDiscovery.StartableInZone(Catalog, States, Bentbranch).Select(q => q.Name));
    }

    [Fact]
    public void IssuedBy_lists_the_npcs_quests_in_journal_order_regardless_of_state()
    {
        Assert.Equal([65603u, 65604u, 65605u], QuestDiscovery.IssuedBy(Catalog, Bertennant).Select(q => q.RowId));
        Assert.Equal([65601u, 65602u], QuestDiscovery.IssuedBy(Catalog, Mother).Select(q => q.RowId));
        Assert.Empty(QuestDiscovery.IssuedBy(Catalog, 0));
        Assert.Empty(QuestDiscovery.IssuedBy(Catalog, 42));
    }
}
