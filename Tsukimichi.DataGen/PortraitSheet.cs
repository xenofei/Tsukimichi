using System.Globalization;
using System.Text;
using Lumina.Data.Files;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;

namespace Tsukimichi.DataGen;

/// <summary>
/// The giver portrait contact sheet (feature plan v7 F3; 1.15 design spec A2.3): every face the portrait index gives a
/// giver, cropped as the plugin crops it, so curation checks every crop at a glance.
/// <list type="bullet">
/// <item><c>&lt;Family&gt;-&lt;n&gt;.png</c>: each crop at 120 px on the night plate, ungraded, delivery portraits through
/// their keep mask, with the guide bands of <see cref="PortraitFraming"/> (crown 8–12 % blue, eye line 42–46 % gold with
/// a line at 44 %, chin 78–84 % red with a line at 81 %, two ticks at the foot 55 % of the diameter apart) and the
/// circle; labelled with the icon id, the giver, the box (hr px) and the quests it is picked for, and flagged CLAMPED
/// (the box meets the texture's edge) or KEY n (script-colour pixels left inside the circle).</item>
/// <item><c>Delivery-keyed.png</c>: every masked delivery portrait at 128 px three ways: source, keyed, keyed and graded
/// (the colour family's night grade, spec A3), for signing off a mask before it ships.</item>
/// <item><c>&lt;Family&gt;-&lt;n&gt;-source.png</c>: each whole texture with a grid every 0.1 in texture coordinates and the
/// crop's box, for measuring a per-icon box.</item>
/// <item><c>portraits.md</c>: every cell's icon, box, flags, quests and givers.</item>
/// </list>
/// The pages hold game art: they are for review, never committed or shipped.
/// </summary>
internal static class PortraitSheet
{
    private const int Crop = 120;
    private const int CellWidth = 136;
    private const int CellHeight = 166;
    private const int Review = 128;

    private static readonly (byte R, byte G, byte B) Label = (226, 222, 210);
    private static readonly (byte R, byte G, byte B) Dim = (150, 156, 176);
    private static readonly (byte R, byte G, byte B) Warn = (255, 120, 100);
    private static readonly (byte R, byte G, byte B) Brass = (230, 207, 152);

    /// <summary>The colour family's night grade (spec A3): rows R, G, B as r, g, b and an offset, in sRGB 0–1.</summary>
    private static readonly double[,] ColourGrade =
    {
        { .6162, .1372, .0138, .0147 },
        { .0413, .7224, .0140, .0196 },
        { .0435, .1462, .6279, .0353 },
    };

