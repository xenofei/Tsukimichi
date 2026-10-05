using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Tsukimichi.Core.Portraits;

/// <summary>
/// The portrait pack (feature plan v7 F4): the folder it lives in, its manifest, and each giver's image path, worked out
/// once so a plate asks for one without allocating. Since 1.23 the pack ships inside the plugin
/// (<see cref="BundledPortraits"/>). Immutable; safe to read from any thread.
/// </summary>
public sealed class PortraitPack
{
    private readonly FrozenDictionary<uint, string> paths;
    private readonly FrozenDictionary<uint, int> boxes;

    /// <param name="folder">The folder holding the manifest's images.</param>
    /// <param name="manifest">The pack's manifest.</param>
    /// <param name="sha256">The pack's identity: the SHA-256 of its manifest (which lists every image's hash).</param>
    /// <param name="tag">Where it came from, for the log ("bundled").</param>
    public PortraitPack(string folder, PortraitPackManifest manifest, string sha256, string tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        Folder = Path.GetFullPath(folder);
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        Sha256 = sha256 ?? string.Empty;
        Tag = tag ?? string.Empty;
        var files = manifest.Files.Keys.ToDictionary(name => name, name => Path.Combine(Folder, name), StringComparer.Ordinal);
        paths = manifest.Entries.ToFrozenDictionary(kv => kv.Key, kv => files[kv.Value]);
        boxes = manifest.Entries.Where(kv => manifest.Boxes.ContainsKey(kv.Value)).ToFrozenDictionary(kv => kv.Key, kv => manifest.Boxes[kv.Value]);
    }

    /// <summary>How many distinct faces (images) it holds.</summary>
    public int Faces => Manifest.Files.Count;

    /// <summary>The head box (source pixels) of <paramref name="npcId"/>'s photo; 0 when the pack does not say.</summary>
    public int BoxOf(uint npcId) => boxes.GetValueOrDefault(npcId);

    /// <summary>The pack's folder (full path).</summary>
    public string Folder { get; }

    public PortraitPackManifest Manifest { get; }

    /// <summary>The pack's identity (the SHA-256 of its manifest): a new pack's photos are new art.</summary>
    public string Sha256 { get; }

    /// <summary>Where it came from, for the log ("bundled").</summary>
    public string Tag { get; }

    /// <summary>How many givers have a portrait in it.</summary>
    public int Givers => paths.Count;

    /// <summary>The game version the pack was built for.</summary>
    public string GameVersion => Manifest.GameVersion;

    /// <summary>Whether the pack has a portrait of ENpcResident <paramref name="npcId"/>.</summary>
    public bool Has(uint npcId) => paths.ContainsKey(npcId);

    /// <summary>The full path of <paramref name="npcId"/>'s image; false when the pack has none.</summary>
    public bool TryGetPath(uint npcId, [MaybeNullWhen(false)] out string path) => paths.TryGetValue(npcId, out path);
}
