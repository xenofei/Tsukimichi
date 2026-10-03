namespace Tsukimichi.Core.Companions;

/// <summary>
/// The companion plugins' settings that matter to Tsukimichi's hand-offs, with the value Tsukimichi recommends
/// (companion setup). Each was read from the plugin's own source; the commit is named per plugin below. A setting is
/// listed only when a wrong value makes a hand-off stall, need the player, or do more than the quest asked; a matter of
/// taste is left alone. Tsukimichi sets a setting only through the plugin's own IPC and only after the player confirms
/// the list of changes; every other plugin's settings are read, never written (its file is opened read only).
/// <para>
/// Questionable (PunishXIV/Questionable <c>new-main</c> e7bb011, 2026-10-02): <c>pluginConfigs/Questionable.json</c>,
/// written by Newtonsoft with enums as numbers. It has no IPC for its settings, so they are listed with a check mark and
/// changed by the player in <c>/qst config</c>. Its IPC start (<c>StartQuest</c>) checks none of them.
/// </para>
/// <para>
/// TextAdvance (NightmareXIV/TextAdvance 9dee627): its getter gates (<c>TextAdvance.GetEnableQuestAccept</c> …,
/// <c>TextAdvance.IsPaused</c>), which answer the value in force. While Questionable runs with "Automatically configure
/// TextAdvance" on it takes TextAdvance over (<c>EnableExternalControl</c>) and turns quest accept and complete on
/// itself, so TextAdvance's own switches matter only when that Questionable setting is off (<see cref="SetupRequirement.CoveredBy"/>).
/// </para>
/// <para>
/// AutoDuty (erdelf/AutoDuty 39b9a87, release 0.0.0.375): <c>AutoDuty.GetConfig(string) -&gt; string</c> reads the
/// active profile (its file, <c>pluginConfigs/AutoDuty/AutoDutyConfigV2.json</c>, holds every profile and cannot tell
/// which is active), <c>AutoDuty.SetConfig(string, object)</c> sets and saves. Keys are dotted paths, bools are
/// "True"/"False". "Run with AutoDuty" already runs in bare mode, which turns off pre-loop, between-loop and
/// termination actions (repair, equip, extract, desynth, Grand Company turn-in, stop conditions, logout), so none of
/// those is listed; the run's queue, loop count and mode are pushed as temporary overrides per run.
/// </para>
/// <para>
/// Boss Mod, Boss Mod Reborn, Wrath Combo and Rotation Solver Reborn need nothing set by the player for AutoDuty, which
/// manages them itself (presets, autorotation lease, operating mode). Lifestream, vnavmesh, Artisan, GatherBuddy,
/// Allagan Tools, Quest Map and Chat 2: see their entries.
/// </para>
/// </summary>
public static class CompanionSetupCatalog
{
    private static readonly CompanionPlugin[] QuestionableHandOff = [CompanionPlugin.Questionable];
    private static readonly CompanionPlugin[] AutoDutyHandOff = [CompanionPlugin.AutoDuty];
    private static readonly string[] True = ["true"];
    private static readonly string[] False = ["false"];

