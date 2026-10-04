using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
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
/// Settings; the game keeps its input), and its default is yes: "Download portraits" is the primary pill.
/// <para>
/// <b>Keys only by choice.</b> Dalamud hands every key to the game and to ImGui alike, so the offer opens without taking
/// the focus (<c>NoFocusOnAppearing</c>), and its keys wait until the player is engaged: has clicked inside it since it
/// last appeared and kept its focus (<see cref="PortraitPackWelcome.AnswerOf"/>, <see cref="PortraitPackWelcome.OwnsKeys"/>).
/// Then, after <see cref="PortraitPackWelcome.KeySettleSeconds"/>, Download portraits takes the keyboard focus, Enter
/// accepts, Esc and the close hotkey decline, and Enter and Esc are kept from the game (<see cref="ConsumeKeys"/>). Before
/// that, an Enter or Esc meant for the chat or the game's menu answers nothing; the mouse is the way to say yes or no.
/// "Not now" declines. Any answer is kept (<see cref="Configuration.PortraitPackOfferAnswered"/>), so it never shows again.
/// It steps aside, unanswered, outside the world, in a fight or a scene, and while something that goes first is on screen
/// (<see cref="PortraitPackWelcome.Visible"/>).
/// </para>
/// <para>
/// Download starts the existing download (<see cref="PortraitPackService.StartDownload"/>): nothing went online before
/// that click. The window then shows the download in place of the offer at the same size (progress with the Settings
/// row's bar, checking, installed, or the row's error words with Try again; <see cref="PortraitPackWelcome.ViewAfterDownload"/>);
/// closing it leaves the download running, and Settings › Look shows it too.
/// </para>
/// </summary>
public sealed class PortraitPackOfferWindow : Window
{
    private const string Id = "###TsukimichiPortraitPackOffer";
    private const float WidthLogical = 470f;

    /// <summary>How long a draw's key ownership stays good for <see cref="ConsumeKeys"/>, as the tour's.</summary>
    private const long KeysOwnedGraceMs = 250;

    private const ImGuiWindowFlags OfferFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDocking
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoFocusOnAppearing;

    private static readonly VirtualKey[] OfferKeys = [VirtualKey.RETURN, VirtualKey.ESCAPE];

    private readonly Configuration settings;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly PortraitPackService service;
    private readonly IPluginLog log;
    private readonly string pluginVersion;

    // The player chose Download here: the window shows the download in place of the offer.
    private bool downloading;

    // When the window appeared (or came back after it stepped aside), and the last frame it drew.
    private double appearedAt;
    private int drawnFrame = -10;

    // The player clicked inside the offer since it appeared and it kept the focus: only then do keys answer it.
    private bool engaged;

    // The keyboard focus goes to Download portraits once the offer owns the keys (engaged and settled).
    private bool focusPending;

    // Until when (Environment.TickCount64) Enter and Esc are kept from the game: refreshed by each draw that owns them.
    private long keysOwnedUntil;

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

