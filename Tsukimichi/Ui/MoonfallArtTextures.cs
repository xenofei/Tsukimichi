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
/// Moonfall's art textures (feature plan v9 G8): the atlas's sheets, the sky and the level's scene, held only while the
/// Moonfall window is open, as the What's new art is (<see cref="ReleaseArtTexture"/>). The lifetime rules (what loads
/// when, what is let go, when it is disposed) are <see cref="MoonfallArtLoader{T}"/>'s, tested without the game; this is
/// its host on Dalamud: the manifest is read and its pictures' headers checked on a worker
/// (<see cref="MoonfallArtFiles.Load"/>), and each picture is rented from Dalamud's shared cache (<c>RentAsync</c>), so
/// decoding is off the framework thread too. Framework thread only.
/// </summary>
internal sealed class MoonfallArtTextures : IMoonfallArtHost<IDalamudTextureWrap>, IDisposable
{
    private readonly ITextureProvider textures;
    private readonly IPluginLog? log;
    private readonly MoonfallArtLoader<IDalamudTextureWrap> loader;

    /// <param name="textures">Dalamud's texture provider.</param>
    /// <param name="pluginDirectory">The plugin's folder; the art is in <c>assets/moonfall/</c> under it.</param>
    /// <param name="log">Where a missing or broken picture is logged, once each.</param>
    public MoonfallArtTextures(ITextureProvider textures, string pluginDirectory, IPluginLog? log)
    {
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.log = log;
        loader = new MoonfallArtLoader<IDalamudTextureWrap>(this, MoonfallArtFiles.Folder(pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory))));
    }

    /// <summary>The art set's manifest, once read; null while it reads or when it was refused.</summary>
    public MoonfallAtlas? Atlas => loader.Atlas;

    /// <inheritdoc cref="MoonfallArtLoader{T}.Frame"/>
    public void Frame(bool wantTwoX, string? scene) => loader.Frame(wantTwoX, scene);

    /// <summary>The atlas sheet to draw with this frame (<see cref="MoonfallArtSlots{T}.Sheet"/>); null: draw the primitives.</summary>
    public IDalamudTextureWrap? Sheet(bool wantTwoX, out bool twoX) => loader.Sheet(wantTwoX, out twoX);

    /// <summary>The board's ground picture this frame (<see cref="MoonfallArtSlots{T}.Ground"/>).</summary>
    public IDalamudTextureWrap? Ground(bool wantTwoX, out MoonfallGround ground) => loader.Ground(wantTwoX, out ground);

    /// <inheritdoc cref="MoonfallArtLoader{T}.Release"/>
    public void Release() => loader.Release();

    /// <inheritdoc cref="MoonfallArtLoader{T}.Tick"/>
    public void Tick() => loader.Tick();

    /// <inheritdoc cref="MoonfallArtLoader{T}.Dispose"/>
    public void Dispose() => loader.Dispose();

    // ---- The host on Dalamud ----

    int IMoonfallArtHost<IDalamudTextureWrap>.Frame => ImGui.GetFrameCount();

    Task<MoonfallAtlasLoad> IMoonfallArtHost<IDalamudTextureWrap>.ReadManifest(string folder) => Task.Run(() => MoonfallArtFiles.Load(folder));

    bool IMoonfallArtHost<IDalamudTextureWrap>.Exists(string path) => File.Exists(path);

    Task<IDalamudTextureWrap> IMoonfallArtHost<IDalamudTextureWrap>.Rent(string path) => textures.GetFromFile(path).RentAsync();

    (int Width, int Height) IMoonfallArtHost<IDalamudTextureWrap>.Size(IDalamudTextureWrap texture) => (texture.Width, texture.Height);

    void IMoonfallArtHost<IDalamudTextureWrap>.Dispose(IDalamudTextureWrap texture) => texture.Dispose();

    void IMoonfallArtHost<IDalamudTextureWrap>.DisposeWhenLanded(Task<IDalamudTextureWrap> load) => _ = load.ToContentDisposedTask(true);

    void IMoonfallArtHost<IDalamudTextureWrap>.Warn(string message) => log?.Warning(message);
}
