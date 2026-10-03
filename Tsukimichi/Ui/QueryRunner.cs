using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    private bool countsFreeTrial;
    private bool ranFreeTrial;

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

    // Multibox (D11): the pin edits made here and not queued for a save yet, and those of the save on the background
    // writer. A save applies them to the file as it is on disk, one quest at a time, so another game client's pins,
    // for other characters or for the same one, are never saved over. One save is in flight at a time.
    private readonly List<PinChange> pinChanges = [];
    private List<PinChange>? pinChangesInFlight;

    // Bumped by "Delete all data": a save or reload queued before it is ignored when it lands.
    private int pinsGeneration;

    // The session whose data events are subscribed; it exists only after the plugin's game state initialized.
    private SessionState? subscribed;

    // String caches for the table body.
    private readonly string?[] levelText = new string?[256];
    private readonly string?[] expansionText = new string?[256];

    // The Journal's EXP column text per quest (1.9.0, R6 G), cleared with the catalog and the language.
    private readonly Dictionary<uint, string> expText = [];
    private readonly Dictionary<uint, JobLabel> jobShort = [];

    // The language the expansion and job caches were filled in (Loc.Version); a switch empties them.
    private int textLanguage = -1;

    public QueryRunner(Plugin plugin, UiState ui, IPluginLog log)
    {
        this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        ui.IsOtherPath = rowId => plugin.Session?.States is { } states && states.TryGetValue(rowId, out var evaluation) && evaluation.IsOtherPath;
        ui.RevealContext = () => lastContext;
    }

    // The last query's context, for a reveal to ask which filters and search would hide its quest (UiState.RevealContext).
    private QueryContext? lastContext;

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

    /// <summary>
    /// Under the free-trial view (1.9.0), how many trailing <see cref="Rows"/> lie beyond the trial, the "Beyond your
    /// trial" group; 0 otherwise. <see cref="BeyondTrialCaption"/> names it above the table.
    /// </summary>
    public int BeyondTrial { get; private set; }

    /// <summary>"Beyond your trial: 12 quests, listed last", or null when <see cref="BeyondTrial"/> is 0.</summary>
    public string? BeyondTrialCaption { get; private set; }

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
        pinsFile is not null && pinsKey != 0 && pinsKey != NoPinsKey && pinsFile.TryGetValue(pinsKey, out var list) && list is not null ? list : NoPins;

    private static readonly uint[] NoPins = [];

    /// <summary>Bumped whenever <see cref="Pinned"/> changes: a toggle, a character switch, a reload or a delete.</summary>
    public int PinsVersion { get; private set; }

    /// <summary>
    /// Bumped whenever any character's pins may have changed (<see cref="PinsOf"/> may answer differently), whoever is
    /// on view: a pin here or through IPC, another client's save merged in, a forget or a delete. What reads the logged-in
    /// character's pins while an alt is on view (IPC <c>GetPins</c>, the nameplate marks) keys on it; a view switch alone
    /// does not move it.
    /// </summary>
    public int AllPinsVersion { get; private set; }

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
            pinChanges.Add(new PinChange(key, rowId, PinChangeKind.Unpin));
        }
        else
        {
            pinned.Add(rowId);
            list.Add(rowId);
            pinChanges.Add(new PinChange(key, rowId, PinChangeKind.Pin));
            PinsVersion++;
            AllPinsVersion++;
            MarkPinsDirty();
            ui.MarkQueryDirty();
            QuestPinned?.Invoke(rowId);
            return true;
        }

        PinsVersion++;
        AllPinsVersion++;
        MarkPinsDirty();
        ui.MarkQueryDirty();
        return true;
    }

    /// <summary>
    /// <see cref="TogglePin"/> from a click in the UI (feature plan v6 S2): an unpin shows the floating Undo
    /// ("Unpinned {quest} · Undo"), which puts the quest back where it was in the list, for the character it was
    /// unpinned for. Call inside the window the click was in.
    /// </summary>
    public bool TogglePinWithUndo(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var rowId = quest.RowId;
        var owner = PinOwner;
        var index = owner is null ? -1 : IndexOf(PinnedInOrder, rowId);
        if (!TogglePin(rowId))
        {
            return false;
        }

        if (index >= 0 && owner is { } contentId)
        {
            UndoToast.Show(
                string.Format(CultureInfo.CurrentCulture, Strings.UndoToastUnpinnedFormat, Spoilers.DisplayName(quest)),
                () => RestorePin(contentId, rowId, index));
        }

        return true;
    }

    /// <summary>Pins <paramref name="rowId"/> again for <paramref name="contentId"/> at <paramref name="index"/> in its list (Undo of an unpin).</summary>
    private void RestorePin(ulong contentId, uint rowId, int index)
    {
        if (!SetPin(contentId, rowId, pin: true) || pinsFile is null || !pinsFile.TryGetValue(contentId, out var list) || !list.Remove(rowId))
        {
            return;
        }

        list.Insert(Math.Clamp(index, 0, list.Count), rowId);
    }

    private static int IndexOf(IReadOnlyList<uint> list, uint rowId)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == rowId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Raised on the draw thread when <see cref="TogglePin"/> pins a quest (not on unpin), with its row id.</summary>
    public event Action<uint>? QuestPinned;

    /// <summary>The session the rows were computed from; null before the plugin's game state exists (Copy table as TSV reads it).</summary>
    public SessionState? Session => plugin.Session;

    /// <summary>
    /// IPC (1.8.0, <c>Tsukimichi.GetPins</c>): one character's pins in the order they were pinned, a fresh array; empty
    /// for 0 or a character without pins. Framework thread only.
    /// </summary>
    public uint[] PinsOf(ulong contentId)
    {
        if (plugin.Session is not { } session || contentId == 0)
        {
            return [];
        }

        EnsurePins(session);
        return pinsFile!.TryGetValue(contentId, out var list) && list is { Count: > 0 } ? list.ToArray() : [];
    }

    /// <summary>
    /// IPC (1.8.0, <c>Tsukimichi.PinQuest</c>): pins or unpins a quest for one character (the logged-in one), whether
    /// or not it is the one on view; saved like a pin made here, but without the first-pin prompt. True when the quest
    /// ends up as asked (already so included); false for 0. Framework thread only.
    /// </summary>
    public bool SetPin(ulong contentId, uint rowId, bool pin)
    {
        if (plugin.Session is not { } session || contentId == 0 || rowId == 0)
        {
            return false;
        }

        EnsurePins(session);
        if (!pinsFile!.TryGetValue(contentId, out var list))
        {
            if (!pin)
            {
                return true;
            }

            list = [];
            pinsFile[contentId] = list;
        }

        if (list.Contains(rowId) == pin)
        {
            return true;
        }

        if (pin)
        {
            list.Add(rowId);
        }
        else
        {
            list.Remove(rowId);
        }

        pinChanges.Add(new PinChange(contentId, rowId, pin ? PinChangeKind.Pin : PinChangeKind.Unpin));
        if (contentId == pinsKey)
        {
            if (pin)
            {
                pinned.Add(rowId);
            }
            else
            {
                pinned.Remove(rowId);
            }

            ui.MarkQueryDirty();
        }

        PinsVersion++;
        AllPinsVersion++;
        MarkPinsDirty();
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
            expText.Clear();
        }

        // Journal text (P9): loads or builds the search index once it is wanted and the catalog exists.
        plugin.QuestText?.Update(current.Catalog);

        var includeUnlisted = ui.Filters.IncludeUnlisted;
        var freeTrial = plugin.Settings.FreeTrialView;
        if (catalogChanged || countsVersion != session.Version || countsIncludeUnlisted != includeUnlisted || countsFreeTrial != freeTrial || Counts is null)
        {
            // The free-trial view (1.9.0): what the trial does not include leaves the counts, tallied apart.
            Counts = TreeCounts.Compute(current.Catalog, session.States, includeUnlisted, freeTrial ? FreeTrial.IsBeyond : null);
            (FeatureCount, FeatureReady) = ComputeFeatureCount(session, current.Catalog, freeTrial);
            countsVersion = session.Version;
            countsIncludeUnlisted = includeUnlisted;
            countsFreeTrial = freeTrial;
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
            || ranFreeTrial != freeTrial
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

    /// <summary>
    /// The quest's base EXP for the Journal's EXP column ("12,345", a Quest Sync range "54,000–57,240"); empty when it
    /// gives none or the formula does not cover it (<see cref="Core.Rewards.QuestExp"/>). Cached per quest.
    /// </summary>
    public string ExpText(QuestRecord quest)
    {
        EnsureTextLanguage();
        if (expText.TryGetValue(quest.RowId, out var cached))
        {
            return cached;
        }

        var exp = bundle is null ? Core.Rewards.ExpReward.Unknown : Core.Rewards.QuestExp.For(quest, bundle.ExpTable);
        var culture = CultureInfo.CurrentCulture;
        var text = exp.Kind switch
        {
            Core.Rewards.ExpKind.Fixed => exp.Min.ToString("N0", culture),
            Core.Rewards.ExpKind.Range => string.Format(culture, Strings.PlanningExpRangeShortFormat, exp.Min.ToString("N0", culture), exp.Max.ToString("N0", culture)),
            _ => string.Empty,
        };
        expText[quest.RowId] = text;
        return text;
    }

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
    /// The job <paramref name="rowId"/> is ready on when it is Ready on another job (its medal's badge, feature plan v6
    /// G1); 0 otherwise or when there is no answer.
    /// </summary>
    public byte ReadyOnJob(uint rowId) =>
        plugin.Session?.States is { } states && states.TryGetValue(rowId, out var evaluation) && evaluation?.ReadyOnJob is { } job ? job : (byte)0;

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
        expText.Clear();
        jobShort.Clear();
    }

    public void Dispose()
    {
        Unsubscribe();
        if (pinChanges.Count > 0)
        {
            // Queued even behind a save in flight: the writer runs both, in order, before the plugin unloads.
            SavePins(final: true);
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

    /// <summary>
    /// "Delete all data" rewrote user/pins.json with only the characters live in another game client (D11): the copy
    /// held here keeps just those too, and saves or reloads queued before are ignored, so a later pin resurrects nothing.
    /// </summary>
    private void OnDataDeleted()
    {
        pinsGeneration++;
        pinChanges.Clear();
        pinChangesInFlight = null;
        var kept = new Dictionary<ulong, List<uint>>();
        if (pinsFile is not null && plugin.Session is { } session)
        {
            foreach (var (id, list) in pinsFile)
            {
                if (session.IsLiveElsewhere(id))
                {
                    kept[id] = list;
                }
            }
        }

        pinsFile = kept;
        pinsKey = NoPinsKey;
        pinned.Clear();
        pinsDirty = false;
        PinsVersion++;
        AllPinsVersion++;
        ui.MarkQueryDirty();
    }

    /// <summary>A forgotten character takes its pins with it; the file is rewritten without them.</summary>
    private void OnCharacterForgotten(ulong contentId)
    {
        if (pinsFile is null || !pinsFile.Remove(contentId))
        {
            return;
        }

        pinChanges.Add(new PinChange(contentId, 0, PinChangeKind.Forget));
        AllPinsVersion++;

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

    /// <summary>The Job column's short label for a category (<see cref="CatalogBundle.ClassifyJobs"/>), and the job when it admits one.</summary>
    private static (string Label, byte? Single) ComputeJobShort(CatalogBundle b, uint category)
    {
        var (group, single) = b.ClassifyJobs(category);
        switch (group)
        {
            case JobGroup.Any:
                return (Strings.JobAny, null);
            case JobGroup.Single:
                var abbreviation = b.Names.ClassJobAbbreviation(single);
                return abbreviation.Length > 0 ? (abbreviation, single) : (Strings.JobMulti, null);
            case JobGroup.Land:
                return (Strings.JobDol, null);
            case JobGroup.Hand:
                return (Strings.JobDoh, null);
            case JobGroup.HandAndLand:
                return (Strings.JobDohDol, null);
            case JobGroup.WarAndMagic:
                return (Strings.JobDowDom, null);
            default:
                return (Strings.JobMulti, null);
        }
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
            JournalHits: plugin.Settings.JournalTextSearch ? plugin.QuestText?.MatchCompleted(appliedSearch, session.States) : null,
            NewSinceData: plugin.Freshness?.Current.NewQuestIds,
            JustOpened: ui.JustOpened,
            NewGamePlus: current.NewGamePlus,
            Chains: session.Chains,
            FreeTrial: plugin.Settings.FreeTrialView);
        ranFreeTrial = ctx.FreeTrial;

        // The Unlocks quick view reads best with its unlocks from the newest patch series on top (P8), then what can be picked up
        // now; the other presets keep the table's sort.
        var unlocks = ui.Filters.Preset == Preset.FeatureQuests;
        var effectiveSort = ui.Sort with { AvailableFirst = unlocks, NewThisPatchFirst = unlocks };
        lastContext = ctx;
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
        BeyondTrial = result.BeyondTrial;
        BeyondTrialCaption = result.BeyondTrial == 0
            ? null
            : string.Format(CultureInfo.CurrentCulture, result.BeyondTrial == 1 ? Strings.TrialBeyondCaptionOne : Strings.TrialBeyondCaptionFormat, result.BeyondTrial);

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

    private static (NodeCount Count, int Ready) ComputeFeatureCount(SessionState session, QuestCatalog catalog, bool freeTrial)
    {
        var ready = 0;
        var done = 0;
        var total = 0;
        var excluded = 0;
        var beyondTrial = 0;
        foreach (var rowId in session.FeatureQuestIds)
        {
            if (!catalog.ByRowId.TryGetValue(rowId, out var quest))
            {
                continue;
            }

            // Foreclosed and out-of-season quests leave the total, as in TreeCounts, and so under the free-trial view
            // (1.9.0) does an unlock quest the trial does not include and the character has not done.
            session.States.TryGetValue(rowId, out var evaluation);
            if (evaluation is { LeavesTotals: true })
            {
                excluded++;
            }
            else if (freeTrial && evaluation is not { CountsAsDone: true } && FreeTrial.IsBeyond(quest))
            {
                excluded++;
                beyondTrial++;
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

        return (new NodeCount(done, total, excluded) { BeyondTrial = beyondTrial }, ready);
    }

    private void EnsurePins(SessionState session)
    {
        if (pinsFile is null)
        {
            var warnings = new List<string>();
            pinsFile = PinsFile.Load(plugin.Paths.PinsFile, warnings);
            AllPinsVersion++;
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

    /// <summary>
    /// Queues the pin edits made here on the background writer, so the framework thread never waits on the disk or the
    /// cross-client lock. Multibox (D11): the edits are applied to the file as it is on disk, quest by quest; every
    /// other pin keeps what another game client saved. One save is in flight at a time (edits made meanwhile follow
    /// it), except at unload (<paramref name="final"/>), when the writer runs both in order.
    /// </summary>
    private void SavePins(bool final = false)
    {
        pinsDirty = false;
        if (pinsFile is null || pinChanges.Count == 0 || (pinChangesInFlight is not null && !final))
        {
            return;
        }

        var changes = new List<PinChange>(pinChanges);
        pinChanges.Clear();
        pinChangesInFlight = changes;
        // What stands in for a file that cannot be parsed: this client's own map, these edits included.
        var fallback = PinsFile.Copy(pinsFile);
        var generation = pinsGeneration;
        var path = plugin.Paths.PinsFile;
        var warnings = new List<string>();
        SerialWriter.Submit(plugin.Writer, () => PinsFile.SaveChanges(path, changes, fallback, warnings), (merged, error) =>
        {
            if (generation != pinsGeneration)
            {
                return;
            }

            if (ReferenceEquals(pinChangesInFlight, changes))
            {
                pinChangesInFlight = null;
            }

            foreach (var warning in warnings)
            {
                log.Warning("Pins: {Warning}", warning);
            }

            if (error is not null || merged is null)
            {
                // Kept: the next save (the next pin, or unload) tries again.
                pinChanges.InsertRange(0, changes);
                log.Warning(error, "Pins could not be saved");
                return;
            }

            // The saved file, with the edits made here since it was queued.
            PinsFile.Apply(merged, pinChanges);
            AdoptPins(merged);
            if (pinChanges.Count > 0)
            {
                MarkPinsDirty();
            }
        });
    }

    /// <summary>
    /// Multibox (D11): <c>user/pins.json</c> changed on disk (another game client pinned, or "Delete all data" ran
    /// there). The file is read on the background writer, after any save queued before, and the edits made here and not
    /// saved yet are applied on top. It is never quarantined: a file that cannot be read or parsed right now, or that
    /// is missing, leaves the pins held here as they are.
    /// </summary>
    public void ReloadPinsFromDisk()
    {
        if (pinsFile is null)
        {
            // Never loaded here: the first use reads the file as it is.
            return;
        }

        var generation = pinsGeneration;
        var path = plugin.Paths.PinsFile;
        SerialWriter.Submit(plugin.Writer, () => PinsFile.LoadShared(path), (read, error) =>
        {
            if (generation != pinsGeneration || pinsFile is null)
            {
                return;
            }

            if (error is not null || !read.IsLoaded)
            {
                if (error is not null || read.Status is SharedLoad.Unreadable or SharedLoad.Invalid)
                {
                    log.Debug(error, "Pins not merged from disk: {Problem}", read.Problem ?? error?.Message ?? string.Empty);
                }

                return;
            }

            var merged = read.Value!;
            if (pinChangesInFlight is { } inFlight)
            {
                PinsFile.Apply(merged, inFlight);
            }

            PinsFile.Apply(merged, pinChanges);
            AdoptPins(merged);
        });
    }

    /// <summary>
    /// Takes a merged pins map as the one held here, refreshing the viewed character's pinned set when it changed;
    /// <see cref="AllPinsVersion"/> moves when any character's pins did (the logged-in one's while an alt is on view).
    /// </summary>
    private void AdoptPins(Dictionary<ulong, List<uint>> merged)
    {
        var before = pinsKey is not (0 or NoPinsKey) && pinsFile is not null && pinsFile.TryGetValue(pinsKey, out var old) ? old : null;
        if (!SamePins(pinsFile, merged))
        {
            AllPinsVersion++;
        }

        pinsFile = merged;
        if (pinsKey is 0 or NoPinsKey)
        {
            return;
        }

        var after = merged.TryGetValue(pinsKey, out var list) ? list : null;
        if (ReferenceEquals(before, after) || (before is not null && after is not null && before.SequenceEqual(after)) || (before is null && after is { Count: 0 }) || (after is null && before is { Count: 0 }))
        {
            return;
        }

        pinned.Clear();
        if (after is not null)
        {
            foreach (var rowId in after)
            {
                pinned.Add(rowId);
            }
        }

        PinsVersion++;
        ui.MarkQueryDirty();
    }

    /// <summary>The same characters with the same pins in the same order; a character with an empty list reads as none.</summary>
    private static bool SamePins(Dictionary<ulong, List<uint>>? a, Dictionary<ulong, List<uint>> b)
    {
        if (a is null)
        {
            return false;
        }

        foreach (var (id, list) in a)
        {
            var other = b.TryGetValue(id, out var found) ? found : null;
            if ((list?.Count ?? 0) != (other?.Count ?? 0) || (list is { Count: > 0 } && !list.SequenceEqual(other!)))
            {
                return false;
            }
        }

        foreach (var (id, list) in b)
        {
            if (list is { Count: > 0 } && !a.ContainsKey(id))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>A quest's Job column: the short label, the game icon of its one job (0 for a group or everyone) and the name its hover shows (empty for everyone).</summary>
public readonly record struct JobLabel(string Short, uint IconId, string Name)
{
    /// <summary>Everyone's label, in the current language.</summary>
    public static JobLabel Any => new(Strings.JobAny, 0, string.Empty);
}
