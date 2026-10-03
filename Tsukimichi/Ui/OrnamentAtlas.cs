using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>Which corner of a frame a corner mark sits in; the atlas holds the top-left one and the others flip its UVs.</summary>
public enum FrameCorner
{
    TopLeft,
    TopRight,
    BottomRight,
    BottomLeft,
}

/// <summary>
/// The bundled ornament atlas (Moon Road proposal §5, §6.4): the crest, the corner mark, the sigil star, the divider's
/// phases and the seventeen gap glyphs, embedded as <c>Tsukimichi.assets.ui.ornaments.png</c> (256 × 128) and
/// <c>ornaments@2x.png</c> (512 × 256). Both load once through <see cref="ITextureProvider.GetFromManifestResource"/>
/// (Dalamud's shared texture cache); the UVs come from the const table in <see cref="OrnamentLayout"/>. A draw picks
/// the 2x texture when the sprite is drawn above 1.25× its 1x size, so glyphs stay crisp at every UI and icon scale.
/// Nothing allocates per frame. Until the textures have loaded (or if they fail), glyphs draw a NightRaised square and
/// corner marks fall back to lines.
/// </summary>
public static class OrnamentAtlas
{
    private const float TwoXThreshold = 1.25f;

    private static ITextureProvider? provider;
    private static ISharedImmediateTexture? atlas1x;
    private static ISharedImmediateTexture? atlas2x;

    /// <summary>
    /// Sets the texture provider; optional, since the plugin's own service is used when none is set. Tests and tools
    /// that draw without the plugin call this first.
    /// </summary>
    public static void Initialize(ITextureProvider textures)
    {
        provider = textures ?? throw new ArgumentNullException(nameof(textures));
        atlas1x = atlas2x = null;
    }

    /// <summary>Whether the 1x atlas has loaded (the 2x may still be loading).</summary>
    public static bool IsReady => TryGetWrap(false, out _);

    /// <summary>Draws a gap glyph into <paramref name="min"/>..<paramref name="max"/>; false (after drawing the placeholder) while the atlas loads.</summary>
    public static bool Draw(ImDrawListPtr dl, OrnamentGlyph glyph, Vector2 min, Vector2 max, uint tint = 0xFFFFFFFFu)
    {
        if (glyph == OrnamentGlyph.None)
        {
            return false;
        }

        return DrawRect(dl, OrnamentLayout.Glyph(glyph), min, max, tint, placeholder: true);
    }

    /// <summary>Draws one of the non-glyph sprites (crest, corner mark, sigil, divider phases); false while the atlas loads.</summary>
    public static bool Draw(ImDrawListPtr dl, OrnamentSprite sprite, Vector2 min, Vector2 max, uint tint = 0xFFFFFFFFu) =>
        DrawRect(dl, OrnamentLayout.Sprite(sprite), min, max, tint, placeholder: false);

