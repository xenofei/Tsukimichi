using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Ui;

/// <summary>
/// The companions (spec-rich2.md §4, screens2.characters): the eleven Triple Triad cards (face up; dimmed with their
/// stage when met and not yet reached; the card back, "Not yet met", when the story has not introduced them, the power
/// still named), the selection glow in the companion's colour, and the detail panel: the hero art, name and role, a line
/// in their voice, where they join, the power with its portrait, what it does and how long it lasts, the power on a real
/// board, the levels won together, and Play with (Quick Play with them). The spoiler shield decides every face
/// (<see cref="MoonfallLooks.Companion"/>); the twins stay face down until Alisaie is met.
/// </summary>
public sealed partial class MoonfallWindow
{
    private int charSel = -1;

    // The grid's and the detail's words, made when the selection, the progress or the layout changes.
    private (int Views, int Sel, bool Small) charWordsKey = (-1, -1, false);
    private readonly string[] charNames = new string[MoonfallCompanions.Count];
    private readonly string[] charSubs = new string[MoonfallCompanions.Count];
    private string charName = string.Empty;
    private string[] charRole = [];
    private string[] charQuote = [];
    private string charJoins = string.Empty;
    private string[] charDoes = [];
    private string charLasts = string.Empty;
    private string charPlay = string.Empty;
    private string charPlayTip = string.Empty;
    private int charWon;

    private static readonly string[] CardIds = MakeIds("##mfCard", MoonfallCompanions.Count);

    private void DrawCharacters(in MenuPen m)
    {
        JewelNight(m);
        Moondust(m, m.Small ? 16 : 30);
        var all = MoonfallCompanions.All;
        if (charSel < 0 || charSel >= all.Count)
        {
            charSel = 0;
            for (var i = 0; i < all.Count; i++)
            {
                if (StateOf(all[i].Companion) == MoonfallCompanionState.Available)
                {
                    charSel = i;
                    break;
                }
            }
        }

        MakeCharacterWords(m);
        var small = m.Small;
        if (small)
        {
            if (MenuButton(m, "##mfBack", 10, 10, 78, 38, Strings.MoonfallBack, 13.7f, primaryFace: false))
            {
                Back();
            }

            MenuTitle(m, 92, 25, Strings.MoonfallScreenCompanions, 34);
        }
        else
        {
            if (MenuButton(m, "##mfBack", 40, 30, 150, 66, Strings.MoonfallBack, 18.7f, primaryFace: false))
            {
                Back();
            }

            MenuTitle(m, 176, 50, Strings.MoonfallScreenCompanions, 58);
            MenuText(m, MoonfallFace.Axis, 15, 178, 86, Strings.MoonfallCompanionsLine, Ink2, edge: 1f, maxWidth: 1060);
        }

        var (cw, gx, gy, x0, y0, nameGap, subGap) = small ? (66.0, 12.0, 8.0, 12.0, 52.0, 32.0, 0.0) : (118.0, 26.0, 20.0, 46.0, 122.0, 58.0, 0.0);
        var chh = cw * MoonfallCards.CardHeight / MoonfallCards.CardWidth;
        _ = subGap;
        for (var i = 0; i < all.Count; i++)
        {
            var (r, col) = Math.DivRem(i, 4);
            var x = x0 + (col * (cw + gx)) + (r < 2 ? 0 : (cw + gx) / 2);
            var y = y0 + (r * (chh + nameGap + gy));
            var look = MoonfallLooks.Companion(all[i], StateOf(all[i].Companion));
            var hit = MenuHit(m, CardIds[i], x - 4, y - 4, x + cw + 4, y + chh + (small ? 30 : 46), out var hovered, out var nav);
            CompanionCard(m, look, x, y, cw, i == charSel, nav || hovered);
            if (look.Face == MoonfallCardFace.Dimmed)
            {
                // Met but not reached: the same padlock as the pickers' drained medallions.
                Padlock(m, x + cw - (small ? 9 : 14), y + chh - (small ? 10 : 16), small ? 5.5 : 8);
            }

            if (nav)
            {
                FocusOutline(m.Dl, m.V.Map(x, y), m.V.Map(x + cw, y + chh), m.V.Size(6));
            }

            var accent = MoonfallCards.For(all[i].Power)?.Accent ?? GoldInk;
            var met = look.Face == MoonfallCardFace.Up;
            var cx = x + (cw / 2);
            MenuText(m, MoonfallFace.Jupiter, small ? 15 : 25, cx, y + chh + (small ? 10 : 16), charNames[i], met ? Cream : Ink2, Anchor.Centre, edge: 1f, maxWidth: (float)(cw + (small ? 10 : 22)));
            MenuText(m, MoonfallFace.Axis, small ? 12 : 13.5f, cx, y + chh + (small ? 24 : 36), charSubs[i], met ? Tint(accent, 0.35f) : Ink2, Anchor.Centre, edge: 1f, maxWidth: (float)(cw + (small ? 10 : 24)));
            if (hit && charSel != i)
            {
                SoundClick();
                charSel = i;
            }
            else if (hit && look.Playable)
            {
                PlayWith(all[i].Companion);
            }
        }

        if (small)
        {
            CharacterDetailSmall(m, 330, 52, 628, 470);
        }
        else
        {
            MenuText(m, MoonfallFace.Axis, 13.5f, 46, 774, Strings.MoonfallCompanionsFootnote, Ink2, edge: 1f, maxWidth: 620);
            CharacterDetail(m, 676, 104, 1244, 764);
        }
    }

