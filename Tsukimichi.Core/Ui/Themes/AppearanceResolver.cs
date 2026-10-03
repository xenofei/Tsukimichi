using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// An appearance resolved for drawing (theme-system §5.3): the glyph set of each of the eight states, the palette, the
/// frame kit and the high-contrast flag, with every unknown or unavailable key already replaced by the theme's own choice.
/// Immutable; <see cref="AppearanceCache"/> keeps one per saved appearance so a frame allocates nothing.
/// </summary>
public sealed class ResolvedAppearance
{
    private readonly GlyphSetId[] sets;

    internal ResolvedAppearance(ThemePreset theme, GlyphSetId[] sets, PaletteId palette, FrameKitId frames, bool highContrast, IReadOnlyList<string> unknown)
    {
        Theme = theme;
        this.sets = sets;
        Palette = palette;
        Frames = frames;
        HighContrast = highContrast;
        Unknown = unknown;

        var used = 0;
        var mixed = false;
        foreach (var set in sets)
        {
            used |= 1 << (int)set;
            mixed |= set != theme.Glyphs;
        }

        usedMask = used;
        IsMixed = mixed;
    }

    private readonly int usedMask;

    /// <summary>The default appearance: Menphina's Medallion on Night with Brass, standard contrast.</summary>
    public static ResolvedAppearance Default { get; } = AppearanceResolver.Resolve(new AppearanceConfig());

    /// <summary>The theme in effect (the default theme when the saved key is unknown).</summary>
    public ThemePreset Theme { get; }

    /// <summary>The palette in effect (<see cref="PaletteId.FollowDalamud"/> maps the Dalamud style).</summary>
    public PaletteId Palette { get; }

    /// <summary>The frame kit in effect.</summary>
    public FrameKitId Frames { get; }

    /// <summary>Whether the shared high-contrast set and the palette's high-contrast form draw.</summary>
    public bool HighContrast { get; }

    /// <summary>Whether the Classic (1.11) moons draw everything, the gauges included.</summary>
    public bool Classic => Theme.Id == ThemeId.Classic;

    /// <summary>Whether any state draws from a set other than the theme's own.</summary>
    public bool IsMixed { get; }

    /// <summary>The saved keys this build did not know or could not use, as "field: key" (for a one-time notice); empty when none.</summary>
    public IReadOnlyList<string> Unknown { get; }

    /// <summary>The glyph set that draws <paramref name="state"/>.</summary>
    public GlyphSetId SetFor(QuestState state) => sets[AppearanceStates.Index(state)];

    /// <summary>Whether any state draws from <paramref name="set"/>.</summary>
    public bool Uses(GlyphSetId set) => (usedMask & (1 << (int)set)) != 0;

    /// <summary>The 1.15 Moon style this appearance amounts to (written back for a downgrade, and read by the 1.11 gauges' switch).</summary>
    public MoonStyle MoonStyle => Classic ? MoonStyle.Classic : MoonStyle.Medallion;

    /// <summary>The 1.15 Moon colours setting this appearance amounts to.</summary>
    public GlyphPaletteKind GlyphPalette => HighContrast ? GlyphPaletteKind.HighContrast : GlyphPaletteKind.Standard;

    /// <summary>Whether the windows take the user's Dalamud colours (the 1.15 Follow Dalamud colours toggle).</summary>
    public bool FollowDalamud => Palette == PaletteId.FollowDalamud;

    /// <summary>The medals' finish at Decoration <paramref name="flair"/> (<see cref="FlairRules.Medal"/>).</summary>
    public MedalFinish FinishFor(Flair flair) => FlairRules.Medal(flair, MoonStyle);
}

