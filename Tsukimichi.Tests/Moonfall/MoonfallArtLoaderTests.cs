using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// The art textures' lifetime (<see cref="MoonfallArtLoader{T}"/>, which the plugin's <c>MoonfallArtTextures</c> hosts on
/// Dalamud), driven frame by frame with loads the test lands by hand: a scene changed while it loads, the window closed
/// while pictures load, the scene tier dropped once the other lands, and a refused manifest read again.
/// </summary>
public sealed class MoonfallArtLoaderTests
{
    private const string Folder = "art";

    private sealed class Texture(string path, int width, int height)
    {
        public string Path { get; } = path;

        public int Width { get; } = width;

        public int Height { get; } = height;

        public bool Disposed { get; set; }

        public override string ToString() => System.IO.Path.GetFileName(Path);
    }

    /// <summary>A host whose loads land only when the test says, on the test's thread.</summary>
    private sealed class Host : IMoonfallArtHost<Texture>
    {
        public int Frame { get; set; } = 100;

        public int ManifestReads { get; private set; }

        public TaskCompletionSource<MoonfallAtlasLoad>? Manifest { get; private set; }

        public List<(string Path, TaskCompletionSource<Texture> Load)> Rents { get; } = [];

        public List<Texture> Disposed { get; } = [];

        public List<string> Warnings { get; } = [];

        public Task<MoonfallAtlasLoad> ReadManifest(string folder)
        {
            ManifestReads++;
            Manifest = new TaskCompletionSource<MoonfallAtlasLoad>();
            return Manifest.Task;
        }

        public bool Exists(string path) => true;

        public Task<Texture> Rent(string path)
        {
            var load = new TaskCompletionSource<Texture>();
            Rents.Add((path, load));
            return load.Task;
        }

        public (int Width, int Height) Size(Texture texture) => (texture.Width, texture.Height);

        public void Dispose(Texture texture)
        {
            Assert.False(texture.Disposed, $"{texture} disposed twice");
            texture.Disposed = true;
            Disposed.Add(texture);
        }

        public void DisposeWhenLanded(Task<Texture> load) =>
            load.ContinueWith(t => Dispose(t.Result), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

        public void Warn(string message) => Warnings.Add(message);

        /// <summary>Whether a load of <paramref name="file"/> (its file name) is waiting.</summary>
        public bool Waiting(string file) => Rents.Any(r => r.Path.EndsWith(file, StringComparison.Ordinal) && !r.Load.Task.IsCompleted);

        /// <summary>Lands the oldest waiting load of <paramref name="file"/> at the size given; returns its texture.</summary>
        public Texture Land(string file, (int Width, int Height) size)
        {
            var (path, load) = Rents.First(r => r.Path.EndsWith(file, StringComparison.Ordinal) && !r.Load.Task.IsCompleted);
            var texture = new Texture(path, size.Width, size.Height);
            load.SetResult(texture);
            return texture;
        }
    }

    private static readonly MoonfallAtlas Atlas = MoonfallArtFiles.Load(MoonfallArtTests.ArtFolder()).Atlas!;

    private static (int, int) OneX => (Atlas.Width, Atlas.Height);

    private static (int, int) TwoX => (Atlas.Width * 2, Atlas.Height * 2);

    private static (int, int) Sky => ((int)Atlas.Sky.W, (int)Atlas.Sky.H);

    /// <summary>A loader whose manifest has been read (two frames: the read starts, then lands).</summary>
    private static (MoonfallArtLoader<Texture> Loader, Host Host) Opened(bool twoX, string? scene)
    {
        var host = new Host();
        var loader = new MoonfallArtLoader<Texture>(host, Folder);
        loader.Frame(twoX, scene);
        Assert.Null(loader.Atlas);
        host.Manifest!.SetResult(new MoonfallAtlasLoad(Atlas, []));
        loader.Frame(twoX, scene);
        Assert.Same(Atlas, loader.Atlas);
        return (loader, host);
    }

