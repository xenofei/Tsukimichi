using System.Text.RegularExpressions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// Source lints for Moonfall's screens (the test project does not reference the plugin, so it reads the files): every
/// key Moonfall answers is claimed from the game, Esc goes back (closing the window only from the title), a click that
/// opens or resumes the board never shoots, the destructive entries are held to confirm, and a collapsed window holds
/// its sound.
/// </summary>
public sealed class MoonfallScreensLintTests
{
    private static string Root => ResxFiles.RepositoryRoot();

    private static string Code(string source) =>
        string.Join('\n', source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(static line =>
        {
            var at = line.IndexOf("//", StringComparison.Ordinal);
            return at < 0 ? line : line[..at];
        }));

    private static string Ui(string name) => Code(File.ReadAllText(Path.Combine(Root, "Tsukimichi", "Ui", name)));

    private static string Plugin(string name) => Code(File.ReadAllText(Path.Combine(Root, "Tsukimichi", name)));

    /// <summary>The body of the first member whose declaration contains <paramref name="signature"/>, to its closing brace.</summary>
    private static string Member(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"not found: {signature}");
        var end = source.IndexOf("\n    }", at, StringComparison.Ordinal);
        return source[at..(end < 0 ? source.Length : end)];
    }

    [Fact]
    public void Esc_and_the_gamepads_back_go_through_the_keyboard_helpers_and_are_claimed_from_the_game()
    {
        var keys = Member(Ui("MoonfallWindow.Flow.cs"), "private void HandleKeys(");
        Assert.Contains("var hasKeys = Keyboard.WindowHasKeys();", keys, StringComparison.Ordinal);
        Assert.Contains("Keys?.ClaimBack();", keys, StringComparison.Ordinal);
        Assert.Contains("Keys?.ClaimNavigation();", keys, StringComparison.Ordinal);
        Assert.Contains("Keyboard.BackPressed()", keys, StringComparison.Ordinal);
        Assert.Contains("Keyboard.StartPressed()", keys, StringComparison.Ordinal);

        // Back is Esc or the gamepad's East, and only while the window has the keys.
        var keyboard = Code(File.ReadAllText(Path.Combine(Root, "Tsukimichi", "Ui", "Keyboard.cs")));
        var back = Member(keyboard, "public static bool BackPressed(");
        Assert.Contains("WindowHasKeys() &&", back, StringComparison.Ordinal);
        Assert.Contains("ImGuiKey.Escape", back, StringComparison.Ordinal);
        Assert.Contains("ImGuiKey.GamepadFaceRight", back, StringComparison.Ordinal);
    }

    [Fact]
    public void The_claimed_keys_are_cleared_from_the_games_key_state_on_each_framework_update()
    {
        var claim = Code(File.ReadAllText(Path.Combine(Root, "Tsukimichi", "Ui", "GameKeyClaim.cs")));
        var consume = Member(claim, "public void Consume(");
        Assert.Contains("state[VirtualKey.ESCAPE] = false;", consume, StringComparison.Ordinal);
        Assert.Contains("foreach (var key in NavigationKeys)", consume, StringComparison.Ordinal);
        foreach (var key in new[] { "UP", "DOWN", "LEFT", "RIGHT", "RETURN", "SPACE", "TAB" })
        {
            Assert.Contains("VirtualKey." + key, claim, StringComparison.Ordinal);
        }

        // Wired to the framework while Moonfall lives, and unwired when it goes.
        var wiring = Plugin("Plugin.Moonfall.cs");
        Assert.Contains("window.Keys = moonfallKeys;", wiring, StringComparison.Ordinal);
        Assert.Contains("Framework.Update += moonfallKeys.Consume;", wiring, StringComparison.Ordinal);
        Assert.Contains("Framework.Update -= moonfallKeys.Consume;", wiring, StringComparison.Ordinal);
    }

    [Fact]
    public void Dalamuds_close_key_closes_the_window_only_from_the_title()
    {
        var draw = Member(Ui("MoonfallWindow.cs"), "private void DrawWindow(");
        Assert.Contains("RespectCloseHotkey = flow.Current == MoonfallScreen.Title;", draw, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(string.Concat(Directory.GetFiles(Path.Combine(Root, "Tsukimichi", "Ui"), "MoonfallWindow*.cs").Select(f => Code(File.ReadAllText(f)))), @"RespectCloseHotkey\s*=\s*flow"));
    }

    [Fact]
    public void A_click_that_opens_or_resumes_the_board_never_shoots()
    {
        var board = Member(Ui("MoonfallWindow.Board.cs"), "private void DrawBoard(");
        // While paused (the pause menu is over it), over the tally, and in the first moments after it appears, the board
        // takes no press at all; the crest's pause is taken before the board's own button.
        Assert.Contains("if (over || pause.Paused || !BoardArmed)", board, StringComparison.Ordinal);
        Assert.True(board.IndexOf("PauseCrest(", StringComparison.Ordinal) < board.IndexOf("ImGui.InvisibleButton(\"##moonfallBoard\"", StringComparison.Ordinal));
        var start = Member(Ui("MoonfallWindow.Flow.cs"), "private void StartBoard(");
        Assert.Contains("boardArmedAt = ImGui.GetTime() + BoardArmSeconds;", start, StringComparison.Ordinal);

        // The pause menu resumes with Resume, Esc, or a click outside it; none of them is the board's button.
        var pauseMenu = Member(Ui("MoonfallWindow.Pause.cs"), "private void DrawPauseMenu(");
        Assert.DoesNotContain("Shoot", pauseMenu, StringComparison.Ordinal);
    }

    [Fact]
    public void Restart_and_leave_are_held_to_confirm()
    {
        var pauseMenu = Member(Ui("MoonfallWindow.Pause.cs"), "private void DrawPauseMenu(");
        Assert.Contains("HoldButton(m, \"##mfRestart\"", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("HoldButton(m, \"##mfLeave\"", pauseMenu, StringComparison.Ordinal);
        Assert.DoesNotContain("MenuButton(m, \"##mfRestart\"", pauseMenu, StringComparison.Ordinal);
        Assert.DoesNotContain("MenuButton(m, \"##mfLeave\"", pauseMenu, StringComparison.Ordinal);
        var hold = Member(Ui("MoonfallWindow.Menu.cs"), "private bool HoldButton(");
        Assert.Contains("var held = ImGui.IsItemActive();", hold, StringComparison.Ordinal);
        Assert.Contains("var done = hold.Update(held,", hold, StringComparison.Ordinal);
    }

    [Fact]
    public void A_collapsed_window_holds_its_sound_and_its_board()
    {
        // Dalamud calls PreOpenCheck every frame but skips Draw while the window is collapsed: a frame with no draw since
        // the last holds the sound (the finale too) as a paused board does.
        var check = Member(Ui("MoonfallWindow.Art.cs"), "public override void PreOpenCheck(");
        Assert.Contains("if (IsOpen && drawWatch.Missed(ImGui.GetFrameCount()))", check, StringComparison.Ordinal);
        Assert.Contains("HoldWhileHidden();", check, StringComparison.Ordinal);
        var hold = Member(Ui("MoonfallWindow.Sound.cs"), "private void HoldWhileHidden(");
        Assert.Contains("Audio?.Update(true,", hold, StringComparison.Ordinal);
        Assert.Contains("pause.Pause(MoonfallPauseReason.Unfocused);", hold, StringComparison.Ordinal);
        Assert.Contains("drawWatch.Drew(ImGui.GetFrameCount());", Member(Ui("MoonfallWindow.cs"), "private void DrawWindow("), StringComparison.Ordinal);
    }

    [Fact]
    public void A_finished_level_is_recorded_through_the_modes_once()
    {
        var finish = Member(Ui("MoonfallWindow.Flow.cs"), "private void FinishIfOver(");
        Assert.Contains("if (finished || !LevelOver(g))", finish, StringComparison.Ordinal);
        Assert.Contains("modes.FinishLevel(start, g)", finish, StringComparison.Ordinal);
        Assert.Contains("modes.FinishChallenge(", finish, StringComparison.Ordinal);
        Assert.Contains("modes.FinishDuel(d)", finish, StringComparison.Ordinal);

        // Nothing else in the window writes the progress.
        var writes = Directory.GetFiles(Path.Combine(Root, "Tsukimichi", "Ui"), "MoonfallWindow*.cs")
            .SelectMany(f => Regex.Matches(Code(File.ReadAllText(f)), @"progress\.Record(Level|Challenge|Duel)\(").Select(m => Path.GetFileName(f) + ": " + m.Value))
            .ToList();
        Assert.True(writes.Count == 0, string.Join(Environment.NewLine, writes));
    }
}
