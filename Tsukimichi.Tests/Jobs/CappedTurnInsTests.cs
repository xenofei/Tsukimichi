using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Rewards;
using Tsukimichi.Core.Todo;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.JobLadders;

/// <summary>
/// "Turn in on a job that isn't capped" (feature plan v7, C8; spec-1.19 "The Todo row"): a journal quest at its turn-in
/// step that gives EXP, while the current job is capped and another job would get it, becomes a Todo row, and the
/// optional chat line speaks once per quest per reaching that step. A synthetic table keeps the sums plain:
/// EXP = ExpFactor × level.
/// </summary>
public class CappedTurnInsTests
{
    private const byte Dragoon = 22;
    private const byte Sage = 40;
    private const byte BlueMage = 36;
    private const byte TurnIn = 255;

    private static readonly QuestExpTable Table = QuestExpTable.From(Enumerable.Range(1, 100).Select(l => (l, (uint)l, 100u)));
    private static readonly EvalContext Context = new();
    private static readonly Dictionary<uint, string> JobNames = new() { [Dragoon] = "DRG", [Sage] = "SGE" };

    private static QuestRecord Aery() => Quest(Target, "Into the Aery") with { Level = 56, ExpFactor = 10 };

    private static CharacterSnapshot On(byte current, byte sequence, params (byte Job, short Level)[] levels) => Snapshot() with
    {
        CurrentJob = current,
        LevelCap = 100,
        JobLevels = Levels(levels),
        Accepted = [Accepted(Target, sequence)],
    };

    private static List<CappedTurnIn> Find(QuestRecord quest, CharacterSnapshot snapshot, Func<byte, bool>? isLimited = null) =>
        CappedTurnIns.Find(Catalog(quest), snapshot, Table, Context, isLimited);

    private static TodoInputs Inputs(QuestRecord quest, CharacterSnapshot snapshot, IReadOnlyList<CappedTurnIn> turnIns) => new(
        Catalog(quest),
        new Dictionary<uint, QuestEvaluation>(),
        [],
        new HashSet<uint>(),
        0,
        snapshot.CurrentJob,
        snapshot.JobLevels,
        JobLadder.Empty,
        JobNames,
        ShowPins: false,
        ShowNearbyFeature: false,
        ShowMsq: false,
        ShowJobQuests: false,
        ShowSeasonal: false,
        ShowPlan: false,
        ShowRoute: false,
        CappedTurnIns: turnIns);

    [Fact]
    public void A_quest_at_its_turn_in_step_on_a_capped_job_names_the_job_that_gets_the_exp()
    {
        var turnIns = Find(Aery(), On(Sage, TurnIn, (Sage, 100), (Dragoon, 56)));

        var turnIn = Assert.Single(turnIns);
        Assert.Equal(Target, turnIn.Quest.RowId);
        Assert.Equal(ExpWarning.Capped, turnIn.Advice.Warning);
        Assert.Equal(new JobExp(Dragoon, 56, 560), turnIn.Advice.Best);
    }

    [Fact]
    public void A_job_under_the_cap_gets_the_exp_itself_so_there_is_no_row()
    {
        Assert.Empty(Find(Aery(), On(Dragoon, TurnIn, (Sage, 100), (Dragoon, 56))));
    }

    [Fact]
    public void When_every_job_is_capped_no_other_job_could_take_it_so_there_is_no_row()
    {
        Assert.Empty(Find(Aery(), On(Sage, TurnIn, (Sage, 100), (Dragoon, 100))));

        // A limited job under the cap is never the job to hand in on.
        Assert.Empty(Find(Aery(), On(Sage, TurnIn, (Sage, 100), (BlueMage, 70)), job => job == BlueMage));

        // Nor is a job under the quest's level.
        Assert.Empty(Find(Aery(), On(Sage, TurnIn, (Sage, 100), (Dragoon, 40))));
    }

    [Fact]
    public void Quests_with_no_known_exp_never_have_a_row()
    {
        var capped = On(Sage, TurnIn, (Sage, 100), (Dragoon, 56));
        Assert.Empty(Find(Aery() with { ExpFactor = 0 }, capped));
        Assert.Empty(Find(Aery() with { BeastTribe = 2 }, capped));
        Assert.Empty(Find(Aery() with { Festival = 7 }, capped));
        Assert.Empty(CappedTurnIns.Find(Catalog(Aery()), capped, QuestExpTable.Empty, Context));
    }

