using System;

namespace Tsukimichi.Config;

/// <summary>
/// The portrait pack's settings. 1.20 to 1.22 offered the pack as a download (and 1.22 once, on first run); the photos
/// now ship with the plugin, so the offer's flag is only read to drop it, and Giver portraits' new default, Game art +
/// photos, is applied once to configurations saved before (<see cref="ApplyBundledPhotos"/>).
/// </summary>
public sealed partial class Configuration
{
    /// <summary>
    /// 1.22's "the first-run portrait pack offer was answered". The offer is gone (the photos ship with the plugin), so an
    /// old file's value is read and dropped: write-only, so it is never saved again.
    /// </summary>
    [Newtonsoft.Json.JsonProperty]
    [Obsolete("The portrait pack is no longer offered: its photos ship with the plugin. Read only to drop the old value.")]
    public bool PortraitPackOfferAnswered
    {
        set { }
    }

    /// <summary>Whether <see cref="ApplyBundledPhotos"/> has run for this configuration.</summary>
    public bool BundledPhotosApplied { get; set; }

    /// <summary>
    /// Once, when the photos first ship with the plugin: a configuration saved before, on Game art (the old default),
    /// moves to Game art + photos (the new one) unless the player had downloaded the pack (a <c>current.json</c> in the
    /// config folder's <c>portraits/</c>) and still chose Game art, which was then their choice. Off stays Off.
    /// </summary>
    internal static void ApplyBundledPhotos(Configuration config, bool hadFile, string downloadedPackDir)
    {
        if (config.BundledPhotosApplied)
        {
            return;
        }

        config.BundledPhotosApplied = true;
        config.GiverPortraits = Core.Portraits.BundledPortraits.ModeOnceBundled(config.GiverPortraits, hadFile, Core.Portraits.BundledPortraits.HasDownloadedPack(downloadedPackDir));
    }
}
