using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Ui;

/// <summary>
/// Quick Play, the challenges and the duel (plan v9 G7; moonfall-modes.md §1, §4–6), in the menus' own kit. Quick Play:
/// any level reached, with any companion met and reached (or none). The challenges: the list (sealed until The Moon
/// Road is won), each with its rules, its best and its levels; one whose levels are not built yet says so. The duel: the
/// opponent (every companion the story has introduced), the difficulty, the level and the player's own companion, with the
/// record against that opponent. On the board a duel's HUD gives each side its score, whose turn it is, and the
/// opponent's thinking.
/// </summary>
public sealed partial class MoonfallWindow
{
    private int quickSel = -1;
    private int quickPage;
    private MoonfallCompanion quickCompanion = MoonfallCompanion.None;
    private bool quickCompanionChosen;
    private int challengeSel;
    private MoonfallCompanion challengeCompanion = MoonfallCompanion.None;
    private int duelOpponentSel = -1;
    private MoonfallAiDifficulty duelDifficulty = MoonfallAiDifficulty.Adept;
    private int duelLevelSel = -1;
    private MoonfallCompanion duelCompanion = MoonfallCompanion.None;

    // Their words, made when the screen, its selection or the progress changes.
    private (int Views, int Sel, int Page, MoonfallCompanion Companion, bool Small) quickWordsKey = (-1, -1, -1, MoonfallCompanion.None, false);
    private string[] quickCodes = [];
    private string[] quickBests = [];
    private string quickTitleLine = string.Empty;
    private string quickBest = string.Empty;
    private string quickCompanionLine = string.Empty;
    private string[] quickDoes = [];
    private string quickPlay = string.Empty;
    private string quickPageText = string.Empty;
    private (int Views, int Sel, int Page, MoonfallCompanion Companion, bool Small) challengeWordsKey = (-1, -1, -1, MoonfallCompanion.None, false);
    private int challengePage = -1;
    private string challengePageText = string.Empty;
    private IReadOnlyList<(MoonfallChallenge Challenge, MoonfallChallengeState State)> challengeList = [];
    private string[] challengeSubs = [];
    private string[] challengeText = [];
    private string challengeRules = string.Empty;
    private string challengeLevels = string.Empty;
    private string challengeBest = string.Empty;
    private string challengeCompanionLine = string.Empty;
    private string challengePlay = string.Empty;
    private string challengePickLine = string.Empty;
    private (int Views, int Opponent, MoonfallAiDifficulty Difficulty, int Level, MoonfallCompanion Companion, bool Small) duelWordsKey = (-1, -1, MoonfallAiDifficulty.Novice, -1, MoonfallCompanion.None, false);
    private string duelRecord = string.Empty;
    private string duelLevelLine = string.Empty;
    private string duelLevelCode = string.Empty;
    private string duelOpponentRole = string.Empty;
    private string[] duelOpponentLore = [];
    private bool duelCompanionChosen;
    private string duelLevelPage = string.Empty;
    private string duelCompanionLine = string.Empty;
    private string duelPlay = string.Empty;
    private string duelOpponentName = string.Empty;

    private static readonly string[] QuickRowIds = MakeIds("##mfQuickRow", 20);
    private static readonly string[] PickIds = MakeIds("##mfPickCompanion", MoonfallCompanions.Count + 1);
    private static readonly string[] ChallengeIds = MakeIds("##mfChallenge", 40);
    private static readonly string[] OpponentIds = MakeIds("##mfOpponent", MoonfallCompanions.Count);
    private static readonly string[] DifficultyIds = MakeIds("##mfDifficulty", 3);

    // ---- A screen's header ----

    private void ScreenHeader(in MenuPen m, string title, string? line)
    {
        if (m.Small)
        {
            if (MenuButton(m, "##mfBack", 10, 10, 78, 38, Strings.MoonfallBack, 13.7f, primaryFace: false))
            {
                Back();
            }

            MenuTitle(m, 92, 25, title, 34, maxWidth: 520);
            return;
        }

        if (MenuButton(m, "##mfBack", 40, 30, 150, 66, Strings.MoonfallBack, 18.7f, primaryFace: false))
        {
            Back();
        }

        MenuTitle(m, 176, 50, title, 58, maxWidth: 1060);
        if (line is not null)
        {
            MenuText(m, MoonfallFace.Axis, 15, 178, 86, line, Ink2, edge: 1f, maxWidth: 1060);
        }
    }

    /// <summary>A companion picker: the eleven in rings (and "none" first), the ones who can be played with selectable, the rest as the shield shows them.</summary>
    private MoonfallCompanion CompanionPicker(in MenuPen m, double x, double y, int columns, double gap, float r, MoonfallCompanion selected, bool allowNone)
    {
        var all = MoonfallCompanions.All;
        var chosen = selected;
        var count = all.Count + (allowNone ? 1 : 0);
        for (var i = 0; i < count; i++)
        {
            var (row, col) = Math.DivRem(i, columns);
            var cx = x + (col * gap);
            var cy = y + (row * gap);
            var companion = allowNone && i == 0 ? MoonfallCompanion.None : all[i - (allowNone ? 1 : 0)].Companion;
            var state = companion == MoonfallCompanion.None ? MoonfallCompanionState.Available : StateOf(companion);
            var playable = state == MoonfallCompanionState.Available;
            var hit = MenuHit(m, PickIds[i], cx - r - 4, cy - r - 4, cx + r + 4, cy + r + 4, out var hovered, out var nav);
            var isSel = companion == selected;
            if (companion == MoonfallCompanion.None)
            {
                m.Dl.AddCircleFilled(m.V.Map(cx, cy), m.V.Size(r * 1.04), Ink(PlateInk), 32);
                GiltRing(m.C, cx, cy, r);
                MenuText(m, MoonfallFace.Axis, m.Small ? 12 : 13, cx, cy, Strings.MoonfallNoPowerShort, Ink2, Anchor.Centre, edge: 0f);
            }
            else
            {
                var power = MoonfallCompanions.Get(companion).Power;
                Medallion(m, power, cx, cy, r, glow: isSel ? 0.45f : 0f, drained: state == MoonfallCompanionState.MetNotReached, back: state == MoonfallCompanionState.NotMet,
                    dimRing: !playable);
                if (state == MoonfallCompanionState.MetNotReached)
                {
                    Padlock(m, cx + (r * 0.9), cy + (r * 0.85), Math.Max(r * 0.34, 5.0));
                }
            }

            // The selection ring goes over the medallion's glow, so it always shows.
            if (isSel || nav || (hovered && playable))
            {
                var ring = nav ? Cream : isSel ? GoldHiInk : MoonfallColor.Hex("#9DC0FF");
                SelectRing(m, cx, cy, r, ring, nav || isSel ? 0.95f : 0.6f);
            }

            if (hovered || nav)
            {
                UiMetrics.Tooltip(PickerTip(companion, state));
            }

            if (hit && playable && companion != selected)
            {
                SoundClick();
                chosen = companion;
            }
        }

        return chosen;
    }

    /// <summary>
    /// A medallion's selection ring: clear of its gilt ring (at 1.6 r), on a dark under-stroke so it reads over the
    /// medallion's glow.
    /// </summary>
    private static void SelectRing(in MenuPen m, double cx, double cy, float r, Vector3 ink, float alpha)
    {
        var width = MathF.Max(1.5f, m.V.Size(2.2));
        m.Dl.AddCircle(m.V.Map(cx, cy), m.V.Size(r * 1.6), Ink(MoonfallColor.Hex("#070A1C"), 0.7f * alpha), 40, width + 2f);
        m.Dl.AddCircle(m.V.Map(cx, cy), m.V.Size(r * 1.6), Ink(ink, alpha), 40, width);
    }

