using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Game;

/// <summary>
/// "The game confirms it" (feature plan v7, C1), game side: records the quests the game itself shows the logged-in
/// character as available, from two places. Every <see cref="ReadInterval"/> it walks the map's "available quest"
/// markers (<c>Map.UnacceptedQuestMarkers</c>, what the map and minimap draw as the "!" of a quest not taken yet); and
/// each quest the quest offer window shows is handed in by <see cref="QuestOfferHint"/> (<see cref="Offered"/>). The
/// sightings go to the character's sidecar (<see cref="OfferSightings"/>, saved on the background writer) and are
/// compared with Tsukimichi's own states (<see cref="GameOfferChecks"/>): the detail pane, <c>/tsuki why</c>, the
/// Report block and Settings › Advanced › Diagnostics show what they say. Nothing here changes a state.
/// <para>
/// Safety: only reads, never calls a game function; nothing runs while the shared <see cref="HookGate"/> holds the game
/// hooks, and a read that throws is logged once and skipped. Framework thread only.
/// </para>
/// </summary>
public sealed unsafe class OfferObserver : IDisposable
{
    /// <summary>Milliseconds between two reads of the map's markers.</summary>
    public const long ReadInterval = 2000;

    /// <summary>A changed book is saved at most this often while the character stays (always at a logout or a switch).</summary>
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(30);

    /// <summary>Prefix of the log lines naming a quest the game offered while Tsukimichi reads it Blocked.</summary>
    public const string LogPrefix = "[game offers]";

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly SessionState session;
    private readonly SnapshotService snapshots;
    private readonly SerialWriter writer;
    private readonly HookGate gate;
    private readonly IPluginLog log;

    private readonly Dictionary<ushort, OfferSighting> book = [];
    private readonly UnseenMarkers unseen = new();
    private readonly HashSet<uint> markerRows = [];
    private readonly HashSet<uint> markerTerritories = [];
    private readonly HashSet<ushort> disagreementLogged = [];

    private ulong contentId;
    private bool loading;
    private bool dirty;
    private DateTime lastSave = DateTime.MinValue;
    private long lastRead;
    private bool readWarned;
    private bool disposed;

    // The quests whose giver stands in the current zone, rebuilt when the zone or the catalog changes.
    private uint candidatesTerritory;
    private QuestCatalog? candidatesCatalog;
    private List<QuestRecord> candidates = [];

    public OfferObserver(IFramework framework, IClientState clientState, SessionState session, SnapshotService snapshots, SerialWriter writer, HookGate gate, IPluginLog log)
    {
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        framework.Update += OnUpdate;
        snapshots.LoggingOut += OnLoggingOut;
        session.CharacterForgotten += OnCharacterForgotten;
        session.DataDeleted += OnDataDeleted;
    }

    /// <summary>Moves whenever a sighting or an unseen count changes, so a surface can rebuild what it shows from them.</summary>
    public int Version { get; private set; }

    /// <summary>
    /// The quest offer window showed <paramref name="rowId"/> to the logged-in character (<see cref="QuestOfferHint"/>).
    /// Framework thread.
    /// </summary>
    public void Offered(uint rowId)
    {
        if (disposed || !gate.HooksAllowed || !Bind() || session.Bundle is not { } bundle || session.LiveSnapshot is not { } snapshot)
        {
            return;
        }

        if (OfferSightings.Record(book, [rowId], OfferSource.Offer, DateTime.UtcNow, bundle.Catalog, snapshot))
        {
            Changed();
        }
    }

    /// <summary>
    /// What the game's offers say about <paramref name="quest"/> for the viewed character: only the logged-in character
    /// is observed, so another character on view gets <see cref="GameOfferCheck.Nothing"/>.
    /// </summary>
    public GameOfferCheck Check(QuestRecord quest, QuestEvaluation? evaluation)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (!session.IsLive || contentId == 0 || session.LiveContentId != contentId)
        {
            return GameOfferCheck.Nothing;
        }

