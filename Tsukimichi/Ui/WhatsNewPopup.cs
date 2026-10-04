using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Releases;
using Tsukimichi.Core.Ui;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// What's new (spec-1.22 W1, W2; replaces the main window's What's new card, W4). After an update has installed, a
/// popup shows what arrived, once, at the first quiet moment after login (<see cref="WhatsNew.IsQuietMoment"/>: not in
/// combat, a duty, a cutscene, group pose or a loading screen, and 10 s after the character is in the world), never on a
/// fresh install (<see cref="WhatsNew.Decide(string?, string?, bool, bool)"/>). Several releases at once are pages,
/// newest first, under "You were on 1.18.0". It is not modal: the game keeps its input. Close, × or Esc closes it, and
/// only then is the version recorded (<see cref="Configuration.LastSeenVersion"/>), so a game closed first shows it
/// again next time. Settings › Advanced › What's new opens it on any release, with ‹ › walking the whole history.
/// <para>
/// The layout never moves while the player pages (<see cref="WhatsNewLayout"/>): 560 px wide, the notes block sized to
/// the tallest page at the current Text size, capped at 80 % of the screen (past that a page's notes scroll), and the
/// art band on every page or on none. Full draws the theme's frame kit and the release's picture in the theme's craft;
/// Quiet a tonal card with Classic's picture; Plain a flat ledger with no picture, loading none. The picture is one
/// texture held only while the popup is open (<see cref="ReleaseArtTexture"/>); a missing one shows the band's flat sky.
/// Opens with a fade and a 4 px rise, pages cross-fade in place, Close fades out; all instant under Reduce motion and at
/// Plain (<see cref="Motion.Enabled"/>).
/// </para>
/// </summary>
public sealed class WhatsNewPopup : Window, IDisposable
{
    private const string Id = "###TsukimichiWhatsNew";

    private const ImGuiWindowFlags PopupFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoBackground;

    // Logical px at UI scale 1.0 (spec-1.22 W1, "Anatomy").
    private const float PadLogical = 16f;
    private const float BodyTopLogical = 12f;
    private const float TitleGapLogical = 10f;
    private const float PointGapLogical = 8f;
    private const float BulletIndentLogical = 14f;
    private const float MoonLogical = 28f;
    private const float DotLogical = 5f;
    private const float PlainDotLogical = 4f;
    private const float ArtRoundingLogical = 4f;
    private const float QuietRoundingLogical = 8f;
    private const float ShadowOffsetLogical = 22f;
    private const float ShadowBlurLogical = 48f;
    private const float ShadowAlpha = 0.55f;
    private const float ScrollbarLogical = 6f;

    private readonly Configuration settings;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly ReleaseArtTexture art;
    private readonly string pluginDir;

    // The pages on screen, and per page its points as one line each with the lead's length, and its subtitle.
    private ReleaseNote[] pages = [];
    private string[][] lines = [];
    private int[][] leads = [];
    private string[] subtitles = [];
    private int subtitlesLanguage = -1;
    private int page;
    private string? wasOn;

    // Whether closing records the running version (the update's popup, or the history opened before it showed).
    private bool recordOnClose;

    // The update's pages wait here for the first quiet moment.
    private ReleaseNote[]? pending;
    private string? pendingWasOn;

    // The pictures per page for the look they were found for, and whether the art band shows.
    private string?[] artPaths = [];
    private (string Theme, Flair Flair, ReleaseNote[] Pages)? artKey;
    private bool artBand;

    // Placement and motion.
    private bool placed;
    private Vector2 target;
    private Vector2 size = new(560f, 480f);
    private double openedAt;
    private double leavingAt = double.NaN;
    private double pageAt = double.NegativeInfinity;
    private int previousPage = -1;
    private bool scrollToTop;

    // "1 of 4" and "You were on 1.18.0", composed when they change.
    private (int Page, int Count, int Language) counterKey = (-1, -1, -1);
    private string counter = string.Empty;

    // The widest counter ("9 of 9"), so the arrows keep their places from page to page.
    private string counterWidest = string.Empty;
    private (string? Version, int Language) wasOnKey;
    private string wasOnText = string.Empty;

