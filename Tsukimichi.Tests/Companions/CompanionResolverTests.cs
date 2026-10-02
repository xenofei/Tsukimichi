using Tsukimichi.Core.Companions;

namespace Tsukimichi.Tests.Companions;

/// <summary>
/// The companion plugin registry's rules (<see cref="CompanionResolver"/>, feature plan v5 decision 1): loaded,
/// installed but turned off, outdated (below the minimum, or built for an older Dalamud API) and missing; several
/// builds of one plugin; and the catalog itself (the nine questing companions, their internal names and repositories).
/// </summary>
public class CompanionResolverTests
{
    private static readonly CompanionDefinition AutoDuty = CompanionCatalog.Get(CompanionPlugin.AutoDuty);
    private static readonly CompanionDefinition GatherBuddy = CompanionCatalog.Get(CompanionPlugin.GatherBuddy);
    private static readonly CompanionDefinition Lifestream = CompanionCatalog.Get(CompanionPlugin.Lifestream);

    private static CompanionStatus Resolve(CompanionDefinition definition, params InstalledPlugin[] installed) =>
        CompanionResolver.Resolve(definition, installed);

    [Fact]
    public void Nothing_installed_is_missing_under_the_recommended_name()
    {
        var status = Resolve(GatherBuddy);
        Assert.Equal(CompanionState.Missing, status.State);
        Assert.Equal("GatherBuddy", status.DisplayName);
        Assert.Null(status.InstalledVersion);
        Assert.False(status.IsLoaded);
    }

    [Fact]
    public void Another_plugin_does_not_count()
    {
        Assert.Equal(CompanionState.Missing, Resolve(Lifestream, new InstalledPlugin("LifestreamPlus", new Version(9, 0), true)).State);
    }

    [Fact]
    public void A_loaded_plugin_is_loaded_whatever_the_case_of_its_internal_name()
    {
        var status = Resolve(Lifestream, new InstalledPlugin("lifestream", new Version(2, 5, 4, 23), true));
        Assert.Equal(CompanionState.Loaded, status.State);
        Assert.Equal(new Version(2, 5, 4, 23), status.InstalledVersion);
    }

    [Fact]
    public void An_installed_plugin_that_is_not_loaded_is_disabled()
    {
        Assert.Equal(CompanionState.Disabled, Resolve(Lifestream, new InstalledPlugin("Lifestream", new Version(2, 5), false)).State);
    }

    [Fact]
    public void A_build_for_an_older_Dalamud_API_is_outdated_not_disabled()
    {
        Assert.Equal(CompanionState.Outdated, Resolve(Lifestream, new InstalledPlugin("Lifestream", new Version(2, 4), false, IsOutdated: true)).State);
    }

    [Theory]
    [InlineData(0, 0, 0, 335, true, CompanionState.Outdated)]
    [InlineData(0, 0, 0, 336, true, CompanionState.Loaded)]
    [InlineData(0, 0, 0, 375, true, CompanionState.Loaded)]
    [InlineData(0, 0, 0, 300, false, CompanionState.Outdated)]
    [InlineData(0, 0, 0, 375, false, CompanionState.Disabled)]
    public void AutoDuty_below_its_minimum_is_outdated(int major, int minor, int build, int revision, bool loaded, CompanionState expected)
    {
        var status = Resolve(AutoDuty, new InstalledPlugin("AutoDuty", new Version(major, minor, build, revision), loaded));
        Assert.Equal(expected, status.State);
        Assert.Equal(CompanionCatalog.AutoDutyMinimum, status.MinimumVersion);
    }

    /// <summary>The reported bug: Allagan Tools 1.15.0.13, the newest build, read "Outdated, needs 15.0.12".</summary>
    [Theory]
    [InlineData(1, 15, 0, 13, CompanionState.Loaded)]
    [InlineData(1, 15, 0, 12, CompanionState.Loaded)]
    [InlineData(1, 15, 0, 11, CompanionState.Outdated)]
    public void Allagan_Tools_is_compared_in_the_numbering_Dalamud_reports(int major, int minor, int build, int revision, CompanionState expected)
    {
        var status = Resolve(CompanionCatalog.Get(CompanionPlugin.AllaganTools), new InstalledPlugin("InventoryTools", new Version(major, minor, build, revision), true));
        Assert.Equal(expected, status.State);
    }

    /// <summary>
    /// Every minimum is written in its plugin's own numbering: four parts, the same leading part as a released build
    /// Dalamud reports (Allagan Tools 1.x, AutoDuty 0.x), and not above that build, so no released build is turned away.
    /// </summary>
    [Fact]
    public void Every_minimum_is_in_the_numbering_of_a_released_build()
    {
        foreach (var variant in CompanionCatalog.All.SelectMany(static d => d.Variants))
        {
            var known = variant.KnownBuild;
            Assert.True(known.Revision >= 0, $"{variant.DisplayName}: the known build {known} should have four parts, as Dalamud reports");
            if (variant.MinimumVersion is not { } minimum)
            {
                continue;
            }

            Assert.True(minimum.Revision >= 0, $"{variant.DisplayName}: minimum {minimum} should have four parts");
            Assert.True(minimum.Major == known.Major, $"{variant.DisplayName}: minimum {minimum} is not in the numbering of {known}");
            Assert.True(minimum.Major != 0 || minimum.Minor == known.Minor, $"{variant.DisplayName}: minimum {minimum} is not in the numbering of {known}");
            Assert.False(CompanionResolver.IsBelow(known, minimum), $"{variant.DisplayName}: the released build {known} would read as outdated against {minimum}");
            Assert.Equal(CompanionState.Loaded, CompanionResolver.Resolve(
                CompanionCatalog.All.Single(d => d.Variants.Contains(variant)),
                [new InstalledPlugin(variant.InternalName, known, true)]).State);
        }
    }

