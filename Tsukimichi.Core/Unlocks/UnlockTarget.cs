using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;

namespace Tsukimichi.Core.Unlocks;

/// <summary>
/// The section an unlock row is drawn under (feature plan v6 K2), in display order: Areas, Aetherytes, Duties, Features
/// &amp; systems, Actions &amp; emotes, Items &amp; collectables, Next quests.
/// </summary>
public enum UnlockGroup : byte
{
    Area,
    Aetheryte,
    Duty,
    Feature,
    ActionEmote,
    Collectable,
    NextQuest,
}

/// <summary>
/// What one unlock row is (feature plan v6 K1). The declaration order is the order inside a group: a dungeon before a
/// trial, a zone before a world-map region.
/// </summary>
public enum UnlockTarget : byte
{
    /// <summary>A town or field zone, a housing district, the Gold Saucer or the Firmament (TerritoryType row id).</summary>
    Zone,

    /// <summary>A world-map region the map reveals (PlaceName row id): The First, Norvrandt, Ilsabard.</summary>
    WorldMap,

    /// <summary>A teleportable aetheryte (Aetheryte row id).</summary>
    Aetheryte,

    /// <summary>An aethernet shard or an invisible aethernet gate (Aetheryte row id).</summary>
    AethernetShard,

    Dungeon,
    Trial,
    NormalRaid,
    AllianceRaid,
    FieldOperation,

    /// <summary>Any other duty: a guildhest, a deep dungeon, PvP, a Gold Saucer attraction, a quest battle.</summary>
    OtherDuty,

    /// <summary>A class or job (ClassJob row id).</summary>
    Job,

    /// <summary>Flying in a zone (the zone's name; an aether current) or the curated flying unlock.</summary>
    Flying,

    /// <summary>A game system or feature: retainers, the Gold Saucer, hunts, Wondrous Tails, a scrip exchange.</summary>
    System,

    Action,
    Trait,
    Emote,
    GeneralAction,
    BlueMageSpell,

    Mount,
    Minion,
    Orchestrion,
    Card,
    Hairstyle,
    Barding,
    Ornament,
    Title,

    /// <summary>A quest this one is a prerequisite of (Quest row id).</summary>
    NextQuest,
}

/// <summary>
/// How sure an unlock row is, from most to least trusted for dedupe (<see cref="UnlockTargets.Trust"/>): curated with
/// evidence, stated by the game's sheets, or inferred by the first-visit rule ("Likely: you first reach it here").
/// </summary>
public enum UnlockSource : byte
{
    /// <summary>The game's own data says so: a sheet column, a quest reward.</summary>
    Sheet,

    /// <summary>A curated file says so, with evidence.</summary>
    Curated,

    /// <summary>Inferred: the quest's objectives are the first of the character's main scenario to reach it.</summary>
    Derived,
}

/// <summary>Groups, names and mappings of <see cref="UnlockTarget"/>.</summary>
public static class UnlockTargets
{
    /// <summary>Every group, in display order.</summary>
    public static readonly UnlockGroup[] Groups =
    [
        UnlockGroup.Area, UnlockGroup.Aetheryte, UnlockGroup.Duty, UnlockGroup.Feature, UnlockGroup.ActionEmote,
        UnlockGroup.Collectable, UnlockGroup.NextQuest,
    ];

    /// <summary>The group a target is drawn under.</summary>
    public static UnlockGroup GroupOf(UnlockTarget target) => target switch
    {
        UnlockTarget.Zone or UnlockTarget.WorldMap => UnlockGroup.Area,
        UnlockTarget.Aetheryte or UnlockTarget.AethernetShard => UnlockGroup.Aetheryte,
        >= UnlockTarget.Dungeon and <= UnlockTarget.OtherDuty => UnlockGroup.Duty,
        >= UnlockTarget.Job and <= UnlockTarget.System => UnlockGroup.Feature,
        >= UnlockTarget.Action and <= UnlockTarget.BlueMageSpell => UnlockGroup.ActionEmote,
        >= UnlockTarget.Mount and <= UnlockTarget.Title => UnlockGroup.Collectable,
        _ => UnlockGroup.NextQuest,
    };

    /// <summary>A group's bit in <see cref="QuestUnlocks.GroupMask"/>.</summary>
    public static ushort Bit(UnlockGroup group) => (ushort)(1 << (int)group);

    /// <summary>Higher is more trusted: Curated, then Sheet, then Derived. The most trusted row of a target wins.</summary>
    public static int Trust(UnlockSource source) => source switch
    {
        UnlockSource.Curated => 2,
        UnlockSource.Sheet => 1,
        _ => 0,
    };

    /// <summary>The duty row's target for a duty's plan kind.</summary>
    public static UnlockTarget FromDutyKind(UnlockKind kind) => kind switch
    {
        UnlockKind.Dungeon => UnlockTarget.Dungeon,
        UnlockKind.Trial => UnlockTarget.Trial,
        UnlockKind.NormalRaid => UnlockTarget.NormalRaid,
        UnlockKind.AllianceRaid => UnlockTarget.AllianceRaid,
        UnlockKind.FieldOperation => UnlockTarget.FieldOperation,
        _ => UnlockTarget.OtherDuty,
    };

