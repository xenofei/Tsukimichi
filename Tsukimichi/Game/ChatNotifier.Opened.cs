using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Unique;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// 1.7.0 "In the game" lines of the notifier (feature plan v5):
/// <list type="bullet">
/// <item>"Opened by that" (R9 F1): after a completion, one line summing up what became available, "Opened: 2 feature
/// quests, 8 side quests (3 with a story) · [Show]", the Show link opening the main window on those quests. The events
/// the notifier scans feed an <see cref="OpenedBatcher"/>; <see cref="Tick"/> (every framework update) prints the batch
/// once it has been quiet for a few polls, so several turn-ins in a row read as one line. Separate from the pinned and
/// feature notice and its own setting (<see cref="Config.Configuration.ChatNoticeOpened"/>); counts only, so the spoiler
/// shield has no name to mask; nothing is printed when nothing opened.</item>
/// <item>Quest toasts (R8 I): the game's quest toast, "Moonlit reward: Wind-up Cursor" or "Duty unlocked: The Vault",
/// when a completed quest has a Moonlit reward, while <see cref="Config.Configuration.QuestToasts"/> is on (off by
/// default) and the hook gate allows game calls.</item>
/// </list>
/// </summary>
public sealed partial class ChatNotifier
{
    private readonly OpenedBatcher opened = new();
    private ulong? openedContentId;

    /// <summary>The Moonlit catalog, for the quest toasts; set by the plugin. Null shows none.</summary>
    public Func<UniqueRewardCatalog>? MoonlitCatalog { get; set; }

    /// <summary>The game's toasts; set by the plugin. Null shows none.</summary>
    public Dalamud.Plugin.Services.IToastGui? Toasts { get; set; }

    /// <summary>The shared hook gate: toasts call into the game, so they wait while it pauses game hooks. Null allows them.</summary>
    public HookGate? Gate { get; set; }

    /// <summary>Folds the events just scanned into the open batch and shows the toasts their completions earn.</summary>
    private void TrackOpened(List<QuestEvent> fresh)
    {
        if (session.LiveContentId != openedContentId)
        {
            openedContentId = session.LiveContentId;
            opened.Reset();
        }

        if (fresh.Count == 0)
        {
            return;
        }

        opened.Add(fresh, DateTime.UtcNow);
        ShowToasts(fresh);
    }

    /// <summary>
    /// Prints the "Opened:" line once the open batch has been quiet for a few polls. Framework thread, every update; a
    /// field read while no batch is open.
    /// </summary>
    public void Tick()
    {
        if (disposed || !opened.Pending)
        {
            return;
        }

        try
        {
            // A quiet spell of a few polls: the re-evaluation after a turn-in can land a poll or two late.
            var quiet = TimeSpan.FromSeconds(Math.Max(4.0, config.PollInterval.TotalSeconds * 2.5));
            if (opened.Take(DateTime.UtcNow, quiet) is { } batch)
            {
                PrintOpened(batch);
            }
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Opened line failed");
        }
    }

    private void PrintOpened(OpenedBatch batch)
    {
        if (!config.ChatNoticeOpened || session.Bundle is not { } bundle)
        {
            return;
        }

        var counts = OpenedSummary.Count(bundle.Catalog, batch.Opened, session.FeatureQuestIds, session.Stories.RowIds);
        if (counts.Total == 0)
        {
            return;
        }

        var builder = new SeStringBuilder().AddText(string.Format(CultureInfo.CurrentCulture, Strings.OpenedChatFormat, OpenedSummary.Text(counts)));
        if (links.Actions is { } actions)
        {
            builder.AddText(Strings.ChatSuffixSeparator);
            actions.AppendShow(builder, batch);
        }

        chat.Print(builder.Build(), Strings.ChatTag);
    }

    private void ShowToasts(List<QuestEvent> fresh)
    {
        if (!config.QuestToasts || Toasts is not { } toasts || MoonlitCatalog?.Invoke() is not { } catalog || Gate is { HooksAllowed: false })
        {
            return;
        }

        foreach (var e in fresh)
        {
            if (e.Kind != QuestEventKind.Completed)
            {
                continue;
            }

            var entries = catalog.ForQuest(e.RowId);
            if (entries.Count == 0)
            {
                continue;
            }

            // A duty the quest opens reads best as such; otherwise the first reward, and how many more.
            var entry = entries[0];
            foreach (var candidate in entries)
            {
                if (candidate.Kind is RewardKind.DutyUnlock or RewardKind.Instance)
                {
                    entry = candidate;
                    break;
                }
            }

            var format = entry.Kind is RewardKind.DutyUnlock or RewardKind.Instance ? Strings.ToastDutyFormat : Strings.ToastMoonlitFormat;
            var text = string.Format(CultureInfo.CurrentCulture, format, entry.RewardName);
            if (entries.Count > 1)
            {
                text += string.Format(CultureInfo.CurrentCulture, Strings.ToastMoreFormat, entries.Count - 1);
            }

            toasts.ShowQuest(text, new QuestToastOptions { DisplayCheckmark = true, PlaySound = true });
        }
    }
}
