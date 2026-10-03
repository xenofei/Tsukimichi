using System;
using Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The Caption and Display type roles (ui-revamp §4.2, T17): game font handles from the plugin's own font atlas
/// (<c>UiBuilder.FontAtlas.NewDelegateFontHandle</c>: the game's Axis glyphs, the punctuation Axis may lack ("›", "·",
/// "–") from Dalamud's default font, and the extra glyphs of Dalamud's language for Chinese and Korean clients), one
/// pair per UI-scale bucket (0.9 / 1.0 / 1.15 / 1.3 / 1.6), each the
/// Axis size nearest to what that bucket draws (<see cref="TypeScale"/>): the game's fonts are pre-baked bitmaps, and
/// one handle scaled bilinearly across every scale would blur (dalamud-developer panel §5). When the UI scale moves to
/// another bucket the old handles are disposed and the new ones built by the atlas in the background; until a handle
/// is <see cref="IFontHandle.Available"/>, its role falls back to the default font drawn at the role's size.
/// <para>
/// A scope (<see cref="Caption"/>, <see cref="Display"/>) pushes the handle and sets the current window's font scale so
/// that <see cref="ImGui.GetFontSize"/> is the role's size: 0.85× the body with a 12 px floor, or 1.2×. Widgets,
/// <c>CalcTextSize</c> and <c>AddText(pos, col, text)</c> inside it all draw at that size. On dispose the window's own
/// scale and font come back. Allocation-free per frame (the handle's push is pooled by Dalamud).
/// </para>
/// <para>
/// The Moon Road roles (proposal §4, feature plan v4 V3) are built the same way, one handle per bucket at the nearest
/// native size (<see cref="TypeScale"/>): <see cref="TypeRole.Eyebrow"/> in TrumpGothic, <see cref="TypeRole.Title"/>
/// in Jupiter and <see cref="TypeRole.Numeral"/> in MiedingerMid. They hold the game font's glyphs only, so a string
/// the face cannot draw falls back as a whole (<see cref="Push"/>) instead of mixing faces within a word. What the game's
/// font files hold (read from the 2026.09 client's <c>common/font/*.fdt</c>): TrumpGothic and Jupiter have all of
/// ASCII and Latin-1 (accented letters for French and German names, "·" U+00B7) plus "–", "—" and "’", but no "›",
/// "…", "→" or "•"; MiedingerMid has ASCII and Latin-1 with "·" but no "–", "—", "’", "›" or "…"; none has kana,
/// kanji or hangul. Jupiter 45 and 90 are digits only and are never used. The roles are built only while
/// <see cref="GameHeadingFonts"/> is on (Settings › General › Look), and each falls back to the role it replaces.
/// </para>
/// <para>
/// Text size (feature plan v6 U7): away from 100% a body font is built at that size from Dalamud's default font and
/// pushed around every Tsukimichi window (<see cref="Body"/>), and every role is picked from the body size times the
/// text size, so captions, headings and numbers follow it. A change rebuilds the handles on the next frame; meanwhile
/// the default font, scaled, stands in at the same size.
/// </para>
/// </summary>
public static class Typography
{
    private static readonly GameFontFamilyAndSize[] GameFonts =
    [
        GameFontFamilyAndSize.Axis96,
        GameFontFamilyAndSize.Axis12,
        GameFontFamilyAndSize.Axis14,
        GameFontFamilyAndSize.Axis18,
        GameFontFamilyAndSize.Axis36,
    ];

    // In the order of TypeScale.EyebrowGameFontSizesPx, TitleGameFontSizesPx and NumeralGameFontSizesPx.
    private static readonly GameFontFamilyAndSize[] EyebrowFonts =
    [
        GameFontFamilyAndSize.TrumpGothic184,
        GameFontFamilyAndSize.TrumpGothic23,
        GameFontFamilyAndSize.TrumpGothic34,
        GameFontFamilyAndSize.TrumpGothic68,
    ];

    private static readonly GameFontFamilyAndSize[] TitleFonts =
    [
        GameFontFamilyAndSize.Jupiter16,
        GameFontFamilyAndSize.Jupiter20,
        GameFontFamilyAndSize.Jupiter23,
        GameFontFamilyAndSize.Jupiter46,
    ];

