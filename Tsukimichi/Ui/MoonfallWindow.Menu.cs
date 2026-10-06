using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Ui;

/// <summary>
/// Moonfall's menus (spec-rich2.md §4; the approved <c>docs/design/v9/rich2/screens/</c>): the kit every screen is drawn
/// with, from the game's own UI art (the journal's gilt frame over an enamel ground, the Gold Saucer's pills in their
/// normal, focus, danger and locked fills, Lord of Verminion's rings, Triple Triad's cards, back and selection glow) and
/// the game's fonts. A screen is laid out in the design's own units, at 1280 × 800 or, in a small window, at the 640 ×
/// 480 design, scaled to the window and centred; its backdrop fills the window. Text is never set below the floors.
/// <para>
/// Every entry is a real ImGui item, so ImGui's navigation moves the focus with the keyboard (and the gamepad, through
/// Dalamud's gamepad navigation): the focused entry glows warm with a cream outline, the screen's default entry glows
/// warm until the keys are used, and a new screen starts with its default entry focused for a keyboard player. Drawing
/// allocates nothing a frame once a screen's words are made (each screen makes them when it opens or its selection or
/// the progress changes).
/// </para>
/// </summary>
public sealed partial class MoonfallWindow
{
    private const float MenuGap = 10f;

    private static readonly Vector3 GoldInk = MoonfallColor.Hex("#E8B54A");
    private static readonly Vector3 Ink3 = MoonfallColor.Hex("#AEB6D6");
    private static readonly Vector3 DangerInk = MoonfallColor.Hex("#F0C8D8");

    /// <summary>A sealed entry's label: slate, but at least 4.5:1 on its pill.</summary>
    private static readonly Vector3 LockedInk = MoonfallColor.Hex("#A9B0CC");

    /// <summary>A tab not chosen: at least 4.5:1 on the tab.</summary>
    private static readonly Vector3 IdleTabInk = MoonfallColor.Hex("#BCC4E4");

    /// <summary>A sheet with no parts, so the kit draws its plain shapes before (or without) the game's UI art.</summary>
    private static readonly Lazy<MoonfallChromeSheet> NoChrome = new(() => MoonfallChromeArt.Build(new Dictionary<string, MoonfallImage>(StringComparer.Ordinal)));

    /// <summary>A new screen's default entry takes the keyboard focus (the player was using the keys).</summary>
    private bool focusAsked;

    /// <summary>The pause menu's two held buttons.</summary>
    private (MoonfallHold Restart, MoonfallHold Leave) pauseHolds;

    /// <summary>What a menu draws with this frame: the design's view, the chrome, the art set, its size and the window's area.</summary>
    private readonly record struct MenuPen(ImDrawListPtr Dl, View V, ChromePen C, ArtPen A, bool HasArt, bool Small, Vector2 AreaMin, Vector2 AreaMax)
    {
        /// <summary>The design's width (1280 or 640).</summary>
        public float W => Small ? 640f : 1280f;

        /// <summary>The design's height (800 or 480).</summary>
        public float H => Small ? 480f : 800f;

        /// <summary>The window area's left edge in the design's units (left of 0 when the window is wider than the design).</summary>
        public float Left => (AreaMin.X - V.Origin.X) / V.Scale;

        /// <summary>The window area's right edge in the design's units.</summary>
        public float Right => (AreaMax.X - V.Origin.X) / V.Scale;

        /// <summary>The window area's top edge in the design's units.</summary>
        public float Top => (AreaMin.Y - V.Origin.Y) / V.Scale;
    }

    private enum MenuStyle : byte
    {
        Normal,
        Danger,
        Locked,

        /// <summary>Not sealed, but nothing to open yet (a stage whose levels are on their way): slate, with no padlock.</summary>
        Waiting,
    }

    /// <summary>Whether the menus' art has settled (the offline renderer waits for it): the backdrop the screen asks for, the cards it shows.</summary>
    private bool MenuArtSettled => gameArt is null || flow.Current == MoonfallScreen.Play || (menuArtPending == 0 && gameArt.ThumbsPending == 0);

    /// <summary>How many pieces of art the last menu frame drew without (still loading).</summary>
    private int menuArtPending;

    /// <summary>Once per new screen: the focus for a keyboard player, the holds, and the screen's views made again.</summary>
    private void EnteredScreen()
    {
        focusAsked = ImGui.GetIO().NavVisible;
        pauseHolds = default;
        menuViewsFor = -1;
    }

    /// <summary>The window's area for a menu, the design it is laid out at (1280 × 800, or 640 × 480 in a small window) and its pens.</summary>
    private MenuPen BeginMenu()
    {
        var start = ImGui.GetCursorScreenPos();
        var avail = Vector2.Max(ImGui.GetContentRegionAvail(), Vector2.One);
        var small = avail.X < 980f || avail.Y < 610f;
        var design = small ? new Vector2(640f, 480f) : new Vector2(1280f, 800f);
        var scale = MathF.Min(avail.X / design.X, avail.Y / design.Y);
        var origin = start + ((avail - (design * scale)) * 0.5f);
        origin = new Vector2(MathF.Round(origin.X), MathF.Round(origin.Y));
        var view = new View(origin, scale, 1f, BoardCentre);
        var dl = ImGui.GetWindowDrawList();
        menuArtPending = 0;

        // The art the menus share with the board: the chrome sheet and the atlas (the pips, the pegs of the thumbnails).
        art?.Frame(false, null);
        gameArt?.Menu();
        artDrawnFrame = ImGui.GetFrameCount();
        var sheet = gameArt?.Chrome is { } s && gameArt.ChromeTexture is { } t ? (s, t.Handle) : (NoChrome.Value, default(ImTextureID));
        if (gameArt is not null && gameArt.ChromeTexture is null)
        {
            menuArtPending++;
        }

        var chromePen = new ChromePen(dl, view, sheet.Item1, sheet.Item2);
        var hasArt = art?.Atlas is not null && art.Sheet(false, out _) is not null;
        var artPen = hasArt ? new ArtPen(dl, view, art!.Atlas!, art.Sheet(false, out _)!.Handle) : default;
        return new MenuPen(dl, view, chromePen, artPen, hasArt, small, start, start + avail);
    }

