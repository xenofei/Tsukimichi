using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// Prints one chat line, with a quest link and the giver's map link, when a pinned or feature quest becomes
/// available (a <see cref="QuestEventKind.NewlyAvailable"/> event from the poller). Main scenario quests are left out
/// unless <see cref="Configuration.IncludeMsqInNotices"/>; each quest is announced at most once per login session;
/// nothing is printed while <see cref="Configuration.ChatNoticeNewlyAvailable"/> is off, though events are still
/// consumed so turning it on later does not replay them. Pins are read from <c>user/pins.json</c> when a candidate
/// needs them. Runs on the framework thread, from <see cref="SessionState.Changed"/>.
/// <para>
/// Also the level-up nudge (V2-11): the live snapshot's job levels are compared with the last seen ones, and when a
/// job's level rose and the next quest of its ladder or its role's ladder is open now, "Level N Job: [quest] is
/// available" is printed, once per quest per session, while <see cref="Configuration.JobQuestNudge"/> is on. The
/// first capture of a character is a baseline, not a level-up.
/// </para>
/// <para>
/// And the abandoned notice (P10): an <see cref="QuestEventKind.Abandoned"/> event prints "Abandoned: [quest] (step 3
/// of 5)" with the step from the live ledger, once per quest per session, while
/// <see cref="Configuration.ChatNoticeAbandoned"/> is on (the default), so a mis-click in the journal is noticed.
/// </para>
/// <para>
/// And the seasonal line (P11): "Moonfire Faire is running: 2 quests ready (ends Aug 28) · [quest]" once per event
/// per login when the event has a Ready quest, while <see cref="Configuration.ChatNoticeSeasonal"/> is on (the default).
/// </para>
/// <para>
/// And "Before you continue" (P5): "Before you continue: Finish the Eden raid series first. Next: [quest]" once per
/// payoff gate per character, the first time the gate speaks, while <see cref="Configuration.ChatNoticePayoffGates"/>
/// is on (the default).
/// </para>
/// </summary>
public sealed partial class ChatNotifier : IDisposable
{
    private readonly SessionState session;
    private readonly Configuration config;
    private readonly PluginPaths paths;
    private readonly GameLinks links;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly NoticeTracker tracker = new();
    private bool warned;
    private bool disposed;

    // Level-up nudge state: the ladder per bundle and the levels of the last live snapshot seen.
    private JobLadder ladder = JobLadder.Empty;
    private CatalogBundle? ladderBundle;
    private CharacterSnapshot? lastSnapshot;
    private readonly Dictionary<byte, short> lastLevels = [];

    // Seasonal notice: the live capture and evaluations last scanned for Ready event quests.
    private CharacterSnapshot? seasonalScannedSnapshot;
    private IReadOnlyDictionary<uint, QuestEvaluation>? seasonalScannedStates;

