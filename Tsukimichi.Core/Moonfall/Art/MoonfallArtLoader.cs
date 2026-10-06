namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// What <see cref="MoonfallArtLoader{T}"/> needs from the plugin: Dalamud's textures, the files, the frame counter and the
/// log. The plugin's <c>MoonfallArtTextures</c> is the real one; the tests drive the loader with their own.
/// </summary>
/// <typeparam name="T">A texture.</typeparam>
public interface IMoonfallArtHost<T>
    where T : class
{
    /// <summary>The frame now (ImGui's frame count).</summary>
    int Frame { get; }

    /// <summary>Reads and checks the manifest in <paramref name="folder"/> off the framework thread (<see cref="MoonfallArtFiles.Load"/>).</summary>
    Task<MoonfallAtlasLoad> ReadManifest(string folder);

    /// <summary>Whether <paramref name="path"/> exists (a stat, no decode).</summary>
    bool Exists(string path);

    /// <summary>Starts decoding the picture at <paramref name="path"/> off the framework thread.</summary>
    Task<T> Rent(string path);

    /// <summary>A texture's size in pixels.</summary>
    (int Width, int Height) Size(T texture);

    /// <summary>Disposes a texture now (never one a frame could still draw).</summary>
    void Dispose(T texture);

    /// <summary>A load no longer wanted: what it brings is disposed when it lands.</summary>
    void DisposeWhenLanded(Task<T> load);

    /// <summary>Logs a missing or broken picture.</summary>
    void Warn(string message);
}

