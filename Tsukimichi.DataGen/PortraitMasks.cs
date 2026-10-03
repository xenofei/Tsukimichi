using System.Text;
using System.Text.Json;
using Lumina.Data.Files;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;

namespace Tsukimichi.DataGen;

/// <summary>
/// <c>--portrait-masks</c>: keys every custom delivery portrait the curated file seeds (<c>deliveryKeys</c> in
/// <c>giver_portraits.json</c>) with <see cref="DeliveryKey"/>, and writes the keep masks the plugin ships into
/// <c>curated/portrait_masks/</c>: one 1-bit PNG per icon at the hr size, and <c>masks.json</c> naming each mask's texture
/// and the SHA-256 of that texture's file, so a patch that changes the art invalidates its mask (the portrait then
/// falls back until this runs again). Deterministic: the same art writes the same bytes. Fails when a key would touch
/// the figure, which the algorithm promises never happens.
/// </summary>
internal static class PortraitMasks
{
    public static int Write(Lumina.GameData data, string curatedDir)
    {
        var warnings = new List<string>();
        var curation = PortraitCuration.Load(Path.Combine(curatedDir, CuratedData.GiverPortraitsFileName), warnings);
        foreach (var warning in warnings)
        {
            Console.WriteLine($"  curated: {warning}");
        }

        var folder = Path.Combine(curatedDir, PortraitCuration.MaskFolder);
        Directory.CreateDirectory(folder);
        var entries = new SortedDictionary<uint, (string File, string Texture, string Hash, int Width, int Height, int Removed)>();
        var failed = false;
        foreach (var (icon, key) in curation.DeliveryKeys.OrderBy(kv => kv.Key))
        {
            var path = HrPath(icon);
            var raw = data.GetFile(path);
            var texture = data.GetFile<TexFile>(path);
            if (raw is null || texture is null)
            {
                Console.WriteLine($"  {icon:D6}: no hr texture at {path}; no mask written");
                continue;
            }

            int width = texture.Header.Width, height = texture.Header.Height;
            var result = DeliveryKey.Run(texture.ImageData, width, height, key.SeedX, key.SeedY);
            var figure = result.Figure.Count(f => f);
            Console.WriteLine($"  {icon:D6}: figure {figure}, removed {result.Removed}, removed inside figure {result.RemovedInsideFigure}"
                              + (result.KeptIslands.Count > 0 ? $", kept islands (area, script share): {string.Join(" ", result.KeptIslands.Take(8))}" : string.Empty));
            if (result.RemovedInsideFigure != 0 || figure < 100)
            {
                Console.Error.WriteLine($"  {icon:D6}: the key touched the figure, or the seed found no face; check its seed in giver_portraits.json");
                failed = true;
                continue;
            }

            var file = $"{icon:D6}.png";
            PortraitMaskFile.Write(Path.Combine(folder, file), result.Keep, width, height);
            entries[icon] = (file, path, PortraitMask.HashOf(raw.Data), width, height, result.Removed);
        }

        // Masks no longer seeded go, so the folder holds exactly what the manifest lists.
        foreach (var stale in Directory.GetFiles(folder, "*.png"))
        {
            if (!entries.Values.Any(e => string.Equals(e.File, Path.GetFileName(stale), StringComparison.Ordinal)))
            {
                File.Delete(stale);
            }
        }

        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            json.WriteNumber("schema", 1);
            json.WriteString("note", "Written by `Tsukimichi.DataGen --portrait-masks` (tools/regen.ps1); do not edit by hand. One 1-bit keep mask per custom delivery portrait at the hr texture's size (white = keep): the client emblem's lettered ring keyed out, figure first, from the face seed in giver_portraits.json (design spec 1.15 A2.4). sha256 is the hash of the texture file the mask was keyed from; when the game's file no longer matches, the plugin shows the fallback for that portrait until this is rerun.");
            json.WriteStartObject("entries");
            foreach (var (icon, entry) in entries)
            {
                json.WriteStartObject(icon.ToString(System.Globalization.CultureInfo.InvariantCulture));
                json.WriteString("file", entry.File);
                json.WriteString("texture", entry.Texture);
                json.WriteString("sha256", entry.Hash);
                json.WriteNumber("width", entry.Width);
                json.WriteNumber("height", entry.Height);
                json.WriteNumber("removed", entry.Removed);
                json.WriteEndObject();
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        var text = Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        File.WriteAllText(Path.Combine(folder, PortraitCuration.MaskManifestFileName), text, new UTF8Encoding(false));
        Console.WriteLine($"wrote:   {entries.Count} masks and {Path.Combine(folder, PortraitCuration.MaskManifestFileName)}");
        return failed ? 1 : 0;
    }

    /// <summary>The game path of an icon's hr texture.</summary>
    public static string HrPath(uint icon) => RewardArtIndex.IconPath(icon).Replace(".tex", "_hr1.tex", StringComparison.Ordinal);
}
