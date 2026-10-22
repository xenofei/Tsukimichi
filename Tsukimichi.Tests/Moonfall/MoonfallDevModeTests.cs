using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Text;
using static Tsukimichi.Tests.Moonfall.MoonfallTestKit;

namespace Tsukimichi.Tests.Moonfall;

// Developer mode: remove after all 115 Moonfall levels ship (delete this file with it).
/// <summary>Moonfall's hidden developer mode (<see cref="MoonfallModes.UnlockAll"/>): everything reached, nothing recorded.</summary>
public sealed class MoonfallDevModeTests
{
    /// <summary>Campaigns where every Adventure id has a level (the shipped base-01, under each id), so a test can walk all 115.</summary>
    private static MoonfallCampaigns Full()
    {
        var shape = MoonfallCampaigns.LoadBuiltIn().Base.Levels[0];
        MoonfallCampaign Make(MoonfallCampaignKind kind) =>
            new(kind, Enumerable.Range(0, MoonfallStages.LevelCount(kind)).Select(i => shape with { Id = MoonfallStages.LevelId(kind, i) }).ToList());
        return new MoonfallCampaigns(Make(MoonfallCampaignKind.Base), Make(MoonfallCampaignKind.Expansion), []);
    }

    private static MoonfallModes Modes(MoonfallProgress progress, MoonfallCampaigns campaigns, MoonfallStory? story = null) =>
        new(campaigns, progress, story ?? MoonfallStory.Everyone, MoonfallChallenges.LoadBuiltIn().Challenges);

    [Fact]
    public void Unlock_all_reaches_every_level_stage_and_companion_and_opens_the_shield()
    {
        var campaigns = Full();
        var story = new MoonfallStory(static _ => false);
        var shut = new MoonfallShield(static _ => true, static zone => zone, static () => 1);

        // A fresh player has one level, one open stage and no companion the story has not introduced.
        var fresh = new MoonfallModes(campaigns, new MoonfallProgress(), story, []);
        Assert.Single(fresh.QuickPlayLevels());
        Assert.Empty(fresh.QuickPlayCompanions());

        var modes = new MoonfallModes(campaigns, new MoonfallProgress(), story, []) { Shield = shut };
        modes.UnlockAll = true;
        Assert.Equal(MoonfallStages.BaseLevels + MoonfallStages.ExpansionLevels, modes.QuickPlayLevels().Count);
        Assert.All(modes.QuickPlayLevels(), static slot => Assert.True(slot.Reached));
        foreach (var campaign in (ReadOnlySpan<MoonfallCampaignKind>)[MoonfallCampaignKind.Base, MoonfallCampaignKind.Expansion])
        {
            Assert.True(modes.CampaignOpen(campaign));
            Assert.All(modes.Stages(campaign), static view => Assert.True(view.State is MoonfallStageState.Open or MoonfallStageState.Done));
            Assert.All(MoonfallStages.Of(campaign), stage => Assert.True(modes.StageReached(stage)));
        }

        Assert.All(MoonfallCompanions.All, info =>
        {
            Assert.True(modes.CompanionReached(info.Companion));
            Assert.Equal(MoonfallCompanionState.Available, modes.CompanionState(info.Companion));
        });
        Assert.Same(MoonfallShield.Open, modes.Shield);
        Assert.NotNull(modes.QuickPlay("expansion-60", MoonfallCompanion.Moogle));

        // Off again: the locks and the shield are back.
        modes.UnlockAll = false;
        Assert.Same(shut, modes.Shield);
        Assert.True(modes.QuickPlayLevels().Count < MoonfallStages.BaseLevels);
    }

    [Fact]
    public void A_level_started_in_developer_mode_writes_no_progress_whatever_the_mode_is_when_it_ends()
    {
        // One orange straight below the shot: the first shot wins.
        var level = Board(Orange(400, 300)) with { Id = "base-01" };
        var plain = new MoonfallStart(MoonfallMode.Adventure, "base-01", level, 1, MoonfallCompanion.None, MoonfallRules.BallsPerLevel, 1);
        var campaigns = Full();

        MoonfallLevelResult Win(MoonfallModes modes, MoonfallStart start)
        {
            var game = start.Create(3);
            PlayOut(game, [0]);
            Assert.Equal(MoonfallPhase.Won, game.Phase);
            return modes.FinishLevel(start, game);
        }

        // Started with the mode off, the win is recorded (the control: the test would pass vacuously if the level could not be won),
        // even when the mode is on by the time it ends.
        var recorded = new MoonfallProgress();
        Assert.True(Win(new MoonfallModes(campaigns, recorded, MoonfallStory.Everyone, []) { UnlockAll = true }, plain).Won);
        Assert.Equal(1, recorded.BaseCleared);

        // Started in the mode (the modes stamp the start), nothing is recorded, even when the mode is off by the time it ends.
        var progress = new MoonfallProgress();
        var modes = new MoonfallModes(campaigns, progress, MoonfallStory.Everyone, []) { UnlockAll = true };
        Assert.False(Modes(progress, campaigns).Adventure(MoonfallCampaignKind.Base, 0)!.Developer);
        Assert.True(modes.Adventure(MoonfallCampaignKind.Base, 0)!.Developer);
        Assert.True(modes.QuickPlay("base-01", MoonfallCompanion.None)!.Developer);
        modes.UnlockAll = false;
        var result = Win(modes, plain with { Developer = true });
        Assert.True(result.Won);
        Assert.True(result.Score > 0);
        Assert.Empty(progress.Levels);
        Assert.Equal(0, progress.BaseCleared);
        Assert.Equal(0, progress.Best("base-01"));
        Assert.Equal(0, progress.Reach(MoonfallCampaignKind.Base));
    }

    [Fact]
    public void Unlock_all_opens_the_challenges_and_a_challenge_run_started_in_it_records_nothing()
    {
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        var progress = new MoonfallProgress();
        var modes = new MoonfallModes(campaigns, progress, MoonfallStory.Everyone, MoonfallChallenges.LoadBuiltIn().Challenges);
        Assert.False(modes.ChallengesOpen);
        Assert.DoesNotContain(modes.ChallengeList(), static c => c.State == MoonfallChallengeState.Open);
        modes.UnlockAll = true;
        Assert.True(modes.ChallengesOpen);
        var playable = modes.ChallengeList().Where(static c => c.State == MoonfallChallengeState.Open).ToList();
        Assert.NotEmpty(playable);
        var run = modes.StartChallenge(playable[0].Challenge.Id, MoonfallCompanion.None, 1);
        Assert.NotNull(run);
        Assert.True(run.Developer);
    }

    [Fact]
    public void The_dev_command_is_only_moonfall_dev()
    {
        Assert.True(CommandLine.RunsMoonfallDev(CommandLine.Parse("moonfall dev")));
        Assert.True(CommandLine.RunsMoonfallDev(CommandLine.Parse("moonfall DEV")));
        Assert.False(CommandLine.RunsMoonfallDev(CommandLine.Parse("moonfall")));
        Assert.False(CommandLine.RunsMoonfallDev(CommandLine.Parse("moonfall developer")));
        Assert.False(CommandLine.RunsMoonfall(CommandLine.Parse("moonfall dev")));
    }
}
