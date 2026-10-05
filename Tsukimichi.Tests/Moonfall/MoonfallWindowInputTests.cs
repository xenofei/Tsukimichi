using System.Text.RegularExpressions;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Tests.Localization;
using static Tsukimichi.Tests.Moonfall.MoonfallTestKit;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// Moonfall's input in the window: the board's click rule (<see cref="MoonfallBoardPress"/>), and source lints over the
/// plugin's Moonfall files (the test project does not reference the plugin, so it reads them from the repository): no
/// key reaches the game from Moonfall, the board shoots only through the click rule, and the art board draws every ball.
/// </summary>
public sealed partial class MoonfallWindowInputTests
{
    // ---- The click rule ----

    [Fact]
    public void A_click_begun_while_aiming_shoots()
    {
        var press = default(MoonfallBoardPress);
        press.Pressed(MoonfallPhase.Aiming, paused: false);
        Assert.True(press.Released(MoonfallPhase.Aiming, paused: false));

        // The press is spent: a second release (none pressed) does not shoot again.
        Assert.False(press.Released(MoonfallPhase.Aiming, paused: false));
    }

    [Theory]
    [InlineData(MoonfallPhase.Flying)]
    [InlineData(MoonfallPhase.Clearing)]
    public void A_hold_that_spans_into_the_next_ball_does_not_shoot(MoonfallPhase pressedIn)
    {
        // Held for the flippers through the flight (or pressed while the pegs clear), let go once the next ball waits.
        var press = default(MoonfallBoardPress);
        press.Pressed(pressedIn, paused: false);
        Assert.False(press.Released(MoonfallPhase.Aiming, paused: false));

        // The next press, begun while aiming, shoots as ever.
        press.Pressed(MoonfallPhase.Aiming, paused: false);
        Assert.True(press.Released(MoonfallPhase.Aiming, paused: false));
    }

    [Fact]
    public void A_click_that_resumes_the_board_or_ends_after_the_shot_does_not_shoot()
    {
        var press = default(MoonfallBoardPress);
        press.Pressed(MoonfallPhase.Aiming, paused: true);
        Assert.False(press.Released(MoonfallPhase.Aiming, paused: true));

        // Paused (combat) while held: the release resumes, it does not shoot.
        press.Pressed(MoonfallPhase.Aiming, paused: false);
        Assert.False(press.Released(MoonfallPhase.Aiming, paused: true));

        press.Pressed(MoonfallPhase.Aiming, paused: false);
        Assert.False(press.Released(MoonfallPhase.Flying, paused: false));
    }

    [Fact]
    public void A_hold_through_a_drained_ball_does_not_fire_the_next_one_in_a_real_game()
    {
        // The window's order each frame: the press noted as it starts, the game ticks, the release asked about.
        var game = new MoonfallGame(Board(Blue(120, 250)), 1, 1);
        var press = default(MoonfallBoardPress);
        press.Pressed(game.Phase, paused: false);
        Assert.True(press.Released(game.Phase, paused: false) && game.Shoot(85));

        // Pressed during the flight (the flippers' hold), held until the next ball waits in the launcher.
        press.Pressed(game.Phase, paused: false);
        Assert.Equal(MoonfallPhase.Flying, game.Phase);
        RunUntil(game, static g => g.Phase == MoonfallPhase.Aiming);
        Assert.Equal(MoonfallPhase.Aiming, game.Phase);
        var balls = game.BallsLeft;
        Assert.False(press.Released(game.Phase, paused: false));
        Assert.Equal(balls, game.BallsLeft);
    }

    // ---- Source lints ----

    private static string Root => ResxFiles.RepositoryRoot();

