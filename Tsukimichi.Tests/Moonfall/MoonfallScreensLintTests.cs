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

    /// <summary>An expression-bodied member whose declaration contains <paramref name="signature"/>, to its semicolon.</summary>
    private static string Expression(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"not found: {signature}");
        var end = source.IndexOf(";\n", at, StringComparison.Ordinal);
        Assert.True(end >= 0, $"no end: {signature}");
        return source[at..(end + 1)];
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
        Assert.Contains("RespectCloseHotkey = flow.Current == MoonfallScreen.Title && !escAfterPopup;", draw, StringComparison.Ordinal);

        // An Esc that dismissed a popup on the title (the shield's reveal menu) never also closes the window (UX m27):
        // latched from the popup until Esc is let go, and set before the close key is gated on it.
        Assert.Contains("escAfterPopup = (escAfterPopup || popupWasOpen || ImGui.IsPopupOpen(", draw, StringComparison.Ordinal);
        Assert.Contains("&& Keyboard.EscapeHeld();", draw, StringComparison.Ordinal);
        Assert.True(draw.IndexOf("escAfterPopup = ", StringComparison.Ordinal) < draw.IndexOf("RespectCloseHotkey = ", StringComparison.Ordinal));
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

    [Fact]
    public void The_navigation_keys_are_claimed_over_the_menus_the_pause_and_the_tally_and_while_still_held()
    {
        var keys = Member(Ui("MoonfallWindow.Flow.cs"), "private void HandleKeys(");

        // The tally is a menu too: its Replay, Map and Next take the arrows, Enter and Space.
        Assert.Contains("var menu = flow.Current != MoonfallScreen.Play || pause.Paused || (game is { } g && LevelOver(g));", keys, StringComparison.Ordinal);

        // A key a menu took stays claimed until it is let go (Enter that pressed Play, a key held through a hold).
        Assert.Contains("var stillHeld = navigationTaken && Keyboard.NavigationKeyHeld();", keys, StringComparison.Ordinal);
        Assert.Contains("if ((hasKeys && menu) || stillHeld)", keys, StringComparison.Ordinal);
    }

    [Fact]
    public void A_press_outside_the_pause_resumes_only_when_it_began_outside_and_every_resume_re_arms_the_board()
    {
        var pauseMenu = Member(Ui("MoonfallWindow.Pause.cs"), "private void DrawPauseMenu(");
        Assert.Contains("outsidePress = !inside && onBoard && BoardArmed && ImGui.IsWindowHovered() && !ImGui.IsAnyItemHovered();", pauseMenu, StringComparison.Ordinal);
        Assert.Contains("if (outsidePress && ImGui.IsMouseReleased(ImGuiMouseButton.Left))", pauseMenu, StringComparison.Ordinal);

        // A pause or a resume re-arms the board (a double click on Resume or on the crest never shoots) and forgets a press.
        var play = Member(Ui("MoonfallWindow.cs"), "private void DrawPlay(");
        Assert.Contains("if (pause.Paused != wasPaused)", play, StringComparison.Ordinal);
        Assert.Contains("boardArmedAt = ImGui.GetTime() + BoardArmSeconds;", play, StringComparison.Ordinal);
        Assert.Contains("outsidePress = false;", play, StringComparison.Ordinal);
    }

    [Fact]
    public void The_screens_draw_stage_and_level_names_only_through_the_spoiler_shields_helpers()
    {
        // The Far Shore follows the spoiler shield: a stage set past the player's story prints the shield's placeholder,
        // and its levels no names. Every name the screens draw goes through StageNameShown, LevelNameShown or
        // PlayLevelName, so none can leak by another path.
        var map = Ui("MoonfallWindow.Map.cs");
        var helpers = new[]
        {
            Expression(map, "private string StageNameShown("),
            Expression(map, "private static string LevelNameShown("),
            Expression(map, "private string PlayLevelName("),
        };
        Assert.Contains("modes.StageName(stage)", helpers[0], StringComparison.Ordinal);
        Assert.Contains("MoonfallLevelState.Veiled", helpers[1], StringComparison.Ordinal);
        Assert.Contains("modes.LevelVeiled(level.Id)", helpers[2], StringComparison.Ordinal);

        var leaks = new List<string>();
        var pattern = new Regex(@"\.Stage\.Name\b|stage\??\.Name\b|\]\.Name\b|Level\??\.Name\b|\blevel\.Name\b|playLevel\??\.Name\b");
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "Tsukimichi", "Ui"), "MoonfallWindow*.cs"))
        {
            var code = Code(File.ReadAllText(file));
            foreach (var helper in helpers)
            {
                code = code.Replace(helper, string.Empty, StringComparison.Ordinal);
            }

            leaks.AddRange(pattern.Matches(code).Select(m => Path.GetFileName(file) + ": " + m.Value));
        }

        Assert.True(leaks.Count == 0, string.Join(Environment.NewLine, leaks));
    }

    [Fact]
    public void The_key_that_closes_a_popup_is_not_also_back()
    {
        // ImGui closes the top popup on Esc or B during NewFrame, before the window draws: a popup open at the end of the
        // last frame keeps that key from also going back a screen or pausing.
        var keys = Member(Ui("MoonfallWindow.Flow.cs"), "private void HandleKeys(");
        Assert.Contains("if (popupWasOpen || ImGui.IsPopupOpen(", keys, StringComparison.Ordinal);
        var draw = Member(Ui("MoonfallWindow.cs"), "private void DrawWindow(");
        Assert.Contains("popupWasOpen = ImGui.IsPopupOpen(", draw, StringComparison.Ordinal);
        Assert.True(draw.IndexOf("ShieldText.DrawMenu(", StringComparison.Ordinal) < draw.IndexOf("popupWasOpen = ", StringComparison.Ordinal));
    }

    [Fact]
    public void A_veiled_stage_reveals_on_a_press_and_its_head_offers_levels_only_when_it_has_them()
    {
        var menu = Member(Ui("MoonfallWindow.Menu.cs"), "private bool MenuButton(");
        Assert.Contains("if (clicked && (!inert || style == MenuStyle.Veiled))", menu, StringComparison.Ordinal);

        var map = Ui("MoonfallWindow.Map.cs");
        var reveal = Member(map, "private void PlayOrReveal(");
        Assert.Contains("ShieldText.RequestMenu(SpoilerKind.Area, zone, panelName);", reveal, StringComparison.Ordinal);
        Assert.Contains("panelRevealable", reveal, StringComparison.Ordinal);

        // A reveal is offered only where it opens something: the road has come to the stage and its levels are built
        // (GD m14). The 640 short line that names the reveal is drawn only then (GD m17, UX m28).
        Assert.Contains("var veiledComing = sel.State == MoonfallStageState.Veiled && sel.Reached && !modes.StageBuilt(sel.Stage);", map, StringComparison.Ordinal);
        Assert.Contains("panelRevealable = sel.State == MoonfallStageState.Veiled && sel.Reached && !veiledComing;", map, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(map, @"Strings\.MoonfallStageVeiledShort"));
        Assert.Contains("panelRevealable ? Strings.MoonfallStageVeiledShort :", map, StringComparison.Ordinal);
        Assert.Contains("!view.Reached ? Strings.MoonfallStopVeiledSealedLine : modes.StageBuilt(view.Stage) ? Strings.MoonfallStopVeiledLine : Strings.MoonfallStopVeiledComingLine", map, StringComparison.Ordinal);

        var small = Member(map, "private void SmallStagePanel(");
        Assert.Contains("var headOpens = view.State is MoonfallStageState.Open or MoonfallStageState.Done;", small, StringComparison.Ordinal);
        Assert.Contains("if ((hovered || nav) && headOpens)", small, StringComparison.Ordinal);
    }

    [Fact]
    public void The_tallys_next_and_notes_follow_where_the_road_goes_next()
    {
        // Next leads on to the road's next playable level even outside the stage order's reach (base-55 to FS 1-1; GD m16).
        var next = Member(Ui("MoonfallWindow.Flow.cs"), "private MoonfallLevelPlace? AdventureNext(");
        Assert.Contains(": modes.Next() is { Veiled: false } next ? next.Place : null;", next, StringComparison.Ordinal);

        // "The last level for now" only when nothing anywhere is left to play; the veil note when the road waits.
        var tally = Ui("MoonfallWindow.Tally.cs");
        Assert.Contains("modes.Next() is { Veiled: true } waits", tally, StringComparison.Ordinal);
        Assert.Contains("modes.Next() is null)", tally, StringComparison.Ordinal);
        Assert.Contains("focus: !nextFocus && won && tallyVeil is null", tally, StringComparison.Ordinal);
        Assert.Contains("tallyVeil is not null)))", tally, StringComparison.Ordinal);

        // Leaving the board over a road-waits tally, by any way, opens the map on the waiting stage (UX m25), and the
        // plain tally's default press is then Map (UX m29).
        var end = Member(Ui("MoonfallWindow.Flow.cs"), "private void EndBoard(");
        Assert.Contains("tallyVeil is { } waiting", end, StringComparison.Ordinal);
        Assert.Contains("mapStage = waiting.Number - 1;", end, StringComparison.Ordinal);
        var plain = Member(Ui("MoonfallWindow.Board.cs"), "private void DrawEnd(");
        Assert.Contains("var homeFocus = !nextFocus && (!won || tallyAgain is null || tallyVeil is not null);", plain, StringComparison.Ordinal);
        Assert.True(plain.IndexOf("if (homeFocus)", StringComparison.Ordinal) < plain.IndexOf("ImGui.SetItemDefaultFocus();", plain.IndexOf("if (homeFocus)", StringComparison.Ordinal), StringComparison.Ordinal));
    }

    [Fact]
    public void A_new_tally_gives_its_default_press_the_keyboard_focus_as_a_new_menu_screen_does()
    {
        // SetItemDefaultFocus acts only on a window's appearing frame; the tally comes long after (UX m31). A new tally
        // asks for the focus once, when nav is showing, and its default press takes it before its item, rich and plain.
        var tally = Ui("MoonfallWindow.Tally.cs");
        var prepare = Member(tally, "private void PrepareTally(");
        Assert.Contains("tallyFocusAsked = ImGui.GetIO().NavVisible;", prepare, StringComparison.Ordinal);
        Assert.True(prepare.IndexOf("if (!ReferenceEquals(tallyTextsFor, g))", StringComparison.Ordinal) < prepare.IndexOf("tallyFocusAsked = ", StringComparison.Ordinal));

        var before = Member(tally, "private void TallyFocusBefore(");
        Assert.Contains("if (isDefault && tallyFocusAsked)", before, StringComparison.Ordinal);
        Assert.Contains("ImGui.SetKeyboardFocusHere();", before, StringComparison.Ordinal);
        Assert.Contains("tallyFocusAsked = false;", before, StringComparison.Ordinal);

        var pill = Member(tally, "private bool PillButton(");
        Assert.True(pill.IndexOf("TallyFocusBefore(focus);", StringComparison.Ordinal) is >= 0 and var at && at < pill.IndexOf("ImGui.InvisibleButton(", StringComparison.Ordinal));

        var plain = Member(Ui("MoonfallWindow.Board.cs"), "private void DrawEnd(");
        foreach (var (focus, button) in (ReadOnlySpan<(string, string)>)[("TallyFocusBefore(againFocus);", "ImGui.Button(tallyAgainLabel"), ("TallyFocusBefore(homeFocus);", "ImGui.Button(tallyHomeLabel"), ("TallyFocusBefore(nextFocus);", "ImGui.Button(tallyNextLabel")])
        {
            var f = plain.IndexOf(focus, StringComparison.Ordinal);
            Assert.True(f >= 0 && f < plain.IndexOf(button, StringComparison.Ordinal), focus);
        }

        // The menus' own rule, which this mirrors.
        Assert.Contains("ImGui.SetKeyboardFocusHere();", Member(Ui("MoonfallWindow.Menu.cs"), "private void DefaultFocusBefore("), StringComparison.Ordinal);
    }

    [Fact]
    public void The_plain_bar_draws_whole_parts_only_with_the_game_state_first_in_a_duel()
    {
        var bar = Member(Ui("MoonfallWindow.cs"), "private float DrawPlainBar(");
        Assert.Contains("? [turn, ballsText, orangesText, multiplierText, stageText, levelName]", bar, StringComparison.Ordinal);
        // Solo play keeps the level's code, then the game state, and the name last: a narrow window drops the name first.
        Assert.Contains(": [stageText, ballsText, orangesText, multiplierText, levelName];", bar, StringComparison.Ordinal);
        Assert.Contains("if (x + w > right)", bar, StringComparison.Ordinal);
        Assert.DoesNotContain("PushClipRect", bar, StringComparison.Ordinal);
    }

    [Fact]
    public void The_plugin_gives_moonfall_the_viewed_characters_shield_for_places_and_its_reveal()
    {
        var plugin = Plugin("Plugin.Moonfall.cs");
        Assert.Contains("Session.Spoilers.IsNameMasked(Core.Query.SpoilerKind.Area, zone)", plugin, StringComparison.Ordinal);
        Assert.Contains("() => Session.Spoilers.Fingerprint", plugin, StringComparison.Ordinal);
        Assert.Contains("ShieldSession = Session,", plugin, StringComparison.Ordinal);

        // Its placeholders answer as every placeholder does: the shield's hover, and its right-click menu at the window's root.
        var map = Ui("MoonfallWindow.Map.cs");
        Assert.Contains("ShieldText.Interact(min, max, session, SpoilerKind.Area, zone, shown);", map, StringComparison.Ordinal);
        Assert.Contains("ShieldText.DrawMenu(nameof(MoonfallWindow), session);", Ui("MoonfallWindow.cs"), StringComparison.Ordinal);
    }
}
