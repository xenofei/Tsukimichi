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
/// total in gilt counting up, the ACED and NEW BEST callout (a lit moon, ACED and NEW BEST in gilt, the ace score), the
/// companion's medallion, and the buttons as the Gold Saucer's gilt pills: Replay, the mode's own screen (Map for
/// Adventure) and Next, focused. A duel's tally sets the two sides' scores; a challenge's says where the run stands.
/// The level is recorded once, as it ends (<see cref="MoonfallModes.FinishLevel"/>, in MoonfallWindow.Flow.cs). Two
/// layouts: the 1280 window's and the small window's, whose sizes hold the text floors.
/// </summary>
public sealed partial class MoonfallWindow
{
    private string tallySub = string.Empty;
    private (int Level, int Balls, int Language) tallySubFor = (-1, -1, -1);
    private string aceLine = string.Empty;
    private long aceLineFor = -1;
    private string usesLine = string.Empty;
    private string ballsCountLine = string.Empty;
    private (MoonfallPower Power, int Uses, int Language) usesFor = (MoonfallPower.None, -1, -1);

    /// <summary>The Ace bonus the level earned (0 unless aced), added to the tally's total.</summary>
    private long aceBonus;

    // The tally's words, made once as the level ends (no string a frame).
    private string tallyBanner = string.Empty;
    private string tallyNote = string.Empty;
    private string? tallyNext;
    private string? tallyAgain;
    private string tallyHome = string.Empty;
    private string? tallyNextLabel;
    private string? tallyAgainLabel;
    private string tallyHomeLabel = string.Empty;
    private MoonfallGame? tallyTextsFor;
    private int tallyTextsLanguage = -1;
    private readonly string[] duelRows = new string[4];
    private (long Player, long Opponent) duelRowsFor = (-1, -1);

    /// <summary>The level's Ace score (the shipped table; the offline renderer stages its own).</summary>
    internal Func<string?, long?> AceFor => modes.AceOf;

    /// <summary>Whether the tally reads as a win: the level won, the duel won, the run met (or going on).</summary>
    private bool TallyWon(MoonfallGame g) => duel is { } d
        ? d.Outcome == MoonfallDuelOutcome.Won
        : challengeRun is not null ? runStatus != MoonfallChallengeStatus.Failed && g.Phase == MoonfallPhase.Won
        : g.Phase == MoonfallPhase.Won;

    /// <summary>How many score rows the tally sets (with the total): a won level's three or four; none for the rest, which say a line instead.</summary>
    private int TallyRows(MoonfallGame g) =>
        duel is null && challengeRun is null && g.Phase == MoonfallPhase.Won && g.Tally is not null ? (aceBonus > 0 ? 4 : 3) : 0;

