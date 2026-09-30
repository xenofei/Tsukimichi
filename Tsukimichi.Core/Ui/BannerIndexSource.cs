using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// Hands the detail pane a <see cref="BannerIndex"/> for the loaded catalog (feature plan V4). The index is built once
/// per catalog, and again when the lookups' key changes (the duty unlock index follows the curated overlay and the
/// Moonlit catalog), off the calling thread; until it is ready, and for a quest it does not know, a quest gets
/// <see cref="BannerIndex.Resolve"/> without a sibling or lookups: its own journal banner, else its category art. A build
/// that throws is reported once and not retried until the catalog or the key changes. Poll it from one thread (the
/// framework thread); a frame without a change costs two delegate calls and two reference comparisons.
/// </summary>
public sealed class BannerIndexSource<TKey>
    where TKey : class
{
    private readonly Func<QuestCatalog?> catalog;
    private readonly Func<TKey?> key;
    private readonly Func<QuestCatalog, TKey?, BannerIndex> build;
    private readonly Func<Func<BannerIndex>, Task<BannerIndex>> start;
    private readonly Action<Exception>? onError;

    private BannerIndex? current;
    private Task<BannerIndex>? pending;
    private QuestCatalog? pendingCatalog;
    private TKey? pendingKey;

    // What the current index (or the last failure) was built for; a request for the same pair starts nothing.
    private QuestCatalog? doneCatalog;
    private TKey? doneKey;

    /// <param name="catalog">The loaded catalog, or null while none is.</param>
    /// <param name="key">What else the build reads (the duty unlock index); a new reference rebuilds.</param>
    /// <param name="build">The whole chain for a catalog and key (in the plugin, <c>BannerSources.Build(…).Resolve(catalog)</c>); runs off the polling thread, so it reads only what it is given.</param>
    /// <param name="start">Runs a build; <see cref="Task.Run{TResult}(Func{TResult})"/> by default, a synchronous task in tests.</param>
    /// <param name="onError">Told once about a build that threw.</param>
    public BannerIndexSource(Func<QuestCatalog?> catalog, Func<TKey?> key, Func<QuestCatalog, TKey?, BannerIndex> build, Func<Func<BannerIndex>, Task<BannerIndex>>? start = null, Action<Exception>? onError = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.key = key ?? throw new ArgumentNullException(nameof(key));
        this.build = build ?? throw new ArgumentNullException(nameof(build));
        this.start = start ?? Task.Run;
        this.onError = onError;
    }

    /// <summary>Whether an index for the catalog and key as they are now is in hand (a failed build counts as done).</summary>
    public bool IsCurrent
    {
        get
        {
            Poll();
            return pending is null && catalog() is { } c && ReferenceEquals(doneCatalog, c) && ReferenceEquals(doneKey, key());
        }
    }

    /// <summary>
    /// The quest's banner: from the index when it has the quest (an index for an earlier catalog still serves while the
    /// new one builds; a row id names the same quest in both), else its own journal banner or its category art.
    /// </summary>
    public BannerChoice For(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        Poll();
        return current is { } index && index.TryGet(quest.RowId, out var choice) ? choice : BannerIndex.Resolve(quest, null, null);
    }

    /// <summary>Collects a finished build and starts one when the catalog or the key moved on.</summary>
    public void Poll()
    {
        if (pending is { IsCompleted: true } done)
        {
            pending = null;
            doneCatalog = pendingCatalog;
            doneKey = pendingKey;
            if (done.IsCompletedSuccessfully)
            {
                current = done.Result;
            }
            else if (done.Exception?.GetBaseException() is { } error)
            {
                onError?.Invoke(error);
            }
        }

        if (pending is not null || catalog() is not { } c)
        {
            return;
        }

        var k = key();
        if (ReferenceEquals(doneCatalog, c) && ReferenceEquals(doneKey, k))
        {
            return;
        }

        pendingCatalog = c;
        pendingKey = k;
        try
        {
            pending = start(() => build(c, k));
        }
        catch (Exception ex)
        {
            // A start that throws synchronously is a failed build: remembered, so it is not retried every frame.
            pending = null;
            doneCatalog = c;
            doneKey = k;
            onError?.Invoke(ex);
        }
    }
}
