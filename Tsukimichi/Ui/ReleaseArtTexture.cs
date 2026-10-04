using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Dalamud.Utility;

namespace Tsukimichi.Ui;

/// <summary>
/// The What's new popup's one release picture (spec-1.22 W2, "Cost"): at most one texture at a time, held only while the
/// popup is open, inside the 12 MB budget. The JPEG is rented from Dalamud's shared cache
/// (<see cref="ISharedImmediateTexture.RentAsync"/>, decoded off the framework thread, as <c>ThemeAtlasCache</c> does);
/// while the band fits the 1x tier (<see cref="Core.Releases.ReleaseArt.TierFor"/>) it is scaled to 560 × 220 on the GPU
/// (<see cref="ITextureProvider.CreateFromExistingTextureAsync"/>) and the full picture let go, so the popup holds 0.5 MB
/// at UI scale 1 and the full 1.9 MB only above it. Another page's picture replaces it, and closing the popup releases
/// it. A released texture is disposed <see cref="RetireFrames"/> frames later, never in a frame that could still draw it.
/// A picture that fails to load is logged once and treated as missing: the band shows its flat sky. Framework thread only.
/// </summary>
internal sealed class ReleaseArtTexture : IDisposable
{
    /// <summary>Frames a released texture is kept before it is disposed.</summary>
    private const int RetireFrames = 3;

    private readonly ITextureProvider textures;
    private readonly IPluginLog log;
    private readonly List<(IDalamudTextureWrap Wrap, int Frame)> retired = [];
    private readonly HashSet<string> failed = new(StringComparer.OrdinalIgnoreCase);

    private string? path;
    private (int Width, int Height) tier;
    private Task<IDalamudTextureWrap>? rent;
    private Task<IDalamudTextureWrap>? scaling;
    private IDalamudTextureWrap? wrap;
    private double landedAt;

    public ReleaseArtTexture(ITextureProvider textures, IPluginLog log)
    {
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// Asks for the picture at <paramref name="wanted"/> (null for none), kept at <paramref name="size"/>. Another
    /// picture or size than the one held releases it; the new one starts loading off the framework thread.
    /// </summary>
    public void Want(string? wanted, (int Width, int Height) size)
    {
        if (string.Equals(wanted, path, StringComparison.OrdinalIgnoreCase) && (wanted is null || size == tier))
        {
            return;
        }

        Release();
        if (wanted is null || failed.Contains(wanted))
        {
            return;
        }

        path = wanted;
        tier = size;
        try
        {
            rent = textures.GetFromFile(wanted).RentAsync();
        }
        catch (Exception ex)
        {
            Fail(wanted, ex);
        }
    }

    /// <summary>
    /// The picture once it has loaded, and how far its fade in has run (0 to 1, over <paramref name="fadeSeconds"/>; 1
    /// at once when <paramref name="fadeSeconds"/> is 0). Null while it loads, when none is wanted, or when it failed.
    /// </summary>
    public IDalamudTextureWrap? Current(float fadeSeconds, out float fade)
    {
        fade = 0f;
        Land();
        if (wrap is null)
        {
            return null;
        }

        fade = fadeSeconds > 0f ? Math.Clamp((float)((ImGui.GetTime() - landedAt) / fadeSeconds), 0f, 1f) : 1f;
        return wrap;
    }

    /// <summary>Takes a finished load: the full picture, scaled down to the tier when it is smaller, then the copy.</summary>
    private void Land()
    {
        if (rent is { IsCompleted: true } loaded)
        {
            rent = null;
            if (!loaded.IsCompletedSuccessfully)
            {
                Fail(path, loaded.Exception?.GetBaseException());
                return;
            }

            var full = loaded.Result;
            if (full.Width <= tier.Width && full.Height <= tier.Height)
            {
                Take(full);
                return;
            }

            try
            {
                // The copy owns the full picture from here: Dalamud disposes it once the copy is made.
                scaling = textures.CreateFromExistingTextureAsync(full, new TextureModificationArgs { NewWidth = tier.Width, NewHeight = tier.Height }, leaveWrapOpen: false, debugName: "Tsukimichi What's new");
            }
            catch (Exception ex)
            {
                full.Dispose();
                Fail(path, ex);
            }
        }

        if (scaling is { IsCompleted: true } scaled)
        {
            scaling = null;
            if (scaled.IsCompletedSuccessfully)
            {
                Take(scaled.Result);
            }
            else
            {
                Fail(path, scaled.Exception?.GetBaseException());
            }
        }
    }

    private void Take(IDalamudTextureWrap texture)
    {
        wrap = texture;
        landedAt = ImGui.GetTime();
    }

    /// <summary>Lets the picture go: its texture is disposed a few frames later, and a load still running disposes its result when it lands.</summary>
    public void Release()
    {
        if (wrap is { } held)
        {
            retired.Add((held, ImGui.GetFrameCount()));
        }

        Abandon();
    }

    /// <summary>Once a frame, open or not: disposes the textures released more than <see cref="RetireFrames"/> frames ago.</summary>
    public void Tick()
    {
        var frame = ImGui.GetFrameCount();
        for (var i = retired.Count - 1; i >= 0; i--)
        {
            if (frame - retired[i].Frame > RetireFrames)
            {
                DisposeWrap(retired[i].Wrap);
                retired.RemoveAt(i);
            }
        }
    }

    /// <summary>On unload: everything held or retired is disposed now.</summary>
    public void Dispose()
    {
        if (wrap is { } held)
        {
            DisposeWrap(held);
        }

        Abandon();
        foreach (var (old, _) in retired)
        {
            DisposeWrap(old);
        }

        retired.Clear();
    }

    /// <summary>Forgets the picture; loads still running dispose what they bring.</summary>
    private void Abandon()
    {
        if (rent is { } running)
        {
            _ = running.ToContentDisposedTask(true);
        }

        if (scaling is { } copying)
        {
            _ = copying.ToContentDisposedTask(true);
        }

        wrap = null;
        rent = null;
        scaling = null;
        path = null;
    }

    private void Fail(string? file, Exception? error)
    {
        if (file is not null)
        {
            failed.Add(file);
        }

        log.Warning("What's new: {File} could not be loaded ({Error}); the page shows no picture", file ?? "a picture", error?.Message ?? "cancelled");
        Abandon();
    }

    private void DisposeWrap(IDalamudTextureWrap texture)
    {
        try
        {
            texture.Dispose();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "What's new: a release picture could not be disposed");
        }
    }
}
