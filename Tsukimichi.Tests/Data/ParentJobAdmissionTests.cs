using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Parent-job admission on the frozen catalog: a class-pinned quest (Lancer) taken on the class's job (Dragoon).
/// The journal's <c>AcceptClassJob</c> is the evidence; without it the quest stays "ready on Lancer".
/// </summary>
public class ParentJobAdmissionTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const byte Lancer = 4;
    private const byte Dragoon = 22;

    private EvalContext Context() => EvalContextBuilder.Build(
        new Dictionary<ushort, FestivalInfo>(),
        fixture.Bundle.Jobs,
        static () => DateTime.UtcNow,
        jobParents: fixture.Bundle.JobParents());

    private QuestRecord LancerQuest()
    {
        var quest = fixture.Bundle.Catalog.All.FirstOrDefault(q => q.ClassJobRequired == Lancer && !q.IsUnlisted && q.Level >= 30);
        Assert.NotNull(quest);
        return quest;
    }

    [Fact]
    public void Sheet_says_dragoon_grew_out_of_lancer()
    {
        var parents = fixture.Bundle.JobParents();
        Assert.Equal(Lancer, parents[Dragoon]);
        Assert.Equal(Lancer, parents[Lancer]);
        Assert.Equal(Lancer, Context().ParentJob!(Dragoon));
    }

    [Fact]
    public void Lancer_pinned_quest_accepted_on_dragoon_resolves_Accepted_not_ReadyOnOtherJob()
    {
        var quest = LancerQuest();
        var snapshot = Fixture.Snapshot(quest.PreviousQuests.QuestIds) with
        {
            CurrentJob = Dragoon,
            JobLevels = Fixture.Levels((Lancer, 30), (Dragoon, 90)),
            Accepted = [Fixture.Accepted(quest.RowId, 1, Dragoon)],
        };

        var result = StateResolver.Resolve(quest, snapshot, fixture.Bundle.Catalog, Context());

        Assert.Equal(QuestState.Accepted, result.State);
        Assert.True(Fixture.Only(result.Requirements, RequirementKind.ClassJob).Met, "Dragoon is admitted to its class's quest once the journal shows it accepted there");
    }

    [Fact]
    public void Lancer_pinned_quest_not_in_the_journal_stays_ready_on_lancer_for_a_dragoon()
    {
        var quest = LancerQuest();
        var snapshot = Fixture.Snapshot(quest.PreviousQuests.QuestIds) with
        {
            CurrentJob = Dragoon,
            JobLevels = Fixture.Levels((Lancer, 90), (Dragoon, 90)),
        };

        var result = StateResolver.Resolve(quest, snapshot, fixture.Bundle.Catalog, Context());

        Assert.Equal(QuestState.ReadyOnOtherJob, result.State);
        Assert.Equal(Lancer, result.ReadyOnJob);
        Assert.False(Fixture.Only(result.Requirements, RequirementKind.ClassJob).Met);
    }
}