    /// <summary>Play with: Quick Play with this companion chosen.</summary>
    private void PlayWith(MoonfallCompanion companion)
    {
        quickCompanion = companion;
        quickCompanionChosen = true;
        Open(MoonfallScreen.QuickPlay);
    }

    private void MakeCharacterWords(in MenuPen m)
    {
        var key = (menuViewsKey.GetHashCode(), charSel, m.Small);
        if (key == charWordsKey)
        {
            return;
        }

        charWordsKey = key;
        var c = CultureInfo.CurrentCulture;
        var all = MoonfallCompanions.All;
        for (var i = 0; i < all.Count; i++)
        {
            var info = all[i];
            var look = MoonfallLooks.Companion(info, StateOf(info.Companion));
            var power = Strings.MoonfallPowerName(info.Power);
            charNames[i] = look.Face == MoonfallCardFace.Back ? (m.Small ? Strings.MoonfallNotMetShort : Strings.MoonfallNotYetMet)
                : m.Small && info.Name.Length > 9 ? ShortName(info.Companion) : info.Name;
            // At 640 the power's name alone, as the player learns it; the stage is the detail panel's and the dimmed card's.
            if (m.Small)
            {
                // Within the card's pitch (the floor stops it shrinking): the full name, else its short form ("Draw").
                var pitch = m.V.Size(66 + 12 - 4);
                var px = NamePx(m.V, 12, MoonfallFace.Axis);
                charSubs[i] = MeasureText(MoonfallFace.Axis, px, power) <= pitch ? power : FitLine(MoonfallFace.Axis, px, ShortPowerName(info.Power), pitch);
                continue;
            }

            charSubs[i] = !look.ShowsStage ? power
                : look.FarShore ? string.Format(c, Strings.MoonfallPowerFarShoreFormat, power)
                : string.Format(c, Strings.MoonfallPowerStageFormat, power, info.Stage);
        }

        var sel = all[charSel];
        var selLook = MoonfallLooks.Companion(sel, StateOf(sel.Companion));
        var lore = MoonfallLooks.LoreOf(sel.Companion);
        var small = m.Small;
        var textWidth = small ? (628f - 16f) - (330f + 20f + 104f + 16f) : 1244f - 34f - (676f + 34f + 190f + 30f);
        charName = selLook.Named ? sel.Name : Strings.MoonfallNotYetMet;
        charRole = Wrap(m, MoonfallFace.Axis, small ? 12 : 15, selLook.Named ? lore.Role : Strings.MoonfallNotMetRole, textWidth);
        charQuote = selLook.Named ? Wrap(m, MoonfallFace.Axis, small ? 12 : 17.5f, "“" + lore.Line + "”", textWidth) : [];
        var stageName = StageNameShown(MoonfallStages.Of(sel.Campaign)[sel.Stage - 1]);
        charJoins = sel.Campaign == MoonfallCampaignKind.Expansion
            ? string.Format(c, Strings.MoonfallJoinsFarShoreFormat, sel.Stage, stageName)
            : string.Format(c, Strings.MoonfallJoinsFormat, sel.Stage, stageName);
        charDoes = Wrap(m, MoonfallFace.Axis, small ? 12.5f : 17, lore.Does, small ? 258f : 280f);
        charLasts = lore.Lasts;
        charWon = 0;
        var stage = (sel.Campaign == MoonfallCampaignKind.Expansion ? farStages : baseStages) is { } list && sel.Stage - 1 < list.Count ? list[sel.Stage - 1] : null;
        if (stage is not null)
        {
            foreach (var slot in stage.Levels)
            {
                charWon += slot.State == MoonfallLevelState.Cleared ? 1 : 0;
            }
        }

        charPlay = string.Format(c, Strings.MoonfallPlayWithFormat, ShortName(sel.Companion));
        charPlayTip = selLook.Face switch
        {
            MoonfallCardFace.Back => Strings.MoonfallPlayWithNotMet,
            MoonfallCardFace.Dimmed => sel.Campaign == MoonfallCampaignKind.Expansion
                ? Strings.MoonfallPlayWithFarShore
                : string.Format(c, Strings.MoonfallPlayWithStageFormat, sel.Stage),
            _ => string.Empty,
        };
    }

