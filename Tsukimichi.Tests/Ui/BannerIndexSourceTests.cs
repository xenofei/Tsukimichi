using System.Globalization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// How the detail pane gets its banners (feature plan V4): one index per catalog and duty index, built off the frame,
/// with the quest's own banner or its category art standing in until it is ready, and no retry loop after a failure.
/// </summary>
public sealed class BannerIndexSourceTests
{
    private sealed class Key;

    private static QuestRecord Quest(uint row, uint genre, uint icon = 0, int sort = 0) => new()
    {
        RowId = row,
        Name = "Q" + row.ToString(CultureInfo.InvariantCulture),
        Journal = new JournalRef(BannerArts.SidequestSection, "S", 59, "C", genre, "G", sort),
        Icon = icon,
    };

    private static Task<BannerIndex> Now(Func<BannerIndex> build) => Task.FromResult(build());

    [Fact]
    public void Until_the_index_is_built_a_quest_shows_its_own_banner_or_its_category_art()
    {
        var own = Quest(1, 112, icon: 100001, sort: 1);
        var bare = Quest(2, 112, sort: 2);
        var catalog = QuestCatalog.Build([own, bare]);
        var gate = new TaskCompletionSource<BannerIndex>();
        var source = new BannerIndexSource<Key>(() => catalog, () => null, (c, _) => BannerIndex.Build(c, null), _ => gate.Task);

        Assert.Equal(BannerSource.Own, source.For(own).Source);
        Assert.Equal(BannerChoice.ForArt(BannerArt.SideLaNoscea), source.For(bare));
        Assert.False(source.IsCurrent);

        gate.SetResult(BannerIndex.Build(catalog, null));
        Assert.Equal(new BannerChoice(BannerSource.Sibling, 100001, null, BannerArt.SideLaNoscea, 1), source.For(bare));
        Assert.True(source.IsCurrent);
    }

    [Fact]
    public void It_builds_once_per_catalog_and_key_and_again_when_either_changes()
    {
        var catalog = QuestCatalog.Build([Quest(1, 112, icon: 100001, sort: 1), Quest(2, 112, sort: 2)]);
        var key = new Key();
        var builds = 0;
        var source = new BannerIndexSource<Key>(() => catalog, () => key, (c, _) =>
        {
            builds++;
            return BannerIndex.Build(c, null);
        }, Now);

        for (var frame = 0; frame < 5; frame++)
        {
            source.Poll();
            source.Poll();
        }

        Assert.Equal(1, builds);

        key = new Key();
        source.Poll();
        source.Poll();
        Assert.Equal(2, builds);

        catalog = QuestCatalog.Build([Quest(1, 112, icon: 100001, sort: 1)]);
        source.Poll();
        source.Poll();
        Assert.Equal(3, builds);
    }

    [Fact]
    public void Nothing_is_built_without_a_catalog()
    {
        QuestCatalog? catalog = null;
        var builds = 0;
        var source = new BannerIndexSource<Key>(() => catalog, () => null, (c, _) =>
        {
            builds++;
            return BannerIndex.Build(c, null);
        }, Now);

        source.Poll();
        Assert.Equal(0, builds);
        Assert.Equal(BannerSource.Category, source.For(Quest(2, 112)).Source);
    }

    [Fact]
    public void A_failed_build_is_reported_once_and_not_retried_until_the_catalog_changes()
    {
        var catalog = QuestCatalog.Build([Quest(1, 112, icon: 100001, sort: 1), Quest(2, 112, sort: 2)]);
        var builds = 0;
        var errors = new List<Exception>();
        var source = new BannerIndexSource<Key>(() => catalog, () => null, (_, _) =>
        {
            builds++;
            throw new InvalidOperationException("sheet missing");
        }, build => Task.Run(build), errors.Add);

        source.Poll();
        SpinWait.SpinUntil(() => source.IsCurrent, TimeSpan.FromSeconds(5));
        for (var frame = 0; frame < 5; frame++)
        {
            source.Poll();
        }

        Assert.Equal(1, builds);
        Assert.Single(errors);
        Assert.Equal("sheet missing", errors[0].Message);

        // The quest still has a banner: its own, or its category art.
        Assert.Equal(BannerSource.Category, source.For(catalog.GetByRowId(2)!).Source);

        catalog = QuestCatalog.Build([Quest(1, 112, icon: 100001, sort: 1)]);
        source.Poll();
        SpinWait.SpinUntil(() => source.IsCurrent, TimeSpan.FromSeconds(5));
        Assert.Equal(2, builds);
    }
}
