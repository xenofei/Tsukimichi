using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// 1.7.0 "In the game" wiring (feature plan v5): the clickable chat actions, the "Opened:" line and quest toasts on the
/// chat notifier, Chat 2's "Open in Tsukimichi" and the nameplate marks. Called once from the constructor
/// (<see cref="InitializeInGame"/>) and unwound by <see cref="DisposeInGame"/>.
/// </summary>
public sealed partial class Plugin
{
    [PluginService] internal static INamePlateGui NamePlateGui { get; private set; } = null!;
    [PluginService] internal static IToastGui ToastGui { get; private set; } = null!;

    private Game.ChatActions? chatActions;
    private Game.ChatTwoIpc? chatTwo;
    private Game.NamePlateHooks? namePlateHooks;
    private bool openedTickSubscribed;

    /// <summary>Hooks up the 1.7.0 chat, Chat 2 and nameplate surfaces; needs the chat notifier and the settings window.</summary>
    private void InitializeInGame(Core.Runtime.HookGate gate, Core.Unique.RewardLookupSource rewardLookup, Core.HandIn.HandInIndexSource handIns, MoonlitPane moonlit)
    {
        void Reveal(QuestRecord quest)
        {
            mainWindow.IsOpen = true;
            mainWindow.BringToFront();
            MoonlitPane.Reveal(ui, quest);
        }

        // [Open] [Pin] [Route] after the quest link on every line GameLinks and the notifier print; [Show] after "Opened:".
        chatActions = new Game.ChatActions(ChatGui, Session, Log)
        {
            Enabled = () => Settings.ChatLinkActions,
            Open = Reveal,
            Pin = PinFromChat,
            Route = quest => ui.OpenRoute(Core.Route.RouteTarget.ForQuest(quest.RowId, gameLinks.NameOf(quest))),
            ShowOpened = (serial, rowIds) =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                ui.ShowJustOpened(serial, rowIds);
            },
            Print = gameLinks.PrintText,
        };
        gameLinks.Actions = chatActions;

        // "Opened:" after a completion, and the optional quest toasts; the line waits out a quiet spell each update.
        if (chatNotifier is { } notifier)
        {
            notifier.MoonlitCatalog = () => moonlit.Catalog;
            notifier.Toasts = ToastGui;
            notifier.Gate = gate;
            Framework.Update += OnOpenedTick;
            openedTickSubscribed = true;
        }

        // Chat 2's Integrations submenu on quest and item links.
        chatTwo = new Game.ChatTwoIpc(PluginInterface, Session, rewardLookup, Reveal, Log)
        {
            HandIns = handIns,
            NeededForEnabled = () => Settings.ItemNeededForEnabled,
        };
        chatTwo.Enabled = Settings.ChatTwoIntegration;

        // Nameplate marks on quest givers: off by default, behind the hook gate.
        namePlateHooks = new Game.NamePlateHooks(NamePlateGui, Session, gate, Log)
        {
            LivePins = LivePins,
            PinsVersion = () => queryRunner.PinsVersion,
            Moonlit = () => moonlit.Catalog,
        };
        namePlateHooks.Enabled = Settings.NamePlateMarks;

        if (configWindow is { } settings)
        {
            Game.ChatTwoIpc chatTwoIpc = chatTwo;
            Game.NamePlateHooks plates = namePlateHooks;
            settings.ChatTwoToggled = enabled => chatTwoIpc.Enabled = enabled;
            settings.NamePlateMarksToggled = enabled => plates.Enabled = enabled;
        }
    }

    private void OnOpenedTick(IFramework framework) => chatNotifier?.Tick();

    /// <summary>
    /// [Pin] on a chat line: chat speaks for the logged-in character, so its pins are the ones changed; when another
    /// character is on view the main window switches to the logged-in one first. Pinning only, never unpinning: an old
    /// line clicked twice must not undo the first click.
    /// </summary>
    private void PinFromChat(QuestRecord quest)
    {
        if (Session.LiveContentId is not { } live)
        {
            gameLinks.PrintText(Strings.ChatActionPinNeedsLogin);
            return;
        }

        if (Session.ViewedContentId != live)
        {
            Session.ViewCharacter(live);
        }

        var name = gameLinks.NameOf(quest);
        if (queryRunner.IsPinned(quest.RowId))
        {
            gameLinks.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.ChatActionAlreadyPinnedFormat, name));
        }
        else if (queryRunner.TogglePin(quest.RowId))
        {
            gameLinks.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.ChatActionPinnedFormat, name));
        }
    }

    /// <summary>The logged-in character's pins for the nameplate marks: the live set while it is on view, else its saved file.</summary>
    private IReadOnlySet<uint> LivePins()
    {
        if (Session.LiveContentId is not { } live)
        {
            return new HashSet<uint>();
        }

        if (Session.IsLive)
        {
            return queryRunner.Pinned;
        }

        var warnings = new List<string>();
        var pins = Core.Storage.PinsFile.Load(Paths.PinsFile, warnings);
        return pins.TryGetValue(live, out var list) ? new HashSet<uint>(list) : new HashSet<uint>();
    }

    /// <summary>Unwinds <see cref="InitializeInGame"/>: the tick, the nameplate hook, the Chat 2 registration and every chat link handler.</summary>
    private void DisposeInGame()
    {
        if (openedTickSubscribed)
        {
            openedTickSubscribed = false;
            Framework.Update -= OnOpenedTick;
        }

        Unwind("nameplate marks", () => namePlateHooks?.Dispose());
        Unwind("chat 2 ipc", () => chatTwo?.Dispose());
        Unwind("chat actions", () =>
        {
            if (gameLinks is not null)
            {
                gameLinks.Actions = null;
            }

            chatActions?.Dispose();
        });
    }
}