/// <summary>
/// Resolves a saved appearance (theme-system §5.3). Pure, and tested. The rules, in order:
/// <list type="number">
/// <item>An unknown or not-offered theme key reads as the default theme.</item>
/// <item>Classic is whole theme only: every state draws Classic and any mix is ignored.</item>
/// <item>High contrast draws one shared low-vision set, Medallion's shapes on the high-contrast ladder, whatever the
/// theme or mix (theme-system §3.4); the mix comes back when it is turned off.</item>
/// <item>A state's override counts when it names an offered, mixable set; otherwise the theme's set draws it.</item>
/// <item>The palette and frames overrides count when they name an offered choice; otherwise the theme's.</item>
/// </list>
/// </summary>
public static class AppearanceResolver
{
    /// <summary>Resolves <paramref name="config"/>; null resolves as the default appearance.</summary>
    public static ResolvedAppearance Resolve(AppearanceConfig? config)
    {
        config ??= new AppearanceConfig();
        List<string>? unknown = null;

        var theme = ThemePresets.Default;
        if (ThemePresets.TryGet(config.Theme, out var named) && named.Offered)
        {
            theme = named;
        }
        else
        {
            Note(ref unknown, "theme", config.Theme);
        }

        var sets = new GlyphSetId[AppearanceStates.Count];
        var themeSet = Usable(GlyphSets.Get(theme.Glyphs)) ? theme.Glyphs : GlyphSetId.Medallion;
        Array.Fill(sets, theme.Id == ThemeId.Classic ? GlyphSetId.Classic : config.HighContrast ? GlyphSetId.Medallion : themeSet);

        if (config.Glyphs is { Count: > 0 } glyphs)
        {
            foreach (var (stateKey, setKey) in glyphs)
            {
                if (!AppearanceStates.TryParse(stateKey, out var state))
                {
                    Note(ref unknown, "state", stateKey);
                    continue;
                }

                if (!GlyphSets.TryGet(setKey, out var set) || !set.Offered || !set.Mixable)
                {
                    Note(ref unknown, AppearanceStates.Key(state), setKey);
                    continue;
                }

                if (theme.Id != ThemeId.Classic && !config.HighContrast)
                {
                    sets[AppearanceStates.Index(state)] = set.Id;
                }
            }
        }

        var palette = theme.Palette;
        if (config.Palette is not null)
        {
            if (PaletteChoices.TryGet(config.Palette, out var p) && p.Offered)
            {
                palette = p.Id;
            }
            else
            {
                Note(ref unknown, "palette", config.Palette);
            }
        }

        if (!PaletteChoices.Get(palette).Offered)
        {
            palette = PaletteId.Night;
        }

        var frames = theme.Frames;
        if (config.Frames is not null)
        {
            if (FrameKits.TryGet(config.Frames, out var kit) && kit.Offered)
            {
                frames = kit.Id;
            }
            else
            {
                Note(ref unknown, "frames", config.Frames);
            }
        }

        if (!FrameKits.Get(frames).Offered)
        {
            frames = FrameKitId.Brass;
        }

        return new ResolvedAppearance(theme, sets, palette, frames, config.HighContrast, unknown is null ? [] : unknown);
    }

    private static bool Usable(GlyphSetInfo set) => set.Offered;

    private static void Note(ref List<string>? unknown, string field, string? key) =>
        (unknown ??= []).Add($"{field}: {key ?? "(none)"}");
}

/// <summary>
/// The resolved appearance for the saved one, rebuilt only when the saved one changes (theme-system §5.3: resolved once,
/// cached, nothing allocated per frame). <see cref="Get"/> compares the saved fields with the snapshot it last resolved,
/// a few string comparisons a frame.
/// </summary>
public sealed class AppearanceCache
{
    private AppearanceConfig? snapshot;
    private ResolvedAppearance resolved = ResolvedAppearance.Default;

    /// <summary>How many times the appearance has been resolved (for tests).</summary>
    public int Resolutions { get; private set; }

    /// <summary>The resolved form of <paramref name="config"/>, from the cache when it has not changed.</summary>
    public ResolvedAppearance Get(AppearanceConfig? config)
    {
        config ??= Empty;
        if (snapshot is not null && snapshot.SameAs(config))
        {
            return resolved;
        }

        resolved = AppearanceResolver.Resolve(config);
        snapshot = config.Clone();
        Resolutions++;
        return resolved;
    }

    private static readonly AppearanceConfig Empty = new();
}
