using Tsukimichi.Core.Updates;

namespace Tsukimichi.Tests.Updates;

/// <summary>
/// The update check's rules (plan v8 U1; spec-1.22 U1): when Dalamud is asked, which answers make the note show, Later,
/// and a newer version after Later. Tsukimichi asks Dalamud (<c>CheckForUpdateAsync</c>); nothing here goes online.
/// </summary>
public sealed class UpdateStateTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void No_newer_version_reads_none()
    {
        // Nothing offered, the running version again, an older one, or a version that does not parse.
        foreach (var found in new string?[] { null, "1.22.0", "1.22.0.0", "1.21.0", "not a version" })
        {
            var state = UpdateRules.Answer("1.22.0", found, "notes", dismissed: string.Empty, Now);
            Assert.Equal(UpdateStatus.None, state.Status);
            Assert.Null(state.Available);
            Assert.False(state.ShowsNote);
            Assert.False(state.HasUpdate);
            Assert.Equal(Now, state.CheckedUtc);
        }

        Assert.Equal(UpdateStatus.None, UpdateState.Unknown.Status);
        Assert.Null(UpdateState.Unknown.CheckedUtc);
    }

    [Fact]
    public void A_newer_version_is_ready_with_its_notes()
    {
        var state = UpdateRules.Answer("1.22.0.0", "1.23.0.0", "  - Point one\n", dismissed: string.Empty, Now);

        Assert.Equal(UpdateStatus.Ready, state.Status);
        Assert.Equal("1.23.0", state.Available);
        Assert.Equal("- Point one", state.Notes);
        Assert.True(state.ShowsNote);
        Assert.True(state.HasUpdate);
    }

    [Fact]
    public void Later_hides_the_note_until_a_newer_version()
    {
        var ready = UpdateRules.Answer("1.22.0", "1.23.0", null, string.Empty, Now);
        var later = UpdateRules.Dismiss(ready);
        Assert.Equal(UpdateStatus.Dismissed, later.Status);
        Assert.False(later.ShowsNote);
        Assert.True(later.HasUpdate);
        Assert.Equal("1.23.0", later.Available);

        // The next check finds the same version: it stays dismissed (the version is saved as dismissed).
        var again = UpdateRules.Answer("1.22.0", "1.23.0", null, dismissed: "1.23.0", Now.AddHours(3));
        Assert.Equal(UpdateStatus.Dismissed, again.Status);

        // A newer one shows again.
        var newer = UpdateRules.Answer("1.22.0", "1.23.1", null, dismissed: "1.23.0", Now.AddHours(6));
        Assert.Equal(UpdateStatus.Ready, newer.Status);
        Assert.Equal("1.23.1", newer.Available);

        // Later on nothing changes nothing.
        Assert.Same(UpdateState.Unknown, UpdateRules.Dismiss(UpdateState.Unknown));
    }

    [Fact]
    public void After_updating_past_a_dismissed_version_nothing_shows()
    {
        // The player dismissed 1.23.0, then installed it: running 1.23.0 is up to date.
        var state = UpdateRules.Answer("1.23.0", "1.23.0", null, dismissed: "1.23.0", Now);
        Assert.Equal(UpdateStatus.None, state.Status);
    }

    [Fact]
    public void Turning_the_check_off_forgets_the_update()
    {
        var ready = UpdateRules.Answer("1.22.0", "1.23.0", null, string.Empty, Now);
        var off = UpdateRules.Off(ready);
        Assert.Equal(UpdateStatus.None, off.Status);
        Assert.Null(off.Available);
        Assert.Equal(Now, off.CheckedUtc);
    }

    [Fact]
    public void The_check_runs_at_login_then_every_three_hours_and_never_while_off()
    {
        Assert.Equal(TimeSpan.FromHours(3), UpdateRules.Interval);
        Assert.True(UpdateRules.Due(true, null, Now));
        Assert.False(UpdateRules.Due(true, Now.AddHours(-2), Now));
        Assert.True(UpdateRules.Due(true, Now.AddHours(-3), Now));
        Assert.False(UpdateRules.Due(false, null, Now));

        // A clock that went back asks again rather than waiting hours.
        Assert.True(UpdateRules.Due(true, Now.AddHours(1), Now));
    }

    [Theory]
    [InlineData("1.23.0", "1.22.0", true)]
    [InlineData("1.22.1", "1.22.0.0", true)]
    [InlineData("1.22", "1.22.0.0", false)]
    [InlineData("2.0.0", "1.99.99", true)]
    [InlineData("1.22.0", "1.22.0", false)]
    [InlineData("", "1.22.0", false)]
    [InlineData("1.23.0", null, false)]
    public void Newer_compares_three_part_versions(string? candidate, string? running, bool newer)
    {
        Assert.Equal(newer, UpdateRules.IsNewer(candidate, running));
    }

    [Fact]
    public void Versions_are_written_in_three_parts()
    {
        Assert.Equal("1.23.0", UpdateRules.Normalise("1.23.0.0"));
        Assert.Equal("1.23.0", UpdateRules.Normalise("1.23"));
        Assert.Equal("garbage", UpdateRules.Normalise(" garbage "));
    }

    [Fact]
    public void Plain_notes_turn_list_marks_into_bullets_and_fold_blank_runs()
    {
        var notes = UpdateNotes.Plain("- What's new, in pictures.\r\n\r\n\r\n* Know when an update is ready.\n  - A moon on your screen.\n");
        Assert.Equal("• What's new, in pictures.\n\n• Know when an update is ready.\n• A moon on your screen.", notes);
        Assert.EndsWith("…", UpdateNotes.Plain(new string('a', 1300)));
        Assert.Equal(1201, UpdateNotes.Plain(new string('a', 1300)).Length);
    }
}
