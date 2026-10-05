using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Ui;

/// <summary>
/// The tally in the rich chrome (spec-rich2.md §4, play2.tally): the board dimmed under a journal-framed window, LEVEL
/// CLEAR on the laurel ribbon as large as the level's name, the rows in Jupiter with their numbers in TrumpGothic, the
/// total in gilt counting up, the ACED and NEW BEST callout (a lit moon, ACED and NEW BEST in gilt, the ace score),
/// the companion's medallion, and the buttons as the Gold Saucer's gilt pills. Two layouts: the 1280 window's and the
/// small window's, whose sizes hold the text floors.
/// </summary>
public sealed partial class MoonfallWindow
{
    private string tallySub = string.Empty;
    private (int Level, int Balls, int Language) tallySubFor = (-1, -1, -1);
    private string aceLine = string.Empty;
    private long aceLineFor = -1;
    private string usesLine = string.Empty;
    private (MoonfallPower Power, int Uses, int Language) usesFor = (MoonfallPower.None, -1, -1);

    /// <summary>
    /// The level's win, recorded in the progress (its best, cleared, aced: <see cref="MoonfallProgress.RecordLevel"/>) and
    /// shown on the tally: ACED (the level's Ace score reached, <see cref="MoonfallAces"/>) and NEW BEST (an earlier best
    /// beaten; a first win sets the best without the callout).
    /// </summary>
    private void NoteWin(MoonfallGame g)
    {
        var level = campaigns[campaign].Levels[levelIndex];
        var total = g.Tally?.Total ?? g.Score;
        var ace = AceFor(level.Id);
        var before = progress.Best(level.Id);
        wonAced = ace is { } target && total >= target;
        wonNewBest = before > 0 && total > before;
        unsaved |= progress.RecordLevel(level.Id, won: true, total, ace);
    }

    /// <summary>The level's Ace score (the shipped table; the offline renderer stages its own).</summary>
    internal Func<string?, long?> AceFor { get; set; } = MoonfallAces.For;

