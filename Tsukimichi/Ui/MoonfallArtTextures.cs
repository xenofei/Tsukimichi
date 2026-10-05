using System;
using System.IO;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Ui;

/// <summary>
/// The host on Dalamud of Moonfall's interim art set (feature plan v9 G8): the atlas's sheets, the sky and a level's
/// shipped picture, held only while the Moonfall window is open, as the What's new art is (<see cref="ReleaseArtTexture"/>).
/// The lifetime rules (what loads when, what is let go, when it is disposed) are <see cref="MoonfallArtLoader{T}"/>'s,
/// tested without the game; here the manifest is read and its pictures' headers checked on a worker
/// (<see cref="MoonfallArtFiles.Load"/>), and each picture is rented from Dalamud's shared cache (<c>RentAsync</c>), so
/// decoding is off the framework thread too. Framework thread only.
/// </summary>
internal sealed class MoonfallArtTextures : IMoonfallArtHost<IDalamudTextureWrap>
{
    private readonly ITextureProvider textures;
    private readonly IPluginLog? log;

    /// <param name="textures">Dalamud's texture provider.</param>
    /// <param name="log">Where a missing or broken picture is logged, once each.</param>
    public MoonfallArtTextures(ITextureProvider textures, IPluginLog? log)
    {
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.log = log;
    }

    public int Frame => ImGui.GetFrameCount();

    public Task<MoonfallAtlasLoad> ReadManifest(string folder) => Task.Run(() => MoonfallArtFiles.Load(folder));

    public bool Exists(string path) => File.Exists(path);

    public Task<IDalamudTextureWrap> Rent(string path) => textures.GetFromFile(path).RentAsync();

    public (int Width, int Height) Size(IDalamudTextureWrap texture) => (texture.Width, texture.Height);

    public void Dispose(IDalamudTextureWrap texture) => texture.Dispose();

    public void DisposeWhenLanded(Task<IDalamudTextureWrap> load) => _ = load.ToContentDisposedTask(true);

    public void Warn(string message) => log?.Warning(message);
}
