using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The moon icon (feature plan v8 H1 and H2; spec-1.22 H1, H2; moon-icon-1.22.png, icon-particles-1.22.png): a small
/// moon on the HUD, its own borderless window drawn from <c>UiBuilder.Draw</c> as the Needs you panel is. It takes the
/// pointer only over its disc, so the game keeps every click around it.
/// <list type="bullet">
/// <item><b>Click</b> opens or closes Tsukimichi. <b>Drag</b> (past a 4 px dead zone, so a click is never a drag) moves
/// it; its place is saved per screen size and kept 8 px inside the screen and clear of Umbra's toolbar
/// (<see cref="Umbra"/>), the clearance applied on top and never written back, and never changed while the player is
/// on the icon. <b>Locked</b>, a drag does nothing and says "Locked in place · right-click to unlock": no padlock is
/// drawn, because a closed padlock means Blocked.</item>
/// <item><b>Hover</b> lifts it with a cool glow (<see cref="MoonIconHover"/>) and opens the quick card after 0.25 s
/// (<see cref="MoonIconCard"/>); pressing or dragging closes the card.</item>
/// <item><b>Right-click</b>: Lock in place (or Unlock), Hide icon (with <c>/tsuki icon</c> as its hint), Tonight and
/// Settings, in words only. Hide shows the 8 s Undo: "Moon icon hidden · Undo · /tsuki icon shows it again".</item>
/// <item><b>Dots</b>: copper for Needs you (it wins), Tide for an update ready (<see cref="Updates"/>).</item>
/// <item><b>First run</b>: "Right-click for options" once, for 8 s or until a click (decision 5).</item>
/// <item><b>When it hides</b>: logged out, and by Settings › In game › Moon icon in cutscenes and Group Pose (on by
/// default) and in duties (off).</item>
/// </list>
/// The face and its particles are the theme's (<see cref="MoonIconFace"/>, <see cref="IconParticles"/>); the particles'
/// clock runs only while the icon shows, and drawing it allocates nothing.
/// </summary>
public sealed class MoonIconWindow
{
    private const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking
        | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoScrollbar;

    private const ImGuiWindowFlags ToastFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoFocusOnAppearing;

    private const string MenuId = "##moonIconMenu";

    /// <summary>How long "Locked in place · right-click to unlock" stays after a drag on a locked icon.</summary>
    private const double LockedHintSeconds = 2.5;

    /// <summary>The Hide toast's fade in.</summary>
    private const float ToastFadeSeconds = 0.18f;

    private readonly Configuration settings;
    private readonly Action save;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly Action toggleMain;
    private readonly Action openTonight;
    private readonly Action openSettings;
    private readonly MoonIconCard card;
    private readonly MoonIconHint hint;
    private readonly MoonIconHideUndo hideUndo = new();
    private readonly IconParticle[] particles = new IconParticle[IconParticles.Max];

    private double clock;
    private double lastTime = double.NaN;
    private float hover;
    private double hoverSince = double.NaN;
    private bool cardOpen;
    private bool menuOpen;
    private bool pressed;
    private bool dragging;
    private bool dragBlocked;
    private Vector2 pressAt;
    private Vector2 pressCentre;
    private Vector2 dragCentre;
    private double lockedHintUntil = double.NegativeInfinity;
    private ToolbarClearance clearance;
    private Vector2 keyScreen = new(-1f);
    private string screenKey = string.Empty;
    private Vector2 toastAt;
    private Vector2 toastSize;
    private int toastSettled;
    private bool toastHovered;

