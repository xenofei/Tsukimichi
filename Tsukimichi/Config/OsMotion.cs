using System;
using System.Runtime.InteropServices;

namespace Tsukimichi.Config;

/// <summary>
/// The operating system's animation preference (accessibility panel B5): Windows' "Show animations in Windows"
/// switch, read through <c>SystemParametersInfo(SPI_GETCLIENTAREAANIMATION)</c>. Used only as the default of
/// <see cref="Configuration.ReduceMotion"/> until the user sets it.
/// </summary>
public static class OsMotion
{
    private const uint SpiGetClientAreaAnimation = 0x1042;

    /// <summary>
    /// True when the OS asks for no animation, false when animations are on, null when the setting cannot be read
    /// (an older Windows, Wine without the parameter, or any failure), in which case the caller keeps its value.
    /// </summary>
    public static bool? AnimationsOff()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            return SystemParametersInfo(SpiGetClientAreaAnimation, 0, out var enabled, 0) ? enabled == 0 : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            return null;
        }
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint param, out int value, uint winIni);
}