    /// <summary>A power's short name, for a 640 card whose pitch the full one overruns ("Draw" for "Moon-Viewing Draw").</summary>
    private static string ShortPowerName(MoonfallPower power) => power switch
    {
        MoonfallPower.Draw => Strings.MoonfallPowerShortDraw,
        MoonfallPower.Burst => Strings.MoonfallPowerShortBurst,
        MoonfallPower.Wings => Strings.MoonfallPowerShortWings,
        MoonfallPower.Path => Strings.MoonfallPowerShortPath,
        MoonfallPower.Bolt => Strings.MoonfallPowerShortBolt,
        _ => Strings.MoonfallPowerName(power),
    };

    /// <summary>The hero image: the art inside the card's gilt border, in the journal's frame, glowing in the companion's colour (the card back when not met).</summary>
    private double Hero(in MenuPen m, MoonfallCompanionInfo info, MoonfallCompanionLook look, double x, double y, double w)
    {
        var dl = m.Dl;
        var v = m.V;
        var bounds = MoonfallCards.ArtBounds;
        var h = w * ((bounds.W - bounds.Y) * MoonfallCards.CardHeight) / ((bounds.Z - bounds.X) * MoonfallCards.CardWidth);
        var accent = MoonfallCards.For(info.Power)?.Accent ?? GoldInk;
        if (m.HasArt && look.Named)
        {
            var breath = MoonfallMotion.Breath(menuClock, 6f, 0.10f, motion != MoonfallMotionLevel.Full);
            Put(m.A, m.A.Atlas[MoonfallSprite.Soft], x + (w / 2), y + (h / 2), (float)(h * 0.62 / 4), Ink(accent, 0.30f * breath));
        }

        dl.AddRectFilled(v.Map(x + 5, y + 8), v.Map(x + w + 5, y + h + 8), Ink(Vector3.Zero, 0.5f), v.Size(4));
        if (!look.Named)
        {
            Part(m.C, MoonfallChromePart.CardBack, x, y, x + w, y + h, uint.MaxValue, u0: 14, v0: 14, u1: 188, v1: 240);
        }
        else if (gameArt?.Card(info.Power) is { } card)
        {
            dl.AddImage(card.Handle, v.Map(x, y), v.Map(x + w, y + h), new Vector2(bounds.X, bounds.Y), new Vector2(bounds.Z, bounds.W));
            if (look.Face == MoonfallCardFace.Dimmed)
            {
                dl.AddRectFilled(v.Map(x, y), v.Map(x + w, y + h), Ink(MoonfallColor.Hex("#060816"), 0.5f));
            }
        }
        else
        {
            dl.AddRectFilled(v.Map(x, y), v.Map(x + w, y + h), Ink(MoonfallColor.Hex("#16245A")));
        }

        GiltFrame(m.C, x, y, x + w, y + h, m.Small ? 0.3 : 0.42);
        return h;
    }

