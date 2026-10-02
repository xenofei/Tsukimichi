using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.HandIn;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// "Open in Tsukimichi" in Chat 2's context menu on quest and item links (feature plan v5, 1.7.0; C6 #2). The gates, as
/// Chat 2 registers them (<c>ChatTwo/IpcManager.cs</c> and <c>ipc.md</c>, checked against Infiziert90/ChatTwo a63403c,
/// 2026-09-27):
/// <list type="bullet">
/// <item><c>ChatTwo.Register() -> string</c>: a registration id, which every <c>Invoke</c> message carries.</item>
/// <item><c>ChatTwo.Unregister(string id)</c>.</item>
/// <item>message <c>ChatTwo.Available</c>: Chat 2 (re)loaded, with an empty registration list, so register again.</item>
/// <item>message <c>ChatTwo.Invoke(string id, PlayerPayload? sender, ulong contentId, Payload? payload, SeString?
/// senderString, SeString? content)</item>: sent once per registration while Chat 2 draws its "Integrations" submenu
/// (inside an ImGui <c>BeginMenu</c>, <c>Ui/Handler/PayloadHandler.cs</c>); <c>payload</c> is the link right-clicked.
/// </list>
/// For a <see cref="QuestPayload"/> of a catalog quest the submenu gets "Open in Tsukimichi · Ready" (the quest's state
/// for the logged-in character); for an <see cref="ItemPayload"/> that is a Moonlit reward, or that an open quest asks
/// for (while Settings › "Say which open quests need an item" is on), one "Open in Tsukimichi: (quest) · (state)" line
/// per quest. Quest names go through the spoiler shield. Optional like Lifestream's wrapper: without Chat 2 every call
/// fails quietly and nothing is drawn; <see cref="Enabled"/> follows <see cref="Config.Configuration.ChatTwoIntegration"/>
/// and <see cref="Dispose"/> unregisters.
/// </summary>
public sealed class ChatTwoIpc : IDisposable
{
    public const string PluginInternalName = "ChatTwo";
    private const string RegisterGate = "ChatTwo.Register";
    private const string UnregisterGate = "ChatTwo.Unregister";
    private const string AvailableGate = "ChatTwo.Available";
    private const string InvokeGate = "ChatTwo.Invoke";

    /// <summary>Most quests one item lists, so a common material never fills the submenu.</summary>
    public const int MaxItemQuests = 6;

    private readonly SessionState session;
    private readonly RewardLookupSource rewards;
    private readonly Action<QuestRecord> reveal;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<string>? register;
    private readonly ICallGateSubscriber<string, object?>? unregister;
    private readonly ICallGateSubscriber<object?>? available;
    private readonly ICallGateSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?>? invoke;

    private string? registrationId;
    private bool enabled;
    private bool subscribed;
    private bool disposed;
    private bool warned;

    /// <param name="reveal">Opens the main window on the quest; called from Chat 2's draw on the framework thread.</param>
    public ChatTwoIpc(IDalamudPluginInterface pluginInterface, SessionState session, RewardLookupSource rewards, Action<QuestRecord> reveal, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        this.reveal = reveal ?? throw new ArgumentNullException(nameof(reveal));
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        try
        {
            register = pluginInterface.GetIpcSubscriber<string>(RegisterGate);
            unregister = pluginInterface.GetIpcSubscriber<string, object?>(UnregisterGate);
            available = pluginInterface.GetIpcSubscriber<object?>(AvailableGate);
            invoke = pluginInterface.GetIpcSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?>(InvokeGate);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Chat 2 IPC subscribers unavailable");
            register = null;
            unregister = null;
            available = null;
            invoke = null;
        }
    }

    /// <summary>Item to the open quests that ask for it; null leaves those lines out.</summary>
    public HandInIndexSource? HandIns { get; set; }

    /// <summary>Reads Settings › "Say which open quests need an item"; null reads as on.</summary>
    public Func<bool>? NeededForEnabled { get; set; }

    /// <summary>Whether Chat 2 holds a registration of ours now.</summary>
    public bool IsRegistered => registrationId is not null;

