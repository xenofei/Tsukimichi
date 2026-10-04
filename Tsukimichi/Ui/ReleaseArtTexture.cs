using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Tsukimichi.Core.Releases;

namespace Tsukimichi.Ui;

/// <summary>
/// The What's new popup's release pictures (spec-1.22 W2, "Cost"): the page's, and while a page change cross-fades the
/// outgoing one (<see cref="ReleaseArtSlots{T}"/>), so at most two textures, held only while the popup is open, inside
/// the 12 MB budget (<see cref="Core.Releases.ReleaseArt.HeldBytes"/>). The JPEG is rented from Dalamud's shared cache
/// (<see cref="ISharedImmediateTexture.RentAsync"/>, decoded off the framework thread, as <c>ThemeAtlasCache</c> does);
/// while the band fits the 1x tier (<see cref="Core.Releases.ReleaseArt.TierFor"/>) it is scaled to 560 × 220 on the GPU
/// (<see cref="ITextureProvider.CreateFromExistingTextureAsync"/>) and the full picture let go, so the popup holds 0.5 MB
/// at UI scale 1 and the full 1.9 MB only above it. Another page's picture moves the shown one to the outgoing slot, kept
/// until the new one lands or the cross-fade has run; closing the popup releases both. A released texture is disposed
/// <see cref="RetireFrames"/> frames later, never in a frame that could still draw it. A picture that fails to load is
/// logged once and treated as missing: the band shows its flat sky. Framework thread only.
/// </summary>
internal sealed class ReleaseArtTexture : IDisposable
{
    /// <summary>Frames a released texture is kept before it is disposed.</summary>
    private const int RetireFrames = 3;

    private readonly ITextureProvider textures;
    private readonly IPluginLog log;
    private readonly List<(IDalamudTextureWrap Wrap, int Frame)> retired = [];
    private readonly HashSet<string> failed = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReleaseArtSlots<IDalamudTextureWrap> slots = new();

    private string? path;
    private (int Width, int Height) tier;
    private Task<IDalamudTextureWrap>? rent;
    private Task<IDalamudTextureWrap>? scaling;
    private double landedAt;

    public ReleaseArtTexture(ITextureProvider textures, IPluginLog log)
    {
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// Asks for the picture at <paramref name="wanted"/> (null for none), kept at <paramref name="size"/>. Another
    /// picture or size than the one held moves the shown one to the outgoing slot (<see cref="Leaving"/>); the new one
    /// starts loading off the framework thread.
    /// </summary>
    public void Want(string? wanted, (int Width, int Height) size)
    {
        if (string.Equals(wanted, path, StringComparison.OrdinalIgnoreCase) && (wanted is null || size == tier))
        {
            return;
        }

        Retire(slots.Replace());
        Abandon();
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
        if (slots.Current is not { } shown)
        {
            return null;
        }

        fade = fadeSeconds > 0f ? Math.Clamp((float)((ImGui.GetTime() - landedAt) / fadeSeconds), 0f, 1f) : 1f;
        return shown;
    }

    /// <summary>
    /// The previous page's picture while it fades out (after <see cref="Current"/>, which lets it go once the new one has
    /// landed); <paramref name="faded"/> (the page's cross-fade has run) lets it go now. Null when none is leaving.
    /// </summary>
    public IDalamudTextureWrap? Leaving(bool faded)
    {
        if (faded)
        {
            Retire(slots.Retire());
            return null;
        }

        return slots.Outgoing;
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

    /// <summary>The new picture is the page's; the outgoing one is let go now that it has something to fade over.</summary>
    private void Take(IDalamudTextureWrap texture)
    {
        var (old, outgoing) = slots.Land(texture);
        Retire(old);
        Retire(outgoing);
        landedAt = ImGui.GetTime();
    }

    /// <summary>Lets both pictures go: their textures are disposed a few frames later, and a load still running disposes its result when it lands.</summary>
    public void Release()
    {
        var (shown, outgoing) = slots.Clear();
        Retire(shown);
        Retire(outgoing);
        Abandon();
    }

    /// <summary>A picture let go is disposed <see cref="RetireFrames"/> frames from now (<see cref="Tick"/>).</summary>
    private void Retire(IDalamudTextureWrap? texture)
    {
        if (texture is not null)
        {
            retired.Add((texture, ImGui.GetFrameCount()));
        }
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
        var (shown, outgoing) = slots.Clear();
        if (shown is not null)
        {
            DisposeWrap(shown);
        }

        if (outgoing is not null)
        {
            DisposeWrap(outgoing);
        }

        Abandon();
        foreach (var (old, _) in retired)
        {
            DisposeWrap(old);
        }

        retired.Clear();
    }

    /// <summary>Forgets the picture asked for; loads still running dispose what they bring. The slots keep theirs.</summary>
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
