using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Tsukimichi.Config;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The first-run portrait pack offer (1.22.0; the owner's request: "after a user installs the app, give them a choice to
/// install the portrait pack, yes by default"). Once, to a player without the pack, at the first quiet moment and after
/// What's new (<see cref="PortraitPackWelcome.Next"/>). It reads as Settings' download confirmation (spec-1.20 F4): 470 px,
/// the title, what the pack does and that faces show in quest details, the facts (size, the source on Tsukimichi's GitHub
/// release, the Garland Tools credit, the fingerprint check), the promise in a sunk inset, the spoiler line, and where the
/// pack is later ("Settings › Look"). Unlike that confirmation it has no scrim (it opens over the game, not over
/// Settings; the game keeps its input) and its default is yes: "Download portraits" is the primary pill and takes the
/// keyboard focus, and Enter accepts (<see cref="PortraitPackWelcome.AnswerOf"/>, not before
/// <see cref="PortraitPackWelcome.KeySettleSeconds"/>). "Not now", Esc and closing it decline. Any answer is kept
/// (<see cref="Configuration.PortraitPackOfferAnswered"/>), so it never shows again.
/// <para>
/// Download starts the existing download (<see cref="PortraitPackService.StartDownload"/>): nothing went online before
/// that click. The window then shows the download in place of the offer at the same size (progress with the Settings
/// row's bar, checking, installed, or the row's error words with Try again); closing it leaves the download running, and
/// Settings › Look shows it too.
/// </para>
/// </summary>
public sealed class PortraitPackOfferWindow : Window
{
    private const string Id = "###TsukimichiPortraitPackOffer";
    private const float WidthLogical = 470f;

    private const ImGuiWindowFlags OfferFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDocking
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

    private readonly Configuration settings;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly PortraitPackService service;
    private readonly IPluginLog log;
    private readonly string pluginVersion;

    // The player chose Download here: the window shows the download in place of the offer.
    private bool downloading;

    // When the window appeared (or came back after a cutscene or a fight hid it), and the last frame it drew.
    private double appearedAt;
    private int drawnFrame = -10;

    // The keyboard focus goes to Download portraits once Enter may accept (PortraitPackWelcome.KeySettleSeconds).
    private bool focusPending;

    // The footer row's top in the offer (window-local), held by the download's states so the window keeps its size.
    private float footerY;
    private float bar;

    private Theme.StyleScope nightChrome;
    private bool styled;

    /// <param name="pluginVersion">The running version, for "Checked against the fingerprint built into Tsukimichi …".</param>
    public PortraitPackOfferWindow(Configuration settings, IDalamudPluginInterface pluginInterface, PortraitPackService service, IPluginLog log, string pluginVersion)
        : base(Strings.PackConfirmTitle + Id, OfferFlags)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.pluginVersion = pluginVersion ?? string.Empty;
        RespectCloseHotkey = true;
        DisableFadeInFadeOut = true;
        AllowPinning = false;
        AllowClickthrough = false;
    }

    /// <summary>What the player is doing now, for the quiet moment; set by the plugin. Null never shows the offer.</summary>
    public Func<WhatsNewMoment>? Moment { get; set; }

    /// <summary>Something goes first (What's new, the tour or its offer, Settings' own confirmation); set by the plugin.</summary>
    public Func<bool>? OtherFirst { get; set; }

    /// <summary>Every frame, open or not: while the offer is owed, shows it at the first quiet moment, or retires it.</summary>
    public override void PreOpenCheck()
    {
        if (IsOpen || settings.PortraitPackOfferAnswered || Moment?.Invoke() is not { } moment)
        {
            return;
        }

        var state = new PortraitPackWelcomeState(false, service.Loaded, service.State, service.Busy, OtherFirst?.Invoke() ?? false);
        switch (PortraitPackWelcome.Next(state, moment))
        {
            case PortraitPackWelcomeStep.Retire:
                log.Information("Portrait pack offer: not shown, the pack is already here or downloading");
                Record();
                break;
            case PortraitPackWelcomeStep.Show:
                WindowName = Strings.PackConfirmTitle + Id;
                downloading = false;
                IsOpen = true;
                BringToFront();
                break;
        }
    }

    /// <summary>Never over a cutscene, group pose, a loading screen or a fight; it comes back after them.</summary>
    public override bool DrawConditions() =>
        Moment?.Invoke() is not { } moment || (!moment.InCutscene && !moment.GroupPose && !moment.Loading && !moment.InCombat);

    public override void PreDraw()
    {
        nightChrome = Theme.PushNightWindow();

        // Rise (0.16 s, 4 px) with a fade, as Settings' confirmation; instant under Reduce motion.
        var frame = ImGui.GetFrameCount();
        if (drawnFrame != frame - 1)
        {
            appearedAt = ImGui.GetTime();
            focusPending = !downloading;
        }

        var t = UiMetrics.ReduceMotion ? 1f : (float)Math.Clamp((ImGui.GetTime() - appearedAt) / MotionTokens.Rise, 0d, 1d);
        var ease = 1f - ((1f - t) * (1f - t) * (1f - t));
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ease);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(UiMetrics.Px(18f), UiMetrics.Px(16f)));
        styled = true;

        var viewport = ImGuiHelpers.MainViewport;
        var centre = viewport.WorkPos + (viewport.WorkSize * 0.5f) + new Vector2(0f, UiMetrics.Px(MotionTokens.RiseLogical) * (1f - ease));
        ImGui.SetNextWindowPos(centre, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(UiMetrics.Px(WidthLogical), 0f), ImGuiCond.Always);
    }

    public override void PostDraw()
    {
        if (styled)
        {
            ImGui.PopStyleVar(2);
            styled = false;
        }

        nightChrome.Dispose();
        nightChrome = default;
    }

    public override void Draw()
    {
        drawnFrame = ImGui.GetFrameCount();
        if (service.Offer is not { } offer)
        {
            IsOpen = false;
            return;
        }

        UiMetrics.ApplyFontScale();
        var wrap = UiMetrics.Px(WidthLogical - 36f);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap);
        try
        {
            if (downloading)
            {
                DrawDownload(offer, wrap);
            }
            else
            {
                DrawOffer(offer, wrap);
            }
        }
        finally
        {
            ImGui.PopTextWrapPos();
            ImGui.SetWindowFontScale(1f);
        }
    }

    /// <summary>Closed by Not now, Esc or anything else before Download: the offer counts as answered.</summary>
    public override void OnClose()
    {
        if (!downloading)
        {
            Record();
        }

        downloading = false;
        drawnFrame = -10;
    }

    // ------------------------------------------------------------------ the offer

    private void DrawOffer(PortraitPackOffer offer, float wrap)
    {
        var s = Theme.Surface;
        var c = CultureInfo.CurrentCulture;
        using (Typography.Title(Strings.PackConfirmTitle))
        {
            ImGui.TextUnformatted(Strings.PackConfirmTitle);
        }

        ImGui.Spacing();
        ImGui.TextWrapped(string.Format(c, Strings.PackOfferWhat, offer.Givers.ToString("N0", c)));
        ImGui.Spacing();

        // The facts, as Settings' confirmation states them: Tertiary keys, Text values, Secondary asides.
        var keyWidth = UiMetrics.Px(78f);
        ConfigWindow.Fact(Strings.PackFactSize, PortraitPackOffer.SizeText(offer.Size), Strings.PackFactSizeAside, keyWidth, s);
        ConfigWindow.Fact(Strings.PackFactFrom, "github.com/xenofei/Tsukimichi", string.Format(c, Strings.PackFactFromAside, offer.ReleaseName), keyWidth, s);
        ConfigWindow.Fact(Strings.PackFactPhotos, Strings.PackFactPhotosValue, Strings.PackFactPhotosAside, keyWidth, s);
        ConfigWindow.Fact(Strings.PackFactChecked, string.Format(c, Strings.PackFactCheckedValue, pluginVersion), string.Empty, keyWidth, s);
        ImGui.Spacing();

        // The promise, in a sunk inset with a line outline.
        var dl = ImGui.GetWindowDrawList();
        var insetMin = ImGui.GetCursorScreenPos();
        var pad = UiMetrics.Px(10f);
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        ImGui.SetCursorScreenPos(insetMin + new Vector2(pad));
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap - (pad * 2f));
        using (Typography.Title(Strings.PackOfferPromiseLead))
        {
            ImGui.TextUnformatted(Strings.PackOfferPromiseLead);
        }

        ImGui.TextWrapped(Strings.PackOfferPromise);
        ImGui.PopTextWrapPos();
        var insetMax = new Vector2(insetMin.X + wrap, ImGui.GetCursorScreenPos().Y + pad - ImGui.GetStyle().ItemSpacing.Y);
        dl.ChannelsSetCurrent(0);
        dl.AddRectFilled(insetMin, insetMax, Theme.U32(s.Sunken), UiMetrics.Px(6f));
        dl.AddRect(insetMin, insetMax, Theme.U32(s.Line), UiMetrics.Px(6f), ImDrawFlags.None, UiMetrics.Hairline);
        dl.ChannelsMerge();
        ImGui.SetCursorScreenPos(new Vector2(insetMin.X, insetMax.Y + UiMetrics.Px(10f)));

        using (Theme.PushText(s.TextSecondary))
        {
            ImGui.TextWrapped(Strings.PackSpoilers);
        }

        using (Typography.Caption())
        using (Theme.PushText(s.TextTertiary))
        {
            ImGui.TextWrapped(Strings.PackOfferLater);
        }

        ImGui.Spacing();
        footerY = ImGui.GetCursorPosY();

        // Read before any button: Esc declines, Enter accepts once the offer has settled, and only on this window.
        var sinceOpen = ImGui.GetTime() - appearedAt;
        var focused = ImGui.IsWindowFocused();
        var enter = ImGui.IsKeyPressed(ImGuiKey.Enter, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter, false);
        var escape = ImGui.IsKeyPressed(ImGuiKey.Escape, false);

        // "What Tsukimichi sends": the whole statement in the browser.
        using (Theme.PushText(s.TextSecondary))
        {
            ImGui.TextUnformatted(Strings.PackWhatItSends);
        }

        var linkMin = ImGui.GetItemRectMin();
        var linkMax = ImGui.GetItemRectMax();
        dl.AddLine(new Vector2(linkMin.X, linkMax.Y), linkMax, Theme.U32(s.TextSecondary), UiMetrics.Hairline);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (ImGui.IsItemClicked())
        {
            Util.OpenLink(ConfigWindow.PrivacyStatementUrl);
        }

        ImGui.SameLine(0f, UiMetrics.Px(14f));
        var download = Chrome.ActionPill("##packOfferDownload", FontAwesomeIcon.Download.ToIconString(), Strings.PackOfferDownload, PillTone.Primary, !service.Busy, null, PillLayout.Frame);
        if (focusPending && sinceOpen >= PortraitPackWelcome.KeySettleSeconds)
        {
            // Yes by default: the keyboard focus rests on Download portraits (not before Enter may accept it).
            ImGui.SetItemDefaultFocus();
            ImGui.SetKeyboardFocusHere(-1);
            focusPending = false;
        }

        ImGui.SameLine();
        var notNow = ImGui.Button(Strings.PackOfferNotNow);

        switch (PortraitPackWelcome.AnswerOf(download, notNow, enter, escape, focused, sinceOpen))
        {
            case PortraitPackWelcomeAnswer.Download:
                Record();

                // The other place a download starts from the player's click: Download portraits (or Enter on it).
                if (service.StartDownload())
                {
                    log.Information("Portrait pack offer: the player chose Download");
                    downloading = true;
                    bar = 0f;
                }
                else
                {
                    IsOpen = false;
                }

                break;
            case PortraitPackWelcomeAnswer.NotNow:
                Record();
                IsOpen = false;
                break;
        }
    }

    // ------------------------------------------------------------------ after Download

    /// <summary>
    /// The download in place of the offer, at the offer's size: progress with the Settings row's bar, checking, installed,
    /// or what went wrong. Close leaves a running download running; Cancel stops it.
    /// </summary>
    private void DrawDownload(PortraitPackOffer offer, float wrap)
    {
        var s = Theme.Surface;
        var c = CultureInfo.CurrentCulture;
        var phase = service.Phase;
        string title;
        string line;
        string? note = null;
        float? target = null;
        var failed = false;
        var retry = false;
        switch (phase)
        {
            case PortraitPackPhase.Downloading:
                var progress = service.Progress;
                title = Strings.PackOfferDownloading;
                line = service.SecondsLeft is { } left
                    ? string.Format(c, Strings.PackLineProgress, PortraitPackOffer.SizeText(progress.Received), PortraitPackOffer.SizeText(progress.Total), left.ToString(c))
                    : string.Format(c, Strings.PackLineProgressNoEta, PortraitPackOffer.SizeText(progress.Received), PortraitPackOffer.SizeText(progress.Total));
                target = progress.Fraction;
                note = Strings.PackOfferKeepsGoing;
                break;
            case PortraitPackPhase.Installing:
                title = Strings.PackOfferChecking;
                line = string.Format(c, Strings.PackLineChecking, pluginVersion);
                target = 1f;
                note = Strings.PackOfferKeepsGoing;
                break;
            default:
                if (service.LastResult == PortraitPackFailure.None && service.Installed is { } pack)
                {
                    title = Strings.PackOfferInstalled;
                    line = string.Format(c, Strings.PackOfferInstalledLine, pack.Faces.ToString("N0", c));
                }
                else if (service.LastResult == PortraitPackFailure.Cancelled)
                {
                    title = Strings.PackStatusCancelled;
                    line = Strings.PackLineCancelled;
                    note = Strings.PackOfferLater;
                }
                else
                {
                    title = Strings.PackStatusFailed;
                    line = ConfigWindow.PackFailureReason(service.LastResult, offer);
                    note = Strings.PackOfferLater;
                    failed = true;
                    retry = true;
                }

                break;
        }

        // The title, with the 1.17 Settings hint's dot beside it on an error.
        var dl = ImGui.GetWindowDrawList();
        if (failed)
        {
            var dot = UiMetrics.Px(6f);
            var at = ImGui.GetCursorScreenPos();
            using (Typography.Title(title))
            {
                var height = ImGui.GetTextLineHeight();
                dl.AddCircleFilled(new Vector2(at.X + (dot * 0.5f), at.Y + (height * 0.5f)), dot * 0.5f, Theme.U32(ConfigWindow.PackHintDot));
                ImGui.SetCursorScreenPos(at + new Vector2(dot + UiMetrics.Px(6f), 0f));
                ImGui.TextUnformatted(title);
            }
        }
        else
        {
            using (Typography.Title(title))
            {
                ImGui.TextUnformatted(title);
            }
        }

        ImGui.Spacing();
        using (Theme.PushText(s.TextSecondary))
        {
            ImGui.TextWrapped(line);
        }

        // The bar's slot is kept in every state, so the words below it never move.
        var barHeight = UiMetrics.Px(4f);
        var barMin = ImGui.GetCursorScreenPos() + new Vector2(0f, UiMetrics.Px(4f));
        if (target is { } goal)
        {
            // Eases to each update over 0.12 s, as the Settings row's; steps under Reduce motion.
            bar = UiMetrics.ReduceMotion ? goal : bar + ((goal - bar) * MathF.Min(1f, ImGui.GetIO().DeltaTime / 0.12f));
            var barWidth = MathF.Min(wrap, UiMetrics.Px(348f));
            dl.AddRectFilled(barMin, barMin + new Vector2(barWidth, barHeight), Theme.U32(s.Line), barHeight * 0.5f);
            dl.AddRectFilled(barMin, barMin + new Vector2(barWidth * Math.Clamp(bar, 0f, 1f), barHeight), Theme.U32(s.TextSecondary), barHeight * 0.5f);
        }

        ImGui.Dummy(new Vector2(wrap, barHeight + UiMetrics.Px(8f)));
        if (note is not null)
        {
            using (Typography.Caption())
            using (Theme.PushText(s.TextTertiary))
            {
                ImGui.TextWrapped(note);
            }
        }

        // The buttons on the offer's footer row, so the window keeps its size.
        ImGui.SetCursorPosY(MathF.Max(ImGui.GetCursorPosY(), footerY));
        if (retry)
        {
            if (Chrome.ActionPill("##packOfferRetry", FontAwesomeIcon.Download.ToIconString(), Strings.PackTryAgain, PillTone.Primary, !service.Busy, null, PillLayout.Frame))
            {
                // Try again: the player clicks it, as they chose Download before.
                service.StartDownload();
            }

            ImGui.SameLine();
        }

        if (ImGui.Button(Strings.PackOfferClose))
        {
            IsOpen = false;
        }

        if (phase == PortraitPackPhase.Downloading)
        {
            ImGui.SameLine();
            if (ImGui.Button(Strings.PackCancel))
            {
                service.Cancel();
            }
        }
    }

    /// <summary>Keeps the answer: the offer never shows again.</summary>
    private void Record()
    {
        if (settings.PortraitPackOfferAnswered)
        {
            return;
        }

        settings.PortraitPackOfferAnswered = true;
        try
        {
            settings.Save(pluginInterface);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not save the portrait pack offer's answer");
        }
    }
}
