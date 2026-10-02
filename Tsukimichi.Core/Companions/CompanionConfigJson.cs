using System.Globalization;
using System.Text.Json;

namespace Tsukimichi.Core.Companions;

/// <summary>
/// Reads one setting out of another plugin's configuration file (Dalamud's <c>pluginConfigs/&lt;InternalName&gt;.json</c>,
/// written by the plugin itself with Newtonsoft.Json). Read only: Tsukimichi never writes another plugin's file; a
/// setting it may change goes through that plugin's own IPC. Pure, so the path rules are tested without a file.
/// <para>
/// A path is the property names from the root joined by dots (<c>General.CombatModule</c>). Each name is matched exactly
/// first, then ignoring case. A scalar reads as text: <c>true</c>/<c>false</c>, a number as written (an enum Newtonsoft
/// saved as its number stays that number), a string as itself, <c>null</c> as null. An object or array reads as its
/// JSON text, so a rule can still say "not empty".
/// </para>
/// </summary>
public static class CompanionConfigJson
{
    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Parses a configuration file's text; null when it is not JSON.</summary>
    public static JsonDocument? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(text, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The setting at <paramref name="path"/> as text; <see cref="SetupReading.Missing"/> when the file has no such
    /// setting (the plugin's default then applies).
    /// </summary>
    public static SetupReading Read(JsonElement root, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var current = root;
        foreach (var name in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !TryProperty(current, name, out current))
            {
                return SetupReading.Missing;
            }
        }

        return SetupReading.Of(Text(current));
    }

    /// <summary>Reads <paramref name="path"/> from a file's text; <see cref="SetupReading.Unread"/> when the text is not JSON.</summary>
    public static SetupReading Read(string text, string path)
    {
        using var document = Parse(text);
        return document is null ? SetupReading.Unread : Read(document.RootElement, path);
    }

    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? Text(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var whole)
            ? whole.ToString(CultureInfo.InvariantCulture)
            : element.GetRawText(),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => element.GetRawText(),
    };
}