        return GameOfferChecks.Judge(quest, evaluation, book.GetValueOrDefault(quest.QuestId), unseen.Misses.GetValueOrDefault(quest.QuestId), DateTime.UtcNow);
    }

    /// <summary>The logged-in character's disagreements, confirmations and unseen Ready quests (<see cref="GameOfferChecks.Disagreements"/>).</summary>
    public List<GameDisagreement> Disagreements()
    {
        if (contentId == 0 || session.LiveContentId != contentId || session.Bundle is not { } bundle)
        {
            return [];
        }

        return GameOfferChecks.Disagreements(bundle.Catalog, session.LiveStates, book, unseen.Misses, DateTime.UtcNow);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        framework.Update -= OnUpdate;
        snapshots.LoggingOut -= OnLoggingOut;
        session.CharacterForgotten -= OnCharacterForgotten;
        session.DataDeleted -= OnDataDeleted;
        Save(force: true);
    }

    private void OnUpdate(IFramework _)
    {
        if (disposed || !Bind())
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now - lastRead < ReadInterval)
        {
            return;
        }

        lastRead = now;
        if (gate.HooksAllowed && session.Bundle is { } bundle && session.LiveSnapshot is { } snapshot)
        {
            Observe(bundle.Catalog, snapshot);
        }

        if (dirty && DateTime.UtcNow - lastSave >= SaveInterval)
        {
            Save(force: false);
        }
    }

    /// <summary>One read of the markers, the unseen counts and the reconcile against the journal.</summary>
    private void Observe(QuestCatalog catalog, CharacterSnapshot snapshot)
    {
        var changed = OfferSightings.Reconcile(book, snapshot);
        if (ReadMarkers())
        {
            changed |= OfferSightings.Record(book, markerRows, OfferSource.Marker, DateTime.UtcNow, catalog, snapshot);
            var territory = (uint)clientState.TerritoryType;
            changed |= unseen.Observe(territory, CandidatesIn(catalog, territory), session.LiveStates, markerRows, markerTerritories);
        }

        if (changed)
        {
            Changed();
        }

        LogDisagreements(catalog);
    }

    /// <summary>Fills <see cref="markerRows"/> and <see cref="markerTerritories"/>; false when the map could not be read.</summary>
    private bool ReadMarkers()
    {
        markerRows.Clear();
        markerTerritories.Clear();
        try
        {
            var map = Map.Instance();
            if (map == null)
            {
                return false;
            }

            foreach (var marker in map->UnacceptedQuestMarkers)
            {
                var rowId = OfferSightings.MarkerRowId(marker.ObjectiveId);
                if (rowId == 0)
                {
                    continue;
                }

                markerRows.Add(rowId);
                var data = marker.MarkerData;
                var count = data.Count;
                for (var i = 0; i < count; i++)
                {
                    var territory = data[i].TerritoryTypeId;
                    if (territory != 0)
                    {
                        markerTerritories.Add(territory);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            if (!readWarned)
            {
                readWarned = true;
                log.Warning(ex, "{Prefix} reading the map's available-quest markers failed; the game's offers are not recorded from the map", LogPrefix);
            }

            markerRows.Clear();
            markerTerritories.Clear();
            return false;
        }
    }

    private List<QuestRecord> CandidatesIn(QuestCatalog catalog, uint territory)
    {
        if (territory != candidatesTerritory || !ReferenceEquals(catalog, candidatesCatalog))
        {
            candidatesTerritory = territory;
            candidatesCatalog = catalog;
            candidates = UnseenMarkers.Candidates(catalog, territory);
        }

        return candidates;
    }

    /// <summary>One log line per quest the game offered while Tsukimichi reads it Blocked: what a bug report needs.</summary>
    private void LogDisagreements(QuestCatalog catalog)
    {
        if (book.Count == 0)
        {
            return;
        }

        var states = session.LiveStates;
        var now = DateTime.UtcNow;
        foreach (var sighting in book.Values)
        {
            if (disagreementLogged.Contains(sighting.QuestId)
                || catalog.GetByRowId(sighting.RowId) is not { } quest
                || !states.TryGetValue(quest.RowId, out var evaluation))
            {
                continue;
            }

            var check = GameOfferChecks.Judge(quest, evaluation, sighting, 0, now);
            if (check.Verdict == GameOfferVerdict.Disagrees)
            {
                disagreementLogged.Add(sighting.QuestId);
                log.Information("{Prefix} quest {RowId} is shown by the game but Tsukimichi reads {State}: {Detail}", LogPrefix, quest.RowId, evaluation.State, GameOfferChecks.DiagnosticText(check, evaluation) ?? string.Empty);
            }
        }
    }

    /// <summary>
    /// Follows the logged-in character: another one (or none) saves and drops the book, and a new one starts loading
    /// its sidecar on the writer. False while no character is logged in.
    /// </summary>
    private bool Bind()
    {
        var live = session.LiveContentId ?? 0;
        if (live == contentId)
        {
            return live != 0;
        }

        Save(force: true);
        Reset();
        contentId = live;
        if (live == 0)
        {
            return false;
        }

        loading = true;
        var path = OfferSightings.PathFor(session.Paths.CharactersDir, live);
        var warnings = new List<string>();
        writer.Enqueue(
            () => OfferSightings.Load(path, warnings),
            (loaded, error) => Loaded(live, loaded, error, warnings));
        return true;
    }

    /// <summary>Framework thread: the sidecar was read. What was seen meanwhile is kept beside it.</summary>
    private void Loaded(ulong owner, Dictionary<ushort, OfferSighting>? loaded, Exception? error, List<string> warnings)
    {
        if (disposed || owner != contentId)
        {
            return;
        }

        loading = false;
        if (error is not null)
        {
            log.Warning(error, "{Prefix} the game's offers could not be read; they start over", LogPrefix);
        }

        foreach (var warning in warnings)
        {
            log.Warning("{Prefix} {Warning}", LogPrefix, warning);
        }

        if (loaded is null || loaded.Count == 0)
        {
            return;
        }

        foreach (var (questId, sighting) in loaded)
        {
            if (!book.TryGetValue(questId, out var seen))
            {
                book[questId] = sighting;
                continue;
            }

            book[questId] = seen with
            {
                FirstSeenUtc = sighting.FirstSeenUtc < seen.FirstSeenUtc ? sighting.FirstSeenUtc : seen.FirstSeenUtc,
                Sources = seen.Sources | sighting.Sources,
            };
        }

        Version++;
    }

    /// <summary>Hands the book to the writer: when it changed, never while it is still loading (that would overwrite the file with less), and only when this client may write the character.</summary>
    private void Save(bool force)
    {
        if (!dirty || loading || contentId == 0 || !snapshots.CanWrite(contentId))
        {
            return;
        }

        if (!force && DateTime.UtcNow - lastSave < SaveInterval)
        {
            return;
        }

        dirty = false;
        lastSave = DateTime.UtcNow;
        var path = OfferSightings.PathFor(session.Paths.CharactersDir, contentId);
        var copy = new Dictionary<ushort, OfferSighting>(book);
        var owner = contentId;
        writer.Enqueue(
            () => OfferSightings.Save(path, copy),
            error =>
            {
                if (error is not null)
                {
                    log.Warning(error, "{Prefix} saving the game's offers failed", LogPrefix);
                    dirty |= owner == contentId;
                }
            });
    }

    private void Changed()
    {
        dirty = true;
        Version++;
    }

    private void Reset()
    {
        book.Clear();
        unseen.Clear();
        disagreementLogged.Clear();
        dirty = false;
        loading = false;
        Version++;
    }

    private void OnLoggingOut() => Save(force: true);

    /// <summary>
    /// A forgotten character's sidecar is deleted with the rest: its book is dropped unsaved, so it is not written
    /// back, and the next update binds the character afresh (an empty file).
    /// </summary>
    private void OnCharacterForgotten(ulong forgotten)
    {
        if (forgotten == contentId)
        {
            Reset();
            contentId = 0;
        }
    }

    private void OnDataDeleted()
    {
        Reset();
        contentId = 0;
    }
}
