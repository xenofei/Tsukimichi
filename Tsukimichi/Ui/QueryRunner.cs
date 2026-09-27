using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Owns the cached <see cref="QueryResult"/> and <see cref="TreeCounts"/> the panes render from. <see cref="Update"/>
/// runs once per frame and re-runs the query only when something it depends on changed: the session version, the
/// UI query version, the filters, scope, sort, or the search text once it has settled for
/// <see cref="SearchDebounceMs"/>. Also owns the pinned set (user/pins.json, per character) and the small
/// per-value string caches the table reads so its body allocates nothing.
/// </summary>
public sealed class QueryRunner : IDisposable
{
    public const int SearchDebounceMs = 150;

    private static readonly TimeSpan PinsSaveDebounce = TimeSpan.FromSeconds(1);
    private static readonly QuestRow[] NoRows = [];
    private static readonly HashSet<ushort> NoFestivals = [];

    private readonly Plugin plugin;
    private readonly UiState ui;
    private readonly IPluginLog log;

    // Query cache keys.
    private CatalogBundle? bundle;
    private int sessionVersion = -1;
    private int queryVersion = -1;
    private FilterSet filtersSnapshot = new();
    private QuestScope scope = QuestScope.None;
    private SortSpec sort = SortSpec.Default;
    private string pendingSearch = string.Empty;
    private string appliedSearch = string.Empty;
    private DateTime lastKeystrokeUtc;
    private bool searchDirty;

    // Counts cache keys.
    private int countsVersion = -1;
    private bool countsIncludeUnlisted;

    // Festivals of the viewed snapshot, rebuilt when the snapshot instance changes.
    private CharacterSnapshot? festivalsSnapshot;
    private HashSet<ushort> festivals = NoFestivals;

    // Pins.
    private Dictionary<ulong, List<uint>>? pinsFile;
    private ulong pinsKey = ulong.MaxValue;
    private readonly HashSet<uint> pinned = [];
    private bool pinsDirty;
    private DateTime pinsDirtyAtUtc;

    // String caches for the table body.
    private readonly string?[] levelText = new string?[256];
    private readonly string?[] expansionText = new string?[256];
    private readonly Dictionary<uint, string> jobShort = [];

