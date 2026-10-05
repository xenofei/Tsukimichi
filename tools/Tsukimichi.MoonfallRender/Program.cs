using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Bindings.ImGui;
using Lumina;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;
using Tsukimichi.Localization;
using Tsukimichi.MoonfallRender;
using Tsukimichi.Ui;
using LuminaGameData = Lumina.GameData;

// Moonfall's offline renderer (see the project file):
//   Tsukimichi.MoonfallRender <out.png> [--level base-01|base-p1] [--size 1280x800] [--moment hud|power|fever|tally]
//                              [--marks] [--reduce-motion] [--no-game-art] [--seconds N]
// The board is the plugin's own window, drawn by its own code under a headless ImGui, rasterized to the PNG.
var dalamud = Environment.GetEnvironmentVariable("DALAMUD_HOME") is { Length: > 0 } home ? home
    : typeof(Raster).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(static a => a.Key == "DalamudLibPath")?.Value;
if (dalamud is null || !File.Exists(Path.Combine(dalamud, "Dalamud.dll")))
{
    Console.Error.WriteLine($"Dalamud not found at \"{dalamud}\" (set DALAMUD_HOME)");
    return 2;
}

AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    if (!string.IsNullOrEmpty(name.CultureName))
    {
        return null;
    }

    var path = Path.Combine(dalamud, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};

return Render.Run(args);

