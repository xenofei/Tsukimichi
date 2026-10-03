using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Config;

// ---- 1.16.0: themes (feature plan v7 T1) ----
public sealed partial class Configuration
{
    /// <summary>
    /// The saved appearance as read from the file; null in a configuration from before 1.16.0. Read and written through
    /// <see cref="AppearanceJson"/>, so fields a newer build saved there survive this build's save.
    /// </summary>
    [Newtonsoft.Json.JsonProperty(nameof(Appearance))]
    [Newtonsoft.Json.JsonConverter(typeof(AppearanceJsonConverter))]
    private AppearanceConfig? appearance;

    /// <summary>
    /// The appearance (docs/research/plan-v7/theme-system.md §5.3): the theme, the user's overrides of its moons, palette
    /// and frames, and high contrast. A configuration from before 1.16.0 has none, and gets one built from its Moon style,
    /// Moon colours and Follow Dalamud colours (<see cref="AppearanceMigration.FromLegacy"/>). Those three settings are
    /// still saved, written from this on every <see cref="Save"/>, so a downgrade finds the look it knows; read this, not them.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public AppearanceConfig Appearance
    {
        get => appearance ??= AppearanceMigration.FromLegacy(new LegacyAppearance(MoonStyle, GlyphPalette, FollowDalamudColours));
        set => appearance = value ?? throw new System.ArgumentNullException(nameof(value));
    }

    /// <summary>Writes the 1.15 look settings from <see cref="Appearance"/> (for a downgrade); <see cref="Save"/> calls it.</summary>
    private void SyncLegacyAppearance()
    {
        var legacy = AppearanceMigration.ToLegacy(Appearance);
        MoonStyle = legacy.MoonStyle;
        GlyphPalette = legacy.GlyphPalette;
        FollowDalamudColours = legacy.FollowDalamudColours;
    }

    /// <summary>Migrates (on first read) and tidies the appearance at load; logs keys this build does not know, once.</summary>
    private static void LoadAppearance(Configuration config, Dalamud.Plugin.Services.IPluginLog? log)
    {
        var migrated = config.appearance is null;
        var unknown = AppearanceMigration.Sanitize(config.Appearance);
        if (migrated)
        {
            log?.Information("Appearance migrated from Moon style {MoonStyle}, Moon colours {Palette}, Follow Dalamud colours {Follow}", config.MoonStyle, config.GlyphPalette, config.FollowDalamudColours);
        }

        if (unknown.Count > 0)
        {
            log?.Warning("Appearance keys this build does not know draw as the theme's own: {Keys}", string.Join(", ", unknown));
        }
    }
}

/// <summary>
/// Hands the settings file's "Appearance" object to <see cref="AppearanceJson"/> as JSON text and writes back what it
/// returns, so the fields a newer build saved (<see cref="AppearanceConfig.Unknown"/>) go back into the file unchanged.
/// Strings are read as written (no date parsing), so an unknown field's text never changes shape on the way through.
/// </summary>
internal sealed class AppearanceJsonConverter : Newtonsoft.Json.JsonConverter<AppearanceConfig?>
{
    public override AppearanceConfig? ReadJson(Newtonsoft.Json.JsonReader reader, System.Type objectType, AppearanceConfig? existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
        if (reader.TokenType == Newtonsoft.Json.JsonToken.Null)
        {
            return null;
        }

        var dates = reader.DateParseHandling;
        reader.DateParseHandling = Newtonsoft.Json.DateParseHandling.None;
        try
        {
            return AppearanceJson.Read(Newtonsoft.Json.Linq.JToken.ReadFrom(reader).ToString(Newtonsoft.Json.Formatting.None));
        }
        finally
        {
            reader.DateParseHandling = dates;
        }
    }

    public override void WriteJson(Newtonsoft.Json.JsonWriter writer, AppearanceConfig? value, Newtonsoft.Json.JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        using var text = new System.IO.StringReader(AppearanceJson.Write(value));
        using var json = new Newtonsoft.Json.JsonTextReader(text) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None };
        Newtonsoft.Json.Linq.JToken.ReadFrom(json).WriteTo(writer);
    }
}
