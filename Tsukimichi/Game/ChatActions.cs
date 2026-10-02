using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// Clickable actions on Tsukimichi's own chat lines (feature plan v5, 1.7.0; R8 H, C6 #3): "[Open] [Pin] [Route]" after a
/// quest link, and "[Show]" after an "Opened:" line. Each action is a Dalamud link payload whose command id carries the
/// action and the quest row id (or the "Opened:" batch's serial; <see cref="ChatLinkRegistry"/>), registered through
/// <c>IChatGui.AddChatLinkHandler</c> the first time a line needs it and kept for the
/// <see cref="ChatLinkRegistry.DefaultCapacity"/> most recently printed ids; older ones are removed with
/// <c>RemoveChatLinkHandler</c>, and every one on <see cref="Dispose"/>.
/// <para>
/// Verified against goatcorp/Dalamud master b666d82 (2026-09-29), <c>Dalamud/Game/Gui/ChatGui.cs</c>: a click on a
/// Dalamud link in the game's chat calls the handler registered under (plugin internal name, command id) with that id
/// and the link's own text; the payload's extra values cannot be set by a plugin, so the id is the only carrier. Chat 2
/// (Infiziert90/ChatTwo a63403c, <c>Ui/Handler/PayloadHandler.cs</c> ClickLinkPayload) looks up the same table and runs
/// the handler on the next framework tick, so the links work there too. Both call on the framework thread.
/// </para>
/// <para>
/// Labels and links are only added while <see cref="Enabled"/> says so (Settings › Notices › Chat actions); the actions
/// themselves are the plugin's (<see cref="Open"/>, <see cref="Pin"/>, <see cref="Route"/>, <see cref="ShowOpened"/>).
/// Every call is wrapped: a failed registration leaves that link out of the line, and the first failure is logged.
/// </para>
/// </summary>
public sealed class ChatActions : IDisposable
{
    /// <summary>"Opened:" batches whose Show link still answers; older ones say the list is gone.</summary>
    public const int MaxBatches = 50;

    private readonly IChatGui chat;
    private readonly SessionState session;
    private readonly IPluginLog log;
    private readonly ChatLinkRegistry registry = new();
    private readonly Dictionary<uint, DalamudLinkPayload> payloads = [];
    private readonly Dictionary<uint, IReadOnlyList<uint>> batches = [];
    private readonly Queue<uint> batchOrder = new();
    private bool disposed;
    private bool warned;

    public ChatActions(IChatGui chat, SessionState session, IPluginLog log)
    {
        this.chat = chat ?? throw new ArgumentNullException(nameof(chat));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Reads Settings › "Clickable actions on Tsukimichi's chat lines"; null reads as on.</summary>
    public Func<bool>? Enabled { get; set; }

    /// <summary>Opens the quest in the main window.</summary>
    public Action<QuestRecord>? Open { get; set; }

    /// <summary>Pins the quest for the logged-in character.</summary>
    public Action<QuestRecord>? Pin { get; set; }

    /// <summary>Opens the unlock route to the quest.</summary>
    public Action<QuestRecord>? Route { get; set; }

    /// <summary>Opens the main window on one "Opened:" line's quests: the batch serial and its row ids.</summary>
    public Action<uint, IReadOnlyList<uint>>? ShowOpened { get; set; }

    /// <summary>Prints a plain chat line (the "list is gone" note).</summary>
    public Action<string>? Print { get; set; }

    /// <summary>How many link handlers are registered now (diagnostics and tests of the wiring).</summary>
    public int RegisteredCount => registry.Count;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        registry.Clear();
        payloads.Clear();
        try
        {
            chat.RemoveChatLinkHandler();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Removing the chat link handlers failed");
        }
    }

