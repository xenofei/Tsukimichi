using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Commands;
using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// Plugin entry point. Service wiring, windows and commands are added in later tasks.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    // UI
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    // /UI

    // UI
    private readonly WindowSystem windowSystem = new("Tsukimichi");
    private readonly GlyphDebugWindow glyphDebugWindow;
    private readonly TsukimichiCommand command;
    // /UI

    public Plugin()
    {
        Log.Information("Tsukimichi loaded (config directory: {Dir})", PluginInterface.GetPluginConfigDirectory());

        // UI
        glyphDebugWindow = new GlyphDebugWindow();
        windowSystem.AddWindow(glyphDebugWindow);
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;

        // Until the main window lands, a bare /tsukimichi toggles the glyph sheet.
        command = new TsukimichiCommand(CommandManager, toggleMainWindow: glyphDebugWindow.Toggle, toggleGlyphWindow: glyphDebugWindow.Toggle);
        // /UI
    }

    public void Dispose()
    {
        // UI
        command.Dispose();
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        windowSystem.RemoveAllWindows();
        // /UI

        Log.Information("Tsukimichi unloaded");
    }
}