    private void RichEnd(ImDrawListPtr dl, Vector2 origin, Vector2 size, float scale, MoonfallGame g, MoonfallChromeSheet sheet, ImTextureID ui, in ArtPen boardPen)
    {
        var won = g.Phase == MoonfallPhase.Won;
        var v = new View(origin, scale, 1f, BoardCentre);
        var c = new ChromePen(dl, v, sheet, ui);
        var p = boardPen with { View = v };
        var small = scale < 1f;
        var companion = MoonfallCards.For(g.Power);
        var accent = companion?.Accent ?? MoonfallColor.Hex("#FFB45E");
        dl.AddRectFilled(origin, origin + size, Ink(MoonfallColor.Hex("#03040C"), 0.6f));

        // The window: enamel in the companion's colour under the journal's frame.
        double x0 = small ? 118 : 181, y0 = small ? 54 : 110, x1 = small ? 682 : 619, y1 = small ? 590 : 580;
        if (!won)
        {
            y1 = y0 + (small ? 300 : 260);
        }

        dl.AddRectFilledMultiColor(v.Map(x0, y0), v.Map(x1, y1), Ink(MoonfallColor.Hex("#22357A"), 0.97f), Ink(MoonfallColor.Hex("#1B2A63"), 0.97f),
            Ink(MoonfallColor.Hex("#0A1030"), 0.97f), Ink(MoonfallColor.Hex("#121C48"), 0.97f));
        dl.AddRectFilledMultiColor(v.Map(x0, y0), v.Map(x1, y1), Ink(accent, 0f), Ink(accent, 0f), Ink(accent, 0.10f), Ink(accent, 0f));
        GiltFrame(c, x0, y0, x1, y1, small ? 0.40 : 0.36);
        var cx = (x0 + x1) / 2;
        Banner(c, cx, y0 + 3, won ? Strings.MoonfallBannerLevelClear : Strings.MoonfallBannerOutOfBalls, small ? 35f : 33f, null, 0, accent, 1f, 1f, laurel: true, x1 - x0 + 120, 4f);

        var level = campaigns[campaign].Levels[levelIndex];
        var titleY = y0 + (small ? 60 : 57);
        DrawText(dl, MoonfallFace.Jupiter, NamePx(v, small ? 40 : 37.5f, MoonfallFace.Jupiter), v.Map(cx, titleY), Anchor.Centre, Ink(GoldHiInk), level.Name, Ink(MoonfallColor.Hex("#1A0F04")), v.Size(1.4));
        if (tallySubFor != (levelIndex, g.BallsLeft, Localization.Loc.Version))
        {
            tallySubFor = (levelIndex, g.BallsLeft, Localization.Loc.Version);
            tallySub = string.Format(CultureInfo.CurrentCulture, Strings.MoonfallTallyStageFormat, Strings.MoonfallCampaignName(campaign), stageText, g.BallsLeft);
        }

        DrawText(dl, MoonfallFace.Axis, NamePx(v, small ? 15 : 11.25f, MoonfallFace.Axis), v.Map(cx, titleY + (small ? 27 : 24)), Anchor.Centre, Ink(Ink2), tallySub);
        var ruleY = titleY + (small ? 46 : 43);
        if (sheet[MoonfallChromePart.ShortRule] is { } rule)
        {
            var rw = small ? 380 : 330;
            Part(c, MoonfallChromePart.ShortRule, cx - (rw / 2.0), ruleY - (rule.H * 0.2), cx + (rw / 2.0), ruleY + (rule.H * 0.2), uint.MaxValue, u0: 20, u1: 60);
            if (sheet[MoonfallChromePart.Crest] is { } crest)
            {
                var cw = crest.W * 0.3;
                var ch = crest.H * 0.3;
                Part(c, MoonfallChromePart.Crest, cx - (cw / 2), ruleY - ch + (rule.H * 0.22), cx + (cw / 2), ruleY + (rule.H * 0.22), uint.MaxValue);
            }
        }

        var y = ruleY + (small ? 30 : 26);
        var rowStep = small ? 31f : 28.5f;
        var lx = x0 + (small ? 42 : 57);
        var rx = x1 - (small ? 42 : 57);
        if (won && g.Tally is { } tally)
        {
            RefreshTallyLines(g, tally);
            for (var k = 0; k < 6; k += 2)
            {
                DrawText(dl, MoonfallFace.Jupiter, NamePx(v, small ? 23.75f : 20f, MoonfallFace.Jupiter), v.Map(lx, y), Anchor.Left, Ink(Cream), tallyLines[k], Ink(EdgeInk), v.Size(0.8));
                DrawText(dl, MoonfallFace.Trump, NumberPx(v, small ? 22.5f : 19.5f, MoonfallFace.Trump), v.Map(rx, y), Anchor.Right, Ink(Cream), tallyLines[k + 1], Ink(EdgeInk), v.Size(0.6));
                y += rowStep;
            }

            if (sheet[MoonfallChromePart.ShortRule] is { } line)
            {
                Part(c, MoonfallChromePart.ShortRule, lx - 8, y - 12 - (line.H * 0.18), rx + 8, y - 12 + (line.H * 0.18), uint.MaxValue, u0: 20, u1: 60);
            }

            y += small ? 12 : 10;
            DrawText(dl, MoonfallFace.Jupiter, NamePx(v, small ? 32.5f : 28.5f, MoonfallFace.Jupiter), v.Map(lx, y), Anchor.Left, Ink(GoldHiInk), tallyLines[6], Ink(MoonfallColor.Hex("#140A02")), v.Size(1.2));
            Put(p, p.Atlas[MoonfallSprite.Soft], rx - 40, y, 34f / 4f, Ink(MoonfallColor.Hex("#FFB45E"), 0.16f));
            DrawText(dl, MoonfallFace.Trump, NumberPx(v, small ? 37.5f : 33f, MoonfallFace.Trump), v.Map(rx, y), Anchor.Right, Ink(GoldHiInk), tallyLines[7], Ink(MoonfallColor.Hex("#140A02")), v.Size(1.0));
            y += small ? 52 : 48;
            Callout(c, p, g, level, lx, rx, y, small, accent);
        }
        else
        {
            DrawText(dl, MoonfallFace.Axis, NamePx(v, small ? 18 : 15, MoonfallFace.Axis), v.Map(cx, y + 10), Anchor.Centre, Ink(Ink2),
                string.Format(CultureInfo.CurrentCulture, Strings.MoonfallOrangesLeftFormat, g.OrangesLeft), Ink(EdgeInk), v.Size(0.6));
        }

        // The way on: Next (focused) when there is one, and this level again.
        var next = levelIndex + 1;
        var last = won && next >= campaigns[campaign].Levels.Count;
        var bh = small ? 37.5 : 31.5;
        var by0 = y1 - bh - (small ? 20 : 16.5);
        if (won && !last)
        {
            var split = x0 + ((x1 - x0) * 0.42);
            if (PillButton(c, x0 + 37, by0, split - 6, by0 + bh, Strings.MoonfallPlayAgain, "##moonfallAgainRich", focus: false))
            {
                Go(RestartChoice);
            }

            if (PillButton(c, split + 6, by0, x1 - 37, by0 + bh, Strings.MoonfallNextLevel, "##moonfallNextRich", focus: true))
            {
                Go(next);
            }
        }
        else if (PillButton(c, cx - 110, by0, cx + 110, by0 + bh, won ? Strings.MoonfallPlayAgain : Strings.MoonfallTryAgain, "##moonfallAgainRich", focus: true))
        {
            Go(RestartChoice);
        }

        if (last)
        {
            DrawText(dl, MoonfallFace.Axis, NamePx(v, small ? 14 : 11.25f, MoonfallFace.Axis), v.Map(cx, by0 - 12), Anchor.Centre, Ink(Ink2), Strings.MoonfallLastLevel);
        }
    }

