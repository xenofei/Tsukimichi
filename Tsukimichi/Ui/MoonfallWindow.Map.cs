using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Ui;

/// <summary>
/// Adventure's map and level select (spec-rich2.md §4, screens2.adventure_map and levels). The map: the world map as a
/// moonlit chart, The Moon Road and The Far Shore as tabs (The Far Shore opens once The Moon Road is won), the road
/// walked in gilt and the rest in dashes drifting on, each stop a companion's portrait in a lattice ring in one of the
/// four states (won: a lit orange moon; here: a glow in the carrier's colour; not reached: drained, with a padlock; not
/// met: the card back) or the free-choice star, each number on its own plate, the selected stage's panel with its five
/// levels and Play, a legend, and a tooltip for the stop under the mouse or the focus. Level select: the stage's five
/// tiles (the open one glowing in the carrier's colour, sealed ones the darkened card back with a padlock), best and
/// ACED, and the Play strip with the Ace score.
/// </summary>
public sealed partial class MoonfallWindow
{
    /// <summary>The Moon Road's stops on the world map, in its own pixels (screens2.BASE_STOPS).</summary>
    private static readonly Vector2[] BaseStops =
    [
        new(245, 712), new(300, 760), new(440, 750), new(445, 645), new(545, 610), new(478, 520),
        new(395, 470), new(620, 560), new(760, 640), new(920, 700), new(1060, 760),
    ];

    /// <summary>
    /// [J] The Far Shore's stops: the voyage out from the western harbours along the southern seas, past the islands to
    /// the far east, where the road ends at the moon (decision 20). Kept clear of the panel and the legend at 1280.
    /// </summary>
    private static readonly Vector2[] FarStops =
    [
        new(215, 640), new(275, 590), new(350, 620), new(420, 690), new(510, 735), new(600, 700),
        new(680, 745), new(765, 790), new(850, 745), new(935, 785), new(1015, 735), new(1110, 775),
    ];

    private MoonfallCampaignKind mapCampaign = MoonfallCampaignKind.Base;
    private int mapStage = -1;
    private int levelsSel = -1;
    private MoonfallCompanion levelsPick = MoonfallCompanion.None;

    // The map's and level select's words and the road's points, made when the screen, its selection or the progress changes.
    private int screenWordsFor = -1;
    private (int Views, MoonfallCampaignKind Campaign, int Stage, int Level, MoonfallCompanion Pick, bool Small) screenWordsKey;
    private string mapCount = string.Empty;
    private string[] stopNumbers = [];
    private string[] stopTips = [];
    private string[] stopTipLines = [];
    private bool[] stopComing = [];
    private bool[] stopVeiled = [];
    private string[] panelVeiledLines = [];

    /// <summary>The area the selected stage hides while it is set past the player's story (what "Reveal this name" reveals); null otherwise.</summary>
    private string? panelVeiledZone;

    // ---- The spoiler shield's names (every stage or level name the screens draw goes through these) ----

    /// <summary>A stage's name as drawn: the shield's placeholder ("Endwalker area 3") while it is set past the story.</summary>
    private string StageNameShown(MoonfallStage stage) => modes.StageName(stage);

    /// <summary>A level's name as drawn on the menus: none of a veiled stage's ("Past your story"), "On its way" for one not built.</summary>
    private static string LevelNameShown(in MoonfallLevelSlot slot) =>
        slot.State == MoonfallLevelState.Veiled ? Strings.MoonfallVeiledLevel : slot.Level?.Name ?? Strings.MoonfallLevelComing;

    /// <summary>A level's name where it is played (the HUD, the pause, the tally): "Past your story" should the shield come to hide it mid-level.</summary>
    private string PlayLevelName(MoonfallLevel level) => modes.LevelVeiled(level.Id) ? Strings.MoonfallVeiledLevel : level.Name;

