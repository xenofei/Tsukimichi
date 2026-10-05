using System.Buffers.Binary;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>What one level's board holds on the GPU while it is drawn (RGBA, 4 bytes a pixel).</summary>
/// <param name="Sheet1x">The atlas's 1x sheet, always held while the art draws.</param>
/// <param name="Sheet2x">The 2x sheet, held when the board is drawn above 1 px per unit (a large window or the Full Moon zoom); else 0.</param>
/// <param name="Ground">The level's scene at the tier in use, or the sky for a level without one.</param>
public readonly record struct MoonfallArtBudget(long Sheet1x, long Sheet2x, long Ground)
{
    public long Total => Sheet1x + Sheet2x + Ground;
}

/// <summary>
/// Moonfall's art files on disk (feature plan v9 G8): <c>assets/moonfall/</c> beside the plugin, with the atlas
/// (<see cref="MoonfallAtlas"/>) and the levels' background scenes in <c>scenes/</c>. <see cref="Load"/> reads the
/// manifest and checks each picture's header (its size) without decoding it, so it is cheap and runs off the framework
/// thread; decoding is Dalamud's, also off the thread. Never throws on bad files.
/// </summary>
public static class MoonfallArtFiles
{
    public const string ManifestName = "atlas.json";
    public const string ScenesFolder = "scenes";

    /// <summary>A scene's 1x size: the whole board (its 2x picture is 1600 × 1200).</summary>
    public const int SceneWidth = 800;

    /// <inheritdoc cref="SceneWidth"/>
    public const int SceneHeight = 600;

    /// <summary>The largest manifest read.</summary>
    public const int MaxManifestBytes = 1 << 20;

    private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>The art folder for the plugin folder <paramref name="pluginDirectory"/>.</summary>
    public static string Folder(string pluginDirectory) => Path.Combine(pluginDirectory, "assets", "moonfall");

    /// <summary>
    /// Reads <c>atlas.json</c> in <paramref name="folder"/> and checks that its three pictures are PNGs of the sizes it
    /// says (the 2x sheet twice the 1x). Any failure refuses the whole set, and the board keeps its primitives.
    /// </summary>
    public static MoonfallAtlasLoad Load(string folder)
    {
        string json;
        try
        {
            var path = Path.Combine(folder, ManifestName);
            if (!File.Exists(path))
            {
                return new MoonfallAtlasLoad(null, [$"{ManifestName} is missing"]);
            }

            if (new FileInfo(path).Length > MaxManifestBytes)
            {
                return new MoonfallAtlasLoad(null, [$"{ManifestName} is larger than {MaxManifestBytes} bytes"]);
            }

            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MoonfallAtlasLoad(null, [$"{ManifestName} could not be read: {ex.Message}"]);
        }

        var load = MoonfallAtlas.Parse(json);
        if (load.Atlas is not { } atlas)
        {
            return load;
        }

        var errors = new List<string>();
        Expect(folder, atlas.OneXFile, atlas.Width, atlas.Height, errors);
        Expect(folder, atlas.TwoXFile, atlas.Width * 2, atlas.Height * 2, errors);
        Expect(folder, atlas.SkyFile, (int)atlas.Sky.W, (int)atlas.Sky.H, errors);
        return errors.Count == 0 ? load : new MoonfallAtlasLoad(null, errors);
    }

    private static void Expect(string folder, string file, int width, int height, List<string> errors)
    {
        if (!TryPngSize(Path.Combine(folder, file), out var w, out var h))
        {
            errors.Add($"{file} is missing or not a PNG");
        }
        else if (w != width || h != height)
        {
            errors.Add($"{file} is {w} x {h}; the manifest needs {width} x {height}");
        }
    }

    /// <summary>A scene's picture: <c>scenes/&lt;scene&gt;.png</c>, or <c>&lt;scene&gt;@2x.png</c> for the 2x tier.</summary>
    public static string ScenePath(string folder, string scene, bool twoX) => Path.Combine(folder, ScenesFolder, scene + (twoX ? "@2x.png" : ".png"));

    /// <summary>The size a scene's picture must have at a tier.</summary>
    public static (int Width, int Height) SceneSize(bool twoX) => twoX ? (SceneWidth * 2, SceneHeight * 2) : (SceneWidth, SceneHeight);

    /// <summary>
    /// Whether <paramref name="path"/> is a usable scene picture at the tier: a PNG of <see cref="SceneSize"/>. A level whose
    /// picture is not draws the sky instead.
    /// </summary>
    public static bool SceneUsable(string path, bool twoX, out string? error)
    {
        var (width, height) = SceneSize(twoX);
        if (!TryPngSize(path, out var w, out var h))
        {
            error = $"{Path.GetFileName(path)} is missing or not a PNG";
            return false;
        }

        error = w == width && h == height ? null : $"{Path.GetFileName(path)} is {w} x {h}; a scene is {width} x {height}";
        return error is null;
    }

    /// <summary>A PNG's size from its header (the signature and IHDR), reading 24 bytes; false for anything else.</summary>
    public static bool TryPngSize(string path, out int width, out int height)
    {
        width = height = 0;
        Span<byte> head = stackalloc byte[24];
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false) < head.Length)
            {
                return false;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }

        if (!head[..8].SequenceEqual(PngSignature) || !head.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return false;
        }

        var w = BinaryPrimitives.ReadInt32BigEndian(head.Slice(16, 4));
        var h = BinaryPrimitives.ReadInt32BigEndian(head.Slice(20, 4));
        if (w <= 0 || h <= 0)
        {
            return false;
        }

        width = w;
        height = h;
        return true;
    }

    /// <summary>The bytes one picture of the art holds on the GPU (RGBA).</summary>
    public static long SlotBytes(MoonfallAtlas atlas, MoonfallArtSlot slot)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        return slot switch
        {
            MoonfallArtSlot.Sheet1x => atlas.SheetBytes(false),
            MoonfallArtSlot.Sheet2x => atlas.SheetBytes(true),
            MoonfallArtSlot.Sky => atlas.SkyBytes,
            MoonfallArtSlot.Scene1x => (long)SceneWidth * SceneHeight * 4,
            _ => (long)SceneWidth * SceneHeight * 16,
        };
    }

    /// <summary>
    /// What a level holds on the GPU (see <see cref="MoonfallArtBudget"/>): the 1x sheet always, the 2x sheet at
    /// <paramref name="twoX"/>, and its scene at that tier when <paramref name="hasScene"/>, else the sky. A level that
    /// changes tier (the window resized across 800 × 600, or the Full Moon zoom) keeps the 2x sheet once loaded, and lets
    /// the other tier's scene go as soon as the wanted one lands (<see cref="MoonfallArtSlots{T}.SceneToDrop"/>), so this
    /// 2x figure is a level's worst case once settled. While the new tier decodes, the old one still draws.
    /// </summary>
    public static MoonfallArtBudget Budget(MoonfallAtlas atlas, bool twoX, bool hasScene)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        var (w, h) = SceneSize(twoX);
        return new MoonfallArtBudget(atlas.SheetBytes(false), twoX ? atlas.SheetBytes(true) : 0, hasScene ? (long)w * h * 4 : atlas.SkyBytes);
    }
}