    /// <summary>The power on a real board: a cut of a level of their stage round its first green peg, with the effect's accent ring.</summary>
    private void PowerAtWork(in MenuPen m, MoonfallCompanionInfo info, double x, double y, double w)
    {
        var level = FirstShippedLevel(info) ?? campaigns.Base.Levels[0];
        var g = Preview(level);
        var green = -1;
        for (var i = 0; i < g.PegCount; i++)
        {
            if (g.Peg(i).Colour == PegColour.Green)
            {
                green = i;
                break;
            }
        }

        // A cut of the board round its green peg, 300 units across, kept inside the opening (75..725, 41..594).
        const double Cw = 300, Ch = Cw * 1106 / 1300;
        var gx = green >= 0 ? g.Peg(green).X : 400;
        var gy = green >= 0 ? g.Peg(green).Y : 320;
        var ox = Math.Clamp(gx - (Cw / 2), 75, 725 - Cw);
        var oy = Math.Clamp(gy - (Ch * 0.4), 41, 594 - Ch);
        var h = LevelThumb(m, level, x, y, w, 0.26, new Vector4((float)ox, (float)oy, (float)Cw, (float)Ch));
        var k = w / Cw;
        var accent = MoonfallCards.For(info.Power)?.Accent ?? GoldInk;
        // Clipped to the opening inside the thumbnail's gilt frame, so no mark sits on the frame.
        var inset = w * 0.04;
        m.Dl.PushClipRect(m.V.Map(x + inset, y + inset), m.V.Map(x + w - inset, y + h - inset), true);
        PowerGlyph(m, info.Power, x + ((gx - ox) * k), y + ((gy - oy) * k), k, accent, x, y, w, h);
        m.Dl.PopClipRect();
    }

