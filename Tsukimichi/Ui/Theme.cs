using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Colour tokens (spec §2.3, ui-revamp §4.3) and the style pushes built from them. The fixed tokens (Night … Eclipse,
/// the glyph gradients) never change; the surface and text roles the chrome draws with live in <see cref="Surface"/>,
/// which is the Night palette or, with <see cref="Configuration.FollowDalamudColours"/> on, a palette mapped from the
/// user's Dalamud style (one layout, two palettes: game UX panel finding 9). <see cref="Refresh"/> picks the palette
/// once per frame, before any window draws.
///
/// Gold discipline (game UX panel finding 2): Moon is for what the player can act on now (Ready, Accepted, the next
/// step, pins, the MSQ pill, primary buttons, progress) and the selected tree row's rule; chrome that is not a call to
/// action (sort arrows, selection outlines, focus rings, generic badges, active segments) uses Silver or VeilLine.
/// </summary>
public static class Theme
{
    /// <summary>#0F1424 – panel backgrounds for the detail pane and path view.</summary>
    public static readonly Vector4 Night = Rgb(GlyphTokens.NightHex);

    /// <summary>#F2D27A – Completed, Ready, gold accents, progress fill.</summary>
    public static readonly Vector4 Moon = Rgb(GlyphTokens.MoonHex);

    /// <summary>#DDE3F0 – primary text on Night, silver glyphs.</summary>
    public static readonly Vector4 Silver = Rgb(GlyphTokens.SilverHex);

    /// <summary>#7C86A8 – tertiary text, rings, separators (5.1 : 1 on Night; fails AA on raised surfaces, so never body text on cards).</summary>
    public static readonly Vector4 Dusk = Rgb(GlyphTokens.DuskHex);

    /// <summary>#B25C7F – Foreclosed, destructive actions.</summary>
    public static readonly Vector4 Eclipse = Rgb(GlyphTokens.EclipseHex);

    /// <summary>#4A5270 – disabled, unknown.</summary>
    public static readonly Vector4 Veil = Rgb(GlyphTokens.VeilHex);

    /// <summary>#3A4363 – the dark side of every state moon and the filling moon (glyph proposal v2.1 §3.4); always behind a rim.</summary>
    public static readonly Vector4 Shadow = Rgb(GlyphTokens.ShadowHex);

    /// <summary>#5C6584 – line tone that clears 3 : 1 on Night (3.19 : 1): the halo gauge track and similar thin strokes.</summary>
    public static readonly Vector4 VeilLine = Rgb(GlyphTokens.VeilLineHex);

    /// <summary>Dark disc of the unlit glyph states. Now <see cref="Shadow"/>; the name stays so callers compile.</summary>
    public static readonly Vector4 UnlitDisc = Shadow;

    /// <summary>#2C334A – the old unlit disc, kept as the maria, crater and vignette tint of the moons' interior detail only (glyph proposal §3.6).</summary>
    public static readonly Vector4 Umbra = Rgb(0x2C334A);

    /// <summary>#FFF0BE – the highlight stop of the gold gradient (glyph proposal §3.1); also the terminator glow on gold.</summary>
    public static readonly Vector4 MoonHigh = Rgb(GlyphTokens.MoonHighHex);

    /// <summary>#D6B25A – the deep stop of the gold gradient, at the limb away from the light.</summary>
    public static readonly Vector4 MoonDeep = Rgb(GlyphTokens.MoonDeepHex);

    /// <summary>#FFFFFF – the highlight stop of the silver gradient.</summary>
    public static readonly Vector4 SilverHigh = Rgb(GlyphTokens.SilverHighHex);

    /// <summary>#B9C2D8 – the deep stop of the silver gradient.</summary>
    public static readonly Vector4 SilverDeep = Rgb(GlyphTokens.SilverDeepHex);

    /// <summary>
    /// Moon dimmed toward Dusk, for the name and count of a completed tree node: still reads as gold, but quieter than
    /// a Ready row's glyph so finished chapters recede (ui-revamp §2.3, T11).
    /// </summary>
    public static readonly Vector4 MoonDim = Vector4.Lerp(Moon, Dusk, 0.35f);

    /// <summary>A panel one step above Night (a quarter of the way to Veil, #1E2437), for cards and header cards on the Night background.</summary>
    public static readonly Vector4 NightRaised = Vector4.Lerp(Night, Veil, 0.25f);

    /// <summary>#0B0F1C – wells below the window: search pill, gauge wells, level pills, the title bar (ui-revamp §4.3).</summary>
    public static readonly Vector4 NightSunken = Rgb(GlyphTokens.NightSunkenHex);

    /// <summary>#262D45 – hover fill for rows, tabs and buttons.</summary>
    public static readonly Vector4 NightHover = Rgb(0x262D45);

    /// <summary>#2A3149 – hairlines, card borders, row separators (subtle; <see cref="VeilLine"/> where 3 : 1 is needed).</summary>
    public static readonly Vector4 NightLine = Rgb(0x2A3149);

    /// <summary>#A9B2CC – secondary text: hints, captions, chip labels (8.7 : 1 on Night, 7.3 on NightRaised). Dusk stays tertiary.</summary>
    public static readonly Vector4 Mist = Rgb(0xA9B2CC);

    /// <summary>#8A93B0 – the Unknown state's text and dashed ring (Veil itself fails AA for text).</summary>
    public static readonly Vector4 VeilText = Rgb(0x8A93B0);

    /// <summary>#D68AA8 – Foreclosed text and state-pill text (Eclipse fails 4.5 : 1 for text).</summary>
    public static readonly Vector4 EclipseText = Rgb(0xD68AA8);

    /// <summary>Alternate table row tint for zebra striping: Veil at low alpha, readable on Night and on the default style alike.</summary>
    public static readonly Vector4 ZebraRow = Veil with { W = 0.16f };

    public static readonly uint NightU32 = Pack(Night);
    public static readonly uint MoonU32 = Pack(Moon);
    public static readonly uint SilverU32 = Pack(Silver);
    public static readonly uint DuskU32 = Pack(Dusk);
    public static readonly uint EclipseU32 = Pack(Eclipse);
    public static readonly uint VeilU32 = Pack(Veil);
    public static readonly uint ShadowU32 = Pack(Shadow);
    public static readonly uint VeilLineU32 = Pack(VeilLine);
    public static readonly uint UnlitDiscU32 = Pack(UnlitDisc);
    public static readonly uint UmbraU32 = Pack(Umbra);
    public static readonly uint MoonHighU32 = Pack(MoonHigh);
    public static readonly uint MoonDeepU32 = Pack(MoonDeep);
    public static readonly uint SilverHighU32 = Pack(SilverHigh);
    public static readonly uint SilverDeepU32 = Pack(SilverDeep);
    public static readonly uint MoonDimU32 = Pack(MoonDim);
    public static readonly uint NightRaisedU32 = Pack(NightRaised);
    public static readonly uint NightSunkenU32 = Pack(NightSunken);
    public static readonly uint NightHoverU32 = Pack(NightHover);
    public static readonly uint NightLineU32 = Pack(NightLine);
    public static readonly uint MistU32 = Pack(Mist);
    public static readonly uint VeilTextU32 = Pack(VeilText);
    public static readonly uint EclipseTextU32 = Pack(EclipseText);

    /// <summary>The Night palette as surface roles: what <see cref="Surface"/> is unless the user follows Dalamud's colours.</summary>
    public static readonly SurfaceColors NightSurface = new(Night, NightSunken, NightRaised, NightHover, NightLine, VeilLine, Silver, Mist, Dusk, Veil, Light: false);

    /// <summary>The surface and text roles in effect this frame (see <see cref="Refresh"/>).</summary>
    public static SurfaceColors Surface { get; private set; } = NightSurface;

    /// <summary>Whether <see cref="Surface"/> is mapped from the user's Dalamud style this frame.</summary>
    public static bool FollowingDalamud { get; private set; }

    /// <summary>
    /// The glyph palette in effect this frame (Settings › Display › Glyph palette), resolved against the palette's window
    /// colour: Standard, or the high-contrast variant for a dark or a light host (<see cref="GlyphPalette.Resolve"/>).
    /// <see cref="MoonGlyph"/>, <see cref="Marks"/> and the table stripe paint from it.
    /// </summary>
    public static GlyphPalette Glyphs { get; private set; } = GlyphPalette.Standard;

    /// <summary>
    /// Gold for text and small ink (pill labels, the active tab icon): <see cref="Moon"/>, or under "Follow Dalamud
    /// colours" Moon pushed towards the palette's text colour until it reads at 4.5 : 1 on the window, so a light
    /// Dalamud style gets a deep gold instead of Moon's 1.4 : 1. Fills, rims and glyphs keep Moon.
    /// </summary>
    public static Vector4 Accent { get; private set; } = Moon;

    /// <summary><see cref="Accent"/> packed for ImDrawList calls.</summary>
    public static uint AccentU32 { get; private set; } = MoonU32;

    /// <summary><see cref="MoonDim"/> as text, made to read on the palette the way <see cref="Accent"/> is.</summary>
    public static Vector4 AccentDim { get; private set; } = MoonDim;

    /// <summary>The host style's window background alpha as of the last <see cref="Refresh"/>; <see cref="PushNightWindow"/> keeps it.</summary>
    private static float hostWindowAlpha = 1f;

    private static readonly Vector4 Transparent = Vector4.Zero;

    /// <summary>
    /// Picks this frame's palette. Call once per frame before any window draws (nothing is pushed then, so the style
    /// read is the user's own): with <paramref name="followDalamud"/> the surface roles are mapped from the Dalamud
    /// style (<see cref="SurfaceColors.FromHost"/>), otherwise they are the Night tokens. The glyph palette
    /// <paramref name="glyphPalette"/> is resolved against the resulting window colour (<see cref="Glyphs"/>), so the
    /// high-contrast glyphs switch to their light variant on a light Dalamud theme. Allocates nothing.
    /// </summary>
    public static void Refresh(bool followDalamud, GlyphPaletteKind glyphPalette = GlyphPaletteKind.Standard)
    {
        var colors = ImGui.GetStyle().Colors;
        var windowBg = colors[(int)ImGuiCol.WindowBg];
        hostWindowAlpha = float.IsFinite(windowBg.W) ? Math.Clamp(windowBg.W, 0f, 1f) : 1f;
        FollowingDalamud = followDalamud;
        Surface = followDalamud
            ? SurfaceColors.FromHost(
                windowBg,
                colors[(int)ImGuiCol.FrameBg],
                colors[(int)ImGuiCol.FrameBgHovered],
                colors[(int)ImGuiCol.Border],
                colors[(int)ImGuiCol.Text],
                colors[(int)ImGuiCol.TextDisabled])
            : NightSurface;
        var s = Surface;
        Accent = followDalamud ? ColorMath.EnsureContrast(Moon, s.Text, s.Window, SurfaceColors.TextMinContrast) : Moon;
        AccentDim = followDalamud ? ColorMath.EnsureContrast(MoonDim, s.Text, s.Window, SurfaceColors.TextMinContrast) : MoonDim;
        AccentU32 = Pack(Accent);
        Glyphs = GlyphPalette.Resolve(glyphPalette, s.Window);
    }

    /// <summary>
    /// Draws with <paramref name="palette"/> until the returned scope is disposed, then restores the frame's palette:
    /// the glyph window's side-by-side comparison. A struct; <c>using</c> allocates nothing.
    /// </summary>
    public static GlyphScope PushGlyphs(GlyphPalette palette)
    {
        var previous = Glyphs;
        Glyphs = palette;
        return new GlyphScope(previous);
    }

    /// <summary>Restores the glyph palette that was in effect before <see cref="PushGlyphs"/>. Dispose exactly once.</summary>
    public readonly struct GlyphScope(GlyphPalette previous) : IDisposable
    {
        public void Dispose()
        {
            if (previous is not null)
            {
                Glyphs = previous;
            }
        }
    }

    /// <summary>Text color for a state badge next to a glyph.</summary>
    public static Vector4 StateColor(QuestState state) => state switch
    {
        QuestState.Completed => Moon,
        QuestState.Accepted => Moon,
        QuestState.Ready => Moon,
        QuestState.ReadyOnOtherJob => Silver,
        QuestState.DoneThisCycle => Silver,
        QuestState.Blocked => Dusk,
        QuestState.Foreclosed => Eclipse,
        QuestState.Unknown => Veil,
        _ => Dusk,
    };

    public static uint StateColorU32(QuestState state) => Pack(StateColor(state));

    /// <summary>The token with a different alpha, packed for ImDrawList calls.</summary>
    public static uint WithAlpha(Vector4 color, float alpha) => Pack(color with { W = Math.Clamp(alpha, 0f, 1f) });

    public static Vector4 WithAlphaVector(Vector4 color, float alpha) => color with { W = Math.Clamp(alpha, 0f, 1f) };

    /// <summary>A colour packed for ImDrawList calls (IM_COL32, no style alpha applied); allocation-free.</summary>
    public static uint U32(Vector4 color) => Pack(color);

    /// <summary>The outline behind text drawn over game scenes (<c>Chrome.OutlinedText</c>): the palette's window colour, opaque.</summary>
    public static uint OutlineU32 => Pack(Surface.Window with { W = 1f });

    /// <summary>
    /// Night panel colours for the detail pane, path view and similar: child background, text, secondary text, borders
    /// and separators from <see cref="Surface"/> (Night unless following Dalamud's colours). Dispose to pop.
    /// </summary>
    public static ImRaii.ColorDisposable PushNightPanel(bool condition = true) =>
        ImRaii.PushColor(ImGuiCol.ChildBg, Surface.Window, condition)
              .Push(ImGuiCol.Text, Surface.Text, condition)
              .Push(ImGuiCol.TextDisabled, Surface.TextSecondary, condition)
              .Push(ImGuiCol.Border, Surface.Line, condition)
              .Push(ImGuiCol.Separator, Surface.Line, condition)
              .Push(ImGuiCol.TableBorderLight, Surface.Line, condition)
              .Push(ImGuiCol.TableBorderStrong, Surface.StrongLine, condition);

    /// <summary>
    /// A count of pushed style colours and variables, popped on <see cref="Dispose"/>. A struct, so <c>using</c> on it
    /// allocates nothing; <c>default</c> pops nothing. Dispose exactly once.
    /// </summary>
    public readonly struct StyleScope(int colors, int vars) : IDisposable
    {
        public int Colors { get; } = colors;

        public int Vars { get; } = vars;

        public void Dispose()
        {
            if (Vars > 0)
            {
                ImGui.PopStyleVar(Vars);
            }

            if (Colors > 0)
            {
                ImGui.PopStyleColor(Colors);
            }
        }
    }

    /// <summary>
    /// Window-wide Night chrome, for <c>Window.PreDraw</c> (dispose the returned scope in <c>PostDraw</c>): window,
    /// title bar, child, frame, button, header, tab, table, scrollbar, separator and resize-grip colours plus text,
    /// check marks, slider grabs and the nav highlight, all from <see cref="Surface"/>. The window background keeps the
    /// host style's alpha and <c>Window.BgAlpha</c> is never touched, so the user's Dalamud opacity still applies
    /// (Dalamud panel §4).
    ///
    /// Popups and tooltips, decided explicitly: a colour pushed before Begin is still on the stack while Draw runs, so
    /// every popup, combo and tooltip begun inside the window inherits it. <c>PopupBg</c> is therefore pushed as well,
    /// so they read as Night as a whole rather than half-Night by accident; <see cref="PushTooltip"/> (every
    /// <see cref="UiMetrics.Tooltip(string)"/>) and <see cref="PushPopup"/> restyle them explicitly on top, which is
    /// also what windows without this push (the Todo overlay's menus, Nearby, Settings) get. The title bar and
    /// Dalamud's own title-bar buttons take <c>TitleBg*</c> from here. With <see cref="FollowingDalamud"/> nothing is
    /// pushed: the host style already is the palette.
    /// </summary>
    public static StyleScope PushNightWindow()
    {
        if (FollowingDalamud)
        {
            return default;
        }

        var s = Surface;
        var count = 0;
        Push(ImGuiCol.WindowBg, s.Window with { W = hostWindowAlpha }, ref count);
        Push(ImGuiCol.ChildBg, Transparent, ref count);
        Push(ImGuiCol.PopupBg, s.Window with { W = 0.97f }, ref count);
        Push(ImGuiCol.Border, s.Line, ref count);
        Push(ImGuiCol.BorderShadow, Transparent, ref count);
        Push(ImGuiCol.Text, s.Text, ref count);
        Push(ImGuiCol.TextDisabled, s.TextSecondary, ref count);
        Push(ImGuiCol.TextSelectedBg, WithAlphaVector(s.Text, 0.20f), ref count);
        Push(ImGuiCol.TitleBg, s.Sunken, ref count);
        Push(ImGuiCol.TitleBgActive, s.Raised, ref count);
        Push(ImGuiCol.TitleBgCollapsed, WithAlphaVector(s.Sunken, 0.75f), ref count);
        Push(ImGuiCol.MenuBarBg, s.Raised, ref count);
        Push(ImGuiCol.FrameBg, s.Sunken, ref count);
        Push(ImGuiCol.FrameBgHovered, s.Hover, ref count);
        Push(ImGuiCol.FrameBgActive, Vector4.Lerp(s.Hover, s.Text, 0.08f) with { W = 1f }, ref count);
        Push(ImGuiCol.Button, s.Raised, ref count);
        Push(ImGuiCol.ButtonHovered, s.Hover, ref count);
        Push(ImGuiCol.ButtonActive, Vector4.Lerp(s.Hover, s.Text, 0.12f) with { W = 1f }, ref count);
        // Selection is not a call to action: a neutral wash, never gold (the tree keeps its own gold rule).
        Push(ImGuiCol.Header, SelectionWash, ref count);
        Push(ImGuiCol.HeaderHovered, s.Hover, ref count);
        Push(ImGuiCol.HeaderActive, SelectionWashActive, ref count);
        Push(ImGuiCol.Tab, s.Sunken, ref count);
        Push(ImGuiCol.TabHovered, s.Hover, ref count);
        Push(ImGuiCol.TabActive, s.Raised, ref count);
        Push(ImGuiCol.TabUnfocused, s.Sunken, ref count);
        Push(ImGuiCol.TabUnfocusedActive, s.Raised, ref count);
        Push(ImGuiCol.TableHeaderBg, s.Raised, ref count);
        Push(ImGuiCol.TableBorderStrong, s.StrongLine, ref count);
        Push(ImGuiCol.TableBorderLight, s.Line, ref count);
        Push(ImGuiCol.TableRowBg, Transparent, ref count);
        Push(ImGuiCol.TableRowBgAlt, ZebraRow, ref count);
        Push(ImGuiCol.ScrollbarBg, WithAlphaVector(s.Sunken, 0.5f), ref count);
        Push(ImGuiCol.ScrollbarGrab, WithAlphaVector(s.StrongLine, 0.7f), ref count);
        Push(ImGuiCol.ScrollbarGrabHovered, s.StrongLine, ref count);
        Push(ImGuiCol.ScrollbarGrabActive, s.TextTertiary, ref count);
        Push(ImGuiCol.Separator, s.Line, ref count);
        Push(ImGuiCol.SeparatorHovered, s.StrongLine, ref count);
        Push(ImGuiCol.SeparatorActive, s.TextTertiary, ref count);
        Push(ImGuiCol.ResizeGrip, WithAlphaVector(s.StrongLine, 0.35f), ref count);
        Push(ImGuiCol.ResizeGripHovered, s.StrongLine, ref count);
        Push(ImGuiCol.ResizeGripActive, s.TextTertiary, ref count);
        Push(ImGuiCol.CheckMark, s.Text, ref count);
        Push(ImGuiCol.SliderGrab, s.StrongLine, ref count);
        Push(ImGuiCol.SliderGrabActive, s.Text, ref count);
        Push(ImGuiCol.NavHighlight, s.Text, ref count);
        return new StyleScope(count, 0);
    }

    /// <summary>
    /// A tooltip in the active palette (ui-revamp §3 "Tooltip"): fill at 0.96, a hairline border, primary text, the
    /// secondary tone for <c>TextDisabled</c> lines, rounding 6 and padding 10 × 8. Push before <c>BeginTooltip</c>
    /// (<see cref="UiMetrics.Tooltip(string)"/> does), dispose after it ends.
    /// </summary>
    public static StyleScope PushTooltip()
    {
        var s = Surface;
        var count = 0;
        Push(ImGuiCol.PopupBg, s.Window with { W = 0.96f }, ref count);
        Push(ImGuiCol.Border, s.Line, ref count);
        Push(ImGuiCol.Text, s.Text, ref count);
        Push(ImGuiCol.TextDisabled, s.TextSecondary, ref count);
        Push(ImGuiCol.Separator, s.Line, ref count);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, UiMetrics.Px(6f));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(UiMetrics.Px(10f), UiMetrics.Px(8f)));
        return new StyleScope(count, 3);
    }

    /// <summary>
    /// A popup or context menu in the active palette: the tooltip's surface plus menu-item, frame, button and check
    /// colours, rounding 6. Push before <c>BeginPopup…</c>, dispose after it ends; the items inside draw with it.
    /// </summary>
    public static StyleScope PushPopup()
    {
        var s = Surface;
        var count = 0;
        Push(ImGuiCol.PopupBg, s.Window with { W = 0.98f }, ref count);
        Push(ImGuiCol.Border, s.Line, ref count);
        Push(ImGuiCol.Text, s.Text, ref count);
        Push(ImGuiCol.TextDisabled, s.TextSecondary, ref count);
        Push(ImGuiCol.Separator, s.Line, ref count);
        Push(ImGuiCol.Header, SelectionWash, ref count);
        Push(ImGuiCol.HeaderHovered, s.Hover, ref count);
        Push(ImGuiCol.HeaderActive, SelectionWashActive, ref count);
        Push(ImGuiCol.FrameBg, s.Sunken, ref count);
        Push(ImGuiCol.FrameBgHovered, s.Hover, ref count);
        Push(ImGuiCol.Button, s.Raised, ref count);
        Push(ImGuiCol.ButtonHovered, s.Hover, ref count);
        Push(ImGuiCol.CheckMark, s.Text, ref count);
        Push(ImGuiCol.NavHighlight, s.Text, ref count);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, UiMetrics.Px(6f));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
        return new StyleScope(count, 2);
    }

    /// <summary>Eclipse-toned button for destructive actions (Forget character, Delete all data). Dispose to pop.</summary>
    public static ImRaii.ColorDisposable PushDestructiveButton(bool condition = true) =>
        ImRaii.PushColor(ImGuiCol.Button, WithAlphaVector(Eclipse, 0.75f), condition)
              .Push(ImGuiCol.ButtonHovered, Eclipse, condition)
              .Push(ImGuiCol.ButtonActive, Vector4.Lerp(Eclipse, Night, 0.25f) with { W = 1f }, condition)
              .Push(ImGuiCol.Text, Silver, condition);

    /// <summary>Text in the token color, for badges. Dispose to pop.</summary>
    public static ImRaii.ColorDisposable PushText(Vector4 color, bool condition = true) =>
        ImRaii.PushColor(ImGuiCol.Text, color, condition);

    /// <summary>Selected rows and menu items: a neutral wash of the text colour (never gold).</summary>
    private static Vector4 SelectionWash => WithAlphaVector(Surface.Text, 0.10f);

    private static Vector4 SelectionWashActive => WithAlphaVector(Surface.Text, 0.16f);

    private static void Push(ImGuiCol idx, Vector4 color, ref int count)
    {
        ImGui.PushStyleColor(idx, color);
        count++;
    }

    private static Vector4 Rgb(uint hex) => new(
        ((hex >> 16) & 0xFF) / 255f,
        ((hex >> 8) & 0xFF) / 255f,
        (hex & 0xFF) / 255f,
        1f);

    /// <summary>IM_COL32 layout (0xAABBGGRR). Done here rather than through ImGui so the static fields need no ImGui context.</summary>
    private static uint Pack(Vector4 c)
    {
        static uint Channel(float v) => (uint)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
        return Channel(c.X) | (Channel(c.Y) << 8) | (Channel(c.Z) << 16) | (Channel(c.W) << 24);
    }
}
