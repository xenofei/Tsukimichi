using System.IO.Compression;
using System.Text.Json;

namespace Tsukimichi.Core.Umbra;

/// <summary>
/// Umbra's toolbar as its saved settings describe it (plan v8 M3; spec-1.22 M3 "Keeping clear"), read-only. Umbra
/// keeps one settings file per profile in its plugin config folder (<c>pluginConfigs/Umbra/&lt;profile&gt;.profile.json</c>),
/// a flat JSON object of its config variables; these are the keys read:
/// <list type="bullet">
/// <item><c>Toolbar.Enabled</c> (bool): the toolbar is shown at all.</item>
/// <item><c>Toolbar.IsTopAligned</c> (bool): the toolbar holds the top edge; false is the bottom edge.</item>
/// <item><c>Toolbar.IsAutoHideEnabled</c> (bool): the toolbar slides away until the pointer nears it.</item>
/// <item><c>Toolbar.IsStretched</c> (bool): the toolbar spans the screen; false is Umbra's floating, centred bar.</item>
/// <item><c>Toolbar.Height</c> (int, logical px, 32 by default) and <c>Toolbar.YOffset</c> (int, px from its edge).</item>
/// <item><c>General.UiScale</c> (int, percent, 100 by default): Umbra's own UI scale.</item>
/// </list>
/// </summary>
/// <param name="Enabled">The toolbar is shown.</param>
/// <param name="TopAligned">It holds the top edge (else the bottom).</param>
/// <param name="AutoHide">It hides until the pointer nears it.</param>
/// <param name="Stretched">It spans the screen (false: floating).</param>
/// <param name="Height">Its height in Umbra's logical px.</param>
/// <param name="YOffset">Its distance from its edge in Umbra's logical px.</param>
/// <param name="UiScalePercent">Umbra's UI scale in percent.</param>
public sealed record UmbraToolbar(bool Enabled, bool TopAligned, bool AutoHide, bool Stretched, int Height, int YOffset, int UiScalePercent)
{
    /// <summary>Umbra's default toolbar height (its <c>Toolbar.Height</c> default and the 32 px bar of its <c>toolbar.xml</c>).</summary>
    public const int DefaultHeight = 32;

    /// <summary>What Tsukimichi assumes while Umbra runs and its settings can't be read (spec-1.22 M3): a 32 px top bar.</summary>
    public static readonly UmbraToolbar Assumed = new(Enabled: true, TopAligned: true, AutoHide: false, Stretched: true, DefaultHeight, YOffset: 0, UiScalePercent: 100);

    /// <summary>Umbra's UI scale as a factor (1 at 100 %), held to 0.5–4 so a stray value never pushes a window off screen.</summary>
    public float Scale => Math.Clamp(UiScalePercent / 100f, 0.5f, 4f);

    /// <summary>
    /// Whether the toolbar holds an edge (spec-1.22 M3): shown, not auto-hidden and not floating. A floating or
    /// auto-hidden bar holds no edge, so nothing moves for it.
    /// </summary>
    public bool HoldsEdge => Enabled && !AutoHide && Stretched && Height > 0;
}

/// <summary>Umbra's saved colour profile in use, by role name (Umbra's own: "Window.Background", "Window.Text", …).</summary>
/// <param name="Name">The profile's name ("Umbra (built-in)", "YoRHa Light (built-in)", or the player's own).</param>
/// <param name="Colors">Each role's colour as Umbra stores it: 0xAABBGGRR.</param>
public sealed record UmbraColorProfile(string Name, IReadOnlyDictionary<string, uint> Colors)
{
    /// <summary>A role's colour as RGBA in 0–1; <paramref name="fallback"/> when the profile lacks it.</summary>
    public System.Numerics.Vector4 Get(string role, System.Numerics.Vector4 fallback) =>
        Colors.TryGetValue(role, out var abgr) ? UmbraSettings.FromAbgr(abgr) : fallback;

    /// <summary>Whether the profile has <paramref name="role"/>.</summary>
    public bool Has(string role) => Colors.ContainsKey(role);
}

