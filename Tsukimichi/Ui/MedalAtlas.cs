using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The bundled hero-size medal atlas (feature plan v6 G1; docs/design/moon-v6/round5/gen_atlas.py): the round 5 medals
/// rendered from their SVG masters at four tiers, embedded as <c>Tsukimichi.assets.ui.medals.png</c> (782 × 574) and
/// <c>medals@2x.png</c> (1564 × 1148), loaded once each through <see cref="ITextureProvider.GetFromManifestResource"/>
/// as <see cref="OrnamentAtlas"/> loads the ornaments. <see cref="MedalLayout.Pick"/> chooses the tier, so a medal is
/// never shrunk by more than 1.5× (image textures have one mip level). The 2x texture is only asked for when a medal
/// is drawn above 128 px. Until a texture has loaded, <see cref="TryDraw"/> returns false and the caller draws the
/// vector medal instead.
/// </summary>
public static class MedalAtlas
{
    private static ITextureProvider? provider;
    private static ISharedImmediateTexture? atlas1x;
    private static ISharedImmediateTexture? atlas2x;

    /// <summary>Sets the texture provider; optional, since the plugin's own service is used when none is set.</summary>
    public static void Initialize(ITextureProvider textures)
    {
        provider = textures ?? throw new ArgumentNullException(nameof(textures));
        atlas1x = atlas2x = null;
    }

    /// <summary>Whether the 1x atlas has loaded.</summary>
    public static bool IsReady => TryGetWrap(twoX: false, out _);

    /// <summary>
    /// Draws <paramref name="sprite"/> filling <paramref name="min"/>..<paramref name="max"/> (the medal's 128-unit box),
    /// tinted by <paramref name="tint"/>; false, with nothing drawn, while the texture it needs is still loading.
    /// </summary>
    public static bool TryDraw(ImDrawListPtr dl, MedalSprite sprite, Vector2 min, Vector2 max, uint tint = 0xFFFFFFFFu)
    {
        var (tier, twoX) = MedalLayout.Pick(max.X - min.X);
        if (!TryGetWrap(twoX, out var wrap))
        {
            // While the 2x texture loads, the 1x one's largest tier stands in (never the other way: that would load 2x early).
            if (!twoX || !TryGetWrap(twoX: false, out wrap))
            {
                return false;
            }

            tier = MedalLayout.Tiers[^1];
        }

        var (u0, v0, u1, v1) = MedalLayout.Uv(MedalLayout.Rect(sprite, tier));
        dl.AddImage(wrap.Handle, min, max, new Vector2(u0, v0), new Vector2(u1, v1), tint);
        return true;
    }

    /// <summary>The texture of one atlas (for the glyph window's colour-vision copies); null while it loads.</summary>
    internal static IDalamudTextureWrap? Wrap(bool twoX) => TryGetWrap(twoX, out var wrap) ? wrap : null;

    private static bool TryGetWrap(bool twoX, out IDalamudTextureWrap wrap)
    {
        var textures = provider ?? Plugin.TextureProvider;
        if (textures is null)
        {
            wrap = null!;
            return false;
        }

        if (twoX)
        {
            atlas2x ??= textures.GetFromManifestResource(typeof(MedalAtlas).Assembly, MedalLayout.ResourceName2x);
            return atlas2x.TryGetWrap(out wrap!, out _);
        }

        atlas1x ??= textures.GetFromManifestResource(typeof(MedalAtlas).Assembly, MedalLayout.ResourceName1x);
        return atlas1x.TryGetWrap(out wrap!, out _);
    }
}
