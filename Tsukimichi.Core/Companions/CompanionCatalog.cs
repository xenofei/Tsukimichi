namespace Tsukimichi.Core.Companions;

/// <summary>
/// The other plugins Tsukimichi works with (feature plan v5, decisions 1–3): the nine it hands questing work to or
/// reads from, then the ones those need to do the work (Boss Mod and a rotation for AutoDuty's fights and Questionable's
/// combat, TextAdvance for Questionable's dialogue). Tsukimichi automates only when the player presses a button that
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

    // What those plugins need themselves: listed in Settings as "needed by" rows.

    /// <summary>Veyn's Boss Mod or Boss Mod Reborn: AutoDuty's boss fights, Questionable's combat when chosen there.</summary>
    BossMod,

    /// <summary>Wrath Combo or Rotation Solver Reborn: AutoDuty's rotation (Boss Mod's own autorotation also serves).</summary>
    RotationPlugin,

    /// <summary>TextAdvance: Questionable's dialogue, quest accept and turn-in (its manifest's "Required Plugins").</summary>
    TextAdvance,
}

/// <summary>
/// One build of a companion plugin as Dalamud's installed list names it. A plugin with forks or successors has several
/// (GatherBuddy and GatherBuddy Reborn); the first is the one Settings recommends.
/// </summary>
/// <param name="InternalName">The manifest's <c>InternalName</c>, matched without regard to case.</param>
/// <param name="DisplayName">The name players know it by ("GatherBuddy Reborn").</param>
/// <param name="MinimumVersion">
/// The oldest build that has every gate Tsukimichi calls, in the numbering Dalamud reports (<paramref name="KnownBuild"/>);
/// null when every build on the current Dalamud API has them (Dalamud itself flags a build for an older API as outdated).
/// </param>
/// <param name="RepositoryUrl">The custom repository URL players add in /xlsettings › Experimental; null for a plugin in Dalamud's official repository.</param>
/// <param name="KnownBuild">
/// A released build's version exactly as Dalamud's installed list reports it (checked 2026-10-02 against the plugin's
/// releases, its repository's plugin list or its official-repository manifest). It records the plugin's numbering, so a
/// test can hold <paramref name="MinimumVersion"/> to the same scheme: Allagan Tools' changelog says 15.0.12 where Dalamud
/// reports 1.15.0.12, and a minimum written the changelog's way turned every build away.
/// </param>
public sealed record CompanionVariant(string InternalName, string DisplayName, Version? MinimumVersion, string? RepositoryUrl, Version KnownBuild)
{
    /// <summary>The plugin is in Dalamud's official repository: no custom repository needed.</summary>
    public bool IsOfficial => RepositoryUrl is null;
}

/// <summary>A companion plugin: its builds in order of preference, whether Settings lists it, and who needs it.</summary>
/// <param name="Plugin">Which companion.</param>
/// <param name="Variants">Its builds; the first is the recommended one, whose name and repository Settings shows.</param>
/// <param name="Listed">Shown in Settings › Integrations › Companion plugins.</param>
/// <param name="NeededBy">The companions that need this one to do their work (TextAdvance for Questionable); null for one Tsukimichi calls itself.</param>
/// <param name="ForAutomation">
/// Part of "full automation": the overall setup line counts it when it is not loaded. False for the plugins Tsukimichi
/// only reads from (Allagan Tools, Quest Map, Chat 2) and for the rotation plugin, which Boss Mod's autorotation stands in for.
/// </param>
public sealed record CompanionDefinition(
    CompanionPlugin Plugin,
    IReadOnlyList<CompanionVariant> Variants,
    bool Listed = true,
    IReadOnlyList<CompanionPlugin>? NeededBy = null,
    bool ForAutomation = true)
{
    /// <summary>The recommended build.</summary>
    public CompanionVariant Primary => Variants[0];

    /// <summary>The recommended build's name ("Lifestream", "Allagan Tools").</summary>
    public string DisplayName => Primary.DisplayName;

    /// <summary>The companions that need this one; empty for one Tsukimichi calls itself.</summary>
    public IReadOnlyList<CompanionPlugin> Dependents => NeededBy ?? [];
}

