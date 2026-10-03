using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The sheet columns <see cref="PaneIcons"/> reads (Lumina-backed in <c>Tsukimichi.GameData.PaneIconSheets</c>, read
/// once off the frame), so a patch that adds a society or redraws a tile needs no code change. Every method returns 0
/// for "none".
/// </summary>
public interface IPaneIconSheets
{
    /// <summary><c>ContentType.Icon</c>: the Duty Finder and Timers category tiles.</summary>
    uint ContentTypeIcon(uint contentType);

    /// <summary><c>BeastTribe.Icon</c>: the society's coloured emblem, as the game's Allied Societies window shows it.</summary>
    uint TribeIcon(byte tribe);

    /// <summary><c>BeastTribe.IconReputation</c>: the society's plain mark (the Journal tree's).</summary>
    uint TribeReputationIcon(byte tribe);

    /// <summary><c>GrandCompanyRank.IconMaelstrom</c>, <c>IconSerpents</c> or <c>IconFlames</c>: the rank's insignia in that company.</summary>
    uint GrandCompanyRankIcon(byte grandCompany, byte rank);

    /// <summary><c>Achievement.Icon</c>.</summary>
    uint AchievementIcon(uint achievement);

    /// <summary><c>Mount.Icon</c> (UI-5e, a mount requirement's line).</summary>
    uint MountIcon(uint mount);
}

/// <summary>Which kind of job a row or a group stands for, for its icon (<see cref="PaneIcons.Family"/>).</summary>
public enum JobFamily : byte
{
    /// <summary>No icon (a quest any job may take).</summary>
    None,

    Tank,
    Healer,
    Melee,
    PhysicalRanged,
    MagicalRanged,

    /// <summary>Disciples of the Hand (crafters).</summary>
    Hand,

    /// <summary>Disciples of the Land (gatherers).</summary>
    Land,

    /// <summary>Several disciplines at once (DoW/DoM, DoH/DoL, a mixed list of jobs).</summary>
    Mixed,
}

/// <summary>
/// The game icons of UI-5d (owner point 9, "add any icons that are missing"): the role ladders and job groups of the
/// Characters dashboard, the Journal table's job groups, the Grand Company rank, the allied societies, the achievements
/// that need several quests, the Plan tab's unlock kinds and its expansions. Only small, English-independent hand
/// lists live here, keyed by sheet ids (the role icons, the ContentType tile per kind); everything else is read from
/// the sheets through <see cref="IPaneIconSheets"/>. The game-data tests check every icon exists in the game.
/// </summary>
public static class PaneIcons
{
    /// <summary>The framed role icons (062581 tank … 062587 magical ranged), the set the job icons (062101…) share a frame with.</summary>
    public const uint TankRoleIcon = 62581;
    public const uint HealerRoleIcon = 62582;
    public const uint MeleeRoleIcon = 62584;
    public const uint PhysicalRangedRoleIcon = 62586;
    public const uint MagicalRangedRoleIcon = 62587;

    /// <summary>ContentType rows of the Duty Finder tiles the plan's duty kinds wear.</summary>
    public const uint DungeonsContent = 2;
    public const uint TrialsContent = 4;
    public const uint RaidsContent = 5;
    public const uint EurekaContent = 26;

    /// <summary>The role's framed icon; 0 for none.</summary>
    public static uint Role(JobRole role) => role switch
    {
        JobRole.Tank => TankRoleIcon,
        JobRole.Healer => HealerRoleIcon,
        JobRole.Melee => MeleeRoleIcon,
        JobRole.PhysicalRanged => PhysicalRangedRoleIcon,
        JobRole.MagicalRanged => MagicalRangedRoleIcon,
        _ => 0,
    };

    /// <summary>The family a role belongs to.</summary>
    public static JobFamily FamilyOf(JobRole role) => role switch
    {
        JobRole.Tank => JobFamily.Tank,
        JobRole.Healer => JobFamily.Healer,
        JobRole.Melee => JobFamily.Melee,
        JobRole.PhysicalRanged => JobFamily.PhysicalRanged,
        JobRole.MagicalRanged => JobFamily.MagicalRanged,
        _ => JobFamily.None,
    };

    /// <summary>
    /// The family of the jobs a quest's ClassJobCategory admits when it admits more than one: crafters only the Disciples
    /// of the Hand, gatherers only the Land, any mix the Class &amp; Job emblem; nothing admitted, none.
    /// </summary>
    public static JobFamily Disciples(bool hand, bool land, bool combat) => (hand, land, combat) switch
    {
        (false, false, false) => JobFamily.None,
        (true, false, false) => JobFamily.Hand,
        (false, true, false) => JobFamily.Land,
        _ => JobFamily.Mixed,
    };

    /// <summary>
    /// The family's icon: a role's framed icon, the Duty Finder tile of the Disciples of the Hand or of the Land (as the
    /// Journal tree's class categories wear), or the framed Class &amp; Job emblem for a mix. 0 for none.
    /// </summary>
    public static uint Family(JobFamily family, IPaneIconSheets sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        return family switch
        {
            JobFamily.Tank => TankRoleIcon,
            JobFamily.Healer => HealerRoleIcon,
            JobFamily.Melee => MeleeRoleIcon,
            JobFamily.PhysicalRanged => PhysicalRangedRoleIcon,
            JobFamily.MagicalRanged => MagicalRangedRoleIcon,
            JobFamily.Hand => sheets.ContentTypeIcon(NodeIcons.HandContent),
            JobFamily.Land => sheets.ContentTypeIcon(NodeIcons.LandContent),
            JobFamily.Mixed => NodeIcons.ClassJobEmblem,
            _ => 0,
        };
    }

    /// <summary>An allied society's emblem: the coloured one, else its plain mark; 0 for none.</summary>
    public static uint Tribe(byte tribe, IPaneIconSheets sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        if (tribe == 0)
        {
            return 0;
        }

        var emblem = sheets.TribeIcon(tribe);
        return emblem != 0 ? emblem : sheets.TribeReputationIcon(tribe);
    }

    /// <summary>
    /// The insignia of <paramref name="rank"/> in <paramref name="grandCompany"/> (1 Maelstrom, 2 Twin Adder, 3 Immortal
    /// Flames); the Grand Company tile while the rank has none; 0 without a company.
    /// </summary>
    public static uint GrandCompanyRank(byte grandCompany, byte rank, IPaneIconSheets sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        if (grandCompany is 0 or > 3)
        {
            return 0;
        }

        var insignia = rank == 0 ? 0u : sheets.GrandCompanyRankIcon(grandCompany, rank);
        return insignia != 0 ? insignia : sheets.ContentTypeIcon(NodeIcons.GrandCompanyContent);
    }

    /// <summary>
    /// The icon of an unlock kind in the plan (I14): the Duty Finder tile for duty kinds (a normal raid and an alliance raid
    /// share the Raids tile, as in the Duty Finder; field operations wear Eureka's), the Class &amp; Job emblem, the
    /// Society Quests tile, the aether current, the feature quest marker for a game system. 0 for Other.
    /// </summary>
    public static uint UnlockKind(UnlockKind kind, IPaneIconSheets sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        return kind switch
        {
            Plan.UnlockKind.Dungeon => sheets.ContentTypeIcon(DungeonsContent),
            Plan.UnlockKind.Trial => sheets.ContentTypeIcon(TrialsContent),
            Plan.UnlockKind.NormalRaid or Plan.UnlockKind.AllianceRaid => sheets.ContentTypeIcon(RaidsContent),
            Plan.UnlockKind.FieldOperation => sheets.ContentTypeIcon(EurekaContent),
            Plan.UnlockKind.Job => NodeIcons.ClassJobEmblem,
            Plan.UnlockKind.Society => sheets.ContentTypeIcon(NodeIcons.SocietyContent),
            Plan.UnlockKind.Flying => QuestUnlocks.AetherCurrentIcon,
            Plan.UnlockKind.System => NodeIcons.FeatureMarker,
            _ => 0,
        };
    }

    /// <summary>An expansion's ring (061875 A Realm Reborn …), as the Journal tree and the Flight tab wear; 0 for an expansion the table does not know.</summary>
    public static uint Expansion(byte expansion) =>
        expansion < Evaluation.Expansions.Count ? NodeIcons.FirstExpansionRingIcon + expansion : 0u;

    /// <summary>Every icon id <see cref="PaneIcons"/> answers without a sheet, for the game-data test.</summary>
    public static IReadOnlyList<uint> FixedIcons { get; } =
    [
        TankRoleIcon, HealerRoleIcon, MeleeRoleIcon, PhysicalRangedRoleIcon, MagicalRangedRoleIcon,
        NodeIcons.ClassJobEmblem, NodeIcons.FeatureMarker, QuestUnlocks.AetherCurrentIcon,
    ];

    /// <summary>Every ContentType row <see cref="PaneIcons"/> reads, for the game-data test.</summary>
    public static IReadOnlyList<uint> ContentTypeRows { get; } =
    [
        NodeIcons.HandContent, NodeIcons.LandContent, NodeIcons.GrandCompanyContent, NodeIcons.SocietyContent,
        DungeonsContent, TrialsContent, RaidsContent, EurekaContent,
    ];
}