    public QueryRunner(Plugin plugin, UiState ui, IPluginLog log)
    {
        this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public QuestRow[] Rows { get; private set; } = NoRows;

    /// <summary>Null when <see cref="Rows"/> is non-empty.</summary>
    public EmptyReason? Empty { get; private set; }

    public int TotalInScope { get; private set; }

    /// <summary>Null until a catalog exists.</summary>
    public TreeCounts? Counts { get; private set; }

    /// <summary>Done/total over the curated Feature Unlocks quests.</summary>
    public NodeCount FeatureCount { get; private set; }

    /// <summary>Pinned row ids for the viewed character.</summary>
    public IReadOnlySet<uint> Pinned => pinned;

    /// <summary>Search text the current rows were computed with (after debounce).</summary>
    public string AppliedSearch => appliedSearch;

    public bool IsPinned(uint rowId) => pinned.Contains(rowId);

    /// <summary>Pins or unpins a quest for the viewed character; saved after <see cref="PinsSaveDebounce"/> and on dispose.</summary>
    public void TogglePin(uint rowId)
    {
        if (plugin.Session is not { } session)
        {
            return;
        }

        EnsurePins(session);
        var list = pinsFile![pinsKey];
        if (pinned.Remove(rowId))
        {
            list.Remove(rowId);
        }
        else
        {
            pinned.Add(rowId);
            list.Add(rowId);
        }

        pinsDirty = true;
        pinsDirtyAtUtc = DateTime.UtcNow;
        ui.MarkQueryDirty();
    }

    /// <summary>Applies the pending search text immediately instead of waiting out the debounce (used by the chat command).</summary>
    public void FlushSearch()
    {
        pendingSearch = ui.SearchText;
        if (pendingSearch != appliedSearch)
        {
            appliedSearch = pendingSearch;
            searchDirty = true;
        }
    }

    /// <summary>Once per frame: debounces the search, re-runs the query and counts when dirty, saves pins when due.</summary>
    public void Update(DateTime nowUtc)
    {
        if (plugin.Session is not { } session || session.Bundle is not { } current)
        {
            return;
        }

        EnsurePins(session);

        if (!string.Equals(ui.SearchText, pendingSearch, StringComparison.Ordinal))
        {
            pendingSearch = ui.SearchText;
            lastKeystrokeUtc = nowUtc;
        }

        if (!searchDirty && !string.Equals(pendingSearch, appliedSearch, StringComparison.Ordinal)
            && (nowUtc - lastKeystrokeUtc).TotalMilliseconds >= SearchDebounceMs)
        {
            appliedSearch = pendingSearch;
            searchDirty = true;
        }

        var catalogChanged = !ReferenceEquals(bundle, current);
        if (catalogChanged)
        {
            bundle = current;
            jobShort.Clear();
        }

        var includeUnlisted = ui.Filters.IncludeUnlisted;
        if (catalogChanged || countsVersion != session.Version || countsIncludeUnlisted != includeUnlisted || Counts is null)
        {
            Counts = TreeCounts.Compute(current.Catalog, session.States, includeUnlisted);
            FeatureCount = ComputeFeatureCount(session, current.Catalog);
            countsVersion = session.Version;
            countsIncludeUnlisted = includeUnlisted;
        }

        var dirty = catalogChanged
            || sessionVersion != session.Version
            || queryVersion != ui.QueryVersion
            || scope != ui.Scope
            || sort != ui.Sort
            || searchDirty
            || !filtersSnapshot.Equals(ui.Filters);

        if (dirty)
        {
            Run(session, current);
        }

        if (pinsDirty && nowUtc - pinsDirtyAtUtc >= PinsSaveDebounce)
        {
            SavePins();
        }
    }

    /// <summary>Level as text, cached per value.</summary>
    public string LevelText(byte level) =>
        levelText[level] ??= level.ToString(CultureInfo.InvariantCulture);

    /// <summary>Short expansion label, cached per value.</summary>
    public string ExpansionShort(byte expansion) =>
        expansionText[expansion] ??= Strings.ExpansionShort(expansion);

    /// <summary>
    /// Short label for a quest's ClassJobCategory: "Any" for everyone, a job abbreviation for a single job, else the
    /// discipline group. Cached per category id.
    /// </summary>
    public string JobShort(QuestRecord quest)
    {
        var category = quest.ClassJobCategory;
        if (category <= 1 || bundle is not { } b)
        {
            return Strings.JobAny;
        }

        if (jobShort.TryGetValue(category, out var cached))
        {
            return cached;
        }

        var label = ComputeJobShort(b, category);
        jobShort[category] = label;
        return label;
    }

    public void Dispose()
    {
        if (pinsDirty)
        {
            SavePins();
        }
    }

    private static string ComputeJobShort(CatalogBundle b, uint category)
    {
        var count = 0;
        var hand = 0;
        var land = 0;
        byte single = 0;
        foreach (var job in b.Jobs.JobsIn(category))
        {
            count++;
            single = job;
            if (job is >= 8 and <= 15)
            {
                hand++;
            }
            else if (job is >= 16 and <= 18)
            {
                land++;
            }
        }

        if (count == 0 || (b.Jobs.JobColumns > 0 && count >= b.Jobs.JobColumns - 1))
        {
            return Strings.JobAny;
        }

        if (count == 1)
        {
            var abbreviation = b.Names.ClassJobAbbreviation(single);
            return abbreviation.Length > 0 ? abbreviation : Strings.JobMulti;
        }

        var war = count - hand - land;
        if (war == 0)
        {
            return hand == 0 ? Strings.JobDol : land == 0 ? Strings.JobDoh : Strings.JobDohDol;
        }

        return hand + land == 0 ? Strings.JobDowDom : Strings.JobMulti;
    }

    private void Run(SessionState session, CatalogBundle current)
    {
        var snapshot = session.ViewedSnapshot;
        if (!ReferenceEquals(snapshot, festivalsSnapshot))
        {
            festivalsSnapshot = snapshot;
            festivals = snapshot is null || snapshot.ActiveFestivals.Count == 0 ? NoFestivals : new HashSet<ushort>(snapshot.ActiveFestivals);
        }

        var ctx = new QueryContext(festivals, pinned, session.Curated.FeatureQuests, SearchIndex: SearchIndex.For(current.Catalog));
        var result = QuestQuery.Apply(current.Catalog, session.States, ui.Filters, ui.Scope, ui.Sort, appliedSearch, ctx);

        Rows = result.Rows;
        Empty = result.Empty;
        TotalInScope = result.TotalInScope;

        sessionVersion = session.Version;
        queryVersion = ui.QueryVersion;
        scope = ui.Scope;
        sort = ui.Sort;
        searchDirty = false;
        filtersSnapshot = ui.Filters.Clone();
    }

    private static NodeCount ComputeFeatureCount(SessionState session, QuestCatalog catalog)
    {
        var done = 0;
        var total = 0;
        var foreclosed = 0;
        foreach (var rowId in session.Curated.FeatureQuests)
        {
            if (!catalog.ByRowId.ContainsKey(rowId))
            {
                continue;
            }

            var state = session.States.TryGetValue(rowId, out var evaluation) ? evaluation.State : QuestState.Unknown;
            switch (state)
            {
                case QuestState.Completed:
                    done++;
                    total++;
                    break;
                case QuestState.Foreclosed:
                    foreclosed++;
                    break;
                default:
                    total++;
                    break;
            }
        }

        return new NodeCount(done, total, foreclosed);
    }

    private void EnsurePins(SessionState session)
    {
        if (pinsFile is null)
        {
            var warnings = new List<string>();
            pinsFile = PinsFile.Load(plugin.Paths.PinsFile, warnings);
            foreach (var warning in warnings)
            {
                log.Warning("Pins: {Warning}", warning);
            }
        }

        var key = session.ViewedContentId ?? 0;
        if (key == pinsKey)
        {
            return;
        }

        pinsKey = key;
        if (!pinsFile.TryGetValue(key, out var list))
        {
            list = [];
            pinsFile[key] = list;
        }

        pinned.Clear();
        foreach (var rowId in list)
        {
            pinned.Add(rowId);
        }

        ui.MarkQueryDirty();
    }

    private void SavePins()
    {
        pinsDirty = false;
        if (pinsFile is null)
        {
            return;
        }

        try
        {
            var toSave = new Dictionary<ulong, List<uint>>();
            foreach (var (key, list) in pinsFile)
            {
                if (list.Count > 0)
                {
                    toSave[key] = list;
                }
            }

            PinsFile.Save(plugin.Paths.PinsFile, toSave);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Pins could not be saved");
        }
    }
}
