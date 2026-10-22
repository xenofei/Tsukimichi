using System.Security.Cryptography;
using System.Text;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Ui;

namespace Tsukimichi.MoonfallRender;

/// <summary>
/// The optional <c>--cache &lt;dir&gt;</c> of the renderer: a picture is keyed on every argument but the output path, the
/// hashes of the Tsukimichi.Core.dll and Tsukimichi.dll the renderer loaded, and the runtime's level and scene JSON and
/// shipped Moonfall art on disk; a matching key copies the stored PNG instead of rendering. A single level's picture
/// (<c>--scene-only</c>, or the play board of an explicit <c>--level</c>) is keyed on that level alone (see <see cref="Key"/>),
/// so editing another level keeps it cached. Not part of the key: the game
/// install's textures and the Windows fonts (a game patch needs a fresh <c>--cache</c> folder). Runs that print
/// diagnostics (<c>--alloc</c>, <c>--text-check</c>) are never cached.
/// </summary>
internal static class RenderCache
{
    public static bool Applies(string[] args) => !args.Contains("--alloc") && !args.Contains("--text-check");

    /// <summary>
    /// The key, or null when this run must not be cached. A level's own picture (<c>--scene-only</c>, or the play board of
    /// an explicit <c>--level</c>) is keyed per level: its arguments, the renderer's own build, the C# sources of the
    /// engine and the window (the built DLLs embed every level, so their hashes would change with any level), that level's
    /// JSON, its scene recipe and its pictures. The DLLs must be newer than all of those, or the key is null (a stale build
    /// would be cached under the new sources' key). Every other screen is keyed on every level and picture, as before.
    /// </summary>
    public static string? Key(string[] args, string repo)
    {
        var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string s) => h.AppendData(Encoding.UTF8.GetBytes(s + "\0"));
        void AddFile(string path) => h.AppendData(SHA256.HashData(File.ReadAllBytes(path)));

        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--cache")
            {
                i++;
                continue;
            }

            Add(args[i]);
        }

        var levelsDir = Path.Combine(repo, "Tsukimichi.Core", "Moonfall", "Levels");
        var assetsDir = Path.Combine(repo, "Tsukimichi", "assets", "moonfall");
        var perLevel = PerLevelFiles(args, levelsDir, assetsDir);
        if (perLevel is null)
        {
            AddFile(typeof(MoonfallGame).Assembly.Location);
            AddFile(typeof(MoonfallWindow).Assembly.Location);
            foreach (var dir in new[] { levelsDir, assetsDir })
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).OrderBy(static f => f, StringComparer.Ordinal))
                {
                    Add(Path.GetRelativePath(repo, f).Replace('\\', '/'));
                    AddFile(f);
                }
            }

            return Convert.ToHexString(h.GetHashAndReset()).ToLowerInvariant();
        }

        var built = new[] { typeof(MoonfallGame).Assembly.Location, typeof(MoonfallWindow).Assembly.Location };
        var oldestBuild = built.Min(static f => File.GetLastWriteTimeUtc(f));
        var code = new List<string>();
        foreach (var project in new[] { "Tsukimichi.Core", "Tsukimichi" })
        {
            var root = Path.Combine(repo, project);
            code.AddRange(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(f =>
            {
                var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
                return (rel.EndsWith(".cs", StringComparison.Ordinal) || rel.EndsWith(".csproj", StringComparison.Ordinal))
                    && !rel.StartsWith("obj/", StringComparison.Ordinal) && !rel.StartsWith("bin/", StringComparison.Ordinal);
            }));
        }

        code.AddRange(Directory.EnumerateFiles(repo, "Directory.*.props", SearchOption.TopDirectoryOnly));
        code.Sort(StringComparer.Ordinal);
        if (code.Concat(perLevel).Any(f => File.GetLastWriteTimeUtc(f) > oldestBuild))
        {
            return null;
        }

        AddFile(typeof(RenderCache).Assembly.Location);
        foreach (var f in code.Concat(perLevel.Order(StringComparer.Ordinal)))
        {
            Add(Path.GetRelativePath(repo, f).Replace('\\', '/'));
            AddFile(f);
        }

        return Convert.ToHexString(h.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>The files that make up one level's picture besides the code, or null when this run is not one level's.</summary>
    private static List<string>? PerLevelFiles(string[] args, string levelsDir, string assetsDir)
    {
        string? Arg(string name) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        var sceneOnly = args.Contains("--scene-only");
        var board = (Arg("--screen") ?? "play") == "play" && !args.Contains("--legacy") && Arg("--level") is not null;
        if ((!sceneOnly && !board) || Arg("--level") is not { } level)
        {
            return null;
        }

        var levelFile = Path.Combine(levelsDir, level + ".json");
        if (!File.Exists(levelFile))
        {
            return null;
        }

        string? scene;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(levelFile));
            scene = doc.RootElement.TryGetProperty("scene", out var sc) ? sc.GetString() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }

        var files = new List<string> { levelFile };
        if (!string.IsNullOrEmpty(scene))
        {
            var recipe = Path.Combine(levelsDir, "scenes", scene + ".json");
            if (!File.Exists(recipe))
            {
                return null;
            }

            files.Add(recipe);
            var names = new List<string> { scene };
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(recipe));
                if (doc.RootElement.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } rn)
                {
                    names.Add(rn);
                }

                if (doc.RootElement.TryGetProperty("fallback", out var fb) && fb.GetString() is { Length: > 0 } fn)
                {
                    names.Add(fn);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }

            var pictures = Path.Combine(assetsDir, "scenes");
            if (Directory.Exists(pictures))
            {
                files.AddRange(Directory.EnumerateFiles(pictures).Where(f => names.Any(n => Path.GetFileName(f).StartsWith(n, StringComparison.Ordinal))));
            }
        }

        if (Directory.Exists(assetsDir))
        {
            files.AddRange(Directory.EnumerateFiles(assetsDir, "*", SearchOption.TopDirectoryOnly));
        }

        return files;
    }

    /// <summary>Copies the stored PNG to <paramref name="outPath"/> if this key has one.</summary>
    public static bool TryServe(string cacheDir, string key, string outPath)
    {
        var stored = Path.Combine(cacheDir, key + ".png");
        if (!File.Exists(stored))
        {
            return false;
        }

        File.Copy(stored, outPath, overwrite: true);
        return true;
    }

    public static void Store(string cacheDir, string key, string outPath)
    {
        Directory.CreateDirectory(cacheDir);
        var tmp = Path.Combine(cacheDir, $"{key}.{Environment.ProcessId}.tmp");
        File.Copy(outPath, tmp, overwrite: true);
        File.Move(tmp, Path.Combine(cacheDir, key + ".png"), overwrite: true);
    }
}