    /// <summary>Frames go by: <see cref="MoonfallArtLoader{T}.Tick"/> each, as the window's PreOpenCheck runs it.</summary>
    private static void Frames(MoonfallArtLoader<Texture> loader, Host host, int count)
    {
        for (var k = 0; k < count; k++)
        {
            host.Frame++;
            loader.Tick();
        }
    }

    [Fact]
    public void A_scene_changed_while_it_loads_never_draws_and_is_disposed_when_it_lands()
    {
        var (loader, host) = Opened(false, "moon-road-night");
        Assert.True(host.Waiting("moon-road-night.png"));
        Assert.Equal(MoonfallSlotState.Loading, loader.State(MoonfallArtSlot.Scene1x));

        // The next level, before the first scene has landed: its load is started, the first one forgotten.
        loader.Frame(false, "harbour-dusk");
        Assert.True(host.Waiting("harbour-dusk.png"));
        var old = host.Land("moon-road-night.png", MoonfallArtFiles.SceneSize(false));
        Assert.True(old.Disposed, "the old scene's picture was kept");

        // Until the new one lands the ground is flat (the sheet in, no sky needed for a level with a scene).
        host.Land("atlas.png", OneX);
        loader.Frame(false, "harbour-dusk");
        Assert.Null(loader.Ground(false, out var ground));
        Assert.Equal(MoonfallGround.Flat, ground);
        Assert.False(host.Waiting("sky.png"));

        var scene = host.Land("harbour-dusk.png", MoonfallArtFiles.SceneSize(false));
        loader.Frame(false, "harbour-dusk");
        Assert.Same(scene, loader.Ground(false, out ground));
        Assert.Equal(MoonfallGround.Scene, ground);
        Assert.False(scene.Disposed);
    }

    [Fact]
    public void Closing_while_pictures_load_disposes_them_as_they_land_and_lets_the_held_ones_go_a_few_frames_later()
    {
        var (loader, host) = Opened(false, null);
        var sheet = host.Land("atlas.png", OneX);
        loader.Frame(false, null);
        Assert.Same(sheet, loader.Sheet(false, out _));

        // The zoom asks for the 2x sheet; the window closes while it and the sky load.
        loader.Frame(true, null);
        Assert.True(host.Waiting("atlas@2x.png"));
        Assert.True(host.Waiting("sky.png"));
        loader.Release();
        Assert.Null(loader.Sheet(false, out _));
        Assert.Equal(MoonfallSlotState.Idle, loader.State(MoonfallArtSlot.Sheet2x));

        var two = host.Land("atlas@2x.png", TwoX);
        var sky = host.Land("sky.png", Sky);
        Assert.True(two.Disposed);
        Assert.True(sky.Disposed);

        // The sheet that was drawn waits out the frames that could still draw it.
        Frames(loader, host, MoonfallArtLoader<Texture>.RetireFrames);
        Assert.False(sheet.Disposed);
        Frames(loader, host, 1);
        Assert.True(sheet.Disposed);
        Assert.Equal(0, loader.Retiring);

        // Opened again: everything is asked for afresh, the manifest is not read again.
        loader.Frame(false, null);
        Assert.True(host.Waiting("atlas.png"));
        Assert.Equal(1, host.ManifestReads);
    }