    /// <summary>The tally's banner, note and buttons, made once per level's end (and again if the language changes).</summary>
    private void PrepareTally(MoonfallGame g)
    {
        if (ReferenceEquals(tallyTextsFor, g) && tallyTextsLanguage == Localization.Loc.Version)
        {
            return;
        }

        tallyTextsFor = g;
        tallyTextsLanguage = Localization.Loc.Version;
        var won = g.Phase == MoonfallPhase.Won;
        tallyNext = null;
        tallyNote = string.Empty;
        switch (playKind)
        {
            case MoonfallPlayKind.Duel or MoonfallPlayKind.Challenge when duel is { } d && challengeRun is null:
                tallyBanner = d.Outcome switch
                {
                    MoonfallDuelOutcome.Won => Strings.MoonfallBannerDuelWon,
                    MoonfallDuelOutcome.Lost => Strings.MoonfallBannerDuelLost,
                    _ => Strings.MoonfallBannerDuelDrawn,
                };
                tallyNote = string.Format(CultureInfo.CurrentCulture, Strings.MoonfallDuelAgainstFormat, OpponentName(d), Strings.MoonfallDifficultyName(d.Opponent.Difficulty));
                tallyAgain = Strings.MoonfallRematch;
                tallyHome = Strings.MoonfallScreenDuel;
                break;

            case MoonfallPlayKind.Challenge when challengeRun is { } run:
                tallyBanner = runStatus switch
                {
                    MoonfallChallengeStatus.Met => Strings.MoonfallBannerChallengeMet,
                    MoonfallChallengeStatus.Failed => Strings.MoonfallBannerChallengeFailed,
                    _ => won || duel is { Outcome: MoonfallDuelOutcome.Won } ? Strings.MoonfallBannerLevelClear : Strings.MoonfallBannerOutOfBalls,
                };
                tallyNote = run.Challenge.Kind == MoonfallChallengeKind.Score
                    ? string.Format(CultureInfo.CurrentCulture, Strings.MoonfallRunScoreFormat, run.Challenge.Name, run.Total.ToString("N0", CultureInfo.CurrentCulture), run.Challenge.Target.ToString("N0", CultureInfo.CurrentCulture))
                    : string.Format(CultureInfo.CurrentCulture, Strings.MoonfallRunLevelFormat, run.Challenge.Name, run.LevelIndex + (runStatus == MoonfallChallengeStatus.Playing ? 0 : 1), run.Challenge.LevelIds.Count);
                if (runStatus == MoonfallChallengeStatus.Playing)
                {
                    tallyNext = string.Format(CultureInfo.CurrentCulture, Strings.MoonfallRunNextFormat, run.LevelIndex + 1, run.Challenge.LevelIds.Count);
                    tallyAgain = null;
                }
                else
                {
                    tallyAgain = Strings.MoonfallTryAgain;
                }

                tallyHome = Strings.MoonfallScreenChallenges;
                break;

            default:
                tallyBanner = won ? Strings.MoonfallBannerLevelClear : Strings.MoonfallBannerOutOfBalls;
                tallyAgain = won ? Strings.MoonfallReplay : Strings.MoonfallTryAgain;
                tallyHome = playKind == MoonfallPlayKind.QuickPlay ? Strings.MoonfallScreenQuickPlay : Strings.MoonfallMap;
                if (!won)
                {
                    tallyNote = string.Format(CultureInfo.CurrentCulture, Strings.MoonfallOrangesLeftFormat, g.OrangesLeft);
                }

                if (HasNext(g))
                {
                    tallyNext = playKind == MoonfallPlayKind.Adventure
                        ? string.Format(CultureInfo.CurrentCulture, Strings.MoonfallNextCodeFormat, LevelCode(levelIndex + 1))
                        : Strings.MoonfallNextLevel;
                }
                else if (won && playKind == MoonfallPlayKind.Adventure)
                {
                    tallyNote = Strings.MoonfallLastLevel;
                }

                break;
        }

        // The buttons' labels with their ImGui ids, made here once.
        tallyNextLabel = tallyNext is null ? null : tallyNext + "##moonfallNext";
        tallyAgainLabel = tallyAgain is null ? null : tallyAgain + "##moonfallAgain";
        tallyHomeLabel = tallyHome + "##moonfallHome";
    }