    [Fact]
    public void Only_the_turn_in_step_counts_and_a_capture_without_job_or_cap_gives_nothing()
    {
        Assert.Empty(Find(Aery(), On(Sage, 3, (Sage, 100), (Dragoon, 56))));
        Assert.Empty(Find(Aery(), On(Sage, TurnIn, (Sage, 100), (Dragoon, 56)) with { LevelCap = 0 }));
        Assert.Empty(Find(Aery(), On(0, TurnIn, (Sage, 100), (Dragoon, 56))));
        Assert.Empty(Find(Aery(), On(Sage, TurnIn, (Sage, 100), (Dragoon, 56)) with { Accepted = [] }));
    }

    [Fact]
    public void A_quest_the_current_job_cannot_take_is_the_exp_line_s_hand_in_not_a_capped_row()
    {
        // A Dragoon quest while on a capped Sage: the game would not let the Sage hand it in at all.
        var dragoonQuest = Aery() with { ClassJobRequired = Dragoon };
        Assert.Empty(Find(dragoonQuest, On(Sage, TurnIn, (Sage, 100), (Dragoon, 56))));
    }

    [Fact]
    public void The_todo_row_leads_the_overlay_and_names_the_quest_and_the_job_to_switch_to()
    {
        var snapshot = On(Sage, TurnIn, (Sage, 100), (Dragoon, 56));
        var model = TodoList.Build(Inputs(Aery(), snapshot, Find(Aery(), snapshot)));

        var section = Assert.Single(model.Sections);
        Assert.Equal(TodoSection.TurnIn, section.Section);
        var row = Assert.Single(section.Rows);
        Assert.Equal(Target, row.RowId);
        Assert.Equal("Turn in on a job that isn't capped: Into the Aery", row.Name);
        Assert.Equal("DRG Lv 56 · 560 EXP", row.Hint);
        Assert.Equal(QuestState.Accepted, row.State);
        Assert.Equal(TodoRowKind.TurnIn, row.Kind);

        // Not a section the player turns on: with every other section off the model still reads "nothing enabled".
        Assert.Equal(0, model.EnabledSections);
    }

    [Fact]
    public void The_todo_row_names_the_quest_through_the_spoiler_shield()
    {
        var snapshot = On(Sage, TurnIn, (Sage, 100), (Dragoon, 56));
        var masked = Inputs(Aery(), snapshot, Find(Aery(), snapshot)) with
        {
            Names = BlockerNames.Default with { QuestName = static _ => "Hidden quest" },
        };

        var row = Assert.Single(Assert.Single(TodoList.Build(masked).Sections).Rows);
        Assert.Equal("Turn in on a job that isn't capped: Hidden quest", row.Name);
        Assert.DoesNotContain("Into the Aery", row.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void No_capped_turn_in_means_no_section()
    {
        var snapshot = On(Dragoon, TurnIn, (Sage, 100), (Dragoon, 56));
        Assert.True(TodoList.Build(Inputs(Aery(), snapshot, Find(Aery(), snapshot))).IsEmpty);
        Assert.True(TodoList.Build(Inputs(Aery(), snapshot, [])).IsEmpty);
    }

    [Fact]
    public void The_chat_line_speaks_once_per_quest_per_reaching_the_turn_in_step()
    {
        var notice = new CappedTurnInNotice();
        var capped = On(Sage, TurnIn, (Sage, 100), (Dragoon, 56));
        var uncapped = capped with { CurrentJob = Dragoon };

        Assert.Single(notice.Fresh(1, capped, Find(Aery(), capped)));

        // Every later scan while it waits, a switch to Dragoon and back included, says nothing.
        Assert.Empty(notice.Fresh(1, capped, Find(Aery(), capped)));
        Assert.Empty(notice.Fresh(1, uncapped, Find(Aery(), uncapped)));
        Assert.Empty(notice.Fresh(1, capped, Find(Aery(), capped)));

        // Another character's quest is its own.
        Assert.Single(notice.Fresh(2, capped, Find(Aery(), capped)));

        // Once the quest leaves its turn-in step (handed in, abandoned), reaching it again speaks again.
        var gone = capped with { Accepted = [] };
        Assert.Empty(notice.Fresh(1, gone, Find(Aery(), gone)));
        Assert.Single(notice.Fresh(1, capped, Find(Aery(), capped)));

        // Character 1's scan never forgets character 2's quest.
        Assert.Empty(notice.Fresh(2, capped, Find(Aery(), capped)));
    }
}