    /// <summary>"  [Open] [Pin] [Route]" for <paramref name="quest"/>, appended while the setting is on.</summary>
    public void AppendQuestActions(SeStringBuilder builder, QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(quest);
        if (disposed || Enabled?.Invoke() == false || quest.RowId > ChatLinkRegistry.MaxValue)
        {
            return;
        }

        builder.AddText(Strings.ChatActionGap);
        AppendLink(builder, ChatLinkAction.Open, quest.RowId, Strings.ChatActionOpen);
        builder.AddText(" ");
        AppendLink(builder, ChatLinkAction.Pin, quest.RowId, Strings.ChatActionPin);
        builder.AddText(" ");
        AppendLink(builder, ChatLinkAction.Route, quest.RowId, Strings.ChatActionRoute);
    }

    /// <summary>
    /// "[Show]" for one "Opened:" line: remembers the batch's quests under its serial (the last <see cref="MaxBatches"/>)
    /// and links to them. Added whatever the actions setting says: the line is its own setting, and Show is its point.
    /// </summary>
    public void AppendShow(SeStringBuilder builder, OpenedBatch batch)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(batch);
        if (disposed)
        {
            return;
        }

        if (!batches.ContainsKey(batch.Serial))
        {
            batchOrder.Enqueue(batch.Serial);
            while (batchOrder.Count > MaxBatches)
            {
                batches.Remove(batchOrder.Dequeue());
            }
        }

        batches[batch.Serial] = batch.Opened;
        AppendLink(builder, ChatLinkAction.ShowOpened, batch.Serial, Strings.ChatActionShow);
    }

    private void AppendLink(SeStringBuilder builder, ChatLinkAction action, uint value, string label)
    {
        if (Payload(ChatLinkRegistry.Encode(action, value)) is not { } payload)
        {
            builder.AddText(label);
            return;
        }

        builder.Add(payload).AddText(label).Add(RawPayload.LinkTerminator);
    }

    /// <summary>The link payload for <paramref name="commandId"/>, registering its handler the first time; null when registering failed.</summary>
    private DalamudLinkPayload? Payload(uint commandId)
    {
        var isNew = registry.Touch(commandId, out var evicted);
        if (evicted is { } old)
        {
            payloads.Remove(old);
            try
            {
                chat.RemoveChatLinkHandler(old);
            }
            catch (Exception ex)
            {
                Warn(ex, "Removing a chat link handler failed");
            }
        }

        if (!isNew && payloads.TryGetValue(commandId, out var known))
        {
            return known;
        }

        try
        {
            var payload = chat.AddChatLinkHandler(commandId, OnLink);
            payloads[commandId] = payload;
            return payload;
        }
        catch (Exception ex)
        {
            Warn(ex, "Chat link registration failed");
            return null;
        }
    }

    /// <summary>A click on one of the links, on the framework thread.</summary>
    private void OnLink(uint commandId, SeString link)
    {
        try
        {
            if (disposed || !ChatLinkRegistry.TryDecode(commandId, out var action, out var value))
            {
                return;
            }

            if (action == ChatLinkAction.ShowOpened)
            {
                if (batches.TryGetValue(value, out var rowIds))
                {
                    ShowOpened?.Invoke(value, rowIds);
                }
                else
                {
                    Print?.Invoke(Strings.ChatActionShowGone);
                }

                return;
            }

            if (session.Bundle?.Catalog.GetByRowId(value) is not { } quest)
            {
                return;
            }

            var handler = action switch
            {
                ChatLinkAction.Open => Open,
                ChatLinkAction.Pin => Pin,
                ChatLinkAction.Route => Route,
                _ => null,
            };
            handler?.Invoke(quest);
        }
        catch (Exception ex)
        {
            Warn(ex, "Chat link action failed");
        }
    }

    private void Warn(Exception ex, string message)
    {
        if (!warned)
        {
            warned = true;
            log.Warning(ex, "{Message}; further failures are logged at debug level", message);
        }
        else
        {
            log.Debug(ex, "{Message}", message);
        }
    }
}
