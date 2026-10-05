using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// What <see cref="MoonfallGameArt{T}"/> needs from the plugin: the game's files through Dalamud, Moonfall's own
/// pictures, textures made from pixels, the frame counter and the log. The plugin's <c>MoonfallGameArtTextures</c> is the
/// real one; the tests and the offline renderer bring their own.
/// </summary>
/// <typeparam name="T">A texture.</typeparam>
public interface IMoonfallGameArtHost<T>
    where T : class
{
    /// <summary>The frame now.</summary>
    int Frame { get; }

    /// <summary>Reads and decodes a texture from the player's install, off the framework thread; null when it is missing.</summary>
    Task<MoonfallImage?> ReadGameTexture(string path);

    /// <summary>Reads one of Moonfall's shipped scene pictures (<c>assets/moonfall/scenes/&lt;name&gt;.png</c>) off the framework thread; null when missing.</summary>
    Task<MoonfallImage?> ReadPicture(string name);

    /// <summary>Makes a texture from RGBA pixels, off the framework thread.</summary>
    Task<T> Upload(MoonfallRgba pixels, string name);

    /// <summary>Disposes a texture now (never one a frame could still draw).</summary>
    void Dispose(T texture);

    /// <summary>Logs a missing or broken texture, once each.</summary>
    void Warn(string message);
}

/// <summary>Where a level's built scene is.</summary>
public enum MoonfallSceneState : byte
{
    /// <summary>The level names no recipe: the board shows its shipped picture or the night sky, as before.</summary>
    NoRecipe,

    /// <summary>Reading, grading and dressing off the framework thread; the night sky stands in.</summary>
    Building,

    /// <summary>Ready to draw.</summary>
    Ready,

    /// <summary>The build failed (logged): the board shows the recipe's fallback picture, or the night sky.</summary>
    Failed,
}

/// <summary>A built scene's textures on the GPU.</summary>
/// <typeparam name="T">A texture.</typeparam>
public sealed class MoonfallSceneTextures<T>
    where T : class
{
    public required MoonfallSceneLayers Layers { get; init; }

    public required T Base { get; init; }

    public required T Backdrop { get; init; }

    public required T SkyMask { get; init; }

    public T? BeamsA { get; init; }

    public T? BeamsB { get; init; }

    public T? MoonFront { get; init; }

    public T? Enamel { get; init; }

    public IReadOnlyList<T> Mist { get; init; } = [];

    internal IEnumerable<T> All()
    {
        yield return Base;
        yield return Backdrop;
        yield return SkyMask;
        foreach (var t in new[] { BeamsA, BeamsB, MoonFront, Enamel })
        {
            if (t is not null)
            {
                yield return t;
            }
        }

        foreach (var m in Mist)
        {
            yield return m;
        }
    }
}

/// <summary>What the in-play art holds on the GPU (RGBA bytes): the UI chrome, the companions' cards, the scene and the peg marks.</summary>
public readonly record struct MoonfallGameArtBytes(long Chrome, long Cards, long Scene, long Marks)
{
    public long Total => Chrome + Cards + Scene + Marks;
}