/// <summary>
/// What Tsukimichi knows about each companion plugin. Repository URLs are the ones each project's README gives (checked
/// 2026-10-01): Questionable, Artisan and Wrath Combo in Puni.sh's <c>love.puni.sh/ment.json</c>, AutoDuty in erdelf's
/// and vnavmesh and Boss Mod in veyn's Puni.sh repositories, Lifestream and TextAdvance in NightmareXIV's, GatherBuddy
/// Reborn, Boss Mod Reborn and Rotation Solver Reborn in Combat Reborn's; GatherBuddy, Allagan Tools
/// (<c>InventoryTools</c>), Quest Map and Chat 2 are in Dalamud's official repository (goatcorp/DalamudPluginsD17
/// <c>stable/</c>).
/// <para>
/// Version numbering, as Dalamud reports it (each variant's <see cref="CompanionVariant.KnownBuild"/>): AutoDuty
/// <c>0.0.0.N</c> (its release tag; the csproj says 0.0.0.0); Allagan Tools <c>1.15.0.N</c> (its changelog and tags
/// drop the leading 1); Quest Map <c>&lt;API&gt;.&lt;patch&gt;.N.0</c> since API 14 (15.755.2.0; 1.x before);
/// Questionable <c>15.756.3.N</c>; Lifestream, TextAdvance, vnavmesh, Artisan, GatherBuddy, Chat 2 and Wrath Combo plain
/// <c>a.b.c.d</c> from their csproj or tag; Boss Mod, Boss Mod Reborn, GatherBuddy Reborn and Rotation Solver Reborn
/// <c>7.5.6.N</c> from their tags.
/// </para>
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
    /// 2026-08-23, e61d002), which "Run with AutoDuty" sets before it runs. AutoDuty numbers its releases 0.0.0.N.
    /// </summary>
    public static readonly Version AutoDutyMinimum = new(0, 0, 0, 336);

    /// <summary>
    /// Allagan Tools 1.15.0.12 (2026-08-31; its changelog calls it 15.0.12, and Dalamud's official repository published
    /// it as 1.15.0.12 from InventoryTools 84af185, goatcorp/DalamudPluginsD17 b0e378b) added
    /// <c>AllaganTools.ItemCountOwnedByCategory</c> and <c>GetItemCountsByCharacter</c>, which the Hand in section's
    /// retainer counts and Moonlit's relic ownership read.
    /// </summary>
    public static readonly Version AllaganToolsMinimum = new(1, 15, 0, 12);

    /// <summary>Every companion, in the order Settings shows them: a plugin's "needed by" rows follow the plugin.</summary>
    public static readonly IReadOnlyList<CompanionDefinition> All =
    [
        new(CompanionPlugin.Lifestream, [new("Lifestream", "Lifestream", null, NightmareXiv, new(2, 5, 4, 23))]),
        new(CompanionPlugin.Vnavmesh, [new("vnavmesh", "vnavmesh", null, PuniShVeyn, new(1, 2, 3, 14))]),
        new(CompanionPlugin.Questionable, [new("Questionable", "Questionable", null, PuniShMain, new(15, 756, 3, 26))]),
        new(CompanionPlugin.TextAdvance, [new("TextAdvance", "TextAdvance", null, NightmareXiv, new(3, 3, 0, 1))], NeededBy: [CompanionPlugin.Questionable]),
        new(CompanionPlugin.AutoDuty, [new("AutoDuty", "AutoDuty", AutoDutyMinimum, PuniShErdelf, new(0, 0, 0, 375))]),
        new(
            CompanionPlugin.BossMod,
            [new("BossMod", "Boss Mod", null, PuniShVeyn, new(7, 5, 6, 9)), new("BossModReborn", "Boss Mod Reborn", null, CombatReborn, new(7, 5, 6, 27))],
            NeededBy: [CompanionPlugin.AutoDuty, CompanionPlugin.Questionable]),
        new(
            CompanionPlugin.RotationPlugin,
            [new("WrathCombo", "Wrath Combo", null, PuniShMain, new(1, 0, 4, 26)), new("RotationSolver", "Rotation Solver Reborn", null, CombatReborn, new(7, 5, 6, 13))],
            NeededBy: [CompanionPlugin.AutoDuty, CompanionPlugin.Questionable],
            ForAutomation: false),
        new(CompanionPlugin.Artisan, [new("Artisan", "Artisan", null, PuniShMain, new(4, 0, 5, 21))]),
        new(
            CompanionPlugin.GatherBuddy,
            [new("GatherBuddy", "GatherBuddy", null, null, new(3, 8, 11, 1)), new("GatherBuddyReborn", "GatherBuddy Reborn", null, CombatReborn, new(7, 5, 6, 1))]),
        new(CompanionPlugin.AllaganTools, [new("InventoryTools", "Allagan Tools", AllaganToolsMinimum, null, new(1, 15, 0, 13))], ForAutomation: false),

        // Quest Map's gates came in 1.7.2.2 (GemPlugins/QuestMap b2ce55e, 2025-06-05); since API 14 it numbers its builds
        // <API>.<patch>.N.0, so every build for this API has them and no minimum is needed.
        new(CompanionPlugin.QuestMap, [new("QuestMap", "Quest Map", null, null, new(15, 755, 2, 0))], ForAutomation: false),
        new(CompanionPlugin.ChatTwo, [new("ChatTwo", "Chat 2", null, null, new(1, 40, 9, 0))], ForAutomation: false),
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
