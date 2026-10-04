using System.Numerics;
using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// The Follow Umbra palette (plan v8 M3, decision 4; spec-1.22 M3 and decision 17): Tsukimichi's palette roles mapped
/// from the player's Umbra colour profile, read-only from Umbra's saved settings (<see cref="UmbraSettings"/>), with the
/// same safeguards as Follow Dalamud, and Night when the profile can't be read or can't be made to read. Pure.
/// <para>
/// <b>The mapping.</b> Umbra's roles stand in for the Dalamud style's: the window ← <c>Window.Background</c>, cards ←
/// <c>Window.BackgroundLight</c> (else <c>Widget.Background</c>), hovered rows ← <c>Widget.BackgroundHover</c> (Umbra's
/// <c>Input.BackgroundHover</c> is an inverted fill on its light profiles, so it is not used), lines ←
/// <c>Window.Border</c>, text ← <c>Window.Text</c>, tertiary ← <c>Window.TextMuted</c> (else <c>Window.TextDisabled</c>),
/// each translucent colour laid over the window. Gold keeps its meaning of act now, so Umbra's accent never becomes
/// Tsukimichi's gold; it is not used.
/// </para>
/// <para>
/// <b>The clamp.</b> Follow Dalamud already pushes every derived ink to 4.5 : 1 on every surface and every line and
/// stripe to 3 : 1. The one role it takes as given is the text itself, so a profile whose text barely reads on its own
/// background has its text pushed towards white (dark window) or black (light window) until it reads at 4.5 : 1 on
/// every surface. The inks Follow Dalamud only meets on its own hosts are re-made for Umbra's: the destructive button's
/// text (black or white, whichever reads), the tree ring, and a light window's gauge keyline, track and flat arc (each
/// pushed towards the text to 3 : 1 on the window). If the palette still misses any pair (<see cref="UiPalette.TextPairs"/>,
/// <see cref="UiPalette.LinePairs"/>), Night is used instead.
/// </para>
/// </summary>
public static class UmbraPalette
{
    /// <summary>The palette's key and name (it is drawn with Follow Dalamud's id: a palette built from another program's colours).</summary>
    public const string Key = "umbra";

    /// <summary>The palette's name in English.</summary>
    public const string Name = "Follow Umbra";

    /// <summary>The roles read from Umbra's profile, for docs and tests.</summary>
    public static readonly IReadOnlyList<string> Roles =
    [
        "Window.Background", "Window.BackgroundLight", "Widget.Background", "Widget.BackgroundHover",
        "Window.Border", "Window.Text", "Window.TextMuted", "Window.TextDisabled",
    ];

    /// <summary>
    /// The palette for <paramref name="profile"/>, or Night (with <paramref name="clamped"/> false) when there is none,
    /// it lacks the window or text colour, or even the clamped palette misses a contrast pair.
    /// </summary>
    /// <param name="profile">The colour profile in use; null when unread.</param>
    /// <param name="clamped">Whether the text had to be pushed to read (the profile's own text did not).</param>
    /// <param name="fellBack">Whether Night is returned in its place.</param>
    public static UiPalette From(UmbraColorProfile? profile, out bool clamped, out bool fellBack)
    {
        clamped = false;
        fellBack = true;
        if (profile is null || !profile.Has("Window.Background") || !profile.Has("Window.Text"))
        {
            return UiPalettes.Night;
        }

        var window = ColorMath.Opaque(profile.Get("Window.Background", Vector4.One));
        var raised = Pick(profile, "Window.BackgroundLight", "Widget.Background", window);
        var hover = profile.Get("Widget.BackgroundHover", window);
        var border = profile.Get("Window.Border", ColorMath.Mix(window, Vector4.One, 0.2f));
        var text = ColorMath.Over(profile.Get("Window.Text", Vector4.One), window);
        var muted = ColorMath.Over(Pick(profile, "Window.TextMuted", "Window.TextDisabled", ColorMath.Mix(text, window, 0.5f)), window);

        // The text must read on every surface the palette derives (window, cards, wells, hovered rows).
        var host = SurfaceColors.FromHost(window, raised, hover, border, text, muted);
        var extreme = host.Light ? new Vector4(0f, 0f, 0f, 1f) : Vector4.One;
        var readable = Readable(text, host, extreme);
        if (readable != text)
        {
            // Push past the bare minimum towards black or white, so the derived secondary and tertiary inks have room.
            readable = Readable(ColorMath.Mix(readable, extreme, 0.25f), host, extreme);
            clamped = true;
        }

        var palette = Repair(UiPalettes.FollowDalamud(window, raised, hover, border, readable, muted) with { Key = Key, Name = Name });
        if (!Passes(palette) || !Passes(palette.HighContrast))
        {
            clamped = false;
            return UiPalettes.Night;
        }

        fellBack = false;
        return palette;
    }

