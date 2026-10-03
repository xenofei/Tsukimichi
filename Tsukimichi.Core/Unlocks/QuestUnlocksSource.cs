using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Unlocks;

/// <summary>
/// Hands every surface the <see cref="QuestUnlocks"/> of the loaded catalog (feature plan v6 K1). The index is built
/// once per catalog off the calling thread (it reads a few sheets and walks the main scenario); until it is ready the
/// index of the previous catalog serves (a row id names the same quest in both), or <see cref="QuestUnlocks.Empty"/>. A
/// build that throws is reported once and not retried until the catalog changes. Poll it from one thread (the framework
/// thread). The one-line forms (<see cref="Summary"/>, <see cref="Names"/>) are memoized per quest until the index or the
/// language changes, so a tooltip drawn every frame allocates once.
/// </summary>
public sealed class QuestUnlocksSource
{
    private readonly Func<QuestCatalog?> catalog;
    private readonly Func<QuestCatalog, QuestUnlocks> build;
    private readonly Func<Func<QuestUnlocks>, Task<QuestUnlocks>> start;
    private readonly Action<Exception>? onError;
    private readonly Dictionary<uint, string> summaries = [];
    private readonly Dictionary<uint, string> names = [];
    private readonly Dictionary<uint, string> opensLines = [];
    private readonly Dictionary<uint, string> places = [];

    private QuestUnlocks current = QuestUnlocks.Empty;
    private Task<QuestUnlocks>? pending;
    private QuestCatalog? pendingCatalog;
    private QuestCatalog? doneCatalog;
    private int textVersion = -1;

    /// <param name="catalog">The loaded catalog, or null while none is.</param>
    /// <param name="build">The index for a catalog; runs off the polling thread, so it reads only what it is given.</param>
    /// <param name="start">Runs a build; <see cref="Task.Run{TResult}(Func{TResult})"/> by default, a synchronous task in tests.</param>
    /// <param name="onError">Told once about a build that threw.</param>
    public QuestUnlocksSource(Func<QuestCatalog?> catalog, Func<QuestCatalog, QuestUnlocks> build, Func<Func<QuestUnlocks>, Task<QuestUnlocks>>? start = null, Action<Exception>? onError = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.build = build ?? throw new ArgumentNullException(nameof(build));
        this.start = start ?? Task.Run;
        this.onError = onError;
    }

    /// <summary>Moves whenever <see cref="Current"/> is replaced, so callers can memoize what they derive from it.</summary>
    public int Revision { get; private set; }

    /// <summary>The index for the loaded catalog (or the previous one while it builds); never null.</summary>
    public QuestUnlocks Current
    {
        get
        {
            Poll();
            return current;
        }
    }

    /// <summary>Whether the index for the catalog as it is now is in hand (a failed build counts as done).</summary>
    public bool IsCurrent
    {
        get
        {
            Poll();
            return pending is null && catalog() is { } c && ReferenceEquals(doneCatalog, c);
        }
    }

    /// <summary>What the quest opens (<see cref="QuestUnlocks.For"/>).</summary>
    public IReadOnlyList<UnlockEntry> For(uint rowId) => Current.For(rowId);

    /// <summary><see cref="UnlockText.Summary"/> of the quest, memoized; empty when it opens nothing but next quests.</summary>
    public string Summary(uint rowId) => Memo(summaries, rowId, static e => UnlockText.Summary(e));

    /// <summary><see cref="UnlockText.Names"/> of the quest, memoized; empty when it opens nothing but next quests.</summary>
    public string Names(uint rowId) => Memo(names, rowId, static e => UnlockText.Names(e));

    /// <summary><see cref="UnlockText.OpensLine"/> of the quest, memoized: "Opens Kugane (area) · …"; empty when none.</summary>
    public string OpensLine(uint rowId) => Memo(opensLines, rowId, static e => UnlockText.OpensLine(e));

    /// <summary><see cref="UnlockText.Places"/> of the quest, memoized: areas, aetherytes, duties and features only.</summary>
    public string Places(uint rowId) => Memo(places, rowId, static e => UnlockText.Places(e));

    private string Memo(Dictionary<uint, string> cache, uint rowId, Func<IReadOnlyList<UnlockEntry>, string> compose)
    {
        var index = Current;
        if (textVersion != CoreText.Version)
        {
            textVersion = CoreText.Version;
            ClearText();
        }

        if (cache.TryGetValue(rowId, out var known))
        {
            return known;
        }

        var text = compose(index.For(rowId));
        cache[rowId] = text;
        return text;
    }

    /// <summary>Collects a finished build and starts one when the catalog moved on.</summary>
    public void Poll()
    {
        if (pending is { IsCompleted: true } done)
        {
            pending = null;
            doneCatalog = pendingCatalog;
            if (done.IsCompletedSuccessfully)
            {
                Replace(done.Result);
            }
            else if (done.Exception?.GetBaseException() is { } error)
            {
                onError?.Invoke(error);
            }
        }

        if (pending is not null)
        {
            return;
        }

        var c = catalog();
        if (c is null)
        {
            if (doneCatalog is not null)
            {
                doneCatalog = null;
                Replace(QuestUnlocks.Empty);
            }

            return;
        }

        if (ReferenceEquals(doneCatalog, c))
        {
            return;
        }

        pendingCatalog = c;
        try
        {
            pending = start(() => build(c));
        }
        catch (Exception ex)
        {
            // A start that throws synchronously is a failed build: remembered, so it is not retried every frame.
            pending = null;
            doneCatalog = c;
            onError?.Invoke(ex);
        }

        // A synchronous start (the tests) is collected at once.
        if (pending is { IsCompleted: true })
        {
            Poll();
        }
    }

    private void Replace(QuestUnlocks index)
    {
        if (ReferenceEquals(current, index))
        {
            return;
        }

        current = index;
        ClearText();
        Revision++;
    }

    private void ClearText()
    {
        summaries.Clear();
        names.Clear();
        opensLines.Clear();
        places.Clear();
    }
}
