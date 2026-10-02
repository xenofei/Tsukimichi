using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// A game gate (<c>curated/game_gates.json</c>, <see cref="QuestCatalog.GameGateOf"/>): something the game checks that
/// Tsukimichi cannot read. It is never judged, so it never blocks; it keeps the quest from reading Ready; and its
/// "after" quests are prerequisites like any other.
/// </summary>
public class GameGateTests
{
    private const string Nexus = "a relic weapon nexus equipped";

    private static readonly BlockerNames Names = new()
    {
        JobAbbreviation = id => id switch { Gladiator => "GLA", Conjurer => "CNJ", _ => string.Empty },
    };

    /// <summary>A side quest (journal section 3): the shared factory files quests under the main scenario.</summary>
    private static QuestRecord Side(uint rowId, string name) =>
        Quest(rowId, name) with { Journal = new JournalRef(3, "Sidequests", 1, "Category", 1, "Genre", (int)rowId) };

    /// <summary>A side quest gated by <see cref="Nexus"/>, which cannot be passed before <see cref="A"/>.</summary>
    private static (QuestRecord Quest, QuestCatalog Catalog) Gated(Func<QuestRecord, QuestRecord>? shape = null)
    {
        var quest = Side(Target, "His Dark Materia");
        quest = shape?.Invoke(quest) ?? quest;
        var catalog = QuestCatalog.Build([quest, Side(A, "Mmmmmm, Soulglazed Relics")], null, new Dictionary<uint, QuestGate> { [Target] = new(Nexus, [A]) });
        return (quest, catalog);
    }

    [Fact]
    public void The_after_quests_are_prerequisites_and_block_until_done()
    {
        var (quest, catalog) = Gated();

        Assert.Equal([A], catalog.PrerequisitesOf(quest).QuestIds);
        Assert.Equal([A], catalog.ExtraPrerequisitesOf(Target));
        Assert.Equal(Nexus, catalog.GameGateOf(Target)!.Gate);
        Assert.Null(catalog.GameGateOf(A));

        var blocked = StateResolver.Resolve(quest, Snapshot(), catalog, EvalContext.Default);
        Assert.Equal(QuestState.Blocked, blocked.State);
        Assert.Equal(RequirementKind.PreviousQuests, blocked.NextStep!.Req.Kind);
        Assert.Equal("Blocked · after: Mmmmmm, Soulglazed Relics", BlockerText.StatusText(blocked, quest, Names with { Catalog = catalog }));
    }

    [Fact]
    public void Once_nothing_else_is_missing_the_quest_is_Not_checked_rather_than_Ready()
    {
        var (quest, catalog) = Gated();

        var result = StateResolver.Resolve(quest, Snapshot(A), catalog, EvalContext.Default);

        Assert.Equal(QuestState.Unknown, result.State);
        var gate = Assert.IsType<GameGateRequirement>(result.NextStep!.Req);
        Assert.Equal(Nexus, gate.Gate);
        Assert.False(result.NextStep.Met);
        Assert.Equal("needs a relic weapon nexus equipped, not checked", result.NextStep.Detail);
        Assert.Equal(StateNames.Name(QuestState.Unknown, quest) + " · " + Nexus, BlockerText.StatusText(result, quest, Names with { Catalog = catalog }));
        Assert.Equal("Not checked: " + Nexus, BlockerText.For(result, quest, Names with { Catalog = catalog }));
        Assert.Equal("needs a relic weapon nexus equipped, not checked", RequirementDetail.Text(result.NextStep, Names with { Catalog = catalog }));
        Assert.Equal(new IpcBlocker(IpcBlockerKinds.Unchecked, 0, 0, 0), IpcBlocker.Of(result, catalog, null));
        Assert.Equal("not checked", QuestDiagnostic.Verdict(result.NextStep));
    }

    [Fact]
    public void A_quest_ready_on_another_job_is_Not_checked_too()
    {
        var (quest, catalog) = Gated(q => q with { ClassJobRequired = Conjurer, Level = 10 });
        var snapshot = Snapshot(A) with { JobLevels = Levels((Gladiator, 50), (Conjurer, 20)) };

        var result = StateResolver.Resolve(quest, snapshot, catalog, EvalContext.Default);

        Assert.Equal(QuestState.Unknown, result.State);
        Assert.Null(result.ReadyOnJob);
        Assert.Equal(RequirementKind.GameGate, result.NextStep!.Req.Kind);
    }

    [Fact]
    public void Another_unmet_gate_still_reads_Blocked_by_that_gate()
    {
        var (quest, catalog) = Gated(q => q with { Level = 90 });

        var result = StateResolver.Resolve(quest, Snapshot(A), catalog, EvalContext.Default);

        Assert.Equal(QuestState.Blocked, result.State);
        Assert.Equal(RequirementKind.Level, result.NextStep!.Req.Kind);
    }

    [Fact]
    public void Completed_and_accepted_quests_ignore_the_gate()
    {
        var (quest, catalog) = Gated();

        Assert.Equal(QuestState.Completed, StateResolver.Resolve(quest, Snapshot(Target), catalog, EvalContext.Default).State);
        Assert.Equal(QuestState.Accepted, StateResolver.Resolve(quest, Snapshot(A) with { Accepted = [Accepted(Target)] }, catalog, EvalContext.Default).State);
    }

    [Fact]
    public void A_gate_on_a_row_the_catalog_lacks_or_with_unknown_after_ids_adds_nothing_unknown()
    {
        var quest = Quest(Target);
        var catalog = QuestCatalog.Build([quest], null, new Dictionary<uint, QuestGate> { [Target] = new(Nexus, [A]), [B] = new(Nexus, []) });

        Assert.Empty(catalog.PrerequisitesOf(quest).QuestIds);
        Assert.NotNull(catalog.GameGateOf(Target));
        Assert.Null(catalog.GameGateOf(B));
        Assert.Equal(QuestState.Unknown, StateResolver.Resolve(quest, Snapshot(), catalog, EvalContext.Default).State);
    }
}
