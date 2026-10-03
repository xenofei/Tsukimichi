using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Portraits;

/// <summary>A face the game does not name, named by hand: an icon of <paramref name="Source"/> showing <paramref name="Name"/>.</summary>
/// <param name="Era">The expansion the face belongs to (ExVersion row: 0 A Realm Reborn … 5 Dawntrail); null takes the giver's first quest's.</param>
public sealed record CuratedFace(string Name, PortraitSource Source, byte? Era, string Note);

/// <summary>A source's name for an NPC mapped to the name the givers carry (<c>PAPARIMO</c> → "Papalymo").</summary>
public sealed record CuratedPortraitAlias(string Name, string Note);

/// <summary>
/// A wrong match kept out: the giver (by NPC id, or every giver of a name) never wears <paramref name="Icons"/>, or
/// any portrait at all when the list is empty.
/// </summary>
/// <param name="GiverId">The ENpcResident row id; 0 when the block is by name.</param>
/// <param name="NormalizedName">The giver name's normal form (<see cref="PortraitNames.Normalize"/>); empty when by id.</param>
public sealed record CuratedPortraitBlock(uint GiverId, string NormalizedName, IReadOnlyList<uint> Icons, string Note)
{
    /// <summary>Whether this block keeps <paramref name="icon"/> from the giver.</summary>
    public bool Blocks(uint icon) => Icons.Count == 0 || Icons.Contains(icon);
}

/// <summary>A giver pinned to one portrait, ahead of every rule (an era pick the rules get wrong).</summary>
public sealed record CuratedPortraitPin(uint Icon, PortraitSource Source, string Note);

/// <summary>
/// The face seed of a custom delivery portrait (1.15 design spec A2.4): the hr px inside the client's face from which
/// DataGen floods the figure before it keys out the emblem script around it.
/// </summary>
public sealed record CuratedDeliveryKey(int SeedX, int SeedY, string Note);

/// <summary>
/// The curated overlay for giver portraits, <c>curated/giver_portraits.json</c> (feature plan v7 F3): crops per family
/// and per icon, names for faces the game leaves unnamed, aliases between a source's spelling and the givers', wrong
/// matches blocked, and pins. Every entry carries a note; an entry that is malformed is skipped with a warning.
/// <code>
/// {
///   "schema": 1,
///   "note": "...",
///   "crops": { "BattleTalk": { "box": [ 164, 154, 172 ] } },                                       (replaces the family default)
///   "iconCrops": { "87019": { "box": [ 70, 62, 75 ], "note": "..." }, "72659": { "eyes": [ 0.5, 0.564 ], "chin": 0.68, "note": "..." } },
///   "faces": { "73270": { "name": "Sphene", "source": "BattleTalk", "era": 5, "note": "..." } },   (source defaults to BattleTalk, era optional)
///   "aliases": { "PAPARIMO": { "name": "Papalymo", "note": "..." } },
///   "blocks": { "Clive": { "icons": [ 87371 ], "note": "..." }, "1012345": { "note": "..." } },  (by giver name or ENpcResident id; no icons = every portrait)
///   "pins": { "1001234": { "icon": 73034, "source": "BattleTalk", "note": "..." } },              (by ENpcResident id)
///   "deliveryKeys": { "61661": { "seed": [ 190, 225 ], "note": "..." } }                            (the face seed DataGen keys the emblem script from)
/// }
/// </code>
/// A crop is written three ways: <c>"box": [x, y, side]</c> in px of the family's hr texture, as the design spec writes
/// boxes; <c>"eyes": [u, v]</c> and <c>"chin": v</c> measured on the texture, which the framing rule turns into a box
/// (<see cref="PortraitFraming"/>); or <c>"crop": [u0, v0, u1, v1]</c> in texture coordinates. The keep masks DataGen
/// writes from the delivery keys sit beside the file, in <c>portrait_masks/</c> with their manifest
/// <c>masks.json</c> (<see cref="Masks"/>).
/// </summary>
public sealed record PortraitCuration
{
    /// <summary>Nothing curated: the defaults, no names, aliases, blocks or pins.</summary>
    public static readonly PortraitCuration Empty = new();