    /// <summary>The target of an action, emote or collectable reward kind; null for a kind that is no such row.</summary>
    public static UnlockTarget? FromReward(RewardKind kind) => kind switch
    {
        RewardKind.Action => UnlockTarget.Action,
        RewardKind.Trait => UnlockTarget.Trait,
        RewardKind.Emote => UnlockTarget.Emote,
        RewardKind.GeneralAction => UnlockTarget.GeneralAction,
        RewardKind.BlueMageSpell => UnlockTarget.BlueMageSpell,
        RewardKind.Mount => UnlockTarget.Mount,
        RewardKind.Minion => UnlockTarget.Minion,
        RewardKind.Orchestrion => UnlockTarget.Orchestrion,
        RewardKind.TripleTriadCard => UnlockTarget.Card,
        RewardKind.Hairstyle => UnlockTarget.Hairstyle,
        RewardKind.Barding => UnlockTarget.Barding,
        RewardKind.Ornament => UnlockTarget.Ornament,
        RewardKind.Title => UnlockTarget.Title,
        _ => null,
    };

    /// <summary>The word a row's caption opens with: "Area", "Dungeon", "Emote", ….</summary>
    public static string Name(UnlockTarget target) => target switch
    {
        UnlockTarget.Zone => CoreText.T("Core.UnlockTarget.Zone", "Area"),
        UnlockTarget.WorldMap => CoreText.T("Core.UnlockTarget.WorldMap", "World map"),
        UnlockTarget.Aetheryte => CoreText.T("Core.UnlockTarget.Aetheryte", "Aetheryte"),
        UnlockTarget.AethernetShard => CoreText.T("Core.UnlockTarget.AethernetShard", "Aethernet"),
        UnlockTarget.Dungeon => CoreText.T("Core.Unlock.Dungeon", "Dungeon"),
        UnlockTarget.Trial => CoreText.T("Core.Unlock.Trial", "Trial"),
        UnlockTarget.NormalRaid => CoreText.T("Core.Unlock.NormalRaid", "Raid"),
        UnlockTarget.AllianceRaid => CoreText.T("Core.Unlock.AllianceRaid", "Alliance raid"),
        UnlockTarget.FieldOperation => CoreText.T("Core.Unlock.FieldOperation", "Field operation"),
        UnlockTarget.OtherDuty => CoreText.T("Core.UnlockTarget.OtherDuty", "Duty"),
        UnlockTarget.Job => CoreText.T("Core.Unlock.Job", "Job"),
        UnlockTarget.Flying => CoreText.T("Core.Unlock.Flying", "Flying"),
        UnlockTarget.System => CoreText.T("Core.UnlockTarget.System", "Feature"),
        UnlockTarget.Action => CoreText.T("Core.UnlockTarget.Action", "Action"),
        UnlockTarget.Trait => CoreText.T("Core.UnlockTarget.Trait", "Trait"),
        UnlockTarget.Emote => CoreText.T("Core.UnlockTarget.Emote", "Emote"),
        UnlockTarget.GeneralAction => CoreText.T("Core.UnlockTarget.GeneralAction", "General action"),
        UnlockTarget.BlueMageSpell => CoreText.T("Core.UnlockTarget.BlueMageSpell", "Blue magic"),
        UnlockTarget.Mount => CoreText.T("Core.UnlockTarget.Mount", "Mount"),
        UnlockTarget.Minion => CoreText.T("Core.UnlockTarget.Minion", "Minion"),
        UnlockTarget.Orchestrion => CoreText.T("Core.UnlockTarget.Orchestrion", "Orchestrion roll"),
        UnlockTarget.Card => CoreText.T("Core.UnlockTarget.Card", "Triple Triad card"),
        UnlockTarget.Hairstyle => CoreText.T("Core.UnlockTarget.Hairstyle", "Hairstyle"),
        UnlockTarget.Barding => CoreText.T("Core.UnlockTarget.Barding", "Barding"),
        UnlockTarget.Ornament => CoreText.T("Core.UnlockTarget.Ornament", "Fashion accessory"),
        UnlockTarget.Title => CoreText.T("Core.UnlockTarget.Title", "Title"),
        _ => CoreText.T("Core.UnlockTarget.NextQuest", "Quest"),
    };

    /// <summary>A group's caption: "Areas", "Aetherytes", "Duties", "Features", "Actions & emotes", "Items", "Next quests".</summary>
    public static string GroupName(UnlockGroup group) => group switch
    {
        UnlockGroup.Area => CoreText.T("Core.UnlockGroup.Area", "Areas"),
        UnlockGroup.Aetheryte => CoreText.T("Core.UnlockGroup.Aetheryte", "Aetherytes"),
        UnlockGroup.Duty => CoreText.T("Core.UnlockGroup.Duty", "Duties"),
        UnlockGroup.Feature => CoreText.T("Core.UnlockGroup.Feature", "Features & systems"),
        UnlockGroup.ActionEmote => CoreText.T("Core.UnlockGroup.ActionEmote", "Actions & emotes"),
        UnlockGroup.Collectable => CoreText.T("Core.UnlockGroup.Collectable", "Items"),
        _ => CoreText.T("Core.UnlockGroup.NextQuest", "Next quests"),
    };
}