    private static readonly GameFontFamilyAndSize[] NumeralFonts =
    [
        GameFontFamilyAndSize.MiedingerMid10,
        GameFontFamilyAndSize.MiedingerMid12,
        GameFontFamilyAndSize.MiedingerMid14,
        GameFontFamilyAndSize.MiedingerMid18,
        GameFontFamilyAndSize.MiedingerMid36,
    ];

    private static IFontAtlas? atlas;
    private static IPluginLog? log;
    private static int bucket = -1;

    // The game fonts the current handles were built with (indices into GameFonts); -1 before the first build.
    private static int captionFont = -1;
    private static int displayFont = -1;
    private static IFontHandle? caption;
    private static IFontHandle? display;

    // The Moon Road roles: indices into EyebrowFonts, TitleFonts and NumeralFonts, -1 while not built.
    private static int eyebrowFont = -1;
    private static int titleFont = -1;
    private static int numeralFont = -1;
    private static IFontHandle? eyebrow;
    private static IFontHandle? title;
    private static IFontHandle? numeral;
    private static bool headingFontsFailed;

    // The body font at the text size (Settings › General › Text size): built only away from 100%, in pixels at global
    // scale 1; 0 while none is built or wanted.
    private static IFontHandle? body;
    private static float bodyPx;
    private static bool bodyBuilt;
    private static bool bodyFailed;

    /// <summary>
    /// Whether the Moon Road roles draw in the game's fonts: Settings › Display › Look › Game fonts for headings, off
    /// under Plain flair (<see cref="FlairRules.GameHeadingFonts"/>). As of the last <see cref="Update"/>.
    /// </summary>
    public static bool GameHeadingFonts { get; private set; }

    /// <summary>Gives the typography the plugin's font atlas; handles are created on the first <see cref="Update"/>.</summary>
    public static void Initialize(IFontAtlas fontAtlas, IPluginLog? pluginLog)
    {
        atlas = fontAtlas ?? throw new ArgumentNullException(nameof(fontAtlas));
        log = pluginLog;
        bucket = -1;
        captionFont = -1;
        displayFont = -1;
        eyebrowFont = titleFont = numeralFont = -1;
        headingFontsFailed = false;
        bodyFailed = false;
        bodyPx = 0f;
    }

    /// <summary>
    /// Once per frame after <see cref="UiMetrics.Update"/>, outside any window: when the UI scale entered another bucket,
    /// or Dalamud's font size changed enough that another Axis size is now nearest, disposes the old handles and asks
    /// the atlas for the new pair. The heading roles are built the same way while <paramref name="headingFonts"/> is
    /// on, and disposed when it goes off. Nothing happens otherwise.
    /// </summary>
    public static void Update(bool headingFonts)
    {
        GameHeadingFonts = headingFonts;
        if (atlas is null)
        {
            return;
        }

        var next = TypeScale.Bucket(UiMetrics.UiScale);

        // The body size at UI scale 1 and global scale 1: the default font's size without Dalamud's global scale.
        var defaultPx = ImGui.GetFont().FontSize / UiMetrics.GlobalScale;
        if (!float.IsFinite(defaultPx) || defaultPx <= 0f)
        {
            UiMetrics.SetTextFontBuilt(false);
            return;
        }

        UpdateBodyHandle(atlas, defaultPx, UiMetrics.TextScale);

        // Every role is sized from the body, so the text size carries into the game font each role picks.
        var basePx = defaultPx * UiMetrics.TextScale;

        UpdateHeadingHandles(atlas, next, basePx, headingFonts);
        CheckHeadingLoad();
        var nextCaption = TypeScale.CaptionGameFont(next, basePx);
        var nextDisplay = TypeScale.DisplayGameFont(next, basePx);
        if (next == bucket && nextCaption == captionFont && nextDisplay == displayFont)
        {
            return;
        }

        DisposeHandles();
        bucket = next;
        captionFont = nextCaption;
        displayFont = nextDisplay;
        try
        {
            caption = NewRoleHandle(atlas, GameFonts[captionFont]);
            display = NewRoleHandle(atlas, GameFonts[displayFont]);
        }
        catch (Exception ex)
        {
            // No game fonts (a data problem, an atlas that refuses): the roles keep their fallback for good.
            log?.Warning(ex, "Game font handles unavailable; captions and titles use the default font");
            DisposeHandles();
        }
    }

