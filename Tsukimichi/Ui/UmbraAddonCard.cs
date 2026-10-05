using System;
using System.Collections.Generic;
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
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Umbra;
using Tsukimichi.Game;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// "Add Tsukimichi to your Umbra bar?" (the owner's request: set up the Umbra add-on for the player, as far as
/// possible). Once, at the first quiet moment, to a player who runs Umbra without Tsukimichi for Umbra, after What's new
/// and the tour (<see cref="UmbraAddonSetup.Next"/>); it steps aside in a fight, a duty, a
/// cutscene, Group Pose, a loading screen, outside the world, and while anything that goes first is on screen.
/// <list type="bullet">
/// <item><b>Add to Umbra</b> changes nothing: it opens the confirmation, which looks at Umbra first and lists only what
/// will change (custom plugins turned on, with Umbra's own warning in plain words and the open-source link; the
/// repository added; a restart of Umbra's toolbar; the widget placed). <b>Agree and add</b> is the one button that
/// changes Umbra (<see cref="UmbraAddonSetupService.AddToUmbra"/>), and the player's click on it is their agreement.</item>
/// <item><b>Show me how</b>: the three manual steps, Copy repository link and Open Umbra's settings.</item>
/// <item><b>Not now</b>: never asks again; Settings › About › Umbra keeps the same actions.</item>
/// </list>
/// Progress and the outcome show in place at one card height: every view is measured each frame and the body takes the
/// tallest, so the buttons never move. The outcome is confirmed by the add-on's IPC hello; a failure gives the reason in
/// plain words and the manual steps. Mouse only: no key answers it. Rise and the current step's pulse are off under
/// Reduce motion and at Decoration Plain, where the warning's inset is an outline only. English only.
/// </summary>
public sealed class UmbraAddonCard : Window
{
    /// <summary>The add-on's repository page, opened in the browser (never fetched by Tsukimichi).</summary>
    public const string RepositoryPage = "https://github.com/" + UmbraAddon.Repository;

    private const string Id = "###TsukimichiUmbraAddon";
    private const float WidthLogical = 460f;
    private const float PulseSeconds = 1.6f;

    private const ImGuiWindowFlags CardFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDocking
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoFocusOnAppearing;

    private static readonly LocText RepositoryLine = new(static () => string.Format(CultureInfo.CurrentCulture, Strings.UmbraSetupChangeRepositoryFormat, UmbraAddon.Repository));
    private static readonly LocText HowStep2 = new(static () => string.Format(CultureInfo.CurrentCulture, Strings.UmbraHowStep2Format, UmbraAddon.Repository));
    private static readonly UmbraSetupStep[] AllSteps = [UmbraSetupStep.TurnOnCustomPlugins, UmbraSetupStep.AddRepository, UmbraSetupStep.RestartUmbra, UmbraSetupStep.PlaceWidget];
    // The button labels with their ids, made once per language (no string is built per frame).
    private static readonly LocText CopyLabel = new(static () => Strings.UmbraSetupCopyLink + "##umbraCardCopy");
    private static readonly LocText OpenLabel = new(static () => Strings.UmbraSetupOpenUmbraSettings + "##umbraCardOpen");
    private static readonly LocText HowLabel = new(static () => Strings.UmbraSetupShowHow + "##umbraCardHow");
    private static readonly LocText NotNowLabel = new(static () => Strings.UmbraSetupNotNow + "##umbraCardNotNow");
    private static readonly LocText BackLabel = new(static () => Strings.UmbraSetupBack + "##umbraCardBack");
    private static readonly LocText CloseLabel = new(static () => Strings.UmbraSetupClose + "##umbraCardClose");
    private static readonly Page[] AllPages = [Page.Offer, Page.How, Page.Confirm, Page.Progress, Page.Result];
    private static readonly UmbraFailure[] AllFailures = (UmbraFailure[])Enum.GetValues(typeof(UmbraFailure));

    private readonly Configuration settings;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly UmbraAddonSetupService setup;
    private readonly UmbraProbe umbra;
    private readonly IPluginLog log;

