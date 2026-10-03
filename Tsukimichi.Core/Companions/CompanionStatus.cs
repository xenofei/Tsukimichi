namespace Tsukimichi.Core.Companions;

/// <summary>Where a companion plugin stands in Dalamud's installed list.</summary>
public enum CompanionState
{
    /// <summary>Not installed under any of its internal names.</summary>
    Missing,

    /// <summary>Installed, but turned off (or failed to load).</summary>
    Disabled,

    /// <summary>Installed but older than the build Tsukimichi needs, or built for an older Dalamud API.</summary>
    Outdated,

    /// <summary>Loaded and recent enough: what it unlocks in Tsukimichi works.</summary>
    Loaded,
}

/// <summary>One entry of Dalamud's installed plugin list, as much of it as the registry reads.</summary>
/// <param name="InternalName">The manifest's internal name.</param>
/// <param name="Version">The installed build's version.</param>
/// <param name="IsLoaded">Dalamud has it loaded.</param>
/// <param name="IsOutdated">Dalamud flags it as built for an older API (it cannot load).</param>
public readonly record struct InstalledPlugin(string InternalName, Version? Version, bool IsLoaded, bool IsOutdated = false);

/// <summary>A companion's state as of the last read of the installed list.</summary>
/// <param name="Definition">Which companion.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Variant">The build found (the loaded one first); the recommended build when none is installed.</param>
/// <param name="InstalledVersion">The found build's version; null when missing or unknown.</param>
public sealed record CompanionStatus(CompanionDefinition Definition, CompanionState State, CompanionVariant Variant, Version? InstalledVersion)
{
    public CompanionPlugin Plugin => Definition.Plugin;

    /// <summary>What it unlocks in Tsukimichi works.</summary>
    public bool IsLoaded => State == CompanionState.Loaded;

    /// <summary>The name to show: the build found, else the recommended one.</summary>
    public string DisplayName => Variant.DisplayName;

    /// <summary>The oldest build Tsukimichi accepts for the build found; null when any build on the current API does.</summary>
    public Version? MinimumVersion => Variant.MinimumVersion;
}

/// <summary>
/// Turns Dalamud's installed plugin list into a <see cref="CompanionStatus"/> per companion. Pure, so the rules are
/// tested without Dalamud: among the builds installed under any of a companion's internal names (matched ignoring
/// case; a do-nothing placeholder, <see cref="CompanionVariant.PlaceholderFrom"/>, counts as not installed), a loaded
/// one wins over one that is not, an earlier variant over a later one. A loaded build below its
/// variant's minimum is <see cref="CompanionState.Outdated"/>, else <see cref="CompanionState.Loaded"/>. A build that is
/// not loaded is <see cref="CompanionState.Outdated"/> when Dalamud flags it as built for an older API or it is below
/// the minimum (enabling it would not help), else <see cref="CompanionState.Disabled"/>.
/// </summary>
public static class CompanionResolver
{
    public static CompanionStatus Resolve(CompanionDefinition definition, IReadOnlyList<InstalledPlugin> installed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(installed);

        (CompanionVariant Variant, InstalledPlugin Plugin)? best = null;
        foreach (var variant in definition.Variants)
        {
            foreach (var plugin in installed)
            {
                if (!variant.Recognises(plugin.InternalName, plugin.Version))
                {
                    continue;
                }

                // A loaded build beats any build that is not; otherwise the first found (earlier variant) stays.
                if (best is null || (plugin.IsLoaded && !best.Value.Plugin.IsLoaded))
                {
                    best = (variant, plugin);
                }
            }
        }

        if (best is not { } found)
        {
            return new CompanionStatus(definition, CompanionState.Missing, definition.Primary, null);
        }

        var tooOld = IsBelow(found.Plugin.Version, found.Variant.MinimumVersion);
        var state = found.Plugin.IsLoaded
            ? tooOld ? CompanionState.Outdated : CompanionState.Loaded
            : tooOld || found.Plugin.IsOutdated ? CompanionState.Outdated : CompanionState.Disabled;
        return new CompanionStatus(definition, state, found.Variant, found.Plugin.Version);
    }

    /// <summary>Every companion of <see cref="CompanionCatalog.All"/>, in its order.</summary>
    public static IReadOnlyList<CompanionStatus> ResolveAll(IReadOnlyList<InstalledPlugin> installed)
    {
        var result = new CompanionStatus[CompanionCatalog.All.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = Resolve(CompanionCatalog.All[i], installed);
        }

        return result;
    }

    /// <summary>
    /// The version is below the minimum. An unknown version passes (Dalamud always reports one; a dev build may say
    /// 0.0.0.0, read as unknown so a locally built plugin is not turned away).
    /// </summary>
    public static bool IsBelow(Version? version, Version? minimum)
    {
        if (minimum is null || version is null || version == new Version(0, 0, 0, 0) || version == new Version(0, 0))
        {
            return false;
        }

        return Normalize(version) < Normalize(minimum);
    }

    /// <summary>The version is known and at or above <paramref name="floor"/> (unset parts read as zero).</summary>
    public static bool IsAtLeast(Version? version, Version floor)
    {
        ArgumentNullException.ThrowIfNull(floor);
        return version is not null && Normalize(version) >= Normalize(floor);
    }

    // Version compares an unset build or revision (-1) as lower than 0; a manifest's "1.7.2" means 1.7.2.0.
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
}
