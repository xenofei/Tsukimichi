using System.Globalization;
using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>The moon icon's size (spec-1.22 H1): Small 32, Medium 40 (the default) or Large 48 logical px.</summary>
public enum MoonIconSize
{
    Small = 0,
    Medium = 1,
    Large = 2,
}

/// <summary>Where the player put the moon icon on one screen size: its centre, in pixels from the screen's top left.</summary>
/// <param name="X">The centre's distance from the screen's left edge.</param>
/// <param name="Y">The centre's distance from the screen's top edge.</param>
public record struct MoonIconPlace(float X, float Y);

/// <summary>The screen edge a toolbar holds (spec-1.22 M3): none for a floating or auto-hidden bar.</summary>
public enum ToolbarEdge
{
    None = 0,
    Top = 1,
    Bottom = 2,
}

/// <summary>
/// The room a toolbar keeps at a screen edge (spec-1.22 M3, Umbra's): the edge, and the bar's height in screen pixels.
/// Surfaces keep their top edge at <see cref="Height"/> + 8 under a top bar, their bottom edge at the bar's top − 8 over a
/// bottom bar, and stay where they are for <see cref="None"/>.
/// </summary>
public readonly record struct ToolbarClearance(ToolbarEdge Edge, float Height)
{
    /// <summary>No bar holds an edge.</summary>
    public static ToolbarClearance None => default;

    /// <summary>The bar's height, or 0 for none or a value that is not a positive finite number.</summary>
    public float Room => Edge != ToolbarEdge.None && float.IsFinite(Height) && Height > 0f ? Height : 0f;
}

/// <summary>
/// Where Umbra's toolbar is (spec-1.22 M3), read-only. The Umbra probe implements it; until it is wired the moon icon
/// takes no clearance (a null layout reads as <see cref="ToolbarClearance.None"/>).
/// </summary>
public interface IUmbraLayout
{
    /// <summary>The edge Umbra's toolbar holds and its height in screen pixels; <see cref="ToolbarClearance.None"/> when it holds none.</summary>
    ToolbarClearance Toolbar { get; }
}

/// <summary>
/// Whether a newer Tsukimichi is ready in Dalamud (spec-1.22 U1). The update watcher implements it; until it is wired the
/// moon icon shows no update dot (a null state reads as no update).
/// </summary>
public interface IUpdateState
{
    /// <summary>
    /// The version ready to install ("1.23.0") while its note shows, or null: none is ready, or the player chose Later for it.
    /// </summary>
    string? ReadyVersion { get; }
}

/// <summary>The dot on the moon icon's upper right (spec-1.22 H1): Needs you (copper) wins over Update ready (Tide).</summary>
public enum MoonIconDot
{
    None = 0,
    UpdateReady = 1,
    NeedsYou = 2,
}

/// <summary>What the game is doing, as the moon icon's hiding rules read it.</summary>
/// <param name="LoggedIn">A character is logged in.</param>
/// <param name="Cutscene">A cutscene plays.</param>
/// <param name="GroupPose">Group Pose is open.</param>
/// <param name="InDuty">The character is bound by a duty.</param>
public readonly record struct MoonIconContext(bool LoggedIn, bool Cutscene, bool GroupPose, bool InDuty);

/// <summary>The moon icon's hiding options (Settings › In game › Moon icon).</summary>
/// <param name="Cutscenes">Hide in cutscenes (on by default).</param>
/// <param name="GroupPose">Hide in Group Pose (on by default).</param>
/// <param name="Duties">Hide in duties (off by default).</param>
public readonly record struct MoonIconHideRules(bool Cutscenes, bool GroupPose, bool Duties)
{
    /// <summary>The defaults: hidden in cutscenes and Group Pose, shown in duties.</summary>
    public static MoonIconHideRules Default => new(true, true, false);
}

/// <summary>
/// The moon icon's rules (feature plan v8 H1; spec-1.22 H1), pure so they are tested: its size, when it shows, which dot
/// it wears, where it sits (per screen size, clamped on screen and clear of a toolbar) and where its quick card opens.
/// </summary>
public static class MoonIconRules
{
    /// <summary>How far inside the screen's edges the icon stays, in logical px.</summary>
    public const float EdgeMarginLogical = 8f;

    /// <summary>How far a press must move before it is a drag, in logical px: a click is never a drag.</summary>
    public const float DeadZoneLogical = 4f;

    /// <summary>The gap between the icon and its quick card, in logical px.</summary>
    public const float CardGapLogical = 8f;

    /// <summary>The quick card's width, in logical px.</summary>
    public const float CardWidthLogical = 300f;

