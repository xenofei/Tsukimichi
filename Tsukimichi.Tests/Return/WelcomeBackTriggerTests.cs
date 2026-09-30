using Tsukimichi.Core.Return;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Return;

/// <summary>When "Since you were away" opens on its own (P7), with the alt-nag guard, and its per-character sidecar.</summary>
public sealed class WelcomeBackTriggerTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
    private const ulong Main = 100;
    private const ulong Alt = 200;

    private static SnapshotSummary Capture(ulong id, double daysAgo) => new(id, "C" + id, 1, Now.AddDays(-daysAgo), 10);

    private static WelcomeBackDecision Decide(
        ulong id,
        IReadOnlyList<SnapshotSummary> captures,
        WelcomeBackState? state = null,
        int days = WelcomeBackTrigger.DefaultDays,
        bool shown = false) =>
        WelcomeBackTrigger.Decide(id, captures, state ?? WelcomeBackState.Empty, days, shown, Now);

    [Fact]
    public void Shows_after_n_days_when_the_account_was_away()
    {
        Assert.Equal(WelcomeBackDecision.Show, Decide(Main, [Capture(Main, 200)]));
        Assert.Equal(WelcomeBackDecision.Show, Decide(Main, [Capture(Main, 14)]));
        Assert.Equal(WelcomeBackDecision.Show, Decide(Main, [Capture(Main, 200), Capture(Alt, 30)]));
        Assert.Equal(WelcomeBackDecision.None, Decide(Main, [Capture(Main, 13.9)]));
    }

    [Fact]
    public void An_alt_stale_only_because_the_main_was_played_is_left_alone()
    {
        Assert.Equal(WelcomeBackDecision.None, Decide(Alt, [Capture(Main, 1), Capture(Alt, 60)]));
        Assert.False(WelcomeBackTrigger.AccountAway([Capture(Main, 1), Capture(Alt, 60)], TimeSpan.FromDays(14), Now));
        Assert.True(WelcomeBackTrigger.AccountAway([], TimeSpan.FromDays(14), Now));
    }

    [Fact]
    public void Off_quiet_already_shown_this_session_or_for_this_capture_says_nothing()
    {
        var captures = new[] { Capture(Main, 200) };

        Assert.Equal(WelcomeBackDecision.None, Decide(Main, captures, days: 0));
        Assert.Equal(WelcomeBackDecision.None, Decide(Main, captures, shown: true));
        Assert.Equal(WelcomeBackDecision.None, Decide(Main, captures, WelcomeBackState.Empty with { Quiet = true }));
        Assert.Equal(WelcomeBackDecision.None, Decide(Main, captures, WelcomeBackState.Empty with { ShownForUtc = captures[0].TakenUtc }));
        Assert.Equal(WelcomeBackDecision.Show, Decide(Main, captures, WelcomeBackState.Empty with { ShownForUtc = captures[0].TakenUtc.AddDays(-300) }));
    }

    [Fact]
    public void A_character_without_a_capture_is_asked_once_when_the_account_was_away()
    {
        // A fresh install: nobody captured yet.
        Assert.Equal(WelcomeBackDecision.AskPatch, Decide(Main, []));
        Assert.Equal(WelcomeBackDecision.AskPatch, Decide(Alt, [Capture(Main, 90)]));

        // Answered once (a patch or "I'm new"), never again; a new alt while the main is active is not asked.
        Assert.Equal(WelcomeBackDecision.None, Decide(Main, [], WelcomeBackState.Empty with { LastPlayedPatch = "7.2" }));
        Assert.Equal(WelcomeBackDecision.None, Decide(Main, [], WelcomeBackState.Empty with { LastPlayedPatch = WelcomeBackState.NewPlayer }));
        Assert.Equal(WelcomeBackDecision.None, Decide(Alt, [Capture(Main, 2)]));
        Assert.Equal(WelcomeBackDecision.None, Decide(Main, [], days: 0));
    }

    [Fact]
    public void State_round_trips_and_lives_with_the_character_sidecars()
    {
        using var tmp = new TempDir();
        var characters = tmp.File("characters");
        Directory.CreateDirectory(characters);
        var path = WelcomeBackStateFile.PathFor(characters, Main);
        var state = new WelcomeBackState { LastPlayedPatch = "7.2", SeenPatch = "7.55", ShownForUtc = Now, Quiet = true };

        WelcomeBackStateFile.Save(path, state);
        var loaded = WelcomeBackStateFile.Load(path);

        Assert.Equal(state, loaded);
        Assert.True(loaded.Answered);
        Assert.False(loaded.IsNewPlayer);
        Assert.DoesNotContain("answered", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(path, CharacterSidecars.PathsFor(characters, Main));
        Assert.Contains(path, CharacterSidecars.FindAll(characters));

        // The store's listing skips the sidecar ("100.return" is not a content id).
        var store = new JsonSnapshotStore(tmp.Path);
        Assert.Empty(store.List());
        Assert.Empty(store.Warnings);
    }

    [Fact]
    public void State_file_that_is_missing_broken_or_odd_reads_as_a_fresh_start()
    {
        using var tmp = new TempDir();
        var path = WelcomeBackStateFile.PathFor(tmp.Path, Main);
        var warnings = new List<string>();

        Assert.Same(WelcomeBackState.Empty, WelcomeBackStateFile.Load(path, warnings));
        Assert.Empty(warnings);

        File.WriteAllText(path, "{ not json");
        Assert.Same(WelcomeBackState.Empty, WelcomeBackStateFile.Load(path, warnings));
        Assert.Single(warnings);

        File.WriteAllText(path, """{ "lastPlayedPatch": "7.25", "seenPatch": "soon", "quiet": true }""");
        var odd = WelcomeBackStateFile.Load(path);
        Assert.Equal("7.2", odd.LastPlayedPatch); // an answer is a series
        Assert.Equal(string.Empty, odd.SeenPatch);
        Assert.True(odd.Quiet);

        File.WriteAllText(path, """{ "lastPlayedPatch": "new" }""");
        Assert.True(WelcomeBackStateFile.Load(path).IsNewPlayer);
    }
}
