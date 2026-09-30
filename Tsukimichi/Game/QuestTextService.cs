using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Data.Files.Excel;
using Lumina.Text.ReadOnly;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Text;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>Whether the Journal card has something to show, and why not.</summary>
public enum JournalAvailability
{
    /// <summary>The quest is neither completed nor in the journal: the game has shown none of its journal.</summary>
    NotReadable,

    /// <summary>The client has no journal text for the quest (no sheet, or nothing in it).</summary>
    NoText,

    /// <summary>At least one entry or objective can be shown.</summary>
    Available,
}

/// <summary>
/// A quest's journal as the Journal card shows it: the entries and objectives the character has seen, evaluated for
/// the logged-in character (its name and gender) or neutrally for a stored one.
/// </summary>
/// <param name="Withheld">Entries left out because the quest has not reached them yet (in the journal only).</param>
public sealed record JournalView(JournalAvailability Availability, IReadOnlyList<string> Entries, IReadOnlyList<string> Objectives, int Withheld)
{
    public static readonly JournalView NotReadable = new(JournalAvailability.NotReadable, [], [], 0);
    public static readonly JournalView NoText = new(JournalAvailability.NoText, [], [], 0);
}

/// <summary>Where the journal search index stands.</summary>
public enum JournalIndexStatus
{
    /// <summary>Search of journal text is off; no index is held.</summary>
    Off,

    /// <summary>Waiting for the quest catalog before starting.</summary>
    Waiting,

    /// <summary>Reading the saved index for this game version.</summary>
    Loading,

    /// <summary>Reading every quest's text sheet to build the index (<see cref="QuestTextService.Progress"/>).</summary>
    Building,

    /// <summary>The index is held and the search box matches journal words.</summary>
    Ready,

    /// <summary>The build failed (<see cref="QuestTextService.Error"/>); turning the setting off and on retries.</summary>
    Failed,
}

/// <summary>
/// The journal text reader and search (P9). The reader reads one quest's text sheet on demand, when its Journal card is
/// opened, through raw file reads (<see cref="QuestTextFiles"/>) so no sheet stays in the Excel module's cache, and
/// keeps the last few evaluated journals. The search index is opt-in (Settings › Journal text): on first enable it is
/// read from <c>cache/journal-index.&lt;game version&gt;.&lt;lang&gt;.bin</c> or, when there is none for the running
/// game version, built on a background task from every quest's sheet and saved there; the files of other versions are
/// removed. It holds words and quest ids only. <see cref="MatchCompleted"/> restricts matches to the quests the viewed
/// character has completed, so a search never surfaces text of a quest the character has not played. Framework
/// thread, except the background build, which publishes through volatile fields.
/// </summary>
public sealed class QuestTextService : IDisposable
{
    private const int CacheSize = 8;

    private readonly IDataManager data;
    private readonly ISeStringEvaluator evaluator;
    private readonly IPluginLog log;
    private readonly string configDir;
    private readonly QuestTextFiles files;
    private readonly Lumina.Data.Language language;
    private readonly ClientLanguage clientLanguage;
    private readonly string gameVersion;

    // Evaluated journals, most recent last; keyed by everything the view depends on.
    private readonly List<(ViewKey Key, JournalView View)> views = [];

    // Index state. The build task writes these; the framework thread reads them.
    private readonly object indexLock = new();
    private CancellationTokenSource? indexCts;
    private Task? indexTask;
    private volatile JournalTextIndex? index;
    private volatile JournalIndexStatus status = JournalIndexStatus.Off;
    private volatile string? error;
    private int done;
    private int total;
    private int version;
    private bool enabled;

    public QuestTextService(IDataManager data, ISeStringEvaluator evaluator, IPluginLog log, string configDir, string gameVersion)
    {
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        ArgumentException.ThrowIfNullOrWhiteSpace(configDir);
        this.configDir = configDir;
        this.gameVersion = string.IsNullOrWhiteSpace(gameVersion) ? "unknown" : gameVersion;
        clientLanguage = data.Language;
        language = clientLanguage.ToLumina();
        files = new QuestTextFiles(data.GetFile<ExcelHeaderFile>, data.GetFile<ExcelDataFile>);
    }

    /// <summary>Where the search index stands.</summary>
    public JournalIndexStatus Status => status;

    /// <summary>The build's progress, 0 to 1; 1 once ready.</summary>
    public float Progress
    {
        get
        {
            var all = Volatile.Read(ref total);
            return all <= 0 ? 0f : Math.Clamp(Volatile.Read(ref done) / (float)all, 0f, 1f);
        }
    }

    /// <summary>Why the build failed; null otherwise.</summary>
    public string? Error => error;

    /// <summary>The index when <see cref="Status"/> is <see cref="JournalIndexStatus.Ready"/>; null otherwise.</summary>
    public JournalTextIndex? Index => index;