    /// <summary>How long the pointer rests on the icon before the quick card opens.</summary>
    public const double CardDelaySeconds = 0.25;

    /// <summary>Where a new screen size puts the icon: this share of the screen's width from the left, and this many logical px down.</summary>
    public const float DefaultXShare = 0.25f;

    public const float DefaultYLogical = 72f;

    /// <summary>The icon's diameter in logical px (times the UI scale on screen; Text size never changes it).</summary>
    public static float SizeLogical(MoonIconSize size) => size switch
    {
        MoonIconSize.Small => 32f,
        MoonIconSize.Large => 48f,
        _ => 40f,
    };

    /// <summary>Whether the icon shows: it is turned on, a character is logged in, and no hiding option the player kept on applies.</summary>
    public static bool Shows(bool enabled, in MoonIconHideRules rules, in MoonIconContext context) =>
        enabled
        && context.LoggedIn
        && !(rules.Cutscenes && context.Cutscene)
        && !(rules.GroupPose && context.GroupPose)
        && !(rules.Duties && context.InDuty);

    /// <summary>The dot: Needs you wins over Update ready (decision 12); none when neither is up.</summary>
    public static MoonIconDot Dot(bool needsYou, string? readyVersion) =>
        needsYou ? MoonIconDot.NeedsYou : !string.IsNullOrEmpty(readyVersion) ? MoonIconDot.UpdateReady : MoonIconDot.None;

    /// <summary>The key a place is saved under: the screen's size in whole pixels, "1920x1080".</summary>
    public static string ScreenKey(Vector2 screen) =>
        string.Create(CultureInfo.InvariantCulture, $"{Whole(screen.X)}x{Whole(screen.Y)}");

    /// <summary>Whether a press that moved by <paramref name="moved"/> px is a drag, past a dead zone of <paramref name="deadZone"/> px.</summary>
    public static bool IsDrag(Vector2 moved, float deadZone) =>
        float.IsFinite(moved.X) && float.IsFinite(moved.Y) && moved.LengthSquared() > MathF.Max(0f, deadZone) * MathF.Max(0f, deadZone);

    /// <summary>Where the icon's centre goes on a screen it was never placed on: a quarter across, under the top edge.</summary>
    public static Vector2 DefaultCentre(Vector2 screen, float diameter, float scale) =>
        new(screen.X * DefaultXShare, (DefaultYLogical * Safe(scale)) + (diameter * 0.5f));

    /// <summary>
    /// The icon's centre kept on screen: <paramref name="margin"/> px inside every edge, and clear of a toolbar (its top
    /// edge at the bar's height + margin under a top bar, its bottom edge at the bar's top − margin over a bottom bar). A
    /// screen too small for the icon puts it at the top left corner's margin. A position that is not a finite number
    /// reads as the screen's centre.
    /// </summary>
    public static Vector2 Clamp(Vector2 centre, float diameter, Vector2 screen, float margin, ToolbarClearance toolbar)
    {
        var half = MathF.Max(0f, Safe(diameter, 0f)) * 0.5f;
        var w = MathF.Max(0f, Safe(screen.X, 0f));
        var h = MathF.Max(0f, Safe(screen.Y, 0f));
        var m = MathF.Max(0f, Safe(margin, 0f));
        var x = float.IsFinite(centre.X) ? centre.X : w * 0.5f;
        var y = float.IsFinite(centre.Y) ? centre.Y : h * 0.5f;

        var top = m + half;
        var bottom = h - m - half;
        var room = toolbar.Room;
        if (toolbar.Edge == ToolbarEdge.Top)
        {
            top += room;
        }
        else if (toolbar.Edge == ToolbarEdge.Bottom)
        {
            bottom -= room;
        }

        var left = m + half;
        var right = w - m - half;
        x = right < left ? left : Math.Clamp(x, left, right);
        y = bottom < top ? top : Math.Clamp(y, top, bottom);
        return new Vector2(x, y);
    }

    /// <summary>
    /// Where the icon's centre is on <paramref name="screen"/>: the place saved for that screen size (or the default for a
    /// size it was never placed on), clamped on screen and clear of the toolbar. The toolbar's clearance is applied on
    /// top and never written back, so the saved place comes back once the bar goes.
    /// </summary>
    public static Vector2 Resolve(IReadOnlyDictionary<string, MoonIconPlace>? places, Vector2 screen, float diameter, float scale, ToolbarClearance toolbar) =>
        Place(places is not null && places.TryGetValue(ScreenKey(screen), out var place) ? place : null, screen, diameter, scale, toolbar);