    /// <summary>A picker ring's tooltip: the companion and their power, or why they cannot be picked.</summary>
    private static string PickerTip(MoonfallCompanion companion, MoonfallCompanionState state)
    {
        if (companion == MoonfallCompanion.None)
        {
            return Strings.MoonfallNoPowerTip;
        }

        var info = MoonfallCompanions.Get(companion);
        return state switch
        {
            MoonfallCompanionState.NotMet => string.Format(CultureInfo.CurrentCulture, Strings.MoonfallPickerNotMetFormat, Strings.MoonfallPowerName(info.Power)),
            MoonfallCompanionState.MetNotReached => string.Format(CultureInfo.CurrentCulture, Strings.MoonfallPickerNotReachedFormat, info.Name, info.Stage),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.MoonfallCarrierFormat, info.Name, Strings.MoonfallPowerName(info.Power)),
        };
    }

    /// <summary>A companion's line under a picker: "Minfilia · Super Guide" and what it does, or "No companion: no power".</summary>
    private (string Line, string[] Does) CompanionWords(in MenuPen m, MoonfallCompanion companion, float width)
    {
        if (!MoonfallCompanions.TryGet(companion, out var info))
        {
            return (Strings.MoonfallNoCompanion, []);
        }

        var line = string.Format(CultureInfo.CurrentCulture, Strings.MoonfallCarrierFormat, info.Name, Strings.MoonfallPowerName(info.Power));
        return (line, Wrap(m, MoonfallFace.Axis, m.Small ? 12f : 14.5f, MoonfallLooks.LoreOf(companion).Does, width));
    }

    // ---- Quick Play ----

    private int QuickRowsPerPage(bool small) => small ? 11 : 17;

    private void DrawQuickPlay(in MenuPen m)
    {
        JewelNight(m);
        var levels = QuickLevels();
        var small = m.Small;
        var rows = QuickRowsPerPage(small);
        if (quickSel < 0 || quickSel >= levels.Count)
        {
            quickSel = Math.Max(0, levels.Count - 1);
            quickPage = quickSel / rows;
        }
        else if (quickPage * rows >= levels.Count)
        {
            quickPage = quickSel / rows;
        }

        if (!quickCompanionChosen && levels.Count > 0)
        {
            var carrier = MoonfallStages.AdventureCompanion(levels[quickSel].Place.Campaign, levels[quickSel].Place.Index);
            quickCompanion = StateOf(carrier) == MoonfallCompanionState.Available ? carrier : quickCompanion;
        }

        MakeQuickWords(m, levels);
        ScreenHeader(m, Strings.MoonfallScreenQuickPlay, Strings.MoonfallQuickPlayLine);
        if (levels.Count == 0)
        {
            MenuText(m, MoonfallFace.Axis, small ? 14 : 18, m.W / 2, m.H / 2, Strings.MoonfallQuickPlayLocked, Cream, Anchor.Centre);
            return;
        }

        var (lx0, ly0, lx1, ly1) = small ? (10.0, 50.0, 318.0, 470.0) : (46.0, 120.0, 620.0, 770.0);
        Panel(m, lx0, ly0, lx1, ly1, null, small ? 0.3 : 0.4);
        var rowH = small ? 31.0 : 32.0;
        var y = ly0 + (small ? 40 : 56);
        var inset = small ? 26.0 : 34.0;
        var first = quickPage * rows;
        for (var i = 0; i < rows && first + i < levels.Count; i++)
        {
            var index = first + i;
            var slot = levels[index];
            var yy = y + (i * rowH);
            var hit = MenuHit(m, QuickRowIds[i], lx0 + inset, yy - (rowH / 2) + 1, lx1 - inset, yy + (rowH / 2) - 1, out var hovered, out var nav);
            if (index == quickSel || hovered || nav)
            {
                m.Dl.AddRectFilled(m.V.Map(lx0 + inset, yy - (rowH / 2) + 1), m.V.Map(lx1 - inset, yy + (rowH / 2) - 1), Ink(index == quickSel ? MoonfallColor.Hex("#E8B54A") : MoonfallColor.Hex("#9DC0FF"), index == quickSel ? 0.16f : 0.10f), m.V.Size(6));
                if (nav)
                {
                    FocusOutline(m.Dl, m.V.Map(lx0 + inset, yy - (rowH / 2) + 1), m.V.Map(lx1 - inset, yy + (rowH / 2) - 1), m.V.Size(6));
                }
            }

            MenuText(m, MoonfallFace.Trump, small ? 15 : 19, lx0 + inset + (small ? 8 : 12), yy, quickCodes[index], GoldHiInk, edge: 0.6f);
            MenuText(m, MoonfallFace.Jupiter, small ? 15 : 22, lx0 + inset + (small ? 46 : 64), yy, LevelNameShown(slot), Cream, edge: 1f, maxWidth: small ? 140 : 290);
            if (slot.Aced)
            {
                MenuText(m, MoonfallFace.Trump, small ? 13 : 16, lx1 - inset - 8, yy, Strings.MoonfallAced, GoldHiInk, Anchor.Right, edge: 0.6f);
            }
            else if (slot.State == MoonfallLevelState.Cleared)
            {
                Pip(m, lx1 - inset - 14, yy, small ? 5f : 6.5f, index);
            }

            if (!small && quickBests[index].Length > 0)
            {
                MenuText(m, MoonfallFace.Axis, 13.5f, lx1 - inset - 70, yy, quickBests[index], Ink2, Anchor.Right, edge: 0f);
            }

            if (hit)
            {
                if (index == quickSel)
                {
                    PlayQuickSelected(levels);
                    return;
                }

                SoundClick();
                quickSel = index;
            }
        }

        var pages = (levels.Count + rows - 1) / rows;
        if (pages > 1)
        {
            var step = MenuStepper(m, "##mfQuickPage", (lx0 + lx1) / 2 + (small ? 50 : 70), ly1 - (small ? 18 : 26), quickPageText, small, quickPage > 0, quickPage < pages - 1);
            if (step != 0)
            {
                quickPage = Math.Clamp(quickPage + step, 0, pages - 1);
                SoundClick();
            }
        }

        // The selected level and the companion.
        var (rx0, ry0, rx1, ry1) = small ? (328.0, 50.0, 630.0, 470.0) : (660.0, 120.0, 1234.0, 770.0);
        var accent = MoonfallCards.For(MoonfallCompanions.TryGet(quickCompanion, out var qi) ? qi.Power : MoonfallPower.None)?.Accent;
        Panel(m, rx0, ry0, rx1, ry1, accent, small ? 0.3 : 0.4);
        var sel = levels[quickSel];
        if (sel.Level is { } level)
        {
            var tw = small ? 120.0 : 260.0;
            gameArt?.Warm(level, BoardTwoX(m));
            LevelThumb(m, level, rx0 + (small ? 14 : 30), ry0 + (small ? 16 : 30), tw);
            var tx = rx0 + (small ? 146 : 320);
            MenuText(m, MoonfallFace.Jupiter, small ? 20 : 34, tx, ry0 + (small ? 30 : 56), PlayLevelName(level), Cream, edge: 1f, maxWidth: (float)(rx1 - tx - 16));
            MenuText(m, MoonfallFace.Axis, small ? 12 : 14.5f, tx, ry0 + (small ? 52 : 92), quickTitleLine, Ink2, edge: 0f, maxWidth: (float)(rx1 - tx - 16));
            MenuText(m, MoonfallFace.Axis, small ? 12 : 14.5f, tx, ry0 + (small ? 70 : 118), quickBest, Ink2, edge: 0f, maxWidth: (float)(rx1 - tx - 16));
        }

        var py = ry0 + (small ? 168 : 330);
        MenuText(m, MoonfallFace.Axis, small ? 12.5f : 14, rx0 + (small ? 14 : 30), py - (small ? 28 : 40), Strings.MoonfallCompanionCaps, GoldInk, edge: 0f);
        var picked = CompanionPicker(m, rx0 + (small ? 30 : 64), py, 6, small ? 48 : 84, small ? 14f : 24f, quickCompanion, allowNone: true);
        if (picked != quickCompanion)
        {
            quickCompanion = picked;
            quickCompanionChosen = true;
        }

        var cy = py + (small ? 112 : 186);
        MenuText(m, MoonfallFace.Jupiter, small ? 17 : 24, rx0 + (small ? 14 : 30), cy, quickCompanionLine, accent is { } a ? Tint(a, 0.2f) : Cream, edge: 1f, maxWidth: (float)(rx1 - rx0 - 40));
        for (var i = 0; i < quickDoes.Length; i++)
        {
            MenuText(m, MoonfallFace.Axis, small ? 12 : 14.5f, rx0 + (small ? 14 : 30), cy + (small ? 20 : 30) + (i * (small ? 15 : 20)), quickDoes[i], Ink2, edge: 0f);
        }

        if (MenuButton(m, "##mfQuickPlay", rx1 - (small ? 190 : 260), ry1 - (small ? 46 : 70), rx1 - (small ? 14 : 30), ry1 - (small ? 12 : 22), quickPlay, small ? 22 : 34, isDefault: true))
        {
            PlayQuickSelected(levels);
        }
    }

    private void PlayQuickSelected(IReadOnlyList<MoonfallLevelSlot> levels)
    {
        if (quickSel >= 0 && quickSel < levels.Count)
        {
            PlayQuick(levels[quickSel].Id, quickCompanion);
        }
    }

    private void MakeQuickWords(in MenuPen m, IReadOnlyList<MoonfallLevelSlot> levels)
    {
        var key = (menuViewsKey.GetHashCode(), quickSel, quickPage, quickCompanion, m.Small);
        if (key == quickWordsKey && quickCodes.Length == levels.Count)
        {
            return;
        }

        quickWordsKey = key;
        var c = CultureInfo.CurrentCulture;
        quickCodes = new string[levels.Count];
        quickBests = new string[levels.Count];
        for (var i = 0; i < levels.Count; i++)
        {
            var slot = levels[i];
            quickCodes[i] = slot.Place.Campaign == MoonfallCampaignKind.Expansion ? "FS " + LevelCode(slot.Place.Index) : LevelCode(slot.Place.Index);
            quickBests[i] = slot.Best > 0 ? slot.Best.ToString("N0", c) : string.Empty;
        }

        var rows = QuickRowsPerPage(m.Small);
        quickPageText = string.Format(c, Strings.MoonfallPageFormat, quickPage + 1, (levels.Count + rows - 1) / rows);
        if (levels.Count == 0)
        {
            return;
        }

        var sel = levels[Math.Clamp(quickSel, 0, levels.Count - 1)];
        var stage = MoonfallStages.StageOf(sel.Place.Campaign, sel.Place.Index);
        quickTitleLine = m.Small
            ? string.Format(c, Strings.MoonfallCarrierFormat, LevelCode(sel.Place.Index), (stage is null ? string.Empty : StageNameShown(stage)))
            : string.Format(c, Strings.MoonfallQuickLevelLineFormat, Strings.MoonfallCampaignName(sel.Place.Campaign), LevelCode(sel.Place.Index), (stage is null ? string.Empty : StageNameShown(stage)));
        quickBest = sel.Best > 0
            ? (sel.Ace is { } ace ? string.Format(c, Strings.MoonfallBestAceFormat, string.Format(c, Strings.MoonfallBestFormat, sel.Best.ToString("N0", c)), ace.ToString("N0", c)) : string.Format(c, Strings.MoonfallBestFormat, sel.Best.ToString("N0", c)))
            : sel.Ace is { } ace2 ? string.Format(c, Strings.MoonfallAceFormat, ace2.ToString("N0", c)) : Strings.MoonfallNotYetWon;
        (quickCompanionLine, quickDoes) = CompanionWords(m, quickCompanion, m.Small ? 270f : 500f);
        quickPlay = string.Format(c, Strings.MoonfallPlayFormat, quickCodes[Math.Clamp(quickSel, 0, levels.Count - 1)]);
    }

    /// <summary>Whether the board will build its scene at 2 pixels a unit in this window (so a level is built ahead at its tier).</summary>
    private static bool BoardTwoX(in MenuPen m)
    {
        var size = m.AreaMax - m.AreaMin;
        return MathF.Min(size.X / (float)MoonfallRules.Width, size.Y / (float)MoonfallRules.Height) > ArtTwoXAbove;
    }

    // ---- The challenges ----

    private void DrawChallenges(in MenuPen m)
    {
        JewelNight(m);
        var small = m.Small;
        if (challengeList.Count == 0 || challengeWordsKey.Views != menuViewsKey.GetHashCode())
        {
            challengeList = modes.ChallengeList();
        }

        var clamped = Math.Clamp(challengeSel, 0, Math.Max(0, challengeList.Count - 1));
        if (clamped != challengeSel || challengePage < 0)
        {
            challengeSel = clamped;
            challengePage = -1;
        }

        if (challengePage < 0 || challengePage * ChallengeRowsPerPage(small) >= Math.Max(1, challengeList.Count))
        {
            // The list opens on the page holding the selection.
            challengePage = challengeSel / ChallengeRowsPerPage(small);
        }

        MakeChallengeWords(m);
        ScreenHeader(m, Strings.MoonfallScreenChallenges, Strings.MoonfallChallengesLine);
        var (lx0, ly0, lx1, ly1) = small ? (10.0, 50.0, 318.0, 470.0) : (46.0, 120.0, 620.0, 770.0);
        Panel(m, lx0, ly0, lx1, ly1, null, small ? 0.3 : 0.4);
        // The rows stand clear of the frame's corner ornaments, a page at a time (as Quick Play's).
        var rowH = small ? 34.0 : 50.0;
        var rows = ChallengeRowsPerPage(small);
        var y = ly0 + (small ? 40 : 60);
        var (rx0Row, rx1Row) = (lx0 + (small ? 26 : 34), lx1 - (small ? 26 : 34));
        var first = challengePage * rows;
        for (var r = 0; r < rows && first + r < challengeList.Count; r++)
        {
            var i = first + r;
            var (challenge, state) = challengeList[i];
            var yy = y + (r * rowH);
            var hit = MenuHit(m, ChallengeIds[r], rx0Row, yy - (rowH / 2) + 2, rx1Row, yy + (rowH / 2) - 2, out var hovered, out var nav);
            if (i == challengeSel || hovered || nav)
            {
                m.Dl.AddRectFilled(m.V.Map(rx0Row, yy - (rowH / 2) + 2), m.V.Map(rx1Row, yy + (rowH / 2) - 2), Ink(i == challengeSel ? GoldInk : MoonfallColor.Hex("#9DC0FF"), i == challengeSel ? 0.16f : 0.10f), m.V.Size(6));
                if (nav)
                {
                    FocusOutline(m.Dl, m.V.Map(rx0Row, yy - (rowH / 2) + 2), m.V.Map(rx1Row, yy + (rowH / 2) - 2), m.V.Size(6));
                }
            }

            var open = state is MoonfallChallengeState.Open or MoonfallChallengeState.Done;
            MenuText(m, MoonfallFace.Jupiter, small ? 15 : 22, rx0Row + (small ? 10 : 14), yy - (small ? 0 : 9), challenge.Name, open ? Cream : Ink3, edge: 1f, maxWidth: small ? 220 : 410);
            if (!small)
            {
                MenuText(m, MoonfallFace.Axis, 13, rx0Row + 14, yy + 12, challengeSubs[i], Ink2, edge: 0f, maxWidth: 430);
            }

            if (state == MoonfallChallengeState.Done)
            {
                Pip(m, rx1Row - (small ? 16 : 22), yy, small ? 5.5f : 7f, i);
            }
            else if (state == MoonfallChallengeState.Veiled)
            {
                ShieldMark(m, rx1Row - (small ? 16 : 22), yy, small ? 6 : 8);
            }
            else if (state == MoonfallChallengeState.Sealed)
            {
                Padlock(m, rx1Row - (small ? 16 : 22), yy, small ? 6 : 8);
            }

            if (hit)
            {
                if (i == challengeSel && open)
                {
                    PlaySelectedChallenge();
                    return;
                }

                SoundClick();
                challengeSel = i;
            }
        }

        var pages = (challengeList.Count + rows - 1) / rows;
        if (pages > 1)
        {
            var step = MenuStepper(m, "##mfChallengePage", ((lx0 + lx1) / 2) + (small ? 50 : 70), ly1 - (small ? 18 : 26), challengePageText, small, challengePage > 0, challengePage < pages - 1);
            if (step != 0)
            {
                challengePage = Math.Clamp(challengePage + step, 0, pages - 1);
                SoundClick();
            }
        }

        if (challengeList.Count == 0)
        {
            return;
        }

        var (sel, selState) = challengeList[challengeSel];
        var (rx0, ry0, rx1, ry1) = small ? (328.0, 50.0, 630.0, 470.0) : (660.0, 120.0, 1234.0, 770.0);
        Panel(m, rx0, ry0, rx1, ry1, null, small ? 0.3 : 0.4);
        var tx = rx0 + (small ? 22 : 40);
        MenuTitle(m, tx, ry0 + (small ? 34 : 56), sel.Name, small ? 24 : 40, maxWidth: (float)(rx1 - tx - 22));
        var y2 = ry0 + (small ? 56 : 96);
        foreach (var line in challengeText)
        {
            MenuText(m, MoonfallFace.Axis, small ? 12.5f : 17, tx, y2, line, Cream, edge: 0f);
            y2 += small ? 16 : 24;
        }

        y2 += small ? 6 : 12;
        MenuText(m, MoonfallFace.Axis, small ? 12 : 14.5f, tx, y2, challengeRules, Ink2, edge: 0f, maxWidth: (float)(rx1 - tx - 16));
        y2 += small ? 16 : 24;
        MenuText(m, MoonfallFace.Axis, small ? 12 : 14.5f, tx, y2, challengeLevels, Ink2, edge: 0f, maxWidth: (float)(rx1 - tx - 16));
        y2 += small ? 16 : 24;
        MenuText(m, MoonfallFace.Axis, small ? 12 : 14.5f, tx, y2, challengeBest, Ink2, edge: 0f, maxWidth: (float)(rx1 - tx - 16));
        y2 += small ? 30 : 48;
        var open2 = selState is MoonfallChallengeState.Open or MoonfallChallengeState.Done;
        if (sel.Companion == MoonfallCompanion.None && open2)
        {
            MenuText(m, MoonfallFace.Axis, small ? 12.5f : 14, tx, y2 - (small ? 4 : 0), Strings.MoonfallCompanionCaps, GoldInk, edge: 0f);
            var picked = CompanionPicker(m, tx + (small ? 16 : 34), y2 + (small ? 22 : 40), 6, small ? 46 : 82, small ? 14f : 24f, challengeCompanion, allowNone: true);
            if (picked != challengeCompanion)
            {
                challengeCompanion = picked;
            }

            // The pick in words, "No companion: no power" included (as Quick Play says it).
            var pickAccent = MoonfallCards.For(MoonfallCompanions.TryGet(challengeCompanion, out var pickInfo) ? pickInfo.Power : MoonfallPower.None)?.Accent;
            MenuText(m, MoonfallFace.Jupiter, small ? 15 : 22, tx, y2 + (small ? 104 : 178), challengePickLine, pickAccent is { } pa ? Tint(pa, 0.2f) : Cream, edge: 1f, maxWidth: (float)(rx1 - tx - 20));
        }
        else if (sel.Companion != MoonfallCompanion.None)
        {
            var power = MoonfallCompanions.Get(sel.Companion).Power;
            Medallion(m, power, tx + (small ? 16 : 26), y2 + (small ? 12 : 20), small ? 14 : 24, glow: 0.35f, back: StateOf(sel.Companion) == MoonfallCompanionState.NotMet);
            MenuText(m, MoonfallFace.Axis, small ? 12.5f : 15, tx + (small ? 40 : 64), y2 + (small ? 12 : 20), challengeCompanionLine, Cream, edge: 0f, maxWidth: (float)(rx1 - tx - 80));
        }

        if (MenuButton(m, "##mfChallengePlay", rx1 - (small ? 190 : 260), ry1 - (small ? 46 : 70), rx1 - (small ? 14 : 30), ry1 - (small ? 12 : 22), challengePlay, small ? 22 : 34,
            isDefault: true, style: open2 ? MenuStyle.Normal : selState switch { MoonfallChallengeState.Veiled => MenuStyle.Veiled, MoonfallChallengeState.Unavailable => MenuStyle.Waiting, _ => MenuStyle.Locked }))
        {
            PlaySelectedChallenge();
        }
    }

    private static int ChallengeRowsPerPage(bool small) => small ? 10 : 11;

    private void PlaySelectedChallenge()
    {
        if (challengeSel < challengeList.Count && challengeList[challengeSel] is { } entry && entry.State is MoonfallChallengeState.Open or MoonfallChallengeState.Done)
        {
            PlayChallenge(entry.Challenge.Id, challengeCompanion);
        }
    }

    private void MakeChallengeWords(in MenuPen m)
    {
        var key = (menuViewsKey.GetHashCode(), challengeSel, challengePage, challengeCompanion, m.Small);
        if (key == challengeWordsKey && challengeSubs.Length == challengeList.Count)
        {
            return;
        }

        challengeWordsKey = key;
        var c = CultureInfo.CurrentCulture;
        var rows = ChallengeRowsPerPage(m.Small);
        challengePageText = string.Format(c, Strings.MoonfallPageFormat, challengePage + 1, Math.Max(1, (challengeList.Count + rows - 1) / rows));
        challengeSubs = new string[challengeList.Count];
        for (var i = 0; i < challengeList.Count; i++)
        {
            var (ch, state) = challengeList[i];
            challengeSubs[i] = state switch
            {
                MoonfallChallengeState.Unavailable => Strings.MoonfallChallengeUnavailable,
                MoonfallChallengeState.Veiled => Strings.MoonfallChallengeVeiled,
                MoonfallChallengeState.Sealed => Strings.MoonfallChallengesSealed,
                _ => ch.Text,
            };
        }

        if (challengeList.Count == 0)
        {
            return;
        }

        var (sel, selState) = challengeList[challengeSel];
        challengeText = Wrap(m, MoonfallFace.Axis, m.Small ? 12.5f : 17, sel.Text, m.Small ? 270f : 500f);
        challengeRules = string.Format(c, Strings.MoonfallChallengeRulesFormat, ChallengeKindName(sel.Kind), sel.Balls, sel.Oranges);
        var codes = new List<string>(sel.LevelIds.Count);
        foreach (var id in sel.LevelIds)
        {
            codes.Add(MoonfallStages.TryPlace(id, out var place) ? LevelCode(place.Index) : id);
        }

        challengeLevels = string.Format(c, Strings.MoonfallChallengeLevelsFormat, string.Join(", ", codes));
        var record = progress.Challenges.TryGetValue(sel.Id, out var r) ? r : null;
        challengeBest = selState switch
        {
            MoonfallChallengeState.Sealed => Strings.MoonfallChallengesSealedTooltip,
            MoonfallChallengeState.Unavailable => Strings.MoonfallChallengeUnavailableLine,
            MoonfallChallengeState.Veiled => Strings.MoonfallStageVeiledLine,
            _ when record is { Done: true } => string.Format(c, Strings.MoonfallChallengeDoneFormat, record.Best.ToString("N0", c)),
            _ when record is { Best: > 0 } => string.Format(c, Strings.MoonfallBestFormat, record.Best.ToString("N0", c)),
            _ => Strings.MoonfallChallengeNotTried,
        };
        challengeCompanionLine = sel.Companion != MoonfallCompanion.None && MoonfallCompanions.TryGet(sel.Companion, out var info)
            ? (StateOf(sel.Companion) == MoonfallCompanionState.NotMet
                ? string.Format(c, Strings.MoonfallNotMetPowerFormat, Strings.MoonfallPowerName(info.Power))
                : string.Format(c, Strings.MoonfallCarrierFormat, info.Name, Strings.MoonfallPowerName(info.Power)))
            : string.Empty;
        challengePlay = selState switch
        {
            MoonfallChallengeState.Sealed => Strings.MoonfallSealed,
            MoonfallChallengeState.Unavailable => Strings.MoonfallLevelComing,
            MoonfallChallengeState.Veiled => Strings.MoonfallStageVeiledButton,
            MoonfallChallengeState.Done => Strings.MoonfallPlayAgain,
            _ => Strings.MoonfallPlay,
        };
        challengePickLine = CompanionWords(m, challengeCompanion, 400).Line;
    }

    private static string ChallengeKindName(MoonfallChallengeKind kind) => kind switch
    {
        MoonfallChallengeKind.Score => Strings.MoonfallChallengeKindScore,
        MoonfallChallengeKind.ClearAll => Strings.MoonfallChallengeKindClear,
        MoonfallChallengeKind.Duel => Strings.MoonfallChallengeKindDuel,
        _ => Strings.MoonfallChallengeKindWin,
    };

    // ---- The duel's setup ----

    private void DrawDuelSetup(in MenuPen m)
    {
        JewelNight(m);
        var small = m.Small;
        var levels = QuickLevels();
        if (duelOpponents.Count == 0 || levels.Count == 0)
        {
            ScreenHeader(m, Strings.MoonfallScreenDuel, Strings.MoonfallDuelLine);
            MenuText(m, MoonfallFace.Axis, small ? 14 : 18, m.W / 2, m.H / 2, Strings.MoonfallDuelNone, Cream, Anchor.Centre);
            return;
        }

        if (duelOpponentSel < 0 || duelOpponentSel >= duelOpponents.Count)
        {
            duelOpponentSel = Math.Max(0, IndexOf(duelOpponents, modes.TitleOpponent()));
        }

        if (duelLevelSel < 0 || duelLevelSel >= levels.Count)
        {
            duelLevelSel = 0;
        }

        if (!duelCompanionChosen)
        {
            // The player's side starts with the companion Adventure has them with now, not bare against a powered opponent.
            duelCompanion = AdventureCompanionNow();
        }

        if (duelCompanion != MoonfallCompanion.None && StateOf(duelCompanion) != MoonfallCompanionState.Available)
        {
            duelCompanion = MoonfallCompanion.None;
        }

        MakeDuelWords(m, levels);
        ScreenHeader(m, Strings.MoonfallScreenDuel, Strings.MoonfallDuelLine);
        var (x0, y0, x1, y1) = small ? (10.0, 50.0, 630.0, 470.0) : (46.0, 120.0, 1234.0, 770.0);
        Panel(m, x0, y0, x1, y1, null, small ? 0.3 : 0.45);

        // The opponents: every companion the story has introduced.
        var ox = x0 + (small ? 40 : 70);
        var oy = y0 + (small ? 52 : 92);
        MenuText(m, MoonfallFace.Axis, small ? 12.5f : 14, x0 + (small ? 24 : 44), y0 + (small ? 22 : 40), Strings.MoonfallOpponentCaps, GoldInk, edge: 0f);
        var gap = small ? 50.0 : 92.0;
        var r = small ? 16f : 30f;
        for (var i = 0; i < duelOpponents.Count && i < OpponentIds.Length; i++)
        {
            var who = duelOpponents[i];
            var info = MoonfallCompanions.Get(who);
            var cx = ox + (i * gap);
            var hit = MenuHit(m, OpponentIds[i], cx - r - 4, oy - r - 4, cx + r + 4, oy + r + 4, out var hovered, out var nav);
            var isSel = i == duelOpponentSel;
            Medallion(m, info.Power, cx, oy, r, glow: isSel ? 0.5f : 0f);
            if (isSel || nav || hovered)
            {
                // Over the medallion's glow, so the choice always shows.
                SelectRing(m, cx, oy, r, nav ? Cream : isSel ? GoldHiInk : MoonfallColor.Hex("#9DC0FF"), 0.95f);
            }

            if (hovered || nav)
            {
                UiMetrics.Tooltip(info.Name);
            }

            if (hit && !isSel)
            {
                SoundClick();
                duelOpponentSel = i;
            }
        }

        var opponent = duelOpponents[duelOpponentSel];
        var oppInfo = MoonfallCompanions.Get(opponent);
        var accent = MoonfallCards.For(oppInfo.Power)?.Accent ?? GoldInk;
        var cx2 = x0 + (small ? 330 : 700);
        var leftW = (float)(cx2 - x0 - (small ? 30 : 60));
        MenuText(m, MoonfallFace.Jupiter, small ? 20 : 34, x0 + (small ? 16 : 34), oy + r + (small ? 22 : 40), duelOpponentName, Tint(accent, 0.2f), edge: 1f, maxWidth: leftW);
        MenuText(m, MoonfallFace.Axis, small ? 12 : 14.5f, x0 + (small ? 16 : 34), oy + r + (small ? 40 : 70), duelRecord, Ink2, edge: 0f, maxWidth: leftW);

        // Who the opponent is, beside them: their role and their line.
        var ry = oy + r + (small ? 16 : 30);
        MenuText(m, MoonfallFace.Axis, small ? 12.5f : 14, cx2, ry, duelOpponentRole, GoldInk, edge: 0f, maxWidth: (float)(x1 - cx2 - 20));
        for (var i = 0; i < duelOpponentLore.Length; i++)
        {
            MenuText(m, MoonfallFace.Axis, small ? 12 : 15, cx2, ry + (small ? 18 : 28) + (i * (small ? 15 : 22)), duelOpponentLore[i], Ink2, edge: 0f);
        }

        // The difficulty.
        var dy = oy + r + (small ? 66 : 120);
        MenuText(m, MoonfallFace.Axis, small ? 12.5f : 14, x0 + (small ? 16 : 34), dy, Strings.MoonfallDifficultyCaps, GoldInk, edge: 0f);
        var dw = small ? 92.0 : 160.0;
        for (var i = 0; i < 3; i++)
        {
            var difficulty = (MoonfallAiDifficulty)i;
            var bx = x0 + (small ? 16 : 34) + (i * (dw + 12));
            var by = dy + (small ? 12 : 18);
            var active = difficulty == duelDifficulty;
            if (MenuTab(m, DifficultyIds[i], bx, by, bx + dw, by + (small ? 26 : 38), Strings.MoonfallDifficultyName(difficulty), active, false, null))
            {
                duelDifficulty = difficulty;
            }
        }

        // The level and the player's companion.
        var ly = dy + (small ? 64 : 100);
        MenuText(m, MoonfallFace.Axis, small ? 12.5f : 14, x0 + (small ? 16 : 34), ly, Strings.MoonfallLevelCaps, GoldInk, edge: 0f);
        var level = levels[duelLevelSel];
        if (level.Level is { } board)
        {
            gameArt?.Warm(board, BoardTwoX(m));
            LevelThumb(m, board, x0 + (small ? 16 : 34), ly + (small ? 12 : 20), small ? 110 : 220);
        }

        var tx = x0 + (small ? 140 : 280);
        var codeW = MenuText(m, MoonfallFace.Trump, small ? 17 : 24, tx, ly + (small ? 28 : 46), duelLevelCode, GoldHiInk, edge: 0.6f);
        MenuText(m, MoonfallFace.Jupiter, small ? 17 : 26, tx + codeW + (small ? 6 : 10), ly + (small ? 28 : 46), duelLevelLine, Cream, edge: 1f, maxWidth: (float)((small ? 170 : 380) - codeW - 10));
        var step = MenuStepper(m, "##mfDuelLevel", tx + (small ? 150 : 260), ly + (small ? 56 : 92), duelLevelPage, small, duelLevelSel > 0, duelLevelSel < levels.Count - 1);
        if (step != 0)
        {
            duelLevelSel = Math.Clamp(duelLevelSel + step, 0, levels.Count - 1);
            SoundClick();
        }

        MenuText(m, MoonfallFace.Axis, small ? 12.5f : 14, cx2, ly, Strings.MoonfallYourCompanionCaps, GoldInk, edge: 0f);
        var picked = CompanionPicker(m, cx2 + (small ? 16 : 30), ly + (small ? 30 : 52), 6, small ? 46 : 80, small ? 14f : 24f, duelCompanion, allowNone: true);
        if (picked != duelCompanion)
        {
            duelCompanion = picked;
            duelCompanionChosen = true;
        }

        MenuText(m, MoonfallFace.Axis, small ? 12 : 14.5f, cx2, ly + (small ? 120 : 210), duelCompanionLine, Ink2, edge: 0f, maxWidth: small ? 270 : 470);
        if (MenuButton(m, "##mfDuelPlay", x1 - (small ? 190 : 300), y1 - (small ? 46 : 74), x1 - (small ? 14 : 34), y1 - (small ? 12 : 22), duelPlay, small ? 20 : 32, isDefault: true))
        {
            PlayDuel(level.Id, duelCompanion, opponent, duelDifficulty);
        }
    }

    /// <summary>The companion Adventure has the player with now (Continue's level's), or none when that one cannot be played with.</summary>
    private MoonfallCompanion AdventureCompanionNow()
    {
        if (continuePlace is not { } next)
        {
            return MoonfallCompanion.None;
        }

        var companion = MoonfallStages.AdventureCompanion(next.Campaign, next.Index);
        if (companion == MoonfallCompanion.None)
        {
            // A free-choice stage: the player's own pick there.
            return FirstAvailable();
        }

        return StateOf(companion) == MoonfallCompanionState.Available ? companion : MoonfallCompanion.None;
    }

    private static int IndexOf(IReadOnlyList<MoonfallCompanion> list, MoonfallCompanion who)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == who)
            {
                return i;
            }
        }

        return -1;
    }

    private void MakeDuelWords(in MenuPen m, IReadOnlyList<MoonfallLevelSlot> levels)
    {
        var key = (menuViewsKey.GetHashCode(), duelOpponentSel, duelDifficulty, duelLevelSel, duelCompanion, m.Small);
        if (key == duelWordsKey)
        {
            return;
        }

        duelWordsKey = key;
        var c = CultureInfo.CurrentCulture;
        var opponent = MoonfallCompanions.Get(duelOpponents[duelOpponentSel]);
        duelOpponentName = string.Format(c, Strings.MoonfallCarrierFormat, opponent.Name, Strings.MoonfallPowerName(opponent.Power));
        var record = progress.DuelRecord(opponent.Companion, duelDifficulty);
        duelRecord = string.Format(c, Strings.MoonfallDuelRecordFormat, Strings.MoonfallDifficultyName(duelDifficulty), record.Wins, record.Losses, record.Draws);
        var slot = levels[duelLevelSel];
        duelLevelCode = slot.Place.Campaign == MoonfallCampaignKind.Expansion ? "FS " + LevelCode(slot.Place.Index) : LevelCode(slot.Place.Index);
        duelLevelLine = LevelNameShown(slot);
        var lore = MoonfallLooks.LoreOf(opponent.Companion);
        duelOpponentRole = lore.Role;
        duelOpponentLore = Wrap(m, MoonfallFace.Axis, m.Small ? 12f : 15f, lore.Line, m.Small ? 270f : 480f);
        duelLevelPage = string.Format(c, Strings.MoonfallPageFormat, duelLevelSel + 1, levels.Count);
        duelCompanionLine = CompanionWords(m, duelCompanion, 400).Line;
        duelPlay = string.Format(c, Strings.MoonfallDuelPlayFormat, ShortName(opponent.Companion));
    }

    // ---- The duel on the board ----

    /// <summary>A companion's face (the card's face crop) in a small lattice ring on the board's chrome; false when there is none to show.</summary>
    private bool FaceRing(in ChromePen c, MoonfallPower power, double cx, double cy, float r)
    {
        var dl = c.Dl;
        var v = c.View;
        dl.AddCircleFilled(v.Map(cx, cy), v.Size(r), Ink(PlateInk), 32);
        var drew = false;
        if (MoonfallCards.For(power) is { } card && gameArt?.Card(power) is { } tex)
        {
            var (fx, fy, fs) = card.Face;
            dl.AddImageRounded(tex.Handle, v.Map(cx - r, cy - r), v.Map(cx + r, cy + r),
                new Vector2(fx / (float)MoonfallCards.CardWidth, fy / (float)MoonfallCards.CardHeight),
                new Vector2((fx + fs) / (float)MoonfallCards.CardWidth, (fy + fs) / (float)MoonfallCards.CardHeight), uint.MaxValue, v.Size(r));
            drew = true;
        }

        GiltRing(c, cx, cy, r);
        return drew;
    }

    private string duelYou = string.Empty;
    private string duelFoe = string.Empty;
    private string duelFoeShown = string.Empty;
    private float duelFoePx;
    private string duelYouScore = string.Empty;
    private string duelFoeScore = string.Empty;
    private string duelYourShot = string.Empty;
    private string duelFoeThinking = string.Empty;
    private string duelFoeShot = string.Empty;
    private (long You, long Foe, int Language, float Scale, bool Fonts) duelHudFor = (-1, -1, -1, 0f, false);
    private (int You, int Foe) duelBallsFor = (-1, -1);
    private string duelYouBalls = string.Empty;
    private string duelFoeBalls = string.Empty;
    private string duelBallsLabel = string.Empty;
    private MoonfallDuel? duelNamesFor;
    private int duelNamesLanguage = -1;

    /// <summary>The fewest letters the opponent's name is cut to; shorter, it is left out (the face and the caption carry it).</summary>
    private const int MinNameLetters = 4;

    /// <summary>
    /// The duel's words that change only with the duel and the language: YOU, the opponent's name, and the turn captions
    /// in capitals ("YOUR SHOT", "LOUISOIX IS THINKING", "THE TWINS ARE THINKING"). A new duel remakes them, so a duel
    /// left before its first shot never lends its opponent's name to the next.
    /// </summary>
    private void RefreshDuelNames(MoonfallDuel d)
    {
        if (ReferenceEquals(d, duelNamesFor) && duelNamesLanguage == Localization.Loc.Version)
        {
            return;
        }

        duelNamesFor = d;
        duelNamesLanguage = Localization.Loc.Version;
        duelHudFor = (-1, -1, -1, 0f, false);
        duelBallsFor = (-1, -1);
        var culture = CultureInfo.CurrentCulture;
        var opponent = d.Companion(MoonfallDuel.OpponentSide);
        var name = MoonfallCompanions.TryGet(opponent, out var info) ? ShortName(info.Companion) : string.Empty;
        duelYou = Strings.MoonfallDuelYouCaps;
        duelFoe = name.ToUpper(culture);
        duelBallsLabel = Strings.MoonfallHudBalls;
        duelYourShot = Strings.MoonfallDuelYourShot.ToUpper(culture);
        duelFoeThinking = (opponent == MoonfallCompanion.Twins ? Strings.MoonfallDuelTwinsThinking : string.Format(culture, Strings.MoonfallDuelThinkingFormat, name)).ToUpper(culture);
        duelFoeShot = (opponent == MoonfallCompanion.Twins ? Strings.MoonfallDuelTwinsShot : string.Format(culture, Strings.MoonfallDuelTheirShotFormat, name)).ToUpper(culture);
    }

    /// <summary>The caption of whose shot it is now, for the rich rail and the plain bar alike; empty once the duel is decided.</summary>
    private string DuelTurnText(MoonfallDuel d) =>
        d.Outcome != MoonfallDuelOutcome.Undecided ? string.Empty
        : d.Turn == MoonfallDuel.PlayerSide ? duelYourShot
        : d.Opponent.Thinking ? duelFoeThinking
        : duelFoeShot;

    /// <summary>
    /// A duel's top rail. The name plate is the player's side (their companion's face, YOU and their score), the score
    /// plate the opponent's (its face, its name fitted beside the score, and its score). The side to shoot is lit: its
    /// plate glows in its companion's colour with a rim of it (breathing ±15% over 3 s, still under Reduce motion), the
    /// other plate is dimmed under its words, and a caption under the lit plate says whose shot it is, "LOUISOIX IS
    /// THINKING" with its dots while the opponent weighs its shot.
    /// </summary>
    private void DuelPlates(in ChromePen c, MoonfallDuel d)
    {
        var dl = c.Dl;
        var v = c.View;
        RefreshDuelNames(d);
        var you = d.ShownScore(MoonfallDuel.PlayerSide);
        var foe = d.ShownScore(MoonfallDuel.OpponentSide);
        var foeInfo = MoonfallCompanions.TryGet(d.Companion(MoonfallDuel.OpponentSide), out var oi) ? oi : null;
        var youInfo = MoonfallCompanions.TryGet(d.Companion(MoonfallDuel.PlayerSide), out var pi) ? pi : null;
        var scorePx = NumberPx(v, 26, MoonfallFace.Trump);
        var youBalls = d.BallsLeft(MoonfallDuel.PlayerSide);
        var foeBalls = d.BallsLeft(MoonfallDuel.OpponentSide);
        if (duelBallsFor != (youBalls, foeBalls))
        {
            duelBallsFor = (youBalls, foeBalls);
            duelYouBalls = youBalls.ToString(CultureInfo.CurrentCulture);
            duelFoeBalls = foeBalls.ToString(CultureInfo.CurrentCulture);
        }

        var fontsReady = fonts is { } f && f.TryGet(MoonfallFace.Axis, 12f, out _);
        if (duelHudFor != (you, foe, Localization.Loc.Version, v.Scale, fontsReady))
        {
            duelHudFor = (you, foe, Localization.Loc.Version, v.Scale, fontsReady);
            var culture = CultureInfo.CurrentCulture;
            duelYouScore = you.ToString("N0", culture);
            duelFoeScore = foe.ToString("N0", culture);

            // The opponent's name fits the room left of its score: shrunk as far as the label floor, then cut short,
            // and left out rather than cut below four letters.
            var room = v.Size(701 - 618 - 6) - MeasureText(MoonfallFace.Trump, scorePx, duelFoeScore);
            var px = NamePx(v, 11.5f, MoonfallFace.Axis);
            var floor = NamePx(v, 1f, MoonfallFace.Axis);
            var w = MeasureText(MoonfallFace.Axis, px, duelFoe);
            if (w > room && w > 0)
            {
                px = MathF.Max(floor, px * room / w);
            }

            duelFoePx = px;
            duelFoeShown = duelFoe.Length > 0 ? FitLine(MoonfallFace.Axis, px, duelFoe, room) : duelFoe;
            var letters = duelFoeShown.EndsWith("...", StringComparison.Ordinal) ? duelFoeShown.Length - 3 : duelFoeShown.Length;
            if (MeasureText(MoonfallFace.Axis, px, duelFoeShown) > room || (letters < MinNameLetters && letters < duelFoe.Length))
            {
                duelFoeShown = string.Empty;
            }
        }

        var playerTurn = d.Turn == MoonfallDuel.PlayerSide && d.Outcome == MoonfallDuelOutcome.Undecided;
        var foeTurn = d.Turn == MoonfallDuel.OpponentSide && d.Outcome == MoonfallDuelOutcome.Undecided;
        var breath = MoonfallMotion.Breath(boardClock, 3f, 0.15f, motion == MoonfallMotionLevel.Still);
        var youAccent = MoonfallCards.For(youInfo?.Power ?? MoonfallPower.None)?.Accent ?? GoldHiInk;
        var foeAccent = MoonfallCards.For(foeInfo?.Power ?? MoonfallPower.None)?.Accent ?? GoldHiInk;

        // The player's plate.
        var (nx0, ny0, nx1, ny1) = MoonfallHud.NamePlate;
        if (playerTurn)
        {
            TurnGlow(c, nx0, ny0, nx1, ny1, youAccent, breath);
        }

        // The waiting plate is dimmed under its words (the pill and the face), so its label keeps its contrast.
        Pill(c, nx0, ny0, nx1, ny1);
        var youFace = FaceRing(c, youInfo?.Power ?? MoonfallPower.None, 103, 21, 10);
        if (foeTurn)
        {
            IdleDim(c, nx0, ny0, nx1, ny1);
        }

        if (!youFace)
        {
            DrawText(dl, MoonfallFace.Trump, NumberPx(v, 16.5f, MoonfallFace.Trump), v.Map(103, 21.5), Anchor.Centre, Ink(GoldHiInk), stageText, Ink(EdgeInk), v.Size(0.6));
        }

        DrawText(dl, MoonfallFace.Jupiter, NamePx(v, 13f, MoonfallFace.Jupiter), v.Map(124, 21), Anchor.Left, Ink(playerTurn ? Tint(youAccent, 0.3f) : Cream), duelYou, Ink(EdgeInk), v.Size(0.8));
        DrawText(dl, MoonfallFace.Trump, NumberPx(v, 24, MoonfallFace.Trump), v.Map(nx1 - 14, 21.5), Anchor.Right, Ink(GoldHiInk), duelYouScore, Ink(MoonfallColor.Hex("#120A02")), v.Size(0.8));

        // The opponent's plate.
        var (sx0, sy0, sx1, sy1) = MoonfallHud.ScorePlate;
        if (foeTurn)
        {
            TurnGlow(c, sx0, sy0, sx1, sy1, foeAccent, breath);
        }

        Pill(c, sx0, sy0, sx1, sy1);
        dl.AddRectFilled(v.Map(614, 12.5), v.Map(706, 29.5), Ink(MoonfallColor.Hex("#060A1E")), v.Size(8.5));
        FaceRing(c, foeInfo?.Power ?? MoonfallPower.None, 601, 21, 10);
        if (playerTurn)
        {
            IdleDim(c, sx0, sy0, sx1, sy1);
        }

        if (duelFoeShown.Length > 0)
        {
            DrawText(dl, MoonfallFace.Axis, duelFoePx, v.Map(618, 21), Anchor.Left, Ink(foeTurn ? Tint(foeAccent, 0.3f) : Cream), duelFoeShown);
        }

        DrawText(dl, MoonfallFace.Trump, scorePx, v.Map(701, 21.5), Anchor.Right, Ink(GoldHiInk), duelFoeScore, Ink(MoonfallColor.Hex("#120A02")), v.Size(0.8));

        // Whose shot it is, under the lit plate; under the waiting one, its balls left (the tube counts the shooter's).
        if (playerTurn)
        {
            TurnCaption(c, (nx0 + nx1) / 2, DuelTurnText(d), youAccent, thinking: false);
            BallsChip(c, (sx0 + sx1) / 2, duelFoeBalls);
        }
        else if (foeTurn)
        {
            TurnCaption(c, (sx0 + sx1) / 2, DuelTurnText(d), foeAccent, d.Opponent.Thinking);
            BallsChip(c, (nx0 + nx1) / 2, duelYouBalls);
        }
    }

    /// <summary>A chip <paramref name="w"/> units wide centred on <paramref name="cx"/>, kept inside the rail's plates (86–716): it never runs past the wall.</summary>
    private static (double X0, double X1) ChipSpan(double cx, double w)
    {
        var x0 = cx - (w / 2);
        var x1 = cx + (w / 2);
        var (left, _, _, _) = MoonfallHud.NamePlate;
        var (_, _, right, _) = MoonfallHud.ScorePlate;
        if (x1 > right)
        {
            (x0, x1) = (x0 - (x1 - right), right);
        }

        if (x0 < left)
        {
            (x0, x1) = (left, x1 + (left - x0));
        }

        return (x0, x1);
    }

    /// <summary>The waiting side's balls left, on a quiet dark chip under its plate: BALLS and the count.</summary>
    private void BallsChip(in ChromePen c, double cx, string count)
    {
        var dl = c.Dl;
        var v = c.View;
        var labelPx = NamePx(v, 9.5f, MoonfallFace.Axis);
        var numberPx = NumberPx(v, 13f, MoonfallFace.Trump);
        var lw = MeasureText(MoonfallFace.Axis, labelPx, duelBallsLabel) / v.Scale;
        var nw = MeasureText(MoonfallFace.Trump, numberPx, count) / v.Scale;
        var (x0, x1) = ChipSpan(cx, lw + 5 + nw + 14);
        const double Y0 = 43, Y1 = 58;
        dl.AddRectFilled(v.Map(x0, Y0), v.Map(x1, Y1), Ink(MoonfallColor.Hex("#060816"), 0.75f), v.Size(4));
        dl.AddRect(v.Map(x0, Y0), v.Map(x1, Y1), Ink(LabelInk, 0.35f), v.Size(4), ImDrawFlags.None, MathF.Max(1f, v.Size(1)));
        DrawText(dl, MoonfallFace.Axis, labelPx, v.Map(x0 + 7, (Y0 + Y1) / 2), Anchor.Left, Ink(LabelInk, 0.9f), duelBallsLabel);
        DrawText(dl, MoonfallFace.Trump, numberPx, v.Map(x1 - 7, ((Y0 + Y1) / 2) + 0.5), Anchor.Right, Ink(Ink2), count, Ink(EdgeInk), v.Size(0.6));
    }

    /// <summary>The side to shoot: a glow round its plate in its companion's colour, and a rim of it.</summary>
    private static void TurnGlow(in ChromePen c, float x0, float y0, float x1, float y1, Vector3 accent, float breath)
    {
        var v = c.View;
        var r = ((y1 - y0) / 2) + 5;
        c.Dl.AddRectFilled(v.Map(x0 - 6, y0 - 5), v.Map(x1 + 6, y1 + 5), Ink(accent, Math.Clamp(0.5f * breath, 0f, 1f)), v.Size(r + 1));
        c.Dl.AddRect(v.Map(x0 - 3, y0 - 2.5), v.Map(x1 + 3, y1 + 2.5), Ink(Tint(accent, 0.2f), 0.95f), v.Size(r - 2), ImDrawFlags.None, MathF.Max(1.5f, v.Size(1.6)));
    }

    /// <summary>The side waiting: its plate dimmed to about 60%.</summary>
    private static void IdleDim(in ChromePen c, float x0, float y0, float x1, float y1)
    {
        var v = c.View;
        c.Dl.AddRectFilled(v.Map(x0, y0), v.Map(x1, y1), Ink(MoonfallColor.Hex("#03040C"), 0.4f), v.Size((y1 - y0) / 2));
    }

    /// <summary>
    /// The caption under the lit plate, on a dark plate of its own in the top band above the pieces: whose shot it is, and
    /// while the opponent thinks, three dots breathing in turn over 2.4 s (still under Reduce motion).
    /// </summary>
    private void TurnCaption(in ChromePen c, double cx, string text, Vector3 accent, bool thinking)
    {
        var dl = c.Dl;
        var v = c.View;
        var px = NamePx(v, 12f, MoonfallFace.Jupiter);
        var w = MeasureText(MoonfallFace.Jupiter, px, text) / v.Scale;
        var dots = thinking ? 18.0 : 0.0;
        var (x0, x1) = ChipSpan(cx, w + dots + 16);
        const double Y0 = 43, Y1 = 58;
        dl.AddRectFilled(v.Map(x0, Y0), v.Map(x1, Y1), Ink(MoonfallColor.Hex("#060816"), 0.85f), v.Size(4));
        dl.AddRect(v.Map(x0, Y0), v.Map(x1, Y1), Ink(accent, 0.6f), v.Size(4), ImDrawFlags.None, MathF.Max(1f, v.Size(1)));
        DrawText(dl, MoonfallFace.Jupiter, px, v.Map(x0 + 8, (Y0 + Y1) / 2), Anchor.Left, Ink(Tint(accent, 0.3f)), text, Ink(EdgeInk), v.Size(0.7));
        if (!thinking)
        {
            return;
        }

        var still = motion == MoonfallMotionLevel.Still;
        for (var i = 0; i < 3; i++)
        {
            var phase = (float)((boardClock / 2.4 * 2 * Math.PI) - (i * 0.9));
            var a = still ? 0.85f : 0.40f + (0.55f * MathF.Max(0f, MathF.Sin(phase)));
            dl.AddCircleFilled(v.Map(x0 + 8 + w + 5 + (i * 5), ((Y0 + Y1) / 2) + 2), MathF.Max(1.2f, v.Size(1.6)), Ink(Tint(accent, 0.3f), a), 10);
        }
    }
}

