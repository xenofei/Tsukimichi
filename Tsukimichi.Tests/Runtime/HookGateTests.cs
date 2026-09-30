using System.Text.RegularExpressions;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

public sealed class HookGateTests
{
    private const string Tested = "2026.09.15.0000.0000";

    [Fact]
    public void Same_version_is_allowed_without_a_note()
    {
        var decision = HookGate.Decide(Tested, "2026.09.15.0000.0000", enableAnyway: false);

        Assert.Equal(HookGateVerdict.Tested, decision.Verdict);
        Assert.True(decision.Allowed);
        Assert.Null(decision.LogNote);
    }

    [Fact]
    public void Surrounding_whitespace_and_a_trailing_newline_do_not_matter()
    {
        // ffxivgame.ver is read as a file; a newline at its end is common.
        Assert.Equal(HookGateVerdict.Tested, HookGate.Decide(" " + Tested, Tested + "\r\n", enableAnyway: false).Verdict);
    }

    [Fact]
    public void Newer_date_is_paused()
    {
        var decision = HookGate.Decide(Tested, "2026.10.28.0000.0000", enableAnyway: false);

        Assert.Equal(HookGateVerdict.Paused, decision.Verdict);
        Assert.False(decision.Allowed);
        Assert.Contains("paused", decision.LogNote);
    }

    [Fact]
    public void Newer_build_on_the_same_date_is_paused()
    {
        // A hotfix keeps the date and bumps the build.
        Assert.Equal(HookGateVerdict.Paused, HookGate.Decide(Tested, "2026.09.15.0001.0000", enableAnyway: false).Verdict);
        Assert.Equal(HookGateVerdict.Paused, HookGate.Decide(Tested, "2026.09.15.0000.0001", enableAnyway: false).Verdict);
    }

    [Fact]
    public void Newer_version_with_the_override_is_allowed_and_noted()
    {
        var decision = HookGate.Decide(Tested, "2026.10.28.0000.0000", enableAnyway: true);

        Assert.Equal(HookGateVerdict.Overridden, decision.Verdict);
        Assert.True(decision.Allowed);
        Assert.NotNull(decision.LogNote);
    }

    [Fact]
    public void The_override_does_not_change_a_tested_or_older_version()
    {
        Assert.Equal(HookGateVerdict.Tested, HookGate.Decide(Tested, Tested, enableAnyway: true).Verdict);
        Assert.Equal(HookGateVerdict.Older, HookGate.Decide(Tested, "2026.08.01.0000.0000", enableAnyway: true).Verdict);
    }

    [Fact]
    public void Older_version_is_allowed_with_a_note()
    {
        var decision = HookGate.Decide(Tested, "2026.08.01.0000.0000", enableAnyway: false);

        Assert.Equal(HookGateVerdict.Older, decision.Verdict);
        Assert.True(decision.Allowed);
        Assert.Contains("older", decision.LogNote);
    }

    [Fact]
    public void Older_build_on_the_same_date_is_older()
    {
        Assert.Equal(HookGateVerdict.Older, HookGate.Decide("2026.09.15.0002.0000", "2026.09.15.0001.0009", enableAnyway: false).Verdict);
    }

    [Fact]
    public void Date_outranks_build()
    {
        // A later date with a lower build number is still newer.
        Assert.Equal(HookGateVerdict.Paused, HookGate.Decide("2026.09.15.0009.0000", "2026.09.16.0000.0000", enableAnyway: false).Verdict);
        // An earlier date with a higher build number is still older.
        Assert.Equal(HookGateVerdict.Older, HookGate.Decide("2026.09.15.0000.0000", "2026.09.14.9999.9999", enableAnyway: false).Verdict);
    }

    [Fact]
    public void Parts_compare_as_numbers_not_text()
    {
        // "2027.1.2" is newer than "2026.12.31" though it sorts first as text.
        Assert.Equal(HookGateVerdict.Paused, HookGate.Decide("2026.12.31.0000.0000", "2027.1.2.0.0", enableAnyway: false).Verdict);
    }

