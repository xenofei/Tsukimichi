namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// What Settings › Themes offers and how it lays out (plan v7 T9; docs/design/v7/ui/spec-1.16.md §B), pure so it is
/// tested without ImGui: the theme cards, the palette tiles, the frames choice, the card grid's columns, and what the
/// page previews (<see cref="ThemesPreview"/>).
/// </summary>
public static class ThemesPage
{
    /// <summary>The card grid's most columns (spec §B2: 3 per row; it wraps by width only).</summary>
    public const int MaxColumns = 3;

    private static readonly ThemePreset[] OfferedThemes = [.. ThemePresets.All.Where(static t => t.Offered)];
    private static readonly FrameKitInfo[] OfferedKits = [.. FrameKits.All.Where(static k => k.Offered)];

    /// <summary>
    /// The theme cards, in the registry's order: only the themes this build offers (Sumi stays hidden until its art
    /// ships, spec §B6), Classic last as Legacy.
    /// </summary>
    public static IReadOnlyList<ThemePreset> Themes => OfferedThemes;

    /// <summary>The frame kits this build offers, after "From theme" in the Frames choice.</summary>
    public static IReadOnlyList<FrameKitInfo> Kits => OfferedKits;

    /// <summary>
    /// The palette tiles: every palette the catalog offers that can draw, in the catalog's order. A designed palette
    /// shows once it is registered (<paramref name="designed"/>; Ishgard Snow arrives with T8); Follow Dalamud is built
    /// from the host style, so it always can.
    /// </summary>
    public static IReadOnlyList<PaletteInfo> Palettes(Func<PaletteId, bool> designed)
    {
        ArgumentNullException.ThrowIfNull(designed);
        var shown = new List<PaletteInfo>(PaletteChoices.All.Count);
        foreach (var palette in PaletteChoices.All)
        {
            if (palette.Offered && (palette.Id == PaletteId.FollowDalamud || designed(palette.Id)))
            {
                shown.Add(palette);
            }
        }

        return shown;
    }

    /// <summary>
    /// The palette a card or the preview draws <paramref name="palette"/> in: itself when it can draw
    /// (<paramref name="designed"/>), else Night, as the window would. Follow Dalamud draws from the host style.
    /// </summary>
    public static PaletteId Drawable(PaletteId palette, Func<PaletteId, bool> designed)
    {
        ArgumentNullException.ThrowIfNull(designed);
        return palette == PaletteId.FollowDalamud || designed(palette) ? palette : PaletteId.Night;
    }

    /// <summary>
    /// Whether the Frames choice can be used: at least two offered kits draw a metal of their own
    /// (<paramref name="ownMetal"/>). From 1.17 (T11) Brass, Silver, Lead came and Astrolabe each do, so it is; a build
    /// with fewer shows each theme's own kit and says why it is fixed (spec-1.16 §B2 with the T9 brief).
    /// </summary>
    public static bool FramesChoosable(Func<FrameKitId, bool> ownMetal)
    {
        ArgumentNullException.ThrowIfNull(ownMetal);
        var count = 0;
        foreach (var kit in OfferedKits)
        {
            if (ownMetal(kit.Id))
            {
                count++;
            }
        }

        return count >= 2;
    }

    /// <summary>The Frames choice's index for <paramref name="config"/>: 0 for "From theme", else 1 + the kit's place in <see cref="Kits"/>.</summary>
    public static int FramesIndex(AppearanceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Frames is null || !FrameKits.TryGet(config.Frames, out var kit))
        {
            return 0;
        }