    [Fact]
    public void A_dev_build_without_a_version_is_not_turned_away()
    {
        Assert.Equal(CompanionState.Loaded, Resolve(AutoDuty, new InstalledPlugin("AutoDuty", new Version(0, 0, 0, 0), true)).State);
        Assert.Equal(CompanionState.Loaded, Resolve(AutoDuty, new InstalledPlugin("AutoDuty", null, true)).State);
    }

    [Fact]
    public void Versions_compare_with_unset_parts_as_zero()
    {
        Assert.False(CompanionResolver.IsBelow(new Version(1, 7, 2, 2), new Version(1, 7, 2, 2)));
        Assert.True(CompanionResolver.IsBelow(new Version(1, 7, 2), new Version(1, 7, 2, 2)));
        Assert.False(CompanionResolver.IsBelow(new Version(15, 755, 2, 0), new Version(1, 7, 2, 2)));
        Assert.Null(CompanionCatalog.Get(CompanionPlugin.QuestMap).Primary.MinimumVersion);
        Assert.False(CompanionResolver.IsBelow(new Version(1, 0), null));
    }

    [Fact]
    public void A_loaded_successor_beats_a_turned_off_original()
    {
        var status = Resolve(
            GatherBuddy,
            new InstalledPlugin("GatherBuddy", new Version(3, 8), false),
            new InstalledPlugin("GatherBuddyReborn", new Version(7, 5, 6, 1), true));
        Assert.Equal(CompanionState.Loaded, status.State);
        Assert.Equal("GatherBuddy Reborn", status.DisplayName);
        Assert.Equal(CompanionCatalog.CombatReborn, status.Variant.RepositoryUrl);
    }

    [Fact]
    public void With_both_loaded_the_recommended_build_is_named()
    {
        var status = Resolve(
            GatherBuddy,
            new InstalledPlugin("GatherBuddyReborn", new Version(7, 5), true),
            new InstalledPlugin("GatherBuddy", new Version(3, 8), true));
        Assert.Equal("GatherBuddy", status.DisplayName);
    }

    [Fact]
    public void The_catalog_lists_every_companion_once_with_needed_by_rows_after_the_plugin_that_needs_them()
    {
        var listed = CompanionCatalog.All.Where(static d => d.Listed).Select(static d => d.Plugin).ToList();
        Assert.Equal(
            [
                CompanionPlugin.Lifestream, CompanionPlugin.Vnavmesh, CompanionPlugin.Questionable, CompanionPlugin.TextAdvance,
                CompanionPlugin.AutoDuty, CompanionPlugin.BossMod, CompanionPlugin.RotationPlugin, CompanionPlugin.Artisan,
                CompanionPlugin.GatherBuddy, CompanionPlugin.AllaganTools, CompanionPlugin.QuestMap, CompanionPlugin.ChatTwo,
            ],
            listed);
        Assert.Equal([CompanionPlugin.Questionable], CompanionCatalog.Get(CompanionPlugin.TextAdvance).Dependents);
        Assert.Contains(CompanionPlugin.AutoDuty, CompanionCatalog.Get(CompanionPlugin.BossMod).Dependents);
        Assert.Empty(CompanionCatalog.Get(CompanionPlugin.Lifestream).Dependents);
        Assert.All(Enum.GetValues<CompanionPlugin>(), static p => Assert.Equal(p, CompanionCatalog.Get(p).Plugin));
        var names = CompanionCatalog.All.SelectMany(static d => d.Variants).Select(static v => v.InternalName.ToUpperInvariant()).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Theory]
    [InlineData(CompanionPlugin.AllaganTools, "InventoryTools")]
    [InlineData(CompanionPlugin.QuestMap, "QuestMap")]
    [InlineData(CompanionPlugin.ChatTwo, "ChatTwo")]
    [InlineData(CompanionPlugin.Vnavmesh, "vnavmesh")]
    [InlineData(CompanionPlugin.Lifestream, "Lifestream")]
    [InlineData(CompanionPlugin.AutoDuty, "AutoDuty")]
    [InlineData(CompanionPlugin.TextAdvance, "TextAdvance")]
    public void Internal_names_are_the_manifests(CompanionPlugin plugin, string internalName)
    {
        Assert.Equal(internalName, CompanionCatalog.Get(plugin).Primary.InternalName);
    }

    [Fact]
    public void Official_plugins_have_no_custom_repository_and_the_rest_one_over_https()
    {
        foreach (var definition in CompanionCatalog.All)
        {
            foreach (var variant in definition.Variants)
            {
                var official = variant.InternalName is "GatherBuddy" or "InventoryTools" or "QuestMap" or "ChatTwo";
                Assert.Equal(official, variant.IsOfficial);
                if (!official)
                {
                    Assert.StartsWith("https://", variant.RepositoryUrl, StringComparison.Ordinal);
                }
            }
        }
    }

    [Fact]
    public void Resolve_all_keeps_the_catalog_order()
    {
        var all = CompanionResolver.ResolveAll([new InstalledPlugin("QuestMap", new Version(15, 755, 2, 0), true)]);
        Assert.Equal(CompanionCatalog.All.Count, all.Count);
        Assert.Equal(CompanionCatalog.All.Select(static d => d.Plugin), all.Select(static s => s.Plugin));
        Assert.Equal(CompanionState.Loaded, all.Single(static s => s.Plugin == CompanionPlugin.QuestMap).State);
        Assert.All(all.Where(static s => s.Plugin != CompanionPlugin.QuestMap), static s => Assert.Equal(CompanionState.Missing, s.State));
    }
}
