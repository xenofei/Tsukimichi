using System;
using System.Globalization;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Discovery;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// Adds "Tsukimichi: quests here (N)" to the context menu of a targeted event NPC that hands out at least one catalog
/// quest (feature plan v3 P2, "why is this NPC not giving me the quest?"). Clicking it opens the main window on the
/// Journal tab scoped to that NPC's quests (<see cref="Core.Query.QuestScope.Issuer"/>), each row's Status column
/// saying what blocks it. N counts the quests the NPC hands out today, removed quests left out, the same list
/// <c>/tsuki which</c> prints.
/// <para>
/// Where the menu comes from: right-clicking an NPC in the world interacts with it, so the Default menu for an NPC
/// target opens from the target bar or the target's name, and that path raises <see cref="IContextMenu.OnMenuOpened"/>
/// with a <see cref="MenuTargetDefault"/>. Only its <see cref="MenuTargetDefault.TargetObject"/> (or, when that is
/// null, the current target with the same object id) is read, and of it only <see cref="IGameObject.ObjectKind"/> and
/// <see cref="IGameObject.BaseId"/> (the ENpcResident row <c>Quest.IssuerStart</c> names). The filter to
/// <see cref="ObjectKind.EventNpc"/> keeps the entry off player menus; <see cref="MenuTargetDefault.TargetContentId"/>
/// is never read and nothing about the target is stored.
/// </para>
/// <para>
/// <see cref="Enabled"/> follows <c>Configuration.NpcContextMenuEnabled</c> and subscribes or unsubscribes the handler;
/// <see cref="Dispose"/> unsubscribes for good. Same shape as <see cref="ItemHooks"/>.
/// </para>
/// </summary>
public sealed class NpcHooks : IDisposable
{
    /// <summary>Boxed letter shown before the entry, shared with the item entry.</summary>
    public const char PrefixLetter = ItemHooks.PrefixLetter;

    private readonly IContextMenu contextMenu;
    private readonly SessionState session;
    private readonly ITargetManager targets;
    private readonly Action<uint> showIssuer;
    private readonly IPluginLog log;

    private bool enabled;
    private bool disposed;
    private bool warned;

    /// <param name="showIssuer">Opens the main window on the NPC's quests; called on the framework thread from the menu click with the ENpcResident row id.</param>
    public NpcHooks(IContextMenu contextMenu, SessionState session, ITargetManager targets, Action<uint> showIssuer, IPluginLog log)
    {
        this.contextMenu = contextMenu ?? throw new ArgumentNullException(nameof(contextMenu));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
        this.showIssuer = showIssuer ?? throw new ArgumentNullException(nameof(showIssuer));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Whether the handler is subscribed (default off until the plugin applies the setting).</summary>
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
            if (value)
            {
                contextMenu.OnMenuOpened += OnMenuOpened;
            }
            else
            {
                contextMenu.OnMenuOpened -= OnMenuOpened;
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Enabled = false;
        disposed = true;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        try
        {
            if (args.Target is not MenuTargetDefault target)
            {
                return;
            }

            // The object can be missing from the object table at that instant; the current target stands in when
            // it is the same object.
            var npc = target.TargetObject ?? CurrentTargetIf(target.TargetObjectId);
            if (npc is null || npc.ObjectKind != ObjectKind.EventNpc || npc.BaseId == 0)
            {
                return;
            }

            if (session.Bundle is not { } bundle)
            {
                return;
            }

            // A catalog scan, synchronous in the handler; a few thousand rows is nothing at menu-open time.
            var npcId = npc.BaseId;
            var count = QuestDiscovery.IssuedBy(bundle.Catalog, npcId).Count;
            if (count == 0)
            {
                return;
            }

            args.AddMenuItem(new MenuItem
            {
                Name = string.Format(CultureInfo.CurrentCulture, Strings.NpcMenuFormat, count),
                PrefixChar = PrefixLetter,
                OnClicked = _ => showIssuer(npcId),
            });
        }
        catch (Exception ex)
        {
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "NPC context menu entry failed");
            }
        }
    }

    private IGameObject? CurrentTargetIf(ulong objectId)
    {
        var current = targets.Target;
        return current is not null && objectId != 0 && current.GameObjectId == objectId ? current : null;
    }
}
