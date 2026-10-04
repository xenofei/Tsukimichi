using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tsukimichi.Game;

/// <summary>
/// Flashes the game's taskbar button once (feature plan v7, 1.18.0, A5: a "Needs you" alert while the game is not the
/// foreground window), through <c>FlashWindowEx</c> on the game's own main window (tray and caption, one flash; Windows
/// then keeps the button lit until the player comes back). Nothing happens while the game is in front, off Windows, or
/// when the call is missing (Wine without it). Never takes focus and never raises the window.
/// </summary>
public static class TaskbarFlash
{
    private const uint FlashAll = 0x3;

    private static nint gameWindow;

    /// <summary>Flashes the game's taskbar button once unless the game is the foreground window; false when it did not.</summary>
    public static bool FlashIfBackground()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            var window = GameWindow();
            if (window == 0 || GetForegroundWindow() == window)
            {
                return false;
            }

            var info = new FlashInfo
            {
                Size = (uint)Marshal.SizeOf<FlashInfo>(),
                Window = window,
                Flags = FlashAll,
                Count = 1,
                Timeout = 0,
            };
            return FlashWindowEx(ref info);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>The game's main window, read once and kept while it lives.</summary>
    private static nint GameWindow()
    {
        if (gameWindow == 0)
        {
            using var process = Process.GetCurrentProcess();
            gameWindow = process.MainWindowHandle;
        }

        return gameWindow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public nint Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashInfo info);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
}
