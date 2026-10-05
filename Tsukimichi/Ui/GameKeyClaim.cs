using System;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;

namespace Tsukimichi.Ui;

/// <summary>
/// Keys a window acts on, taken back from the game (the rule: never let a key reach the game while the plugin acts on
/// it). Dalamud hands every key to the game as well as to ImGui (only a text field swallows them), so a window that
/// answers Esc, or moves its focus with the arrows and Enter, would also close the game's top window, turn the
/// character or open the chat. The window claims the keys on each draw that answers them (<see cref="ClaimBack"/>,
/// <see cref="ClaimNavigation"/>); a <c>Framework.Update</c> handler (<see cref="Consume"/>) clears the claimed keys
/// from the game's key state before the game reads them, for <see cref="GraceMs"/> after the last claim. The same
/// mechanism as the main window's Esc ladder (<see cref="MainWindow.ConsumeEscape"/>) and the tour's keys.
/// </summary>
public sealed class GameKeyClaim
{
    /// <summary>How long a draw's claim stays good.</summary>
    public const long GraceMs = 250;

    private static readonly VirtualKey[] NavigationKeys =
    [
        VirtualKey.UP, VirtualKey.DOWN, VirtualKey.LEFT, VirtualKey.RIGHT, VirtualKey.RETURN, VirtualKey.SPACE, VirtualKey.TAB,
    ];

    private readonly IKeyState? keys;
    private long backAt = long.MinValue / 2;
    private long navigationAt = long.MinValue / 2;

    /// <param name="keys">The game's key state; null claims nothing (offline, or before the plugin has it).</param>
    public GameKeyClaim(IKeyState? keys)
    {
        this.keys = keys;
    }

    /// <summary>The claim over the game's own key state.</summary>
    public static GameKeyClaim ForGame() => new(Plugin.KeyState);

    /// <summary>The window answers Esc this draw (it has the keys and Esc goes back or pauses).</summary>
    public void ClaimBack() => backAt = Environment.TickCount64;

    /// <summary>The window moves its focus with the arrows, Tab, Enter and Space this draw (a menu with the keys).</summary>
    public void ClaimNavigation() => navigationAt = Environment.TickCount64;

    /// <summary>Whether Esc is claimed now (for the tests and the window's own checks).</summary>
    public bool BackClaimed => Environment.TickCount64 - backAt <= GraceMs;

    /// <summary>Whether the navigation keys are claimed now.</summary>
    public bool NavigationClaimed => Environment.TickCount64 - navigationAt <= GraceMs;

    /// <summary><c>Framework.Update</c>: clears the claimed keys from the game's key state.</summary>
    public void Consume(IFramework framework)
    {
        if (keys is not { } state)
        {
            return;
        }

        if (BackClaimed && state[VirtualKey.ESCAPE])
        {
            state[VirtualKey.ESCAPE] = false;
        }

        if (!NavigationClaimed)
        {
            return;
        }

        foreach (var key in NavigationKeys)
        {
            if (state[key])
            {
                state[key] = false;
            }
        }
    }
}