/// <summary>
/// Moonfall's art textures over their lifetime (feature plan v9 G8), as pure logic behind <see cref="IMoonfallArtHost{T}"/>
/// so the tests can drive it: the atlas's sheets, the sky and the level's scene, held only while the window is open.
/// The manifest is read once, on a worker; each picture is decoded off the framework thread. Nothing here blocks a
/// frame: until the manifest and a sheet are in, <see cref="Sheet"/> is null and the board draws its stage 1 primitives,
/// and a missing, corrupt or wrongly sized picture is logged once and treated as absent (<see cref="MoonfallArtSlots{T}"/>
/// holds the fallback rules). A load overtaken (another scene, the window closing) disposes what it brings when it
/// lands; a texture let go is disposed <see cref="RetireFrames"/> frames later, never in a frame that could still draw
/// it. Framework thread only.
/// </summary>
/// <typeparam name="T">A texture.</typeparam>
public sealed class MoonfallArtLoader<T> : IDisposable
    where T : class
{
    /// <summary>Frames a released texture is kept before it is disposed.</summary>
    public const int RetireFrames = 3;

    private const int SlotCount = (int)MoonfallArtSlot.Scene2x + 1;

    private readonly IMoonfallArtHost<T> host;
    private readonly string folder;
    private readonly MoonfallArtSlots<T> slots = new();
    private readonly Task<T>?[] rents = new Task<T>?[SlotCount];
    private readonly ((int Width, int Height) Size, string Path)[] expected = new ((int, int), string)[SlotCount];
    private readonly List<T> released = [];
    private readonly List<(T Texture, int Frame)> retired = [];
    private readonly HashSet<string> warned = new(StringComparer.OrdinalIgnoreCase);
    private Task<MoonfallAtlasLoad>? manifest;

    /// <param name="host">The textures, files, frame counter and log.</param>
    /// <param name="folder">The art folder (<see cref="MoonfallArtFiles.Folder"/>).</param>
    public MoonfallArtLoader(IMoonfallArtHost<T> host, string folder)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.folder = folder ?? throw new ArgumentNullException(nameof(folder));
    }

    /// <summary>The art set's manifest, once read; null while it reads or when it was refused.</summary>
    public MoonfallAtlas? Atlas => slots.Atlas;

    /// <summary>Where <paramref name="slot"/>'s picture is, for the tests.</summary>
    public MoonfallSlotState State(MoonfallArtSlot slot) => slots.State(slot);

    /// <summary>Textures let go and not yet disposed (retired, or released this frame).</summary>
    public int Retiring => retired.Count + released.Count;

    /// <summary>
    /// Once a frame while the board draws: reads the manifest the first time, lands finished loads, and starts what the
    /// frame needs: the 1x sheet always, the 2x sheet when <paramref name="wantTwoX"/>, the level's
    /// <paramref name="scene"/> at that tier (another scene lets the old one go) and the sky only when no scene stands in.
    /// A scene that is not a scene's name (<see cref="MoonfallLevelLoader.IsSceneName"/>) is none.
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
                manifest = host.ReadManifest(folder);
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

        scene = MoonfallLevelLoader.IsSceneName(scene) ? scene : null;
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

        // Each path is made only when its slot loads (never a string a frame once everything is in).
        if (slots.ShouldLoad(MoonfallArtSlot.Sheet1x))
        {
            Start(MoonfallArtSlot.Sheet1x, Path.Combine(folder, atlas.OneXFile), (atlas.Width, atlas.Height));
        }

        if (wantTwoX && slots.ShouldLoad(MoonfallArtSlot.Sheet2x))
        {
            Start(MoonfallArtSlot.Sheet2x, Path.Combine(folder, atlas.TwoXFile), (atlas.Width * 2, atlas.Height * 2));
        }

        var sceneSlot = wantTwoX ? MoonfallArtSlot.Scene2x : MoonfallArtSlot.Scene1x;
        if (scene is not null && slots.ShouldLoad(sceneSlot))
        {
            Start(sceneSlot, MoonfallArtFiles.ScenePath(folder, scene, wantTwoX), MoonfallArtFiles.SceneSize(wantTwoX));
        }

        if (slots.NeedsSky && slots.ShouldLoad(MoonfallArtSlot.Sky))
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
    public T? Sheet(bool wantTwoX, out bool twoX) => slots.Sheet(wantTwoX, out twoX);

    /// <summary>The board's ground picture this frame (<see cref="MoonfallArtSlots{T}.Ground"/>).</summary>
    public T? Ground(bool wantTwoX, out MoonfallGround ground) => slots.Ground(wantTwoX, out ground);

    /// <summary>
    /// The window has closed: every picture is let go (disposed a few frames later), loads still running dispose what
    /// they bring, and a refused manifest is read again on the next open; a manifest that was read stays read.
    /// </summary>
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
        var frame = host.Frame;
        for (var i = retired.Count - 1; i >= 0; i--)
        {
            if (frame - retired[i].Frame > RetireFrames)
            {
                DisposeTexture(retired[i].Texture);
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

        foreach (var texture in released)
        {
            DisposeTexture(texture);
        }

        released.Clear();
        foreach (var (texture, _) in retired)
        {
            DisposeTexture(texture);
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
        if (!host.Exists(path))
        {
            slots.Fail(slot);
            Warn(path, $"Moonfall art: {Path.GetFileName(path)} is missing; {Fallback(slot)}");
            return;
        }

        try
        {
            slots.Begin(slot);
            rents[(int)slot] = host.Rent(path);
            expected[(int)slot] = (size, path);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            slots.Fail(slot);
            Warn(path, $"Moonfall art: {Path.GetFileName(path)} could not be loaded ({ex.Message}); {Fallback(slot)}");
        }
    }

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

        var texture = rent.Result;
        var (width, height) = host.Size(texture);
        if (width != size.Width || height != size.Height)
        {
            DisposeTexture(texture);
            slots.Fail(slot);
            Warn(path, $"Moonfall art: {Path.GetFileName(path)} is {width} x {height}, not {size.Width} x {size.Height}; {Fallback(slot)}");
            return;
        }

        if (slots.Land(slot, texture) is { } late)
        {
            released.Add(late);
        }
    }

    private void Release(MoonfallArtSlot slot)
    {
        if (slots.Clear(slot) is { } texture)
        {
            released.Add(texture);
        }
    }

    /// <summary>Forgets a load still running; what it brings is disposed when it lands.</summary>
    private void Abandon(MoonfallArtSlot slot)
    {
        if (rents[(int)slot] is { } running)
        {
            host.DisposeWhenLanded(running);
            rents[(int)slot] = null;
        }
    }

    private void Retire()
    {
        if (released.Count == 0)
        {
            return;
        }

        var frame = host.Frame;
        foreach (var texture in released)
        {
            retired.Add((texture, frame));
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
            host.Warn(message);
        }
    }

    private void DisposeTexture(T texture)
    {
        try
        {
            host.Dispose(texture);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            host.Warn($"Moonfall art: a texture could not be disposed ({ex.Message})");
        }
    }
}
