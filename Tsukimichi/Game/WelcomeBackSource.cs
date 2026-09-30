using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Return;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Game;

/// <summary>What the "Since you were away" card shows: whose, and either the summary or the patch question.</summary>
/// <param name="ContentId">The character it speaks for.</param>
/// <param name="Name">That character's name, for the title.</param>
/// <param name="Summary">The summary; null while it is computed, or while <paramref name="Asking"/>.</param>
/// <param name="Asking">Showing "When did you last play?" instead of a summary.</param>
public sealed record WelcomeBackView(ulong ContentId, string Name, WelcomeBackSummary? Summary, bool Asking);

/// <summary>
/// Runs "Since you were away" (feature plan v3 P7) for the plugin: keeps each stale character's stored capture as it
/// was before this login's first save (taken when the plugin loads and at every login, before the poller's first
/// pass overwrites the file), decides at each character's first live evaluation of the session whether the card
/// opens (<see cref="WelcomeBackTrigger"/>: the alt-nag guard, once per return, once per session), records the patch
/// this session's captures are taken on, and computes the summary on a worker. The dashboard's "Since you were
/// away…" opens it for any character. Everything here runs on the framework thread but the compute.
/// </summary>
public sealed class WelcomeBackSource : IDisposable
{
    private readonly SessionState session;
    private readonly SnapshotService snapshots;
    private readonly IClientState clientState;
    private readonly Configuration settings;
    private readonly IPluginLog log;

    // The stored captures as they were at the last login (or plugin load), and the full snapshots of the stale ones.
    private List<SnapshotSummary> capturesAtLogin = [];
    private readonly Dictionary<ulong, CharacterSnapshot> previousById = [];

    // Each character's state as it was loaded at its first live evaluation this session, before this session's patch
    // was saved over SeenPatch: a summary opened by hand later is measured from the patch the old capture was taken on.
    private readonly Dictionary<ulong, WelcomeBackState> stateAtLogin = [];

    // Characters already evaluated this session; summaries computed this session, by character.
    private readonly HashSet<ulong> handled = [];
    private readonly Dictionary<ulong, WelcomeBackSummary> summaries = [];
    private bool shownThisSession;

    // The compute in flight: its task, the character it is for, and whether the card opened on its own.
    private (Task<WelcomeBackSummary> Task, ulong For, bool Automatic)? pending;
    private bool disposed;

