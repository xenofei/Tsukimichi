using System.IO.Compression;
using Lumina;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;

const string Game = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";
using var data = new GameData(Game, new LuminaOptions { PanicOnSheetChecksumMismatch = false });
var cmd = args[0];
if (cmd == "loading")
{
    foreach (var t in data.GetExcelSheet<TerritoryType>()!)
    {
        if (t.LoadingImage.RowId == 0 || t.LoadingImage.ValueNullable is not { } img) continue;
        var f = img.FileName.ExtractText();
        if (f.Length == 0) continue;
        var name = t.PlaceName.ValueNullable?.Name.ExtractText() ?? "";
        Console.WriteLine($"{f}\t{t.RowId}\t{name}");
    }
}
else if (cmd == "maps")
{
    foreach (var m in data.GetExcelSheet<Map>()!)
    {
        var id = m.Id.ExtractText();
        if (id.Length == 0) continue;
        var name = m.PlaceName.ValueNullable?.Name.ExtractText() ?? "";
        var region = m.PlaceNameRegion.ValueNullable?.Name.ExtractText() ?? "";
        Console.WriteLine($"{m.RowId}\t{id}\t{region}\t{name}");
    }
}
else if (cmd == "dump")
{
    // dump <outdir> <path> [<path> ...]
    var outDir = args[1];
    Directory.CreateDirectory(outDir);
    foreach (var p in args.Skip(2))
    {
        var tex = data.GetFile<TexFile>(p);
        if (tex is null) { Console.WriteLine($"MISSING {p}"); continue; }
        var w = tex.Header.Width; var h = tex.Header.Height;
        var bgra = tex.ImageData;
        var file = Path.Combine(outDir, p.Replace('/', '_').Replace(".tex", ".png"));
        WritePng(file, w, h, bgra);
        Console.WriteLine($"OK {p} {w}x{h} -> {file}");
    }
}
else if (cmd == "dumplist")
{
    // dumplist <outdir> <listfile>
    var outDir = args[1];
    Directory.CreateDirectory(outDir);
    foreach (var p in File.ReadAllLines(args[2]).Where(l => l.Trim().Length > 0))
    {
        var tex = data.GetFile<TexFile>(p.Trim());
        if (tex is null) { Console.WriteLine($"MISSING {p}"); continue; }
        var file = Path.Combine(outDir, p.Trim().Replace('/', '_').Replace(".tex", ".png"));
        WritePng(file, tex.Header.Width, tex.Header.Height, tex.ImageData);
        Console.WriteLine($"OK {p} {tex.Header.Width}x{tex.Header.Height}");
    }
}
else if (cmd == "bannerbg")
{
    foreach (var b in data.GetExcelSheet<BannerBg>()!)
    {
        Console.WriteLine($"{b.RowId}\t{b.Image}\t{b.Name.ExtractText()}");
    }
}
else if (cmd == "icon")
{
    // icon <outdir> <id>...: dumps ui/icon/NNN000/NNNNNN_hr1.tex (falls back to the 1x texture)
    var outDir = args[1];
    Directory.CreateDirectory(outDir);
    foreach (var s in args.Skip(2))
    {
        var id = uint.Parse(s);
        var folder = (id / 1000) * 1000;
        foreach (var p in new[] { $"ui/icon/{folder:D6}/{id:D6}_hr1.tex", $"ui/icon/{folder:D6}/{id:D6}.tex" })
        {
            var tex = data.GetFile<TexFile>(p);
            if (tex is null) continue;
            var file = Path.Combine(outDir, $"icon_{id:D6}.png");
            WritePng(file, tex.Header.Width, tex.Header.Height, tex.ImageData);
            Console.WriteLine($"OK {p} {tex.Header.Width}x{tex.Header.Height}");
            break;
        }
    }
}
else if (cmd == "iconrange")
{
    // iconrange <outdir> <from> <to>: every existing icon in the range (hr1 when present)
    var outDir = args[1];
    Directory.CreateDirectory(outDir);
    var n = 0;
    for (var id = uint.Parse(args[2]); id <= uint.Parse(args[3]); id++)
    {
        var folder = (id / 1000) * 1000;
        foreach (var p in new[] { $"ui/icon/{folder:D6}/{id:D6}_hr1.tex", $"ui/icon/{folder:D6}/{id:D6}.tex" })
        {
            var tex = data.GetFile<TexFile>(p);
            if (tex is null) continue;
            WritePng(Path.Combine(outDir, $"icon_{id:D6}.png"), tex.Header.Width, tex.Header.Height, tex.ImageData);
            n++;
            break;
        }
    }

    Console.WriteLine($"{n} icons");
}
else if (cmd == "exists")
{
    foreach (var p in args.Skip(1)) Console.WriteLine($"{(data.FileExists(p) ? "Y" : "N")} {p}");
}

static void WritePng(string file, int w, int h, byte[] bgra)
{
    using var fs = File.Create(file);
    fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    void Chunk(string type, byte[] body)
    {
        var len = BitConverter.GetBytes(body.Length); Array.Reverse(len); fs.Write(len);
        var t = System.Text.Encoding.ASCII.GetBytes(type); fs.Write(t); fs.Write(body);
        var crc = Crc(t.Concat(body).ToArray()); var c = BitConverter.GetBytes(crc); Array.Reverse(c); fs.Write(c);
    }
    var ihdr = new byte[13];
    void Be(int off, int v) { ihdr[off] = (byte)(v >> 24); ihdr[off + 1] = (byte)(v >> 16); ihdr[off + 2] = (byte)(v >> 8); ihdr[off + 3] = (byte)v; }
    Be(0, w); Be(4, h); ihdr[8] = 8; ihdr[9] = 6;
    Chunk("IHDR", ihdr);
    using var ms = new MemoryStream();
    using (var z = new ZLibStream(ms, CompressionLevel.Fastest, true))
    {
        var row = new byte[w * 4 + 1];
        for (var y = 0; y < h; y++)
        {
            row[0] = 0;
            for (var x = 0; x < w; x++)
            {
                var i = (y * w + x) * 4;
                row[1 + x * 4] = bgra[i + 2]; row[2 + x * 4] = bgra[i + 1]; row[3 + x * 4] = bgra[i]; row[4 + x * 4] = bgra[i + 3];
            }
            z.Write(row);
        }
    }
    Chunk("IDAT", ms.ToArray());
    Chunk("IEND", Array.Empty<byte>());
}

static uint Crc(byte[] b)
{
    uint c = 0xFFFFFFFF;
    foreach (var x in b) { c ^= x; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; }
    return c ^ 0xFFFFFFFF;
}