    /// <summary>
    /// The power at work over the cut (board units scaled by <paramref name="k"/> round the green peg at px, py): the
    /// effect's light at the green (a white-gold ring with an outer edge in the companion's colour, spec-rich2.md §3),
    /// and the power's own mark: the guide run on past the bounce, the twin ball, the wings on the bucket, the burst's
    /// reach, the oars, the gate's return, the flowers, the drum, the fireball, the weighed angles, the bolt.
    /// </summary>
    private void PowerGlyph(in MenuPen m, MoonfallPower power, double px, double py, double k, Vector3 accent, double x0, double y0, double w, double h)
    {
        var dl = m.Dl;
        var v = m.V;
        var ink = Ink(accent, 0.95f);
        var soft = Ink(accent, 0.35f);
        var white = Ink(MoonfallColor.Hex("#FFF4D8"), 0.95f);
        var line = MathF.Max(1.4f, v.Size(2.2 * k));
        void Dots(double ax, double ay, double bx, double by, int count, uint colour)
        {
            for (var i = 0; i <= count; i++)
            {
                var t = i / (double)count;
                dl.AddCircleFilled(v.Map(ax + ((bx - ax) * t), ay + ((by - ay) * t)), MathF.Max(1.2f, v.Size(2.4 * k)), colour, 8);
            }
        }

        switch (power)
        {
            case MoonfallPower.SuperGuide:
                // The guide down to the green, and on past the bounce.
                Dots(px - (90 * k), py - (150 * k), px, py, 9, white);
                Dots(px, py, px + (110 * k), py + (70 * k), 9, ink);
                break;
            case MoonfallPower.Multiball:
                dl.AddLine(v.Map(px, py), v.Map(px - (80 * k), py + (90 * k)), soft, line);
                dl.AddLine(v.Map(px, py), v.Map(px + (80 * k), py + (90 * k)), soft, line);
                dl.AddCircleFilled(v.Map(px - (80 * k), py + (90 * k)), v.Size(7 * k), white, 16);
                dl.AddCircleFilled(v.Map(px + (80 * k), py + (90 * k)), v.Size(7 * k), white, 16);
                break;
            case MoonfallPower.Wings:
                if (m.C.Sheet[MoonfallChromePart.Wing] is { } wing)
                {
                    var ww = w * 0.32;
                    var wh = ww * wing.H / wing.W;
                    var cy = y0 + h - (wh * 0.9);
                    Part(m.C, MoonfallChromePart.Wing, x0 + (w / 2) - ww - 4, cy, x0 + (w / 2) - 4, cy + wh, uint.MaxValue);
                    Part(m.C, MoonfallChromePart.Wing, x0 + (w / 2) + 4, cy, x0 + (w / 2) + 4 + ww, cy + wh, uint.MaxValue, flipX: true);
                }

                break;
            case MoonfallPower.Burst:
                dl.AddCircleFilled(v.Map(px, py), v.Size(85 * k), Ink(accent, 0.16f), 48);
                dl.AddCircle(v.Map(px, py), v.Size(85 * k), ink, 48, line);
                break;
            case MoonfallPower.Flippers:
                dl.AddLine(v.Map(x0 + (w * 0.06), y0 + h - (h * 0.10)), v.Map(x0 + (w * 0.30), y0 + h - (h * 0.04)), ink, line * 2);
                dl.AddLine(v.Map(x0 + w - (w * 0.06), y0 + h - (h * 0.10)), v.Map(x0 + w - (w * 0.30), y0 + h - (h * 0.04)), ink, line * 2);
                break;
            case MoonfallPower.Gate:
                // The ball falls out at the foot and drops back in from the sky above.
                Dots(px + (60 * k), y0 + h - (10 * k), px + (60 * k), py + (40 * k), 5, soft);
                dl.AddCircleFilled(v.Map(px + (60 * k), y0 + (16 * k)), v.Size(7 * k), white, 16);
                Dots(px + (60 * k), y0 + (30 * k), px + (60 * k), py - (40 * k), 5, ink);
                break;
            case MoonfallPower.Bloom:
                for (var i = 0; i < 6; i++)
                {
                    var a = i * MathF.PI / 3;
                    dl.AddCircleFilled(v.Map(px + (MathF.Cos(a) * 20 * k), py + (MathF.Sin(a) * 20 * k)), v.Size(8 * k), Ink(Tint(accent, 0.3f), 0.85f), 16);
                }

                break;
            case MoonfallPower.Draw:
                dl.AddCircle(v.Map(px + (60 * k), py - (40 * k)), v.Size(26 * k), ink, 32, line);
                for (var i = 0; i < 3; i++)
                {
                    var a = (i * 2 * MathF.PI / 3) - (MathF.PI / 2);
                    dl.AddLine(v.Map(px + (60 * k), py - (40 * k)), v.Map(px + (60 * k) + (MathF.Cos(a) * 26 * k), py - (40 * k) + (MathF.Sin(a) * 26 * k)), ink, line);
                }

                break;
            case MoonfallPower.Fireball:
                for (var i = 4; i >= 1; i--)
                {
                    dl.AddCircleFilled(v.Map(px - (i * 16 * k), py - (i * 22 * k)), v.Size((12 - (i * 2)) * k), Ink(MoonfallColor.Hex("#FF9A3D"), 0.18f * (5 - i)), 16);
                }

                dl.AddCircleFilled(v.Map(px, py), v.Size(10 * k), Ink(MoonfallColor.Hex("#FFD27A")), 16);
                break;
            case MoonfallPower.Path:
                for (var i = -3; i <= 3; i++)
                {
                    var bright = i == 1;
                    dl.AddLine(v.Map(px - (120 * k), py - (180 * k)), v.Map(px + (i * 30 * k), py), bright ? ink : Ink(accent, 0.25f), bright ? line : MathF.Max(1f, line * 0.5f));
                }

                break;
            case MoonfallPower.Bolt:
                {
                    var zx = px;
                    var zy = py;
                    for (var i = 0; i < 6; i++)
                    {
                        var nx = px + ((i % 2 == 0 ? 14 : -14) * k);
                        var ny = zy + (30 * k);
                        dl.AddLine(v.Map(zx, zy), v.Map(nx, ny), ink, line * 1.4f);
                        zx = nx;
                        zy = ny;
                    }
                }

                break;
        }

        // The light at the green itself.
        dl.AddCircle(v.Map(px, py), v.Size(16 * k), white, 32, MathF.Max(1.5f, v.Size(1.8 * k)));
        dl.AddCircle(v.Map(px, py), v.Size(24 * k), ink, 32, MathF.Max(1.5f, v.Size(2.0 * k)));
    }