    private void RefreshTallyLines(MoonfallGame g, MoonfallTally tally)
    {
        if (tallyFor != tally || tallyKeyText != Strings.MoonfallTallyTotal)
        {
            tallyFor = tally;
            tallyKeyText = Strings.MoonfallTallyTotal;
            tallyShown = g.ShownScore;
            tallyLines =
            [
                Strings.MoonfallTallyLevel, tally.LevelScore.ToString("N0", CultureInfo.CurrentCulture),
                Strings.MoonfallTallyFullMoon, tally.FeverBonus.ToString("N0", CultureInfo.CurrentCulture),
                string.Format(CultureInfo.CurrentCulture, Strings.MoonfallTallyBallsFormat, tally.BallsLeft), tally.BallBonus.ToString("N0", CultureInfo.CurrentCulture),
                Strings.MoonfallTallyTotal, g.ShownScore.ToString("N0", CultureInfo.CurrentCulture),
            ];
        }

        // The total counts up with the score counter (shown at once under Reduce motion, as the counter is).
        if (tallyShown != g.ShownScore)
        {
            tallyShown = g.ShownScore;
            tallyLines[7] = tallyShown.ToString("N0", CultureInfo.CurrentCulture);
        }
    }

    /// <summary>The ACED and NEW BEST callout (a lit moon, the words in gilt, the ace score) beside the companion's medallion.</summary>
    private void Callout(in ChromePen c, in ArtPen p, MoonfallGame g, MoonfallLevel level, double lx, double rx, double cy, bool small, Vector3 accent)
    {
        var dl = c.Dl;
        var v = c.View;
        if (wonAced || wonNewBest)
        {
            var w = small ? 250 : 202;
            var plate0 = v.Map(lx - 12, cy - 22);
            var plate1 = v.Map(lx - 12 + w, cy + 22);
            dl.AddRectFilled(plate0, plate1, Ink(MoonfallColor.Hex("#2A1206"), 0.85f));
            GiltBand(c, lx - 12, cy - 22, lx - 12 + w, cy + 22, 0.22);
            Put(p, p.Atlas[MoonfallSprite.Halo], lx + 15, cy, 1.4f, Ink(MoonfallColor.Hex("#FFB070"), 0.8f));
            Put(p, p.Atlas.Peg(PegColour.Orange, 1, true), lx + 15, cy, 1.3f, uint.MaxValue);
            var x = lx + 37;
            if (wonAced)
            {
                var aw = DrawText(dl, MoonfallFace.Trump, NumberPx(v, small ? 25 : 24, MoonfallFace.Trump), v.Map(x, cy - 6), Anchor.Left, Ink(GoldHiInk), Strings.MoonfallAced, Ink(MoonfallColor.Hex("#140A02")), v.Size(0.9));
                x += (aw / v.Scale) + 14;
            }

            if (wonNewBest)
            {
                DrawText(dl, MoonfallFace.Trump, NumberPx(v, small ? 18.75f : 16.5f, MoonfallFace.Trump), v.Map(x, cy - 6), Anchor.Left, Ink(Tint(accent, 0.2f)), Strings.MoonfallNewBest, Ink(MoonfallColor.Hex("#140A02")), v.Size(0.8));
            }

            if (AceFor(level.Id) is { } ace)
            {
                if (aceLineFor != ace)
                {
                    aceLineFor = ace;
                    aceLine = string.Format(CultureInfo.CurrentCulture, Strings.MoonfallAceScoreFormat, ace.ToString("N0", CultureInfo.CurrentCulture));
                }

                DrawText(dl, MoonfallFace.Axis, NamePx(v, small ? 15 : 10.5f, MoonfallFace.Axis), v.Map(lx + 37, cy + 12), Anchor.Left, Ink(Ink2), aceLine);
            }
        }

        if (MoonfallCards.For(g.Power) is not { } companion)
        {
            return;
        }

        var mr = small ? 20f : 19.5f;
        var mx = rx - mr - 6;
        Put(p, p.Atlas[MoonfallSprite.Soft], mx, cy, mr * 1.9f / 4f, Ink(companion.Accent, 0.40f));
        dl.AddCircleFilled(v.Map(mx, cy), v.Size(mr * 1.04f), Ink(PlateInk), 32);
        if (gameArt?.Card(g.Power) is { } card)
        {
            var (fx, fy, fs) = companion.Face;
            var r = mr * 1.04f;
            dl.AddImageRounded(card.Handle, v.Map(mx - r, cy - r), v.Map(mx + r, cy + r),
                new Vector2(fx / (float)MoonfallCards.CardWidth, fy / (float)MoonfallCards.CardHeight),
                new Vector2((fx + fs) / (float)MoonfallCards.CardWidth, (fy + fs) / (float)MoonfallCards.CardHeight), uint.MaxValue, v.Size(r));
        }

        GiltRing(c, mx, cy, mr);
        if (!small)
        {
            var uses = (uint)g.Power < (uint)powerUses.Length ? powerUses[(int)g.Power] : 0;
            if (usesFor != (g.Power, uses, Localization.Loc.Version))
            {
                usesFor = (g.Power, uses, Localization.Loc.Version);
                usesLine = string.Create(CultureInfo.CurrentCulture, $"{Strings.MoonfallPowerName(g.Power)} ×{uses}");
            }

            DrawText(dl, MoonfallFace.Jupiter, NamePx(v, 17, MoonfallFace.Jupiter), v.Map(mx - mr - 10, cy - 7), Anchor.Right, Ink(Cream), Strings.MoonfallCompanionName(g.Power), Ink(EdgeInk), v.Size(0.8));
            DrawText(dl, MoonfallFace.Axis, NamePx(v, 10.5f, MoonfallFace.Axis), v.Map(mx - mr - 10, cy + 9), Anchor.Right, Ink(Tint(companion.Accent, 0.3f)), usesLine);
        }
    }