    /// <summary>
    /// <see cref="Resolve"/> for the place already looked up under this screen's <see cref="ScreenKey"/> (null when none
    /// was saved), so a caller that keeps the key builds no string per frame.
    /// </summary>
    public static Vector2 Place(MoonIconPlace? saved, Vector2 screen, float diameter, float scale, ToolbarClearance toolbar)
    {
        var centre = saved is { } place ? new Vector2(place.X, place.Y) : DefaultCentre(screen, diameter, scale);
        return Clamp(centre, diameter, screen, EdgeMarginLogical * Safe(scale), toolbar);
    }

    /// <summary>
    /// Saves where the player dropped the icon on <paramref name="screen"/>, kept on screen but without the toolbar's
    /// clearance, so other screen sizes keep their own places. Returns the place saved.
    /// </summary>
    public static MoonIconPlace Store(IDictionary<string, MoonIconPlace> places, Vector2 screen, Vector2 centre, float diameter, float scale)
    {
        ArgumentNullException.ThrowIfNull(places);
        var kept = Clamp(centre, diameter, screen, EdgeMarginLogical * Safe(scale), ToolbarClearance.None);
        var place = new MoonIconPlace(kept.X, kept.Y);
        places[ScreenKey(screen)] = place;
        return place;
    }

    /// <summary>
    /// The quick card's top left beside the icon <paramref name="icon"/>: on the right when the screen has room there,
    /// else on the left, top-aligned with the icon and kept on screen vertically; with room on neither side, below the
    /// icon, else above it. It never covers the icon.
    /// </summary>
    public static Vector2 CardPlace(in ScreenRect icon, Vector2 card, in ScreenRect screen, float gap)
    {
        var g = MathF.Max(0f, Safe(gap, 0f));
        var y = MathF.Max(screen.Min.Y, MathF.Min(icon.Min.Y, screen.Max.Y - card.Y));
        var x = MathF.Max(screen.Min.X, MathF.Min(icon.Min.X, screen.Max.X - card.X));

        if (icon.Max.X + g + card.X <= screen.Max.X)
        {
            return new Vector2(icon.Max.X + g, y);
        }

        if (icon.Min.X - g - card.X >= screen.Min.X)
        {
            return new Vector2(icon.Min.X - g - card.X, y);
        }

        if (icon.Max.Y + g + card.Y <= screen.Max.Y)
        {
            return new Vector2(x, icon.Max.Y + g);
        }

        return new Vector2(x, icon.Min.Y - g - card.Y);
    }

    private static long Whole(float value) => float.IsFinite(value) ? (long)MathF.Round(MathF.Max(0f, value)) : 0;

    private static float Safe(float value, float fallback = 1f) => float.IsFinite(value) && value > 0f ? value : fallback;
}

/// <summary>
/// The moon icon's hover and press (spec-1.22 H1 and H2): a 2 px rise over <see cref="MotionTokens.HoverIn"/> with a
/// cool moonlight glow (#E2E8F4, .22 at its core, out to 1.4 radii) and a longer, softer shadow, as an object lifted
/// toward the light; back over <see cref="MotionTokens.HoverOut"/>. Quiet rises 1 px with a lighter glow (.7); Plain
/// draws no shadow or glow and rings the glyph in Text; Reduce motion keeps the icon still and shows the glow at once.
/// </summary>
public static class MoonIconHover
{
    /// <summary>The glow's colour, the moonlight of the v7 Completed moon: cool, so Ready keeps the only warm halo.</summary>
    public const uint GlowHex = 0xE2E8F4;

    /// <summary>The glow's alpha at its core at Full.</summary>
    public const float GlowCoreAlpha = 0.22f;

    /// <summary>How far the glow reaches, in icon radii.</summary>
    public const float GlowReach = 1.4f;

    /// <summary>The Plain hover ring's width, in logical px.</summary>
    public const float PlainRingLogical = 1.5f;

    /// <summary>How far the icon rises at full hover, in logical px: 2 at Full, 1 at Quiet, none at Plain or under Reduce motion.</summary>
    public static float RiseLogical(Flair flair, bool reduceMotion) => reduceMotion ? 0f : flair switch
    {
        Flair.Full => 2f,
        Flair.Quiet => 1f,
        _ => 0f,
    };

    /// <summary>The glow's strength at full hover (a factor on <see cref="GlowCoreAlpha"/>): 1 at Full, .7 at Quiet, none at Plain.</summary>
    public static float GlowStrength(Flair flair) => flair switch
    {
        Flair.Full => 1f,
        Flair.Quiet => 0.7f,
        _ => 0f,
    };