    /// <summary>Size of the saved index file in bytes; 0 before it is saved or loaded.</summary>
    public long FileBytes { get; private set; }

    /// <summary>How long the last build took; null when the index was loaded from disk.</summary>
    public TimeSpan? BuildTime { get; private set; }

    /// <summary>Bumped whenever the index becomes ready or is dropped, so the query re-runs.</summary>
    public int Version => Volatile.Read(ref version);

    // ------------------------------------------------------------------ reader

    /// <summary>
    /// The journal the character has seen of <paramref name="quest"/>: every entry of a completed quest, the entries up
    /// to the current step of one in the journal (<see cref="JournalVisibility"/>), nothing otherwise. Evaluated with
    /// the game's evaluator for the logged-in character (<paramref name="live"/>), neutrally otherwise, with
    /// <paramref name="storedName"/> for the player's name. Reads the sheet on first call; later calls are a cache hit.
    /// </summary>
    public JournalView Journal(QuestRecord quest, QuestState state, byte? sequence, bool live, string? storedName)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var through = JournalVisibility.VisibleThrough(state, sequence, quest.StepCount);
        if (through == JournalVisibility.None)
        {
            return JournalView.NotReadable;
        }

        var key = new ViewKey(quest.RowId, through, JournalVisibility.IsCompleted(state), sequence, live, live ? null : storedName);
        for (var i = views.Count - 1; i >= 0; i--)
        {
            if (views[i].Key == key)
            {
                return views[i].View;
            }
        }

        var view = Build(quest, state, sequence, through, live, storedName);
        views.Add((key, view));
        if (views.Count > CacheSize)
        {
            views.RemoveAt(0);
        }