    /// <summary>A button drawn as the Gold Saucer's gilt pill with its label in Jupiter; true when clicked. Focus adds the game's warm selection glow.</summary>
    private bool PillButton(in ChromePen c, double x0, double y0, double x1, double y1, string label, string id, bool focus)
    {
        var v = c.View;
        var min = v.Map(x0, y0);
        var max = v.Map(x1, y1);
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, Vector2.Max(max - min, Vector2.One));
        var hovered = ImGui.IsItemHovered();
        if (focus || hovered)
        {
            c.Dl.AddRectFilled(min - new Vector2(v.Size(6)), max + new Vector2(v.Size(6)), Ink(MoonfallColor.Hex(focus ? "#FFCF7A" : "#9DC0FF"), hovered ? 0.30f : 0.22f), v.Size((y1 - y0) / 2 + 6));
        }

        Pill(c, x0, y0, x1, y1);
        var h = y1 - y0;
        DrawText(c.Dl, MoonfallFace.Jupiter, NamePx(v, (float)(h * 0.62), MoonfallFace.Jupiter), v.Map((x0 + x1) / 2, (y0 + y1) / 2), Anchor.Centre, Ink(Cream), label, Ink(MoonfallColor.Hex("#0A0F2A")), v.Size(1.0));
        return clicked;
    }
}