    /// <summary>
    /// The plugin's Moonfall sources, comments dropped: the window's partials, its art textures, its strings, the main
    /// window's button and the plugin's wiring.
    /// </summary>
    private static IEnumerable<(string Name, string Code)> MoonfallSources()
    {
        var sep = Path.DirectorySeparatorChar;
        return Directory.GetFiles(Path.Combine(Root, "Tsukimichi"), "*Moonfall*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{sep}obj{sep}", StringComparison.Ordinal) && !file.Contains($"{sep}bin{sep}", StringComparison.Ordinal))
            .OrderBy(static f => f, StringComparer.Ordinal)
            .Select(static file => (Path.GetFileName(file), Code(File.ReadAllText(file))));
    }

    private static string Code(string source) =>
        string.Join('\n', source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(static line =>
        {
            var at = line.IndexOf("//", StringComparison.Ordinal);
            return at < 0 ? line : line[..at];
        }));

    private static string Source(string name) => Code(File.ReadAllText(Path.Combine(Root, "Tsukimichi", "Ui", name)));

    /// <summary>The body of the first member whose declaration contains <paramref name="signature"/>, to its closing brace.</summary>
    private static string Member(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"not found: {signature}");
        var end = source.IndexOf("\n    }", at, StringComparison.Ordinal);
        return source[at..(end < 0 ? source.Length : end)];
    }

    [GeneratedRegex(@"\b(IsKeyDown|IsKeyPressed|IsKeyReleased|IsKeyChordPressed|GetKeyPressedAmount|ImGuiKey|KeysDown|IKeyState|KeyState|VirtualKey|GetAsyncKeyState)\b")]
    private static partial Regex RawKey();

    [Fact]
    public void Moonfall_polls_no_key_of_its_own()
    {
        // Dalamud hands every key on to the game too, so a key bound here would also jump, cast or type into the chat. A
        // key Moonfall needs goes through Keyboard's helpers (WindowHasKeys and the rest), which check the window's focus
        // and that no text field wants the keys; the flippers take the mouse alone.
        var files = MoonfallSources().ToList();
        Assert.True(files.Count >= 8, $"found {files.Count} Moonfall sources");
        Assert.Contains(files, static f => f.Name == "MoonfallWindow.Powers.cs");
        var offenders = files
            .SelectMany(static f => RawKey().Matches(f.Code).Select(m => $"{f.Name}: {m.Value}"))
            .ToList();
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void The_flippers_take_the_mouse_over_the_board()
    {
        var feed = Member(Source("MoonfallWindow.Powers.cs"), "private void FeedFlippers(");
        Assert.Contains("boardHovered && ImGui.IsMouseDown(ImGuiMouseButton.Left)", feed, StringComparison.Ordinal);
        Assert.Contains("g.SetFlippers(held && g.FlippersOut);", feed, StringComparison.Ordinal);
    }

    [Fact]
    public void The_board_shoots_only_through_the_click_rule()
    {
        var board = Member(Source("MoonfallWindow.Board.cs"), "private void DrawBoard(");
        Assert.Contains("if (ImGui.IsItemActivated())", board, StringComparison.Ordinal);
        Assert.Contains("boardPress.Pressed(g.Phase, pause.Paused);", board, StringComparison.Ordinal);
        Assert.Contains("var shoot = boardPress.Released(g.Phase, pause.Paused);", board, StringComparison.Ordinal);
        Assert.Contains("else if (shoot)", board, StringComparison.Ordinal);

        // One shot in the window, and it is that one.
        var shots = MoonfallSources().Sum(static f => Regex.Count(f.Code, @"\.Shoot\("));
        Assert.Equal(1, shots);
        Assert.Equal(1, Regex.Count(board, @"\.Shoot\("));
    }

    [Fact]
    public void The_art_board_draws_every_ball_with_the_sprite_and_trails_the_cameras_ball()
    {
        var art = Member(Source("MoonfallWindow.Art.cs"), "private void ArtBall(");
        Assert.Contains("for (var k = 0; k < g.BallsInPlay; k++)", art, StringComparison.Ordinal);
        Assert.Contains("Put(p, ball, x, y, size, uint.MaxValue);", art, StringComparison.Ordinal);
        Assert.Contains("g.CameraBall", art, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawBall(", art, StringComparison.Ordinal);

        // The primitive twins only where the art did not draw the board.
        var extra = Member(Source("MoonfallWindow.Powers.cs"), "private void DrawExtraBalls(");
        Assert.Contains("if (k > 0 && !art)", extra, StringComparison.Ordinal);
        Assert.Contains("DrawPowers(dl, view, g, alpha, drewArt);", Source("MoonfallWindow.Board.cs"), StringComparison.Ordinal);
    }
}