    [Fact]
    public void The_other_scene_tier_goes_once_the_wanted_one_lands_and_stands_in_until_then()
    {
        var (loader, host) = Opened(false, "moon-road-night");
        host.Land("atlas.png", OneX);
        var one = host.Land("moon-road-night.png", MoonfallArtFiles.SceneSize(false));
        loader.Frame(false, "moon-road-night");
        Assert.Same(one, loader.Ground(false, out _));

        // Zoomed in: the 2x scene loads while the 1x one still draws.
        loader.Frame(true, "moon-road-night");
        Assert.True(host.Waiting("moon-road-night@2x.png"));
        Assert.Same(one, loader.Ground(true, out _));
        var two = host.Land("moon-road-night@2x.png", MoonfallArtFiles.SceneSize(true));
        loader.Frame(true, "moon-road-night");
        Assert.Same(two, loader.Ground(true, out _));
        Assert.Equal(MoonfallSlotState.Idle, loader.State(MoonfallArtSlot.Scene1x));

        // The 1x scene goes back (retired on the next frame), disposed only after the frames that could still draw it.
        Assert.False(one.Disposed);
        Frames(loader, host, MoonfallArtLoader<Texture>.RetireFrames + 1);
        Assert.False(one.Disposed);
        Frames(loader, host, 1);
        Assert.True(one.Disposed);
        Assert.False(two.Disposed);

        // Out again, and back in before the 1x lands: the 2x stands in, and the 1x load is dropped with nothing kept.
        loader.Frame(false, "moon-road-night");
        Assert.True(host.Waiting("moon-road-night.png"));
        Assert.Same(two, loader.Ground(false, out _));
        loader.Frame(true, "moon-road-night");
        Assert.Equal(MoonfallSlotState.Idle, loader.State(MoonfallArtSlot.Scene1x));
        var late = host.Land("moon-road-night.png", MoonfallArtFiles.SceneSize(false));
        Assert.True(late.Disposed);
        loader.Frame(true, "moon-road-night");
        Assert.Same(two, loader.Ground(true, out _));
        Assert.Equal(MoonfallSlotState.Idle, loader.State(MoonfallArtSlot.Scene1x));
    }

    [Fact]
    public void A_wrongly_sized_picture_is_disposed_logged_once_and_not_asked_for_again_while_open()
    {
        var (loader, host) = Opened(false, null);
        var wrong = host.Land("atlas.png", (OneX.Item1 + 1, OneX.Item2));
        loader.Frame(false, null);
        Assert.True(wrong.Disposed);
        Assert.Equal(MoonfallSlotState.Failed, loader.State(MoonfallArtSlot.Sheet1x));
        Assert.Null(loader.Sheet(false, out _));
        loader.Frame(false, null);
        Assert.False(host.Waiting("atlas.png"));
        Assert.Single(host.Warnings, w => w.Contains("atlas.png", StringComparison.Ordinal));
    }

    [Fact]
    public void A_refused_manifest_is_read_again_when_the_window_opens_again()
    {
        var host = new Host();
        var loader = new MoonfallArtLoader<Texture>(host, Folder);
        loader.Frame(false, null);
        host.Manifest!.SetResult(new MoonfallAtlasLoad(null, ["sprite ball is missing"]));
        loader.Frame(false, null);
        loader.Frame(false, null);
        Assert.Null(loader.Atlas);
        Assert.Equal(1, host.ManifestReads);
        Assert.Empty(host.Rents);

        // The player fixes the file and opens the window again.
        loader.Release();
        loader.Frame(false, null);
        Assert.Equal(2, host.ManifestReads);
        host.Manifest!.SetResult(new MoonfallAtlasLoad(Atlas, []));
        loader.Frame(false, null);
        Assert.Same(Atlas, loader.Atlas);
        Assert.True(host.Waiting("atlas.png"));
    }

    [Fact]
    public void A_scene_that_is_not_a_scenes_name_is_no_scene()
    {
        var (loader, host) = Opened(false, "../../evil");
        Assert.DoesNotContain(host.Rents, r => r.Path.Contains("evil", StringComparison.Ordinal));
        Assert.True(host.Waiting("sky.png"));
        Assert.Equal(MoonfallSlotState.Idle, loader.State(MoonfallArtSlot.Scene1x));
    }

    [Fact]
    public void Unloading_disposes_everything_held_retired_or_still_loading()
    {
        var (loader, host) = Opened(true, "moon-road-night");
        var sheet = host.Land("atlas.png", OneX);
        loader.Frame(true, "moon-road-night");
        loader.Release();
        loader.Frame(true, "moon-road-night");
        var again = host.Land("atlas.png", OneX);
        loader.Frame(true, "moon-road-night");

        loader.Dispose();
        Assert.True(sheet.Disposed);
        Assert.True(again.Disposed);
        foreach (var (_, load) in host.Rents.Where(r => !r.Load.Task.IsCompleted).ToList())
        {
            load.SetResult(new Texture("late.png", 1, 1));
        }

        Assert.All(host.Rents, r => Assert.True(r.Load.Task.Result.Disposed, $"{r.Path} kept"));
    }
}