    /// <param name="notes">The shipped release notes (<c>whats_new.json</c>).</param>
    /// <param name="runningVersion">The plugin's version.</param>
    /// <param name="pluginDir">The plugin's folder: the release pictures are under it (<see cref="ReleaseArt.Folder"/>).</param>
    public WhatsNewPopup(Configuration settings, IDalamudPluginInterface pluginInterface, IPluginLog log, ITextureProvider textures, ReleaseNotes notes, string runningVersion, string pluginDir)
        : base(Strings.WhatsNew.Title + Id, PopupFlags)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        Notes = notes ?? throw new ArgumentNullException(nameof(notes));
        RunningVersion = ChangelogSection.NormalizeVersion(runningVersion);
        History = Notes.History(RunningVersion);
        this.pluginDir = pluginDir ?? throw new ArgumentNullException(nameof(pluginDir));
        art = new ReleaseArtTexture(textures, log);
        RespectCloseHotkey = true;
        DisableFadeInFadeOut = true;
        AllowPinning = false;
        AllowClickthrough = false;
    }

    /// <summary>The shipped release notes.</summary>
    public ReleaseNotes Notes { get; }

    /// <summary>The running version, three parts ("1.22.0").</summary>
    public string RunningVersion { get; }

    /// <summary>What the player is doing now, for the quiet moment; set by the plugin. Null never shows the update's popup.</summary>
    public Func<WhatsNewMoment>? Moment { get; set; }

    /// <summary>The main window's place and size when it drew this frame or the last; the popup centres on it.</summary>
    public Func<(Vector2 Pos, Vector2 Size)?>? MainWindowRect { get; set; }

    /// <summary>"All releases": opens Settings › Advanced › What's new.</summary>
    public Action? OpenAllReleases { get; set; }

    /// <summary>Every release up to the running one, newest first: Settings' list.</summary>
    public IReadOnlyList<ReleaseNote> History { get; }

    /// <summary>
    /// Once at load: decides whether the update's popup is due (it then waits for the first quiet moment), or records
    /// the version silently (a fresh install, nothing to show, or the setting off).
    /// </summary>
    public void CheckAfterLoad()
    {
        var seen = settings.LastSeenVersion;
        var arrived = settings.HasPriorConfig || seen.Length > 0 ? Notes.Since(seen, RunningVersion) : [];
        switch (WhatsNew.Decide(seen, RunningVersion, arrived.Count > 0, settings.HasPriorConfig))
        {
            case WhatsNewDecision.Show when settings.ShowWhatsNewAfterUpdate:
                pending = [.. arrived];
                pendingWasOn = arrived.Count > 1 ? ChangelogSection.NormalizeVersion(seen) : null;
                break;
            case WhatsNewDecision.Show:
            case WhatsNewDecision.RecordSilently:
                MarkSeen();
                break;
        }
    }

    /// <summary>Opens the popup on <paramref name="release"/>, with ‹ › walking the whole history and no "You were on".</summary>
    public void OpenAt(ReleaseNote release)
    {
        ArgumentNullException.ThrowIfNull(release);
        var history = History;
        var index = 0;
        for (var i = 0; i < history.Count; i++)
        {
            if (history[i].Version == release.Version)
            {
                index = i;
                break;
            }
        }

        // The history opened before the update's popup showed counts as seeing it.
        if (pending is not null)
        {
            pending = null;
            recordOnClose = true;
        }

        Show([.. history], index, null);
    }

    public override void PreOpenCheck()
    {
        art.Tick();
        if (pending is null || IsOpen)
        {
            return;
        }

        if (!settings.ShowWhatsNewAfterUpdate)
        {
            pending = null;
            MarkSeen();
            return;
        }

        if (Moment?.Invoke() is { } moment && WhatsNew.IsQuietMoment(moment))
        {
            var due = pending;
            pending = null;
            recordOnClose = true;
            Show(due, 0, pendingWasOn);
        }
    }

    /// <summary>Never over a cutscene, group pose or a loading screen; the update's popup also steps aside in a fight.</summary>
    public override bool DrawConditions()
    {
        if (Moment?.Invoke() is not { } moment)
        {
            return true;
        }

        return !moment.InCutscene && !moment.GroupPose && !moment.Loading && !(recordOnClose && moment.InCombat);
    }

    private Theme.StyleScope nightChrome;
    private bool styled;

    public override void PreDraw()
    {
        nightChrome = Theme.PushNightWindow();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        styled = true;

        if (!placed)
        {
            // The measuring frame: drawn invisible at the screen's centre, then placed once its size is known.
            var viewport = ImGuiHelpers.MainViewport;
            ImGui.SetNextWindowPos(viewport.WorkPos + ((viewport.WorkSize - size) * 0.5f), ImGuiCond.Always);
        }
        else if (Rising(ImGui.GetTime(), out var rise))
        {
            ImGui.SetNextWindowPos(target + new Vector2(0f, rise), ImGuiCond.Always);
        }

        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
    }

    public override void PostDraw()
    {
        if (styled)
        {
            ImGui.PopStyleVar(3);
            styled = false;
        }

        nightChrome.Dispose();
        nightChrome = default;
    }

    public override void Draw()
    {
        if (pages.Length == 0)
        {
            IsOpen = false;
            return;
        }

        UiMetrics.ApplyFontScale();
        var now = ImGui.GetTime();
        var flair = Theme.Flair;
        RefreshArt(flair);
        RefreshSubtitles();

        // The size first: the tallest page's body at this Text size, capped to the screen.
        var scale = UiMetrics.Scale;
        var width = MathF.Round(UiMetrics.Px(WhatsNewLayout.Width));
        var pad = UiMetrics.Px(PadLogical);
        var textWidth = MathF.Max(1f, width - (2f * pad) - UiMetrics.Px(BulletIndentLogical));
        var tallest = 0f;
        for (var i = 0; i < pages.Length; i++)
        {
            tallest = MathF.Max(tallest, BodyHeight(i, flair, textWidth));
        }

        var fixedHeight = WhatsNewLayout.FixedHeight(flair, artBand, scale);
        var block = WhatsNewLayout.NotesBlock(tallest, fixedHeight, ImGuiHelpers.MainViewport.WorkSize.Y, scale);
        size = new Vector2(width, fixedHeight + block);

        var measuring = !placed;
        if (measuring)
        {
            Place();
        }

        var alpha = measuring ? 0f : Alpha(now);
        if (!double.IsNaN(leavingAt) && alpha <= 0f)
        {
            IsOpen = false;
        }

        var dl = ImGui.GetWindowDrawList();
        var start = dl.VtxBuffer.Size;
        var min = ImGui.GetWindowPos();
        var max = min + size;
        DrawSurface(dl, min, max, flair);
        var header = UiMetrics.Px(flair == Flair.Plain ? WhatsNewLayout.HeaderPlain : WhatsNewLayout.Header);
        var footer = UiMetrics.Px(flair == Flair.Plain ? WhatsNewLayout.FooterPlain : WhatsNewLayout.Footer);
        DrawHeader(dl, min, width, header, flair);
        var y = min.Y + header;
        var swap = PageFade(now);
        if (artBand)
        {
            var band = MathF.Round(UiMetrics.Px(WhatsNewLayout.ArtHeight));
            DrawArt(dl, new Vector2(min.X, y), width, band, flair, swap);
            y += band;
        }

        DrawBody(new Vector2(min.X, y), new Vector2(width, max.Y - footer - y), flair, textWidth, alpha, swap);
        DrawFooter(dl, new Vector2(min.X, max.Y - footer), width, footer, flair);
        if (alpha < 1f)
        {
            Chrome.FadeVertices(dl, start, alpha);
        }
    }

    public override void OnClose()
    {
        art.Release();
        placed = false;
        leavingAt = double.NaN;
        previousPage = -1;
        if (recordOnClose)
        {
            recordOnClose = false;
            MarkSeen();
        }
    }

    public void Dispose() => art.Dispose();

    // ------------------------------------------------------------------ pages

    private void Show(ReleaseNote[] shown, int index, string? before)
    {
        if (shown.Length == 0)
        {
            return;
        }

        pages = shown;
        page = Math.Clamp(index, 0, shown.Length - 1);
        wasOn = before is { Length: > 0 } ? before : null;
        lines = new string[shown.Length][];
        leads = new int[shown.Length][];
        for (var p = 0; p < shown.Length; p++)
        {
            var points = shown[p].Points;
            lines[p] = new string[points.Count];
            leads[p] = new int[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                lines[p][i] = points[i].Line;
                leads[p][i] = points[i].Lead.Length;
            }
        }

        subtitlesLanguage = -1;
        artKey = null;
        previousPage = -1;
        pageAt = double.NegativeInfinity;
        if (!IsOpen)
        {
            placed = false;
            leavingAt = double.NaN;
        }

        IsOpen = true;
        BringToFront();
    }

    private void GoTo(int index)
    {
        var next = Math.Clamp(index, 0, pages.Length - 1);
        if (next == page)
        {
            return;
        }

        previousPage = page;
        page = next;
        pageAt = ImGui.GetTime();
        scrollToTop = true;
    }

    private void MarkSeen()
    {
        if (RunningVersion.Length == 0 || settings.LastSeenVersion == RunningVersion)
        {
            return;
        }

        settings.LastSeenVersion = RunningVersion;
        try
        {
            settings.Save(pluginInterface);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not save the last seen version");
        }
    }

    private void RefreshSubtitles()
    {
        if (subtitlesLanguage == Loc.Version && subtitles.Length == pages.Length)
        {
            return;
        }

        subtitlesLanguage = Loc.Version;
        subtitles = new string[pages.Length];
        for (var i = 0; i < pages.Length; i++)
        {
            subtitles[i] = string.Format(CultureInfo.CurrentCulture, Strings.WhatsNew.VersionDateFormat, pages[i].Version, pages[i].LongDate);
        }
    }

    /// <summary>Finds each page's picture for the look (once per theme, level and page set) and whether the band shows.</summary>
    private void RefreshArt(Flair flair)
    {
        var theme = Themes.GlyphSeam.Appearance.Theme.Key;
        if (artKey is { } key && key.Theme == theme && key.Flair == flair && ReferenceEquals(key.Pages, pages))
        {
            return;
        }

        artKey = (theme, flair, pages);
        artPaths = new string?[pages.Length];
        Span<bool> found = pages.Length <= 64 ? stackalloc bool[pages.Length] : new bool[pages.Length];
        for (var i = 0; i < pages.Length; i++)
        {
            artPaths[i] = ReleaseArt.Find(pluginDir, pages[i].Version, flair, theme, File.Exists);
            found[i] = artPaths[i] is not null;
        }

        artBand = WhatsNewLayout.ArtBand(flair, found);
    }

    // ------------------------------------------------------------------ placement and motion

    /// <summary>Centred on the main window when it is open, else on the screen, and kept inside the screen.</summary>
    private void Place()
    {
        var viewport = ImGuiHelpers.MainViewport;
        var centre = MainWindowRect?.Invoke() is { } main ? main.Pos + (main.Size * 0.5f) : viewport.WorkPos + (viewport.WorkSize * 0.5f);
        var pos = centre - (size * 0.5f);
        var low = viewport.WorkPos;
        var high = viewport.WorkPos + viewport.WorkSize - size;
        target = new Vector2(MathF.Round(Math.Clamp(pos.X, low.X, MathF.Max(low.X, high.X))), MathF.Round(Math.Clamp(pos.Y, low.Y, MathF.Max(low.Y, high.Y))));
        placed = true;
        openedAt = ImGui.GetTime();
    }

    /// <summary>The open's rise: 4 px over <see cref="MotionTokens.Rise"/>, eased; none under Reduce motion or at Plain.</summary>
    private bool Rising(double now, out float offset)
    {
        offset = 0f;
        if (!Motion.Enabled)
        {
            return false;
        }

        var t = (float)((now - openedAt) / MotionTokens.Rise);
        if (t >= 1f)
        {
            return false;
        }

        offset = MathF.Round(UiMetrics.Px(MotionTokens.RiseLogical) * (1f - MotionMath.EaseOutCubic(Math.Clamp(t, 0f, 1f))));
        return true;
    }

    /// <summary>The whole popup's opacity: fading in over <see cref="MotionTokens.Rise"/>, out over <see cref="MotionTokens.Leave"/>.</summary>
    private float Alpha(double now)
    {
        if (!Motion.Enabled)
        {
            return double.IsNaN(leavingAt) ? 1f : 0f;
        }

        if (!double.IsNaN(leavingAt))
        {
            return Math.Clamp(1f - (float)((now - leavingAt) / MotionTokens.Leave), 0f, 1f);
        }

        return Math.Clamp((float)((now - openedAt) / MotionTokens.Rise), 0f, 1f);
    }

    /// <summary>How far a page change's cross-fade has run (1 when none is running).</summary>
    private float PageFade(double now) =>
        !Motion.Enabled || previousPage < 0 ? 1f : Math.Clamp((float)((now - pageAt) / MotionTokens.Select), 0f, 1f);

    /// <summary>Close, ×: fades out, then closes; at once without motion.</summary>
    private void Leave()
    {
        if (!Motion.Enabled)
        {
            IsOpen = false;
            return;
        }

        if (double.IsNaN(leavingAt))
        {
            leavingAt = ImGui.GetTime();
        }
    }

    // ------------------------------------------------------------------ drawing

    private static void DrawSurface(ImDrawListPtr dl, Vector2 min, Vector2 max, Flair flair)
    {
        var s = Theme.Surface;
        switch (flair)
        {
            case Flair.Full:
            {
                // The kit's frame on the palette's own sheet, opaque over the game, with a long shadow straight down.
                var reach = UiMetrics.Px(ShadowOffsetLogical + ShadowBlurLogical);
                dl.PushClipRect(min - new Vector2(reach), max + new Vector2(reach), false);
                var rounding = UiMetrics.Px(Theme.Spacing.CardRounding);
                Ornament.DropShadow(dl, min, max, rounding, UiMetrics.Px(ShadowOffsetLogical), UiMetrics.Px(ShadowBlurLogical), ShadowAlpha);
                dl.AddRectFilled(min, max, Theme.U32(s.Window with { W = 1f }), rounding);
                Chrome.CardSurface(dl, min, max);
                dl.PopClipRect();
                break;
            }

            case Flair.Quiet:
            {
                var rounding = UiMetrics.Px(QuietRoundingLogical);
                dl.AddRectFilled(min, max, Theme.U32(s.Window with { W = 1f }), rounding);
                dl.AddRectFilled(min, max, Theme.U32(Theme.Tones.Card with { W = 1f }), rounding);
                dl.AddRect(min, max, Theme.U32(Theme.Glyphs.HighContrast ? s.StrongLine : s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
                break;
            }

            default:
                dl.AddRectFilled(min, max, Theme.U32(s.Window with { W = 1f }));
                dl.AddRect(min, max, Theme.U32(s.Line), 0f, ImDrawFlags.None, UiMetrics.Hairline);
                break;
        }
    }

    private void DrawHeader(ImDrawListPtr dl, Vector2 min, float width, float height, Flair flair)
    {
        var s = Theme.Surface;
        var pad = UiMetrics.Px(PadLogical);
        var mid = min.Y + (height * 0.5f);
        var x = min.X + pad;
        if (flair == Flair.Plain)
        {
            dl.AddRectFilled(min, new Vector2(min.X + width, min.Y + height), Theme.U32(Theme.Tones.Band));
        }
        else
        {
            var radius = UiMetrics.Px(MoonLogical) * 0.5f;
            DrawMoon(dl, new Vector2(x + radius, mid), radius, flair);
            x += (2f * radius) + UiMetrics.Px(10f);
        }

        // The eyebrow: TrumpGothic in the lighter gilt at Full, semibold Secondary at Quiet, the band's text at Plain.
        var title = Strings.WhatsNew.Title;
        if (flair == Flair.Full)
        {
            var upper = SectionHeading.Label(title);
            using var eyebrow = Typography.Eyebrow(upper);
            var text = eyebrow.GameFace ? upper : title;
            var tracking = eyebrow.GameFace ? Typography.SectionTracking(in eyebrow) : 0f;
            var line = ImGui.GetTextLineHeight();
            Chrome.TrackedTextAt(dl, new Vector2(x, MathF.Round(mid - (line * 0.5f))), width, text, Theme.U32(Theme.OrnamentLight), tracking, Theme.DropShadow(0.55f));
        }
        else
        {
            var line = ImGui.GetTextLineHeight();
            ImGui.SetCursorScreenPos(new Vector2(x, MathF.Round(mid - (line * 0.5f))));
            Chrome.SemiboldText(title, flair == Flair.Quiet ? s.TextSecondary : s.Text);
        }

        // × at the right end, and "You were on 1.18.0" before it when several releases arrived.
        var close = UiMetrics.MinTarget;
        var closeX = min.X + width - pad - close;
        ImGui.SetCursorScreenPos(new Vector2(closeX, MathF.Round(mid - (close * 0.5f))));
        if (Chrome.IconButtonRound("##close", Chrome.Icon(FontAwesomeIcon.Times), Strings.WhatsNew.Close, enabled: double.IsNaN(leavingAt)))
        {
            Leave();
        }

        if (wasOn is { } before)
        {
            if (wasOnKey != (before, Loc.Version))
            {
                wasOnKey = (before, Loc.Version);
                wasOnText = string.Format(CultureInfo.CurrentCulture, Strings.WhatsNew.WasOnFormat, before);
            }

            using (Typography.Caption())
            {
                var size = ImGui.CalcTextSize(wasOnText);
                dl.AddText(new Vector2(MathF.Round(closeX - UiMetrics.Px(8f) - size.X), MathF.Round(mid - (size.Y * 0.5f))), Theme.U32(s.TextSecondary), wasOnText);
            }
        }
    }

    /// <summary>
    /// A small moon in the header (28 px, no particles): a round well with the kit's resting rim at Full (a hairline at
    /// Quiet), and a crescent lit on its upper-left limb, as every Tsukimichi moon is.
    /// </summary>
    private static void DrawMoon(ImDrawListPtr dl, Vector2 centre, float radius, Flair flair)
    {
        var s = Theme.Surface;
        dl.AddCircleFilled(centre, radius, Theme.U32(s.Deep with { W = 1f }), 32);
        if (flair == Flair.Full)
        {
            dl.AddCircle(centre, radius - (UiMetrics.Hairline * 0.5f), Theme.U32(Theme.Brass.Body), 32, MathF.Max(1f, UiMetrics.Px(1.5f)));
        }
        else
        {
            dl.AddCircle(centre, radius - (UiMetrics.Hairline * 0.5f), Theme.U32(s.Line), 32, UiMetrics.Hairline);
        }

        var disc = radius * 0.58f;
        dl.AddCircleFilled(centre, disc, Theme.WithAlpha(Theme.Scene.Moonlight, 0.18f), 32);
        dl.AddCircleFilled(centre, disc, Theme.U32(Theme.Scene.Moonlight), 32);
        dl.AddCircleFilled(centre + new Vector2(disc * 0.42f, disc * 0.30f), disc * 0.92f, Theme.U32(s.Deep with { W = 1f }), 32);
    }

    private void DrawArt(ImDrawListPtr dl, Vector2 top, float width, float height, Flair flair, float swap)
    {
        var s = Theme.Surface;
        var inset = MathF.Round(UiMetrics.Px(WhatsNewLayout.ArtInset));
        var min = new Vector2(top.X + inset, top.Y);
        var max = new Vector2(top.X + width - inset, top.Y + height);
        var rounding = UiMetrics.Px(ArtRoundingLogical);

        // The theme's flat sky while the picture loads, and on a page that has none.
        dl.AddRectFilled(min, max, Theme.U32(Theme.Scene.Zenith with { W = 1f }), rounding);
        art.Want(artPaths[page], ReleaseArt.TierFor(max.X - min.X));
        if (art.Current(Motion.Enabled ? MotionTokens.Select : 0f, out var fade) is { } picture)
        {
            var tint = Theme.WithAlpha(Vector4.One, fade * swap);
            Chrome.ImageCoverAt(dl, picture.Handle, min, max, new Vector2(picture.Width, picture.Height), tint, rounding);
        }

        if (flair == Flair.Full && !Theme.Glyphs.HighContrast)
        {
            Ornament.BrassBorder(dl, min, max, rounding, UiMetrics.Hairline);
        }
        else
        {
            dl.AddRect(min, max, Theme.U32(Theme.Glyphs.HighContrast ? s.StrongLine : s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        }
    }

    /// <summary>
    /// The notes block: a child of fixed height, so a page longer than the cap scrolls inside it and nothing below
    /// moves. A page change cross-fades the old page out and the new one in, in place.
    /// </summary>
    private void DrawBody(Vector2 pos, Vector2 blockSize, Flair flair, float textWidth, float alpha, float swap)
    {
        // The child starts at the text's left edge (a borderless child has no padding of its own) and reaches past the
        // text's right edge by the thin scrollbar, so a scrolling page never wraps differently from its measure.
        var pad = UiMetrics.Px(PadLogical);
        var bar = MathF.Max(4f, UiMetrics.Px(ScrollbarLogical));
        ImGui.SetCursorScreenPos(new Vector2(pos.X + pad, pos.Y));
        using var scrollbar = ImRaii.PushStyle(ImGuiStyleVar.ScrollbarSize, bar);
        using var child = ImRaii.Child("##whatsNewNotes", new Vector2(MathF.Max(1f, blockSize.X - (2f * pad) + bar + UiMetrics.Px(4f)), MathF.Max(1f, blockSize.Y)), false, ImGuiWindowFlags.NoBackground);
        if (!child)
        {
            return;
        }

        // A new page starts at its top.
        if (scrollToTop)
        {
            scrollToTop = false;
            ImGui.SetScrollY(0f);
        }

        var dl = ImGui.GetWindowDrawList();
        var start = dl.VtxBuffer.Size;
        var origin = ImGui.GetCursorScreenPos();
        if (swap < 1f && previousPage >= 0 && previousPage < pages.Length)
        {
            var old = dl.VtxBuffer.Size;
            DrawPage(previousPage, flair, textWidth);
            Chrome.FadeVertices(dl, old, 1f - swap);
            ImGui.SetCursorScreenPos(origin);
        }

        var fresh = dl.VtxBuffer.Size;
        DrawPage(page, flair, textWidth);
        if (swap < 1f)
        {
            Chrome.FadeVertices(dl, fresh, swap);
        }

        if (alpha < 1f)
        {
            Chrome.FadeVertices(dl, start, alpha);
        }
    }

    private void DrawPage(int index, Flair flair, float textWidth)
    {
        var s = Theme.Surface;
        var release = pages[index];
        ImGui.Dummy(new Vector2(1f, MathF.Max(0f, UiMetrics.Px(BodyTopLogical) - ImGui.GetStyle().ItemSpacing.Y)));
        var room = textWidth + UiMetrics.Px(BulletIndentLogical);
        var dl = ImGui.GetWindowDrawList();

        // The release's name in the Title face (semibold body × 1.15 at Plain), then its version and date.
        using (TitleFont(flair, release.Name))
        {
            var at = ImGui.GetCursorScreenPos();
            var line = ImGui.GetTextLineHeight();
            Chrome.EllipsisTextAt(dl, at, room, release.Name, Theme.U32(s.Text));
            if (flair == Flair.Plain)
            {
                Chrome.EllipsisTextAt(dl, at + new Vector2(MathF.Max(0.5f, UiMetrics.Px(0.5f)), 0f), room, release.Name, Theme.U32(s.Text));
            }

            ImGui.Dummy(new Vector2(1f, line));
        }

        using (Typography.Caption())
        {
            Chrome.EllipsisTextAt(dl, ImGui.GetCursorScreenPos(), room, subtitles[index], Theme.U32(s.TextSecondary));
            ImGui.Dummy(new Vector2(1f, ImGui.GetTextLineHeight()));
        }

        ImGui.Dummy(new Vector2(1f, MathF.Max(0f, UiMetrics.Px(TitleGapLogical) - ImGui.GetStyle().ItemSpacing.Y)));
        var indent = UiMetrics.Px(BulletIndentLogical);
        var text = Theme.U32(s.Text);
        for (var i = 0; i < lines[index].Length; i++)
        {
            if (i > 0)
            {
                ImGui.Dummy(new Vector2(1f, MathF.Max(0f, UiMetrics.Px(PointGapLogical) - ImGui.GetStyle().ItemSpacing.Y)));
            }

            var at = ImGui.GetCursorScreenPos();
            DrawBullet(dl, new Vector2(at.X + (indent * 0.35f), at.Y + (ImGui.GetTextLineHeight() * 0.5f)), flair);
            ImGui.SetCursorScreenPos(new Vector2(at.X + indent, at.Y));
            TextFlow.WrappedLead(lines[index][i], leads[index][i], textWidth, text);
        }
    }

    /// <summary>The point's mark, centred on its first line at any Text size: a 5 px dot in the kit's ink, Secondary at Quiet, a 4 px square at Plain.</summary>
    private static void DrawBullet(ImDrawListPtr dl, Vector2 centre, Flair flair)
    {
        switch (flair)
        {
            case Flair.Full:
                dl.AddCircleFilled(centre, UiMetrics.Px(DotLogical) * 0.5f, Theme.U32(Theme.Brass.Body), 12);
                break;
            case Flair.Quiet:
                dl.AddCircleFilled(centre, UiMetrics.Px(DotLogical) * 0.5f, Theme.U32(Theme.Surface.TextSecondary), 12);
                break;
            default:
                var half = MathF.Round(UiMetrics.Px(PlainDotLogical) * 0.5f);
                dl.AddRectFilled(centre - new Vector2(half), centre + new Vector2(half), Theme.U32(Theme.Surface.Text));
                break;
        }
    }

    /// <summary>The page's body height at this Text size: its top room, title, subtitle, gap and points.</summary>
    private float BodyHeight(int index, Flair flair, float textWidth)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var height = MathF.Max(UiMetrics.Px(BodyTopLogical), spacing);
        using (TitleFont(flair, pages[index].Name))
        {
            height += ImGui.GetTextLineHeight() + spacing;
        }

        using (Typography.Caption())
        {
            height += ImGui.GetTextLineHeight() + spacing;
        }

        height += MathF.Max(UiMetrics.Px(TitleGapLogical), spacing);
        for (var i = 0; i < lines[index].Length; i++)
        {
            if (i > 0)
            {
                height += MathF.Max(UiMetrics.Px(PointGapLogical), spacing);
            }

            height += TextFlow.Height(lines[index][i], textWidth) + spacing;
        }

        return height;
    }

    private static Typography.Scope TitleFont(Flair flair, string name) => flair == Flair.Plain ? Typography.Lead() : Typography.Title(name);

    private void DrawFooter(ImDrawListPtr dl, Vector2 min, float width, float height, Flair flair)
    {
        var s = Theme.Surface;
        var pad = UiMetrics.Px(PadLogical);
        var max = new Vector2(min.X + width, min.Y + height);
        if (flair == Flair.Plain)
        {
            dl.AddRectFilled(min, max, Theme.U32(Theme.Tones.Band));
        }
        else
        {
            dl.AddLine(new Vector2(min.X + pad, min.Y), new Vector2(max.X - pad, min.Y), Theme.U32(s.Line), UiMetrics.Hairline);
        }

        var mid = min.Y + (height * 0.5f);
        var enabled = double.IsNaN(leavingAt);
        using var disabled = ImRaii.Disabled(!enabled);
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, flair == Flair.Plain ? 0f : ImGui.GetFrameHeight() * 0.5f);

        // All releases: a quiet text link on the left.
        var link = Strings.WhatsNew.AllReleases;
        var linkSize = ImGui.CalcTextSize(link);
        var linkPos = new Vector2(min.X + pad, MathF.Round(mid - (linkSize.Y * 0.5f)));
        ImGui.SetCursorScreenPos(linkPos);
        if (ImGui.InvisibleButton("##allReleases", linkSize))
        {
            OpenAllReleases?.Invoke();
        }

        var hovered = ImGui.IsItemHovered();
        Chrome.FocusRing();
        dl.AddText(linkPos, Theme.U32(hovered ? s.Text : s.TextSecondary), link);
        dl.AddLine(new Vector2(linkPos.X, linkPos.Y + linkSize.Y), new Vector2(linkPos.X + linkSize.X, linkPos.Y + linkSize.Y), Theme.U32(hovered ? s.TextSecondary : s.Line), UiMetrics.Hairline);
        if (hovered)
        {
            UiMetrics.Tooltip(Strings.WhatsNew.AllReleasesTooltip);
        }

        // ‹ 1 of 4 › in the middle, only when there is more than one page; the arrows stay put, disabled at the ends.
        if (pages.Length > 1)
        {
            if (counterKey != (page, pages.Length, Loc.Version))
            {
                counterKey = (page, pages.Length, Loc.Version);
                counter = string.Format(CultureInfo.CurrentCulture, Strings.WhatsNew.PageFormat, page + 1, pages.Length);
                counterWidest = string.Format(CultureInfo.CurrentCulture, Strings.WhatsNew.PageFormat, pages.Length, pages.Length);
            }

            var arrow = UiMetrics.MinTarget;
            var widest = ImGui.CalcTextSize(counterWidest).X;
            var gap = UiMetrics.Px(8f);
            var total = (2f * arrow) + (2f * gap) + widest;
            var left = MathF.Round(min.X + ((width - total) * 0.5f));
            var top = MathF.Round(mid - (arrow * 0.5f));
            ImGui.SetCursorScreenPos(new Vector2(left, top));
            if (Chrome.IconButtonRound("##newer", Chrome.Icon(FontAwesomeIcon.ChevronLeft), Strings.WhatsNew.Newer, enabled: page > 0))
            {
                GoTo(page - 1);
            }

            var counterSize = ImGui.CalcTextSize(counter);
            dl.AddText(new Vector2(MathF.Round(left + arrow + gap + ((widest - counterSize.X) * 0.5f)), MathF.Round(mid - (counterSize.Y * 0.5f))), Theme.U32(s.Text), counter);
            ImGui.SetCursorScreenPos(new Vector2(left + arrow + gap + widest + gap, top));
            if (Chrome.IconButtonRound("##older", Chrome.Icon(FontAwesomeIcon.ChevronRight), Strings.WhatsNew.Older, enabled: page < pages.Length - 1))
            {
                GoTo(page + 1);
            }
        }

        // Close: a neutral pill on the right, never the gold of an act-now button (decision 3).
        var close = Strings.WhatsNew.Close;
        var closeWidth = ImGui.CalcTextSize(close).X + (ImGui.GetStyle().FramePadding.X * 2f) + UiMetrics.Px(8f);
        ImGui.SetCursorScreenPos(new Vector2(MathF.Round(max.X - pad - closeWidth), MathF.Round(mid - (ImGui.GetFrameHeight() * 0.5f))));
        if (ImGui.Button(close + "##closeFooter", new Vector2(closeWidth, 0f)))
        {
            Leave();
        }
    }
}