        var at = Array.FindIndex(OfferedKits, k => k.Id == kit.Id);
        return at < 0 ? 0 : at + 1;
    }

    /// <summary>The kit for a Frames choice index; null for "From theme" (0) or an index out of range.</summary>
    public static FrameKitInfo? KitAt(int index) => index >= 1 && index <= OfferedKits.Length ? OfferedKits[index - 1] : null;

    /// <summary>
    /// How many cards fit in a row of <paramref name="width"/> px: cells of <paramref name="cell"/> with
    /// <paramref name="gap"/> between them, at least 1 and at most <see cref="MaxColumns"/>.
    /// </summary>
    public static int Columns(float width, float cell, float gap)
    {
        if (!(cell > 0f) || !float.IsFinite(width))
        {
            return 1;
        }

        var fit = (int)MathF.Floor((width + MathF.Max(0f, gap)) / (cell + MathF.Max(0f, gap)));
        return Math.Clamp(fit, 1, MaxColumns);
    }

    /// <summary>How many rows <paramref name="count"/> cards take in <paramref name="columns"/> columns.</summary>
    public static int Rows(int count, int columns) => count <= 0 ? 0 : (count + Math.Max(1, columns) - 1) / Math.Max(1, columns);

    /// <summary>
    /// What clicking <paramref name="theme"/>'s card would save (<see cref="AppearanceEdits.ApplyTheme"/> on a copy of
    /// <paramref name="saved"/>): the theme with its own palette and frames, high contrast kept.
    /// </summary>
    public static AppearanceConfig WhatIf(AppearanceConfig saved, ThemePreset theme)
    {
        ArgumentNullException.ThrowIfNull(saved);
        var copy = saved.Clone();
        AppearanceEdits.ApplyTheme(copy, theme);
        return copy;
    }

    /// <summary>Whether <paramref name="theme"/> is the one in use (the saved theme, overrides or not).</summary>
    public static bool InUse(ResolvedAppearance saved, ThemePreset theme)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(theme);
        return saved.Theme.Id == theme.Id;
    }
}

/// <summary>
/// The Themes page's preview (spec-1.16 §B2, §B3): each card's appearance (its theme as a click would save it, with the
/// user's high contrast), resolved once and kept until high contrast changes, and which appearance the Preview panel
/// draws: the hovered card's, or the saved one. Hovering previews; nothing is saved until a click.
/// </summary>
public sealed class ThemesPreview
{
    private readonly ResolvedAppearance?[] cards = new ResolvedAppearance?[16];
    private bool cardsHighContrast;

    /// <summary>How many card appearances have been resolved (for tests: hovering does not resolve every frame).</summary>
    public int Resolutions { get; private set; }

    /// <summary>
    /// <paramref name="theme"/>'s card as a click on it would leave <paramref name="saved"/> (<see cref="ThemesPage.WhatIf"/>),
    /// from the cache unless high contrast changed.
    /// </summary>
    public ResolvedAppearance Card(AppearanceConfig saved, ThemePreset theme)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(theme);
        if (saved.HighContrast != cardsHighContrast)
        {
            Array.Clear(cards);
            cardsHighContrast = saved.HighContrast;
        }

        var slot = (int)theme.Id;
        if ((uint)slot >= (uint)cards.Length)
        {
            return AppearanceResolver.Resolve(ThemesPage.WhatIf(saved, theme));
        }

        if (cards[slot] is { } cached)
        {
            return cached;
        }

        Resolutions++;
        return cards[slot] = AppearanceResolver.Resolve(ThemesPage.WhatIf(saved, theme));
    }

    /// <summary>
    /// What the Preview panel draws this frame: the hovered card's appearance while one is hovered and it would change
    /// something (<see cref="PreviewTarget.Previewing"/>), else <paramref name="savedResolved"/>.
    /// </summary>
    public PreviewTarget Target(AppearanceConfig saved, ResolvedAppearance savedResolved, ThemePreset? hovered)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(savedResolved);
        if (hovered is null)
        {
            return new PreviewTarget(savedResolved, savedResolved.Theme, false);
        }

        var card = Card(saved, hovered);
        var same = hovered.Id == savedResolved.Theme.Id && !AppearanceEdits.IsCustom(saved);
        return same ? new PreviewTarget(savedResolved, savedResolved.Theme, false) : new PreviewTarget(card, hovered, true);
    }
}

/// <summary>What the Preview panel draws: <paramref name="Appearance"/> under <paramref name="Theme"/>'s name; <paramref name="Previewing"/> while a hovered card differs from the saved appearance.</summary>
public readonly record struct PreviewTarget(ResolvedAppearance Appearance, ThemePreset Theme, bool Previewing);
