using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The filter drawer's surfaces at one <see cref="Flair"/> level (plan v7 UI-2, docs/design/v7/ui/spec.md §2.2): at
/// Full a raised gradient sheet under a brass edge, at Quiet a flat sheet one tone above the tree with a hairline edge,
/// at Plain the ledger's flat sheet with a 1 px line and its header and footer bands. On the Night palette they are the
/// design's own hexes; under "Follow Dalamud colours" and the high-contrast palette the same roles are mixed from the
/// palette in effect, as <see cref="FlairTones"/> does, so a light host gets a light sheet.
/// </summary>
/// <param name="SheetTop">The sheet's fill at its top.</param>
/// <param name="SheetFoot">The sheet's fill at its foot (the same as the top where the sheet is flat).</param>
/// <param name="Edge">The 1 px edge on the right and bottom (Quiet and Plain; Full draws brass).</param>
/// <param name="Pill">The neutral count pills ("3 on", "2 set").</param>
/// <param name="HeaderBand">The header's band (Plain); the sheet itself elsewhere.</param>
/// <param name="FooterBand">The footer's band (Plain); the sheet itself elsewhere.</param>
/// <param name="Hover">A row's or the Reset action's hover wash (drawn at <see cref="HoverAlpha"/>).</param>
/// <param name="Heading">The section heads' ink: OrnamentLight (GiltLight) at Full, the text tone elsewhere.</param>
public readonly record struct DrawerTones(
    Vector4 SheetTop,
    Vector4 SheetFoot,
    Vector4 Edge,
    Vector4 Pill,
    Vector4 HeaderBand,
    Vector4 FooterBand,
    Vector4 Hover,
    Vector4 Heading)
{
    /// <summary>Full on Night: the sheet's top and foot.</summary>
    public const uint FullTopHex = 0x212742;
    public const uint FullFootHex = 0x171C2F;

    /// <summary>The sheet's opacity at Full: a hair under opaque, so the sky is felt, never read.</summary>
    public const float FullSheetAlpha = 0.995f;

    /// <summary>Quiet on Night: the flat sheet, one tone above the tree, and its edge.</summary>
    public const uint QuietSheetHex = 0x1A2135;
    public const uint QuietEdgeHex = 0x2E3650;

    /// <summary>Plain on Night: the flat sheet, its edge, the header band and the footer band.</summary>
    public const uint PlainSheetHex = 0x151A25;
    public const uint PlainEdgeHex = 0x303648;
    public const uint PlainHeaderHex = 0x1A1F2C;
    public const uint PlainFooterHex = 0x0D1018;

    /// <summary>The neutral pill on Night: a count asks for nothing, so it is never gold.</summary>
    public const uint PillHex = 0x262D42;

    /// <summary>The hover wash on Night (NightHover), drawn at <see cref="HoverAlpha"/>.</summary>
    public const uint HoverHex = 0x262D45;

    /// <summary>How strong the hover wash is drawn.</summary>
    public const float HoverAlpha = 0.8f;

    /// <summary>The designed tones on Night at each level: the Night palette's <see cref="Themes.UiPalette.DrawerTones"/>.</summary>
    public static readonly Themes.DrawerToneSet NightSet = new(
        For(Flair.Full, SurfaceColors.Night),
        For(Flair.Quiet, SurfaceColors.Night),
        For(Flair.Plain, SurfaceColors.Night));

    /// <summary>
    /// The drawer's tones for <paramref name="flair"/> in <paramref name="palette"/>: its designed tones when it has them
    /// (Night's hexes), otherwise mixed from its surface. Allocates nothing.
    /// </summary>
    public static DrawerTones For(Flair flair, Themes.UiPalette palette) =>
        palette.DrawerTones is { } designed ? designed.For(flair) : Build(flair, palette.Surface, night: false);

    /// <summary>The drawer's tones for <paramref name="flair"/> on <paramref name="surface"/>: Night's hexes on the Night surface, else mixed.</summary>
    public static DrawerTones For(Flair flair, in SurfaceColors surface) => Build(flair, surface, surface == SurfaceColors.Night);

    private static DrawerTones Build(Flair flair, in SurfaceColors surface, bool night)
    {
        var s = surface;
        var down = s.Light ? s.Text : new Vector4(0f, 0f, 0f, 1f);
        var pill = night ? ColorMath.FromHex(PillHex) : ColorMath.Mix(s.Raised, s.Text, 0.08f);
        var hover = night ? ColorMath.FromHex(HoverHex) : s.Hover;
        switch (flair)
        {
            case Flair.Full:
            {
                var top = night ? ColorMath.FromHex(FullTopHex) : ColorMath.Mix(s.Raised, s.Text, 0.02f);
                var foot = night ? ColorMath.FromHex(FullFootHex) : ColorMath.Mix(s.Raised, down, s.Light ? 0.03f : 0.25f);
                return new DrawerTones(top, foot, s.Line, pill, top, foot, hover, s.OrnamentLight);
            }

            case Flair.Quiet:
            {
                var sheet = night ? ColorMath.FromHex(QuietSheetHex) : ColorMath.Mix(s.Window, s.Text, 0.05f);
                var edge = night ? ColorMath.FromHex(QuietEdgeHex) : s.Line;
                return new DrawerTones(sheet, sheet, edge, pill, sheet, sheet, hover, s.Text);
            }

            default:
            {
                var sheet = night ? ColorMath.FromHex(PlainSheetHex) : ColorMath.Mix(s.Window, s.Text, 0.02f);
                var edge = night ? ColorMath.FromHex(PlainEdgeHex) : s.StrongLine with { W = 1f };
                var header = night ? ColorMath.FromHex(PlainHeaderHex) : ColorMath.Mix(s.Window, s.Text, 0.05f);
                var footer = night ? ColorMath.FromHex(PlainFooterHex) : ColorMath.Mix(s.Window, down, s.Light ? 0.03f : 0.3f);
                return new DrawerTones(sheet, sheet, edge, pill, header, footer, hover, s.Text);
            }
        }
    }
}