internal static class Render
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: Tsukimichi.MoonfallRender <out.png> [--level base-01|base-p1] [--size 1280x800] [--moment hud|power|fever|tally] [--marks] [--reduce-motion] [--no-game-art]");
            return 2;
        }

        var outPath = args[0];
        var levelName = Arg(args, "--level") ?? "base-01";
        var size = (Arg(args, "--size") ?? "1280x800").Split('x');
        var (w, h) = (int.Parse(size[0], CultureInfo.InvariantCulture), int.Parse(size[1], CultureInfo.InvariantCulture));
        var moment = Arg(args, "--moment") ?? "hud";
        var repo = FindRepo();
        var gamePath = Environment.GetEnvironmentVariable("TSUKIMICHI_GAME_PATH") ?? @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";
        using var game = new LuminaGameData(gamePath, new LuminaOptions { PanicOnSheetChecksumMismatch = false });

        Loc.SetLanguage(Loc.English);
        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(w, h);
        io.DeltaTime = 1f / 60f;
        var raster = new Raster(w, h);
        var store = new TextureStore(raster);
        var fonts = new GameFonts();
        unsafe
        {
            // Nothing is written next to the renderer (no imgui.ini).
            io.IniFilename = null;
            // The window's own text: a sans close to Dalamud's default font.
            var segoe = @"C:\Windows\Fonts\segoeui.ttf";
            if (File.Exists(segoe))
            {
                io.Fonts.AddFontFromFileTTF(segoe, 17f, default, null);
            }
            else
            {
                io.Fonts.AddFontDefault();
            }

            fonts.Add(io.Fonts, game);
            io.Fonts.Build();
            // The atlas may span several pages (Dalamud's ImGui): each is filled, then becomes a texture of its own.
            var pages = new List<(nint, int, int)>();
            for (var i = 0; i < io.Fonts.Textures.Size; i++)
            {
                byte* pixels;
                int aw, ah, bpp;
                io.Fonts.GetTexDataAsRGBA32(i, &pixels, &aw, &ah, &bpp);
                pages.Add(((nint)pixels, aw, ah));
            }

            fonts.Fill(io.Fonts, game, pages);
            for (var i = 0; i < pages.Count; i++)
            {
                var (pixels, aw, ah) = pages[i];
                var copy = new byte[aw * ah * 4];
                new ReadOnlySpan<byte>((byte*)pixels, copy.Length).CopyTo(copy);
                raster.Textures[(ulong)(1 + i)] = new Texture(aw, ah, copy);
                io.Fonts.SetTexID(i, new ImTextureID((ulong)(1 + i)));
            }
        }

        if (args.Contains("--reduce-motion"))
        {
            // UiMetrics reads Reduce motion from the plugin's settings each frame in the game; offline it is set once.
            typeof(UiMetrics).GetProperty(nameof(UiMetrics.ReduceMotion))!.SetValue(null, true);
        }

        var (campaigns, index) = Campaign(repo, levelName);
        var progress = new MoonfallProgress { BaseCleared = index };
        // The tally's callout: an earlier best to beat and an Ace within reach of the staged win, so ACED and NEW BEST show.
        progress.Levels[campaigns.Base.Levels[index].Id] = new MoonfallLevelRecord { Best = 60_000 };
        var artHost = new ArtHost(store);
        var gameHost = new GameHost(store, args.Contains("--no-game-art") ? null : game, Path.Combine(repo, "Tsukimichi", "assets", "moonfall", "scenes"));
        var options = new Options { PegMarks = args.Contains("--marks"), PegMarksHintSeen = !args.Contains("--hint") };
        var temp = Path.Combine(Path.GetTempPath(), "moonfall-render-progress.json");
        var window = new MoonfallWindow(campaigns, progress, temp, static () => MoonfallPauseReason.None, null, artHost,
            Path.Combine(repo, "Tsukimichi", "assets", "moonfall"), gameHost, fonts, options)
        { SeedForRender = 1 };
        if (moment == "tally")
        {
            window.AceFor = static _ => 100_000;
        }

        void Frame(float dt, bool draw = false)
        {
            FrameInner(dt, draw);
        }

        void FrameInner(float dt, bool draw)
        {
            artHost.Frame++;
            gameHost.Frame++;
            io.DeltaTime = dt;
            io.MousePos = new Vector2(-1000, -1000);
            ImGui.NewFrame();
            window.PreDraw();
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(w, h));
            ImGui.SetNextWindowFocus();
            if (ImGui.Begin(window.WindowName, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse))
            {
                window.Draw();
            }

            ImGui.End();
            window.PostDraw();
            ImGui.Render();
            if (draw)
            {
                raster.Clear(new Vector3(0.02f, 0.02f, 0.05f));
                raster.Draw(ImGui.GetDrawData());
            }
        }

        // Let the art load (manifest, sheets, the game's UI art, the scene's build and its uploads): frames until it has.
        for (var i = 0; i < 12 || (!window.ArtSettledForRender && i < 3000); i++)
        {
            Frame(1f / 60f);
            if (!window.ArtSettledForRender)
            {
                Thread.Sleep(10);
            }
        }

        var g = window.GameForRender ?? throw new InvalidOperationException("no game");
        Stage(moment, window, g, Frame);
        var seconds = Arg(args, "--seconds") is { } s ? double.Parse(s, CultureInfo.InvariantCulture) : 0;
        for (var t = 0.0; t < seconds; t += 1 / 60.0)
        {
            Frame(1f / 60f);
        }

        if (args.Contains("--alloc"))
        {
            // The window's own steady-state allocation: the board's drawing (not ImGui's) over 10 s of frames, after 2 s
            // to settle; the art is loaded and nothing new is asked for.
            for (var i = 0; i < 120; i++)
            {
                Frame(1f / 60f);
            }

            using var listener = new AllocListener();
            listener.On = true;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 600; i++)
            {
                Frame(1f / 60f);
            }

            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            listener.On = false;
            Console.WriteLine($"alloc: {bytes} bytes over 600 frames ({levelName} {moment} {w} x {h})");
            foreach (var (type, n) in listener.Types.OrderByDescending(static kv => kv.Value))
            {
                Console.WriteLine($"  ~{n * 100} KB {type}");
            }

            // The text calls the chrome makes, alone: do the bindings allocate for a span?
            ImGui.NewFrame();
            var probe = ImGui.GetForegroundDrawList();
            var font = ImGui.GetFont();
            ReadOnlySpan<char> text = "THE AIRSHIP ROAD";
            var probeBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                _ = ImGui.CalcTextSizeA(font, 20f, float.MaxValue, 0f, text, out _);
                probe.AddText(font, 20f, new Vector2(10, 10), uint.MaxValue, text);
            }

            Console.WriteLine($"alloc probe: {GC.GetAllocatedBytesForCurrentThread() - probeBefore} bytes for 1000 measured and drawn spans");
            var labelBefore = GC.GetAllocatedBytesForCurrentThread();
            var pauseText = "Pause";
            for (var i = 0; i < 1000; i++)
            {
                _ = ImGui.CalcTextSize(pauseText + "##moonfallPause");
            }

            Console.WriteLine($"alloc probe: {GC.GetAllocatedBytesForCurrentThread() - labelBefore} bytes for 1000 concatenated labels");
            ImGui.EndFrame();
        }

        Frame(1f / 60f, draw: true);
        File.WriteAllBytes(outPath, MoonfallPng.Encode(raster.ToRgba(), w, h));
        Console.WriteLine($"{outPath}: {levelName} {moment} {w} x {h}");
        return 0;
    }

    /// <summary>Plays the board to the moment asked for (the engine's own play, with its test hooks to stage a power or Fever).</summary>
    private static void Stage(string moment, MoonfallWindow window, MoonfallGame g, Action<float, bool> frame)
    {
        void Run(double seconds)
        {
            for (var t = 0.0; t < seconds; t += 1 / 60.0)
            {
                frame(1f / 60f, false);
            }
        }

        void UntilAiming()
        {
            for (var k = 0; k < 60 * 30 && g.Phase != MoonfallPhase.Aiming; k++)
            {
                frame(1f / 60f, false);
            }
        }

        // Two shots in, as a game under way: a score, a few pegs gone, the multiplier still low.
        foreach (var aim in new[] { -0.32, 0.41 })
        {
            g.Shoot(aim);
            UntilAiming();
        }

        switch (moment)
        {
            case "hud":
                window.AimForRender = -0.35;
                Run(0.2);
                break;

            case "power":
            {
                window.AimForRender = -0.35;
                var green = Enumerable.Range(0, g.PegCount).FirstOrDefault(i => g.Peg(i).Colour == PegColour.Green && !g.Peg(i).Cleared);
                g.TriggerForTest(g.Power, green);
                window.RibbonForRender("LONG SHOT", "+25,000");
                Run(0.7);
                break;
            }

            case "fever":
            case "tally":
            {
                // Every orange but the lowest cleared, then the ball dropped onto the last.
                var oranges = Enumerable.Range(0, g.PegCount).Where(i => g.Peg(i).Colour == PegColour.Orange && !g.Peg(i).Cleared).OrderBy(i => g.Peg(i).Y).ToList();
                var last = oranges[^1];
                foreach (var i in oranges[..^1])
                {
                    g.LightForTest(i);
                }

                g.PlaceBall(MoonfallRules.LeftWall + 8, 590, 0, 200);
                UntilAiming();
                var peg = g.Peg(last);
                g.PlaceBall(peg.X + 3, peg.Y - 40, 0, 60);
                for (var k = 0; k < 60 * 10 && !g.Fever; k++)
                {
                    frame(1f / 60f, false);
                }

                if (moment == "fever")
                {
                    Run(1.8);
                }
                else
                {
                    for (var k = 0; k < 60 * 120 && (g.Phase != MoonfallPhase.Won || g.ShownScore != g.Tally?.Total); k++)
                    {
                        frame(1f / 60f, false);
                    }

                    Run(0.5);
                }

                break;
            }
        }
    }

    private static (MoonfallCampaigns Campaigns, int Index) Campaign(string repo, string name)
    {
        var builtIn = MoonfallCampaigns.LoadBuiltIn();
        var index = builtIn.Base.Levels.ToList().FindIndex(l => l.Id == name);
        if (index >= 0)
        {
            return (builtIn, index);
        }

        // A pilot (docs/design/v9/rich/levels): on The Moon Road's stage 3, as the mocks show it, with its recipe.
        var file = Path.Combine(repo, "docs", "design", "v9", "rich", "levels", name + ".json");
        var load = MoonfallLevelLoader.Parse(File.ReadAllText(file));
        var pilot = load.Level ?? throw new InvalidOperationException(string.Join("; ", load.Errors));
        var scene = name switch { "base-p1" => "airship-road", "base-p2" => "holy-see", _ => null };
        pilot = pilot with { Scene = scene };
        var levels = new List<MoonfallLevel>();
        for (var i = 0; i < 12; i++)
        {
            levels.Add(builtIn.Base.Levels[i % builtIn.Base.Levels.Count]);
        }

        levels.Add(pilot);
        levels.Add(builtIn.Base.Levels[0]);
        return (new MoonfallCampaigns(new MoonfallCampaign(MoonfallCampaignKind.Base, levels), builtIn.Expansion, []), 12);
    }

    private static string? Arg(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static string FindRepo()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Tsukimichi.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("the repository was not found above the tool");
    }
}
