using System.Security.Cryptography;
using System.Text;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Ui;

namespace Tsukimichi.MoonfallRender;

/// <summary>
/// The optional <c>--cache &lt;dir&gt;</c> of the renderer: a picture is keyed on every argument but the output path, the
/// hashes of the Tsukimichi.Core.dll and Tsukimichi.dll the renderer loaded, and the runtime's level and scene JSON and
/// shipped Moonfall art on disk; a matching key copies the stored PNG instead of rendering. Not part of the key: the game
/// install's textures and the Windows fonts (a game patch needs a fresh <c>--cache</c> folder). Runs that print
/// diagnostics (<c>--alloc</c>, <c>--text-check</c>) are never cached.
/// </summary>
internal static class RenderCache
{
    public static bool Applies(string[] args) => !args.Contains("--alloc") && !args.Contains("--text-check");

    public static string Key(string[] args, string repo)
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

        AddFile(typeof(MoonfallGame).Assembly.Location);
        AddFile(typeof(MoonfallWindow).Assembly.Location);
        foreach (var dir in new[] { Path.Combine(repo, "Tsukimichi.Core", "Moonfall", "Levels"), Path.Combine(repo, "Tsukimichi", "assets", "moonfall") })
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