    /// <summary>A level's code in its campaign, "3-4".</summary>
    private static string LevelCode(int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{MoonfallCharacters.Stage(index)}-{MoonfallCharacters.LevelInStage(index)}");

    private static string OpponentName(MoonfallDuel d) =>
        MoonfallCompanions.TryGet(d.Companion(MoonfallDuel.OpponentSide), out var info) ? info.Name : string.Empty;

    private void RichEnd(ImDrawListPtr dl, Vector2 origin, Vector2 size, Vector2 areaMin, Vector2 areaMax, float scale, MoonfallGame g, MoonfallChromeSheet sheet,
        ImTextureID ui, in ArtPen boardPen)
    {
        PrepareTally(g);
        var won = TallyWon(g);
        var small = scale < 1f;
        var power = duel?.Companion(MoonfallDuel.PlayerSide) is { } pc && pc != MoonfallCompanion.None && MoonfallCompanions.TryGet(pc, out var pi) ? pi.Power : g.Power;
        var companion = MoonfallCards.For(power);
        var accent = companion?.Accent ?? MoonfallColor.Hex("#FFB45E");
        dl.AddRectFilled(origin, origin + size, Ink(MoonfallColor.Hex("#03040C"), 0.6f));

        // The window fits its rows (no dead space), and in a small window it is set at the approved 640 size (0.8 px a
        // unit) when the window has the room, not at the board's scale: it is a modal panel, not part of the board.
        var rows = TallyRows(g);
        var duelRowsShown = duel is not null;
        double x0 = small ? 118 : 181, x1 = small ? 682 : 619;
        var titleOffset = small ? 60.0 : 57.0;
        var ruleOffset = titleOffset + (small ? 46 : 43);
        var rowsOffset = ruleOffset + (small ? 30 : 26);
        var rowStep = small ? 31f : 28.5f;
        var shownRows = rows > 0 ? rows : duelRowsShown ? 2 : 1;
        var totalOffset = rowsOffset + (shownRows * rowStep) + (small ? 12 : 10);
        var calloutOffset = totalOffset + (small ? 54 : 48);
        var bh = small ? 37.5 : 31.5;
        var height = rows > 0 ? calloutOffset + (small ? 26 : 22) + 26 + bh + (small ? 20 : 16.5) : rowsOffset + (shownRows * rowStep) + (small ? 40 : 34) + bh + (small ? 20 : 16.5);
        var y0 = 300 - (height / 2) + 8;
        var y1 = y0 + height;
        var tv = scale;
        if (small)
        {
            var area = areaMax - areaMin;
            tv = MathF.Max(scale, MathF.Min(0.8f, MathF.Min((area.X - 8) / (float)(x1 - x0 + 40), (area.Y - 8) / (float)(height + 40))));
        }

        // Centred on the board's centre on screen, at the panel's own scale.
        var centre = origin + (size * 0.5f);
        var v = new View(centre - (new Vector2(400f, (float)(y0 + y1) / 2f) * tv), tv, 1f, BoardCentre);
        var c = new ChromePen(dl, v, sheet, ui);
        var p = boardPen with { View = v };

        dl.AddRectFilledMultiColor(v.Map(x0, y0), v.Map(x1, y1), Ink(MoonfallColor.Hex("#22357A"), 0.97f), Ink(MoonfallColor.Hex("#1B2A63"), 0.97f),
            Ink(MoonfallColor.Hex("#0A1030"), 0.97f), Ink(MoonfallColor.Hex("#121C48"), 0.97f));
        dl.AddRectFilledMultiColor(v.Map(x0, y0), v.Map(x1, y1), Ink(accent, 0f), Ink(accent, 0f), Ink(accent, 0.10f), Ink(accent, 0f));
        GiltFrame(c, x0, y0, x1, y1, small ? 0.40 : 0.36);
        var cx = (x0 + x1) / 2;
        Banner(c, cx, y0 + 3, tallyBanner, small ? 35f : 33f, null, 0, accent, 1f, 1f, laurel: true, x1 - x0 + 120, 4f);

        var level = g.Level;
        var titleY = y0 + titleOffset;
        DrawText(dl, MoonfallFace.Jupiter, NamePx(v, small ? 40 : 37.5f, MoonfallFace.Jupiter), v.Map(cx, titleY), Anchor.Centre, Ink(GoldHiInk), level.Name, Ink(MoonfallColor.Hex("#1A0F04")), v.Size(1.4));
        if (tallySubFor != (levelIndex, g.BallsLeft, Localization.Loc.Version))
        {
            tallySubFor = (levelIndex, g.BallsLeft, Localization.Loc.Version);
            tallySub = string.Format(CultureInfo.CurrentCulture, Strings.MoonfallTallyStageFormat, Strings.MoonfallCampaignName(campaign), stageText, g.BallsLeft);
        }

        var sub = rows > 0 ? tallySub : tallyNote;
        DrawText(dl, MoonfallFace.Axis, NamePx(v, small ? 15 : 11.25f, MoonfallFace.Axis), v.Map(cx, titleY + (small ? 27 : 24)), Anchor.Centre, Ink(Ink2), sub);
        var ruleY = y0 + ruleOffset;
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

        var y = y0 + rowsOffset;
        var lx = x0 + (small ? 42 : 57);
        var rx = x1 - (small ? 42 : 57);
        var labelPx = NamePx(v, small ? 23.75f : 20f, MoonfallFace.Jupiter);
        var numberPx = NumberPx(v, small ? 22.5f : 19.5f, MoonfallFace.Trump);
        if (rows > 0 && g.Tally is { } tally)
        {
            RefreshTallyLines(g, tally);
            var last = tallyLines.Length - 2;
            for (var k = 0; k < last; k += 2)
            {
                var lw = DrawText(dl, MoonfallFace.Jupiter, labelPx, v.Map(lx, y), Anchor.Left, Ink(Cream), tallyLines[k], Ink(EdgeInk), v.Size(0.8));
                if (k == 4)
                {
                    // The balls' count and their worth in the game's sans, lining figures beside the word (Jupiter's
                    // old-style "11" reads as "II").
                    DrawText(dl, MoonfallFace.Axis, NumberPx(v, small ? 17.5f : 15f, MoonfallFace.Axis), v.Map(lx + (lw / v.Scale) + 10, y), Anchor.Left, Ink(Ink2), ballsCountLine, Ink(EdgeInk), v.Size(0.6));
                }

                DrawText(dl, MoonfallFace.Trump, numberPx, v.Map(rx, y), Anchor.Right, Ink(Cream), tallyLines[k + 1], Ink(EdgeInk), v.Size(0.6));
                y += rowStep;
            }

            if (sheet[MoonfallChromePart.ShortRule] is { } line)
            {
                Part(c, MoonfallChromePart.ShortRule, lx - 8, y - 12 - (line.H * 0.18), rx + 8, y - 12 + (line.H * 0.18), uint.MaxValue, u0: 20, u1: 60);
            }

            y = y0 + totalOffset;
            DrawText(dl, MoonfallFace.Jupiter, NamePx(v, small ? 32.5f : 28.5f, MoonfallFace.Jupiter), v.Map(lx, y), Anchor.Left, Ink(GoldHiInk), tallyLines[last], Ink(MoonfallColor.Hex("#140A02")), v.Size(1.2));
            Put(p, p.Atlas[MoonfallSprite.Soft], rx - 40, y, 34f / 4f, Ink(MoonfallColor.Hex("#FFB45E"), 0.16f));
            DrawText(dl, MoonfallFace.Trump, NumberPx(v, small ? 37.5f : 33f, MoonfallFace.Trump), v.Map(rx, y), Anchor.Right, Ink(GoldHiInk), tallyLines[last + 1], Ink(MoonfallColor.Hex("#140A02")), v.Size(1.0));
            Callout(c, p, g, level, lx, rx, y0 + calloutOffset, small, accent);
        }
        else if (duel is { } d)
        {
            // The two sides' scores, counting up; the winner's in gilt.
            RefreshDuelRows(d);
            for (var side = 0; side < 2; side++)
            {
                var lead = d.Outcome == (side == MoonfallDuel.PlayerSide ? MoonfallDuelOutcome.Won : MoonfallDuelOutcome.Lost);
                var ink = Ink(lead ? GoldHiInk : Cream);
                DrawText(dl, MoonfallFace.Jupiter, labelPx, v.Map(lx, y), Anchor.Left, ink, duelRows[side * 2], Ink(EdgeInk), v.Size(0.8));
                DrawText(dl, MoonfallFace.Trump, NumberPx(v, small ? 26f : 23f, MoonfallFace.Trump), v.Map(rx, y), Anchor.Right, ink, duelRows[(side * 2) + 1], Ink(EdgeInk), v.Size(0.6));
                y += rowStep;
            }
        }
        else if (challengeRun is not null)
        {
            DrawText(dl, MoonfallFace.Axis, NamePx(v, small ? 18 : 15, MoonfallFace.Axis), v.Map(cx, y + 6), Anchor.Centre, Ink(Ink2), challengeRun.Challenge.Text, Ink(EdgeInk), v.Size(0.6));
        }
        else
        {
            DrawText(dl, MoonfallFace.Axis, NamePx(v, small ? 18 : 15, MoonfallFace.Axis), v.Map(cx, y + 6), Anchor.Centre, Ink(Ink2), BestLine(level), Ink(EdgeInk), v.Size(0.6));
        }

        // The way on: Replay, the mode's own screen, and Next (focused) when there is one; else Replay is focused.
        var by0 = y1 - bh - (small ? 20 : 16.5);
        var buttons = (tallyNext is not null ? 1 : 0) + (tallyAgain is not null ? 1 : 0) + 1;
        var inner0 = x0 + (small ? 24 : 46);
        var inner1 = x1 - (small ? 24 : 46);
        const double Gap = 10;
        var widths = buttons == 3 ? (ReadOnlySpan<double>)[0.27, 0.25, 0.48] : buttons == 2 ? (ReadOnlySpan<double>)[0.5, 0.5] : [1.0];
        var bx = inner0;
        var room = inner1 - inner0 - (Gap * (buttons - 1));
        var index = 0;
        var nextFocus = tallyNext is not null;
        if (tallyAgainLabel is { } againLabel)
        {
            var w = room * widths[index++];
            if (PillButton(c, bx, by0, bx + w, by0 + bh, tallyAgain!, againLabel, focus: !nextFocus && won))
            {
                SoundClick();
                Restart();
                return;
            }

            bx += w + Gap;
        }

        {
            var w = room * widths[index++];
            if (PillButton(c, bx, by0, bx + w, by0 + bh, tallyHome, tallyHomeLabel, focus: !nextFocus && (!won || tallyAgain is null)))
            {
                SoundClick();
                LeaveBoard();
                return;
            }

            bx += w + Gap;
        }

        if (tallyNextLabel is { } nextLabel)
        {
            var w = room * widths[index];
            if (PillButton(c, bx, by0, bx + w, by0 + bh, tallyNext!, nextLabel, focus: true))
            {
                SoundClick();
                Next();
                return;
            }
        }

        // The next level's scene is built while the tally shows, so Next opens straight onto it.
        if (gameArt is not null && NextLevelToWarm(g) is { } warm)
        {
            gameArt.Warm(warm, scale > ArtTwoXAbove);
        }
    }

    private string bestLine = string.Empty;
    private (string? Id, long Best, int Language) bestLineFor = (null, -1, -1);

    /// <summary>"Best 214,300" for a level lost (its best so far), or nothing when it has none.</summary>
    private string BestLine(MoonfallLevel level)
    {
        var best = progress.Best(level.Id);
        if (bestLineFor != (level.Id, best, Localization.Loc.Version))
        {
            bestLineFor = (level.Id, best, Localization.Loc.Version);
            bestLine = best > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.MoonfallBestFormat, best.ToString("N0", CultureInfo.CurrentCulture)) : string.Empty;
        }

        return bestLine;
    }

