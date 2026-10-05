using System;
using System.Diagnostics;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Game;

/// <summary>
/// Adding Tsukimichi for Umbra to Umbra, and removing it again, for the card (<see cref="Ui.UmbraAddonCard"/>) and
/// Settings › About › Umbra: the plugin's side of <see cref="UmbraSetupCoordinator"/> (Core, tested). It saves the record
/// book in Tsukimichi's settings on the framework thread, feeds the add-on's IPC hello to the wait each frame, refuses
/// to start anything that restarts Umbra outside a quiet moment (<see cref="UmbraAddonSetup.CanChangeNow"/>), and opens
/// Umbra's own settings. <see cref="AddToUmbra"/> is called only from the card's "Agree and add" and
/// <see cref="RemoveFromUmbra"/> only from Settings' Remove confirmation (source-linted).
/// </summary>
public sealed class UmbraAddonSetupService : IDisposable
{
    private readonly UmbraSetupCoordinator coordinator;
    private readonly Configuration settings;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IFramework framework;
    private readonly UmbraProbe umbra;
    private readonly IPluginLog log;

    public UmbraAddonSetupService(IUmbraControl control, Configuration settings, IDalamudPluginInterface pluginInterface, IFramework framework, UmbraProbe umbra, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(control);
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.umbra = umbra ?? throw new ArgumentNullException(nameof(umbra));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        coordinator = new UmbraSetupCoordinator(control, UmbraSetupBook.FromData(settings.UmbraSetupRecords), Save, line => log.Information(line));
        framework.Update += Tick;
    }

    /// <summary>What the player is doing now; set by the plugin. Null allows any moment.</summary>
    public Func<WhatsNewMoment>? Moment { get; set; }

    /// <summary>What Tsukimichi changed in Umbra and has not undone, per Umbra profile.</summary>
    public UmbraSetupBook Book => coordinator.Book;

    /// <summary>What runs now and how the last runs ended.</summary>
    public UmbraSetupView View => coordinator.View;

    /// <summary>The last look at Umbra for the confirmation.</summary>
    public UmbraSetupPreview Preview => coordinator.Preview;

    /// <summary>Something runs.</summary>
    public bool Busy => coordinator.Busy;

    /// <summary>
    /// Umbra's configuration profile for the logged-in character, as its saved settings were last read (Umbra's
    /// profiles.json); null before a read. The Remove itself checks the live profile (<see cref="UmbraSetupRunner.Remove"/>).
    /// </summary>
    public string? CurrentProfile => umbra.ProfileName;

    /// <summary>Anything that restarts Umbra's toolbar may start now: not in a fight, a duty, a cutscene or a loading screen.</summary>
    public bool CanChangeNow => Moment?.Invoke() is not { } moment || UmbraAddonSetup.CanChangeNow(moment);

    /// <summary>Looks at Umbra (changes nothing) for the confirmation.</summary>
    public void RefreshPreview() => _ = coordinator.RefreshPreview();

    /// <summary>"Agree and add": false when something runs, or it is not a quiet moment. Call only from the Agree button (source-linted).</summary>
    public bool AddToUmbra() => CanChangeNow && coordinator.AddToUmbra() is not null;

    /// <summary>"Remove from Umbra": false when something runs, there is nothing to undo, or it is not a quiet moment. Call only from Settings' Remove confirmation (source-linted).</summary>
    public bool RemoveFromUmbra() => CanChangeNow && coordinator.RemoveFromUmbra() is not null;

    /// <summary>Opens Umbra's own settings window through Dalamud; false when Umbra isn't loaded or has none.</summary>
    public bool OpenUmbraSettings()
    {
        try
        {
            var plugin = pluginInterface.InstalledPlugins.FirstOrDefault(p => p.IsLoaded && string.Equals(p.InternalName, UmbraSettings.InternalName, StringComparison.Ordinal));
            if (plugin is not { HasConfigUi: true })
            {
                return false;
            }

            plugin.OpenConfigUi();
            return true;
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Umbra's settings could not be opened");
            return false;
        }
    }

    public void Dispose()
    {
        coordinator.Stop();
        framework.Update -= Tick;
    }

    private void Tick(IFramework fw) =>
        coordinator.Tick(umbra.AddonVersion is not null, Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);

    /// <summary>Saves the book in Tsukimichi's settings on the framework thread; also while Tsukimichi unloads.</summary>
    private void Save(UmbraSetupBook book)
    {
        _ = framework.RunOnFrameworkThread(() =>
        {
            settings.UmbraSetupRecords = book.ToData();
            try
            {
                settings.Save(pluginInterface);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Umbra setup: could not save what was changed in Umbra");
            }
        });
    }
}
