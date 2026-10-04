using Lumina.Data;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Companions;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Data;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Companions;

/// <summary>
/// The duty guard (plan v7, 1.18.0, A3) against the game data: which duties a quest's script names
/// (<see cref="QuestScriptDuties.NamedRuns"/>), which of them an NPC party can run, and what the guard does at those
/// quests' duty steps.
/// </summary>
public sealed class DutyGuardSheetsTests(DutyRunFixture fixture, ITestOutputHelper output) : IClassFixture<DutyRunFixture>
{
    private const string Duty = DutyGuard.DutyInteraction;

    private IReadOnlyList<DutyRunInfo> Runs(uint questRowId) =>
        QuestScriptDuties.NamedRuns(fixture.Game.Excel, Language.English, questRowId, fixture.Index);

    private string QuestName(uint questRowId) =>
        fixture.Game.Excel.GetSheet<Quest>(Language.English).GetRow(questRowId).Name.ExtractText();

    [GameDataFact]
    public void Duty_Support_and_Trust_duties_are_NPC_runnable_and_the_others_are_not()
    {
        Assert.True(DutyGuard.NpcRunnable(fixture.Index.ByCondition(4)!)); // Sastasha: Duty Support
        Assert.True(DutyGuard.NpcRunnable(fixture.Index.ByCondition(676)!)); // Holminster Switch: Duty Support and Trust
        Assert.False(DutyGuard.NpcRunnable(fixture.Index.ByCondition(20)!)); // Brayflox's Longstop (Hard)
        Assert.False(DutyGuard.NpcRunnable(fixture.Index.ByCondition(796)!)); // the Final Day (trial)
        Assert.False(DutyGuard.NpcRunnable(fixture.Index.ByCondition(111)!)); // the World of Darkness (alliance raid)
    }

    [GameDataFact]
    public void A_main_scenario_dungeon_step_runs_with_Duty_Support()
    {
        // The Burden of Knowledge names the Qitana Ravel, which Duty Support and Trust both list.
        Assert.Equal("The Burden of Knowledge", QuestName(68876));
        var runs = Runs(68876);
        Assert.Equal([651u], runs.Select(static duty => duty.ContentFinderConditionId));
        Assert.Equal(DutyGuardVerdict.None, DutyGuard.Decide(DutyGuardMode.Stop, Duty, runs));
    }

    [GameDataFact]
    public void A_main_scenario_trial_step_is_stopped()
    {
        // The Martyr names the Dark Inside, a trial no NPC party runs.
        Assert.Equal("The Martyr", QuestName(69934));
        var verdict = DutyGuard.Decide(DutyGuardMode.Stop, Duty, Runs(69934));
        Assert.Equal(DutyGuardAction.Stop, verdict.Action);
        Assert.True(verdict.Certain);
        Assert.Equal(802u, verdict.Duty!.ContentFinderConditionId);
        Assert.Equal("the Dark Inside", verdict.Duty.Name);
    }

    [GameDataFact]
    public void An_expansion_finale_is_narrowed_by_what_is_cleared()
    {
        // Endwalker names the Dead Ends (Duty Support) and the Final Day (other players).
        Assert.Equal("Endwalker", QuestName(70000));
        var runs = Runs(70000);
        Assert.Equal([792u, 796u], runs.Select(static duty => duty.ContentFinderConditionId));

        var first = DutyGuard.Decide(DutyGuardMode.Stop, Duty, runs, static _ => false);
        Assert.Equal(DutyGuardAction.Stop, first.Action);
        Assert.False(first.Certain);
        Assert.Equal(796u, first.Duty!.ContentFinderConditionId);

        var dungeonDone = DutyGuard.Decide(DutyGuardMode.Stop, Duty, runs, static duty => duty.ContentFinderConditionId == 792);
        Assert.True(dungeonDone.Certain);
        Assert.Equal(796u, dungeonDone.Duty!.ContentFinderConditionId);
    }

    [GameDataFact]
    public void A_quest_without_duties_and_an_unknown_row_name_none()
    {
        Assert.Empty(Runs(65564)); // Close to Home (Gridania): no duty in its script
        Assert.Empty(Runs(1));
        Assert.Empty(Runs(uint.MaxValue));
    }

    /// <summary>
    /// The guard must not get in the way of the main scenario's dungeons: every dungeon a main scenario quest's script
    /// names is one Duty Support or Trust runs. Its trials and raids (journal section 0, 1) are listed for the log.
    /// </summary>
    [GameDataFact]
    public void Every_main_scenario_dungeon_has_an_NPC_party()
    {
        var withPlayers = new List<string>();
        var dungeonsWithPlayers = new List<string>();
        var quests = 0;
        foreach (var quest in fixture.Game.Excel.GetSheet<Quest>(Language.English))
        {
            var section = quest.JournalGenre.ValueNullable?.JournalCategory.ValueNullable?.JournalSection.RowId;
            if (section is not (0 or 1))
            {
                continue;
            }

            var runs = Runs(quest.RowId);
            if (runs.Count == 0)
            {
                continue;
            }

            quests++;
            foreach (var duty in runs)
            {
                if (DutyGuard.NpcRunnable(duty))
                {
                    continue;
                }

                var line = $"{quest.RowId} {quest.Name.ExtractText()}: {duty.Name} (content type {duty.ContentTypeId})";
                withPlayers.Add(line);
                if (duty.ContentTypeId == DutyRunInfo.Dungeons)
                {
                    dungeonsWithPlayers.Add(line);
                }
            }
        }

        output.WriteLine($"{quests} main scenario quests name a duty; {withPlayers.Count} of their duties need other players:");
        withPlayers.ForEach(output.WriteLine);
        Assert.True(quests > 50, $"only {quests} main scenario quests name a duty");
        Assert.Empty(dungeonsWithPlayers);
    }
}
