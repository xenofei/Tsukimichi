using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Ui;

/// <summary>
/// The title (spec-rich2.md §4, screens2.title): Sohm Al jewel-graded behind MOONFALL in gilt Jupiter over the journal's
/// crest rule, the modes as gilt pills, and the Continue card, the default focus: the next board, its companion's
/// portrait and power in their colour, and Continue. Challenges stay locked (slate, saying why) until The Moon Road is
/// won. At 640 Continue is the first pill. The menus' shared views (the stages, the companions' states, the levels Quick
/// Play offers) are made here, once a screen and whenever the progress changes.
/// </summary>
public sealed partial class MoonfallWindow
{
    // ---- The menus' views, made once a screen (and when the progress or the language changes) ----

    private int menuViewsFor = -1;
    private (int Epoch, int Version, int Language) menuViewsKey;
    private IReadOnlyList<MoonfallStageView> baseStages = [];
    private IReadOnlyList<MoonfallStageView> farStages = [];
    private bool farOpen;
    private MoonfallLevelPlace? continuePlace;
    private readonly MoonfallCompanionState[] companionStates = new MoonfallCompanionState[MoonfallCompanions.Count + 1];
    private IReadOnlyList<MoonfallLevelSlot> quickLevels = [];
    private IReadOnlyList<MoonfallCompanion> duelOpponents = [];
    private int quickLevelsEpoch = -1;

    // The title's words.
    private string titleAdventureSub = string.Empty;
    private string titleChallengesSub = string.Empty;
    private string titleDuelSub = string.Empty;
    private string titleContinue = string.Empty;
    private string titleContinueCode = string.Empty;
    private string titleContinueName = string.Empty;
    private string titleContinueLine = string.Empty;
    private string titleContinueWith = string.Empty;
    private string titleContinueSmall = string.Empty;
    private MoonfallLevel? titleContinueLevel;
    private MoonfallPower titleContinuePower;
    private string versionText = string.Empty;

    private void RefreshMenuViews()
    {
        var key = (progressEpoch, flow.Version, Localization.Loc.Version);
        if (menuViewsFor >= 0 && key == menuViewsKey)
        {
            return;
        }

        menuViewsKey = key;
        menuViewsFor = 0;
        baseStages = modes.Stages(MoonfallCampaignKind.Base);
        farStages = modes.Stages(MoonfallCampaignKind.Expansion);
        farOpen = modes.CampaignOpen(MoonfallCampaignKind.Expansion);
        continuePlace = modes.Continue();
        foreach (var info in MoonfallCompanions.All)
        {
            companionStates[(int)info.Companion] = modes.CompanionState(info.Companion);
        }

        _ = QuickLevels();
        duelOpponents = modes.DuelOpponents();
        MakeTitleWords();
        screenWordsFor = -1;
    }

    /// <summary>The levels Quick Play offers (and its Next follows), made when the progress changes.</summary>
    private IReadOnlyList<MoonfallLevelSlot> QuickLevels()
    {
        if (quickLevelsEpoch != progressEpoch)
        {
            quickLevelsEpoch = progressEpoch;
            quickLevels = modes.QuickPlayLevels();
        }

        return quickLevels;
    }

    /// <summary>How <paramref name="companion"/> shows now (the spoiler shield and the progress).</summary>
    private MoonfallCompanionState StateOf(MoonfallCompanion companion) =>
        (uint)companion < (uint)companionStates.Length ? companionStates[(int)companion] : MoonfallCompanionState.NotMet;

    /// <summary>The stages of <paramref name="kind"/> as the map shows them.</summary>
    private IReadOnlyList<MoonfallStageView> StagesOf(MoonfallCampaignKind kind) => kind == MoonfallCampaignKind.Expansion ? farStages : baseStages;

    private void MakeTitleWords()
    {
        var c = CultureInfo.CurrentCulture;
        versionText = typeof(MoonfallWindow).Assembly.GetName().Version is { } version
            ? string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}.{version.Build}")
            : string.Empty;

        // Adventure: where the road stands.
        var at = continuePlace ?? new MoonfallLevelPlace(MoonfallCampaignKind.Base, Math.Min(progress.BaseCleared, MoonfallStages.BaseLevels - 1));
        var stages = MoonfallStages.Of(at.Campaign).Count;
        titleAdventureSub = string.Format(c, Strings.MoonfallTitleAdventureSubFormat, Strings.MoonfallCampaignName(at.Campaign), at.Stage, stages);