    /// <summary>The menu's screen this frame.</summary>
    private void DrawMenu(MoonfallScreen screen)
    {
        RefreshMenuViews();
        var m = BeginMenu();
        switch (screen)
        {
            case MoonfallScreen.Title:
                DrawTitle(m);
                break;
            case MoonfallScreen.Map:
                DrawMap(m);
                break;
            case MoonfallScreen.Levels:
                DrawLevels(m);
                break;
            case MoonfallScreen.Characters:
                DrawCharacters(m);
                break;
            case MoonfallScreen.QuickPlay:
                DrawQuickPlay(m);
                break;
            case MoonfallScreen.Challenges:
                DrawChallenges(m);
                break;
            case MoonfallScreen.Duel:
                DrawDuelSetup(m);
                break;
            case MoonfallScreen.Options:
                DrawOptions(m);
                break;
        }

        ImGui.SetCursorScreenPos(m.AreaMin);
        ImGui.Dummy(m.AreaMax - m.AreaMin);
    }

    // ---- Backdrops ----

    /// <summary>A backdrop over the whole window, cropped to fill it (cover): the source rectangle <paramref name="uv0"/>..<paramref name="uv1"/> of the texture.</summary>
    private static void Cover(ImDrawListPtr dl, ImTextureID tex, Vector2 min, Vector2 max, Vector2 uv0, Vector2 uv1, float aspect, uint tint = uint.MaxValue)
    {
        var size = max - min;
        var want = size.X / MathF.Max(size.Y, 1f);
        var du = uv1.X - uv0.X;
        var dv = uv1.Y - uv0.Y;
        if (want > aspect)
        {
            // Wider than the source: crop its height round the middle.
            var keep = aspect / want;
            var mid = uv0.Y + (dv * 0.5f);
            uv0.Y = mid - (dv * keep * 0.5f);
            uv1.Y = mid + (dv * keep * 0.5f);
        }
        else
        {
            var keep = want / aspect;
            var mid = uv0.X + (du * 0.5f);
            uv0.X = mid - (du * keep * 0.5f);
            uv1.X = mid + (du * keep * 0.5f);
        }

        dl.AddImage(tex, min, max, uv0, uv1, tint);
    }

    /// <summary>
    /// screens2.jewel_night: the characters' night (and the menus' fallback): the amethyst and sapphire sky falling to the
    /// deep, two soft jewels, the moon's glow from the upper left, and stars; under Full Decoration the stars twinkle.
    /// </summary>
    private void JewelNight(in MenuPen m)
    {
        var dl = m.Dl;
        var sky = Ink(MoonfallColor.Hex("#1C2E8E"));
        var deep = Ink(MoonfallColor.Hex("#070A22"));
        var mid = Ink(Vector3.Lerp(MoonfallColor.Hex("#1C2E8E"), MoonfallColor.Hex("#070A22"), 0.55f));
        dl.AddRectFilledMultiColor(m.AreaMin, m.AreaMax, sky, mid, deep, mid);
        if (!m.HasArt)
        {
            return;
        }

        ref readonly var soft = ref m.A.Atlas[MoonfallSprite.Soft];
        var area = new ArtPen(m.Dl, new View(m.AreaMin, 1f, 1f, BoardCentre), m.A.Atlas, m.A.Sheet);
        var size = m.AreaMax - m.AreaMin;
        Put(area, soft, size.X * 0.30, size.Y * 0.40, size.X * 0.14f, Ink(MoonfallColor.Hex("#7A4FC8"), 0.16f));
        Put(area, soft, size.X * 0.78, size.Y * 0.62, size.X * 0.12f, Ink(MoonfallColor.Hex("#6A4FC8"), 0.14f));
        Put(area, soft, size.X * 0.08, size.Y * 0.06, size.X * 0.20f, Ink(MoonfallColor.Hex("#C9D6FF"), 0.10f));
        Stars(area, size, 90, 0.55f);
    }

    /// <summary>The night's stars, at fixed places (a hash, not a random draw a frame), twinkling under Full Decoration.</summary>
    private void Stars(in ArtPen area, Vector2 size, int count, float bright)
    {
        ref readonly var speck = ref area.Atlas[MoonfallSprite.Speck];
        var full = motion == MoonfallMotionLevel.Full;
        for (var i = 0; i < count; i++)
        {
            var h = (uint)(i * 2654435761u);
            var x = (h % 10007) / 10007f * size.X;
            var y = ((h / 10007) % 9973) / 9973f * size.Y * 0.85f;
            var r = 0.6f + ((h >> 7) % 100 / 100f * 1.2f);
            var twinkle = full ? 0.65f + (0.35f * MathF.Sin((float)(menuClock * (2 * MathF.PI / (2f + (i % 2)))) + i)) : 0.8f;
            Put(area, speck, x, y, r, Ink(MoonfallColor.Hex("#E8EEFF"), bright * twinkle * (0.4f + ((h >> 13) % 60 / 100f))));
        }
    }

    /// <summary>Moondust drifting up and to the right (5–9 px/s), under Full Decoration only, masked off the given rectangles by the caller drawing over it.</summary>
    private void Moondust(in MenuPen m, int count)
    {
        if (!m.HasArt || motion != MoonfallMotionLevel.Full)
        {
            return;
        }

        var area = new ArtPen(m.Dl, new View(m.AreaMin, 1f, 1f, BoardCentre), m.A.Atlas, m.A.Sheet);
        var size = m.AreaMax - m.AreaMin;
        ref readonly var speck = ref area.Atlas[MoonfallSprite.Speck];
        for (var i = 0; i < count; i++)
        {
            var h = (uint)((i + 17) * 2246822519u);
            var speed = 5f + (h % 5);
            var x = ((h % 9001) / 9001f * size.X) + (float)(menuClock * speed * 0.6);
            var y = ((h / 9001) % 7919 / 7919f * size.Y) - (float)(menuClock * speed);
            x %= size.X;
            y %= size.Y;
            if (y < 0)
            {
                y += size.Y;
            }

            Put(area, speck, x, y, 1.2f, Ink(MoonfallColor.Hex("#E4ECFF"), 0.35f));
        }
    }

    // ---- Panels, rules, titles ----

