using System;
using System.Runtime.InteropServices;

namespace Tsukimichi.Game;

/// <summary>
/// Flashes the game's taskbar button once (feature plan v7, 1.18.0, A5: a "Needs you" alert while the game is not the
/// foreground window), through <c>FlashWindowEx</c> on the game's own window (tray and caption, one flash; Windows then
/// keeps the button lit until the player comes back). The window is the game's, as the game itself holds it
/// (FFXIVClientStructs <c>Framework.Instance()->GameWindow->WindowHandle</c>), not the process's "main window", which
/// can be another top-level window of the process; "in front" means that window is the foreground one. Nothing happens
/// while the game is in front, off Windows, or when the call is missing (Wine without it). Never takes focus and never
/// raises the window. Call on the framework thread.
/// </summary>
public static class TaskbarFlash
{
    private const uint FlashAll = 0x3;

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

    /// <summary>The game's window as the game holds it; 0 when there is none yet.</summary>
    private static unsafe nint GameWindow()
    {
        var framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        if (framework == null || framework->GameWindow == null)
        {
            return 0;
        }

        return framework->GameWindow->WindowHandle;
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