        // Challenges: how many are won, or why they are sealed.
        if (modes.ChallengesOpen)
        {
            var done = 0;
            foreach (var challenge in modes.Challenges)
            {
                done += progress.IsChallengeDone(challenge.Id) ? 1 : 0;
            }

            titleChallengesSub = string.Format(c, Strings.MoonfallTitleChallengesSubFormat, done, modes.Challenges.Count);
        }
        else
        {
            titleChallengesSub = Strings.MoonfallChallengesSealed;
        }

        var opponent = modes.TitleOpponent();
        titleDuelSub = MoonfallCompanions.TryGet(opponent, out var foe)
            ? string.Format(c, Strings.MoonfallTitleDuelSubFormat, foe.Name)
            : Strings.MoonfallTitleDuelNone;

        // The Continue card.
        titleContinueLevel = null;
        titleContinuePower = MoonfallPower.None;
        if (continuePlace is { } next && modes.Slot(next.Campaign, next.Index) is { Level: { } level } slot)
        {
            titleContinueLevel = level;
            titleContinueCode = LevelCode(next.Index);
            titleContinueName = level.Name;
            titleContinue = string.Format(c, Strings.MoonfallContinueFormat, titleContinueCode);
            var companion = MoonfallStages.AdventureCompanion(next.Campaign, next.Index);
            titleContinuePower = MoonfallCompanions.TryGet(companion, out var info) ? info.Power : MoonfallPower.None;
            var best = slot.Best > 0 ? string.Format(c, Strings.MoonfallBestFormat, slot.Best.ToString("N0", c)) : Strings.MoonfallNotYetWon;
            titleContinueLine = slot.Ace is { } ace ? string.Format(c, Strings.MoonfallBestAceFormat, best, ace.ToString("N0", c)) : best;
            titleContinueWith = titleContinuePower == MoonfallPower.None
                ? Strings.MoonfallYourPick
                : string.Format(c, Strings.MoonfallWithFormat, ShortName(companion));
            titleContinueSmall = titleContinuePower == MoonfallPower.None
                ? titleContinueName
                : string.Format(c, Strings.MoonfallContinueSmallFormat, titleContinueName, ShortName(companion));
        }
        else
        {
            titleContinueCode = string.Empty;
            titleContinueName = Strings.MoonfallRoadGoesOn;
            titleContinueLine = Strings.MoonfallRoadGoesOnLine;
            titleContinue = Strings.MoonfallScreenMap;
            titleContinueWith = string.Empty;
            titleContinueSmall = Strings.MoonfallRoadGoesOn;
        }
    }

    /// <summary>A companion's name as the menus say it in a line ("Cid", "the twins", "Moogle").</summary>
    private static string ShortName(MoonfallCompanion companion) => companion switch
    {
        MoonfallCompanion.Cid => "Cid",
        MoonfallCompanion.Twins => Strings.MoonfallTheTwins,
        MoonfallCompanion.Moogle => "Moogle",
        _ => MoonfallCompanions.TryGet(companion, out var info) ? info.Name : string.Empty,
    };

    // ---- The title ----

    private void DrawTitle(in MenuPen m)
    {
        var dl = m.Dl;
        // The backdrop: Sohm Al, graded; the night stands in while it builds.
        if (gameArt?.Backdrop(MoonfallBackdrop.Title) is { } backdrop)
        {
            Cover(dl, backdrop.Handle, m.AreaMin, m.AreaMax, Vector2.Zero, Vector2.One, MoonfallBackdrops.TitleWidth / (float)MoonfallBackdrops.TitleHeight);
        }
        else
        {
            if (gameArt is not null)
            {
                menuArtPending++;
            }

            JewelNight(m);
        }

        var small = m.Small;
        var v = m.V;
        if (!small)
        {
            // The modes' side darkened so the gilt reads (screens2: 600 to 140 units, 55%).
            var dark = Ink(MoonfallColor.Hex("#04050E"), 0.55f);
            var clear = Ink(MoonfallColor.Hex("#04050E"), 0f);
            dl.AddRectFilled(m.AreaMin, new Vector2(v.Map(140, 0).X, m.AreaMax.Y), dark);
            dl.AddRectFilledMultiColor(new Vector2(v.Map(140, 0).X, m.AreaMin.Y), new Vector2(v.Map(600, 0).X, m.AreaMax.Y), dark, clear, clear, dark);
            Moon(m, 200, 112, 40);
        }
        else
        {
            var dark = Ink(MoonfallColor.Hex("#04050E"), 0.5f);
            var clear = Ink(MoonfallColor.Hex("#04050E"), 0f);
            dl.AddRectFilledMultiColor(v.Map(100, 110), v.Map(320, 480), clear, dark, dark, clear);
            dl.AddRectFilledMultiColor(v.Map(320, 110), v.Map(540, 480), dark, clear, clear, dark);
            Moon(m, 86, 60, 22);
        }

        Moondust(m, small ? 18 : 36);
        Logotype(m, small ? 320 : 330, small ? 86 : 236, small ? 66 : 118, small ? 20 : 34);
        if (small)
        {
            TitleSmall(m);
        }
        else
        {
            TitleLarge(m);
        }

        PegMarksHint(m);
        MenuText(m, MoonfallFace.Axis, small ? 14 : 12, small ? 16 : 40, small ? 466 : 776, versionText, Ink3, edge: 1f);
    }

    private void TitleLarge(in MenuPen m)
    {
        var y = 350.0;
        if (MenuButton(m, "##mfAdventure", 172, y, 488, y + 54, Strings.MoonfallScreenAdventure, 36, sub: titleAdventureSub))
        {
            Open(MoonfallScreen.Map);
        }

        y += 66;
        if (MenuButton(m, "##mfQuick", 172, y, 488, y + 54, Strings.MoonfallScreenQuickPlay, 36, sub: Strings.MoonfallTitleQuickSub,
            style: QuickLevels().Count > 0 ? MenuStyle.Normal : MenuStyle.Locked, tooltip: QuickLevels().Count > 0 ? null : Strings.MoonfallQuickPlayLocked))
        {
            Open(MoonfallScreen.QuickPlay);
        }

        y += 66;
        var sealedChallenges = !modes.ChallengesOpen;
        if (MenuButton(m, "##mfChallenges", 172, y, 488, y + 54, Strings.MoonfallScreenChallenges, 36, sub: titleChallengesSub,
            style: sealedChallenges ? MenuStyle.Locked : MenuStyle.Normal, tooltip: sealedChallenges ? Strings.MoonfallChallengesSealedTooltip : null))
        {
            Open(MoonfallScreen.Challenges);
        }

        y += 66;
        if (MenuButton(m, "##mfDuel", 172, y, 488, y + 54, Strings.MoonfallScreenDuel, 36, sub: titleDuelSub,
            style: duelOpponents.Count > 0 && QuickLevels().Count > 0 ? MenuStyle.Normal : MenuStyle.Locked))
        {
            Open(MoonfallScreen.Duel);
        }

        y += 66;
        if (MenuButton(m, "##mfCompanions", 172, y + 6, 322, y + 44, Strings.MoonfallScreenCompanions, 18.7f, primaryFace: false))
        {
            Open(MoonfallScreen.Characters);
        }

        if (MenuButton(m, "##mfOptions", 338, y + 6, 488, y + 44, Strings.MoonfallScreenOptions, 18.7f, primaryFace: false))
        {
            Open(MoonfallScreen.Options);
        }

        ContinueCard(m, 852, 520, 1244, 772);
    }

    private void TitleSmall(in MenuPen m)
    {
        var y = 156.0;
        if (ContinueAccent() is { } accent)
        {
            EntryGlow(m, 200, y, 440, y + 40, true, false, accent);
        }

        if (MenuButton(m, "##mfContinue", 200, y, 440, y + 40, titleContinue, 26, isDefault: true))
        {
            Continue();
        }

        MenuText(m, MoonfallFace.Axis, 12.5f, 320, y + 54, titleContinueSmall, Ink2, Anchor.Centre, edge: 1f, maxWidth: 420);
        y += 72;
        if (MenuButton(m, "##mfAdventure", 214, y, 426, y + 34, Strings.MoonfallScreenAdventure, 24))
        {
            Open(MoonfallScreen.Map);
        }

        y += 42;
        if (MenuButton(m, "##mfQuick", 214, y, 426, y + 34, Strings.MoonfallScreenQuickPlay, 24, style: QuickLevels().Count > 0 ? MenuStyle.Normal : MenuStyle.Locked,
            tooltip: QuickLevels().Count > 0 ? null : Strings.MoonfallQuickPlayLocked))
        {
            Open(MoonfallScreen.QuickPlay);
        }

        y += 42;
        var sealedChallenges = !modes.ChallengesOpen;
        if (MenuButton(m, "##mfChallenges", 214, y, 426, y + 34, Strings.MoonfallScreenChallenges, 24, style: sealedChallenges ? MenuStyle.Locked : MenuStyle.Normal,
            tooltip: sealedChallenges ? Strings.MoonfallChallengesSealedTooltip : null))
        {
            Open(MoonfallScreen.Challenges);
        }

        y += 42;
        if (MenuButton(m, "##mfDuel", 214, y, 426, y + 34, Strings.MoonfallScreenDuel, 24, style: duelOpponents.Count > 0 && QuickLevels().Count > 0 ? MenuStyle.Normal : MenuStyle.Locked))
        {
            Open(MoonfallScreen.Duel);
        }

        y += 42;
        if (MenuButton(m, "##mfCompanions", 214, y + 2, 316, y + 32, Strings.MoonfallScreenCompanions, 13.7f, primaryFace: false))
        {
            Open(MoonfallScreen.Characters);
        }

        if (MenuButton(m, "##mfOptions", 324, y + 2, 426, y + 32, Strings.MoonfallScreenOptions, 13.7f, primaryFace: false))
        {
            Open(MoonfallScreen.Options);
        }
    }

    private Vector3? ContinueAccent() => MoonfallCards.For(titleContinuePower)?.Accent;

    /// <summary>The Continue card (the default focus): the next board framed, its code and name, best and Ace, its companion, and Continue.</summary>
    private void ContinueCard(in MenuPen m, double x0, double y0, double x1, double y1)
    {
        var accent = ContinueAccent() ?? GoldInk;
        var dl = m.Dl;
        var v = m.V;
        // The card's own glow in the companion's colour (breathing ±15% over 3 s under Full).
        var breath = MoonfallMotion.Breath(menuClock, 3f, 0.15f, motion != MoonfallMotionLevel.Full);
        for (var k = 3; k >= 1; k--)
        {
            var grow = 4.0 * k;
            dl.AddRectFilled(v.Map(x0 - grow, y0 - grow), v.Map(x1 + grow, y1 + grow), Ink(accent, 0.22f * breath * (4 - k) / 3f), v.Size(10 + grow));
        }

        Panel(m, x0, y0, x1, y1, accent, 0.36);
        var tx = x0 + 212;
        var tw = x1 - 24 - tx;
        if (titleContinueLevel is { } level)
        {
            LevelThumb(m, level, x0 + 24, y0 + 30, 166);
            MenuText(m, MoonfallFace.Axis, 14, tx, y0 + 34, Strings.MoonfallContinueCaps, GoldInk, edge: 0f);
            var cw = MenuText(m, MoonfallFace.Trump, 26, tx, y0 + 62, titleContinueCode, GoldHiInk, edge: 0.6f);
            MenuText(m, MoonfallFace.Jupiter, 28, tx + cw + 12, y0 + 62, titleContinueName, Cream, edge: 1f, maxWidth: (float)(tw - cw - 12));
            MenuText(m, MoonfallFace.Axis, 14, tx, y0 + 88, titleContinueLine, Ink2, edge: 0f, maxWidth: (float)tw);
            if (titleContinuePower != MoonfallPower.None)
            {
                Medallion(m, titleContinuePower, tx + 18, y0 + 128, 17, glow: 0.5f);
                MenuText(m, MoonfallFace.Axis, 14, tx + 44, y0 + 120, titleContinueWith, Cream, edge: 0f);
                MenuText(m, MoonfallFace.Jupiter, 23, tx + 44, y0 + 141, Strings.MoonfallPowerName(titleContinuePower), Tint(accent, 0.2f), edge: 1f);
            }
            else
            {
                MenuText(m, MoonfallFace.Axis, 14, tx, y0 + 128, titleContinueWith, Cream, edge: 0f, maxWidth: (float)tw);
            }
        }
        else
        {
            MenuText(m, MoonfallFace.Jupiter, 28, (x0 + x1) / 2, y0 + 70, titleContinueName, Cream, Anchor.Centre, edge: 1f, maxWidth: (float)(x1 - x0 - 48));
            MenuText(m, MoonfallFace.Axis, 14, (x0 + x1) / 2, y0 + 104, titleContinueLine, Ink2, Anchor.Centre, edge: 0f, maxWidth: (float)(x1 - x0 - 48));
        }

        if (MenuButton(m, "##mfContinue", x0 + 22, y1 - 54, x1 - 24, y1 - 18, titleContinue, 28, isDefault: true))
        {
            Continue();
        }
    }

    /// <summary>Continue: Adventure's next level, straight onto the board; with none shipped yet, the map.</summary>
    private void Continue()
    {
        if (continuePlace is { } next)
        {
            var pick = MoonfallStages.StageOf(next.Campaign, next.Index) is { PlayerPicks: true } ? FirstAvailable() : MoonfallCompanion.None;
            if (PlayAdventure(next.Campaign, next.Index, pick))
            {
                return;
            }
        }

        Open(MoonfallScreen.Map);
    }

    /// <summary>The first companion who can be played with (for a "Your Pick" level started from Continue).</summary>
    private MoonfallCompanion FirstAvailable()
    {
        if (adventurePick != MoonfallCompanion.None && StateOf(adventurePick) == MoonfallCompanionState.Available)
        {
            return adventurePick;
        }

        foreach (var info in MoonfallCompanions.All)
        {
            if (StateOf(info.Companion) == MoonfallCompanionState.Available)
            {
                return info.Companion;
            }
        }

        return MoonfallCompanion.None;
    }

    /// <summary>screens2.logotype: MOONFALL in gilt Jupiter with a warm glow, the journal's crest rule under it, and the line under that.</summary>
    private void Logotype(in MenuPen m, double cx, double y, float size, float subSize)
    {
        var v = m.V;
        var tracking = v.Size(size * 0.06);
        var px = NamePx(v, size, MoonfallFace.Jupiter);
        if (m.HasArt)
        {
            Put(m.A, m.A.Atlas[MoonfallSprite.Soft], cx, y, size * 1.6f / 4f, Ink(MoonfallColor.Hex("#FFB45E"), 0.10f));
        }

        var w = DrawText(m.Dl, MoonfallFace.Jupiter, px, v.Map(cx, y), Anchor.Centre, Ink(GoldHiInk), Strings.MoonfallLogotype, Ink(MoonfallColor.Hex("#140A02")), v.Size(size * 0.019), tracking) / v.Scale;
        // One glint crosses the logotype every 6 s (0.8 s), under Full only.
        var t = MoonfallMotion.Glint(menuClock, 6f, 1.5f, motion != MoonfallMotionLevel.Full);
        if (t >= 0 && m.HasArt)
        {
            var gx = cx - (w / 2) + (t * w);
            m.Dl.PushClipRect(v.Map(cx - (w / 2), y - (size * 0.45)), v.Map(cx + (w / 2), y + (size * 0.45)), true);
            Put(m.A, m.A.Atlas[MoonfallSprite.Soft], gx, y, size * 0.35f / 4f, Ink(MoonfallColor.Hex("#FFF4D0"), 0.35f * MathF.Sin(t * MathF.PI)));
            m.Dl.PopClipRect();
        }

        CrestRule(m, cx, y + (size * 0.42), w * 0.86, true, 0.5 * size / 120);
        MenuText(m, MoonfallFace.Jupiter, subSize, cx, y + (size * 0.66), Strings.MoonfallSubtitle, Cream, Anchor.Centre, edge: 1.2f);
    }

    /// <summary>The title's moon: an emissive disc with its halo breathing (±6% over 6 s under Full).</summary>
    private void Moon(in MenuPen m, double x, double y, float r)
    {
        var breath = MoonfallMotion.Breath(menuClock, 6f, 0.06f, motion == MoonfallMotionLevel.Still);
        if (m.HasArt)
        {
            Put(m.A, m.A.Atlas[MoonfallSprite.Soft], x, y, r * 4.2f / 4f, Ink(MoonfallColor.Hex("#C9D6FF"), 0.16f * breath));
            Put(m.A, m.A.Atlas[MoonfallSprite.Soft], x, y, r * 2.0f / 4f, Ink(MoonfallColor.Hex("#EEF0F8"), 0.30f * breath));
        }

        var v = m.V;
        m.Dl.AddCircleFilled(v.Map(x, y), v.Size(r), Ink(MoonfallColor.Hex("#EEF0F8")), 48);
        // Its seas, faint, lit from the upper left.
        m.Dl.AddCircleFilled(v.Map(x - (r * 0.30), y - (r * 0.15)), v.Size(r * 0.22), Ink(MoonfallColor.Hex("#C9CFE6"), 0.65f), 24);
        m.Dl.AddCircleFilled(v.Map(x + (r * 0.25), y + (r * 0.20)), v.Size(r * 0.28), Ink(MoonfallColor.Hex("#C9CFE6"), 0.55f), 24);
        m.Dl.AddCircleFilled(v.Map(x + (r * 0.10), y - (r * 0.40)), v.Size(r * 0.14), Ink(MoonfallColor.Hex("#D2D8EC"), 0.6f), 20);
    }
}
