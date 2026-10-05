using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Text;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>Progress per account, the campaigns' unlocks, the pause and <c>/tsuki moonfall</c> (plan v9 G9).</summary>
public sealed class MoonfallProgressTests
{
    [Fact]
    public void Progress_lives_in_the_shared_user_folder()
    {
        var paths = new PluginPaths(Path.Combine("C:", "config"), Path.Combine("C:", "plugin"));
        Assert.Equal(Path.Combine(paths.UserDir, "moonfall.json"), MoonfallProgress.PathFor(paths));
    }

    [Fact]
    public void A_missing_file_is_a_fresh_start_and_a_record_only_moves_forward()
    {
        using var dir = new TempDir();
        var path = dir.File("user/moonfall.json");
        var fresh = MoonfallProgress.Load(path);
        Assert.Equal(0, fresh.BaseCleared);
        Assert.Equal(0, fresh.ExpansionCleared);

        Assert.Equal(3, MoonfallProgress.Record(path, MoonfallCampaignKind.Base, 3).BaseCleared);
        Assert.Equal(3, MoonfallProgress.Record(path, MoonfallCampaignKind.Base, 1).BaseCleared);
        Assert.Equal(2, MoonfallProgress.Record(path, MoonfallCampaignKind.Expansion, 2).ExpansionCleared);
        var read = MoonfallProgress.Load(path);
        Assert.Equal(3, read.BaseCleared);
        Assert.Equal(2, read.ExpansionCleared);
    }

    [Fact]
    public void Another_clients_further_progress_is_kept()
    {
        using var dir = new TempDir();
        var path = dir.File("user/moonfall.json");
        Directory.CreateDirectory(dir.File("user"));
        File.WriteAllText(path, """{ "version": 1, "baseCleared": 5, "expansionCleared": 1 }""");
        var merged = MoonfallProgress.Record(path, MoonfallCampaignKind.Base, 2);
        Assert.Equal(5, merged.BaseCleared);
        Assert.Equal(1, merged.ExpansionCleared);
    }

    [Fact]
    public void A_corrupt_file_is_set_aside_and_reported()
    {
        using var dir = new TempDir();
        var path = dir.File("moonfall.json");
        File.WriteAllText(path, "{ not json");
        var warnings = new List<string>();
        var progress = MoonfallProgress.Load(path, warnings);
        Assert.Equal(0, progress.BaseCleared);
        Assert.Single(warnings);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Negative_counts_read_as_none()
    {
        using var dir = new TempDir();
        var path = dir.File("moonfall.json");
        File.WriteAllText(path, """{ "baseCleared": -4 }""");
        Assert.Equal(0, MoonfallProgress.Load(path).BaseCleared);
    }

    [Fact]
    public void The_base_campaign_opens_level_by_level_and_the_expansion_after_it()
    {
        var level = MoonfallCampaigns.LoadBuiltIn().Base.Levels[0];
        var campaigns = new MoonfallCampaigns(
            new MoonfallCampaign(MoonfallCampaignKind.Base, [level, level, level]),
            new MoonfallCampaign(MoonfallCampaignKind.Expansion, [level, level]),
            []);
        var progress = new MoonfallProgress();
        Assert.Equal(1, campaigns.Playable(MoonfallCampaignKind.Base, progress));
        Assert.Equal(0, campaigns.Playable(MoonfallCampaignKind.Expansion, progress));
        Assert.False(campaigns.ExpansionOpen(progress));

        progress.BaseCleared = 2;
        Assert.Equal(3, campaigns.Playable(MoonfallCampaignKind.Base, progress));
        Assert.False(campaigns.ExpansionOpen(progress));

        progress.BaseCleared = 3;
        Assert.Equal(3, campaigns.Playable(MoonfallCampaignKind.Base, progress));
        Assert.True(campaigns.ExpansionOpen(progress));
        Assert.Equal(1, campaigns.Playable(MoonfallCampaignKind.Expansion, progress));

        // An expansion with no levels yet never opens.
        var baseOnly = new MoonfallCampaigns(campaigns.Base, new MoonfallCampaign(MoonfallCampaignKind.Expansion, []), []);
        Assert.False(baseOnly.ExpansionOpen(progress));
    }

    // ---- The pause ----

    [Fact]
    public void Combat_pauses_and_the_board_waits_for_a_click_after_it_ends()
    {
        var pause = new MoonfallPauseState();
        pause.Update(MoonfallPauseReason.None);
        Assert.False(pause.Paused);

        pause.Update(MoonfallPauseReason.Combat);
        Assert.True(pause.Paused);
        Assert.Equal(MoonfallPauseReason.Combat, pause.Shown);
        Assert.False(pause.TryResume());

        pause.Update(MoonfallPauseReason.None);
        Assert.True(pause.Paused);
        Assert.True(pause.AwaitingResume);
        Assert.True(pause.TryResume());
        Assert.False(pause.Paused);
    }

    [Fact]
    public void The_named_cause_is_the_most_pressing_one()
    {
        var pause = new MoonfallPauseState();
        pause.Pause(MoonfallPauseReason.Player);
        pause.Update(MoonfallPauseReason.Unfocused | MoonfallPauseReason.Duty);
        Assert.Equal(MoonfallPauseReason.Duty, pause.Shown);
        pause.Update(MoonfallPauseReason.Unfocused);
        Assert.Equal(MoonfallPauseReason.Unfocused, pause.Shown);
        pause.Update(MoonfallPauseReason.None);
        Assert.Equal(MoonfallPauseReason.Player, pause.Shown);
        Assert.True(pause.TryResume());
        Assert.Equal(MoonfallPauseReason.None, pause.Shown);
    }

    [Fact]
    public void Reopening_the_window_waits_for_a_click()
    {
        var pause = new MoonfallPauseState();
        pause.Pause(MoonfallPauseReason.Reopened);
        Assert.True(pause.Paused);
        Assert.Equal(MoonfallPauseReason.Reopened, pause.Shown);
        Assert.True(pause.TryResume());
    }

    // ---- /tsuki moonfall ----

    [Theory]
    [InlineData("moonfall")]
    [InlineData("MOONFALL")]
    [InlineData("  moonfall ")]
    public void Moonfall_alone_opens_or_closes_the_game(string line)
    {
        var parsed = CommandLine.Parse(line);
        Assert.Equal(Subcommand.Moonfall, parsed.Kind);
        Assert.True(CommandLine.RunsMoonfall(parsed));
        Assert.Contains("moonfall", CommandLine.ListedWords);
    }

    [Theory]
    [InlineData("moonfall over Ishgard")]
    [InlineData("moonfalls")]
    [InlineData("moonlit")]
    [InlineData("")]
    public void Anything_else_is_not_the_game(string line)
    {
        Assert.False(CommandLine.RunsMoonfall(CommandLine.Parse(line)));
    }

    [Fact]
    public void A_near_miss_of_moonfall_suggests_it()
    {
        Assert.Equal("moonfall", CommandLine.DidYouMean("moonfal"));
        Assert.Equal("moonfall", CommandLine.DidYouMean("monfall"));
        Assert.Equal("moonlit", CommandLine.DidYouMean("moonlt"));
    }
}