    /// <summary>screens2.panel: the journal's gilt frame over an enamel ground, with a soft shadow below and to the right; a jewel glows from its lower right.</summary>
    private void Panel(in MenuPen m, double x0, double y0, double x1, double y1, Vector3? jewel = null, double scale = 0.5, bool corners = true, float alpha = 0.96f, bool shadow = true)
    {
        var dl = m.Dl;
        var v = m.V;
        if (shadow)
        {
            var dark = Ink(Vector3.Zero, 0.45f);
            dl.AddRectFilled(v.Map(x0 + 4, y0 + 7), v.Map(x1 + 6, y1 + 9), dark, v.Size(8));
        }

        var min = v.Map(x0, y0);
        var max = v.Map(x1, y1);
        if (gameArt?.MenuEnamel is { } enamel)
        {
            // The grain tile repeated over the panel at its own size (one unit a pixel of the tile at 1x).
            var tw = (float)(enamel.Width * v.Scale);
            var th = (float)(enamel.Height * v.Scale);
            dl.AddImageRounded(enamel.Handle, min, max, Vector2.Zero, new Vector2((max.X - min.X) / tw, (max.Y - min.Y) / th), Ink(Vector3.One, alpha), v.Size(4));
        }
        else
        {
            dl.AddRectFilled(min, max, Ink(MoonfallColor.Hex("#16245A"), alpha), v.Size(4));
        }

        // The light from above (1.12 at the top to 0.82 at the foot), the jewel's glow, the sheen toward the light.
        dl.AddRectFilledMultiColor(min, max, Ink(Vector3.Zero, 0f), Ink(Vector3.Zero, 0f), Ink(Vector3.Zero, 0.27f * alpha), Ink(Vector3.Zero, 0.27f * alpha));
        if (jewel is { } j)
        {
            dl.AddRectFilledMultiColor(min, max, Ink(j, 0f), Ink(j, 0f), Ink(j, 0.14f * alpha), Ink(j, 0f));
        }

        dl.AddRectFilledMultiColor(min, max, Ink(MoonfallColor.Hex("#9FB2E8"), 0.10f * alpha), Ink(MoonfallColor.Hex("#9FB2E8"), 0f), Ink(MoonfallColor.Hex("#9FB2E8"), 0f), Ink(MoonfallColor.Hex("#9FB2E8"), 0f));
        if (corners)
        {
            GiltFrame(m.C, x0, y0, x1, y1, scale);
        }
        else
        {
            GiltBand(m.C, x0 + (6 * scale), y0 + (6 * scale), x1 - (6 * scale), y1 - (6 * scale), scale);
        }

        if (m.C.Sheet[MoonfallChromePart.RuleTop] is null)
        {
            dl.AddRect(min, max, Ink(GoldInk, 0.8f), v.Size(4), ImDrawFlags.None, MathF.Max(1f, v.Size(1.2)));
        }
    }

    /// <summary>r2kit.crest_rule: the journal's gilt rule across <paramref name="w"/> units at (cx, y), with its scrolled crest in the middle when asked.</summary>
    private static void CrestRule(in MenuPen m, double cx, double y, double w, bool crest, double scale)
    {
        if (m.C.Sheet[MoonfallChromePart.ShortRule] is not { } rule)
        {
            m.Dl.AddLine(m.V.Map(cx - (w / 2), y), m.V.Map(cx + (w / 2), y), Ink(GoldInk, 0.8f), MathF.Max(1f, m.V.Size(1.2)));
            return;
        }

        var rh = rule.H * scale;
        Part(m.C, MoonfallChromePart.ShortRule, cx - (w / 2), y - (rh / 2), cx + (w / 2), y + (rh / 2), uint.MaxValue, u0: 20, u1: 60);
        if (crest && m.C.Sheet[MoonfallChromePart.Crest] is { } c)
        {
            var cw = c.W * scale;
            var ch = c.H * scale;
            Part(m.C, MoonfallChromePart.Crest, cx - (cw / 2), y - ch + (rh * 0.55), cx + (cw / 2), y + (rh * 0.55), uint.MaxValue);
        }
    }

    /// <summary>r2kit.title: a title in Jupiter, gilt, with the game's dark edge; returns its width in units.</summary>
    private float MenuTitle(in MenuPen m, double x, double y, string text, float size, Anchor anchor = Anchor.Left, float maxWidth = 0f)
    {
        var px = NamePx(m.V, size, MoonfallFace.Jupiter);
        if (maxWidth > 0)
        {
            var w = MeasureText(MoonfallFace.Jupiter, px, text);
            var room = m.V.Size(maxWidth);
            if (w > room && w > 0)
            {
                px = MathF.Max(px * room / w, NamePx(m.V, 9f, MoonfallFace.Jupiter));
            }
        }

        return DrawText(m.Dl, MoonfallFace.Jupiter, px, m.V.Map(x, y), anchor, Ink(GoldHiInk), text, Ink(MoonfallColor.Hex("#1A0F04")), m.V.Size(1.4)) / m.V.Scale;
    }

    /// <summary>A line of text in <paramref name="face"/> at <paramref name="size"/> units (raised to the floor), its caps' middle at (x, y); returns its width in units.</summary>
    private float MenuText(in MenuPen m, MoonfallFace face, float size, double x, double y, string text, Vector3 ink, Anchor anchor = Anchor.Left, float edge = 0.8f, float alpha = 1f, float maxWidth = 0f)
    {
        var px = face == MoonfallFace.Trump ? NumberPx(m.V, size, face) : NamePx(m.V, size, face);
        if (maxWidth > 0)
        {
            var w = MeasureText(face, px, text);
            var room = m.V.Size(maxWidth);
            var floor = face == MoonfallFace.Trump ? NumberPx(m.V, 1f, face) : NamePx(m.V, 1f, face);
            if (w > room && w > 0)
            {
                px = MathF.Max(px * room / w, floor);
            }
        }

        return DrawText(m.Dl, face, px, m.V.Map(x, y), anchor, Ink(ink, alpha), text, edge > 0 ? Ink(EdgeInk, alpha) : 0u, m.V.Size(edge)) / m.V.Scale;
    }

