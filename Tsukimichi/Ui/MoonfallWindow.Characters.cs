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
            var shortPower = m.Small ? ShortPowerName(info.Power) : power;
            charSubs[i] = !look.ShowsStage ? power
                : look.FarShore ? string.Format(c, m.Small ? Strings.MoonfallPowerFarShoreShortFormat : Strings.MoonfallPowerFarShoreFormat, shortPower)
                : string.Format(c, m.Small ? Strings.MoonfallPowerStageShortFormat : Strings.MoonfallPowerStageFormat, shortPower, info.Stage);
        }

        var sel = all[charSel];
        var selLook = MoonfallLooks.Companion(sel, StateOf(sel.Companion));
        var lore = MoonfallLooks.LoreOf(sel.Companion);
        var small = m.Small;
        var textWidth = small ? (628f - 16f) - (330f + 20f + 104f + 16f) : 1244f - 34f - (676f + 34f + 190f + 30f);
        charName = selLook.Named ? sel.Name : Strings.MoonfallNotYetMet;
        charRole = Wrap(m, MoonfallFace.Axis, small ? 12 : 15, selLook.Named ? lore.Role : Strings.MoonfallNotMetRole, textWidth);
        charQuote = selLook.Named ? Wrap(m, MoonfallFace.Axis, small ? 12 : 17.5f, "“" + lore.Line + "”", textWidth) : [];
        var stageName = MoonfallStages.Of(sel.Campaign)[sel.Stage - 1].Name;
        charJoins = sel.Campaign == MoonfallCampaignKind.Expansion
            ? string.Format(c, Strings.MoonfallJoinsFarShoreFormat, sel.Stage, stageName)
            : string.Format(c, Strings.MoonfallJoinsFormat, sel.Stage, stageName);
        charDoes = Wrap(m, MoonfallFace.Axis, small ? 12.5f : 17, lore.Does, small ? 258f : 300f);
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

    /// <summary>A power's short name for the 640 grid ("Draw", "Burst", "Wings").</summary>
    private static string ShortPowerName(MoonfallPower power) => power switch
    {
        MoonfallPower.Draw => Strings.MoonfallPowerShortDraw,
        MoonfallPower.Burst => Strings.MoonfallPowerShortBurst,
        MoonfallPower.Wings => Strings.MoonfallPowerShortWings,
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

        // The whole opening, with the power's light at its green peg: a white-gold ring, its outer edge in their colour.
        LevelThumb(m, level, x, y, w, 0.26);
        if (green >= 0)
        {
            var accent = MoonfallCards.For(info.Power)?.Accent ?? GoldInk;
            var k = w / 650.0;
            var px = x + ((g.Peg(green).X - 75) * k);
            var py = y + ((g.Peg(green).Y - 41) * k);
            m.Dl.AddCircle(m.V.Map(px, py), m.V.Size(26 * k), Ink(MoonfallColor.Hex("#FFF4D8"), 0.9f), 32, MathF.Max(1.5f, m.V.Size(1.8)));
            m.Dl.AddCircle(m.V.Map(px, py), m.V.Size(40 * k), Ink(accent, 0.75f), 32, MathF.Max(1.5f, m.V.Size(2.0)));
        }
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

        const double Tw2 = 150;
        var tx2 = ((x0 + x1) / 2) - (Tw2 / 2);
        if (py + 6 + (Tw2 * 1106 / 1300) < y1 - 50)
        {
            PowerAtWork(m, info, tx2, py + 6, Tw2);
        }

        if (MenuButton(m, "##mfPlayWith", x0 + 30, y1 - 44, x1 - 30, y1 - 14, look.Playable ? charPlay : charPlayTip, 21, isDefault: true,
            style: look.Playable ? MenuStyle.Normal : MenuStyle.Locked))
        {
            PlayWith(info.Companion);
        }
    }
}