    /// <param name="only">Icons to draw alone and larger (four to a row), worn or not, for measuring a face; null draws
    /// every face a giver wears.</param>
    public static int Write(Lumina.GameData data, string outDir, string curatedDir, IReadOnlyCollection<uint>? only = null)
    {
        Directory.CreateDirectory(outDir);
        var columns = only is null ? 8 : 4;
        var perPage = only is null ? 48 : 8;
        var sourceCell = only is null ? 288 : 480;
        var prefix = only is null ? string.Empty : "focus-";
        var curated = CuratedData.Load(curatedDir);
        foreach (var warning in curated.Warnings)
        {
            Console.WriteLine($"  curated: {warning}");
        }

        var curation = curated.GiverPortraits;
        var inputs = GiverPortraitSources.Read(data.Excel, icon => data.FileExists(RewardArtIndex.IconPath(icon)), line => Console.WriteLine($"  {line}"));
        var index = PortraitIndex.Build(inputs, curation);

        // Every face a giver wears, with the givers wearing it.
        var cells = new SortedDictionary<(int Rank, uint Icon), (PortraitSource Source, SortedSet<string> Givers)>();
        foreach (var giver in inputs.Givers)
        {
            foreach (var variant in index.Variants(giver.NpcId))
            {
                var key = (PortraitSources.Rank(variant.Source), variant.Icon);
                if (!cells.TryGetValue(key, out var cell))
                {
                    cell = (variant.Source, new SortedSet<string>(StringComparer.Ordinal));
                    cells[key] = cell;
                }

                cell.Givers.Add(giver.Name);
            }
        }

        if (only is not null)
        {
            var kept = new SortedDictionary<(int Rank, uint Icon), (PortraitSource Source, SortedSet<string> Givers)>();
            foreach (var icon in only)
            {
                var source = PortraitSources.FamilyOfIcon(icon);
                var key = (PortraitSources.Rank(source), icon);
                kept[key] = cells.TryGetValue(key, out var worn) ? worn : (source, new SortedSet<string>(StringComparer.Ordinal));
            }

            cells = kept;
        }

        // How many quests each face is picked for: the faces worth curating first.
        var picked = new Dictionary<uint, int>();
        foreach (var quest in inputs.Quests)
        {
            if (index.For(quest.GiverId, quest.Expansion, quest.BeastTribe) is { HasArt: true } portrait)
            {
                picked[portrait.Icon] = picked.GetValueOrDefault(portrait.Icon) + 1;
            }
        }

        var masks = new Dictionary<uint, bool[]>();
        foreach (var (icon, mask) in curation.Masks)
        {
            if (PortraitMaskFile.TryRead(mask.File, out _, out _, out var keep))
            {
                masks[icon] = keep;
            }
        }

        var md = new StringBuilder();
        md.AppendLine("# Giver portrait contact sheet");
        md.AppendLine();
        md.AppendLine(CultureInfo.InvariantCulture, $"Written by `Tsukimichi.DataGen --portrait-sheet`. {cells.Count} faces, worn by {index.GiversWithArt} of {index.GiverCount} giver ids; {picked.Values.Sum()} quests show a portrait. Boxes are (x, y, side) in hr px. Flags: CLAMPED, the box meets the texture's edge; KEY n, n script-colour pixels left inside the circle of a keyed delivery portrait.");
        var flagged = 0;

        foreach (var family in cells.GroupBy(c => c.Value.Source))
        {
            var list = family.ToList();
            for (var page = 0; page * perPage < list.Count; page++)
            {
                var slice = list.Skip(page * perPage).Take(perPage).ToList();
                var file = $"{prefix}{family.Key}-{page + 1}.png";
                var sourceFile = $"{prefix}{family.Key}-{page + 1}-source.png";
                var rows = (slice.Count + columns - 1) / columns;
                var canvas = new SheetCanvas(columns * CellWidth, rows * CellHeight);
                var sources = new SheetCanvas(columns * sourceCell, rows * sourceCell);
                md.AppendLine();
                md.AppendLine(CultureInfo.InvariantCulture, $"## {file}");
                md.AppendLine();
                md.AppendLine("| Cell | Icon | Box | Flags | Quests | Givers |");
                md.AppendLine("|---:|---:|---|---|---:|---|");
                for (var n = 0; n < slice.Count; n++)
                {
                    var ((_, icon), (source, givers)) = (slice[n].Key, slice[n].Value);
                    var crop = index.Crops.For(source, icon);
                    var texture = Load(data, icon);
                    var mask = masks.GetValueOrDefault(icon);
                    int left = (n % columns) * CellWidth, top = (n / columns) * CellHeight;
                    var keyLeft = DrawCrop(canvas, texture, crop, mask, left + 8, top + 6, Crop, grade: false, guides: true);
                    var (x, y, side) = crop.ToBox(source);
                    var flags = new List<string>();
                    if (mask is not null)
                    {
                        flags.Add("KEYED");
                    }

                    if (crop.U0 <= 0.002f || crop.V0 <= 0.002f || crop.U1 >= 0.998f || crop.V1 >= 0.998f)
                    {
                        flags.Add("CLAMPED");
                    }

                    if (keyLeft > 0)
                    {
                        flags.Add($"KEY {keyLeft}");
                    }

                    flagged += flags.Any(f => f != "KEYED") ? 1 : 0;
                    var quests = picked.GetValueOrDefault(icon);
                    var name = givers.Count == 0 ? "(no giver)" : givers.Count == 1 ? givers.First() : $"{givers.First()} +{givers.Count - 1}";
                    canvas.Text(left + 8, top + Crop + 10, $"{icon:D6} Q{quests}", Label);
                    canvas.Text(left + 8, top + Crop + 19, name, Label, Crop);
                    canvas.Text(left + 8, top + Crop + 28, string.Create(CultureInfo.InvariantCulture, $"{x:0},{y:0},{side:0}"), Dim, Crop);
                    var flagText = string.Join(' ', flags.Where(f => f != "KEYED"));
                    canvas.Text(left + 8, top + Crop + 37, flags.Contains("KEYED") ? ("SCRIPT KEYED " + flagText).Trim() : flagText, flagText.Length > 0 ? Warn : Dim, Crop);
                    DrawSource(sources, texture, crop, (n % columns) * sourceCell, (n / columns) * sourceCell, sourceCell);
                    md.AppendLine(CultureInfo.InvariantCulture, $"| {n + 1} | {icon:D6} | {x:0}, {y:0}, {side:0} | {string.Join(", ", flags)} | {quests} | {string.Join(", ", givers)} |");
                }

                canvas.Save(Path.Combine(outDir, file));
                sources.Save(Path.Combine(outDir, sourceFile));
                Console.WriteLine($"wrote:   {Path.Combine(outDir, file)} and its source page ({slice.Count} faces)");
            }
        }

        // The keyed delivery portraits three ways, for signing a mask off.
        var keyed = curation.Masks.Keys.Where(masks.ContainsKey).Where(i => only is null || only.Contains(i)).Order().ToList();
        if (keyed.Count > 0)
        {
            var review = new SheetCanvas((3 * (Review + 8)) + 8, keyed.Count * (Review + 24));
            md.AppendLine();
            md.AppendLine(CultureInfo.InvariantCulture, $"## {prefix}Delivery-keyed.png");
            md.AppendLine();
            md.AppendLine("Each row: source, keyed, keyed and graded, at 128 px.");
            for (var row = 0; row < keyed.Count; row++)
            {
                var icon = keyed[row];
                var texture = Load(data, icon);
                var crop = index.Crops.For(PortraitSource.Delivery, icon);
                var top = (row * (Review + 24)) + 4;
                DrawCrop(review, texture, crop, null, 8, top, Review, grade: false, guides: false);
                DrawCrop(review, texture, crop, masks[icon], 8 + Review + 8, top, Review, grade: false, guides: false);
                DrawCrop(review, texture, crop, masks[icon], 8 + (2 * (Review + 8)), top, Review, grade: true, guides: false);
                review.Text(8, top + Review + 6, $"{icon:D6} SOURCE / KEYED / KEYED + GRADED", Label);
            }

            review.Save(Path.Combine(outDir, $"{prefix}Delivery-keyed.png"));
            Console.WriteLine($"wrote:   {Path.Combine(outDir, $"{prefix}Delivery-keyed.png")} ({keyed.Count} portraits)");
        }

        var mdFile = Path.Combine(outDir, prefix + "portraits.md");
        File.WriteAllText(mdFile, md.ToString());
        Console.WriteLine($"wrote:   {mdFile} ({flagged} cells flagged)");
        return 0;
    }