    public ChatNotifier(SessionState session, Configuration config, PluginPaths paths, GameLinks links, IChatGui chat, IPluginLog log)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.chat = chat ?? throw new ArgumentNullException(nameof(chat));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        session.Changed += OnChanged;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        session.Changed -= OnChanged;
    }

    private void OnChanged()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            Announce();
            Nudge();
            AnnounceSeasonal();
            AnnouncePayoffGates();
        }
        catch (Exception ex)
        {
            // Never let a chat problem reach the poller or the UI; one warning is enough.
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "Chat notice failed; further failures are logged at debug level");
            }
            else
            {
                log.Debug(ex, "Chat notice failed again");
            }
        }
    }

    private void Announce()
    {
        var fresh = tracker.ScanEvents(session.RecentEvents, session.LiveContentId);
        TrackOpened(fresh);
        if (fresh.Count == 0 || session.Bundle is not { } bundle)
        {
            return;
        }

        AnnounceAbandoned(fresh, bundle);

        var newlyAvailable = new List<uint>();
        foreach (var e in fresh)
        {
            if (e.Kind == QuestEventKind.NewlyAvailable)
            {
                newlyAvailable.Add(e.RowId);
            }
        }

        if (newlyAvailable.Count == 0 || !config.ChatNoticeNewlyAvailable)
        {
            return;
        }

        HashSet<uint>? pins = null;
        foreach (var rowId in newlyAvailable)
        {
            if (tracker.WasNotified(rowId) || bundle.Catalog.GetByRowId(rowId) is not { } quest)
            {
                continue;
            }

            var feature = session.FeatureQuestIds.Contains(rowId);
            var pinned = false;
            if (!feature)
            {
                pins ??= LoadPins();
                pinned = pins.Contains(rowId);
            }

            if (!NoticeTracker.Qualifies(quest, pinned, feature, config.IncludeMsqInNotices))
            {
                continue;
            }

            tracker.MarkNotified(rowId);
            var (before, after) = Strings.SplitAtLink(Strings.ChatNewlyAvailableFormat, Strings.LinkSlot);
            Print(before, quest, after);
        }
    }

    /// <summary>
    /// "Abandoned: [quest] (step 3 of 5)" for each quest that left the journal uncompleted in the events just scanned,
    /// once per quest per session. Events are consumed whether or not the setting is on.
    /// </summary>
    private void AnnounceAbandoned(List<QuestEvent> fresh, CatalogBundle bundle)
    {
        if (!config.ChatNoticeAbandoned)
        {
            return;
        }

        foreach (var e in fresh)
        {
            if (e.Kind != QuestEventKind.Abandoned
                || bundle.Catalog.GetByRowId(e.RowId) is not { } quest
                || !tracker.MarkAbandonNoticed(e.RowId))
            {
                continue;
            }

            var step = session.LiveAbandoned.TryGetValue(quest.QuestId, out var entry) ? entry.StepText : string.Empty;
            var suffix = step.Length == 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.AbandonedChatStepFormat, step);
            var (before, after) = Strings.SplitAtLink(Strings.AbandonedChatFormat, Strings.LinkSlot);
            Print(before, quest, after + suffix);
        }
    }

    /// <summary>
    /// "Moonfire Faire is running: 2 quests ready (ends Aug 28) · [quest]" once per event per login, the first time
    /// the logged-in character's evaluations show a Ready quest of a running event (P11). The quest link is the first
    /// Ready one, named through the live character's spoiler shield; the end only when curated data announces it.
    /// Waits for the first pass (no live evaluations yet reads as nothing ready). The session reset is the tracker's:
    /// <see cref="Announce"/> scans first, so a logout or another character clears what was announced.
    /// </summary>
    private void AnnounceSeasonal()
    {
        if (!config.ChatNoticeSeasonal
            || session.Bundle is not { } bundle
            || session.LiveSnapshot is not { ActiveFestivals.Count: > 0 } snapshot
            || session.LiveStates.Count == 0)
        {
            return;
        }

        // Changed fires on every session bump, most of which (a view switch, a revealed name, a pin) leave the live
        // evaluations alone. The running events' Ready sets only change with them, so the catalog is scanned once per
        // live capture and evaluation; an event with no quest, or none Ready, is not rescanned until they change.
        if (ReferenceEquals(snapshot, seasonalScannedSnapshot) && ReferenceEquals(session.LiveStates, seasonalScannedStates))
        {
            return;
        }

        seasonalScannedSnapshot = snapshot;
        seasonalScannedStates = session.LiveStates;

        var all = true;
        foreach (var id in snapshot.ActiveFestivals)
        {
            all &= tracker.WasSeasonalNoticed(id);
        }

        if (all)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var running = SeasonalNow.Running(bundle.Catalog, snapshot, session.LiveStates, session.Curated.Festivals, now);
        foreach (var festival in tracker.TakeSeasonalNotices(running))
        {
            foreach (var entry in festival.Quests)
            {
                if (entry.State == QuestState.Ready)
                {
                    Print(SeasonalNow.NoticeText(festival, now) + Strings.SeasonalChatNextPrefix, entry.Quest, string.Empty);
                    break;
                }
            }
        }
    }

    /// <summary>The payoff gates for the one-time "Before you continue" line (P5); set by the plugin. Null prints none.</summary>
    public PayoffGateSource? PayoffGates { get; set; }

    /// <summary>Persists the configuration after a gate was announced (the once-per-character record); set by the plugin.</summary>
    public Action? SaveSettings { get; set; }

    /// <summary>
    /// "Before you continue: Finish the Eden raid series first. Next: [quest]" the first time a payoff gate speaks for
    /// the logged-in character (its milestone Ready or in the journal, its content not done), once per gate per
    /// character ever: the announced ids are kept in <see cref="Configuration.PayoffGatesNoticedByCharacter"/>. The line
    /// is the curated instruction only, never the reason; the link is the first content quest left. Nothing is marked
    /// while <see cref="Configuration.ChatNoticePayoffGates"/> or <see cref="Configuration.ShowPayoffGates"/> is off, so
    /// turning it on later still announces a gate that is speaking then.
    /// </summary>
    private void AnnouncePayoffGates()
    {
        if (!config.ShowPayoffGates
            || !config.ChatNoticePayoffGates
            || PayoffGates is not { } source
            || session.LiveContentId is not { } contentId
            || session.Bundle is not { } bundle)
        {
            return;
        }

        var active = source.Live();
        if (active.Count == 0)
        {
            return;
        }

        if (!config.PayoffGatesNoticedByCharacter.TryGetValue(contentId, out var noticed) || noticed is null)
        {
            noticed = new HashSet<string>(StringComparer.Ordinal);
            config.PayoffGatesNoticedByCharacter[contentId] = noticed;
        }

        var fresh = Core.Payoff.PayoffGates.TakeNotices(active, noticed);
        if (fresh.Count == 0)
        {
            return;
        }

        SaveSettings?.Invoke();
        foreach (var gate in fresh)
        {
            QuestRecord? next = null;
            foreach (var rowId in gate.Resolved.Content)
            {
                if (!(session.LiveStates.TryGetValue(rowId, out var evaluation) && evaluation.State == QuestState.Completed))
                {
                    next = bundle.Catalog.GetByRowId(rowId);
                    break;
                }
            }

            var line = string.Format(CultureInfo.CurrentCulture, Strings.PayoffFormat, gate.Gate.Instruction);
            if (next is null)
            {
                chat.Print(new SeStringBuilder().AddText(line).Build(), Strings.ChatTag);
            }
            else
            {
                var (before, after) = Strings.SplitAtLink(Strings.PayoffChatFormat, gate.Gate.Instruction, Strings.LinkSlot);
                Print(before, next, after);
            }
        }
    }

    /// <summary>
    /// Compares the live snapshot's job levels with the last ones seen and prints the ladder quests a level-up opened.
    /// The baseline always advances, so turning the setting on later does not replay old level-ups.
    /// </summary>
    private void Nudge()
    {
        var snapshot = session.LiveSnapshot;
        if (ReferenceEquals(snapshot, lastSnapshot))
        {
            return;
        }

        var previous = lastSnapshot;
        lastSnapshot = snapshot;
        if (snapshot is null)
        {
            lastLevels.Clear();
            return;
        }

        var firstCapture = previous is null || previous.ContentId != snapshot.ContentId;
        if (!firstCapture && config.JobQuestNudge && session.Bundle is { } bundle)
        {
            RefreshLadder(bundle);
            foreach (var nudge in ladder.LevelUpNudges(lastLevels, snapshot.JobLevels, session.LiveStates))
            {
                if (tracker.WasJobNudged(nudge.RowId) || bundle.Catalog.GetByRowId(nudge.RowId) is not { } quest)
                {
                    continue;
                }

                tracker.MarkJobNudged(nudge.RowId);
                var job = DisplayName(nudge.Job.Name);

                // "[quest] is available", or, when the level was the only thing this level-up settled, the one blocker
                // left ("[quest] · after MSQ: The Vault"), so the player learns why no quest appeared.
                var (before, after) = Strings.SplitAtLink(Strings.JobsNudgeFormat, nudge.Level, job, Strings.LinkSlot);
                if (nudge.State == QuestState.Blocked && session.LiveStates.TryGetValue(nudge.RowId, out var evaluation))
                {
                    var blocker = BlockerText.For(evaluation, quest, session.LiveNames, session.LiveStates);
                    (before, after) = blocker.Length > 0
                        ? Strings.SplitAtLink(Strings.JobsNudgeBlockerFormat, nudge.Level, job, Strings.LinkSlot, blocker)
                        : Strings.SplitAtLink(Strings.JobsNudgeBlockedFormat, nudge.Level, job, Strings.LinkSlot);
                }

                Print(before, quest, after);
            }
        }

        lastLevels.Clear();
        foreach (var (job, level) in snapshot.JobLevels)
        {
            lastLevels[job] = level;
        }
    }

    private void RefreshLadder(CatalogBundle bundle)
    {
        if (ReferenceEquals(ladderBundle, bundle))
        {
            return;
        }

        ladderBundle = bundle;
        ladder = bundle.BuildJobLadder();
    }

    /// <summary>The sheet names jobs in lower case ("dark knight"); chat shows them as titles, like the dashboard.</summary>
    private static string DisplayName(string name) =>
        name.Length == 0 ? name : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);

    /// <summary>The live character's pins; read on demand since the file is small and notices are rare.</summary>
    private HashSet<uint> LoadPins()
    {
        var result = new HashSet<uint>();
        if (session.LiveContentId is not { } contentId)
        {
            return result;
        }

        var warnings = new List<string>();
        var pins = PinsFile.Load(paths.PinsFile, warnings);
        foreach (var warning in warnings)
        {
            log.Warning("Pins: {Warning}", warning);
        }

        if (pins.TryGetValue(contentId, out var list))
        {
            result.UnionWith(list);
        }

        return result;
    }

    /// <summary>"&lt;prefix&gt;[quest]&lt;suffix&gt;  [place x, y]" under the plugin's chat tag, shaped like GameLinks' quest line.</summary>
    private void Print(string prefix, QuestRecord quest, string suffix)
    {
        var builder = new SeStringBuilder()
            .AddText(prefix)
            .Add(new QuestPayload(quest.RowId))
            .AddText(session.LiveSpoilers.DisplayName(quest))
            .Add(RawPayload.LinkTerminator);

        if (suffix.Length > 0)
        {
            builder.AddText(suffix);
        }

        if (links.MapLink(quest) is { } link)
        {
            builder.AddText("  ")
                   .Add(link)
                   .AddText(link.PlaceName + " " + link.CoordinateString)
                   .Add(RawPayload.LinkTerminator);
        }

        links.Actions?.AppendQuestActions(builder, quest);
        chat.Print(builder.Build(), Strings.ChatTag);
    }
}