    private void RefreshDuelRows(MoonfallDuel d)
    {
        var shown = (d.ShownScore(MoonfallDuel.PlayerSide), d.ShownScore(MoonfallDuel.OpponentSide));
        if (shown == duelRowsFor && duelRows[0] is not null)
        {
            return;
        }

        duelRowsFor = shown;
        duelRows[0] = Strings.MoonfallDuelYou;
        duelRows[1] = shown.Item1.ToString("N0", CultureInfo.CurrentCulture);
        duelRows[2] = OpponentName(d);
        duelRows[3] = shown.Item2.ToString("N0", CultureInfo.CurrentCulture);
    }

    private void RefreshTallyLines(MoonfallGame g, MoonfallTally tally)
    {
        if (tallyFor != tally || tallyKeyText != Strings.MoonfallTallyTotal)
        {
            tallyFor = tally;
            tallyKeyText = Strings.MoonfallTallyTotal;
            tallyShown = -1;
            tallyLines = aceBonus > 0
                ?
                [
                    Strings.MoonfallTallyLevel, tally.LevelScore.ToString("N0", CultureInfo.CurrentCulture),
                    Strings.MoonfallTallyFullMoon, tally.FeverBonus.ToString("N0", CultureInfo.CurrentCulture),
                    Strings.MoonfallTallyBallsLabel, tally.BallBonus.ToString("N0", CultureInfo.CurrentCulture),
                    Strings.MoonfallTallyAceBonus, aceBonus.ToString("N0", CultureInfo.CurrentCulture),
                    Strings.MoonfallTallyTotal, string.Empty,
                ]
                :
                [
                    Strings.MoonfallTallyLevel, tally.LevelScore.ToString("N0", CultureInfo.CurrentCulture),
                    Strings.MoonfallTallyFullMoon, tally.FeverBonus.ToString("N0", CultureInfo.CurrentCulture),
                    Strings.MoonfallTallyBallsLabel, tally.BallBonus.ToString("N0", CultureInfo.CurrentCulture),
                    Strings.MoonfallTallyTotal, string.Empty,
                ];
            ballsCountLine = string.Format(CultureInfo.CurrentCulture, Strings.MoonfallTallyBallsCountFormat, tally.BallsLeft);
        }

        // The total counts up with the score counter (shown at once under Reduce motion, as the counter is), the Ace
        // bonus with it.
        var total = g.ShownScore + aceBonus;
        if (tallyShown != total)
        {
            tallyShown = total;
            tallyLines[^1] = total.ToString("N0", CultureInfo.CurrentCulture);
        }
    }