    private MoonfallLevel? FirstShippedLevel(MoonfallCompanionInfo info)
    {
        // Asked every frame the detail shows: an index walk (no enumerator), and the campaigns' lookup by id.
        var ids = MoonfallStages.Of(info.Campaign)[info.Stage - 1].LevelIds;
        for (var i = 0; i < ids.Count; i++)
        {
            if (campaigns.Find(ids[i]) is { } level)
            {
                return level;
            }
        }

        return null;
    }

    private void CharacterDetail(in MenuPen m, double x0, double y0, double x1, double y1)
    {
        var info = MoonfallCompanions.All[charSel];
        var look = MoonfallLooks.Companion(info, StateOf(info.Companion));
        var accent = MoonfallCards.For(info.Power)?.Accent ?? GoldInk;
        Panel(m, x0, y0, x1, y1, accent);
        const double Hw = 190;
        var hx = x0 + 34;
        var hy = y0 + 36;
        var hh = Hero(m, info, look, hx, hy, Hw);
        var tx = hx + Hw + 30;
        var tw = (float)(x1 - 34 - tx);
        MenuTitle(m, tx, hy + 22, charName, 46, maxWidth: tw);
        var y = hy + 56;
        foreach (var line in charRole)
        {
            MenuText(m, MoonfallFace.Axis, 15, tx, y, line, Ink2, edge: 0f);
            y += 20;
        }

        y += 12;
        CrestRule(m, tx + (tw / 2), y + 4, tw, false, 0.42);
        y += 28;
        foreach (var line in charQuote)
        {
            MenuText(m, MoonfallFace.Axis, 17.5f, tx, y, line, Cream, edge: 0f);
            y += 24;
        }

        y += 8;
        MenuText(m, MoonfallFace.Axis, 14, tx, y, charJoins, Ink2, edge: 0f, maxWidth: tw);
        MenuText(m, MoonfallFace.Axis, 14, tx, y + 20, Strings.MoonfallSpoilersLine, Ink2, edge: 0f, maxWidth: tw);

        var py = hy + hh + 34;
        CrestRule(m, (x0 + x1) / 2, py, x1 - x0 - 80, true, 0.5);
        py += 48;
        Medallion(m, info.Power, x0 + 72, py + 8, 25, glow: look.Named ? 0.45f : 0f, back: !look.Named);
        MenuText(m, MoonfallFace.Jupiter, 44, x0 + 124, py - 2, Strings.MoonfallPowerName(info.Power), Tint(accent, 0.15f), edge: 1.4f, maxWidth: 360);
        MenuText(m, MoonfallFace.Axis, 14.5f, x0 + 126, py + 24, charLasts, Ink2, edge: 0f);
        py += 58;
        const double Tw2 = 190;
        var th2 = Tw2 * 1106 / 1300;
        var tx2 = x1 - 36 - Tw2;
        PowerAtWork(m, info, tx2, py - 8, Tw2);
        MenuText(m, MoonfallFace.Axis, 12.5f, tx2 + (Tw2 / 2), py + th2 + 6, Strings.MoonfallPowerInPlay, Ink2, Anchor.Centre, edge: 0f, maxWidth: (float)Tw2 + 30);
        var yy = py;
        foreach (var line in charDoes)
        {
            MenuText(m, MoonfallFace.Axis, 17, x0 + 40, yy, line, Cream, edge: 0f);
            yy += 23;
        }

        yy += 14;
        MenuText(m, MoonfallFace.Axis, 14.5f, x0 + 40, yy, Strings.MoonfallWonTogether, Ink2, edge: 0f);
        for (var i = 0; i < MoonfallCharacters.LevelsPerStage; i++)
        {
            Pip(m, x0 + 52 + (i * 22), yy + 24, 7.5f, i, lit: i < charWon);
        }

        if (MenuButton(m, "##mfPlayWith", x0 + 120, y1 - 64, x1 - 120, y1 - 22, look.Playable ? charPlay : charPlayTip, 30, isDefault: true,
            style: look.Playable ? MenuStyle.Normal : MenuStyle.Locked))
        {
            PlayWith(info.Companion);
        }
    }

