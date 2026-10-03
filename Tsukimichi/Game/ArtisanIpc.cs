using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Companions;

namespace Tsukimichi.Game;

/// <summary>
/// "Craft with Artisan" (feature plan v5 decision 1) through Artisan's IPC (internal name <c>Artisan</c>; gates as
/// registered in PunishXIV/Artisan <c>Artisan/IPC/IPC.cs</c> at 247c2df, 2026-09-25):
/// <c>Artisan.CraftItem(ushort recipeId, int amount)</c>, an action that selects the recipe and starts Endurance for
/// that many crafts, <c>Artisan.IsBusy() -&gt; bool</c> and <c>Artisan.SetEnduranceStatus(bool)</c>, which
/// <c>/tsuki stop</c> (1.11.0) calls with false to end the Endurance run CraftItem started. Crafting only ever starts on
/// the player's click on the button; a run started that way is remembered (<see cref="HandOffClaim"/>) so a stop leaves
/// Artisan's own lists alone. <see cref="IsBusy"/> is cached for <see cref="IsBusyCacheMs"/>; every call is wrapped and
/// the first failure logged.
/// </summary>
public sealed class ArtisanIpc : IDisposable
{
    public const string PluginInternalName = "Artisan";
    private const string CraftItemGate = "Artisan.CraftItem";
    private const string IsBusyGate = "Artisan.IsBusy";
    private const string SetEnduranceGate = "Artisan.SetEnduranceStatus";

    /// <summary>How long an <see cref="IsBusy"/> answer is reused before Artisan is asked again.</summary>
    public const long IsBusyCacheMs = 500;

    private readonly IPluginLog log;
    private readonly PluginPresence presence;
    private readonly ICallGateSubscriber<ushort, int, object>? craftItem;
    private readonly ICallGateSubscriber<bool>? isBusy;
    private readonly ICallGateSubscriber<bool, object>? setEndurance;
    private readonly HandOffClaim claim = new();
    private bool busyCached;
    private long? busyCheckedAt;
    private bool warned;

    public ArtisanIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        presence = new PluginPresence(pluginInterface, log, PluginInternalName);
        try
        {
            craftItem = pluginInterface.GetIpcSubscriber<ushort, int, object>(CraftItemGate);
            isBusy = pluginInterface.GetIpcSubscriber<bool>(IsBusyGate);
            setEndurance = pluginInterface.GetIpcSubscriber<bool, object>(SetEnduranceGate);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Artisan IPC subscribers unavailable");
        }
    }

    /// <summary>True while Artisan is installed and loaded.</summary>
    public bool Available => craftItem is not null && presence.Loaded;

    /// <summary>True while Artisan runs a list, Endurance or a craft; false when idle or it cannot be asked.</summary>
    public bool IsBusy
    {
        get
        {
            if (!Available || isBusy is null)
            {
                busyCheckedAt = null;
                return false;
            }

            var now = Environment.TickCount64;
            if (busyCheckedAt is { } at && now - at < IsBusyCacheMs)
            {
                return busyCached;
            }

            try
            {
                busyCached = isBusy.InvokeFunc();
            }
            catch (IpcNotReadyError)
            {
                presence.Invalidate();
                busyCached = false;
            }
            catch (Exception ex)
            {
                WarnOnce(ex, "Artisan.IsBusy failed");
                busyCached = false;
            }

            busyCheckedAt = now;
            return busyCached;
        }
    }

    /// <summary>Asks Artisan to craft <paramref name="amount"/> of a recipe. False when it is absent or the call threw.</summary>
    public bool Craft(uint recipeId, int amount)
    {
        if (!Available || craftItem is null || recipeId is 0 or > ushort.MaxValue || amount < 1)
        {
            return false;
        }

        try
        {
            craftItem.InvokeAction((ushort)recipeId, amount);
            busyCheckedAt = null;
            claim.Claim(Environment.TickCount64);
            log.Information("Handed recipe {RecipeId} x{Amount} to Artisan", recipeId, amount);
            return true;
        }
        catch (IpcNotReadyError)
        {
            presence.Invalidate();
            return false;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Artisan.CraftItem failed");
            return false;
        }
    }

    /// <summary>True while a craft Tsukimichi handed to Artisan may still be under way (<see cref="TrackHandOff"/> keeps it current).</summary>
    public bool HandOffClaimed => claim.Claimed;

    /// <summary>
    /// True while Artisan crafts a run Tsukimichi started; ends the claim once that run is over. Called each frame while
    /// <see cref="HandOffClaimed"/>, and by <c>/tsuki stop</c>.
    /// </summary>
    public bool TrackHandOff() => claim.Observe(IsBusy, Environment.TickCount64);

    /// <summary>
    /// Ends the Endurance run Tsukimichi started (<c>Artisan.SetEnduranceStatus(false)</c>); Artisan finishes the craft
    /// in hand. False when Artisan is absent, lacks the gate or the call threw.
    /// </summary>
    public bool Stop()
    {
        if (!Available || setEndurance is null)
        {
            return false;
        }

        try
        {
            setEndurance.InvokeAction(false);
            busyCheckedAt = null;
            claim.Release();
            log.Information("Asked Artisan to end the craft Tsukimichi started");
            return true;
        }
        catch (IpcNotReadyError)
        {
            presence.Invalidate();
            return false;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Artisan.SetEnduranceStatus failed");
            return false;
        }
    }

    public void Dispose() => presence.Dispose();

    private void WarnOnce(Exception ex, string message)
    {
        if (warned)
        {
            log.Debug(ex, message);
            return;
        }

        warned = true;
        log.Warning(ex, message);
    }
}
