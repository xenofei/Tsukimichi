using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// The 0.8.0 upgrade notice for the todo overlay's Locked setting (<see cref="Configuration.TodoLockNoticeDue"/>): before
/// 0.8.0 Locked only stopped the overlay from moving, now it is click-through, so a player who upgrades with it on
/// gets one chat line saying so and how to unlock, at load when already logged in or at the next login. Printed once
/// ever (<see cref="Configuration.TodoLockNoticePrinted"/>); Settings › Todo overlay keeps a line until they unlock.
/// Framework thread.
/// </summary>
public sealed class TodoLockNotice : IDisposable
{
    private readonly Configuration settings;
    private readonly IClientState clientState;
    private readonly IChatGui chat;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private bool disposed;

    public TodoLockNotice(Configuration settings, IClientState clientState, IChatGui chat, IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.chat = chat ?? throw new ArgumentNullException(nameof(chat));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        clientState.Login += TryPrint;
        TryPrint();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        clientState.Login -= TryPrint;
    }

    private void TryPrint()
    {
        if (disposed || !settings.TodoLockNoticeDue || settings.TodoLockNoticePrinted || !settings.TodoOverlayLocked || !clientState.IsLoggedIn)
        {
            return;
        }

        settings.TodoLockNoticePrinted = true;
        try
        {
            chat.Print(Strings.TodoLockUpgradeChat, Strings.ChatTag);
            settings.Save(pluginInterface);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Todo overlay lock notice failed");
        }
    }
}
