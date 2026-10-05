using System;
using System.Collections.Generic;
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
/// Moonfall window is open, as the What's new art is (<see cref="ReleaseArtTexture"/>). The manifest is read and its
/// pictures' headers checked on a worker (<see cref="MoonfallArtFiles.Load"/>); each picture is rented from Dalamud's
/// shared cache (<c>RentAsync</c>), so decoding is off the framework thread too. Nothing here blocks a frame: until the
/// manifest and a sheet are in, <see cref="Sheet"/> is null and the board draws its stage 1 primitives, and a missing,
/// corrupt or wrongly sized picture is logged once and treated as absent (<see cref="MoonfallArtSlots{T}"/> holds the
/// fallback rules). A texture let go is disposed <see cref="RetireFrames"/> frames later, never in a frame that could
/// still draw it. Framework thread only.
/// </summary>
internal sealed class MoonfallArtTextures : IDisposable
{
    /// <summary>Frames a released texture is kept before it is disposed.</summary>
    private const int RetireFrames = 3;

    private const int SlotCount = (int)MoonfallArtSlot.Scene2x + 1;

    private readonly ITextureProvider textures;
    private readonly IPluginLog? log;
    private readonly string folder;
    private readonly MoonfallArtSlots<IDalamudTextureWrap> slots = new();
    private readonly Task<IDalamudTextureWrap>?[] rents = new Task<IDalamudTextureWrap>?[SlotCount];
    private readonly List<IDalamudTextureWrap> released = [];
    private readonly List<(IDalamudTextureWrap Wrap, int Frame)> retired = [];
    private readonly HashSet<string> warned = new(StringComparer.OrdinalIgnoreCase);
    private Task<MoonfallAtlasLoad>? manifest;