    /// <summary>The player's setting. While on, Tsukimichi registers with Chat 2 (now, and whenever Chat 2 says it is available).</summary>
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value || disposed)
            {
                return;
            }

            enabled = value;
            Apply();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Apply();
    }

    private void Apply()
    {
        var want = enabled && !disposed && register is not null && available is not null && invoke is not null;
        if (want == subscribed)
        {
            return;
        }

        subscribed = want;
        try
        {
            if (want)
            {
                available!.Subscribe(OnAvailable);
                invoke!.Subscribe(OnInvoke);
                Register();
            }
            else
            {
                Unregister();
                available?.Unsubscribe(OnAvailable);
                invoke?.Unsubscribe(OnInvoke);
            }
        }
        catch (Exception ex)
        {
            Warn(ex, "Chat 2 IPC subscription failed");
        }
    }

    /// <summary>Chat 2 loaded or reloaded with an empty list: the old id means nothing now.</summary>
    private void OnAvailable()
    {
        registrationId = null;
        if (subscribed)
        {
            Register();
        }
    }

    private void Register()
    {
        try
        {
            registrationId = register?.InvokeFunc();
        }
        catch (IpcNotReadyError)
        {
            // Chat 2 is not loaded; its Available message registers us when it is.
            registrationId = null;
        }
        catch (Exception ex)
        {
            registrationId = null;
            Warn(ex, "Chat 2 registration failed");
        }
    }

    private void Unregister()
    {
        if (registrationId is not { } id)
        {
            return;
        }

        registrationId = null;
        try
        {
            unregister?.InvokeAction(id);
        }
        catch (IpcNotReadyError)
        {
            // Chat 2 already unloaded; nothing to undo.
        }
        catch (Exception ex)
        {
            Warn(ex, "Chat 2 unregistration failed");
        }
    }

    /// <summary>Chat 2 draws its Integrations submenu; draws ours when the id is ours. Inside Chat 2's ImGui frame.</summary>
    private void OnInvoke(string id, PlayerPayload? sender, ulong contentId, Payload? payload, SeString? senderString, SeString? content)
    {
        if (disposed || registrationId is null || !string.Equals(id, registrationId, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            switch (payload)
            {
                case QuestPayload quest:
                    DrawQuest(quest.Quest.RowId);
                    break;
                case ItemPayload item:
                    DrawItem(item.ItemId);
                    break;
            }
        }
        catch (Exception ex)
        {
            Warn(ex, "Chat 2 menu entry failed");
        }
    }

    private void DrawQuest(uint rowId)
    {
        if (session.Bundle?.Catalog.GetByRowId(rowId) is not { } quest)
        {
            return;
        }

        var label = string.Format(CultureInfo.CurrentCulture, Strings.ChatTwoOpenQuestFormat, StateWord(quest));
        if (ImGui.Selectable(label + "##tsukimichiQuest"))
        {
            reveal(quest);
        }
    }

    private void DrawItem(uint itemId)
    {
        if (itemId == 0)
        {
            return;
        }

        var quests = new List<QuestRecord>();
        var lookup = rewards.Current;
        foreach (var entry in lookup.ByItem(itemId))
        {
            if (lookup.QuestFor(entry) is { } quest && !quests.Exists(q => q.RowId == quest.RowId))
            {
                quests.Add(quest);
            }
        }

        if (NeededForEnabled?.Invoke() != false && HandIns is { } handIns)
        {
            foreach (var quest in handIns.Current.NeededFor(itemId, States))
            {
                if (!quests.Exists(q => q.RowId == quest.RowId))
                {
                    quests.Add(quest);
                }
            }
        }

        var shown = Math.Min(quests.Count, MaxItemQuests);
        for (var i = 0; i < shown; i++)
        {
            var quest = quests[i];
            var label = string.Format(CultureInfo.CurrentCulture, Strings.ChatTwoOpenItemQuestFormat, session.LiveSpoilers.DisplayName(quest), StateWord(quest));
            if (ImGui.Selectable(label + "##tsukimichiItem" + quest.RowId.ToString(CultureInfo.InvariantCulture)))
            {
                reveal(quest);
            }
        }

        if (quests.Count > shown)
        {
            ImGui.TextDisabled(string.Format(CultureInfo.CurrentCulture, Strings.AndMoreFormat, quests.Count - shown));
        }
    }

    /// <summary>The logged-in character's states (chat speaks for it), or the viewed one's while nobody is logged in.</summary>
    private IReadOnlyDictionary<uint, QuestEvaluation> States => session.LiveContentId is null ? session.States : session.LiveStates;

    private string StateWord(QuestRecord quest) =>
        States.TryGetValue(quest.RowId, out var evaluation) ? Strings.StateName(evaluation.State, quest) : Strings.StateName(QuestState.Unknown, quest);

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