    public IReadOnlyDictionary<PortraitSource, PortraitCrop> Crops { get; init; } = new Dictionary<PortraitSource, PortraitCrop>();

    public IReadOnlyDictionary<uint, PortraitCrop> IconCrops { get; init; } = new Dictionary<uint, PortraitCrop>();

    /// <summary>Faces named by hand, by icon id.</summary>
    public IReadOnlyDictionary<uint, CuratedFace> Faces { get; init; } = new Dictionary<uint, CuratedFace>();

    /// <summary>Aliases by the source name's normal form.</summary>
    public IReadOnlyDictionary<string, CuratedPortraitAlias> Aliases { get; init; } = new Dictionary<string, CuratedPortraitAlias>();

    public IReadOnlyList<CuratedPortraitBlock> Blocks { get; init; } = [];

    /// <summary>Pins by ENpcResident id.</summary>
    public IReadOnlyDictionary<uint, CuratedPortraitPin> Pins { get; init; } = new Dictionary<uint, CuratedPortraitPin>();

    /// <summary>The face seed (hr px) DataGen keys each delivery portrait's emblem script from, by icon id.</summary>
    public IReadOnlyDictionary<uint, CuratedDeliveryKey> DeliveryKeys { get; init; } = new Dictionary<uint, CuratedDeliveryKey>();

    /// <summary>
    /// The shipped keep masks by icon id, from <c>portrait_masks/masks.json</c> beside the file; only masks whose file
    /// exists are listed. A delivery portrait without one is never drawn (its emblem script would show).
    /// </summary>
    public IReadOnlyDictionary<uint, PortraitMask> Masks { get; init; } = new Dictionary<uint, PortraitMask>();

    /// <summary>The folder beside <see cref="CuratedData.GiverPortraitsFileName"/> that holds the keep masks.</summary>
    public const string MaskFolder = "portrait_masks";

    /// <summary>The masks' manifest in <see cref="MaskFolder"/>.</summary>
    public const string MaskManifestFileName = "masks.json";

    /// <summary>The family crops with this file's replacements and icon crops applied.</summary>
    public PortraitCrops CropTable() => new(Crops, IconCrops);

    /// <summary>
    /// Reads the file at <paramref name="path"/> and the mask manifest beside it. A missing file is <see cref="Empty"/>
    /// (with the masks) without a warning; one that does not parse as strict JSON, or whose root is not an object, is
    /// <see cref="Empty"/> with one warning.
    /// </summary>
    public static PortraitCuration Load(string path, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        var fileName = Path.GetFileName(path);
        var masks = LoadMasks(Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, MaskFolder), warnings);
        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            if (ioError is not null)
            {
                warnings.Add($"{fileName} could not be read: {ioError}");
            }