    [Theory]
    [InlineData("", Tested)]
    [InlineData(Tested, "")]
    [InlineData(null, Tested)]
    [InlineData(Tested, null)]
    [InlineData(Tested, "unknown")]
    [InlineData(Tested, "2026.13.01.0000.0000")]
    [InlineData(Tested, "2026.02.30.0000.0000")]
    [InlineData(Tested, "2026.09")]
    [InlineData(Tested, "2026.09.15.0000.0000.0000")]
    [InlineData(Tested, "2026.09.15.00a0.0000")]
    [InlineData(Tested, "2026..15.0000.0000")]
    [InlineData(Tested, "-2026.09.15.0000.0000")]
    public void Unknown_or_unparseable_versions_are_allowed_with_a_note(string? tested, string? running)
    {
        var decision = HookGate.Decide(tested, running, enableAnyway: false);

        Assert.Equal(HookGateVerdict.Unknown, decision.Verdict);
        Assert.True(decision.Allowed);
        Assert.NotNull(decision.LogNote);
    }

    [Fact]
    public void Game_version_parses_the_ffxivgame_ver_format()
    {
        Assert.True(GameVersion.TryParse("2026.09.15.0000.0000", out var version));
        Assert.Equal(new GameVersion(2026, 9, 15, 0, 0), version);
        Assert.Equal("2026.09.15.0000.0000", version.ToString());
    }

    [Fact]
    public void Game_version_without_build_parts_reads_them_as_zero()
    {
        Assert.True(GameVersion.TryParse("2026.09.15", out var version));
        Assert.Equal(new GameVersion(2026, 9, 15, 0, 0), version);
    }

    [Fact]
    public void Gate_starts_from_its_inputs()
    {
        var gate = new HookGate(Tested, "2026.10.28.0000.0000");

        Assert.True(gate.IsPaused);
        Assert.False(gate.HooksAllowed);
        Assert.Equal(Tested, gate.TestedVersion);
    }

    [Fact]
    public void Gate_without_a_running_version_is_allowed_until_one_is_known()
    {
        var gate = new HookGate(Tested);
        var changes = 0;
        gate.Changed += () => changes++;

        Assert.Equal(HookGateVerdict.Unknown, gate.Decision.Verdict);
        Assert.True(gate.HooksAllowed);

        gate.SetRunningVersion("2026.10.28.0000.0000");

        Assert.True(gate.IsPaused);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Ticking_the_override_raises_changed_and_allows_the_hooks()
    {
        var gate = new HookGate(Tested, "2026.10.28.0000.0000");
        var seen = new List<bool>();
        gate.Changed += () => seen.Add(gate.HooksAllowed);

        gate.SetEnableAnyway(true);
        gate.SetEnableAnyway(false);

        Assert.Equal([true, false], seen);
        Assert.True(gate.IsPaused);
    }

    [Fact]
    public void Changed_is_not_raised_when_the_decision_stays_the_same()
    {
        var gate = new HookGate(Tested, Tested);
        var changes = 0;
        gate.Changed += () => changes++;

        gate.SetEnableAnyway(true);
        gate.SetRunningVersion(Tested);

        Assert.Equal(0, changes);
        Assert.Equal(HookGateVerdict.Tested, gate.Decision.Verdict);
    }

    [Fact]
    public void Repository_csproj_carries_a_parseable_tested_game_version()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "Tsukimichi", "Tsukimichi.csproj"));
        var match = Regex.Match(csproj, "<TsukimichiTestedGameVersion>([^<]*)</TsukimichiTestedGameVersion>");

        Assert.True(match.Success, "Tsukimichi.csproj has no <TsukimichiTestedGameVersion>");
        Assert.True(GameVersion.TryParse(match.Groups[1].Value, out _), $"'{match.Groups[1].Value}' is not a game version like 2026.09.15.0000.0000");
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Tsukimichi.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        Assert.NotNull(dir);
        return dir;
    }
}
