using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Rewards;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// 1.19.0 "Right answers" (feature plan v7, C8 and C9) on the game's own data: the frozen catalog's class, job and
/// role quests, its allied society dailies and main scenario, and (with game files) the live ParamGrow rows under
/// Quest Sync.
/// </summary>
public sealed class RightAnswersDataTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const byte Gladiator = 1;
    private const byte Marauder = 3;
    private const byte Paladin = 19;
    private const byte Warrior = 21;

    private EvalContext Context() => EvalContextBuilder.Build(
        fixture.Curated.Festivals,
        fixture.Bundle.Jobs,
        static () => DateTime.UtcNow,
        jobParents: fixture.Bundle.JobParents(),
        jobRoles: fixture.Bundle.JobRoles());

    private static CharacterSnapshot Character(byte current, params (byte Job, short Level)[] levels) => new()
    {
        ContentId = 1,
        CurrentJob = current,
        LevelCap = 100,
        AchievementsLoaded = true,
        JobLevels = levels.ToDictionary(l => l.Job, l => l.Level),
    };

    [Fact]
    public void Every_paladin_quest_offers_a_switch_from_warrior_to_a_paladin_gearset_and_names_paladin_for_its_exp()
    {
        var context = Context();
        var quests = fixture.Bundle.Catalog.All.Where(q => q.ClassJobRequired == Paladin && !q.IsRemoved).ToList();
        Assert.NotEmpty(quests);

        var warrior = Character(Warrior, (Marauder, 100), (Warrior, 100), (Gladiator, 100), (Paladin, 100));
        GearsetInfo[] gearsets = [new(0, Warrior, 700, "WAR"), new(1, Paladin, 690, "PLD")];
        foreach (var quest in quests)
        {
            Assert.True(GearsetChoice.Needed(quest, warrior, context, QuestState.ReadyOnOtherJob), quest.Name);
            Assert.Equal(1, GearsetChoice.Pick(gearsets, quest, warrior, context)?.Id);
        }

        // A quest any job takes never offers the switch.
        var anyJob = fixture.Bundle.Catalog.All.First(q => q.ClassJobRequired == 0 && fixture.Bundle.AdmitsEveryJob(q.ClassJobCategory) && !q.IsRemoved);
        Assert.False(GearsetChoice.Needed(anyJob, warrior, context, QuestState.Ready));
    }

    [Fact]
    public void Gladiator_s_own_quests_are_handed_in_on_gladiator_not_paladin()
    {
        var context = Context();
        var table = QuestExpTable.From(Enumerable.Range(1, 100).Select(l => (l, (uint)l, 100u)));
        var gladiatorQuest = fixture.Bundle.Catalog.All.First(q => q.ClassJobRequired == Gladiator && q.ExpFactor > 0 && q.BeastTribe == 0 && q.Festival == 0);
        var onPaladin = Character(Paladin, (Gladiator, 60), (Paladin, 60));

        var advice = ExpAdvisor.Advise(gladiatorQuest, onPaladin, table, context);

        Assert.NotNull(advice);
        Assert.Equal(ExpWarning.NeedsJob, advice.Warning);
        Assert.Equal(Gladiator, advice.Best?.Job);
    }

    [Fact]
    public void The_journal_counts_quests_but_not_allied_society_dailies_and_never_offers_to_drop_the_main_scenario()
    {
        var catalog = fixture.Bundle.Catalog;
        var daily = catalog.All.First(q => q.IsAlliedSocietyDaily);
        var story = catalog.All.First(q => Core.Query.FeaturePresets.IsMainScenario(q) && q.Issuer is not null && !q.IsRemoved);
        var side = catalog.All.First(q => !Core.Query.FeaturePresets.IsMainScenario(q) && !q.IsRepeatable && q.Festival == 0
            && q.Issuer is { Name.Length: > 0 } && !q.IsRemoved && !q.IsUnlisted);
        var snapshot = Character(Warrior, (Warrior, 90)) with
        {
            Accepted = [new(daily.QuestId, 0), new(story.QuestId, 1), new(side.QuestId, 1)],
        };

        Assert.Equal(new JournalSlots(2, JournalSlots.GameCap), JournalSlots.Of(snapshot, catalog));
        var rows = MakeRoom.Rank(snapshot, catalog);
        Assert.Equal([RoomAdvice.SafeToDrop, RoomAdvice.Keep], rows.Select(r => r.Advice));
        Assert.Equal(side.QuestId, rows[0].Entry.QuestId);
        Assert.Equal(story.QuestId, rows[1].Entry.QuestId);
    }

    [GameDataFact]
    public void Under_the_live_param_grow_a_quest_sync_quest_pays_by_the_job_s_level_and_nothing_at_the_cap()
    {
        using var game = new GameDataFixture();
        var table = game.Bundle.ExpTable;
        var cry = game.Bundle.Catalog.GetByRowId(68879u)!; // "A Cry for Help": 54,000–57,240 on the wiki
        Assert.Equal(54_000UL, QuestExp.ForLevel(cry, table, cry.DisplayLevel, 100));
        Assert.Equal(57_240UL, QuestExp.ForLevel(cry, table, 100 - 1, 100));
        Assert.Equal(0UL, QuestExp.ForLevel(cry, table, 100, 100));
    }
}