/// <summary>
/// The in-play art read from the player's install at runtime (spec-rich2.md §6) over its lifetime, as pure logic behind
/// <see cref="IMoonfallGameArtHost{T}"/> so the tests can drive it: the chrome sheet (nine UI textures, graded once),
/// the current companion's card, the level's scene (its recipe built at the board's tier) and the peg marks. Everything
/// loads only while the window is open and goes back when it closes, as the atlas does (<see cref="MoonfallArtLoader{T}"/>);
/// reading, grading and uploading all run off the framework thread, and nothing here blocks a frame. The graded pixels
/// are cached for the session in memory (never on disk), so reopening the window or coming back to a level only uploads.
/// A texture that is missing or changed falls back (logged once): the chrome to the interim frame, a card to the card
/// back or a plain ring, a scene to its fallback picture or the night sky. Framework thread only.
/// </summary>
/// <typeparam name="T">A texture.</typeparam>
public sealed class MoonfallGameArt<T> : IDisposable
    where T : class
{
    public const int RetireFrames = 3;

    /// <summary>Built scenes kept in memory for the session (graded pixels, not textures).</summary>
    public const int SceneCache = 2;

    private readonly IMoonfallGameArtHost<T> host;
    private readonly IReadOnlyDictionary<string, MoonfallSceneRecipe> recipes;
    private readonly HashSet<string> warned = new(StringComparer.Ordinal);
    private readonly List<T> released = [];
    private readonly List<(T Texture, int Frame)> retired = [];
    private readonly List<Task<T>> abandoned = [];

    // ---- The session's CPU caches ----
    private Task<MoonfallChromeSheet>? chromeBuild;
    private readonly Dictionary<MoonfallPower, Task<MoonfallRgba?>> cardBuilds = [];
    private readonly List<(string Key, MoonfallSceneLayers Layers)> sceneCache = [];
    private MoonfallRgba? marksPixels;

    // ---- On the GPU while the window is open ----
    private Task<T>? chromeUpload;
    private T? chromeTexture;
    private readonly Dictionary<MoonfallPower, (Task<T>? Upload, T? Texture)> cards = [];
    private string? sceneKey;
    private Task<MoonfallSceneLayers?>? sceneBuild;
    private string? sceneBuildKey;
    private List<(string Name, Task<T> Upload)>? sceneUploads;
    private MoonfallSceneLayers? sceneUploading;
    private MoonfallSceneTextures<T>? scene;
    private MoonfallSceneState sceneState = MoonfallSceneState.NoRecipe;
    private Task<T>? marksUpload;
    private T? marksTexture;
    private bool open;

    /// <param name="host">The game's files, textures, frame counter and log.</param>
    /// <param name="recipes">The shipped scene recipes, by name (<see cref="MoonfallSceneRecipeLoader.LoadBuiltIn"/>).</param>
    public MoonfallGameArt(IMoonfallGameArtHost<T> host, IReadOnlyDictionary<string, MoonfallSceneRecipe> recipes)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
    }

    /// <summary>Builds every scene with the fuller-board checks (tests, Debug builds): <see cref="MoonfallSceneLayers.Report"/>.</summary>
    public bool CheckScenes { get; init; }

    /// <summary>The chrome sheet's layout once built (its texture is <see cref="ChromeTexture"/>); null until then.</summary>
    public MoonfallChromeSheet? Chrome => chromeBuild is { IsCompletedSuccessfully: true } t ? t.Result : null;

    /// <summary>The chrome sheet's texture, or null while it loads or when the UI textures are missing.</summary>
    public T? ChromeTexture => chromeTexture;

    /// <summary>The level's scene on the GPU, or null (see <see cref="SceneState"/>).</summary>
    public MoonfallSceneTextures<T>? Scene => scene;

    /// <summary>Where the level's scene is.</summary>
    public MoonfallSceneState SceneState => sceneState;

    /// <summary>The peg marks' sheet (<see cref="MoonfallPegMarks.Sheet"/>), or null until asked for and uploaded.</summary>
    public T? Marks => marksTexture;

    /// <summary>The recipe <paramref name="level"/> names, or null.</summary>
    public MoonfallSceneRecipe? RecipeFor(MoonfallLevel level) =>
        level?.Scene is { } name && recipes.TryGetValue(name, out var recipe) ? recipe : null;

    /// <summary>A companion's card (light-graded) once uploaded; asks for it the first time.</summary>
    public T? Card(MoonfallPower power)
    {
        if (!open || MoonfallCompanions.For(power) is not { } companion)
        {
            return null;
        }

        if (cards.TryGetValue(power, out var held))
        {
            return held.Texture;
        }

        if (!cardBuilds.TryGetValue(power, out var build))
        {
            build = cardBuilds[power] = Task.Run(async () =>
            {
                var card = await host.ReadGameTexture(companion.CardPath).ConfigureAwait(false);
                if (card is null || card.Width != MoonfallCompanions.CardWidth || card.Height != MoonfallCompanions.CardHeight || card.A is null)
                {
                    return null;
                }

                return MoonfallGrade.LightGrade(card).ToRgba() is { } bytes ? new MoonfallRgba(card.Width, card.Height, bytes, new Vector4(0, 0, card.Width, card.Height)) : null;
            });
        }

        cards[power] = (null, null);
        return null;
    }

    /// <summary>
    /// Once a frame while the board draws: lands what finished and starts what the frame needs. <paramref name="level"/>'s
    /// recipe is built at 2 pixels a unit when <paramref name="twoX"/>, else 1; the chrome sheet, the cards asked for and
    /// (with <paramref name="pegMarks"/>) the marks' sheet are loaded.
    /// </summary>
    public void Frame(MoonfallLevel level, bool twoX, bool pegMarks)
    {
        ArgumentNullException.ThrowIfNull(level);
        open = true;
        Retire();
        Chromes();
        Cards();
        Scenes(level, twoX);
        if (pegMarks)
        {
            marksPixels ??= MoonfallPegMarks.Sheet();
            if (marksTexture is null && marksUpload is null)
            {
                marksUpload = Upload(marksPixels, "Moonfall peg marks");
            }
        }

        if (marksUpload is { IsCompleted: true } mu)
        {
            marksUpload = null;
            marksTexture = Landed(mu, "peg marks");
        }
    }

    private void Chromes()
    {
        chromeBuild ??= Task.Run(async () =>
        {
            var textures = new Dictionary<string, MoonfallImage>(StringComparer.Ordinal);
            foreach (var (path, _, _) in MoonfallChromeArt.Textures)
            {
                if (await host.ReadGameTexture(path).ConfigureAwait(false) is { } image)
                {
                    textures[path] = image;
                }
            }

            return MoonfallChromeArt.Build(textures);
        });
        if (!chromeBuild.IsCompleted)
        {
            return;
        }

        if (!chromeBuild.IsCompletedSuccessfully)
        {
            Warn("chrome", $"Moonfall's frame could not be built from the game's UI art ({Reason(chromeBuild)}); the board keeps its interim frame");
            return;
        }

        var sheet = chromeBuild.Result;
        foreach (var missing in sheet.Missing)
        {
            Warn(missing, $"Moonfall: the game's UI texture {missing} is missing or changed; its parts of the frame fall back");
        }

        if (chromeTexture is null && chromeUpload is null && sheet.Missing.Count < MoonfallChromeArt.Textures.Count)
        {
            chromeUpload = Upload(sheet.Sheet, "Moonfall chrome");
        }

        if (chromeUpload is { IsCompleted: true } up)
        {
            chromeUpload = null;
            chromeTexture = Landed(up, "chrome");
        }
    }

    private void Cards()
    {
        foreach (var power in cards.Keys.ToArray())
        {
            var (upload, texture) = cards[power];
            if (texture is not null)
            {
                continue;
            }

            if (upload is null)
            {
                if (cardBuilds.TryGetValue(power, out var build) && build.IsCompleted)
                {
                    if (build.IsCompletedSuccessfully && build.Result is { } pixels)
                    {
                        cards[power] = (Upload(pixels, $"Moonfall card {power}"), null);
                    }
                    else
                    {
                        Warn("card" + power, $"Moonfall: {MoonfallCompanions.For(power)?.CardPath} is missing or changed; the companion shows as a plain ring");
                    }
                }

                continue;
            }

            if (upload.IsCompleted)
            {
                cards[power] = (null, Landed(upload, "card"));
            }
        }
    }

    private void Scenes(MoonfallLevel level, bool twoX)
    {
        var recipe = RecipeFor(level);
        var key = recipe is null ? null : $"{recipe.Name}@{(twoX ? 2 : 1)}";
        if (!string.Equals(key, sceneKey, StringComparison.Ordinal))
        {
            // Another scene or tier: the old one is drawn until the new one is ready, unless it is another scene altogether.
            var sameScene = scene is not null && key is not null && sceneKey is not null && SameRecipe(sceneKey, key);
            if (!sameScene)
            {
                LetSceneGo();
            }

            AbandonSceneUploads();
            sceneKey = key;
            sceneState = recipe is null ? MoonfallSceneState.NoRecipe : MoonfallSceneState.Building;
        }

        if (recipe is null || key is null)
        {
            return;
        }

        if (scene is not null && sceneUploading is null && sceneBuild is null && sceneState == MoonfallSceneState.Ready && string.Equals(scene.Layers.Name + "@" + (int)scene.Layers.Scale, key, StringComparison.Ordinal))
        {
            return;
        }

        if (sceneState == MoonfallSceneState.Failed)
        {
            return;
        }

        // Built this session already: only upload.
        if (sceneBuild is null && sceneUploading is null && sceneCache.FirstOrDefault(c => c.Key == key) is { Layers: { } cached })
        {
            StartUploads(cached, recipe);
            return;
        }

        if (sceneBuild is null && sceneUploading is null)
        {
            sceneBuildKey = key;
            var tier = twoX ? 2 : 1;
            var check = CheckScenes;
            sceneBuild = Task.Run(async () =>
            {
                MoonfallImage? painting;
                var fallback = false;
                if (recipe.Source.Kind == MoonfallSourceKind.Game)
                {
                    painting = await host.ReadGameTexture(recipe.Source.Path).ConfigureAwait(false);
                }
                else
                {
                    painting = await host.ReadPicture(recipe.Source.Path).ConfigureAwait(false);
                }

                if (painting is null && recipe.Fallback is { } name)
                {
                    painting = await host.ReadPicture(name).ConfigureAwait(false);
                    fallback = true;
                }

                return painting is null ? null : MoonfallSceneBuilder.Build(recipe, level, painting, tier, fallback, check);
            });
        }

        if (sceneBuild is { IsCompleted: true } done && string.Equals(sceneBuildKey, key, StringComparison.Ordinal))
        {
            sceneBuild = null;
            if (!done.IsCompletedSuccessfully || done.Result is null)
            {
                sceneState = MoonfallSceneState.Failed;
                Warn("scene:" + recipe.Name, done.IsCompletedSuccessfully
                    ? $"Moonfall: the painting for scene {recipe.Name} ({recipe.Source.Path}) is missing; the board shows the night sky"
                    : $"Moonfall: scene {recipe.Name} could not be built ({Reason(done)}); the board shows the night sky");
                return;
            }

            var layers = done.Result;
            sceneCache.RemoveAll(c => c.Key == key);
            sceneCache.Insert(0, (key, layers));
            if (sceneCache.Count > SceneCache)
            {
                sceneCache.RemoveAt(sceneCache.Count - 1);
            }

            StartUploads(layers, recipe);
        }

        if (sceneUploads is not null && sceneUploads.TrueForAll(static u => u.Upload.IsCompleted))
        {
            var landed = sceneUploads.ToDictionary(static u => u.Name, u => Landed(u.Upload, "scene"), StringComparer.Ordinal);
            var layers = sceneUploading!;
            sceneUploads = null;
            sceneUploading = null;
            if (landed["base"] is not { } baseTex || landed["backdrop"] is not { } backdrop || landed["sky"] is not { } sky)
            {
                foreach (var t in landed.Values)
                {
                    if (t is not null)
                    {
                        released.Add(t);
                    }
                }

                sceneState = MoonfallSceneState.Failed;
                return;
            }

            LetSceneGo();
            scene = new MoonfallSceneTextures<T>
            {
                Layers = layers,
                Base = baseTex,
                Backdrop = backdrop,
                SkyMask = sky,
                BeamsA = landed.GetValueOrDefault("beamsA"),
                BeamsB = landed.GetValueOrDefault("beamsB"),
                MoonFront = landed.GetValueOrDefault("moon"),
                Enamel = landed.GetValueOrDefault("enamel"),
                Mist = landed.Where(static p => p.Key.StartsWith("mist", StringComparison.Ordinal) && p.Value is not null).OrderBy(static p => p.Key, StringComparer.Ordinal).Select(static p => p.Value!).ToList(),
            };
            sceneState = MoonfallSceneState.Ready;
        }
    }

    private static bool SameRecipe(string a, string b) => string.Equals(a[..a.LastIndexOf('@')], b[..b.LastIndexOf('@')], StringComparison.Ordinal);

    private void StartUploads(MoonfallSceneLayers layers, MoonfallSceneRecipe recipe)
    {
        sceneUploading = layers;
        sceneUploads =
        [
            ("base", Upload(layers.Base, $"Moonfall scene {recipe.Name}")),
            ("backdrop", Upload(layers.Backdrop, "Moonfall backdrop")),
            ("sky", Upload(layers.SkyMask, "Moonfall sky")),
        ];
        if (layers.BeamsA is { } a)
        {
            sceneUploads.Add(("beamsA", Upload(a, "Moonfall beams")));
        }

        if (layers.BeamsB is { } b)
        {
            sceneUploads.Add(("beamsB", Upload(b, "Moonfall beams")));
        }

        if (layers.MoonFront is { } m)
        {
            sceneUploads.Add(("moon", Upload(m, "Moonfall moon")));
        }

        for (var i = 0; i < layers.Mist.Count; i++)
        {
            sceneUploads.Add(($"mist{i}", Upload(layers.Mist[i].Tile, "Moonfall mist")));
        }

        if (Chrome?.Grain is { } grain)
        {
            sceneUploads.Add(("enamel", Upload(MoonfallChromeArt.EnamelTile(grain, layers.Chrome), "Moonfall enamel")));
        }
    }

    private Task<T> Upload(MoonfallRgba pixels, string name)
    {
        try
        {
            return host.Upload(pixels, name);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Task.FromException<T>(ex);
        }
    }

    private T? Landed(Task<T> upload, string what)
    {
        if (upload.IsCompletedSuccessfully)
        {
            return upload.Result;
        }

        Warn("upload:" + what, $"Moonfall: the {what} texture could not be made ({Reason(upload)})");
        return null;
    }

    private void LetSceneGo()
    {
        if (scene is not null)
        {
            released.AddRange(scene.All());
            scene = null;
        }
    }

    private void AbandonSceneUploads()
    {
        if (sceneUploads is not null)
        {
            abandoned.AddRange(sceneUploads.Select(static u => u.Upload));
            sceneUploads = null;
            sceneUploading = null;
        }

        // A build still running for another scene finishes into the cache only if asked again; its result is dropped here.
        if (sceneBuild is not null)
        {
            sceneBuild = null;
            sceneBuildKey = null;
        }
    }

    /// <summary>The window has closed: every texture is let go (disposed a few frames later); the graded pixels stay cached for the session.</summary>
    public void Release()
    {
        if (!open)
        {
            return;
        }

        open = false;
        LetSceneGo();
        AbandonSceneUploads();
        sceneKey = null;
        sceneState = MoonfallSceneState.NoRecipe;
        if (chromeTexture is not null)
        {
            released.Add(chromeTexture);
            chromeTexture = null;
        }

        if (chromeUpload is not null)
        {
            abandoned.Add(chromeUpload);
            chromeUpload = null;
        }

        foreach (var (upload, texture) in cards.Values)
        {
            if (texture is not null)
            {
                released.Add(texture);
            }
            else if (upload is not null)
            {
                abandoned.Add(upload);
            }
        }

        cards.Clear();
        if (marksTexture is not null)
        {
            released.Add(marksTexture);
            marksTexture = null;
        }

        if (marksUpload is not null)
        {
            abandoned.Add(marksUpload);
            marksUpload = null;
        }

        // A chrome build that failed is tried again when the window opens again (a file fixed, a patch installed).
        if (chromeBuild is { IsCompleted: true, IsCompletedSuccessfully: false })
        {
            chromeBuild = null;
        }

        Retire();
    }

    /// <summary>Once a frame, open or not: disposes what was let go more than <see cref="RetireFrames"/> frames ago, and uploads abandoned once they land.</summary>
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

        for (var i = abandoned.Count - 1; i >= 0; i--)
        {
            if (abandoned[i].IsCompleted)
            {
                if (abandoned[i].IsCompletedSuccessfully)
                {
                    DisposeTexture(abandoned[i].Result);
                }

                abandoned.RemoveAt(i);
            }
        }
    }

    /// <summary>Textures let go and not yet disposed, and uploads abandoned and not yet landed.</summary>
    public int Retiring => retired.Count + released.Count + abandoned.Count;

    /// <summary>What is on the GPU now.</summary>
    public MoonfallGameArtBytes HeldBytes()
    {
        long cardBytes = cards.Values.Count(static c => c.Texture is not null) * (long)MoonfallCompanions.CardWidth * MoonfallCompanions.CardHeight * 4;
        long sceneBytes = scene is null ? 0 : scene.Layers.Bytes + (scene.Enamel is not null && Chrome?.Grain is { } g ? (long)(g.Width / 2) * (g.Height / 2) * 4 : 0);
        return new MoonfallGameArtBytes(chromeTexture is not null && Chrome is { } c ? c.Sheet.Bytes : 0, cardBytes, sceneBytes, marksTexture is not null && marksPixels is { } m ? m.Bytes : 0);
    }

    /// <summary>On unload: everything held, retired or still loading is disposed now.</summary>
    public void Dispose()
    {
        Release();
        foreach (var t in released)
        {
            DisposeTexture(t);
        }

        released.Clear();
        foreach (var (t, _) in retired)
        {
            DisposeTexture(t);
        }

        retired.Clear();
        foreach (var upload in abandoned)
        {
            _ = upload.ContinueWith(u => DisposeTexture(u.Result), CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
        }

        abandoned.Clear();
    }

    private void Retire()
    {
        if (released.Count == 0)
        {
            return;
        }

        var frame = host.Frame;
        foreach (var t in released)
        {
            retired.Add((t, frame));
        }

        released.Clear();
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

    private void Warn(string key, string message)
    {
        if (warned.Add(key))
        {
            host.Warn(message);
        }
    }

    private static string Reason(Task task) => task.Exception?.GetBaseException().Message ?? "cancelled";
}