    /// <summary>Every recommended setting, grouped by plugin in <see cref="CompanionCatalog.All"/> order.</summary>
    public static readonly IReadOnlyList<SetupRequirement> All =
    [
        // ---- Questionable: read from its file, changed in /qst config ----

        // The one-time setup window ("Finish Setup", enabled once vnavmesh, Lifestream, TextAdvance and the chosen
        // combat plugin are loaded). Until then /qst and Questionable's window refuse; its IPC still works.
        new("questionable.setup", CompanionPlugin.Questionable, QuestionableHandOff, SetupSource.ConfigFile,
            "PluginSetupCompleteVersion", SetupMatch.NoneOf, ["0"], "0", null, SetupImpact.Recommended),

        // "Preferred Combat Module": None (0) leaves every fight of a quest to the player (CombatController).
        new("questionable.combat-module", CompanionPlugin.Questionable, QuestionableHandOff, SetupSource.ConfigFile,
            "General.CombatModule", SetupMatch.NoneOf, ["0", "None"], "0", null, SetupImpact.Recommended),

        // "Automatically configure TextAdvance…": Questionable then turns TextAdvance's quest accept and complete on
        // itself while it runs (External/TextAdvanceIpc.cs).
        new("questionable.configure-textadvance", CompanionPlugin.Questionable, QuestionableHandOff, SetupSource.ConfigFile,
            "General.ConfigureTextAdvance", SetupMatch.AnyOf, True, "true", null, SetupImpact.Recommended),

        // "…but don't skip cutscenes or dialogue": Questionable then stops answering dialogue choices
        // (GameUi/DialogueChoiceHandler.cs), so a run waits at every prompt.
        new("questionable.dont-skip-cutscenes", CompanionPlugin.Questionable, QuestionableHandOff, SetupSource.ConfigFile,
            "General.DontSkipCutscenes", SetupMatch.AnyOf, False, "false", null, SetupImpact.Recommended),

        // "Prevent quest completion" (Advanced): quests are never turned in (Steps/Interactions/Interact.cs).
        new("questionable.prevent-completion", CompanionPlugin.Questionable, QuestionableHandOff, SetupSource.ConfigFile,
            "Advanced.PreventQuestCompletion", SetupMatch.AnyOf, False, "false", null, SetupImpact.Blocking),

        // "Stop Questionable when any of the conditions below are met": a run sent from Tsukimichi can stop midway.
        new("questionable.stop-conditions", CompanionPlugin.Questionable, QuestionableHandOff, SetupSource.ConfigFile,
            "Stop.Enabled", SetupMatch.AnyOf, False, "false", null, SetupImpact.Recommended),

        // "Run command when Questionable finishes automatic questing": Tsukimichi's Stop runs it too (default /li auto).
        new("questionable.command-after-stop", CompanionPlugin.Questionable, QuestionableHandOff, SetupSource.ConfigFile,
            "Stop.RunCommandAfterStop", SetupMatch.AnyOf, False, "false", null, SetupImpact.Recommended),

        // "Run instanced content with AutoDuty and BossMod": off, a quest's dungeon only opens the Duty Finder.
        new("questionable.autoduty", CompanionPlugin.Questionable, QuestionableHandOff, SetupSource.ConfigFile,
            "Duties.RunInstancedContentWithAutoDuty", SetupMatch.AnyOf, True, "false", null, SetupImpact.Recommended),

        // "Run quest battles with BossMod": off, a solo quest battle waits for the player.
        new("questionable.solo-duties", CompanionPlugin.Questionable, QuestionableHandOff, SetupSource.ConfigFile,
            "SinglePlayerDuties.RunSoloInstancesWithBossMod", SetupMatch.AnyOf, True, "false", null, SetupImpact.Recommended),

        // ---- TextAdvance: its getter gates, the value in force ----

        // "Automatic quest accept (QA)": without it Questionable stalls at every quest offer.
        new("textadvance.quest-accept", CompanionPlugin.TextAdvance, QuestionableHandOff, SetupSource.Ipc,
            "GetEnableQuestAccept", SetupMatch.AnyOf, True, null, null, SetupImpact.Blocking,
            CoveredBy: "questionable.configure-textadvance"),

        // "Automatic quest complete (QC)": without it Questionable stalls at every turn-in.
        new("textadvance.quest-complete", CompanionPlugin.TextAdvance, QuestionableHandOff, SetupSource.Ipc,
            "GetEnableQuestComplete", SetupMatch.AnyOf, True, null, null, SetupImpact.Blocking,
            CoveredBy: "questionable.configure-textadvance"),

        // "Automatic talk skip (TS)": without it every conversation waits for a click.
        new("textadvance.talk-skip", CompanionPlugin.TextAdvance, QuestionableHandOff, SetupSource.Ipc,
            "GetEnableTalkSkip", SetupMatch.AnyOf, True, null, null, SetupImpact.Recommended,
            CoveredBy: "questionable.configure-textadvance"),

        // "Enable plugin (non-persistent)": TextAdvance's own on switch, which it turns off at logout unless the
        // character is on its auto-enable list. Questionable's control turns it on while it runs.
        new("textadvance.enabled", CompanionPlugin.TextAdvance, QuestionableHandOff, SetupSource.Ipc,
            "IsEnabled", SetupMatch.AnyOf, True, null, null, SetupImpact.Recommended,
            CoveredBy: "questionable.configure-textadvance"),

        // Another plugin paused TextAdvance (its red "stopped by these plugins" banner): even Questionable's control waits.
        new("textadvance.not-paused", CompanionPlugin.TextAdvance, QuestionableHandOff, SetupSource.Ipc,
            "IsPaused", SetupMatch.AnyOf, False, null, null, SetupImpact.Recommended),

        // ---- AutoDuty: read with GetConfig, set with SetConfig (saved to the active profile) ----

        // "Auto Manage Rotation Plugin State": off, AutoDuty never turns Wrath Combo, Rotation Solver Reborn or Boss
        // Mod's autorotation on, so the character does not attack. AutoDuty turns it off itself when Wrath Combo
        // refuses its lease.
        new("autoduty.manage-rotation", CompanionPlugin.AutoDuty, AutoDutyHandOff, SetupSource.Ipc,
            "DutyConfig.AutoManageRotationPluginState", SetupMatch.AnyOf, True, null, "True", SetupImpact.Recommended),

        // "Using Alternative Rotation Plugin": on, AutoDuty skips rotation management entirely.
        new("autoduty.alternative-rotation", CompanionPlugin.AutoDuty, AutoDutyHandOff, SetupSource.Ipc,
            "DutyConfig.UsingAlternativeRotationPlugin", SetupMatch.AnyOf, False, null, "False", SetupImpact.Recommended),

        // "Auto setup jobs for autorotation": off, a job Wrath Combo has not set up gets no rotation from it.
        new("autoduty.wrath-setup-jobs", CompanionPlugin.AutoDuty, AutoDutyHandOff, SetupSource.Ipc,
            "DutyConfig.Wrath.AutoSetupJobs", SetupMatch.AnyOf, True, null, "True", SetupImpact.Recommended),

        // "Auto Manage BossMod AI Settings": off, no "AutoDuty Passive" preset: the character neither dodges nor follows.
        new("autoduty.manage-bossmod", CompanionPlugin.AutoDuty, AutoDutyHandOff, SetupSource.Ipc,
            "DutyConfig.AutoManageBossModAISettings", SetupMatch.AnyOf, True, null, "True", SetupImpact.Recommended),

        // "Auto Leave Duty in last loop": off, the run ends inside the duty.
        new("autoduty.leave-duty", CompanionPlugin.AutoDuty, AutoDutyHandOff, SetupSource.Ipc,
            "DutyConfig.AutoExitDuty", SetupMatch.AnyOf, True, null, "True", SetupImpact.Recommended),

        // "Block leaving duty until it's complete": on, a path that ends before the duty is done never leaves.
        new("autoduty.block-leaving", CompanionPlugin.AutoDuty, AutoDutyHandOff, SetupSource.Ipc,
            "DutyConfig.OnlyExitWhenDutyDone", SetupMatch.AnyOf, False, null, "False", SetupImpact.Recommended),

        // "Unsynced": on, a Duty Finder run queues unsynced and takes the path's unsynced steps.
        new("autoduty.unsynced", CompanionPlugin.AutoDuty, AutoDutyHandOff, SetupSource.Ipc,
            "Meta.Unsynced", SetupMatch.AnyOf, False, null, "False", SetupImpact.Recommended),

        // ---- Lifestream (NightmareXIV/Lifestream ef759e9): pluginConfigs/Lifestream/DefaultConfig.json, no settings IPC ----

        // "Add firmament location into Foundation aetheryte": off, AethernetTeleportToFirmament answers true and then
        // fails with "No destination Firmament found", so Go to giver's hop to the Firmament goes nowhere.
        new("lifestream.firmament", CompanionPlugin.Lifestream, [CompanionPlugin.Lifestream], SetupSource.ConfigFile,
            "Firmament", SetupMatch.AnyOf, True, "true", null, SetupImpact.Recommended, File: LifestreamFile),

        // "Allow custom alias and house alias to override built-in commands": an alias named "island" or "occult"
        // would then replace the /li island and /li occult that Teleport runs for those givers.
        new("lifestream.custom-overrides", CompanionPlugin.Lifestream, [CompanionPlugin.Lifestream], SetupSource.ConfigFile,
            "AllowCustomOverrides", SetupMatch.AnyOf, False, "false", null, SetupImpact.Recommended, File: LifestreamFile),

        // ---- vnavmesh (awgil/ffxiv_navmesh 6fc8072): Nav.IsAutoLoad / Nav.SetAutoLoad ----

        // "Automatically load/build navigation data when changing zones": off, the navmesh never loads after a zone
        // change and Walk to giver waits at "Preparing path…".
        new("vnavmesh.auto-load", CompanionPlugin.Vnavmesh, [CompanionPlugin.Vnavmesh], SetupSource.Ipc,
            "Nav.IsAutoLoad", SetupMatch.AnyOf, True, null, "true", SetupImpact.Blocking, ApplyKey: "Nav.SetAutoLoad"),

        // ---- Artisan (PunishXIV/Artisan 247c2df): pluginConfigs/Artisan.json; its IPC has no getter for these ----

        // Endurance (what CraftItem runs) switches to the crafter's saved gear set and aborts without one.
        new("artisan.gearsets", CompanionPlugin.Artisan, [CompanionPlugin.Artisan], SetupSource.Manual,
            null, SetupMatch.AnyOf, [], null, null, SetupImpact.Recommended),

        // "Exit Crafting Stance After Completion": off, the character stays seated at the crafting log after the
        // craft and cannot walk to the turn-in.
        new("artisan.exit-stance", CompanionPlugin.Artisan, [CompanionPlugin.Artisan], SetupSource.ConfigFile,
            "ExitCraftStanceEndurance", SetupMatch.AnyOf, True, "true", null, SetupImpact.Recommended),

        // "Disable Endurance Mode Upon Crafting an NQ item" / "…Upon Failed Craft": either stops short of the amount asked.
        new("artisan.stop-nq", CompanionPlugin.Artisan, [CompanionPlugin.Artisan], SetupSource.ConfigFile,
            "EnduranceStopNQ", SetupMatch.AnyOf, False, "false", null, SetupImpact.Recommended),
        new("artisan.stop-fail", CompanionPlugin.Artisan, [CompanionPlugin.Artisan], SetupSource.ConfigFile,
            "EnduranceStopFail", SetupMatch.AnyOf, False, "false", null, SetupImpact.Recommended),

        // ---- GatherBuddy (Ottermandias/GatherBuddy 1e39592) and GatherBuddy Reborn (a204f2f): <InternalName>.json ----

        // "Enable Teleport": off, /gather only marks the node and the player travels there alone.
        new("gatherbuddy.teleport", CompanionPlugin.GatherBuddy, [CompanionPlugin.GatherBuddy], SetupSource.ConfigFile,
            "UseTeleport", SetupMatch.AnyOf, True, "true", null, SetupImpact.Recommended),

        // GatherBuddy (not Reborn, which picks the set by job) changes gear with /gearset change "<name>": the names in
        // its "Set Names" must match the player's gear sets, or the change fails with a chat error.
        new("gatherbuddy.set-names", CompanionPlugin.GatherBuddy, [CompanionPlugin.GatherBuddy], SetupSource.Manual,
            null, SetupMatch.AnyOf, [], null, null, SetupImpact.Recommended, Variant: "GatherBuddy"),

        // ---- Allagan Tools (Critical-Impact/InventoryTools 70a9f41) ----

        // Retainers, saddlebags, the armoire and the glamour dresser count only once opened (CriticalCommonLib's
        // InventoryScanner); nothing can read whether they have been.
        new("allagantools.open-once", CompanionPlugin.AllaganTools, [CompanionPlugin.AllaganTools], SetupSource.Manual,
            null, SetupMatch.AnyOf, [], null, null, SetupImpact.Recommended),
    ];

