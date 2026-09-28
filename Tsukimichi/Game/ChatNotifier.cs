using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// Prints one chat line, with a quest link and the giver's map link, when a pinned or feature quest becomes
/// available (a <see cref="QuestEventKind.NewlyAvailable"/> event from the poller). Main scenario quests are left out
/// unless <see cref="Configuration.IncludeMsqInNotices"/>; each quest is announced at most once per login session;
/// nothing is printed while <see cref="Configuration.ChatNoticeNewlyAvailable"/> is off, though events are still
/// consumed so turning it on later does not replay them. Pins are read from <c>user/pins.json</c> when a candidate
/// needs them. Runs on the framework thread, from <see cref="SessionState.Changed"/>.
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
            Print(quest);
        }
    }

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

    /// <summary>"Now available: [quest]  [place x, y]" under the plugin's chat tag, shaped like GameLinks' quest line.</summary>
    private void Print(QuestRecord quest)
    {
        var builder = new SeStringBuilder()
            .AddText(Strings.ChatNewlyAvailablePrefix)
            .Add(new QuestPayload(quest.RowId))
            .AddText(quest.Name)
            .Add(RawPayload.LinkTerminator);

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
