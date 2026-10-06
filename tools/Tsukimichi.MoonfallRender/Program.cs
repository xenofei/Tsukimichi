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
//   Tsukimichi.MoonfallRender <out.png> [--screen title|map|levels|characters|quickplay|challenges|duel|options|play|pause|tally|duelhud]
//                              [--level base-01|base-p1] [--size 1280x800] [--moment hud|power|fever|tally]
//                              [--marks] [--hint] [--reduce-motion] [--decoration full|simple|off] [--no-game-art] [--seconds N]
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

        // The screen to show (--screen): a menu, or the board ("play", the default) staged at --moment. With a screen,
        // the progress is the mocks' one state (r2state.py): The Moon Road, stage 3, with Cid; stages 1 and 2 won; the
        // twins face down (Alisaie not yet met). Without one, the board plays --level as before.
        var screen = Arg(args, "--screen") ?? "play";
        var (campaigns, progress, aces, story) = args.Contains("--legacy") || (screen == "play" && Arg(args, "--level") is not null)
            ? Legacy(repo, levelName, moment)
            : MockState(repo, screen, moment);
        var artHost = new ArtHost(store);
        var gameHost = new GameHost(store, args.Contains("--no-game-art") ? null : game, Path.Combine(repo, "Tsukimichi", "assets", "moonfall", "scenes"));
        var options = new Options { PegMarks = args.Contains("--marks"), PegMarksHintSeen = !args.Contains("--hint") };
        if (Arg(args, "--decoration") is { } decoration)
        {
            options.Decoration = decoration switch { "simple" => Tsukimichi.Core.Ui.Flair.Quiet, "off" => Tsukimichi.Core.Ui.Flair.Plain, _ => Tsukimichi.Core.Ui.Flair.Full };
        }

        // The spoiler shield for places (--story N: the story has reached expansion N, 0 A Realm Reborn … 5 Dawntrail).
        // The Far Shore is staged at Shadowbringers by default, so its Endwalker stages show veiled.
        var storyArg = Arg(args, "--story");
        var reach = storyArg is not null ? byte.Parse(storyArg, CultureInfo.InvariantCulture) : screen == "far" ? MoonfallPlaces.Shadowbringers : (byte?)null;
        var shield = reach is { } r ? StoryShield(r) : null;
        if (reach is not null)
        {
            // A story staged by expansion has met everyone the shield places in A Realm Reborn (the twins included).
            story = MoonfallStory.Everyone;
        }

        // --far-built: the Far Shore's 60 levels stand in (copies of base-01), so its road can be walked;
        // --far-won N: The Moon Road won and the Far Shore's first N levels won (the road's frontier, stepping over veils).
        if (args.Contains("--far-built"))
        {
            var shape = campaigns.Base.Levels[0];
            var far = Enumerable.Range(0, MoonfallStages.ExpansionLevels).Select(i => shape with { Id = MoonfallStages.LevelId(MoonfallCampaignKind.Expansion, i) }).ToList();
            campaigns = new MoonfallCampaigns(campaigns.Base, new MoonfallCampaign(MoonfallCampaignKind.Expansion, far), []);
        }

        if (Arg(args, "--far-won") is { } farWon)
        {
            progress.BaseCleared = MoonfallStages.BaseLevels;
            var won = int.Parse(farWon, CultureInfo.InvariantCulture);
            for (var i = 0; i < won; i++)
            {
                progress.Levels[MoonfallStages.LevelId(MoonfallCampaignKind.Expansion, i)] = new MoonfallLevelRecord { Cleared = true, Best = 120_000 };
            }

            progress.ExpansionCleared = won;
        }

        // --far-walk: The Moon Road won, then the Far Shore walked as Continue leads (stepping over veiled stages) until
        // nothing is left to play: the road waits at the first stage past the story.
        if (args.Contains("--far-walk"))
        {
            progress.BaseCleared = MoonfallStages.BaseLevels;
            var walker = new MoonfallModes(campaigns, progress, story ?? MoonfallStory.Everyone, []) { Shield = shield ?? MoonfallShield.Open };
            string? last = null;
            for (var guard = 0; guard < 200 && walker.Continue() is { } next; guard++)
            {
                last = MoonfallStages.LevelId(next.Campaign, next.Index);
                progress.RecordLevel(last, won: true, score: 120_000);
            }

            // --leave-one: the walk's last level is left unwon, so "--screen tally" plays and wins it there: the tally
            // with no Next because the road then waits past the story (its note), and Map opening on that stage.
            if (args.Contains("--leave-one") && last is not null && MoonfallStages.TryPlace(last, out var lastPlace))
            {
                progress.Levels.Remove(last);
                progress.ExpansionCleared = Math.Min(progress.ExpansionCleared, lastPlace.Index);
            }
        }
        var temp = Path.Combine(Path.GetTempPath(), "moonfall-render-progress.json");
        var window = new MoonfallWindow(campaigns, progress, temp, static () => MoonfallPauseReason.None, null, artHost,
            Path.Combine(repo, "Tsukimichi", "assets", "moonfall"), gameHost, fonts, options, story, null, aces, shield)
        { SeedForRender = 1 };

        // --text-check: every string drawn is heard, and none may hold a veiled stage's name or place.
        var drawn = new HashSet<string>(StringComparer.Ordinal);
        if (args.Contains("--text-check"))
        {
            MoonfallWindow.TextSinkForRender = text => drawn.Add(text.ToString());
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
        void Settle()
        {
            // The board's clock is held while the art loads, so a staged moment is the same however long that takes.
            window.HoldBoardForRender = true;
            using var release = new HoldRelease(window);
            for (var i = 0; i < 12 || (!window.ArtSettledForRender && i < 3000); i++)
            {
                Frame(1f / 60f);
                if (!window.ArtSettledForRender)
                {
                    Thread.Sleep(10);
                }
            }
        }

        Settle();
        var board = screen is "play" or "pause" or "tally" or "duelhud";
        if (!window.ShowForRender(screen is "pause" or "tally" ? "play" : screen))
        {
            Console.Error.WriteLine($"no screen \"{screen}\" (title, map, levels, characters, quickplay, challenges, duel, options, play, pause, tally, duelhud)");
            return 2;
        }

        // --stage N: the map's selected stage (from 1), so a veiled stage's panel can be shown.
        if (Arg(args, "--stage") is { } stageArg)
        {
            window.MapStageForRender = int.Parse(stageArg, CultureInfo.InvariantCulture) - 1;
        }

        Settle();
        if (board)
        {
            var g = window.GameForRender ?? throw new InvalidOperationException("no game");
            if (screen == "duelhud")
            {
                StageDuel(window, Frame);
            }
            else
            {
                Stage(screen == "tally" ? "tally" : moment, window, g, Frame);
            }

            if (screen == "pause")
            {
                window.PauseForRender.Pause(MoonfallPauseReason.Player);
                Frame(1f / 60f);
            }

            // The duel is caught mid-thought: settling would run the opponent's thought out (its art settled before it was staged).
            if (screen != "duelhud")
            {
                Settle();
            }
        }
        else
        {
            // The menus' ambient motion a few seconds in (it is still under Reduce motion).
            for (var i = 0; i < 90; i++)
            {
                Frame(1f / 60f);
            }
        }
        var seconds = Arg(args, "--seconds") is { } s ? double.Parse(s, CultureInfo.InvariantCulture) : 0;
        for (var t = 0.0; t < seconds; t += 1 / 60.0)
        {
            Frame(1f / 60f);
        }

        if (args.Contains("--text-check"))
        {
            // No string drawn on any frame may hold the name or the place of a stage the shield veils.
            var hidden = new List<string>();
            foreach (var stage in MoonfallStages.Of(MoonfallCampaignKind.Expansion))
            {
                var place = MoonfallPlaces.OfStage(stage);
                if (shield is not null && shield.Hides(place))
                {
                    hidden.Add(stage.Name);
                    hidden.Add(place.Zone!);
                }
            }

            var leaks = drawn.Where(t => hidden.Any(h => t.Contains(h, StringComparison.OrdinalIgnoreCase))).ToList();
            Console.WriteLine($"text-check: {drawn.Count} strings drawn, {hidden.Count / 2} stages veiled, {leaks.Count} leaks{(leaks.Count > 0 ? ": " + string.Join(" | ", leaks) : string.Empty)}");
            if (leaks.Count > 0)
            {
                return 3;
            }
        }

        if (args.Contains("--alloc"))
        {
            // The window's own steady-state allocation: the board's drawing (not ImGui's) over 10 s of frames, after 2 s
            // to settle; the art is loaded and nothing new is asked for.
            for (var i = 0; i < 120; i++)
            {
                Frame(1f / 60f);
            }

            var allocFrames = Arg(args, "--alloc-frames") is { } af ? int.Parse(af, CultureInfo.InvariantCulture) : 600;
            using var listener = new AllocListener();
            listener.On = true;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < allocFrames; i++)
            {
                Frame(1f / 60f);
            }

            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            listener.On = false;
            Console.WriteLine($"alloc: {bytes} bytes over {allocFrames} frames ({screen} {w} x {h})");
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

    /// <summary>A duel staged at the opponent's turn: the player's first shot taken, the opponent thinking.</summary>
    private static void StageDuel(MoonfallWindow window, Action<float, bool> frame)
    {
        var d = window.DuelForRender ?? throw new InvalidOperationException("no duel");
        window.AimForRender = 24.0;
        d.Shoot(24.0);
        for (var k = 0; k < 60 * 30 && (d.PlayersTurn || !d.Opponent.Thinking); k++)
        {
            frame(1f / 60f, false);
            if (d.Outcome != MoonfallDuelOutcome.Undecided)
            {
                break;
            }
        }

        // A few ticks into its thought.
        for (var k = 0; k < 40 && d.Opponent.Thinking; k++)
        {
            frame(1f / 60f, false);
        }
    }

    /// <summary>The board of the old renderer: --level (a shipped level, or a pilot on stage 3), its progress just short of it.</summary>
    /// <summary>
    /// A stand-in for the spoiler shield whose story has reached expansion <paramref name="reach"/>: it hides an area
    /// Moonfall tags with a later era and prints the shield's placeholder shape for it ("Endwalker area 2").
    /// </summary>
    /// <summary>Lets the board's clock run again when a settle ends.</summary>
    private readonly struct HoldRelease(MoonfallWindow window) : IDisposable
    {
        public void Dispose() => window.HoldBoardForRender = false;
    }

    private static MoonfallShield StoryShield(byte reach)
    {
        string[] expansions = ["A Realm Reborn", "Heavensward", "Stormblood", "Shadowbringers", "Endwalker", "Dawntrail"];
        var eras = new Dictionary<string, (byte Era, int Number)>(StringComparer.Ordinal);
        foreach (var place in MoonfallStages.Of(MoonfallCampaignKind.Expansion).Select(MoonfallPlaces.OfStage).Concat(MoonfallPlaces.Scenes.Values).Append(MoonfallPlaces.OfBackdrop(MoonfallBackdrop.Title)).Append(MoonfallPlaces.OfBackdrop(MoonfallBackdrop.TitleEarly)))
        {
            if (place.Zone is { } zone && !eras.ContainsKey(zone))
            {
                eras[zone] = (place.Era, eras.Values.Count(e => e.Era == place.Era) + 1);
            }
        }

        return new MoonfallShield(
            zone => eras.TryGetValue(zone, out var e) && e.Era > reach,
            zone => eras.TryGetValue(zone, out var e) ? $"{expansions[e.Era]} area {e.Number}" : zone);
    }

    private static (MoonfallCampaigns, MoonfallProgress, Func<string?, long?>?, MoonfallStory?) Legacy(string repo, string levelName, string moment)
    {
        var (campaigns, index) = Campaign(repo, levelName);
        var progress = new MoonfallProgress { BaseCleared = index };
        // The tally's callout: an earlier best to beat and an Ace within reach of the staged win, so ACED and NEW BEST show.
        progress.Levels[MoonfallStages.LevelId(MoonfallCampaignKind.Base, index)] = new MoonfallLevelRecord { Best = 60_000 };
        Func<string?, long?>? aces = moment == "tally" ? static _ => 100_000 : null;
        return (campaigns, progress, aces, null);
    }

    /// <summary>
    /// The mocks' one state (docs/design/v9/rich2/src/r2state.py): The Moon Road, stage 3, with Cid Garlond; stages 1 and 2
    /// won; 3-1 The Moonlit Post won (214,300), 3-2 The Holy See aced (251,880), 3-3 The Airship Road next (Ace 240,000);
    /// the twins face down (Alisaie not yet met). Stage 3's levels are the three approved pilots; the rest are the shipped
    /// levels standing in. The challenges' screen is staged with The Moon Road won, so its list is open.
    /// </summary>
    private static (MoonfallCampaigns, MoonfallProgress, Func<string?, long?>?, MoonfallStory?) MockState(string repo, string screen, string moment)
    {
        var builtIn = MoonfallCampaigns.LoadBuiltIn();
        MoonfallLevel Pilot(string name)
        {
            var load = MoonfallLevelLoader.Parse(File.ReadAllText(Path.Combine(repo, "docs", "design", "v9", "rich", "levels", name + ".json")));
            var level = load.Level ?? throw new InvalidOperationException(string.Join("; ", load.Errors));
            return level with { Scene = name switch { "base-p1" => "airship-road", "base-p2" => "holy-see", _ => null } };
        }

        var levels = new List<MoonfallLevel>();
        for (var i = 0; i < 15; i++)
        {
            var id = MoonfallStages.LevelId(MoonfallCampaignKind.Base, i);
            var level = i switch
            {
                10 => Pilot("base-p3") with { Name = "The Moonlit Post" },
                11 => Pilot("base-p2") with { Name = "The Holy See" },
                12 => Pilot("base-p1") with { Name = "The Airship Road" },
                13 => builtIn.Base.Levels[0] with { Name = "Above the Clouds" },
                14 => builtIn.Base.Levels[1] with { Name = "The Ironworks Dock" },
                _ => builtIn.Base.Levels[i % builtIn.Base.Levels.Count],
            };
            levels.Add(level with { Id = id });
        }

        var campaigns = new MoonfallCampaigns(new MoonfallCampaign(MoonfallCampaignKind.Base, levels), builtIn.Expansion, []);
        var progress = new MoonfallProgress { BaseCleared = 12 };
        for (var i = 0; i < 12; i++)
        {
            progress.Levels[MoonfallStages.LevelId(MoonfallCampaignKind.Base, i)] = new MoonfallLevelRecord { Cleared = true, Best = 180_000 + (i * 7_130), Aced = i % 4 == 1 };
        }

        progress.Levels["base-11"] = new MoonfallLevelRecord { Cleared = true, Best = 214_300 };
        progress.Levels["base-12"] = new MoonfallLevelRecord { Cleared = true, Best = 251_880, Aced = true };
        var aces = new Dictionary<string, long>(StringComparer.Ordinal) { ["base-11"] = 200_000, ["base-12"] = 240_000, ["base-13"] = 240_000 };
        if (screen == "tally" || moment == "tally")
        {
            // The staged win reaches an Ace and beats an earlier best, so ACED and NEW BEST show (the mock's callout).
            aces["base-13"] = 100_000;
            progress.Levels["base-13"] = new MoonfallLevelRecord { Best = 60_000 };
        }

        if (screen == "far")
        {
            // The Far Shore open: every level of The Moon Road won (its proposed stage names on the map).
            progress.BaseCleared = MoonfallStages.BaseLevels;
        }

        if (screen == "challenges")
        {
            progress.BaseCleared = MoonfallStages.BaseLevels;
            progress.Challenges["ch-01"] = new MoonfallChallengeRecord { Done = true, Best = 168_000 };
            progress.Challenges["ch-02"] = new MoonfallChallengeRecord { Best = 91_400 };
        }

        var story = new MoonfallStory(static name => name != "Alisaie");
        return (campaigns, progress, id => id is not null && aces.TryGetValue(id, out var ace) ? ace : null, story);
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
            levels.Add(builtIn.Base.Levels[i % builtIn.Base.Levels.Count] with { Id = MoonfallStages.LevelId(MoonfallCampaignKind.Base, i) });
        }

        levels.Add(pilot with { Id = MoonfallStages.LevelId(MoonfallCampaignKind.Base, 12) });
        levels.Add(builtIn.Base.Levels[0] with { Id = MoonfallStages.LevelId(MoonfallCampaignKind.Base, 13) });
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