        return view;
    }

    private JournalView Build(QuestRecord quest, QuestState state, byte? sequence, int through, bool live, string? storedName)
    {
        QuestText? text;
        try
        {
            var sequences = QuestTextReader.ObjectiveSequences(data.Excel, quest.RowId, language);
            text = QuestTextReader.Read(files, quest.InternalId, language, sequences);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Journal text of quest {RowId} could not be read", quest.RowId);
            return JournalView.NoText;
        }

        if (text is null || text.IsEmpty)
        {
            return JournalView.NoText;
        }

        var entries = new List<string>();
        var withheld = 0;
        foreach (var line in text.Journal)
        {
            if (line.Index > through)
            {
                withheld++;
                continue;
            }

            var rendered = Render(line.Text, live, storedName);
            if (rendered.Length > 0)
            {
                entries.Add(rendered);
            }
        }

        var objectives = new List<string>();
        foreach (var line in text.Objectives)
        {
            if (!JournalVisibility.ObjectiveVisible(state, sequence, quest.StepCount, line.Sequence))
            {
                continue;
            }

            var rendered = Render(line.Text, live, storedName);
            if (rendered.Length > 0)
            {
                objectives.Add(rendered);
            }
        }

        return entries.Count == 0 && objectives.Count == 0
            ? JournalView.NoText
            : new JournalView(JournalAvailability.Available, entries, objectives, withheld);
    }

    /// <summary>The game's own evaluation for the logged-in character (name, gender, race forks); the neutral one otherwise or when it fails.</summary>
    private string Render(ReadOnlySeString text, bool live, string? storedName)
    {
        if (live)
        {
            try
            {
                var evaluated = evaluator.Evaluate(text, default, clientLanguage);
                return Tidy(evaluated.ExtractText());
            }
            catch (Exception ex)
            {
                log.Debug(ex, "Journal line not evaluated; shown neutrally");
            }
        }

        return QuestTextNeutral.Render(text, storedName, data.Excel, language);
    }

    /// <summary>Drops the game font's private glyphs and trims each line, as the neutral rendering does.</summary>
    private static string Tidy(string text)
    {
        var chars = text.Where(c => c is < '' or > '').ToArray();
        var lines = new string(chars).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].Trim();
        }

        return string.Join('\n', lines).Trim();
    }

    private readonly record struct ViewKey(uint RowId, int Through, bool Completed, byte? Sequence, bool Live, string? Name);

    // ------------------------------------------------------------------ search index

    /// <summary>
    /// Turns the search index on or off (Settings › Journal text). On: loads or builds it once a catalog is given to
    /// <see cref="Update"/>. Off: cancels a build in flight and drops the index; the file stays for the next time.
    /// </summary>
    public void SetEnabled(bool on)
    {
        if (enabled == on)
        {
            return;
        }

        enabled = on;
        if (!on)
        {
            StopIndex();
            index = null;
            status = JournalIndexStatus.Off;
            error = null;
            Interlocked.Increment(ref version);
        }
        else
        {
            status = JournalIndexStatus.Waiting;
        }
    }

    /// <summary>Once per frame (framework thread): starts loading or building the index when it is wanted and not there yet.</summary>
    public void Update(QuestCatalog? catalog)
    {
        if (!enabled || status is not JournalIndexStatus.Waiting || catalog is null)
        {
            return;
        }

        // Snapshot the ids on this thread; the task never touches the catalog.
        var quests = catalog.All.Where(q => q.InternalId.Length > 0).Select(q => (q.RowId, q.InternalId)).ToArray();
        lock (indexLock)
        {
            indexCts?.Cancel();
            indexCts = new CancellationTokenSource();
            var token = indexCts.Token;
            status = JournalIndexStatus.Loading;
            Volatile.Write(ref done, 0);
            Volatile.Write(ref total, quests.Length);
            indexTask = Task.Run(() => LoadOrBuild(quests, token), token);
        }
    }

    /// <summary>
    /// The quests whose journal text contains every term of <paramref name="query"/>, among those the viewed character
    /// has completed in <paramref name="states"/> (spoiler rule: never a quest it has not played). Null when the index
    /// is not ready or the query has nothing to look for in the journal.
    /// </summary>
    public IReadOnlySet<uint>? MatchCompleted(string? query, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        if (index is not { } ready || string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        return ready.Match(query, id => states.TryGetValue(id, out var evaluation) && JournalVisibility.IsCompleted(evaluation.State));
    }

    private void LoadOrBuild((uint RowId, string InternalId)[] quests, CancellationToken token)
    {
        var langCode = QuestTextFiles.LanguageSuffix(language);
        var path = JournalIndexStore.PathFor(configDir, gameVersion, langCode);
        try
        {
            var loaded = JournalIndexStore.Load(configDir, gameVersion, langCode);
            token.ThrowIfCancellationRequested();
            if (loaded is not null)
            {
                FileBytes = new System.IO.FileInfo(path).Length;
                BuildTime = null;
                Publish(loaded, token);
                log.Information("Journal index loaded: {Quests} quests, {Words} words, {Bytes} bytes", loaded.QuestCount, loaded.WordCount, FileBytes);
                return;
            }

            lock (indexLock)
            {
                if (!token.IsCancellationRequested)
                {
                    status = JournalIndexStatus.Building;
                }
            }

            var before = GC.GetTotalMemory(false);
            var stopwatch = Stopwatch.StartNew();
            var built = QuestTextIndexer.Build(
                files,
                quests,
                language,
                gameVersion,
                (count, all) =>
                {
                    Volatile.Write(ref done, count);
                    Volatile.Write(ref total, all);
                },
                token);
            stopwatch.Stop();
            token.ThrowIfCancellationRequested();
            FileBytes = JournalIndexStore.Save(configDir, built);
            var removed = JournalIndexStore.DeleteOthers(configDir, path);
            BuildTime = stopwatch.Elapsed;
            Publish(built, token);
            log.Information(
                "Journal index built in {Seconds:0.0} s: {Quests} quests, {Words} words, {Bytes} bytes on disk, managed heap {Before} -> {After} MiB, {Removed} older file(s) removed",
                stopwatch.Elapsed.TotalSeconds,
                built.QuestCount,
                built.WordCount,
                FileBytes,
                before / (1024 * 1024),
                GC.GetTotalMemory(false) / (1024 * 1024),
                removed);
        }
        catch (OperationCanceledException)
        {
            log.Debug("Journal index build cancelled");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Journal index could not be built");
            if (!token.IsCancellationRequested)
            {
                error = ex.GetBaseException().Message;
                status = JournalIndexStatus.Failed;
            }
        }
    }

    private void Publish(JournalTextIndex ready, CancellationToken token)
    {
        lock (indexLock)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            index = ready;
            Volatile.Write(ref done, Volatile.Read(ref total));
            status = JournalIndexStatus.Ready;
            error = null;
            Interlocked.Increment(ref version);
        }
    }

    /// <summary>
    /// Cancels a load or build in flight. The task checks its token between quests and publishes nothing once
    /// cancelled; it is not awaited here (the framework thread must not block), only at unload, where Dispose waits briefly.
    /// </summary>
    private void StopIndex()
    {
        lock (indexLock)
        {
            indexCts?.Cancel();
            indexCts?.Dispose();
            indexCts = null;
            indexTask = null;
        }
    }

    public void Dispose()
    {
        Task? task;
        lock (indexLock)
        {
            indexCts?.Cancel();
            task = indexTask;
        }

        try
        {
            task?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Cancelled or failed; already logged.
        }

        lock (indexLock)
        {
            indexCts?.Dispose();
            indexCts = null;
            indexTask = null;
        }

        index = null;
        views.Clear();
    }
}
