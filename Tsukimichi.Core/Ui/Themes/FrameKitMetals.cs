using System.Numerics;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// Each frame kit's metal outside the medals (feature plan v7 T11; docs/design/v7/ui/spec-1.17.md §B1): the Decoration
/// ornament's ramp (card frames, corner marks, rules: <see cref="BrassTokens"/>) and the gauges' arc (the halo gauge, the
/// orbit ring: <see cref="GaugeInks"/>). Brass is the palette's own, exactly as shipped. The other kits recolour those two
/// on a dark palette from their resting ramp (spec §B1, lit upper left: highlight → body → shadow → deep), placed as
/// Brass's own ramp is (<see cref="KitMetal.FromRamp"/>). A light palette's gauge arc takes the palette's gauge ink (spec
/// §B1, spec-1.16 §A6) and its ornament stays the palette's, designed to read at 3 : 1 on snow; high contrast draws its
/// flat ladder. Allocation-free: every ramp is a static, and a gauge's other inks (groove, moon, knob) stay the palette's.
/// </summary>
public static class FrameKitMetals
{
    /// <summary>Moonstone silver (Aether Crystal's kit): #E2E8F4 → #A9B5D0 → #7B8AAF → #5E6E97.</summary>
    public static readonly KitMetal Silver = KitMetal.FromRamp(0xE2E8F4, 0xA9B5D0, 0x7B8AAF, 0x5E6E97);

    /// <summary>Lead came (Ishgard Glass's kit): #B8C0D0 → #8C95B0 → #5A6278 → #323950.</summary>
    public static readonly KitMetal Came = KitMetal.FromRamp(0xB8C0D0, 0x8C95B0, 0x5A6278, 0x323950);

    /// <summary>Astrolabe brass (Astrologian's Orrery's kit): #EAD3A0 → #B8924E → #7C6034 → #4E3B1E.</summary>
    public static readonly KitMetal Astrolabe = KitMetal.FromRamp(0xEAD3A0, 0xB8924E, 0x7C6034, 0x4E3B1E);

    /// <summary>
    /// Cut gold leaf (Sumi to Kinpaku's kit): its concept's leaf ramp, #F4DA92 → #DEB862 → #A98843 → #6E5426. The kit's
    /// metal is its leaf, not its ro-iro lacquer, which is near black and would vanish as a rule or an arc on a dark pane.
    /// </summary>
    public static readonly KitMetal Kirikane = KitMetal.FromRamp(0xF4DA92, 0xDEB862, 0xA98843, 0x6E5426);

    /// <summary>
    /// Whether <paramref name="kit"/> has a metal of its own (every kit: Brass, Silver, Came, Astrolabe and Kirikane).
    /// Settings › Themes offers the Frames choice once two offered kits do.
    /// </summary>
    public static bool HasOwnMetal(FrameKitId kit) => kit == FrameKitId.Brass || Of(kit) is not null;

    /// <summary>The metal of <paramref name="kit"/>, or null for Brass (the palette's own) and a kit without one.</summary>
    public static KitMetal? Of(FrameKitId kit) => kit switch
    {
        FrameKitId.Silver => Silver,
        FrameKitId.Came => Came,
        FrameKitId.Astrolabe => Astrolabe,
        FrameKitId.Kirikane => Kirikane,
        _ => null,
    };

    /// <summary>
    /// Whether <paramref name="kit"/> ships its Decoration ornament as sprites (<c>ornaments.png</c>, ATLAS-CONTRACT §8):
    /// Kirikane's crest sigil, lozenge and kamon corner. The other kits recolour the palette's drawn ornament.
    /// </summary>
    public static bool HasOrnamentSprites(FrameKitId kit) => kit == FrameKitId.Kirikane;

    /// <summary>
    /// Whether <paramref name="palette"/> draws <paramref name="kit"/>'s ornament sprites: a kit that has them, on a dark,
    /// standard-contrast palette (the sprites are gold leaf, measured on the dark windows; a light palette keeps its own
    /// ornament, designed for 3 : 1 on snow, and high contrast its strong line), as the metal recolours.
    /// </summary>
    public static bool DrawsOrnamentSprites(UiPalette palette, FrameKitId kit)
    {
        ArgumentNullException.ThrowIfNull(palette);
        return Recolours(palette) && HasOrnamentSprites(kit);
    }

    /// <summary>The ornament ramp <paramref name="palette"/> draws with <paramref name="kit"/>'s frames.</summary>
    public static BrassTokens Ornament(UiPalette palette, FrameKitId kit)
    {
        ArgumentNullException.ThrowIfNull(palette);
        return Recolours(palette) && Of(kit) is { } metal ? metal.Ornament : palette.Brass;
    }

    /// <summary>The gauge inks <paramref name="palette"/> draws with <paramref name="kit"/>'s frames: its arc in the kit's metal.</summary>
    public static GaugeInks Gauges(UiPalette palette, FrameKitId kit)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (!Recolours(palette) || Of(kit) is not { } metal)
        {
            return palette.Gauges;
        }

        return palette.Gauges with
        {
            ArcBase = metal.ArcBase,
            ArcDim = metal.ArcDim,
            OuterSlope = metal.OuterSlope,
            InnerSlope = metal.InnerSlope,
        };
    }

    /// <summary>Whether a kit's metal replaces the palette's: on a dark, standard-contrast palette.</summary>
    private static bool Recolours(UiPalette palette) => !palette.IsHighContrast && !palette.Surface.Light;
}

/// <summary>A kit's metal: its ornament ramp and its gauge arc (see <see cref="FrameKitMetals"/>).</summary>
/// <param name="Ornament">The Decoration ornament's ramp: highlight, body, shadow, reflected lift, deep, and the corner marks' lit and shaded tones.</param>
/// <param name="ArcBase">The gauge arc's base stroke.</param>
/// <param name="ArcDim">A finished gauge stepping back.</param>
/// <param name="OuterSlope">The arc's outer slope, upper left to lower right.</param>
/// <param name="InnerSlope">The arc's inner slope.</param>
public sealed record KitMetal(
    BrassTokens Ornament,
    Vector4 ArcBase,
    Vector4 ArcDim,
    (float At, Vector4 Color)[] OuterSlope,
    (float At, Vector4 Color)[] InnerSlope)
{
    /// <summary>
    /// A kit's metal from its four-stop resting ramp (spec-1.17 §B1), each token where Brass's own sits on Brass's ramp
    /// (#E2C78C → #A88B52 → #6E5732 → #5A4729): the reflected lift three quarters of the way from shadow back to body, the
    /// lit corner part-way to white, the shaded corner a step up from the body; the gauge arc's slopes on the ramp's stops
    /// as Night's are on the gilt's, its base near the highlight and its dim the shadow.
    /// </summary>
    public static KitMetal FromRamp(uint high, uint body, uint shadow, uint deep)
    {
        var h = ColorMath.FromHex(high);
        var b = ColorMath.FromHex(body);
        var s = ColorMath.FromHex(shadow);
        var d = ColorMath.FromHex(deep);
        var baseInk = Vector4.Lerp(b, h, 0.7f);
        return new KitMetal(
            new BrassTokens(h, b, s, Vector4.Lerp(s, b, 0.75f), d, Vector4.Lerp(h, Vector4.One, 0.45f), Vector4.Lerp(b, h, 0.3f)),
            ArcBase: baseInk,
            ArcDim: s,
            OuterSlope: [(0f, h), (0.42f, b), (0.78f, s), (1f, d)],
            InnerSlope: [(0f, d), (0.55f, s), (1f, baseInk)]);
    }
}
