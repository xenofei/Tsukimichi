using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Lumina.Data.Files;
using Tsukimichi.Core.Moonfall.Art;
using Tsukimichi.Ui;
using LuminaGameData = Lumina.GameData;

namespace Tsukimichi.MoonfallRender;

/// <summary>A texture of the offline render: an id the rasterizer knows, standing in for one on the GPU.</summary>
internal sealed class Wrap(ulong id, int width, int height) : IDalamudTextureWrap
{
    public ImTextureID Handle { get; } = new(id);

    public int Width { get; } = width;

    public int Height { get; } = height;

    public Vector2 Size => new(Width, Height);

    public IDalamudTextureWrap CreateWrapSharingLowLevelResource() => this;

    public void Dispose()
    {
    }
}

/// <summary>Registers RGBA pictures as textures with the rasterizer.</summary>
internal sealed class TextureStore(Raster raster)
{
    private ulong next = 100;

    public Wrap Add(int width, int height, byte[] rgba)
    {
        var id = next++;
        raster.Textures[id] = new Texture(width, height, rgba);
        return new Wrap(id, width, height);
    }
}

/// <summary>The interim art set from the repository's assets, loaded at once.</summary>
internal sealed class ArtHost(TextureStore store) : IMoonfallArtHost<IDalamudTextureWrap>
{
    public int Frame { get; set; }

    public Task<MoonfallAtlasLoad> ReadManifest(string folder) => Task.FromResult(MoonfallArtFiles.Load(folder));

    public bool Exists(string path) => File.Exists(path);

    public Task<IDalamudTextureWrap> Rent(string path)
    {
        var decoded = MoonfallPng.Decode(File.ReadAllBytes(path), out var error) ?? throw new InvalidDataException(error);
        return Task.FromResult<IDalamudTextureWrap>(store.Add(decoded.Width, decoded.Height, decoded.Rgba));
    }

    public (int Width, int Height) Size(IDalamudTextureWrap texture) => (texture.Width, texture.Height);

    public void Dispose(IDalamudTextureWrap texture)
    {
    }

    public void DisposeWhenLanded(Task<IDalamudTextureWrap> load)
    {
    }

    public void Warn(string message) => Console.Error.WriteLine("art: " + message);
}

/// <summary>The game's art from the install through Lumina, and Moonfall's own pictures from the repository, at once.</summary>
internal sealed class GameHost(TextureStore store, LuminaGameData? game, string scenes) : IMoonfallGameArtHost<IDalamudTextureWrap>
{
    public int Frame { get; set; }

    /// <summary>Game paths to treat as missing (to render the fallbacks).</summary>
    public HashSet<string> Missing { get; } = new(StringComparer.Ordinal);

    public Task<MoonfallImage?> ReadGameTexture(string path)
    {
        if (game is null || Missing.Contains(path))
        {
            return Task.FromResult<MoonfallImage?>(null);
        }

        lock (game)
        {
            var tex = game.GetFile<TexFile>(path);
            return Task.FromResult(tex is null ? null : MoonfallImage.FromBytes(tex.ImageData, tex.Header.Width, tex.Header.Height, tex.Header.Width * 4, bgra: true, keepAlpha: true));
        }
    }

    public Task<MoonfallImage?> ReadPicture(string name)
    {
        foreach (var file in new[] { name + "@2x.png", name + ".png" })
        {
            var path = Path.Combine(scenes, file);
            if (File.Exists(path) && MoonfallPng.Decode(File.ReadAllBytes(path), out _) is { } d)
            {
                return Task.FromResult<MoonfallImage?>(MoonfallImage.FromBytes(d.Rgba, d.Width, d.Height, d.Width * 4, bgra: false, keepAlpha: false));
            }
        }

        return Task.FromResult<MoonfallImage?>(null);
    }

    public Task<IDalamudTextureWrap> Upload(MoonfallRgba pixels, string name) =>
        Task.FromResult<IDalamudTextureWrap>(store.Add(pixels.Width, pixels.Height, pixels.Pixels));

    public void Dispose(IDalamudTextureWrap texture)
    {
    }

    public void Warn(string message) => Console.Error.WriteLine("game art: " + message);
}

/// <summary>Moonfall's options for the render.</summary>
internal sealed class Options : IMoonfallOptions
{
    public bool PegMarks { get; set; }

    public bool PegMarksHintSeen { get; set; } = true;

    public void Save()
    {
    }
}
