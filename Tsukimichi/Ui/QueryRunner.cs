using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
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
    private long ranStalledHour;

    // The journal search index version the rows were computed with (P9); a newly ready or dropped index re-runs the query.
    private int journalVersion = -1;

    // Counts cache keys.
    private int countsVersion = -1;
    private bool countsIncludeUnlisted;

    // The festivals running on the server for the viewed character (SessionState.ServerFestivals), rebuilt when they change.
    private ServerFestivals? festivalsServer;
    private HashSet<ushort> festivals = NoFestivals;

    // The Sprout caption's reach count, rebuilt once per session version and catalog.
    private int sproutVersion = -1;
    private CatalogBundle? sproutBundle;
    private string? sproutCaption;

    // Pins. pinsKey is the content id the pinned set was loaded for; NoPinsKey until the first Update, 0 in browse mode.
    private const ulong NoPinsKey = ulong.MaxValue;
    private Dictionary<ulong, List<uint>>? pinsFile;
    private ulong pinsKey = NoPinsKey;
    private readonly HashSet<uint> pinned = [];
    private bool pinsDirty;
    private DateTime pinsDirtyAtUtc;

    // The session whose data events are subscribed; it exists only after the plugin's game state initialized.
    private SessionState? subscribed;

    // String caches for the table body.
    private readonly string?[] levelText = new string?[256];
    private readonly string?[] expansionText = new string?[256];
    private readonly Dictionary<uint, JobLabel> jobShort = [];

    // The language the expansion and job caches were filled in (Loc.Version); a switch empties them.
    private int textLanguage = -1;

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

    /// <summary>
    /// "412 quests in your reach" while the Sprout mode quick view is on, counting the whole catalog within reach rather
    /// than the table's rows; null otherwise.
    /// </summary>
    public string? SproutCaption { get; private set; }

    /// <summary>
    /// Under the Unlocks quick view, how many leading <see cref="Rows"/> are the "New in 7.5x" group (P8: its unlock
    /// quests from the newest patch series); 0 when none or another view is on. <see cref="NewThisPatchCaption"/> names
    /// the group above the table.
    /// </summary>
    public int NewThisPatch { get; private set; }

    /// <summary>"New in 7.5x: 12 unlock quests, listed first", or null when <see cref="NewThisPatch"/> is 0.</summary>
    public string? NewThisPatchCaption { get; private set; }

    /// <summary>Null until a catalog exists.</summary>
    public TreeCounts? Counts { get; private set; }

    /// <summary>Done/total over the derived feature quests (<see cref="SessionState.FeatureQuestIds"/>).</summary>
    public NodeCount FeatureCount { get; private set; }

    /// <summary>How many of the derived feature quests are Ready, for the tree node's badge.</summary>
    public int FeatureReady { get; private set; }

    /// <summary>Pinned row ids for the viewed character.</summary>
    public IReadOnlySet<uint> Pinned => pinned;

    /// <summary>
    /// The viewed character's pins in the order they were pinned (the file's order); empty in browse mode. The list
    /// is the runner's own, so read it on the draw thread only and key any memo on <see cref="PinsVersion"/>.
    /// </summary>
    public IReadOnlyList<uint> PinnedInOrder =>
        pinsFile is not null && pinsKey != 0 && pinsKey != NoPinsKey && pinsFile.TryGetValue(pinsKey, out var list) ? list : NoPins;

    private static readonly uint[] NoPins = [];

    /// <summary>Bumped whenever <see cref="Pinned"/> changes: a toggle, a character switch, a reload or a delete.</summary>
    public int PinsVersion { get; private set; }

    /// <summary>
    /// The viewed character's spoiler shield (<see cref="SessionState.Spoilers"/>); <see cref="SpoilerMask.None"/> before
    /// the session exists. The table prints <see cref="SpoilerMask.DisplayName(QuestRecord)"/> for every name.
    /// </summary>
    public SpoilerMask Spoilers => plugin.Session?.Spoilers ?? SpoilerMask.None;

    /// <summary>Story sidequests of the catalog (<see cref="SessionState.Stories"/>); the table's book badge reads it.</summary>
    public StorySidequests Stories => plugin.Session?.Stories ?? StorySidequests.Empty;

    /// <summary>Every chain of the catalog, side stories included (<see cref="SessionState.Chains"/>).</summary>
    public ChainCatalog Chains => plugin.Session?.Chains ?? ChainCatalog.Empty;

    /// <summary>
    /// The book badge's hover line for a story sidequest: "Part of a side story: &lt;chain&gt; (3 of 7)", the chain
    /// named through the spoiler shield, or the one-quest line. Built on the first hovered frame and cached.
    /// </summary>
    public string StoryBadgeText(uint rowId)
    {
        if (plugin.Session is not { Bundle: { } bundle } session || session.Chains.ForQuest(rowId) is not { } chain)
        {
            return Strings.StoryBadgeLone;
        }

        // Built once per row and session version (spoiler reveals bump it too), so a hover held over the badge
        // allocates nothing after its first frame.
        if (storyBadgeText is { } cached && storyBadgeRowId == rowId && storyBadgeVersion == session.Version && ReferenceEquals(storyBadgeBundle, bundle))
        {
            return cached;
        }

        var title = ChainCatalog.Title(chain, id => session.Spoilers.DisplayName(bundle.Catalog, id, id.ToString(CultureInfo.InvariantCulture)));
        storyBadgeRowId = rowId;
        storyBadgeVersion = session.Version;
        storyBadgeBundle = bundle;
        storyBadgeText = string.Format(CultureInfo.CurrentCulture, Strings.StoryBadgeFormat, title, ChainCatalog.IndexOf(chain, rowId) + 1, chain.RowIds.Count);
        return storyBadgeText;
    }

    // StoryBadgeText's one-entry cache: the row, session version and catalog it was built for.
    private uint storyBadgeRowId;
    private int storyBadgeVersion = -1;
    private CatalogBundle? storyBadgeBundle;
    private string? storyBadgeText;

    /// <summary>Search text the current rows were computed with (after debounce).</summary>
    public string AppliedSearch => appliedSearch;

    /// <summary>
    /// Whether the viewed character pinned <paramref name="rowId"/>. Re-syncs the pinned set to the viewed character
    /// first (one compare when it is unchanged), so a window drawn while the main window is closed never answers from
    /// the previous character's pins.
    /// </summary>
    public bool IsPinned(uint rowId)
    {
        SyncPins();
        return pinned.Contains(rowId);
    }

    /// <summary>Loads the viewed character's pins if the view moved since the last sync; bumps <see cref="PinsVersion"/> when it does.</summary>
    public void SyncPins()
    {
        if (plugin.Session is { } session)
        {
            EnsurePins(session);
        }
    }

    /// <summary>False in browse mode: pins are per character, so there is nobody to pin for.</summary>
    public bool CanPin => plugin.Session?.ViewedContentId is not null;

    /// <summary>The character <see cref="IsPinned"/> and <see cref="TogglePin"/> act on (the viewed one); null in browse mode.</summary>
    public ulong? PinOwner => plugin.Session?.ViewedContentId;

    /// <summary>
    /// Pins or unpins a quest for the viewed character; saved after <see cref="PinsSaveDebounce"/> and on dispose.
    /// Returns false (and changes nothing) when <see cref="CanPin"/> is false.
    /// </summary>
    public bool TogglePin(uint rowId)
    {
        if (plugin.Session is not { } session || session.ViewedContentId is not { } key)
        {
            return false;
        }

        EnsurePins(session);
        if (!pinsFile!.TryGetValue(key, out var list))
        {
            list = [];
            pinsFile[key] = list;
        }

        if (pinned.Remove(rowId))
        {
            list.Remove(rowId);
        }
        else
        {
            pinned.Add(rowId);
            list.Add(rowId);
        }

        PinsVersion++;
        MarkPinsDirty();
        ui.MarkQueryDirty();
        return true;
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

        Subscribe(session);
        EnsurePins(session);

        if (!string.Equals(ui.SearchText, pendingSearch, StringComparison.Ordinal))
        {
            pendingSearch = ui.SearchText;
            lastKeystrokeUtc = nowUtc;
            if (pendingSearch.Length == 0)
            {
                // Clearing the search is not typing: apply it now instead of showing stale rows for the debounce.
                appliedSearch = pendingSearch;
                searchDirty = true;
            }
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

        // Journal text (P9): loads or builds the search index once it is wanted and the catalog exists.
        plugin.QuestText?.Update(current.Catalog);

        var includeUnlisted = ui.Filters.IncludeUnlisted;
        if (catalogChanged || countsVersion != session.Version || countsIncludeUnlisted != includeUnlisted || Counts is null)
        {
            Counts = TreeCounts.Compute(current.Catalog, session.States, includeUnlisted);
            (FeatureCount, FeatureReady) = ComputeFeatureCount(session, current.Catalog);
            countsVersion = session.Version;
            countsIncludeUnlisted = includeUnlisted;
        }

        // The Stalled preset compares accepted times with the clock, so it is re-run once an hour even when nothing else moved.
        var stalledHour = ui.Filters.Preset == Preset.Stalled ? nowUtc.Ticks / TimeSpan.TicksPerHour : 0L;
        var dirty = catalogChanged
            || sessionVersion != session.Version
            || queryVersion != ui.QueryVersion
            || scope != ui.Scope
            || sort != ui.Sort
            || searchDirty
            || stalledHour != ranStalledHour
            || journalVersion != (plugin.QuestText?.Version ?? 0)
            || !filtersSnapshot.Equals(ui.Filters);

        if (dirty)
        {
            Run(session, current, nowUtc);
            ranStalledHour = stalledHour;
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
    public string ExpansionShort(byte expansion)
    {
        EnsureTextLanguage();
        return expansionText[expansion] ??= Strings.ExpansionShort(expansion);
    }

    /// <summary>
    /// Short label for a quest's ClassJobCategory: "Any" for everyone, a job abbreviation for a single job, else the
    /// discipline group. Cached per category id.
    /// </summary>
    public string JobShort(QuestRecord quest) => Job(quest).Short;

    /// <summary>
    /// <see cref="JobShort"/> with the game icon of a quest limited to one job (0 otherwise) and a hover name: the
    /// job's name, or the category's for a group (empty for everyone). Cached per category id.
    /// </summary>
    public JobLabel Job(QuestRecord quest)
    {
        var category = quest.ClassJobCategory;
        if (category <= 1 || bundle is not { } b)
        {
            return JobLabel.Any;
        }

        EnsureTextLanguage();
        if (jobShort.TryGetValue(category, out var cached))
        {
            return cached;
        }

        var label = ComputeJob(b, category);
        jobShort[category] = label;
        return label;
    }

    /// <summary>Empties the expansion and job label caches after a language switch.</summary>
    private void EnsureTextLanguage()
    {
        if (textLanguage == Localization.Loc.Version)
        {
            return;
        }

        textLanguage = Localization.Loc.Version;
        System.Array.Clear(expansionText);
        jobShort.Clear();
    }

    public void Dispose()
    {
        Unsubscribe();
        if (pinsDirty)
        {
            SavePins();
        }
    }

    private void Subscribe(SessionState session)
    {
        if (ReferenceEquals(subscribed, session))
        {
            return;
        }

        Unsubscribe();
        subscribed = session;
        session.DataDeleted += OnDataDeleted;
        session.CharacterForgotten += OnCharacterForgotten;
    }

    private void Unsubscribe()
    {
        if (subscribed is not { } session)
        {
            return;
        }

        session.DataDeleted -= OnDataDeleted;
        session.CharacterForgotten -= OnCharacterForgotten;
        subscribed = null;
    }

    /// <summary>"Delete all data" removed user/pins.json: drop the in-memory copy so a later pin does not resurrect it.</summary>
    private void OnDataDeleted()
    {
        pinsFile = null;
        pinsKey = NoPinsKey;
        pinned.Clear();
        pinsDirty = false;
        PinsVersion++;
        ui.MarkQueryDirty();
    }

    /// <summary>A forgotten character takes its pins with it; the file is rewritten without them.</summary>
    private void OnCharacterForgotten(ulong contentId)
    {
        if (pinsFile is null || !pinsFile.Remove(contentId))
        {
            return;
        }

        if (pinsKey == contentId)
        {
            pinsKey = NoPinsKey;
            pinned.Clear();
            PinsVersion++;
            ui.MarkQueryDirty();
        }

        MarkPinsDirty();
    }

    private void MarkPinsDirty()
    {
        pinsDirty = true;
        pinsDirtyAtUtc = DateTime.UtcNow;
    }

    private static JobLabel ComputeJob(CatalogBundle b, uint category)
    {
        var groupName = b.Names.ClassJobCategory(category);
        var (label, single) = ComputeJobShort(b, category);
        if (single is not { } job)
        {
            return new JobLabel(label, 0, ReferenceEquals(label, Strings.JobAny) ? string.Empty : groupName);
        }

        var name = b.Names.ClassJobs.GetValueOrDefault(job, string.Empty);
        return new JobLabel(label, name.Length > 0 ? JobIconBase + job : 0u, name.Length > 0 ? name : groupName);
    }

    /// <summary>First id of the game's job icon set (062101 Gladiator …), offset by ClassJob row id (<see cref="ClassJobInfo.IconId"/>).</summary>
    private const uint JobIconBase = 62100u;

    private static (string Label, byte? Single) ComputeJobShort(CatalogBundle b, uint category)
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
            return (Strings.JobAny, null);
        }

        if (count == 1)
        {
            var abbreviation = b.Names.ClassJobAbbreviation(single);
            return abbreviation.Length > 0 ? (abbreviation, single) : (Strings.JobMulti, null);
        }

        var war = count - hand - land;
        if (war == 0)
        {
            return (hand == 0 ? Strings.JobDol : land == 0 ? Strings.JobDoh : Strings.JobDohDol, null);
        }

        return (hand + land == 0 ? Strings.JobDowDom : Strings.JobMulti, null);
    }

    /// <summary>The viewed character's abandoned quest ids for the Abandoned filter; a copy, since the live ledger changes in place.</summary>
    private static IReadOnlySet<ushort> AbandonedIds(SessionState session) =>
        session.Abandoned.Count == 0 ? NoFestivals : new HashSet<ushort>(session.Abandoned.Keys);

    private void Run(SessionState session, CatalogBundle current, DateTime nowUtc)
    {
        var snapshot = session.ViewedSnapshot;

        // Festivals are server-wide: a stored character on view gets the live character's flags (or its own, less the
        // stale ones), the same set its states were resolved with, so "Seasonal active" and the states agree.
        var server = session.ServerFestivals;
        if (!server.SameAs(festivalsServer))
        {
            festivalsServer = server;
            festivals = server.Ids.Count == 0 ? NoFestivals : new HashSet<ushort>(server.Ids);
        }

        var ctx = new QueryContext(
            festivals,
            pinned,
            session.FeatureQuestIds,
            SearchIndex: SearchIndex.For(current.Catalog),
            AcceptedSince: session.AcceptedSince,
            NowUtc: nowUtc,
            CurrentLevel: CurrentLevel(snapshot),
            StalledDays: plugin.Settings.StalledDaysClamped,
            Names: session.Names,
            Abandoned: AbandonedIds(session),
            Spoilers: session.Spoilers,
            Stories: session.Stories,
            JournalHits: plugin.Settings.JournalTextSearch ? plugin.QuestText?.MatchCompleted(appliedSearch, session.States) : null);

        // The Unlocks quick view reads best with its unlocks from the newest patch series on top (P8), then what can be picked up
        // now; the other presets keep the table's sort.
        var unlocks = ui.Filters.Preset == Preset.FeatureQuests;
        var effectiveSort = ui.Sort with { AvailableFirst = unlocks, NewThisPatchFirst = unlocks };
        var result = QuestQuery.Apply(current.Catalog, session.States, ui.Filters, ui.Scope, effectiveSort, appliedSearch, ctx);

        Rows = result.Rows;
        Empty = result.Empty;
        TotalInScope = result.TotalInScope;
        SproutCaption = ui.Filters.Preset == Preset.Sprout ? SproutReach(session, current) : null;
        NewThisPatch = result.NewThisPatch;
        NewThisPatchCaption = result.NewThisPatch == 0
            ? null
            : string.Format(
                CultureInfo.CurrentCulture,
                result.NewThisPatch == 1 ? Strings.NewThisPatchCaptionOneFormat : Strings.NewThisPatchCaptionFormat,
                PatchIndex.For(current.Catalog).NewestSeries,
                result.NewThisPatch);

        sessionVersion = session.Version;
        journalVersion = plugin.QuestText?.Version ?? 0;
        queryVersion = ui.QueryVersion;
        scope = ui.Scope;
        sort = ui.Sort;
        searchDirty = false;
        filtersSnapshot = ui.Filters.Clone();
    }

    /// <summary>
    /// "412 quests in your reach": every quest in the catalog at or below the expansion the main scenario has reached
    /// (removed quests left out), whatever the scope, search or other filters narrow the table to. Counted once per
    /// session version and catalog.
    /// </summary>
    private string SproutReach(SessionState session, CatalogBundle current)
    {
        if (sproutCaption is not null && sproutVersion == session.Version && ReferenceEquals(sproutBundle, current))
        {
            return sproutCaption;
        }

        var reach = session.Spoilers.ReachExpansion;
        var count = 0;
        foreach (var quest in current.Catalog.All)
        {
            if (!quest.IsRemoved && quest.Expansion <= reach)
            {
                count++;
            }
        }

        sproutVersion = session.Version;
        sproutBundle = current;
        sproutCaption = string.Format(CultureInfo.CurrentCulture, Strings.SproutReachFormat, count);
        return sproutCaption;
    }

    /// <summary>Unsynced level of the snapshot's current job for the Around-my-level preset; 0 without a snapshot or a recorded level.</summary>
    private static byte CurrentLevel(CharacterSnapshot? snapshot)
    {
        if (snapshot is null || !snapshot.JobLevels.TryGetValue(snapshot.CurrentJob, out var level) || level <= 0)
        {
            return 0;
        }

        return (byte)Math.Min(level, byte.MaxValue);
    }

    private static (NodeCount Count, int Ready) ComputeFeatureCount(SessionState session, QuestCatalog catalog)
    {
        var ready = 0;
        var done = 0;
        var total = 0;
        var excluded = 0;
        foreach (var rowId in session.FeatureQuestIds)
        {
            if (!catalog.ByRowId.ContainsKey(rowId))
            {
                continue;
            }

            // Foreclosed and out-of-season quests leave the total, as in TreeCounts.
            session.States.TryGetValue(rowId, out var evaluation);
            if (evaluation is { LeavesTotals: true })
            {
                excluded++;
            }
            else
            {
                total++;
                if (evaluation is { State: QuestState.Completed })
                {
                    done++;
                }
                else if (evaluation is { State: QuestState.Ready })
                {
                    ready++;
                }
            }
        }

        return (new NodeCount(done, total, excluded), ready);
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

        // Browse mode (key 0) has no pins and never gets an entry in the file; TogglePin creates the list on demand.
        pinsKey = key;
        pinned.Clear();
        if (key != 0 && pinsFile.TryGetValue(key, out var list))
        {
            foreach (var rowId in list)
            {
                pinned.Add(rowId);
            }
        }

        PinsVersion++;
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

/// <summary>A quest's Job column: the short label, the game icon of its one job (0 for a group or everyone) and the name its hover shows (empty for everyone).</summary>
public readonly record struct JobLabel(string Short, uint IconId, string Name)
{
    /// <summary>Everyone's label, in the current language.</summary>
    public static JobLabel Any => new(Strings.JobAny, 0, string.Empty);
}