    public WelcomeBackSource(SessionState session, SnapshotService snapshots, IClientState clientState, Configuration settings, IPluginLog log)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        TakeBaseline();
        clientState.Login += TakeBaseline;
        session.Changed += OnSessionChanged;
        session.CharacterForgotten += OnCharacterForgotten;
        session.DataDeleted += OnDataDeleted;
    }

    /// <summary>What the card shows; null while it is closed.</summary>
    public WelcomeBackView? View { get; private set; }

    /// <summary>The patch series the question offers, newest first ("7.5" … "2.0"); empty before the catalog is built.</summary>
    public IReadOnlyList<PatchSeries> PickerSeries => session.Bundle is { } bundle ? PatchIndex.For(bundle.Catalog).Series : [];

    /// <summary>Takes a finished compute, if any. Called by the card each frame it could draw.</summary>
    public void Poll()
    {
        if (pending is not { Task.IsCompleted: true } done)
        {
            return;
        }

        pending = null;
        var (task, pendingFor, pendingAuto) = done;
        if (!task.IsCompletedSuccessfully)
        {
            log.Warning(task.Exception?.GetBaseException(), "\"Since you were away\" could not be computed");
            if (View is { } failed && failed.ContentId == pendingFor)
            {
                View = null;
            }

            return;
        }

        summaries[pendingFor] = task.Result;
        if (View is { } view && view.ContentId == pendingFor && !view.Asking)
        {
            // Opened on its own only when there is something to say; the dashboard button still shows an empty one.
            View = pendingAuto && task.Result.IsEmpty ? null : view with { Summary = task.Result };
        }
    }

    /// <summary>
    /// The dashboard's "Since you were away…": opens the card for a character (the viewed one). Uses this session's
    /// summary when there is one, the capture kept from before this login when it was stale, the remembered answer
    /// otherwise, and asks when there is none of those.
    /// </summary>
    public void Open(ulong contentId)
    {
        if (summaries.TryGetValue(contentId, out var known))
        {
            View = new WelcomeBackView(contentId, NameOf(contentId), known, false);
            return;
        }

        var state = LoadState(contentId);
        if (previousById.ContainsKey(contentId) || (state.Answered && !state.IsNewPlayer))
        {
            StartCompute(contentId, state.MeasuredFrom(stateAtLogin.GetValueOrDefault(contentId)), automatic: false);
            return;
        }

        View = new WelcomeBackView(contentId, NameOf(contentId), null, true);
    }

    /// <summary>"Change" beside the answer: back to the question for the character shown.</summary>
    public void Ask()
    {
        if (View is { } view)
        {
            View = view with { Summary = null, Asking = true };
        }
    }

    /// <summary>
    /// The question's answer: a patch series ("7.2") or <see cref="WelcomeBackState.NewPlayer"/>. Remembered for the
    /// character; a patch opens the summary since it, "I'm new" closes the card.
    /// </summary>
    public void Answer(string series)
    {
        if (View is not { } view)
        {
            return;
        }

        var state = LoadState(view.ContentId) with { LastPlayedPatch = series };
        SaveState(view.ContentId, state);
        summaries.Remove(view.ContentId);
        if (state.IsNewPlayer)
        {
            View = null;
            return;
        }

        StartCompute(view.ContentId, state.MeasuredFrom(stateAtLogin.GetValueOrDefault(view.ContentId)), automatic: false);
    }

    public void Close() => View = null;

    /// <summary>"Don't show again": the card never opens on its own for this character; the dashboard button still opens it.</summary>
    public void DontShowAgain()
    {
        if (View is not { } view)
        {
            return;
        }

        SaveState(view.ContentId, LoadState(view.ContentId) with { Quiet = true });
        View = null;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        pending?.Task.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        clientState.Login -= TakeBaseline;
        session.Changed -= OnSessionChanged;
        session.CharacterForgotten -= OnCharacterForgotten;
        session.DataDeleted -= OnDataDeleted;
    }

    /// <summary>
    /// The stored captures before this login saves anything, with the full snapshot of each that is old enough to
    /// open the card (a few files at most; nothing is read while the card is off).
    /// </summary>
    private void TakeBaseline()
    {
        capturesAtLogin = [.. snapshots.Characters];
        var days = settings.WelcomeBackDays;
        if (days <= 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var capture in capturesAtLogin)
        {
            if (now - capture.TakenUtc < TimeSpan.FromDays(days) || previousById.TryGetValue(capture.ContentId, out var kept) && kept.TakenUtc == capture.TakenUtc)
            {
                continue;
            }

            try
            {
                if (snapshots.Load(capture.ContentId) is { } snapshot)
                {
                    previousById[capture.ContentId] = snapshot;
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Could not read the stored capture of {ContentId} for \"Since you were away\"", capture.ContentId);
            }
        }
    }

    /// <summary>A character's first live evaluation this session: decide, record this session's patch, and start the summary.</summary>
    private void OnSessionChanged()
    {
        if (session.LiveContentId is not { } id || session.LiveSnapshot is null || session.Bundle is not { } bundle || !handled.Add(id))
        {
            return;
        }

        var state = LoadState(id);
        stateAtLogin[id] = state;
        var decision =WelcomeBackTrigger.Decide(id, capturesAtLogin, state, settings.WelcomeBackDays, shownThisSession, DateTime.UtcNow);
        var newest = PatchIndex.For(bundle.Catalog).Newest;
        var updated = newest.Length > 0 ? state with { SeenPatch = newest } : state;
        switch (decision)
        {
            case WelcomeBackDecision.Show when previousById.TryGetValue(id, out var previous):
                shownThisSession = true;
                updated = updated with { ShownForUtc = previous.TakenUtc };
                // Measured from the patch recorded with the old capture, not the one this session records.
                StartCompute(id, state, automatic: true);
                break;
            case WelcomeBackDecision.AskPatch:
                shownThisSession = true;
                View = new WelcomeBackView(id, NameOf(id), null, true);
                break;
        }

        if (updated != state)
        {
            SaveState(id, updated);
        }
    }

    /// <summary>Computes the summary for a character on a worker; the card shows "Reading…" until <see cref="Poll"/> takes it.</summary>
    private void StartCompute(ulong contentId, WelcomeBackState state, bool automatic)
    {
        if (session.Bundle is not { } bundle)
        {
            return;
        }

        var live = contentId == session.LiveContentId && session.LiveSnapshot is not null;
        var current = live ? session.LiveSnapshot : contentId == session.ViewedContentId ? session.ViewedSnapshot : null;
        if (current is null)
        {
            return;
        }

        // Copied here: the poller replaces the live map between polls, and the worker must read a settled one.
        var states = new Dictionary<uint, QuestEvaluation>(live ? session.LiveStates : session.States);
        var input = new WelcomeBackInput(bundle.Catalog, current, states, DateTime.UtcNow)
        {
            Previous = previousById.GetValueOrDefault(contentId),
            Context = session.BaseContext,
            RecordedPatch = state.SeenPatch,
            LastPlayedPatch = state.LastPlayedPatch,
            FeatureQuestIds = session.FeatureQuestIds,
            Festivals = live ? ServerFestivals.Of(current) : session.ServerFestivals,
            CuratedFestivals = session.Curated.Festivals,
        };

        View = new WelcomeBackView(contentId, current.Name, null, false);
        pending = (Task.Run(() => WelcomeBack.Compute(input)), contentId, automatic);
    }

    private string NameOf(ulong contentId)
    {
        if (session.LiveSnapshot is { } live && live.ContentId == contentId)
        {
            return live.Name;
        }

        if (session.ViewedSnapshot is { } viewed && viewed.ContentId == contentId)
        {
            return viewed.Name;
        }

        foreach (var capture in snapshots.Characters)
        {
            if (capture.ContentId == contentId)
            {
                return capture.Name;
            }
        }

        return string.Empty;
    }

    private WelcomeBackState LoadState(ulong contentId)
    {
        var warnings = new List<string>();
        var state = WelcomeBackStateFile.Load(WelcomeBackStateFile.PathFor(session.Paths.CharactersDir, contentId), warnings);
        foreach (var warning in warnings)
        {
            log.Warning("Character sidecar: {Warning}", warning);
        }

        return state;
    }

    private void SaveState(ulong contentId, WelcomeBackState state)
    {
        try
        {
            WelcomeBackStateFile.Save(WelcomeBackStateFile.PathFor(session.Paths.CharactersDir, contentId), state);
        }
        catch (Exception ex)
        {
            // Derived data: the next login records the patch again; an answer lost here is asked again.
            log.Warning(ex, "Could not save the \"Since you were away\" state of {ContentId}", contentId);
        }
    }

    private void OnCharacterForgotten(ulong contentId)
    {
        previousById.Remove(contentId);
        stateAtLogin.Remove(contentId);
        summaries.Remove(contentId);
        if (View is { } view && view.ContentId == contentId)
        {
            View = null;
        }
    }

    private void OnDataDeleted()
    {
        previousById.Clear();
        stateAtLogin.Clear();
        summaries.Clear();
        capturesAtLogin = [];
        View = null;
    }
}
