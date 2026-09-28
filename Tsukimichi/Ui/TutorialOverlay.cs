using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Tsukimichi.Config;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The interactive tour (<see cref="ITutorial"/>): fifteen steps, each pointing at a region the main window
/// recorded in <see cref="UiState.Rects"/>. Drawn on the foreground draw list at the end of the main window's
/// Draw: the window area (union of every recorded rect, or the screen) is dimmed with Night at 70 % except for a
/// rounded cutout around the target and around the card; the target gets a Moon border with a soft glow; the card
/// is a small ImGui window placed beside the target, flipping sides near the screen edge, with the step number,
/// title, body and Back / Next / Skip. Esc skips while the card is focused. A step whose region is not on screen
/// shows without a highlight.
/// On first run (<see cref="Configuration.TutorialCompleted"/> false) the welcome card offers the tour when the
/// main window first opens; finishing or declining sets the flag. Sizes go through
/// <see cref="ImGuiHelpers.GlobalScale"/> (the main window's UiMetrics is not used here).
/// </summary>
public sealed class TutorialOverlay : ITutorial
{
    private const float CutoutPad = 6f;
    private const float BorderRounding = 8f;
    private const float BorderThickness = 2f;
    private const float GlowStep = 3f;
    private const int GlowLayers = 3;
    private const float Gap = 14f;
    private const float CardWidth = 330f;
    private const float CardHeightGuess = 160f;

    /// <summary>
    /// Frames a step is drawn before the card's measured size is trusted: the auto-resized card takes its size from
    /// the previous frame's content, so the frame a step changes (and the measurement taken on it) still reflect the
    /// previous step.
    /// </summary>
    private const int CardSettleFrames = 2;
    private const float TitleScale = 1.2f;
    private const float DimAlpha = 0.7f;

    /// <summary>Two holes (target and card) split the area into at most 4² pieces.</summary>
    private const int PieceCapacity = 16;

    private static readonly uint DimU32 = Theme.WithAlpha(Theme.Night, DimAlpha);
    private static readonly Vector4 CardBorder = Theme.WithAlphaVector(Theme.Moon, 0.8f);
    private static readonly Vector4 CardButton = Vector4.Lerp(Theme.Night, Theme.Veil, 0.45f) with { W = 1f };
    private static readonly Vector4 CardButtonHovered = Theme.Veil;
    private static readonly Vector4 CardButtonActive = Theme.Dusk;

    private const ImGuiWindowFlags CardFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking |
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse;

    private static readonly string[] NoKeys = [];

    /// <summary>Steps 2–10 point at Journal regions, so entering them (forwards or with Back) shows that tab.</summary>
    private static readonly Action<UiState> ShowJournal = static ui => ui.Tab = NavTab.Journal;

    /// <summary>
    /// One step: the rect keys whose union is highlighted (empty for the welcome and finish cards), keys tried
    /// when none of the first set is recorded, and what to change when the step is shown.
    /// </summary>
    private readonly record struct Step(string Title, string Body, string[] Keys, string[] FallbackKeys, Action<UiState>? OnShow);