        // The close hotkey (Esc) counts only once the player is engaged (Draw turns it on then).
        RespectCloseHotkey = false;
        DisableFadeInFadeOut = true;
        AllowPinning = false;
        AllowClickthrough = false;
    }

    /// <summary>What the player is doing now, for the quiet moment; set by the plugin. Null never shows the offer.</summary>
    public Func<WhatsNewMoment>? Moment { get; set; }

    /// <summary>Something goes first (What's new, the tour or its offer, Settings' own confirmation); set by the plugin.</summary>
    public Func<bool>? OtherFirst { get; set; }

    /// <summary>The game's key state, for <see cref="ConsumeKeys"/>; null leaves the game's keys alone.</summary>
    public IKeyState? KeyState { get; set; }

    /// <summary>
    /// <c>Framework.Update</c> handler: while the offer owns the keys (engaged and settled, decided on the last draw, and
    /// for a moment after an Enter that downloaded), clears Enter and Esc from the game's key state before the game reads
    /// them, so an Enter that accepts does not also open the chat box, nor an Esc the system menu.
    /// </summary>
    public void ConsumeKeys(IFramework framework)
    {
        if (Environment.TickCount64 > keysOwnedUntil || KeyState is not { } keys)
        {
            return;
        }

        foreach (var key in OfferKeys)
        {
            if (keys[key])
            {
                keys[key] = false;
            }
        }
    }

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
                // Opened without the focus (NoFocusOnAppearing, and never raised to the front with it): keys stay the
                // game's until the player clicks inside the offer.
                WindowName = Strings.PackConfirmTitle + Id;
                downloading = false;
                IsOpen = true;
                break;
        }
    }

    /// <summary>Steps aside, unanswered, outside the world, in a fight or a scene, and while something that goes first is on screen.</summary>
    public override bool DrawConditions() =>
        Moment?.Invoke() is not { } moment || PortraitPackWelcome.Visible(moment, OtherFirst?.Invoke() ?? false);

    public override void PreDraw()
    {
        nightChrome = Theme.PushNightWindow();

        // Back after stepping aside (or opened): the rise plays again, and the player must click inside it again.
        var frame = ImGui.GetFrameCount();
        if (drawnFrame != frame - 1)
        {
            appearedAt = ImGui.GetTime();
            Disengage();
        }

        // Rise (0.16 s, 4 px) with a fade, as Settings' confirmation; instant under Reduce motion.
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

        // Engaged: a click inside the offer since it appeared, and it kept the focus. A click lands as focus only at the
        // end of the frame, so the click's own frame counts before the focus does.
        var clickedInside = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
            && (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right));
        if (clickedInside)
        {
            engaged = true;
        }
        else if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
        {
            Disengage();
        }

        RespectCloseHotkey = engaged;

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

    /// <summary>
    /// Closed before Download (Not now, or Esc once engaged): the offer counts as answered. An unload or a game exit
    /// while it is open, unanswered, closes no window and records nothing, deliberately: the player never got to answer,
    /// so it shows again next session.
    /// </summary>
    public override void OnClose()
    {
        if (!downloading)
        {
            Record();
        }

        downloading = false;
        drawnFrame = -10;
        keysOwnedUntil = 0;
        Disengage();
    }

    /// <summary>Keys go back to the game until the player clicks inside the offer again.</summary>
    private void Disengage()
    {
        engaged = false;
        focusPending = true;
        RespectCloseHotkey = false;
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

        // Read before any button. Keys answer only once the player is engaged: Esc declines, and Enter accepts once the
        // offer has settled. Until then they are the game's.
        var sinceOpen = ImGui.GetTime() - appearedAt;
        var ownsKeys = PortraitPackWelcome.OwnsKeys(engaged, sinceOpen, asking: true);
        if (ownsKeys)
        {
            keysOwnedUntil = Environment.TickCount64 + KeysOwnedGraceMs;
        }

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

        // The primary pill from the first frame (yes by default); the keyboard focus only once the offer owns the keys,
        // so Space or a gamepad's confirm cannot press it before the player engaged.
        ImGui.SameLine(0f, UiMetrics.Px(14f));
        var download = Chrome.ActionPill("##packOfferDownload", FontAwesomeIcon.Download.ToIconString(), Strings.PackOfferDownload, PillTone.Primary, !service.Busy, null, PillLayout.Frame);
        if (focusPending && ownsKeys)
        {
            ImGui.SetItemDefaultFocus();
            ImGui.SetKeyboardFocusHere(-1);
            focusPending = false;
        }

        ImGui.SameLine();
        var notNow = ImGui.Button(Strings.PackOfferNotNow);

        switch (PortraitPackWelcome.AnswerOf(download, notNow, enter, escape, engaged, sinceOpen))
        {
            case PortraitPackWelcomeAnswer.Download:
                Record();
                if (enter)
                {
                    // An accepted Enter (on its own, or pressing the focused pill): kept from the game now and for a
                    // moment, so it does not also open the chat box.
                    ConsumeEnter();
                }

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

    /// <summary>Clears Enter from the game's key state now, and keeps doing so in <see cref="ConsumeKeys"/> for a moment.</summary>
    private void ConsumeEnter()
    {
        keysOwnedUntil = Environment.TickCount64 + KeysOwnedGraceMs;
        if (KeyState is { } keys && keys[VirtualKey.RETURN])
        {
            keys[VirtualKey.RETURN] = false;
        }
    }

    // ------------------------------------------------------------------ after Download

    /// <summary>
    /// The download in place of the offer, at the offer's size: progress with the Settings row's bar, checking, installed,
    /// or what went wrong. Close leaves a running download running; Cancel stops it. A removal since (in Settings) closes
    /// the window rather than reading as a failed download.
    /// </summary>
    private void DrawDownload(PortraitPackOffer offer, float wrap)
    {
        var s = Theme.Surface;
        var c = CultureInfo.CurrentCulture;
        var phase = service.Phase;
        var view = PortraitPackWelcome.ViewAfterDownload(
            phase == PortraitPackPhase.Downloading,
            phase == PortraitPackPhase.Installing,
            phase == PortraitPackPhase.Removing,
            service.LastFinishedUtc != default,
            service.LastWasRemoval,
            service.LastResult,
            service.Installed is not null);
        string title;
        string line;
        string? note = null;
        float? target = null;
        switch (view)
        {
            case PortraitPackOfferView.Downloading:
                var progress = service.Progress;
                title = Strings.PackOfferDownloading;
                line = service.SecondsLeft is { } left
                    ? string.Format(c, Strings.PackLineProgress, PortraitPackOffer.SizeText(progress.Received), PortraitPackOffer.SizeText(progress.Total), left.ToString(c))
                    : string.Format(c, Strings.PackLineProgressNoEta, PortraitPackOffer.SizeText(progress.Received), PortraitPackOffer.SizeText(progress.Total));
                target = progress.Fraction;
                note = Strings.PackOfferKeepsGoing;
                break;
            case PortraitPackOfferView.Checking:
                title = Strings.PackOfferChecking;
                line = string.Format(c, Strings.PackLineChecking, pluginVersion);
                target = 1f;
                note = Strings.PackOfferKeepsGoing;
                break;
            case PortraitPackOfferView.Installed:
                title = Strings.PackOfferInstalled;
                line = string.Format(c, Strings.PackOfferInstalledLine, (service.Installed?.Faces ?? 0).ToString("N0", c));
                break;
            case PortraitPackOfferView.Cancelled:
                title = Strings.PackStatusCancelled;
                line = Strings.PackLineCancelled;
                note = Strings.PackOfferLater;
                break;
            case PortraitPackOfferView.Failed:
                title = Strings.PackStatusFailed;
                line = ConfigWindow.PackFailureReason(service.LastResult, offer);
                note = Strings.PackOfferLater;
                break;
            default:
                IsOpen = false;
                return;
        }

        // The title, with the 1.17 Settings hint's dot beside it on an error.
        var dl = ImGui.GetWindowDrawList();
        var failed = view == PortraitPackOfferView.Failed;
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
        if (failed)
        {
            var retry = Chrome.ActionPill("##packOfferRetry", FontAwesomeIcon.Download.ToIconString(), Strings.PackTryAgain, PillTone.Primary, !service.Busy, null, PillLayout.Frame);
            if (retry)
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

        if (view == PortraitPackOfferView.Downloading)
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
