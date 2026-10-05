using System;
using System.IO;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Ui;

/// <summary>
/// The in-play art read from the player's install at runtime (spec-rich2.md §6; <see cref="MoonfallGameArt{T}"/> holds
/// the lifetime rules): this is its host on Dalamud. A game texture is read and decoded off the framework thread with
/// <see cref="IDataManager.GetFileAsync{T}"/> (<c>_hr1</c> paths, BGRA as Lumina decodes them); one of Moonfall's own
/// scene pictures is read from <c>assets/moonfall/scenes/</c> beside the plugin and decoded off the thread too; textures
/// are made with <see cref="ITextureProvider.CreateFromRawAsync(RawImageSpecification, ReadOnlyMemory{byte}, string?, System.Threading.CancellationToken)"/>.
/// Nothing is written anywhere, and nothing goes over the network. Framework thread only.
/// </summary>
internal sealed class MoonfallGameArtTextures : IMoonfallGameArtHost<IDalamudTextureWrap>
{
    private readonly ITextureProvider textures;
    private readonly IDataManager data;
    private readonly string scenes;
    private readonly IPluginLog? log;

    /// <param name="textures">Dalamud's texture provider.</param>
    /// <param name="data">Dalamud's game data, for the game's own textures.</param>
    /// <param name="pluginDirectory">The plugin's folder: Moonfall's own scene pictures are in <c>assets/moonfall/scenes/</c> under it.</param>
    /// <param name="log">Where a missing or broken texture is logged, once each.</param>
    public MoonfallGameArtTextures(ITextureProvider textures, IDataManager data, string pluginDirectory, IPluginLog? log)
    {
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        scenes = Path.Combine(MoonfallArtFiles.Folder(pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory))), MoonfallArtFiles.ScenesFolder);
        this.log = log;
    }

    public int Frame => ImGui.GetFrameCount();

    public async Task<MoonfallImage?> ReadGameTexture(string path)
    {
        try
        {
            var tex = await data.GetFileAsync<TexFile>(path, default).ConfigureAwait(false);
            if (tex is null)
            {
                return null;
            }

            // Lumina decodes every format to B8G8R8A8.
            int w = tex.Header.Width, h = tex.Header.Height;
            return MoonfallImage.FromBytes(tex.ImageData, w, h, w * 4, bgra: true, keepAlpha: true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log?.Debug(ex, $"Moonfall: {path} could not be read");
            return null;
        }
    }

    public Task<MoonfallImage?> ReadPicture(string name) => Task.Run<MoonfallImage?>(() =>
    {
        if (!Core.Moonfall.MoonfallLevelLoader.IsSceneName(name))
        {
            return null;
        }

        foreach (var file in (ReadOnlySpan<string>)[name + "@2x.png", name + ".png"])
        {
            var path = Path.Combine(scenes, file);
            if (!File.Exists(path))
            {
                continue;
            }

            var decoded = MoonfallPng.Decode(File.ReadAllBytes(path), out var error);
            if (decoded is { } d)
            {
                return MoonfallImage.FromBytes(d.Rgba, d.Width, d.Height, d.Width * 4, bgra: false, keepAlpha: false);
            }

            log?.Warning($"Moonfall: {file} could not be read ({error})");
        }

        return null;
    });

    public async Task<IDalamudTextureWrap> Upload(MoonfallRgba pixels, string name) =>
        await textures.CreateFromRawAsync(RawImageSpecification.Rgba32(pixels.Width, pixels.Height), pixels.Pixels, name).ConfigureAwait(false);

    public void Dispose(IDalamudTextureWrap texture) => texture.Dispose();

    public void Warn(string message) => log?.Warning(message);
}