    /// <summary>Whether the icon draws a shadow (not at Plain, the flat glyph).</summary>
    public static bool Shadow(Flair flair) => flair != Flair.Plain;

    /// <summary>The shadow straight down at <paramref name="hover"/> (0 at rest, 1 hovered): offset and blur in logical px, and its alpha.</summary>
    public static (float Offset, float Blur, float Alpha) ShadowAt(float hover)
    {
        var h = Math.Clamp(float.IsFinite(hover) ? hover : 0f, 0f, 1f);
        return (2f + (2f * h), 2f + (1.6f * h), 0.50f - (0.08f * h));
    }

    /// <summary>
    /// The hover amount after <paramref name="delta"/> seconds toward <paramref name="hovered"/>: eased in over
    /// <see cref="MotionTokens.HoverIn"/> and out over <see cref="MotionTokens.HoverOut"/>, or at once under Reduce motion
    /// and at Plain.
    /// </summary>
    public static float Step(float current, bool hovered, float delta, Flair flair, bool reduceMotion)
    {
        var target = hovered ? 1f : 0f;
        if (reduceMotion || flair == Flair.Plain || !float.IsFinite(current))
        {
            return target;
        }

        return MotionMath.ApproachAsym(current, target, MotionTokens.RateFor(MotionTokens.HoverIn), MotionTokens.RateFor(MotionTokens.HoverOut), delta);
    }
}

/// <summary>
/// "Right-click for options" (spec-1.22 H1, owner decision 5): shown once after the icon first appears, for
/// <see cref="Seconds"/> or until the player clicks. <see cref="Seen"/> turns true the first time it shows, so the caller
/// saves it and it never shows again.
/// </summary>
public sealed class MoonIconHint(bool seen)
{
    /// <summary>How long the hint stays.</summary>
    public const double Seconds = 8.0;

    private double shownAt = double.NaN;
    private bool over;

    /// <summary>Whether the hint has ever shown (the saved flag).</summary>
    public bool Seen { get; private set; } = seen;

    /// <summary>
    /// Advances the hint at <paramref name="now"/>: it starts the first time the icon shows (unless it was seen), and ends
    /// after <see cref="Seconds"/> or on a click. Returns whether it shows this frame; <paramref name="becameSeen"/> is
    /// true on the frame it first shows, when the caller saves.
    /// </summary>
    public bool Update(double now, bool iconShown, bool clicked, out bool becameSeen)
    {
        becameSeen = false;
        if (over)
        {
            return false;
        }

        if (double.IsNaN(shownAt))
        {
            if (Seen || !iconShown)
            {
                return false;
            }

            shownAt = now;
            Seen = true;
            becameSeen = true;
        }

        if (clicked || !double.IsFinite(now) || now - shownAt >= Seconds || now < shownAt)
        {
            over = true;
            return false;
        }

        return iconShown;
    }
}

/// <summary>
/// The menu's Hide icon and its Undo (spec-1.22 H1, decision 11): hiding shows "Moon icon hidden · Undo · /tsuki icon
/// shows it again" for <see cref="SafetyRules.UndoSeconds"/> (the pointer resting on it stops the clock); Undo shows the
/// icon again while the toast is up. Showing the icon some other way (Settings, <c>/tsuki icon</c>) takes the toast down.
/// </summary>
public sealed class MoonIconHideUndo
{
    private readonly UndoTimer timer = new();

    /// <summary>Whether the toast is up.</summary>
    public bool Showing => timer.Showing;

    /// <summary>When the toast started, on the caller's clock (its fade reads its age).</summary>
    public double StartedAt => timer.StartedAt;

    /// <summary>The icon was hidden from its menu at <paramref name="now"/>: the toast starts.</summary>
    public void Hidden(double now) => timer.Start(now, SafetyRules.UndoSeconds);

    /// <summary>Undo: true (show the icon again) while the toast is up, which it takes down; false once it has gone.</summary>
    public bool TryUndo()
    {
        if (!timer.Showing)
        {
            return false;
        }

        timer.Stop();
        return true;
    }

    /// <summary>
    /// Advances the toast to <paramref name="now"/>; <paramref name="paused"/> while the pointer rests on it. An icon shown
    /// again some other way ends it. Returns whether it is still up.
    /// </summary>
    public bool Tick(double now, bool paused, bool iconEnabled)
    {
        if (iconEnabled)
        {
            timer.Stop();
        }

        return timer.Tick(now, paused);
    }
}