/// <summary>What one read of Umbra's settings found.</summary>
/// <param name="Toolbar">The toolbar; null when the file could not be read.</param>
/// <param name="Colors">The colour profile in use; null when it could not be read.</param>
/// <param name="CustomPluginsOn">Umbra's custom plugins (its add-ons) are turned on.</param>
/// <param name="AddonListed">Umbra's add-on list names Tsukimichi for Umbra.</param>
/// <param name="Problem">Why the file or the profile could not be read, in a few words for the log; null when it could.</param>
public sealed record UmbraRead(UmbraToolbar? Toolbar, UmbraColorProfile? Colors, bool CustomPluginsOn, bool AddonListed, string? Problem)
{
    /// <summary>Nothing could be read.</summary>
    public static UmbraRead Failed(string problem) => new(null, null, false, false, problem);
}

/// <summary>
/// Reads Umbra's saved settings, read-only and pure (plan v8 M3 and decision 4; spec-1.22 M3): which profile file a
/// character uses, the toolbar's place, and the colour profile in use. Umbra has no API for either (research,
/// plan-v8/umbra-and-updates.md), so this reads what Umbra saved, and anything it can't read is reported, never thrown.
/// <para>
/// <b>Where.</b> <c>pluginConfigs/Umbra/profiles.json</c> maps a character's content id (as a decimal string) to a
/// profile name; that profile's settings are <c>pluginConfigs/Umbra/&lt;name&gt;.profile.json</c>, "Default" when the
/// character has none.
/// </para>
/// <para>
/// <b>Colours.</b> <c>ColorProfileName</c> names the profile in use and <c>ColorProfileData</c> holds every profile:
/// base64 of a raw deflate stream (no zlib header) of a JSON object <c>{ profile name: { role: colour } }</c>, each colour
/// a 32-bit 0xAABBGGRR number (Dear ImGui's packed order).
/// </para>
/// </summary>
public static class UmbraSettings
{
    /// <summary>Umbra's plugin internal name (its manifest's <c>InternalName</c>) and its config folder's name.</summary>
    public const string InternalName = "Umbra";

    /// <summary>The profile map's file name in Umbra's config folder.</summary>
    public const string ProfilesFile = "profiles.json";

    /// <summary>The profile a character without an entry in <see cref="ProfilesFile"/> uses.</summary>
    public const string DefaultProfile = "Default";

    /// <summary>How the add-on is named in Umbra's add-on list (its repository, <c>xenofei/Tsukimichi.Umbra</c>).</summary>
    public const string AddonName = "Tsukimichi.Umbra";

    /// <summary>The largest settings file read (Umbra's are about 20 KB; its widget data is the bulk).</summary>
    public const int MaxFileBytes = 4 * 1024 * 1024;

    /// <summary>The largest decoded colour data accepted (the five built-in profiles are about 15 KB).</summary>
    public const int MaxColorBytes = 1024 * 1024;

    /// <summary>The settings file a character uses: <c>&lt;profile&gt;.profile.json</c> from the profile map, "Default" otherwise.</summary>
    /// <param name="profilesJson">The text of <see cref="ProfilesFile"/>; null or unreadable reads as no map.</param>
    /// <param name="contentId">The logged-in character; null for none.</param>
    public static string ProfileFileName(string? profilesJson, ulong? contentId)
    {
        var name = DefaultProfile;
        if (contentId is { } id && !string.IsNullOrWhiteSpace(profilesJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(profilesJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty(id.ToString(System.Globalization.CultureInfo.InvariantCulture), out var entry)
                    && entry.ValueKind == JsonValueKind.String
                    && SafeName(entry.GetString()) is { } named)
                {
                    name = named;
                }
            }
            catch (JsonException)
            {
                // An unreadable map reads as none: the default profile.
            }
        }

        return name + ".profile.json";
    }

