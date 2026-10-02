using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.HandIn;

namespace Tsukimichi.Game;

/// <summary>
/// "Gather with GatherBuddy" (feature plan v5 decision 1, research C7 A): runs GatherBuddy's own chat command through
/// <see cref="ICommandManager.ProcessCommand"/>, exactly as if the player typed it. <c>/gather &lt;item&gt;</c> marks the
/// nearest node, teleports to the closest aetheryte and equips gathering gear; <c>/gatherfish &lt;fish&gt;</c> does the
/// same for a fishing spot. Both GatherBuddy (Ottermandias/GatherBuddy, internal name <c>GatherBuddy</c>,
/// <c>GatherBuddy.Commands.cs</c> at 1e39592) and GatherBuddy Reborn (FFXIV-CombatReborn/GatherBuddyReborn, internal
/// name <c>GatherBuddyReborn</c>, a204f2f) register those two commands. Only ever run from the player's click.
/// </summary>
public sealed class GatherBuddyCommands : IDisposable
{
    public const string GatherBuddyInternalName = "GatherBuddy";
    public const string RebornInternalName = "GatherBuddyReborn";

    private readonly ICommandManager commands;
    private readonly IPluginLog log;
    private readonly PluginPresence gatherBuddy;
    private readonly PluginPresence reborn;

    public GatherBuddyCommands(IDalamudPluginInterface pluginInterface, ICommandManager commands, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        gatherBuddy = new PluginPresence(pluginInterface, log, GatherBuddyInternalName);
        reborn = new PluginPresence(pluginInterface, log, RebornInternalName);
    }

    /// <summary>Which GatherBuddy is loaded (Reborn first); <see cref="GatherPlugin.None"/> when neither.</summary>
    public GatherPlugin Plugin => HandInActions.GatherPluginOf(gatherBuddy.Loaded, reborn.Loaded);

    /// <summary>Runs the command (<see cref="HandInActions.GatherCommandFor"/>). False when no GatherBuddy is loaded or no handler took it.</summary>
    public bool Run(string command)
    {
        if (Plugin == GatherPlugin.None || string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        try
        {
            var handled = commands.ProcessCommand(command);
            log.Information("Handed \"{Command}\" to GatherBuddy ({Handled})", command, handled ? "handled" : "not handled");
            return handled;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "GatherBuddy command failed");
            return false;
        }
    }

    public void Dispose()
    {
        gatherBuddy.Dispose();
        reborn.Dispose();
    }
}