    /// <summary>Words wrapped to <paramref name="width"/> units in <paramref name="face"/> at <paramref name="size"/>: made once (on a screen's words), never a frame.</summary>
    private string[] Wrap(in MenuPen m, MoonfallFace face, float size, string text, float width)
    {
        var px = NamePx(m.V, size, face);
        var room = m.V.Size(width);
        var lines = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = string.Empty;
        foreach (var word in words)
        {
            var next = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && MeasureText(face, px, next) > room)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = next;
            }
        }

        if (line.Length > 0)
        {
            lines.Add(line);
        }

        return [.. lines];
    }

    // ---- Entries ----

    /// <summary>The warm glow round a focused entry, and the cool one round a hovered one (r2kit.glow_rect).</summary>
    private static void EntryGlow(in MenuPen m, double x0, double y0, double x1, double y1, bool focus, bool hovered, Vector3? warm = null)
    {
        if (!focus && !hovered)
        {
            return;
        }

        var v = m.V;
        var ink = focus ? warm ?? MoonfallColor.Hex("#FFCF7A") : MoonfallColor.Hex("#9DC0FF");
        var r = (y1 - y0) / 2;
        for (var k = 3; k >= 1; k--)
        {
            var grow = k * 3.0;
            m.Dl.AddRectFilled(v.Map(x0 - grow, y0 - grow), v.Map(x1 + grow, y1 + grow), Ink(ink, (hovered && !focus ? 0.07f : 0.09f) * (4 - k) / 3f * 1.6f), v.Size(r + grow));
        }
    }

    /// <summary>The keyboard's focus, made plain: a cream ring round the entry (only while the keys are in use).</summary>
    private static void FocusOutline(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding)
    {
        var grow = new Vector2(3f);
        dl.AddRect(min - grow, max + grow, Ink(Cream, 0.95f), rounding + 3f, ImDrawFlags.None, 2f);
    }

    /// <summary>Asks the default entry for the keyboard focus on a new screen, before its item; then marks it the window's default.</summary>
    private void DefaultFocusBefore(bool isDefault)
    {
        if (isDefault && focusAsked)
        {
            ImGui.SetKeyboardFocusHere();
            focusAsked = false;
        }
    }

    /// <summary>
    /// r2kit.button: a Gold Saucer pill with its label (Jupiter for the main entries, AXIS for the rest) and a line under it
    /// when given; the screen's default entry and the focused one glow warm. A locked entry is slate, says why on hover and
    /// does nothing. True on the click (or the keyboard's or gamepad's activation).
    /// </summary>
    private bool MenuButton(in MenuPen m, string id, double x0, double y0, double x1, double y1, string label, float labelSize, bool primaryFace = true,
        string? sub = null, MenuStyle style = MenuStyle.Normal, bool isDefault = false, string? tooltip = null)
    {
        var v = m.V;
        var min = v.Map(x0, y0);
        var max = v.Map(x1, y1);
        DefaultFocusBefore(isDefault);
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, Vector2.Max(max - min, Vector2.One));
        if (isDefault)
        {
            ImGui.SetItemDefaultFocus();
        }

        var hovered = ImGui.IsItemHovered();
        var nav = ImGui.GetIO().NavVisible && ImGui.IsItemFocused();
        // A sealed entry (Locked, with a padlock) or one whose content is still to come (Waiting) does nothing.
        var inert = style is MenuStyle.Locked or MenuStyle.Waiting;
        var lit = !inert && (nav || (isDefault && !ImGui.GetIO().NavVisible));
        EntryGlow(m, x0, y0, x1, y1, lit, hovered && !inert);
        var state = style switch
        {
            MenuStyle.Danger => MoonfallChromePart.PillDanger,
            MenuStyle.Locked or MenuStyle.Waiting => MoonfallChromePart.PillLocked,
            _ => lit ? MoonfallChromePart.PillFocus : MoonfallChromePart.Pill,
        };
        Pill(m.C, x0, y0, x1, y1, uint.MaxValue, state);
        var h = y1 - y0;
        var face = primaryFace ? MoonfallFace.Jupiter : MoonfallFace.Axis;
        var ink = inert ? LockedInk : Cream;
        var cy = ((y0 + y1) / 2) + (sub is not null ? -h * 0.13 : 0);
        // A sealed entry says so without a mouse: a padlock just before its label (the label moved over to make room for
        // it), and its reason on focus as on hover.
        var locked = style == MenuStyle.Locked;
        var room = (float)(x1 - x0 - (h * (locked ? 1.4 : 0.6)));
        var lx = ((x0 + x1) / 2) + (locked ? h * 0.4 : 0);
        var lw = MenuText(m, face, labelSize, lx, cy, label, ink, Anchor.Centre, edge: 1.0f, maxWidth: room);
        if (locked)
        {
            var px = Math.Max(x0 + (h * 0.5), lx - (Math.Min(lw, room) / 2) - (h * 0.42));
            Padlock(m, px, cy, h * (sub is not null ? 0.16 : 0.22));
        }

        if (sub is not null)
        {
            MenuText(m, MoonfallFace.Axis, (float)(h * 0.27), (x0 + x1) / 2, cy + (h * 0.30), sub, lit ? GoldHiInk : Ink3, Anchor.Centre, edge: 0f, maxWidth: (float)(x1 - x0 - (h * 0.6)));
        }

        if (nav)
        {
            FocusOutline(m.Dl, min, max, v.Size(h / 2));
        }

        if ((hovered || nav) && tooltip is not null)
        {
            UiMetrics.Tooltip(tooltip);
        }

        if (clicked && !inert)
        {
            SoundClick();
            return true;
        }

        return false;
    }

    /// <summary>
    /// A danger pill held to confirm (the owner's rule for Restart and Leave): a lighter fill sweeps it from the left as it
    /// is held, with the mouse or the keyboard's or gamepad's activate key; true on the frame the hold completes.
    /// </summary>
    private bool HoldButton(in MenuPen m, string id, double x0, double y0, double x1, double y1, string label, float labelSize, ref MoonfallHold hold)
    {
        var v = m.V;
        var min = v.Map(x0, y0);
        var max = v.Map(x1, y1);
        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(id, Vector2.Max(max - min, Vector2.One));
        var held = ImGui.IsItemActive();
        var hovered = ImGui.IsItemHovered();
        var nav = ImGui.GetIO().NavVisible && ImGui.IsItemFocused();
        EntryGlow(m, x0, y0, x1, y1, nav, hovered, MoonfallColor.Hex("#FF9AB8"));
        Pill(m.C, x0, y0, x1, y1, uint.MaxValue, MoonfallChromePart.PillDanger);
        var done = hold.Update(held, ImGui.GetIO().DeltaTime);
        var t = hold.Progress;
        if (t > 0)
        {
            var h = y1 - y0;
            var inset = h * 0.07;
            var fx1 = x0 + inset + ((x1 - x0 - (2 * inset)) * t);
            m.Dl.AddRectFilled(v.Map(x0 + inset, y0 + inset), v.Map(fx1, y1 - inset), Ink(MoonfallColor.Hex("#FF9AB8"), 0.26f), v.Size((h / 2) - inset));
        }

        MenuText(m, MoonfallFace.Jupiter, labelSize, (x0 + x1) / 2, (y0 + y1) / 2, label, Cream, Anchor.Centre, edge: 1.0f);
        if (nav)
        {
            FocusOutline(m.Dl, min, max, v.Size((y1 - y0) / 2));
        }

        if (done)
        {
            SoundClick();
        }

        return done;
    }

    /// <summary>play2.toggle: a switch, its track filled in the accent when on, its gilt knob, and On or Off beside it; true when it changed.</summary>
    private bool MenuSwitch(in MenuPen m, string id, double x, double y, bool on, bool small, Vector3 accent)
    {
        var v = m.V;
        var (w, h) = small ? (34.0, 17.0) : (44.0, 22.0);
        var x0 = x - w - (small ? 44 : 54);
        var min = v.Map(x0, y - (h / 2) - 4);
        var max = v.Map(x, y + (h / 2) + 4);
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, Vector2.Max(max - min, Vector2.One));
        var hovered = ImGui.IsItemHovered();
        var nav = ImGui.GetIO().NavVisible && ImGui.IsItemFocused();
        EntryGlow(m, x - w, y - (h / 2), x, y + (h / 2), nav, hovered);
        Pill(m.C, x - w, y - (h / 2), x, y + (h / 2), uint.MaxValue, on ? MoonfallChromePart.PillFocus : MoonfallChromePart.PillLocked);
        if (on)
        {
            m.Dl.AddRectFilled(v.Map(x - w + 3, y - (h / 2) + 3), v.Map(x - 3, y + (h / 2) - 3), Ink(accent, 0.7f), v.Size((h / 2) - 3));
        }

        var kx = on ? x - (h / 2) - 1 : x - w + (h / 2) + 1;
        var kr = v.Size(h * 0.36);
        m.Dl.AddCircleFilled(v.Map(kx, y + (h * 0.04)), kr * 1.05f, Ink(MoonfallColor.Hex("#5A3A10")), 24);
        m.Dl.AddCircleFilled(v.Map(kx, y), kr, Ink(MoonfallColor.Hex("#E2B85A")), 24);
        m.Dl.AddCircleFilled(v.Map(kx - (h * 0.08), y - (h * 0.10)), kr * 0.55f, Ink(MoonfallColor.Hex("#FFF2C8"), 0.85f), 20);
        MenuText(m, MoonfallFace.Axis, small ? 12 : 14, x - w - 10, y, on ? Strings.MoonfallOn : Strings.MoonfallOff, on ? Cream : Ink2, Anchor.Right, edge: 0f);
        if (nav)
        {
            FocusOutline(m.Dl, v.Map(x - w, y - (h / 2)), v.Map(x, y + (h / 2)), v.Size(h / 2));
        }

        if (clicked)
        {
            SoundClick();
        }

        return clicked;
    }

    /// <summary>play2.stepper: a value between two gilt chevron buttons that hug it (20 × 20 at 1280, 16 × 16 at 640); −1 or +1 when one is pressed, 0 otherwise.</summary>
    private int MenuStepper(in MenuPen m, string id, double x, double y, string value, bool small, bool canDown, bool canUp, bool dimValue = false)
    {
        var v = m.V;
        var size = small ? 14f : 16f;
        var b = small ? 16.0 : 20.0;
        var w = Math.Max(MeasureText(MoonfallFace.Axis, NamePx(v, size, MoonfallFace.Axis), value) / v.Scale, small ? 28 : 34);
        var xr = x - (b / 2);
        var xl = x - b - 8 - w - 8 - (b / 2);
        var step = 0;
        for (var side = 0; side < 2; side++)
        {
            var bx = side == 0 ? xl : xr;
            var enabled = side == 0 ? canDown : canUp;
            var min = v.Map(bx - (b / 2) - 2, y - (b / 2) - 2);
            var max = v.Map(bx + (b / 2) + 2, y + (b / 2) + 2);
            ImGui.SetCursorScreenPos(min);
            ImGui.PushID(side);
            var clicked = ImGui.InvisibleButton(id, Vector2.Max(max - min, Vector2.One));
            ImGui.PopID();
            var hovered = ImGui.IsItemHovered();
            var nav = ImGui.GetIO().NavVisible && ImGui.IsItemFocused();
            EntryGlow(m, bx - (b / 2), y - (b / 2), bx + (b / 2), y + (b / 2), nav, hovered && enabled);
            Pill(m.C, bx - (b / 2), y - (b / 2), bx + (b / 2), y + (b / 2), uint.MaxValue, enabled ? MoonfallChromePart.Pill : MoonfallChromePart.PillLocked);
            Chevron(m, bx, y, b * 0.22, side == 0 ? -1 : 1, enabled ? GoldHiInk : Ink3);
            if (nav)
            {
                FocusOutline(m.Dl, v.Map(bx - (b / 2), y - (b / 2)), v.Map(bx + (b / 2), y + (b / 2)), v.Size(b / 2));
            }

            if (clicked && enabled)
            {
                step = side == 0 ? -1 : 1;
            }
        }

        MenuText(m, MoonfallFace.Axis, size, (xl + xr) / 2, y, value, dimValue ? Ink3 : GoldHiInk, Anchor.Centre, edge: 0f);
        return step;
    }

    private static void Chevron(in MenuPen m, double cx, double cy, double arm, int direction, Vector3 ink)
    {
        var v = m.V;
        var tip = v.Map(cx + (direction * arm * 0.55), cy);
        var thickness = MathF.Max(1.2f, v.Size(1.8));
        m.Dl.AddLine(v.Map(cx - (direction * arm * 0.55), cy - arm), tip, Ink(ink), thickness);
        m.Dl.AddLine(tip, v.Map(cx - (direction * arm * 0.55), cy + arm), Ink(ink), thickness);
    }

    /// <summary>r2kit.tab: the window kit's tab plate, lit when active, dim when not (a locked tab is dimmer and says why).</summary>
    private bool MenuTab(in MenuPen m, string id, double x0, double y0, double x1, double y1, string label, bool active, bool locked, string? tooltip)
    {
        var v = m.V;
        var min = v.Map(x0, y0);
        var max = v.Map(x1, y1);
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, Vector2.Max(max - min, Vector2.One));
        var hovered = ImGui.IsItemHovered();
        var nav = ImGui.GetIO().NavVisible && ImGui.IsItemFocused();
        var tint = active ? new Vector3(0.62f, 0.80f, 1f) : locked ? new Vector3(0.30f, 0.34f, 0.55f) : new Vector3(0.42f, 0.50f, 0.85f);
        if (hovered && !active && !locked)
        {
            tint = new Vector3(0.55f, 0.66f, 1f);
        }

        if (m.C.Sheet[MoonfallChromePart.Tab] is not null)
        {
            Part(m.C, MoonfallChromePart.Tab, x0, y0, x1, y1, Ink(tint));
        }
        else
        {
            m.Dl.AddRectFilled(min, max, Ink(tint * 0.4f), v.Size(6));
        }

        var h = y1 - y0;
        var lw = MenuText(m, MoonfallFace.Jupiter, (float)(h * 0.62), (x0 + x1) / 2, (y0 + y1) / 2, label, active ? Cream : locked ? LockedInk : IdleTabInk, Anchor.Centre, edge: 1f);
        if (locked)
        {
            Padlock(m, ((x0 + x1) / 2) - (lw / 2) - (h * 0.42), (y0 + y1) / 2, h * 0.2);
        }

        if (nav)
        {
            FocusOutline(m.Dl, min, max, v.Size(6));
        }

        if ((hovered || nav) && tooltip is not null)
        {
            UiMetrics.Tooltip(tooltip);
        }

        if (clicked && !locked)
        {
            SoundClick();
            return true;
        }

        return false;
    }

    /// <summary>A focusable area with no look of its own (a card, a map stop, a level tile): true on its activation; the caller draws it, lit while <paramref name="nav"/>.</summary>
    private static bool MenuHit(in MenuPen m, string id, double x0, double y0, double x1, double y1, out bool hovered, out bool nav)
    {
        var min = m.V.Map(x0, y0);
        var max = m.V.Map(x1, y1);
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, Vector2.Max(max - min, Vector2.One));
        hovered = ImGui.IsItemHovered();
        nav = ImGui.GetIO().NavVisible && ImGui.IsItemFocused();
        return clicked;
    }

    // ---- Companions: medallions and cards ----

    /// <summary>
    /// r2kit.medallion: the companion's face (the card's face crop) in a Lord of Verminion ring; a glow in their colour
    /// when asked; drained and darkened when <paramref name="drained"/>; the card back's centre for one not yet met.
    /// </summary>
    private void Medallion(in MenuPen m, MoonfallPower power, double cx, double cy, float r, float glow = 0f, bool drained = false, bool back = false, bool dimRing = false)
    {
        var dl = m.Dl;
        var v = m.V;
        var card = MoonfallCards.For(power);
        if (glow > 0 && card is not null && m.HasArt)
        {
            Put(m.A, m.A.Atlas[MoonfallSprite.Soft], cx, cy, r * 2.0f / 4f, Ink(card.Accent, glow));
        }

        dl.AddCircleFilled(v.Map(cx, cy), v.Size(r * 1.04), Ink(PlateInk), 32);
        if (back && m.C.Sheet[MoonfallChromePart.CardBack] is { } cb)
        {
            // The card back's tooled centre in the ring (screens2: the back's middle).
            var rr = r * 1.04;
            var (uv0, uv1) = m.C.Sheet.Uv(MoonfallChromePart.CardBack, 44, 70, 158, 184);
            dl.AddImageRounded(m.C.Tex, v.Map(cx - rr, cy - rr), v.Map(cx + rr, cy + rr), uv0, uv1, uint.MaxValue, v.Size(rr));
            _ = cb;
        }
        else if (!back && card is not null && gameArt?.Card(power) is { } tex)
        {
            var (fx, fy, fs) = card.Face;
            var rr = r * 1.04;
            dl.AddImageRounded(tex.Handle, v.Map(cx - rr, cy - rr), v.Map(cx + rr, cy + rr),
                new Vector2(fx / (float)MoonfallCards.CardWidth, fy / (float)MoonfallCards.CardHeight),
                new Vector2((fx + fs) / (float)MoonfallCards.CardWidth, (fy + fs) / (float)MoonfallCards.CardHeight), uint.MaxValue, v.Size(rr));
        }
        else if (!back && card is not null && gameArt is not null)
        {
            menuArtPending++;
        }

        if (drained)
        {
            dl.AddCircleFilled(v.Map(cx, cy), v.Size(r * 1.04), Ink(MoonfallColor.Hex("#3A3E4C"), 0.45f), 32);
            dl.AddCircleFilled(v.Map(cx, cy), v.Size(r * 1.04), Ink(MoonfallColor.Hex("#070A18"), 0.50f), 32);
        }

        GiltRing(m.C, cx, cy, r, tint: dimRing ? Ink(new Vector3(0.62f), 0.75f) : uint.MaxValue);
        if (m.C.Sheet[MoonfallChromePart.Ring] is null)
        {
            dl.AddCircle(v.Map(cx, cy), v.Size(r * 1.08), Ink(GoldInk, dimRing ? 0.5f : 1f), 32, MathF.Max(1f, v.Size(2.2)));
        }
    }

    /// <summary>
    /// r2kit.card: a companion's Triple Triad card (the card and its gilt border), face down (the back) when the story has
    /// not introduced them, dimmed when met and not reached; the selection glow in their colour when selected.
    /// </summary>
    private void CompanionCard(in MenuPen m, in MoonfallCompanionLook look, double x, double y, double w, bool selected, bool focus)
    {
        var dl = m.Dl;
        var v = m.V;
        var h = w * MoonfallCards.CardHeight / MoonfallCards.CardWidth;
        var power = MoonfallCompanions.TryGet(look.Companion, out var info) ? info.Power : MoonfallPower.None;
        var accent = MoonfallCards.For(power)?.Accent ?? GoldInk;
        if ((selected || focus) && m.C.Sheet[MoonfallChromePart.CardSelect] is not null)
        {
            var breath = selected ? MoonfallMotion.Breath(menuClock, 3f, 0.15f, motion != MoonfallMotionLevel.Full) : 1f;
            var tint = Ink(Vector3.Lerp(accent, Vector3.One, 0.45f), Math.Clamp((selected ? 0.8f : 0.5f) * breath, 0f, 1f));
            Part(m.C, MoonfallChromePart.CardSelect, x - 9, y - 9, x + w + 9, y + h + 9, tint);
        }

        dl.AddRectFilled(v.Map(x + 5, y + 8), v.Map(x + w + 1, y + h + 4), Ink(Vector3.Zero, 0.45f), v.Size(6));
        if (look.Face == MoonfallCardFace.Back)
        {
            if (m.C.Sheet[MoonfallChromePart.CardBack] is not null)
            {
                Part(m.C, MoonfallChromePart.CardBack, x, y, x + w, y + h, uint.MaxValue);
            }
            else
            {
                dl.AddRectFilled(v.Map(x, y), v.Map(x + w, y + h), Ink(MoonfallColor.Hex("#5A4418")), v.Size(6));
            }

            return;
        }

        if (gameArt?.Card(power) is { } tex)
        {
            dl.AddImage(tex.Handle, v.Map(x, y), v.Map(x + w, y + h));
        }
        else
        {
            if (gameArt is not null)
            {
                menuArtPending++;
            }

            dl.AddRectFilled(v.Map(x, y), v.Map(x + w, y + h), Ink(MoonfallColor.Hex("#16245A")), v.Size(6));
            dl.AddRect(v.Map(x, y), v.Map(x + w, y + h), Ink(GoldInk), v.Size(6), ImDrawFlags.None, MathF.Max(1f, v.Size(2)));
        }

        if (look.Face == MoonfallCardFace.Dimmed)
        {
            dl.AddRectFilled(v.Map(x + 3, y + 3), v.Map(x + w - 3, y + h - 3), Ink(MoonfallColor.Hex("#060816"), 0.58f), v.Size(6));
        }
    }

    /// <summary>screens2.lock_mark: a small padlock in dim gilt on a dark disc (a stop or a level not reached yet).</summary>
    private static void Padlock(in MenuPen m, double x, double y, double s)
    {
        var dl = m.Dl;
        var v = m.V;
        dl.AddCircleFilled(v.Map(x, y), v.Size(s * 1.25), Ink(MoonfallColor.Hex("#0A0E22"), 0.95f), 24);
        var gilt = Ink(MoonfallColor.Hex("#C9A15A"));
        dl.PathArcTo(v.Map(x, y - (s * 0.1)), v.Size(s * 0.42), MathF.PI, 2 * MathF.PI, 12);
        dl.PathStroke(gilt, ImDrawFlags.None, MathF.Max(1f, v.Size(s * 0.22)));
        dl.AddLine(v.Map(x - (s * 0.42), y - (s * 0.1)), v.Map(x - (s * 0.42), y + (s * 0.05)), gilt, MathF.Max(1f, v.Size(s * 0.22)));
        dl.AddLine(v.Map(x + (s * 0.42), y - (s * 0.1)), v.Map(x + (s * 0.42), y + (s * 0.05)), gilt, MathF.Max(1f, v.Size(s * 0.22)));
        dl.AddRectFilled(v.Map(x - (s * 0.62), y - (s * 0.15)), v.Map(x + (s * 0.62), y + (s * 0.75)), Ink(MoonfallColor.Hex("#E2B85A")), v.Size(s * 0.15));
        dl.AddRectFilled(v.Map(x - (s * 0.62), y + (s * 0.35)), v.Map(x + (s * 0.62), y + (s * 0.75)), Ink(MoonfallColor.Hex("#8A5A12"), 0.6f), v.Size(s * 0.15));
        dl.AddCircleFilled(v.Map(x, y + (s * 0.28)), v.Size(s * 0.12), Ink(MoonfallColor.Hex("#2A1A06")), 8);
    }

    /// <summary>screens2.pick_mark: the free-choice stage's sign, a gilt four-point star on an enamel disc.</summary>
    private static void PickStar(in MenuPen m, double x, double y, double r, bool dim)
    {
        var dl = m.Dl;
        var v = m.V;
        dl.AddCircleFilled(v.Map(x, y), v.Size(r * 1.02), Ink(MoonfallColor.Hex("#1A2766")), 32);
        dl.AddCircleFilled(v.Map(x, y + (r * 0.25)), v.Size(r * 0.8), Ink(MoonfallColor.Hex("#0A1030"), 0.6f), 32);
        var hi = Ink(MoonfallColor.Hex("#FFF2C8"), dim ? 0.55f : 1f);
        var lo = Ink(MoonfallColor.Hex("#C8962E"), dim ? 0.55f : 1f);
        var c = v.Map(x, y);
        var a = r * 0.78;
        var waist = r * 0.16;
        // Four slim triangles from the centre, lit from the upper left.
        ReadOnlySpan<(double Dx, double Dy)> points = [(0, -1), (1, 0), (0, 1), (-1, 0)];
        foreach (var (dx, dy) in points)
        {
            var tip = v.Map(x + (dx * a), y + (dy * a));
            var side1 = v.Map(x + (dy * waist), y - (dx * waist));
            var side2 = v.Map(x - (dy * waist), y + (dx * waist));
            dl.AddTriangleFilled(c, side1, tip, dx + dy < 0 ? hi : lo);
            dl.AddTriangleFilled(c, tip, side2, dx + dy < 0 ? lo : hi);
        }
    }

    /// <summary>A lit orange moon (the won pip): the halo and the lit orange peg sprite.</summary>
    private void Pip(in MenuPen m, double x, double y, float r, int variant = 1, bool lit = true)
    {
        if (!m.HasArt)
        {
            m.Dl.AddCircleFilled(m.V.Map(x, y), m.V.Size(r), Ink(MoonfallColor.Hex(lit ? "#FF9A3C" : "#3A2A1A")), 16);
            return;
        }

        if (lit)
        {
            Put(m.A, m.A.Atlas[MoonfallSprite.Halo], x, y, r / 10f * 1.4f, Ink(MoonfallColor.Hex("#FFB070"), 0.8f));
            Put(m.A, m.A.Atlas.Peg(PegColour.Orange, variant % Math.Max(1, m.A.Atlas.PegVariants), true), x, y, r / 10f, uint.MaxValue);
        }
        else
        {
            Put(m.A, m.A.Atlas.Peg(PegColour.Orange, variant % Math.Max(1, m.A.Atlas.PegVariants), false), x, y, r / 10f, Ink(new Vector3(0.45f), 0.8f));
        }
    }

    // ---- Level thumbnails ----

    /// <summary>Each level's pegs as dealt for its thumbnail (a game made once per level, seed 1, never a frame).</summary>
    private readonly Dictionary<string, MoonfallGame> previews = new(StringComparer.Ordinal);

    private MoonfallGame Preview(MoonfallLevel level)
    {
        if (!previews.TryGetValue(level.Id, out var g))
        {
            var number = MoonfallStages.TryPlace(level.Id, out var place) ? place.Number : 5;
            g = new MoonfallGame(level, Math.Max(number, MoonfallRules.FirstGreenLevel), 1);
            previews[level.Id] = g;
        }

        return g;
    }

    /// <summary>
    /// A level's thumbnail in a gilt frame: its board as dealt (every peg and brick in its colour, the launcher), over the
    /// night in its scene's own palette. <paramref name="w"/> units wide; the height keeps the opening's 1300 × 1106 shape.
    /// </summary>
    private double LevelThumb(in MenuPen m, MoonfallLevel level, double x, double y, double w, double frameScale = 0.3, Vector4? clip = null)
    {
        var dl = m.Dl;
        var v = m.V;
        var h = w * 1106 / 1300;
        dl.AddRectFilled(v.Map(x + 3, y + 6), v.Map(x + w + 3, y + h + 6), Ink(Vector3.Zero, 0.5f), v.Size(2));
        var palette = gameArt?.PaletteFor(level) ?? MoonfallChromePalette.Medallion;
        var min = v.Map(x, y);
        var max = v.Map(x + w, y + h);
        // The opening between the walls (x 75..725, y 41..594 of the board) fills the thumbnail (composite2's crop).
        var (ox, oy, ow) = clip is { } c ? (c.X, c.Y, c.Z) : (75f, 41f, 650f);
        var k = (float)(w / ow * v.Scale);
        var sub = new View(min - (new Vector2(ox, oy) * k), k, 1f, BoardCentre);
        dl.PushClipRect(min, max, true);
        if (gameArt?.Thumb(level, out var board) is { } scene)
        {
            // The level's own scene, built at 1x and scaled down; the night in its palette stands in while it builds.
            dl.AddImage(scene.Handle, sub.Map(board.X, board.Y), sub.Map(board.Z, board.W));
        }
        else
        {
            var sky = Ink(Vector3.Lerp(palette.Sky, palette.Jewel1, 0.35f));
            var deep = Ink(palette.Deep);
            var jewel = Ink(Vector3.Lerp(palette.Deep, palette.Jewel2, 0.5f));
            dl.AddRectFilledMultiColor(min, max, sky, sky, jewel, deep);
        }

        var g = Preview(level);
        if (m.HasArt)
        {
            var pen = m.A with { View = sub };
            for (var i = 0; i < g.PegCount; i++)
            {
                var peg = g.Peg(i);
                if (peg.Shape == PegShape.Round)
                {
                    Put(pen, pen.Atlas.Peg(peg.Colour, i % Math.Max(1, pen.Atlas.PegVariants), false), peg.X, peg.Y, (float)(peg.Radius / 10), uint.MaxValue);
                }
                else
                {
                    ThumbBrick(dl, sub, peg);
                }
            }

            Put(pen, pen.Atlas[MoonfallSprite.LauncherHub], MoonfallRules.LauncherX, MoonfallRules.LauncherY, 1f, uint.MaxValue);
        }
        else
        {
            for (var i = 0; i < g.PegCount; i++)
            {
                var peg = g.Peg(i);
                dl.AddCircleFilled(sub.Map(peg.X, peg.Y), MathF.Max(1f, sub.Size(peg.Radius)), Theme.U32(ThumbInk(peg.Colour)), 10);
            }
        }

        dl.PopClipRect();
        GiltBand(m.C, x, y, x + w, y + h, frameScale);
        return h;
    }

    private static Vector4 ThumbInk(PegColour colour) => colour switch
    {
        PegColour.Orange => new Vector4(MoonfallColor.Hex("#FF9A3D"), 1f),
        PegColour.Green => new Vector4(MoonfallColor.Hex("#73D980"), 1f),
        PegColour.Purple => new Vector4(MoonfallColor.Hex("#CC80F2"), 1f),
        _ => new Vector4(MoonfallColor.Hex("#BFD1FA"), 1f),
    };

    private static void ThumbBrick(ImDrawListPtr dl, in View sub, in MoonfallPegView peg)
    {
        var ink = Theme.U32(ThumbInk(peg.Colour));
        var thick = MathF.Max(1f, sub.Size(peg.Thickness));
        if (peg.Shape == PegShape.Line)
        {
            dl.AddLine(sub.Map(peg.X, peg.Y), sub.Map(peg.X2, peg.Y2), ink, thick);
            return;
        }

        dl.PathArcTo(sub.Map(peg.X, peg.Y), sub.Size(peg.Radius), (float)peg.StartRadians, (float)(peg.StartRadians + peg.SweepRadians), 10);
        dl.PathStroke(ink, ImDrawFlags.None, thick);
    }

    /// <summary>A sealed tile: the card back's tooled gilt, darkened and drained so it recedes, with a padlock.</summary>
    private void SealedThumb(in MenuPen m, double x, double y, double w, double h)
    {
        var dl = m.Dl;
        var v = m.V;
        if (m.C.Sheet[MoonfallChromePart.CardBack] is not null)
        {
            Part(m.C, MoonfallChromePart.CardBack, x, y, x + w, y + h, Ink(new Vector3(0.36f, 0.36f, 0.40f)), u0: 20, v0: 30, u1: 182, v1: 224);
            dl.AddRectFilled(v.Map(x, y), v.Map(x + w, y + h), Ink(MoonfallColor.Hex("#101830"), 0.45f));
        }
        else
        {
            dl.AddRectFilled(v.Map(x, y), v.Map(x + w, y + h), Ink(MoonfallColor.Hex("#14182A")));
        }

        GiltBand(m.C, x, y, x + w, y + h, 0.3, tint: Ink(Vector3.One, 0.6f));
    }
}
