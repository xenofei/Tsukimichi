namespace Tsukimichi.Core.Companions;

/// <summary>
/// The other plugins Tsukimichi works with (feature plan v5, decisions 1–3): the nine it hands questing work to or
/// reads from, then the ones AutoDuty itself needs. Tsukimichi automates only when the player presses a button that
/// hands the work to one of these; without the plugin the button stays visible, disabled, and names it.
/// </summary>
public enum CompanionPlugin
{
    Questionable,
    AutoDuty,
    Artisan,
    GatherBuddy,
    Vnavmesh,
    Lifestream,
    AllaganTools,
    QuestMap,
    ChatTwo,

    // AutoDuty's own requirements (its README's "Required Plugins"): not listed in Settings, named when missing.

    /// <summary>Veyn's Boss Mod or Boss Mod Reborn: AutoDuty's boss fights.</summary>
    BossMod,

    /// <summary>Wrath Combo or Rotation Solver Reborn: AutoDuty's rotation (Boss Mod's own autorotation also serves).</summary>
    RotationPlugin,
}

/// <summary>
/// One build of a companion plugin as Dalamud's installed list names it. A plugin with forks or successors has several
/// (GatherBuddy and GatherBuddy Reborn); the first is the one Settings recommends.
/// </summary>
/// <param name="InternalName">The manifest's <c>InternalName</c>, matched without regard to case.</param>
/// <param name="DisplayName">The name players know it by ("GatherBuddy Reborn").</param>
/// <param name="MinimumVersion">
/// The oldest build that has every gate Tsukimichi calls; null when every build on the current Dalamud API has them
/// (Dalamud itself flags a build for an older API as outdated).
/// </param>
/// <param name="RepositoryUrl">The custom repository URL players add in /xlsettings › Experimental; null for a plugin in Dalamud's official repository.</param>
public sealed record CompanionVariant(string InternalName, string DisplayName, Version? MinimumVersion, string? RepositoryUrl)
{
    /// <summary>The plugin is in Dalamud's official repository: no custom repository needed.</summary>
    public bool IsOfficial => RepositoryUrl is null;
}

/// <summary>A companion plugin: its builds in order of preference and whether Settings lists it.</summary>
/// <param name="Plugin">Which companion.</param>
/// <param name="Variants">Its builds; the first is the recommended one, whose name and repository Settings shows.</param>
/// <param name="Listed">Shown in Settings › Integrations › Companion plugins (false for AutoDuty's own requirements).</param>
public sealed record CompanionDefinition(CompanionPlugin Plugin, IReadOnlyList<CompanionVariant> Variants, bool Listed = true)
{
    /// <summary>The recommended build.</summary>
    public CompanionVariant Primary => Variants[0];

    /// <summary>The recommended build's name ("Lifestream", "Allagan Tools").</summary>
    public string DisplayName => Primary.DisplayName;
}

/// <summary>
/// What Tsukimichi knows about each companion plugin. Repository URLs are the ones each project's README gives (checked
/// 2026-10-01): Questionable and Artisan in Puni.sh's <c>love.puni.sh/ment.json</c>, AutoDuty in erdelf's and vnavmesh
/// and Boss Mod in veyn's Puni.sh repositories, Lifestream in NightmareXIV's, GatherBuddy Reborn, Boss Mod Reborn and
/// Rotation Solver Reborn in Combat Reborn's; GatherBuddy, Allagan Tools (<c>InventoryTools</c>), Quest Map and Chat 2
/// are in Dalamud's official repository (goatcorp/DalamudPluginsD17 <c>stable/</c>).
/// </summary>
public static class CompanionCatalog
{
    public const string PuniShMain = "https://love.puni.sh/ment.json";
    public const string PuniShErdelf = "https://puni.sh/api/repository/erdelf";
    public const string PuniShVeyn = "https://puni.sh/api/repository/veyn";
    public const string NightmareXiv = "https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json";
    public const string CombatReborn = "https://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json";

    /// <summary>
    /// AutoDuty 0.0.0.336 (2026-09-13) is the first build with both <c>AutoDuty.PushConfigOverrides</c> (added
    /// 2026-07-25, erdelf/AutoDuty 5dad32f) and the duty mode at <c>Meta.DutyModeEnum</c> (the config revamp of
    /// 2026-08-23, e61d002), which "Run with AutoDuty" sets before it runs.
    /// </summary>
    public static readonly Version AutoDutyMinimum = new(0, 0, 0, 336);

    /// <summary>Quest Map 1.7.2.2 (GemPlugins/QuestMap b2ce55e, 2025-06-05) added <c>QuestMap.ShowGraphByQuestId</c> and <c>ShowInfoByQuestId</c>.</summary>
    public static readonly Version QuestMapMinimum = new(1, 7, 2, 2);

    /// <summary>Every companion, listed ones first in the order Settings shows them.</summary>
    public static readonly IReadOnlyList<CompanionDefinition> All =
    [
        new(CompanionPlugin.Lifestream, [new("Lifestream", "Lifestream", null, NightmareXiv)]),
        new(CompanionPlugin.Vnavmesh, [new("vnavmesh", "vnavmesh", null, PuniShVeyn)]),
        new(CompanionPlugin.Questionable, [new("Questionable", "Questionable", null, PuniShMain)]),
        new(CompanionPlugin.AutoDuty, [new("AutoDuty", "AutoDuty", AutoDutyMinimum, PuniShErdelf)]),
        new(CompanionPlugin.Artisan, [new("Artisan", "Artisan", null, PuniShMain)]),
        new(CompanionPlugin.GatherBuddy, [new("GatherBuddy", "GatherBuddy", null, null), new("GatherBuddyReborn", "GatherBuddy Reborn", null, CombatReborn)]),
        new(CompanionPlugin.AllaganTools, [new("InventoryTools", "Allagan Tools", null, null)]),
        new(CompanionPlugin.QuestMap, [new("QuestMap", "Quest Map", QuestMapMinimum, null)]),
        new(CompanionPlugin.ChatTwo, [new("ChatTwo", "Chat 2", null, null)]),
        new(CompanionPlugin.BossMod, [new("BossMod", "Boss Mod", null, PuniShVeyn), new("BossModReborn", "Boss Mod Reborn", null, CombatReborn)], Listed: false),
        new(CompanionPlugin.RotationPlugin, [new("WrathCombo", "Wrath Combo", null, PuniShMain), new("RotationSolver", "Rotation Solver Reborn", null, CombatReborn)], Listed: false),
    ];

    /// <summary>The definition of one companion.</summary>
    public static CompanionDefinition Get(CompanionPlugin plugin)
    {
        foreach (var definition in All)
        {
            if (definition.Plugin == plugin)
            {
                return definition;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(plugin), plugin, "Unknown companion plugin");
    }
}
