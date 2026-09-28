using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>Job ladders over the real Quest, ClassJob and ClassJobCategory sheets.</summary>
public class JobLadderDataTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    private const uint GladiatorGenre = 156;
    private const byte Gladiator = 1;
    private const byte Paladin = 19;
    private const byte DarkKnight = 32;
    private const uint PaladinsPledge = 66591;
    private const uint OurEnd = 67589;

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    [GameDataFact]
    public void Paladin_ladder_opens_with_the_gladiator_quests_and_includes_the_pledge()
    {
        var ladder = fixture.Bundle.BuildJobLadder();
        var paladin = ladder.ForJob(Paladin);
        Assert.NotNull(paladin);

        var pledgeAt = paladin.QuestRowIds.ToList().IndexOf(PaladinsPledge);
        Assert.True(pledgeAt > 0, "Paladin's Pledge is on the Paladin ladder after the class quests");
        Assert.Equal("Paladin's Pledge", Catalog.GetByRowId(PaladinsPledge)!.Name);
        for (var i = 0; i < pledgeAt; i++)
        {
            var quest = Catalog.GetByRowId(paladin.QuestRowIds[i])!;
            Assert.Equal(GladiatorGenre, quest.Journal.GenreId);
            Assert.True(quest.Level <= 30, $"{quest.Name} is a class quest above level 30");
        }

        // Every Gladiator quest on the class's own ladder comes first on the job's, in the same order.
        var gladiator = ladder.ForJob(Gladiator);
        Assert.NotNull(gladiator);
        Assert.Equal(gladiator.QuestRowIds, paladin.QuestRowIds.Take(gladiator.QuestRowIds.Count));
        Assert.True(paladin.QuestRowIds.Count >= 25, $"expected the Gladiator and Paladin lines, found {paladin.QuestRowIds.Count}");

        // Levels never fall along the ladder.
        var levels = paladin.QuestRowIds.Select(id => (int)Catalog.GetByRowId(id)!.Level).ToArray();
        Assert.Equal(levels.OrderBy(l => l), levels);
    }

    [GameDataFact]
    public void Dark_knight_ladder_has_no_class_quests_and_opens_with_its_unlock_quest()
    {
        var ladder = fixture.Bundle.BuildJobLadder();
        var darkKnight = ladder.ForJob(DarkKnight);
        Assert.NotNull(darkKnight);
        foreach (var id in darkKnight.QuestRowIds.Take(3))
        {
            var quest = Catalog.GetByRowId(id)!;
            output.WriteLine($"{id} {quest.Name} Lv {quest.Level}");
        }

        // "Our End" is a level-50 quest in the sheet (any combat job at 50) yet opens the line.
        Assert.Equal(OurEnd, darkKnight.QuestRowIds[0]);
        Assert.Equal(50, Catalog.GetByRowId(OurEnd)!.Level);
        Assert.Equal("Our End", Catalog.GetByRowId(OurEnd)!.Name);
        Assert.All(darkKnight.QuestRowIds, id =>
        {
            var quest = Catalog.GetByRowId(id)!;
            Assert.True(quest.Level >= 30, $"{quest.Name} (Lv {quest.Level}) is below the job's start");
            Assert.True(quest.ClassJobRequired is 0 or DarkKnight, $"{quest.Name} requires job {quest.ClassJobRequired}");
        });
        Assert.True(darkKnight.QuestRowIds.Count >= 18, $"expected the Dark Knight line, found {darkKnight.QuestRowIds.Count}");
    }

    [GameDataFact]
    public void Most_jobs_have_a_ladder_and_every_role_has_role_quests()
    {
        var ladder = fixture.Bundle.BuildJobLadder();
        foreach (var entry in ladder.Jobs)
        {
            output.WriteLine($"{entry.Job.Name}: {entry.QuestRowIds.Count} quests");
        }

        Assert.True(ladder.Jobs.Count >= 20, $"expected at least 20 jobs with a ladder, found {ladder.Jobs.Count}");
        Assert.All(ladder.Jobs, e => Assert.Equal(e.QuestRowIds.Distinct().Count(), e.QuestRowIds.Count));

        foreach (var role in Enum.GetValues<JobRole>())
        {
            var quests = ladder.RoleLadder(role);
            output.WriteLine($"{role}: {quests.Count} role quests");
            Assert.True(quests.Count >= 18, $"{role} has only {quests.Count} role quests");
            Assert.All(quests, id => Assert.True(JobLadder.IsRoleQuest(Catalog.GetByRowId(id)!)));
        }

        // Shadowbringers physical DPS quests sit on both the melee and the physical ranged ladder.
        var shared = ladder.RoleLadder(JobRole.Melee).Intersect(ladder.RoleLadder(JobRole.PhysicalRanged)).Count();
        Assert.True(shared >= 6, $"expected the shared Shadowbringers physical DPS line, found {shared}");

        Assert.Equal(JobRole.Tank, ladder.RoleOf(Paladin));
        Assert.Equal(JobRole.MagicalRanged, ladder.RoleOf(25));
        Assert.Equal(JobRole.PhysicalRanged, ladder.RoleOf(23));
        Assert.Null(ladder.RoleOf(8));
    }
}