    /// <summary>
    /// Builds the body font at the text size (Dalamud's own default font, its icons and the glyphs of Dalamud's language
    /// included), rebuilding it when the text size or Dalamud's font size moved, and says whether this frame draws with
    /// it (<see cref="UiMetrics.SetTextFontBuilt"/>). At 100% none is built: Dalamud's font is already that size. Until a
    /// new handle is built the default font stands in, scaled to the same size, so nothing moves when it lands.
    /// </summary>
    private static void UpdateBodyHandle(IFontAtlas fontAtlas, float defaultPx, float textScale)
    {
        var wanted = bodyFailed || MathF.Abs(textScale - ScaleMetrics.DefaultTextScale) < 0.001f ? 0f : ScaleMetrics.TextFontPx(defaultPx, textScale);
        if (wanted != bodyPx)
        {
            DisposeBodyHandle();
            bodyPx = wanted;
            if (wanted > 0f)
            {
                try
                {
                    body = fontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(wanted)));
                }
                catch (Exception ex)
                {
                    log?.Warning(ex, "Text size font unavailable; text is scaled from the default font instead");
                    bodyFailed = true;
                    DisposeBodyHandle();
                }
            }
        }

        if (!bodyFailed && body?.LoadException is { } error)
        {
            log?.Warning(error, "Text size font failed to build; text is scaled from the default font instead");
            bodyFailed = true;
            DisposeBodyHandle();
        }

        bodyBuilt = body is { Available: true };
        UiMetrics.SetTextFontBuilt(bodyBuilt);
    }

    private static void DisposeBodyHandle()
    {
        body?.Dispose();
        body = null;
        bodyPx = 0f;
        bodyBuilt = false;
    }

    /// <summary>
    /// The body font until disposed: the font built at the text size once it is ready, Dalamud's default font otherwise
    /// (scaled to the same size by <see cref="UiMetrics.FontScale"/>). The plugin pushes it around every window it
    /// draws; tooltips push it again so one hung off a heading role still reads in the body font. Allocation-free.
    /// </summary>
    public static BodyScope Body() => new(bodyBuilt ? body : null);

    /// <summary>Builds, rebuilds or disposes the Eyebrow, Title and Numeral handles for the bucket and the setting.</summary>
    private static void UpdateHeadingHandles(IFontAtlas fontAtlas, int next, float basePx, bool on)
    {
        var nextEyebrow = on ? TypeScale.EyebrowGameFont(next, basePx) : -1;
        var nextTitle = on ? TypeScale.TitleGameFont(next, basePx) : -1;
        var nextNumeral = on ? TypeScale.NumeralGameFont(next, basePx) : -1;
        if (nextEyebrow == eyebrowFont && nextTitle == titleFont && nextNumeral == numeralFont)
        {
            return;
        }

        DisposeHeadingHandles();
        eyebrowFont = nextEyebrow;
        titleFont = nextTitle;
        numeralFont = nextNumeral;
        if (!on || headingFontsFailed)
        {
            return;
        }

        try
        {
            eyebrow = NewGameOnlyHandle(fontAtlas, EyebrowFonts[eyebrowFont]);
            title = NewGameOnlyHandle(fontAtlas, TitleFonts[titleFont]);
            numeral = NewGameOnlyHandle(fontAtlas, NumeralFonts[numeralFont]);
        }
        catch (Exception ex)
        {
            // The headings keep the Caption and Display roles for the session rather than retrying every bucket change.
            log?.Warning(ex, "Game heading fonts unavailable; headings use the caption and display roles");
            headingFontsFailed = true;
            DisposeHeadingHandles();
        }
    }

    /// <summary>
    /// A heading handle whose build failed (<see cref="IFontHandle.LoadException"/>: the delegate handle never throws when
    /// created, the atlas records the failure when it builds): logged once, and the headings keep the Caption and
    /// Display roles for the session, as when creating the handles throws.
    /// </summary>
    private static void CheckHeadingLoad()
    {
        if (headingFontsFailed)
        {
            return;
        }

        var error = eyebrow?.LoadException ?? title?.LoadException ?? numeral?.LoadException;
        if (error is null)
        {
            return;
        }

        log?.Warning(error, "Game heading fonts failed to build; headings use the caption and display roles");
        headingFontsFailed = true;
        DisposeHeadingHandles();
    }

    /// <summary>
    /// A heading role's handle: the game font's own glyphs and nothing merged in, so <see cref="Push"/> can tell from
    /// the built font whether a string is drawable in the face.
    /// </summary>
    private static IFontHandle NewGameOnlyHandle(IFontAtlas fontAtlas, GameFontFamilyAndSize family)
    {
        var style = new GameFontStyle(family);
        return fontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.Font = tk.AddGameGlyphs(style, null, default)));
    }

    /// <summary>
    /// Punctuation the captions and titles use (the scope's "›", "·", "–", "…" and quotes), merged from Dalamud's
    /// default font so a glyph the game's Axis lacks never shows as the fallback box: Latin-1 and General Punctuation.
    /// </summary>
    private static readonly ushort[] PunctuationRanges = [0x00A0, 0x00FF, 0x2010, 0x205E, 0];

    /// <summary>
    /// A role's handle: the game font's glyphs first, then the punctuation of Dalamud's default font and the extra
    /// glyphs of Dalamud's language (Chinese and Korean quest names) merged in at the same size.
    /// </summary>
    private static IFontHandle NewRoleHandle(IFontAtlas fontAtlas, GameFontFamilyAndSize family)
    {
        var style = new GameFontStyle(family);
        return fontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            var font = tk.AddGameGlyphs(style, null, default);
            tk.AddDalamudAssetFont(DalamudAsset.NotoSansCjkMedium, new SafeFontConfig { SizePx = style.SizePx, MergeFont = font, GlyphRanges = PunctuationRanges });
            tk.AttachExtraGlyphsForDalamudLanguage(new SafeFontConfig { SizePx = style.SizePx, MergeFont = font });
            tk.Font = font;
        }));
    }

    /// <summary>Disposes the handles (plugin unload).</summary>
    public static void Dispose()
    {
        DisposeHandles();
        DisposeHeadingHandles();
        DisposeBodyHandle();
        bodyFailed = false;
        atlas = null;
        bucket = -1;
        captionFont = -1;
        displayFont = -1;
        eyebrowFont = titleFont = numeralFont = -1;
    }

    /// <summary>The caption size in the current window: 0.85× its body size, never under 12 px.</summary>
    public static float CaptionSize => TypeScale.CaptionPx(ImGui.GetFontSize());

    /// <summary>The display size in the current window: 1.2× its body size.</summary>
    public static float DisplaySize => TypeScale.DisplayPx(ImGui.GetFontSize());

    /// <summary>Draws in the caption role until disposed (table headers, pills, the status bar, card titles, the provenance line).</summary>
    public static Scope Caption() => new(caption, CaptionSize);

    /// <summary>Draws in the display role until disposed (the hero title, the empty-state and tour headings).</summary>
    public static Scope Display() => new(display, DisplaySize);

    /// <summary>
    /// Draws in a Moon Road role until disposed, for <paramref name="text"/>: the role's game font at its size
    /// (<see cref="TypeScale.EyebrowPxFor"/>, <see cref="TypeScale.TitlePxFor"/>, <see cref="TypeScale.NumeralPxFor"/>)
    /// when <see cref="GameHeadingFonts"/> is on, the handle is built and the font has a glyph for every character of
    /// <paramref name="text"/>; otherwise the role it replaces: Eyebrow → <see cref="Caption"/>, Title →
    /// <see cref="Display"/>, Numeral → the current font unchanged. Pass the string the scope will draw, so one the game
    /// face lacks a glyph for (a Japanese quest name, "›") falls back as a whole; an empty span skips the check (for
    /// strings known to be ASCII). Allocation-free.
    /// </summary>
    public static Scope Push(TypeRole role, ReadOnlySpan<char> text = default)
    {
        var handle = GameHeadingFonts ? HeadingHandle(role) : null;
        if (handle is { Available: true })
        {
            var body = ImGui.GetFontSize();
            var scope = new Scope(handle, role switch
            {
                TypeRole.Eyebrow => TypeScale.EyebrowPxFor(body),
                TypeRole.Title => TypeScale.TitlePxFor(body),
                _ => TypeScale.NumeralPxFor(body),
            });
            if (scope.GameFont && Covers(text))
            {
                return scope;
            }

            scope.Dispose();
        }

        return role switch
        {
            TypeRole.Eyebrow => Caption(),
            TypeRole.Title => Display(),
            _ => new Scope(null, ImGui.GetFontSize()),
        };
    }

    /// <summary>Section headings in the Eyebrow role (TrumpGothic), falling back to the caption role: <see cref="Push"/>.</summary>
    public static Scope Eyebrow(ReadOnlySpan<char> text) => Push(TypeRole.Eyebrow, text);

    /// <summary>A pane's one title in the Title role (Jupiter), falling back to the display role: <see cref="Push"/>.</summary>
    public static Scope Title(ReadOnlySpan<char> text) => Push(TypeRole.Title, text);

    /// <summary>Counts and percentages in the Numeral role (MiedingerMid), falling back to the current font: <see cref="Push"/>.</summary>
    public static Scope Numeral(ReadOnlySpan<char> text) => Push(TypeRole.Numeral, text);

    private static IFontHandle? HeadingHandle(TypeRole role) => role switch
    {
        TypeRole.Eyebrow => eyebrow,
        TypeRole.Title => title,
        TypeRole.Numeral => numeral,
        _ => null,
    };

    /// <summary>Whether the current font (a heading role just pushed) has a glyph of its own for every printable character.</summary>
    private static unsafe bool Covers(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return true;
        }

        var font = ImGui.GetFont();
        foreach (var c in text)
        {
            if (c < ' ')
            {
                continue;
            }

            if (font.FindGlyphNoFallback(c) == null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// An icon-font glyph as a text item at the current font size (inside a <see cref="Caption"/> scope, the caption
    /// size), so an icon beside a role's text matches it; the window's font scale comes back afterwards.
    /// </summary>
    public static void Icon(string icon)
    {
        var target = ImGui.GetFontSize();
        var window = ImGuiP.GetCurrentWindow();
        var own = window.FontWindowScale;
        ImGui.PushFont(UiBuilder.IconFont);
        var now = ImGui.GetFontSize();
        if (now > 0f && target > 0f && float.IsFinite(target))
        {
            ImGui.SetWindowFontScale(own * target / now);
        }

        ImGui.TextUnformatted(icon);
        ImGui.SetWindowFontScale(own);
        ImGui.PopFont();
    }

    private static void DisposeHandles()
    {
        caption?.Dispose();
        display?.Dispose();
        caption = null;
        display = null;
    }

    private static void DisposeHeadingHandles()
    {
        eyebrow?.Dispose();
        title?.Dispose();
        numeral?.Dispose();
        eyebrow = null;
        title = null;
        numeral = null;
    }

    /// <summary>A pushed role: the game font when it is built, and the window font scale that brings it to the role's size.</summary>
    public struct Scope : IDisposable
    {
        private readonly float ownScale;
        private IDisposable? font;
        private bool active;

        internal Scope(IFontHandle? handle, float targetPx)
        {
            var window = ImGuiP.GetCurrentWindow();
            ownScale = window.FontWindowScale;
            font = handle is { Available: true } ? handle.Push() : null;
            active = true;

            // Whatever font is now current, scale the window so text lands on the target size.
            var now = ImGui.GetFontSize();
            if (now > 0f && targetPx > 0f && float.IsFinite(targetPx))
            {
                ImGui.SetWindowFontScale(ownScale * targetPx / now);
            }
        }

        /// <summary>Whether the game font is in use (false while it is still being built: the default font stands in).</summary>
        public readonly bool GameFont => font is not null;

        public void Dispose()
        {
            if (!active)
            {
                return;
            }

            active = false;
            font?.Dispose();
            font = null;
            ImGui.SetWindowFontScale(ownScale);
        }
    }
}

/// <summary>A pushed body font (<see cref="Typography.Body"/>): the text-size handle's push, or the default font's.</summary>
public readonly struct BodyScope : IDisposable
{
    private readonly IDisposable? pushed;
    private readonly bool active;

    internal BodyScope(IFontHandle? handle)
    {
        active = true;
        if (handle is { Available: true })
        {
            pushed = handle.Push();
        }
        else
        {
            pushed = null;
            ImGui.PushFont(UiBuilder.DefaultFont);
        }
    }

    public void Dispose()
    {
        if (!active)
        {
            return;
        }

        if (pushed is not null)
        {
            pushed.Dispose();
        }
        else
        {
            ImGui.PopFont();
        }
    }
}

/// <summary>The Moon Road type roles (proposal §4): each a game display font, each falling back to the role it replaces.</summary>
public enum TypeRole
{
    /// <summary>TrumpGothic: section headings ("Requirements"), table headers, rail labels. Falls back to Caption.</summary>
    Eyebrow,

    /// <summary>Jupiter: the one title per pane (the hero quest name, a zone or character name). Falls back to Display.</summary>
    Title,

    /// <summary>MiedingerMid: counts, percentages, level pills. Falls back to the current font.</summary>
    Numeral,
}
