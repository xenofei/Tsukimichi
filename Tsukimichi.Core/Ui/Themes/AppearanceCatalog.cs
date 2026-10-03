namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// A glyph set (theme-system §3.2): its stable key (the configuration's word for it, and its atlas folder under
/// <c>assets/ui/themes/</c>), how it draws, the kit and palette it was designed with, whether it can be mixed with other
/// sets state by state (Classic cannot: it predates the shared state grammar), whether it has a flat finish of its own for
/// Decoration Plain, and whether this build offers it (a set still in its design round is registered but not offered).
/// </summary>
/// <param name="Name">The set's proper name in English, for logs and tests; the Settings page takes its label from Strings.</param>
public sealed record GlyphSetInfo(
    GlyphSetId Id,
    string Key,
    string Name,
    GlyphRenderKind Kind,
    FrameKitId DefaultFrames,
    PaletteId DefaultPalette,
    bool Mixable,
    bool HasPlainFinish,
    bool Offered);

/// <summary>A frame kit (theme-system §3.3); see <see cref="GlyphSetInfo"/> for the fields.</summary>
public sealed record FrameKitInfo(FrameKitId Id, string Key, string Name, bool Offered);

/// <summary>A UI palette choice (theme-system §8); <paramref name="Light"/> for a light palette.</summary>
public sealed record PaletteInfo(PaletteId Id, string Key, string Name, bool Light, bool Offered);

/// <summary>
/// A theme (theme-system §3.1): a named preset of the three axes. <paramref name="Legacy"/> marks Classic, which is whole
/// theme only and labelled "Legacy" on the Themes page.
/// </summary>
public sealed record ThemePreset(ThemeId Id, string Key, string Name, GlyphSetId Glyphs, FrameKitId Frames, PaletteId Palette, bool Legacy, bool Offered);

/// <summary>The registered glyph sets, in the order the Themes page lists them.</summary>
public static class GlyphSets
{
    public static readonly GlyphSetInfo Medallion = new(GlyphSetId.Medallion, "medallion", "Menphina's Medallion", GlyphRenderKind.Procedural, FrameKitId.Brass, PaletteId.Night, Mixable: true, HasPlainFinish: true, Offered: true);
    public static readonly GlyphSetInfo Classic = new(GlyphSetId.Classic, "classic", "Classic", GlyphRenderKind.Procedural, FrameKitId.Brass, PaletteId.Night, Mixable: false, HasPlainFinish: true, Offered: true);
    public static readonly GlyphSetInfo AetherCrystal = new(GlyphSetId.AetherCrystal, "aether-crystal", "Aether Crystal", GlyphRenderKind.Atlas, FrameKitId.Silver, PaletteId.Night, Mixable: true, HasPlainFinish: true, Offered: true);
    public static readonly GlyphSetInfo IshgardGlass = new(GlyphSetId.IshgardGlass, "ishgard-glass", "Ishgard Glass", GlyphRenderKind.Atlas, FrameKitId.Came, PaletteId.IshgardSnow, Mixable: true, HasPlainFinish: true, Offered: true);
    public static readonly GlyphSetInfo Orrery = new(GlyphSetId.Orrery, "astrologian-orrery", "Astrologian's Orrery", GlyphRenderKind.Atlas, FrameKitId.Astrolabe, PaletteId.Dawn, Mixable: true, HasPlainFinish: true, Offered: false);
    public static readonly GlyphSetInfo Sumi = new(GlyphSetId.Sumi, "sumi-to-kinpaku", "Sumi to Kinpaku", GlyphRenderKind.Atlas, FrameKitId.Kirikane, PaletteId.KuganeLacquer, Mixable: true, HasPlainFinish: true, Offered: false);

    /// <summary>Every registered set, offered or not.</summary>
    public static readonly IReadOnlyList<GlyphSetInfo> All = [Medallion, Classic, AetherCrystal, IshgardGlass, Orrery, Sumi];

    /// <summary>The set with id <paramref name="id"/>; Medallion for an id this build does not know.</summary>
    public static GlyphSetInfo Get(GlyphSetId id) => Catalog.ById(All, id, static s => s.Id) ?? Medallion;

    /// <summary>The set with <paramref name="key"/> (case-insensitive); false for an unknown key.</summary>
    public static bool TryGet(string? key, out GlyphSetInfo set) => Catalog.TryByKey(All, key, static s => s.Key, out set);
}

/// <summary>The registered frame kits.</summary>
public static class FrameKits
{
    public static readonly FrameKitInfo Brass = new(FrameKitId.Brass, "brass", "Brass", Offered: true);
    public static readonly FrameKitInfo Silver = new(FrameKitId.Silver, "silver", "Silver", Offered: true);
    public static readonly FrameKitInfo Came = new(FrameKitId.Came, "came", "Lead came", Offered: true);
    public static readonly FrameKitInfo Astrolabe = new(FrameKitId.Astrolabe, "astrolabe", "Astrolabe", Offered: false);
    public static readonly FrameKitInfo Kirikane = new(FrameKitId.Kirikane, "kirikane", "Kirikane", Offered: false);

    public static readonly IReadOnlyList<FrameKitInfo> All = [Brass, Silver, Came, Astrolabe, Kirikane];

    /// <summary>The kit with id <paramref name="id"/>; Brass for an id this build does not know.</summary>
    public static FrameKitInfo Get(FrameKitId id) => Catalog.ById(All, id, static k => k.Id) ?? Brass;

