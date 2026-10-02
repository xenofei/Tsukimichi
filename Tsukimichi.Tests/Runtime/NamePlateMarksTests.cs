using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Runtime;

/// <summary>
/// The optional nameplate marks (feature plan v5, 1.7.0; R8 D): which quest givers get "☾ Pinned", "☾ Moonlit reward"
/// or "☾ Ready on WHM", worked out once into an NPC-to-mark table so the nameplate hook only looks up.
/// </summary>
public sealed class NamePlateMarksTests
{
    private const uint Gerolt = 1003075;
    private const uint Rowena = 1003076;
    private const uint Hildi = 1003077;
    private const byte Whm = 24;

    private static QuestRecord Given(uint rowId, uint npcId, int sortKey = 0, bool retired = false) =>
        Quest(rowId, $"Quest {rowId}", section: 3, genre: 103, sortKey: sortKey) with
        {
            Issuer = new Issuer(npcId, $"NPC {npcId}", 140, 20, 0f, 0f, 0f),
            IsRetired = retired,
        };

    private static QuestEvaluation Eval(QuestState state, byte? job = null, bool spare = false) =>
        new(state, [], null, job, null) { IsSpareAlternative = spare };

    [Theory]
    [InlineData(QuestState.Ready, true, false, NamePlateMarkKind.Pinned)]
    [InlineData(QuestState.Ready, true, true, NamePlateMarkKind.Pinned)]
    [InlineData(QuestState.Ready, false, true, NamePlateMarkKind.MoonlitReward)]
    [InlineData(QuestState.ReadyOnOtherJob, true, false, NamePlateMarkKind.OtherJob)]
    [InlineData(QuestState.ReadyOnOtherJob, false, true, NamePlateMarkKind.OtherJob)]
    public void A_quest_the_character_can_take_and_cares_about_earns_a_mark(QuestState state, bool pinned, bool moonlit, NamePlateMarkKind expected)
    {
        Assert.Equal(expected, NamePlateMarks.Select(state, pinned, moonlit));
    }

    [Theory]
    [InlineData(QuestState.Ready, false, false)]
    [InlineData(QuestState.ReadyOnOtherJob, false, false)]
    [InlineData(QuestState.Accepted, true, true)]
    [InlineData(QuestState.Completed, true, true)]
    [InlineData(QuestState.Blocked, true, true)]
    [InlineData(QuestState.Foreclosed, true, true)]
    [InlineData(QuestState.Unknown, true, true)]
    public void Any_other_quest_or_state_earns_none(QuestState state, bool pinned, bool moonlit)
    {
        Assert.Null(NamePlateMarks.Select(state, pinned, moonlit));
    }

    [Fact]
    public void The_strongest_mark_wins_per_giver_and_ties_go_to_journal_order()
    {
        var catalog = QuestCatalog.Build(
        [
            Given(1, Gerolt, sortKey: 10),
            Given(2, Gerolt, sortKey: 20),
            Given(3, Gerolt, sortKey: 30),
            Given(4, Rowena, sortKey: 40),
            Given(5, Rowena, sortKey: 50),
            Given(6, Hildi, sortKey: 60),
        ]);
        var states = new Dictionary<uint, QuestEvaluation>
        {
            [1] = Eval(QuestState.ReadyOnOtherJob, Whm),
            [2] = Eval(QuestState.Ready),
            [3] = Eval(QuestState.Ready),
            [4] = Eval(QuestState.Ready),
            [5] = Eval(QuestState.Ready),
            [6] = Eval(QuestState.ReadyOnOtherJob, Whm),
        };
        var pinned = new HashSet<uint> { 3 };
        var moonlit = new HashSet<uint> { 1, 2, 4, 5, 6 };

        var marks = NamePlateMarks.Build(catalog, states, pinned, moonlit);

        Assert.Equal(new NamePlateMark(NamePlateMarkKind.Pinned, 3, 0), marks[Gerolt]);
        Assert.Equal(new NamePlateMark(NamePlateMarkKind.MoonlitReward, 4, 0), marks[Rowena]);
        Assert.Equal(new NamePlateMark(NamePlateMarkKind.OtherJob, 6, Whm), marks[Hildi]);
        Assert.Equal(3, marks.Count);
    }

    [Fact]
    public void Removed_quests_spare_alternatives_and_givers_without_an_npc_are_left_out()
    {
        var catalog = QuestCatalog.Build(
        [
            Given(1, Gerolt, retired: true),
            Given(2, Rowena),
            Given(3, 0),
            Quest(4, "No issuer", section: 3, genre: 103),
        ]);
        var states = new Dictionary<uint, QuestEvaluation>
        {
            [1] = Eval(QuestState.Ready),
            [2] = Eval(QuestState.Ready, spare: true),
            [3] = Eval(QuestState.Ready),
            [4] = Eval(QuestState.Ready),
        };

        var marks = NamePlateMarks.Build(catalog, states, new HashSet<uint> { 1, 2, 3, 4 }, new HashSet<uint>());

        Assert.Empty(marks);
    }

    [Fact]
    public void Nothing_pinned_or_moonlit_or_no_states_builds_an_empty_table()
    {
        var catalog = QuestCatalog.Build([Given(1, Gerolt)]);
        var ready = new Dictionary<uint, QuestEvaluation> { [1] = Eval(QuestState.Ready) };

        Assert.Empty(NamePlateMarks.Build(catalog, ready, new HashSet<uint>(), new HashSet<uint>()));
        Assert.Empty(NamePlateMarks.Build(catalog, new Dictionary<uint, QuestEvaluation>(), new HashSet<uint> { 1 }, new HashSet<uint>()));
    }

    [Fact]
    public void Same_compares_kind_and_job_per_giver()
    {
        var a = new Dictionary<uint, NamePlateMark> { [Gerolt] = new(NamePlateMarkKind.Pinned, 1, 0) };
        var sameButOtherQuest = new Dictionary<uint, NamePlateMark> { [Gerolt] = new(NamePlateMarkKind.Pinned, 2, 0) };
        var otherKind = new Dictionary<uint, NamePlateMark> { [Gerolt] = new(NamePlateMarkKind.MoonlitReward, 1, 0) };
        var otherNpc = new Dictionary<uint, NamePlateMark> { [Rowena] = new(NamePlateMarkKind.Pinned, 1, 0) };

        // The plate shows the kind (and job), not the quest, so another quest with the same mark needs no redraw.
        Assert.True(NamePlateMarks.Same(a, sameButOtherQuest));
        Assert.False(NamePlateMarks.Same(a, otherKind));
        Assert.False(NamePlateMarks.Same(a, otherNpc));
        Assert.False(NamePlateMarks.Same(a, new Dictionary<uint, NamePlateMark>()));
    }
}
