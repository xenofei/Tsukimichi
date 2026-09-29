using Tsukimichi.Core.Discovery;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Tests.Data;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// <see cref="QuestScope.Issuer"/>: the scope the NPC context menu opens the Journal on (feature plan v3 P2). It lists
/// what one NPC hands out, in journal order, removed quests left out, and behaves like a journal node for the
/// Include removed toggle.
/// </summary>
public class IssuerScopeTests
{
    private const uint Gerolt = 1003075;
    private const uint Rowena = 1003076;

    private static QuestRecord Issued(uint rowId, string name, uint npcId, string npcName, int sortKey, bool retired = false) =>
        Quest(rowId, name, section: 1, category: 10, genre: 100, sortKey: sortKey) with
        {
            Issuer = new Issuer(npcId, npcName, 140, 20, 0f, 0f, 0f),
            IsRetired = retired,
        };

    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Issued(1, "Up in Arms", Gerolt, "Gerolt", sortKey: 30),
        Issued(2, "A Relic Reborn", Gerolt, "Gerolt", sortKey: 10),
        Issued(3, "Old and Retired", Gerolt, "Gerolt", sortKey: 20, retired: true),
        Issued(4, "Rowena's Deal", Rowena, "Rowena", sortKey: 40),
        Quest(5, "No issuer", section: 1, category: 10, genre: 100, sortKey: 50),
    ]);

    private static readonly Dictionary<uint, QuestState> AllStates = States(
        (1, QuestState.Blocked),
        (2, QuestState.Completed),
        (3, QuestState.Completed),
        (4, QuestState.Ready),
        (5, QuestState.Ready));

    [Fact]
    public void Issuer_scope_lists_the_npcs_live_quests_in_journal_order()
    {
        var result = Run(Catalog, AllStates, scope: QuestScope.Issuer(Gerolt));

        Assert.Equal(new uint[] { 2, 1 }, RowIds(result));
        Assert.Equal(2, result.TotalInScope);
        Assert.Null(result.Empty);
    }

    [Fact]
    public void Issuer_scope_never_shows_removed_quests_even_when_included()
    {
        var result = Run(Catalog, AllStates, new FilterSet { IncludeUnlisted = true }, scope: QuestScope.Issuer(Gerolt));

        Assert.Equal(new uint[] { 2, 1 }, RowIds(result));
        Assert.Equal(2, result.TotalInScope);
    }

    [Fact]
    public void Issuer_scope_of_an_unknown_npc_is_an_empty_scope()
    {
        var result = Run(Catalog, AllStates, scope: QuestScope.Issuer(42));

        Assert.Empty(result.Rows);
        Assert.Equal(0, result.TotalInScope);
        Assert.True(result.Empty!.ScopeIsEmpty);

        var none = Run(Catalog, AllStates, scope: QuestScope.Issuer(0));
        Assert.True(none.Empty!.ScopeIsEmpty);
    }

    [Fact]
    public void Issuer_scope_rows_carry_the_status_text_and_filters_still_apply()
    {
        var evaluations = Evaluations(AllStates, new Dictionary<uint, string> { [1] = "Lv 50" });
        var result = Run(Catalog, evaluations, scope: QuestScope.Issuer(Gerolt));
        Assert.Equal("Blocked · Lv 50", result.Rows[1].Status);

        var hidden = Run(Catalog, evaluations, new FilterSet { HideCompleted = true }, scope: QuestScope.Issuer(Gerolt));
        Assert.Equal(new uint[] { 1 }, RowIds(hidden));
        Assert.Equal(2, hidden.TotalInScope);
    }

    [Fact]
    public void Issuer_scopes_compare_by_npc_id()
    {
        Assert.Equal(QuestScope.Issuer(Gerolt), QuestScope.Issuer(Gerolt));
        Assert.NotEqual(QuestScope.Issuer(Gerolt), QuestScope.Issuer(Rowena));
        Assert.NotEqual(QuestScope.Issuer(100), QuestScope.Genre(100));
        Assert.Equal(ScopeKind.VirtualIssuer, QuestScope.Issuer(Gerolt).Kind);
    }

    [Fact]
    public void IssuerName_comes_from_the_catalog()
    {
        Assert.Equal("Gerolt", QuestDiscovery.IssuerName(Catalog, Gerolt));
        Assert.Equal("Rowena", QuestDiscovery.IssuerName(Catalog, Rowena));
        Assert.Null(QuestDiscovery.IssuerName(Catalog, 42));
        Assert.Null(QuestDiscovery.IssuerName(Catalog, 0));
    }
}

/// <summary>The same scope over the frozen catalog: Vorsaile Heuloix, the Twin Adder's quest-giver in New Gridania.</summary>
public class IssuerScopeFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const uint VorsaileHeuloix = 1000168;

    [Fact]
    public void Vorsaile_Heuloix_hands_out_nine_quests()
    {
        var catalog = fixture.Bundle.Catalog;
        var states = States(catalog, QuestState.Ready);

        var result = Run(catalog, states, scope: QuestScope.Issuer(VorsaileHeuloix));

        Assert.Equal(9, result.TotalInScope);
        Assert.Equal(9, result.Rows.Length);
        Assert.All(result.Rows, row => Assert.Equal(VorsaileHeuloix, row.Quest.Issuer!.NpcId));
        Assert.Contains("My Little Chocobo (Twin Adder)", Names(result));
        Assert.Contains("A Pup No Longer (Twin Adder)", Names(result));
        Assert.Equal("Vorsaile Heuloix", QuestDiscovery.IssuerName(catalog, VorsaileHeuloix));

        // The same list /tsuki which prints, in the same order.
        Assert.Equal(QuestDiscovery.IssuedBy(catalog, VorsaileHeuloix).Select(q => q.RowId), RowIds(result));
    }

    [Fact]
    public void The_placeholder_issuer_of_removed_quests_scopes_to_nothing()
    {
        // 1034221 is the issuer the game gives rows it removed (refiling rule 1); every one of them is IsRemoved.
        var result = Run(fixture.Bundle.Catalog, States(fixture.Bundle.Catalog, QuestState.Ready), new FilterSet { IncludeUnlisted = true }, scope: QuestScope.Issuer(1034221));

        Assert.Empty(result.Rows);
        Assert.True(result.Empty!.ScopeIsEmpty);
    }
}