    private static readonly Step[] Steps =
    [
        new(Strings.Tutorial.WelcomeTitle, Strings.Tutorial.WelcomeBody, NoKeys, NoKeys, null),
        new(Strings.Tutorial.SearchTitle, Strings.Tutorial.SearchBody, ["search"], ["toolbar"], ShowJournal),
        new(Strings.Tutorial.FiltersTitle, Strings.Tutorial.FiltersBody, ["filterPanel", "filtersButton"], NoKeys, static ui =>
        {
            ui.Tab = NavTab.Journal;
            ui.FilterPanelOpen = true;
        }),
        new(Strings.Tutorial.ChipsTitle, Strings.Tutorial.ChipsBody, ["chips"], ["search"], ShowJournal),
        new(Strings.Tutorial.TabsTitle, Strings.Tutorial.TabsBody, ["tabs"], NoKeys, ShowJournal),
        new(Strings.Tutorial.TreeTitle, Strings.Tutorial.TreeBody, ["tree"], NoKeys, ShowJournal),
        new(Strings.Tutorial.TableTitle, Strings.Tutorial.TableBody, ["table"], NoKeys, ShowJournal),
        new(Strings.Tutorial.RequirementsTitle, Strings.Tutorial.RequirementsBody, ["detail.requirements"], ["detail"], ShowJournal),
        new(Strings.Tutorial.PathTitle, Strings.Tutorial.PathBody, ["detail.path"], ["detail"], ShowJournal),
        new(Strings.Tutorial.GiverTitle, Strings.Tutorial.GiverBody, ["detail.giver"], ["detail"], ShowJournal),
        new(Strings.Tutorial.MoonlitTitle, Strings.Tutorial.MoonlitBody, ["moonlit.kinds", "moonlit.table"], NoKeys, static ui => ui.Tab = NavTab.Moonlit),
        new(Strings.Tutorial.CharactersTitle, Strings.Tutorial.CharactersBody, ["characters.dashboard"], ["characters.list"], static ui => ui.Tab = NavTab.Characters),
        new(Strings.Tutorial.FlightTitle, Strings.Tutorial.FlightBody, ["flight.table"], ["flight.zones"], static ui => ui.Tab = NavTab.Flight),
        new(Strings.Tutorial.HelpTitle, Strings.Tutorial.HelpBody, ["helpButton", "tutorialButton", "settingsButton"], ["toolbar"], null),
        new(Strings.Tutorial.FinishTitle, Strings.Tutorial.FinishBody, NoKeys, NoKeys, null),
    ];

    private readonly Configuration settings;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly UiState ui;

    /// <summary>"3 of 15" per step, built once.</summary>
    private readonly string[] progress;

    private int index = -1;
    private bool offering;
    private bool offeredThisSession;
    private bool mainWasOpen;
    private bool focusCard;
    private Vector2 cardSize;

    /// <summary>Frames drawn on the current step; the card is placed from an estimate until <see cref="CardSettleFrames"/>.</summary>
    private int stepFrames;

