using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Localization;

/// <summary>Settings › Display › Plugin language.</summary>
public enum PluginLanguage
{
    /// <summary>Dalamud's UI language (<see cref="IDalamudPluginInterface.UiLanguage"/>); English where Tsukimichi has no translation.</summary>
    FollowDalamud = 0,

    /// <summary>English whatever Dalamud's language is.</summary>
    English = 1,

    /// <summary>
    /// Pseudo-localization (<see cref="Loc.PseudoLanguage"/>): English stretched by 40 % and bracketed, for checking
    /// the layout against long translations. Offered in Settings only while Shift is held.
    /// </summary>
    Pseudo = 2,
}

/// <summary>
/// Keeps <see cref="Loc"/> on the language the player asked for (V2-19): Dalamud's UI language, followed live through
/// <see cref="IDalamudPluginInterface.LanguageChanged"/>, unless Settings pins English. Quest, item, NPC and place names
/// are not this service's business: they come from the game sheets in the client's language.
/// </summary>
public sealed class LocService : IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Configuration settings;
    private readonly IFramework framework;
    private readonly IPluginLog log;

    public LocService(IDalamudPluginInterface pluginInterface, Configuration settings, IFramework framework, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.settings = settings;
        this.framework = framework;
        this.log = log;

        // Subscribed only once the first switch went through, so a throw here leaves no handler behind.
        Apply();
        pluginInterface.LanguageChanged += OnDalamudLanguageChanged;
    }

    /// <summary>The language code the settings resolve to now: "en", "ja", "de", "fr" or "qps".</summary>
    public string Resolved => settings.PluginLanguage switch
    {
        PluginLanguage.English => Loc.English,
        PluginLanguage.Pseudo => Loc.PseudoLanguage,
        _ => Loc.Resolve(pluginInterface.UiLanguage),
    };

    /// <summary>Dalamud's own UI language code, as it reports it.</summary>
    public string DalamudLanguage => pluginInterface.UiLanguage ?? string.Empty;

    /// <summary>Switches to <see cref="Resolved"/> (framework thread). Settings calls it after a change.</summary>
    public void Apply()
    {
        var language = Resolved;
        if (language == Loc.Language && Loc.Version > 0)
        {
            return;
        }

        Loc.SetLanguage(language);
        if (language is Loc.Japanese or Loc.German or Loc.French)
        {
            var translated = Loc.TranslatedCount;
            if (translated == 0)
            {
                // The satellite assembly (ja/Tsukimichi.resources.dll beside the plugin) did not load.
                log.Warning(Loc.LastReadError, "Plugin language {Language}: its resource file did not load; English is shown", language);
            }
            else
            {
                log.Information("Plugin language {Language}: {Translated} of {Keys} strings translated", language, translated, Loc.KeyCount);
            }
        }
        else
        {
            log.Debug("Plugin language {Language}", language);
        }
    }

    public void Dispose()
    {
        pluginInterface.LanguageChanged -= OnDalamudLanguageChanged;
        CoreText.Use(null);
    }

    private void OnDalamudLanguageChanged(string langCode)
    {
        if (settings.PluginLanguage != PluginLanguage.FollowDalamud)
        {
            return;
        }

        // Dalamud raises this from its settings window; the switch belongs on the framework thread with the draws.
        framework.RunOnFrameworkThread(Apply).ContinueWith(
            t => log.Warning(t.Exception?.GetBaseException(), "Plugin language could not be switched"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
