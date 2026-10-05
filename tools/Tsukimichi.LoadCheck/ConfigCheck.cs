using Dalamud.Configuration;
using Newtonsoft.Json;
using Tsukimichi.Config;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.LoadCheck;

/// <summary>
/// Old configuration files still load: a 1.22 <c>config.json</c> with the portrait pack's fields (the first-run offer's
/// <c>PortraitPackOfferAnswered</c>, Giver portraits on Game art + pack), read the way Dalamud reads a plugin's
/// configuration (Newtonsoft, type names on objects). The obsolete field is read and dropped, never written again.
/// </summary>
internal static class ConfigCheck
{
    private const string Old122 = """
        {
          "$type": "Tsukimichi.Config.Configuration, Tsukimichi",
          "Version": 1,
          "GiverPortraits": 2,
          "PortraitPackOfferAnswered": true,
          "LastSeenVersion": "1.22.0"
        }
        """;

    /// <summary>Adds a line to <paramref name="failures"/> for each way an old file does not load as it should.</summary>
    public static void Run(List<string> failures)
    {
        var settings = new JsonSerializerSettings
        {
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            TypeNameHandling = TypeNameHandling.Objects,
        };

        try
        {
            if (JsonConvert.DeserializeObject<IPluginConfiguration>(Old122, settings) is not Configuration config)
            {
                failures.Add("config: a 1.22 config.json with the portrait pack's fields did not load as a Configuration");
                return;
            }

            if (config.GiverPortraits != GiverPortraitMode.GameArtAndPack || config.LastSeenVersion != "1.22.0")
            {
                failures.Add($"config: a 1.22 config.json loaded with Giver portraits {config.GiverPortraits} and last seen version '{config.LastSeenVersion}'");
            }

            var saved = JsonConvert.SerializeObject(config, Formatting.Indented, settings);
            if (saved.Contains("PortraitPackOfferAnswered", StringComparison.Ordinal))
            {
                failures.Add("config: the obsolete PortraitPackOfferAnswered is written again on save");
            }

            Console.WriteLine("  config: a 1.22 config.json with the portrait pack's fields loads, and the old flag is dropped on save");
        }
        catch (Exception ex)
        {
            failures.Add($"config: a 1.22 config.json with the portrait pack's fields threw {ex.GetType().Name}: {ex.Message}");
        }
    }
}