    private const string LifestreamFile = "Lifestream/DefaultConfig.json";

    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>One plugin's recommended settings, in catalog order.</summary>
    public static IReadOnlyList<SetupRequirement> For(CompanionPlugin plugin) => All.Where(r => r.Plugin == plugin).ToList();

    /// <summary>One requirement by id; null when none has it.</summary>
    public static SetupRequirement? Get(string id) => All.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// Whether a change to <paramref name="relativePath"/>, under Dalamud's <c>pluginConfigs</c> folder, may change what
    /// the Setup list reads: a companion's own file (<c>Questionable.json</c>, <c>WigglyQuest.json</c>) or anything in
    /// its folder (<c>Lifestream/DefaultConfig.json</c>, <c>AutoDuty/AutoDutyConfigV2.json</c>). The plugins read
    /// through their IPC save there too, so their changes count. Tsukimichi's own saves and other plugins' do not.
    /// </summary>
    public static bool Concerns(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var path = relativePath.TrimStart('/', '\\');
        var slash = path.IndexOfAny(PathSeparators);
        var first = slash >= 0 ? path[..slash] : path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? path[..^5] : string.Empty;
        if (first.Length == 0)
        {
            return false;
        }

        foreach (var definition in CompanionCatalog.All)
        {
            foreach (var variant in definition.Variants)
            {
                if (string.Equals(first, variant.InternalName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