    /// <summary>
    /// One corner mark in the box <paramref name="min"/>..<paramref name="max"/> (the mark's own square), turned to
    /// <paramref name="corner"/> by flipping UVs. Without the atlas it draws the same L and inner echo with lines.
    /// </summary>
    public static void Corner(ImDrawListPtr dl, Vector2 min, Vector2 max, FrameCorner corner, uint tint = 0xFFFFFFFFu)
    {
        var rect = OrnamentLayout.Sprite(OrnamentSprite.CornerMark);
        var size = max - min;
        if (TryGetWrap(size.X > rect.Width * TwoXThreshold, out var wrap))
        {
            var (u0, v0) = rect.Uv0;
            var (u1, v1) = rect.Uv1;
            var flipX = corner is FrameCorner.TopRight or FrameCorner.BottomRight;
            var flipY = corner is FrameCorner.BottomLeft or FrameCorner.BottomRight;
            var uv0 = new Vector2(flipX ? u1 : u0, flipY ? v1 : v0);
            var uv1 = new Vector2(flipX ? u0 : u1, flipY ? v0 : v1);
            dl.AddImage(wrap.Handle, min, max, uv0, uv1, tint);
            return;
        }

        // Fallback: the outer L (1 px) and the inner echo, in the tint.
        var s = size.X;
        var t = MathF.Max(1f, s / 12f);
        var (ox, oy, dx, dy) = corner switch
        {
            FrameCorner.TopLeft => (min.X, min.Y, 1f, 1f),
            FrameCorner.TopRight => (max.X, min.Y, -1f, 1f),
            FrameCorner.BottomRight => (max.X, max.Y, -1f, -1f),
            _ => (min.X, max.Y, 1f, -1f),
        };
        var o = new Vector2(ox + dx * s * 0.125f, oy + dy * s * 0.125f);
        dl.AddLine(o, o + new Vector2(dx * s * 0.83f, 0f), tint, t);
        dl.AddLine(o, o + new Vector2(0f, dy * s * 0.83f), tint, t);
        var i = new Vector2(ox + dx * s * 0.33f, oy + dy * s * 0.33f);
        var echo = ((uint)((tint >> 24) * 0.6f) << 24) | (tint & 0x00FFFFFFu);
        dl.AddLine(i, i + new Vector2(dx * s * 0.375f, 0f), echo, t * 0.6f);
        dl.AddLine(i, i + new Vector2(0f, dy * s * 0.375f), echo, t * 0.6f);
    }

    /// <summary>
    /// The four corner marks of a frame (the hero banner, one card per pane): marks of <paramref name="size"/> px inset
    /// <paramref name="inset"/> px from each corner of <paramref name="frameMin"/>..<paramref name="frameMax"/>.
    /// </summary>
    public static void Corners(ImDrawListPtr dl, Vector2 frameMin, Vector2 frameMax, float size, float inset, uint tint = 0xFFFFFFFFu)
    {
        var s = new Vector2(size, size);
        var a = frameMin + new Vector2(inset, inset);
        var b = frameMax - new Vector2(inset, inset);
        Corner(dl, a, a + s, FrameCorner.TopLeft, tint);
        Corner(dl, new Vector2(b.X - size, a.Y), new Vector2(b.X, a.Y + size), FrameCorner.TopRight, tint);
        Corner(dl, b - s, b, FrameCorner.BottomRight, tint);
        Corner(dl, new Vector2(a.X, b.Y - size), new Vector2(a.X + size, b.Y), FrameCorner.BottomLeft, tint);
    }

    private static bool DrawRect(ImDrawListPtr dl, AtlasRect rect, Vector2 min, Vector2 max, uint tint, bool placeholder)
    {
        if (rect.Width == 0)
        {
            return false;
        }

        if (!TryGetWrap(max.X - min.X > rect.Width * TwoXThreshold, out var wrap))
        {
            if (placeholder)
            {
                dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Raised), (max.X - min.X) * 0.15f);
            }

            return false;
        }

        var (u0, v0) = rect.Uv0;
        var (u1, v1) = rect.Uv1;
        dl.AddImage(wrap.Handle, min, max, new Vector2(u0, v0), new Vector2(u1, v1), tint);
        return true;
    }

    /// <summary>The 2x atlas when asked for and loaded, else the 1x one; false while neither has loaded.</summary>
    private static bool TryGetWrap(bool preferTwoX, out IDalamudTextureWrap wrap)
    {
        var textures = provider ?? Plugin.TextureProvider;
        if (textures is null)
        {
            wrap = null!;
            return false;
        }

        if (preferTwoX)
        {
            atlas2x ??= textures.GetFromManifestResource(typeof(OrnamentAtlas).Assembly, OrnamentLayout.ResourceName2x);
            if (atlas2x.TryGetWrap(out var big, out _))
            {
                wrap = big;
                return true;
            }
        }

        atlas1x ??= textures.GetFromManifestResource(typeof(OrnamentAtlas).Assembly, OrnamentLayout.ResourceName1x);
        if (atlas1x.TryGetWrap(out var small, out _))
        {
            wrap = small;
            return true;
        }

        wrap = null!;
        return false;
    }
}