    /// <inheritdoc cref="GlyphSets.TryGet"/>
    public static bool TryGet(string? key, out FrameKitInfo kit) => Catalog.TryByKey(All, key, static k => k.Key, out kit);
}

/// <summary>The registered palettes (their colours are plan v7 T2 and T8; this is only what an appearance can name).</summary>
public static class PaletteChoices
{
    public static readonly PaletteInfo Night = new(PaletteId.Night, "night", "Night", Light: false, Offered: true);
    public static readonly PaletteInfo Dawn = new(PaletteId.Dawn, "dawn", "Dawn", Light: false, Offered: false);
    public static readonly PaletteInfo IshgardSnow = new(PaletteId.IshgardSnow, "ishgard-snow", "Ishgard Snow", Light: true, Offered: true);
    public static readonly PaletteInfo KuganeLacquer = new(PaletteId.KuganeLacquer, "kugane-lacquer", "Kugane Lacquer", Light: false, Offered: false);

    /// <summary>The user's Dalamud style; light or dark as the style is, so <see cref="PaletteInfo.Light"/> is false here.</summary>
    public static readonly PaletteInfo FollowDalamud = new(PaletteId.FollowDalamud, "dalamud", "Follow Dalamud", Light: false, Offered: true);

    public static readonly IReadOnlyList<PaletteInfo> All = [Night, Dawn, IshgardSnow, KuganeLacquer, FollowDalamud];

    /// <summary>The palette with id <paramref name="id"/>; Night for an id this build does not know.</summary>
    public static PaletteInfo Get(PaletteId id) => Catalog.ById(All, id, static p => p.Id) ?? Night;

    /// <inheritdoc cref="GlyphSets.TryGet"/>
    public static bool TryGet(string? key, out PaletteInfo palette) => Catalog.TryByKey(All, key, static p => p.Key, out palette);
}

/// <summary>The themes (theme-system §3.1 table).</summary>
public static class ThemePresets
{
    public static readonly ThemePreset Medallion = new(ThemeId.Medallion, "medallion", "Menphina's Medallion", GlyphSetId.Medallion, FrameKitId.Brass, PaletteId.Night, Legacy: false, Offered: true);
    public static readonly ThemePreset AetherCrystal = new(ThemeId.AetherCrystal, "aether-crystal", "Aether Crystal", GlyphSetId.AetherCrystal, FrameKitId.Silver, PaletteId.Night, Legacy: false, Offered: true);
    public static readonly ThemePreset IshgardGlass = new(ThemeId.IshgardGlass, "ishgard-glass", "Ishgard Glass", GlyphSetId.IshgardGlass, FrameKitId.Came, PaletteId.IshgardSnow, Legacy: false, Offered: true);
    public static readonly ThemePreset Orrery = new(ThemeId.Orrery, "astrologian-orrery", "Astrologian's Orrery", GlyphSetId.Orrery, FrameKitId.Astrolabe, PaletteId.Dawn, Legacy: false, Offered: false);
    public static readonly ThemePreset Sumi = new(ThemeId.Sumi, "sumi-to-kinpaku", "Sumi to Kinpaku", GlyphSetId.Sumi, FrameKitId.Kirikane, PaletteId.KuganeLacquer, Legacy: false, Offered: false);
    public static readonly ThemePreset Classic = new(ThemeId.Classic, "classic", "Classic", GlyphSetId.Classic, FrameKitId.Brass, PaletteId.Night, Legacy: true, Offered: true);

    /// <summary>
    /// Every registered theme, in the Themes page's order: Menphina's Medallion, Ishgard Glass, Aether Crystal (the
    /// approved Themes design puts Glass second; the critic ranked it first), the 1.17 themes, and Classic last, as Legacy.
    /// </summary>
    public static readonly IReadOnlyList<ThemePreset> All = [Medallion, IshgardGlass, AetherCrystal, Orrery, Sumi, Classic];

    /// <summary>The default theme.</summary>
    public static ThemePreset Default => Medallion;

    /// <summary>The theme with id <paramref name="id"/>; the default for an id this build does not know.</summary>
    public static ThemePreset Get(ThemeId id) => Catalog.ById(All, id, static t => t.Id) ?? Default;

    /// <inheritdoc cref="GlyphSets.TryGet"/>
    public static bool TryGet(string? key, out ThemePreset theme) => Catalog.TryByKey(All, key, static t => t.Key, out theme);
}

/// <summary>Lookups shared by the registries (a handful of entries each, so a scan beats a dictionary).</summary>
internal static class Catalog
{
    public static T? ById<T, TId>(IReadOnlyList<T> all, TId id, Func<T, TId> idOf)
        where T : class
        where TId : struct, Enum
    {
        for (var i = 0; i < all.Count; i++)
        {
            if (EqualityComparer<TId>.Default.Equals(idOf(all[i]), id))
            {
                return all[i];
            }
        }

        return null;
    }

    public static bool TryByKey<T>(IReadOnlyList<T> all, string? key, Func<T, string> keyOf, out T found)
        where T : class
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            var trimmed = key.AsSpan().Trim();
            for (var i = 0; i < all.Count; i++)
            {
                if (trimmed.Equals(keyOf(all[i]), StringComparison.OrdinalIgnoreCase))
                {
                    found = all[i];
                    return true;
                }
            }
        }

        found = null!;
        return false;
    }
}
