using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Tsukimichi.Core.Storage;

/// <summary>Serializer settings shared by every file Tsukimichi reads or writes, plus small tolerant-parsing helpers.</summary>
internal static class StorageJson
{
    /// <summary>camelCase properties, enums as strings, indented, unknown properties ignored, comments and trailing commas tolerated.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Parses a JSON object key as an unsigned id. Only plain decimal digits are accepted.</summary>
    public static bool TryParseKey(string key, out uint id) =>
        uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out id);

    /// <summary>Parses a JSON object key as an unsigned 16-bit id.</summary>
    public static bool TryParseKey(string key, out ushort id) =>
        ushort.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out id);

    /// <summary>Reads an id from a JSON value that is either a non-negative number or a string of digits.</summary>
    public static bool TryReadId(JsonNode? node, out uint id)
    {
        id = 0;
        if (node is not JsonValue value)
        {
            return false;
        }

        if (value.TryGetValue<uint>(out id))
        {
            return true;
        }

        return value.TryGetValue<string>(out var text) && TryParseKey(text, out id);
    }

    /// <summary>Reads a string property; returns null when absent, JSON null, or not a string.</summary>
    public static string? ReadString(JsonObject obj, string name) =>
        obj.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

    /// <summary>Reads a UTC timestamp property. Absent or null yields (true, null); an unparseable value yields false.</summary>
    public static bool TryReadUtc(JsonObject obj, string name, out DateTime? result)
    {
        result = null;
        if (!obj.TryGetPropertyValue(name, out var node) || node is null)
        {
            return true;
        }

        if (node is not JsonValue value || !value.TryGetValue<string>(out var text))
        {
            return false;
        }

        if (!DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }
}