    /// <summary>Whether every text pair reads at 4.5 : 1 and every line pair at 3 : 1 (PaletteContrastTests' bar).</summary>
    public static bool Passes(UiPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        foreach (var (_, ink, ground) in palette.TextPairs())
        {
            if (ColorMath.Contrast(ink, ground) < ColorMath.AaText)
            {
                return false;
            }
        }

        foreach (var (_, ink, ground) in palette.LinePairs())
        {
            if (ColorMath.Contrast(ink, ground) < ColorMath.AaNonText)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <paramref name="text"/> pushed towards <paramref name="extreme"/> (black on a light window, white on a dark one)
    /// until it reads at 4.5 : 1 on the window, cards, wells and hovered rows; itself when it already does.
    /// </summary>
    private static Vector4 Readable(Vector4 text, in SurfaceColors s, Vector4 extreme)
    {
        for (var pass = 0; pass < 4; pass++)
        {
            var worst = s.Window;
            var worstRatio = ColorMath.Contrast(text, worst);
            foreach (var ground in (ReadOnlySpan<Vector4>)[s.Raised, s.Hover, s.Sunken])
            {
                var ratio = ColorMath.Contrast(text, ground);
                if (ratio < worstRatio)
                {
                    (worst, worstRatio) = (ground, ratio);
                }
            }

            if (worstRatio >= SurfaceColors.TextMinContrast)
            {
                break;
            }

            text = ColorMath.EnsureContrast(text, extreme, worst, SurfaceColors.TextMinContrast);
        }

        return text;
    }

    private static Vector4 Pick(UmbraColorProfile profile, string role, string fallbackRole, Vector4 fallback) =>
        profile.Has(role) ? profile.Get(role, fallback) : profile.Get(fallbackRole, fallback);

    /// <summary>
    /// The inks Follow Dalamud tunes for its own hosts, re-made for any window: the destructive button's text (black or
    /// white, whichever reads better, when the mapped one does not), the tree ring and, on a light window, the gauges'
    /// keyline, track and flat arc, each at 3 : 1 on the window. Everything else is Follow Dalamud's.
    /// </summary>
    private static UiPalette Repair(UiPalette palette)
    {
        var s = palette.Surface;
        var inks = palette.Inks;
        var button = ColorMath.Over(inks.Danger with { W = PaletteInks.DangerButtonAlpha }, s.Window);
        if (!ColorMath.ReadsAsText(inks.OnDanger, button))
        {
            var black = new Vector4(0f, 0f, 0f, 1f);
            inks = inks with { OnDanger = ColorMath.Contrast(black, button) >= ColorMath.Contrast(Vector4.One, button) ? black : Vector4.One };
        }

        // The tree ring and the flat gauges are UI graphics: 3 : 1 on the window, whatever grey Umbra's window is.
        inks = inks with
        {
            GaugeArc = ColorMath.EnsureContrast(inks.GaugeArc, s.Text, s.Window, SurfaceColors.LineMinContrast),
            GaugeDone = ColorMath.EnsureContrast(inks.GaugeDone, s.Text, s.Window, SurfaceColors.LineMinContrast),
        };

        // A light window's gauges carry their own opaque ink (a dark one keeps Night's medal material and its strong line).
        var gauges = palette.Gauges;
        if (palette.IsLight)
        {
            const float line = SurfaceColors.LineMinContrast;
            gauges = gauges with
            {
                Keyline = ColorMath.EnsureContrast(gauges.Keyline, s.Text, s.Window, line),
                Arc = ColorMath.EnsureContrast(gauges.Arc, s.Text, s.Window, line),
                Track = ColorMath.EnsureContrast(gauges.Track, s.Text, s.Window, line),
            };
        }

        return palette with { Inks = inks, Gauges = gauges };
    }
}