    private readonly List<Line> lines = [];
    private readonly List<Line> measured = [];

    private Page page;
    private Page howReturn;
    private bool openFailed;
    private (string? Version, int Language) addedKey = (null, -1);
    private string addedLine = string.Empty;

    // When the card appeared (or came back after it stepped aside), and the last frame it drew.
    private double appearedAt;
    private int drawnFrame = -10;

    private Theme.StyleScope nightChrome;
    private bool styled;

    public UmbraAddonCard(Configuration settings, IDalamudPluginInterface pluginInterface, UmbraAddonSetupService setup, UmbraProbe umbra, IPluginLog log)
        : base(Strings.UmbraSetupTitle + Id, CardFlags)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.setup = setup ?? throw new ArgumentNullException(nameof(setup));
        this.umbra = umbra ?? throw new ArgumentNullException(nameof(umbra));
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        // Mouse only: Esc never answers it.
        RespectCloseHotkey = false;
        DisableFadeInFadeOut = true;
        AllowPinning = false;
        AllowClickthrough = false;
    }

    private enum Page : byte
    {
        Offer,
        How,
        Confirm,
        Progress,
        Result,
    }

    private enum Kind : byte
    {
        Title,
        Body,
        Secondary,
        Caption,
        Inset,
        Link,
        Step,
        Actions,
    }

    private enum StepState : byte
    {
        None,
        Done,
        Current,
        Pending,
    }

    /// <summary>What the player is doing now, for the quiet moment; set by the plugin. Null never shows the card.</summary>
    public Func<WhatsNewMoment>? Moment { get; set; }

    /// <summary>Something goes first (What's new, or the tour and its offer); set by the plugin.</summary>
    public Func<bool>? OtherFirst { get; set; }

    /// <summary>Settings › About › Umbra's "Add to Umbra…": the card opens on its confirmation, whether or not it was answered.</summary>
    public void ShowConfirm()
    {
        // An add that runs shows its progress; otherwise (nothing runs, or a Remove runs) the confirmation, whose
        // Agree and add stays off while anything runs.
        page = setup.View.Activity is UmbraSetupActivity.Adding or UmbraSetupActivity.PuttingBack or UmbraSetupActivity.Waiting ? Page.Progress : Page.Confirm;
        howReturn = Page.Confirm;
        if (page == Page.Confirm)
        {
            setup.RefreshPreview();
        }

        WindowName = Strings.UmbraSetupTitle + Id;
        IsOpen = true;

        // Asked for from Settings: in front of the Settings window, which would otherwise cover it.
        BringToFront();
    }

    /// <summary>Every frame, open or not: while the card is owed, shows it at the first quiet moment, or retires it.</summary>
    public override void PreOpenCheck()
    {
        if (IsOpen || settings.UmbraSetupOfferAnswered || Moment?.Invoke() is not { } moment)
        {
            return;
        }

        var state = new UmbraOfferState(
            false,
            umbra.Loaded,
            umbra.Read is not null,
            umbra.AddonVersion is not null,
            umbra.AddonPresent,
            setup.Busy,
            OtherFirst?.Invoke() ?? false);
        switch (UmbraAddonSetup.Next(state, moment))
        {
            case UmbraOfferStep.Retire:
                log.Information("Umbra add-on card: not shown, Tsukimichi for Umbra already runs");
                Record();
                break;
            case UmbraOfferStep.Show:
                page = Page.Offer;
                howReturn = Page.Offer;
                WindowName = Strings.UmbraSetupTitle + Id;
                IsOpen = true;
                break;
        }
    }

    /// <summary>Steps aside, open, outside the world, in a fight, a duty or a scene, and while something that goes first is on screen.</summary>
    public override bool DrawConditions() =>
        Moment?.Invoke() is not { } moment || UmbraAddonSetup.Visible(moment, OtherFirst?.Invoke() ?? false);

    public override void PreDraw()
    {
        nightChrome = Theme.PushNightWindow();

        // Back after stepping aside (or opened): the rise plays again.
        var frame = ImGui.GetFrameCount();
        if (drawnFrame != frame - 1)
        {
            appearedAt = ImGui.GetTime();
        }

        // Rise (4 px) with a fade; instant under Reduce motion and at Plain.
        var still = UiMetrics.ReduceMotion || Theme.Flair == Flair.Plain;
        var t = still ? 1f : (float)Math.Clamp((ImGui.GetTime() - appearedAt) / MotionTokens.Rise, 0d, 1d);
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
        var state = setup.View;

        // Progress never outlives the run: its outcome replaces it, or, with none (a Remove ran instead), the confirmation.
        if (page == Page.Progress && state.Activity == UmbraSetupActivity.Idle)
        {
            if (state.Outcome is not null)
            {
                page = Page.Result;
            }
            else
            {
                page = Page.Confirm;
                setup.RefreshPreview();
            }
        }

        UiMetrics.ApplyFontScale();
        var wrap = UiMetrics.Px(WidthLogical - 36f);
        try
        {
            // One height for every page: the tallest is measured, so progress and the outcome never move the buttons.
            var body = 0f;
            foreach (var p in AllPages)
            {
                body = MathF.Max(body, Tallest(p, state, wrap));
            }

            var top = ImGui.GetCursorPosY();
            Build(page, state, lines, tallest: false);
            DrawLines(lines, wrap);
            ImGui.SetCursorPosY(top + body + ImGui.GetStyle().ItemSpacing.Y);
            DrawFooter(state);
        }
        finally
        {
            ImGui.SetWindowFontScale(1f);
        }
    }

    /// <summary>Closed by the player from any page: the card counts as answered. An unload records nothing.</summary>
    public override void OnClose()
    {
        Record();
        drawnFrame = -10;
        openFailed = false;
    }

    // ------------------------------------------------------------------ pages

    /// <summary>The page's lines; <paramref name="tallest"/> builds its longest form (every change, the longest reason) for the height.</summary>
    private void Build(Page p, in UmbraSetupView state, List<Line> into, bool tallest, UmbraFailure reason = UmbraFailure.Unexpected)
    {
        into.Clear();
        switch (p)
        {
            case Page.Offer:
                into.Add(new Line(Strings.UmbraSetupTitle, Kind.Title));
                into.Add(new Line(Strings.UmbraSetupLead, Kind.Body));
                into.Add(new Line(Strings.UmbraSetupLater, Kind.Caption));
                break;
            case Page.How:
                into.Add(new Line(Strings.UmbraSetupShowHow, Kind.Title));
                AddManual(into);
                if (tallest || openFailed)
                {
                    into.Add(new Line(Strings.UmbraSetupOpenUmbraFailed, Kind.Caption));
                }

                break;
            case Page.Confirm:
                BuildConfirm(into, tallest);
                break;
            case Page.Progress:
                BuildProgress(state, into, tallest);
                break;
            case Page.Result:
                BuildResult(state, into, tallest, reason);
                break;
        }
    }

    private void BuildConfirm(List<Line> into, bool tallest)
    {
        into.Add(new Line(Strings.UmbraSetupConfirmTitle, Kind.Title));
        var preview = setup.Preview;
        if (!tallest && !preview.Ready)
        {
            into.Add(new Line(Strings.UmbraSetupChecking, Kind.Secondary));
            return;
        }

        if (!tallest && preview.Failure != UmbraFailure.None)
        {
            // Nothing can change from here: the reason, and the way by hand.
            into.Add(new Line(Strings.UmbraSetupReason(preview.Failure), Kind.Body));
            into.Add(new Line(Strings.UmbraSetupByHand, Kind.Secondary));
            return;
        }

        IReadOnlyList<UmbraSetupStep> plan = tallest ? AllSteps : preview.Plan;
        if (plan.Count == 0)
        {
            into.Add(new Line(Strings.UmbraSetupNothingToChange, Kind.Body));
            return;
        }

        foreach (var step in plan)
        {
            switch (step)
            {
                case UmbraSetupStep.TurnOnCustomPlugins:
                    into.Add(new Line(Strings.UmbraSetupChangeCustomPlugins, Kind.Body));
                    into.Add(new Line(Strings.UmbraSetupWarning, Kind.Inset));
                    break;
                case UmbraSetupStep.AddRepository:
                    into.Add(new Line(RepositoryLine.Value, Kind.Body));
                    break;
                case UmbraSetupStep.RestartUmbra:
                    into.Add(new Line(Strings.UmbraSetupChangeRestart, Kind.Body));
                    break;
                case UmbraSetupStep.PlaceWidget:
                    into.Add(new Line(Strings.UmbraSetupChangeWidget, Kind.Body));
                    break;
            }
        }

        into.Add(new Line(RepositoryPage, Kind.Link));
        into.Add(new Line(Strings.UmbraSetupAgreement, Kind.Caption));
    }

    private static void BuildProgress(in UmbraSetupView state, List<Line> into, bool tallest)
    {
        var puttingBack = state.Activity == UmbraSetupActivity.PuttingBack;
        into.Add(new Line(puttingBack ? Strings.UmbraSetupPuttingBack : Strings.UmbraSetupProgressTitle, Kind.Title));
        IReadOnlyList<UmbraSetupStep> plan = tallest ? AllSteps : state.Plan;
        var current = state.Step is { } step ? IndexOf(plan, step) : -1;
        var waiting = state.Activity == UmbraSetupActivity.Waiting;
        for (var i = 0; i < plan.Count; i++)
        {
            var done = waiting || (current >= 0 && i < current);
            var now = !waiting && !puttingBack && i == current;
            into.Add(new Line(StepText(plan[i]), Kind.Step, done ? StepState.Done : now ? StepState.Current : StepState.Pending));
        }

        into.Add(new Line(Strings.UmbraSetupStepHello, Kind.Step, waiting ? StepState.Current : StepState.Pending));
        into.Add(new Line(Strings.UmbraSetupKeepsGoing, Kind.Caption));
    }

    private void BuildResult(in UmbraSetupView state, List<Line> into, bool tallest, UmbraFailure reason)
    {
        var outcome = tallest ? UmbraSetupOutcome.FailedPartly : state.Outcome ?? UmbraSetupOutcome.Failed;
        switch (outcome)
        {
            case UmbraSetupOutcome.Added:
                into.Add(new Line(Strings.UmbraSetupAddedTitle, Kind.Title));
                into.Add(new Line(AddedLine(umbra.AddonVersion), Kind.Body));
                return;
            case UmbraSetupOutcome.NotConfirmed:
                into.Add(new Line(Strings.UmbraSetupNotConfirmedTitle, Kind.Title));
                into.Add(new Line(Strings.UmbraSetupNotConfirmedLine, Kind.Secondary));
                break;
            default:
                into.Add(new Line(Strings.UmbraSetupFailedTitle, Kind.Title));
                into.Add(new Line(Strings.UmbraSetupReason(tallest ? reason : state.Failure), Kind.Body));
                into.Add(new Line(
                    outcome == UmbraSetupOutcome.FailedPartly ? Strings.UmbraSetupNotPutBack
                    : !tallest && state.KeptCustomPluginsOn ? Strings.UmbraSetupPutBackKeptOn
                    : Strings.UmbraSetupPutBack,
                    Kind.Secondary));
                break;
        }

        into.Add(new Line(Strings.UmbraSetupByHand, Kind.Secondary));
        AddManual(into);
    }

    /// <summary>The three steps by hand, and Copy repository link and Open Umbra's settings.</summary>
    private static void AddManual(List<Line> into)
    {
        into.Add(new Line(Strings.UmbraHowStep1, Kind.Caption));
        into.Add(new Line(HowStep2.Value, Kind.Caption));
        into.Add(new Line(Strings.UmbraHowStep3, Kind.Caption));
        into.Add(new Line(string.Empty, Kind.Actions));
    }

    private static int IndexOf(IReadOnlyList<UmbraSetupStep> plan, UmbraSetupStep step)
    {
        for (var i = 0; i < plan.Count; i++)
        {
            if (plan[i] == step)
            {
                return i;
            }
        }

        return -1;
    }

    private static string StepText(UmbraSetupStep step) => step switch
    {
        UmbraSetupStep.TurnOnCustomPlugins => Strings.UmbraSetupStepCustomPlugins,
        UmbraSetupStep.AddRepository => Strings.UmbraSetupStepRepository,
        UmbraSetupStep.RestartUmbra => Strings.UmbraSetupStepRestart,
        _ => Strings.UmbraSetupStepWidget,
    };

    /// <summary>"Tsukimichi for Umbra 1.0.1 is running…", rebuilt when the version or the language changes.</summary>
    private string AddedLine(string? version)
    {
        var key = (version, Loc.Version);
        if (key != addedKey || addedLine.Length == 0)
        {
            addedKey = key;
            addedLine = string.Format(CultureInfo.CurrentCulture, Strings.UmbraSetupAddedFormat, version ?? "?");
        }

        return addedLine;
    }

    // ------------------------------------------------------------------ measuring and drawing

    /// <summary>The body height of <paramref name="p"/> in its longest form (for a result, its longest reason).</summary>
    private float Tallest(Page p, in UmbraSetupView state, float wrap)
    {
        if (p != Page.Result)
        {
            Build(p, state, measured, tallest: true);
            return Measure(measured, wrap);
        }

        var most = 0f;
        foreach (var reason in AllFailures)
        {
            Build(p, state, measured, tallest: true, reason);
            most = MathF.Max(most, Measure(measured, wrap));
        }

        return most;
    }

    private static float Measure(List<Line> page, float wrap)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var total = 0f;
        foreach (var line in page)
        {
            total += Height(line, wrap) + spacing;
        }

        return MathF.Max(0f, total - spacing);
    }

    private static float Height(in Line line, float wrap)
    {
        switch (line.Kind)
        {
            case Kind.Title:
                using (Typography.Title(line.Text))
                {
                    return TextFlow.Height(line.Text, wrap);
                }

            case Kind.Caption:
                using (Typography.Caption())
                {
                    return TextFlow.Height(line.Text, wrap);
                }

            case Kind.Inset:
                return TextFlow.Height(line.Text, wrap - (InsetPad * 2f)) + (InsetPad * 2f);
            case Kind.Link:
                return ImGui.GetTextLineHeight();
            case Kind.Step:
                return TextFlow.Height(line.Text, wrap - StepIndent);
            case Kind.Actions:
                return ImGui.GetFrameHeight();
            default:
                return TextFlow.Height(line.Text, wrap);
        }
    }

    private static float InsetPad => UiMetrics.Px(10f);

    private static float StepIndent => UiMetrics.Px(20f);

    private void DrawLines(List<Line> page, float wrap)
    {
        var s = Theme.Surface;
        foreach (var line in page)
        {
            switch (line.Kind)
            {
                case Kind.Title:
                    using (Typography.Title(line.Text))
                    {
                        Text(line.Text, wrap, s.Text);
                    }

                    break;
                case Kind.Body:
                    Text(line.Text, wrap, s.Text);
                    break;
                case Kind.Secondary:
                    Text(line.Text, wrap, s.TextSecondary);
                    break;
                case Kind.Caption:
                    using (Typography.Caption())
                    {
                        Text(line.Text, wrap, s.TextSecondary);
                    }

                    break;
                case Kind.Inset:
                    DrawInset(line.Text, wrap);
                    break;
                case Kind.Link:
                    DrawLink(line.Text);
                    break;
                case Kind.Step:
                    DrawStep(line, wrap);
                    break;
                case Kind.Actions:
                    DrawManualActions();
                    break;
            }
        }
    }

    /// <summary>Wrapped text at the cursor, then the cursor moves below it.</summary>
    private static void Text(string text, float wrap, Vector4 color, float indent = 0f)
    {
        var at = ImGui.GetCursorScreenPos();
        var height = TextFlow.Height(text, wrap - indent);
        TextFlow.DrawClamped(ImGui.GetWindowDrawList(), at + new Vector2(indent, 0f), text, wrap - indent, 64, Theme.U32(color));
        ImGui.Dummy(new Vector2(wrap, height));
    }

    /// <summary>Umbra's warning in a sunk inset with a line outline; an outline only at Plain.</summary>
    private static void DrawInset(string text, float wrap)
    {
        var s = Theme.Surface;
        var pad = InsetPad;
        var min = ImGui.GetCursorScreenPos();
        var height = TextFlow.Height(text, wrap - (pad * 2f)) + (pad * 2f);
        var max = min + new Vector2(wrap, height);
        var dl = ImGui.GetWindowDrawList();
        var rounding = Theme.Flair == Flair.Plain ? 0f : UiMetrics.Px(6f);
        if (Theme.Flair != Flair.Plain)
        {
            dl.AddRectFilled(min, max, Theme.U32(s.Sunken), rounding);
        }

        dl.AddRect(min, max, Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        TextFlow.DrawClamped(dl, min + new Vector2(pad), text, wrap - (pad * 2f), 64, Theme.U32(s.TextSecondary));
        ImGui.Dummy(new Vector2(wrap, height));
    }

    /// <summary>The repository page, underlined: a click opens it in the browser.</summary>
    private static void DrawLink(string url)
    {
        var s = Theme.Surface;
        using (Theme.PushText(s.TextSecondary))
        {
            ImGui.TextUnformatted(url);
        }

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, max.Y), max, Theme.U32(s.TextSecondary), UiMetrics.Hairline);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (ImGui.IsItemClicked())
        {
            Util.OpenLink(url);
        }
    }

    /// <summary>A progress row: a check once done, a gently pulsing dot while running (still under Reduce motion and at Plain), a ring while pending.</summary>
    private static void DrawStep(in Line line, float wrap)
    {
        var s = Theme.Surface;
        var at = ImGui.GetCursorScreenPos();
        var lineHeight = ImGui.GetTextLineHeight();
        var dl = ImGui.GetWindowDrawList();
        var centre = at + new Vector2(UiMetrics.Px(6f), lineHeight * 0.5f);
        var radius = UiMetrics.Px(3.5f);
        Vector4 color;
        switch (line.State)
        {
            case StepState.Done:
                color = s.TextSecondary;
                var r = UiMetrics.Px(4.5f);
                var thick = MathF.Max(1.5f, UiMetrics.Px(1.5f));
                var tick = centre + new Vector2(-r * 0.25f, r * 0.8f);
                dl.AddLine(centre + new Vector2(-r, 0f), tick, Theme.U32(Theme.Accent), thick);
                dl.AddLine(tick, centre + new Vector2(r * 1.1f, -r * 0.9f), Theme.U32(Theme.Accent), thick);
                break;
            case StepState.Current:
                var alpha = Pulse();
                color = Theme.WithAlphaVector(s.Text, alpha);
                dl.AddCircleFilled(centre, radius, Theme.U32(Theme.WithAlphaVector(Theme.Accent, alpha)));
                break;
            default:
                color = s.TextTertiary;
                dl.AddCircle(centre, radius, Theme.U32(s.TextTertiary), 0, UiMetrics.Hairline);
                break;
        }

        Text(line.Text, wrap, color, StepIndent);
    }

    /// <summary>The running step's alpha: a slow breath at Full and Quiet, steady under Reduce motion and at Plain.</summary>
    private static float Pulse()
    {
        if (UiMetrics.ReduceMotion || Theme.Flair == Flair.Plain)
        {
            return 1f;
        }

        var phase = (float)(ImGui.GetTime() % PulseSeconds) / PulseSeconds;
        return 0.55f + (0.45f * (0.5f + (0.5f * MathF.Cos(phase * MathF.Tau))));
    }

    /// <summary>Copy repository link and Open Umbra's settings, under the manual steps.</summary>
    private void DrawManualActions()
    {
        if (ImGui.Button(CopyLabel.Value))
        {
            ImGui.SetClipboardText(UmbraAddon.Repository);
        }

        ImGui.SameLine();
        if (ImGui.Button(OpenLabel.Value))
        {
            openFailed = !setup.OpenUmbraSettings();
        }
    }

    // ------------------------------------------------------------------ the buttons

    private void DrawFooter(in UmbraSetupView state)
    {
        switch (page)
        {
            case Page.Offer:
                if (Chrome.ActionPill("##umbraCardAdd", FontAwesomeIcon.Plus.ToIconString(), Strings.UmbraSetupAdd, PillTone.Primary, true, null, PillLayout.Frame))
                {
                    // Changes nothing: the confirmation looks at Umbra and says what would change.
                    page = Page.Confirm;
                    setup.RefreshPreview();
                }

                ImGui.SameLine();
                if (ImGui.Button(HowLabel.Value))
                {
                    howReturn = Page.Offer;
                    page = Page.How;
                }

                ImGui.SameLine();
                if (ImGui.Button(NotNowLabel.Value))
                {
                    IsOpen = false;
                }

                break;
            case Page.How:
                if (ImGui.Button(BackLabel.Value))
                {
                    page = howReturn;
                    openFailed = false;
                }

                ImGui.SameLine();
                CloseButton();
                break;
            case Page.Confirm:
                DrawConfirmFooter();
                break;
            case Page.Progress:
                CloseButton();
                break;
            case Page.Result:
                if (state.Outcome is UmbraSetupOutcome.Failed or UmbraSetupOutcome.FailedPartly && UmbraAddonSetup.CanTryAgain(state.Failure))
                {
                    // Try again goes back to the confirmation: Umbra is looked at afresh, and the player agrees again.
                    if (Chrome.ActionPill("##umbraCardRetry", FontAwesomeIcon.Redo.ToIconString(), Strings.UmbraSetupTryAgain, PillTone.Primary, !setup.Busy, null, PillLayout.Frame))
                    {
                        page = Page.Confirm;
                        setup.RefreshPreview();
                    }

                    ImGui.SameLine();
                }

                CloseButton();
                break;
        }
    }

    /// <summary>
    /// Agree and add, the one button that changes Umbra: enabled once Umbra was looked at, can be driven from here, and
    /// something is missing. Otherwise Show me how (when it can't) or Close (when nothing is missing).
    /// </summary>
    private void DrawConfirmFooter()
    {
        var preview = setup.Preview;
        if (preview is { Ready: true, Failure: not UmbraFailure.None })
        {
            if (ImGui.Button(HowLabel.Value))
            {
                howReturn = Page.Confirm;
                page = Page.How;
            }

            ImGui.SameLine();
            BackButton();
            return;
        }

        if (preview is { Ready: true, Plan.Count: 0 })
        {
            CloseButton();
            return;
        }

        var ready = preview.Ready && !setup.Busy && setup.CanChangeNow;
        var agree = Chrome.ActionPill("##umbraCardAgree", FontAwesomeIcon.Check.ToIconString(), Strings.UmbraSetupAgree, PillTone.Primary, ready, null, PillLayout.Frame);
        ImGui.SameLine();
        BackButton();
        if (agree && ready)
        {
            // The player's click on Agree and add is their agreement: the only place a setup starts.
            Record();
            if (setup.AddToUmbra())
            {
                page = Page.Progress;
            }
        }
    }

    private void BackButton()
    {
        if (ImGui.Button(BackLabel.Value))
        {
            page = Page.Offer;
        }
    }

    private void CloseButton()
    {
        if (ImGui.Button(CloseLabel.Value))
        {
            IsOpen = false;
        }
    }

    /// <summary>Keeps the answer: the card never shows on its own again.</summary>
    private void Record()
    {
        if (settings.UmbraSetupOfferAnswered)
        {
            return;
        }

        settings.UmbraSetupOfferAnswered = true;
        try
        {
            settings.Save(pluginInterface);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not save the Umbra add-on card's answer");
        }
    }

    private readonly record struct Line(string Text, Kind Kind, StepState State = StepState.None);
}
