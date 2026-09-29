using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Tests.Evaluation;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Diagnostics;

/// <summary>
/// The lines <c>/tsuki why</c> prints (feature plan v3 P2): Ready says where to go, Blocked and Locked out lead with
/// the Status text and list every requirement the way the diagnostic block does, and a curated quirk closes the list.
/// </summary>
public class WhyTextTests
{
    private const uint PeaceForThanalan = B;
    private const uint LockA = A;

    private static readonly QuestRecord Target = Quest(Fixture.Target, "Brotherhood of Ash") with
    {
        Level = 24,
        PreviousQuests = new Prereq([PeaceForThanalan], JoinKind.All),
        QuestLocks = [LockA],
        Issuer = new Issuer(1005552, "Yadovv Gah", 140, 20, 0f, 0f, 0f),
    };

    private static readonly BlockerNames Names = new()
    {
        // A sidequest section: a prerequisite filed under section 1 would read "after MSQ:".
        Catalog = Catalog(Target, Quest(PeaceForThanalan, "Peace for Thanalan") with { Journal = new JournalRef(3, "Side Quests", 1, "Category", 89, "Genre", 1) }, Quest(LockA, "Lock A")),
        JobAbbreviation = id => id == Paladin ? "PLD" : string.Empty,
    };

    private static QuestEvaluation Ready() => new(
        QuestState.Ready,
        [new RequirementResult(new LevelRequirement(24, 31), true, "24 ≤ 31"), new RequirementResult(new PreviousQuestsRequirement([PeaceForThanalan], JoinKind.All, 1, [PeaceForThanalan]), true, "done")],
        null,
        null,
        null);

    private static QuestEvaluation Blocked()
    {
        var prereq = new RequirementResult(new PreviousQuestsRequirement([PeaceForThanalan], JoinKind.All, 0, []), false, "not done");
        return new(QuestState.Blocked, [new RequirementResult(new LevelRequirement(24, 31), true, "24 ≤ 31"), prereq], prereq, null, null);
    }

    private static QuestEvaluation LockedOut()
    {
        var lockResult = new RequirementResult(new ForeclosureRequirement([LockA], [LockA]), false, "closed");
        return new(QuestState.Foreclosed, [lockResult], lockResult, null, null);
    }

    [Fact]
    public void Ready_says_where_to_go_with_place_and_coordinates()
    {
        var lines = WhyText.Lines(Ready(), Target, Names, place: "Northern Thanalan", coordinates: (23.14f, 14.2f));

        Assert.Equal("Ready — talk to Yadovv Gah in Northern Thanalan (23.1, 14.2)", lines[0]);
        Assert.Equal("  - Level: met (24 ≤ 31)", lines[1]);
        Assert.Equal($"  - PreviousQuests: met ({PeaceForThanalan} Peace for Thanalan: done)", lines[2]);
        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public void Ready_without_a_map_names_the_giver_only_and_without_a_giver_the_state_only()
    {
        Assert.Equal("Ready — talk to Yadovv Gah", WhyText.Headline(Ready(), Target, Names));
        Assert.Equal("Ready", WhyText.Headline(Ready(), Target with { Issuer = null }, Names, place: "Northern Thanalan", coordinates: (1f, 2f)));
    }

    [Fact]
    public void Ready_on_another_job_names_the_job()
    {
        var evaluation = Ready() with { State = QuestState.ReadyOnOtherJob, ReadyOnJob = Paladin };

        Assert.Equal("Ready on another job (PLD) — talk to Yadovv Gah in Northern Thanalan", WhyText.Headline(evaluation, Target, Names, place: "Northern Thanalan"));
    }

    [Fact]
    public void Blocked_leads_with_the_status_text_then_one_line_per_requirement()
    {
        var lines = WhyText.Lines(Blocked(), Target, Names, place: "Northern Thanalan", coordinates: (23.1f, 14.2f));

        Assert.Equal("Blocked · after: Peace for Thanalan", lines[0]);
        Assert.Equal("  - Level: met (24 ≤ 31)", lines[1]);
        Assert.Equal($"  - PreviousQuests: unmet ({PeaceForThanalan} Peace for Thanalan: not done)", lines[2]);
        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public void Locked_out_leads_with_the_status_text_and_names_the_lock()
    {
        var evaluation = LockedOut();
        var lines = WhyText.Lines(evaluation, Target, Names);

        Assert.Equal(BlockerText.StatusText(evaluation, Target, Names), lines[0]);
        Assert.StartsWith("Locked out", lines[0], StringComparison.Ordinal);
        Assert.Equal($"  - Foreclosure: unmet (locks {LockA} Lock A; completed {LockA} Lock A)", lines[1]);
        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void A_quirk_note_closes_the_list_and_an_empty_one_is_left_out()
    {
        var withNote = WhyText.Lines(Blocked(), Target, Names, quirkNote: " Optional once the Zenith is in hand. ");
        Assert.Equal("Note: Optional once the Zenith is in hand.", withNote[^1]);
        Assert.Equal(4, withNote.Count);

        Assert.Equal(3, WhyText.Lines(Blocked(), Target, Names, quirkNote: "  ").Count);
    }

    [Fact]
    public void Without_an_evaluation_only_the_not_checked_line_and_the_note_print()
    {
        var lines = WhyText.Lines(null, Target, Names, quirkNote: "A note.");

        Assert.Equal([WhyText.NoEvaluation, "Note: A note."], lines);
        Assert.StartsWith("Not checked", WhyText.NoEvaluation, StringComparison.Ordinal);
    }

    [Fact]
    public void Requirement_lines_match_the_diagnostic_block()
    {
        var evaluation = Blocked();
        var block = QuestDiagnostic.Compose(new DiagnosticInputs { Quest = Target, Evaluation = evaluation, Names = Names, QuirkNote = "Waived by the game." });

        foreach (var line in WhyText.RequirementLines(evaluation, Names))
        {
            Assert.Contains("\n" + line + "\n", block, StringComparison.Ordinal);
        }

        Assert.Contains("\nquirk: Waived by the game.\n", block, StringComparison.Ordinal);
        Assert.DoesNotContain("quirk:", QuestDiagnostic.Compose(new DiagnosticInputs { Quest = Target, Evaluation = evaluation, Names = Names }), StringComparison.Ordinal);
    }
}