            return Empty with { Masks = masks };
        }

        JsonObject root;
        try
        {
            if (JsonNode.Parse(text, documentOptions: CuratedData.StrictOptions) is not JsonObject obj)
            {
                warnings.Add($"{fileName}: root is not a JSON object; file ignored.");
                return Empty with { Masks = masks };
            }

            root = obj;
        }
        catch (JsonException ex)
        {
            warnings.Add($"{fileName} could not be parsed: {ex.Message}");
            return Empty with { Masks = masks };
        }

        var crops = new Dictionary<PortraitSource, PortraitCrop>();
        ForEach(root, "crops", fileName, warnings, (key, node, warn) =>
        {
            if (!PortraitSources.TryParse(key, out var source))
            {
                warn("key is not a portrait family (TrustBust, TripleTriadCard, BattleTalk, Delivery, TrustStrip)");
                return;
            }

            if (node is not JsonObject obj || !TryReadAnyCrop(obj, source, out var crop))
            {
                warn(CropFormats);
                return;
            }

            crops[source] = crop;
        });

        var iconCrops = new Dictionary<uint, PortraitCrop>();
        ForEach(root, "iconCrops", fileName, warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint icon) || icon == 0)
            {
                warn("key is not an icon id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            if (!TryReadAnyCrop(obj, PortraitSources.FamilyOfIcon(icon), out var crop))
            {
                warn(CropFormats + " (the icon must be of a known family)");
                return;
            }

            if (!HasNote(obj, warn))
            {
                return;
            }

            iconCrops[icon] = crop;
        });

        var faces = new Dictionary<uint, CuratedFace>();
        ForEach(root, "faces", fileName, warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint icon) || icon == 0)
            {
                warn("key is not an icon id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            var name = StorageJson.ReadString(obj, "name")?.Trim();
            if (string.IsNullOrEmpty(name) || PortraitNames.Normalize(name).Length == 0)
            {
                warn("name is missing");
                return;
            }

            var source = PortraitSource.BattleTalk;
            if (obj.ContainsKey("source") && !PortraitSources.TryParse(StorageJson.ReadString(obj, "source"), out source))
            {
                warn("source is not a portrait family");
                return;
            }

            byte? era = null;
            if (obj.TryGetPropertyValue("era", out var eraNode))
            {
                if (!StorageJson.TryReadId(eraNode, out var eraValue) || eraValue > MaxEra)
                {
                    warn($"era must be an expansion number from 0 to {MaxEra}");
                    return;
                }

                era = (byte)eraValue;
            }

            if (!HasNote(obj, warn, out var note))
            {
                return;
            }

            faces[icon] = new CuratedFace(name, source, era, note);
        });

        var aliases = new Dictionary<string, CuratedPortraitAlias>(StringComparer.Ordinal);
        ForEach(root, "aliases", fileName, warnings, (key, node, warn) =>
        {
            var from = PortraitNames.Normalize(key);
            if (from.Length == 0)
            {
                warn("key has no letters");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            var name = StorageJson.ReadString(obj, "name")?.Trim();
            if (string.IsNullOrEmpty(name) || PortraitNames.Normalize(name).Length == 0)
            {
                warn("name is missing");
                return;
            }

            if (PortraitNames.Normalize(name) == from)
            {
                warn("name is the key itself");
                return;
            }

            if (aliases.ContainsKey(from))
            {
                warn("another key has the same letters");
                return;
            }

            if (!HasNote(obj, warn, out var note))
            {
                return;
            }

            aliases[from] = new CuratedPortraitAlias(name, note);
        });

        var blocks = new List<CuratedPortraitBlock>();
        ForEach(root, "blocks", fileName, warnings, (key, node, warn) =>
        {
            uint giverId = 0;
            var normalized = string.Empty;
            if (!StorageJson.TryParseKey(key, out giverId))
            {
                normalized = PortraitNames.Normalize(key);
                if (normalized.Length == 0)
                {
                    warn("key is neither an ENpcResident id nor a name");
                    return;
                }
            }
            else if (giverId == 0)
            {
                warn("key is not an ENpcResident id");
                return;
            }

            if (node is not JsonObject obj)
            {
                warn("value is not an object");
                return;
            }

            var icons = new List<uint>();
            if (obj.TryGetPropertyValue("icons", out var iconsNode))
            {
                if (iconsNode is not JsonArray array || array.Count == 0)
                {
                    warn("icons must be a non-empty array of icon ids (leave it out to block every portrait)");
                    return;
                }

                foreach (var element in array)
                {
                    if (!StorageJson.TryReadId(element, out var icon) || icon == 0 || icons.Contains(icon))
                    {
                        warn($"icon '{element}' is not an icon id, or repeats");
                        return;
                    }

                    icons.Add(icon);
                }
            }

            if (!HasNote(obj, warn, out var note))
            {
                return;
            }

            blocks.Add(new CuratedPortraitBlock(giverId, normalized, icons, note));
        });

        var pins = new Dictionary<uint, CuratedPortraitPin>();
        ForEach(root, "pins", fileName, warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint giverId) || giverId == 0)
            {
                warn("key is not an ENpcResident id");
                return;
            }

            if (node is not JsonObject obj || !obj.TryGetPropertyValue("icon", out var iconNode) || !StorageJson.TryReadId(iconNode, out var icon) || icon == 0)
            {
                warn("icon is not an icon id");
                return;
            }

            if (!PortraitSources.TryParse(StorageJson.ReadString(obj, "source"), out var source))
            {
                warn("source is not a portrait family");
                return;
            }

            if (!HasNote(obj, warn, out var note))
            {
                return;
            }

            pins[giverId] = new CuratedPortraitPin(icon, source, note);
        });

        var deliveryKeys = new Dictionary<uint, CuratedDeliveryKey>();
        ForEach(root, "deliveryKeys", fileName, warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint icon) || PortraitSources.FamilyOfIcon(icon) != PortraitSource.Delivery)
            {
                warn("key is not a delivery portrait's icon id");
                return;
            }

            var (width, height) = PortraitSources.TextureSize(PortraitSource.Delivery);
            if (node is not JsonObject obj || !obj.TryGetPropertyValue("seed", out var seedNode) || !TryReadNumbers(seedNode, 2, out var seed)
                || seed[0] < 0 || seed[1] < 0 || seed[0] >= width || seed[1] >= height || seed[0] != MathF.Floor(seed[0]) || seed[1] != MathF.Floor(seed[1]))
            {
                warn($"seed must be [x, y]: whole hr px inside the {width} × {height} texture");
                return;
            }

            if (!HasNote(obj, warn, out var note))
            {
                return;
            }

            deliveryKeys[icon] = new CuratedDeliveryKey((int)seed[0], (int)seed[1], note);
        });

        return new PortraitCuration
        {
            Crops = crops,
            IconCrops = iconCrops,
            Faces = faces,
            Aliases = aliases,
            Blocks = blocks,
            Pins = pins,
            DeliveryKeys = deliveryKeys,
            Masks = masks,
        };
    }

    /// <summary>
    /// <c>portrait_masks/masks.json</c>: <c>{ "schema": 1, "entries": { "61661": { "file": "061661.png", "texture":
    /// "ui/icon/061000/061661_hr1.tex", "sha256": "…", "width": 400, "height": 480 } } }</c>, written by
    /// <c>Tsukimichi.DataGen --portrait-masks</c>. An entry whose file is missing or malformed is skipped with a warning.
    /// </summary>
    public static IReadOnlyDictionary<uint, PortraitMask> LoadMasks(string folder, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        var masks = new Dictionary<uint, PortraitMask>();
        var path = Path.Combine(folder, MaskManifestFileName);
        var text = AtomicFile.Read(path, out var ioError);
        if (text is null)
        {
            if (ioError is not null)
            {
                warnings.Add($"{MaskFolder}/{MaskManifestFileName} could not be read: {ioError}");
            }

            return masks;
        }

        JsonObject root;
        try
        {
            if (JsonNode.Parse(text, documentOptions: CuratedData.StrictOptions) is not JsonObject obj)
            {
                warnings.Add($"{MaskFolder}/{MaskManifestFileName}: root is not a JSON object; file ignored.");
                return masks;
            }

            root = obj;
        }
        catch (JsonException ex)
        {
            warnings.Add($"{MaskFolder}/{MaskManifestFileName} could not be parsed: {ex.Message}");
            return masks;
        }

        ForEach(root, "entries", $"{MaskFolder}/{MaskManifestFileName}", warnings, (key, node, warn) =>
        {
            if (!StorageJson.TryParseKey(key, out uint icon) || icon == 0 || node is not JsonObject obj)
            {
                warn("key is not an icon id, or the value is not an object");
                return;
            }

            var file = StorageJson.ReadString(obj, "file")?.Trim();
            var texture = StorageJson.ReadString(obj, "texture")?.Trim();
            var hash = StorageJson.ReadString(obj, "sha256")?.Trim();
            if (string.IsNullOrEmpty(file) || file.Contains('/') || file.Contains('\\') || string.IsNullOrEmpty(texture)
                || hash is not { Length: 64 } || !hash.All(char.IsAsciiHexDigitLower)
                || !obj.TryGetPropertyValue("width", out var w) || !StorageJson.TryReadId(w, out var width) || width == 0
                || !obj.TryGetPropertyValue("height", out var h) || !StorageJson.TryReadId(h, out var height) || height == 0)
            {
                warn("needs file (a name in the folder), texture, sha256 (64 lower-case hex digits), width and height");
                return;
            }

            var full = Path.Combine(folder, file);
            if (!File.Exists(full))
            {
                warn($"{file} is missing");
                return;
            }

            masks[icon] = new PortraitMask(icon, full, texture, hash, (int)width, (int)height);
        });

        return masks;
    }

    /// <summary>The newest expansion an era may name (Dawntrail is 5; Evercold, when it ships, is 6).</summary>
    public const byte MaxEra = 15;

    private delegate void EntryHandler(string key, JsonNode? node, Action<string> warn);

    private static void ForEach(JsonObject root, string section, string fileName, List<string> warnings, EntryHandler handle)
    {
        if (!root.TryGetPropertyValue(section, out var node) || node is null)
        {
            return;
        }

        if (node is not JsonObject entries)
        {
            warnings.Add($"{fileName}: \"{section}\" is not an object; section ignored.");
            return;
        }

        foreach (var pair in entries)
        {
            if (pair.Key.StartsWith('$'))
            {
                continue;
            }

            handle(pair.Key, pair.Value, reason => warnings.Add($"{fileName}: {section} entry \"{pair.Key}\" skipped: {reason}"));
        }
    }

    private static bool HasNote(JsonObject obj, Action<string> warn) => HasNote(obj, warn, out _);

    private static bool HasNote(JsonObject obj, Action<string> warn, out string note)
    {
        note = StorageJson.ReadString(obj, "note")?.Trim() ?? string.Empty;
        if (note.Length == 0)
        {
            warn("note is missing");
            return false;
        }

        return true;
    }

    private const string CropFormats =
        "needs \"box\": [x, y, side] in hr px inside the texture, \"eyes\": [u, v] with \"chin\": v below them, or \"crop\": [u0, v0, u1, v1] within 0–1";

    /// <summary>A crop in any of the three forms (<c>box</c>, <c>eyes</c> + <c>chin</c>, <c>crop</c>), for an icon of <paramref name="source"/>.</summary>
    private static bool TryReadAnyCrop(JsonObject obj, PortraitSource source, out PortraitCrop crop)
    {
        crop = default;
        if (obj.TryGetPropertyValue("box", out var boxNode))
        {
            if (source == PortraitSource.None || !TryReadNumbers(boxNode, 3, out var box))
            {
                return false;
            }

            crop = PortraitCrop.FromBox(source, box[0], box[1], box[2]);
            return crop.IsValid;
        }

        if (obj.TryGetPropertyValue("crop", out var cropNode))
        {
            return TryReadCrop(cropNode, out crop);
        }

        return TryReadLandmarks(obj, source, out crop);
    }

    private static bool TryReadCrop(JsonNode? node, out PortraitCrop crop)
    {
        crop = default;
        if (!TryReadNumbers(node, 4, out var values))
        {
            return false;
        }

        crop = new PortraitCrop(values[0], values[1], values[2], values[3]);
        return crop.IsValid;
    }

    /// <summary>
    /// A face measured on its texture: <c>"eyes": [u, v]</c> (the midpoint between the eyes) and <c>"chin": v</c>, in
    /// texture coordinates; the crop follows from the framing rule
    /// (<see cref="PortraitFraming.CropFor(PortraitSource, PortraitLandmarks)"/>) on <paramref name="source"/>'s texture,
    /// so the face moves with the rule if the rule is retuned.
    /// </summary>
    private static bool TryReadLandmarks(JsonObject obj, PortraitSource source, out PortraitCrop crop)
    {
        crop = default;
        if (source == PortraitSource.None
            || !obj.TryGetPropertyValue("eyes", out var eyesNode) || !TryReadNumbers(eyesNode, 2, out var eyes)
            || !obj.TryGetPropertyValue("chin", out var chinNode) || chinNode is not JsonValue chinValue
            || !chinValue.TryGetValue<double>(out var chin) || !double.IsFinite(chin))
        {
            return false;
        }

        crop = PortraitFraming.CropFor(source, new PortraitLandmarks(eyes[0], eyes[1], (float)chin));
        return crop.IsValid && crop != PortraitCrop.Full;
    }

    private static bool TryReadNumbers(JsonNode? node, int count, out float[] values)
    {
        values = new float[count];
        if (node is not JsonArray array || array.Count != count)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (array[i] is not JsonValue value || !value.TryGetValue<double>(out var number) || !double.IsFinite(number))
            {
                return false;
            }

            values[i] = (float)number;
        }

        return true;
    }
}
