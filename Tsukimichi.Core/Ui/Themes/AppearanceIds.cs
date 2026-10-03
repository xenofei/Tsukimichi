namespace Tsukimichi.Core.Ui.Themes;

// The appearance's ids (feature plan v7 T1; docs/research/plan-v7/theme-system.md §3, §5.3). The configuration stores the
// string keys of GlyphSets, FrameKits, PaletteChoices and ThemePresets, so its JSON stays readable and survives
// reordering; the numbers below exist for share codes (1.17 T12), which pack them in four bits each. They are fixed
// forever, 0 is never an id (a share code's "from theme"), and ThemeRegistryTests pins every one against its key.

/// <summary>A set of the eight state glyphs.</summary>
public enum GlyphSetId : byte
{
    /// <summary>Menphina's Medallion (1.12), the default.</summary>
    Medallion = 1,

    /// <summary>The 1.11 moons; whole theme only.</summary>
    Classic = 2,

    /// <summary>Moonstone in a silver bezel.</summary>
    AetherCrystal = 3,

    /// <summary>The Holy See's rose window.</summary>
    IshgardGlass = 4,

    /// <summary>A Sharlayan instrument (1.17).</summary>
    Orrery = 5,

    /// <summary>Tsukimi crests in sumi and cut gold leaf (1.17).</summary>
    Sumi = 6,
}

/// <summary>A frame kit: the metal of medal rims, badges and gauges, and the Decoration ornament.</summary>
public enum FrameKitId : byte
{
    /// <summary>The Moon Road's gilt brass, as shipped.</summary>
    Brass = 1,

    /// <summary>Moonstone silver, for Aether Crystal.</summary>
    Silver = 2,

    /// <summary>Lead came with a gilt line on act-now, for Ishgard Glass.</summary>
    Came = 3,

    /// <summary>Astrolabe brass with a hairline scale (1.17).</summary>
    Astrolabe = 4,

    /// <summary>Cut gold leaf over sumi (1.17).</summary>
    Kirikane = 5,
}

/// <summary>A UI colour palette: surfaces, text, accent and state inks (the colours themselves are plan v7 T2 and T8).</summary>
public enum PaletteId : byte
{
    /// <summary>The Night palette, the default.</summary>
    Night = 1,

    /// <summary>The hour before sunrise, a dark palette (1.17).</summary>
    Dawn = 2,

    /// <summary>The first light palette.</summary>
    IshgardSnow = 3,

    /// <summary>Black lacquer with a vermilion dusk (1.17).</summary>
    KuganeLacquer = 4,

    /// <summary>Mapped from the user's Dalamud style each frame (the old "Follow Dalamud colours").</summary>
    FollowDalamud = 5,
}

/// <summary>A named preset of glyph set, frame kit and palette.</summary>
public enum ThemeId : byte
{
    Medallion = 1,
    Classic = 2,
    AetherCrystal = 3,
    IshgardGlass = 4,
    Orrery = 5,
    Sumi = 6,
}

/// <summary>How a glyph set draws.</summary>
public enum GlyphRenderKind : byte
{
    /// <summary>Drawn in code (Medallion's meshes and embedded hero atlas, Classic's 1.11 moons).</summary>
    Procedural,

    /// <summary>Pre-rendered atlases loaded from disk per set (docs/design/v7/themes/ATLAS-CONTRACT.md).</summary>
    Atlas,
}