    /// <summary>The ACED and NEW BEST callout (a lit moon, the words in gilt, the ace score) beside the companion's medallion.</summary>
    private void Callout(in ChromePen c, in ArtPen p, MoonfallGame g, MoonfallLevel level, double lx, double rx, double cy, bool small, Vector3 accent)
    {
        var dl = c.Dl;
        var v = c.View;
        // Each is shown once the counting total reaches it, so NEW BEST lands as the old best is passed (at once under
        // Reduce motion, where the total does).
        var shown = g.ShownScore + aceBonus;
        var aced = wonAced && AceFor(level.Id) is { } target && shown >= target;
        var newBest = wonNewBest && shown > wonPreviousBest;
        if (aced || newBest)
        {
            var w = small ? 232 : 202;
            var half = small ? 27 : 22;
            var plate0 = v.Map(lx - 12, cy - half);
            var plate1 = v.Map(lx - 12 + w, cy + half);
            dl.AddRectFilled(plate0, plate1, Ink(MoonfallColor.Hex("#2A1206"), 0.85f));
            GiltBand(c, lx - 12, cy - half, lx - 12 + w, cy + half, 0.22);
            Put(p, p.Atlas[MoonfallSprite.Halo], lx + 15, cy, 1.4f, Ink(MoonfallColor.Hex("#FFB070"), 0.8f));
            Put(p, p.Atlas.Peg(PegColour.Orange, 1, true), lx + 15, cy, 1.3f, uint.MaxValue);
            var x = lx + 37;
            if (aced)
            {
                var aw = DrawText(dl, MoonfallFace.Trump, NumberPx(v, small ? 25 : 24, MoonfallFace.Trump), v.Map(x, cy - 6), Anchor.Left, Ink(GoldHiInk), Strings.MoonfallAced, Ink(MoonfallColor.Hex("#140A02")), v.Size(0.9));
                x += (aw / v.Scale) + 14;
            }

            if (newBest)
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

                DrawText(dl, MoonfallFace.Axis, NamePx(v, small ? 15 : 10.5f, MoonfallFace.Axis), v.Map(lx + 37, cy + (small ? 13 : 12)), Anchor.Left, Ink(Ink2), aceLine);
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
        // The companion's name and how often the power fired (left out while it never did).
        var uses = (uint)g.Power < (uint)powerUses.Length ? powerUses[(int)g.Power] : 0;
        if (usesFor != (g.Power, uses, Localization.Loc.Version))
        {
            usesFor = (g.Power, uses, Localization.Loc.Version);
            usesLine = uses > 0 ? string.Create(CultureInfo.CurrentCulture, $"{Strings.MoonfallPowerName(g.Power)} ×{uses}") : Strings.MoonfallPowerName(g.Power);
        }

        // The name and the uses sit left of the medallion, inside the frame (round 3: never touching it).
        DrawText(dl, MoonfallFace.Jupiter, NamePx(v, small ? 19 : 17, MoonfallFace.Jupiter), v.Map(mx - mr - 10, cy - 7), Anchor.Right, Ink(Cream), Strings.MoonfallCompanionName(g.Power), Ink(EdgeInk), v.Size(0.8));
        DrawText(dl, MoonfallFace.Axis, NamePx(v, small ? 13 : 10.5f, MoonfallFace.Axis), v.Map(mx - mr - 10, cy + 10), Anchor.Right, Ink(Tint(companion.Accent, 0.3f)), usesLine);
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
        var navFocus = ImGui.GetIO().NavVisible && ImGui.IsItemFocused();
        if (focus)
        {
            ImGui.SetItemDefaultFocus();
        }

        var lit = navFocus || (focus && !ImGui.GetIO().NavVisible);
        if (lit || hovered)
        {
            c.Dl.AddRectFilled(min - new Vector2(v.Size(6)), max + new Vector2(v.Size(6)), Ink(MoonfallColor.Hex(lit ? "#FFCF7A" : "#9DC0FF"), hovered ? 0.30f : 0.22f), v.Size(((y1 - y0) / 2) + 6));
        }

        Pill(c, x0, y0, x1, y1, uint.MaxValue, lit ? MoonfallChromePart.PillFocus : MoonfallChromePart.Pill);
        var h = y1 - y0;
        DrawText(c.Dl, MoonfallFace.Jupiter, NamePx(v, (float)(h * 0.62), MoonfallFace.Jupiter), v.Map((x0 + x1) / 2, (y0 + y1) / 2), Anchor.Centre, Ink(Cream), label, Ink(MoonfallColor.Hex("#0A0F2A")), v.Size(1.0));
        if (navFocus)
        {
            FocusOutline(c.Dl, min, max, v.Size(h / 2));
        }

        return clicked;
    }
}