    private void CharacterDetailSmall(in MenuPen m, double x0, double y0, double x1, double y1)
    {
        var info = MoonfallCompanions.All[charSel];
        var look = MoonfallLooks.Companion(info, StateOf(info.Companion));
        var accent = MoonfallCards.For(info.Power)?.Accent ?? GoldInk;
        Panel(m, x0, y0, x1, y1, accent, 0.36);
        const double Hw = 104;
        var hh = Hero(m, info, look, x0 + 20, y0 + 20, Hw);
        var tx = x0 + 20 + Hw + 16;
        MenuTitle(m, tx, y0 + 36, charName, 30, maxWidth: (float)(x1 - 16 - tx));
        var y = y0 + 60;
        foreach (var line in charRole)
        {
            MenuText(m, MoonfallFace.Axis, 12, tx, y, line, Ink2, edge: 0f);
            y += 15;
        }

        y += 6;
        foreach (var line in charQuote)
        {
            MenuText(m, MoonfallFace.Axis, 12, tx, y, line, Cream, edge: 0f);
            y += 15;
        }

        var py = y0 + 20 + hh + 36;
        Medallion(m, info.Power, x0 + 46, py, 16, glow: look.Named ? 0.35f : 0f, back: !look.Named);
        MenuText(m, MoonfallFace.Jupiter, 26, x0 + 72, py, Strings.MoonfallPowerName(info.Power), Tint(accent, 0.15f), edge: 1f, maxWidth: 160);
        MenuText(m, MoonfallFace.Axis, 12, x1 - 18, py, charLasts, Ink2, Anchor.Right, edge: 0f);
        py += 32;
        foreach (var line in charDoes)
        {
            MenuText(m, MoonfallFace.Axis, 12.5f, x0 + 20, py, line, Cream, edge: 0f);
            py += 17;
        }

        // The power at work, as large as the room above Play allows (at least 80 units across).
        var tw2 = Math.Min(150, (y1 - 52 - (py + 4)) * 1300 / 1106);
        if (tw2 >= 80)
        {
            PowerAtWork(m, info, ((x0 + x1) / 2) - (tw2 / 2), py + 4, tw2);
        }

        if (MenuButton(m, "##mfPlayWith", x0 + 30, y1 - 44, x1 - 30, y1 - 14, look.Playable ? charPlay : charPlayTip, 21, isDefault: true,
            style: look.Playable ? MenuStyle.Normal : MenuStyle.Locked))
        {
            PlayWith(info.Companion);
        }
    }
}
