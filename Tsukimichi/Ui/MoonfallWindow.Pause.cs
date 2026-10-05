using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The pause menu and Options (spec-rich2.md §4, play2.pause): a taller journal window over the dimmed board (below the
/// top rail at 640) with Resume focused, Restart and Leave as garnet pills each held to confirm (the owner's rule for
/// destructive clicks: a lighter fill sweeps the pill as it is held), Options, and the quick settings: Reduce motion and
/// Peg marks as On/Off switches, Decoration and Sound between gilt chevrons; then the note that Moonfall pauses itself.
/// While combat, a duty or a cutscene holds the pause, Resume is off and says so. Options holds every Moonfall setting in
/// one spacious panel. On the board the crest's moonstone pauses (the mouse's way in; Esc and the gamepad's Start too).
/// </summary>
public sealed partial class MoonfallWindow
{
    private Vector2 boardOrigin;
    private Vector2 boardSize;
    private Vector2 boardAreaMin;
    private Vector2 boardAreaMax;
    private bool crestHovered;

    // The pause menu's words.
    private string pauseLine = string.Empty;
    private (int Level, int Balls, int Oranges, MoonfallPauseReason Reason, int Language) pauseLineFor = (-1, -1, -1, MoonfallPauseReason.None, -1);

    /// <summary>The pause's mouse target on the board: the crest's moonstone (taken before the board's own button, so it never shoots).</summary>
    private void PauseCrest(in View view, MoonfallGame g)
    {
        var min = view.Map(400 - 13, 20 - 13);
        var max = view.Map(400 + 13, 20 + 13);
        ImGui.SetCursorScreenPos(min);
        if (ImGui.InvisibleButton("##mfPauseCrest", Vector2.Max(max - min, Vector2.One)))
        {
            TogglePause(g);
        }

        crestHovered = ImGui.IsItemHovered();
        if (crestHovered)
        {
            UiMetrics.Tooltip(Strings.MoonfallPauseTooltip);
        }
    }

    /// <summary>A pen over the window's content for a panel drawn over the board (the pause menu), at the menus' layout.</summary>
    private MenuPen OverlayPen()
    {
        var avail = Vector2.Max(boardAreaMax - boardAreaMin, Vector2.One);
        var small = avail.X < 980f || avail.Y < 610f;
        var design = small ? new Vector2(640f, 480f) : new Vector2(1280f, 800f);
        var scale = MathF.Min(avail.X / design.X, avail.Y / design.Y);
        var origin = boardAreaMin + ((avail - (design * scale)) * 0.5f);
        origin = new Vector2(MathF.Round(origin.X), MathF.Round(origin.Y));
        var view = new View(origin, scale, 1f, BoardCentre);
        var dl = ImGui.GetWindowDrawList();
        var sheet = gameArt?.Chrome is { } s && gameArt.ChromeTexture is { } t ? (s, t.Handle) : (NoChrome.Value, default(ImTextureID));
        var hasArt = art?.Atlas is not null && art.Sheet(false, out _) is not null;
        var artPen = hasArt ? new ArtPen(dl, view, art!.Atlas!, art.Sheet(false, out _)!.Handle) : default;
        return new MenuPen(dl, view, new ChromePen(dl, view, sheet.Item1, sheet.Item2), artPen, hasArt, small, boardAreaMin, boardAreaMax);
    }

    private void DrawPauseMenu(MoonfallGame g)
    {
        var m = OverlayPen();
        var dl = m.Dl;
        dl.AddRectFilled(m.AreaMin, m.AreaMax, Ink(MoonfallColor.Hex("#03040C"), 0.6f));
        RefreshPauseLine(g);
        var small = m.Small;
        var held = pause.Held;
        var (x0, y0, x1, y1) = small ? (160.0, 44.0, 480.0, 476.0) : (420.0, 70.0, 860.0, 760.0);

        // A click on the dimmed board outside the window resumes, and never shoots (the board takes no press while paused).
        var panelMin = m.V.Map(x0, y0);
        var panelMax = m.V.Map(x1, y1);
        ImGui.SetCursorScreenPos(m.AreaMin);
        ImGui.Dummy(Vector2.One);
        Panel(m, x0, y0, x1, y1, null, small ? 0.32 : 0.5);
        var cx = (x0 + x1) / 2;
        MenuTitle(m, cx, y0 + (small ? 34 : 58), Strings.MoonfallPausedTitle, small ? 40 : 60, Anchor.Centre);
        if (!small)
        {
            MenuText(m, MoonfallFace.Axis, 15, cx, y0 + 96, pauseLine, held ? DangerInk : Ink2, Anchor.Centre, edge: 0f, maxWidth: (float)(x1 - x0 - 40));
            CrestRule(m, cx, y0 + 124, 320, true, 0.4);
        }

        var (pad, bh, top, step) = small ? (30.0, 30.0, 110.0, 58.0) : (66.0, 42.0, 226.0, 84.0);
        if (MenuButton(m, "##mfResume", x0 + pad, y0 + (small ? 64 : 154), x1 - pad, y0 + (small ? 100 : 206), Strings.MoonfallResume, small ? 26 : 36, isDefault: true,
            style: held ? MenuStyle.Locked : MenuStyle.Normal, tooltip: held ? Strings.MoonfallWaitsForIt : null))
        {
            pause.TryResume();
        }

        if (HoldButton(m, "##mfRestart", x0 + pad, y0 + top, x1 - pad, y0 + top + bh, Strings.MoonfallRestartLevel, small ? 20 : 28, ref pauseHolds.Restart))
        {
            Restart();
            return;
        }

        MenuText(m, MoonfallFace.Axis, small ? 12.5f : 14, cx, y0 + top + bh + (small ? 14 : 18), small ? Strings.MoonfallHoldToRestartShort : Strings.MoonfallHoldToRestart, DangerInk, Anchor.Centre, edge: 0f);
        var ly = y0 + top + step;
        if (HoldButton(m, "##mfLeave", x0 + pad, ly, x1 - pad, ly + bh, LeaveLabel(), small ? 20 : 28, ref pauseHolds.Leave))
        {
            LeaveBoard();
            return;
        }

        MenuText(m, MoonfallFace.Axis, small ? 12.5f : 14, cx, ly + bh + (small ? 14 : 18), small ? Strings.MoonfallHoldToLeaveShort : Strings.MoonfallHoldToLeave, DangerInk, Anchor.Centre, edge: 0f);
        var oy = ly + step;
        if (MenuButton(m, "##mfPauseOptions", x0 + pad, oy, x1 - pad, oy + bh, Strings.MoonfallScreenOptions, small ? 20 : 28))
        {
            Open(MoonfallScreen.Options);
        }

        if (!small)
        {
            CrestRule(m, cx, y0 + 468, 320, false, 0.36);
        }

        QuickSettings(m, x0 + (small ? 32 : 76), x1 - (small ? 32 : 76), y0 + (small ? 286 : 502), small ? 30 : 36, small);
        if (!small)
        {
            MenuText(m, MoonfallFace.Axis, 14, cx, y1 - 46, Strings.MoonfallPausesItself, Ink2, Anchor.Centre, edge: 0f);
        }

        // Outside the window: a click resumes (taken after the window's own entries, so they win where they overlap).
        var mouse = ImGui.GetMousePos();
        var inside = mouse.X >= panelMin.X && mouse.X <= panelMax.X && mouse.Y >= panelMin.Y && mouse.Y <= panelMax.Y;
        if (!inside && !held && ImGui.IsMouseReleased(ImGuiMouseButton.Left) && ImGui.IsWindowHovered() && !ImGui.IsAnyItemActive()
            && mouse.X >= boardOrigin.X && mouse.X <= boardOrigin.X + boardSize.X && mouse.Y >= boardOrigin.Y && mouse.Y <= boardOrigin.Y + boardSize.Y)
        {
            pause.TryResume();
        }
    }

    /// <summary>Leave's label: where the board is left for (the map in Adventure).</summary>
    private string LeaveLabel() => playKind switch
    {
        MoonfallPlayKind.QuickPlay => Strings.MoonfallLeaveToQuickPlay,
        MoonfallPlayKind.Challenge => Strings.MoonfallLeaveChallenge,
        MoonfallPlayKind.Duel => Strings.MoonfallLeaveDuel,
        _ => Strings.MoonfallLeaveToMap,
    };

    private void RefreshPauseLine(MoonfallGame g)
    {
        var reason = pause.Held ? pause.Shown : MoonfallPauseReason.None;
        var key = (levelIndex, g.BallsLeft, g.OrangesLeft, reason, Localization.Loc.Version);
        if (key == pauseLineFor)
        {
            return;
        }

        pauseLineFor = key;
        pauseLine = pause.Held && PauseReasonText() is { } why
            ? string.Format(CultureInfo.CurrentCulture, Strings.MoonfallPauseHeldFormat, why)
            : string.Format(CultureInfo.CurrentCulture, Strings.MoonfallPauseLineFormat, stageText, g.Level.Name, g.BallsLeft, g.OrangesLeft);
    }

    /// <summary>The quick settings: Reduce motion and Peg marks as switches, Decoration and Sound between chevrons.</summary>
    private void QuickSettings(in MenuPen m, double lx, double rx, double y, double step, bool small)
    {
        var size = small ? 13f : 16f;
        var accent = MoonfallCards.For(game?.Power ?? MoonfallPower.None)?.Accent ?? MoonfallColor.Hex("#E69461");
        MenuText(m, MoonfallFace.Axis, size, lx, y, Strings.MoonfallReduceMotion, Cream, edge: 0f);
        if (MenuSwitch(m, "##mfReduce", rx, y, UiMetrics.ReduceMotion, small, accent))
        {
            SetReduceMotion(!UiMetrics.ReduceMotion);
        }

        y += step;
        MenuText(m, MoonfallFace.Axis, size, lx, y, Strings.MoonfallPegMarks, Cream, edge: 0f);
        if (MenuSwitch(m, "##mfMarks", rx, y, options?.PegMarks == true, small, accent))
        {
            SetPegMarks(options?.PegMarks != true);
        }

        y += step;
        MenuText(m, MoonfallFace.Axis, size, lx, y, Strings.MoonfallDecoration, Cream, edge: 0f);
        var d = MenuStepper(m, "##mfDecoration", rx, y, DecorationName(decoration), small, decoration != Flair.Plain, decoration != Flair.Full);
        if (d != 0)
        {
            SetDecoration(d);
        }

        y += step;
        MenuText(m, MoonfallFace.Axis, size, lx, y, Strings.MoonfallSound, Cream, edge: 0f);
        var s = MenuStepper(m, "##mfSound", rx, y, HasSound ? SoundValueText() : Strings.MoonfallSoundNone, small, HasSound && SoundPercent > 0, HasSound && SoundPercent < 100);
        if (s != 0)
        {
            SoundStep(s);
        }
    }

    private static string DecorationName(Flair flair) => flair switch
    {
        Flair.Plain => Strings.MoonfallDecorationOff,
        Flair.Quiet => Strings.MoonfallDecorationSimple,
        _ => Strings.MoonfallDecorationFull,
    };

    /// <summary>Decoration a step down (−1: Full, Simple, Off) or up (+1).</summary>
    private void SetDecoration(int step)
    {
        if (options is null)
        {
            return;
        }

        // Plain (Off) < Quiet (Simple) < Full.
        var order = decoration switch { Flair.Plain => 0, Flair.Quiet => 1, _ => 2 };
        order = Math.Clamp(order + step, 0, 2);
        options.Decoration = order switch { 0 => Flair.Plain, 1 => Flair.Quiet, _ => Flair.Full };
        options.Save();
        decoration = options.Decoration;
        motion = MoonfallMotion.For(decoration, UiMetrics.ReduceMotion);
    }

    private void SetReduceMotion(bool on)
    {
        if (options is null)
        {
            return;
        }

        options.ReduceMotion = on;
        options.Save();
    }

    private void SetPegMarks(bool on)
    {
        if (options is null)
        {
            return;
        }

        options.PegMarks = on;
        options.PegMarksHintSeen = true;
        options.Save();
    }

    // ---- Options ----

    private void DrawOptions(in MenuPen m)
    {
        JewelNight(m);
        var small = m.Small;
        var (x0, y0, x1, y1) = small ? (20.0, 54.0, 620.0, 470.0) : (240.0, 120.0, 1040.0, 740.0);
        if (small)
        {
            if (MenuButton(m, "##mfBack", 10, 10, 78, 38, Strings.MoonfallBack, 13.7f, primaryFace: false, isDefault: true))
            {
                Back();
            }

            MenuTitle(m, 92, 25, Strings.MoonfallScreenOptions, 34);
        }
        else
        {
            if (MenuButton(m, "##mfBack", 40, 30, 150, 66, Strings.MoonfallBack, 18.7f, primaryFace: false, isDefault: true))
            {
                Back();
            }

            MenuTitle(m, 176, 50, Strings.MoonfallScreenOptions, 58);
            MenuText(m, MoonfallFace.Axis, 15, 178, 86, Strings.MoonfallOptionsLine, Ink2, edge: 1f, maxWidth: 1000);
        }

        Panel(m, x0, y0, x1, y1, null, small ? 0.36 : 0.5);
        var lx = x0 + (small ? 28 : 60);
        var rx = x1 - (small ? 28 : 60);
        var y = y0 + (small ? 40 : 70);
        var step = small ? 92.0 : 132.0;
        var label = small ? 17f : 22f;
        var note = small ? 12f : 14.5f;
        var accent = MoonfallColor.Hex("#E69461");
        var width = (float)(rx - lx - (small ? 150 : 200));

        // Decoration.
        MenuText(m, MoonfallFace.Jupiter, label, lx, y, Strings.MoonfallDecoration, Cream, edge: 1f);
        var d = MenuStepper(m, "##mfOptDecoration", rx, y, DecorationName(decoration), small, decoration != Flair.Plain, decoration != Flair.Full);
        if (d != 0)
        {
            SetDecoration(d);
        }

        MenuText(m, MoonfallFace.Axis, note, lx, y + (small ? 22 : 30), Strings.MoonfallDecorationNote, Ink2, edge: 0f, maxWidth: (float)(rx - lx));
        y += step;

        // Reduce motion.
        MenuText(m, MoonfallFace.Jupiter, label, lx, y, Strings.MoonfallReduceMotion, Cream, edge: 1f);
        if (MenuSwitch(m, "##mfOptReduce", rx, y, UiMetrics.ReduceMotion, small, accent))
        {
            SetReduceMotion(!UiMetrics.ReduceMotion);
        }

        MenuText(m, MoonfallFace.Axis, note, lx, y + (small ? 22 : 30), Strings.MoonfallReduceMotionNote, Ink2, edge: 0f, maxWidth: (float)(rx - lx));
        y += step;

        // Sound.
        MenuText(m, MoonfallFace.Jupiter, label, lx, y, Strings.MoonfallSound, Cream, edge: 1f);
        var s = MenuStepper(m, "##mfOptSound", rx, y, HasSound ? SoundValueText() : Strings.MoonfallSoundNone, small, HasSound && SoundPercent > 0, HasSound && SoundPercent < 100);
        if (s != 0)
        {
            SoundStep(s);
        }

        MenuText(m, MoonfallFace.Axis, note, lx, y + (small ? 22 : 30), Strings.MoonfallSoundNote, Ink2, edge: 0f, maxWidth: (float)(rx - lx));
        y += step;

        // Peg marks, with the marks themselves on the four kinds of peg.
        MenuText(m, MoonfallFace.Jupiter, label, lx, y, Strings.MoonfallPegMarks, Cream, edge: 1f);
        if (MenuSwitch(m, "##mfOptMarks", rx, y, options?.PegMarks == true, small, accent))
        {
            SetPegMarks(options?.PegMarks != true);
        }

        MenuText(m, MoonfallFace.Axis, note, lx, y + (small ? 22 : 30), Strings.MoonfallPegMarksTooltip, Ink2, edge: 0f, maxWidth: width);
        PegMarkSamples(m, rx - (small ? 120 : 170), y + (small ? 26 : 36), small ? 9f : 12f);
        _ = y1;
    }

    /// <summary>The four kinds of peg with their marks (blue plain), as the board would show them with Peg marks on.</summary>
    private void PegMarkSamples(in MenuPen m, double x, double y, float r)
    {
        if (!m.HasArt)
        {
            return;
        }

        ReadOnlySpan<PegColour> kinds = [PegColour.Orange, PegColour.Green, PegColour.Purple, PegColour.Blue];
        var marks = options?.PegMarks == true ? gameArt?.Marks : null;
        for (var i = 0; i < kinds.Length; i++)
        {
            var px = x + (i * r * 3.2);
            Put(m.A, m.A.Atlas.Peg(kinds[i], i, false), px, y, r / 10f, uint.MaxValue);
            var mark = MoonfallPegMarks.For(kinds[i]);
            if (marks is null || mark == MoonfallPegMark.None)
            {
                continue;
            }

            var min = m.V.Map(px - r, y - r);
            var max = m.V.Map(px + r, y + r);
            if (mark == MoonfallPegMark.Star)
            {
                var (r0, r1) = MoonfallPegMarks.Uv(MoonfallPegMark.StarRim);
                m.Dl.AddImage(marks.Handle, min, max, r0, r1, Theme.WithAlpha(new Vector4(MoonfallPegMarks.RimInk.X, MoonfallPegMarks.RimInk.Y, MoonfallPegMarks.RimInk.Z, 1), MoonfallPegMarks.RimInk.W));
            }

            var (uv0, uv1) = MoonfallPegMarks.Uv(mark);
            m.Dl.AddImage(marks.Handle, min, max, uv0, uv1, Theme.WithAlpha(new Vector4(MoonfallPegMarks.Ink.X, MoonfallPegMarks.Ink.Y, MoonfallPegMarks.Ink.Z, 1), MoonfallPegMarks.Ink.W));
        }
    }

    /// <summary>
    /// The Peg marks hint (decision 27: off by default, with a first-run hint): a small plate at the foot of the title
    /// until it is answered, Turn on or No thanks.
    /// </summary>
    private void PegMarksHint(in MenuPen m)
    {
        if (options is null || options.PegMarksHintSeen)
        {
            return;
        }

        var small = m.Small;
        var (x0, y0, x1, y1) = small ? (14.0, 390.0, 204.0, 452.0) : (100.0, 700.0, 640.0, 760.0);
        m.Dl.AddRectFilled(m.V.Map(x0, y0), m.V.Map(x1, y1), Ink(MoonfallColor.Hex("#070A1C"), 0.92f), m.V.Size(4));
        GiltBand(m.C, x0, y0, x1, y1, 0.22);
        MenuText(m, MoonfallFace.Axis, small ? 12 : 14, x0 + 12, y0 + (small ? 14 : 18), small ? Strings.MoonfallPegMarksHintShort : Strings.MoonfallPegMarksHint, Cream, edge: 0f, maxWidth: (float)(x1 - x0 - 24));
        var by = small ? y0 + 30 : y0 + 30;
        var bw = small ? 80.0 : 120.0;
        if (MenuButton(m, "##mfMarksOn", x0 + 12, by, x0 + 12 + bw, by + (small ? 26 : 24), Strings.MoonfallTurnOn, small ? 12 : 14, primaryFace: false))
        {
            SetPegMarks(true);
        }

        if (MenuButton(m, "##mfMarksNo", x0 + 22 + bw, by, x0 + 22 + (2 * bw), by + (small ? 26 : 24), Strings.MoonfallNoThanks, small ? 12 : 14, primaryFace: false))
        {
            options.PegMarksHintSeen = true;
            options.Save();
        }
    }
}