    public TutorialOverlay(Configuration settings, IDalamudPluginInterface pluginInterface, UiState ui)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));

        progress = new string[Steps.Length];
        for (var i = 0; i < Steps.Length; i++)
        {
            progress[i] = string.Format(CultureInfo.InvariantCulture, Strings.Tutorial.ProgressFormat, i + 1, Steps.Length);
        }
    }

    /// <summary>The main window, watched by <see cref="CheckFirstRun"/> for its first opening.</summary>
    public Window? WatchedWindow { get; set; }

    /// <summary>Opens the help window; the finish card's "Open help" button. Null hides the button.</summary>
    public Action? OpenHelp { get; set; }

    /// <inheritdoc/>
    public bool Active => index >= 0;

    /// <summary>Zero-based step shown, or -1.</summary>
    public int StepIndex => index;

    public int StepCount => Steps.Length;

    /// <summary>True while the first-run welcome card is showing "Take the tour" / "Not now".</summary>
    public bool Offering => offering;

    /// <inheritdoc/>
    public void Start()
    {
        offering = false;
        ui.Tab = NavTab.Journal;
        GoTo(0);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        index = -1;
        offering = false;
    }

    /// <summary>
    /// <c>UiBuilder.Draw</c> handler: the first time <see cref="WatchedWindow"/> is seen open while
    /// <see cref="Configuration.TutorialCompleted"/> is false, shows the welcome card with the offer. Once per session.
    /// </summary>
    public void CheckFirstRun()
    {
        if (WatchedWindow is not { } window)
        {
            return;
        }

        var open = window.IsOpen;
        if (open && !mainWasOpen && !offeredThisSession && !settings.TutorialCompleted && !Active)
        {
            offeredThisSession = true;
            offering = true;
            index = 0;
            focusCard = true;
            stepFrames = 0;
        }

        mainWasOpen = open;
    }

    /// <inheritdoc/>
    public void Draw(UiState ui)
    {
        if (!Active)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        ref readonly var step = ref Steps[index];

        var viewport = ImGuiHelpers.MainViewport;
        var screen = ScreenRect.FromSize(viewport.WorkPos, viewport.WorkSize);
        var area = UnionOfRects(ui);
        if (area.IsEmpty)
        {
            area = screen;
        }

        var hasTarget = TryTarget(ui, in step, out var target);
        if (hasTarget)
        {
            target = target.Expand(CutoutPad * scale);
        }

        // Right after a step change the card is placed from a conservative estimate and gets no hole in the dim;
        // once it has settled, its measured size is used.
        var cardMeasured = stepFrames >= CardSettleFrames;
        var size = cardMeasured ? cardSize : new Vector2(CardWidth * scale, CardHeightGuess * scale);
        var cardPos = hasTarget
            ? OverlayGeometry.PlaceCard(in target, size, in screen, Gap * scale, out _)
            : OverlayGeometry.CenterIn(in area, size);
        var card = ScreenRect.FromSize(cardPos, size);

        DrawDim(in area, hasTarget, in target, in card, cardMeasured);
        if (hasTarget)
        {
            DrawHighlight(in target, scale);
        }

        DrawCard(cardPos, in step, scale);

        // A click in the main window gives it focus; its NoBringToFrontOnFocus flag (set by MainWindow while the tour
        // runs) already keeps the card in front. As a second guard the card asks for focus again on the frame after any
        // release, but not while a widget is active or a popup is open (focusing would deactivate the widget or close
        // the popup, and the window underneath must stay usable).
        if (Active && AnyMouseReleased() && !ImGui.IsAnyItemActive() && !ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel))
        {
            focusCard = true;
        }
    }

    private static bool AnyMouseReleased() =>
        ImGui.IsMouseReleased(ImGuiMouseButton.Left) || ImGui.IsMouseReleased(ImGuiMouseButton.Right) || ImGui.IsMouseReleased(ImGuiMouseButton.Middle);

    // ------------------------------------------------------------------ steps

    private void GoTo(int i)
    {
        index = i;
        focusCard = true;
        stepFrames = 0;
        Steps[i].OnShow?.Invoke(ui);
    }

    /// <summary>Finishing the tour or declining the first-run offer both stop it and remember that it need not be offered again.</summary>
    private void Complete()
    {
        settings.TutorialCompleted = true;
        settings.Save(pluginInterface);
        Stop();
    }

    // ------------------------------------------------------------------ geometry

    private static ScreenRect UnionOfRects(UiState ui)
    {
        var area = default(ScreenRect);
        foreach (var entry in ui.Rects)
        {
            var rect = new ScreenRect(entry.Value.Min, entry.Value.Max);
            area = ScreenRect.Union(in area, in rect);
        }

        return area;
    }

    /// <summary>Union of the step's recorded keys; the fallback keys when none of the first set is present.</summary>
    private static bool TryTarget(UiState ui, in Step step, out ScreenRect target)
    {
        return TryUnion(ui, step.Keys, out target) || TryUnion(ui, step.FallbackKeys, out target);
    }

    private static bool TryUnion(UiState ui, string[] keys, out ScreenRect target)
    {
        target = default;
        var found = false;
        foreach (var key in keys)
        {
            if (!ui.Rects.TryGetValue(key, out var r))
            {
                continue;
            }

            var rect = new ScreenRect(r.Min, r.Max);
            if (rect.IsEmpty)
            {
                continue;
            }

            target = found ? ScreenRect.Union(in target, in rect) : rect;
            found = true;
        }

        return found;
    }

    // ------------------------------------------------------------------ drawing

    /// <summary>Night at 70 % over the area, as bands around the target and the card (ImGui has no cutouts).</summary>
    private void DrawDim(in ScreenRect area, bool hasTarget, in ScreenRect target, in ScreenRect card, bool cardMeasured)
    {
        Span<ScreenRect> holes = stackalloc ScreenRect[2];
        var holeCount = 0;
        if (hasTarget)
        {
            holes[holeCount++] = target;
        }

        // The card's hole is cut only from its measured rectangle; an estimate would leave a visible seam.
        if (cardMeasured)
        {
            holes[holeCount++] = card;
        }

        Span<ScreenRect> pieces = stackalloc ScreenRect[PieceCapacity];
        Span<ScreenRect> scratch = stackalloc ScreenRect[PieceCapacity];
        var count = OverlayGeometry.Cutout(in area, holes[..holeCount], pieces, scratch);

        var dl = ImGui.GetForegroundDrawList();
        for (var i = 0; i < count; i++)
        {
            dl.AddRectFilled(pieces[i].Min, pieces[i].Max, DimU32);
        }
    }

    /// <summary>A Moon border with three fading rings outside it.</summary>
    private static void DrawHighlight(in ScreenRect target, float scale)
    {
        var dl = ImGui.GetForegroundDrawList();
        var rounding = BorderRounding * scale;
        for (var k = GlowLayers; k >= 1; k--)
        {
            var spread = k * GlowStep * scale;
            var alpha = 0.28f - 0.08f * k;
            dl.AddRect(target.Min - new Vector2(spread), target.Max + new Vector2(spread), Theme.WithAlpha(Theme.Moon, alpha), rounding + spread, ImDrawFlags.RoundCornersAll, GlowStep * scale);
        }

        dl.AddRect(target.Min, target.Max, Theme.MoonU32, rounding, ImDrawFlags.RoundCornersAll, BorderThickness * scale);
    }

    private void DrawCard(Vector2 pos, in Step step, float scale)
    {
        var width = CardWidth * scale;
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0f), new Vector2(width, float.MaxValue));
        if (focusCard)
        {
            ImGui.SetNextWindowFocus();
            focusCard = false;
        }

        using var colors = ImRaii.PushColor(ImGuiCol.WindowBg, Theme.Night)
                                 .Push(ImGuiCol.Border, CardBorder)
                                 .Push(ImGuiCol.Text, Theme.Silver)
                                 .Push(ImGuiCol.TextDisabled, Theme.Dusk)
                                 .Push(ImGuiCol.Separator, Theme.Veil)
                                 .Push(ImGuiCol.Button, CardButton)
                                 .Push(ImGuiCol.ButtonHovered, CardButtonHovered)
                                 .Push(ImGuiCol.ButtonActive, CardButtonActive);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, BorderRounding * scale)
                                 .Push(ImGuiStyleVar.WindowBorderSize, 1f)
                                 .Push(ImGuiStyleVar.WindowPadding, new Vector2(16f, 14f) * scale);

        var visible = ImGui.Begin(Strings.Tutorial.CardId, CardFlags);
        try
        {
            if (visible)
            {
                // Measured before the content, whose buttons may move to another step and restart the count.
                cardSize = ImGui.GetWindowSize();
                if (stepFrames < CardSettleFrames)
                {
                    stepFrames++;
                }

                DrawCardContent(in step, scale);

                // Esc skips only while the card itself has focus; with the main window focused it does nothing (the
                // window's own close hotkey is suspended by MainWindow for the tour).
                if (Active && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && ImGui.IsKeyPressed(ImGuiKey.Escape, false))
                {
                    Stop();
                }
            }
        }
        finally
        {
            ImGui.End();
        }
    }

    private void DrawCardContent(in Step step, float scale)
    {
        ImGui.TextDisabled(progress[index]);
        ImGui.SetWindowFontScale(TitleScale);
        using (Theme.PushText(Theme.Moon))
        {
            ImGui.TextUnformatted(step.Title);
        }

        ImGui.SetWindowFontScale(1f);
        ImGui.Spacing();
        ImGui.TextWrapped(step.Body);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var last = index == Steps.Length - 1;
        if (offering)
        {
            if (ImGui.Button(Strings.Tutorial.TakeTour))
            {
                offering = false;
                ui.Tab = NavTab.Journal;
                GoTo(1);
            }

            ImGui.SameLine(0f, 8f * scale);
            if (ImGui.Button(Strings.Tutorial.NotNow))
            {
                Complete();
            }

            return;
        }

        if (last)
        {
            if (OpenHelp is { } openHelp)
            {
                if (ImGui.Button(Strings.Tutorial.OpenHelp))
                {
                    openHelp();
                    Complete();
                }

                ImGui.SameLine(0f, 8f * scale);
            }

            if (ImGui.Button(Strings.Tutorial.Done))
            {
                Complete();
            }

            return;
        }

        if (index > 0)
        {
            if (ImGui.Button(Strings.Tutorial.Back))
            {
                GoTo(index - 1);
            }

            ImGui.SameLine(0f, 8f * scale);
        }

        if (ImGui.Button(Strings.Tutorial.Next))
        {
            GoTo(index + 1);
        }

        var skipWidth = ImGuiHelpers.GetButtonSize(Strings.Tutorial.Skip).X;
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - skipWidth);
        if (ImGui.Button(Strings.Tutorial.Skip))
        {
            Stop();
        }
    }
}