    /// <param name="textures">Dalamud's texture provider.</param>
    /// <param name="pluginDirectory">The plugin's folder; the art is in <c>assets/moonfall/</c> under it.</param>
    /// <param name="log">Where a missing or broken picture is logged, once each.</param>
    public MoonfallArtTextures(ITextureProvider textures, string pluginDirectory, IPluginLog? log)
    {
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        folder = MoonfallArtFiles.Folder(pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory)));
        this.log = log;
    }

    /// <summary>The art set's manifest, once read; null while it reads or when it was refused.</summary>
    public MoonfallAtlas? Atlas => slots.Atlas;

    /// <summary>
    /// Once a frame while the board draws: reads the manifest the first time, lands finished loads, and starts what the
    /// frame needs: the 1x sheet always, the 2x sheet when <paramref name="wantTwoX"/>, the level's
    /// <paramref name="scene"/> at that tier (another scene lets the old one go) and the sky only when no scene stands in.
    /// </summary>
    public void Frame(bool wantTwoX, string? scene)
    {
        if (slots.Atlas is null)
        {
            if (slots.ManifestFailed)
            {
                return;
            }

            if (manifest is null)
            {
                var at = folder;
                manifest = Task.Run(() => MoonfallArtFiles.Load(at));
                return;
            }

            if (!manifest.IsCompleted)
            {
                return;
            }

            var load = manifest.IsCompletedSuccessfully ? manifest.Result : new MoonfallAtlasLoad(null, [manifest.Exception?.GetBaseException().Message ?? "cancelled"]);
            manifest = null;
            slots.SetManifest(load.Atlas);
            if (load.Atlas is null)
            {
                Warn("atlas", $"Moonfall art not loaded; the board draws its plain shapes ({string.Join("; ", load.Errors)})");
                return;
            }
        }

        if (slots.Atlas is not { } atlas)
        {
            return;
        }

        if (!string.Equals(scene, slots.Scene, StringComparison.Ordinal))
        {
            // Another level's scene: loads of the old one still running dispose what they bring.
            Abandon(MoonfallArtSlot.Scene1x);
            Abandon(MoonfallArtSlot.Scene2x);
            slots.WantScene(scene, released);
        }

        Retire();
        for (var i = 0; i < SlotCount; i++)
        {
            Land((MoonfallArtSlot)i);
        }

        // The wanted scene tier has landed: the other (a resize across 800 x 600, or the zoom in or out) goes back.
        if (slots.SceneToDrop(wantTwoX, out var unused))
        {
            Abandon(unused);
            Release(unused);
        }

        Start(MoonfallArtSlot.Sheet1x, Path.Combine(folder, atlas.OneXFile), (atlas.Width, atlas.Height));
        if (wantTwoX)
        {
            Start(MoonfallArtSlot.Sheet2x, Path.Combine(folder, atlas.TwoXFile), (atlas.Width * 2, atlas.Height * 2));
        }

        if (scene is not null)
        {
            Start(wantTwoX ? MoonfallArtSlot.Scene2x : MoonfallArtSlot.Scene1x, MoonfallArtFiles.ScenePath(folder, scene, wantTwoX), MoonfallArtFiles.SceneSize(wantTwoX));
        }

        if (slots.NeedsSky)
        {
            Start(MoonfallArtSlot.Sky, Path.Combine(folder, atlas.SkyFile), ((int)atlas.Sky.W, (int)atlas.Sky.H));
        }
        else if (slots.State(MoonfallArtSlot.Sky) == MoonfallSlotState.Ready && slots.Ground(wantTwoX, out var ground) is not null && ground == MoonfallGround.Scene)
        {
            // The scene has landed: the sky's memory goes back.
            Release(MoonfallArtSlot.Sky);
        }
    }

    /// <summary>The atlas sheet to draw with this frame (<see cref="MoonfallArtSlots{T}.Sheet"/>); null: draw the primitives.</summary>
    public IDalamudTextureWrap? Sheet(bool wantTwoX, out bool twoX) => slots.Sheet(wantTwoX, out twoX);

    /// <summary>The board's ground picture this frame (<see cref="MoonfallArtSlots{T}.Ground"/>).</summary>
    public IDalamudTextureWrap? Ground(bool wantTwoX, out MoonfallGround ground) => slots.Ground(wantTwoX, out ground);

    /// <summary>The window has closed: every picture is let go (disposed a few frames later); the manifest stays read.</summary>
    public void Release()
    {
        slots.ReleaseAll(released);
        for (var i = 0; i < SlotCount; i++)
        {
            Abandon((MoonfallArtSlot)i);
        }

        Retire();
    }

    /// <summary>Once a frame, open or not: disposes what was let go more than <see cref="RetireFrames"/> frames ago.</summary>
    public void Tick()
    {
        Retire();
        var frame = ImGui.GetFrameCount();
        for (var i = retired.Count - 1; i >= 0; i--)
        {
            if (frame - retired[i].Frame > RetireFrames)
            {
                DisposeWrap(retired[i].Wrap);
                retired.RemoveAt(i);
            }
        }
    }

    /// <summary>On unload: everything held, retired or still loading is disposed now.</summary>
    public void Dispose()
    {
        slots.ReleaseAll(released);
        for (var i = 0; i < SlotCount; i++)
        {
            Abandon((MoonfallArtSlot)i);
        }

        foreach (var wrap in released)
        {
            DisposeWrap(wrap);
        }

        released.Clear();
        foreach (var (wrap, _) in retired)
        {
            DisposeWrap(wrap);
        }

        retired.Clear();
    }

    private void Start(MoonfallArtSlot slot, string path, (int Width, int Height) size)
    {
        if (!slots.ShouldLoad(slot))
        {
            return;
        }

        // A missing file is known at once (a stat, no decode); the size is checked when the texture lands.
        if (!File.Exists(path))
        {
            slots.Fail(slot);
            Warn(path, $"Moonfall art: {Path.GetFileName(path)} is missing; {Fallback(slot)}");
            return;
        }

        try
        {
            slots.Begin(slot);
            rents[(int)slot] = textures.GetFromFile(path).RentAsync();
            expected[(int)slot] = (size, path);
        }
        catch (Exception ex)
        {
            slots.Fail(slot);
            Warn(path, $"Moonfall art: {Path.GetFileName(path)} could not be loaded ({ex.Message}); {Fallback(slot)}");
        }
    }

    private readonly ((int Width, int Height) Size, string Path)[] expected = new ((int, int), string)[SlotCount];

    private void Land(MoonfallArtSlot slot)
    {
        if (rents[(int)slot] is not { IsCompleted: true } rent)
        {
            return;
        }

        rents[(int)slot] = null;
        var (size, path) = expected[(int)slot];
        if (!rent.IsCompletedSuccessfully)
        {
            slots.Fail(slot);
            Warn(path, $"Moonfall art: {Path.GetFileName(path)} could not be decoded ({rent.Exception?.GetBaseException().Message ?? "cancelled"}); {Fallback(slot)}");
            return;
        }

        var wrap = rent.Result;
        if (wrap.Width != size.Width || wrap.Height != size.Height)
        {
            DisposeWrap(wrap);
            slots.Fail(slot);
            Warn(path, $"Moonfall art: {Path.GetFileName(path)} is {wrap.Width} x {wrap.Height}, not {size.Width} x {size.Height}; {Fallback(slot)}");
            return;
        }

        if (slots.Land(slot, wrap) is { } late)
        {
            released.Add(late);
        }
    }

    private void Release(MoonfallArtSlot slot)
    {
        if (slots.Clear(slot) is { } wrap)
        {
            released.Add(wrap);
        }
    }

    /// <summary>Forgets a load still running; what it brings is disposed when it lands.</summary>
    private void Abandon(MoonfallArtSlot slot)
    {
        if (rents[(int)slot] is { } running)
        {
            _ = running.ToContentDisposedTask(true);
            rents[(int)slot] = null;
        }
    }

    private void Retire()
    {
        if (released.Count == 0)
        {
            return;
        }

        var frame = ImGui.GetFrameCount();
        foreach (var wrap in released)
        {
            retired.Add((wrap, frame));
        }

        released.Clear();
    }

    private static string Fallback(MoonfallArtSlot slot) => slot switch
    {
        MoonfallArtSlot.Scene1x or MoonfallArtSlot.Scene2x => "the level shows the night sky",
        MoonfallArtSlot.Sky => "the board shows its flat ground",
        MoonfallArtSlot.Sheet2x => "the board scales the 1x art",
        _ => "the board draws its plain shapes",
    };

    private void Warn(string key, string message)
    {
        if (warned.Add(key))
        {
            log?.Warning(message);
        }
    }

    private void DisposeWrap(IDalamudTextureWrap wrap)
    {
        try
        {
            wrap.Dispose();
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "Moonfall art: a texture could not be disposed");
        }
    }
}
