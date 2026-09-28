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
/// </summary>
public sealed class ChatNotifier : IDisposable
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
        var newlyAvailable = tracker.Scan(session.RecentEvents, session.LiveContentId);
        if (newlyAvailable.Count == 0 || !config.ChatNoticeNewlyAvailable || session.Bundle is not { } bundle)
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
            Print(Strings.ChatNewlyAvailablePrefix, quest, string.Empty);
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
                var prefix = string.Format(CultureInfo.CurrentCulture, Strings.JobsNudgePrefixFormat, nudge.Level, DisplayName(nudge.Job.Name));

                // "[quest] is available", or, when the level was the only thing this level-up settled, the one blocker
                // left ("[quest] · after MSQ: The Vault"), so the player learns why no quest appeared.
                var suffix = Strings.JobsNudgeSuffix;
                if (nudge.State == QuestState.Blocked && session.LiveStates.TryGetValue(nudge.RowId, out var evaluation))
                {
                    var blocker = BlockerText.For(evaluation, quest, session.Names, session.LiveStates);
                    suffix = blocker.Length > 0 ? Strings.StateReasonSeparator + blocker : Strings.JobsNudgeBlockedSuffix;
                }

                Print(prefix, quest, suffix);
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
            .AddText(quest.Name)
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

        chat.Print(builder.Build(), Strings.ChatTag);
    }
}