    /// <summary>
    /// Reads one profile file's text: the toolbar and the colour profile in use. Never throws; what it can't read is
    /// left null and named in <see cref="UmbraRead.Problem"/>.
    /// </summary>
    public static UmbraRead Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return UmbraRead.Failed("empty");
        }

        if (json.Length > MaxFileBytes)
        {
            return UmbraRead.Failed("too large");
        }

        try
        {
            using var doc = JsonDocument.Parse(json.TrimStart('﻿'), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return UmbraRead.Failed("not an object");
            }

            UmbraToolbar? toolbar = null;
            if (root.TryGetProperty("Toolbar.IsTopAligned", out _) || root.TryGetProperty("Toolbar.Height", out _))
            {
                toolbar = new UmbraToolbar(
                    Bool(root, "Toolbar.Enabled", true),
                    Bool(root, "Toolbar.IsTopAligned", true),
                    Bool(root, "Toolbar.IsAutoHideEnabled", false),
                    Bool(root, "Toolbar.IsStretched", true),
                    Math.Clamp(Int(root, "Toolbar.Height", UmbraToolbar.DefaultHeight), 0, 512),
                    Math.Clamp(Int(root, "Toolbar.YOffset", 0), -4096, 4096),
                    Math.Clamp(Int(root, "General.UiScale", 100), 25, 400));
            }

            string? problem = toolbar is null ? "no toolbar settings" : null;
            UmbraColorProfile? colors = null;
            var profileName = Str(root, "ColorProfileName");
            var data = Str(root, "ColorProfileData");
            if (profileName is null || data is null)
            {
                problem ??= "no colour profile";
            }
            else if (DecodeProfiles(data) is not { } profiles)
            {
                problem ??= "colour profile data unreadable";
            }
            else if (!profiles.TryGetValue(profileName, out var chosen))
            {
                problem ??= "colour profile not found";
            }
            else
            {
                colors = new UmbraColorProfile(profileName, chosen);
            }

            var entries = Str(root, "PluginEntries") ?? string.Empty;
            return new UmbraRead(
                toolbar,
                colors,
                Bool(root, "CustomPlugins.Enabled", false),
                entries.Contains(AddonName, StringComparison.OrdinalIgnoreCase),
                problem);
        }
        catch (JsonException)
        {
            return UmbraRead.Failed("not JSON");
        }
    }

    /// <summary>
    /// Every colour profile in <c>ColorProfileData</c>: base64, raw deflate, then JSON of profile name to role to
    /// 0xAABBGGRR. Null when any step fails or the data is larger than <see cref="MaxColorBytes"/> decoded.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, uint>>? DecodeProfiles(string data)
    {
        byte[] compressed;
        try
        {
            compressed = Convert.FromBase64String(data.Trim());
        }
        catch (FormatException)
        {
            return null;
        }

        byte[] json;
        try
        {
            using var input = new MemoryStream(compressed, writable: false);
            using var inflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = inflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > MaxColorBytes)
                {
                    return null;
                }

                output.Write(buffer, 0, read);
            }

            json = output.ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var profiles = new Dictionary<string, IReadOnlyDictionary<string, uint>>(StringComparer.Ordinal);
            foreach (var profile in doc.RootElement.EnumerateObject())
            {
                if (profile.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var roles = new Dictionary<string, uint>(StringComparer.Ordinal);
                foreach (var role in profile.Value.EnumerateObject())
                {
                    if (role.Value.ValueKind == JsonValueKind.Number && role.Value.TryGetUInt32(out var color))
                    {
                        roles[role.Name] = color;
                    }
                }

                profiles[profile.Name] = roles;
            }

            return profiles;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Encodes profiles the way Umbra stores them (base64 of raw deflate of the JSON): for tests and sample files, so a
    /// fixture never needs Umbra's own output.
    /// </summary>
    public static string EncodeProfiles(IReadOnlyDictionary<string, IReadOnlyDictionary<string, uint>> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var json = JsonSerializer.SerializeToUtf8Bytes(profiles);
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(json, 0, json.Length);
        }

        return Convert.ToBase64String(output.ToArray());
    }

    /// <summary>A colour packed 0xAABBGGRR (Umbra's and Dear ImGui's order) as RGBA in 0–1.</summary>
    public static System.Numerics.Vector4 FromAbgr(uint abgr) => new(
        (abgr & 0xFF) / 255f,
        ((abgr >> 8) & 0xFF) / 255f,
        ((abgr >> 16) & 0xFF) / 255f,
        ((abgr >> 24) & 0xFF) / 255f);

    /// <summary>RGB 0xRRGGBB with alpha as Umbra packs it (0xAABBGGRR); for fixtures.</summary>
    public static uint ToAbgr(uint rgb, byte alpha = 0xFF) =>
        ((uint)alpha << 24) | ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF);

    /// <summary>A profile name usable as a file name; null for one that is empty or names a path.</summary>
    private static string? SafeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        return trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || trimmed.Contains("..", StringComparison.Ordinal) ? null : trimmed;
    }

    private static bool Bool(JsonElement root, string key, bool fallback) =>
        root.TryGetProperty(key, out var value) ? value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback,
        } : fallback;

    private static int Int(JsonElement root, string key, int fallback) =>
        root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : fallback;

    private static string? Str(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