    private static TexFile? Load(Lumina.GameData data, uint icon) =>
        data.GetFile<TexFile>(PortraitMasks.HrPath(icon)) ?? data.GetFile<TexFile>(RewardArtIndex.IconPath(icon));

    /// <summary>
    /// Draws <paramref name="crop"/> of the texture into a <paramref name="size"/> square on the night plate, through
    /// <paramref name="keep"/> when given (a hr-sized mask), graded when asked, with the guides when asked. Returns how
    /// many plate pixels inside the circle show script colour after keying (0 without a mask).
    /// </summary>
    private static int DrawCrop(SheetCanvas canvas, TexFile? texture, PortraitCrop crop, bool[]? keep, int left, int top, int size, bool grade, bool guides)
    {
        canvas.Fill(left, top, size, size, SheetCanvas.Night);
        var scriptLeft = 0;
        var radius = size / 2.0;
        if (texture is not null)
        {
            var source = texture.ImageData;
            int w = texture.Header.Width, h = texture.Header.Height;
            var maskMatches = keep is not null && keep.Length == w * h;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var sx = Math.Clamp((int)((crop.U0 + ((crop.U1 - crop.U0) * (x + 0.5) / size)) * w), 0, w - 1);
                    var sy = Math.Clamp((int)((crop.V0 + ((crop.V1 - crop.V0) * (y + 0.5) / size)) * h), 0, h - 1);
                    var p = (sy * w) + sx;
                    var si = p * 4;
                    double b = source[si], g = source[si + 1], r = source[si + 2], a = source[si + 3] / 255.0;
                    if (maskMatches && !keep![p])
                    {
                        a = 0;
                    }

                    var inside = Math.Pow(x + 0.5 - radius, 2) + Math.Pow(y + 0.5 - radius, 2) <= radius * radius;
                    if (maskMatches && inside && a > 40 / 255.0 && DeliveryKey.IsScript(source[si], source[si + 1], source[si + 2], source[si + 3]))
                    {
                        scriptLeft++;
                    }

                    if (grade)
                    {
                        (r, g, b) = Grade(r, g, b);
                    }

                    canvas.Blend(left + x, top + y, r, g, b, a);
                    if (!inside)
                    {
                        // Outside the circle the plugin draws nothing: dim it so the circle reads.
                        canvas.Blend(left + x, top + y, 22, 24, 34, 0.6);
                    }
                }
            }
        }

        if (guides)
        {
            Band(canvas, left, top, size, PortraitFraming.CrownMin, PortraitFraming.CrownMax, (90, 140, 255), 0.22, null);
            Band(canvas, left, top, size, PortraitFraming.EyeLineMin, PortraitFraming.EyeLineMax, (230, 190, 90), 0.30, PortraitFraming.EyeLine);
            Band(canvas, left, top, size, PortraitFraming.ChinMin, PortraitFraming.ChinMax, (230, 80, 80), 0.26, PortraitFraming.Chin);

            // The face's width: two ticks at the foot, 55 % of the diameter apart.
            foreach (var tick in new[] { 0.5 - (PortraitFraming.FaceShare / 2), 0.5 + (PortraitFraming.FaceShare / 2) })
            {
                var tx = left + (int)Math.Round(tick * size);
                for (var y = top + size - 6; y < top + size; y++)
                {
                    canvas.Blend(tx, y, 230, 190, 90, 0.9);
                }
            }
        }

        canvas.Circle(left + size / 2, top + size / 2, radius - 0.5, Brass, 0.85);
        return scriptLeft;
    }

    private static void Band(SheetCanvas canvas, int left, int top, int size, float from, float to, (byte R, byte G, byte B) colour, double alpha, float? line)
    {
        var y0 = top + (int)Math.Round(from * size);
        var y1 = top + (int)Math.Round(to * size);
        canvas.Fill(left, y0, size, Math.Max(1, y1 - y0), colour, alpha);
        if (line is { } at)
        {
            canvas.Fill(left, top + (int)Math.Round(at * size), size, 1, colour, 0.9);
        }
    }

    private static (double R, double G, double B) Grade(double r, double g, double b)
    {
        double rr = r / 255, gg = g / 255, bb = b / 255;
        double Row(int i) => Math.Clamp((ColourGrade[i, 0] * rr) + (ColourGrade[i, 1] * gg) + (ColourGrade[i, 2] * bb) + ColourGrade[i, 3], 0, 1) * 255;
        return (Row(0), Row(1), Row(2));
    }

    /// <summary>The whole texture fitted into the cell, a 0.1 grid in texture coordinates, and the crop's box.</summary>
    private static void DrawSource(SheetCanvas canvas, TexFile? texture, PortraitCrop crop, int left, int top, int size)
    {
        if (texture is null)
        {
            return;
        }

        const int Pad = 4;
        var source = texture.ImageData;
        int w = texture.Header.Width, h = texture.Header.Height;
        var scale = (size - (2 * Pad)) / (float)Math.Max(w, h);
        int dw = (int)(w * scale), dh = (int)(h * scale);
        int ox = left + Pad + ((size - (2 * Pad) - dw) / 2), oy = top + Pad + ((size - (2 * Pad) - dh) / 2);
        for (var y = 0; y < dh; y++)
        {
            for (var x = 0; x < dw; x++)
            {
                var si = ((Math.Min(h - 1, (int)(y / scale)) * w) + Math.Min(w - 1, (int)(x / scale))) * 4;
                canvas.Blend(ox + x, oy + y, source[si + 2], source[si + 1], source[si], source[si + 3] / 255.0);
            }
        }

        for (var tick = 0; tick <= 10; tick++)
        {
            var alpha = tick == 5 ? 0.86 : 0.35;
            canvas.Fill(ox + (int)(tick / 10f * (dw - 1)), oy, 1, dh, (120, 200, 255), alpha);
            canvas.Fill(ox, oy + (int)(tick / 10f * (dh - 1)), dw, 1, (120, 200, 255), alpha);
        }

        int x0 = ox + (int)(crop.U0 * dw), x1 = ox + (int)(crop.U1 * dw), y0 = oy + (int)(crop.V0 * dh), y1 = oy + (int)(crop.V1 * dh);
        canvas.Fill(x0, y0, x1 - x0 + 1, 1, (255, 220, 60));
        canvas.Fill(x0, y1, x1 - x0 + 1, 1, (255, 220, 60));
        canvas.Fill(x0, y0, 1, y1 - y0 + 1, (255, 220, 60));
        canvas.Fill(x1, y0, 1, y1 - y0 + 1, (255, 220, 60));
    }
}
