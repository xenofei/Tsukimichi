using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Tests.Data;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Diagnostics;

/// <summary>
/// The diagnostic block over the frozen catalog with the bundle's own names (feature plan v3 T18: "the block
/// serialises the same for a fixture evaluation"): Brotherhood of Ash on a level 31 White Mage who is Recognized with
/// the Amalj'aa but has not done Peace for Thanalan (the society story quests carry no rank gate, so the standing
/// stays out of the inputs line).
/// </summary>
public class QuestDiagnosticFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const byte WhiteMage = 24;
    private const byte Amaljaa = 1;
    private const byte OrderOfTheTwinAdder = 2;
    private const uint BrotherhoodOfAsh = 66754;

    private static readonly DateTime Captured = new(2026, 9, 28, 21, 14, 2, DateTimeKind.Utc);
    private static readonly DateTime Generated = new(2026, 9, 28, 22, 39, 14, DateTimeKind.Utc);

    private static CharacterSnapshot WhiteMage31() => Fixture.Snapshot() with
    {
        ContentId = 4242424242424242,
        Name = "Michiru Tsukikage",
        World = 55,
        TakenUtc = Captured,
        CurrentJob = WhiteMage,
        JobLevels = Fixture.Levels((WhiteMage, 45)),
        GrandCompany = OrderOfTheTwinAdder,
        GcRanks = [0, 0, 5],
        Tribes = new Dictionary<byte, TribeStanding> { [Amaljaa] = new(2, 0) },
        TribeAllowance = 0,
    };

    private DiagnosticInputs Inputs(CharacterSnapshot snapshot, bool withStates)
    {
        var catalog = fixture.Bundle.Catalog;
        var quest = catalog.GetByRowId(BrotherhoodOfAsh)!;
        var context = new EvalContext { ClassJobs = fixture.Bundle.Jobs };
        var states = withStates ? StateResolver.ResolveAll(catalog, snapshot, context) : null;
        return new DiagnosticInputs
        {
            PluginVersion = "0.6.0.0",
            ClientGameVersion = fixture.GameVersion,
            DataGameVersion = fixture.GameVersion,
            DataGeneratedUtc = Generated,
            CuratedRevision = "573d225",
            Quest = quest,
            Evaluation = states is not null ? states[BrotherhoodOfAsh] : StateResolver.Resolve(quest, snapshot, catalog, context),
            Names = fixture.Bundle.BlockerNames(),
            States = states,
            Snapshot = snapshot,
            Context = context,
            IsLive = true,
        };
    }

    [Fact]
    public void Brotherhood_of_Ash_serialises_the_same_block_for_the_fixture_evaluation()
    {
        var block = QuestDiagnostic.Compose(Inputs(WhiteMage31(), withStates: true));

        var expected = string.Join('\n',
        [
            "```tsukimichi-diagnostic",
            "plugin: 0.6.0.0",
            $"game: {fixture.GameVersion} (client) / {fixture.GameVersion} (data)",
            "data: unique_quests generated 2026-09-28T22:39:14Z, curated 573d225, schema 1",
            "quest: 66754 \"Brotherhood of Ash\" (genre 40, lvl 43 / display 43, filing n/a)",
            "state: Blocked · after: Peace for Thanalan",
            "requirements:",
            "  - ClassJob: met (category 142 Any Disciple of War or Magic (excluding limited jobs), you are WHM)",
            "  - Level: met (43 ≤ 45)",
            "  - PreviousQuests: unmet (66753 Peace for Thanalan: not done)",
            // No rank gate on the society story quests, so no society standing among the inputs: only what was read.
            "inputs: job WHM 45, msq 65621",
            "captured: 2026-09-28T21:14:02Z live",
            "```",
        ]);
        Assert.Equal(expected, block);

        // Composed twice, the same string; and nothing identifying the character is in it.
        Assert.Equal(block, QuestDiagnostic.Compose(Inputs(WhiteMage31(), withStates: true)));
        Assert.DoesNotContain("4242424242424242", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Michiru", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Tsukikage", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_every_state_the_main_scenario_position_is_left_out()
    {
        var block = QuestDiagnostic.Compose(Inputs(WhiteMage31(), withStates: false));

        Assert.Contains("\nstate: Blocked · after: Peace for Thanalan\n", block, StringComparison.Ordinal);
        Assert.Contains("\ninputs: job WHM 45\n", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Once_the_prerequisite_is_done_the_quest_is_Ready_and_the_lines_say_met()
    {
        var snapshot = WhiteMage31() with { CompletedBits = Fixture.Bits(66753) };
        var block = QuestDiagnostic.Compose(Inputs(snapshot, withStates: false));

        Assert.Contains("\nstate: Ready\n", block, StringComparison.Ordinal);
        Assert.Contains("  - PreviousQuests: met (66753 Peace for Thanalan: done)\n", block, StringComparison.Ordinal);
    }
}