    /// <summary>
    /// A placeholder's hover and right-click over <paramref name="x0"/>,<paramref name="y0"/>–<paramref name="x1"/>,<paramref name="y1"/>:
    /// the shield's own three-line hover, and "Reveal this name" for the hidden area (Tsukimichi's reveal, for the
    /// session), as every placeholder in Tsukimichi answers.
    /// </summary>
    private void ShieldPlaceholder(in MenuPen m, double x0, double y0, double x1, double y1, string zone, string shown)
    {
        var min = m.V.Map(x0, y0);
        var max = m.V.Map(x1, y1);
        if (ShieldSession is { } session)
        {
            ShieldText.Interact(min, max, session, SpoilerKind.Area, zone, shown);
        }
        else if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(min, max))
        {
            ShieldText.Hover();
        }
    }

    /// <summary>
    /// A stage past the player's story: a cool slate veil over its ring's face (desaturated under an indigo wash),
    /// unlike the progress drain, so a veiled stop never looks more open than a sealed one.
    /// </summary>
    private static void StoryVeil(in MenuPen m, double x, double y, float r)
    {
        var c = m.V.Map(x, y);
        m.Dl.AddCircleFilled(c, m.V.Size(r * 1.04), Ink(MoonfallColor.Hex("#5A6488"), 0.45f), 32);
        m.Dl.AddCircleFilled(c, m.V.Size(r * 1.04), Ink(MoonfallColor.Hex("#141A3A"), 0.45f), 32);
    }

    /// <summary>
    /// The spoiler shield's mark (Tsukimichi's eye-slash, as on the Spoilers help and settings), drawn small at
    /// <paramref name="x"/>,<paramref name="y"/> on a dark disc: a stage set past the player's story. Not the padlock,
    /// which is Moonfall's own progress.
    /// </summary>
    private static void ShieldMark(in MenuPen m, double x, double y, double s)
    {
        var dl = m.Dl;
        var v = m.V;
        dl.AddCircleFilled(v.Map(x, y), v.Size(s * 1.25), Ink(MoonfallColor.Hex("#0A0E22"), 0.95f), 24);
        var ink = Ink(MoonfallColor.Hex("#C9CFE6"));
        if (v.Size(s) < 7f)
        {
            // Small (640): the almond and the slash alone, at 1.5 px, so it still reads as an eye struck through.
            dl.PathArcTo(v.Map(x, y + (s * 0.6)), v.Size(s * 1.0), MathF.PI * 1.2f, MathF.PI * 1.8f, 10);
            dl.PathStroke(ink, ImDrawFlags.None, 1.5f);
            dl.PathArcTo(v.Map(x, y - (s * 0.6)), v.Size(s * 1.0), MathF.PI * 0.2f, MathF.PI * 0.8f, 10);
            dl.PathStroke(ink, ImDrawFlags.None, 1.5f);
            dl.AddLine(v.Map(x - (s * 0.75), y - (s * 0.75)), v.Map(x + (s * 0.75), y + (s * 0.75)), ink, 1.5f);
            return;
        }

        var w = MathF.Max(1f, v.Size(s * 0.16));

        // The eye: two arcs meeting at its corners, and the pupil.
        dl.PathArcTo(v.Map(x, y + (s * 0.55)), v.Size(s * 0.95), MathF.PI * 1.22f, MathF.PI * 1.78f, 12);
        dl.PathStroke(ink, ImDrawFlags.None, w);
        dl.PathArcTo(v.Map(x, y - (s * 0.55)), v.Size(s * 0.95), MathF.PI * 0.22f, MathF.PI * 0.78f, 12);
        dl.PathStroke(ink, ImDrawFlags.None, w);
        dl.AddCircleFilled(v.Map(x, y), v.Size(s * 0.24), ink, 12);

        // The slash, with a dark cut either side so it reads at a few pixels.
        dl.AddLine(v.Map(x - (s * 0.72), y - (s * 0.72)), v.Map(x + (s * 0.72), y + (s * 0.72)), Ink(MoonfallColor.Hex("#0A0E22")), w * 2.4f);
        dl.AddLine(v.Map(x - (s * 0.72), y - (s * 0.72)), v.Map(x + (s * 0.72), y + (s * 0.72)), ink, w);
    }
    private bool panelComing;

    /// <summary>The stage panel's Play: lit when there is a level to play, waiting (no padlock) when its levels are on their way, else sealed.</summary>
    private MenuStyle PanelPlayStyle => panelPlayable ? MenuStyle.Normal : panelVeiledZone is not null ? MenuStyle.Veiled : panelComing ? MenuStyle.Waiting : MenuStyle.Locked;
    private string panelState = string.Empty;
    private string panelStage = string.Empty;
    private string panelName = string.Empty;
    private string panelCarrier = string.Empty;
    private string panelPlay = string.Empty;
    private bool panelPlayable;
    private readonly string[] panelCodes = new string[MoonfallCharacters.LevelsPerStage];
    private readonly string[] panelNames = new string[MoonfallCharacters.LevelsPerStage];
    private readonly string[] panelScores = new string[MoonfallCharacters.LevelsPerStage];
    private readonly string[] tileLines = new string[MoonfallCharacters.LevelsPerStage];
    private readonly string[][] tileNames = new string[MoonfallCharacters.LevelsPerStage][];
    private readonly string?[] tileFull = new string?[MoonfallCharacters.LevelsPerStage];
    private string levelsHeader = string.Empty;
    private string levelsLine = string.Empty;
    private string stripName = string.Empty;
    private string[] stripDescription = [];
    private string stripAce = string.Empty;
    private string stripAceSmall = string.Empty;
    private string stripWithSmall = string.Empty;
    private string stripWith = string.Empty;
    private string stripPlay = string.Empty;
    private Vector2[] roadPoints = [];
    private int roadWalked;

    /// <summary>The map's header height and the stops' places in the design's units for the layout.</summary>
    private static (float Head, float K, float CropY, float Ch) ChartLayout(bool small)
    {
        var (w, h, head) = small ? (640f, 480f, 46f) : (1280f, 800f, 62f);
        var ch = 1060f * (h - head) / w;
        return (head, w / 1060f, MathF.Min(400f, 872f - ch), ch);
    }

    private static Vector2 StopAt(Vector2 source, bool small)
    {
        var (head, k, cropY, _) = ChartLayout(small);
        return new Vector2((source.X - 150f) * k, head + ((source.Y - cropY) * k));
    }

    /// <summary>
    /// The chart over the window's area: the world map's strip mapped so that its design rectangle lands where the
    /// screens2 crop puts it (the stops' places), extended to the area's edges as far as the strip reaches, darkened by
    /// <paramref name="dim"/>; the night stands in while it builds.
    /// </summary>
    private void Chart(in MenuPen m, float top, float k, float cropY, float dim)
    {
        var dl = m.Dl;
        dl.AddRectFilled(m.AreaMin, m.AreaMax, Ink(MoonfallColor.Hex("#070C24")));
        if (gameArt?.Backdrop(MoonfallBackdrop.Chart) is not { } chart)
        {
            if (gameArt is not null)
            {
                menuArtPending++;
            }

            JewelNight(m);
            return;
        }

        // Window pixels to the strip's pixels: design units (x, y) show strip pixel (150 + x / k, cropY + (y - top) / k).
        var v = m.V;
        Vector2 Uv(Vector2 screen)
        {
            var ux = (screen.X - v.Origin.X) / v.Scale;
            var uy = (screen.Y - v.Origin.Y) / v.Scale;
            return new Vector2(ux / k / MoonfallBackdrops.ChartRegion.Z, (cropY + ((uy - top) / k)) / MoonfallBackdrops.ChartRegion.W);
        }

        var min = new Vector2(m.AreaMin.X, MathF.Max(m.AreaMin.Y, v.Map(0, top).Y));
        var max = m.AreaMax;
        var uv0 = Uv(min);
        var uv1 = Uv(max);
        // Keep within the strip (a wide window shows the deep colour past its ends rather than a repeat).
        var c0 = Vector2.Clamp(uv0, Vector2.Zero, Vector2.One);
        var c1 = Vector2.Clamp(uv1, Vector2.Zero, Vector2.One);
        var size = max - min;
        var a = min + (size * ((c0 - uv0) / (uv1 - uv0)));
        var b = min + (size * ((c1 - uv0) / (uv1 - uv0)));
        dl.AddImage(chart.Handle, a, b, c0, c1, Ink(new Vector3(1f - dim)));
    }

    /// <summary>The map, open on <paramref name="stage"/> (a stage the road waits at, past the player's story).</summary>
    private void OpenMapAt(MoonfallStage stage)
    {
        mapCampaign = stage.Campaign;
        mapStage = stage.Number - 1;
        if (flow.Current != MoonfallScreen.Map)
        {
            Open(MoonfallScreen.Map);
        }
    }

    private void EnsureMapSelection()
    {
        if (mapCampaign == MoonfallCampaignKind.Expansion && !farOpen)
        {
            mapCampaign = MoonfallCampaignKind.Base;
            mapStage = -1;
        }

        var stages = StagesOf(mapCampaign);
        if (mapStage >= 0 && mapStage < stages.Count)
        {
            return;
        }

        mapStage = 0;
        for (var i = 0; i < stages.Count; i++)
        {
            if (stages[i].Here)
            {
                mapStage = i;
                return;
            }

            if (stages[i].State is MoonfallStageState.Open or MoonfallStageState.Done)
            {
                mapStage = i;
            }
        }
    }

    // ---- The map ----

    private void DrawMap(in MenuPen m)
    {
        EnsureMapSelection();
        var small = m.Small;
        var (head, k, cropY, _) = ChartLayout(small);
        MakeMapWords(m);
        Chart(m, head, k, cropY, 0f);
        var stages = StagesOf(mapCampaign);
        Road(m, stages);
        Stops(m, stages);

        // The selected stage's open level is built ahead, so Play opens onto its scene.
        var levels = stages[mapStage].Levels;
        for (var i = 0; i < levels.Count; i++)
        {
            if (levels[i].State == MoonfallLevelState.Open && levels[i].Level is { } open)
            {
                gameArt?.Warm(open, BoardTwoX(m));
                break;
            }
        }

        // The header: the journal's frame across the top, Back, the campaigns' tabs and the count.
        Panel(m, m.Left - 8, m.Top - 8, m.Right + 8, head - 4, null, 0.36, corners: false);
        if (small)
        {
            if (MenuButton(m, "##mfBack", 8, 9, 72, 37, Strings.MoonfallBack, 13.7f, primaryFace: false))
            {
                Back();
            }

            CampaignTabs(m, 184, 9, 150, 31, 20);
            SmallStagePanel(m, 368, 58, 630, 180);
        }
        else
        {
            if (MenuButton(m, "##mfBack", 22, 16, 122, 48, Strings.MoonfallBack, 17.3f, primaryFace: false))
            {
                Back();
            }

            CampaignTabs(m, 430, 16, 200, 34, 24);
            MenuText(m, MoonfallFace.Axis, 14, m.W - 30, 33, mapCount, Ink2, Anchor.Right, edge: 0f);
            StagePanel(m, 900, 84, 1248, 456);
            Legend(m, 900, 470, 1248, anyVeiled ? 580 : 548);
            StopTooltip(m);
        }
    }

    private void CampaignTabs(in MenuPen m, double x, double y, double w, double h, float size)
    {
        if (MenuTab(m, "##mfTabBase", x, y, x + w, y + h, Strings.MoonfallCampaignName(MoonfallCampaignKind.Base), mapCampaign == MoonfallCampaignKind.Base, false, null))
        {
            mapCampaign = MoonfallCampaignKind.Base;
            mapStage = -1;
        }

        if (MenuTab(m, "##mfTabFar", x + w + 10, y, x + w + 10 + w, y + h, Strings.MoonfallCampaignName(MoonfallCampaignKind.Expansion), mapCampaign == MoonfallCampaignKind.Expansion, !farOpen,
            farOpen ? null : Strings.MoonfallFarShoreSealed))
        {
            mapCampaign = MoonfallCampaignKind.Expansion;
            mapStage = -1;
        }

        _ = size;
    }

    /// <summary>The road: walked in gilt to the stage the player is on, the rest in dashes that drift on along it under Full.</summary>
    private void Road(in MenuPen m, IReadOnlyList<MoonfallStageView> stages)
    {
        if (roadPoints.Length < 2)
        {
            return;
        }

        var dl = m.Dl;
        var v = m.V;
        var small = m.Small;
        var walked = Ink(MoonfallColor.Hex("#FFD98A"), 0.9f);
        var ahead = Ink(MoonfallColor.Hex("#B08A4A"), 0.65f);
        var wWalked = MathF.Max(1.2f, v.Size(small ? 2.4 : 3.2));
        var wAhead = MathF.Max(1f, v.Size(small ? 1.8 : 2.4));
        for (var i = 1; i <= roadWalked && i < roadPoints.Length; i++)
        {
            dl.AddLine(v.Map(roadPoints[i - 1].X, roadPoints[i - 1].Y), v.Map(roadPoints[i].X, roadPoints[i].Y), walked, wWalked);
        }

        // Dashes of 8 with gaps of 6 units, drifting forward at 6 units a second (still under Reduce motion).
        const float Dash = 8f, Gap = 6f;
        var shift = motion == MoonfallMotionLevel.Full ? (float)(menuClock * 6.0 % (Dash + Gap)) : 0f;
        var along = -shift;
        for (var i = Math.Max(1, roadWalked + 1); i < roadPoints.Length; i++)
        {
            var a = roadPoints[i - 1];
            var b = roadPoints[i];
            var length = Vector2.Distance(a, b);
            if (length < 0.01f)
            {
                continue;
            }

            var dir = (b - a) / length;
            var s = along;
            while (s < length)
            {
                var s0 = MathF.Max(s, 0f);
                var s1 = MathF.Min(s + Dash, length);
                if (s1 > s0)
                {
                    var p0 = a + (dir * s0);
                    var p1 = a + (dir * s1);
                    dl.AddLine(v.Map(p0.X, p0.Y), v.Map(p1.X, p1.Y), ahead, wAhead);
                }

                s += Dash + Gap;
            }

            along = s - length;
        }

        _ = stages;
    }

    private void Stops(in MenuPen m, IReadOnlyList<MoonfallStageView> stages)
    {
        var small = m.Small;
        var r = small ? 13f : 22f;
        var spots = mapCampaign == MoonfallCampaignKind.Expansion ? FarStops : BaseStops;
        hoveredStop = -1;
        for (var i = 0; i < stages.Count && i < spots.Length; i++)
        {
            var view = stages[i];
            var look = MoonfallLooks.Stop(view, i < stopComing.Length && stopComing[i]);
            var at = StopAt(spots[i], small);
            var power = MoonfallCompanions.TryGet(view.Stage.Companion, out var info) ? info.Power : MoonfallPower.None;
            var accent = MoonfallCards.For(power)?.Accent ?? GoldInk;
            var hit = MenuHit(m, stopIds[i], at.X - r - 4, at.Y - r - 4, at.X + r + 4, at.Y + r + 18, out var hovered, out var nav);
            if (hovered || nav)
            {
                hoveredStop = i;
            }

            if (look.Glow)
            {
                var breath = MoonfallMotion.Breath(menuClock, 3f, 0.15f, motion != MoonfallMotionLevel.Full);
                if (m.HasArt)
                {
                    Put(m.A, m.A.Atlas[MoonfallSprite.Soft], at.X, at.Y, r * 2.9f / 4f, Ink(accent, 0.62f * breath));
                }
            }

            if (i == mapStage || hovered || nav)
            {
                var ring = nav ? Cream : i == mapStage ? GoldHiInk : MoonfallColor.Hex("#9DC0FF");
                m.Dl.AddCircle(m.V.Map(at.X, at.Y), m.V.Size(r * 1.5), Ink(ring, nav ? 0.95f : 0.55f), 40, MathF.Max(1.5f, m.V.Size(1.6)));
            }

            switch (look.Face)
            {
                case MoonfallStopFace.PickStar:
                    PickStar(m, at.X, at.Y, r, look.Dim);
                    GiltRing(m.C, at.X, at.Y, r, tint: look.Dim ? Ink(new Vector3(0.62f), 0.75f) : uint.MaxValue);
                    break;
                case MoonfallStopFace.CardBack:
                    Medallion(m, power, at.X, at.Y, r, back: true, dimRing: look.Dim);
                    break;
                default:
                    Medallion(m, power, at.X, at.Y, r, drained: look.Drained, dimRing: look.Dim);
                    break;
            }

            if (look.Veiled)
            {
                StoryVeil(m, at.X, at.Y, r);
            }

            // The shield's mark (past the story) and the padlock (not reached) at the ring's foot; both when both hold.
            var markSize = Math.Max(r * 0.36, small ? 7.0 : 5.0);
            if (look.Veiled)
            {
                ShieldMark(m, at.X + (r * 0.95), at.Y + (r * 0.9), markSize);
            }

            if (look.Padlock)
            {
                Padlock(m, at.X + (r * (look.Veiled ? -0.95 : 0.95)), at.Y + (r * 0.9), Math.Max(r * 0.36, 5.0));
            }

            if (look.Pip)
            {
                Pip(m, at.X + r, at.Y + (r * 0.95), MathF.Max(6f, r * 0.32f), i);
            }

            // The number on its own plate, clear of the road and the ring.
            var ny = at.Y + (r * 1.62) + (small ? 2 : 3);
            m.Dl.AddRectFilled(m.V.Map(at.X - 11, ny - 8.5), m.V.Map(at.X + 11, ny + 8.5), Ink(MoonfallColor.Hex("#070A1C"), 0.88f), m.V.Size(6));
            MenuText(m, MoonfallFace.Trump, small ? 15 : 18, at.X, ny + 0.5, stopNumbers[i], look.Dim ? Ink3 : GoldHiInk, Anchor.Centre, edge: 0.8f);

            if (hit)
            {
                if (mapStage == i && view.State is MoonfallStageState.Open or MoonfallStageState.Done)
                {
                    OpenLevels(i, -1);
                }
                else
                {
                    mapStage = i;
                }
            }
        }
    }

    private static readonly string[] stopIds = MakeIds("##mfStop", 12);

    private static string[] MakeIds(string prefix, int count)
    {
        var ids = new string[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = prefix + i.ToString(CultureInfo.InvariantCulture);
        }

        return ids;
    }

    private int hoveredStop = -1;

    /// <summary>Opens level select on stage <paramref name="stage"/> of the map's campaign, with <paramref name="level"/> selected (−1: its open level).</summary>
    private void OpenLevels(int stage, int level)
    {
        mapStage = stage;
        levelsSel = level;
        levelsPick = FirstAvailable();
        Open(MoonfallScreen.Levels);
    }

    /// <summary>The selected stage's panel (1280): its companion, name and carrier, its five levels, and Play.</summary>
    private void StagePanel(in MenuPen m, double x0, double y0, double x1, double y1)
    {
        var view = StagesOf(mapCampaign)[mapStage];
        var power = MoonfallCompanions.TryGet(view.Stage.Companion, out var info) ? info.Power : MoonfallPower.None;
        var accent = MoonfallCards.For(power)?.Accent ?? GoldInk;
        Panel(m, x0, y0, x1, y1, accent, 0.36);
        StageFace(m, view, x0 + 64, y0 + 76, 34, 0.45f);
        MenuText(m, MoonfallFace.Axis, 14, x0 + 122, y0 + 46, panelStage, GoldInk, edge: 0f);
        // A veiled stage's name is the shield's placeholder: the same slot and size, in the secondary tone, with its hover and reveal.
        // Set in Axis while veiled, so the placeholder's number reads as a figure ("area 1", not Jupiter's "I").
        var nameW = panelVeiledZone is null
            ? MenuText(m, MoonfallFace.Jupiter, 32, x0 + 122, y0 + 74, panelName, Cream, edge: 1f, maxWidth: 210)
            : MenuText(m, MoonfallFace.Axis, 22, x0 + 122, y0 + 74, panelName, Ink2, edge: 1f, maxWidth: 210);
        if (panelVeiledZone is { } zone)
        {
            ShieldPlaceholder(m, x0 + 122, y0 + 60, x0 + 122 + Math.Min(nameW, 210), y0 + 88, zone, panelName);
        }

        MenuText(m, MoonfallFace.Axis, 14.5f, x0 + 122, y0 + 100, panelCarrier, Tint(accent, 0.3f), edge: 0f, maxWidth: 210);
        CrestRule(m, x0 + 174, y0 + 140, 296, false, 0.36);
        if (panelVeiledZone is not null)
        {
            // Past the story: the five codes on one row, then why and how it opens, printed (not only on hover).
            for (var i = 0; i < view.Levels.Count; i++)
            {
                MenuText(m, MoonfallFace.Trump, 19, x0 + 40 + (i * 62), y0 + 168, panelCodes[i], Ink3, edge: 0.6f);
            }

            for (var i = 0; i < panelVeiledLines.Length && i < 5; i++)
            {
                MenuText(m, MoonfallFace.Axis, 14.5f, x0 + 26, y0 + 204 + (i * 21), panelVeiledLines[i], Ink2, edge: 0f);
            }
        }

        for (var i = 0; i < view.Levels.Count && panelVeiledZone is null; i++)
        {
            var slot = view.Levels[i];
            var yy = y0 + 168 + (i * 29);
            var reached = slot.Reached;
            if (MenuHit(m, rowIds[i], x0 + 16, yy - 13, x1 - 16, yy + 13, out var hovered, out var nav) && slot.State != MoonfallLevelState.Missing && view.State is MoonfallStageState.Open or MoonfallStageState.Done)
            {
                SoundClick();
                OpenLevels(mapStage, i);
            }

            if (hovered || nav)
            {
                m.Dl.AddRectFilled(m.V.Map(x0 + 16, yy - 13), m.V.Map(x1 - 16, yy + 13), Ink(MoonfallColor.Hex("#9DC0FF"), 0.10f), m.V.Size(6));
                if (nav)
                {
                    FocusOutline(m.Dl, m.V.Map(x0 + 16, yy - 13), m.V.Map(x1 - 16, yy + 13), m.V.Size(6));
                }
            }

            MenuText(m, MoonfallFace.Trump, 19, x0 + 26, yy, panelCodes[i], reached ? GoldHiInk : Ink3, edge: 0.6f);
            MenuText(m, MoonfallFace.Jupiter, 24, x0 + 66, yy, panelNames[i], reached ? Cream : Ink3, edge: 1f, maxWidth: slot.Aced && reached ? 168 : 196);
            if (slot.Aced && reached)
            {
                // An aced level says so, as level select and Quick Play do.
                MenuText(m, MoonfallFace.Trump, 15, x0 + 330, yy, Strings.MoonfallAced, GoldHiInk, Anchor.Right, edge: 0.6f);
                MenuText(m, MoonfallFace.Axis, 13.5f, x0 + 290, yy, panelScores[i], Ink2, Anchor.Right, edge: 0f);
            }
            else if (slot.State == MoonfallLevelState.Cleared)
            {
                Pip(m, x0 + 322, yy, 6.5f, i);
                MenuText(m, MoonfallFace.Axis, 13.5f, x0 + 308, yy, panelScores[i], Ink2, Anchor.Right, edge: 0f);
            }
            else if (panelScores[i].Length > 0)
            {
                MenuText(m, MoonfallFace.Axis, 13.5f, x0 + 322, yy, panelScores[i], slot.State == MoonfallLevelState.Open ? Tint(accent, 0.3f) : Ink3, Anchor.Right, edge: 0f);
            }
        }

        if (MenuButton(m, "##mfStagePlay", x0 + 74, y0 + 316, x0 + 274, y0 + 354, panelPlay, 28, isDefault: true, style: PanelPlayStyle, tooltip: PanelPlayTip))
        {
            PlayOrReveal(view);
        }
    }

    /// <summary>The stage Play's reason when it cannot play: the veil's (with its reveal) or the padlock's.</summary>
    private string? PanelPlayTip => panelPlayable || panelComing ? null : panelVeiledZone is not null ? Strings.MoonfallStageVeiledPillTip : Strings.MoonfallStageSealedTooltip;

    /// <summary>
    /// The stage's Play: on a stage past the player's story, pressing it (mouse, keyboard or gamepad) opens the shield's
    /// own "Reveal this name" menu for the stage's place, so the reveal never needs a right-click.
    /// </summary>
    private void PlayOrReveal(MoonfallStageView view)
    {
        if (panelVeiledZone is { } zone)
        {
            ShieldText.RequestMenu(SpoilerKind.Area, zone, panelName);
            return;
        }

        PlayStage(view);
    }

    private static readonly string[] rowIds = MakeIds("##mfRow", MoonfallCharacters.LevelsPerStage);

    /// <summary>The selected stage's panel at 640: its companion, number and name (opening level select), and Play.</summary>
    private void SmallStagePanel(in MenuPen m, double x0, double y0, double x1, double y1)
    {
        var view = StagesOf(mapCampaign)[mapStage];
        var power = MoonfallCompanions.TryGet(view.Stage.Companion, out var info) ? info.Power : MoonfallPower.None;
        var accent = MoonfallCards.For(power)?.Accent ?? GoldInk;
        Panel(m, x0, y0, x1, y1, accent, 0.3);
        if (MenuHit(m, "##mfStageHead", x0 + 10, y0 + 8, x1 - 10, y0 + 56, out var hovered, out var nav) && view.State is MoonfallStageState.Open or MoonfallStageState.Done)
        {
            SoundClick();
            OpenLevels(mapStage, -1);
        }

        // The head opens level select only on a stage that has one to open; elsewhere it offers nothing (a veiled
        // name's hover is the shield's own).
        var headOpens = view.State is MoonfallStageState.Open or MoonfallStageState.Done;
        if ((hovered || nav) && headOpens)
        {
            m.Dl.AddRectFilled(m.V.Map(x0 + 10, y0 + 8), m.V.Map(x1 - 10, y0 + 56), Ink(MoonfallColor.Hex("#9DC0FF"), 0.10f), m.V.Size(6));
            if (hovered)
            {
                UiMetrics.Tooltip(Strings.MoonfallSeeLevelsTooltip);
            }
        }

        if (nav)
        {
            FocusOutline(m.Dl, m.V.Map(x0 + 10, y0 + 8), m.V.Map(x1 - 10, y0 + 56), m.V.Size(6));
        }

        StageFace(m, view, x0 + 32, y0 + 38, 18, 0.4f);
        MenuText(m, MoonfallFace.Axis, 12.5f, x0 + 62, y0 + 22, panelStage, GoldInk, edge: 0f);
        var nameW = panelVeiledZone is null
            ? MenuText(m, MoonfallFace.Jupiter, 21, x0 + 62, y0 + 44, panelName, Cream, edge: 1f, maxWidth: (float)(x1 - x0 - 74))
            : MenuText(m, MoonfallFace.Axis, 15, x0 + 62, y0 + 44, panelName, Ink2, edge: 1f, maxWidth: (float)(x1 - x0 - 74));
        if (panelVeiledZone is { } zone)
        {
            ShieldPlaceholder(m, x0 + 62, y0 + 33, x0 + 62 + Math.Min(nameW, x1 - x0 - 74), y0 + 55, zone, panelName);
        }

        // Its state in words (no legend or stop tooltip at 640), then Levels and Play.
        MenuText(m, MoonfallFace.Axis, 12, x0 + 16, y0 + 68, panelState, Ink2, edge: 0f, maxWidth: (float)(x1 - x0 - 32));
        var sealedStage = view.State is MoonfallStageState.Sealed or MoonfallStageState.Veiled;
        if (!sealedStage && MenuButton(m, "##mfStageLevels", x0 + 12, y0 + 82, x0 + 92, y0 + 112, Strings.MoonfallLevelsButton, 15, primaryFace: false,
            tooltip: Strings.MoonfallSeeLevelsTooltip))
        {
            OpenLevels(mapStage, -1);
        }

        if (MenuButton(m, "##mfStagePlay", x0 + (sealedStage ? 12 : 100), y0 + 82, x1 - 12, y0 + 112, panelPlay, 20, isDefault: true, style: PanelPlayStyle, tooltip: PanelPlayTip))
        {
            PlayOrReveal(view);
        }
    }

    /// <summary>A stage's face in a ring: its companion (as the shield allows), or the free-choice star.</summary>
    private void StageFace(in MenuPen m, MoonfallStageView view, double x, double y, float r, float glow)
    {
        var look = MoonfallLooks.Stop(view, mapStage >= 0 && mapStage < stopComing.Length && stopComing[mapStage]);
        var power = MoonfallCompanions.TryGet(view.Stage.Companion, out var info) ? info.Power : MoonfallPower.None;
        switch (look.Face)
        {
            case MoonfallStopFace.PickStar:
                PickStar(m, x, y, r, false);
                GiltRing(m.C, x, y, r);
                break;
            case MoonfallStopFace.CardBack:
                Medallion(m, power, x, y, r, back: true);
                break;
            default:
                Medallion(m, power, x, y, r, glow: look.Drained || look.Veiled ? 0f : glow, drained: look.Drained);
                break;
        }

        if (look.Veiled)
        {
            StoryVeil(m, x, y, r);
        }
    }

    /// <summary>The stage's Play: its open level; a stage all won opens level select to choose one.</summary>
    private void PlayStage(MoonfallStageView view)
    {
        foreach (var slot in view.Levels)
        {
            if (slot.State == MoonfallLevelState.Open)
            {
                var pick = view.Stage.PlayerPicks ? FirstAvailable() : MoonfallCompanion.None;
                if (PlayAdventure(slot.Place.Campaign, slot.Place.Index, pick))
                {
                    return;
                }
            }
        }

        OpenLevels(mapStage, -1);
    }

    /// <summary>The map's legend: won, you are here, not reached, not yet met, your pick.</summary>
    private void Legend(in MenuPen m, double x0, double y0, double x1, double y1)
    {
        var dl = m.Dl;
        var v = m.V;
        dl.AddRectFilled(v.Map(x0, y0), v.Map(x1, y1), Ink(MoonfallColor.Hex("#070A1C"), 0.85f), v.Size(6));
        GiltBand(m.C, x0, y0, x1, y1, 0.22);
        Pip(m, x0 + 26, y0 + 24, 7f);
        MenuText(m, MoonfallFace.Axis, 14, x0 + 44, y0 + 24, Strings.MoonfallLegendWon, Ink2, edge: 0f);
        if (m.HasArt)
        {
            Put(m.A, m.A.Atlas[MoonfallSprite.Soft], x0 + 140, y0 + 24, 14f / 4f, Ink(MoonfallColor.Hex("#E69461"), 0.8f));
        }

        MenuText(m, MoonfallFace.Axis, 14, x0 + 158, y0 + 24, Strings.MoonfallLegendHere, Ink2, edge: 0f);
        Padlock(m, x0 + 26, y0 + 56, 7);
        MenuText(m, MoonfallFace.Axis, 14, x0 + 44, y0 + 56, Strings.MoonfallLegendNotReached, Ink2, edge: 0f);
        if (m.C.Sheet[MoonfallChromePart.CardBack] is not null)
        {
            var (uv0, uv1) = m.C.Sheet.Uv(MoonfallChromePart.CardBack, 44, 70, 158, 184);
            dl.AddImageRounded(m.C.Tex, v.Map(x0 + 131, y0 + 47), v.Map(x0 + 149, y0 + 65), uv0, uv1, uint.MaxValue, v.Size(9));
        }

        MenuText(m, MoonfallFace.Axis, 14, x0 + 158, y0 + 56, Strings.MoonfallLegendNotMet, Ink2, edge: 0f);
        PickStar(m, x0 + 260, y0 + 56, 9, false);
        MenuText(m, MoonfallFace.Axis, 14, x0 + 276, y0 + 56, Strings.MoonfallLegendPick, Ink2, edge: 0f);
        if (anyVeiled)
        {
            ShieldMark(m, x0 + 26, y0 + 88, 7);
            MenuText(m, MoonfallFace.Axis, 14, x0 + 44, y0 + 88, Strings.MoonfallLegendStory, Ink2, edge: 0f);
        }
    }

    /// <summary>A stop of the map's campaign is set past the player's story (the legend's shield row shows only then).</summary>
    private bool anyVeiled;

    /// <summary>The stop under the mouse or the focus explains itself (1280): its stage, name, state and power, and why it shows as it does.</summary>
    private void StopTooltip(in MenuPen m)
    {
        if (hoveredStop < 0 || hoveredStop >= stopTips.Length)
        {
            return;
        }

        var dl = m.Dl;
        var v = m.V;
        const double X = 14, Y = 716, W = 300;
        var lines = stopTipLines[hoveredStop].Length > 0 ? 2 : 1;
        var h = 12 + (20 * lines);
        dl.AddRectFilled(v.Map(X, Y), v.Map(X + W, Y + h), Ink(MoonfallColor.Hex("#070A1C"), 0.94f), v.Size(4));
        GiltBand(m.C, X, Y, X + W, Y + h, 0.22);
        MenuText(m, MoonfallFace.Axis, 14, X + 12, Y + 16, stopTips[hoveredStop], stopVeiled[hoveredStop] ? Ink2 : Cream, edge: 0f, maxWidth: (float)(W - 24));
        if (lines > 1)
        {
            MenuText(m, MoonfallFace.Axis, 14, X + 12, Y + 36, stopTipLines[hoveredStop], Ink2, edge: 0f, maxWidth: (float)(W - 24));
        }
    }

    /// <summary>The map's and level select's words, made when the screen, its selection or the progress changes.</summary>
    private void MakeMapWords(in MenuPen m)
    {
        var key = (menuViewsKey.GetHashCode(), mapCampaign, mapStage, levelsSel, levelsPick, m.Small);
        if (screenWordsFor >= 0 && key == screenWordsKey)
        {
            return;
        }

        screenWordsKey = key;
        screenWordsFor = 0;
        var c = CultureInfo.CurrentCulture;
        var stages = StagesOf(mapCampaign);
        var reachedStages = 0;
        stopNumbers = new string[stages.Count];
        stopTips = new string[stages.Count];
        stopTipLines = new string[stages.Count];
        stopComing = new bool[stages.Count];
        stopVeiled = new bool[stages.Count];
        anyVeiled = false;
        var cleared = modes.CampaignOpen(mapCampaign) ? progress.Cleared(mapCampaign) : -1;
        for (var i = 0; i < stages.Count; i++)
        {
            var view = stages[i];
            stopComing[i] = MoonfallLooks.Coming(view, cleared);
            reachedStages += view.State is MoonfallStageState.Open or MoonfallStageState.Done ? 1 : 0;
            stopNumbers[i] = view.Stage.Number.ToString(c);
            var state = view.State switch
            {
                MoonfallStageState.Done => Strings.MoonfallStageWon,
                MoonfallStageState.Open when view.Here => Strings.MoonfallStageHere,
                MoonfallStageState.Open => Strings.MoonfallStageOpen,
                MoonfallStageState.Veiled => Strings.MoonfallStageVeiledState,
                _ when stopComing[i] => Strings.MoonfallLevelsComing,
                _ => Strings.MoonfallStageNotReached,
            };
            var power = MoonfallCompanions.TryGet(view.Stage.Companion, out var info) ? Strings.MoonfallPowerName(info.Power) : Strings.MoonfallYourPick;
            // A veiled stop's tip leaves its name out (a tooltip cannot answer as a placeholder must).
            stopVeiled[i] = view.State == MoonfallStageState.Veiled;
            anyVeiled |= stopVeiled[i];
            stopTips[i] = stopVeiled[i]
                ? string.Format(c, Strings.MoonfallStopTipVeiledFormat, view.Stage.Number, power)
                : string.Format(c, Strings.MoonfallStopTipFormat, view.Stage.Number, StageNameShown(view.Stage), state, power);
            stopTipLines[i] = view.State == MoonfallStageState.Veiled ? (view.Reached ? Strings.MoonfallStageVeiledLine : Strings.MoonfallStageVeiledSealedLine)
                : view.Companion == MoonfallCompanionState.NotMet && !view.Stage.PlayerPicks ? Strings.MoonfallStopNotMetLine
                : view.State == MoonfallStageState.Sealed && !stopComing[i] ? Strings.MoonfallStopSealedLine
                : string.Empty;
        }

        mapCount = string.Format(c, Strings.MoonfallStagesCountFormat, reachedStages, stages.Count);
        roadPoints = new Vector2[stages.Count];
        var spots = mapCampaign == MoonfallCampaignKind.Expansion ? FarStops : BaseStops;
        roadWalked = 0;
        for (var i = 0; i < stages.Count && i < spots.Length; i++)
        {
            roadPoints[i] = StopAt(spots[i], m.Small);
            if (stages[i].State is MoonfallStageState.Open or MoonfallStageState.Done)
            {
                roadWalked = i;
            }
        }

        // The selected stage's panel.
        var sel = stages[Math.Clamp(mapStage, 0, stages.Count - 1)];
        panelStage = string.Format(c, Strings.MoonfallStageCapsFormat, sel.Stage.Number);
        panelName = StageNameShown(sel.Stage);
        panelVeiledZone = modes.VeiledZone(sel.Stage);
        if (sel.Stage.PlayerPicks)
        {
            panelCarrier = Strings.MoonfallYourPickLine;
        }
        else if (MoonfallCompanions.TryGet(sel.Stage.Companion, out var carrier))
        {
            panelCarrier = sel.Companion == MoonfallCompanionState.NotMet
                ? string.Format(c, Strings.MoonfallNotMetPowerFormat, Strings.MoonfallPowerName(carrier.Power))
                : string.Format(c, Strings.MoonfallCarrierFormat, ShortName(carrier.Companion), Strings.MoonfallPowerName(carrier.Power));
        }

        panelPlayable = false;
        var selComing = stopComing[Math.Clamp(mapStage, 0, stages.Count - 1)];
        panelComing = selComing;
        panelPlay = sel.State == MoonfallStageState.Veiled ? Strings.MoonfallStageVeiledButton : selComing ? Strings.MoonfallLevelsComing : sel.State == MoonfallStageState.Sealed ? Strings.MoonfallNotReached : Strings.MoonfallChooseLevel;

        // At 640 the panel's state line stands in for the legend and the stop's tooltip.
        var veiledLine = sel.Reached ? Strings.MoonfallStageVeiledLine : Strings.MoonfallStageVeiledSealedLine;
        panelVeiledLines = sel.State == MoonfallStageState.Veiled && !m.Small ? Wrap(m, MoonfallFace.Axis, 14.5f, veiledLine, 300f) : [];
        panelState = sel.State == MoonfallStageState.Veiled ? (m.Small ? Strings.MoonfallStageVeiledShort : veiledLine)
            : sel.Companion == MoonfallCompanionState.NotMet && !sel.Stage.PlayerPicks ? Strings.MoonfallStopNotMetLine
            : sel.State == MoonfallStageState.Sealed && !selComing ? Strings.MoonfallStopSealedLine
            : panelCarrier;
        for (var i = 0; i < sel.Levels.Count; i++)
        {
            var slot = sel.Levels[i];
            panelCodes[i] = LevelCode(slot.Place.Index);
            panelNames[i] = LevelNameShown(slot);
            panelScores[i] = slot.State switch
            {
                MoonfallLevelState.Cleared => slot.Best > 0 ? slot.Best.ToString("N0", c) : string.Empty,
                MoonfallLevelState.Open => Strings.MoonfallNext,
                MoonfallLevelState.Missing => string.Empty,
                _ => string.Empty,
            };
            if (slot.State == MoonfallLevelState.Open && !panelPlayable)
            {
                panelPlayable = true;
                panelPlay = string.Format(c, Strings.MoonfallPlayFormat, panelCodes[i]);
            }
        }

        if (sel.State == MoonfallStageState.Done)
        {
            panelPlayable = true;
        }

        MakeLevelsWords(m, sel);
    }

    // ---- Level select ----

    private void EnsureLevelsSelection(MoonfallStageView view)
    {
        if (levelsSel >= 0 && levelsSel < view.Levels.Count && view.Levels[levelsSel].Reached)
        {
            return;
        }

        levelsSel = 0;
        for (var i = 0; i < view.Levels.Count; i++)
        {
            if (view.Levels[i].State == MoonfallLevelState.Open)
            {
                levelsSel = i;
                return;
            }
        }
    }

    private void MakeLevelsWords(in MenuPen m, MoonfallStageView view)
    {
        if (flow.Current != MoonfallScreen.Levels)
        {
            return;
        }

        var c = CultureInfo.CurrentCulture;
        EnsureLevelsSelection(view);
        levelsHeader = string.Format(c, Strings.MoonfallLevelsHeaderFormat, Strings.MoonfallCampaignName(view.Stage.Campaign).ToUpper(c), view.Stage.Number);
        if (view.Stage.PlayerPicks)
        {
            levelsLine = Strings.MoonfallLevelsPickLine;
        }
        else if (MoonfallCompanions.TryGet(view.Stage.Companion, out var info))
        {
            levelsLine = view.Companion == MoonfallCompanionState.NotMet
                ? string.Format(c, Strings.MoonfallLevelsNotMetLineFormat, Strings.MoonfallPowerName(info.Power))
                : string.Format(c, Strings.MoonfallLevelsLineFormat, ShortName(info.Companion), Strings.MoonfallPowerName(info.Power));
        }

        var small = m.Small;
        for (var i = 0; i < view.Levels.Count; i++)
        {
            var slot = view.Levels[i];
            var name = LevelNameShown(slot);
            // One line: at 1280 shrunk to the tile; at 640 (where the floor stops shrinking) without its "The", cut short if
            // it still does not fit (the strip names the selected level in full).
            tileNames[i] = small
                ? [FitLine(MoonfallFace.Jupiter, NamePx(m.V, 15, MoonfallFace.Jupiter), name.StartsWith("The ", StringComparison.Ordinal) ? name[4..] : name, m.V.Size(108 - 30 - 14))]
                : [name];

            // A name cut short shows whole on the tile's hover and focus (a sealed tile cannot be selected into the strip).
            tileFull[i] = small && !string.Equals(tileNames[i][0], name, StringComparison.Ordinal) ? name : null;
            tileLines[i] = slot.State switch
            {
                MoonfallLevelState.Cleared => string.Format(c, Strings.MoonfallTileBestFormat, slot.Best.ToString("N0", c)),
                MoonfallLevelState.Open when slot.Ace is { } ace => string.Format(c, Strings.MoonfallAceFormat, ace.ToString("N0", c)),
                MoonfallLevelState.Open => string.Empty,
                MoonfallLevelState.Missing => Strings.MoonfallLevelComingLine,
                MoonfallLevelState.Veiled => string.Empty,
                _ => i > 0 ? string.Format(c, Strings.MoonfallOpensAfterFormat, LevelCode(slot.Place.Index - 1)) : Strings.MoonfallNotReached,
            };
        }

        var sel = view.Levels[Math.Clamp(levelsSel, 0, view.Levels.Count - 1)];
        stripName = LevelNameShown(sel);
        var carrierPower = view.Stage.PlayerPicks
            ? (MoonfallCompanions.TryGet(levelsPick, out var picked) ? picked.Power : MoonfallPower.None)
            : MoonfallCompanions.TryGet(view.Stage.Companion, out var carrier2) ? carrier2.Power : MoonfallPower.None;
        var does = MoonfallCompanions.TryGet(MoonfallCompanions.Carrying(carrierPower), out var who) && (view.Stage.PlayerPicks || view.Companion != MoonfallCompanionState.NotMet)
            ? MoonfallLooks.LoreOf(who.Companion).Does
            : string.Empty;
        // The level's own line (its place on the road), then what the power does on it.
        var place = string.Format(c, Strings.MoonfallStripLevelFormat, LevelCode(sel.Place.Index), StageNameShown(view.Stage));
        stripDescription = Wrap(m, MoonfallFace.Axis, small ? 12.5f : 15f, place + (does.Length > 0 ? " " + does : string.Empty) + " " + Strings.MoonfallClearTheOranges, small ? 410f : 860f);
        if (stripDescription.Length > 2)
        {
            stripDescription = stripDescription[..2];
        }

        stripAce = sel.Ace is { } a ? a.ToString("N0", c) : Strings.MoonfallNoAce;
        stripAceSmall = Strings.MoonfallAceCaps + " " + stripAce;
        stripWith = carrierPower == MoonfallPower.None
            ? Strings.MoonfallNoCompanion
            : string.Format(c, Strings.MoonfallWithFormat, ShortName(MoonfallCompanions.Carrying(carrierPower)));
        var notMet = !view.Stage.PlayerPicks && view.Companion == MoonfallCompanionState.NotMet;
        stripWithSmall = notMet ? Strings.MoonfallNotYetMet : stripWith + (carrierPower != MoonfallPower.None ? " · " + Strings.MoonfallPowerName(carrierPower) : string.Empty);
        stripPlay = sel.Reached ? string.Format(c, Strings.MoonfallPlayFormat, LevelCode(sel.Place.Index)) : Strings.MoonfallNotReached;
    }

    /// <summary><paramref name="text"/> as it fits <paramref name="room"/> pixels at <paramref name="px"/>: whole, or cut short with an ellipsis.</summary>
    /// <summary>Whether <paramref name="text"/> ends on an article or a small linking word, which a cut never ends on ("Above the…").</summary>
    private static bool EndsOnSmallWord(ReadOnlySpan<char> text)
    {
        var at = text.LastIndexOf(' ');
        var word = at < 0 ? text : text[(at + 1)..];
        foreach (var small in (ReadOnlySpan<string>)["the", "a", "an", "of", "to", "in", "on", "and", "by"])
        {
            if (word.Equals(small, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string FitLine(MoonfallFace face, float px, string text, float room)
    {
        if (MeasureText(face, px, text) <= room)
        {
            return text;
        }

        // At a word's end first ("Above the..."), and only within a word when no whole word fits.
        for (var n = text.Length - 1; n > 1; n--)
        {
            if (text[n] == ' ' && text[n - 1] != ' ' && !EndsOnSmallWord(text.AsSpan(0, n)))
            {
                var words = string.Concat(text.AsSpan(0, n), "...");
                if (MeasureText(face, px, words) <= room)
                {
                    return words;
                }
            }
        }

        for (var n = text.Length - 1; n > 1; n--)
        {
            var cut = string.Concat(text.AsSpan(0, n).TrimEnd(), "...");
            if (MeasureText(face, px, cut) <= room)
            {
                return cut;
            }
        }

        return text[..1] + "...";
    }

    private void DrawLevels(in MenuPen m)
    {
        EnsureMapSelection();
        var stages = StagesOf(mapCampaign);
        var view = stages[mapStage];
        MakeMapWords(m);
        var small = m.Small;
        var (_, k, _, _) = ChartLayout(small);
        // The chart under the whole screen, darkened by half (screens2.levels).
        var ch = 1060f * m.H / m.W;
        Chart(m, 0, m.W / 1060f, MathF.Min(400f, 872f - ch), 0.5f);
        _ = k;
        var power = MoonfallCompanions.TryGet(view.Stage.Companion, out var info) ? info.Power : MoonfallPower.None;
        var carrierPower = view.Stage.PlayerPicks ? (MoonfallCompanions.TryGet(levelsPick, out var p) ? p.Power : MoonfallPower.None) : power;
        var accent = MoonfallCards.For(carrierPower)?.Accent ?? GoldInk;
        if (small)
        {
            if (MenuButton(m, "##mfMap", 8, 10, 72, 38, Strings.MoonfallMap, 13.7f, primaryFace: false))
            {
                Back();
            }

            StageFace(m, view, 104, 30, 18, 0.4f);
            MenuText(m, MoonfallFace.Axis, 12, 132, 18, panelStage, GoldInk, edge: 1f);
            MenuTitle(m, 130, 40, StageNameShown(view.Stage), 30, maxWidth: 500);
        }
        else
        {
            if (MenuButton(m, "##mfMap", 22, 20, 122, 52, Strings.MoonfallMap, 17.3f, primaryFace: false))
            {
                Back();
            }

            StageFace(m, view, 200, 78, 40, 0.5f);
            MenuText(m, MoonfallFace.Axis, 14, 270, 52, levelsHeader, GoldInk, edge: 1f);
            MenuTitle(m, 268, 88, StageNameShown(view.Stage), 52, maxWidth: 940);
            MenuText(m, MoonfallFace.Axis, 15, 270, 122, levelsLine, Ink2, edge: 1f, maxWidth: 960);
        }

        var (w, gx, x0, ty) = small ? (108.0, 14.0, 12.0, 82.0) : (214.0, 28.0, 46.0, 186.0);
        for (var i = 0; i < view.Levels.Count; i++)
        {
            LevelTile(m, view, i, x0 + (i * (w + gx)), ty, w, accent);
        }

        PlayStrip(m, view, accent);

        // The selected level is built ahead while level select shows, so Play opens onto its scene.
        if (view.Levels[Math.Clamp(levelsSel, 0, view.Levels.Count - 1)] is { Reached: true, Level: { } selected })
        {
            gameArt?.Warm(selected, BoardTwoX(m));
        }
    }

    private static readonly string[] tileIds = MakeIds("##mfTile", MoonfallCharacters.LevelsPerStage);

    /// <summary>screens2.level_tile: a level's thumbnail in its gilt frame with its code and name, its best or ACED, or the sealed back.</summary>
    private void LevelTile(in MenuPen m, MoonfallStageView view, int i, double x, double y, double w, Vector3 accent)
    {
        var small = m.Small;
        var slot = view.Levels[i];
        var th = w * 1106 / 1300;
        var capH = small ? 48.0 : 60.0;
        var dl = m.Dl;
        var v = m.V;
        var hit = MenuHit(m, tileIds[i], x - 8, y - 8, x + w + 8, y + th + capH, out var hovered, out var nav);
        var selected = i == levelsSel;
        if ((selected || nav) && slot.Reached && m.C.Sheet[MoonfallChromePart.CardSelect] is not null)
        {
            var breath = MoonfallMotion.Breath(menuClock, 3f, 0.15f, motion != MoonfallMotionLevel.Full);
            Part(m.C, MoonfallChromePart.CardSelect, x - 14, y - 14, x + w + 14, y + th + capH + 6, Ink(Vector3.Lerp(accent, Vector3.One, 0.45f), 0.85f * breath));
        }

        Panel(m, x - 8, y - 8, x + w + 8, y + th + capH, selected && slot.Reached ? accent : null, 0.3, corners: false, shadow: true);
        if (slot.Level is { } level && slot.Reached)
        {
            LevelThumb(m, level, x, y, w);
        }
        else
        {
            SealedThumb(m, x, y, w, th);
            if (slot.State == MoonfallLevelState.Veiled)
            {
                ShieldMark(m, x + (w / 2), y + (th / 2) - (small ? 6 : 8), small ? 7 : 9);
            }
            else
            {
                Padlock(m, x + (w / 2), y + (th / 2) - (small ? 6 : 8), small ? 7 : 9);
            }
        }

        if (hovered && !selected)
        {
            dl.AddRect(v.Map(x - 8, y - 8), v.Map(x + w + 8, y + th + capH), Ink(MoonfallColor.Hex("#9DC0FF"), 0.6f), v.Size(4), ImDrawFlags.None, MathF.Max(1f, v.Size(1.4)));
        }

        if ((hovered || nav) && tileFull[i] is { } full)
        {
            UiMetrics.Tooltip(full);
        }

        if (nav)
        {
            FocusOutline(dl, v.Map(x - 8, y - 8), v.Map(x + w + 8, y + th + capH), v.Size(4));
        }

        // The captions sit 6 units in from the frame on either side.
        const double Pad = 6;
        var ty = y + th + (small ? 14 : 19);
        var reached = slot.Reached;
        MenuText(m, MoonfallFace.Trump, small ? 14 : 19, x + Pad, ty, panelCodes[i], reached ? GoldHiInk : Ink3, edge: 0.6f);
        var cx0 = x + Pad + (small ? 24 : 36);
        var avail = (float)(w - (small ? 24 : 36) - (2 * Pad) - 2);
        var names = tileNames[i];
        if (names.Length == 1)
        {
            MenuText(m, MoonfallFace.Jupiter, small ? 15 : 25, cx0, ty, names[0], reached ? Cream : Ink2, edge: 1f, maxWidth: avail);
        }
        else
        {
            for (var n = 0; n < names.Length; n++)
            {
                MenuText(m, MoonfallFace.Jupiter, 14, cx0, ty - 7 + (12 * n), names[n], reached ? Cream : Ink2, edge: 1f, maxWidth: avail);
            }
        }

        var yy = y + th + (small ? 32 : 42);
        if (slot.State == MoonfallLevelState.Cleared)
        {
            MenuText(m, MoonfallFace.Axis, small ? 14 : 13.5f, x + Pad, yy, tileLines[i], Ink2, edge: 0f, maxWidth: (float)(w - 40 - Pad));
            if (slot.Aced && reached)
            {
                if (small)
                {
                    dl.AddRectFilled(v.Map(x + w - 44, y + 3), v.Map(x + w - 3, y + 21), Ink(MoonfallColor.Hex("#2A1206"), 0.92f), v.Size(4));
                    MenuText(m, MoonfallFace.Trump, 15, x + w - 23.5, y + 12.5, Strings.MoonfallAced, GoldHiInk, Anchor.Centre, edge: 0.6f);
                }
                else
                {
                    MenuText(m, MoonfallFace.Trump, 17, x + w - Pad, yy, Strings.MoonfallAced, GoldHiInk, Anchor.Right, edge: 0.6f);
                }
            }
            else
            {
                Pip(m, x + w - 8 - Pad, yy, small ? 5.5f : 6.5f, i);
            }
        }
        else if (tileLines[i].Length > 0)
        {
            var open = slot.State == MoonfallLevelState.Open;
            if (open)
            {
                MenuText(m, MoonfallFace.Axis, small ? 14 : 13.5f, x + Pad, yy, tileLines[i], Tint(accent, 0.4f), edge: 0f, maxWidth: (float)(w - (2 * Pad)));
            }
            else
            {
                // The sealed tile's line sits on its back, under the padlock.
                MenuText(m, MoonfallFace.Axis, small ? 12 : 14, x + (w / 2), y + (th / 2) + (small ? 12 : 18), tileLines[i], Ink2, Anchor.Centre, edge: 1.2f, maxWidth: (float)(w - 8));
            }
        }

        if (hit && reached)
        {
            if (selected)
            {
                PlaySelectedLevel(view);
            }
            else
            {
                SoundClick();
                levelsSel = i;
            }
        }
    }

    /// <summary>The Play strip: the selected level framed, its name, a line on its power, the Ace score, its companion, and Play.</summary>
    private void PlayStrip(in MenuPen m, MoonfallStageView view, Vector3 accent)
    {
        var small = m.Small;
        var sel = view.Levels[Math.Clamp(levelsSel, 0, view.Levels.Count - 1)];
        var carrierPower = view.Stage.PlayerPicks
            ? (MoonfallCompanions.TryGet(levelsPick, out var p) ? p.Power : MoonfallPower.None)
            : MoonfallCompanions.TryGet(view.Stage.Companion, out var info) ? info.Power : MoonfallPower.None;
        var notMet = !view.Stage.PlayerPicks && view.Companion == MoonfallCompanionState.NotMet;
        if (small)
        {
            const double Sy = 300;
            Panel(m, 10, Sy, 630, 470, accent, 0.3);
            if (sel.Level is { } level && sel.Reached)
            {
                LevelThumb(m, level, 26, Sy + 18, 150);
            }

            MenuText(m, MoonfallFace.Trump, 17, 196, Sy + 22, panelCodes[Math.Clamp(levelsSel, 0, 4)], GoldHiInk, edge: 0.6f);
            MenuText(m, MoonfallFace.Jupiter, 24, 228, Sy + 22, stripName, Cream, edge: 1f, maxWidth: 390);
            for (var i = 0; i < stripDescription.Length; i++)
            {
                MenuText(m, MoonfallFace.Axis, 12.5f, 196, Sy + 48 + (17 * i), stripDescription[i], Ink2, edge: 0f);
            }

            MenuText(m, MoonfallFace.Trump, 15, 196, Sy + 88, stripAceSmall, GoldHiInk, edge: 0.6f);
            StripCompanion(m, view, carrierPower, 210, Sy + 128, 13, 230, Sy + 128, true, notMet);
            if (MenuButton(m, "##mfLevelPlay", 470, Sy + 110, 616, Sy + 148, stripPlay, 26, isDefault: true, style: sel.Reached ? MenuStyle.Normal : MenuStyle.Locked))
            {
                PlaySelectedLevel(view);
            }

            return;
        }

        Panel(m, 46, 520, 1234, 770, accent, 0.4);
        if (sel.Level is { } big && sel.Reached)
        {
            LevelThumb(m, big, 76, 552, 230);
        }
        else
        {
            SealedThumb(m, 76, 552, 230, 230 * 1106 / 1300.0);
        }

        MenuTitle(m, 336, 574, stripName, 46, maxWidth: 860);
        for (var i = 0; i < stripDescription.Length; i++)
        {
            MenuText(m, MoonfallFace.Axis, 15, 336, 612 + (24 * i), stripDescription[i], Ink2, edge: 0f);
        }

        MenuText(m, MoonfallFace.Axis, 12.5f, 336, 672, Strings.MoonfallAceScoreCaps, GoldInk, edge: 0f);
        MenuText(m, MoonfallFace.Trump, 30, 336, 698, stripAce, GoldHiInk, edge: 0.8f);
        StripCompanion(m, view, carrierPower, 530, 690, 18, 562, 682, false, notMet);
        if (MenuButton(m, "##mfLevelPlay", 980, 676, 1200, 728, stripPlay, 36, isDefault: true, style: sel.Reached ? MenuStyle.Normal : MenuStyle.Locked))
        {
            PlaySelectedLevel(view);
        }
    }

    /// <summary>The strip's companion: "with Cid" and the power; on a "Your Pick" stage, the companion picked, with arrows to change it.</summary>
    private void StripCompanion(in MenuPen m, MoonfallStageView view, MoonfallPower power, double mx, double my, float r, double tx, double ty, bool small, bool notMet)
    {
        var accent = MoonfallCards.For(power)?.Accent ?? GoldInk;
        if (power != MoonfallPower.None)
        {
            Medallion(m, power, mx, my, r, glow: 0.35f, back: notMet);
        }

        if (small)
        {
            MenuText(m, MoonfallFace.Axis, 12.5f, tx, ty, stripWithSmall, Cream, edge: 0f, maxWidth: 220);
        }
        else
        {
            MenuText(m, MoonfallFace.Axis, 15, tx, ty, notMet ? Strings.MoonfallNotYetMet : stripWith, Cream, edge: 0f);
            if (power != MoonfallPower.None)
            {
                MenuText(m, MoonfallFace.Jupiter, 23, tx, ty + 22, Strings.MoonfallPowerName(power), Tint(accent, 0.25f), edge: 1f);
            }
        }

        if (!view.Stage.PlayerPicks)
        {
            return;
        }

        // Your Pick: arrows through the companions who can be played with.
        var step = MenuStepper(m, "##mfPick", small ? 450 : 900, small ? my : ty + 10, string.Empty, small, true, true);
        if (step != 0)
        {
            levelsPick = NextAvailable(levelsPick, step);
        }
    }

    /// <summary>The next companion (by <paramref name="step"/>) who can be played with, round the cast.</summary>
    private MoonfallCompanion NextAvailable(MoonfallCompanion from, int step)
    {
        var all = MoonfallCompanions.All;
        var index = Math.Max(0, (int)from - 1);
        for (var n = 0; n < all.Count; n++)
        {
            index = (index + step + all.Count) % all.Count;
            if (StateOf(all[index].Companion) == MoonfallCompanionState.Available)
            {
                return all[index].Companion;
            }
        }

        return from;
    }

    private void PlaySelectedLevel(MoonfallStageView view)
    {
        var sel = view.Levels[Math.Clamp(levelsSel, 0, view.Levels.Count - 1)];
        if (!sel.Reached)
        {
            return;
        }

        PlayAdventure(sel.Place.Campaign, sel.Place.Index, view.Stage.PlayerPicks ? levelsPick : MoonfallCompanion.None);
    }
}