    /// <param name="settings">The icon's settings; read every frame, so Settings' changes show at once.</param>
    /// <param name="save">Saves the settings (a drop, Lock, Hide, the first-run hint).</param>
    /// <param name="clientState">Logged in, and Group Pose.</param>
    /// <param name="condition">Cutscenes and duties.</param>
    /// <param name="toggleMain">Opens or closes Tsukimichi (a click).</param>
    /// <param name="openTonight">Opens Tsukimichi on Tonight (the menu).</param>
    /// <param name="openSettings">Opens Settings on the moon icon's block (the menu).</param>
    /// <param name="card">The quick card.</param>
    internal MoonIconWindow(
        Configuration settings,
        Action save,
        IClientState clientState,
        ICondition condition,
        Action toggleMain,
        Action openTonight,
        Action openSettings,
        MoonIconCard card)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
        this.toggleMain = toggleMain ?? throw new ArgumentNullException(nameof(toggleMain));
        this.openTonight = openTonight ?? throw new ArgumentNullException(nameof(openTonight));
        this.openSettings = openSettings ?? throw new ArgumentNullException(nameof(openSettings));
        this.card = card ?? throw new ArgumentNullException(nameof(card));
        hint = new MoonIconHint(settings.MoonIconHintSeen);
    }

    /// <summary>Where Umbra's toolbar is (M3); null until the Umbra probe is wired, which keeps no clearance.</summary>
    public IUmbraLayout? Umbra { get; set; }

    /// <summary>Whether a newer version is ready (U1); null until the update watcher is wired, which shows no update dot.</summary>
    public IUpdateState? Updates { get; set; }

    /// <summary>The Needs you alerts (1.18 A5) behind the copper dot; null shows none.</summary>
    public RunStops? Stops { get; set; }

    /// <summary>Shows or hides the icon (<c>/tsuki icon</c>, Settings) and saves; a hidden icon's Undo toast goes.</summary>
    public void ToggleEnabled()
    {
        settings.MoonIconEnabled = !settings.MoonIconEnabled;
        save();
    }

    /// <summary>Forgets where the icon was put on this screen size, so it goes back to where it starts (Settings).</summary>
    public void ResetPosition()
    {
        var key = MoonIconRules.ScreenKey(ImGuiHelpers.MainViewport.Size);
        if (settings.MoonIconPlaces?.Remove(key) == true)
        {
            save();
        }
    }

    /// <summary>Draws the icon (when it shows) and its Hide toast; once per frame from <c>UiBuilder.Draw</c>. Never throws into the draw loop.</summary>
    public void Draw()
    {
        var now = ImGui.GetTime();
        var delta = double.IsNaN(lastTime) ? 0.0 : Math.Clamp(now - lastTime, 0.0, 0.25);
        lastTime = now;
        var shows = MoonIconRules.Shows(settings.MoonIconEnabled, settings.MoonIconHiding(), Context());
        if (shows)
        {
            DrawIcon(now, delta);
        }
        else
        {
            Rest();
        }

        DrawHiddenToast(now);
    }

    private MoonIconContext Context() => new(
        clientState.IsLoggedIn,
        condition[ConditionFlag.WatchingCutscene] || condition[ConditionFlag.WatchingCutscene78] || condition[ConditionFlag.OccupiedInCutSceneEvent],
        clientState.IsGPosing,
        condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95]);

    /// <summary>Not shown: nothing is held, the card is closed and the particles' clock stands still.</summary>
    private void Rest()
    {
        hover = 0f;
        hoverSince = double.NaN;
        pressed = false;
        dragging = false;
        menuOpen = false;
        CloseCard();
    }

    private void DrawIcon(double now, double delta)
    {
        var viewport = ImGuiHelpers.MainViewport;
        var origin = viewport.Pos;
        var screen = viewport.Size;
        var scale = UiMetrics.Px(1f);
        var diameter = MathF.Round(UiMetrics.Px(MoonIconRules.SizeLogical(settings.MoonIconSize)));
        var radius = diameter * 0.5f;
        var flair = Theme.Flair;
        var reduce = UiMetrics.ReduceMotion;
        var io = ImGui.GetIO();
        var places = settings.MoonIconPlaces ??= [];

        // Umbra's clearance is taken only while the player is not on the icon, so it never moves under the pointer.
        if (!pressed && !dragging && !menuOpen && hover <= 0f)
        {
            clearance = Umbra?.Toolbar ?? ToolbarClearance.None;
        }

        // The screen size's key is built again only when the size changes, so a frame builds no string.
        if (screen != keyScreen)
        {
            keyScreen = screen;
            screenKey = MoonIconRules.ScreenKey(screen);
        }

        var saved = places.TryGetValue(screenKey, out var place) ? place : (MoonIconPlace?)null;
        var centreLocal = dragging ? dragCentre : MoonIconRules.Place(saved, screen, diameter, scale, clearance);
        var centre = origin + centreLocal;

        // The window takes the pointer only over the disc (or while it holds a press), so clicks round it reach the game.
        var over = Vector2.DistanceSquared(io.MousePos, centre) <= radius * radius;
        var flags = over || pressed || dragging ? BaseFlags : BaseFlags | ImGuiWindowFlags.NoInputs;
        var rise = MoonIconHover.RiseLogical(flair, reduce) * scale;
        var half = MathF.Ceiling((radius * (IconParticles.MaxReach + 0.15f)) + rise + (6f * scale));
        ImGui.SetNextWindowPos(centre - new Vector2(half), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(half * 2f), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        using var body = Typography.Body();
        using var chrome = Theme.PushNightWindow();
        var visible = ImGui.Begin("##tsukimichiMoonIcon", flags);
        var hovered = false;
        var clicked = false;
        try
        {
            if (visible)
            {
                UiMetrics.ApplyFontScale();
                ImGui.SetCursorScreenPos(centre - new Vector2(radius));
                clicked = ImGui.InvisibleButton("##moonIconHit", new Vector2(diameter));
                hovered = over && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
                Press(io, now, centreLocal, screen, diameter, scale, ImGui.IsItemActive(), clicked, places);
                var rightClicked = hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right);
                if (rightClicked)
                {
                    ImGui.OpenPopup(MenuId);
                }

                Menu(now, centre);

                // The face, lifted by the hover: Full, Quiet and Plain each their own; particles only at Full.
                hover = MoonIconHover.Step(hover, hovered || pressed || dragging || menuOpen, (float)delta, flair, reduce);
                var count = 0;
                if (IconParticles.Enabled(flair, reduce))
                {
                    clock += delta;
                    count = IconParticles.At(GlyphSeamTheme(), flair, reduce, clock, particles);
                }

                var look = new MoonIconFace.Look(GlyphSeamTheme(), Themes.GlyphSeam.Appearance.Frames, flair);
                var dl = ImGui.GetWindowDrawList();
                var drawCentre = new Vector2(MathF.Round(centre.X), MathF.Round(centre.Y));
                var orbit = count > 0 && IconParticles.HasOrbit(look.Theme);
                MoonIconFace.Draw(dl, drawCentre, radius, look, hover, rise * hover, particles.AsSpan(0, count), orbit, DotInk(), scale);

                // The first-run hint and the locked hint, beside the icon.
                var showHint = hint.Update(now, true, clicked || rightClicked, out var becameSeen);
                if (becameSeen)
                {
                    settings.MoonIconHintSeen = true;
                    save();
                }

                var iconRect = new ScreenRect(centre - new Vector2(radius), centre + new Vector2(radius));
                if (now < lockedHintUntil)
                {
                    Pill(dl, iconRect, Strings.MoonIconLockedHint, viewport.Pos, viewport.Size);
                }
                else if (showHint)
                {
                    Pill(dl, iconRect, Strings.MoonIconFirstRunHint, viewport.Pos, viewport.Size);
                }
            }
        }
        finally
        {
            ImGui.End();
            ImGui.PopStyleVar(2);
        }

        // The quick card after a short rest on the icon; never while pressing, dragging or in the menu.
        if (hovered && !pressed && !dragging && !menuOpen)
        {
            if (double.IsNaN(hoverSince))
            {
                hoverSince = now;
            }
        }
        else
        {
            hoverSince = double.NaN;
        }

        if (!double.IsNaN(hoverSince) && now - hoverSince >= MoonIconRules.CardDelaySeconds)
        {
            cardOpen = true;
            card.Draw(new ScreenRect(centre - new Vector2(radius), centre + new Vector2(radius)), settings.MoonIconLocked, now);
        }
        else
        {
            CloseCard();
        }
    }

    /// <summary>
    /// The dot's ink: copper for Needs you (it wins), the cool accent for an update, none otherwise. A dot never carries
    /// its meaning alone (spec-1.22 "Colour language"): the quick card says it in words, Needs you with
    /// <c>Stops?.NeedsYou.Current?.Title</c> and the update with its version (<see cref="MoonIconCard"/>).
    /// </summary>
    private System.Numerics.Vector4? DotInk() => MoonIconRules.Dot(Stops?.NeedsYou.Current is not null, Updates?.ReadyVersion) switch
    {
        MoonIconDot.NeedsYou => Theme.Copper,
        MoonIconDot.UpdateReady => Theme.Surface.Cool,
        _ => null,
    };

    /// <summary>The theme whose face and particles the icon wears: the appearance's theme in effect.</summary>
    private static Core.Ui.Themes.ThemeId GlyphSeamTheme() => Themes.GlyphSeam.Appearance.Theme.Id;

    /// <summary>
    /// A press on the icon: past the dead zone it is a drag (or, locked, it says why it stays); a release that was no
    /// drag is a click, which opens or closes Tsukimichi; a drop saves the place for this screen size.
    /// </summary>
    private void Press(ImGuiIOPtr io, double now, Vector2 centreLocal, Vector2 screen, float diameter, float scale, bool active, bool clicked, System.Collections.Generic.Dictionary<string, MoonIconPlace> places)
    {
        if (active && !pressed)
        {
            pressed = true;
            dragBlocked = false;
            pressAt = io.MousePos;
            pressCentre = centreLocal;
        }

        if (active && pressed)
        {
            var moved = io.MousePos - pressAt;
            if (!dragging && MoonIconRules.IsDrag(moved, MoonIconRules.DeadZoneLogical * scale))
            {
                if (settings.MoonIconLocked)
                {
                    if (!dragBlocked)
                    {
                        dragBlocked = true;
                        lockedHintUntil = now + LockedHintSeconds;
                    }
                }
                else
                {
                    dragging = true;
                }
            }

            if (dragging)
            {
                dragCentre = MoonIconRules.Clamp(pressCentre + moved, diameter, screen, MoonIconRules.EdgeMarginLogical * scale, clearance);
            }

            return;
        }

        if (!pressed)
        {
            return;
        }

        // Released.
        pressed = false;
        if (dragging)
        {
            dragging = false;
            MoonIconRules.Store(places, screen, dragCentre, diameter, scale);
            save();
        }
        else if (clicked && !dragBlocked)
        {
            toggleMain();
        }
    }

    /// <summary>The right-click menu, in words: Lock in place or Unlock, Hide icon, then Tonight and Settings.</summary>
    private void Menu(double now, Vector2 centre)
    {
        menuOpen = false;
        if (!ImGui.BeginPopup(MenuId))
        {
            return;
        }

        menuOpen = true;
        try
        {
            if (ImGui.MenuItem(settings.MoonIconLocked ? Strings.MoonIconMenuUnlock : Strings.MoonIconMenuLock))
            {
                settings.MoonIconLocked = !settings.MoonIconLocked;
                save();
            }

            if (ImGui.MenuItem(Strings.MoonIconMenuHide, Strings.MoonIconCommand, false))
            {
                settings.MoonIconEnabled = false;
                save();
                toastAt = centre;
                toastSize = Vector2.Zero;
                toastSettled = 0;
                hideUndo.Hidden(now);
            }

            ImGui.Separator();
            if (ImGui.MenuItem(Strings.TonightTitle))
            {
                openTonight();
            }

            if (ImGui.MenuItem(Strings.MoonIconMenuSettings))
            {
                openSettings();
            }
        }
        finally
        {
            ImGui.EndPopup();
        }
    }

    /// <summary>
    /// "Moon icon hidden · Undo · /tsuki icon shows it again", where the icon was, for 8 s (the pointer on it stops the
    /// clock); kept on screen. It settles its size unseen, then fades in (at once under Reduce motion).
    /// </summary>
    private void DrawHiddenToast(double now)
    {
        var hoveredToast = ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel) is false && toastHovered;
        if (!hideUndo.Tick(now, hoveredToast, settings.MoonIconEnabled))
        {
            toastHovered = false;
            return;
        }

        var viewport = ImGuiHelpers.MainViewport;
        var screen = new ScreenRect(viewport.Pos, viewport.Pos + viewport.Size);
        var margin = UiMetrics.Px(MoonIconRules.EdgeMarginLogical);
        var pos = toastAt - (toastSize * 0.5f);
        pos = new Vector2(
            Math.Clamp(pos.X, screen.Min.X + margin, MathF.Max(screen.Min.X + margin, screen.Max.X - margin - toastSize.X)),
            Math.Clamp(pos.Y, screen.Min.Y + margin, MathF.Max(screen.Min.Y + margin, screen.Max.Y - margin - toastSize.Y)));
        ImGui.SetNextWindowPos(new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y)), ImGuiCond.Always);
        var measuring = toastSettled < GamePanelShell.SettleFrames;
        var fade = measuring ? 0f : UiMetrics.ReduceMotion ? 1f : Math.Clamp((float)((now - hideUndo.StartedAt) / ToastFadeSeconds), 0f, 1f);
        using var body = Typography.Body();
        using var style = GamePanelShell.PushPanelStyle(measuring);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, fade);
        var flags = measuring ? ToastFlags | ImGuiWindowFlags.NoInputs : ToastFlags;
        try
        {
            if (ImGui.Begin("##tsukimichiMoonHidden", flags))
            {
                UiMetrics.ApplyFontScale();
                toastHovered = !measuring && ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows);
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(Strings.MoonIconHidden);
                ImGui.SameLine(0f, 0f);
                ImGui.TextDisabled(Strings.UndoToastSeparator);
                ImGui.SameLine(0f, 0f);
                using (Theme.PushText(Theme.Accent))
                {
                    if (ImGui.SmallButton(Strings.UndoToastUndo) && hideUndo.TryUndo())
                    {
                        settings.MoonIconEnabled = true;
                        save();
                    }
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.UndoToastUndoTooltip);
                }

                ImGui.SameLine(0f, 0f);
                ImGui.TextDisabled(Strings.UndoToastSeparator);
                ImGui.SameLine(0f, 0f);
                ImGui.TextDisabled(Strings.MoonIconShowsAgain);
                toastSize = ImGui.GetWindowSize();
                if (measuring)
                {
                    toastSettled++;
                }
            }
        }
        finally
        {
            ImGui.End();
            ImGui.PopStyleVar();
        }
    }

    /// <summary>A small pill beside the icon (the side with room), for the first-run and locked hints; drawn in the icon's own list.</summary>
    private static void Pill(ImDrawListPtr dl, in ScreenRect icon, string text, Vector2 screenMin, Vector2 screenSize)
    {
        var s = Theme.Surface;
        var size = ImGui.CalcTextSize(text);
        var padX = UiMetrics.Px(10f);
        var padY = UiMetrics.Px(4f);
        var pill = new Vector2(size.X + (2f * padX), size.Y + (2f * padY));
        var gap = UiMetrics.Px(MoonIconRules.CardGapLogical);
        var screen = new ScreenRect(screenMin, screenMin + screenSize);
        var left = icon.Max.X + gap + pill.X <= screen.Max.X ? icon.Max.X + gap : icon.Min.X - gap - pill.X;
        var top = MathF.Round(icon.Center.Y - (pill.Y * 0.5f));
        var min = new Vector2(MathF.Round(left), top);
        var max = min + pill;
        dl.PushClipRect(screenMin, screenMin + screenSize, false);
        dl.AddRectFilled(min, max, Theme.WithAlpha(s.Window, 0.94f), pill.Y * 0.5f);
        dl.AddRect(min, max, Theme.U32(s.Line), pill.Y * 0.5f, ImDrawFlags.None, UiMetrics.Hairline);
        dl.AddText(min + new Vector2(padX, padY), Theme.U32(s.Text), text);
        dl.PopClipRect();
    }

    private void CloseCard()
    {
        if (cardOpen)
        {
            cardOpen = false;
            card.Close();
        }
    }
}
