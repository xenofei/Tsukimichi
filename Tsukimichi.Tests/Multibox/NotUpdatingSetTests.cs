using Tsukimichi.Core.Model;
using Tsukimichi.Core.Multibox;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Multibox;

/// <summary>
/// The "not updating" marks (1.8.0, R7 G): kept across scans, cleared when the file reads again, goes, or this client
/// saves over it itself, and never shown for the character logged in here.
/// </summary>
public sealed class NotUpdatingSetTests
{
    private static readonly FileStamp Stamp = new(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc), 100);

    private static FolderScanResult Scan(
        IEnumerable<ulong> present,
        IEnumerable<CharacterSnapshot>? changed = null,
        Dictionary<ulong, SharedLoad>? problems = null) =>
        new([], present.ToDictionary(static id => id, static _ => Stamp), [.. changed ?? []], [], [], problems);

    [Fact]
    public void A_mark_stays_across_quiet_scans_and_goes_when_the_file_reads_or_goes()
    {
        var set = new Dictionary<ulong, SharedLoad>();
        Assert.True(NotUpdatingSet.Track(set, Scan([1, 2, 3], problems: new() { [1] = SharedLoad.Newer, [2] = SharedLoad.Invalid }), []));

        // The next scans do not read the unchanged files again: the marks stay.
        Assert.False(NotUpdatingSet.Track(set, Scan([1, 2, 3]), []));
        Assert.Equal(2, set.Count);

        // 1 reads again (its owner saved it), 2's file is gone.
        Assert.True(NotUpdatingSet.Track(set, Scan([1, 3], changed: [new CharacterSnapshot { ContentId = 1 }]), []));
        Assert.Empty(set);
    }

    [Fact]
    public void This_clients_own_save_clears_the_mark_of_the_character_it_skips()
    {
        var set = new Dictionary<ulong, SharedLoad>();
        NotUpdatingSet.Track(set, Scan([7], problems: new() { [7] = SharedLoad.Invalid }), []);

        // Logged in here: the scan skips its file from now on, so only this client's save says it reads again.
        Assert.False(NotUpdatingSet.Track(set, Scan([7]), []));
        Assert.True(NotUpdatingSet.Track(set, Scan([7]), [7]));
        Assert.Empty(set);
    }

    [Fact]
    public void A_reason_that_changes_counts_as_a_change()
    {
        var set = new Dictionary<ulong, SharedLoad> { [4] = SharedLoad.Newer };

        Assert.True(NotUpdatingSet.Track(set, Scan([4], problems: new() { [4] = SharedLoad.Invalid }), []));
        Assert.Equal(SharedLoad.Invalid, set[4]);
    }

    [Fact]
    public void The_character_logged_in_here_is_never_shown()
    {
        var all = new Dictionary<ulong, SharedLoad> { [1] = SharedLoad.Newer, [2] = SharedLoad.Invalid };

        var shown = NotUpdatingSet.Shown(all, 1);

        Assert.Equal([2UL], shown.Keys);
        Assert.Same(all, NotUpdatingSet.Shown(all, 9));
        Assert.Same(all, NotUpdatingSet.Shown(all, null));
    }

    [Fact]
    public void The_viewed_characters_status_moves_with_its_reason_too()
    {
        var newer = new Dictionary<ulong, SharedLoad> { [1] = SharedLoad.Newer };
        var invalid = new Dictionary<ulong, SharedLoad> { [1] = SharedLoad.Invalid };
        var none = new Dictionary<ulong, SharedLoad>();

        Assert.False(NotUpdatingSet.SameFor(newer, invalid, 1));
        Assert.False(NotUpdatingSet.SameFor(newer, none, 1));
        Assert.True(NotUpdatingSet.SameFor(newer, newer, 1));
        Assert.True(NotUpdatingSet.SameFor(newer, invalid, 2));
        Assert.False(NotUpdatingSet.Same(newer, invalid));
        Assert.True(NotUpdatingSet.Same(none, new Dictionary<ulong, SharedLoad>()));
    }
}
