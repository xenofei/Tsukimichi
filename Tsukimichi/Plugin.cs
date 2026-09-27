using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Commands;
using Tsukimichi.Data;
using Tsukimichi.GameData;
using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// Plugin entry point. Starts the catalog build on load; windows and commands are added in later tasks.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    // UI
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    // /UI

    private static readonly TimeSpan DisposeWait = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource catalogCts = new();

    /// <summary>The catalog build started at load. Faulted or cancelled when the build did not finish.</summary>
    internal Task<CatalogBundle> CatalogTask { get; }

    // UI
    private readonly WindowSystem windowSystem = new("Tsukimichi");
    private readonly GlyphDebugWindow glyphDebugWindow;
    private readonly TsukimichiCommand command;
    // /UI

    public Plugin()
    {
        Log.Information("Tsukimichi loaded (config directory: {Dir})", PluginInterface.GetPluginConfigDirectory());

        var loader = new LuminaCatalogLoader(DataManager, Log);
        CatalogTask = loader.BuildBundleAsync(DataManager.Language, catalogCts.Token);
        CatalogTask.ContinueWith(
            static t =>
            {
                if (t.IsCanceled)
                {
                    Log.Debug("Catalog build cancelled");
                }
                else if (t.IsFaulted)
                {
                    Log.Error(t.Exception?.GetBaseException(), "Catalog unavailable");
                }
                else
                {
                    Log.Information("Catalog ready: {Count} quests, {Sections} journal sections", t.Result.Catalog.Count, t.Result.Catalog.BySection.Count);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

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

        catalogCts.Cancel();
        try
        {
            if (!CatalogTask.Wait(DisposeWait))
            {
                Log.Warning("Catalog build did not stop within {Seconds} s", DisposeWait.TotalSeconds);
            }
        }
        catch (AggregateException)
        {
            // Cancelled or faulted builds surface here; both were already logged.
        }

        catalogCts.Dispose();
        Log.Information("Tsukimichi unloaded");
    }
}
