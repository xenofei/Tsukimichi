using System;
using System.Collections.Generic;
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
/// Plan v7 adds <see cref="TypeRole.Section"/> (the detail pane's card headings, 1.80× tapering to 1.60× at 150 % Text
/// size), <see cref="TypeRole.Header"/> (the Journal's column headers, 1.55×) and <see cref="TypeRole.HeroTitle"/> (the
/// Full quest title, kept above the Section headings). A heading never falls back below the body size: the Section
/// role falls back to <see cref="Lead"/>, the body face built at 1.15×, never to the caption.
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

    // Quiet's quest title (and the Full hero title's fallback): an Axis size, built as the display role is.
    private static int quietTitleFont = -1;
    private static IFontHandle? quietTitle;

    // The game-face roles (TypeRole): the game font each draws in, or null while not built. Roles that pick the same
    // font share one handle from the pool, so Eyebrow, Section and Header at one TrumpGothic size cost one build.
    private static readonly GameFontFamilyAndSize?[] RoleFonts = new GameFontFamilyAndSize?[RoleCount];
    private static readonly Dictionary<GameFontFamilyAndSize, IFontHandle> HeadingPool = [];
    private static readonly List<GameFontFamilyAndSize> UnusedFonts = [];
    private static bool headingFontsFailed;

    private const int RoleCount = (int)TypeRole.HeroTitle + 1;

    // The body font at the text size (Settings › General › Text size): built only away from 100%, in pixels at global
    // scale 1; 0 while none is built or wanted.
    private static IFontHandle? body;
    private static float bodyPx;
    private static bool bodyBuilt;
    private static bool bodyFailed;

    // The Lead font (the body face at 1.15×: Quiet's section headings, the Section fallback, Plain's quest title), built
    // from Dalamud's default font at the text size; 0 while none is built.
    private static IFontHandle? lead;
    private static float leadPx;
    private static bool leadFailed;

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
        quietTitleFont = -1;
        Array.Clear(RoleFonts);
        headingFontsFailed = false;
        bodyFailed = false;
        bodyPx = 0f;
        leadFailed = false;
        leadPx = 0f;
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

        UpdateLeadHandle(atlas, basePx);
        UpdateHeadingHandles(atlas, next, basePx, UiMetrics.TextScale, headingFonts);
        CheckHeadingLoad();
        var nextCaption = TypeScale.CaptionGameFont(next, basePx);
        var nextDisplay = TypeScale.DisplayGameFont(next, basePx);
        var nextQuietTitle = TypeScale.QuietTitleGameFont(next, basePx);
        if (next == bucket && nextCaption == captionFont && nextDisplay == displayFont && nextQuietTitle == quietTitleFont)
        {
            return;
        }

        DisposeHandles();
        bucket = next;
        captionFont = nextCaption;
        displayFont = nextDisplay;
        quietTitleFont = nextQuietTitle;
        try
        {
            caption = NewRoleHandle(atlas, GameFonts[captionFont]);
            display = NewRoleHandle(atlas, GameFonts[displayFont]);
            quietTitle = NewRoleHandle(atlas, GameFonts[quietTitleFont]);
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

    /// <summary>
    /// Builds the Lead font (<see cref="TypeScale.LeadFontPx"/>: the body face at 1.15× the body at the text size),
    /// rebuilding it when that size moved. Like the body font it reaches the UI scale through the window's font scale,
    /// so it is exactly as crisp as the body text under it. Until it is built the body font stands in, scaled.
    /// </summary>
    private static void UpdateLeadHandle(IFontAtlas fontAtlas, float basePx)
    {
        var wanted = leadFailed ? 0f : TypeScale.LeadFontPx(basePx);
        if (wanted != leadPx)
        {
            DisposeLeadHandle();
            leadPx = wanted;
            if (wanted > 0f)
            {
                try
                {
                    lead = fontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(wanted)));
                }
                catch (Exception ex)
                {
                    log?.Warning(ex, "Heading font unavailable; headings are scaled from the body font instead");
                    leadFailed = true;
                    DisposeLeadHandle();
                }
            }
        }

        if (!leadFailed && lead?.LoadException is { } error)
        {
            log?.Warning(error, "Heading font failed to build; headings are scaled from the body font instead");
            leadFailed = true;
            DisposeLeadHandle();
        }
    }

    private static void DisposeLeadHandle()
    {
        lead?.Dispose();
        lead = null;
        leadPx = 0f;
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

    /// <summary>
    /// Picks the game font of every game-face role (<see cref="TypeRole"/>) for the bucket, the body and the text size
    /// (the Section role and the hero title taper with the text size), builds the ones the pool lacks and disposes the
    /// ones no role uses any more. While <paramref name="on"/> is off every handle goes. Nothing happens when no role's
    /// font moved.
    /// </summary>
    private static void UpdateHeadingHandles(IFontAtlas fontAtlas, int next, float basePx, float textScale, bool on)
    {
        var changed = false;
        for (var i = 0; i < RoleCount; i++)
        {
            GameFontFamilyAndSize? font = on && !headingFontsFailed ? RoleFont((TypeRole)i, next, basePx, textScale) : null;
            if (RoleFonts[i] != font)
            {
                RoleFonts[i] = font;
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        UnusedFonts.Clear();
        foreach (var font in HeadingPool.Keys)
        {
            if (Array.IndexOf(RoleFonts, font) < 0)
            {
                UnusedFonts.Add(font);
            }
        }

        foreach (var font in UnusedFonts)
        {
            HeadingPool[font].Dispose();
            HeadingPool.Remove(font);
        }

        try
        {
            foreach (var wanted in RoleFonts)
            {
                if (wanted is { } font && !HeadingPool.ContainsKey(font))
                {
                    HeadingPool[font] = NewGameOnlyHandle(fontAtlas, font);
                }
            }
        }
        catch (Exception ex)
        {
            // The headings keep their fallbacks for the session rather than retrying every bucket change.
            log?.Warning(ex, "Game heading fonts unavailable; headings use the body and display faces");
            headingFontsFailed = true;
            DisposeHeadingHandles();
        }
    }

    /// <summary>The game font <paramref name="role"/> draws in for the bucket, the body at UI scale 1 and the text size.</summary>
    private static GameFontFamilyAndSize RoleFont(TypeRole role, int next, float basePx, float textScale) => role switch
    {
        TypeRole.Eyebrow => EyebrowFonts[TypeScale.EyebrowGameFont(next, basePx)],
        TypeRole.Title => TitleFonts[TypeScale.TitleGameFont(next, basePx)],
        TypeRole.Numeral => NumeralFonts[TypeScale.NumeralGameFont(next, basePx)],
        TypeRole.Section => EyebrowFonts[TypeScale.SectionGameFont(next, basePx, textScale)],
        TypeRole.Header => EyebrowFonts[TypeScale.HeaderGameFont(next, basePx)],
        _ => TitleFonts[TypeScale.HeroTitleGameFont(next, basePx, textScale)],
    };

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

        Exception? error = null;
        foreach (var handle in HeadingPool.Values)
        {
            error ??= handle.LoadException;
        }

        if (error is null)
        {
            return;
        }

        log?.Warning(error, "Game heading fonts failed to build; headings use the body and display faces");
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
        DisposeLeadHandle();
        bodyFailed = false;
        leadFailed = false;
        atlas = null;
        bucket = -1;
        captionFont = -1;
        displayFont = -1;
        quietTitleFont = -1;
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
                TypeRole.Section => TypeScale.SectionPxFor(body, UiMetrics.TextScale),
                TypeRole.Header => TypeScale.HeaderPxFor(body),
                TypeRole.HeroTitle => TypeScale.HeroTitlePxFor(body, UiMetrics.TextScale),
                _ => TypeScale.NumeralPxFor(body),
            }, gameFace: true);
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
            TypeRole.Section => Lead(),
            TypeRole.HeroTitle => QuietTitle(),
            _ => new Scope(null, ImGui.GetFontSize()),
        };
    }

    /// <summary>Section headings in the Eyebrow role (TrumpGothic), falling back to the caption role: <see cref="Push"/>.</summary>
    public static Scope Eyebrow(ReadOnlySpan<char> text) => Push(TypeRole.Eyebrow, text);

    /// <summary>A pane's one title in the Title role (Jupiter), falling back to the display role: <see cref="Push"/>.</summary>
    public static Scope Title(ReadOnlySpan<char> text) => Push(TypeRole.Title, text);

    /// <summary>Counts and percentages in the Numeral role (MiedingerMid), falling back to the current font: <see cref="Push"/>.</summary>
    public static Scope Numeral(ReadOnlySpan<char> text) => Push(TypeRole.Numeral, text);

    /// <summary>
    /// A Section heading at Full (the detail pane's cards, the filter drawer's sections): TrumpGothic at
    /// <see cref="TypeScale.SectionPxFor"/>, falling back to <see cref="Lead"/> (never a caption): <see cref="Push"/>.
    /// Whether the game face is in use is the scope's <see cref="Scope.GameFace"/>, which says whether to track it
    /// (<see cref="SectionTracking"/>) and upper-case it.
    /// </summary>
    public static Scope Section(ReadOnlySpan<char> text) => Push(TypeRole.Section, text);

    /// <summary>The Journal's column headers at Full: TrumpGothic at 1.55× the body, falling back to the body size: <see cref="Push"/>.</summary>
    public static Scope Header(ReadOnlySpan<char> text) => Push(TypeRole.Header, text);

    /// <summary>Full's hero quest title: Jupiter at <see cref="TypeScale.HeroTitlePxFor"/>, falling back to <see cref="QuietTitle"/>: <see cref="Push"/>.</summary>
    public static Scope HeroTitle(ReadOnlySpan<char> text) => Push(TypeRole.HeroTitle, text);

    /// <summary>
    /// Draws in the body face one step up (1.15×, <see cref="TypeScale.LeadFactor"/>) until disposed: Quiet's section
    /// headings, the Section role's fallback and Plain's quest title. The Lead font once it is built, the body font
    /// scaled until then.
    /// </summary>
    public static Scope Lead() => new(lead, TypeScale.LeadPxFor(ImGui.GetFontSize()));

    /// <summary>Draws Quiet's quest title block until disposed: the display face at 1.40× the body (<see cref="TypeScale.QuietTitleFactor"/>).</summary>
    public static Scope QuietTitle() => new(quietTitle, TypeScale.QuietTitlePxFor(ImGui.GetFontSize()));

    /// <summary>
    /// The letter spacing of the Section role in the current font, in pixels: +0.08 em while <paramref name="scope"/>
    /// draws in the game face, none in the body face's fallback. Call inside the scope.
    /// </summary>
    public static float SectionTracking(in Scope scope) => scope.GameFace ? TypeScale.TrackingPx(ImGui.GetFontSize(), TypeScale.SectionTrackingEm) : 0f;

    /// <summary>The column headers' letter spacing in the current font: +0.08 em in the game face, none otherwise. Call inside the scope.</summary>
    public static float HeaderTracking(in Scope scope) => scope.GameFace ? TypeScale.TrackingPx(ImGui.GetFontSize(), TypeScale.HeaderTrackingEm) : 0f;

    private static IFontHandle? HeadingHandle(TypeRole role) =>
        RoleFonts[(int)role] is { } font && HeadingPool.TryGetValue(font, out var handle) ? handle : null;

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
        quietTitle?.Dispose();
        caption = null;
        display = null;
        quietTitle = null;
    }

    /// <summary>Disposes every game-face handle; the roles fall back until <see cref="Update"/> builds them again.</summary>
    private static void DisposeHeadingHandles()
    {
        foreach (var handle in HeadingPool.Values)
        {
            handle.Dispose();
        }

        HeadingPool.Clear();
        Array.Clear(RoleFonts);
    }

    /// <summary>A pushed role: the game font when it is built, and the window font scale that brings it to the role's size.</summary>
    public struct Scope : IDisposable
    {
        private readonly float ownScale;
        private readonly bool gameFace;
        private IDisposable? font;
        private bool active;

        internal Scope(IFontHandle? handle, float targetPx, bool gameFace = false)
        {
            var window = ImGuiP.GetCurrentWindow();
            ownScale = window.FontWindowScale;
            font = handle is { Available: true } ? handle.Push() : null;
            this.gameFace = gameFace;
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

        /// <summary>
        /// Whether a Moon Road role draws in its game display face (not a fallback): what decides the capitals and
        /// the letter spacing of a Section heading or a column header. False for the plain roles and while building.
        /// </summary>
        public readonly bool GameFace => gameFace && font is not null;

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

    /// <summary>TrumpGothic, tracked: the detail pane's card headings, the filter drawer's sections (plan v7). Falls back to the Lead face.</summary>
    Section,

    /// <summary>TrumpGothic, tracked: the Journal's column headers at Full (plan v7). Falls back to the body size.</summary>
    Header,

    /// <summary>Jupiter: the Full hero's quest title, the largest text in the pane (plan v7). Falls back to Quiet's title face.</summary>
    HeroTitle,
}
