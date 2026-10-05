using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tsukimichi.DataGen;

/// <summary>
/// Per-NPC head boxes for the portrait pack (the portrait audit's change C9): <c>tools/portrait-pack/portrait_pack_overrides.json</c>,
/// <c>{ "overrides": { "&lt;ENpc id&gt;": { "photoBox": [x, y, side], "note": "…" } } }</c>, the box a square in the
/// giver's Garland photo, in its own pixels. When a giver has one, <c>--portrait-pack</c> resamples that square instead of
/// <see cref="PortraitHeadCrop"/>'s. Givers who share a photo share one image, so their boxes must agree; a box must lie
/// inside the photo and be at least <see cref="PortraitPackBuilder.MinBox"/> px. A build input only: the plugin never
/// reads it. <c>tools/portrait-pack/make_overrides.py</c> writes it from the audit and the owner's answers.
/// </summary>
internal sealed class PortraitPackOverrides
{
    public const string DefaultPath = "tools/portrait-pack/portrait_pack_overrides.json";

    public static readonly PortraitPackOverrides None = new(new SortedDictionary<uint, Entry>());

    private PortraitPackOverrides(SortedDictionary<uint, Entry> entries) => Entries = entries;

    /// <summary>One giver's box and why it is set.</summary>
    public readonly record struct Entry(PhotoBox Box, string Note);

    public IReadOnlyDictionary<uint, Entry> Entries { get; }

    /// <summary>Reads the file; <paramref name="errors"/> lists every entry it could not take (nothing is guessed).</summary>
    public static PortraitPackOverrides Parse(string json, out List<string> errors)
    {
        errors = [];
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            errors.Add($"not JSON: {ex.Message}");
            return None;
        }

        if (root?["overrides"] is not JsonObject overrides)
        {
            errors.Add("no \"overrides\" object");
            return None;
        }

        var entries = new SortedDictionary<uint, Entry>();
        foreach (var (key, value) in overrides)
        {
            if (!uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id == 0)
            {
                errors.Add($"'{key}': not an ENpc id");
                continue;
            }

            if (value?["photoBox"] is not JsonArray { Count: 3 } box || !TryNumber(box[0], out var x) || !TryNumber(box[1], out var y) || !TryNumber(box[2], out var side) || side <= 0)
            {
                errors.Add($"{id}: \"photoBox\" must be [x, y, side] in photo pixels, side above 0");
                continue;
            }

            var note = value["note"] is JsonValue text && text.TryGetValue<string>(out var s) ? s : string.Empty;
            entries[id] = new Entry(new PhotoBox(x, y, side), note);
        }

        return new PortraitPackOverrides(entries);
    }

    /// <summary>Reads <paramref name="path"/>; a missing file is an error (a build must not silently lose the boxes).</summary>
    public static PortraitPackOverrides Load(string path, out List<string> errors)
    {
        if (!File.Exists(path))
        {
            errors = [$"{path}: no such file (give --overrides <file>)"];
            return None;
        }

        return Parse(File.ReadAllText(path), out errors);
    }

    /// <summary>
    /// The box for a photo the givers <paramref name="npcIds"/> share: the one their overrides agree on, or null when
    /// none of them has one. <paramref name="problem"/> names givers whose boxes differ (then the result is null).
    /// </summary>
    public PhotoBox? For(IEnumerable<uint> npcIds, out string? problem)
    {
        problem = null;
        PhotoBox? box = null;
        uint first = 0;
        foreach (var id in npcIds)
        {
            if (!Entries.TryGetValue(id, out var entry))
            {
                continue;
            }

            if (box is null)
            {
                box = entry.Box;
                first = id;
            }
            else if (box.Value != entry.Box)
            {
                problem = $"{first} and {id} share a photo but their boxes differ";
                return null;
            }
        }

        return box;
    }

    /// <summary>Why <paramref name="box"/> cannot frame a <paramref name="width"/> × <paramref name="height"/> photo, or null.</summary>
    public static string? Check(PhotoBox box, int width, int height, int minBox)
    {
        if (!box.Inside(width, height))
        {
            return string.Create(CultureInfo.InvariantCulture, $"box [{box.Left}, {box.Top}, {box.Side}] runs outside the {width} × {height} photo");
        }

        return box.Side < minBox ? string.Create(CultureInfo.InvariantCulture, $"box side {box.Side} is under {minBox} px") : null;
    }

    /// <summary>The ids that are none of <paramref name="givers"/>: an NPC the install does not name as a quest giver.</summary>
    public IReadOnlyList<uint> Unknown(IReadOnlySet<uint> givers) => [.. Entries.Keys.Where(id => !givers.Contains(id))];

    private static bool TryNumber(JsonNode? node, out double value)
    {
        value = 0;
        return node is JsonValue v && v.TryGetValue(out value) && double.IsFinite(value);
    }
}
